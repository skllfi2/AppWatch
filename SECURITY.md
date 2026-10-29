# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 0.2.x   | ✅ Current |
| 0.1.x   | ❌ End of life |

---

## Reporting a Vulnerability

**Do not report security vulnerabilities via public GitHub issues.**

Instead, please email: **security@appwatch.example.com** (or open a [private security advisory](https://github.com/skllfi2/AppWatch/security/advisories/new) on GitHub).

Include:
- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

We will acknowledge receipt within 48 hours and provide a timeline for fix.

---

## Security Measures

### Update Integrity
| Mechanism | Details |
|-----------|---------|
| **Velopack** | Authenticode signed, delta updates, SHA256 verified, GitHub Releases hosting |
| **Zip fallback** | SHA256 checksum verified before extraction, PowerShell replace script |
| **winget** | Uses system winget (Microsoft signed), manifest SHA256 in winget-pkgs |

### Code Signing
- Release binaries signed with Authenticode certificate (CI)
- Timestamp via DigiCert
- Verifiable via `signtool verify /pa /v AppWatch.exe`

### Secrets Management
- No secrets in code or config
- GitHub PAT (optional) stored in user config.json (user-writable, not encrypted)
- CI secrets via GitHub Actions secrets (WINGET_PKGS_TOKEN, CODE_SIGN_CERT)

### Dependencies
- `Dependabot` weekly scans for NuGet + GitHub Actions vulnerabilities
- `dotnet list package --vulnerable` in CI
- Minimal dependency surface (no Node.js, Python, etc.)

### Runtime
- Unpackaged (WindowsPackageType=None) — no MSIX container isolation
- Runs as user (no elevation required)
- No background services — only foreground app + scheduled checks via app
- Config in `%APPDATA%\AppWatch\` (user-writable, ACL protected)

### Network
- HTTPS only (GitHub API, winget source)
- No telemetry by default (opt-in planned)
- No auto-download without user consent (AutoApplyConsent = false by default)

---

## Threat Model (STRIDE)

| Threat | Mitigation |
|--------|------------|
| **Spoofing** | Authenticode signatures on all binaries; GitHub Releases verified via TLS |
| **Tampering** | SHA256 verification on zip; Velopack delta signatures; winget manifest SHA256 |
| **Repudiation** | Signed releases; Git commit signing (recommended) |
| **Information Disclosure** | No PII collected; logs local only; optional telemetry opt-in |
| **Denial of Service** | Rate-limited GitHub API (with optional PAT); winget fallback; background check scheduling |
| **Elevation of Privilege** | Runs as user; no kernel drivers; no system services; self-update via user-context replace script |

---

## Secure Development Practices

- **SAST**: .NET analyzers (`EnableNETAnalyzers=true`, `AnalysisLevel=latest`) in CI
- **SCA**: Dependabot + `dotnet list package --vulnerable` in CI
- **Code Review**: Required for all PRs (see REVIEW.md)
- **Secrets Scanning**: GitHub secret scanning enabled
- **SBOM**: SPDX JSON generated in CI (`dotnet sbom`)

---

## Incident Response

1. **Detect** — Dependabot alert, user report, or internal review
2. **Assess** — Severity (CVSS), affected versions, exploitability
3. **Fix** — Patch in private fork, test, sign
4. **Release** — Hotfix tag (e.g., `v0.2.1`), GitHub Release, winget PR
5. **Notify** — Security advisory on GitHub, email to watchers
6. **Postmortem** — Document root cause, improve defenses

---

## Contact

Security team: **security@appwatch.example.com**

PGP Key: [Available on request]

---

*Last updated: 2024-10-28*