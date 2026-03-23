---
estimated_steps: 6
estimated_files: 14
---

# T02: Author 5 conformance vectors and extend Rust harness

**Slice:** S03 — CountQuotaSlice — Rust + .NET Implementation
**Milestone:** M003

## Description

Authors 5 TOML conformance vectors covering all design-doc scenario sketches for `CountQuotaSlice`, copies them to all three required locations (D082), and extends the Rust conformance harness to verify not only `selected_contents` but also exclusion reasons and shortfalls for count_quota tests.

The 5 scenarios to cover:
1. **Baseline satisfaction** — `require_count=2`, 3 candidates; top-2 selected, 3rd passed to inner; no shortfalls, no cap exclusions.
2. **Cap exclusion** — `cap_count=1`, 3 candidates; 1 selected, 2 excluded; verify excluded count and that cap applies.
3. **Scarcity degrade** — `require_count=3`, only 1 candidate; 1 selected; shortfall recorded with satisfied_count=1.
4. **Tag non-exclusivity** — 1 multi-tag item `["critical","urgent"]`; `RequireCount("critical",1)` + `RequireCount("urgent",1)`; both requirements satisfied by 1 item.
5. **Combined require+cap** — `require_count=2`, `cap_count=2`, 4 candidates; 2 selected, 2 excluded by cap.

Because the `Slicer::slice` interface returns only `Vec<ContextItem>` (no trace collector), the exclusion reason and shortfall assertions cannot go through `run_slicing_test` which calls `slicer.slice()` directly. Instead, write a separate `run_count_quota_test` function that:
- Constructs a `Pipeline` with the count_quota slicer and calls `dry_run` to get a `SelectionReport`
- Checks `selected_contents` as usual
- Checks `excluded.len()` and optionally the `count_requirement_shortfalls` length

Alternatively (simpler): write the count_quota conformance tests as **direct unit tests** in `conformance/slicing.rs` that construct `CountQuotaSlice` directly and verify the returned `Vec<ContextItem>` length + an inline assertion on `count_requirement_shortfalls` by using a custom `MockTraceCollector` or by checking that the slicer's reported shortfalls are captured separately. Since shortfalls are on `SelectionReport` (pipeline-level output from `DiagnosticTraceCollector`), the unit tests should use `Pipeline::dry_run` for scenarios that need shortfall/exclusion-reason verification.

Use TOML vectors for the "selected_contents" contract. Add a `[expected]` section with `shortfall_count` and `cap_excluded_count` integer fields that the extended harness checks. For scenarios that only need selected_contents, reuse `run_slicing_test`.

## Steps

1. **Write 5 TOML conformance vectors** in `spec/conformance/required/slicing/`. Each file: `count-quota-baseline.toml`, `count-quota-cap-exclusion.toml`, `count-quota-scarcity-degrade.toml`, `count-quota-tag-nonexclusive.toml`, `count-quota-require-and-cap.toml`. Use the standard slicing TOML format (`[test]`, `[config]`, `[[items]]`, `[budget]`, `[expected]`). Add `[config.entries]` as an array of tables with `kind`, `require_count`, `cap_count`. Add `scarcity_behavior = "degrade"` (or `"throw"`) to `[config]`. Add `[expected] selected_contents = [...]` for all vectors. For vectors 2, 3, 5 also add `shortfall_count` and `cap_excluded_count` fields to `[expected]` so the extended harness can verify them.

2. **Copy vectors to `conformance/required/slicing/`** (repo root) and **`crates/cupel/conformance/required/slicing/`** — verbatim copies of all 5 files. Run `diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing` and confirm 0 differences.

3. **Extend `build_slicer_by_type` in `crates/cupel/tests/conformance.rs`** with a `"count_quota"` arm:
   - Parse `config.entries` as array of tables (each with `kind: str`, `require_count: int`, `cap_count: int`)
   - Parse `config.inner_slicer` (default `"greedy"`) for the inner slicer type
   - Parse `config.scarcity_behavior` (default `"degrade"`) — map to `ScarcityBehavior::Degrade` or `ScarcityBehavior::Throw`
   - Construct `CountQuotaSlice::new(entries, inner, scarcity).unwrap()`
   - Box and return

