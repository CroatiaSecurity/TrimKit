using System.Diagnostics;
using System.IO;
using TrimKit.Models;

namespace TrimKit.Services;

public class WindowsServiceManager : IWindowsServiceManager
{
    private readonly ILogService _logService;

    public WindowsServiceManager(ILogService logService)
    {
        _logService = logService;
    }

    public async Task<List<WindowsServiceInfo>> GetServicesAsync(string mountPath)
    {
        var services = new List<WindowsServiceInfo>();
        var systemHive = Path.Combine(mountPath, @"Windows\System32\config\SYSTEM");

        if (!File.Exists(systemHive))
        {
            _logService.Log(LogLevel.Warning, "SYSTEM hive not found — cannot enumerate services");
            return services;
        }

        // DISM holds a lock on the hive while mounted — copy to a temp file first
        var tempHive = Path.Combine(Path.GetTempPath(), $"TrimKit_SYSTEM_{Guid.NewGuid():N}");
        const string mountKey = "HKLM\\WW_SVC_ENUM";

        try
        {
            _logService.Log(LogLevel.Info, "Copying SYSTEM hive to temp for service enumeration...");
            File.Copy(systemHive, tempHive, overwrite: true);

            var loadResult = await RunRegWithTimeoutAsync($"LOAD \"{mountKey}\" \"{tempHive}\"", TimeSpan.FromSeconds(5));
            if (loadResult.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                _logService.Log(LogLevel.Warning, $"Could not load SYSTEM hive copy: {loadResult.Trim()}");
                return services;
            }

            // Get list of service subkeys (non-recursive)
            var keysOutput = await RunRegWithTimeoutAsync(
                $"QUERY \"{mountKey}\\ControlSet001\\Services\"",
                TimeSpan.FromSeconds(10));

            if (string.IsNullOrWhiteSpace(keysOutput))
            {
                _logService.Log(LogLevel.Warning, "No output from registry query");
                return services;
            }

            // Parse subkey names — reg query returns full paths like:
            // HKLM\WW_SVC_ENUM\ControlSet001\Services\ServiceName
            var serviceNames = new List<string>();
            var prefix = $"{mountKey}\\ControlSet001\\Services\\";

            foreach (var line in keysOutput.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var name = trimmed[prefix.Length..].Trim();
                    if (!string.IsNullOrEmpty(name) && !name.Contains('\\'))
                        serviceNames.Add(name);
                }
            }

            _logService.Log(LogLevel.Info, $"Found {serviceNames.Count} service keys, reading Start values...");

            // Read Start value for each service
            foreach (var svcName in serviceNames)
            {
                try
                {
                    var output = await RunRegWithTimeoutAsync(
                        $"QUERY \"{mountKey}\\ControlSet001\\Services\\{svcName}\" /v Start",
                        TimeSpan.FromSeconds(2));

                    if (string.IsNullOrWhiteSpace(output)) continue;

                    foreach (var line in output.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Contains("Start", StringComparison.OrdinalIgnoreCase) &&
                            trimmed.Contains("REG_DWORD", StringComparison.OrdinalIgnoreCase))
                        {
                            var dwordIdx = trimmed.IndexOf("REG_DWORD", StringComparison.OrdinalIgnoreCase);
                            var valuePart = trimmed[(dwordIdx + "REG_DWORD".Length)..].Trim();
                            if (valuePart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            {
                                var startValue = Convert.ToInt32(valuePart, 16);
                                if (startValue >= 0 && startValue <= 4)
                                {
                                    services.Add(new WindowsServiceInfo
                                    {
                                        ServiceName = svcName,
                                        DisplayName = svcName,
                                        StartType = (ServiceStartType)startValue,
                                        OriginalStartType = (ServiceStartType)startValue
                                    });
                                }
                            }
                            break;
                        }
                    }
                }
                catch
                {
                    // Skip services we can't read (no Start value, protected, etc.)
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"Service scan error: {ex.Message}");
        }
        finally
        {
            await Task.Delay(300);
            try { await RunRegAsync($"UNLOAD \"{mountKey}\""); } catch { }
            await Task.Delay(200);
            try { File.Delete(tempHive); } catch { }
        }

        _logService.Log(LogLevel.Info, $"Enumerated {services.Count} services");
        return services.OrderBy(s => s.ServiceName).ToList();
    }

