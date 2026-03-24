# S01: SelectionReport structural equality — Research

**Date:** 2026-03-23

## Summary

This slice adds structural equality to `SelectionReport`, `IncludedItem`, and `ExcludedItem` in both Rust and .NET, using exact f64 comparison (D103). The two languages have very different implementation profiles.

**Rust** is nearly done — `ContextItem` already derives `PartialEq`, `ExclusionReason` and `InclusionReason` already have `PartialEq`. The gap is adding `PartialEq` to `IncludedItem`, `ExcludedItem`, `SelectionReport`, `TraceEvent`, `CountRequirementShortfall`, and `OverflowEvent`. Since these contain `f64` fields (scores, duration_ms), `Eq` cannot be derived — only `PartialEq`. This is correct per D103 (exact f64) and still enables `==` comparison. The `#[non_exhaustive]` attribute does NOT affect derive behavior.

**.NET** is the harder side. `sealed record` auto-generates value equality for primitive properties but uses **reference equality** for `IReadOnlyList<T>` and `IReadOnlyDictionary<string, object?>` properties. Affected types:
- `ContextItem` — `Tags` (IReadOnlyList\<string\>) and `Metadata` (IReadOnlyDictionary\<string, object?\>) will compare by reference, not by content.
- `SelectionReport` — `Events`, `Included`, `Excluded`, `CountRequirementShortfalls` (all IReadOnlyList\<T\>) will compare by reference.
- `IncludedItem` — contains `ContextItem Item` which has the above issue.
- `ExcludedItem` — same, plus `DeduplicatedAgainst` (nullable ContextItem).

Each type needs custom `Equals`/`GetHashCode` overrides with sequence-aware collection comparison. The `IEquatable<T>` interface should be explicit even though `sealed record` generates it implicitly — it signals intent.

## Recommendation

**Rust:** Add `PartialEq` derives to 5 structs (`IncludedItem`, `ExcludedItem`, `SelectionReport`, `TraceEvent`, `CountRequirementShortfall`). Do NOT add `Eq` — f64 fields prevent it. This is a ~20-line change across derive annotations.

**.NET:** Implement `IEquatable<T>` with custom `Equals`/`GetHashCode` on `ContextItem`, `SelectionReport`, `IncludedItem`, and `ExcludedItem`. Use `SequenceEqual` for ordered lists and element-wise comparison for dictionaries. `TraceEvent` (readonly record struct with no collections) and `CountRequirementShortfall` (positional record with only primitives + ContextKind) already have correct value equality — no changes needed.

**Key design choice:** For `ContextItem.Metadata` (`IReadOnlyDictionary<string, object?>`), equality should compare keys + values using `Equals` on each value. This handles `double`, `string`, etc. correctly since `object.Equals` dispatches to the runtime type's equality.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Collection sequence equality | `Enumerable.SequenceEqual()` in .NET | Standard, handles null elements, short-circuits on length mismatch |
| Dictionary equality | Element-wise key-value comparison | No built-in, but straightforward with count check + `TryGetValue` loop |

## Existing Code and Patterns

- `crates/cupel/src/model/context_item.rs` — Already derives `PartialEq` on `ContextItem` with `HashMap<String, String>`. HashMap PartialEq works correctly in Rust (element-wise). Pattern: derive-based equality.
- `crates/cupel/src/diagnostics/mod.rs` — `ExclusionReason` already has `PartialEq`, `InclusionReason` has `PartialEq, Eq`. `PipelineStage` has `PartialEq, Eq, Hash`. Pattern: data-carrying enums with PartialEq.
- `crates/cupel/src/model/context_kind.rs` — Custom `PartialEq`/`Eq`/`Hash` impl for case-insensitive comparison. Pattern: manual impl when derive semantics are insufficient.
- `crates/cupel/src/model/context_source.rs` — Custom `PartialEq`/`Eq`/`Hash` impl for case-insensitive comparison. Same pattern.
- `src/Wollax.Cupel/ContextKind.cs` — `IEquatable<ContextKind>` with case-insensitive string comparison. Pattern: explicit IEquatable on sealed class. The .NET diagnostic types should follow this explicit-IEquatable approach.
- `src/Wollax.Cupel/ContextItem.cs` — `sealed record` with NO custom Equals override. Tags and Metadata currently use reference equality. This is the main type that needs fixing.
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — `sealed record` with NO custom Equals. All `IReadOnlyList<T>` properties use reference equality. Needs custom equality.
- `src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs` — Positional `sealed record(ContextKind Kind, int RequiredCount, int SatisfiedCount)`. Since ContextKind already implements IEquatable correctly, this record's auto-generated equality is correct. No changes needed.
- `src/Wollax.Cupel/Diagnostics/TraceEvent.cs` — `readonly record struct` with only primitives + enum + nullable string. Auto-generated equality is correct. No changes needed.
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Must be updated with any new public API surface (IEquatable implementations, operator overloads).

