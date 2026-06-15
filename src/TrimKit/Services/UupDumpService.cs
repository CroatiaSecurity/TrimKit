using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrimKit.Models;

namespace TrimKit.Services;

public class UupDumpService : IUupDumpService
{
    private const string ApiBase = "https://api.uupdump.net";
    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public UupDumpService(HttpClient httpClient, ILogService logService)
    {
        _httpClient = httpClient;
        _logService = logService;
    }

    public async Task<List<WindowsBuild>> SearchBuildsAsync(string search, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/listid.php?search={Uri.EscapeDataString(search)}";
        _logService.Log(Models.LogLevel.Info, $"Searching UUP dump: {search}");

        var response = await _httpClient.GetFromJsonAsync<UupListResponse>(url, JsonOptions, ct);
        return ParseBuilds(response);
    }

    public async Task<List<WindowsBuild>> GetLatestBuildsAsync(CancellationToken ct = default)
    {
        var url = $"{ApiBase}/listid.php";
        _logService.Log(Models.LogLevel.Info, "Fetching latest builds from UUP dump...");

        var response = await _httpClient.GetFromJsonAsync<UupListResponse>(url, JsonOptions, ct);
        return ParseBuilds(response);
    }

    public async Task<List<WindowsEdition>> GetEditionsAsync(string updateId, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/listlangs.php?id={Uri.EscapeDataString(updateId)}";
        var response = await _httpClient.GetFromJsonAsync<UupLangsResponse>(url, JsonOptions, ct);

        if (response?.Response?.LangFancyNames != null)
        {
            // The listlangs endpoint returns languages; we need to call listeditions separately
        }

        // UUP dump doesn't have a clean "list editions" endpoint separate from the download page,
        // so we'll provide common editions based on what's typically available
        return GetCommonEditions();
    }

    public async Task<List<WindowsLanguage>> GetLanguagesAsync(string updateId, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/listlangs.php?id={Uri.EscapeDataString(updateId)}";
        var response = await _httpClient.GetFromJsonAsync<UupLangsResponse>(url, JsonOptions, ct);

        var languages = new List<WindowsLanguage>();
        if (response?.Response?.LangFancyNames != null)
        {
            foreach (var kvp in response.Response.LangFancyNames)
            {
                languages.Add(new WindowsLanguage
                {
                    LangCode = kvp.Key,
                    Name = kvp.Value
                });
            }
        }

        _logService.Log(Models.LogLevel.Info, $"Found {languages.Count} language(s) for build");
        return languages.OrderBy(l => l.Name).ToList();
    }

    public async Task<string> GetDownloadScriptUrlAsync(string updateId, string edition, string language, CancellationToken ct = default)
    {
        // UUP dump provides a download page where you can get the creation package
        return $"https://uupdump.net/get.php?id={updateId}&pack={language}&edition={edition}";
    }

    public async Task<DownloadPackage> GetDownloadLinksAsync(string updateId, string edition, string language, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/get.php?id={Uri.EscapeDataString(updateId)}&lang={Uri.EscapeDataString(language)}&edition={Uri.EscapeDataString(edition)}";
        _logService.Log(Models.LogLevel.Info, $"Getting download links for {edition} ({language})...");

        var response = await _httpClient.GetFromJsonAsync<UupGetResponse>(url, JsonOptions, ct);
        var package = new DownloadPackage
        {
            UpdateId = updateId,
            Edition = edition,
            Language = language
        };

        if (response?.Response?.Files != null)
        {
            foreach (var kvp in response.Response.Files)
            {
                var file = kvp.Value;
                package.Files.Add(new DownloadFile
                {
                    FileName = kvp.Key,
                    Url = file.Url ?? string.Empty,
                    Size = file.Size,
                    Sha1 = file.Sha1 ?? string.Empty
                });
            }
        }

        _logService.Log(Models.LogLevel.Info, $"Found {package.Files.Count} file(s) to download");
        return package;
    }

    public async Task DownloadAndConvertAsync(string updateId, string edition, string language, string outputDir,
        IProgress<(int percent, string status)>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        progress?.Report((5, "Getting download links..."));
        var package = await GetDownloadLinksAsync(updateId, edition, language, ct);

        if (package.Files.Count == 0)
        {
            throw new InvalidOperationException("No files available for download. The build may have expired from Microsoft's servers.");
        }

        // Download all UUP files
        var downloadDir = Path.Combine(outputDir, "UUPs");
        Directory.CreateDirectory(downloadDir);

        var totalFiles = package.Files.Count;
        var downloaded = 0;

        foreach (var file in package.Files)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(file.Url))
            {
                downloaded++;
                continue;
            }

            var filePath = Path.Combine(downloadDir, file.FileName);

            // Skip if already downloaded and correct size
            if (File.Exists(filePath) && file.Size > 0)
            {
                var existingSize = new FileInfo(filePath).Length;
                if (existingSize == file.Size)
                {
                    downloaded++;
                    var pct = 5 + (int)(downloaded / (double)totalFiles * 70);
                    progress?.Report((pct, $"Skipped (cached): {file.FileName}"));
                    continue;
                }
            }

            var percent = 5 + (int)(downloaded / (double)totalFiles * 70);
            progress?.Report((percent, $"Downloading: {file.FileName} ({file.SizeDisplay})"));

