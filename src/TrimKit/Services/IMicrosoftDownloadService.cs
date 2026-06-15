using TrimKit.Models;

namespace TrimKit.Services;

/// <summary>
/// Service for downloading Windows ISOs directly from Microsoft's servers
/// using the same technique as Fido/Rufus (user-agent spoofing to access
/// the non-MCT download page that provides direct links).
/// </summary>
public interface IMicrosoftDownloadService
{
    Task<List<MicrosoftProduct>> GetAvailableProductsAsync(CancellationToken ct = default);
    Task<List<WindowsLanguage>> GetProductLanguagesAsync(string productId, string sessionId, CancellationToken ct = default);
    Task<List<DownloadLink>> GetDownloadLinksAsync(string productId, string languageSkuId, string sessionId, CancellationToken ct = default);
    Task DownloadIsoAsync(string url, string outputPath, IProgress<(int percent, string status)>? progress = null, CancellationToken ct = default);
}

public class MicrosoftProduct
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

public class DownloadLink
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public long Size { get; set; }

    public string DisplayName => $"{FileName} ({Architecture})";
}
