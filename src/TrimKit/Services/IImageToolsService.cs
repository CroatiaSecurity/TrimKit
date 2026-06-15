namespace TrimKit.Services;

/// <summary>
/// Advanced image operations: edition management, WIM/ESD conversion,
/// Windows Update integration, boot.wim driver injection, and ISO building.
/// </summary>
public interface IImageToolsService
{
    // Edition management
    Task<List<ImageEditionInfo>> GetEditionsAsync(string wimPath);
    Task RemoveEditionAsync(string wimPath, int index);
    Task ExportEditionAsync(string sourceWim, int sourceIndex, string destWim);

    // WIM ↔ ESD conversion
    Task ConvertWimToEsdAsync(string wimPath, string esdPath, IProgress<(int percent, string status)>? progress = null);
    Task ConvertEsdToWimAsync(string esdPath, string wimPath, IProgress<(int percent, string status)>? progress = null);
    Task RenameImageAsync(string sourcePath, string destPath);

    // Windows Update integration
    Task IntegrateUpdatesAsync(string mountPath, string updatesFolder, IProgress<(int percent, string status)>? progress = null);
    Task<List<string>> FindUpdatesInFolderAsync(string folder);

    // Boot.wim operations
    Task AddDriversToBootWimAsync(string bootWimPath, string driverFolder, IProgress<(int percent, string status)>? progress = null);

    // ISO building
    Task BuildIsoAsync(string sourceFolder, string outputIso, string volumeLabel, IProgress<(int percent, string status)>? progress = null);

    // Registry import
    Task ImportRegFileAsync(string mountPath, string regFilePath);

    // Apply changes to all editions
    Task ApplyToAllEditionsAsync(string wimPath, Func<string, int, Task> action, IProgress<(int percent, string status)>? progress = null);
}

public class ImageEditionInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long Size { get; set; }
    public string SizeDisplay => $"{Size / (1024.0 * 1024.0 * 1024.0):F2} GB";
}