## Constraints

- `#[non_exhaustive]` on Rust structs does NOT block `PartialEq` derive (derive has crate-level access to all fields).
- .NET `sealed record` with collection properties requires overriding both `Equals(T?)` and `GetHashCode()`. The compiler-generated `PrintMembers` is fine to keep.
- `f64` exact comparison: Rust `PartialEq` on f64 uses bitwise `==`, so `NaN != NaN`. This is correct — deterministic pipelines produce identical scores, and NaN comparison returning false is the IEEE 754 standard.
- .NET `double` comparison: `double.Equals(double)` treats `NaN.Equals(NaN)` as `true` (unlike `==` operator). For `IncludedItem.Score` and `ExcludedItem.Score`, both behaviors are acceptable since NaN scores shouldn't appear in practice. Using `==` is simpler and consistent with D103's "exact f64" language.
- `ContextItem.Metadata` in .NET is `IReadOnlyDictionary<string, object?>` — values can be `double`, `string`, or other types. Equality comparison via `object.Equals()` dispatches correctly for all BCL types.
- PublicAPI analyzers must pass — new `IEquatable<T>` implementations and any `Equals`/`GetHashCode`/`operator ==`/`operator !=` overrides must be listed in `PublicAPI.Unshipped.txt`.
- The `ExcludedItem.DeduplicatedAgainst` nullable property must be compared with null-safe equality.
- `OverflowEvent` does NOT need `PartialEq` — it is not part of `SelectionReport` (it's a separate event type not referenced by the report struct). However, adding it is low-cost and consistent.

## Common Pitfalls

- **Record equality with IReadOnlyList generates reference comparison** — In .NET, `sealed record` uses `EqualityComparer<T>.Default` for each property. For `IReadOnlyList<T>`, this is reference equality. The fix is overriding `Equals` and using `SequenceEqual`. Forgetting this would make `==` appear to work on identical-reference lists (e.g., from the same pipeline run) but fail on deserialized or independently-constructed reports.
- **Forgetting GetHashCode when overriding Equals** — .NET requires consistent `Equals`/`GetHashCode`. For collections, a simple combine of element hashes (or count-based hash for perf) is sufficient. Using `HashCode.Combine` with list length + first/last elements is a pragmatic approach.
- **Rust PartialEq on structs with `Vec<(ExcludedItem, usize)>`** — The `DiagnosticTraceCollector` stores `excluded` as `Vec<(ExcludedItem, usize)>` internally, but `SelectionReport.excluded` is already `Vec<ExcludedItem>` (the usize is stripped in `into_report()`). No issue for the public type.
- **Breaking positional deconstruction (D057)** — Adding `IEquatable<T>` interface to existing `sealed record` types does NOT break positional deconstruction since it doesn't change constructor arity. Safe to add.

## Open Risks

- **ContextItem equality scope creep** — `ContextItem` is a core type used everywhere. Adding custom equality changes its behavior globally, not just for `SelectionReport` comparison. Any existing code relying on record reference-like equality for Tags/Metadata will now get deep equality. This is the correct behavior but could surface latent bugs if any code accidentally depends on reference inequality for distinct-but-equal items. Risk is LOW — the current default equality was broken (reference comparison on collections), so fixing it is strictly better.
- **Performance of deep equality on large reports** — `SelectionReport` with thousands of items will do element-wise comparison of `IncludedItem`/`ExcludedItem` lists, each comparing `ContextItem` (which compares Tags list + Metadata dictionary). For test assertions this is fine. If callers use `==` in hot paths, they'd need to be aware. Risk is LOW — equality is used for testing and diagnostics, not hot-path selection.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| .NET records | n/a | Not applicable — standard C# feature, no skill needed |
| Rust derives | n/a | Not applicable — standard Rust feature, no skill needed |

## Sources

- Codebase exploration of `crates/cupel/src/diagnostics/mod.rs`, `src/Wollax.Cupel/Diagnostics/*.cs`, `src/Wollax.Cupel/ContextItem.cs`
- D103 (exact f64 comparison decision), D057 (positional deconstruction unsupported)
- R050 requirement definition (structural equality prerequisite for R051, R053)