    public async Task SetServiceStartTypeAsync(string mountPath, string serviceName, ServiceStartType startType)
    {
        var systemHive = Path.Combine(mountPath, @"Windows\System32\config\SYSTEM");
        var tempHive = Path.Combine(Path.GetTempPath(), $"TrimKit_SVCMOD_{Guid.NewGuid():N}");
        const string mountKey = "HKLM\\WW_SVC_MOD";

        try
        {
            File.Copy(systemHive, tempHive, overwrite: true);
            await RunRegAsync($"LOAD \"{mountKey}\" \"{tempHive}\"");
            await RunRegAsync($"ADD \"{mountKey}\\ControlSet001\\Services\\{serviceName}\" /v Start /t REG_DWORD /d {(int)startType} /f");
            _logService.Log(LogLevel.Success, $"Set {serviceName} → {startType}");
        }
        finally
        {
            await Task.Delay(200);
            try { await RunRegAsync($"UNLOAD \"{mountKey}\""); } catch { }
            await Task.Delay(200);
            // Copy modified hive back
            try { File.Copy(tempHive, systemHive, overwrite: true); } catch { }
            try { File.Delete(tempHive); } catch { }
        }
    }

    public async Task RemoveServiceAsync(string mountPath, string serviceName)
    {
        var systemHive = Path.Combine(mountPath, @"Windows\System32\config\SYSTEM");
        var tempHive = Path.Combine(Path.GetTempPath(), $"TrimKit_SVCDEL_{Guid.NewGuid():N}");
        const string mountKey = "HKLM\\WW_SVC_DEL";

        try
        {
            File.Copy(systemHive, tempHive, overwrite: true);
            await RunRegAsync($"LOAD \"{mountKey}\" \"{tempHive}\"");
            await RunRegAsync($"DELETE \"{mountKey}\\ControlSet001\\Services\\{serviceName}\" /f");
            _logService.Log(LogLevel.Success, $"Removed service: {serviceName}");
        }
        finally
        {
            await Task.Delay(200);
            try { await RunRegAsync($"UNLOAD \"{mountKey}\""); } catch { }
            await Task.Delay(200);
            try { File.Copy(tempHive, systemHive, overwrite: true); } catch { }
            try { File.Delete(tempHive); } catch { }
        }
    }

    public async Task ConfigureServicesAsync(string mountPath, List<(string serviceName, ServiceStartType startType)> changes)
    {
        if (changes == null || changes.Count == 0) return;

        var systemHive = Path.Combine(mountPath, @"Windows\System32\config\SYSTEM");
        var tempHive = Path.Combine(Path.GetTempPath(), $"TrimKit_SVCMULTI_{Guid.NewGuid():N}");
        const string mountKey = "HKLM\\WW_SVC_MULTI";

        try
        {
            _logService.Log(LogLevel.Info, $"Performing bulk service configuration for {changes.Count} service(s)...");
            File.Copy(systemHive, tempHive, overwrite: true);
            await RunRegAsync($"LOAD \"{mountKey}\" \"{tempHive}\"");

            foreach (var change in changes)
            {
                try
                {
                    if (change.startType == ServiceStartType.Remove)
                    {
                        await RunRegAsync($"DELETE \"{mountKey}\\ControlSet001\\Services\\{change.serviceName}\" /f");
                        _logService.Log(LogLevel.Success, $"Removed service: {change.serviceName}");
                    }
                    else
                    {
                        await RunRegAsync($"ADD \"{mountKey}\\ControlSet001\\Services\\{change.serviceName}\" /v Start /t REG_DWORD /d {(int)change.startType} /f");
                        _logService.Log(LogLevel.Success, $"Set {change.serviceName} → {change.startType}");
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Failed to configure service {change.serviceName}: {ex.Message}");
                }
            }
        }
        finally
        {
            await Task.Delay(200);
            try { await RunRegAsync($"UNLOAD \"{mountKey}\""); } catch { }
            await Task.Delay(200);
            // Copy modified hive back
            try { File.Copy(tempHive, systemHive, overwrite: true); } catch { }
            try { File.Delete(tempHive); } catch { }
        }
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

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return !string.IsNullOrWhiteSpace(output) ? output : error;
    }

    private static async Task<string> RunRegWithTimeoutAsync(string arguments, TimeSpan timeout)
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

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(cts.Token);

            var output = await outputTask;
            return !string.IsNullOrWhiteSpace(output) ? output : await errorTask;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            return string.Empty;
        }
    }
}
