namespace TrimKit.Models;

public class WimImageInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;

    public string DisplayName => $"[{Index}] {Name} ({Architecture})";
    public string SizeDisplay => $"{Size / (1024 * 1024 * 1024.0):F2} GB";
}
