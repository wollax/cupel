# Plan 25-01 Summary: Fix misleading comments in conformance vector TOML files

## Outcome

Complete. All must-haves satisfied. Task 1 commit: `8b59c41`, Task 2 commit: `07767d4`.

## Tasks Completed

| Task | Description | Status |
|------|-------------|--------|
| Task 1 | Fix knapsack-basic.toml and composite-weighted.toml comments | Done |
| Task 2 | Fix pinned-items.toml and u-shaped vector comments | Done |

## Changes Made

**`conformance/required/slicing/knapsack-basic.toml`** (+ spec/ and crates/ copies)

- Deleted abandoned first scenario (lines 6–28) that concluded "same as greedy in this case"
- Removed the "Let me redesign" transition comment
- File now opens directly with the redesigned scenario that demonstrates a genuine knapsack-vs-greedy difference

**`conformance/required/scoring/composite-weighted.toml`** (+ spec/ and crates/ copies)

- Removed 5-line "Wait — let me recompute priority" scratchpad paragraph (former lines 24–28)
- The correct priority values (lines 19–23) were already present and remain unchanged

**`conformance/required/pipeline/pinned-items.toml`** (+ spec/ and crates/ copies)

- Inserted "Density sort" subsection into the Greedy slice comment, showing the density values and sort order before the fill trace
- Added note that density values use scored values from the Score step above

**`conformance/required/placing/u-shaped-basic.toml`** (+ spec/ and crates/ copies)

- Replaced `left[N]`/`right[N]` notation with `result[N]` notation throughout the comment block

**`conformance/required/placing/u-shaped-equal-scores.toml`** (+ spec/ and crates/ copies)

- Replaced `left[N]`/`right[N]` notation with `result[N]` notation throughout the comment block

## Must-Haves Verification

- [x] knapsack-basic.toml: abandoned first scenario deleted (no "Let me redesign" or "same as greedy in this case")
- [x] composite-weighted.toml: scratchpad "Wait —" paragraph removed
- [x] pinned-items.toml: density-sort step present in greedy section
- [x] u-shaped-basic.toml: uses result[N] notation (no left[N]/right[N])
- [x] u-shaped-equal-scores.toml: uses result[N] notation (no left[N]/right[N])
- [x] All five files: conformance/, spec/conformance/, and crates/ copies are byte-identical
- [x] `cargo test --manifest-path crates/cupel/Cargo.toml` passes — 78 tests, 0 failures (comments only, no data change)

## Deviations

**Three-copy architecture discovered during execution.** The plan described two copies (spec/ and crates/), but the repository has three: the canonical `conformance/required/` at the repo root, `spec/conformance/required/`, and `crates/cupel/conformance/required/`. The pre-commit hook enforces byte-identity between the root canonical and crates/. All edits were applied to all three copies to satisfy both the hook and the plan's byte-identity requirement.

## Phase Contribution

Satisfies CONF-01: all five misleading/incorrect comment blocks are corrected in the canonical and all vendored copies.
