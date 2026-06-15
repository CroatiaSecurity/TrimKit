namespace TrimKit.Services;

/// <summary>
/// Scrapes the Microsoft Update Catalog (catalog.update.microsoft.com) to find
/// available updates for a specific Windows build. No official API exists —
/// this uses HTTP requests and HTML parsing like MSCatalog/PowerShell modules.
/// </summary>
public interface IUpdateCatalogService
{
    /// <summary>
    /// Searches the Microsoft Update Catalog for updates matching a query string.
    /// Typically: "Cumulative Update for Windows 11 Version 24H2 x64"
    /// </summary>
    Task<List<CatalogUpdate>> SearchUpdatesAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Gets the download URL(s) for a specific update by its catalog GUID.
    /// </summary>
    Task<List<string>> GetDownloadLinksAsync(string updateId, CancellationToken ct = default);

    /// <summary>
    /// Builds a search query from the mounted image's version info.
    /// </summary>
    string BuildSearchQuery(string windowsVersion, string architecture, string buildNumber);
}

public class CatalogUpdate
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Products { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string LastUpdated { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
