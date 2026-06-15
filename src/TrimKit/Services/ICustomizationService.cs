namespace TrimKit.Services;

/// <summary>
/// Handles all non-removal image customizations:
/// - Wallpaper replacement (desktop, lock screen, setup/boot)
/// - $OEM$ folder creation and script injection
/// - Boot.wim wallpaper (winpe.jpg / background.bmp)
/// - User folder defaults
/// - Branding (OEM info, logos)
/// - Post-install scripts (SetupComplete.cmd, OOBE scripts)
/// - File injection into the image
/// - Cursor schemes
/// - Theme files
/// </summary>
public interface ICustomizationService
{
    // Wallpaper
    Task SetDesktopWallpaperAsync(string mountPath, string imagePath);
    Task SetLockScreenWallpaperAsync(string mountPath, string imagePath);
    Task SetSetupWallpaperAsync(string mountPath, string imagePath);
    Task SetBootWimWallpaperAsync(string bootWimPath, string imagePath);
    Task SetUserAccountPictureAsync(string mountPath, string imagePath);

    // $OEM$ Folder
    Task CreateOemFolderStructureAsync(string isoSourcePath);
    Task AddFileToOemAsync(string isoSourcePath, string relativePath, string sourceFile);
    Task AddFolderToOemAsync(string isoSourcePath, string relativePath, string sourceFolder);

    // Scripts
    Task SetSetupCompleteScriptAsync(string mountPath, string scriptContent);
    Task SetFirstLogonScriptAsync(string mountPath, string scriptContent);
    Task AddRunOnceCommandAsync(string mountPath, string command, string name);
    Task SetOobeScriptAsync(string isoSourcePath, string scriptContent);

    // Branding
    Task SetOemInfoAsync(string mountPath, OemBrandingInfo branding);
    Task SetOemLogoAsync(string mountPath, string logoPath);

    // File injection
    Task InjectFileAsync(string mountPath, string targetRelativePath, string sourceFile);
    Task InjectFolderAsync(string mountPath, string targetRelativePath, string sourceFolder);

    // Default user profile
    Task SetDefaultShellFoldersAsync(string mountPath, DefaultFolderConfig config);
    Task CopyToDefaultProfileAsync(string mountPath, string relativePath, string sourceFile);

    // Themes and cursors
    Task SetDefaultThemeAsync(string mountPath, string themeFilePath);
    Task SetCursorSchemeAsync(string mountPath, CursorSchemeConfig cursors);

    // Cleanup
    Task CleanupImageAsync(string mountPath, bool resetBase = false);
    Task CompactOsAsync(string mountPath);
}

public class OemBrandingInfo
{
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SupportUrl { get; set; } = string.Empty;
    public string SupportPhone { get; set; } = string.Empty;
    public string SupportHours { get; set; } = string.Empty;
}

public class DefaultFolderConfig
{
    public string? DesktopPath { get; set; }
    public string? DocumentsPath { get; set; }
    public string? DownloadsPath { get; set; }
    public string? MusicPath { get; set; }
    public string? PicturesPath { get; set; }
    public string? VideosPath { get; set; }
}

public class CursorSchemeConfig
{
    public string? Arrow { get; set; }
    public string? Help { get; set; }
    public string? AppStarting { get; set; }
    public string? Wait { get; set; }
    public string? Crosshair { get; set; }
    public string? IBeam { get; set; }
    public string? NWPen { get; set; }
    public string? No { get; set; }
    public string? SizeNS { get; set; }
    public string? SizeWE { get; set; }
    public string? SizeNWSE { get; set; }
    public string? SizeNESW { get; set; }
    public string? SizeAll { get; set; }
    public string? UpArrow { get; set; }
    public string? Hand { get; set; }
}
