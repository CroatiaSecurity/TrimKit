namespace TrimKit.Models;

public class WindowsFeature
{
    public string FeatureName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool OriginalState { get; set; }
    public string Description { get; set; } = string.Empty;

    public bool IsModified => IsEnabled != OriginalState;
}
