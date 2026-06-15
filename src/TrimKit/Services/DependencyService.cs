using System.IO;
using System.Net.Http;
using TrimKit.Models;

namespace TrimKit.Services;

/// <summary>
/// Manages external tool dependencies. On first run, downloads:
/// - oscdimg.exe (for ISO building) — bundled with Windows ADK or downloaded
/// - wimlib-imagex (for advanced WIM/ESD operations beyond what DISM offers)
/// 
/// Tools are stored in %LOCALAPPDATA%\TrimKit\tools\
/// </summary>
public class DependencyService : IDependencyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;
    private readonly string _toolsDir;

    // wimlib releases from GitHub
    private const string WimlibUrl = "https://wimlib.net/downloads/wimlib-1.14.4-windows-x86_64-bin.zip";

    public DependencyService(HttpClient httpClient, ILogService logService)
    {
        _httpClient = httpClient;
        _logService = logService;
        _toolsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrimKit", "tools");
        Directory.CreateDirectory(_toolsDir);
    }

    public Task<bool> CheckDependenciesAsync()
    {
        var hasDism = File.Exists(Path.Combine(Environment.SystemDirectory, "dism.exe"));
        var hasOscdimg = IsAdkInstalled() || File.Exists(Path.Combine(_toolsDir, "oscdimg.exe"));

        if (!hasDism)
        {
            _logService.Log(LogLevel.Error, "DISM not found. This should not happen on Windows 10+.");
            return Task.FromResult(false);
        }

        if (!hasOscdimg)
        {
            _logService.Log(LogLevel.Warning,
                "oscdimg not found. ISO building will not be available. Install Windows ADK or the tool will be downloaded on first use.");
        }

        return Task.FromResult(hasDism);
    }

    public async Task DownloadMissingDependenciesAsync(IProgress<(int percent, string status)>? progress = null)
    {
        // Check if wimlib is already present
        var wimlibPath = Path.Combine(_toolsDir, "wimlib-imagex.exe");
        if (!File.Exists(wimlibPath))
        {
            progress?.Report((10, "Downloading wimlib (advanced WIM tools)..."));
            _logService.Log(LogLevel.Info, "Downloading wimlib...");

            try
            {
                var zipPath = Path.Combine(_toolsDir, "wimlib.zip");
                await DownloadFileAsync(WimlibUrl, zipPath);

                progress?.Report((70, "Extracting wimlib..."));
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, _toolsDir, overwriteFiles: true);
                File.Delete(zipPath);

                _logService.Log(LogLevel.Success, "wimlib downloaded and ready");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Could not download wimlib: {ex.Message}. DISM will be used instead.");
            }
        }

        progress?.Report((100, "Dependencies ready"));
    }

    public string? GetToolPath(string toolName)
    {
        // Check tools directory first
        var localPath = Path.Combine(_toolsDir, toolName);
        if (File.Exists(localPath))
            return localPath;

        // Check subdirectories (wimlib extracts into a folder)
        foreach (var dir in Directory.GetDirectories(_toolsDir))
        {
            var subPath = Path.Combine(dir, toolName);
            if (File.Exists(subPath))
                return subPath;
        }

        // Check system path
        var systemPath = Path.Combine(Environment.SystemDirectory, toolName);
        return File.Exists(systemPath) ? systemPath : null;
    }

    public bool IsAdkInstalled()
    {
        var adkPaths = new[]
        {
            @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
            @"C:\Program Files\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe"
        };

        return adkPaths.Any(File.Exists);
    }

    private async Task DownloadFileAsync(string url, string outputPath)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await stream.CopyToAsync(fileStream);
    }
}
