# Architecture Decision Records (ADR) Template

## ADR Template

```markdown
# ADR {NNN}: {Title}

## Status
{Proposed | Accepted | Rejected | Superseded by ADR-NNN}

## Context
{What is the issue that we're seeing that is motivating this decision or change?}

## Decision
{What is the change that we're proposing and/or doing?}

## Consequences
{What becomes easier or more difficult to do because of this change?}

### Positive
- ...

### Negative
- ...

### Neutral
- ...
```

---

## ADR Index

| ID | Title | Status | Date |
|----|-------|--------|------|
| 001 | Clean Architecture with Core/Infra/App separation | Accepted | 2024-10-10 |
| 002 | MVVM with CommunityToolkit.Mvvm | Accepted | 2024-10-10 |
| 003 | WinUI 3 with NavigationView shell | Accepted | 2024-10-20 |
| 004 | Dual update distribution: Velopack + Zip fallback | Accepted | 2024-10-18 |
| 005 | Single consolidated notification (not per-app) | Accepted | 2024-10-23 |
| 006 | Background update check with dispatcher marshaling | Accepted | 2024-10-24 |
| 007 | JSON config + typed localization keys | Accepted | 2024-10-22 |
| 008 | No neural search (tokenizer too large) | Accepted | 2024-10-24 |
| 009 | UWP apps via PackageManager + ARP for Win32 | Accepted | 2024-10-24 |
| 010 | Self-update dialog with changelog + progress | Accepted | 2024-10-28 |

---

## ADR 001: Clean Architecture with Core/Infra/App separation

### Status
Accepted

### Context
Need maintainable, testable architecture for a desktop app with multiple update sources and UI.

### Decision
Split into three projects:
- **AppWatch.Core** — Domain models, abstractions (interfaces), pure logic, no UI/framework deps
- **AppWatch.Infrastructure** — Implementations: GitHub client, winget service, config store, icon extractor, self-update
- **AppWatch.App** — WinUI 3 UI: views, viewmodels, DI composition, platform-specific code

Dependencies: App → Infra → Core (Core has no deps on others)

### Consequences

**Positive**
- Core is unit-testable without UI/framework
- Easy to swap implementations (e.g., different GitHub client)
- Clear separation of concerns

**Negative**
- More projects to manage
- DI container needed for composition

**Neutral**
- Standard pattern for .NET desktop apps

---

## ADR 002: MVVM with CommunityToolkit.Mvvm

### Status
Accepted

### Context
Need reactive UI with minimal boilerplate for INotifyPropertyChanged, commands.

### Decision
Use `CommunityToolkit.Mvvm` (v8+) with source generators:
- `[ObservableObject]` / `[ObservableProperty]` for properties
- `[RelayCommand]` for commands
- No manual `INotifyPropertyChanged` implementation

### Consequences

**Positive**
- Minimal boilerplate
- Source generators = compile-time, no reflection overhead
- Well-maintained, Microsoft-supported

