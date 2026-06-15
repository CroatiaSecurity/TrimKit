using System.IO;
using TrimKit.Models;

namespace TrimKit.Services;

/// <summary>
/// Safety guard for ISO file-level debloating.
/// After copying ISO content to the work folder, this determines which files
/// can be safely removed from the ISO structure to reduce final image size
/// without breaking bootability or installation.
///
/// Three tiers:
/// - Critical (NEVER delete): Boot files, EFI bootloader, BCD, setup.exe, winpe core
/// - Protected (user-configurable): Language resources, optional tools, diagnostics
/// - Safe (freely removable): Unneeded language packs, duplicate boot files, metadata
/// </summary>
public static class IsoFileSafetyGuard
{
    /// <summary>
    /// Files/directories that are ABSOLUTELY CRITICAL for a bootable, installable ISO.
    /// Removing any of these = unbootable or uninstallable media.
    /// Paths are relative to ISO root (case-insensitive).
    /// </summary>
    private static readonly string[] CriticalPaths =
    [
        // Boot infrastructure (BIOS)
        @"boot\bcd",
        @"boot\boot.sdi",
        @"boot\bootfix.bin",
        @"boot\etfsboot.com",
        @"boot\memtest.exe",

        // EFI bootloader (UEFI boot)
        @"efi\boot\bootx64.efi",
        @"efi\boot\bootia32.efi",
        @"efi\boot\bootaa64.efi",
        @"efi\microsoft\boot\bcd",
        @"efi\microsoft\boot\efisys.bin",
        @"efi\microsoft\boot\efisys_noprompt.bin",
        @"efi\microsoft\boot\memtest.efi",

        // Setup executables
        @"setup.exe",
        @"sources\setup.exe",

        // Core WIM files (will be handled separately by edition extraction)
        @"sources\boot.wim",
        @"sources\install.wim",
        @"sources\install.esd",

        // Setup infrastructure DLLs (required for Windows Setup to function)
        @"sources\setupplatform.dll",
        @"sources\setupcore.dll",
        @"sources\windlp.dll",
        @"sources\winsetup.dll",
        @"sources\wimgapi.dll",
        @"sources\wdscore.dll",
        @"sources\xmllite.dll",
        @"sources\cmisetup.dll",
        @"sources\wdsclient.dll",
        @"sources\setuphost.exe",
        @"sources\wpx.dll",

        // Autorun/media descriptor
        @"autorun.inf",
        @"bootmgr",
        @"bootmgr.efi",
    ];

    /// <summary>
    /// Patterns for files that are safe to remove (reduce ISO size significantly).
    /// These are language packs, optional tools, and metadata not needed for basic install.
    /// </summary>
    private static readonly string[] SafeToRemovePatterns =
    [
        // Language-specific setup resources (keep only needed language)
        @"sources\*.dll.mui",        // MUI resources (hundreds of files, often 100+ MB)
        @"sources\*_*.dll.mui",

        // Setup diagnostics and logging (not needed for install)
        @"sources\diagerr.xml",
        @"sources\diagwrn.xml",

        // Upgrade-specific files (not needed for clean install)
        @"sources\upgradeagent.dll",
        @"sources\upgradeagent.xml",
        @"sources\upgloader.dll",
        @"sources\mediasetup*.dll",

        // Compat assessment (only for in-place upgrade)
        @"sources\appraiser*.dll",
        @"sources\appraiser*.sdb",
        @"sources\compat*.dll",

        // Optional/unused font files in sources
        @"sources\*.ttf",

        // WinRE tools (optional, we already extract boot.wim separately)
        @"sources\recovery\",

        // Windows 10/11 media refresh (not needed)
        @"sources\inf\",

        // Panther setup logs placeholder
        @"sources\panther\",

        // ESD decryption keys (only for encrypted ESDs)
        @"sources\esd\",
    ];

