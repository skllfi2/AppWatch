# RESEARCH — run `oct17` (autoresearch/oct17 branch)

Domain: **UX-pain-driven rules + real-world fixture corpus** for AppWatch Core.
Duration budget: up to 10 experiments or 3 consecutive non-keeps. Base suite gate: 131/131.

## Where the pain points come from (light web pass, 2026-10-17)

Sources: r/sysadmin & r/Windows11 threads on auto-updates, UserTesting "why users hate
software updates", UniGetUI/WingetUI reviews & troubleshooting pages, winget 1.28
portable-tracking bug coverage. Recurring complaints, mapped to Core-testable rules:

| # | User pain (verbatim theme) | Core rule / fixture domain |
|---|---|---|
| P1 | Auto-update applied without consent / "pinned or exclude packages you don't want to upgrade automatically" | `UpdatePolicy.ShouldAutoApply` — pure consent gate; default OFF; respects Enabled |
| P2 | winget 1.28: "install completed, package never appeared" — invisible installs | tracking-guarantee invariant on engine results (success ⇒ version + dir known) |
| P3 | "It installed, but the version didn't change" / "version comparison looks wrong" | VersionParser.IsNewer fixtures with REAL version strings from catalog repos |
| P4 | "An update does not appear… installer detected under one name" — asset matching | UpdateDecisionEngine.Evaluate fixtures with REAL asset lists from catalog repos |
| P5 | Installs "fail mid-way", "hang on a step", partial files | disk-space preflight (pure) + cancel-cleanliness rule |
| P6 | Repeated failures every cycle undermine trust ("update does not appear… or does, wrongly") | BackgroundCheckOrder: consecutive-error backoff (pure) |

Not automatable now (→ DESIGN.md backlog, do not force): stale-source empty lists,
UAC-prompt context, "blindly update everything" major-version warnings, proxy/TLS issues.

## Known live finding (verify, then fix via Core rule)

`AppConfig.AutoApplyUpdates` exists (default false) and `BackgroundMonitor` respects it,
BUT `MainViewModel.RunBackgroundCheckAsync` auto-applies updates WITHOUT checking it.
Fix pattern: introduce the pure Core gate `UpdatePolicy.ShouldAutoApply(config, app)`
and make BOTH call sites use it. In this run: implement + test the Core gate; the
MainViewModel wiring is a one-line follow-up OUTSIDE this loop (App project, UI risk) —
record it in the report, do not edit App in the loop.

## Harness spec

New test file(s) in `tests/AppWatch.Core.Tests/`:
- `UpdatePolicyTests.cs` — P1 consent gate (default off, respects Enabled, per-app overrides if trivially expressible via existing fields)
- `VersionComparisonFixturesTests.cs` — P3: [Fact] per real version pair with expected verdict, each with a one-line justification comment. Real strings from: catalog.json repos (yt-dlp "2026.08.19", Vesktop "v1.6.7", GameOptimizer "0.5.9.0", zapret "1.10.2"…) — gather via `gh api repos/<owner>/<repo>/releases/latest --jq .tag_name` for all 16 catalog entries
- `AssetSelectionFixturesTests.cs` — P4: [Fact] per catalog repo: real asset name list from latest release (gh api), expected chosen asset for win-x64 portable (hand-verified + justification). Metric counts these.
- `PreflightTests.cs` — P5 disk-space rule (pure function)
- `BackoffTests.cs` — P6: extend BackgroundCheckOrder (pure) — after N consecutive LastCheckStatus==Error, deprioritize for M cycles. Keep the B9 tier contract intact.

Fixture data: gather with `gh api` from the repos in `src/AppWatch.App/Catalog/catalog.json`.
Record gathered fixtures inline in the test files (self-contained, no network in tests).

## Metric

`pain_tests_green / pain_tests_total` (P1..P6 named tests), reported per experiment.
Keep gate: base suite 131+ green AND metric strictly improved (or equal only for a
refactor experiment explicitly marked `refactor:`).

## results.tsv (untracked)

`commit	metric	base_suite	status	description`

## Budget

≤10 experiments, stop after 3 consecutive non-keeps, hard stop 4h wall clock.
Each experiment: implement → commit → build tests → run exe → keep/discard → log.
