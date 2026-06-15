using TrimKit.Models;

namespace TrimKit.Services;

/// <summary>
/// Comprehensive tweak database combining all known tweaks from NTLite, WinReducer, and DISM++.
/// Organized by category for the UI.
/// </summary>
public static class TweakDatabase
{
    public static List<RegistryTweak> GetAllTweaks()
    {
        var tweaks = new List<RegistryTweak>();
        tweaks.AddRange(GetPrivacyTweaks());
        tweaks.AddRange(GetTelemetryTweaks());
        tweaks.AddRange(GetPerformanceTweaks());
        tweaks.AddRange(GetNetworkTweaks());
        tweaks.AddRange(GetExplorerTweaks());
        tweaks.AddRange(GetUiTweaks());
        tweaks.AddRange(GetSecurityTweaks());
        tweaks.AddRange(GetUpdateTweaks());
        tweaks.AddRange(GetGamingTweaks());
        tweaks.AddRange(GetPowerTweaks());
        tweaks.AddRange(GetStartMenuTweaks());
        tweaks.AddRange(GetNotificationTweaks());
        tweaks.AddRange(GetDefenderTweaks());
        tweaks.AddRange(GetCortanaTweaks());
        tweaks.AddRange(GetEdgeTweaks());
        tweaks.AddRange(GetMiscTweaks());
        return tweaks;
    }

