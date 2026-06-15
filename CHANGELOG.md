# Changelog

## 0.0.3 — 2026-06-15

### Added
- Full ISO workflow: Browse → mount (suppressed Explorer) → copy to NTFS temp → pick edition → extract → debloat
- Separate boot.wim and install.wim extraction into dedicated work folders
- Three-tier safety system: `SafetyGuard` (install.wim), `BootWimSafetyGuard` (boot.wim), `IsoFileSafetyGuard` (ISO files)
- ISO file-level debloating with safety (removes upgrade agent, appraiser, unused MUI, keeps bootloader/EFI/BCD)
- ComponentRemovalService wired into Apply All (Appx apps, capabilities, fonts, keyboards, languages, inbox drivers)
- WinSxS cleanup runs after component removal (StartComponentCleanup + ResetBase)
- DISM image cleanup (shrinks WIM after stripping)
- Service configuration in Apply All flow
- Interactive tab content: Apps, Fonts, Keyboards, Languages, Drivers, Features, Services, Components, Tweaks
- All tabs show checkbox lists bound to live data from mounted image
- Tab switching via sidebar (SelectedTabIndex binding)
- Graceful cleanup on app close (always dismounts, deletes temp folder)
- Emergency sync cleanup on crash/process exit (ProcessExit + UnhandledException handlers)
- `ForceCleanupSync()` — synchronous last-resort cleanup using raw dism.exe/PowerShell
- `IsBusy` guard on all long-running commands (prevents double-execution)
- `BrowseWimAsync` — proper async command (no fire-and-forget)
- Admin elevation via manifest (`requireAdministrator`)
- `installer/build.ps1` — automated build script (clean → publish → Inno Setup)
- `InverseBoolToVisibilityConverter` for state-aware tab content
- `IndexToVisibilityConverter` for tab panel switching

### Fixed
- Sidebar tabs now switch content panels (was showing only Source/ISO tab)
- Tab panels show correct state text based on whether image is mounted
- Fire-and-forget async calls replaced with proper awaits
- Assembly name in manifest corrected from "WimWitch" to "TrimKit"

### Changed
- Apply All now runs the full pipeline: packages → features → tweaks → drivers → component removal → services → WinSxS cleanup → DISM cleanup
- Both install.wim and boot.wim debloated in parallel with independent safety guards
- Close behavior: always cleans up without dialog (discards unsaved)

---

## 0.0.2 — 2026-06-14

### Added
- ISO mounting via Explorer shell verb (reliable drive letter assignment)
- Auto ESD→WIM conversion (detects recovery LZMS format)
- UUP dump and Microsoft download integration (in-app API + open-in-browser)
- Full autounattend.xml generator
- Customization service (wallpapers, branding, scripts, $OEM$, cursors)
- WinSxS cleanup service
- Component removal service (all NTLite/WinReducer removal categories)
- Service manager (offline registry manipulation)
- Image tools (edition management, WIM↔ESD, ISO building)
- Preset system with NTLite/WinReducer import and combine
- 100+ registry tweaks database
- SafetyGuard with 45+ compatibility protection areas
- Chrome-dark theme with Mica backdrop (Win11)

---

## 0.0.1 — 2026-06-12

- Initial release
- Basic WIM mount/unmount
- Package listing and removal
- Feature enable/disable
- Driver injection
