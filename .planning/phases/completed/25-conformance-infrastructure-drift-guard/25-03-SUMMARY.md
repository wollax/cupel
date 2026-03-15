# Plan 25-03 Summary: Diagnostics Conformance Vector Schema & Example

**Phase:** 25 — Conformance Infrastructure & Drift Guard
**Plan:** 03
**Completed:** 2026-03-15
**Wave:** 1

## Objective

Document the `[expected.diagnostics]` schema in the conformance format spec chapter and create an example diagnostics conformance vector file. Satisfies SPEC-02.

## Tasks Completed

### Task 1: Add diagnostics vector schema to format.md
**Commit:** `8b59c41` (committed as part of plan 25-01)

The Diagnostics Vectors section was already present in `spec/src/conformance/format.md` from plan 25-01. Verified the section includes:
- Overview paragraph (pipeline-level only, composes with `[[expected_output]]`)
- Complete schema table (11 fields across included, excluded, summary sub-tables)
- Ordering note (included in placed order, excluded sorted by score desc)
- Optionality note (all three sub-tables independently optional)
- Compatibility note (valid TOML 1.0, no conflict with `[[expected_output]]`)
- Example TBD placeholder vector

### Task 2: Create example diagnostics conformance vector file
**Commit:** `5636a5a`

Created `diagnostics-budget-exceeded.toml` in all three copies:
- `spec/conformance/required/pipeline/diagnostics-budget-exceeded.toml`
- `crates/cupel/conformance/required/pipeline/diagnostics-budget-exceeded.toml`
- `conformance/required/pipeline/diagnostics-budget-exceeded.toml` (canonical, required by pre-commit hook)

All copies are byte-identical. The vector demonstrates the BudgetExceeded exclusion reason pattern with `available_tokens = 50` (derived from `target_tokens(200) - tokens_used_by_fits(150) = 50`).

## Artifacts

| Path | Lines | Status |
|---|---|---|
| `spec/src/conformance/format.md` | 271 | Complete — Diagnostics Vectors section at lines 150-255 |
| `spec/conformance/required/pipeline/diagnostics-budget-exceeded.toml` | 70 | Created |
| `crates/cupel/conformance/required/pipeline/diagnostics-budget-exceeded.toml` | 70 | Created (byte-identical) |

## Verification

- [x] format.md contains Diagnostics Vectors section between Pipeline Vectors and Field Types
- [x] Schema table documents all fields: included (content, score_approx, inclusion_reason), excluded (content, score_approx, exclusion_reason, item_tokens, available_tokens, deduplicated_against), summary (total_candidates, total_tokens_considered)
- [x] Section states diagnostics are pipeline-level only
- [x] Section notes that included/excluded/summary are independently optional
- [x] Example vector in format.md matches the actual vector file
- [x] diagnostics-budget-exceeded.toml exists in both spec/ and crates/ pipeline directories
- [x] Both copies are byte-identical (verified with diff)
- [x] Vector file is valid TOML (no syntax errors)
- [x] Pre-commit hook passes (conformance vectors in sync across all three copies)

## Phase Contribution

Satisfies SPEC-02: diagnostics conformance vectors exist with `[expected.diagnostics]` schema for cross-language verification of exclusion reasons and counts. Together with plans 25-01 (comment fixes) and 25-02 (CI drift guard), Phase 25 is complete.
