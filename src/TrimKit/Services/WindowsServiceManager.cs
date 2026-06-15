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
            _logService.Log(LogLevel.Warning, "SYSTEM hive not found");
            return services;
        }

        const string mountKey = "HKLM\\WW_SVC_ENUM";
        try
        {
            await RunRegAsync($"LOAD \"{mountKey}\" \"{systemHive}\"");

            // Enumerate services from ControlSet001\Services
            var output = await RunRegAsync($"QUERY \"{mountKey}\\ControlSet001\\Services\" /s /v Start");

            var currentService = "";
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("\\Services\\"))
                {
                    var parts = trimmed.Split("\\Services\\");
                    if (parts.Length >= 2)
                    {
                        var svcPath = parts[1].Trim();
                        // Only top-level service keys (no sub-keys)
                        if (!svcPath.Contains('\\'))
                            currentService = svcPath;
                        else
                            currentService = "";
                    }
                }
                else if (!string.IsNullOrEmpty(currentService) && trimmed.Contains("Start") && trimmed.Contains("REG_DWORD"))
                {
                    var valuePart = trimmed.Split("REG_DWORD").LastOrDefault()?.Trim();
                    if (valuePart != null && valuePart.StartsWith("0x"))
                    {
                        var startValue = Convert.ToInt32(valuePart, 16);
                        if (startValue >= 0 && startValue <= 4)
                        {
                            services.Add(new WindowsServiceInfo
                            {
                                ServiceName = currentService,
                                DisplayName = currentService,
                                StartType = (ServiceStartType)startValue,
                                OriginalStartType = (ServiceStartType)startValue
                            });
                        }
                    }
                    currentService = "";
                }
            }
        }
        finally
        {
            await Task.Delay(200);
            await RunRegAsync($"UNLOAD \"{mountKey}\"");
        }

        _logService.Log(LogLevel.Info, $"Found {services.Count} services");
        return services.OrderBy(s => s.ServiceName).ToList();
    }

    public async Task SetServiceStartTypeAsync(string mountPath, string serviceName, ServiceStartType startType)
    {
        var systemHive = Path.Combine(mountPath, @"Windows\System32\config\SYSTEM");
        const string mountKey = "HKLM\\WW_SVC_MOD";

        try
        {
            await RunRegAsync($"LOAD \"{mountKey}\" \"{systemHive}\"");
            var hexValue = $"0x{(int)startType:X8}";
            await RunRegAsync($"ADD \"{mountKey}\\ControlSet001\\Services\\{serviceName}\" /v Start /t REG_DWORD /d {(int)startType} /f");
            _logService.Log(LogLevel.Success, $"Set {serviceName} → {startType}");
        }
        finally
        {
            await Task.Delay(200);
            await RunRegAsync($"UNLOAD \"{mountKey}\"");
        }
    }

    public async Task RemoveServiceAsync(string mountPath, string serviceName)
    {
        var systemHive = Path.Combine(mountPath, @"Windows\System32\config\SYSTEM");
        const string mountKey = "HKLM\\WW_SVC_DEL";

        try
        {
            await RunRegAsync($"LOAD \"{mountKey}\" \"{systemHive}\"");
            await RunRegAsync($"DELETE \"{mountKey}\\ControlSet001\\Services\\{serviceName}\" /f");
            _logService.Log(LogLevel.Success, $"Removed service: {serviceName}");
        }
        finally
        {
            await Task.Delay(200);
            await RunRegAsync($"UNLOAD \"{mountKey}\"");
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
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}