    private static List<RegistryTweak> GetPrivacyTweaks() =>
    [
        T("Disable Telemetry", "Turns off Windows diagnostic data collection entirely", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", RegistryValueType.DWord, 0),
        T("Disable Activity History", "Prevents Windows from collecting activity history", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\System", "EnableActivityFeed", RegistryValueType.DWord, 0),
        T("Disable Activity History Upload", "Stops uploading activity history to Microsoft", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\System", "UploadUserActivities", RegistryValueType.DWord, 0),
        T("Disable Advertising ID", "Disables the per-user advertising identifier", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", RegistryValueType.DWord, 1),
        T("Disable Location Tracking", "Turns off Windows location services system-wide", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", RegistryValueType.DWord, 1),
        T("Disable Camera Access", "Blocks app access to the camera", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessCamera", RegistryValueType.DWord, 2),
        T("Disable Microphone Access", "Blocks app access to the microphone", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessMicrophone", RegistryValueType.DWord, 2),
        T("Disable Contacts Access", "Blocks app access to contacts", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessContacts", RegistryValueType.DWord, 2),
        T("Disable Calendar Access", "Blocks app access to calendar", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessCalendar", RegistryValueType.DWord, 2),
        T("Disable Call History Access", "Blocks app access to call history", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessCallHistory", RegistryValueType.DWord, 2),
        T("Disable Email Access", "Blocks app access to email", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessEmail", RegistryValueType.DWord, 2),
        T("Disable Messaging Access", "Blocks app access to messaging", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessMessaging", RegistryValueType.DWord, 2),
        T("Disable Notification Access", "Blocks app access to notifications", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessNotifications", RegistryValueType.DWord, 2),
        T("Disable Account Info Access", "Blocks app access to account info", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessAccountInfo", RegistryValueType.DWord, 2),
        T("Disable Motion Data Access", "Blocks app access to motion data", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessMotion", RegistryValueType.DWord, 2),
        T("Disable Diagnostic Data Viewer", "Disables the diagnostic data viewer", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\DataCollection", "DisableDiagnosticDataViewer", RegistryValueType.DWord, 1),
        T("Disable Feedback Requests", "Stops Windows from asking for feedback", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", RegistryValueType.DWord, 1),
        T("Disable Inking & Typing Data", "Disables inking and typing data collection", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", RegistryValueType.DWord, 1),
        T("Disable Speech Recognition Online", "Prevents online speech recognition", "Privacy",
            "SOFTWARE", @"Policies\Microsoft\InputPersonalization", "AllowInputPersonalization", RegistryValueType.DWord, 0),
    ];

    private static List<RegistryTweak> GetTelemetryTweaks() =>
    [
        T("Disable CEIP", "Disables Customer Experience Improvement Program", "Telemetry",
            "SOFTWARE", @"Policies\Microsoft\SQMClient\Windows", "CEIPEnable", RegistryValueType.DWord, 0),
        T("Disable App Telemetry", "Disables application telemetry", "Telemetry",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppCompat", "AITEnable", RegistryValueType.DWord, 0),
        T("Disable Inventory Collector", "Stops inventory data collection", "Telemetry",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppCompat", "DisableInventory", RegistryValueType.DWord, 1),
        T("Disable Steps Recorder", "Disables the Problem Steps Recorder", "Telemetry",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppCompat", "DisableUAR", RegistryValueType.DWord, 1),
        T("Disable Error Reporting", "Disables Windows Error Reporting", "Telemetry",
            "SOFTWARE", @"Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", RegistryValueType.DWord, 1),
        T("Disable KMS Client Telemetry", "Disables KMS activation telemetry", "Telemetry",
            "SOFTWARE", @"Policies\Microsoft\Windows NT\CurrentVersion\Software Protection Platform", "NoGenTicket", RegistryValueType.DWord, 1),
        T("Disable License Telemetry", "Disables license telemetry", "Telemetry",
            "SOFTWARE", @"Policies\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", RegistryValueType.DWord, 0),
        T("Disable Connected User Experiences", "Disables Connected User Experiences and Telemetry service", "Telemetry",
            "SOFTWARE", @"Policies\Microsoft\Windows\DataCollection", "DisableOneSettingsDownloads", RegistryValueType.DWord, 1),
    ];

    private static List<RegistryTweak> GetPerformanceTweaks() =>
    [
        T("Disable Superfetch/SysMain", "Disables Superfetch memory caching service", "Performance",
            "SYSTEM", @"ControlSet001\Services\SysMain", "Start", RegistryValueType.DWord, 4),
        T("Disable Windows Search Indexer", "Disables background file indexing", "Performance",
            "SYSTEM", @"ControlSet001\Services\WSearch", "Start", RegistryValueType.DWord, 4),
        T("Disable Prefetch", "Disables prefetch file creation", "Performance",
            "SOFTWARE", @"Microsoft\Windows NT\CurrentVersion\Prefetcher", "EnablePrefetcher", RegistryValueType.DWord, 0),
        T("Disable Paging Executive", "Keeps kernel in RAM (improves responsiveness)", "Performance",
            "SYSTEM", @"ControlSet001\Control\Session Manager\Memory Management", "DisablePagingExecutive", RegistryValueType.DWord, 1),
        T("Large System Cache", "Enables large system cache for file operations", "Performance",
            "SYSTEM", @"ControlSet001\Control\Session Manager\Memory Management", "LargeSystemCache", RegistryValueType.DWord, 1),
        T("Optimize NTFS Memory Usage", "Reduces NTFS memory usage", "Performance",
            "SYSTEM", @"ControlSet001\Control\FileSystem", "NtfsMemoryUsage", RegistryValueType.DWord, 2),
        T("Disable Last Access Timestamp", "Disables NTFS last access time updates", "Performance",
            "SYSTEM", @"ControlSet001\Control\FileSystem", "NtfsDisableLastAccessUpdate", RegistryValueType.DWord, 2147483649),
        T("Disable 8.3 Name Creation", "Disables legacy 8.3 filename generation", "Performance",
            "SYSTEM", @"ControlSet001\Control\FileSystem", "NtfsDisable8dot3NameCreation", RegistryValueType.DWord, 1),
        T("Set Foreground Priority", "Prioritizes foreground applications", "Performance",
            "SOFTWARE", @"Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", RegistryValueType.DWord, 0),
        T("Disable Background Apps", "Prevents apps from running in background", "Performance",
            "SOFTWARE", @"Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", RegistryValueType.DWord, 2),
        T("Disable Startup Delay", "Removes startup application delay", "Performance",
            "SOFTWARE", @"Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", RegistryValueType.DWord, 0),
        T("Disable Hibernation File", "Removes hiberfil.sys (saves disk space)", "Performance",
            "SYSTEM", @"ControlSet001\Control\Power", "HibernateEnabled", RegistryValueType.DWord, 0),
        T("Optimize Visual Effects for Performance", "Disables animations for speed", "Performance",
            "SOFTWARE", @"Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", RegistryValueType.DWord, 2),
    ];

    private static List<RegistryTweak> GetNetworkTweaks() =>
    [
        T("Disable Nagle's Algorithm", "Reduces network latency for gaming/real-time", "Network",
            "SOFTWARE", @"Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", RegistryValueType.DWord, 0),
        T("Disable Network Throttling", "Removes network throttling limit", "Network",
            "SOFTWARE", @"Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", RegistryValueType.DWord, -1),
        T("Disable Auto-Tuning Level", "Sets network auto-tuning to normal", "Network",
            "SYSTEM", @"ControlSet001\Services\AFD\Parameters", "EnableDCA", RegistryValueType.DWord, 1),
        T("Disable WiFi Sense", "Disables WiFi Sense (auto-connect/share)", "Network",
            "SOFTWARE", @"Microsoft\WcmSvc\wifinetworkmanager\config", "AutoConnectAllowedOEM", RegistryValueType.DWord, 0),
        T("Disable Hotspot 2.0", "Disables Hotspot 2.0 network detection", "Network",
            "SOFTWARE", @"Microsoft\WlanSvc\AnqpCache", "OsuRegistrationStatus", RegistryValueType.DWord, 0),
        T("Disable Peer-to-Peer Updates", "Prevents Windows Update from using P2P", "Network",
            "SOFTWARE", @"Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", RegistryValueType.DWord, 0),
    ];

    private static List<RegistryTweak> GetExplorerTweaks() =>
    [
        T("Show File Extensions", "Displays file extensions in Explorer", "Explorer",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", RegistryValueType.DWord, 0),
        T("Show Hidden Files", "Shows hidden files and folders", "Explorer",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden", RegistryValueType.DWord, 1),
        T("Show Protected OS Files", "Shows system-protected files", "Explorer",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSuperHidden", RegistryValueType.DWord, 1),
        T("Launch Explorer to This PC", "Opens Explorer to This PC instead of Quick Access", "Explorer",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo", RegistryValueType.DWord, 1),
        T("Classic Right-Click Menu (Win11)", "Restores the full context menu in Windows 11", "Explorer",
            "SOFTWARE", @"Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", RegistryValueType.String, ""),
        T("Disable Thumbnail Cache", "Stops creating thumbs.db files", "Explorer",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisableThumbnailCache", RegistryValueType.DWord, 1),
        T("Disable Quick Access Recent Files", "Removes recent files from Quick Access", "Explorer",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", RegistryValueType.DWord, 0),
        T("Disable Quick Access Frequent Folders", "Removes frequent folders from Quick Access", "Explorer",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent", RegistryValueType.DWord, 0),
        T("Disable Sharing Wizard", "Removes the sharing wizard from context menu", "Explorer",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "SharingWizardOn", RegistryValueType.DWord, 0),
        T("Expand Navigation Pane to Current Folder", "Explorer nav pane follows selection", "Explorer",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "NavPaneExpandToCurrentFolder", RegistryValueType.DWord, 1),
        T("Disable Aero Shake", "Prevents Aero Shake minimize-all gesture", "Explorer",
            "SOFTWARE", @"Policies\Microsoft\Windows\Explorer", "NoWindowMinimizingShortcuts", RegistryValueType.DWord, 1),
    ];

    private static List<RegistryTweak> GetUiTweaks() =>
    [
        T("Disable Widgets (Win11)", "Removes the Widgets panel from taskbar", "UI",
            "SOFTWARE", @"Policies\Microsoft\Dsh", "AllowNewsAndInterests", RegistryValueType.DWord, 0),
        T("Disable Chat Icon (Win11)", "Removes Microsoft Teams Chat from taskbar", "UI",
            "SOFTWARE", @"Policies\Microsoft\Windows\Windows Chat", "ChatIcon", RegistryValueType.DWord, 3),
        T("Disable Copilot (Win11)", "Disables Windows Copilot AI assistant", "UI",
            "SOFTWARE", @"Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", RegistryValueType.DWord, 1),
        T("Disable Taskbar Search Box", "Hides the search box on taskbar", "UI",
            "SOFTWARE", @"Policies\Microsoft\Windows\Windows Search", "SearchboxTaskbarMode", RegistryValueType.DWord, 0),
        T("Disable Task View Button", "Hides the Task View button on taskbar", "UI",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton", RegistryValueType.DWord, 0),
        T("Disable Snap Assist", "Disables window snap suggestions", "UI",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "SnapAssist", RegistryValueType.DWord, 0),
        T("Disable Lock Screen Tips", "Removes tips/ads from the lock screen", "UI",
            "SOFTWARE", @"Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightFeatures", RegistryValueType.DWord, 1),
        T("Disable Suggested Content in Settings", "Removes suggestions in Settings app", "UI",
            "SOFTWARE", @"Policies\Microsoft\Windows\CloudContent", "DisableCloudOptimizedContent", RegistryValueType.DWord, 1),
        T("Disable Start Menu Suggestions", "Removes app suggestions in Start menu", "UI",
            "SOFTWARE", @"Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", RegistryValueType.DWord, 1),
        T("Taskbar Alignment Left (Win11)", "Moves taskbar icons to the left", "UI",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl", RegistryValueType.DWord, 0),
        T("Small Taskbar Icons", "Uses small icons on the taskbar", "UI",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarSmallIcons", RegistryValueType.DWord, 1),
        T("Disable Transparency Effects", "Turns off transparency for performance", "UI",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", RegistryValueType.DWord, 0),
        T("Enable Dark Mode", "Enables system-wide dark theme", "UI",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", RegistryValueType.DWord, 0),
    ];

    private static List<RegistryTweak> GetSecurityTweaks() =>
    [
        T("Disable Remote Desktop", "Blocks Remote Desktop connections", "Security",
            "SOFTWARE", @"Policies\Microsoft\Windows NT\Terminal Services", "fDenyTSConnections", RegistryValueType.DWord, 1),
        T("Disable Remote Assistance", "Disables Remote Assistance invitations", "Security",
            "SOFTWARE", @"Policies\Microsoft\Windows NT\Terminal Services", "fAllowUnsolicited", RegistryValueType.DWord, 0),
        T("Disable AutoRun/AutoPlay", "Disables AutoRun for all drives", "Security",
            "SOFTWARE", @"Policies\Microsoft\Windows\Explorer", "NoAutorun", RegistryValueType.DWord, 1),
        T("Disable Admin Shares", "Removes default administrative shares (C$, ADMIN$)", "Security",
            "SYSTEM", @"ControlSet001\Services\LanmanServer\Parameters", "AutoShareWks", RegistryValueType.DWord, 0),
        T("Disable SMBv1 Protocol", "Disables vulnerable SMBv1 protocol", "Security",
            "SYSTEM", @"ControlSet001\Services\LanmanServer\Parameters", "SMB1", RegistryValueType.DWord, 0),
        T("Disable NetBIOS over TCP/IP", "Disables NetBIOS (legacy protocol)", "Security",
            "SYSTEM", @"ControlSet001\Services\NetBT\Parameters", "EnableNetbIOS", RegistryValueType.DWord, 2),
        T("Disable LLMNR", "Disables Link-Local Multicast Name Resolution", "Security",
            "SOFTWARE", @"Policies\Microsoft\Windows NT\DNSClient", "EnableMulticast", RegistryValueType.DWord, 0),
        T("Disable WDigest Authentication", "Prevents plaintext password caching", "Security",
            "SYSTEM", @"ControlSet001\Control\SecurityProviders\WDigest", "UseLogonCredential", RegistryValueType.DWord, 0),
        T("Disable PowerShell v2", "Disables legacy PowerShell 2.0 engine", "Security",
            "SOFTWARE", @"Policies\Microsoft\Windows\PowerShell", "EnableScripts", RegistryValueType.DWord, 0),
    ];

    private static List<RegistryTweak> GetUpdateTweaks() =>
    [
        T("Disable Auto-Restart for Updates", "Prevents forced reboots after updates", "Updates",
            "SOFTWARE", @"Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", RegistryValueType.DWord, 1),
        T("Disable Automatic Updates", "Sets Windows Update to notify only (no auto-download)", "Updates",
            "SOFTWARE", @"Policies\Microsoft\Windows\WindowsUpdate\AU", "AUOptions", RegistryValueType.DWord, 2),
        T("Disable Update Delivery Optimization", "Stops P2P update sharing", "Updates",
            "SOFTWARE", @"Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", RegistryValueType.DWord, 0),
        T("Disable Driver Updates via Windows Update", "Prevents driver updates through WU", "Updates",
            "SOFTWARE", @"Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", RegistryValueType.DWord, 1),
        T("Disable Feature Update Deferrals", "Sets feature update deferral to 365 days", "Updates",
            "SOFTWARE", @"Policies\Microsoft\Windows\WindowsUpdate", "DeferFeatureUpdatesPeriodInDays", RegistryValueType.DWord, 365),
        T("Disable Windows Update Medic Service", "Disables WaaSMedicSvc (re-enables WU if disabled)", "Updates",
            "SYSTEM", @"ControlSet001\Services\WaaSMedicSvc", "Start", RegistryValueType.DWord, 4),
    ];

    private static List<RegistryTweak> GetGamingTweaks() =>
    [
        T("Disable Game DVR", "Disables background game recording", "Gaming",
            "SOFTWARE", @"Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", RegistryValueType.DWord, 0),
        T("Disable Game Bar", "Disables the Xbox Game Bar overlay", "Gaming",
            "SOFTWARE", @"Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", RegistryValueType.DWord, 0),
        T("Disable Game Mode", "Disables Windows Game Mode (can cause stutters)", "Gaming",
            "SOFTWARE", @"Microsoft\GameBar", "AutoGameModeEnabled", RegistryValueType.DWord, 0),
        T("GPU Priority for Gaming", "Sets GPU scheduling priority to 8 (gaming)", "Gaming",
            "SOFTWARE", @"Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", RegistryValueType.DWord, 8),
        T("CPU Priority for Gaming", "Sets gaming CPU priority to high (6)", "Gaming",
            "SOFTWARE", @"Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", RegistryValueType.DWord, 6),
        T("Disable Fullscreen Optimizations", "Disables DWM fullscreen optimizations globally", "Gaming",
            "SYSTEM", @"ControlSet001\Control\GraphicsDrivers", "HwSchMode", RegistryValueType.DWord, 2),
    ];

    private static List<RegistryTweak> GetPowerTweaks() =>
    [
        T("Disable USB Selective Suspend", "Prevents USB power saving disconnects", "Power",
            "SYSTEM", @"ControlSet001\Services\USB\DisableSelectiveSuspend", "DisableSelectiveSuspend", RegistryValueType.DWord, 1),
        T("Disable Power Throttling", "Prevents CPU power throttling for background apps", "Power",
            "SYSTEM", @"ControlSet001\Control\Power\PowerThrottling", "PowerThrottlingOff", RegistryValueType.DWord, 1),
        T("Disable Connected Standby", "Disables Modern Standby (prevents random wakes)", "Power",
            "SYSTEM", @"ControlSet001\Control\Power", "CsEnabled", RegistryValueType.DWord, 0),
        T("Disable Fast Startup", "Disables hybrid shutdown (can cause issues)", "Power",
            "SYSTEM", @"ControlSet001\Control\Session Manager\Power", "HiberbootEnabled", RegistryValueType.DWord, 0),
    ];

    private static List<RegistryTweak> GetStartMenuTweaks() =>
    [
        T("Disable Bing Search in Start", "Removes web/Bing results from Start menu search", "Start Menu",
            "SOFTWARE", @"Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", RegistryValueType.DWord, 1),
        T("Disable Start Menu Ads", "Removes suggested/promoted apps from Start", "Start Menu",
            "SOFTWARE", @"Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", RegistryValueType.DWord, 1),
        T("Disable Recent Apps in Start", "Removes recently added section", "Start Menu",
            "SOFTWARE", @"Policies\Microsoft\Windows\Explorer", "HideRecentlyAddedApps", RegistryValueType.DWord, 1),
        T("Disable Most Used Apps in Start", "Removes most used apps list", "Start Menu",
            "SOFTWARE", @"Policies\Microsoft\Windows\Explorer", "ShowOrHideMostUsedApps", RegistryValueType.DWord, 2),
    ];

    private static List<RegistryTweak> GetNotificationTweaks() =>
    [
        T("Disable Toast Notifications", "Turns off all toast notifications", "Notifications",
            "SOFTWARE", @"Policies\Microsoft\Windows\CurrentVersion\PushNotifications", "NoToastApplicationNotification", RegistryValueType.DWord, 1),
        T("Disable Lock Screen Notifications", "Hides notifications on lock screen", "Notifications",
            "SOFTWARE", @"Policies\Microsoft\Windows\CurrentVersion\PushNotifications", "NoToastApplicationNotificationOnLockScreen", RegistryValueType.DWord, 1),
        T("Disable Notification Center", "Disables the Action Center/Notification Center", "Notifications",
            "NTUSER", @"Software\Policies\Microsoft\Windows\Explorer", "DisableNotificationCenter", RegistryValueType.DWord, 1),
        T("Disable Tip Notifications", "Removes Windows Tips notifications", "Notifications",
            "SOFTWARE", @"Policies\Microsoft\Windows\CloudContent", "DisableSoftLanding", RegistryValueType.DWord, 1),
    ];

    private static List<RegistryTweak> GetDefenderTweaks() =>
    [
        T("Disable Windows Defender", "Completely disables Windows Defender antivirus", "Defender",
            "SOFTWARE", @"Policies\Microsoft\Windows Defender", "DisableAntiSpyware", RegistryValueType.DWord, 1),
        T("Disable Real-Time Protection", "Turns off real-time scanning", "Defender",
            "SOFTWARE", @"Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", RegistryValueType.DWord, 1),
        T("Disable Cloud Protection", "Disables cloud-delivered protection", "Defender",
            "SOFTWARE", @"Policies\Microsoft\Windows Defender\Spynet", "SpynetReporting", RegistryValueType.DWord, 0),
        T("Disable Sample Submission", "Prevents automatic sample submission", "Defender",
            "SOFTWARE", @"Policies\Microsoft\Windows Defender\Spynet", "SubmitSamplesConsent", RegistryValueType.DWord, 2),
        T("Disable SmartScreen", "Disables Windows SmartScreen filter", "Defender",
            "SOFTWARE", @"Policies\Microsoft\Windows\System", "EnableSmartScreen", RegistryValueType.DWord, 0),
        T("Disable SmartScreen for Edge", "Disables SmartScreen in Microsoft Edge", "Defender",
            "SOFTWARE", @"Policies\Microsoft\MicrosoftEdge\PhishingFilter", "EnabledV9", RegistryValueType.DWord, 0),
    ];

    private static List<RegistryTweak> GetCortanaTweaks() =>
    [
        T("Disable Cortana", "Completely disables Cortana assistant", "Cortana",
            "SOFTWARE", @"Policies\Microsoft\Windows\Windows Search", "AllowCortana", RegistryValueType.DWord, 0),
        T("Disable Cortana on Lock Screen", "Prevents Cortana above lock screen", "Cortana",
            "SOFTWARE", @"Policies\Microsoft\Windows\Windows Search", "AllowCortanaAboveLock", RegistryValueType.DWord, 0),
        T("Disable Search Highlights", "Removes search highlights/news from search", "Cortana",
            "SOFTWARE", @"Policies\Microsoft\Windows\Windows Search", "EnableDynamicContentInWSB", RegistryValueType.DWord, 0),
        T("Disable Web Search", "Disables web results in Windows Search", "Cortana",
            "SOFTWARE", @"Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", RegistryValueType.DWord, 1),
        T("Disable Search History", "Stops storing search history", "Cortana",
            "SOFTWARE", @"Policies\Microsoft\Windows\Windows Search", "AllowSearchToUseLocation", RegistryValueType.DWord, 0),
    ];

    private static List<RegistryTweak> GetEdgeTweaks() =>
    [
        T("Disable Edge First-Run Experience", "Skips Edge welcome/setup page", "Edge",
            "SOFTWARE", @"Policies\Microsoft\Edge", "HideFirstRunExperience", RegistryValueType.DWord, 1),
        T("Disable Edge Desktop Shortcut", "Prevents Edge from creating desktop shortcuts", "Edge",
            "SOFTWARE", @"Policies\Microsoft\EdgeUpdate", "CreateDesktopShortcutDefault", RegistryValueType.DWord, 0),
        T("Disable Edge as Default PDF Reader", "Prevents Edge from claiming PDF association", "Edge",
            "SOFTWARE", @"Policies\Microsoft\Edge", "DefaultBrowserSettingEnabled", RegistryValueType.DWord, 0),
        T("Disable Edge Background Running", "Stops Edge from running in background", "Edge",
            "SOFTWARE", @"Policies\Microsoft\Edge", "BackgroundModeEnabled", RegistryValueType.DWord, 0),
        T("Disable Edge Sidebar", "Removes the Edge sidebar", "Edge",
            "SOFTWARE", @"Policies\Microsoft\Edge", "HubsSidebarEnabled", RegistryValueType.DWord, 0),
    ];

    private static List<RegistryTweak> GetMiscTweaks() =>
    [
        T("Disable Reserved Storage", "Removes 7GB reserved storage for updates", "Misc",
            "SOFTWARE", @"Microsoft\Windows\CurrentVersion\ReserveManager", "ShippedWithReserves", RegistryValueType.DWord, 0),
        T("Disable Clipboard History", "Turns off clipboard history (Win+V)", "Misc",
            "SOFTWARE", @"Policies\Microsoft\Windows\System", "AllowClipboardHistory", RegistryValueType.DWord, 0),
        T("Verbose Boot Messages", "Shows detailed service status during boot", "Misc",
            "SOFTWARE", @"Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", RegistryValueType.DWord, 1),
        T("Disable Shake to Minimize", "Disables Aero Shake gesture", "Misc",
            "SOFTWARE", @"Policies\Microsoft\Windows\Explorer", "NoWindowMinimizingShortcuts", RegistryValueType.DWord, 1),
        T("Enable Long Paths", "Enables paths longer than 260 characters", "Misc",
            "SYSTEM", @"ControlSet001\Control\FileSystem", "LongPathsEnabled", RegistryValueType.DWord, 1),
        T("Disable NumLock on Boot", "Turns off NumLock at login screen", "Misc",
            "DEFAULT", @".DEFAULT\Control Panel\Keyboard", "InitialKeyboardIndicators", RegistryValueType.String, "0"),
        T("Enable NumLock on Boot", "Turns on NumLock at login screen", "Misc",
            "DEFAULT", @".DEFAULT\Control Panel\Keyboard", "InitialKeyboardIndicators", RegistryValueType.String, "2"),
        T("Disable Windows Ink Workspace", "Removes Windows Ink from taskbar", "Misc",
            "SOFTWARE", @"Policies\Microsoft\WindowsInkWorkspace", "AllowWindowsInkWorkspace", RegistryValueType.DWord, 0),
        T("Disable OneDrive Setup on Login", "Prevents OneDrive from auto-starting", "Misc",
            "SOFTWARE", @"Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", RegistryValueType.DWord, 1),
        T("Disable Cortana Button on Taskbar", "Hides Cortana taskbar button", "Misc",
            "NTUSER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCortanaButton", RegistryValueType.DWord, 0),
    ];

    private static RegistryTweak T(string name, string description, string category,
        string hivePath, string keyPath, string valueName, RegistryValueType valueType, object value)
    {
        return new RegistryTweak
        {
            Name = name,
            Description = description,
            Category = category,
            HivePath = hivePath,
            KeyPath = keyPath,
            ValueName = valueName,
            ValueType = valueType,
            Value = value
        };
    }
}
