namespace TrimKit.Services;

/// <summary>
/// Safety guard that prevents removal of components critical to Windows installation
/// and boot. This is the core differentiator from NTLite/WinReducer — TrimKit will
/// NEVER remove anything that prevents a successful install.
///
/// Like NTLite's "Compatibility" dialog, users can check/uncheck functional areas
/// to protect. Checked = protected from removal. Unchecked = can be removed.
///
/// Components are classified into three tiers:
/// - Critical (NEVER removable): Required for install, boot, and basic desktop operation
/// - Protected (user-configurable): Functional areas the user can choose to keep
/// - Safe (freely removable): No impact on installation or basic functionality
/// </summary>
public static class SafetyGuard
{
    /// <summary>
    /// User-configurable compatibility protections (like NTLite's Compatibility dialog).
    /// When enabled, components related to that functionality are protected from removal.
    /// Users can uncheck these to allow removal of those functional areas.
    /// </summary>
    public static List<CompatibilityOption> GetDefaultCompatibilityOptions() =>
    [
        // Application Guard / Containers
        new("AppGuard", "Application Guard / Containers", "Component Functionality", true,
            ["Microsoft.Windows.SecureAssessmentBrowser", "containers", "guardedhost", "isolatedusermode", "deviceguard"]),

        // Apps section
        new("Apps_BattleEye", "BattleEye", "Apps", true,
            ["BattleEye"]),
        new("Apps_CapFrameX", "CapFrameX", "Apps", true,
            ["CapFrameX"]),
        new("Apps_Discord", "Discord", "Apps", true,
            ["Discord"]),
        new("Apps_EAAntiCheat", "EA Anti-Cheat", "Apps", true,
            ["EasyAntiCheat", "EAAntiCheat"]),
        new("Apps_iCloud", "iCloud", "Apps", true,
            ["iCloud"]),
        new("Apps_Kaspersky", "Kaspersky", "Apps", true,
            ["Kaspersky"]),
        new("Apps_NvidiaGeForce", "Nvidia GeForce Experience", "Apps", true,
            ["NvContainer", "NVDisplay", "nvidia"]),
        new("Apps_OneNote", "OneNote", "Apps", false,
            ["OneNote"]),
        new("Apps_SamsungSwitch", "Samsung Smart Switch", "Apps", false,
            ["Samsung"]),
        new("Apps_Spotify", "Spotify", "Apps", false,
            ["Spotify"]),
        new("Apps_TeamViewer", "TeamViewer", "Apps", true,
            ["TeamViewer"]),
        new("Apps_VisualStudio", "Visual Studio", "Apps", true,
            ["VisualStudio", "devenv", "MSBuild", "NuGet"]),

        // File Sharing
        new("FileSharing", "File Sharing support", "Component Functionality", true,
            ["smbv1", "smbdirect", "sharedaccess", "lanmanserver", "lanmanworkstation", "netlogon", "fsmgmt", "offlinefiles"]),

        // Hyper-V
        new("HyperV", "Hyper-V Host", "Component Functionality", false,
            ["hyperv", "hypervguest", "vmcompute", "vmms"]),

        // Modern App support
        new("ModernApps", "Modern App support", "Component Functionality", true,
            ["Microsoft.WindowsStore", "Microsoft.DesktopAppInstaller", "appxsupport", "AppXSvc", "ClipSVC", "Microsoft.UI.Xaml", "Microsoft.VCLibs", "Microsoft.NET.Native"]),
        new("ModernApps_Store", "Windows Store", "Modern App support", true,
            ["Microsoft.WindowsStore", "Microsoft.StorePurchaseApp", "AppXSvc", "ClipSVC"]),

        // Network Discovery
        new("NetworkDiscovery", "Network Discovery", "Component Functionality", false,
            ["fdphost", "FDResPub", "SSDPSRV", "upnphost", "lltdsvc", "nettopology"]),

        // Night Light
        new("NightLight", "Night Light", "Component Functionality", true,
            ["NightLight"]),

        // Printing
        new("Printing", "Printing", "Component Functionality", true,
            ["hwsupport_printer", "printmgmt", "printtopdf", "printworkflow", "spoolsv", "PrintNotify"]),
        new("Printing_Scanner", "Scanner", "Printing", true,
            ["hwsupport_scanner", "stici", "hwsupport_wia"]),

        // Recommended
        new("Recommended", "Recommended", "Component Functionality", true,
            ["servicing", "servicingstack", "TrustedInstaller", "wuauserv"]),
        new("Recommended_CortanaOOBE", "Cortana (OOBE Experience)", "Recommended", true,
            ["OOBE", "Microsoft.Windows.OOBENetworkConnectionFlow"]),

        // Shell Search
        new("ShellSearch", "Shell Search support", "Component Functionality", true,
            ["search", "WSearch", "SearchUI", "Microsoft.Windows.Search", "SearchIndexer"]),

        // Snipping Tool
        new("SnippingTool", "Snipping Tool", "Component Functionality", false,
            ["Microsoft.ScreenSketch", "SnippingTool"]),

        // System File Check (SFC)
        new("SFC", "System File Check (SFC)", "Component Functionality", true,
            ["sfc", "winsxs", "TrustedInstaller", "CBS"]),

        // System Fonts
        new("SystemFonts", "System Fonts", "Component Functionality", true,
            ["segoeui", "seguisym", "segmdl2", "SegUIVar", "marlett", "tahoma", "arial"]),

        // Touch Screen devices
        new("TouchScreen", "Touch Screen devices", "Component Functionality", false,
            ["tabletpc", "osk", "TabletInputService", "TouchHid"]),

        // USB
        new("USB", "USB", "Component Functionality", true,
            ["usbhub", "usbccgp", "USBSTOR", "usbehci", "usbxhci", "usbport"]),
        new("USB_Camera", "Camera", "USB", false,
            ["hwsupport_wia", "frameserver", "webcamexperience"]),
        new("USB_Modem", "USB 3G Modem", "USB", false,
            ["wwanautoconfig", "wwan"]),

        // Video playback
        new("VideoPlayback", "Video playback", "Component Functionality", true,
            ["mediacodec", "mediaplayer", "mpeg2splitter", "mfplat", "mfcore"]),

        // VPN
        new("VPN", "Virtual Private Network (VPN) support", "Component Functionality", false,
            ["vpn", "rasauto", "rasmans", "ikeext", "SSTPSVC"]),

        // Windows Activation
        new("Activation", "Windows Activation", "Component Functionality", true,
            ["sppsvc", "SoftwareProtectionPlatform", "LicensingService", "slui", "ClipUp"]),

        // Windows Setup and Deployment
        new("WindowsSetup", "Windows Setup and Deployment", "Component Functionality", true,
            ["Microsoft-Windows-Setup", "SetupPlatform", "sysprep", "oobe", "WinPE"]),
        new("WindowsSetup_OOBE", "Out-of-Box Experience (OOBE)", "Windows Setup and Deployment", true,
            ["oobe", "Microsoft.Windows.OOBENetworkConnectionFlow", "Microsoft.Windows.OOBENetworkCaptivePortal"]),

        // Windows Firewall
        new("Firewall", "Windows Firewall Control (BidiFilt)", "Component Functionality", true,
            ["mpssvc", "BFE", "wfplwfs", "FirewallAPI"]),

        // Biometrics
        new("Biometrics", "Biometrics (Windows Hello, Yubikey)", "Component Functionality", false,
            ["biometricservice", "facerecognition", "hwsupport_smartcard", "passport"]),

        // Bluetooth
        new("Bluetooth", "Bluetooth", "Component Functionality", true,
            ["hwsupport_bluetooth", "bthserv", "BthAvrcpTg", "BthEnum", "RFCOMM"]),

        // WLAN
        new("WLAN", "WLAN (WiFi)", "Component Functionality", true,
            ["wlan", "WlanSvc", "wlansvc", "NativeWifi", "vwifi"]),

        // Windows Update
        new("WindowsUpdate", "Windows Update", "Component Functionality", true,
            ["wuauserv", "WindowsUpdate", "WaaSMedicSvc", "UsoSvc", "BITS", "deliveryoptimization", "musnotification"]),

        // Edge
        new("Edge", "Microsoft Edge", "Component Functionality", true,
            ["microsoft.microsoftedge.stable", "Microsoft.MicrosoftEdge", "edgehtml"]),

        // Microsoft Office support
        new("OfficeSupport", "Microsoft Office support", "Component Functionality", true,
            ["Office", "ClickToRun", "AppVClient"]),

        // Safe Mode
        new("SafeMode", "Safe Mode", "Component Functionality", true,
            ["SafeMode", "BootSafe"]),

        // Default Fonts (non-system)
        new("DefaultFonts", "Default Fonts (non-system)", "Component Functionality", false,
            ["font_calibri", "font_cambria", "font_consolas", "font_georgia", "font_verdana", "font_trebuchet"]),

        // Machine support (hardware targets)
        new("Machine_Host", "Host Machine (WINDOWS-PC)", "Machine support (Hardware lists)", true,
            []),
        new("Machine_HyperV", "Hyper-V VM", "Machine support (Hardware lists)", false,
            ["vmbus", "storvsc", "netvsc"]),
        new("Machine_Parallels", "Parallels VM", "Machine support (Hardware lists)", false,
            ["prl_"]),
        new("Machine_VirtualBox", "Virtual Box VM", "Machine support (Hardware lists)", false,
            ["VBox"]),
        new("Machine_VMware", "VMware VM", "Machine support (Hardware lists)", false,
            ["vmci", "vm3dmp", "vmhgfs"]),
    ];

