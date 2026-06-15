# TrimKit

Free, open-source Windows Image Customization Tool — a full NTLite/WinReducer/DISM++ alternative.

Built with C# / .NET 10 / WPF. Chrome-dark theme. NTLite-style sidebar navigation.

## Features

### ISO Workflow
- **Browse ISO → auto mount (suppressed Explorer) → copy to NTFS temp → pick edition → extract → debloat**
- Separate boot.wim and install.wim handling with independent safety guards
- ISO file-level debloating (removes upgrade agents, unused MUI, keeps bootloader/EFI)
- Auto recovery (LZMS) → standard WIM conversion
- UUP dump and Microsoft direct download links

### Component Removal
- **Provisioned Apps** (Store apps) — remove via DISM
- **Capabilities** (Features on Demand) — remove via DISM
- **Optional Features** — enable/disable
- **Fonts** — file-level removal with safety protection for system fonts
- **Keyboard Layouts** — remove unused kbd*.dll files
- **Languages** — remove MUI packs and locale data
- **Inbox Drivers** — remove from DriverStore
- **WinRE** — remove entirely or strip optional packages
- **WinSxS cleanup** — post-removal component store reduction

### 100+ Registry Tweaks
Organized by category: Privacy, Telemetry, Performance, Network, Explorer, UI, Security, Updates, Gaming, Power, Start Menu, Notifications, Defender, Cortana, Edge, Misc.

### Service Configuration
- List all services from offline image
- Change startup type (Auto/Manual/Disabled)
- Remove services entirely

### Image Tools
- Edition management (remove/export editions)
- WIM ↔ ESD conversion (both directions)
- Windows Update integration (.msu/.cab, SSU→LCU ordering)
- Boot.wim driver injection
- ISO building (via oscdimg from Windows ADK)
- Registry file (.reg) import into offline hives

### Customization
- Desktop/lock screen/setup wallpaper replacement
- Boot.wim wallpaper
- $OEM$ folder creation and file injection
- SetupComplete.cmd / FirstLogon scripts
- OEM branding (manufacturer, model, support URL, logo)
- Custom cursor schemes and theme files
- DISM cleanup + Compact OS

### Autounattend Generator
- Skip OOBE (no Microsoft account, no privacy questions)
- Bypass TPM/SecureBoot/RAM checks (Win11)
- Auto-create local admin account
- Disable telemetry, UAC, hibernation
- Inject directly into mounted image

### Preset System
- **Keep + Remove philosophy** — unlike NTLite/WinReducer, TrimKit has explicit Keep lists
- Import NTLite (.xml) and WinReducer (.wccf) presets
- Combine multiple presets (Keep always wins over Remove)
- Native TrimKit preset format (.wwp)

### Safety
- **Three-tier safety system:**
  - `SafetyGuard` — install.wim (45+ configurable protection areas)
  - `BootWimSafetyGuard` — boot.wim (stricter, boot-critical)
  - `IsoFileSafetyGuard` — ISO files (bootloader, EFI, BCD protected)
- Absolutely critical components never removable (boot, setup, kernel, filesystem)
- Graceful cleanup on close, crash, and process termination

## Requirements

- Windows 10/11 (64-bit)
- Self-contained — no .NET runtime install needed
- Administrator privileges (auto-elevated via manifest)
- Internet (for ISO download features only)
- Windows ADK (optional, for ISO building with oscdimg)

## Installation

Download `TrimKit-Setup-0.0.4.exe` from [Releases](https://github.com/CroatiaSecurity/TrimKit/releases).

## Building from Source

```bash
dotnet build src\TrimKit\TrimKit.csproj -c Release
dotnet publish src\TrimKit\TrimKit.csproj -c Release -r win-x64 --self-contained -o publish
```

Or use the build script (cleans, publishes, builds installer):

```powershell
cd installer
.\build.ps1
```

## Documentation

- [CHANGELOG.md](CHANGELOG.md) — version history
- [design.md](design.md) — architecture and data flow
- [constraints.md](constraints.md) — technical limitations and safety restrictions

## License

MIT
