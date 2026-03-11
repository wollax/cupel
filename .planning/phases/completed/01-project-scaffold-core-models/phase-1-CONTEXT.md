# Phase 1: Project Scaffold & Core Models - Context

**Gathered:** 2026-03-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Establish the project infrastructure and load-bearing core types that every subsequent phase depends on. Solution builds with TreatWarningsAsErrors, Nullable enable, SourceLink, PublicApiAnalyzers. ContextItem and ContextBudget models are immutable, validated, and JSON-annotated. Core package has zero external dependencies. BenchmarkDotNet project exists with empty-pipeline baseline.

</domain>

<decisions>
## Implementation Decisions

### ContextItem property types
- `Kind` → Smart enum (`ContextKind` class with well-known static values + user extensibility via constructor)
- `Source` → Smart enum (`ContextSource` class, same pattern as `ContextKind`)
- `Priority` → `int?` (nullable, higher = more important, null = unset)
- `Tags` → `IReadOnlyList<string>` (default empty array)
- `Metadata` → `IReadOnlyDictionary<string, object?>` (passthrough bag, Cupel never inspects, default empty)
- `Timestamp` → `DateTimeOffset?` (nullable, null = no temporal signal)
- `FutureRelevanceHint` → `double?` (convention 0.0–1.0, not enforced by type)
- `Content` → `string` (non-nullable, per PROJECT.md)
- `Tokens` → `int` (required, non-nullable, per PROJECT.md)
- `Pinned` → `bool` (default false)
- `OriginalTokens` → `int?` (nullable, for pre-compression tracking)

### Smart enum design
- Both `ContextKind` and `ContextSource` use case-insensitive comparison (`StringComparison.OrdinalIgnoreCase`)
- Both serialize as plain string values via `JsonConverter` (not `{"Value": "..."}`)
- Both implement `IEquatable<T>` with proper `GetHashCode` using `StringComparer.OrdinalIgnoreCase`

### Well-known ContextKind values (Phase 1 minimal set)
- `Message`, `Document`, `ToolOutput`, `Memory`, `SystemPrompt`
- Users extend by constructing `new ContextKind("custom-kind")`

### Well-known ContextSource values
- `Chat`, `Tool`, `Rag` (minimal set, extensible same as Kind)

### ContextBudget semantics
- `MaxTokens` → Hard limit (model's context window ceiling)
- `TargetTokens` → Soft goal (slicer aims here; gap between target and max is the overflow zone)
- `OutputReserve` → Token count subtracted from MaxTokens (reserves space for model output)
- `ReservedSlots` → `Dictionary<ContextKind, int>` carving out guaranteed token budget per Kind before general slicing
- `EstimationSafetyMarginPercent` → Percentage buffer applied to TargetTokens (e.g., 5% on 100k = effective 95k target)
- All inputs validated: non-negative tokens, margin 0–100

### Solution structure
- 3 projects in Phase 1: `Wollax.Cupel` (src), `Wollax.Cupel.Tests` (tests), `Wollax.Cupel.Benchmarks` (benchmarks)
- Companion packages (DI, Tiktoken, Json) created in their respective phases — no empty shells
- Directory layout: `src/`, `tests/`, `benchmarks/` at repo root
- Centralized `Directory.Build.props` for TreatWarningsAsErrors, Nullable, SourceLink, LangVersion, TargetFramework

### Test framework
- TUnit (source-generated test discovery, parallel-by-default, no runtime reflection)

### Claude's Discretion
- Exact Directory.Build.props content beyond the agreed settings
- NuGet package metadata (description, tags, license, icon)
- PublicApiAnalyzers configuration details
- BenchmarkDotNet project structure and initial benchmark design
- Internal code organization within the Core project (folders, namespaces)

</decisions>

<specifics>
## Specific Ideas

- Smart enums should feel natural to construct: `new ContextKind("my-kind")` with static well-knowns for discoverability
- ContextItem is a sealed record with `{ get; init; }` properties — fully immutable
- ContextBudget overflow zone (between TargetTokens and MaxTokens) is where OverflowStrategy activates (Phase 7)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-project-scaffold-core-models*
*Context gathered: 2026-03-10*
