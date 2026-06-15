using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimKit.Models;
using TrimKit.Services;

namespace TrimKit.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDismService _dismService;
    private readonly IRegistryService _registryService;
    private readonly IPresetService _presetService;
    private readonly ILogService _logService;
    private readonly IIsoService _isoService;
    private readonly IWindowsServiceManager _serviceManager;
    private readonly IImageToolsService _imageToolsService;
    private readonly IUnattendService _unattendService;
    private readonly ICustomizationService _customizationService;

    [ObservableProperty] private string _wimFilePath = string.Empty;
    [ObservableProperty] private string _mountPath = string.Empty;
    [ObservableProperty] private bool _isMounted;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private WimImageInfo? _selectedImage;
    [ObservableProperty] private string _selectedTab = "Packages";

    public ObservableCollection<WimImageInfo> WimImages { get; } = [];
    public ObservableCollection<WindowsPackage> Packages { get; } = [];
    public ObservableCollection<WindowsFeature> Features { get; } = [];
    public ObservableCollection<RegistryTweak> RegistryTweaks { get; } = [];
    public ObservableCollection<string> DriverPaths { get; } = [];
    public ObservableCollection<OperationLog> LogEntries { get; } = [];

    public DownloadViewModel DownloadViewModel { get; }

    public MainViewModel(IDismService dismService, IRegistryService registryService,
        IPresetService presetService, ILogService logService, DownloadViewModel downloadViewModel,
        IIsoService isoService, IWindowsServiceManager serviceManager,
        IImageToolsService imageToolsService, IUnattendService unattendService,
        ICustomizationService customizationService)
    {
        _dismService = dismService;
        _registryService = registryService;
        _presetService = presetService;
        _logService = logService;
        _isoService = isoService;
        _serviceManager = serviceManager;
        _imageToolsService = imageToolsService;
        _unattendService = unattendService;
        _customizationService = customizationService;
        DownloadViewModel = downloadViewModel;

        _logService.LogAdded += OnLogAdded;

        // Load built-in registry tweaks
        foreach (var tweak in _registryService.GetBuiltInTweaks())
        {
            RegistryTweaks.Add(tweak);
        }

        // Default mount path
        MountPath = @"C:\TrimKitMount";
    }

    private void OnLogAdded(OperationLog log)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => LogEntries.Add(log));
    }

    [RelayCommand]
    private void BrowseWim()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "WIM Files (*.wim)|*.wim|ESD Files (*.esd)|*.esd|ISO Files (*.iso)|*.iso|All Files (*.*)|*.*",
            Title = "Select Windows Image File"
        };

        if (dialog.ShowDialog() == true)
        {
            var ext = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
            if (ext == ".iso")
            {
                _ = ExtractFromIsoAsync(dialog.FileName);
            }
            else if (ext == ".esd")
            {
                // ESD needs conversion to WIM for editing
                _ = ConvertEsdAndLoadAsync(dialog.FileName);
            }
            else
            {
                WimFilePath = dialog.FileName;
                _ = LoadWimInfoAsync();
            }
        }
    }

    private async Task ConvertEsdAndLoadAsync(string esdPath)
    {
        try
        {
            IsBusy = true;
            StatusText = "Converting ESD (recovery format) to WIM for editing...";
            _logService.Log(Models.LogLevel.Info, $"Converting: {Path.GetFileName(esdPath)}");

            var wimPath = Path.ChangeExtension(esdPath, ".wim");
            var progress = new Progress<(int percent, string status)>(p =>
            {
                ProgressValue = p.percent;
                StatusText = p.status;
            });

            await _imageToolsService.ConvertEsdToWimAsync(esdPath, wimPath, progress);

            if (File.Exists(wimPath))
            {
                WimFilePath = wimPath;
                await LoadWimInfoAsync();
                StatusText = "ESD converted — WIM ready for customization";
            }
            else
            {
                // Fall back to loading ESD directly
                WimFilePath = esdPath;
                await LoadWimInfoAsync();
                StatusText = "Warning: conversion failed, loaded as read-only ESD";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"ESD conversion failed: {ex.Message}";
            _logService.Log(Models.LogLevel.Error, ex.Message);
            // Still try to load it
            WimFilePath = esdPath;
            _ = LoadWimInfoAsync();
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }    private async Task ExtractFromIsoAsync(string isoPath)
    {
        try
        {
            IsBusy = true;
            StatusText = "Extracting install image from ISO...";

            var outputDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(isoPath) ?? "",
                System.IO.Path.GetFileNameWithoutExtension(isoPath) + "_extracted");

            var progress = new Progress<(int percent, string status)>(p =>
            {
                ProgressValue = p.percent;
                StatusText = p.status;
            });

            await _isoService.ExtractWimFromIsoAsync(isoPath, outputDir, progress);

            // Find the extracted WIM
            var wimPath = System.IO.Path.Combine(outputDir, "install.wim");
            if (File.Exists(wimPath))
            {
                WimFilePath = wimPath;
                await LoadWimInfoAsync();
                StatusText = "ISO extraction complete — install.wim ready";
            }
            else
            {
                // If still ESD (recovery format), convert to WIM
                var esdPath = System.IO.Path.Combine(outputDir, "install.esd");
                if (File.Exists(esdPath))
                {
                    StatusText = "Converting install.esd (recovery format) to install.wim...";
                    _logService.Log(Models.LogLevel.Info, "ESD is in recovery format — converting to WIM for editing...");

                    wimPath = System.IO.Path.Combine(outputDir, "install.wim");
                    await _imageToolsService.ConvertEsdToWimAsync(esdPath, wimPath, progress);

                    if (File.Exists(wimPath))
                    {
                        File.Delete(esdPath); // Remove the ESD, keep only WIM
                        WimFilePath = wimPath;
                        await LoadWimInfoAsync();
                        StatusText = "ESD converted to WIM — ready for customization";
                    }
                    else
                    {
                        // Conversion failed, use ESD directly (read-only)
                        WimFilePath = esdPath;
                        await LoadWimInfoAsync();
                        StatusText = "Warning: ESD conversion failed. Image loaded read-only.";
                        _logService.Log(Models.LogLevel.Warning, "ESD→WIM conversion failed. Editing may be limited.");
                    }
                }
                else
                {
                    StatusText = "No install image found in ISO";
                    _logService.Log(Models.LogLevel.Error, "No install.wim or install.esd found in the ISO");
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"ISO extraction failed: {ex.Message}";
            _logService.Log(Models.LogLevel.Error, $"ISO extraction failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    [RelayCommand]
    private void BrowseMountPath()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Mount Directory",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            MountPath = dialog.SelectedPath;
        }
    }

    private async Task LoadWimInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(WimFilePath) || !File.Exists(WimFilePath))
            return;

        try
        {
            IsBusy = true;
            StatusText = "Reading image file...";

            // Detect if this is actually a recovery-compressed ESD (even if named .wim)
            if (await IsRecoveryFormatAsync(WimFilePath))
            {
                StatusText = "Detected recovery/ESD compression — converting to standard WIM...";
                _logService.Log(LogLevel.Info, $"{Path.GetFileName(WimFilePath)} is in recovery (LZMS) format. Converting...");

                var convertedPath = Path.Combine(
                    Path.GetDirectoryName(WimFilePath)!,
                    Path.GetFileNameWithoutExtension(WimFilePath) + "_converted.wim");

                var progress = new Progress<(int percent, string status)>(p =>
                {
                    ProgressValue = p.percent;
                    StatusText = p.status;
                });

                await _imageToolsService.ConvertEsdToWimAsync(WimFilePath, convertedPath, progress);

                if (File.Exists(convertedPath) && new FileInfo(convertedPath).Length > 1024)
                {
                    WimFilePath = convertedPath;
                    _logService.Log(LogLevel.Success, "Converted to standard WIM format");
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "Conversion produced empty file — loading original (may be read-only)");
                }
            }

            StatusText = "Reading WIM file...";
            WimImages.Clear();
            var images = await _dismService.GetWimInfoAsync(WimFilePath);
            foreach (var img in images)
            {
                WimImages.Add(img);
            }

            if (WimImages.Count > 0)
                SelectedImage = WimImages[0];

            StatusText = $"Found {WimImages.Count} image(s) — ready to mount";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _logService.Log(LogLevel.Error, "Failed to read WIM file", ex.Message);
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    /// <summary>
    /// Checks if a WIM/ESD file uses recovery (LZMS) compression by parsing DISM output.
    /// Files in recovery format cannot be mounted for editing — they must be converted first.
    /// </summary>
    private async Task<bool> IsRecoveryFormatAsync(string filePath)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = $"/Get-WimInfo /WimFile:\"{filePath}\" /Index:1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = psi };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // DISM reports compression as "Recovery" for LZMS-compressed ESDs
            // Normal WIMs show "LZX" or "XPRESS" or "None"
            if (output.Contains("Recovery", StringComparison.OrdinalIgnoreCase) &&
                output.Contains("Compression", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Also check if DISM flat-out fails to read it (corrupted or wrong format)
            if (process.ExitCode != 0)
            {
                // Might be encrypted ESD — check error
                var error = await process.StandardError.ReadToEndAsync();
                if (error.Contains("recovery", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            // If we can't determine, assume it's fine
        }

        return false;
    }

    [RelayCommand]
    private async Task MountImageAsync()
    {
        if (SelectedImage == null || string.IsNullOrWhiteSpace(WimFilePath))
        {
            System.Windows.MessageBox.Show("Please select a WIM file and image index.", "TrimKit",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Mounting image...";
            var progress = new Progress<int>(p => ProgressValue = p);

            await _dismService.MountImageAsync(WimFilePath, SelectedImage.Index, MountPath, progress);

            IsMounted = true;
            StatusText = "Image mounted - loading packages and features...";

            await LoadPackagesAndFeaturesAsync();

            StatusText = "Image mounted and ready";
        }
        catch (Exception ex)
        {
            StatusText = $"Mount failed: {ex.Message}";
            _logService.Log(LogLevel.Error, "Mount failed", ex.Message);
            System.Windows.MessageBox.Show($"Failed to mount image:\n{ex.Message}", "TrimKit",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    private async Task LoadPackagesAndFeaturesAsync()
    {
        Packages.Clear();
        Features.Clear();

        var packages = await _dismService.GetPackagesAsync(MountPath);
        foreach (var pkg in packages)
            Packages.Add(pkg);

        var features = await _dismService.GetFeaturesAsync(MountPath);
        foreach (var feat in features)
            Features.Add(feat);
    }

    [RelayCommand]
    private async Task ApplyChangesAsync()
    {
        if (!IsMounted)
        {
            System.Windows.MessageBox.Show("No image is mounted.", "TrimKit",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            "Apply all selected changes to the mounted image?\n\nThis will:\n" +
            $"- Remove {Packages.Count(p => p.IsSelected)} package(s)\n" +
            $"- Modify {Features.Count(f => f.IsModified)} feature(s)\n" +
            $"- Apply {RegistryTweaks.Count(r => r.IsSelected)} registry tweak(s)\n" +
            $"- Add {DriverPaths.Count} driver path(s)",
            "Confirm Changes",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            IsBusy = true;
            var totalOps = Packages.Count(p => p.IsSelected) +
                           Features.Count(f => f.IsModified) +
                           RegistryTweaks.Count(r => r.IsSelected) +
                           DriverPaths.Count;
            var currentOp = 0;

            // Remove selected packages
            foreach (var pkg in Packages.Where(p => p.IsSelected).ToList())
            {
                StatusText = $"Removing: {pkg.DisplayName}";
                try
                {
                    await _dismService.RemovePackageAsync(MountPath, pkg.PackageName);
                    Packages.Remove(pkg);
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Could not remove {pkg.DisplayName}: {ex.Message}");
                }
                currentOp++;
                ProgressValue = (int)((double)currentOp / totalOps * 100);
            }

            // Apply feature changes
            foreach (var feat in Features.Where(f => f.IsModified).ToList())
            {
                StatusText = $"Modifying feature: {feat.FeatureName}";
                try
                {
                    if (feat.IsEnabled)
                        await _dismService.EnableFeatureAsync(MountPath, feat.FeatureName);
                    else
                        await _dismService.DisableFeatureAsync(MountPath, feat.FeatureName);

                    feat.OriginalState = feat.IsEnabled;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Could not modify {feat.FeatureName}: {ex.Message}");
                }
                currentOp++;
                ProgressValue = (int)((double)currentOp / totalOps * 100);
            }

            // Apply registry tweaks
            foreach (var tweak in RegistryTweaks.Where(r => r.IsSelected))
            {
                StatusText = $"Applying tweak: {tweak.Name}";
                try
                {
                    await _registryService.ApplyTweakAsync(MountPath, tweak);
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Could not apply {tweak.Name}: {ex.Message}");
                }
                currentOp++;
                ProgressValue = (int)((double)currentOp / totalOps * 100);
            }

            // Add drivers
            foreach (var driverPath in DriverPaths.ToList())
            {
                StatusText = $"Adding drivers from: {driverPath}";
                try
                {
                    await _dismService.AddDriverAsync(MountPath, driverPath);
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Could not add driver: {ex.Message}");
                }
                currentOp++;
                ProgressValue = (int)((double)currentOp / totalOps * 100);
            }

            StatusText = "All changes applied successfully";
            _logService.Log(LogLevel.Success, "All changes applied");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _logService.Log(LogLevel.Error, "Apply changes failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    [RelayCommand]
    private async Task UnmountAsync(object? parameter)
    {
        var commit = parameter is true or "True" or "true";
        if (!IsMounted)
            return;

        try
        {
            IsBusy = true;
            StatusText = commit ? "Saving and unmounting..." : "Discarding and unmounting...";
            var progress = new Progress<int>(p => ProgressValue = p);

            await _dismService.UnmountImageAsync(MountPath, commit, progress);

            IsMounted = false;
            Packages.Clear();
            Features.Clear();
            StatusText = "Image unmounted";
        }
        catch (Exception ex)
        {
            StatusText = $"Unmount failed: {ex.Message}";
            _logService.Log(LogLevel.Error, "Unmount failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    [RelayCommand]
    private void AddDriverPath()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Driver Folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            if (!DriverPaths.Contains(dialog.SelectedPath))
                DriverPaths.Add(dialog.SelectedPath);
        }
    }

    [RelayCommand]
    private void RemoveDriverPath(string path)
    {
        DriverPaths.Remove(path);
    }

    [RelayCommand]
    private async Task SavePresetAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "TrimKit Preset (*.wwp)|*.wwp",
            Title = "Save TrimKit Preset"
        };

        if (dialog.ShowDialog() != true)
            return;

        var preset = new Preset
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            SourceFormat = "TrimKit",
            RemoveList = Packages.Where(p => p.IsSelected).Select(p => new PresetComponent
            {
                Id = p.PackageName,
                Name = p.DisplayName,
                Category = "Package"
            }).ToList(),
            KeepList = Packages.Where(p => !p.IsSelected).Select(p => new PresetComponent
            {
                Id = p.PackageName,
                Name = p.DisplayName,
                Category = "Package"
            }).ToList(),
            FeatureChanges = Features.Where(f => f.IsModified).Select(f => new FeaturePreset
            {
                FeatureName = f.FeatureName,
                Enable = f.IsEnabled
            }).ToList(),
            RegistryTweaks = RegistryTweaks.Where(r => r.IsSelected).ToList(),
            DriverPaths = DriverPaths.ToList()
        };

        await _presetService.SavePresetAsync(preset, dialog.FileName);
        _logService.Log(LogLevel.Success, $"Preset saved: {preset.Name} (Remove: {preset.RemoveList.Count}, Keep: {preset.KeepList.Count})");
        StatusText = $"Preset saved: {preset.Name}";
    }

    [RelayCommand]
    private async Task LoadPresetAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "All Supported Presets|*.wwp;*.xml;*.wccf|TrimKit Preset (*.wwp)|*.wwp|NTLite Preset (*.xml)|*.xml|WinReducer Preset (*.wccf)|*.wccf|All Files (*.*)|*.*",
            Title = "Load Preset (TrimKit / NTLite / WinReducer)"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var preset = await _presetService.LoadPresetAsync(dialog.FileName);
            ApplyPresetToUI(preset);

            _logService.Log(LogLevel.Success,
                $"Preset loaded: {preset.Name} (Source: {preset.SourceFormat}, Remove: {preset.RemoveList.Count}, Keep: {preset.KeepList.Count})");
            StatusText = $"Preset loaded: {preset.Name} [{preset.SourceFormat}]";
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to load preset: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to load preset:\n{ex.Message}", "TrimKit",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CombinePresetsAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "All Supported Presets|*.wwp;*.xml;*.wccf|All Files (*.*)|*.*",
            Title = "Select Presets to Combine (hold Ctrl for multiple)",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true || dialog.FileNames.Length < 2)
        {
            StatusText = "Select at least 2 presets to combine";
            return;
        }

        try
        {
            var presets = new List<Preset>();
            foreach (var file in dialog.FileNames)
            {
                var preset = await _presetService.LoadPresetAsync(file);
                presets.Add(preset);
            }

            var combinedName = $"Combined_{DateTime.Now:yyyyMMdd_HHmm}";
            var combined = _presetService.CombinePresets(presets, combinedName);

            // Save the combined preset
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "TrimKit Preset (*.wwp)|*.wwp",
                Title = "Save Combined Preset",
                FileName = $"{combinedName}.wwp"
            };

            if (saveDialog.ShowDialog() == true)
            {
                await _presetService.SavePresetAsync(combined, saveDialog.FileName);
                _logService.Log(LogLevel.Success,
                    $"Combined {presets.Count} presets → {combined.Name} (Remove: {combined.RemoveList.Count}, Keep: {combined.KeepList.Count})");

                // Also apply to UI
                ApplyPresetToUI(combined);
                StatusText = $"Combined preset saved and applied: {combined.Name}";
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to combine presets: {ex.Message}");
        }
    }

    private void ApplyPresetToUI(Preset preset)
    {
        // Apply remove/keep lists to packages
        // Keep list takes priority — if a package is in Keep, it stays unselected for removal
        var keepIds = new HashSet<string>(preset.KeepList.Select(k => k.Id), StringComparer.OrdinalIgnoreCase);
        var removeIds = new HashSet<string>(preset.RemoveList.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);

        foreach (var pkg in Packages)
        {
            if (keepIds.Contains(pkg.PackageName))
                pkg.IsSelected = false; // Explicitly kept
            else if (removeIds.Contains(pkg.PackageName))
                pkg.IsSelected = true;  // Marked for removal
            // else: leave unchanged (not mentioned in preset)
        }

        // Apply feature changes
        foreach (var feat in Features)
        {
            var presetFeat = preset.FeatureChanges.FirstOrDefault(
                f => f.FeatureName.Equals(feat.FeatureName, StringComparison.OrdinalIgnoreCase));
            if (presetFeat != null)
                feat.IsEnabled = presetFeat.Enable;
        }

        // Apply registry tweak selections
        foreach (var tweak in RegistryTweaks)
            tweak.IsSelected = preset.RegistryTweaks.Any(r => r.Name == tweak.Name);

        // Apply driver paths
        DriverPaths.Clear();
        foreach (var path in preset.DriverPaths)
            DriverPaths.Add(path);
    }

    [RelayCommand]
    private async Task CleanupAsync()
    {
        try
        {
            IsBusy = true;
            StatusText = "Cleaning up abandoned mounts...";
            await _dismService.CleanupMountsAsync();
            StatusText = "Cleanup complete";
        }
        catch (Exception ex)
        {
            StatusText = $"Cleanup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
        _logService.Clear();
    }
}
