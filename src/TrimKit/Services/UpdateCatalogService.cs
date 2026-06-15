using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using TrimKit.Models;

namespace TrimKit.Services;

/// <summary>
/// Scrapes Microsoft Update Catalog (catalog.update.microsoft.com) for available updates.
/// No official REST API exists — uses HTTP GET/POST + HTML parsing (same approach as MSCatalog PowerShell module).
/// </summary>
public partial class UpdateCatalogService : IUpdateCatalogService
{
    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;
    private const string CatalogBaseUrl = "https://www.catalog.update.microsoft.com";

    public UpdateCatalogService(HttpClient httpClient, ILogService logService)
    {
        _httpClient = httpClient;
        _logService = logService;
    }

    public async Task<List<CatalogUpdate>> SearchUpdatesAsync(string query, CancellationToken ct = default)
    {
        var updates = new List<CatalogUpdate>();

        try
        {
            _logService.Log(LogLevel.Info, $"Searching Microsoft Update Catalog: {query}");

            var url = $"{CatalogBaseUrl}/Search.aspx?q={Uri.EscapeDataString(query)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(ct);

            // Parse the results table rows
            // Each row has: id, title, products, classification, last updated, size
            var rowMatches = TableRowRegex().Matches(html);

            foreach (Match rowMatch in rowMatches)
            {
                var rowHtml = rowMatch.Value;

                // Extract update ID from the onclick or input element
                var idMatch = UpdateIdRegex().Match(rowHtml);
                if (!idMatch.Success) continue;

                // Extract cells
                var cells = CellRegex().Matches(rowHtml);
                if (cells.Count < 4) continue;

                var title = StripHtml(cells[0].Groups[1].Value).Trim();
                var products = cells.Count > 1 ? StripHtml(cells[1].Groups[1].Value).Trim() : "";
                var classification = cells.Count > 2 ? StripHtml(cells[2].Groups[1].Value).Trim() : "";
                var lastUpdated = cells.Count > 3 ? StripHtml(cells[3].Groups[1].Value).Trim() : "";
                var size = cells.Count > 4 ? StripHtml(cells[4].Groups[1].Value).Trim() : "";

                if (string.IsNullOrWhiteSpace(title)) continue;

                updates.Add(new CatalogUpdate
                {
                    Id = idMatch.Groups[1].Value,
                    Title = title,
                    Products = products,
                    Classification = classification,
                    LastUpdated = lastUpdated,
                    Size = size
                });
            }

            // If regex parsing didn't work well, try a simpler line-based approach
            if (updates.Count == 0)
            {
                updates = ParseCatalogSimple(html);
            }

            _logService.Log(LogLevel.Info, $"Found {updates.Count} update(s) from Microsoft Update Catalog");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"Update Catalog search failed: {ex.Message}");
        }

        return updates;
    }

    public async Task<List<string>> GetDownloadLinksAsync(string updateId, CancellationToken ct = default)
    {
        var links = new List<string>();

        try
        {
            // The catalog uses a POST to get download links
            var url = $"{CatalogBaseUrl}/DownloadDialog.aspx";
            var postBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("updateIDs", $"[{{\"size\":0,\"languages\":\"\",\"uidInfo\":\"{updateId}\",\"updateID\":\"{updateId}\"}}]"),
                new KeyValuePair<string, string>("updateIDsBlockedForImport", ""),
                new KeyValuePair<string, string>("wsusApiRecipient", ""),
            });

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = postBody
            };
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await _httpClient.SendAsync(request, ct);
            var html = await response.Content.ReadAsStringAsync(ct);

            // Extract download URLs (*.msu, *.cab files from download.windowsupdate.com)
            var urlMatches = DownloadUrlRegex().Matches(html);
            foreach (Match match in urlMatches)
            {
                var downloadUrl = match.Groups[1].Value;
                if (!links.Contains(downloadUrl))
                    links.Add(downloadUrl);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"Failed to get download links for {updateId}: {ex.Message}");
        }

        return links;
    }

    public string BuildSearchQuery(string windowsVersion, string architecture, string buildNumber)
    {
        // Determine Windows version name from build number
        var versionName = buildNumber switch
        {
            var b when b.StartsWith("26") => "Windows 11 Version 24H2",
            var b when b.StartsWith("22631") => "Windows 11 Version 23H2",
            var b when b.StartsWith("22621") => "Windows 11 Version 22H2",
            var b when b.StartsWith("22000") => "Windows 11 Version 21H2",
            var b when b.StartsWith("19045") => "Windows 10 Version 22H2",
            var b when b.StartsWith("19044") => "Windows 10 Version 21H2",
            var b when b.StartsWith("19043") => "Windows 10 Version 21H1",
            var b when b.StartsWith("19042") => "Windows 10 Version 20H2",
            var b when b.StartsWith("19041") => "Windows 10 Version 2004",
            _ => !string.IsNullOrEmpty(windowsVersion) ? windowsVersion : "Windows 11"
        };

        var arch = architecture.Contains("64", StringComparison.OrdinalIgnoreCase) ? "x64" : "x86";
        return $"Cumulative Update for {versionName} for {arch}-based Systems";
    }

    /// <summary>
    /// Simpler parsing approach — looks for update IDs and titles via known patterns.
    /// </summary>
    private List<CatalogUpdate> ParseCatalogSimple(string html)
    {
        var updates = new List<CatalogUpdate>();

        // Pattern: updateID values paired with title spans
        var idTitleMatches = SimpleUpdateRegex().Matches(html);
        foreach (Match match in idTitleMatches)
        {
            var id = match.Groups[1].Value;
            var title = StripHtml(match.Groups[2].Value).Trim();

            if (!string.IsNullOrWhiteSpace(title) && title.Length > 10)
            {
                updates.Add(new CatalogUpdate
                {
                    Id = id,
                    Title = title,
                    Classification = "Update"
                });
            }
        }

        return updates;
    }

    private static string StripHtml(string html)
    {
        return HtmlTagRegex().Replace(html, "").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&#39;", "'").Replace("&nbsp;", " ").Trim();
    }

    [GeneratedRegex(@"<tr\s[^>]*id=""[^""]*_R\d+""[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TableRowRegex();

    [GeneratedRegex(@"(?:updateid|uid(?:Info)?)[""']?\s*[:=]\s*[""']([a-f0-9\-]{36})", RegexOptions.IgnoreCase)]
    private static partial Regex UpdateIdRegex();

    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CellRegex();

    [GeneratedRegex(@"https?://(?:download\.windowsupdate\.com|catalog\.s\.download\.windowsupdate\.com)[^\s""'<>]+\.(?:msu|cab)", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadUrlRegex();

    [GeneratedRegex(@"<input[^>]*id=""([a-f0-9\-]{36})""[^>]*/>\s*<a[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SimpleUpdateRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
