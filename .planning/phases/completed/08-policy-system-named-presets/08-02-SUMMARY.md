---
phase: 08-policy-system-named-presets
plan: 02
status: complete
started: 2026-03-14T03:55:45Z
completed: 2026-03-14T04:01:53Z
duration: 368s
---

# Plan 08-02 Summary: Named Presets & Intent-Based Registry

## Objective
Create the 7 named presets (CupelPresets static class) and intent-based policy registry (CupelOptions).

## Tasks Completed

### Task 1: CupelPresets with [Experimental] attributes
- Created `CupelPresets` static class with 7 factory methods: Chat, CodeReview, Rag, DocumentQa, ToolUse, LongRunning, Debugging
- Each method has a unique `[Experimental("CUPELXXX")]` diagnostic ID (CUPEL001–CUPEL007)
- All presets use `deduplicationEnabled: true` and `overflowStrategy: OverflowStrategy.Throw`
- DocumentQa uses `SlicerType.Knapsack` with `knapsackBucketSize: 100`; all others use `SlicerType.Greedy`
- Rag and DocumentQa use `PlacerType.UShaped`; all others use `PlacerType.Chronological`
- 14 TDD tests covering policy structure, scorer types/weights, and ordinal weight comparisons

### Task 2: CupelOptions intent-based registry
- Created `CupelOptions` sealed class with case-insensitive `Dictionary<string, CupelPolicy>` backing store
- `AddPolicy(string, CupelPolicy)` — validates inputs, overwrites existing, returns `this` for chaining
- `GetPolicy(string)` — throws `KeyNotFoundException` for unknown intents
- `TryGetPolicy(string, out CupelPolicy?)` — safe lookup with `[NotNullWhen(true)]`
- All methods validate null/whitespace intent and null policy
- 15 TDD tests covering round-trip, case insensitivity, error cases, overwrite, and chaining
- Updated `PublicAPI.Unshipped.txt` with all new public surface

## Decisions
- (none new — followed existing patterns)

## Deviations
- **Auto-fix:** `PipelineBuilder.WithPolicy` was added in plan 01 but missing from `PublicAPI.Unshipped.txt`. The linter auto-added it during this plan's build cycle.

## Files Modified
| File | Action |
|------|--------|
| `src/Wollax.Cupel/CupelPresets.cs` | Created — 7 preset factory methods |
| `src/Wollax.Cupel/CupelOptions.cs` | Created — intent-based policy registry |
| `tests/Wollax.Cupel.Tests/Policy/CupelPresetsTests.cs` | Created — 14 tests |
| `tests/Wollax.Cupel.Tests/Policy/CupelOptionsTests.cs` | Created — 15 tests |
| `src/Wollax.Cupel/PublicAPI.Unshipped.txt` | Updated — all plan 02 public surface |

## Metrics
- Tests: 29 new (14 presets + 15 options), 517 total passing
- Build: zero warnings with `--warnaserror`
- Duration: ~6 minutes

## Commits
| Hash | Message |
|------|---------|
| `bb4881d` | feat(08-02): add CupelPresets static class with 7 named preset factory methods |
| `d02c964` | feat(08-02): add CupelOptions intent-based policy registry with PublicAPI entries |
