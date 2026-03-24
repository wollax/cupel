# S02: PolicySensitivityReport — fork diagnostic

**Goal:** Implement `policy_sensitivity` / `PolicySensitivity` in both Rust and .NET that runs multiple pipeline configurations over the same item set and returns labeled `SelectionReport`s plus a structured diff showing items that changed inclusion status across variants.
**Demo:** A test exercises ≥2 pipeline configurations on the same item set, producing a `PolicySensitivityReport` with labeled variants and a diff showing which items swung between included/excluded.

## Must-Haves

- Rust: `policy_sensitivity(items, budget, variants)` free function in `analytics` module returning `PolicySensitivityReport`
- Rust: `PolicySensitivityReport` struct with `variants: Vec<(String, SelectionReport)>` and `diffs: Vec<PolicySensitivityDiffEntry>`
- Rust: `PolicySensitivityDiffEntry` struct identifying an item (by content) and its status across variants (included/excluded per variant label)
- .NET: `PolicySensitivity(IReadOnlyList<ContextItem>, ContextBudget, (string Label, CupelPipeline Pipeline)[])` extension method on `CupelPipeline` (static)
- .NET: `PolicySensitivityReport` class with `Variants` and `Diffs` properties matching Rust semantics
- .NET: `PolicySensitivityDiffEntry` record with item content identifier and per-variant inclusion status
- Both: diff computation uses `content()` / `Content` as the item identifier for cross-variant matching (not reference equality — each dry_run produces distinct item instances)
- Both: only items whose inclusion status differs across at least two variants appear in the diff
- Both: ≥1 test exercising ≥2 pipeline configurations that produce a meaningful diff (some items swap between included/excluded)
- Both: `cargo test --all-targets` and `dotnet test --configuration Release` pass

## Proof Level

- This slice proves: integration (real pipelines, real dry_run, real diff computation)
- Real runtime required: no (deterministic pipeline runs in unit tests)
- Human/UAT required: no

## Verification

- `cargo test --all-targets` passes with new `policy_sensitivity` tests
- `dotnet test --configuration Release` passes with new `PolicySensitivityTests`
- `cargo clippy --all-targets -- -D warnings` clean
- `dotnet build --configuration Release` — 0 errors, 0 warnings

## Observability / Diagnostics

- Runtime signals: none — pure function, no side effects
- Inspection surfaces: none — library function returns structured data
- Failure visibility: none — errors propagate as `CupelError` / exceptions from underlying `dry_run`
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `Pipeline::dry_run` (Rust), `CupelPipeline.DryRun` (via `DryRunWithBudget` internal or public `DryRun`) (.NET); `SelectionReport` equality from S01 (for future callers, not strictly used in diff computation)
- New wiring introduced in this slice: `policy_sensitivity` free function (Rust) + `PolicySensitivity` static extension method (.NET) — both are leaf analytics functions with no framework integration
- What remains before the milestone is truly usable end-to-end: S03 (IQuotaPolicy + QuotaUtilization), S04 (snapshot testing), S05 (Rust budget simulation)

## Tasks

- [x] **T01: Rust PolicySensitivityReport types and implementation** `est:30m`
  - Why: Implement the core fork diagnostic in Rust — types, diff algorithm, and integration test exercising ≥2 pipeline variants
  - Files: `crates/cupel/src/analytics.rs`, `crates/cupel/src/lib.rs`, `crates/cupel/tests/policy_sensitivity.rs`
  - Do: Add `PolicySensitivityReport`, `PolicySensitivityDiffEntry`, and `policy_sensitivity` function to `analytics.rs`; re-export from `lib.rs`; create integration test with 2 pipeline configs (tight vs loose budget or different scorers) proving items swap in the diff
  - Verify: `cargo test --all-targets` passes; `cargo clippy --all-targets -- -D warnings` clean
  - Done when: `policy_sensitivity` returns correct labeled reports and diff entries for ≥2 variants; test asserts specific items appear in diff with correct per-variant status

- [x] **T02: .NET PolicySensitivityReport types and implementation** `est:30m`
  - Why: Implement the .NET counterpart — matching types, extension method, and test proving fork diagnostic works with real pipelines
  - Files: `src/Wollax.Cupel/Diagnostics/PolicySensitivityReport.cs`, `src/Wollax.Cupel/Diagnostics/PolicySensitivityDiffEntry.cs`, `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs`
  - Do: Create report/diff types; create static extension method `PolicySensitivity` that calls `DryRun` per variant and computes the diff; update PublicAPI; create test with ≥2 pipeline configs proving items swap
  - Verify: `dotnet test --configuration Release` passes; `dotnet build --configuration Release` — 0 errors, 0 warnings
  - Done when: `PolicySensitivity` returns correct labeled reports and diff entries for ≥2 variants; test asserts specific items appear in diff with correct per-variant status

## Files Likely Touched

- `crates/cupel/src/analytics.rs`
- `crates/cupel/src/lib.rs`
- `crates/cupel/tests/policy_sensitivity.rs`
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityReport.cs`
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityDiffEntry.cs`
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
- `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs`
