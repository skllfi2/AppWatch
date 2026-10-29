# Contributing to AppWatch

Thank you for your interest in contributing! This document outlines the process and standards for contributing to AppWatch.

---

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/version/2/1/code_of_conduct/). By participating, you are expected to uphold this code.

---

## How to Contribute

### Reporting Bugs
1. Check [existing issues](https://github.com/skllfi2/AppWatch/issues) first
2. Create a new issue with:
   - Clear title and description
   - Steps to reproduce
   - Expected vs actual behavior
   - Screenshots/logs if applicable
   - Environment: Windows version, AppWatch version

### Suggesting Features
1. Check existing issues/discussions
2. Open a feature request issue with:
   - Use case / motivation
   - Proposed solution
   - Alternatives considered
   - Mockups (if UI)

### Pull Requests
1. Fork the repo and create a branch: `git checkout -b feat/your-feature`
2. Make changes following the [code standards](#code-standards)
3. Run tests: `dotnet test`
4. Run format: `dotnet format`
5. Submit PR with:
   - Clear title (conventional commits: `feat:`, `fix:`, `chore:`, `docs:`)
   - Description of changes
   - Related issue number

---

## Development Setup

### Prerequisites
- **Windows 10/11** (17763+)
- **Visual Studio 2026 Preview** (or VS 2022 17.11+ with .NET 10 SDK)
- **.NET 10 SDK** (preview)
- **Git**

### Clone and Build
```bash
git clone https://github.com/skllfi2/AppWatch.git
cd AppWatch
dotnet restore
dotnet build -c Debug
```

### Run Tests
```bash
dotnet test tests/AppWatch.Core.Tests/AppWatch.Core.Tests.csproj -c Release
```

### Run Locally
```bash
cd src/AppWatch.App
dotnet run -c Debug
```

### Debug in VS 2026
1. Open `AppWatch.slnx`
2. Set `AppWatch.App` as startup project
3. Press F5

---

## Code Standards

### C# Style
- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Enforced via `.editorconfig` — run `dotnet format` before committing
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Use `var` when type is apparent
- Prefer expression-bodied members
- Use pattern matching (`is`, `switch` expressions)

### Architecture
- **Clean Architecture**: Core → Infrastructure → App (no reverse deps)
- **MVVM**: ViewModels in `AppWatch.App/ViewModels`, Views in `AppWatch.App/Views`
- **DI**: Register services in `App.xaml.cs` `ConfigureServices()`
- **Interfaces**: Define in `AppWatch.Core/Abstractions`, implement in `Infrastructure`

### XAML / WinUI 3
- Use `x:Bind` (compiled bindings) over `{Binding}` where possible
- Converters in `AppWatch.App/Converters`, registered in `App.xaml`
- Resource dictionaries merged in `App.xaml` + `ThemeService`
- Mica: 8 brush keys set to `Transparent` in `App.xaml`
- No hardcoded colors — use `StaticResource` from design system

### Localization
- Add keys to both `ru.json` and `en.json` in `AppWatch.App/Localization/`
- Add typed property in `Loc.cs` (same order as JSON)
- Use `Loc.Instance["Key"]` in C#, `{x:Bind Loc.Key}` in XAML

### Git Commit Messages
Follow [Conventional Commits](https://www.conventionalcommits.org/):
```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `chore`, `ci`

Examples:
```
feat(ui): add self-update dialog with progress
fix(winget): handle 0x80070057 catalog crash
docs: update architecture.md with C4 diagrams
chore(deps): update CommunityToolkit.Mvvm to 8.4.0
```

### Branch Naming
- `feat/short-description` — new features
- `fix/short-description` — bug fixes
- `chore/short-description` — maintenance
- `docs/short-description` — documentation

---

## Testing

### Unit Tests
- Location: `tests/AppWatch.Core.Tests/`
- Framework: xUnit + FluentAssertions + Moq
- Run: `dotnet test tests/AppWatch.Core.Tests/... -c Release`
- Target: 364+ tests passing

### UI Tests (planned)
- WinAppDriver / Appium
- Location: `tests/AppWatch.App.UITests/`

### Manual Testing Checklist
- [ ] Cold start < 1.5s
- [ ] Mica backdrop visible
- [ ] NavigationView switches pages
- [ ] Dark/Light theme toggle works
- [ ] Update check finds GitHub/winget updates
- [ ] Self-update dialog shows progress
- [ ] Toast notifications appear
- [ ] Settings persist across restarts

---

## Release Process

### Versioning
[SemVer](https://semver.org/): `MAJOR.MINOR.PATCH[-prerelease]`
- MAJOR: Breaking changes
- MINOR: New features (backward compatible)
- PATCH: Bug fixes
- Prerelease: `-preview.1`, `-rc.1`

### Creating a Release
1. Update version in `Directory.Build.props` (`VersionPrefix`)
2. Update `CHANGELOG.md`
3. Commit: `chore(release): v0.2.0`
4. Tag: `git tag v0.2.0 && git push origin v0.2.0`
5. GitHub Actions `release.yml` builds, tests, publishes, creates GitHub Release
6. Automated winget-pkgs PR created (if configured)

---

## Project Structure

```
AppWatch/
├── .github/
│   ├── workflows/        # CI/CD (build.yml, release.yml)
│   └── dependabot.yml
├── docs/
│   ├── architecture.md   # C4 diagrams, ADRs
│   └── adr/              # Architecture Decision Records
├── src/
│   ├── AppWatch.Core/           # Domain, Abstractions, Models
│   ├── AppWatch.Infrastructure/ # Services, Clients, Parsers
│   └── AppWatch.App/            # WinUI 3 UI (Views, ViewModels)
├── tests/
│   └── AppWatch.Core.Tests/
├── .editorconfig
├── Directory.Build.props
├── Directory.Build.targets
├── AppWatch.slnx
├── README.md
├── CHANGELOG.md
├── LICENSE
├── SECURITY.md
└── NOTICE
```

---

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE) (or Apache-2.0 for komi-store derived code).

---

## Questions?

Open a [Discussion](https://github.com/skllfi2/AppWatch/discussions) or issue.