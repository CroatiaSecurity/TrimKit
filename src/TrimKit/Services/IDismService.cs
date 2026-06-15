using TrimKit.Models;

namespace TrimKit.Services;

public interface IDismService
{
    Task<List<WimImageInfo>> GetWimInfoAsync(string wimPath);
    Task MountImageAsync(string wimPath, int imageIndex, string mountPath, IProgress<int>? progress = null);
    Task UnmountImageAsync(string mountPath, bool commitChanges, IProgress<int>? progress = null);
    Task<List<WindowsPackage>> GetPackagesAsync(string mountPath);
    Task RemovePackageAsync(string mountPath, string packageName);
    Task<List<WindowsFeature>> GetFeaturesAsync(string mountPath);
    Task EnableFeatureAsync(string mountPath, string featureName);
    Task DisableFeatureAsync(string mountPath, string featureName);
    Task AddDriverAsync(string mountPath, string driverPath, bool recurse = true, bool forceUnsigned = false);
    Task<string> GetMountedImageStatus(string mountPath);
    Task CleanupMountsAsync();
}
