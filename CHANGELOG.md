# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- Self-update dialog with changelog, progress bar, and 3 actions (Update Now / Skip Version / Remind Later)
- Progress callback for zip self-update downloads (% and speed)
- `.editorconfig` for consistent code style
- `Directory.Build.props` / `Directory.Build.targets` for centralized versioning and build props
- GitHub Actions: `build.yml` (CI with NuGet cache, code analysis, format check), `release.yml` (tag-based release + winget PR)
- Dependabot configuration for NuGet and GitHub Actions
- Architecture documentation (C4 diagrams in `docs/architecture.md`)
- ADR log in `docs/adr/README.md`
- Contributing guide in `docs/contributing.md`

### Changed
- SettingsViewModel: integrated SelfUpdateDialog before applying zip updates
- Localization: added 9 self-update dialog keys (ru/en) + typed Loc.cs properties
- Converters: added `IsZeroConverter` for indeterminate/determinate progress bar switching

### Fixed
- Converters.cs syntax (missing brace on StringToImageSourceConverter)

---

## [0.2.0] - 2024-10-28

### Added
- NavigationView shell with 6 pages: Tracked Apps, Updates, Winget, History, Settings, About
- TrackedAppsPage: list with icons, versions, source badges, check/remove actions
- UpdatesPage: filtered list (only apps with updates), toolbar with "Check Now" / "Update All"
- WingetPage: system winget upgrades with real-time progress (speed, bytes, %)
- HistoryPage: update log with timestamps, status, changelog links
- SettingsPage: theme, background checks, self-update, export/import stubs, diagnostics
- AboutPage: version, license, links, diagnostics
- SelfUpdateService: Velopack (Setup.exe) + Zip fallback with SHA256 verification
- IconExtractor: Win32 (SHGetFileInfo P/Invoke) + UWP (PackageManager) async icons
- OnboardingDialog: first-run welcome with feature highlights
- BackgroundMonitor: scheduled checks, dispatcher marshaling, single consolidated toast
- CatalogService: GitHub stars import, source management, pin/skip
- UpdateGate: per-app update locking (toast/card/auto-apply funnel)
- PendingUpdate journal: crash recovery (.old restore)
- InstalledAppScanner: ARP (Win32) + PackageManager (UWP) — 142 apps typical
- Fuzzy search (Levenshtein + trigram) — neural search removed
- Mica backdrop via 8 transparent brush keys
- Localization: Russian + English with typed Loc.cs keys
- 364 unit tests (AppWatch.Core.Tests)

### Changed
- AppTheme moved to Core.Domain.Models (break circular dependency)
- ThemeService reads from config, applies at startup
- MainViewModel cleanup: removed InstalledApps logic, added CheckAppAsync
- Package config stabilized to WindowsAppSDK 1.6.241112000 / WinUI 2.5.1 (Microsoft feed)

### Fixed
- XAML compiler locale bug workaround (en-US config patch in CI)
- RPC_E_WRONG_THREAD in background tasks (DispatcherQueue marshaling)
- Winget 0x80070057 catalog crash (WingetCatalogBrokenException handling)
- GC memory tuning (Conserve=5) — Private 126 MB, WS 193 MB
- JSON trailing comma corruption in ru.json/en.json
- App.g.i.cs generation (clean obj/bin before build)
- StringFormat → Display properties in ViewModels

---

## [0.1.0] - 2024-10-18

### Added
- Initial project structure (Clean Architecture)
- GitHub Releases update engine
- Winget update engine (basic)
- Installer update engine (ARP)
- Velopack self-update (Setup.exe)
- Basic WinUI 3 UI with Hub page
- JSON config store
- Serilog logging
- xUnit test project

---

## Version History

| Version | Date | Notes |
|---------|------|-------|
| 0.2.0 | 2024-10-28 | NavigationView, tracked apps, self-update dialog, winget progress |
| 0.1.0 | 2024-10-18 | Initial release |

---

## Release Notes Format

Each release includes:
- **GitHub Release**: binaries (Setup.exe, Portable.zip), SHA256SUMS.txt, changelog
- **winget**: Automated PR to microsoft/winget-pkgs (on stable tags)
- **Changelog**: This file updated with [Keep a Changelog](https://keepachangelog.com/) format