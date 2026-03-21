---
estimated_steps: 5
estimated_files: 2
---

# T01: Add KnapsackSlice DP table size guard

**Slice:** S06 — .NET Quality Hardening
**Milestone:** M001

## Description

Add the R002 DP table size guard to `KnapsackSlice.Slice()`. After the `capacity` variable is discretized and before the `ArrayPool.Rent` calls, check `(long)candidateCount * (capacity + 1) > 50_000_000L`. If exceeded, throw `InvalidOperationException` with a message that names the actual cell count and the 50M limit. Also add a test covering negative-token items being silently skipped (already handled in code — the filter loop uses `tokens > 0` — but no test covers it). Guard arithmetic must use `long` to avoid `int` overflow at large values; use the post-discretization `capacity`, not raw `TargetTokens`.

## Steps

1. Open `src/Wollax.Cupel/KnapsackSlice.cs`. Locate the `// Discretize capacity` block and the subsequent early-return on `capacity == 0`. After `capacity == 0` early-return and after `discretizedWeights` is built (so `candidateCount` is final), insert the guard: `if ((long)candidateCount * (capacity + 1) > 50_000_000L) throw new InvalidOperationException($"KnapsackSlice DP table exceeds the 50,000,000-cell limit. Reduce the number of candidates or increase BucketSize. (candidates={candidateCount}, capacity={capacity}, cells={(long)candidateCount * (capacity + 1)})")`.
2. Open `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs`. Study the existing `CreateItem` helper and `ThrowsExactly<T>` usage pattern.
3. Add a guard boundary test: construct items and a budget whose discretized capacity produces `candidateCount * (capacity + 1) == 50_000_001` (above limit) → expects `InvalidOperationException` via `ThrowsExactly<InvalidOperationException>`. Then construct a case that stays at exactly `50_000_000` cells (at limit — should also throw, since condition is `>`... actually the condition is `> 50_000_000L` so 50M exactly passes). Verify: 50M cells passes, 50M+1 cells throws.
4. Add an above-limit test with clearly large values (e.g. candidateCount=10000, bucketSize=1 so capacity = targetTokens, set targetTokens large enough that `10000 * (capacity+1) > 50M`).
5. Add a negative-token items test: create a scored-items list where some items have `Tokens < 0`, verify they are silently excluded from the result (not included, not crashed on).

## Must-Haves

- [ ] Guard is inserted after `capacity` discretization and after `candidateCount` is finalized, before `ArrayPool.Rent` calls
- [ ] Guard uses `(long)candidateCount * (capacity + 1)` — not raw `TargetTokens`; not `int` arithmetic
- [ ] Guard throws `InvalidOperationException` (not a custom exception type — none exists in .NET)
- [ ] Exception message includes actual cell count and the 50M limit for diagnosability
- [ ] Test: at 50_000_000 cells exactly → passes (condition is `>`, not `>=`)
- [ ] Test: at 50_000_001 cells → throws `InvalidOperationException`
- [ ] Test: at clearly above-limit values → throws `InvalidOperationException`
- [ ] Test: items with negative token counts are silently excluded (not counted as candidates)
- [ ] `dotnet test` — all new tests pass; all 649+ existing tests still pass

## Verification

- `dotnet test --filter "FullyQualifiedName~KnapsackSliceTests"` — all tests pass including the 4 new ones
- `dotnet test` — full suite passes with zero regressions
- `dotnet build` — zero errors, zero warnings

## Observability Impact

- Signals added/changed: `InvalidOperationException.Message` now includes `candidates=N, capacity=C, cells=K` for any OOM-bound call — a future agent can read the exception message to understand why the guard fired
- How a future agent inspects this: test output on failure; exception message in integration context when thrown
- Failure state exposed: `InvalidOperationException` with diagnostic message replaces silent OOM — failure is now locatable and testable

## Inputs

- `src/Wollax.Cupel/KnapsackSlice.cs` — insertion point is after `var capacity = budget.TargetTokens / _bucketSize;` block and after the `candidateCount == 0` early-return, before `ArrayPool.Rent`
- `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs` — study `CreateItem` helper and `ThrowsExactly<T>` pattern before adding tests

## Expected Output

- `src/Wollax.Cupel/KnapsackSlice.cs` — guard added; `InvalidOperationException` thrown at >50M cells
- `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs` — 4 new tests (at-limit passes, above-limit throws, large-values throws, negative-token silent-skip)
