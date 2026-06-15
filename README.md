# TrimKit

Free, open-source Windows Image Customization Tool — a full NTLite/WinReducer/DISM++ alternative.

Built with C# / .NET 10 / WPF. Chrome-dark theme. NTLite-style sidebar navigation.

## Features

### ISO Acquisition
- **UUP dump integration** — browse retail Windows 10/11 builds, select edition + language, downloads converter package (aria2c + wimlib) and builds a proper bootable ISO automatically
- **Microsoft Direct Download** — get official ISOs from Microsoft's servers
- **Save As dialog** — choose exactly where to save your ISO
- **Skip cumulative update** — saves ~3 GB by excluding the LCU package
- **Auto ESD→WIM conversion** — detects recovery-format ESDs (even if renamed to .wim) and converts to editable WIM format

### Component Removal
- **Provisioned Apps** (Store apps) — remove via DISM
- **Capabilities** (Features on Demand) — remove via DISM  
- **Optional Features** — enable/disable
- **Fonts** — file-level removal with safety protection for system fonts
- **Keyboard Layouts** — remove unused kbd*.dll files
- **Languages** — remove MUI packs and locale data
- **Inbox Drivers** — remove from DriverStore
- **WinRE** — remove entirely or strip optional packages

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
- Apply changes to ALL editions automatically

### Customization
- Desktop/lock screen/setup wallpaper replacement
- Boot.wim wallpaper
- $OEM$ folder creation and file injection
- SetupComplete.cmd / FirstLogon scripts
- OEM branding (manufacturer, model, support URL, logo)
- Custom cursor schemes and theme files
- Default user profile configuration
- DISM cleanup + Compact OS

### Autounattend Generator
- Skip OOBE (no Microsoft account, no privacy questions)
- Bypass TPM/SecureBoot/RAM checks (Win11)
- Auto-create local admin account
- Set locale, timezone, computer name
- Disable telemetry, UAC, hibernation
- Custom first-logon commands
- Inject directly into mounted image

### Preset System
- **Keep + Remove philosophy** — unlike NTLite/WinReducer, TrimKit has explicit Keep lists
- Import NTLite (.xml) and WinReducer (.wccf) presets
- Combine multiple presets (Keep always wins over Remove)
- Native TrimKit preset format (.wwp) with full XML structure

### Safety
- **NTLite-style Compatibility protections** — 45+ configurable functional areas (Apps, Printing, USB, WLAN, Bluetooth, Edge, etc.)
- **Absolutely critical components** never removable (boot, setup, kernel, filesystem)
- **WinSxS cleanup** after removal for maximum size reduction
- **Recovery format detection** — auto-converts recovery ESDs even if renamed to .wim

## Requirements

- Windows 10/11 (64-bit)
- Self-contained — no .NET runtime install needed
- Administrator privileges (for DISM/mount operations)
- Internet (for ISO download features only)
- Windows ADK (optional, for ISO building with oscdimg)

## Installation

Download `TrimKit-Setup-0.0.1.exe` from [Releases](https://github.com/CroatiaSecurity/TrimKit/releases).

## Building from Source

```bash
dotnet build src\TrimKit\TrimKit.csproj -c Release
dotnet publish src\TrimKit\TrimKit.csproj -c Release -r win-x64 --self-contained -o publish
```

## License

MIT
