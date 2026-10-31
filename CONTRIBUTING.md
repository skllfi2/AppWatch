# Contributing to AppWatch

Thank you for your interest in contributing to **AppWatch**! We welcome bug reports, feature requests, code improvements, tests, and translations.

This repository strictly follows modern .NET, Clean Architecture, and WinUI 3 Fluent Design standards.

---

## Code of Conduct

Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you help keep this community welcoming, respectful, and safe for everyone.

---

## Getting Started

### Prerequisites

* **Operating System**: Windows 10 (version 1809, build 17763) or Windows 11 (recommended).
* **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* **IDE**: Visual Studio 2022 (v17.11+) or Visual Studio 2026 Preview with:
  * *.NET Desktop Development* workload
  * *Windows App SDK C# Templates*
* **Command Line (Alternative)**: PowerShell 7+ with `dotnet CLI`.

### Clone & Build

```bash
git clone https://github.com/skllfi2/AppWatch.git
cd AppWatch

# Restore dependencies
dotnet restore

# Build x64 Debug
dotnet build -p:Platform=x64

# Run test suite
dotnet test -p:Platform=x64
```

---

## Architectural Principles

AppWatch adheres strictly to **Clean Architecture** and **MVVM (Model-View-ViewModel)**:

```
AppWatch/
├── src/
│   ├── AppWatch.Core/            # Pure domain layer: models, interfaces, update algorithms
│   │                            # NO dependencies on UI, OS-specific P/Invoke, or external APIs
│   │
│   ├── AppWatch.Infrastructure/  # Concrete implementations: GitHub API, WinGet, registry, file system
│   │                            # Implements interfaces declared in AppWatch.Core
│   │
│   └── AppWatch.App/             # WinUI 3 presentation layer: Views (XAML), ViewModels (CommunityToolkit.Mvvm)
│                                # References Core and Infrastructure
└── tests/
    └── AppWatch.Core.Tests/      # Automated unit and fixture tests (Hermetic, xUnit v3)
```

### Dependency Rule
* `Core` never references `Infrastructure` or `App`.
* `Infrastructure` references `Core` only.
* `App` wires dependencies through Dependency Injection in `App.xaml.cs`.

---

## Development Standards

### 1. C# Coding Conventions
* Nullable reference types are strictly enabled (`<Nullable>enable</Nullable>`).
* Use modern C# 12/13/14 language features: file-scoped namespaces, pattern matching, record types, primary constructors.
* Follow Microsoft C# naming conventions: `PascalCase` for types and public members, `_camelCase` for private fields, `camelCase` for parameters.
* Avoid blocking calls on the UI thread; always use async/await properly with `ConfigureAwait(false)` in Infrastructure and Core.

### 2. WinUI 3 & Fluent Design Guidelines
* Use Mica backdrop (`<MicaBackdrop />`) with transparent containers (`NavigationViewDefaultPaneBackground`).
* Prefer compiled bindings (`x:Bind`) over `{Binding}` with explicit `Mode` (`OneWay`, `TwoWay`, `OneTime`).
* Strict usage of semantic theme resources from [`DesignSystem.xaml`](src/AppWatch.App/Styles/DesignSystem.xaml) and [`ThemeResources.xaml`](src/AppWatch.App/Themes/ThemeResources.xaml). Never hardcode hex colors in page layouts.
* Support high contrast, dark mode, and light mode dynamically via `ThemeService`.

### 3. Testing Standards
* Every domain logic change, heuristic, and update policy must have corresponding tests in `AppWatch.Core.Tests`.
* Tests must be **hermetic** (no live network calls, deterministic input/output).
* Ensure all tests pass prior to submitting a pull request:
  ```bash
  dotnet test -p:Platform=x64
  ```

---

## Submitting Pull Requests

1. **Create a Branch**:
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/issue-description
   ```
2. **Commit Messages**:
   Follow [Conventional Commits](https://www.conventionalcommits.org/):
   - `feat: add heuristic repo icon discovery`
   - `fix: resolve icon extraction flag in SHGetFileInfo`
   - `docs: update quickstart guide`
   - `test: add unit tests for custom app flow`
3. **Open a PR**:
   - Provide a clear summary of your changes.
   - Reference related issue numbers (e.g., `Fixes #42`).
   - Fill out the provided [Pull Request Template](.github/PULL_REQUEST_TEMPLATE.md).

---

## Security Vulnerabilities

Please do not report security issues in public GitHub issues. Follow our [Security Policy](SECURITY.md) to report vulnerabilities privately.
