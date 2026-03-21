# S03: Pipeline run_traced & DryRun — UAT

**Milestone:** M001
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All observable behavior is machine-verifiable via `cargo test` and `cargo clippy`. There is no UI, no runtime service, and no human-facing interaction. The conformance test harness exercises the full pipeline end-to-end against specification-authored vectors. No live runtime is required.

## Preconditions

- `cd crates/cupel` (all commands run from this directory)
- `cargo` available and dependencies fetched (`cargo fetch` if offline)

## Smoke Test

```
cargo test --test conformance -- pipeline 2>&1 | grep "test result"
```

Expected: `test result: ok. 10 passed; 0 failed`

## Test Cases

### 1. All pipeline conformance tests pass (10 total)

```
cargo test --test conformance -- pipeline
```

Expected: 10 tests pass, 0 failed — 5 original pipeline tests + 5 new diagnostics tests (diag_negative_tokens, diag_deduplicated, diag_pinned_override, diag_scored_inclusion, diagnostics_budget_exceeded).

### 2. All unit tests unaffected

```
cargo test --lib
```

Expected: 29 tests pass, 0 failed.

### 3. Clippy clean

```
cargo clippy --all-targets -- -D warnings
```

Expected: zero warnings, zero errors.

### 4. Doc build clean

```
cargo doc --no-deps
```

Expected: zero warnings, zero errors.

### 5. Both new public methods present

```
grep -E "pub fn run_traced|pub fn dry_run" src/pipeline/mod.rs
```

Expected: both `pub fn run_traced` and `pub fn dry_run` appear.

### 6. dry_run returns a SelectionReport (doctest)

```
cargo test --doc 2>&1 | grep -E "FAILED|test result"
```

Expected: all doctests pass including the `run_traced` and `dry_run` doctests.

## Edge Cases

### Diagnostics tests only (with assertion detail)

```
cargo test --test conformance -- pipeline::diag --nocapture
```

Expected: 5 diagnostics tests run with field-level assertion messages visible. Each test prints expected vs actual values for any mismatch.

### NullTraceCollector path still compiles and run() unaffected

```
cargo test --test conformance -- pipeline::run_
```

Expected: the 5 original `run_*` tests pass unchanged — confirms `run()` destructures new tuple returns correctly.

## Failure Signals

- Any `FAILED` in `cargo test` output
- Any `^warning` or `^error` from `cargo clippy --all-targets -- -D warnings`
- Any `warning|error` from `cargo doc --no-deps`
- `grep` finding zero matches for `pub fn run_traced` or `pub fn dry_run`
- `cargo build` producing error output

## Requirements Proved By This UAT

- R001 (partial) — `run_traced` and `dry_run` exist in the Rust crate with correct behavior proven against all 5 specification-authored diagnostics conformance vectors. The full R001 validation (including serde round-trip) completes in S04.

## Not Proven By This UAT

- R001 serde integration — `SelectionReport` serialization/deserialization is not yet implemented; that is S04's scope.
- Zero-cost NullTraceCollector allocation guarantee — the is_enabled() guard is in place and enforced by convention, but no micro-benchmark confirms zero allocations in the disabled path.
- Concurrent run_traced safety — no concurrent invocation tests; per-invocation ownership model (D001) is the design guarantee.
- Live runtime / operational use — this is a library; no process, no service, no deployment.

## Notes for Tester

All test cases are automated and deterministic. Run `cargo test --test conformance -- pipeline` for the primary check. If any of the 5 diagnostics tests fail, run with `--nocapture` to see field-level assertion messages that identify the exact mismatch (content, score, reason, count, or variant-specific field).
