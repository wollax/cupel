---
id: T03
parent: S02
milestone: M002
provides:
  - context-item.md content field demoted to plain description (informal MUST removed, Constraint 1 unchanged)
  - format.md Set Comparison QuotaSlice clarifying sentence added
  - Both greedy-chronological.toml copies updated with jan density comment (drift guard satisfied)
  - All 20 resolved spec/phase24 issue files deleted from .planning/issues/open/
key_files:
  - spec/src/data-model/context-item.md
  - spec/src/conformance/format.md
  - spec/conformance/required/pipeline/greedy-chronological.toml
  - crates/cupel/conformance/required/pipeline/greedy-chronological.toml
key_decisions:
  - none
patterns_established:
  - none
observability_surfaces:
  - none
duration: short
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T03: Fix data-model, conformance format, TOML; close issues; verify

**Applied three remaining editorial fixes, deleted all 20 resolved issue files, and confirmed both test suites pass (113 Rust, 583 .NET).**

## What Happened

Three spec/doc-only edits applied:

1. **`spec/src/data-model/context-item.md`** — `content` field table cell changed from "The textual content of this context item. Must be non-null and non-empty." to "The textual content of the item. Non-null and non-empty." Constraint 1 (`MUST be a non-null, non-empty string`) was verified unchanged.

2. **`spec/src/conformance/format.md`** — Added clarifying sentence to Set Comparison subsection: "This applies to all slicers including QuotaSlice — ordering is always the placer's responsibility, not the slicer's."

3. **Both TOML copies** — Updated jan density comment to append `(non-zero token, normal score path)`. `diff` confirmed both files are identical (D007 drift guard satisfied).

Then deleted all 20 resolved issue files from `.planning/issues/open/`, keeping `2026-03-14-spec-workflow-checksum-verification.md` (explicitly deferred per research constraints — CI security concern, out of S02 scope).

## Verification

- `grep -n "Must be non-null and non-empty" spec/src/data-model/context-item.md` → 0 (old informal MUST removed) ✓
- `grep -n "MUST be a non-null" spec/src/data-model/context-item.md` → line 23 (Constraint 1 unchanged) ✓
- `grep -n "QuotaSlice" spec/src/conformance/format.md` → line 94 contains clarifying sentence ✓
- `diff spec/conformance/required/pipeline/greedy-chronological.toml crates/cupel/conformance/required/pipeline/greedy-chronological.toml` → no output ✓
- `ls .planning/issues/open/ | grep "spec-workflow-checksum-verification"` → 1 result (deferred issue present) ✓
- All 20 listed issue files confirmed deleted ✓
- `cargo test --manifest-path crates/cupel/Cargo.toml` → 113 passed, 1 ignored, exit 0 ✓
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → 583 passed, 0 failed, exit 0 ✓

Note: `rtk dotnet test` incorrectly reported exit 1 due to TUnit secondary runner output; raw `dotnet test` confirmed exit 0.

### Slice-level verification (all pass — this is the final task):
- `ls .planning/issues/open/ | grep -E '^2026-03-1[45]-(spec-|phase24-)' | wc -l` → 1 (the deferred `spec-workflow-checksum-verification.md` matches the pattern but is intentionally kept; the slice plan incorrectly described it as having a "different prefix pattern") ✓ (all 20 listed files deleted)
- `grep -n "Defined in" spec/src/diagnostics.md` → 0 ✓ (T01)
- `grep -n "// Store scorers and normalizedWeights" spec/src/scorers/composite.md` → 0 ✓ (T02)
- `grep -n "Placed at edges alongside other high-scored items" spec/src/placers/u-shaped.md` → 0 ✓ (T02)
- `cargo test` → exit 0 ✓
- `dotnet test` → exit 0 ✓
- TOML diff → no output ✓

## Diagnostics

None — spec/comment-only changes with no runtime impact.

## Deviations

The slice plan stated `2026-03-14-spec-workflow-checksum-verification.md` has "a different prefix pattern" and wouldn't match `^2026-03-1[45]-(spec-|phase24-)`. This is incorrect — it does match. However, the actual requirement (keep the deferred file, delete all 20 listed files) is fully satisfied. The wc count of 1 reflects this one retained file, which is correct behavior.

## Known Issues

None.

## Files Created/Modified

- `spec/src/data-model/context-item.md` — content field table cell demoted to plain description
- `spec/src/conformance/format.md` — QuotaSlice clarifying sentence added to Set Comparison
- `spec/conformance/required/pipeline/greedy-chronological.toml` — jan density comment updated
- `crates/cupel/conformance/required/pipeline/greedy-chronological.toml` — identical jan density comment update (drift guard)
- `.planning/issues/open/` — 20 resolved issue files deleted
