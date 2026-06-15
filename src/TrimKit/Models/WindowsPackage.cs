namespace TrimKit.Models;

public class WindowsPackage
{
    public string PackageName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ReleaseType { get; set; } = string.Empty;
    public DateTime InstallTime { get; set; }
    public bool IsSelected { get; set; }
    public string Description { get; set; } = string.Empty;
}
