# S02: MetadataTrustScorer — Rust + .NET Implementation — UAT

**Milestone:** M003
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: MetadataTrustScorer is a pure function with no I/O dependencies, no runtime service, and no UI. All observable outcomes — test counts, build warnings, PublicAPI entry counts, and drift guard output — are mechanically checkable by running commands against build artifacts. No live runtime or human experience required.

## Preconditions

- Rust toolchain installed (`cargo` available)
- .NET 10 SDK installed (`dotnet` available)
- Working directory: repo root (`/Users/wollax/Git/personal/cupel`)

## Smoke Test

Run `cargo test --all-targets` from `crates/cupel/` and `dotnet test` from the repo root. Both must exit 0. If both pass, the slice is fundamentally working.

## Test Cases

### 1. Rust conformance tests pass

1. `cd crates/cupel && cargo test --all-targets`
2. **Expected:** All tests pass (43+), 0 failed. Output includes `test conformance::scoring::metadata_trust_` entries.

### 2. Rust conformance test count is exactly 5

1. `grep -c "fn metadata_trust_" crates/cupel/tests/conformance/scoring.rs`
2. **Expected:** Prints `5`

### 3. Drift guard is clean

1. `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring`
2. **Expected:** No output (exit 0). Any output indicates spec/crate vector divergence.

### 4. .NET build is clean

1. `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj`
2. **Expected:** `Build succeeded`, 0 Warning(s), 0 Error(s)

### 5. .NET tests pass

1. `dotnet test`
2. **Expected:** total ≥ 669, failed: 0

### 6. PublicAPI entries are present

1. `grep "MetadataTrustScorer" src/Wollax.Cupel/PublicAPI.Unshipped.txt | wc -l`
2. **Expected:** Prints `3` (class declaration, constructor, Score method)

## Edge Cases

### NaN string value falls back to defaultScore (Rust)

1. Construct a TOML vector with `metadata = { "cupel:trust" = "NaN" }` and `defaultScore = 0.5`
2. Run `cargo test -- metadata_trust_non_finite --nocapture`
3. **Expected:** Test passes; scored item returns 0.5

### Native double value accepted directly (.NET — D059)

1. Construct a `ContextItem` with `Metadata["cupel:trust"] = 0.85` (boxed double, not string)
2. Call `MetadataTrustScorer.Score(item)`
3. **Expected:** Returns 0.85 directly without string parsing; verified by `MetadataTrustScorerTests.cs` D059 test

### Out-of-range value clamped (both languages)

1. Provide `cupel:trust = "1.5"` in metadata
2. **Expected:** Scorer returns 1.0 (clamped to [0.0, 1.0]), not 1.5 and not defaultScore

### Key absent falls back to defaultScore (both languages)

1. Provide a ContextItem with no `cupel:trust` metadata key
2. **Expected:** Scorer returns defaultScore (default 0.5)

## Failure Signals

- `cargo test --all-targets` exits non-zero — Rust compilation or test failure
- `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring` produces output — vector drift
- `dotnet build` reports RS0016 errors — PublicAPI.Unshipped.txt is missing entries
- `dotnet test` reports failures — regression in suite or new MetadataTrustScorer tests failing
- `grep "MetadataTrustScorer" src/Wollax.Cupel/PublicAPI.Unshipped.txt | wc -l` prints < 3 — missing API manifest entries

## Requirements Proved By This UAT

- R042 (Metadata convention system spec) — `MetadataTrustScorer` is implemented in both Rust and .NET using the `cupel:trust` float convention with string storage, configurable `defaultScore`, explicit parse-failure/non-finite fallback, and out-of-range clamping. All 5 conformance vector outlines from the spec chapter have passing implementations. Drift guard verifies spec/crate TOML parity.

## Not Proven By This UAT

- `cupel:source-type` convention — defined in R042 spec but not a scorer; no scorer implementation required
- Integration with a real caller pipeline — MetadataTrustScorer has no integration tests against a live Pipeline; scorer correctness is sufficient for this slice
- Performance under high item-count workloads — not a requirement for this scorer

## Notes for Tester

- TUnit's `--treenode-filter` flag does not reliably isolate tests via `dotnet test` CLI. If you need to inspect only MetadataTrustScorer tests, run `dotnet test 2>&1 | grep -A2 "MetadataTrust"` instead.
- The `cargo test -- metadata_trust --nocapture` command isolates the 5 Rust conformance tests plus 1 doctest. The `--nocapture` flag shows pass/fail per test function.
- Vectors exist in THREE locations: `spec/conformance/required/scoring/`, `conformance/required/scoring/` (repo root), and `crates/cupel/conformance/required/scoring/`. The drift guard diff checks spec vs crates; the pre-commit hook checks root vs crates. Both must be clean.
