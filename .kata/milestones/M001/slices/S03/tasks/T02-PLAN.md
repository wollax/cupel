---
estimated_steps: 4
estimated_files: 2
---

# T02: Conformance harness: diagnostics parsing and 5 test cases

**Slice:** S03 — Pipeline run_traced & DryRun
**Milestone:** M001

## Description

Extend the existing pipeline conformance harness to parse the `[expected.diagnostics.*]` sections present in all five diagnostics conformance vectors, and add one test function per vector. This closes the R001 validation gate: the vectors authored in S01 were inert until now; after this task, CI runs them as integration tests against the real pipeline implementation from T01.

## Steps

1. **Update `conformance.rs` imports** — in the `use cupel::{...}` block in `tests/conformance.rs`, add `DiagnosticTraceCollector, ExclusionReason, InclusionReason, SelectionReport, TraceDetailLevel` to the existing import. These are needed by the new diagnostics test helpers in `pipeline.rs`.

2. **Add `exclusion_reason_tag` helper and `run_pipeline_diagnostics_test` in `pipeline.rs`** — add:
   ```rust
   fn exclusion_reason_tag(reason: &cupel::ExclusionReason) -> &'static str {
       match reason {
           cupel::ExclusionReason::BudgetExceeded { .. } => "BudgetExceeded",
           cupel::ExclusionReason::NegativeTokens { .. } => "NegativeTokens",
           cupel::ExclusionReason::Deduplicated { .. } => "Deduplicated",
           cupel::ExclusionReason::PinnedOverride { .. } => "PinnedOverride",
           cupel::ExclusionReason::ScoredTooLow { .. } => "ScoredTooLow",
           cupel::ExclusionReason::QuotaCapExceeded { .. } => "QuotaCapExceeded",
           cupel::ExclusionReason::QuotaRequireDisplaced { .. } => "QuotaRequireDisplaced",
           cupel::ExclusionReason::Filtered { .. } => "Filtered",
           _ => "Unknown",
       }
   }
   
   fn inclusion_reason_tag(reason: &cupel::InclusionReason) -> &'static str {
       match reason {
           cupel::InclusionReason::Scored => "Scored",
           cupel::InclusionReason::Pinned => "Pinned",
           cupel::InclusionReason::ZeroToken => "ZeroToken",
           _ => "Unknown",
       }
   }
   ```
   Then add `run_pipeline_diagnostics_test(vector_path: &str)`:
   - Load vector and build pipeline/items/budget via existing helpers (`load_vector`, `build_items`, `build_pipeline_from_config` (the existing private function), budget extraction — reuse the exact same budget-extraction code from `run_pipeline_test`)
   - Create `let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);`
   - Call `pipeline.run_traced(&items, &budget, &mut collector).expect("run_traced should succeed");`
   - `let report = collector.into_report();`
   - Read `[expected.diagnostics.summary]`:
     - `total_candidates`: `assert_eq!(report.total_candidates, expected_tc as usize, "total_candidates mismatch")`
     - `total_tokens_considered`: `assert_eq!(report.total_tokens_considered, expected_ttc, "total_tokens_considered mismatch")`
   - Read `[[expected.diagnostics.included]]` array (if present):
     - `assert_eq!(report.included.len(), expected_included.len(), "included count mismatch: expected {}, got {}", ...)`
     - For each entry in order: extract `content`, `score_approx`, `inclusion_reason` string; find matching `report.included[i]`; assert content, score within epsilon (from `[tolerance.score_epsilon]` or 1e-9), reason string via `inclusion_reason_tag`
   - Read `[[expected.diagnostics.excluded]]` array (if present):
     - `assert_eq!(report.excluded.len(), expected_excluded.len(), "excluded count mismatch: expected {}, got {}", ...)`
     - For each entry in order: extract `content`, `score_approx`, `exclusion_reason` string; find matching `report.excluded[i]`; assert content, score within epsilon, reason tag; then check variant-specific fields: for "NegativeTokens" check `tokens` if present; for "Deduplicated" check `deduplicated_against` if present; for "BudgetExceeded" check `item_tokens` and `available_tokens` if present; for "PinnedOverride" check `displaced_by` if present; use `if let ExclusionReason::BudgetExceeded { item_tokens, available_tokens } = &report.excluded[i].reason` etc.

