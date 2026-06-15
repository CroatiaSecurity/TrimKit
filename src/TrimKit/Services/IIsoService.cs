namespace TrimKit.Services;

/// <summary>
/// Service for mounting/extracting ISO files and locating WIM/ESD images within.
/// Uses PowerShell Mount-DiskImage for ISO mounting (built into Windows).
/// </summary>
public interface IIsoService
{
    Task<string> MountIsoAsync(string isoPath);
    Task UnmountIsoAsync(string isoPath);
    Task<string?> FindInstallImageAsync(string isoMountPath);
    Task ExtractWimFromIsoAsync(string isoPath, string outputDir, IProgress<(int percent, string status)>? progress = null);
}
