using System.Diagnostics;
using System.IO;
using TrimKit.Models;

namespace TrimKit.Services;

public class CustomizationService : ICustomizationService
{
    private readonly ILogService _logService;

    public CustomizationService(ILogService logService)
    {
        _logService = logService;
    }

    #region Wallpaper

    public async Task SetDesktopWallpaperAsync(string mountPath, string imagePath)
    {
        // Replace img0.jpg in Windows\Web\Wallpaper\Windows (default desktop wallpaper)
        var targets = new[]
        {
            Path.Combine(mountPath, @"Windows\Web\Wallpaper\Windows\img0.jpg"),
            Path.Combine(mountPath, @"Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg"),
            Path.Combine(mountPath, @"Windows\Web\4K\Wallpaper\Windows\img0_2560x1600.jpg"),
            Path.Combine(mountPath, @"Windows\Web\4K\Wallpaper\Windows\img0_1920x1200.jpg"),
            Path.Combine(mountPath, @"Windows\Web\4K\Wallpaper\Windows\img0_1920x1080.jpg"),
        };

        foreach (var target in targets)
        {
            if (File.Exists(target) || Directory.Exists(Path.GetDirectoryName(target)!))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(imagePath, target, overwrite: true);
            }
        }

        _logService.Log(LogLevel.Success, $"Desktop wallpaper replaced: {Path.GetFileName(imagePath)}");
    }

    public async Task SetLockScreenWallpaperAsync(string mountPath, string imagePath)
    {
        // Replace default lock screen image
        var target = Path.Combine(mountPath, @"Windows\Web\Screen\img100.jpg");
        var dir = Path.GetDirectoryName(target)!;

        if (Directory.Exists(dir))
        {
            File.Copy(imagePath, target, overwrite: true);
            // Also replace other resolution variants
            foreach (var file in Directory.GetFiles(dir, "img10*.jpg"))
            {
                File.Copy(imagePath, file, overwrite: true);
            }
        }

        _logService.Log(LogLevel.Success, "Lock screen wallpaper replaced");
        await Task.CompletedTask;
    }

    public async Task SetSetupWallpaperAsync(string mountPath, string imagePath)
    {
        // Windows setup background (OOBE)
        var target = Path.Combine(mountPath, @"Windows\System32\oobe\info\backgrounds\backgroundDefault.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(imagePath, target, overwrite: true);

        _logService.Log(LogLevel.Success, "Setup/OOBE wallpaper set");
        await Task.CompletedTask;
    }

    public async Task SetBootWimWallpaperAsync(string bootWimPath, string imagePath)
    {
        // Mount boot.wim index 1 (WinPE), replace winpe.jpg / background.bmp
        var mountDir = Path.Combine(Path.GetTempPath(), "TrimKit_BootWallpaper");
        Directory.CreateDirectory(mountDir);

        try
        {
            await RunDismAsync($"/Mount-Wim /WimFile:\"{bootWimPath}\" /Index:1 /MountDir:\"{mountDir}\"");

            // WinPE background locations
            var targets = new[]
            {
                Path.Combine(mountDir, @"Windows\System32\winpe.jpg"),
                Path.Combine(mountDir, @"Windows\System32\setup.bmp"),
                Path.Combine(mountDir, @"sources\background.bmp"),
            };

            foreach (var target in targets)
            {
                var dir = Path.GetDirectoryName(target)!;
                if (Directory.Exists(dir))
                {
                    File.Copy(imagePath, target, overwrite: true);
                }
            }

            await RunDismAsync($"/Unmount-Wim /MountDir:\"{mountDir}\" /Commit");
            _logService.Log(LogLevel.Success, "Boot.wim wallpaper replaced");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Boot wallpaper failed: {ex.Message}");
            try { await RunDismAsync($"/Unmount-Wim /MountDir:\"{mountDir}\" /Discard"); } catch { }
        }
        finally
        {
            try { Directory.Delete(mountDir, true); } catch { }
        }
    }

    public async Task SetUserAccountPictureAsync(string mountPath, string imagePath)
    {
        var target = Path.Combine(mountPath, @"ProgramData\Microsoft\User Account Pictures\user.png");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(imagePath, target, overwrite: true);
        _logService.Log(LogLevel.Success, "Default user account picture set");
        await Task.CompletedTask;
    }

    #endregion

    #region $OEM$ Folder

    public Task CreateOemFolderStructureAsync(string isoSourcePath)
    {
        // Standard $OEM$ structure:
        // sources\$OEM$\$1\         → maps to C:\ 
        // sources\$OEM$\$$\         → maps to %WINDIR%
        // sources\$OEM$\$1\Users\Default\Desktop\ → default user desktop
        var paths = new[]
        {
            Path.Combine(isoSourcePath, @"sources\$OEM$\$1"),
            Path.Combine(isoSourcePath, @"sources\$OEM$\$$"),
            Path.Combine(isoSourcePath, @"sources\$OEM$\$$\Setup\Scripts"),
            Path.Combine(isoSourcePath, @"sources\$OEM$\$1\Users\Default\Desktop"),
        };

        foreach (var path in paths)
            Directory.CreateDirectory(path);

        _logService.Log(LogLevel.Success, "$OEM$ folder structure created");
        return Task.CompletedTask;
    }

    public Task AddFileToOemAsync(string isoSourcePath, string relativePath, string sourceFile)
    {
        var target = Path.Combine(isoSourcePath, "sources", "$OEM$", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(sourceFile, target, overwrite: true);
        _logService.Log(LogLevel.Info, $"Added to $OEM$: {relativePath}");
        return Task.CompletedTask;
    }

    public Task AddFolderToOemAsync(string isoSourcePath, string relativePath, string sourceFolder)
    {
        var target = Path.Combine(isoSourcePath, "sources", "$OEM$", relativePath);
        CopyDirectory(sourceFolder, target);
        _logService.Log(LogLevel.Info, $"Added folder to $OEM$: {relativePath}");
        return Task.CompletedTask;
    }

    #endregion

    #region Scripts

    public async Task SetSetupCompleteScriptAsync(string mountPath, string scriptContent)
    {
        // SetupComplete.cmd runs after Windows setup completes (before first logon)
        var scriptsDir = Path.Combine(mountPath, @"Windows\Setup\Scripts");
        Directory.CreateDirectory(scriptsDir);

        var scriptPath = Path.Combine(scriptsDir, "SetupComplete.cmd");
        await File.WriteAllTextAsync(scriptPath, scriptContent);
        _logService.Log(LogLevel.Success, "SetupComplete.cmd script set");
    }

    public async Task SetFirstLogonScriptAsync(string mountPath, string scriptContent)
    {
        // First logon script via RunOnce registry key
        var scriptsDir = Path.Combine(mountPath, @"Windows\Setup\Scripts");
        Directory.CreateDirectory(scriptsDir);

        var scriptPath = Path.Combine(scriptsDir, "FirstLogon.cmd");
        await File.WriteAllTextAsync(scriptPath, scriptContent);

        // Register via RunOnce in default user hive
        await AddRunOnceCommandAsync(mountPath, @"C:\Windows\Setup\Scripts\FirstLogon.cmd", "TrimKitFirstLogon");
    }

    public async Task AddRunOnceCommandAsync(string mountPath, string command, string name)
    {
        var ntUserPath = Path.Combine(mountPath, @"Users\Default\NTUSER.DAT");
        const string mountKey = "HKLM\\WW_RUNONCE";

        try
        {
            await RunRegAsync($"LOAD \"{mountKey}\" \"{ntUserPath}\"");
            await RunRegAsync($"ADD \"{mountKey}\\Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce\" /v \"{name}\" /t REG_SZ /d \"{command}\" /f");
        }
        finally
        {
            await Task.Delay(300);
            await RunRegAsync($"UNLOAD \"{mountKey}\"");
        }

        _logService.Log(LogLevel.Success, $"RunOnce command added: {name}");
    }

    public async Task SetOobeScriptAsync(string isoSourcePath, string scriptContent)
    {
        // OOBE script goes in $OEM$\$$\Setup\Scripts\
        var scriptsDir = Path.Combine(isoSourcePath, @"sources\$OEM$\$$\Setup\Scripts");
        Directory.CreateDirectory(scriptsDir);

        var scriptPath = Path.Combine(scriptsDir, "SetupComplete.cmd");
        await File.WriteAllTextAsync(scriptPath, scriptContent);
        _logService.Log(LogLevel.Success, "OOBE SetupComplete script set in $OEM$");
    }

    #endregion

    #region Branding

    public async Task SetOemInfoAsync(string mountPath, OemBrandingInfo branding)
    {
        var softwarePath = Path.Combine(mountPath, @"Windows\System32\config\SOFTWARE");
        const string mountKey = "HKLM\\WW_OEM";

        try
        {
            await RunRegAsync($"LOAD \"{mountKey}\" \"{softwarePath}\"");

            var oemKey = $"{mountKey}\\Microsoft\\Windows\\CurrentVersion\\OEMInformation";
            await RunRegAsync($"ADD \"{oemKey}\" /v Manufacturer /t REG_SZ /d \"{branding.Manufacturer}\" /f");
            await RunRegAsync($"ADD \"{oemKey}\" /v Model /t REG_SZ /d \"{branding.Model}\" /f");
            await RunRegAsync($"ADD \"{oemKey}\" /v SupportURL /t REG_SZ /d \"{branding.SupportUrl}\" /f");

            if (!string.IsNullOrEmpty(branding.SupportPhone))
                await RunRegAsync($"ADD \"{oemKey}\" /v SupportPhone /t REG_SZ /d \"{branding.SupportPhone}\" /f");
            if (!string.IsNullOrEmpty(branding.SupportHours))
                await RunRegAsync($"ADD \"{oemKey}\" /v SupportHours /t REG_SZ /d \"{branding.SupportHours}\" /f");
        }
        finally
        {
            await Task.Delay(300);
            await RunRegAsync($"UNLOAD \"{mountKey}\"");
        }

        _logService.Log(LogLevel.Success, "OEM branding info set");
    }

    public async Task SetOemLogoAsync(string mountPath, string logoPath)
    {
        // Copy logo to Windows\System32\OEMlogo.bmp
        var target = Path.Combine(mountPath, @"Windows\System32\OEMlogo.bmp");
        File.Copy(logoPath, target, overwrite: true);

        // Register in registry
        var softwarePath = Path.Combine(mountPath, @"Windows\System32\config\SOFTWARE");
        const string mountKey = "HKLM\\WW_OEMLOGO";

        try
        {
            await RunRegAsync($"LOAD \"{mountKey}\" \"{softwarePath}\"");
            await RunRegAsync($"ADD \"{mountKey}\\Microsoft\\Windows\\CurrentVersion\\OEMInformation\" /v Logo /t REG_SZ /d \"C:\\Windows\\System32\\OEMlogo.bmp\" /f");
        }
        finally
        {
            await Task.Delay(300);
            await RunRegAsync($"UNLOAD \"{mountKey}\"");
        }

        _logService.Log(LogLevel.Success, "OEM logo set");
    }

    #endregion

    #region File Injection

    public Task InjectFileAsync(string mountPath, string targetRelativePath, string sourceFile)
    {
        var target = Path.Combine(mountPath, targetRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(sourceFile, target, overwrite: true);
        _logService.Log(LogLevel.Info, $"Injected: {targetRelativePath}");
        return Task.CompletedTask;
    }

    public Task InjectFolderAsync(string mountPath, string targetRelativePath, string sourceFolder)
    {
        var target = Path.Combine(mountPath, targetRelativePath);
        CopyDirectory(sourceFolder, target);
        _logService.Log(LogLevel.Info, $"Injected folder: {targetRelativePath}");
        return Task.CompletedTask;
    }

    #endregion

    #region Default User Profile

    public async Task SetDefaultShellFoldersAsync(string mountPath, DefaultFolderConfig config)
    {
        var ntUserPath = Path.Combine(mountPath, @"Users\Default\NTUSER.DAT");
        const string mountKey = "HKLM\\WW_FOLDERS";

        try
        {
            await RunRegAsync($"LOAD \"{mountKey}\" \"{ntUserPath}\"");
            var baseKey = $@"{mountKey}\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";

            if (!string.IsNullOrEmpty(config.DesktopPath))
                await RunRegAsync($"ADD \"{baseKey}\" /v Desktop /t REG_EXPAND_SZ /d \"{config.DesktopPath}\" /f");
            if (!string.IsNullOrEmpty(config.DocumentsPath))
                await RunRegAsync($"ADD \"{baseKey}\" /v Personal /t REG_EXPAND_SZ /d \"{config.DocumentsPath}\" /f");
            if (!string.IsNullOrEmpty(config.DownloadsPath))
                await RunRegAsync($"ADD \"{baseKey}\" /v \"{{374DE290-123F-4565-9164-39C4925E467B}}\" /t REG_EXPAND_SZ /d \"{config.DownloadsPath}\" /f");
            if (!string.IsNullOrEmpty(config.MusicPath))
                await RunRegAsync($"ADD \"{baseKey}\" /v \"My Music\" /t REG_EXPAND_SZ /d \"{config.MusicPath}\" /f");
            if (!string.IsNullOrEmpty(config.PicturesPath))
                await RunRegAsync($"ADD \"{baseKey}\" /v \"My Pictures\" /t REG_EXPAND_SZ /d \"{config.PicturesPath}\" /f");
            if (!string.IsNullOrEmpty(config.VideosPath))
                await RunRegAsync($"ADD \"{baseKey}\" /v \"My Video\" /t REG_EXPAND_SZ /d \"{config.VideosPath}\" /f");
        }
        finally
        {
            await Task.Delay(300);
            await RunRegAsync($"UNLOAD \"{mountKey}\"");
        }

        _logService.Log(LogLevel.Success, "Default shell folders configured");
    }

    public Task CopyToDefaultProfileAsync(string mountPath, string relativePath, string sourceFile)
    {
        var target = Path.Combine(mountPath, "Users", "Default", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(sourceFile, target, overwrite: true);
        _logService.Log(LogLevel.Info, $"Copied to default profile: {relativePath}");
        return Task.CompletedTask;
    }

    #endregion

    #region Themes and Cursors

    public Task SetDefaultThemeAsync(string mountPath, string themeFilePath)
    {
        var target = Path.Combine(mountPath, @"Windows\Resources\Themes\custom.theme");
        File.Copy(themeFilePath, target, overwrite: true);
        _logService.Log(LogLevel.Success, "Custom theme file injected");
        return Task.CompletedTask;
    }

    public async Task SetCursorSchemeAsync(string mountPath, CursorSchemeConfig cursors)
    {
        var ntUserPath = Path.Combine(mountPath, @"Users\Default\NTUSER.DAT");
        const string mountKey = "HKLM\\WW_CURSORS";

        try
        {
            await RunRegAsync($"LOAD \"{mountKey}\" \"{ntUserPath}\"");
            var baseKey = $@"{mountKey}\Control Panel\Cursors";

            if (!string.IsNullOrEmpty(cursors.Arrow))
                await RunRegAsync($"ADD \"{baseKey}\" /v Arrow /t REG_EXPAND_SZ /d \"{cursors.Arrow}\" /f");
            if (!string.IsNullOrEmpty(cursors.Help))
                await RunRegAsync($"ADD \"{baseKey}\" /v Help /t REG_EXPAND_SZ /d \"{cursors.Help}\" /f");
            if (!string.IsNullOrEmpty(cursors.AppStarting))
                await RunRegAsync($"ADD \"{baseKey}\" /v AppStarting /t REG_EXPAND_SZ /d \"{cursors.AppStarting}\" /f");
            if (!string.IsNullOrEmpty(cursors.Wait))
                await RunRegAsync($"ADD \"{baseKey}\" /v Wait /t REG_EXPAND_SZ /d \"{cursors.Wait}\" /f");
            if (!string.IsNullOrEmpty(cursors.Crosshair))
                await RunRegAsync($"ADD \"{baseKey}\" /v Crosshair /t REG_EXPAND_SZ /d \"{cursors.Crosshair}\" /f");
            if (!string.IsNullOrEmpty(cursors.IBeam))
                await RunRegAsync($"ADD \"{baseKey}\" /v IBeam /t REG_EXPAND_SZ /d \"{cursors.IBeam}\" /f");
            if (!string.IsNullOrEmpty(cursors.No))
                await RunRegAsync($"ADD \"{baseKey}\" /v No /t REG_EXPAND_SZ /d \"{cursors.No}\" /f");
            if (!string.IsNullOrEmpty(cursors.SizeNS))
                await RunRegAsync($"ADD \"{baseKey}\" /v SizeNS /t REG_EXPAND_SZ /d \"{cursors.SizeNS}\" /f");
            if (!string.IsNullOrEmpty(cursors.SizeWE))
                await RunRegAsync($"ADD \"{baseKey}\" /v SizeWE /t REG_EXPAND_SZ /d \"{cursors.SizeWE}\" /f");
            if (!string.IsNullOrEmpty(cursors.SizeNWSE))
                await RunRegAsync($"ADD \"{baseKey}\" /v SizeNWSE /t REG_EXPAND_SZ /d \"{cursors.SizeNWSE}\" /f");
            if (!string.IsNullOrEmpty(cursors.SizeNESW))
                await RunRegAsync($"ADD \"{baseKey}\" /v SizeNESW /t REG_EXPAND_SZ /d \"{cursors.SizeNESW}\" /f");
            if (!string.IsNullOrEmpty(cursors.SizeAll))
                await RunRegAsync($"ADD \"{baseKey}\" /v SizeAll /t REG_EXPAND_SZ /d \"{cursors.SizeAll}\" /f");
            if (!string.IsNullOrEmpty(cursors.Hand))
                await RunRegAsync($"ADD \"{baseKey}\" /v Hand /t REG_EXPAND_SZ /d \"{cursors.Hand}\" /f");
        }
        finally
        {
            await Task.Delay(300);
            await RunRegAsync($"UNLOAD \"{mountKey}\"");
        }

        _logService.Log(LogLevel.Success, "Custom cursor scheme set");
    }

    #endregion

    #region Cleanup

    public async Task CleanupImageAsync(string mountPath, bool resetBase = false)
    {
        _logService.Log(LogLevel.Info, "Running DISM cleanup...");

        await RunDismAsync($"/Image:\"{mountPath}\" /Cleanup-Image /StartComponentCleanup" +
            (resetBase ? " /ResetBase" : ""));

        _logService.Log(LogLevel.Success, "Image cleanup complete" + (resetBase ? " (ResetBase applied)" : ""));
    }

    public async Task CompactOsAsync(string mountPath)
    {
        _logService.Log(LogLevel.Info, "Applying Compact OS...");

        // compact /CompactOs:always on the mounted image via offline method
        var psi = new ProcessStartInfo
        {
            FileName = "compact.exe",
            Arguments = $"/CompactOs:always /EXE:LZX",
            WorkingDirectory = mountPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        await process.WaitForExitAsync();

        _logService.Log(LogLevel.Success, "Compact OS applied");
    }

    #endregion

    #region Helpers

    private static async Task<string> RunDismAsync(string arguments)
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
            throw new InvalidOperationException($"DISM error: {(!string.IsNullOrWhiteSpace(error) ? error : output).Trim()}");

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

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }

    #endregion
}
