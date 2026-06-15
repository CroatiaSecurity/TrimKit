namespace TrimKit.Services;

/// <summary>
/// WinSxS (Windows Side-by-Side) cleanup service.
/// The WinSxS folder is the single largest folder in Windows (often 5-10 GB).
/// After component removal, orphaned manifests and assemblies remain in WinSxS
/// unless explicitly cleaned. This service handles:
///
/// 1. StartComponentCleanup — removes superseded updates (safe, recommended)
/// 2. ResetBase — removes all superseded versions permanently (prevents uninstall of updates)
/// 3. AnalyzeComponentStore — reports what can be cleaned
/// 4. Post-removal cleanup — removes orphaned WinSxS entries after component removal
///
/// IMPORTANT: If user opts to keep Windows Update operational, ResetBase is safe but
/// prevents rolling back updates. If WU is being removed entirely, full aggressive
/// cleanup is applied.
/// </summary>
public interface IWinSxsCleanupService
{
    Task<WinSxsAnalysis> AnalyzeAsync(string mountPath);
    Task CleanupAsync(string mountPath, WinSxsCleanupOptions options, IProgress<(int percent, string status)>? progress = null);
}

public class WinSxsAnalysis
{
    public long TotalSizeBytes { get; set; }
    public long SharedWithWindowsBytes { get; set; }
    public long BackupsAndDisabledBytes { get; set; }
    public long CacheAndTempBytes { get; set; }
    public long ReclaimableBytes { get; set; }

    public string TotalSizeDisplay => FormatSize(TotalSizeBytes);
    public string ReclaimableDisplay => FormatSize(ReclaimableBytes);

    private static string FormatSize(long bytes) =>
        bytes > 1024 * 1024 * 1024 ? $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB" :
        $"{bytes / (1024.0 * 1024.0):F0} MB";
}

public class WinSxsCleanupOptions
{
    /// <summary>
    /// Remove superseded update components (safe, saves ~1-3 GB).
    /// Equivalent to DISM /StartComponentCleanup.
    /// </summary>
    public bool StartComponentCleanup { get; set; } = true;

    /// <summary>
    /// Reset the base of superseded components (saves more space but
    /// prevents uninstalling updates). Equivalent to /ResetBase.
    /// Only recommended if Windows Update is being kept disabled.
    /// </summary>
    public bool ResetBase { get; set; }

    /// <summary>
    /// Remove orphaned manifests and assemblies left after component removal.
    /// This is the key operation that actually reduces WIM size after stripping.
    /// </summary>
    public bool RemoveOrphanedManifests { get; set; } = true;

    /// <summary>
    /// Remove backup copies of servicing stack components.
    /// </summary>
    public bool RemoveBackups { get; set; } = true;

    /// <summary>
    /// Whether Windows Update will remain operational.
    /// If false, more aggressive cleanup is applied.
    /// </summary>
    public bool KeepWindowsUpdateOperational { get; set; } = true;
}
