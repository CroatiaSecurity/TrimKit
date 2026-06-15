namespace TrimKit.Models;

/// <summary>
/// TrimKit preset format. Unlike NTLite/WinReducer which only flag items for removal,
/// TrimKit allows users to explicitly KEEP items — anything not in Keep or Remove is left untouched.
/// </summary>
public class Preset
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string Version { get; set; } = "1.0";
    public string? SourceFormat { get; set; } // "TrimKit", "NTLite", "WinReducer", "Combined"
    public string? TargetWindowsVersion { get; set; }

    /// <summary>
    /// Components/packages explicitly marked to REMOVE.
    /// </summary>
    public List<PresetComponent> RemoveList { get; set; } = [];

    /// <summary>
    /// Components/packages explicitly marked to KEEP — these will never be removed
    /// even if other rules would target them. This is the key differentiator from NTLite/WinReducer.
    /// </summary>
    public List<PresetComponent> KeepList { get; set; } = [];

    /// <summary>
    /// Feature state changes (enable/disable).
    /// </summary>
    public List<FeaturePreset> FeatureChanges { get; set; } = [];

    /// <summary>
    /// Registry tweaks to apply.
    /// </summary>
    public List<RegistryTweak> RegistryTweaks { get; set; } = [];

    /// <summary>
    /// Driver paths to integrate.
    /// </summary>
    public List<string> DriverPaths { get; set; } = [];
}

/// <summary>
/// A component entry in the preset, with category and human-readable name.
/// Maps to NTLite component IDs and WinReducer element names.
/// </summary>
public class PresetComponent
{
    /// <summary>
    /// Component identifier (NTLite-style ID like "asimov" or package name).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category for organization (e.g., "Privacy", "Accessories", "Apps").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Source of this entry (which tool/preset it came from).
    /// </summary>
    public string? Source { get; set; }
}

public class FeaturePreset
{
    public string FeatureName { get; set; } = string.Empty;
    public bool Enable { get; set; }
}
