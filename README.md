# WimWitch

A free, open-source Windows Image (WIM) customization tool built with C# / .NET 10 / WPF.

A free alternative to NTLite and WinReducer — with integrated ISO acquisition.

## Features

### ISO Acquisition (Acquire Tab)
- **UUP Dump integration** — Search and download any Windows build (Insider, stable, server) directly via the `api.uupdump.net` JSON API
- **Microsoft Direct Download** — Get official ISOs from `software-download.microsoft.com` using the same technique as Rufus/Fido (user-agent spoofing)
- **ISO extraction** — Mount any local `.iso` and automatically extract `install.wim`/`install.esd`
- **ESD to WIM conversion** — Automatically converts ESD files to WIM for offline editing via DISM
- **Resume support** — Skips already-downloaded files when resuming interrupted downloads

### Image Customization
- **Mount/Unmount WIM images** — Mount any WIM index for offline customization
- **Remove packages** — Strip bloatware and unwanted components from the image
- **Enable/Disable features** — Toggle Windows optional features on or off
- **Registry tweaks** — Apply privacy, performance, and UI tweaks to the offline image
- **Driver integration** — Inject driver folders into the mounted image
- **Presets** — Save and load your customization selections as `.wwp` files
- **Operation log** — Full log of every action taken for troubleshooting

## Data Sources & APIs Used

