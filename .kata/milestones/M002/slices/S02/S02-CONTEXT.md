---
id: S02
milestone: M002
status: ready
---

# S02: Spec Editorial Debt — Context

## Goal

Close the ~8 explicitly named spec/phase24 issues by editing the relevant `spec/src/` files, removing the closed issue files, and updating conformance vectors atomically when a fix requires it.

## Why this Slice

The Cupel spec is a publicly served mdBook. Ambiguous event ordering and misleading algorithm descriptions block new language binding implementors and make conformance test authoring unreliable. S02 has no dependencies and can run in parallel with or before any other M002 slice — closing it early removes the risk of spec debt compounding.

## Scope

### In Scope

The following issues (explicitly named in the roadmap boundary map) are in scope:

- `2026-03-15-phase24-event-ordering-within-stage-unspecified.md` → `spec/src/diagnostics/events.md`: add item-level before/after stage-level event ordering rule
- `2026-03-15-phase24-item-count-sentinel-ambiguity.md` → `spec/src/diagnostics/events.md`: add item_count sentinel disambiguation note
- `2026-03-15-phase24-observer-callback-normative-status.md` → `spec/src/diagnostics/trace-collector.md`: label observer callback section with MAY (non-normative)
- `2026-03-14-spec-greedy-zero-token-ordering.md` → `spec/src/slicers/greedy.md`: add zero-token item ordering note
- `2026-03-14-spec-knapsack-floor-vs-truncation.md` → `spec/src/slicers/knapsack.md`: add floor vs truncation-toward-zero equivalence note
- `2026-03-14-spec-ushaped-pinned-edge-case.md` → `spec/src/placers/u-shaped.md`: correct the pinned edge case table row
- `2026-03-14-spec-composite-pseudocode-storage.md` → `spec/src/scorers/composite.md`: add storage assignment to pseudocode
- `2026-03-14-unbounded-scaled-nesting-depth.md` → `spec/src/scorers/scaled.md`: add nesting depth warning

For each closed issue: remove the issue file from `.planning/issues/open/`.

If fixing the UShapedPlacer edge case (or any other fix) requires updating a conformance vector, update both `spec/conformance/` and `crates/cupel/conformance/` atomically in the same commit per D007.

### Out of Scope

- All other `spec-*` and `phase24-*` issues not in the list above — they stay open for a future quality/editorial slice
- `2026-03-14-spec-workflow-checksum-verification.md` — this is a CI security issue, not a spec content fix; out of scope for S02
- Any implementation code changes (D039 is locked — M002 is design/docs only)
- New spec chapters (those belong to S04, S05, S06)
- Conformance test harness changes beyond syncing the vector files themselves
- Updating `spec/src/SUMMARY.md` (no new chapters are added in S02; existing chapters are amended in-place)

## Constraints

- No .NET or Rust code changes — spec text edits and conformance vector file updates only
- Conformance vector updates (if any) must be done atomically: edit both `spec/conformance/<file>` and `crates/cupel/conformance/<file>` in the same commit
- Issue files must be deleted from `.planning/issues/open/` for each resolved issue — not annotated, deleted
- `cargo test --manifest-path crates/cupel/Cargo.toml` and `dotnet test` must still pass after all changes
- All spec edits must follow the established style: pseudocode in labeled `text` fenced blocks, normative keywords (MUST/SHOULD/MAY) applied consistently

## Integration Points

### Consumes

- `.planning/issues/open/` — the 8 named issue files; read each for the exact problem description and proposed fix before editing the spec
- `spec/src/diagnostics/events.md` — primary target for event ordering and sentinel fixes
- `spec/src/diagnostics/trace-collector.md` — observer callback normative status fix
- `spec/src/slicers/greedy.md` — zero-token ordering note
- `spec/src/slicers/knapsack.md` — floor/truncation note
- `spec/src/placers/u-shaped.md` — pinned edge case table row correction
- `spec/src/scorers/composite.md` — pseudocode storage assignment
- `spec/src/scorers/scaled.md` — nesting depth warning
- `spec/conformance/` + `crates/cupel/conformance/` — only if a spec fix requires a vector update

### Produces

- 8 amended spec files in `spec/src/` with the named issues resolved
- 8 deleted issue files from `.planning/issues/open/`
- Optionally: updated conformance vector files if required by the UShapedPlacer fix or any other fix

## Open Questions

- **UShapedPlacer pinned edge case — conformance vector required?** The issue says the table row is "misleading." If the fix only corrects prose/table text without changing the algorithm, no vector update is needed. If the correction reveals a previously untested scenario, a new vector may be needed. To be determined during execution by reading the existing conformance vectors and the issue detail. Working assumption: spec text correction only; escalate if a vector gap is found.
- **ScaledScorer nesting depth warning — normative or informative?** The `2026-03-14-unbounded-scaled-nesting-depth.md` issue is confirmed in scope (it's the "ScaledScorer nesting warning" from the roadmap boundary map), but whether the warning should use SHOULD NOT (normative) or a non-normative informative note depends on reading the existing spec style in `scorers/scaled.md`. Working assumption: informative note matching the existing chapter style unless the issue explicitly calls for normative language.
