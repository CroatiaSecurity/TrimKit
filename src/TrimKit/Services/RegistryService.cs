using System.Diagnostics;
using System.IO;
using TrimKit.Models;

namespace TrimKit.Services;

public class RegistryService : IRegistryService
{
    private readonly ILogService _logService;

    public RegistryService(ILogService logService)
    {
        _logService = logService;
    }

    public async Task ApplyTweakAsync(string mountPath, RegistryTweak tweak)
    {
        // Determine which hive file to load based on the key path
        var (hivePath, mountKey) = GetHiveInfo(mountPath, tweak.HivePath);

        try
        {
            await LoadHiveAsync(hivePath, mountKey);

            var fullKeyPath = $"{mountKey}\\{tweak.KeyPath}";
            var typeFlag = tweak.ValueType switch
            {
                RegistryValueType.DWord => "REG_DWORD",
                RegistryValueType.QWord => "REG_QWORD",
                RegistryValueType.String => "REG_SZ",
                RegistryValueType.ExpandString => "REG_EXPAND_SZ",
                RegistryValueType.MultiString => "REG_MULTI_SZ",
                RegistryValueType.Binary => "REG_BINARY",
                _ => "REG_SZ"
            };

            var valueStr = tweak.Value?.ToString() ?? "";
            var args = $"ADD \"{fullKeyPath}\" /v \"{tweak.ValueName}\" /t {typeFlag} /d \"{valueStr}\" /f";

            await RunRegAsync(args);
            _logService.Log(LogLevel.Success, $"Applied tweak: {tweak.Name}");
        }
        finally
        {
            await UnloadHiveAsync(mountKey);
        }
    }

    public async Task LoadHiveAsync(string hivePath, string mountKey)
    {
        await RunRegAsync($"LOAD \"{mountKey}\" \"{hivePath}\"");
        _logService.Log(LogLevel.Info, $"Loaded hive: {mountKey}");
    }

    public async Task UnloadHiveAsync(string mountKey)
    {
        // Small delay to ensure handles are released
        await Task.Delay(500);
        await RunRegAsync($"UNLOAD \"{mountKey}\"");
        _logService.Log(LogLevel.Info, $"Unloaded hive: {mountKey}");
    }

    public List<RegistryTweak> GetBuiltInTweaks()
    {
        return TweakDatabase.GetAllTweaks();
    }

    private static (string hivePath, string mountKey) GetHiveInfo(string mountPath, string hiveType)
    {
        return hiveType.ToUpperInvariant() switch
        {
            "SOFTWARE" => (Path.Combine(mountPath, @"Windows\System32\config\SOFTWARE"), "HKLM\\WW_SOFTWARE"),
            "SYSTEM" => (Path.Combine(mountPath, @"Windows\System32\config\SYSTEM"), "HKLM\\WW_SYSTEM"),
            "NTUSER" => (Path.Combine(mountPath, @"Users\Default\NTUSER.DAT"), "HKLM\\WW_NTUSER"),
            "DEFAULT" => (Path.Combine(mountPath, @"Windows\System32\config\DEFAULT"), "HKLM\\WW_DEFAULT"),
            _ => (Path.Combine(mountPath, @"Windows\System32\config\SOFTWARE"), "HKLM\\WW_SOFTWARE")
        };
    }

    private static async Task RunRegAsync(string arguments)
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

        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException($"Registry operation failed: {error.Trim()}");
        }
    }
}