**Negative**
- Requires .NET 6+ (we're on .NET 10)
- Learning curve for attributes

---

## ADR 003: WinUI 3 with NavigationView shell

### Status
Accepted

### Context
Need modern Windows 11 look (Mica, custom titlebar), persistent navigation.

### Decision
- Target Windows App SDK 1.6+ / WinUI 3
- Use `NavigationView` as primary shell (left rail)
- Pages: Tracked Apps, Updates, Winget, History, Settings, About
- Mica backdrop via 8 transparent brush keys
- `ExtendsContentIntoTitleBar="True"` with custom titlebar

### Consequences

**Positive**
- Native Windows 11 look
- Good accessibility support
- x:Bind compiled bindings for performance

**Negative**
- WinUI 2.3.9/2.6.4 XAML compiler has locale bug on ru-RU (fixed in 2.5.1+ but only on Microsoft feed)
- Larger app size (~80 MB self-contained)

---

## ADR 004: Dual update distribution: Velopack + Zip fallback

### Status
Accepted

### Context
Need reliable updates for both installed (MSIX-like) and portable users.

### Decision
- **Velopack** (Setup.exe): Delta updates, Authenticode signing, GitHub Releases hosting, automatic background check
- **Zip fallback**: Portable zip with SHA256, PowerShell replace script, manual or triggered from Settings

Both paths share `SelfUpdateService.PrepareAsync()` / `ApplyAsync()`.

### Consequences

**Positive**
- Covers all user scenarios
- Velopack = professional delta updates
- Zip = zero-install, portable, dev-friendly

**Negative**
- Two code paths to maintain
- Zip requires PowerShell script complexity

---

## ADR 005: Single consolidated notification (not per-app)

### Status
Accepted

### Context
User reported "too many notifications" when multiple apps have updates.

### Decision
Single toast: "N updates available" → click opens Tracked Apps page with filter for updates.

### Consequences

**Positive**
- Less noise
- User controls when to see details

**Negative**
- Less immediate visibility per app

---

## ADR 006: Background update check with dispatcher marshaling

### Status
Accepted

### Context
Background task (`BackgroundMonitor`) crashed with `RPC_E_WRONG_THREAD` when updating ViewModel properties bound to UI.

### Decision
Marshal all ViewModel mutations to UI thread via `DispatcherQueue.TryEnqueue()`. Background work (HTTP, winget) stays async.

### Consequences

**Positive**
- No cross-thread exceptions
- UI stays responsive

**Negative**
- Slight complexity in background service

---

## ADR 007: JSON config + typed localization keys

### Status
Accepted

### Context
Need user settings + multi-language support without resx complexity.

### Decision
- Config: `%APPDATA%\AppWatch\config.json` via `IConfigStore` (System.Text.Json)
- Localization: `ru.json` / `en.json` + `Loc.cs` typed properties (source-generated via T4 or manual)

### Consequences

**Positive**
- Human-editable config
- Type-safe localization keys (compile-time check)
- Easy to add languages

**Negative**
- Manual sync of Loc.cs when adding keys
- No built-in fallback chain (en → ru)

---

## ADR 008: No neural search (tokenizer too large)

### Status
Accepted

### Context
Investigated semantic search for app catalog; smallest tokenizer ~17 MB, model > 100 MB.

### Decision
Remove neural search entirely. Use fuzzy lexical search (Levenshtein) + trigram index. Opt-in flag kept in config for future but ignored.

### Consequences

**Positive**
- App size stays small (~5 MB core)
- No ONNX runtime dependency
- Fast enough for < 1000 catalog items

**Negative**
- No semantic understanding (synonyms, intent)

---

## ADR 009: UWP apps via PackageManager + ARP for Win32

### Status
Accepted

### Context
ARP (Add/Remove Programs) misses Store/UWP apps.

### Decision
- Win32: `Registry.GetUninstallSubKeys()` (HKLM + HKCU)
- UWP: `PackageManager.FindPackagesForUser("")` → filter framework packages
- Merge into single `InstalledApp` list with `PackageFamilyName` for UWP

### Consequences

**Positive**
- Complete installed apps list (142 apps typical vs 84 ARP-only)
- UWP apps launchable via `IApplicationActivationManager`

**Negative**
- Two code paths for scanning
- PackageManager requires `windowsPackageType="None"` unpackaged

---

## ADR 010: Self-update dialog with changelog + progress

### Status
Accepted

### Context
User wants professional update UX like VS Code / Discord / GitHub Desktop.

### Decision
`SelfUpdateDialog` (ContentDialog in code-behind to avoid XAML compiler locale bug):
- Changelog (markdown, scrollable)
- Progress bar (indeterminate → determinate with % + speed)
- 3 buttons: "Update Now", "Skip Version", "Remind Later"
- Integrated in `SettingsViewModel.ApplySelfUpdateAsync`

### Consequences

**Positive**
- Professional UX
- User control over updates
- Progress visibility for zip downloads

**Negative**
- Code-behind dialog (no XAML) due to locale bug
- More code to maintain