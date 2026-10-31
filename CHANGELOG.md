# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [0.2.1] - 2026-10-31

### Added
- **Smart GitHub Repository Icon Discovery**: Automatic heuristic discovery of app icons directly from GitHub repositories (`assets/icon.png`, `logo.png`, `icon.ico`, etc.) using zero-quota parallel HEAD requests to `raw.githubusercontent.com`.
- **GitHub Stars Import Access**: Promoted "Import from GitHub Stars" button directly to the `TrackedAppsPage` toolbar.
- **Source Filtering & Sorting**: Added source filter chips (`GitHub`, `WinGet`, `External`) and "By Source" sort option to manage large collections of tracked software effortlessly.
- **Development Standards Files**: Root `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md` (Contributor Covenant v2.1), GitHub issue forms (`bug_report.yml`, `feature_request.yml`), and `PULL_REQUEST_TEMPLATE.md`.
- **Refined .gitignore**: Comprehensive exclusions for .NET 10, Windows App SDK, Visual Studio 2022/2026, JetBrains Rider, and diagnostic log artifacts.

### Fixed
- **Win32 Executable Icon Extraction**: Removed `SHGFI_USEFILEATTRIBUTES` flag in `IconExtractor.cs` which previously caused Windows Shell to return generic blank `.exe` placeholders instead of actual binary icons. Added fallback to `System.Drawing.Icon.ExtractAssociatedIcon`.
- **System Scanner Empty List Bug**: Changed `OnlyCatalogMatches` default from `true` to `false` in `SystemScannerDialogViewModel.cs`, resolving the bug where hundreds of installed apps were hidden by the empty seed catalog filter.
- **Icon Display Priority**: Local `.exe` icon is now prioritized over repository author avatars in `TrackedAppItem.cs`.
- **UI Dispatcher Queue Fallback**: Ensured background thread icon loading safely falls back to `App.MainWindow.DispatcherQueue`.

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
| 0.2.1 | 2026-10-31 | Smart repo icons, Win32 icon fix, scanner default fix, source chips, standards files |
| 0.2.0 | 2024-10-28 | NavigationView, tracked apps, self-update dialog, winget progress |
| 0.1.0 | 2024-10-18 | Initial release |
