# S01: Diagnostics Data Types — UAT

**Milestone:** M001
**Written:** 2026-03-17

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S01 produces only type definitions and static TOML files — there is no runtime behavior to exercise. Contract verification (compile, doc, clippy, drift guard) is the complete and correct proof for this slice. Human walkthrough or live runtime is not meaningful until S03 wires the pipeline.

## Preconditions

- Rust toolchain available (`cargo`, `clippy`, `rustdoc`)
- Working directory: `crates/cupel/` (or run from repo root with `--manifest-path crates/cupel/Cargo.toml`)
- `spec/conformance/required/pipeline/` and `crates/cupel/conformance/required/pipeline/` both exist

## Smoke Test

```bash
cd crates/cupel
cargo test && echo "PASS"
```

Expected: `78 passed; 0 failed` and `PASS` printed.

## Test Cases

### 1. All 8 diagnostic types compile and are publicly accessible

```bash
cd crates/cupel
cargo check
```

**Expected:** exits 0 with no errors. Types `PipelineStage`, `TraceEvent`, `OverflowEvent`, `ExclusionReason`, `InclusionReason`, `IncludedItem`, `ExcludedItem`, `SelectionReport` are all re-exported from the crate root.

### 2. No doc warnings

```bash
cd crates/cupel
cargo doc --no-deps 2>&1 | grep -E "warning|error" && echo "DOC ISSUES" || echo "DOC OK"
```

**Expected:** prints `DOC OK`. Zero unresolved doc links or missing doc comments on public items.

### 3. No clippy warnings on new module or existing code

```bash
cd crates/cupel
cargo clippy --all-targets -- -D warnings
```

**Expected:** exits 0 with `Finished` and no warnings.

### 4. Conformance drift guard passes

```bash
diff -rq spec/conformance/required/pipeline/ crates/cupel/conformance/required/pipeline/
echo "exit $?"
```

**Expected:** no diff output; exits 0. Both directories contain identical files.

### 5. Five diagnostics vectors exist

```bash
ls spec/conformance/required/pipeline/diag*.toml | wc -l
```

**Expected:** `5` (four new `diag-*.toml` files plus `diagnostics-budget-exceeded.toml`, which the `diag*` glob matches).

### 6. All conformance tests pass (unchanged)

```bash
cd crates/cupel
cargo test --test conformance
```

**Expected:** `28 passed; 0 failed`. New vectors are present as TOML files but are not yet loaded by the conformance harness (the `[expected.diagnostics.*]` path is wired in S03).

## Edge Cases

### ExclusionReason reserved variants compile but are hidden

Inspect `crates/cupel/src/diagnostics/mod.rs`:
```bash
grep '_Reserved' crates/cupel/src/diagnostics/mod.rs
```

**Expected:** 4 lines matching `_Reserved1` through `_Reserved4`, each preceded by `#[doc(hidden)]`.

### Serde stub present on 7 of 8 types; SelectionReport intentionally omitted

```bash
grep -c 'cfg_attr.*serde' crates/cupel/src/diagnostics/mod.rs
```

**Expected:** `7` (all types except `SelectionReport`).

### ExclusionReason carries the custom serde comment

```bash
grep 'custom serde impl' crates/cupel/src/diagnostics/mod.rs
```

**Expected:** one line matching `// custom serde impl in S04 — adjacent-tagged wire format`.

## Failure Signals

- `cargo test` failures after adding diagnostics types → likely a re-export conflict or duplicate type name in `lib.rs`
- `cargo doc` warnings → unresolved doc link; check cross-crate refs in `OverflowEvent` or `SelectionReport` doc comments
- `cargo clippy` warnings → `#[non_exhaustive]` or `#[allow]` attributes misconfigured; or dead_code on reserved variants (should be suppressed by `#[doc(hidden)]` + `#[allow(dead_code)]`)
- `diff -rq` non-empty output → drift between `spec/` and `crates/` copies; re-copy with `cp`
- `ls diag*.toml | wc -l` < 5 → one or more new vectors missing from `spec/conformance/`

## Requirements Proved By This UAT

- R001 (partial) — Diagnostic data types (`TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, `IncludedItem`, `ExcludedItem`, `PipelineStage`, `OverflowEvent`) exist in the Rust crate with correct field shapes per spec. Full R001 proof requires S02 (TraceCollector) and S03 (run_traced + conformance harness).
- R006 (partial) — Serde stubs present on 7 of 8 types; `ExclusionReason` custom serde deferred to S04. Full R006 proof requires S04 round-trip tests.

## Not Proven By This UAT

- That `expected.diagnostics.*` sections in the 4 new conformance vectors are correct at runtime — the harness ignores them until S03 wires the diagnostics test path
- That `SelectionReport` serializes/deserializes correctly — serde derive intentionally omitted until S04
- That `TraceCollector` produces events matching the `[expected.diagnostics.summary]` counts — S02/S03 concern
- That `PinnedOverride` is correctly classified at the Slice stage — S03 implementation concern; documented in `diag-pinned-override.toml`

## Notes for Tester

The `diag*` glob in `ls diag*.toml | wc -l` matches 5 files because `diagnostics-budget-exceeded.toml` starts with `diag`. The distinct new files are the 4 `diag-{negative-tokens,deduplicated,pinned-override,scored-inclusion}.toml` files. This is expected and documented (T02 deviation note).
