namespace TrimKit.Models;

public class WindowsBuild
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Build { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; }

    public string DisplayTitle => $"{Title} [{Architecture}]";
}

public class WindowsEdition
{
    public string EditionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class WindowsLanguage
{
    public string LangCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class DownloadPackage
{
    public string UpdateId { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public List<DownloadFile> Files { get; set; } = [];
}

public class DownloadFile
{
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha1 { get; set; } = string.Empty;

    public string SizeDisplay => Size > 0 ? $"{Size / (1024.0 * 1024.0):F1} MB" : "Unknown";
}

public enum IsoSource
{
    UupDump,
    MicrosoftDirect,
    LocalFile
}

public class IsoDownloadRequest
{
    public IsoSource Source { get; set; }
    public string? BuildId { get; set; }
    public string? Edition { get; set; }
    public string? Language { get; set; }
    public string? Architecture { get; set; }
    public string OutputPath { get; set; } = string.Empty;
}
