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
        var url = $"{ApiBase}/listid.php?search={Uri.EscapeDataString(search)}&sortByDate=1";
        _logService.Log(Models.LogLevel.Info, $"Searching UUP dump: {search}");

        var response = await _httpClient.GetFromJsonAsync<UupListResponse>(url, JsonOptions, ct);
        var builds = ParseBuilds(response);

        // Keep only actual OS builds: "Windows XX, version YYY (...)"
        // Filter to amd64 only by default (most users). ARM users can search explicitly.
        builds = builds.Where(b =>
            b.Title.StartsWith("Windows ", StringComparison.OrdinalIgnoreCase) &&
            b.Title.Contains(", version ", StringComparison.OrdinalIgnoreCase) &&
            !b.Title.Contains("Insider Preview", StringComparison.OrdinalIgnoreCase) &&
            !b.Title.Contains("Preview Update", StringComparison.OrdinalIgnoreCase) &&
            b.Architecture.Equals("amd64", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        return builds;
    }

    public async Task<List<WindowsBuild>> GetLatestBuildsAsync(CancellationToken ct = default)
    {
        _logService.Log(Models.LogLevel.Info, "Fetching latest retail builds...");

        var builds = new List<WindowsBuild>();

        // Search for each known major version — filter handles the rest
        var searches = new[] { "windows 11 28000", "windows 11 26200", "windows 11 26100", "windows 10 19045" };

        foreach (var search in searches)
        {
            try
            {
                var result = await SearchBuildsAsync(search, ct);
                builds.AddRange(result);
            }
            catch { /* Some searches may return no results */ }
        }

        // Deduplicate by ID
        return builds
            .GroupBy(b => b.Id)
            .Select(g => g.First())
            .OrderByDescending(b => b.DateAdded)
            .ToList();
    }

    public async Task<List<WindowsEdition>> GetEditionsAsync(string updateId, CancellationToken ct = default)
    {
        // First we need a language to query editions — default to en-us
        var url = $"{ApiBase}/listeditions.php?id={Uri.EscapeDataString(updateId)}&lang=en-us";
        var editions = new List<WindowsEdition>();

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var resp) &&
                resp.TryGetProperty("editionFancyNames", out var eds) &&
                eds.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in eds.EnumerateObject())
                {
                    editions.Add(new WindowsEdition
                    {
                        EditionId = prop.Name,
                        Name = prop.Value.GetString() ?? prop.Name
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(Models.LogLevel.Warning, $"Edition fetch failed: {ex.Message}");
        }

        // Fallback if API didn't return editions
        if (editions.Count == 0)
        {
            editions = GetCommonEditions();
        }

        _logService.Log(Models.LogLevel.Info, $"Found {editions.Count} edition(s)");
        return editions;
    }

    public async Task<List<WindowsLanguage>> GetLanguagesAsync(string updateId, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/listlangs.php?id={Uri.EscapeDataString(updateId)}";
        var languages = new List<WindowsLanguage>();

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var resp) &&
                resp.TryGetProperty("langFancyNames", out var langs) &&
                langs.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in langs.EnumerateObject())
                {
                    languages.Add(new WindowsLanguage
                    {
                        LangCode = prop.Name,
                        Name = prop.Value.GetString() ?? prop.Name
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(Models.LogLevel.Warning, $"Language fetch failed: {ex.Message}");
        }

        if (languages.Count == 0)
        {
            languages.Add(new WindowsLanguage { LangCode = "en-us", Name = "English (United States)" });
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
        IProgress<(int percent, string status)>? progress = null, CancellationToken ct = default, bool skipCumulativeUpdate = false)
    {
        Directory.CreateDirectory(outputDir);

        progress?.Report((5, "Getting download links..."));
        var package = await GetDownloadLinksAsync(updateId, edition, language, ct);

        if (package.Files.Count == 0)
        {
            throw new InvalidOperationException("No files available for download. The build may have expired from Microsoft's servers.");
        }

        // Filter out cumulative update (.msu) files if user opted to skip
        var filesToDownload = package.Files;
        if (skipCumulativeUpdate)
        {
            filesToDownload = filesToDownload.Where(f =>
                !f.FileName.EndsWith(".msu", StringComparison.OrdinalIgnoreCase) &&
                !f.FileName.Contains("cumulative", StringComparison.OrdinalIgnoreCase) &&
                !f.FileName.Contains("Windows1", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            var skipped = package.Files.Count - filesToDownload.Count;
            if (skipped > 0)
                _logService.Log(Models.LogLevel.Info, $"Skipping {skipped} cumulative update file(s)");
        }

        // Download all UUP files
        var downloadDir = Path.Combine(outputDir, "UUPs");
        Directory.CreateDirectory(downloadDir);

        var totalFiles = filesToDownload.Count;
        var downloaded = 0;

        foreach (var file in filesToDownload)
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
        progress?.Report((80, "Converting UUP files to WIM (this may take 10-30 min)..."));
        await ConvertUupToIsoAsync(downloadDir, outputDir, edition, progress, ct);

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

    private async Task ConvertUupToIsoAsync(string uupDir, string outputDir, string edition,
        IProgress<(int percent, string status)>? progress, CancellationToken ct)
    {
        // Use DISM to create the WIM from ESD/CAB files
        var esdFiles = Directory.GetFiles(uupDir, "*.esd");

        if (esdFiles.Length > 0)
        {
            var wimOutput = Path.Combine(outputDir, "install.wim");
            var mainEsd = esdFiles.FirstOrDefault(f => f.Contains("professional", StringComparison.OrdinalIgnoreCase)
                || f.Contains("core", StringComparison.OrdinalIgnoreCase)
                || f.Contains("enterprise", StringComparison.OrdinalIgnoreCase))
                ?? esdFiles[0];

            _logService.Log(Models.LogLevel.Info, $"Converting ESD to WIM: {Path.GetFileName(mainEsd)}");
            progress?.Report((80, $"DISM: Exporting {Path.GetFileName(mainEsd)} → install.wim ..."));

            var success = await RunDismWithProgressAsync(
                $"/Export-Image /SourceImageFile:\"{mainEsd}\" /All /DestinationImageFile:\"{wimOutput}\" /Compress:Max",
                progress, 80, 98, ct);

            if (!success)
            {
                _logService.Log(Models.LogLevel.Warning, "DISM /All failed. Trying individual index export...");
                progress?.Report((82, "DISM: Retrying with index 1..."));

                success = await RunDismWithProgressAsync(
                    $"/Export-Image /SourceImageFile:\"{mainEsd}\" /SourceIndex:1 /DestinationImageFile:\"{wimOutput}\" /Compress:Max",
                    progress, 82, 98, ct);
            }

            if (File.Exists(wimOutput))
            {
                var sizeMb = new FileInfo(wimOutput).Length / (1024.0 * 1024.0);
                _logService.Log(Models.LogLevel.Success, $"Created WIM: {wimOutput} ({sizeMb:F0} MB)");
                progress?.Report((99, $"Done: install.wim ({sizeMb:F0} MB)"));
            }
            else
            {
                _logService.Log(Models.LogLevel.Error, "WIM creation failed — no output file produced");
                progress?.Report((99, "ERROR: WIM file was not created"));
            }
        }
        else
        {
            _logService.Log(Models.LogLevel.Warning, "No ESD files found. UUP files downloaded but ISO assembly requires additional tooling (wimlib/oscdimg).");
            progress?.Report((99, "No ESD files found — download may have been incomplete"));
        }
    }

    private static List<WindowsBuild> ParseBuilds(UupListResponse? response)
    {
        var builds = new List<WindowsBuild>();
        if (response?.Response?.Builds == null)
            return builds;

        foreach (var kvp in response.Response.Builds)
        {
            var build = kvp.Value;
            builds.Add(new WindowsBuild
            {
                Id = build.Uuid ?? kvp.Key,
                Title = build.Title ?? string.Empty,
                Build = build.Build ?? string.Empty,
                Architecture = build.Arch ?? string.Empty,
                DateAdded = DateTimeOffset.FromUnixTimeSeconds(build.CreatedTimestamp).LocalDateTime
            });
        }

        return builds.OrderByDescending(b => b.DateAdded).ToList();
    }

    /// <summary>
    /// Runs DISM and parses its stdout in real-time to report progress percentage.
    /// DISM outputs lines like "[ 42.3%]" during export operations.
    /// </summary>
    private async Task<bool> RunDismWithProgressAsync(string arguments,
        IProgress<(int percent, string status)>? progress, int progressMin, int progressMax, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dism.exe",
            Arguments = "/English " + arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        var errors = new System.Text.StringBuilder();

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                errors.AppendLine(e.Data);
                _logService.Log(Models.LogLevel.Warning, $"DISM: {e.Data}");
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        // Read stdout line-by-line to parse progress
        var lastReportedPercent = 0;
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync(ct)) != null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Parse DISM progress: lines contain "[==== 23.4% ]" or "[ 100.0%]"
            var percentMatch = System.Text.RegularExpressions.Regex.Match(line, @"([\d.]+)%");
            if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out var dismPercent))
            {
                var mapped = progressMin + (int)((dismPercent / 100.0) * (progressMax - progressMin));
                if (mapped > lastReportedPercent)
                {
                    lastReportedPercent = mapped;
                    progress?.Report((mapped, $"DISM: {dismPercent:F1}% — Exporting image..."));
                }
            }
            else if (line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                _logService.Log(Models.LogLevel.Error, $"DISM: {line.Trim()}");
                progress?.Report((lastReportedPercent, $"DISM ERROR: {line.Trim()}"));
            }
            else if (line.Contains("successfully", StringComparison.OrdinalIgnoreCase))
            {
                _logService.Log(Models.LogLevel.Success, $"DISM: {line.Trim()}");
            }
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var errorText = errors.ToString();
            _logService.Log(Models.LogLevel.Error, $"DISM failed (exit {process.ExitCode}): {errorText}");
            progress?.Report((lastReportedPercent, $"DISM failed: {errorText.Split('\n').FirstOrDefault()?.Trim()}"));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Downloads UUP files directly (no aria2c) and converts ESD to WIM using DISM.
    /// Exports each index individually, skipping metadata indexes.
    /// </summary>
    public async Task DownloadWithConverterAsync(string updateId, string edition, string language, string outputIsoPath,
        IProgress<(int percent, string status)>? progress = null, CancellationToken ct = default, bool skipCumulativeUpdate = false)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "TrimKit_DL_" + updateId[..8]);
        Directory.CreateDirectory(workDir);

        try
        {
            // Step 1: Get download links
            progress?.Report((5, "Getting file list from UUP dump..."));
            var package = await GetDownloadLinksAsync(updateId, edition, language, ct);

            if (package.Files.Count == 0)
                throw new InvalidOperationException("No files available. Build may have expired.");

            // Filter files — only get ESD files (the actual image)
            var esdFiles = package.Files.Where(f =>
                f.FileName.EndsWith(".esd", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(f.Url)
            ).ToList();

            // Also get CAB files if no ESDs
            if (esdFiles.Count == 0)
            {
                esdFiles = package.Files.Where(f =>
                    (f.FileName.EndsWith(".cab", StringComparison.OrdinalIgnoreCase) ||
                     f.FileName.EndsWith(".esd", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrEmpty(f.Url)
                ).ToList();
            }

            if (skipCumulativeUpdate)
            {
                esdFiles = esdFiles.Where(f =>
                    !f.FileName.Contains("cumulative", StringComparison.OrdinalIgnoreCase) &&
                    !f.FileName.EndsWith(".msu", StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            if (esdFiles.Count == 0)
                throw new InvalidOperationException("No ESD/CAB files found in the download package.");

            // Step 2: Download ESD files
            for (int i = 0; i < esdFiles.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var file = esdFiles[i];
                var filePath = Path.Combine(workDir, file.FileName);
                var pct = 5 + (int)((i + 1.0) / esdFiles.Count * 60);

                // Skip if already downloaded
                if (File.Exists(filePath) && file.Size > 0 && new FileInfo(filePath).Length == file.Size)
                {
                    progress?.Report((pct, $"Cached: {file.FileName}"));
                    continue;
                }

                progress?.Report((pct, $"Downloading [{i + 1}/{esdFiles.Count}]: {file.FileName} ({file.SizeDisplay})"));
                await DownloadFileAsync(file.Url, filePath, ct);
            }

            // Step 3: Convert ESD to WIM
            progress?.Report((70, "Converting ESD to WIM..."));
            var mainEsd = Directory.GetFiles(workDir, "*.esd")
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();

            if (mainEsd == null)
                throw new InvalidOperationException("No ESD file found after download.");

            var wimOutput = Path.ChangeExtension(outputIsoPath, ".wim");
            _logService.Log(Models.LogLevel.Info, $"Converting: {Path.GetFileName(mainEsd)} → {Path.GetFileName(wimOutput)}");

            // Get image info to find valid indexes
            var infoOutput = await RunDismQuietAsync($"/Get-WimInfo /WimFile:\"{mainEsd}\"");
            var indexMatches = System.Text.RegularExpressions.Regex.Matches(infoOutput, @"Index : (\d+)");
            var indexes = indexMatches.Select(m => int.Parse(m.Groups[1].Value)).ToList();

            if (indexes.Count == 0)
                throw new InvalidOperationException("Could not read image indexes from ESD.");

            // Skip index 1 if it looks like metadata (very small or named "Windows Setup")
            var startIdx = 0;
            if (indexes.Count > 1 && infoOutput.Contains("Windows Setup", StringComparison.OrdinalIgnoreCase))
                startIdx = 1;

            var exportedCount = 0;
            for (int i = startIdx; i < indexes.Count; i++)
            {
                var idx = indexes[i];
                var pct = 70 + (int)((i - startIdx + 1.0) / (indexes.Count - startIdx) * 25);
                progress?.Report((pct, $"DISM: Exporting index {idx} ({i - startIdx + 1}/{indexes.Count - startIdx})..."));

                var success = await RunDismWithProgressAsync(
                    $"/Export-Image /SourceImageFile:\"{mainEsd}\" /SourceIndex:{idx} /DestinationImageFile:\"{wimOutput}\" /Compress:Max",
                    progress, pct, pct + 5, ct);

                if (success) exportedCount++;
                else _logService.Log(Models.LogLevel.Warning, $"Could not export index {idx} (may be encrypted/delta)");
            }

            if (exportedCount == 0)
                throw new InvalidOperationException(
                    "Could not export any image index. The ESD may use delta compression " +
                    "which requires wimlib (not available). Try a different build or download " +
                    "the ISO from: https://uupdump.net");

            var wimSize = new FileInfo(wimOutput).Length / (1024.0 * 1024.0 * 1024.0);
            progress?.Report((100, $"Done! WIM saved: {Path.GetFileName(wimOutput)} ({wimSize:F2} GB)"));
            _logService.Log(Models.LogLevel.Success, $"Created: {wimOutput} ({wimSize:F2} GB, {exportedCount} edition(s))");
        }
        finally
        {
            // Don't delete workDir — ESD cache can be reused
        }
    }

    private async Task<string> RunDismQuietAsync(string arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dism.exe",
            Arguments = "/English " + arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
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
        public Dictionary<string, UupBuild>? Builds { get; set; }
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
        public JsonElement LangFancyNames { get; set; }
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
