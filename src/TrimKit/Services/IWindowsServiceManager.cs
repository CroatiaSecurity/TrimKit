using TrimKit.Models;

namespace TrimKit.Services;

/// <summary>
/// Manages Windows services in an offline image — list, disable, enable, or set startup type.
/// Uses DISM and offline registry manipulation.
/// </summary>
public interface IWindowsServiceManager
{
    Task<List<WindowsServiceInfo>> GetServicesAsync(string mountPath);
    Task SetServiceStartTypeAsync(string mountPath, string serviceName, ServiceStartType startType);
    Task RemoveServiceAsync(string mountPath, string serviceName);
}

public class WindowsServiceInfo
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ServiceStartType StartType { get; set; }
    public ServiceStartType OriginalStartType { get; set; }
    public bool IsSelected { get; set; }
    public bool IsModified => StartType != OriginalStartType;
}

public enum ServiceStartType
{
    Boot = 0,
    System = 1,
    Automatic = 2,
    Manual = 3,
    Disabled = 4,
    Remove = 99
}