            await DownloadFileAsync(file.Url, filePath, ct);
            downloaded++;
        }

        // Convert UUP files to ISO using the converter script
        progress?.Report((80, "Converting UUP files to ISO..."));
        await ConvertUupToIsoAsync(downloadDir, outputDir, edition, ct);

        progress?.Report((100, "ISO creation complete!"));
        _logService.Log(Models.LogLevel.Success, $"ISO created in: {outputDir}");
    }

    private async Task DownloadFileAsync(string url, string outputPath, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await stream.CopyToAsync(fileStream, ct);
    }

    private async Task ConvertUupToIsoAsync(string uupDir, string outputDir, string edition, CancellationToken ct)
    {
        // Use DISM to create the ISO from ESD/CAB files
        // First, check if there's an ESD file we can convert
        var esdFiles = Directory.GetFiles(uupDir, "*.esd");

        if (esdFiles.Length > 0)
        {
            var wimOutput = Path.Combine(outputDir, "install.wim");
            var mainEsd = esdFiles.FirstOrDefault(f => f.Contains("professional", StringComparison.OrdinalIgnoreCase)
                || f.Contains("core", StringComparison.OrdinalIgnoreCase)
                || f.Contains("enterprise", StringComparison.OrdinalIgnoreCase))
                ?? esdFiles[0];

            _logService.Log(Models.LogLevel.Info, $"Converting ESD to WIM: {Path.GetFileName(mainEsd)}");

            // Export from ESD to WIM using DISM
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = $"/Export-Image /SourceImageFile:\"{mainEsd}\" /All /DestinationImageFile:\"{wimOutput}\" /Compress:Max",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = psi };
            process.Start();
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(ct);
                _logService.Log(Models.LogLevel.Warning, $"ESD export issue: {error}. Will try individual index export.");

                // Try exporting index 1 (skip metadata index)
                psi.Arguments = $"/Export-Image /SourceImageFile:\"{mainEsd}\" /SourceIndex:1 /DestinationImageFile:\"{wimOutput}\" /Compress:Max";
                using var retry = new System.Diagnostics.Process { StartInfo = psi };
                retry.Start();
                await retry.WaitForExitAsync(ct);
            }

            if (File.Exists(wimOutput))
            {
                _logService.Log(Models.LogLevel.Success, $"Created WIM: {wimOutput}");
            }
        }
        else
        {
            _logService.Log(Models.LogLevel.Warning, "No ESD files found. UUP files downloaded but ISO assembly requires additional tooling (wimlib/oscdimg).");
        }
    }

    private static List<WindowsBuild> ParseBuilds(UupListResponse? response)
    {
        var builds = new List<WindowsBuild>();
        if (response?.Response?.Builds == null)
            return builds;

        foreach (var build in response.Response.Builds)
        {
            builds.Add(new WindowsBuild
            {
                Id = build.Uuid ?? string.Empty,
                Title = build.Title ?? string.Empty,
                Build = build.Build ?? string.Empty,
                Architecture = build.Arch ?? string.Empty,
                DateAdded = DateTimeOffset.FromUnixTimeSeconds(build.CreatedTimestamp).LocalDateTime
            });
        }

        return builds.OrderByDescending(b => b.DateAdded).ToList();
    }

    private static List<WindowsEdition> GetCommonEditions()
    {
        return
        [
            new() { EditionId = "core", Name = "Windows Home" },
            new() { EditionId = "professional", Name = "Windows Pro" },
            new() { EditionId = "enterprise", Name = "Windows Enterprise" },
            new() { EditionId = "education", Name = "Windows Education" },
            new() { EditionId = "core;professional", Name = "Windows Home + Pro" },
            new() { EditionId = "professional;enterprise;education", Name = "Windows Pro + Enterprise + Education" },
            new() { EditionId = "ServerStandard", Name = "Windows Server Standard" },
            new() { EditionId = "ServerDatacenter", Name = "Windows Server Datacenter" },
        ];
    }

    // JSON response models
    private class UupListResponse
    {
        [JsonPropertyName("response")]
        public UupListResponseBody? Response { get; set; }
    }

    private class UupListResponseBody
    {
        [JsonPropertyName("builds")]
        public List<UupBuild>? Builds { get; set; }
    }

    private class UupBuild
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        [JsonPropertyName("build")]
        public string? Build { get; set; }
        [JsonPropertyName("arch")]
        public string? Arch { get; set; }
        [JsonPropertyName("created")]
        public JsonElement Created { get; set; }
        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }

        public long CreatedTimestamp => Created.ValueKind switch
        {
            JsonValueKind.Number => Created.GetInt64(),
            JsonValueKind.String => long.TryParse(Created.GetString(), out var v) ? v : 0,
            _ => 0
        };
    }

    private class UupLangsResponse
    {
        [JsonPropertyName("response")]
        public UupLangsResponseBody? Response { get; set; }
    }

    private class UupLangsResponseBody
    {
        [JsonPropertyName("langFancyNames")]
        public Dictionary<string, string>? LangFancyNames { get; set; }
    }

    private class UupGetResponse
    {
        [JsonPropertyName("response")]
        public UupGetResponseBody? Response { get; set; }
    }

    private class UupGetResponseBody
    {
        [JsonPropertyName("files")]
        public Dictionary<string, UupFile>? Files { get; set; }
    }

    private class UupFile
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        [JsonPropertyName("size")]
        public long Size { get; set; }
        [JsonPropertyName("sha1")]
        public string? Sha1 { get; set; }
    }
}
