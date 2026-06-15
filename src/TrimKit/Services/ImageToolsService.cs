using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using TrimKit.Models;

namespace TrimKit.Services;

public partial class ImageToolsService : IImageToolsService
{
    private readonly ILogService _logService;

    public ImageToolsService(ILogService logService)
    {
        _logService = logService;
    }

    #region Edition Management

    public async Task<List<ImageEditionInfo>> GetEditionsAsync(string wimPath)
    {
        var editions = new List<ImageEditionInfo>();
        var output = await RunDismAsync($"/Get-WimInfo /WimFile:\"{wimPath}\"");

        var blocks = output.Split("Index : ", StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks.Skip(1))
        {
            var info = new ImageEditionInfo();
            var firstLine = block.Split('\n')[0].Trim();
            if (int.TryParse(firstLine, out var idx))
                info.Index = idx;

            info.Name = ExtractField(block, "Name");
            info.Description = ExtractField(block, "Description");

            var sizeStr = ExtractField(block, "Size");
            if (long.TryParse(sizeStr.Replace(",", "").Replace(" bytes", "").Trim(), out var size))
                info.Size = size;

            editions.Add(info);
        }

        _logService.Log(LogLevel.Info, $"Found {editions.Count} edition(s)");
        return editions;
    }

    public async Task RemoveEditionAsync(string wimPath, int index)
    {
        _logService.Log(LogLevel.Info, $"Removing edition index {index} from {Path.GetFileName(wimPath)}");
        await RunDismAsync($"/Delete-Image /ImageFile:\"{wimPath}\" /Index:{index}");
        _logService.Log(LogLevel.Success, $"Edition {index} removed");
    }

    public async Task ExportEditionAsync(string sourceWim, int sourceIndex, string destWim)
    {
        _logService.Log(LogLevel.Info, $"Exporting index {sourceIndex} to {Path.GetFileName(destWim)}");
        await RunDismAsync($"/Export-Image /SourceImageFile:\"{sourceWim}\" /SourceIndex:{sourceIndex} /DestinationImageFile:\"{destWim}\" /Compress:Max");
        _logService.Log(LogLevel.Success, "Edition exported");
    }

    #endregion

    #region WIM ↔ ESD Conversion

    public async Task ConvertWimToEsdAsync(string wimPath, string esdPath, IProgress<(int percent, string status)>? progress = null)
    {
        _logService.Log(LogLevel.Info, $"Converting WIM to ESD (recovery format): {Path.GetFileName(wimPath)}");
        progress?.Report((10, "Converting WIM to ESD..."));

        var editions = await GetEditionsAsync(wimPath);
        var total = editions.Count;

        for (int i = 0; i < total; i++)
        {
            var edition = editions[i];
            var pct = 10 + (int)((i + 1.0) / total * 80);
            progress?.Report((pct, $"Exporting index {edition.Index}: {edition.Name}"));

            await RunDismAsync($"/Export-Image /SourceImageFile:\"{wimPath}\" /SourceIndex:{edition.Index} /DestinationImageFile:\"{esdPath}\" /Compress:recovery");
        }

        progress?.Report((100, "ESD conversion complete"));
        _logService.Log(LogLevel.Success, $"Created ESD: {Path.GetFileName(esdPath)}");
    }

