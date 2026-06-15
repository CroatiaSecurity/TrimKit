using TrimKit.Models;

namespace TrimKit.Services;

public interface IRegistryService
{
    Task ApplyTweakAsync(string mountPath, RegistryTweak tweak);
    Task LoadHiveAsync(string hivePath, string mountKey);
    Task UnloadHiveAsync(string mountKey);
    List<RegistryTweak> GetBuiltInTweaks();
}
