---
id: T02
parent: S01
milestone: M001
provides:
  - 4 new diagnostics conformance vectors in spec/conformance/required/pipeline/ and crates/cupel/conformance/required/pipeline/
key_files:
  - spec/conformance/required/pipeline/diag-negative-tokens.toml
  - spec/conformance/required/pipeline/diag-deduplicated.toml
  - spec/conformance/required/pipeline/diag-pinned-override.toml
  - spec/conformance/required/pipeline/diag-scored-inclusion.toml
  - crates/cupel/conformance/required/pipeline/diag-negative-tokens.toml
  - crates/cupel/conformance/required/pipeline/diag-deduplicated.toml
  - crates/cupel/conformance/required/pipeline/diag-pinned-override.toml
  - crates/cupel/conformance/required/pipeline/diag-scored-inclusion.toml
key_decisions:
  - none new
patterns_established:
  - "diag-*.toml vectors follow the diagnostics-budget-exceeded.toml schema: [test], [budget], [config], [[config.scorers]], [[items]], [[expected_output]], [expected.diagnostics.summary], [[expected.diagnostics.included]], [[expected.diagnostics.excluded]]"
  - "PinnedOverride scenario documented with S03 implementation note: with greedy slicer, pinned-token subtraction from effective_target mathematically prevents Place/Truncate overflow, so PinnedOverride is classified as a Slice-stage exclusion caused by pinned budget consumption; S03 diagnostics layer should map this to PinnedOverride"
observability_surfaces:
  - "cat spec/conformance/required/pipeline/diag-*.toml — readable scenario docs"
  - "diff -r spec/conformance/ crates/cupel/conformance/ — drift detection"
duration: short
verification_result: passed
completed_at: 2026-03-17
blocker_discovered: false
---

# T02: Author 4 conformance vectors and vendor to crates/

**Authored 4 diagnostics conformance vectors (NegativeTokens, Deduplicated, PinnedOverride, Scored) in both spec/ and crates/, with zero drift.**

## What Happened

Read `diagnostics-budget-exceeded.toml` and `pinned-items.toml` as schema references, then traced the pipeline stages (classify → score → deduplicate → sort → slice → place) for each scenario to produce self-consistent `expected_output` values.

Each vector includes `[expected.diagnostics.summary]`, `[[expected.diagnostics.included]]`, and (where applicable) `[[expected.diagnostics.excluded]]` sections as spec targets for S03.

**PinnedOverride scenario analysis:** A careful trace of `place.rs` and `classify.rs` revealed that with the greedy slicer, `compute_effective_budget` subtracts pinned tokens from `effective_target` before the slicer runs. This means `sliced_tokens ≤ effective_target = target − pinned_tokens`, so `merged_tokens ≤ target` always holds — the Place/Truncate overflow path is never reached. The regular item is excluded at the Slice stage, not the Place stage. The vector documents this finding in a prominent S03 comment and specifies `PinnedOverride` as the intended diagnostic classification (the S03 layer should detect that the Slice BudgetExceeded was caused by pinned budget consumption and emit PinnedOverride accordingly). The `expected_output` is self-consistent with what the current pipeline produces.

All 4 vectors were copied verbatim with `cp diag-*.toml ...`, drift guard confirmed clean.

## Verification

```
# Drift guard: exits 0
diff -rq spec/conformance/required/pipeline/ crates/cupel/conformance/required/pipeline/
→ (no output, exit 0)

# 4 new diag-*.toml in spec (note: diag* glob also matches diagnostics-budget-exceeded.toml)
ls spec/conformance/required/pipeline/diag-*.toml | wc -l  → 4

# cargo test: all 28 conformance tests + 33 doctests pass
cargo test --test conformance → 28 passed, 0 failed
cargo test (all) → 33 doctests passed, 0 failed

# cargo doc: no warnings
cargo doc --no-deps → DOC OK

# cargo clippy: no warnings
cargo clippy --all-targets -- -D warnings → Finished (0 warnings)
```

## Diagnostics

- Drift: `diff -r spec/conformance/ crates/cupel/conformance/` surfaces any divergence
- Scenario docs: `cat spec/conformance/required/pipeline/diag-*.toml` shows full stage traces in comments
- Malformed TOML: `cargo test` parse errors will surface immediately when S03 harness loads these files

## Deviations

- **diag-pinned-override.toml PinnedOverride trigger**: The task plan proposed a scenario (pinned=120, regular=80, target=150) and correctly noted to verify self-consistency. Mathematical analysis confirms this scenario cannot trigger Place/Truncate overflow with the greedy slicer. The vector was authored with the correct `expected_output` (current runtime behavior: only pinned-item) and the `exclusion_reason = "PinnedOverride"` is declared as the S03 diagnostics spec target with a detailed implementation note in the TOML comments.

- **Slice plan verification count**: The slice plan check `ls spec/conformance/required/pipeline/diag*.toml spec/conformance/required/pipeline/diagnostics-budget-exceeded.toml | wc -l` expects 5, but the glob `diag*` already matches `diagnostics-budget-exceeded.toml`, so the command produces 6 (double-counts that file). The actual distinct diagnostics file count is correct (4 new + 1 existing = 5).

## Known Issues

None.

## Files Created/Modified

- `spec/conformance/required/pipeline/diag-negative-tokens.toml` — NegativeTokens exclusion vector
- `spec/conformance/required/pipeline/diag-deduplicated.toml` — Deduplicated exclusion vector
- `spec/conformance/required/pipeline/diag-pinned-override.toml` — PinnedOverride exclusion vector (with S03 impl note)
- `spec/conformance/required/pipeline/diag-scored-inclusion.toml` — Scored inclusion vector (both items included)
- `crates/cupel/conformance/required/pipeline/diag-negative-tokens.toml` — vendored copy
- `crates/cupel/conformance/required/pipeline/diag-deduplicated.toml` — vendored copy
- `crates/cupel/conformance/required/pipeline/diag-pinned-override.toml` — vendored copy
- `crates/cupel/conformance/required/pipeline/diag-scored-inclusion.toml` — vendored copy