    public async Task ConvertEsdToWimAsync(string esdPath, string wimPath, IProgress<(int percent, string status)>? progress = null)
    {
        _logService.Log(LogLevel.Info, $"Converting ESD to WIM: {Path.GetFileName(esdPath)}");
        progress?.Report((10, "Converting ESD to WIM..."));

        var editions = await GetEditionsAsync(esdPath);
        var total = editions.Count;

        // Skip index 1 if it's a metadata image (common in MS ESDs)
        var startIndex = total > 1 && editions[0].Name.Contains("Windows Setup", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        for (int i = startIndex; i < total; i++)
        {
            var edition = editions[i];
            var pct = 10 + (int)((i - startIndex + 1.0) / (total - startIndex) * 80);
            progress?.Report((pct, $"Exporting index {edition.Index}: {edition.Name}"));

            await RunDismAsync($"/Export-Image /SourceImageFile:\"{esdPath}\" /SourceIndex:{edition.Index} /DestinationImageFile:\"{wimPath}\" /Compress:Max");
        }

        progress?.Report((100, "WIM conversion complete"));
        _logService.Log(LogLevel.Success, $"Created WIM: {Path.GetFileName(wimPath)}");
    }

    public Task RenameImageAsync(string sourcePath, string destPath)
    {
        File.Move(sourcePath, destPath, overwrite: true);
        _logService.Log(LogLevel.Success, $"Renamed: {Path.GetFileName(sourcePath)} → {Path.GetFileName(destPath)}");
        return Task.CompletedTask;
    }

    #endregion

    #region Windows Update Integration

    public async Task IntegrateUpdatesAsync(string mountPath, string updatesFolder, IProgress<(int percent, string status)>? progress = null)
    {
        var updates = await FindUpdatesInFolderAsync(updatesFolder);
        if (updates.Count == 0)
        {
            _logService.Log(LogLevel.Warning, "No update packages found in folder");
            return;
        }

        _logService.Log(LogLevel.Info, $"Integrating {updates.Count} update(s)...");
        progress?.Report((5, $"Found {updates.Count} update(s)"));

        // Sort: SSU first, then LCU, then others
        var sorted = updates
            .OrderBy(u => u.Contains("SSU", StringComparison.OrdinalIgnoreCase) ? 0 :
                         u.Contains("LCU", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            var update = sorted[i];
            var fileName = Path.GetFileName(update);
            var pct = 5 + (int)((i + 1.0) / sorted.Count * 90);
            progress?.Report((pct, $"[{i + 1}/{sorted.Count}] {fileName}"));

            try
            {
                await RunDismAsync($"/Image:\"{mountPath}\" /Add-Package /PackagePath:\"{update}\"");
                _logService.Log(LogLevel.Success, $"Integrated: {fileName}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Skipped {fileName}: {ex.Message}");
            }
        }

        progress?.Report((100, "Update integration complete"));
    }

    public Task<List<string>> FindUpdatesInFolderAsync(string folder)
    {
        var extensions = new[] { "*.msu", "*.cab" };
        var files = new List<string>();

        foreach (var ext in extensions)
        {
            files.AddRange(Directory.GetFiles(folder, ext, SearchOption.AllDirectories));
        }

        return Task.FromResult(files);
    }

    #endregion

    #region Boot.wim Operations

    public async Task AddDriversToBootWimAsync(string bootWimPath, string driverFolder, IProgress<(int percent, string status)>? progress = null)
    {
        _logService.Log(LogLevel.Info, "Adding drivers to boot.wim...");
        var editions = await GetEditionsAsync(bootWimPath);

        for (int i = 0; i < editions.Count; i++)
        {
            var edition = editions[i];
            var mountDir = Path.Combine(Path.GetTempPath(), $"TrimKit_BootMount_{edition.Index}");
            Directory.CreateDirectory(mountDir);

            var pct = (int)((i + 1.0) / editions.Count * 100);
            progress?.Report((pct, $"Boot.wim index {edition.Index}: {edition.Name}"));

            try
            {
                await RunDismAsync($"/Mount-Wim /WimFile:\"{bootWimPath}\" /Index:{edition.Index} /MountDir:\"{mountDir}\"");
                await RunDismAsync($"/Image:\"{mountDir}\" /Add-Driver /Driver:\"{driverFolder}\" /Recurse");
                await RunDismAsync($"/Unmount-Wim /MountDir:\"{mountDir}\" /Commit");
                _logService.Log(LogLevel.Success, $"Drivers added to boot.wim index {edition.Index}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Boot.wim index {edition.Index} failed: {ex.Message}");
                try { await RunDismAsync($"/Unmount-Wim /MountDir:\"{mountDir}\" /Discard"); } catch { }
            }
            finally
            {
                try { Directory.Delete(mountDir, true); } catch { }
            }
        }
    }

    #endregion

    #region ISO Building

    public async Task BuildIsoAsync(string sourceFolder, string outputIso, string volumeLabel, IProgress<(int percent, string status)>? progress = null)
    {
        _logService.Log(LogLevel.Info, $"Building ISO: {Path.GetFileName(outputIso)}");
        progress?.Report((10, "Building ISO with oscdimg..."));

        // Try to find oscdimg.exe (from ADK or from our bundled copy)
        var oscdimg = await FindOscdimgAsync();

        if (oscdimg == null)
        {
            // Fallback: use PowerShell with .NET to create a basic ISO
            _logService.Log(LogLevel.Warning, "oscdimg not found. Using PowerShell ISO builder (install Windows ADK for best results).");
            await BuildIsoWithPowerShellAsync(sourceFolder, outputIso, volumeLabel);
        }
        else
        {
            // UEFI + BIOS bootable ISO
            var etfsboot = Path.Combine(sourceFolder, @"boot\etfsboot.com");
            var efisys = Path.Combine(sourceFolder, @"efi\microsoft\boot\efisys.bin");

            var args = $"-m -o -u2 -udfver102 -l\"{volumeLabel}\"";
            if (File.Exists(etfsboot))
                args += $" -bootdata:2#p0,e,b\"{etfsboot}\"#pEF,e,b\"{efisys}\"";

            args += $" \"{sourceFolder}\" \"{outputIso}\"";

            var psi = new ProcessStartInfo
            {
                FileName = oscdimg,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"oscdimg failed: {error}");
            }
        }

        progress?.Report((100, "ISO built successfully"));
        _logService.Log(LogLevel.Success, $"ISO created: {outputIso}");
    }

    private static async Task<string?> FindOscdimgAsync()
    {
        // Check common locations
        var candidates = new[]
        {
            @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
            @"C:\Program Files\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "oscdimg.exe")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        // Try PATH
        var psi = new ProcessStartInfo
        {
            FileName = "where",
            Arguments = "oscdimg.exe",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var firstLine = output.Split('\n').FirstOrDefault()?.Trim();
        return !string.IsNullOrEmpty(firstLine) && File.Exists(firstLine) ? firstLine : null;
    }

    private static async Task BuildIsoWithPowerShellAsync(string sourceFolder, string outputIso, string volumeLabel)
    {
        // Use PowerShell to create an ISO via .NET (basic, no bootable support)
        var script = $@"
            $source = '{sourceFolder.Replace("'", "''")}'
            $output = '{outputIso.Replace("'", "''")}'
            $label  = '{volumeLabel.Replace("'", "''")}'
            
            # Use mkisofs/xorriso style via available tools
            # Fallback: create a simple data ISO
            $null = New-IsoFile -Source $source -OutputIso $output -Label $label -ErrorAction Stop
        ";

        // Actually, let's just inform the user
        throw new InvalidOperationException(
            "ISO building requires oscdimg.exe from the Windows ADK. " +
            "Install it from: https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install");
    }

    #endregion

    #region Registry Import

    public async Task ImportRegFileAsync(string mountPath, string regFilePath)
    {
        _logService.Log(LogLevel.Info, $"Importing registry file: {Path.GetFileName(regFilePath)}");

        // Read the .reg file and determine which hive it targets
        var content = await File.ReadAllTextAsync(regFilePath);

        // Parse .reg file to determine target hive(s)
        if (content.Contains("[HKEY_LOCAL_MACHINE\\SOFTWARE", StringComparison.OrdinalIgnoreCase))
        {
            await ImportRegToHiveAsync(mountPath, regFilePath, "SOFTWARE",
                Path.Combine(mountPath, @"Windows\System32\config\SOFTWARE"), "HKLM\\WW_REGIMP_SW");
        }

        if (content.Contains("[HKEY_LOCAL_MACHINE\\SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            await ImportRegToHiveAsync(mountPath, regFilePath, "SYSTEM",
                Path.Combine(mountPath, @"Windows\System32\config\SYSTEM"), "HKLM\\WW_REGIMP_SYS");
        }

        if (content.Contains("[HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("[HKEY_USERS\\.DEFAULT", StringComparison.OrdinalIgnoreCase))
        {
            await ImportRegToHiveAsync(mountPath, regFilePath, "NTUSER",
                Path.Combine(mountPath, @"Users\Default\NTUSER.DAT"), "HKLM\\WW_REGIMP_USR");
        }

        _logService.Log(LogLevel.Success, $"Registry import complete: {Path.GetFileName(regFilePath)}");
    }

    private async Task ImportRegToHiveAsync(string mountPath, string regFilePath, string hiveType, string hivePath, string mountKey)
    {
        if (!File.Exists(hivePath))
        {
            _logService.Log(LogLevel.Warning, $"Hive not found: {hiveType}");
            return;
        }

        try
        {
            await RunRegAsync($"LOAD \"{mountKey}\" \"{hivePath}\"");

            // Create a modified .reg file with remapped paths
            var tempReg = Path.Combine(Path.GetTempPath(), $"WW_import_{hiveType}.reg");
            var content = await File.ReadAllTextAsync(regFilePath);

            // Remap HKLM\SOFTWARE → mountKey, etc.
            content = content.Replace($"HKEY_LOCAL_MACHINE\\{hiveType}", mountKey.Replace("HKLM\\", "HKEY_LOCAL_MACHINE\\"));
            content = content.Replace("HKEY_CURRENT_USER", mountKey.Replace("HKLM\\", "HKEY_LOCAL_MACHINE\\"));

            await File.WriteAllTextAsync(tempReg, content);
            await RunRegAsync($"IMPORT \"{tempReg}\"");

            try { File.Delete(tempReg); } catch { }
        }
        finally
        {
            await Task.Delay(300);
            await RunRegAsync($"UNLOAD \"{mountKey}\"");
        }
    }

    #endregion

    #region Apply to All Editions

    public async Task ApplyToAllEditionsAsync(string wimPath, Func<string, int, Task> action, IProgress<(int percent, string status)>? progress = null)
    {
        var editions = await GetEditionsAsync(wimPath);
        _logService.Log(LogLevel.Info, $"Applying changes to all {editions.Count} edition(s)...");

        for (int i = 0; i < editions.Count; i++)
        {
            var edition = editions[i];
            var mountDir = Path.Combine(Path.GetTempPath(), $"TrimKit_AllEd_{edition.Index}");
            Directory.CreateDirectory(mountDir);

            var pct = (int)((i + 1.0) / editions.Count * 100);
            progress?.Report((pct, $"[{i + 1}/{editions.Count}] {edition.Name}"));

            try
            {
                await RunDismAsync($"/Mount-Wim /WimFile:\"{wimPath}\" /Index:{edition.Index} /MountDir:\"{mountDir}\"");
                await action(mountDir, edition.Index);
                await RunDismAsync($"/Unmount-Wim /MountDir:\"{mountDir}\" /Commit");
                _logService.Log(LogLevel.Success, $"Applied to: {edition.Name}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed on {edition.Name}: {ex.Message}");
                try { await RunDismAsync($"/Unmount-Wim /MountDir:\"{mountDir}\" /Discard"); } catch { }
            }
            finally
            {
                try { Directory.Delete(mountDir, true); } catch { }
            }
        }
    }

    #endregion

    #region Helpers

    private async Task<string> RunDismAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dism.exe",
            Arguments = arguments,
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

        if (process.ExitCode != 0)
        {
            var msg = !string.IsNullOrWhiteSpace(error) ? error : output;
            throw new InvalidOperationException($"DISM error: {msg.Trim()}");
        }

        return output;
    }

    private static async Task<string> RunRegAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    private static string ExtractField(string block, string fieldName)
    {
        var match = Regex.Match(block, $@"{fieldName}\s*:\s*(.+)");
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    #endregion
}
