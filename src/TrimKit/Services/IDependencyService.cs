namespace TrimKit.Services;

/// <summary>
/// Handles first-run dependency downloads and tool verification.
/// Downloads oscdimg, wimlib (for advanced WIM operations), and other tools as needed.
/// </summary>
public interface IDependencyService
{
    Task<bool> CheckDependenciesAsync();
    Task DownloadMissingDependenciesAsync(IProgress<(int percent, string status)>? progress = null);
    string? GetToolPath(string toolName);
    bool IsAdkInstalled();
}
