---
title: "Phase 8 review suggestions — code"
source: PR review (Phase 8)
priority: low
area: code-quality
---

# Phase 8 Code Review Suggestions

Suggestions from the Phase 8 PR review that were not fixed immediately.

## Code

1. **`CupelOptions` missing enumeration surface** — No way to enumerate registered intents or count policies. Add `IReadOnlyCollection<string> RegisteredIntents` or `Count` property.
2. **`CupelPresets` allocates new instances every call** — Each preset method creates new `CupelPolicy`/`ScorerEntry` instances. Could cache as static readonly singletons since they are immutable.
3. **`CupelPolicy` property names shadow type names** — `SlicerType SlicerType` and `PlacerType PlacerType` cause Intellisense confusion. Consider renaming properties.
4. **`CupelOptions` missing thread-safety documentation** — No `<remarks>` noting the thread-safety contract. Add `/// <remarks>Not thread-safe. Configure before use.</remarks>`.
5. **`ScorerEntry` accepts empty `TagWeights` dictionary** — A Tag scorer with empty tag weights produces zero scores for everything. Consider `Count > 0` validation.
6. **`CupelPolicy` accepts duplicate `ScorerType` entries** — Two entries with the same ScorerType double-count the same signal. Consider validation or explicit documentation.
7. **`SlicerType.Knapsack` doc "provably optimal" is imprecise** — Should qualify as "within bucket-size granularity" or "for the 0-1 token-budget knapsack".
8. **`CupelPolicy` constructor exception docs** — Single `<exception cref="ArgumentException">` covers two distinct validation rules. Split into two elements.