4. **Add `run_count_quota_slicing_test` function** in `crates/cupel/tests/conformance/slicing.rs`:
   - Takes a `vector_path: &str`
   - Loads vector; builds slicer via `build_slicer`
   - For selected_contents: call `slicer.slice(&scored_items, &budget)` and use `assert_set_eq` as before
   - For shortfall_count: if `expected.shortfall_count` key exists, check `slicer` directly — BUT since `Slicer::slice` doesn't return shortfalls, use a lightweight approach: construct a `Pipeline` with `DiagnosticTraceCollector`, call `dry_run`, check `report.count_requirement_shortfalls.len()` and `report.excluded.len()` for cap-excluded counts (filtered by reason variant if needed)
   - For simplicity: add a `run_count_quota_full_test` variant that goes via `Pipeline::dry_run`; use it for the 3 vectors that need shortfall/cap verification; use `run_slicing_test` for the 2 that only need selected_contents

5. **Write 5 `#[test]` functions** in `conformance/slicing.rs`:
   ```rust
   #[test] fn count_quota_baseline() { run_slicing_test("slicing/count-quota-baseline.toml"); }
   #[test] fn count_quota_cap_exclusion() { run_count_quota_full_test("slicing/count-quota-cap-exclusion.toml"); }
   #[test] fn count_quota_scarcity_degrade() { run_count_quota_full_test("slicing/count-quota-scarcity-degrade.toml"); }
   #[test] fn count_quota_tag_nonexclusive() { run_slicing_test("slicing/count-quota-tag-nonexclusive.toml"); }
   #[test] fn count_quota_require_and_cap() { run_count_quota_full_test("slicing/count-quota-require-and-cap.toml"); }
   ```

6. **Run `cargo test --all-targets`** with `-- count_quota --nocapture` to verify all 5 tests pass. Run `diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing` to confirm drift guard clean.

## Must-Haves

- [ ] 5 TOML vectors exist in `spec/conformance/required/slicing/` (names: `count-quota-baseline.toml`, `count-quota-cap-exclusion.toml`, `count-quota-scarcity-degrade.toml`, `count-quota-tag-nonexclusive.toml`, `count-quota-require-and-cap.toml`)
- [ ] All 5 vectors copied verbatim to `conformance/required/slicing/` and `crates/cupel/conformance/required/slicing/`
- [ ] `diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing` exits 0
- [ ] `build_slicer_by_type("count_quota", ...)` arm implemented in `conformance.rs`
- [ ] 5 `#[test]` functions pass under `cargo test -- count_quota --nocapture`
- [ ] Vectors for cap-exclusion, scarcity-degrade, and require-and-cap scenarios include `shortfall_count` or `cap_excluded_count` in `[expected]` and the harness checks them
- [ ] `cargo test --all-targets` exits 0

## Verification

- `cargo test -- count_quota --nocapture` — 5 tests pass, output shows each test name
- `diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing` — no output (zero drift)
- `ls spec/conformance/required/slicing/count-quota-*.toml | wc -l` — outputs 5
- `ls conformance/required/slicing/count-quota-*.toml | wc -l` — outputs 5
- `ls crates/cupel/conformance/required/slicing/count-quota-*.toml | wc -l` — outputs 5
- `cargo test --all-targets 2>&1 | tail -3` — all tests pass

## Observability Impact

- Signals added/changed: none new at runtime; conformance vectors are test artifacts
- How a future agent inspects this: `cargo test -- count_quota --nocapture` lists pass/fail per scenario; the TOML vectors serve as the human-readable specification of expected behavior
- Failure state exposed: failing `count_quota_*` test names identify which scenario is broken; vector content names the expected selected_contents, shortfall count, and cap excluded count

## Inputs

- `crates/cupel/src/slicer/count_quota.rs` — from T01; `CountQuotaSlice`, `CountQuotaEntry`, `ScarcityBehavior`
- `crates/cupel/src/diagnostics/mod.rs` — from T01; `count_requirement_shortfalls` on `SelectionReport`
- `.planning/design/count-quota-design.md` — 5 scenario outlines in "Conformance Vector Outlines" section
- `crates/cupel/tests/conformance.rs` — existing `build_slicer_by_type` to extend
- `crates/cupel/tests/conformance/slicing.rs` — existing `run_slicing_test` pattern to follow/extend
- `spec/conformance/required/slicing/quota-basic.toml` — reference TOML format for slicing vectors

## Expected Output

- `spec/conformance/required/slicing/count-quota-*.toml` — 5 new TOML files
- `conformance/required/slicing/count-quota-*.toml` — 5 verbatim copies
- `crates/cupel/conformance/required/slicing/count-quota-*.toml` — 5 verbatim copies
- `crates/cupel/tests/conformance.rs` — `"count_quota"` arm added to `build_slicer_by_type`
- `crates/cupel/tests/conformance/slicing.rs` — `run_count_quota_full_test` function + 5 `#[test]` functions
- `cargo test --all-targets` exits 0 with all 5 count_quota tests passing