3. **Add 5 test functions** in `pipeline.rs`:
   ```rust
   #[test]
   fn diag_negative_tokens() {
       run_pipeline_diagnostics_test("pipeline/diag-negative-tokens.toml");
   }
   #[test]
   fn diag_deduplicated() {
       run_pipeline_diagnostics_test("pipeline/diag-deduplicated.toml");
   }
   #[test]
   fn diag_pinned_override() {
       run_pipeline_diagnostics_test("pipeline/diag-pinned-override.toml");
   }
   #[test]
   fn diag_scored_inclusion() {
       run_pipeline_diagnostics_test("pipeline/diag-scored-inclusion.toml");
   }
   #[test]
   fn diagnostics_budget_exceeded() {
       run_pipeline_diagnostics_test("pipeline/diagnostics-budget-exceeded.toml");
   }
   ```

4. **Verify and fix assertion messages** — run `cargo test --test conformance -- pipeline` and iterate until all 10 tests pass. If any diagnostics test fails, check the SelectionReport fields against the vector manually. Assertion messages should name the specific field that mismatched (content, score, reason tag, extra field). Common failure modes: (a) PinnedOverride detection off-by-one in the rule (check item_tokens vs effective_target vs budget.target_tokens()); (b) score lookup miss for included items (check score_lookup build from `sorted` vs the actual scorer output); (c) total_tokens_considered mismatch (should include negative-token items' tokens, since `total_tokens = items.iter().map(|i| i.tokens()).sum()`).

## Must-Haves

- [ ] `run_pipeline_diagnostics_test` function present in `pipeline.rs`
- [ ] `exclusion_reason_tag` and `inclusion_reason_tag` helpers present
- [ ] Five new `#[test]` functions present: `diag_negative_tokens`, `diag_deduplicated`, `diag_pinned_override`, `diag_scored_inclusion`, `diagnostics_budget_exceeded`
- [ ] `cargo test --test conformance -- pipeline` passes all 10 tests (5 existing + 5 new)
- [ ] `cargo clippy --all-targets -- -D warnings 2>&1 | grep -E "^warning|^error"` → zero

## Verification

- `cd crates/cupel && cargo test --test conformance -- pipeline 2>&1 | grep -E "FAILED|^error"` → zero failures
- `cd crates/cupel && cargo test --test conformance -- pipeline 2>&1 | grep "test conformance::pipeline"` → shows 10 lines with "ok"
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings 2>&1 | grep -E "^warning|^error"` → zero

## Observability Impact

- Signals added/changed: Five new conformance test cases that assert on `SelectionReport` fields — these are the executable specification for `run_traced` correctness; any future regression in stage instrumentation will produce a named failing test with the specific mismatched field
- How a future agent inspects this: `cargo test --test conformance -- pipeline::diag --nocapture` runs only the 5 new tests with assertion details; individual test names map to their vector file names
- Failure state exposed: assertion messages print expected vs actual values; `--nocapture` shows them; test name indicates which vector/scenario failed

## Inputs

- `crates/cupel/src/pipeline/mod.rs` (from T01) — `run_traced<C: TraceCollector>` method on `Pipeline`
- `crates/cupel/tests/conformance/pipeline.rs` — existing helpers: `build_pipeline_from_config`, `run_pipeline_test`; existing budget-extraction code (to reuse, not duplicate)
- `crates/cupel/tests/conformance.rs` — existing `use cupel::{...}` import block; `load_vector`, `build_items` helpers
- `crates/cupel/conformance/required/pipeline/diag-*.toml` — 4 vectors (diag-negative-tokens, diag-deduplicated, diag-pinned-override, diag-scored-inclusion)
- `crates/cupel/conformance/required/pipeline/diagnostics-budget-exceeded.toml` — 1 vector (the pre-existing one)
- Key fields to parse per vector:
  - `[expected.diagnostics.summary]` → `total_candidates: i64`, `total_tokens_considered: i64`
  - `[[expected.diagnostics.included]]` → `content: &str`, `score_approx: f64`, `inclusion_reason: &str`
  - `[[expected.diagnostics.excluded]]` → `content: &str`, `score_approx: f64`, `exclusion_reason: &str`; plus optional: `tokens: i64` (NegativeTokens), `deduplicated_against: &str` (Deduplicated), `item_tokens: i64` + `available_tokens: i64` (BudgetExceeded), `displaced_by: &str` (PinnedOverride)

## Expected Output

- `crates/cupel/tests/conformance/pipeline.rs` — new functions: `exclusion_reason_tag`, `inclusion_reason_tag`, `run_pipeline_diagnostics_test`; five new `#[test]` functions
- `crates/cupel/tests/conformance.rs` — updated import block with `DiagnosticTraceCollector`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, `TraceDetailLevel`
