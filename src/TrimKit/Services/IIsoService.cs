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

    /// <summary>
    /// Mounts the ISO via Explorer shell verb, suppresses the auto-open Explorer window,
    /// and returns the drive letter the ISO was mounted to.
    /// </summary>
    Task<string> MountIsoSuppressedAsync(string isoPath);

    /// <summary>
    /// Copies entire ISO content to a working folder on an NTFS drive (temp).
    /// Returns the path to the working folder.
    /// </summary>
    Task<string> CopyIsoToWorkFolderAsync(string mountedDrivePath, IProgress<(int percent, string status)>? progress = null);

    /// <summary>
    /// Checks if install.wim or install.esd is in recovery (LZMS) compression format.
    /// </summary>
    Task<bool> IsRecoveryCompressedAsync(string imagePath);

    /// <summary>
    /// Converts a recovery-compressed install.wim/esd to a normal-compressed install.wim.
    /// Returns the path to the converted WIM.
    /// </summary>
    Task<string> ConvertToNormalWimAsync(string imagePath, IProgress<(int percent, string status)>? progress = null);

    /// <summary>
    /// Exports a single edition (by index) from install.wim into a new WIM file in a dedicated folder.
    /// Returns the path to the exported WIM.
    /// </summary>
    Task<string> ExtractEditionAsync(string wimPath, int editionIndex, string outputFolder, IProgress<(int percent, string status)>? progress = null);

    /// <summary>
    /// Extracts boot.wim from the work folder into its own dedicated folder.
    /// Returns the path to the extracted boot.wim.
    /// </summary>
    Task<string> ExtractBootWimAsync(string workFolder, string outputFolder, IProgress<(int percent, string status)>? progress = null);

    /// <summary>
    /// Finds a suitable NTFS temp drive and creates a working directory.
    /// Returns the path to the created temp working directory.
    /// </summary>
    string GetNtfsTempWorkFolder();
}
