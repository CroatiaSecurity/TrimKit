namespace TrimKit.Models;

public class RegistryTweak
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string HivePath { get; set; } = string.Empty;
    public string KeyPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public RegistryValueType ValueType { get; set; }
    public object? Value { get; set; }
    public bool IsSelected { get; set; }
}

public enum RegistryValueType
{
    DWord,
    QWord,
    String,
    ExpandString,
    MultiString,
    Binary
}
