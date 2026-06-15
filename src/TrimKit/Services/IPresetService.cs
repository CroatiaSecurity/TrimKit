using TrimKit.Models;

namespace TrimKit.Services;

public interface IPresetService
{
    // TrimKit native format (XML with Keep + Remove lists)
    Task SavePresetAsync(Preset preset, string filePath);
    Task<Preset> LoadPresetAsync(string filePath);

    // Import from other formats
    Task<Preset> ImportNtLitePresetAsync(string xmlPath);
    Task<Preset> ImportWinReducerPresetAsync(string wccfPath);

    // Combine multiple presets into one TrimKit preset
    Preset CombinePresets(IEnumerable<Preset> presets, string combinedName);

    // Detect format from file extension
    PresetFormat DetectFormat(string filePath);
}

public enum PresetFormat
{
    TrimKit,    // .wwp (XML)
    NtLite,      // .xml (NTLite namespace)
    WinReducer,  // .wccf (XML with <Packages> root)
    Unknown
}
