using System.Diagnostics;
using System.IO;
using TrimKit.Models;

namespace TrimKit.Services;

/// <summary>
/// Handles ISO mounting via PowerShell's Mount-DiskImage (built into Windows)
/// and extracting install.wim/install.esd from mounted ISOs.
/// </summary>
public class IsoService : IIsoService
{
    private readonly ILogService _logService;

    public IsoService(ILogService logService)
    {
        _logService = logService;
    }

    public async Task<string> MountIsoAsync(string isoPath)
    {
        _logService.Log(LogLevel.Info, $"Mounting ISO: {Path.GetFileName(isoPath)}");

        // Use PowerShell to mount the ISO and get the drive letter
        var script = $@"
            $result = Mount-DiskImage -ImagePath '{isoPath.Replace("'", "''")}' -PassThru
            $volume = $result | Get-Volume
            $volume.DriveLetter
        ";

        var output = await RunPowerShellAsync(script);
        var driveLetter = output.Trim();

        if (string.IsNullOrWhiteSpace(driveLetter) || driveLetter.Length != 1)
        {
            throw new InvalidOperationException($"Failed to mount ISO. Output: {output}");
        }

        var mountPath = $"{driveLetter}:\\";
        _logService.Log(LogLevel.Success, $"ISO mounted at: {mountPath}");
        return mountPath;
    }

    public async Task UnmountIsoAsync(string isoPath)
    {
        _logService.Log(LogLevel.Info, $"Unmounting ISO: {Path.GetFileName(isoPath)}");

        var script = $"Dismount-DiskImage -ImagePath '{isoPath.Replace("'", "''")}'";
        await RunPowerShellAsync(script);

        _logService.Log(LogLevel.Success, "ISO unmounted");
    }

    public Task<string?> FindInstallImageAsync(string isoMountPath)
    {
        // Look for install.wim or install.esd in the sources folder
        var sourcesDir = Path.Combine(isoMountPath, "sources");

        if (!Directory.Exists(sourcesDir))
        {
            _logService.Log(LogLevel.Warning, "No 'sources' directory found in ISO");
            return Task.FromResult<string?>(null);
        }

        // Prefer install.wim over install.esd
        var wimPath = Path.Combine(sourcesDir, "install.wim");
        if (File.Exists(wimPath))
        {
            _logService.Log(LogLevel.Info, "Found install.wim");
            return Task.FromResult<string?>(wimPath);
        }

        var esdPath = Path.Combine(sourcesDir, "install.esd");
        if (File.Exists(esdPath))
        {
            _logService.Log(LogLevel.Info, "Found install.esd");
            return Task.FromResult<string?>(esdPath);
        }

        _logService.Log(LogLevel.Warning, "No install.wim or install.esd found in sources directory");
        return Task.FromResult<string?>(null);
    }

    public async Task ExtractWimFromIsoAsync(string isoPath, string outputDir, IProgress<(int percent, string status)>? progress = null)
    {
        progress?.Report((5, "Mounting ISO..."));
        var mountPath = await MountIsoAsync(isoPath);

        try
        {
            progress?.Report((20, "Locating install image..."));
            var imagePath = await FindInstallImageAsync(mountPath);

            if (imagePath == null)
            {
                throw new FileNotFoundException("Could not find install.wim or install.esd in the ISO");
            }

            Directory.CreateDirectory(outputDir);
            var extension = Path.GetExtension(imagePath);
            var destPath = Path.Combine(outputDir, $"install{extension}");

            progress?.Report((30, $"Copying {Path.GetFileName(imagePath)} ({new FileInfo(imagePath).Length / (1024.0 * 1024.0 * 1024.0):F2} GB)..."));

            // Copy with progress
            await using var source = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            await using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var totalBytes = source.Length;
            var buffer = new byte[81920];
            long bytesRead = 0;
            int read;

            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read));
                bytesRead += read;

                var pct = 30 + (int)(bytesRead / (double)totalBytes * 60);
                progress?.Report((pct, $"Copying: {bytesRead / (1024.0 * 1024.0):F0} / {totalBytes / (1024.0 * 1024.0):F0} MB"));
            }

            // If it's an ESD, convert to WIM for easier editing
            if (extension.Equals(".esd", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report((92, "Converting ESD to WIM..."));
                var wimDest = Path.Combine(outputDir, "install.wim");
                await ConvertEsdToWimAsync(destPath, wimDest);
                File.Delete(destPath);
                _logService.Log(LogLevel.Success, $"Converted ESD to WIM: {wimDest}");
            }

            progress?.Report((100, "Extraction complete!"));
            _logService.Log(LogLevel.Success, $"Install image extracted to: {outputDir}");
        }
        finally
        {
            await UnmountIsoAsync(isoPath);
        }
    }

    private async Task ConvertEsdToWimAsync(string esdPath, string wimPath)
    {
        // Use DISM to export ESD to WIM
        // First get the number of images (skip index 1 which is usually metadata)
        var psi = new ProcessStartInfo
        {
            FileName = "dism.exe",
            Arguments = $"/Get-WimInfo /WimFile:\"{esdPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var infoProcess = new Process { StartInfo = psi };
        infoProcess.Start();
        var infoOutput = await infoProcess.StandardOutput.ReadToEndAsync();
        await infoProcess.WaitForExitAsync();

        // Count indexes (skipping index 1 if it's metadata)
        var indexCount = infoOutput.Split("Index :").Length - 1;
        var startIndex = indexCount > 1 ? 2 : 1; // Skip metadata index if present

        for (int i = startIndex; i <= indexCount; i++)
        {
            var exportPsi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = $"/Export-Image /SourceImageFile:\"{esdPath}\" /SourceIndex:{i} /DestinationImageFile:\"{wimPath}\" /Compress:Max",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var exportProcess = new Process { StartInfo = exportPsi };
            exportProcess.Start();
            await exportProcess.WaitForExitAsync();

            if (exportProcess.ExitCode != 0)
            {
                var error = await exportProcess.StandardError.ReadToEndAsync();
                _logService.Log(LogLevel.Warning, $"ESD index {i} export issue: {error.Trim()}");
            }
        }
    }

    private static async Task<string> RunPowerShellAsync(string script)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException($"PowerShell error: {error.Trim()}");
        }

        return output;
    }
}
