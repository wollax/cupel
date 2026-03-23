---
estimated_steps: 6
estimated_files: 6
---

# T04: Fill test coverage gaps and remove test hygiene issues

**Slice:** S06 — .NET Quality Hardening
**Milestone:** M001

## Description

Eight test changes: six new tests covering real behavioral gaps (Knapsack+StreamBatchSize policy validation, QuotaSlice constructor null args, PriorityScorer range invariant, TagScorer case-insensitive matching, TagScorer zero-weight branch), one duplicate test removal, and one weak assertion tightened. All new tests use the `ThrowsExactly<T>` pattern and each test class's own `CreateItem` helper — no shared fixtures assumed. Test count increases by net 8 (7 additions minus 1 removal, plus 1 that replaces a weak assertion in place without changing count).

## Steps

1. **CupelPolicyTests — Knapsack+Stream batch size** (`tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs`). Add test `Validation_StreamBatchSizeWithKnapsackSlicer_Throws` (symmetric to the existing `Validation_StreamBatchSizeWithGreedySlicer_Throws`). A policy that specifies a `StreamBatchSize` together with `SlicerType.Knapsack` should throw on validation. Study the existing greedy variant for the exact setup pattern.

2. **QuotaSliceTests — null constructor args** (`tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs`). Add two tests: `QuotaSlice_NullSlicer_ThrowsArgumentNull` and `QuotaSlice_NullQuotas_ThrowsArgumentNull`. Use `ThrowsExactly<ArgumentNullException>`. Pass `null!` for the respective constructor argument; verify each throws. Study the class's existing `CreateItem` or helper pattern first.

3. **PriorityScorerTests — range invariant** (`tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs`). Add `ScoresAreInZeroToOneRange`. Follow the RecencyScorer pattern: create items with varying priority values, run through the scorer, assert that every returned score is `>= 0.0` and `<= 1.0`. Use the class's existing `CreateItem` helper.

4. **TagScorerTests — two new tests** (`tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs`). 
   - Add `TagScorer_CaseInsensitiveMatch`: create a tag scorer with key `"important"`, weight `1.0`; create an item whose tags include `"IMPORTANT"` (uppercase); verify the item receives a non-zero score.
   - Add `TagScorer_ZeroTotalWeight_ReturnsZeroScore` (or similar): configure a scorer where all tag weights sum to zero (or no tags match any item tag at all leading to zero-weight path); verify score is `0.0` without throwing. Study the existing class `CreateItem` helper and scoring setup pattern before adding.

5. **CupelServiceCollectionExtensionsTests — remove duplicate** (`tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs`). Locate `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` at approximately line 287 (the second occurrence). Remove this duplicate test method entirely. The test at approximately line 155 (the first occurrence) remains.

6. **PipelineBuilderTests — tighten weak assertion** (`tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs`). Find the assertion `result.Items.Count > 0` at approximately line 688. Change to `IsEqualTo(3)` (the test sets up 3 items that all fit within the 500-token budget). Read the test setup to confirm count before changing.

7. Run `dotnet test` — verify all tests pass. Total passing count should increase by 7 (net: +7 tests added, −1 removed, +0 from assertion tightening which changes no count). Run `dotnet build` to confirm zero warnings.

## Must-Haves

- [ ] `Validation_StreamBatchSizeWithKnapsackSlicer_Throws` added to `CupelPolicyTests` and passes
- [ ] `QuotaSlice_NullSlicer_ThrowsArgumentNull` added with `ThrowsExactly<ArgumentNullException>` and passes
- [ ] `QuotaSlice_NullQuotas_ThrowsArgumentNull` added with `ThrowsExactly<ArgumentNullException>` and passes
- [ ] `ScoresAreInZeroToOneRange` added to `PriorityScorerTests` and passes
- [ ] `TagScorer_CaseInsensitiveMatch` added and passes (uppercase tag key matches lowercase scorer key)
- [ ] Zero-total-weight TagScorer test added and passes without throwing
- [ ] Duplicate `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` at line ~287 removed
- [ ] `PipelineBuilderTests` line ~688 assertion changed from `Count > 0` to `IsEqualTo(3)`
- [ ] No new shared fixtures introduced — each test uses the class-local `CreateItem` helper pattern
- [ ] All exception tests use `ThrowsExactly<T>` (not `Throws<T>`)
- [ ] `dotnet test` — all tests pass; count increases by 7 net

## Verification

- `dotnet test --filter "FullyQualifiedName~CupelPolicyTests"` — new test passes
- `dotnet test --filter "FullyQualifiedName~QuotaSliceTests"` — two new tests pass
- `dotnet test --filter "FullyQualifiedName~PriorityScorerTests|TagScorerTests"` — three new tests pass
- `dotnet test` — full suite passes; no duplicate test name errors; total count increased
- `dotnet build` — zero warnings

## Observability Impact

- Signals added/changed: failing tests now name the specific behavior that broke (e.g. `QuotaSlice_NullSlicer_ThrowsArgumentNull` vs a null-ref crash at runtime without a test)
- How a future agent inspects this: failing test names in `dotnet test` output identify the exact code path; `ThrowsExactly<T>` produces clear assertion messages when wrong exception type is thrown
- Failure state exposed: all new tests exercise previously untested branches — a future regression in those paths is now caught immediately rather than silently

## Inputs

- `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs` — read `Validation_StreamBatchSizeWithGreedySlicer_Throws` for the setup pattern to mirror for Knapsack
- `tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs` — read constructor signature and existing test helper before adding null-arg tests
- `tests/Wollax.Cupel.Tests/Scoring/RecencyScorerTests.cs` — read `ScoresAreInZeroToOneRange` for the pattern to replicate in PriorityScorerTests
- `tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs` — read existing tag-match test to understand helper and scoring setup
- `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs` — confirm line ~287 is a duplicate before removing
- `tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs` — read test setup at line ~688 to confirm 3 items in 500-token budget

## Expected Output

- `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs` — 1 new test
- `tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs` — 2 new tests
- `tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs` — 1 new test
- `tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs` — 2 new tests
- `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs` — 1 test removed (duplicate)
- `tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs` — 1 assertion tightened
