# AppWatch Architecture

## Overview
AppWatch is a Windows application that monitors and manages updates for installed applications from multiple sources (GitHub Releases, winget, portable apps, installers).

---

## C4 Context Diagram

```mermaid
C4Context
title System Context Diagram for AppWatch

Person(user, "User", "Windows user who wants to keep apps updated")

System(appwatch, "AppWatch", "WinUI 3 / .NET 10 desktop app")

System_Ext(github, "GitHub API", "Releases, tags, assets")
System_Ext(winget, "Windows Package Manager", "winget CLI / Microsoft Store")
System_Ext(store, "Microsoft Store", "UWP app metadata")
System_Ext(velo, "Velopack / GitHub Releases", "Self-update distribution")

Rel(user, appwatch, "Uses", "GUI / Notifications")
Rel(appwatch, github, "Queries", "HTTPS / REST")
Rel(appwatch, winget, "Invokes", "CLI / COM")
Rel(appwatch, store, "Queries", "PackageManager API")
Rel(appwatch, velo, "Downloads", "HTTPS / Delta updates")
```

---

## C4 Container Diagram

```mermaid
C4Container
title Container Diagram for AppWatch

Container(app, "AppWatch.App", "WinUI 3, .NET 10", "UI shell, NavigationView, pages, dialogs, toasts")
Container(core, "AppWatch.Core", ".NET 10", "Domain models, abstractions, update engines, parsers")
Container(infra, "AppWatch.Infrastructure", ".NET 10", "GitHub client, Winget service, Config store, Icon extractor, Self-update")
ContainerDb(config, "Config Store", "JSON (%APPDATA%)", "Tracked apps, settings, catalog, theme")
ContainerDb(logs, "Logs", "Text files (%APPDATA%)", "App logs, crash logs, scan logs")

Rel(app, core, "Uses", "DI / Interfaces")
Rel(app, infra, "Uses", "DI / Services")
Rel(app, config, "Reads/Writes", "IConfigStore")
Rel(core, infra, "Implements", "Abstractions")
Rel(infra, github, "Calls", "HTTPS / Octokit")
Rel(infra, winget, "Invokes", "winget.exe")
Rel(infra, config, "Persists", "JSON")
Rel(infra, logs, "Writes", "Serilog")
```

---

## C4 Component Diagram (Core)

```mermaid
C4Component
title Component Diagram - AppWatch.Core

Component(update_engine, "IUpdateEngine", "Interface", "Check for updates, apply updates")
Component(github_engine, "GitHubUpdateEngine", "Class", "GitHub Releases API polling")
Component(winget_engine, "WingetUpdateEngine", "Class", "winget upgrade --list parsing")
Component(installer_engine, "InstallerUpdateEngine", "Class", "ARP/Uninstall registry parsing")
Component(catalog, "CatalogService", "Class", "App catalog (sources, manifests)")
Component(config, "IConfigStore", "Interface", "JSON config persistence")
Component(models, "Domain Models", "Records/Classes", "TrackedAppItem, InstalledApp, PendingUpdate, AppConfig")
Component(parsers, "Parsers", "Classes", "WingetOutputParser, VersionParser")

Rel(github_engine, update_engine, "Implements")
Rel(winget_engine, update_engine, "Implements")
Rel(installer_engine, update_engine, "Implements")
Rel(catalog, github_engine, "Uses")
Rel(catalog, config, "Persists")
Rel(models, parsers, "Used by")
```

---

## Key Design Decisions (ADRs)

See [Architecture Decision Records](adr/) for detailed decisions:

| ADR | Title | Status |
|-----|-------|--------|
| 001 | Clean Architecture with Core/Infra/App separation | Accepted |
| 002 | MVVM with CommunityToolkit.Mvvm | Accepted |
| 003 | WinUI 3 with NavigationView shell | Accepted |
| 004 | Dual update distribution: Velopack + Zip fallback | Accepted |
| 005 | Single consolidated notification (not per-app) | Accepted |
| 006 | Background update check with dispatcher marshaling | Accepted |
| 007 | JSON config + typed localization keys | Accepted |
| 008 | No neural search (tokenizer too large) | Accepted |
| 009 | UWP apps via PackageManager + ARP for Win32 | Accepted |
| 010 | Self-update dialog with changelog + progress | Accepted |

---

## Data Flow: Update Check

