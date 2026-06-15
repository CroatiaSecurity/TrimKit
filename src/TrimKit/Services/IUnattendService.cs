namespace TrimKit.Services;

/// <summary>
/// Generates autounattend.xml files for unattended Windows installations.
/// Inspired by cschneegans/unattend-generator.
/// </summary>
public interface IUnattendService
{
    string GenerateUnattendXml(UnattendConfig config);
    Task SaveUnattendAsync(UnattendConfig config, string outputPath);
    Task InjectIntoImageAsync(string mountPath, UnattendConfig config);
}

public class UnattendConfig
{
    // Locale
    public string Language { get; set; } = "en-US";
    public string InputLocale { get; set; } = "0409:00000409";
    public string TimeZone { get; set; } = "UTC";

    // User
    public string UserName { get; set; } = "User";
    public string? Password { get; set; }
    public bool AutoLogon { get; set; } = true;
    public bool SkipMicrosoftAccount { get; set; } = true;

    // OOBE
    public bool SkipOobe { get; set; } = true;
    public bool HideEula { get; set; } = true;
    public bool DisableTelemetry { get; set; } = true;
    public bool HideWirelessSetup { get; set; } = true;

    // System
    public string ComputerName { get; set; } = "*"; // * = random
    public bool CompactOs { get; set; }
    public bool DisableDefender { get; set; }
    public bool DisableUac { get; set; }
    public bool EnableAdminAccount { get; set; }
    public bool DisableHibernation { get; set; } = true;

    // Product Key (empty = skip)
    public string? ProductKey { get; set; }

    // Install options
    public int ImageIndex { get; set; } = 1;
    public bool BypassTpmCheck { get; set; } = true;
    public bool BypassSecureBootCheck { get; set; } = true;
    public bool BypassRamCheck { get; set; } = true;

    // Post-install commands
    public List<string> FirstLogonCommands { get; set; } = [];
}
