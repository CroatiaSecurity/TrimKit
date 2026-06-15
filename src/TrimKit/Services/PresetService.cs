using System.IO;
using System.Xml.Linq;
using TrimKit.Models;

namespace TrimKit.Services;

public class PresetService : IPresetService
{
    private static readonly XNamespace NtLiteNs = "urn:schemas-nliteos-com:pn.v1";

    #region TrimKit Native Format

    public async Task SavePresetAsync(Preset preset, string filePath)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XComment("TrimKit Preset - https://github.com/Gorstak/TrimKit"),
            new XElement("TrimKitPreset",
                new XAttribute("version", "1.0"),
                new XElement("Metadata",
                    new XElement("Name", preset.Name),
                    new XElement("Description", preset.Description),
                    new XElement("CreatedDate", preset.CreatedDate.ToString("o")),
                    new XElement("SourceFormat", preset.SourceFormat ?? "TrimKit"),
                    new XElement("TargetWindowsVersion", preset.TargetWindowsVersion ?? "")
                ),
                new XElement("RemoveList",
                    preset.RemoveList.Select(c => new XElement("Component",
                        new XAttribute("id", c.Id),
                        new XAttribute("name", c.Name),
                        new XAttribute("category", c.Category),
                        c.Source != null ? new XAttribute("source", c.Source) : null
                    ))
                ),
                new XElement("KeepList",
                    preset.KeepList.Select(c => new XElement("Component",
                        new XAttribute("id", c.Id),
                        new XAttribute("name", c.Name),
                        new XAttribute("category", c.Category),
                        c.Source != null ? new XAttribute("source", c.Source) : null
                    ))
                ),
                new XElement("Features",
                    preset.FeatureChanges.Select(f => new XElement("Feature",
                        new XAttribute("name", f.FeatureName),
                        new XAttribute("enable", f.Enable.ToString().ToLower())
                    ))
                ),
                new XElement("RegistryTweaks",
                    preset.RegistryTweaks.Select(r => new XElement("Tweak",
                        new XAttribute("name", r.Name),
                        new XAttribute("category", r.Category),
                        new XAttribute("hive", r.HivePath),
                        new XAttribute("key", r.KeyPath),
                        new XAttribute("valueName", r.ValueName),
                        new XAttribute("valueType", r.ValueType.ToString()),
                        new XAttribute("value", r.Value?.ToString() ?? "")
                    ))
                ),
                new XElement("Drivers",
                    preset.DriverPaths.Select(d => new XElement("Path", d))
                )
            )
        );

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await doc.SaveAsync(stream, SaveOptions.OmitDuplicateNamespaces, CancellationToken.None);
    }

    public async Task<Preset> LoadPresetAsync(string filePath)
    {
        var format = DetectFormat(filePath);

        return format switch
        {
            PresetFormat.NtLite => await ImportNtLitePresetAsync(filePath),
            PresetFormat.WinReducer => await ImportWinReducerPresetAsync(filePath),
            PresetFormat.TrimKit => await LoadTrimKitPresetAsync(filePath),
            _ => await TryAutoDetectAndLoadAsync(filePath)
        };
    }

    private async Task<Preset> LoadTrimKitPresetAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var doc = XDocument.Parse(content);
        var root = doc.Root!;

        var preset = new Preset();

        var metadata = root.Element("Metadata");
        if (metadata != null)
        {
            preset.Name = metadata.Element("Name")?.Value ?? Path.GetFileNameWithoutExtension(filePath);
            preset.Description = metadata.Element("Description")?.Value ?? "";
            preset.SourceFormat = metadata.Element("SourceFormat")?.Value ?? "TrimKit";
            preset.TargetWindowsVersion = metadata.Element("TargetWindowsVersion")?.Value;

            if (DateTime.TryParse(metadata.Element("CreatedDate")?.Value, out var dt))
                preset.CreatedDate = dt;
        }

        // Remove list
        var removeList = root.Element("RemoveList");
        if (removeList != null)
        {
            foreach (var elem in removeList.Elements("Component"))
            {
                preset.RemoveList.Add(new PresetComponent
                {
                    Id = elem.Attribute("id")?.Value ?? "",
                    Name = elem.Attribute("name")?.Value ?? "",
                    Category = elem.Attribute("category")?.Value ?? "",
                    Source = elem.Attribute("source")?.Value
                });
            }
        }

        // Keep list
        var keepList = root.Element("KeepList");
        if (keepList != null)
        {
            foreach (var elem in keepList.Elements("Component"))
            {
                preset.KeepList.Add(new PresetComponent
                {
                    Id = elem.Attribute("id")?.Value ?? "",
                    Name = elem.Attribute("name")?.Value ?? "",
                    Category = elem.Attribute("category")?.Value ?? "",
                    Source = elem.Attribute("source")?.Value
                });
            }
        }

        // Features
        var features = root.Element("Features");
        if (features != null)
        {
            foreach (var elem in features.Elements("Feature"))
            {
                preset.FeatureChanges.Add(new FeaturePreset
                {
                    FeatureName = elem.Attribute("name")?.Value ?? "",
                    Enable = bool.TryParse(elem.Attribute("enable")?.Value, out var e) && e
                });
            }
        }

        // Registry tweaks
        var tweaks = root.Element("RegistryTweaks");
        if (tweaks != null)
        {
            foreach (var elem in tweaks.Elements("Tweak"))
            {
                preset.RegistryTweaks.Add(new RegistryTweak
                {
                    Name = elem.Attribute("name")?.Value ?? "",
                    Category = elem.Attribute("category")?.Value ?? "",
                    HivePath = elem.Attribute("hive")?.Value ?? "",
                    KeyPath = elem.Attribute("key")?.Value ?? "",
                    ValueName = elem.Attribute("valueName")?.Value ?? "",
                    ValueType = Enum.TryParse<RegistryValueType>(elem.Attribute("valueType")?.Value, out var vt) ? vt : RegistryValueType.DWord,
                    Value = ParseRegistryValue(elem.Attribute("value")?.Value, vt)
                });
            }
        }

        // Drivers
        var drivers = root.Element("Drivers");
        if (drivers != null)
        {
            foreach (var elem in drivers.Elements("Path"))
            {
                if (!string.IsNullOrWhiteSpace(elem.Value))
                    preset.DriverPaths.Add(elem.Value);
            }
        }

        return preset;
    }

    #endregion

    #region NTLite Import

    public async Task<Preset> ImportNtLitePresetAsync(string xmlPath)
    {
        var content = await File.ReadAllTextAsync(xmlPath);
        var doc = XDocument.Parse(content);
        var root = doc.Root!;

        var preset = new Preset
        {
            Name = Path.GetFileNameWithoutExtension(xmlPath),
            SourceFormat = "NTLite",
            Description = $"Imported from NTLite preset: {Path.GetFileName(xmlPath)}"
        };

        // Parse AppInfo
        var appInfo = root.Element(NtLiteNs + "AppInfo") ?? root.Element("AppInfo");
        var imageInfo = root.Element(NtLiteNs + "ImageInfo") ?? root.Element("ImageInfo");
        if (imageInfo != null)
        {
            var version = imageInfo.Element(NtLiteNs + "Version") ?? imageInfo.Element("Version");
            preset.TargetWindowsVersion = version?.Value;
        }

        // Parse RemoveComponents — NTLite format: <c>componentId 'Display Name'</c>
        var removeComponents = root.Element(NtLiteNs + "RemoveComponents") ?? root.Element("RemoveComponents");
        if (removeComponents != null)
        {
            foreach (var c in removeComponents.Elements(NtLiteNs + "c").Concat(removeComponents.Elements("c")))
            {
                var parsed = ParseNtLiteComponent(c.Value);
                preset.RemoveList.Add(new PresetComponent
                {
                    Id = parsed.id,
                    Name = parsed.name,
                    Category = CategorizeNtLiteComponent(parsed.id),
                    Source = "NTLite"
                });
            }
        }

        // Parse Features
        var compatibility = root.Element(NtLiteNs + "Compatibility") ?? root.Element("Compatibility");
        if (compatibility != null)
        {
            var componentFeatures = compatibility.Element(NtLiteNs + "ComponentFeatures") ?? compatibility.Element("ComponentFeatures");
            if (componentFeatures != null)
            {
                foreach (var feat in componentFeatures.Elements(NtLiteNs + "Feature").Concat(componentFeatures.Elements("Feature")))
                {
                    var enabled = feat.Attribute("enabled")?.Value;
                    preset.FeatureChanges.Add(new FeaturePreset
                    {
                        FeatureName = feat.Value,
                        Enable = enabled == "yes"
                    });
                }
            }
        }

        return preset;
    }

    private static (string id, string name) ParseNtLiteComponent(string value)
    {
        // NTLite format: "componentId 'Display Name'" or just "componentId"
        var trimmed = value.Trim();
        var quoteStart = trimmed.IndexOf('\'');

        if (quoteStart > 0)
        {
            var id = trimmed[..quoteStart].Trim();
            var quoteEnd = trimmed.LastIndexOf('\'');
            var name = quoteEnd > quoteStart
                ? trimmed[(quoteStart + 1)..quoteEnd]
                : trimmed[(quoteStart + 1)..];
            return (id, name);
        }

        return (trimmed, trimmed);
    }

    private static string CategorizeNtLiteComponent(string id)
    {
        if (id.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            return "Apps";
        if (id.StartsWith("font_", StringComparison.OrdinalIgnoreCase))
            return "Fonts";
        if (id.StartsWith("kl-", StringComparison.OrdinalIgnoreCase))
            return "Keyboard Layouts";
        if (id.StartsWith("lang", StringComparison.OrdinalIgnoreCase))
            return "Languages";
        if (id.StartsWith("hwsupport_", StringComparison.OrdinalIgnoreCase))
            return "Hardware";
        if (id.StartsWith("driver_", StringComparison.OrdinalIgnoreCase))
            return "Drivers";
        if (id.Contains("32", StringComparison.OrdinalIgnoreCase) && !id.Contains("net"))
            return "32-bit Components";

        return "System";
    }

    #endregion

    #region WinReducer Import

    public async Task<Preset> ImportWinReducerPresetAsync(string wccfPath)
    {
        var content = await File.ReadAllTextAsync(wccfPath);
        var doc = XDocument.Parse(content);
        var root = doc.Root!;

        var preset = new Preset
        {
            Name = Path.GetFileNameWithoutExtension(wccfPath),
            SourceFormat = "WinReducer",
            Description = $"Imported from WinReducer preset: {Path.GetFileName(wccfPath)}"
        };

        // Parse version info
        var version = root.Element("Version")?.Value;
        var date = root.Element("Date")?.Value;
        if (DateTime.TryParse(date, out var dt))
            preset.CreatedDate = dt;

        // Parse Components
        var components = root.Element("Components");
        if (components != null)
        {
            foreach (var elem in components.Elements("Element"))
            {
                var category = elem.Attribute("Category")?.Value ?? "";
                var name = elem.Attribute("Name")?.Value ?? "";
                var selected = elem.Attribute("Selected")?.Value;
                var value = elem.Attribute("Value")?.Value ?? "";

                var isRemoveCategory = category.StartsWith("Remove", StringComparison.OrdinalIgnoreCase)
                    || category.StartsWith("EXPERIMENTAL - Remove", StringComparison.OrdinalIgnoreCase);
                var isFeatureCategory = category.Equals("Features", StringComparison.OrdinalIgnoreCase);
                var isServiceCategory = category.Equals("Services", StringComparison.OrdinalIgnoreCase);
                var isAppearanceCategory = category.Equals("Appearance", StringComparison.OrdinalIgnoreCase);

                // === SERVICES ===
                if (isServiceCategory && selected == "true" && !string.IsNullOrEmpty(value))
                {
                    if (int.TryParse(value, out var startType))
                    {
                        preset.ServiceChanges.Add(new ServicePreset
                        {
                            DisplayName = name,
                            ServiceName = WinReducerServiceNameToId(name),
                            StartType = startType
                        });
                    }
                    continue;
                }

                // === APPEARANCE (wallpapers) ===
                if (isAppearanceCategory && selected == "true" && !string.IsNullOrEmpty(value))
                {
                    preset.Wallpapers ??= new WallpaperPreset();
                    if (name.Contains("Desktop Wallpaper", StringComparison.OrdinalIgnoreCase))
                        preset.Wallpapers.DesktopWallpaperPath = value;
                    else if (name.Contains("Setup Screen", StringComparison.OrdinalIgnoreCase) && !name.Contains("Window"))
                        preset.Wallpapers.SetupScreenPath = value;
                    else if (name.Contains("Lockscreen", StringComparison.OrdinalIgnoreCase))
                        preset.Wallpapers.LockScreenPath = value;
                    continue;
                }

                // === FEATURES ===
                if (isFeatureCategory && selected == "true")
                {
                    preset.FeatureChanges.Add(new FeaturePreset
                    {
                        FeatureName = name,
                        Enable = value == "1"
                    });
                    continue;
                }

                // === REMOVE CATEGORIES ===
                if (isRemoveCategory)
                {
                    var component = new PresetComponent
                    {
                        Id = GenerateWinReducerId(category, name),
                        Name = name,
                        Category = CleanWinReducerCategory(category),
                        Source = "WinReducer"
                    };

                    if (selected == "true")
                        preset.RemoveList.Add(component);
                    else
                        preset.KeepList.Add(component);
                }
            }
        }

        return preset;
    }

    /// <summary>
    /// Maps WinReducer service display names to actual Windows service IDs.
    /// </summary>
    private static string WinReducerServiceNameToId(string displayName)
    {
        // WinReducer uses display names like "ActiveX Service" → real name is "AxInstSV"
        // Common mappings:
        return displayName switch
        {
            "ActiveX Service" => "AxInstSV",
            "AllJoyn Router Service" => "AJRouter",
            "App Readiness Service" => "AppReadiness",
            "Application Layer Gateway Service" => "ALG",
            "Assigned Access Manager Service" => "AssignedAccessManager",
            "Auto Time Zone Updater Service" => "tzautoupdate",
            "Background Intelligent Transfer Service" => "BITS",
            "Beep Service" => "Beep",
            "BitLocker Drive Encryption Service" => "BDESVC",
            "Block Level Backup Engine Service" => "wbengine",
            "Bluetooth Audio Gateway Service" => "BTAGService",
            "Bluetooth AVCTP Service" => "BthAvctpSvc",
            "Bluetooth Support Service" => "bthserv",
            "BranchCache Service" => "PeerDistSvc",
            "Capture Service" => "CaptureService",
            "Cellular Time Service" => "autotimesvc",
            "COM+ Event System Service" => "EventSystem",
            "COM+ System Application Service" => "COMSysApp",
            "Computer Browser Service" => "Browser",
            "Connected Devices Platform Service" => "CDPSvc",
            "Delivery Optimization Service" => "DoSvc",
            "Device Management Enrollment Service" => "DmEnrollmentSvc",
            "Diagnostic Policy Service" => "DPS",
            "Distributed Link Tracking Client Service" => "TrkWks",
            "DNS Client Service" => "Dnscache",
            "Fax Service" => "Fax",
            "Geolocation Service" => "lfsvc",
            "Microsoft Account Sign-in Assistant Service" => "wlidsvc",
            "Microsoft Store Install Service" => "InstallService",
            "Network Connected Devices Auto-Setup Service" => "NcdAutoSetup",
            "Parental Controls Service" => "WpcMonSvc",
            "Payment and NFC/SE Manager Service" => "SEMgrSvc",
            "Phone Service" => "PhoneSvc",
            "Portable Device Enumerator Service" => "WPDBusEnum",
            "Print Spooler Service" => "Spooler",
            "Remote Desktop Configuration Service" => "SessionEnv",
            "Remote Desktop Services" => "TermService",
            "Remote Registry Service" => "RemoteRegistry",
            "Retail Demo Service" => "RetailDemo",
            "Secondary Logon Service" => "seclogon",
            "Sensor Data Service" => "SensorDataService",
            "Sensor Monitoring Service" => "SensrSvc",
            "Sensor Service" => "SensorService",
            "Smart Card Service" => "SCardSvr",
            "SNMP Trap Service" => "SNMPTRAP",
            "Spot Verifier Service" => "svsvc",
            "Telephony Service" => "TapiSrv",
            "Touch Keyboard and Handwriting Panel Service" => "TabletInputService",
            "WalletService Service" => "WalletService",
            "Web Threat Defense Service" => "webthreatdefsvc",
            "Windows Biometric Service" => "WbioSrvc",
            "Windows Error Reporting Service" => "WerSvc",
            "Windows Event Collector Service" => "Wecsvc",
            "Windows Insider Service" => "wisvc",
            "Windows Media Player Network Sharing Service" => "WMPNetworkSvc",
            "Windows Mobile Hotspot Service" => "icssvc",
            "Windows Push Notifications System Service" => "WpnService",
            "Windows Remote Management (WS-Management) Service" => "WinRM",
            "Windows Search Service" => "WSearch",
            "Windows Update Service" => "wuauserv",
            "Xbox Accessory Management Service" => "XboxGipSvc",
            "Xbox Game Monitoring Service" => "xbgm",
            "Xbox Live Auth Manager Service" => "XblAuthManager",
            "Xbox Live Game Save Service" => "XblGameSave",
            "Xbox Live Networking Service" => "XboxNetApiSvc",
            _ => displayName.Replace(" Service", "").Replace(" ", "")
        };
    }

    private static string GenerateWinReducerId(string category, string name)
    {
        // Create a normalized ID from the WinReducer category + name
        var cleanCat = category
            .Replace("Remove - ", "")
            .Replace("EXPERIMENTAL - Remove - ", "")
            .Replace(" ", "")
            .ToLowerInvariant();
        var cleanName = name
            .Replace(" ", "_")
            .Replace("(", "")
            .Replace(")", "")
            .ToLowerInvariant();
        return $"wr_{cleanCat}_{cleanName}";
    }

    private static string CleanWinReducerCategory(string category)
    {
        return category
            .Replace("Remove - ", "")
            .Replace("EXPERIMENTAL - Remove - ", "")
            .Trim();
    }

    #endregion

    #region Combine Presets

    public Preset CombinePresets(IEnumerable<Preset> presets, string combinedName)
    {
        var combined = new Preset
        {
            Name = combinedName,
            Description = "Combined preset from multiple sources",
            SourceFormat = "Combined",
            CreatedDate = DateTime.Now
        };

        var removeSet = new Dictionary<string, PresetComponent>(StringComparer.OrdinalIgnoreCase);
        var keepSet = new Dictionary<string, PresetComponent>(StringComparer.OrdinalIgnoreCase);

        foreach (var preset in presets)
        {
            // Add all remove items
            foreach (var item in preset.RemoveList)
            {
                removeSet.TryAdd(item.Id, item);
            }

            // Add all keep items — Keep takes priority over Remove
            foreach (var item in preset.KeepList)
            {
                keepSet.TryAdd(item.Id, item);
                // If something is in the keep list, remove it from the remove list
                removeSet.Remove(item.Id);
            }

            // Merge features (last one wins)
            foreach (var feat in preset.FeatureChanges)
            {
                var existing = combined.FeatureChanges.FirstOrDefault(f => f.FeatureName == feat.FeatureName);
                if (existing != null)
                    existing.Enable = feat.Enable;
                else
                    combined.FeatureChanges.Add(feat);
            }

            // Merge registry tweaks (deduplicate by name)
            foreach (var tweak in preset.RegistryTweaks)
            {
                if (!combined.RegistryTweaks.Any(t => t.Name == tweak.Name))
                    combined.RegistryTweaks.Add(tweak);
            }

            // Merge driver paths
            foreach (var path in preset.DriverPaths)
            {
                if (!combined.DriverPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    combined.DriverPaths.Add(path);
            }
        }

        combined.RemoveList = removeSet.Values.ToList();
        combined.KeepList = keepSet.Values.ToList();

        return combined;
    }

    #endregion

    #region Format Detection

    public PresetFormat DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        switch (ext)
        {
            case ".wwp":
                return PresetFormat.TrimKit;
            case ".wccf":
                return PresetFormat.WinReducer;
            case ".xml":
                // Could be NTLite or WinReducer renamed — peek at content
                return DetectXmlFormat(filePath);
            default:
                return PresetFormat.Unknown;
        }
    }

    private static PresetFormat DetectXmlFormat(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath);
            var first2K = new char[2048];
            var read = reader.Read(first2K, 0, first2K.Length);
            var header = new string(first2K, 0, read);

            if (header.Contains("urn:schemas-nliteos-com:pn.v1") || header.Contains("<RemoveComponents"))
                return PresetFormat.NtLite;
            if (header.Contains("<WinReducerEX>") || header.Contains("winreducer.net") || header.Contains("<Packages>"))
                return PresetFormat.WinReducer;
            if (header.Contains("<TrimKitPreset"))
                return PresetFormat.TrimKit;
        }
        catch
        {
            // Fall through
        }

        return PresetFormat.Unknown;
    }

    private async Task<Preset> TryAutoDetectAndLoadAsync(string filePath)
    {
        // Try to auto-detect by reading file content
        var content = await File.ReadAllTextAsync(filePath);

        if (content.Contains("urn:schemas-nliteos-com:pn.v1") || content.Contains("<RemoveComponents"))
            return await ImportNtLitePresetAsync(filePath);
        if (content.Contains("<WinReducerEX>") || content.Contains("winreducer.net"))
            return await ImportWinReducerPresetAsync(filePath);

        throw new InvalidOperationException($"Could not detect preset format for: {Path.GetFileName(filePath)}");
    }

    #endregion

    #region Helpers

    private static object? ParseRegistryValue(string? value, RegistryValueType type)
    {
        if (string.IsNullOrEmpty(value))
            return type == RegistryValueType.String ? "" : 0;

        return type switch
        {
            RegistryValueType.DWord => int.TryParse(value, out var i) ? i : 0,
            RegistryValueType.QWord => long.TryParse(value, out var l) ? l : 0L,
            _ => value
        };
    }

    #endregion
}
