---
estimated_steps: 5
estimated_files: 8
---

# T02: Author 4 conformance vectors and vendor to crates/

**Slice:** S01 — Diagnostics Data Types
**Milestone:** M001

## Description

Author 4 new diagnostics conformance vectors covering `NegativeTokens`, `Deduplicated`, `PinnedOverride`, and `Scored` (inclusion). Copy each to `crates/cupel/conformance/required/pipeline/` to satisfy the drift guard. The vectors must follow the exact TOML schema established by `diagnostics-budget-exceeded.toml` so the harness can parse them in S03 without changes.

## Steps

1. Author `spec/conformance/required/pipeline/diag-negative-tokens.toml`:
   - Budget: target=500, max=1000, reserve=0; slicer=greedy, placer=chronological, no scorers, deduplication=false
   - Items: one item with `tokens = -5` (triggers NegativeTokens at Classify stage), one normal item with `tokens = 100`
   - Expected: normal item in `expected_output`; in `[expected.diagnostics.summary]`: `total_candidates=2`, `total_tokens_considered=95` (100 + (-5)); `[[expected.diagnostics.excluded]]` with `content = "<negative-item>"`, `score_approx = 0.0`, `exclusion_reason = "NegativeTokens"`, `tokens = -5`; `[[expected.diagnostics.included]]` for the normal item with `inclusion_reason = "Scored"`

2. Author `spec/conformance/required/pipeline/diag-deduplicated.toml`:
   - Budget: target=500, max=1000, reserve=0; slicer=greedy, placer=chronological, deduplication=true, RecencyScorer
   - Items: two items with identical content (same `content` value), different timestamps (Jan and Jun)
   - Expected: the newer (Jun) item appears in `expected_output`; older (Jan) item excluded with `exclusion_reason = "Deduplicated"`, `deduplicated_against = "<content>"` matching the surviving item; summary has `total_candidates=2`

3. Author `spec/conformance/required/pipeline/diag-pinned-override.toml`:
   - Budget: target=150, max=1000, reserve=0; slicer=greedy, placer=chronological, `overflow_strategy = "truncate"`, no scorers, deduplication=false
   - Items: one pinned item with `tokens = 120` (fits in 150), one non-pinned item with `tokens = 80` (120 + 80 = 200 > 150, so truncation displaces the non-pinned item)
   - After Classify: pinned=["pinned-item"(120)], scoreable=["regular-item"(80)]; after Slice(greedy, effective target=150-120=30): regular-item(80) > 30 → excluded as BudgetExceeded during slice, but pinned consumes space; after Place with Truncate: pinned item fits, regular item was already excluded
   - Note: `PinnedOverride` is emitted by the placer when a pinned item displaces a sliced item during truncation. Verify actual trigger condition against the .NET reference before finalising. If the scenario can't cleanly trigger PinnedOverride with these sizes, document the actual observed behavior in the vector comment and adjust to produce a valid, consistent scenario.
   - Expected: only the pinned item in `expected_output`; regular item excluded with `exclusion_reason = "PinnedOverride"`, `displaced_by = "<pinned-item-content>"`

4. Author `spec/conformance/required/pipeline/diag-scored-inclusion.toml`:
   - Budget: target=500, max=1000, reserve=0; slicer=greedy, placer=chronological, RecencyScorer, deduplication=false
   - Items: two scoreable items (non-pinned), both fit in budget, different timestamps for scoring
   - Expected: both items in `expected_output`; both in `[[expected.diagnostics.included]]` with `inclusion_reason = "Scored"` and correct `score_approx` values (1.0 for newest, 0.0 for oldest with 2 items and RecencyScorer); summary has `total_candidates=2`, no excluded items

5. Copy all 4 new vectors verbatim to `crates/cupel/conformance/required/pipeline/`:
   ```bash
   cp spec/conformance/required/pipeline/diag-*.toml crates/cupel/conformance/required/pipeline/
   ```
   Then verify drift guard: `diff -rq spec/conformance/required/pipeline/ crates/cupel/conformance/required/pipeline/`

## Must-Haves

- [ ] 4 new TOML files authored in `spec/conformance/required/pipeline/`
- [ ] Each vector has `[expected.diagnostics.summary]`, at least one `[[expected.diagnostics.included]]`, and (where applicable) `[[expected.diagnostics.excluded]]` sections
- [ ] All `exclusion_reason` and `inclusion_reason` values are exact string matches to the spec variant names
- [ ] `diag-negative-tokens.toml` triggers `NegativeTokens` exclusion
- [ ] `diag-deduplicated.toml` triggers `Deduplicated` exclusion
- [ ] `diag-pinned-override.toml` triggers `PinnedOverride` exclusion (verify scenario is self-consistent)
- [ ] `diag-scored-inclusion.toml` triggers `Scored` inclusion for both items
- [ ] All 4 vectors copied identically to `crates/cupel/conformance/required/pipeline/`
- [ ] `diff -rq spec/conformance/required/pipeline/ crates/cupel/conformance/required/pipeline/` exits 0
- [ ] `cargo test` still passes (existing harness; vectors won't be exercised until S03)

## Verification

```bash
# Drift guard: no differences
diff -rq spec/conformance/required/pipeline/ crates/cupel/conformance/required/pipeline/

# 4 new diag vectors present in spec
ls spec/conformance/required/pipeline/diag-*.toml | wc -l
# Expected: 4

# Existing tests unaffected
cargo test
```

## Observability Impact

- Signals added/changed: None at runtime — TOML files only; harness coverage of `[expected.diagnostics.*]` sections activates in S03
- How a future agent inspects this: `cat spec/conformance/required/pipeline/diag-*.toml` reveals vector scenarios; `diff -r spec/conformance/ crates/cupel/conformance/` surfaces drift immediately
- Failure state exposed: Drift guard CI step fails with a clear diff if spec and crates copies diverge; malformed TOML caught when `cargo test` loads vectors (parse error in harness)

## Inputs

- `spec/conformance/required/pipeline/diagnostics-budget-exceeded.toml` — canonical vector schema to match exactly
- `spec/conformance/required/pipeline/pinned-items.toml` — reference for pinned item scenario setup
- `crates/cupel/tests/conformance/pipeline.rs` — harness that loads vectors (read to confirm TOML section names match what the parser expects)
- T01 output — the new types are now defined, so variant names are confirmed

## Expected Output

- `spec/conformance/required/pipeline/diag-negative-tokens.toml` (new)
- `spec/conformance/required/pipeline/diag-deduplicated.toml` (new)
- `spec/conformance/required/pipeline/diag-pinned-override.toml` (new)
- `spec/conformance/required/pipeline/diag-scored-inclusion.toml` (new)
- `crates/cupel/conformance/required/pipeline/diag-negative-tokens.toml` (new, identical copy)
- `crates/cupel/conformance/required/pipeline/diag-deduplicated.toml` (new, identical copy)
- `crates/cupel/conformance/required/pipeline/diag-pinned-override.toml` (new, identical copy)
- `crates/cupel/conformance/required/pipeline/diag-scored-inclusion.toml` (new, identical copy)