    /// <summary>
    /// User-configurable ISO file protections.
    /// </summary>
    public static List<IsoFileProtection> GetDefaultProtections() =>
    [
        new("Iso_LanguagePacks", "Setup language resources (*.dll.mui)", true,
            "Hundreds of MUI files for setup UI localization. Removing them forces English-only setup.",
            [@"sources\*.dll.mui", @"sources\*_*.dll.mui", @"boot\*.dll.mui"]),

        new("Iso_UpgradeFiles", "In-place upgrade support", false,
            "Files needed for Windows upgrade (not clean install). Safe to remove for clean install media.",
            [@"sources\upgradeagent*", @"sources\upgloader*", @"sources\mediasetup*", @"sources\appraiser*", @"sources\compat*"]),

        new("Iso_DiagnosticTools", "Setup diagnostic tools", false,
            "Diagnostic XML and assessment files. Not needed for normal installation.",
            [@"sources\diag*", @"sources\hwcompat*"]),

        new("Iso_SetupFonts", "Setup fonts", false,
            "Font files included in sources. Usually duplicated inside boot.wim.",
            [@"sources\*.ttf"]),

        new("Iso_RecoveryTools", "Recovery tools folder", true,
            "Windows Recovery Environment tools. Needed if you want push-button reset after install.",
            [@"sources\recovery\"]),

        new("Iso_SxsCache", "Side-by-side component cache", false,
            "SxS store fragments used during feature installation. Can be re-downloaded via Windows Update.",
            [@"sources\sxs\"]),

        new("Iso_BootFonts", "Boot fonts (non-Latin)", false,
            "Font files for non-Latin boot menus (CJK, Arabic, etc). Safe to remove if using Latin languages only.",
            [@"boot\fonts\chs_boot.ttf", @"boot\fonts\cht_boot.ttf", @"boot\fonts\jpn_boot.ttf", @"boot\fonts\kor_boot.ttf", @"boot\fonts\malgun_boot.ttf"]),
    ];

    /// <summary>
    /// Checks if a file path (relative to ISO root) is absolutely critical.
    /// </summary>
    public static bool IsCritical(string relativePath)
    {
        var normalized = relativePath.Replace('/', '\\').TrimStart('\\');

        foreach (var critical in CriticalPaths)
        {
            if (normalized.Equals(critical, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Also protect all .efi files and BCD stores
        if (normalized.EndsWith(".efi", StringComparison.OrdinalIgnoreCase))
            return true;
        if (Path.GetFileName(normalized).Equals("bcd", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Gets a list of removable files from the work folder, respecting safety guards.
    /// Returns files categorized as safe/protected/critical.
    /// </summary>
    public static IsoDebloatPlan AnalyzeWorkFolder(string workFolder, List<IsoFileProtection>? protections = null)
    {
        protections ??= GetDefaultProtections();

        var plan = new IsoDebloatPlan();
        var allFiles = Directory.GetFiles(workFolder, "*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            var relativePath = Path.GetRelativePath(workFolder, file).Replace('/', '\\');
            var fileInfo = new FileInfo(file);

            if (IsCritical(relativePath))
            {
                plan.Critical.Add(new IsoFileEntry(relativePath, fileInfo.Length, IsoFileSafety.Critical));
                continue;
            }

            // Check if file matches any enabled protection
            var isProtected = false;
            foreach (var protection in protections.Where(p => p.IsEnabled))
            {
                if (MatchesPatterns(relativePath, protection.Patterns))
                {
                    plan.Protected.Add(new IsoFileEntry(relativePath, fileInfo.Length, IsoFileSafety.Protected, protection.Name));
                    isProtected = true;
                    break;
                }
            }

            if (!isProtected)
            {
                // Check against safe-to-remove patterns (only if no protection matched)
                foreach (var protection in protections.Where(p => !p.IsEnabled))
                {
                    if (MatchesPatterns(relativePath, protection.Patterns))
                    {
                        plan.SafeToRemove.Add(new IsoFileEntry(relativePath, fileInfo.Length, IsoFileSafety.Safe, protection.Name));
                        isProtected = true; // Mark as handled
                        break;
                    }
                }

                if (!isProtected)
                {
                    // File doesn't match any pattern — it's kept by default (unknown = keep)
                    plan.Kept.Add(new IsoFileEntry(relativePath, fileInfo.Length, IsoFileSafety.Kept));
                }
            }
        }

        return plan;
    }

    /// <summary>
    /// Executes the debloat plan — deletes files marked as SafeToRemove.
    /// Returns total bytes freed.
    /// </summary>
    public static long ExecuteDebloatPlan(string workFolder, IsoDebloatPlan plan, ILogService logService, IProgress<(int percent, string status)>? progress = null)
    {
        long bytesFreed = 0;
        var toRemove = plan.SafeToRemove;

        if (toRemove.Count == 0)
        {
            logService.Log(LogLevel.Info, "No ISO files to debloat (all protected or critical)");
            return 0;
        }

        logService.Log(LogLevel.Info, $"ISO debloat: removing {toRemove.Count} files ({toRemove.Sum(f => f.Size) / (1024.0 * 1024.0):F1} MB)...");

        for (int i = 0; i < toRemove.Count; i++)
        {
            var entry = toRemove[i];
            var fullPath = Path.Combine(workFolder, entry.RelativePath);

            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    bytesFreed += entry.Size;
                }
            }
            catch
            {
                // Skip files that can't be deleted (in use, permissions)
            }

            if (i % 50 == 0 || i == toRemove.Count - 1)
            {
                var pct = (int)((i + 1.0) / toRemove.Count * 100);
                progress?.Report((pct, $"ISO debloat: [{i + 1}/{toRemove.Count}] {bytesFreed / (1024.0 * 1024.0):F1} MB freed"));
            }
        }

        // Clean up empty directories
        CleanEmptyDirectories(workFolder);

        logService.Log(LogLevel.Success, $"ISO debloat complete: {toRemove.Count} files removed, {bytesFreed / (1024.0 * 1024.0):F1} MB freed");
        return bytesFreed;
    }

    private static bool MatchesPatterns(string relativePath, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var normalizedPattern = pattern.Replace('/', '\\').TrimStart('\\');

            // Directory pattern (ends with \)
            if (normalizedPattern.EndsWith('\\'))
            {
                if (relativePath.StartsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase))
                    return true;
                continue;
            }

            // Wildcard matching
            if (normalizedPattern.Contains('*'))
            {
                var dir = Path.GetDirectoryName(normalizedPattern) ?? "";
                var filePattern = Path.GetFileName(normalizedPattern);
                var fileDir = Path.GetDirectoryName(relativePath) ?? "";

                // Check if directory matches
                if (!string.IsNullOrEmpty(dir) && !fileDir.Equals(dir, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Simple wildcard check
                var fileName = Path.GetFileName(relativePath);
                if (WildcardMatch(fileName, filePattern))
                    return true;
            }
            else
            {
                // Exact match
                if (relativePath.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool WildcardMatch(string input, string pattern)
    {
        // Simple wildcard: *.ext or prefix*suffix
        if (pattern == "*") return true;
        if (pattern.StartsWith('*') && pattern.EndsWith('*'))
            return input.Contains(pattern[1..^1], StringComparison.OrdinalIgnoreCase);
        if (pattern.StartsWith('*'))
            return input.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        if (pattern.EndsWith('*'))
            return input.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        if (pattern.Contains('*'))
        {
            var parts = pattern.Split('*', 2);
            return input.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase) &&
                   input.EndsWith(parts[1], StringComparison.OrdinalIgnoreCase);
        }
        return input.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanEmptyDirectories(string root)
    {
        foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).Reverse())
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch { }
        }
    }
}

/// <summary>
/// User-configurable ISO file protection rule.
/// </summary>
public class IsoFileProtection
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsEnabled { get; set; }
    public string Description { get; set; }
    public string[] Patterns { get; set; }

    public IsoFileProtection(string id, string name, bool isEnabled, string description, string[] patterns)
    {
        Id = id;
        Name = name;
        IsEnabled = isEnabled;
        Description = description;
        Patterns = patterns;
    }
}

public class IsoDebloatPlan
{
    public List<IsoFileEntry> Critical { get; } = [];
    public List<IsoFileEntry> Protected { get; } = [];
    public List<IsoFileEntry> SafeToRemove { get; } = [];
    public List<IsoFileEntry> Kept { get; } = [];

    public long TotalSavings => SafeToRemove.Sum(f => f.Size);
    public string TotalSavingsDisplay => $"{TotalSavings / (1024.0 * 1024.0):F1} MB";
}

public class IsoFileEntry
{
    public string RelativePath { get; set; }
    public long Size { get; set; }
    public IsoFileSafety Safety { get; set; }
    public string? ProtectionName { get; set; }

    public IsoFileEntry(string relativePath, long size, IsoFileSafety safety, string? protectionName = null)
    {
        RelativePath = relativePath;
        Size = size;
        Safety = safety;
        ProtectionName = protectionName;
    }
}

public enum IsoFileSafety
{
    Critical,
    Protected,
    Safe,
    Kept
}
