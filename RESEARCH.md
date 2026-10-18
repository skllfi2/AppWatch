# RESEARCH — run `oct18` (autoresearch/oct18 branch)

Domain: **VersionParser edge cases + platform filter extensions + catalog search quality**.
Duration budget: up to 10 experiments or 3 consecutive non-keeps. Base suite gate: 209/209.

## Domains

### D1: VersionParser — more version-tag prefixes and formats
Real GitHub repos use diverse tag formats: `release-1.2.3`, `rel/v2.0`, `version-0.5.0`,
`v1.2.3-beta.1`, `2026.08.19`, `build-123`. The current parser handles `v` prefix and
numeric versions, but likely misses `release-`, `rel/`, `version-`, build numbers, etc.
- Gather REAL tag names from catalog repos via `gh api repos/<owner>/<repo>/releases --jq '.[].tag_name'`
- Add [Fact] per format with expected parsed result
- Fix VersionParser to handle each format

### D2: Platform filter — non-Windows asset exclusion
Assets like `.snap`, `.flatpak`, `.AppImage`, `.dmg`, `.deb`, `.rpm`, `.apk`, `.jar`
should never be selected for Windows. Currently the platform filter may only handle
some of these.
- Add [Fact] per extension with expected exclusion
- Fix AssetArchitectureMatcher / platform filter

### D3: Catalog search — fuzzy matching
The catalog search uses exact substring matching. Real users type partial names with
typos. A simple fuzzy score (e.g., Levenshtein ≤ 2 or character-subsequence match)
would improve findability.
- Test cases: "ytdl" should find "yt-dlp", "vescop" (typo) should find "Vesktop",
  "optimizer" should find both "optimizerDuck" and "GameOptimizer"
- Implement as a pure Core function `CatalogSearch.Score(query, name, description)` — testable

## Known live findings to address
- VersionParser: current `release-` prefix fix (from oct17) may not handle `rel/` or `version-`
- Platform filter: currently handles some extensions but may miss `.AppImage`, `.flatpak`, `.snap`

## Metric
`domain_tests_green / domain_tests_total` per experiment (D1+D2+D3 named tests).
Keep gate: base 209 green.

## results.tsv (untracked, in .gitignore)
`commit	metric	base_suite	status	description`

## Budget
≤10 experiments, stop after 3 consecutive non-keeps, hard stop 4h.
Each experiment: implement → commit → build tests → run exe → keep/discard → log.
