using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using TrimKit.Models;

namespace TrimKit.Services;

public partial class WinSxsCleanupService : IWinSxsCleanupService
{
    private readonly ILogService _logService;

    public WinSxsCleanupService(ILogService logService)
    {
        _logService = logService;
    }

    public async Task<WinSxsAnalysis> AnalyzeAsync(string mountPath)
    {
        _logService.Log(LogLevel.Info, "Analyzing WinSxS component store...");
        var analysis = new WinSxsAnalysis();

        try
        {
            var output = await RunDismAsync($"/Image:\"{mountPath}\" /Cleanup-Image /AnalyzeComponentStore");

            // Parse the analysis output
            analysis.TotalSizeBytes = ParseSizeField(output, "Component Store.*Size");
            analysis.SharedWithWindowsBytes = ParseSizeField(output, "Shared with Windows");
            analysis.BackupsAndDisabledBytes = ParseSizeField(output, "Backups and Disabled");
            analysis.CacheAndTempBytes = ParseSizeField(output, "Cache and Temporary");

            // Estimate reclaimable
            analysis.ReclaimableBytes = analysis.BackupsAndDisabledBytes + analysis.CacheAndTempBytes;

            if (output.Contains("Component Store Cleanup Recommended : Yes", StringComparison.OrdinalIgnoreCase))
            {
                _logService.Log(LogLevel.Info,
                    $"WinSxS: {analysis.TotalSizeDisplay} total, ~{analysis.ReclaimableDisplay} reclaimable (cleanup recommended)");
            }
            else
            {
                _logService.Log(LogLevel.Info, $"WinSxS: {analysis.TotalSizeDisplay} total");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"WinSxS analysis failed (non-critical): {ex.Message}");

            // Fallback: just measure folder size
            var winSxsDir = Path.Combine(mountPath, "Windows", "WinSxS");
            if (Directory.Exists(winSxsDir))
            {
                analysis.TotalSizeBytes = GetDirectorySize(winSxsDir);
                analysis.ReclaimableBytes = (long)(analysis.TotalSizeBytes * 0.3); // Estimate 30% reclaimable
            }
        }

        return analysis;
    }

    public async Task CleanupAsync(string mountPath, WinSxsCleanupOptions options, IProgress<(int percent, string status)>? progress = null)
    {
        _logService.Log(LogLevel.Info, "Starting WinSxS cleanup...");
        var steps = CountSteps(options);
        var step = 0;

        // Step 1: Standard component cleanup
        if (options.StartComponentCleanup)
        {
            step++;
            progress?.Report((step * 100 / steps, "Running StartComponentCleanup..."));
            try
            {
                await RunDismAsync($"/Image:\"{mountPath}\" /Cleanup-Image /StartComponentCleanup");
                _logService.Log(LogLevel.Success, "StartComponentCleanup complete");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"StartComponentCleanup: {ex.Message}");
            }
        }