| Source | API/Method | Purpose |
|--------|-----------|---------|
| [UUP dump](https://uupdump.net/) | `api.uupdump.net` JSON API | Browse/search builds, get UUP download links |
| [Microsoft](https://www.microsoft.com/software-download/) | `software-download.microsoft.com` | Direct official ISO downloads |
| Windows DISM | `dism.exe` CLI | Mount/unmount WIM, packages, features, drivers, ESD→WIM |
| Windows Registry | `reg.exe` CLI | Offline hive load/unload, apply tweaks |
| Windows PowerShell | `Mount-DiskImage` | ISO mounting/unmounting |

## Built-in Registry Tweaks

| Category    | Tweak                        |
|-------------|------------------------------|
| Privacy     | Disable Telemetry            |
| Privacy     | Disable Activity History     |
| Privacy     | Disable Advertising ID       |
| Privacy     | Disable Location Tracking    |
| Performance | Disable Cortana              |
| Performance | Disable Search Highlights    |
| Performance | Disable Game DVR             |
| Explorer    | Show File Extensions         |
| Explorer    | Show Hidden Files            |
| Explorer    | Classic Right-Click Menu     |
| UI          | Disable Widgets              |
| UI          | Disable Chat Icon            |
| Security    | Disable Remote Desktop       |
| Security    | Disable AutoRun              |
| Updates     | Disable Auto-Restart         |

## Requirements

- Windows 10/11
- .NET 10 Runtime
- **Administrator privileges** (required for DISM and ISO mount operations)
- Internet connection (for ISO acquisition features only)

## Building

```bash
dotnet build src\WimWitch\WimWitch.csproj
```

## Publishing (single-file exe)

```bash
dotnet publish src\WimWitch\WimWitch.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Running

```bash
# Must run as Administrator
dotnet run --project src\WimWitch\WimWitch.csproj
```

Or build and run the exe directly (it will request elevation via manifest).

## Workflow

1. **Acquire** — Use the "Acquire ISO" tab to download a Windows ISO from UUP dump or Microsoft, or select a local `.iso`/`.wim` file
2. **Mount** — Select the image index and click Mount
3. **Customize** — Navigate tabs to:
   - Remove packages (bloatware)
   - Enable/disable features
   - Apply registry tweaks (privacy, performance, UI)
   - Add driver folders
4. **Apply** — Click "Apply Changes" to execute all modifications
5. **Save** — Click "Save & Unmount" to commit back to the WIM

## Presets

WimWitch's preset system is a key differentiator from NTLite and WinReducer:

### Keep + Remove Philosophy

Unlike NTLite/WinReducer which only let you flag items **to remove**, WimWitch presets have **two explicit lists**:

- **Remove List** — components/packages you want gone
- **Keep List** — components/packages you explicitly want preserved (these will never be removed, even if a combined preset tries to remove them)

Anything not in either list is left untouched. This makes presets safer and more predictable — you can share a "debloat" preset without worrying it'll nuke something critical the next person needs.

### Multi-Format Support

WimWitch can import presets from:

| Format | Extension | Notes |
|--------|-----------|-------|
| **WimWitch** | `.wwp` | Native XML format with Keep + Remove lists |
| **NTLite** | `.xml` | Reads `<RemoveComponents>` and `<Compatibility>` sections |
| **WinReducer** | `.wccf` | Reads `<Element Selected="true/false">` entries, maps unselected items to Keep list |

### Combining Presets

Click **Combine Presets** to merge 2+ presets (from any format) into a single WimWitch preset:
- Remove lists are merged (union)
- Keep lists are merged — **Keep always wins over Remove** (if a component appears in both, it's kept)
- Features, tweaks, and drivers are merged (last wins for conflicts)

### WimWitch Preset XML Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- WimWitch Preset -->
<WimWitchPreset version="1.0">
  <Metadata>
    <Name>My Custom Debloat</Name>
    <Description>Privacy-focused Windows 11 preset</Description>
    <SourceFormat>WimWitch</SourceFormat>
    <TargetWindowsVersion>Windows 11 Professional 24H2</TargetWindowsVersion>
  </Metadata>
  <RemoveList>
    <Component id="Microsoft.BingNews" name="BingNews" category="Apps" />
    <Component id="Microsoft.ZuneMusic" name="ZuneMusic" category="Apps" />
    <Component id="asimov" name="Telemetry Client" category="System" />
  </RemoveList>
  <KeepList>
    <Component id="Microsoft.WindowsCalculator" name="Calculator" category="Apps" />
    <Component id="Microsoft.WindowsStore" name="Windows Store" category="Apps" />
  </KeepList>
  <Features>
    <Feature name="Hyper-V" enable="true" />
    <Feature name="Windows-Defender-ApplicationGuard" enable="false" />
  </Features>
  <RegistryTweaks>
    <Tweak name="Disable Telemetry" category="Privacy" hive="SOFTWARE"
           key="Policies\Microsoft\Windows\DataCollection"
           valueName="AllowTelemetry" valueType="DWord" value="0" />
  </RegistryTweaks>
  <Drivers>
    <Path>C:\Drivers\WiFi</Path>
  </Drivers>
</WimWitchPreset>
```

## Architecture

```
WimWitch/
├── Models/            # Data models (WimImageInfo, Preset, RegistryTweak, etc.)
├── Services/          # Business logic
│   ├── DismService        — DISM CLI wrapper (mount, packages, features, drivers)
│   ├── RegistryService    — Offline registry hive manipulation
│   ├── UupDumpService     — UUP dump JSON API client
│   ├── MicrosoftDownloadService — MS direct download (Fido technique)
│   ├── IsoService         — ISO mount/extract via PowerShell
│   ├── PresetService      — Preset save/load (JSON)
│   └── LogService         — Operation logging
├── ViewModels/        # MVVM view models (CommunityToolkit.Mvvm)
├── Converters/        # WPF value converters
└── MainWindow.xaml    # UI (Catppuccin Mocha dark theme)
```

- **MVVM** with CommunityToolkit.Mvvm source generators
- **No external native dependencies** — uses built-in Windows tools (`dism.exe`, `reg.exe`, PowerShell)
- **Dark theme** (Catppuccin Mocha palette)

## Related Projects

- [UUP dump](https://uupdump.net/) — Community UUP download service
- [Rufus](https://rufus.ie/) — Bootable USB creator
- [Fido](https://github.com/pbatard/Fido) — ISO download script (PowerShell)
- [NTLite](https://www.ntlite.com/) — Commercial Windows customization
- [WinReducer](https://www.winreducer.net/) — Windows image reducer

## License

MIT
