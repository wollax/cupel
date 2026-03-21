# S02: TraceCollector Trait & Implementations — UAT

**Milestone:** M001
**Written:** 2026-03-17

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All four types are library primitives with no runtime process or UI surface. Behavioral contracts are fully provable via `cargo test`, compile-time size assertions, and `cargo clippy`. No human experience or live-runtime verification is required.

## Preconditions

- Working directory: `crates/cupel/`
- `cargo` available (Rust toolchain on MSRV 1.85+)
- S01 types present (`TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, `IncludedItem`, `ExcludedItem`)

## Smoke Test

```
cd crates/cupel && cargo test --lib -- diagnostics::trace_collector
```

Expected: all 12 tests pass, 0 failed.

## Test Cases

### 1. NullTraceCollector ZST invariant

1. Run `cargo test --lib -- null_is_zst`
2. **Expected:** test passes; `size_of::<NullTraceCollector>() == 0` holds at compile time

### 2. NullTraceCollector is disabled and all methods are no-ops

1. Run `cargo test --lib -- null_is_not_enabled null_record_methods_are_noop`
2. **Expected:** both tests pass; no panics, `is_enabled()` returns `false`

### 3. DiagnosticTraceCollector is_enabled returns true

1. Run `cargo test --lib -- diagnostic_is_enabled`
2. **Expected:** test passes for both `Stage` and `Item` detail levels

### 4. Stage-level gating suppresses item events

1. Run `cargo test --lib -- stage_level_only_records_stage_events`
2. **Expected:** test passes; `record_item_event` with `Stage` detail level results in 1 event in report (not 2)

### 5. Item-level records both event types

1. Run `cargo test --lib -- item_level_records_both`
2. **Expected:** test passes; both `record_stage_event` and `record_item_event` result in 2 events total

### 6. into_report sort contract — score descending

1. Run `cargo test --lib -- into_report_sort_contract_score_desc`
2. **Expected:** test passes; excluded[0].score == 5.0, excluded[1].score == 2.0

### 7. into_report sort stable on tie

1. Run `cargo test --lib -- into_report_sort_stable_on_tie`
2. **Expected:** test passes; items with equal score appear in insertion order (first recorded at index 0)

### 8. Callback invoked on stage event

1. Run `cargo test --lib -- callback_invoked_on_stage_event`
2. **Expected:** test passes; callback counter == 1 after one `record_stage_event` call

### 9. Item recording populates SelectionReport

1. Run `cargo test --lib -- item_recording_populates_report`
2. **Expected:** test passes; `record_included`, `record_excluded`, `set_candidates` all correctly populate `SelectionReport` fields

### 10. All four types re-exported from crate root

1. Run `grep -r 'TraceCollector\|NullTraceCollector\|DiagnosticTraceCollector\|TraceDetailLevel' crates/cupel/src/lib.rs`
2. **Expected:** all four names appear in the output

## Edge Cases

### Clippy clean with --all-targets

1. Run `cargo clippy --all-targets -- -D warnings`
2. **Expected:** exit 0, zero warnings and zero errors (including type_complexity on struct fields)

### Doc build clean

1. Run `cargo doc --no-deps`
2. **Expected:** exit 0, zero warnings — all four types have rendered API docs

## Failure Signals

- Any `FAILED` line in `cargo test --lib` output
- Any `warning` or `error` in `cargo clippy --all-targets -- -D warnings` output
- Any `warning` or `error` in `cargo doc --no-deps` output
- Missing names in `grep` check against `lib.rs`
- `size_of::<NullTraceCollector>() != 0` (would be a compile error via `assert_eq!` at const eval time)

## Requirements Proved By This UAT

- R001 (partial) — The `TraceCollector` trait contract, `NullTraceCollector` (ZST zero-cost path), and `DiagnosticTraceCollector` (buffered recording with `into_report`) are proven correct by behavioral contract tests. This proves the S02 deliverable of R001; the full validation of R001 requires S03's `run_traced` wiring and conformance vector pass.

## Not Proven By This UAT

- R001 end-to-end — `pipeline.run_traced(&mut collector)` producing a correct `SelectionReport` against conformance vectors is S03's proof responsibility.
- R006 — Serde round-trip for `DiagnosticTraceCollector` is deferred to S04; serde stubs are present but not tested.
- Zero runtime overhead of `NullTraceCollector` at the machine-code level — the ZST assertion proves the type has zero size, and Rust's monomorphization guarantees are relied upon, but no assembly-level inspection was performed. This is an accepted theoretical guarantee, not an empirical measurement.

## Notes for Tester

- Run all verification from `crates/cupel/` directory, not the repo root (no workspace Cargo.toml).
- `cargo test --lib -- diagnostics::trace_collector` scopes cleanly to the 12 S02 tests; the other 17 tests are pre-existing model/budget/kind tests.
- The `TraceEventCallback` type alias is a public type re-exported from the crate — it appears in `cargo doc` output as part of the public API.
