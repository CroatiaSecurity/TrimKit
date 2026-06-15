# TrimKit — Constraints & Restrictions

## Technical Constraints

### DISM Limitations
- Requires administrator privileges (UAC elevation mandatory)
- Cannot mount recovery-compressed (LZMS) WIM/ESD directly — must convert first
- Cannot mount images on non-NTFS filesystems (exFAT, FAT32, ReFS unsupported for mount points)
- Single mount per WIM file at a time (DISM restriction)
- `/Cleanup-Wim` may fail if mount directories are locked by other processes

### ISO Mounting
- PowerShell `Mount-DiskImage` requires the ISO file to not be in use
- Mounted ISOs are read-only (CD-ROM filesystem) — must copy to NTFS for editing
- Some antivirus software blocks ISO mounting or auto-scanning delays detection

### File System Requirements
- Work folder MUST be on NTFS (required by DISM mount operations)
- Minimum 10 GB free space recommended (full Win11 ISO ≈ 5.5 GB + WIM extraction)
- Long path support recommended but not required

### Windows Version Support
- Minimum: Windows 10 1809 (build 17763)
- Recommended: Windows 11 22H2+ (for Mica backdrop, modern DISM features)
- Self-contained .NET 10 — no runtime dependency

## Safety Restrictions

### Never Removable (regardless of user settings)
- Boot infrastructure: bootmgr, winload, BCD, HAL, ACPI
- Kernel: ntoskrnl, csrss, smss, wininit, lsass
- Filesystem: ntfs.sys, volmgr, partmgr, disk.sys
- Servicing stack: TrustedInstaller, CBS, winsxs core
- Setup: setupplatform, setupcore, windeploy
- Registry engine

### Boot.wim Additional Restrictions
- WinPE core (wpeinit, wpeutil, startnet)
- Storage drivers (storahci, stornvme, usbstor)
- Display drivers (BasicDisplay, dxgkrnl)
- Input drivers (kbdclass, mouclass)
- Setup executables

### ISO File Restrictions
- All .efi files (UEFI boot)
- BCD stores (any file named "bcd")
- bootmgr / bootmgr.efi
- setup.exe (root and sources)
- boot.wim / install.wim / install.esd
- Core setup DLLs (setupplatform, wimgapi, etc.)

## Operational Constraints

### Concurrent Operations
- Only one Apply All operation at a time (IsBusy guard)
- Only one ISO workflow at a time
- Mount operations are exclusive per WIM file

### Cleanup Guarantees
- App close: always unmounts + deletes temp (no user choice — prevents orphaned mounts)
- Crash: best-effort sync cleanup via ProcessExit handler
- Task Manager kill: DISM Cleanup-Wim on next launch catches orphans

### Preset Compatibility
- NTLite preset import: component IDs are NTLite-specific, mapped by pattern matching (not 1:1 with DISM names)
- WinReducer preset import: category-based matching
- TrimKit native format (.wwp): full round-trip fidelity

## Performance Characteristics

| Operation | Typical Duration | Bottleneck |
|-----------|-----------------|------------|
| ISO copy to temp | 30-90s | Disk I/O (5-6 GB copy) |
| Recovery→WIM conversion | 2-10 min | CPU (LZMS decompression) |
| Edition extraction | 30-60s | Disk I/O (DISM export) |
| Mount WIM | 5-30s | Disk I/O |
| Component removal (50 items) | 2-5 min | DISM per-item overhead |
| WinSxS cleanup | 30-120s | DISM component analysis |
| Unmount + commit | 30-120s | WIM recompression |
| Full workflow end-to-end | 10-25 min | Combined |