```mermaid
sequenceDiagram
    participant U as User
    participant A as AppWatch.App
    participant VM as MainViewModel
    participant CE as CatalogService
    participant GE as GitHubUpdateEngine
    participant WE as WingetUpdateEngine
    participant GH as GitHub API
    participant WG as winget CLI

    U->>A: Clicks "Check Updates"
    A->>VM: CheckUpdatesAsyncCommand
    VM->>CE: GetTrackedApps()
    CE-->>VM: List<TrackedAppItem>
    par GitHub apps
      VM->>GE: CheckAppAsync(app)
      GE->>GH: GET /repos/{owner}/{repo}/releases
      GH-->>GE: Release assets
      GE-->>VM: UpdateAvailable + PendingNotes
    and Winget apps
      VM->>WE: CheckAppAsync(app)
      WE->>WG: winget upgrade --list --id {id}
      WG-->>WE: Table output
      WE->>WE: WingetOutputParser.Parse()
      WE-->>VM: UpdateAvailable + Stage
    end
    VM->>A: UpdatesGroup updated
    A->>U: Toast "N updates available"
```

---

## Data Flow: Self-Update (Velopack)

```mermaid
sequenceDiagram
    participant U as User
    participant S as SettingsViewModel
    participant SU as SelfUpdateService
    participant VP as Velopack
    participant GH as GitHub Releases

    U->>S: "Check AppWatch updates"
    S->>SU: PrepareAsync(currentVersion)
    SU->>GH: GET /repos/skllfi2/AppWatch/releases
    GH-->>SU: Latest release + assets
    SU->>SU: Download Setup.exe (with progress)
    SU->>SU: Verify signature
    SU-->>S: VelopackUpdateInfo
    S->>U: Show "Update available" button
    U->>S: "Apply Update"
    S->>VP: DownloadUpdatesAsync(progress)
    VP->>GH: Download delta nupkg
    VP->>VP: ApplyUpdatesAndRestart()
    VP->>U: App restarts with new version
```

---

## Data Flow: Self-Update (Zip Fallback)

```mermaid
sequenceDiagram
    participant U as User
    participant S as SettingsViewModel
    participant SU as SelfUpdateService
    participant GH as GitHub Releases
    participant PS as PowerShell

    U->>S: "Check AppWatch updates"
    S->>SU: PrepareAsync(currentVersion, progress)
    SU->>GH: GET /repos/skllfi2/AppWatch/releases
    GH-->>SU: Latest release + zip asset
    SU->>SU: Download zip (progress callback)
    SU->>SU: Verify SHA256
    SU->>SU: Extract to temp
    SU-->>S: PreparedUpdate
    S->>U: SelfUpdateDialog (changelog + progress)
    U->>S: "Update Now"
    S->>PS: Run replace.ps1 (after exit)
    PS->>PS: Copy files, restart app
    PS->>U: New version running
```

---

## Security Considerations

| Area | Approach |
|------|----------|
| **Code signing** | Authenticode for .exe/.msix (CI) |
| **Update verification** | SHA256 checksums (zip), Velopack signatures (Velopack) |
| **GitHub API** | Optional PAT for higher rate limits (stored in config) |
| **winget** | Uses system winget (trusted Microsoft binary) |
| **Config** | JSON in %APPDATA% (user-writable, no elevation) |
| **Logs** | No secrets, PII minimized |

---

## Performance Targets

| Metric | Target |
|--------|--------|
| Cold start (UI visible) | < 1.5 s |
| Working set (idle) | < 150 MB |
| GC Gen2 heap | < 10 MB |
| Update check (10 apps) | < 5 s |
| winget scan | < 3 s |
| Installed apps scan (ARP + UWP) | < 2 s |

---

## Deployment

| Channel | Method |
|---------|--------|
| **Stable** | GitHub Releases (Velopack .exe) + winget manifest PR |
| **Preview** | GitHub Releases (pre-release tag) + Velopack channel |
| **Portable/Dev** | Zip artifact from GitHub Actions |
| **Uninstall** | Velopack uninstaller / winget uninstall / Settings dialog |

---

## Extensibility Points

| Extension | Interface | Implementation |
|-----------|-----------|----------------|
| New update source | `IUpdateEngine` | `GitHubUpdateEngine`, `WingetUpdateEngine`, `InstallerUpdateEngine` |
| New catalog source | `ICatalogSource` | `GitHubCatalogSource`, `WingetCatalogSource`, `StaticCatalogSource` |
| New notification | `IToastService` | `ToastService` (Windows toasts) |
| New config backend | `IConfigStore` | `JsonConfigStore` |
| New icon source | `IIconExtractor` | `IconExtractor` (Win32 + UWP) |