# TrimKit — Design Document

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        WPF UI Layer                          │
│  MainWindow.xaml ← MVVM → MainViewModel / DownloadViewModel │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────┴────────────────────────────────┐
│                      Service Layer                            │
│  DismService │ IsoService │ ComponentRemovalService          │
│  RegistryService │ ImageToolsService │ PresetService         │
│  WindowsServiceManager │ UnattendService │ CustomizationSvc  │
│  WinSxsCleanupService │ IsoFileSafetyGuard │ SafetyGuard    │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────┴────────────────────────────────┐
│                   Windows System Layer                        │
│  dism.exe │ reg.exe │ PowerShell │ oscdimg.exe │ File I/O    │
└─────────────────────────────────────────────────────────────┘
```

## Key Design Decisions

### MVVM with CommunityToolkit.Mvvm
- Source-generated `[ObservableProperty]` and `[RelayCommand]` for minimal boilerplate
- No DI container — manual composition in `App.xaml.cs` (keeps it simple, no framework dependency)

### DISM-only approach (no third-party WIM libraries)
- All WIM/ESD operations go through `dism.exe` process calls
- Maximizes compatibility — DISM is always present on Windows 10/11
- No native DLL dependencies to manage across architectures

### Three-Tier Safety System
1. **SafetyGuard** (install.wim) — 45+ user-configurable protection areas + absolutely critical list
2. **BootWimSafetyGuard** (boot.wim) — stricter, protects PE boot infrastructure
3. **IsoFileSafetyGuard** (ISO files) — protects bootloader, EFI, BCD from file-level deletion

### ISO Workflow
1. Mount ISO via PowerShell `Mount-DiskImage` (suppress Explorer auto-open)
2. Copy entire ISO to NTFS temp folder (DISM can't work on read-only CD-ROM)
3. Unmount ISO immediately (user's original file stays untouched)
4. Debloat ISO file structure (remove upgrade agents, unused MUI)
5. Check/convert recovery compression
6. User picks edition → extract single edition to `Edition_Work/`
7. Extract boot.wim to `Boot_Work/`
8. Mount both for parallel debloating
9. Apply All → both images with independent safety guards
10. Cleanup: unmount, delete temp

### Preset Philosophy: Keep + Remove
Unlike NTLite/WinReducer which only mark items for removal, TrimKit supports explicit **Keep** lists. When combining presets, Keep always wins — this prevents accidental breakage when merging aggressive presets.

### Graceful Cleanup
- Window close → async dismount + temp folder delete
- Process exit (Task Manager kill) → sync `dism /Cleanup-Wim` + folder delete
- Crash → `AppDomain.UnhandledException` handler tries emergency cleanup
- After Apply All → automatic unmount + cleanup (work is done)

## Data Flow

```
User selects ISO
  → MountIsoSuppressedAsync (PowerShell Mount-DiskImage)
  → CopyIsoToWorkFolderAsync (byte-level copy with progress)
  → UnmountIsoAsync
  → IsoFileSafetyGuard.AnalyzeWorkFolder + ExecuteDebloatPlan
  → IsRecoveryCompressedAsync check
  → ConvertToNormalWimAsync (if needed)
  → GetWimInfoAsync → user picks edition
  → ExtractEditionAsync (DISM /Export-Image)
  → ExtractBootWimAsync (file copy)
  → MountImageAsync (both WIMs)
  → LoadPackagesAndFeaturesAsync (populates all UI collections)
  → User selects items in tabs
  → ApplyChangesAsync:
      install.wim: packages → features → tweaks → drivers → components → services → WinSxS → cleanup
      boot.wim: same pipeline with BootWimSafetyGuard filtering
  → GracefulCleanupAsync
```

## Module Responsibilities

| Module | Responsibility |
|--------|---------------|
| `DismService` | Mount/unmount, packages, features, drivers |
| `IsoService` | ISO mount/copy/unmount, recovery detection, edition extraction |
| `ComponentRemovalService` | Appx, capabilities, fonts, keyboards, languages, drivers, WinRE |
| `RegistryService` | Offline hive tweaks (SOFTWARE, SYSTEM, NTUSER) |
| `ImageToolsService` | WIM↔ESD, edition management, ISO building, registry import |
| `WindowsServiceManager` | Offline service startup type configuration |
| `UnattendService` | autounattend.xml generation and injection |
| `CustomizationService` | Wallpapers, branding, scripts, $OEM$, cleanup |
| `WinSxsCleanupService` | Post-removal WinSxS component store cleanup |
| `PresetService` | Load/save/combine presets (.wwp, .xml, .wccf) |
| `SafetyGuard` | Install.wim protection (critical + user-configurable) |
| `BootWimSafetyGuard` | Boot.wim protection (stricter PE requirements) |
| `IsoFileSafetyGuard` | ISO file-level protection (bootloader, EFI, BCD) |
