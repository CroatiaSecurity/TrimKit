using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using TrimKit.Models;

namespace TrimKit.Services;

/// <summary>
/// Downloads Windows ISOs directly from Microsoft using the same technique as
/// Fido (Rufus's companion tool). By spoofing a non-Windows user agent, the
/// Microsoft software download page returns direct ISO download links instead
/// of redirecting to the Media Creation Tool.
/// </summary>
public partial class MicrosoftDownloadService : IMicrosoftDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;

    // Microsoft software download pages
    private const string Win10Url = "https://www.microsoft.com/en-us/software-download/windows10ISO";
    private const string Win11Url = "https://www.microsoft.com/en-us/software-download/windows11";

    // User agent to spoof (non-Windows to get direct links)
    private const string LinuxUserAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:120.0) Gecko/20100101 Firefox/120.0";

    public MicrosoftDownloadService(HttpClient httpClient, ILogService logService)
    {
        _httpClient = httpClient;
        _logService = logService;
    }

    public Task<List<MicrosoftProduct>> GetAvailableProductsAsync(CancellationToken ct = default)
    {
        // These are the well-known Microsoft download page products
        var products = new List<MicrosoftProduct>
        {
            new() { ProductId = "windows11", Name = "Windows 11 (Latest)" },
            new() { ProductId = "windows10", Name = "Windows 10 (22H2)" },
        };

        return Task.FromResult(products);
    }

    public async Task<List<WindowsLanguage>> GetProductLanguagesAsync(string productId, string sessionId, CancellationToken ct = default)
    {
        var pageUrl = productId == "windows10" ? Win10Url : Win11Url;

        _logService.Log(Models.LogLevel.Info, $"Fetching available languages for {productId}...");

        using var request = new HttpRequestMessage(HttpMethod.Get, pageUrl);
        request.Headers.UserAgent.Clear();
        request.Headers.TryAddWithoutValidation("User-Agent", LinuxUserAgent);

        var response = await _httpClient.SendAsync(request, ct);
        var html = await response.Content.ReadAsStringAsync(ct);

        // Parse language options from the page
        var languages = new List<WindowsLanguage>();
        var matches = LanguageOptionRegex().Matches(html);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                languages.Add(new WindowsLanguage
                {
                    LangCode = match.Groups[1].Value,
                    Name = match.Groups[2].Value.Trim()
                });
            }
        }

        // If we couldn't parse the page, provide common languages
        if (languages.Count == 0)
        {
            languages = GetFallbackLanguages();
        }

        _logService.Log(Models.LogLevel.Info, $"Found {languages.Count} language(s)");
        return languages;
    }

    public async Task<List<DownloadLink>> GetDownloadLinksAsync(string productId, string languageSkuId, string sessionId, CancellationToken ct = default)
    {
        // The Microsoft download flow involves:
        // 1. GET the page with non-Windows user agent to get the product selection form
        // 2. POST to select the product edition (gets a session/product ID)
        // 3. POST to select the language (gets direct download links)
        // This is a simplified version that uses known patterns

        var pageUrl = productId == "windows10" ? Win10Url : Win11Url;

        _logService.Log(Models.LogLevel.Info, $"Getting download links for {productId} ({languageSkuId})...");

        var links = new List<DownloadLink>();

        try
        {
            // Step 1: Get the page with spoofed user agent
            using var getRequest = new HttpRequestMessage(HttpMethod.Get, pageUrl);
            getRequest.Headers.UserAgent.Clear();
            getRequest.Headers.TryAddWithoutValidation("User-Agent", LinuxUserAgent);

            var pageResponse = await _httpClient.SendAsync(getRequest, ct);
            var pageHtml = await pageResponse.Content.ReadAsStringAsync(ct);

            // Step 2: Look for the download session and post to the language selection
            // The actual flow depends on what Microsoft currently serves
            // For now, parse any direct download links visible on the page
            var downloadMatches = DownloadLinkRegex().Matches(pageHtml);

            foreach (Match match in downloadMatches)
            {
                if (match.Groups.Count >= 2)
                {
                    var url = match.Groups[1].Value;
                    var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                    var arch = url.Contains("x64", StringComparison.OrdinalIgnoreCase) ? "x64" : "x86";

                    links.Add(new DownloadLink
                    {
                        Url = url,
                        FileName = fileName,
                        Architecture = arch
                    });
                }
            }

            if (links.Count == 0)
            {
                _logService.Log(Models.LogLevel.Warning,
                    "Could not extract direct download links from Microsoft. " +
                    "The page structure may have changed. Try using UUP dump instead.");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(Models.LogLevel.Error, $"Microsoft download failed: {ex.Message}");
        }

        return links;
    }

    public async Task DownloadIsoAsync(string url, string outputPath, IProgress<(int percent, string status)>? progress = null, CancellationToken ct = default)
    {
        _logService.Log(Models.LogLevel.Info, $"Downloading ISO to: {outputPath}");
        progress?.Report((0, "Starting download..."));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Clear();
        request.Headers.TryAddWithoutValidation("User-Agent", LinuxUserAgent);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var totalMb = totalBytes > 0 ? totalBytes / (1024.0 * 1024.0) : 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;

            if (totalBytes > 0)
            {
                var percent = (int)(bytesRead * 100 / totalBytes);
                var downloadedMb = bytesRead / (1024.0 * 1024.0);
                progress?.Report((percent, $"Downloading: {downloadedMb:F0} / {totalMb:F0} MB ({percent}%)"));
            }
        }

        _logService.Log(Models.LogLevel.Success, $"ISO downloaded: {outputPath}");
        progress?.Report((100, "Download complete!"));
    }

    private static List<WindowsLanguage> GetFallbackLanguages()
    {
        return
        [
            new() { LangCode = "en-us", Name = "English (United States)" },
            new() { LangCode = "en-gb", Name = "English (United Kingdom)" },
            new() { LangCode = "de-de", Name = "German" },
            new() { LangCode = "fr-fr", Name = "French" },
            new() { LangCode = "es-es", Name = "Spanish" },
            new() { LangCode = "it-it", Name = "Italian" },
            new() { LangCode = "pt-br", Name = "Portuguese (Brazil)" },
            new() { LangCode = "ja-jp", Name = "Japanese" },
            new() { LangCode = "ko-kr", Name = "Korean" },
            new() { LangCode = "zh-cn", Name = "Chinese (Simplified)" },
            new() { LangCode = "zh-tw", Name = "Chinese (Traditional)" },
            new() { LangCode = "ru-ru", Name = "Russian" },
            new() { LangCode = "pl-pl", Name = "Polish" },
            new() { LangCode = "nl-nl", Name = "Dutch" },
            new() { LangCode = "hr-hr", Name = "Croatian" },
            new() { LangCode = "cs-cz", Name = "Czech" },
            new() { LangCode = "da-dk", Name = "Danish" },
            new() { LangCode = "fi-fi", Name = "Finnish" },
            new() { LangCode = "hu-hu", Name = "Hungarian" },
            new() { LangCode = "nb-no", Name = "Norwegian" },
            new() { LangCode = "sv-se", Name = "Swedish" },
            new() { LangCode = "tr-tr", Name = "Turkish" },
            new() { LangCode = "uk-ua", Name = "Ukrainian" },
        ];
    }

    [GeneratedRegex(@"<option\s+value=""([^""]+)""[^>]*>([^<]+)</option>")]
    private static partial Regex LanguageOptionRegex();

    [GeneratedRegex(@"https://software-download\.microsoft\.com/[^""'\s]+\.iso")]
    private static partial Regex DownloadLinkRegex();
}