    /// <summary>
    /// Components that must NEVER be removed regardless of user settings —
    /// Windows will fail to install or boot without these.
    /// </summary>
    private static readonly HashSet<string> AbsolutelyCritical = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core OS / Boot — these are non-negotiable
        "Microsoft-Windows-Foundation-Package",
        "Microsoft-Windows-Client-Features-Package",
        "Microsoft-Windows-CoreSystem",
        "Microsoft-Windows-ServicingStack",
        "Microsoft-Windows-WinPE-Package",
        "Microsoft-Windows-Setup",
        "Microsoft-Windows-SetupPlatform",
        "Microsoft-Windows-SetupQueue",
        "Microsoft-Windows-SetupCore",
        "Microsoft-Windows-PnpSysprep",
        "Microsoft-Windows-Registry-Engine",
        "Microsoft-Windows-Kernel",
        "ntfs", "volmgr", "partmgr", "disk",
        "lsass", "sam", "smss", "csrss", "wininit",
        "servicing", "servicingstack",
    };

    /// <summary>
    /// Checks if a component is absolutely critical (never removable even if user unchecks all protections).
    /// </summary>
    public static bool IsAbsolutelyCritical(string id, ComponentType type)
    {
        if (type == ComponentType.Package || type == ComponentType.OptionalFeature)
        {
            return AbsolutelyCritical.Any(c => id.Contains(c, StringComparison.OrdinalIgnoreCase));
        }
        return false;
    }

    /// <summary>
    /// Checks if a component is protected by the user's compatibility settings.
    /// </summary>
    public static bool IsProtectedByCompatibility(string id, ComponentType type, List<CompatibilityOption> options)
    {
        // First check absolute critical
        if (IsAbsolutelyCritical(id, type))
            return true;

        // Then check user-configured protections
        foreach (var option in options.Where(o => o.IsEnabled))
        {
            if (option.ProtectedPatterns.Any(pattern =>
                id.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates an entire removal list against compatibility settings.
    /// </summary>
    public static (List<RemovableComponent> safe, List<RemovableComponent> blocked) ValidateRemovalList(
        List<RemovableComponent> components, List<CompatibilityOption>? options = null)
    {
        options ??= GetDefaultCompatibilityOptions();

        var safe = new List<RemovableComponent>();
        var blocked = new List<RemovableComponent>();

        foreach (var comp in components.Where(c => c.IsSelected))
        {
            if (IsAbsolutelyCritical(comp.Id, comp.Type))
            {
                comp.IsProtected = true;
                blocked.Add(comp);
            }
            else if (IsProtectedByCompatibility(comp.Id, comp.Type, options))
            {
                comp.IsProtected = true;
                blocked.Add(comp);
            }
            else
            {
                safe.Add(comp);
            }
        }

        return (safe, blocked);
    }

    // Keep the old simple overload for backward compat
    public static (List<RemovableComponent> safe, List<RemovableComponent> blocked) ValidateRemovalList(
        List<RemovableComponent> components)
    {
        return ValidateRemovalList(components, GetDefaultCompatibilityOptions());
    }
}

/// <summary>
/// A user-configurable compatibility protection option.
/// When enabled, components matching its patterns are protected from removal.
/// This mirrors NTLite's "Compatibility" dialog.
/// </summary>
public class CompatibilityOption
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public bool IsEnabled { get; set; }
    public List<string> ProtectedPatterns { get; set; }

    public CompatibilityOption(string id, string name, string category, bool isEnabled, string[] patterns)
    {
        Id = id;
        Name = name;
        Category = category;
        IsEnabled = isEnabled;
        ProtectedPatterns = [.. patterns];
    }
}

public enum SafetyLevel
{
    /// <summary>Safe to remove — no impact on install or boot.</summary>
    Safe,
    /// <summary>Protected — user-configurable, currently protected by compatibility settings.</summary>
    Protected,
    /// <summary>Critical — NEVER removable. Will break install/boot.</summary>
    Critical
}
