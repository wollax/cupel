# S01: Add on_pipeline_completed hook to core cupel TraceCollector — UAT

**Milestone:** M008
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S01 is a library crate change with no user-facing runtime behavior. Correctness is fully proven by `cargo test --all-targets` (integration tests with a `SpyCollector`) and `cargo clippy`. No UI, service, or human-observable output exists. Human verification adds no signal beyond what the test suite already captures.

## Preconditions

- Rust toolchain installed (`rustup default stable` or equivalent)
- Working directory: `crates/cupel/`
- No prior build artifacts required; `cargo` handles all dependencies

## Smoke Test

```bash
cd crates/cupel && cargo test on_pipeline_completed
```

Both `on_pipeline_completed_called_once_with_five_snapshots` and `on_pipeline_completed_not_called_for_null_collector` must show `ok`.

## Test Cases

### 1. All tests pass with no regressions

```bash
cd crates/cupel && cargo test --all-targets
```

**Expected:** `test result: ok. N passed; 0 failed` across all test binaries (N ≥ 170). No test named `on_pipeline_completed_called_once_with_five_snapshots` should show `FAILED`.

### 2. Clippy clean

```bash
cd crates/cupel && cargo clippy --all-targets -- -D warnings
```

**Expected:** `Finished` with no `error` or `warning` lines.

### 3. Hook called with exactly 5 stage snapshots

```bash
cd crates/cupel && cargo test on_pipeline_completed_called_once_with_five_snapshots -- --nocapture
```

**Expected:** Test output shows `ok`; no panic, no assertion failure mentioning `called == 0` or `expected 5 snapshots`.

### 4. NullTraceCollector path: hook not called

```bash
cd crates/cupel && cargo test on_pipeline_completed_not_called_for_null_collector -- --nocapture
```

**Expected:** `ok`; confirms `run_traced` with `NullTraceCollector` completes without calling the override (zero-cost path validated).

### 5. Default no-op does not panic

```bash
cd crates/cupel && cargo test on_pipeline_completed_default_is_noop -- --nocapture
```

**Expected:** `ok`; calling the defaulted method directly on `NullTraceCollector` and `DiagnosticTraceCollector` with real arguments does not panic.

## Edge Cases

### StageTraceSnapshot is externally readable but not externally constructible

Read the type definition:

```bash
grep -A 10 'pub struct StageTraceSnapshot' crates/cupel/src/diagnostics/mod.rs
```

**Expected:** `#[non_exhaustive]` attribute present. Fields `stage`, `item_count_in`, `item_count_out`, `duration_ms`, `excluded` are all `pub`. No constructor or builder needed for reading (S02 use case).

### Snapshot count is always 5 when collector is enabled

```bash
grep -c 'stage_snapshots.push' crates/cupel/src/pipeline/mod.rs
```

**Expected:** `5`

## Failure Signals

- `on_pipeline_completed_called_once_with_five_snapshots` FAILED with "on_pipeline_completed must be called exactly once" → hook call not wired in `run_with_components`
- `on_pipeline_completed_called_once_with_five_snapshots` FAILED with "expected 5 snapshots" → one or more stage push blocks missing
- Any existing test regression → unintended side effect in `pipeline/mod.rs` changes
- `cargo clippy` exits non-zero → lint violation introduced

## Requirements Proved By This UAT

- R058 (partial) — the `on_pipeline_completed` hook exists in the core `cupel` crate as a defaulted no-op; `run_with_components` calls it with structured `StageTraceSnapshot` data; all existing tests pass (additive, non-breaking); core crate has zero `opentelemetry` dependency. This proves the S01 prerequisite for the OTel bridge crate.

## Not Proven By This UAT

- `CupelOtelTraceCollector` does not exist yet — OTel span emission is S02.
- Three verbosity tiers (StageOnly, StageAndExclusions, Full) are not exercised — S02.
- `cargo package --dry-run` for `cupel-otel` — S03.
- R058 full validation (requires S02 + S03 complete with in-memory exporter tests and spec update).

## Notes for Tester

The integration test in `crates/cupel/tests/on_pipeline_completed.rs` is the primary artifact. Read it to understand the `SpyCollector` contract and what "5 snapshots in order" means. The test asserts stage variants in Classify → Score → Deduplicate → Slice → Place order — any reordering would be caught here.
