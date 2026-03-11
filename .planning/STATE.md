# Cupel — Project State

## Current Position

Phase: 2 of 10 — Interfaces & Diagnostics Infrastructure
Milestone: v1.0 Core Library
Plan: 2 of 2
Status: Phase 2 complete — verified
Next phase: Phase 3
Last activity: 2026-03-11 — Phase 2 verified and completed (2/2 plans, 153 tests, all success criteria met)

## Phase Overview

NEXT_PHASE=3

| Phase | Status |
|-------|--------|
| 1. Project Scaffold & Core Models | ● complete (5/5 plans) |
| 2. Interfaces & Diagnostics Infrastructure | ● complete (2/2 plans) |
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
- ScoredItem lives in root namespace (Wollax.Cupel) — appears in pipeline interface signatures
- TraceEvent uses required init properties (not positional constructor) for clarity
- TUnit HasCount() obsolete in current version — use Count().IsEqualTo(n)
- TUnit Assert.That(constant) triggers analyzer error — use non-constant expressions
- ContextResult.TotalTokens uses manual for-loop (no LINQ) to avoid delegate allocations
- Sealed records with required properties in .NET 10 do not generate public copy constructors
- No AsyncLocal in codebase — explicit ITraceCollector parameter propagation confirmed

### Blockers
(None)

### Technical Debt
(None)
