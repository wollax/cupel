---
estimated_steps: 5
estimated_files: 3
---

# T02: Implement IEquatable on .NET ContextItem with collection-aware equality

**Slice:** S01 — SelectionReport structural equality
**Milestone:** M004

## Description

Override equality on `ContextItem` (sealed record) to provide deep comparison of `Tags` (IReadOnlyList\<string\>) and `Metadata` (IReadOnlyDictionary\<string, object?\>). Without this, the compiler-generated record equality uses reference equality for these collection properties, making `==` unreliable for independently constructed items.

`ContextItem` is the base nested inside `IncludedItem.Item` and `ExcludedItem.Item`, so its equality must be correct before T03 can build on it.

## Steps

1. In `src/Wollax.Cupel/ContextItem.cs`, add `IEquatable<ContextItem>` to the record declaration (explicit, even though sealed record generates it implicitly — signals intent per research)
2. Override `Equals(ContextItem? other)`:
   - Null check → false
   - Compare scalar properties: `Content`, `Tokens`, `Kind`, `Source`, `Priority`, `Pinned`, `OriginalTokens`, `FutureRelevanceHint`, `Timestamp`
   - For `FutureRelevanceHint`: use `==` operator (exact f64 per D103); `Nullable<double>` default equality already does this
   - Tags: `Tags.SequenceEqual(other.Tags)` (ordered comparison)
   - Metadata: count check + element-wise `TryGetValue` + `Equals(v1, v2)` using `object.Equals()` static method for null-safe dispatch
3. Override `GetHashCode()`:
   - Combine: `Content`, `Tokens`, `Kind`, `Source`, `Priority`, `Pinned` via `HashCode.Combine`
   - Add Tags contribution: `Tags.Count` + first element hash (pragmatic, avoids O(n) hashing)
   - Add Metadata contribution: `Metadata.Count`
4. Add `operator ==` and `operator !=` (record generates these but listing explicitly for PublicAPI completeness if needed)
5. Update `src/Wollax.Cupel/PublicAPI.Unshipped.txt` with new public surface:
   - `Wollax.Cupel.ContextItem.Equals(Wollax.Cupel.ContextItem? other) -> bool`
   - `override Wollax.Cupel.ContextItem.GetHashCode() -> int`
   - Any operator entries the analyzer requires
6. Create `tests/Wollax.Cupel.Tests/Models/ContextItemEqualityTests.cs`:
   - Two identical items compare equal
   - Items with different Content compare unequal
   - Items with different Tags (same elements, different order) compare unequal (ordered)
   - Items with same Tags in same order compare equal
   - Items with different Metadata values compare unequal
   - Items with same Metadata (same keys+values) compare equal
   - Items with different Metadata keys compare unequal
   - Items with null vs non-null Timestamp compare unequal
   - Items with different FutureRelevanceHint compare unequal
   - Null-safe: `item.Equals(null)` returns false
   - GetHashCode: equal items produce same hash
7. Run `dotnet test --configuration Release` and `dotnet build --configuration Release`

## Must-Haves

- [ ] `ContextItem` explicitly implements `IEquatable<ContextItem>`
- [ ] `Equals` compares Tags via `SequenceEqual` and Metadata via element-wise comparison
- [ ] `GetHashCode` is consistent with `Equals`
- [ ] `PublicAPI.Unshipped.txt` updated (build produces 0 warnings)
- [ ] ≥10 test cases in `ContextItemEqualityTests.cs`
- [ ] `dotnet test --configuration Release` passes

## Verification

- `dotnet build --configuration Release` — 0 errors, 0 warnings
- `dotnet test --configuration Release` — all tests pass including new equality tests
- `grep -c "SequenceEqual\|IEquatable" src/Wollax.Cupel/ContextItem.cs` — confirms collection-aware equality

## Observability Impact

- Signals added/changed: None
- How a future agent inspects this: `dotnet test` exercises equality paths
- Failure state exposed: None

## Inputs

- `src/Wollax.Cupel/ContextItem.cs` — current sealed record with no custom equality
- `src/Wollax.Cupel/ContextKind.cs` — already implements `IEquatable<ContextKind>` (case-insensitive)
- S01-RESEARCH.md — confirms Tags uses `SequenceEqual`, Metadata uses element-wise comparison

## Expected Output

- `src/Wollax.Cupel/ContextItem.cs` — custom `Equals`/`GetHashCode` with deep collection comparison
- `tests/Wollax.Cupel.Tests/Models/ContextItemEqualityTests.cs` — ≥10 equality test cases
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — updated with new equality surface
