---
estimated_steps: 4
estimated_files: 5
---

# T03: Lock the GreedySlice Tie-Break Contract Across .NET, Rust, and Spec Text

**Slice:** S06 — Budget simulation + tiebreaker + spec alignment
**Milestone:** M003

## Description

Resolve the roadmap/spec mismatch around GreedySlice tie-breaking by committing the real deterministic contract: equal-density items preserve original input order (original-index ascending). No new `ContextItem.Id` surface is added in M003. This task ensures .NET, Rust, and spec text all say the same thing and that repeated dry runs remain deterministic for budget simulation.

## Steps

1. Review the existing .NET and Rust GreedySlice comparators against the T01 regression tests and adjust comments or comparator code only if the implementation and tests disagree.
2. Update `spec/src/slicers/greedy.md` so tie-breaking is described in concrete original-order/original-index terms rather than the ambiguous roadmap shorthand.
3. Update the determinism language in `spec/src/analytics/budget-simulation.md` so the repeated-dry-run contract points at the same original-order tie rule.
4. Re-run the focused .NET tie tests and full Rust suite to confirm the implementation and spec are aligned.

## Must-Haves

- [ ] .NET GreedySlice has explicit regression coverage for equal-density and zero-token ties
- [ ] Rust GreedySlice has matching regression coverage for equal-density and zero-token ties
- [ ] `spec/src/slicers/greedy.md` states the concrete deterministic contract the code implements
- [ ] `spec/src/analytics/budget-simulation.md` references the same tie behavior for dry-run determinism
- [ ] No task in this slice introduces or depends on a new `ContextItem.Id` field

## Verification

- `rtk dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~GreedySliceTests"`
- `rtk cargo test --all-targets`
- `rtk grep "original index|original order|stable" spec/src/slicers/greedy.md spec/src/analytics/budget-simulation.md src/Wollax.Cupel/GreedySlice.cs crates/cupel/src/slicer/greedy.rs`

## Observability Impact

- Signals added/changed: deterministic-order regressions become explicitly visible in two language-specific test suites instead of surfacing later as flaky budget-simulation comparisons
- How a future agent inspects this: run the focused GreedySlice tests or grep the implementation/spec files for the stable-order language
- Failure state exposed: any divergence between code and docs shows up as a test failure or a missing phrase in the spec text

## Inputs

- `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs` — .NET regression tests added in T01
- `crates/cupel/src/slicer/greedy.rs` — Rust comparator and tests
- `src/Wollax.Cupel/GreedySlice.cs` — .NET comparator and comments
- `spec/src/slicers/greedy.md` — current GreedySlice algorithm chapter
- `spec/src/analytics/budget-simulation.md` — dry-run determinism contract consumed by budget simulation
- Slice research summary — documents why literal "id ascending" is impossible with the current data model

## Expected Output

- `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs` — finalized deterministic-tie regression coverage in .NET
- `crates/cupel/src/slicer/greedy.rs` — finalized deterministic-tie regression coverage in Rust
- `spec/src/slicers/greedy.md` — clear original-order/original-index tie-break contract
- `spec/src/analytics/budget-simulation.md` — determinism section aligned to the same contract
- `src/Wollax.Cupel/GreedySlice.cs` — comments or comparator clarified only if needed to match the committed contract
