# AppWatch

> Automatic updates for your Windows apps — GitHub releases, winget, portable apps, installers.

![AppWatch Screenshot](docs/assets/screenshot.png)

---

## Features

| Feature | Description |
|---------|-------------|
| **Multi-source updates** | GitHub Releases, winget, portable (zip/exe), installer (ARP) |
| **Tracked apps only** | Add only the apps you care about — no noise |
| **Unified UI** | WinUI 3 + Mica, NavigationView shell, Dark/Light theme |
| **Self-update** | Velopack (delta, signed) + Zip fallback (portable) |
| **Background checks** | Scheduled, non-intrusive, single consolidated notification |
| **Changelog preview** | View release notes before updating |
| **Winget integration** | Real-time progress, catalog health detection |
| **Icon extraction** | Win32 (SHGetFileInfo) + UWP (PackageManager) |
| **Localization** | Russian / English (extensible) |

---

## Screenshots

| Tracked Apps | Updates | Settings |
|--------------|---------|----------|
| ![Tracked Apps](docs/assets/tracked-apps.png) | ![Updates](docs/assets/updates.png) | ![Settings](docs/assets/settings.png) |

---

## Installation

### Via winget (recommended)
```powershell
winget install AppWatch
```

### Via GitHub Releases
Download latest `AppWatch_Setup.exe` from [Releases](https://github.com/skllfi2/AppWatch/releases).

### Portable (zip)
Download `AppWatch_Portable.zip` from Releases, extract, run `AppWatch.exe`.

---

## Building from Source

### Prerequisites
- Windows 10/11 (17763+)
- Visual Studio 2026 Preview (or VS 2022 17.11+)
- .NET 10 SDK

### Build
```bash
git clone https://github.com/skllfi2/AppWatch.git
cd AppWatch
dotnet restore
dotnet build -c Release
```

### Run Tests
```bash
dotnet test tests/AppWatch.Core.Tests/AppWatch.Core.Tests.csproj -c Release
```

---

## Architecture

```
AppWatch/
├── src/
│   ├── AppWatch.Core/           # Domain, Abstractions, Models
│   ├── AppWatch.Infrastructure/ # GitHub, Winget, Config, Icons, SelfUpdate
│   └── AppWatch.App/            # WinUI 3 UI (Views, ViewModels)
├── tests/
│   └── AppWatch.Core.Tests/
└── docs/
    ├── architecture.md          # C4 diagrams, ADRs
    └── adr/                     # Architecture Decision Records
```

See [Architecture Documentation](docs/architecture.md) for C4 diagrams and design decisions.

---

## Configuration

Config file: `%APPDATA%\AppWatch\config.json`

```json
{
  "theme": "System",
  "trackedApps": [...],
  "backgroundCheckEnabled": true,
  "backgroundCheckIntervalHours": 4,
  "autoApplyConsent": false,
  "semanticSearchEnabled": false
}
```

---

## Contributing

See [CONTRIBUTING.md](docs/contributing.md) for:
- Code standards (C#, XAML, Git)
- Development setup
- Testing guidelines
- Release process

---

## License

MIT License — see [LICENSE](LICENSE) for details.

Third-party acknowledgments in [NOTICE](NOTICE):
- ZuiV2 (WinUI 3 design system) — own code
- UniGetUI (MIT) — winget UI reference
- komi-store (Apache-2.0) — app installer patterns

---

## Links

- [GitHub Repository](https://github.com/skllfi2/AppWatch)
- [Issues](https://github.com/skllfi2/AppWatch/issues)
- [Discussions](https://github.com/skllfi2/AppWatch/discussions)
- [Releases](https://github.com/skllfi2/AppWatch/releases)