        // Step 2: ResetBase (aggressive — removes update rollback)
        if (options.ResetBase)
        {
            step++;
            progress?.Report((step * 100 / steps, "Running ResetBase (removing superseded components)..."));
            try
            {
                await RunDismAsync($"/Image:\"{mountPath}\" /Cleanup-Image /StartComponentCleanup /ResetBase");
                _logService.Log(LogLevel.Success, "ResetBase complete — superseded components removed permanently");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"ResetBase: {ex.Message}");
            }
        }

        // Step 3: Remove orphaned manifests (post-component-removal cleanup)
        if (options.RemoveOrphanedManifests)
        {
            step++;
            progress?.Report((step * 100 / steps, "Removing orphaned WinSxS manifests..."));
            await RemoveOrphanedManifestsAsync(mountPath);
        }

        // Step 4: Remove WinSxS\Backup folder
        if (options.RemoveBackups)
        {
            step++;
            progress?.Report((step * 100 / steps, "Removing WinSxS backup folder..."));
            var backupDir = Path.Combine(mountPath, @"Windows\WinSxS\Backup");
            if (Directory.Exists(backupDir))
            {
                try
                {
                    var sizeBefore = GetDirectorySize(backupDir);
                    Directory.Delete(backupDir, true);
                    Directory.CreateDirectory(backupDir); // Recreate empty (Windows expects it to exist)
                    _logService.Log(LogLevel.Success, $"Removed WinSxS\\Backup ({sizeBefore / (1024.0 * 1024.0):F0} MB freed)");
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Could not clean backup dir: {ex.Message}");
                }
            }
        }

        // Step 5: If Windows Update is NOT being kept, do aggressive cleanup
        if (!options.KeepWindowsUpdateOperational)
        {
            step++;
            progress?.Report((step * 100 / steps, "Aggressive cleanup (WU disabled mode)..."));
            await AggressiveWinSxsCleanupAsync(mountPath);
        }

        progress?.Report((100, "WinSxS cleanup complete"));
        _logService.Log(LogLevel.Success, "WinSxS cleanup finished");
    }

    private async Task RemoveOrphanedManifestsAsync(string mountPath)
    {
        // After component removal, orphaned .manifest files remain in WinSxS\Manifests
        // that reference components no longer present. DISM's cleanup handles most of these,
        // but some orphaned temp/pending files can be cleaned too.

        var tempDir = Path.Combine(mountPath, @"Windows\WinSxS\Temp");
        if (Directory.Exists(tempDir))
        {
            try
            {
                var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                }
                _logService.Log(LogLevel.Info, $"Cleaned WinSxS\\Temp ({files.Length} files)");
            }
            catch { }
        }

        // Clean pending.xml if it exists (leftover from incomplete servicing)
        var pendingXml = Path.Combine(mountPath, @"Windows\WinSxS\pending.xml");
        if (File.Exists(pendingXml))
        {
            try { File.Delete(pendingXml); } catch { }
        }

        // Run DISM cleanup again to handle any new orphans
        try
        {
            await RunDismAsync($"/Image:\"{mountPath}\" /Cleanup-Image /StartComponentCleanup");
        }
        catch { /* May fail if already clean */ }

        _logService.Log(LogLevel.Success, "Orphaned manifest cleanup complete");
    }

    private async Task AggressiveWinSxsCleanupAsync(string mountPath)
    {
        // When Windows Update is NOT being kept operational, we can:
        // 1. Remove the entire WinSxS\ManifestCache
        // 2. Remove TrustedInstaller logs
        // 3. Strip ServicePackCache if present
        // 4. Remove Component Based Servicing logs

        var dirsToClean = new[]
        {
            Path.Combine(mountPath, @"Windows\WinSxS\ManifestCache"),
            Path.Combine(mountPath, @"Windows\Logs\CBS"),
            Path.Combine(mountPath, @"Windows\Logs\DISM"),
            Path.Combine(mountPath, @"Windows\SoftwareDistribution\Download"),
            Path.Combine(mountPath, @"Windows\SoftwareDistribution\DataStore"),
            Path.Combine(mountPath, @"Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization"),
        };

        long totalFreed = 0;
        foreach (var dir in dirsToClean)
        {
            if (Directory.Exists(dir))
            {
                try
                {
                    var size = GetDirectorySize(dir);
                    Directory.Delete(dir, true);
                    Directory.CreateDirectory(dir);
                    totalFreed += size;
                }
                catch { }
            }
        }

        if (totalFreed > 0)
        {
            _logService.Log(LogLevel.Success, $"Aggressive cleanup freed {totalFreed / (1024.0 * 1024.0):F0} MB");
        }

        // Final ResetBase to ensure maximum compression
        try
        {
            await RunDismAsync($"/Image:\"{mountPath}\" /Cleanup-Image /StartComponentCleanup /ResetBase");
        }
        catch { }
    }

    private static int CountSteps(WinSxsCleanupOptions options)
    {
        var steps = 0;
        if (options.StartComponentCleanup) steps++;
        if (options.ResetBase) steps++;
        if (options.RemoveOrphanedManifests) steps++;
        if (options.RemoveBackups) steps++;
        if (!options.KeepWindowsUpdateOperational) steps++;
        return Math.Max(steps, 1);
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch { return 0; }
    }

    private static long ParseSizeField(string output, string fieldPattern)
    {
        var match = Regex.Match(output, $@"{fieldPattern}\s*:\s*([\d.,]+)\s*(GB|MB|KB)", RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(match.Groups[1].Value.Replace(",", ""), out var value))
        {
            return match.Groups[2].Value.ToUpperInvariant() switch
            {
                "GB" => (long)(value * 1024 * 1024 * 1024),
                "MB" => (long)(value * 1024 * 1024),
                "KB" => (long)(value * 1024),
                _ => 0
            };
        }
        return 0;
    }

    private async Task<string> RunDismAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dism.exe",
            Arguments = "/English " + arguments,
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
            throw new InvalidOperationException((!string.IsNullOrWhiteSpace(error) ? error : output).Trim());

        return output;
    }
}
