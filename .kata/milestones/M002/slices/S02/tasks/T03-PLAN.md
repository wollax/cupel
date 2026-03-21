---
estimated_steps: 5
estimated_files: 4
---

# T03: Fix data-model, conformance format, TOML; close issues; verify

**Slice:** S02 — Spec Editorial Debt
**Milestone:** M002

## Description

Apply the three remaining editorial changes (context-item normative alignment, conformance format set-comparison clarification, TOML density comment), delete all 20 resolved issue files from `.planning/issues/open/` (keeping the deferred workflow checksum issue), and run `cargo test` and `dotnet test` to confirm no regressions.

## Steps

1. **`spec/src/data-model/context-item.md`** — Find the `content` field row in the ContextItem fields table. The current cell reads: "The textual content of this context item. Must be non-null and non-empty." Change to: "The textual content of the item. Non-null and non-empty." (plain description, no informal MUST — the normative source remains Constraint 1). After editing, verify that Constraint 1 still reads "MUST be a non-null, non-empty string. Implementations SHOULD reject construction..." — do not touch the Constraints section.

2. **`spec/src/conformance/format.md`** — In the Slicing Vectors > Set Comparison subsection, add a clarifying sentence after the existing "This is because slicers select items but do not determine presentation order (that is the placer's responsibility)." sentence: "This applies to all slicers including QuotaSlice — ordering is always the placer's responsibility, not the slicer's."

3. **Both `greedy-chronological.toml` copies** — In `spec/conformance/required/pipeline/greedy-chronological.toml`, find the comment line for jan's density:
   ```
   #   Densities: dec=1.0/150≈0.00667, aug=0.667/150≈0.00444, mar=0.333/200≈0.00167, jan=0.0/200=0.0
   ```
   Update to:
   ```
   #   Densities: dec=1.0/150≈0.00667, aug=0.667/150≈0.00444, mar=0.333/200≈0.00167, jan=0.0/200=0.0 (non-zero token, normal score path)
   ```
   Apply the **identical** change to `crates/cupel/conformance/required/pipeline/greedy-chronological.toml`. Verify with `diff` that both files are identical after the edit (D007 drift guard).

4. **Delete 20 resolved issue files** from `.planning/issues/open/`:
   ```
   rm .planning/issues/open/2026-03-14-spec-composite-pseudocode-storage.md
   rm .planning/issues/open/2026-03-14-spec-context-item-normative-inconsistency.md
   rm .planning/issues/open/2026-03-14-spec-greedy-zero-token-ordering.md
   rm .planning/issues/open/2026-03-14-spec-kindscorer-case-insensitivity-clarification.md
   rm .planning/issues/open/2026-03-14-spec-knapsack-floor-vs-truncation.md
   rm .planning/issues/open/2026-03-14-spec-pipeline-density-comment-clarity.md
   rm .planning/issues/open/2026-03-14-spec-slicer-set-comparison-clarification.md
   rm .planning/issues/open/2026-03-14-spec-ushaped-pinned-edge-case.md
   rm .planning/issues/open/2026-03-15-phase24-conformance-notes-placement.md
   rm .planning/issues/open/2026-03-15-phase24-event-ordering-within-stage-unspecified.md
   rm .planning/issues/open/2026-03-15-phase24-excluded-item-rationale-repeats-exclusion-reasons.md
   rm .planning/issues/open/2026-03-15-phase24-how-to-obtain-placement.md
   rm .planning/issues/open/2026-03-15-phase24-item-count-sentinel-ambiguity.md
   rm .planning/issues/open/2026-03-15-phase24-null-path-prose-duplicated.md
   rm .planning/issues/open/2026-03-15-phase24-observer-callback-normative-status.md
   rm .planning/issues/open/2026-03-15-phase24-pipeline-stage-sort-omission-redundancy.md
   rm .planning/issues/open/2026-03-15-phase24-rejected-alternative-formatting-inconsistency.md
   rm .planning/issues/open/2026-03-15-phase24-reserved-variants-no-json-example.md
   rm .planning/issues/open/2026-03-15-phase24-summary-table-column-header.md
   rm .planning/issues/open/2026-03-14-unbounded-scaled-nesting-depth.md
   ```
   Keep `2026-03-14-spec-workflow-checksum-verification.md` open (deferred per research constraints).

5. **Run test suites** — `cargo test --manifest-path crates/cupel/Cargo.toml` and `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`. Both must exit 0. The TOML comment change is comment-only; no test logic or expected output changed.

## Must-Haves

- [ ] `context-item.md` `content` field table cell is plain description (no "Must be"); Constraint 1 unchanged and still normative
- [ ] `format.md` Set Comparison note includes QuotaSlice clarifying sentence
- [ ] Both TOML copies have updated `jan` density comment and are identical (`diff` produces no output)
- [ ] `2026-03-14-spec-workflow-checksum-verification.md` still exists in `.planning/issues/open/` (not deleted)
- [ ] All 20 resolved issue files deleted from `.planning/issues/open/`
- [ ] `cargo test --manifest-path crates/cupel/Cargo.toml` exits 0
- [ ] `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0

## Verification

- `grep -n "Must be non-null and non-empty" spec/src/data-model/context-item.md` → 0 (old informal MUST removed from field table)
- `grep -n "MUST be a non-null" spec/src/data-model/context-item.md` → at least 1 (Constraint 1 unchanged)
- `grep -n "QuotaSlice" spec/src/conformance/format.md` → at least 1 (clarifying sentence added)
- `diff spec/conformance/required/pipeline/greedy-chronological.toml crates/cupel/conformance/required/pipeline/greedy-chronological.toml` → no output
- `ls .planning/issues/open/ | grep -E '^2026-03-1[45]-(spec-|phase24-)' | wc -l` → 0
- `ls .planning/issues/open/ | grep "spec-workflow-checksum-verification"` → 1 result (deferred issue still present)
- `cargo test --manifest-path crates/cupel/Cargo.toml` → exits 0
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → exits 0

## Observability Impact

- Signals added/changed: None (spec/doc changes only)
- How a future agent inspects this: `ls .planning/issues/open/ | grep -E 'spec-|phase24-'` → empty means all resolved issues closed
- Failure state exposed: If either test suite fails, the exit code and test output identify the regression; since no code was changed, any failure would indicate a pre-existing issue or environment problem unrelated to S02

## Inputs

- `spec/src/data-model/context-item.md` — field table row for `content` is the target
- `spec/src/conformance/format.md` — Set Comparison subsection is the target
- `spec/conformance/required/pipeline/greedy-chronological.toml` — density comment line is the target
- `crates/cupel/conformance/required/pipeline/greedy-chronological.toml` — must receive identical change (D007)
- Research constraint: `spec-workflow-checksum-verification.md` is explicitly deferred (CI security concern, out of S02 scope) — do NOT delete it
- Research constraint: no code changes in M002 (D039); TOML changes are comment-only

## Expected Output

- `spec/src/data-model/context-item.md` — `content` field table cell demoted to plain description
- `spec/src/conformance/format.md` — QuotaSlice clarifying sentence in Set Comparison
- `spec/conformance/required/pipeline/greedy-chronological.toml` — updated jan density comment
- `crates/cupel/conformance/required/pipeline/greedy-chronological.toml` — identical update (drift guard satisfied)
- `.planning/issues/open/` — 20 issue files removed; workflow checksum issue remains
- Test suites green (no regressions from spec/comment-only changes)
