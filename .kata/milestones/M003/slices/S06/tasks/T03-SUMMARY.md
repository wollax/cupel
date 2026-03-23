---
id: T03
parent: S06
milestone: M003
provides:
  - Explicit deterministic tie-break contract documented in spec/src/slicers/greedy.md
  - Budget-simulation determinism section aligned to the same original-index contract in spec/src/analytics/budget-simulation.md
  - Enhanced doc comments on .NET GreedySlice with tie-break contract
  - Enhanced doc comments on Rust GreedySlice with tie-break contract
  - Cross-reference from budget-simulation spec to GreedySlice tie-break contract
key_files:
  - spec/src/slicers/greedy.md
  - spec/src/analytics/budget-simulation.md
  - src/Wollax.Cupel/GreedySlice.cs
  - crates/cupel/src/slicer/greedy.rs
key_decisions:
  - "Tie-break contract: equal-density items preserve original input order (original-index ascending); no ContextItem.Id field needed"
  - "Zero-token items: score values do NOT affect relative order among zero-token items; tiebreak is original index only"
patterns_established:
  - "Deterministic tie-break contract language: 'original-index ascending' used consistently across .NET, Rust, and spec"
observability_surfaces:
  - Divergence between code and docs shows up as test failure or missing phrase in spec text via grep
duration: 10min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T03: Lock the GreedySlice Tie-Break Contract Across .NET, Rust, and Spec Text

**Committed the concrete deterministic tie-break contract (original-index ascending for equal densities) across .NET/Rust doc comments and both spec chapters, with all 142 .NET + 128 Rust tests green**

## What Happened

Reviewed both implementations (.NET and Rust) against the T01 regression tests — both already implemented the correct comparator (density descending, original index ascending). No code logic changes were needed.

Enhanced doc comments in both implementations with explicit tie-break contract language: equal-density items preserve original input order via original-index ascending tiebreak; zero-token items (all sharing MAX density) use original index only, ignoring score.

Replaced the vague "Sort Stability" section in `spec/src/slicers/greedy.md` with a concrete "Deterministic Tie-Break Contract" section using original-index language and referencing the budget-simulation dependency.

Updated the DryRun Determinism Invariant in `spec/src/analytics/budget-simulation.md` to point specifically at the GreedySlice tie-break contract with a cross-reference link, replacing the prior generic "stable across calls" language.

Updated the conformance notes in the greedy spec to reference the deterministic tie-break contract explicitly.

## Verification

- .NET GreedySlice tests: 14/14 passed (including 4 deterministic tie-break regression tests from T01)
- Rust tests: 128/128 passed (including 4 matching tie-break regression tests)
- Full .NET suite: 723/723 passed
- .NET build: 0 errors, 0 warnings
- Grep confirms "original index" / "original order" / "deterministic" language present across all 4 target files

### Must-Haves

| # | Must-Have | Status | Evidence |
|---|-----------|--------|----------|
| 1 | .NET GreedySlice has explicit regression coverage for equal-density and zero-token ties | ✓ PASS | 4 regression tests from T01 all pass |
| 2 | Rust GreedySlice has matching regression coverage | ✓ PASS | 4 matching tests in greedy.rs all pass |
| 3 | greedy.md states concrete deterministic contract | ✓ PASS | "Deterministic Tie-Break Contract" section with original-index language |
| 4 | budget-simulation.md references same tie behavior | ✓ PASS | Cross-reference to greedy.md#deterministic-tie-break-contract |
| 5 | No ContextItem.Id field introduced | ✓ PASS | grep confirms no Id property on ContextItem |

## Diagnostics

- Run GreedySlice-focused tests to verify tie-break behavior: `dotnet test --project tests/Wollax.Cupel.Tests -- --treenode-filter "/*/*/GreedySliceTests/*"`
- Run Rust tie-break tests: `cargo test --all-targets` in `crates/cupel/`
- Grep for contract language: `rg "original.index|deterministic.*tie" spec/src/slicers/greedy.md spec/src/analytics/budget-simulation.md`

## Deviations

None — implementations already matched the contract; only doc comments and spec text needed updating.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/GreedySlice.cs` — Enhanced XML doc comment with explicit tie-break contract and sort comment
- `crates/cupel/src/slicer/greedy.rs` — Enhanced doc comment with explicit tie-break contract
- `spec/src/slicers/greedy.md` — Replaced "Sort Stability" with "Deterministic Tie-Break Contract" section; updated conformance notes
- `spec/src/analytics/budget-simulation.md` — Updated DryRun Determinism Invariant with concrete GreedySlice cross-reference
