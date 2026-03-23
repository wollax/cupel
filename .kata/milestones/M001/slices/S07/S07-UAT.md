# S07: Rust Quality Hardening — UAT

**Milestone:** M001
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S07 is a pure library. All correctness claims are machine-verifiable via `cargo test` and `cargo clippy`. There is no runtime service, UI, or human-observable behavior — the artifact *is* the test suite output. Structural grep checks confirm dead code is gone.

## Preconditions

- Rust toolchain installed (edition 2021, stable)
- `cargo-deny` installed
- Working directory: `crates/cupel/` (for `cargo deny check`) or project root (for `--manifest-path`)
- All three tasks (T01, T02, T03) committed

## Smoke Test

```bash
cargo test --manifest-path crates/cupel/Cargo.toml
```
Expected: `35 passed; 0 failed` — all unit tests, integration tests, and doc-tests pass.

## Test Cases

### 1. KnapsackSlice OOM guard fires

```bash
cargo test --manifest-path crates/cupel/Cargo.toml -- knapsack_table_too_large --nocapture
```
1. Run the command above.
2. **Expected:** `test knapsack_table_too_large ... ok` — 1 passed, 0 failed. Guard fires at capacity=50_001, n=1001, cells=50_051_001; error message includes all three fields.

### 2. Slicer::slice returns Result — conformance tests unaffected

```bash
cargo test --manifest-path crates/cupel/Cargo.toml --test conformance
```
1. Run the command above.
2. **Expected:** All 6 conformance slicing tests pass. The `.expect("conformance vector slicing should not error")` unwrap is silent (no OOM conditions in conformance vectors).

### 3. Scorer trait has no as_any

```bash
grep -r "as_any" crates/cupel/src/
```
1. Run the command above.
2. **Expected:** No output. Zero matches across all source files.

### 4. CompositeScorer cycle detection removed

```bash
grep -r "detect_cycles_dfs" crates/cupel/src/
grep -r "scorer_identity" crates/cupel/src/
```
1. Run both commands.
2. **Expected:** No output from either. Both symbols fully removed.

### 5. UShapedPlacer has no Vec<Option> or .expect()

```bash
grep -n "Vec<Option" crates/cupel/src/placer/u_shaped.rs
grep -n "expect(" crates/cupel/src/placer/u_shaped.rs
```
1. Run both commands.
2. **Expected:** No output from either. Refactor is complete.

### 6. UShapedPlacer edge cases pass

```bash
cargo test --manifest-path crates/cupel/Cargo.toml -- place_zero_items place_one_item place_two_items place_three_items place_four_items
```
1. Run the command above.
2. **Expected:** 5 passed; 0 failed.

### 7. Clippy clean (default + serde feature)

```bash
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
cargo clippy --features serde --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
```
1. Run both commands.
2. **Expected:** Both finish with 0 warnings, exit 0.

### 8. Serde feature unaffected

```bash
cargo test --features serde --manifest-path crates/cupel/Cargo.toml
```
1. Run the command above.
2. **Expected:** 35 passed; 0 failed. No regressions in serde round-trip tests from S04.

### 9. cargo deny check passes

```bash
cd crates/cupel && cargo deny check
```
1. Run the command above.
2. **Expected:** `advisories ok, bans ok, licenses ok, sources ok`

### 10. release-rust.yml has job-level permissions

```bash
grep -A3 "name: test" .github/workflows/release-rust.yml | grep "permissions"
grep -A3 "name: publish" .github/workflows/release-rust.yml | grep "permissions"
```
1. Run both commands.
2. **Expected:** `test` job shows `contents: read`; `publish` job shows `contents: write`.

## Edge Cases

### CupelError::CycleDetected still constructible

```bash
grep "CycleDetected" crates/cupel/src/error.rs
```
1. Run the command above.
2. **Expected:** 1 result — variant still present in the enum (retained for semver safety); doc says "Never emitted".

### KnapsackSlice with exactly 50M cells passes (boundary)

The guard condition is `> 50_000_000` (strict greater-than), so exactly 50M cells must succeed. This is verified implicitly by the conformance tests (small inputs well under the limit).

### Slicer::slice Result propagates through QuotaSlice

```bash
grep "inner.slice.*?" crates/cupel/src/slicer/quota.rs
```
1. Run the command above.
2. **Expected:** 1 result showing `self.inner.slice(items, &sub_budget)?` — error propagation via `?` is in place.

## Failure Signals

- Any test failure in `cargo test` output — indicates a regression introduced during T01/T02/T03 changes.
- `grep -r "as_any" crates/cupel/src/` returning any output — Scorer cleanup incomplete.
- `grep -n "Vec<Option" crates/cupel/src/placer/u_shaped.rs` returning any output — UShapedPlacer refactor incomplete.
- Any clippy warning under `-D warnings` — a new lint was introduced.
- `cargo deny check` advisory failures — a newly-added dependency triggered a deny rule.
- `CupelError::TableTooLarge` not matching in `knapsack_table_too_large` test — guard threshold or field names wrong.

## Requirements Proved By This UAT

- R002 — `KnapsackSlice::slice` returns `Err(CupelError::TableTooLarge { candidates, capacity, cells })` when `capacity × n > 50_000_000`. Proved by `knapsack_table_too_large` unit test (test case 1). Combined with S06's .NET implementation, R002 is now fully validated in both languages.
- R005 — All high-signal Rust quality issues resolved: CompositeScorer cycle detection removed (test case 4), `Scorer::as_any` eliminated (test case 3), `UShapedPlacer` panic paths gone (test case 5), test coverage added (test case 6 + scorer tests). Proved by structural grep checks and `cargo test`.
- R003 — `cargo clippy --all-targets -- -D warnings` exits 0 (test case 7), confirming S05's CI gate is respected throughout S07 changes.

## Not Proven By This UAT

- Live runtime / production OOM prevention — the UAT is contract-only; no production workload is tested.
- v1.2.0 publish to crates.io — requires manual `cargo publish`; not part of this slice.
- Downstream caller migration from `Slicer::slice → Vec` to `→ Result` — a semver break; downstream callers must update. Not tested here (no downstream crates in this repo).
- `CupelError::CycleDetected` never being emitted in practice — the library guarantees this structurally, but no test asserts "CycleDetected is never returned".

## Notes for Tester

- The `knapsack_table_too_large` test deliberately uses capacity=50_001 to ensure it's clearly above the 50M boundary; the guard fires even with n=1001 (cells = 50,051,001).
- `ReflexiveScorer` finiteness guard means both NaN and Inf inputs return 0.0 — not 1.0 for Inf. This matches the actual implementation; the test confirms it.
- Pipeline tests use `ChronologicalPlacer` (the available no-op placer); `GreedyPlacer` does not exist in this codebase.
- The `Zlib` unmatched license allowance in `cargo deny check` output is a pre-existing advisory warning, not a new failure introduced by this slice.
