# S01 Post-Slice Roadmap Assessment

**Assessed:** 2026-03-24
**Verdict:** Roadmap unchanged — remaining slices are correct as written.

## Success Criterion Coverage

- `CountConstrainedKnapsackSlice constructable in both languages, 5 conformance tests, public API exports` → S02 (.NET half)
- `MetadataKeyScorer constructable in both languages, 5 conformance tests, public API exports` → S04
- `spec/src/slicers/count-constrained-knapsack.md and spec/src/scorers/metadata-key.md, zero TBD fields` → S03
- `cargo test --all-targets green; dotnet test green; clippy clean` → S02, S03, S04 (each slice maintains green)
- `CHANGELOG.md unreleased section reflects both new types` → S02 (CountConstrainedKnapsackSlice .NET entry), S04 (MetadataKeyScorer)

All success criteria have at least one remaining owning slice. Coverage check passes.

## Risk Retirement

All three proof obligations from the roadmap's Proof Strategy were retired in S01:

- **Pre-processing sub-optimality** — retired: test `require_and_cap` proves Phase 1 commits required items before knapsack sees residual. Trade-off documented (D174). Spec note required in S03 — still owned by S03.
- **Cap enforcement after knapsack** — retired: `cap_exclusion` conformance vector passes, Phase 3 drops over-cap items correctly. S03 still owns the spec documentation of this behavior.
- **`is_count_quota()` / `find_min_budget_for` interaction** — retired: `is_count_quota()=true` confirmed and implemented (D176). No change needed in remaining slices.

## New Discoveries and Their Impact

**D180** (Phase 2 output must be re-sorted before Phase 3): Critical for S02. Already documented in S01 Forward Intelligence and the decisions register. S02's boundary map entry already states "Phase 1 / Phase 2 / Phase 3 matching Rust semantics exactly" — this encompasses the re-sort. No roadmap text change needed; the finding is in the right artifact (S01-SUMMARY.md forward intelligence).

**D181** (Phase 3 `selected_count` seeds from Phase 1): Same situation — S02 inherits this via "matching Rust semantics exactly." Documented in decisions register.

**D182** (self-contained integration test pattern): Rust-specific limitation. .NET test projects share helpers normally — no impact on S02 or any remaining slice.

## Slice Order and Dependencies

S02 → S03 → S04 ordering is still correct:
- S02 provides the .NET implementation that S03 needs to validate spec accuracy against both languages.
- S03 spec chapter is the stated prerequisite for S04 (MetadataKeyScorer implementation contract).
- No new risks emerged that would warrant reordering.

## Requirement Coverage

- **R062**: Active, partial. Rust half satisfied by S01. S02 owns the .NET half and the validation trigger.
- **R063**: Active, unmapped. S04 still owns it. No changes to status warranted.
- No requirements were invalidated, re-scoped, or newly surfaced by S01.

## Conclusion

The roadmap is sound. S01 delivered exactly what it promised, retired all assigned risks, and produced the implementation spec-by-example that S02 needs. The boundary map entries for S01 → S02 and S01 → S03 accurately describe what was built. No slice edits required.
