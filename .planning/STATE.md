# Cupel — Project State

## Current Position

Phase: 1 of 10 — Project Scaffold & Core Models
Milestone: v1.0 Core Library
Status: Phase 1 complete — verified
Next phase: Phase 2
Last activity: 2026-03-11 — Phase 1 verified and completed (5/5 plans, 91 tests, all success criteria met)

## Phase Overview

NEXT_PHASE=2

| Phase | Status |
|-------|--------|
| 1. Project Scaffold & Core Models | ● complete (5/5 plans) |
| 2. Interfaces & Diagnostics Infrastructure | ○ planned |
| 3. Individual Scorers | ○ planned |
| 4. Composite Scoring | ○ planned |
| 5. Pipeline Assembly & Basic Execution | ○ planned |
| 6. Advanced Slicers & Quota System | ○ planned |
| 7. Explainability & Overflow Handling | ○ planned |
| 8. Policy System & Named Presets | ○ planned |
| 9. Serialization & JSON Package | ○ planned |
| 10. Companion Packages & Release | ○ planned |

## Accumulated Context

### Decisions
- Content is non-nullable string on ContextItem (brainstorm explored nullable, PROJECT.md specifies non-nullable)
- Ordinal-only scoring invariant confirmed across all brainstorm tracks
- CompositeScorer over scorer DAG (nested composites, ~30 lines vs hundreds)
- Fixed pipeline over middleware (no call-next, no silent-drop)
- IPlacer interface (pluggable, U-shaped as default not mandate)
- GreedySlice is the default slicer; KnapsackSlice is opt-in for provably optimal selection
- Named policies are [Experimental] at launch — opinionated defaults that will evolve
- Phase 1 includes BenchmarkDotNet and PublicApiAnalyzers as non-negotiable infrastructure
- .NET 10 SDK generates .slnx (XML solution format) by default — used Cupel.slnx instead of Cupel.sln
- .NET 10 SDK requires Microsoft.Testing.Platform runner mode via global.json for dotnet test with TUnit
- STJ returns null (not JsonException) for JSON literal `null` on reference types — converter not invoked
- Record value equality uses reference equality for collection properties (IReadOnlyList, IReadOnlyDictionary) — not structural
- TUnit exception assertions: use Throws<T>() / ThrowsExactly<T>(), not ThrowsException().OfType<T>()
- ContextBudget is a sealed class (not record) to prevent with-expressions bypassing constructor validation
- Custom ContextKindDictionaryConverter handles Dictionary<ContextKind, int> serialization for ReservedSlots

### Blockers
(None)

### Technical Debt
(None)
