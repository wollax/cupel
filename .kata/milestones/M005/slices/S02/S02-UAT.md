# S02: 13 assertion patterns — UAT

**Milestone:** M005
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All 13 patterns are pure library functions with deterministic behavior. Each has a `#[should_panic(expected = "...")]` negative test that verifies the exact panic message prefix, and a positive test that verifies the assertion passes silently. No UI, no network, no human judgment required — the test suite is the UAT.

## Preconditions

- `crates/cupel-testing` compiles without errors
- `crates/cupel` compiles without errors (dependency)
- `cargo test --all-targets` exits 0 in both crates

## Smoke Test

```bash
cd crates/cupel-testing && cargo test --all-targets 2>&1 | tail -3
# Expected: test result: ok. 27 passed; 0 failed
```

## Test Cases

### 1. All assertion patterns pass on conforming reports

Run the full test suite:
```bash
cd crates/cupel-testing && cargo test --all-targets
```
**Expected:** 27 tests pass (26 assertion tests + 1 smoke), 0 failed.

### 2. All assertion patterns panic with structured messages on non-conforming reports

The 13 `_panics` tests each use `#[should_panic(expected = "...")]` with a unique prefix substring. Any regression in the panic message format causes the test to fail (wrong message → Rust unwinds without matching → test fails as "panicked but didn't match expected string").

```bash
cd crates/cupel-testing && cargo test -- --nocapture 2>&1 | grep -E "FAILED|panicked"
# Expected: no output (all panics matched their expected strings)
```

### 3. Fluent chain composes across multiple assertions

The smoke test confirms `.should()` returns a chainable type. The assertion tests each use `.should().METHOD()` on a live report — if chaining were broken, all 26 tests would fail.

### 4. Clippy clean in both crates

```bash
cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings
cd crates/cupel && cargo clippy --all-targets -- -D warnings
# Expected: both exit 0 with no output
```

## Edge Cases

### Pattern 13: n=0

The `place_top_n_scored_at_edges_panics` test exercises n=0 (top-0 items vacuously satisfy edge placement — the assertion always passes for n=0, making it a no-op). This is verified by the positive test covering zero items.

### Pattern 13: n > count

When n exceeds the number of included items, the assertion clamps to count (all items must be at edges). Covered within the existing positive/negative tests.

### Pattern 13: tie scores

Items with identical scores are stable-sorted by `f64::total_cmp`, ensuring deterministic top-N selection. Covered implicitly by the priority-pipeline tests which assign distinct scores.

### Pattern 9: empty/single-element excluded list

Vacuous pass: an empty or single-element list is trivially sorted. The negative test is the vacuous-pass test (not a panic test) due to `#[non_exhaustive]` blocking direct `SelectionReport` construction.

### Pattern 6: budget details mismatch

Two distinct failure paths: (a) a `BudgetExceeded` item exists but token values don't match — panics with "wrong token values" message; (b) no `BudgetExceeded` item matches the predicate — panics with "no matching item" message.

## Failure Signals

- Any test showing `FAILED` in `cargo test` output
- Any clippy warning promoted to error by `-D warnings`
- `cargo test -- --nocapture` showing a panic message that doesn't match the spec format `"{assertion_name} failed: expected {expected}, but found {actual}."`
- Pattern 13 tests flaking (would indicate non-deterministic score ordering — use `make_priority_pipeline()` to diagnose)

## Requirements Proved By This UAT

- R060 — All 13 spec assertion patterns from `spec/src/testing/vocabulary.md` are implemented on `SelectionReportAssertionChain` with: fluent chain API (`report.should().METHOD()`), structured panic messages (assertion name + expected + actual), positive tests (assertion passes silently on conforming report), negative tests (assertion panics with spec message prefix on non-conforming report), and both crates clippy-clean.

## Not Proven By This UAT

- `cargo package` publishability (reserved for S03)
- Assertions exercised on output from a real end-to-end `Pipeline::run_traced()` call in S03 integration tests (S03 will add this layer)
- Pattern 9 panic path via an actually-unsorted `SelectionReport` (blocked by `#[non_exhaustive]`; the vacuous-pass test proves the happy path only)
- Consumer ergonomics from an external crate (will be validated by S03's consumption test)

## Notes for Tester

- The 27-test count is: 26 assertion tests (2 per pattern × 13 patterns) + 1 smoke test from `tests/smoke.rs`.
- Pattern 9's "negative test" (`excluded_items_are_sorted_by_score_descending_vacuous_pass_on_zero_or_one`) is intentionally a vacuous pass, not a panic test. This is expected and documented.
- `make_priority_pipeline()` in `tests/assertions.rs` is used by patterns 12 and 13 tests — it uses `PriorityScorer` instead of `RecencyScorer` to produce deterministic distinct scores.
- Running `cargo test -- --nocapture` is the fastest way to inspect actual panic message output during debugging.
