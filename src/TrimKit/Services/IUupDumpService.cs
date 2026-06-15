using TrimKit.Models;

namespace TrimKit.Services;

public interface IUupDumpService
{
    Task<List<WindowsBuild>> SearchBuildsAsync(string search, CancellationToken ct = default);
    Task<List<WindowsBuild>> GetLatestBuildsAsync(CancellationToken ct = default);
    Task<List<WindowsEdition>> GetEditionsAsync(string updateId, CancellationToken ct = default);
    Task<List<WindowsLanguage>> GetLanguagesAsync(string updateId, CancellationToken ct = default);
    Task<string> GetDownloadScriptUrlAsync(string updateId, string edition, string language, CancellationToken ct = default);
    Task<DownloadPackage> GetDownloadLinksAsync(string updateId, string edition, string language, CancellationToken ct = default);
    Task DownloadAndConvertAsync(string updateId, string edition, string language, string outputDir, IProgress<(int percent, string status)>? progress = null, CancellationToken ct = default);
}
