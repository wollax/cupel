---
area: testing
priority: low
source: PR review (phase 03)
---

# Scorer test coverage gaps from PR review

Suggestions identified during Phase 3 PR review that weren't blocking:

1. **PriorityScorerTests missing `ScoresAreInZeroToOneRange`** — RecencyScorerTests has this; PriorityScorerTests should have the equivalent for symmetry.

2. **TagScorerTests missing case-insensitive test** — No test verifying `"IMPORTANT"` matches weight key `"important"` when OrdinalIgnoreCase dict is used.

3. **TagScorerTests missing zero-total-weight test** — Branch `_totalWeight == 0.0` exists but is untested (reachable with all-zero weights).

4. **Tied-top behavior undocumented** — Two items tied at max priority/timestamp both score < 1.0. Tests verify they're equal but don't pin the value or document the behavior.

5. **RecencyScorer/PriorityScorer structural duplication** — Both scorers implement identical rank-normalization logic. Could share a helper, but not urgent since they have distinct public identities.

6. **TagScorer comparer extraction fragile** — Only preserves comparer from `Dictionary<string, double>`. Other `IReadOnlyDictionary` implementations silently fall back to `Ordinal`. Consider accepting explicit `IEqualityComparer<string>?` parameter.

7. **Missing constructor validation tests** — `TagScorerTests` lacks `NullWeights_ThrowsArgumentNull` and `NegativeWeight_ThrowsArgumentOutOfRange`. `KindScorerTests` lacks null guard test.

8. **Missing `/// <inheritdoc />`** on `Score()` in RecencyScorer and PriorityScorer — inconsistent with FrequencyScorer and ReflexiveScorer.

9. **KindScorer default weights undocumented** — XML doc should enumerate SystemPrompt=1.0, Memory=0.8, etc.

10. **ReflexiveScorer XML doc style** — Should use `<see cref="ContextItem.FutureRelevanceHint"/>` instead of plain text.
