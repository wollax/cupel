# S02: 13 assertion patterns

**Goal:** All 13 spec assertion patterns are implemented on `SelectionReportAssertionChain` with positive/negative integration tests and structured panic messages; `cargo test --all-targets` passes across both crates; `cargo clippy --all-targets -- -D warnings` is clean.
**Demo:** A caller can write `report.should().include_item_with_kind(kind).have_at_least_n_exclusions(1).place_top_n_scored_at_edges(2)` and every assertion in the chain either passes silently or panics with a structured message naming the assertion, what was expected, and what was found.

## Must-Haves

- `chain.rs` has all 13 assertion methods, each returning `&mut Self` and panicking on failure (D128)
- `#[allow(dead_code)]` removed from the `report` field in `chain.rs` (first assertion method reads it)
- Structured panic messages follow the spec error message contract: `"{assertion_name} failed: expected {expected}, but found {actual}."` format
- Pattern 6 (`have_excluded_item_with_budget_details`) is the Rust variant taking `(predicate, expected_item_tokens, expected_available_tokens)` with `ExclusionReason::BudgetExceeded { item_tokens, available_tokens }` destructure
- Pattern 13 (`place_top_n_scored_at_edges`) uses the index-based approach — no `HashSet<&IncludedItem>` (blocked by f64/Hash; see research open risk)
- Two integration tests per pattern (1 positive + 1 negative) in `crates/cupel-testing/tests/assertions.rs`
- `cargo test --all-targets` passes in both `cupel` and `cupel-testing`
- `cargo clippy --all-targets -- -D warnings` clean in both crates

## Proof Level

- This slice proves: integration
- Real runtime required: yes — all tests run mini-pipelines via `Pipeline::run_traced()` with `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`
- Human/UAT required: no

## Verification

```bash
# Both of these must exit 0:
cd /Users/wollax/Git/personal/cupel/crates/cupel-testing && cargo test --all-targets 2>&1 | tail -5
cd /Users/wollax/Git/personal/cupel/crates/cupel-testing && cargo clippy --all-targets -- -D warnings 2>&1 | tail -5
cd /Users/wollax/Git/personal/cupel/crates/cupel && cargo test --all-targets 2>&1 | tail -5
cd /Users/wollax/Git/personal/cupel/crates/cupel && cargo clippy --all-targets -- -D warnings 2>&1 | tail -5
```

Test file: `crates/cupel-testing/tests/assertions.rs`
- 26 tests total (2 per pattern × 13 patterns)
- Positive tests: assertion passes on a real pipeline report that satisfies the condition
- Negative tests: assertion panics with the spec error message prefix (verified via `#[should_panic(expected = "...")]`)

## Observability / Diagnostics

- Runtime signals: `cargo test` output shows individual test pass/fail with test names matching the pattern name (e.g., `test include_item_with_kind_passes`, `test include_item_with_kind_panics`)
- Inspection surfaces: `cargo test -- --nocapture` shows full panic messages when debugging negative test failures
- Failure visibility: panic messages carry assertion name + expected + actual values; `#[should_panic(expected = "...")]` provides a pinpoint substring to verify the message
- Redaction constraints: none — test data is synthetic item content with no secrets

## Integration Closure

- Upstream surfaces consumed:
  - `crates/cupel-testing/src/chain.rs` — `SelectionReportAssertionChain<'a>` struct with `pub(crate)` constructor
  - `crates/cupel-testing/src/lib.rs` — `SelectionReportAssertions` trait + `should()` entry point
  - `crates/cupel/src/analytics.rs` — `budget_utilization` and `kind_diversity` free functions
  - `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport`, `IncludedItem`, `ExcludedItem`, `ExclusionReason`, `InclusionReason`
  - `crates/cupel-testing/tests/smoke.rs` — `DiagnosticTraceCollector::new(TraceDetailLevel::Item).into_report()` pattern
- New wiring introduced in this slice:
  - 13 assertion methods on `SelectionReportAssertionChain`
  - Integration test file `crates/cupel-testing/tests/assertions.rs` with 26 tests
- What remains before the milestone is truly usable end-to-end:
  - S03: integration tests on real `Pipeline::run_traced()` output + `cargo package` readiness + publish metadata

## Tasks

- [x] **T01: Implement patterns 1–7 (existence/count/reason checks) with tests** `est:45m`
  - Why: Patterns 1–7 are the simpler half — existence and count checks over `included` and `excluded` lists. Implementing them first also removes the `#[allow(dead_code)]` from `chain.rs` and establishes the test construction pattern for T02.
  - Files: `crates/cupel-testing/src/chain.rs`, `crates/cupel-testing/tests/assertions.rs`
  - Do: (1) Remove `#[allow(dead_code)]` from `report` field; (2) Add `include_item_with_kind`, `include_item_matching`, `include_exact_n_items_with_kind`, `exclude_item_with_reason`, `exclude_item_matching_with_reason`, `have_excluded_item_with_budget_details`, `have_no_exclusions_for_kind` — each returning `&mut Self`, panicking with spec messages; (3) Create `tests/assertions.rs` with 14 tests (2 per pattern) using mini-pipelines via `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`; (4) Verify `cargo test --all-targets` and `cargo clippy --all-targets -- -D warnings` are clean
  - Verify: `cargo test --all-targets` in `crates/cupel-testing/` shows 15 tests passed (14 new + 1 smoke); `cargo clippy --all-targets -- -D warnings` clean
  - Done when: All 14 T01 tests pass, clippy clean, `dead_code` warning gone from `chain.rs`

- [x] **T02: Implement patterns 8–13 (aggregate/budget/coverage/ordering) with tests** `est:45m`
  - Why: Patterns 8–13 are the more complex patterns: aggregate count, sorting conformance, budget utilization, kind diversity, edge placement, and the index-based top-N-at-edges (Pattern 13 with the `Vec<usize>` approach instead of `HashSet<&IncludedItem>`).
  - Files: `crates/cupel-testing/src/chain.rs`, `crates/cupel-testing/tests/assertions.rs`
  - Do: (1) Add `have_at_least_n_exclusions`, `excluded_items_are_sorted_by_score_descending`, `have_budget_utilization_above`, `have_kind_coverage_count`, `place_item_at_edge`, `place_top_n_scored_at_edges` to `chain.rs`; (2) Pattern 10 (`have_budget_utilization_above`) uses `cupel::analytics::budget_utilization`; Pattern 11 uses `cupel::analytics::kind_diversity`; Pattern 13 uses index-based top-N via `enumerate().max_by_score` — collect top-N indices, enumerate edge positions, verify overlap; (3) Add 12 tests to `tests/assertions.rs` (2 per pattern); Pattern 13 needs ≥3 edge-case tests spread across positive/negative covering n=0, n>count, and tie-score tie handling; (4) Run full verification
  - Verify: `cargo test --all-targets` in `crates/cupel-testing/` shows 27 tests passed (26 assertion tests + 1 smoke); `cargo clippy --all-targets -- -D warnings` clean in both crates
  - Done when: All 26 assertion tests pass, Pattern 13 covers ≥3 edge cases within its 2 positive/negative tests, clippy clean in both crates, `cargo test --all-targets` in `cupel` shows no regressions

## Files Likely Touched

- `crates/cupel-testing/src/chain.rs` — 13 assertion methods, `#[allow(dead_code)]` removal
- `crates/cupel-testing/tests/assertions.rs` — 26 integration tests (new file)
- `crates/cupel-testing/src/lib.rs` — possibly add re-exports if needed for test imports
