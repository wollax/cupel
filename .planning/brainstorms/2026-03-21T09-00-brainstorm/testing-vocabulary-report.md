# Testing Vocabulary — Challenger Report

*Task: T02 | Challenger pass: precision-first critique of explorer proposals*
*Date: 2026-03-21 | Brainstorm session: 2026-03-21T09-00*
*Explorer source: testing-vocabulary-ideas.md*

---

## Challenger Position

The goal is not to reject proposals but to identify every place where a vague term will force S05 to make an undocumented design choice. An assertion library that silently passes for the wrong reason is worse than no assertion library. Every "high-scoring", "edge", "dominant", and ordering-dependent assertion must be reduced to a precise definition before S05 can spec it.

D041 is applied throughout without re-debate: no FluentAssertions, no snapshot assertions.

---

## Category 1: Item Presence (P01–P06)

### P01 — IncludeItemWithKind ✅ Ready to spec
**Challenge applied:** Does this depend on ordering? No — it is a membership test (exists in list).
**Ordering dependency:** None.
**Precision requirement:** "At least one item in `Included` where `item.Kind == kind`."
**Error message format:** `"Expected at least one item with Kind={kind} in Included, but Included had {count} items: [{kinds}]."`
**Tolerance:** None needed.
**Verdict:** Ready to spec in S05.

---

### P02 — IncludeExactlyNItemsWithKind ✅ Ready to spec
**Challenge applied:** Exact count — no ordering dependence.
**Precision requirement:** `Included.Count(i => i.Item.Kind == kind) == n`.
**Error message format:** `"Expected exactly {n} items with Kind={kind} in Included, but found {actual}. Included had {count} items total."`
**Tolerance:** None.
**Verdict:** Ready to spec. Note: N=0 overlaps with `NotIncludeKind` (K04). S05 should define whether P02(n=0) is a valid spelling or whether K04 is the canonical form.

---

### P03 — IncludeItemMatching ✅ Ready to spec (with one note)
**Challenge applied:** Predicate-based — callers own the definition. No "high-scoring" language.
**Precision requirement:** `Included.Any(i => predicate(i.Item))`.
**Error message format:** `"Expected at least one item in Included matching the predicate, but none matched. Included had {count} items."`
Optionally include a summary of included items if count is small (e.g., ≤5).
**Note:** S05 must decide whether the predicate is over `ContextItem` (the raw item) or `IncludedItem` (which also carries `score` and `reason`). Predicate over `IncludedItem` is more powerful. Specify this explicitly.
**Verdict:** Ready to spec pending the predicate-type decision.

---

### P04 — IncludeItemsInOrder ✅ Ready to spec (ordering dependency acknowledged)
**Challenge applied:** `Included` is final-placed-order — this is guaranteed. The ordering guarantee is: the Placer determines order.
**Ordering dependency:** YES — depends on the Placer. This assertion is a Placer behavior test, not a pipeline test. S05 must document this: "Only valid when the caller knows the Placer's ordering contract. For DefaultPlacer/UShapedPlacer, refer to the Placer's spec for the order guarantee."
**Precision requirement:** `Included.IndexOf(itemA) < Included.IndexOf(itemB)`. "IndexOf" should use a caller-supplied predicate or equality check — identity comparison (`ReferenceEquals`) is wrong for deserialized reports.
**Error message format:** `"Expected item matching {predicateA} to appear before item matching {predicateB} in Included. {predicateA} was at index {ia}, {predicateB} was at index {ib}. Included had {count} items."`
**Verdict:** Ready to spec, with documented dependency on Placer contract.

---

### P05 — IncludeAtLeastNItems ✅ Ready to spec
**Challenge applied:** Simple count — no ordering dependence, no vague terms.
**Precision requirement:** `Included.Count >= n`.
**Error message format:** `"Expected at least {n} items in Included, but found {actual}."`
**Verdict:** Ready to spec.

---

### P06 — AllIncludedItemsMatchPredicate ✅ Ready to spec
**Challenge applied:** Universal quantifier — no ordering.
**Precision requirement:** `Included.All(i => predicate(i.Item))` (same predicate-type question as P03 applies).
**Error message format:** `"Expected all items in Included to match the predicate, but {failCount} of {total} did not. First failing item: {item}."`
**Verdict:** Ready to spec.

---

## Category 2: Item Absence (A01–A07)

### A01 — ExcludeItemWithReason ✅ Ready to spec
**Challenge applied:** Membership test on `ExclusionReason` variant. `Excluded` ordering is score-descending — but this is a membership test, so ordering does not affect correctness.
**Precision requirement:** `Excluded.Any(e => e.Reason is <ReasonType>)`. Must handle the variant discriminant, not string matching.
**Error message format:** `"Expected at least one item in Excluded with reason {reason}, but found none. Excluded had {count} items with reasons: [{reasonList}]."`
**Tolerance:** None.
**Verdict:** Ready to spec.

---

### A02 — ExcludeItemMatchingWithReason ✅ Ready to spec
**Challenge applied:** Combines a content/kind predicate with a reason check. `Excluded` ordering affects which item "matches first" but not whether a match exists.
**Precision requirement:** `Excluded.Any(e => predicate(e.Item) && e.Reason is <ReasonType>)`.
**Error message format:** `"Expected an item matching the predicate in Excluded with reason {reason}. Predicate matched {predicateMatchCount} item(s) but none had reason {reason}. Matched items had reasons: [{actualReasons}]."`
**Verdict:** Ready to spec.

---

### A03 — ExcludeItemWithBudgetDetails ⚠️ Needs precision work
**Challenge applied:** `BudgetExceeded.available_tokens` is `effective_target - sum(sliced_item.tokens)` at the moment of exclusion (D025). This is a computed value that depends on pipeline execution state at the Slice stage. Asserting an exact value is fragile unless the test fully controls the input set.
**Precision requirement needed:** Should this assert an exact `available_tokens` value, or a range? For precise unit tests, exact is fine. For integration tests, it may be too brittle.
**Recommendation:** Offer two forms — `WithBudgetExceededReason(expectedItemTokens, expectedAvailableTokens)` for unit tests and `WithBudgetExceededReason(expectedItemTokens, availableTokensAtMost: maxAvailable)` for looser checks.
**Error message format:** `"Expected excluded item matching predicate to have BudgetExceeded with item_tokens={expectedItemTokens} and available_tokens={expectedAvailable}, but found item_tokens={actualItemTokens} and available_tokens={actualAvailable}."`
**Verdict:** Needs precision work — dual-form API decision and tolerance semantics.

---

### A04 — ExcludeItemWithDeduplicationTarget ✅ Ready to spec (with one note)
**Challenge applied:** `deduplicated_against` is a string content reference. What does "names a specific `deduplicated_against`" mean — exact string equality, substring, or predicate?
**Recommendation:** `WithDeduplicatedAgainst(string expectedContent)` uses exact string equality. S05 must document: "The value must match the `content` of the original item exactly — no normalization."
**Error message format:** `"Expected a Deduplicated exclusion with deduplicated_against='{expected}', but found deduplicated_against='{actual}'."`
**Verdict:** Ready to spec with the equality semantics decision documented.

---

### A05 — HaveAtLeastNExclusions ✅ Ready to spec
**Ordering dependency:** None — this is a count.
**Precision requirement:** `Excluded.Count >= n`.
**Error message format:** `"Expected at least {n} items in Excluded, but found {actual}."`
**Verdict:** Ready to spec.

---

### A06 — HaveAtLeastNExclusionsWithReason ✅ Ready to spec
**Ordering dependency:** None.
**Precision requirement:** `Excluded.Count(e => e.Reason is <ReasonType>) >= n`.
**Error message format:** `"Expected at least {n} items in Excluded with reason {reason}, but found {actual}. Excluded had {count} items total."`
**Verdict:** Ready to spec.

---

### A07 — ExcludedItemsAreSortedByScoreDescending ✅ Ready to spec (conformance assertion)
**Ordering dependency:** This IS an ordering assertion — it tests the sort guarantee.
**Precision requirement:** `Excluded` must satisfy: for all `i < j`, `Excluded[i].Score >= Excluded[j].Score`. On ties: `Excluded[i]` was inserted before `Excluded[j]` in pipeline execution order.
**Challenge:** The insertion-order tiebreak is unobservable from the `SelectionReport` alone — no `insertionIndex` field is exposed. The sort is guaranteed by the implementation (D019), but a test that verifies the tiebreak requires the test to know insertion order, which is only available if it constructed the candidate list in known order.
**Recommendation:** `ExcludedItemsAreSortedByScoreDescending()` only asserts the score-descending property, not the tiebreak. Document: "Insertion-order tiebreak is guaranteed by the implementation but cannot be asserted from the report alone without controlling the input set order."
**Error message format:** `"Expected Excluded to be sorted score-descending, but item at index {i} (score={si}) has higher score than item at index {i-1} (score={si-1})."`
**Verdict:** Ready to spec with the tiebreak caveat documented.

---

## Category 3: Kind Coverage (K01–K04)

### K01 — HaveKindInIncluded ✅ Ready to spec
**Verdict:** Alias for P01 with `ContextKind` parameter. Identical precision. S05 should decide whether K01 and P01 are the same method or distinct — recommendation: one method (`IncludeItemWithKind`) with a `ContextKind` overload.

---

### K02 — HaveAllKindsInIncluded ✅ Ready to spec
**Precision requirement:** `kinds.All(k => Included.Any(i => i.Item.Kind == k))`.
**Error message format:** `"Expected all kinds [{kindList}] in Included, but missing: [{missingKinds}]. Included contained kinds: [{actualKinds}]."`
**Verdict:** Ready to spec.

---

### K03 — HaveKindCoverageCount ✅ Ready to spec
**Precision requirement:** `Included.Select(i => i.Item.Kind).Distinct().Count() >= n`.
**Error message format:** `"Expected at least {n} distinct ContextKind values in Included, but found {actual}: [{actualKinds}]."`
**Verdict:** Ready to spec.

---

### K04 — NotIncludeKind ✅ Ready to spec
**Precision requirement:** `Included.All(i => i.Item.Kind != kind)`.
**Error message format:** `"Expected no items with Kind={kind} in Included, but found {count}. Included had {count} items total."`
**Verdict:** Ready to spec.

---

## Category 4: Budget and Utilization (B01–B05)

### B01 — HaveTokenUtilizationAbove ⚠️ Needs precision work
**Challenge applied:** `BudgetUtilization = sum(Included[i].tokens) / budget.MaxTokens`. Two precision questions:
1. What is the denominator? `budget.MaxTokens` or `budget.TargetTokens` (if different from max)? The spec defines `ContextBudget` with both a target and an output reserve. S05 must define: utilization is relative to `MaxTokens` (the hard ceiling) or `TargetTokens` (the soft target). This is the same ambiguity that drove the `BudgetUtilization(budget)` extension method design.
2. Floating-point tolerance. `0.80` as a threshold — should the comparison be `>=` with no tolerance, or `>= 0.80 - epsilon`? For floating-point computed ratios, a tolerance of `1e-9` is appropriate.
**Recommendation:** `HaveTokenUtilizationAbove(threshold, budget, tolerance: 1e-9)`. Document which budget field is the denominator.
**Error message format:** `"Expected token utilization >= {threshold} (± {tolerance}), but computed utilization was {actual:.6f}. Included tokens: {includedTokens}, budget: {budgetMax}."`
**Verdict:** Needs precision work — denominator definition and tolerance spec.

---

### B02 — HaveTokenUtilizationBelow ⚠️ Needs precision work (same as B01)
**Same precision questions as B01 apply.** Tolerance: `<= threshold + epsilon`.
**Error message format:** mirror of B01.
**Verdict:** Needs precision work — same denominator and tolerance issues.

---

### B03 — HaveTokenUtilizationInRange ⚠️ Needs precision work (same as B01)
**Same precision questions as B01 apply.** Must specify inclusive vs. exclusive bounds.
**Recommendation:** `[low, high]` inclusive with tolerance on both ends.
**Error message format:** `"Expected utilization in [{low}, {high}] (± {tolerance}), but computed {actual:.6f}."`
**Verdict:** Needs precision work.

---

### B04 — HaveIncludedTokensLessThanBudget ✅ Ready to spec
**Challenge applied:** `sum(Included[i].tokens) <= budget.MaxTokens`. Pure integer comparison — no floating-point.
**Ordering dependency:** None.
**Error message format:** `"Expected included tokens ({actual}) to be within budget ({max}), but overpack detected by {actual - max} tokens."`
**Verdict:** Ready to spec. Note: this is a conformance invariant — the pipeline should never violate it. If this fails, it's a bug in the pipeline, not a policy assertion failure. S05 should mark it as a conformance assertion, not a typical policy assertion.

---

### B05 — HaveTotalTokensConsideredEqualTo ✅ Ready to spec
**Challenge applied:** `TotalTokensConsidered = sum(included.tokens) + sum(excluded.tokens)`. This is a derived field — asserting exact equality requires complete control over the input set.
**Precision requirement:** `report.TotalTokensConsidered == expected` (exact integer equality).
**Error message format:** `"Expected TotalTokensConsidered == {expected}, but was {actual}."`
**Verdict:** Ready to spec, but S05 should note: only meaningful in unit tests where the full input set is known.

---

## Category 5: Placement and Ordering (O01–O06)

### O01 — PlaceItemAtFront ✅ Ready to spec (with Placer caveat)
**Ordering dependency:** YES — depends on Placer. `Included[0]` is defined as the first item in final placed order.
**Precision requirement:** `Included.Count > 0 && predicate(Included[0].Item)`.
**Error message format:** `"Expected item matching predicate at Included[0], but found item with Kind={kind}, tokens={t}, score={s}."`
**Placer caveat:** Must document: "Only meaningful assertions for known Placer behavior. For `UShapedPlacer`, the first position is the highest-scored item. For other Placers, consult the Placer spec."
**Verdict:** Ready to spec with Placer dependency documented.

---

### O02 — PlaceItemAtBack ✅ Ready to spec (same Placer caveat as O01)
**Precision requirement:** `Included.Count > 0 && predicate(Included[^1].Item)`.
**Verdict:** Ready to spec with same Placer caveat.

---

### O03 — PlaceItemAtEdge ✅ Ready to spec (with Placer caveat — this is the primary U-shaped assertion)
**Ordering dependency:** YES — critical. This is the primary assertion from Decision 2's example code.
**Precision requirement:** `predicate(Included[0].Item) || predicate(Included[^1].Item)`.
"At edge" means position 0 OR position `count - 1`. For `UShapedPlacer`, these are the two endpoints of the U.
**Challenge:** "Edge" was undefined in the explorer. The spec must define: "edge" = position 0 or position `Included.Count - 1`, not "first/last 10%" or "first/last N items."
**Error message format:** `"Expected item matching predicate at edge (index 0 or index {last}), but found it at index {actual}. Included had {count} items."`
**Verdict:** Ready to spec pending the "edge = position 0 or N-1" definition.

---

### O04 — PlaceItemsBefore ✅ Ready to spec (ordering dependency acknowledged)
**See P04 analysis.** Identical precision requirements.
**Verdict:** Ready to spec.

---

### O05 — PlaceHighestScoredAtEdges 🚫 Blocked — "high-scoring" is underspecified
**Ordering dependency:** YES — depends on Placer.
**Precision challenge:** "Highest-scored" is undefined. Options:
- Top-N by score (N = 1? N = 2?)
- All items with score above a threshold
- Top quartile
None of these are equivalent. The assertion cannot be specified until "high-scoring" is defined.
**Additional problem:** `UShapedPlacer` places the highest-scored item at index 0 and the second-highest at index `count-1`. Asserting "top 2 are at edges" is a valid test of `UShapedPlacer` behavior — but it requires N=2, and the "edge" definition must match O03.
**Recommendation:** Replace with `PlaceTopNScoredAtEdges(int n)` where `n` is an explicit parameter. Assert that the `n` items with the highest `score` values in `Included` occupy the `n` outermost positions (index 0, index `count-1`, index 1, index `count-2`, ...).
**Verdict:** ⚠️ Needs precision work — redefine as `PlaceTopNScoredAtEdges(n)` with explicit N and explicit edge-position mapping. Do not ship with "high-scoring" language.

---

### O06 — PlaceAllPinnedBeforeScored ⚠️ Needs precision work
**Ordering dependency:** YES.
**Challenge:** `Included` order is Placer-determined. `UShapedPlacer` does not guarantee that all pinned items precede all scored items — it places by score, and pinned items have `score=0.0`. If pinned items have score 0.0 and scored items have score > 0.0, `UShapedPlacer` would place scored items first (higher score at edges) and pinned items in the middle. This assertion may be correct for some Placers and wrong for others.
**Recommendation:** This assertion is Placer-specific and must be labeled as such. S05 should document which Placers support this guarantee before adding the pattern. Consider: `PlaceAllPinnedAtBack()` (valid for score-descending Placers) as the concrete form.
**Verdict:** Needs precision work — Placer contract dependency must be resolved before speccing.

---

## Category 6: Count-Based (C01–C05)

### C01 — HaveExactlyNCandidates ✅ Ready to spec
**Precision requirement:** `report.TotalCandidates == n`.
**Error message format:** `"Expected {n} total candidates, but TotalCandidates was {actual}."`
**Verdict:** Ready to spec. Note: this tests that the correct input set was passed, not pipeline behavior.

---

### C02 — HaveIncludedCount ✅ Ready to spec
**Precision requirement:** `Included.Count == n` (exact) or `Included.Count >= n` (min). S05 should offer both forms.
**Verdict:** Ready to spec.

---

### C03 — HaveExcludedCount ✅ Ready to spec (same as C02)
**Verdict:** Ready to spec.

---

### C04 — HaveNoExclusions ✅ Ready to spec
**Precision requirement:** `Excluded.Count == 0`.
**Error message format:** `"Expected no exclusions, but found {count}. First excluded item: Kind={kind}, Score={score}, Reason={reason}."`
**Verdict:** Ready to spec.

---

### C05 — HaveNoInclusions ✅ Ready to spec
**Precision requirement:** `Included.Count == 0`.
**Error message format:** `"Expected no included items, but Included had {count}."`
**Verdict:** Ready to spec. Note: edge case — useful for zero-budget scenario testing.

---

## Category 7: Diversity (D01–D03)

### D01 — HaveKindDiversityAtLeast ✅ Ready to spec
**See K03.** Identical pattern. S05 should resolve the naming duplication — one canonical method name.
**Verdict:** Ready to spec (consolidate with K03).

---

### D02 — HaveAllCandidateKindsRepresented ⚠️ Needs precision work
**Challenge:** "Every `ContextKind` present in the input candidate set" — the assertion requires access to the original candidate list. `SelectionReport` alone does not expose the full candidate set; it exposes `Included` and `Excluded`, and `TotalCandidates` as a count. To derive "all candidate kinds", the assertion must examine `Included + Excluded`.
**Corrected precision requirement:** `(Included.Select(i => i.Item.Kind) UNION Excluded.Select(e => e.Item.Kind)).ToHashSet() == Included.Select(i => i.Item.Kind).ToHashSet()`.
In words: "all kinds that appear in any candidate (included or excluded) also appear in Included."
**Error message format:** `"Expected all candidate kinds [{allKinds}] to appear in Included, but missing: [{missingKinds}]."`
**Verdict:** Needs precision work — the definition must use `Included ∪ Excluded` as the full candidate kind set, not an external list.

---

### D03 — HaveDominantKind ⚠️ Needs precision work — "dominant" is underspecified
**Challenge:** "More than 50% of included items" is a threshold. Is threshold configurable? What is 50% of 1 item? Is this `> 50%` or `>= 50%`?
**Precision requirement:** `Included.Count(i => i.Item.Kind == kind) * 100.0 / Included.Count > threshold` where threshold is an explicit parameter.
**Recommendation:** Rename to `HaveKindAbovePercentage(ContextKind kind, double percentage)`. Make both the kind and threshold explicit parameters — no default "dominant" interpretation.
**Error message format:** `"Expected Kind={kind} to comprise more than {threshold}% of Included items, but it comprised {actual:.1f}% ({n} of {total})."`
**Tolerance:** For floating-point percentage comparison, tolerance of `1e-9` is appropriate.
**Verdict:** Needs precision work — rename and make threshold explicit.

---

## D041 Application: Snapshot and FluentAssertions (Locked)

**D041 blocks:**
1. Any assertion that serializes `SelectionReport` to a JSON/text file and diffs against a golden file — blocked. Rationale: insertion-order tiebreak for equal scores makes serialized ordering non-deterministic across runs unless the test controls the full input set and all scores are distinct. This is a precondition for snapshot testing that is not currently guaranteed.
2. Any dependency on `FluentAssertions` or `ApprovalTests` — blocked. In-house chain plumbing only.

No explorer proposals explicitly called for snapshots or FA. No items blocked by D041. Status: D041 complied with.

---

## SelectionReport Extension Methods — Placement Verdict

### BudgetUtilization(budget) → **Core analytics (`Wollax.Cupel` or `Wollax.Cupel.Analytics`)**

**Verdict: Core analytics, not testing-only.**

Rationale: `BudgetUtilization` is a production-useful metric — runtime logging, adaptive budget logic, health checks. Locking it in a test-only package means production callers cannot use it without adding a test package as a runtime dependency. The implementation is 3 lines. The API surface cost is trivial. Decision 4 already shaped this as an extension method; the only open question was the namespace. Placing it in core (or a thin `Wollax.Cupel.Analytics` package with zero new types) is correct.

The denominator ambiguity (MaxTokens vs. TargetTokens) must be resolved before implementation: **recommendation is `MaxTokens`** (the hard ceiling), documented explicitly. `TargetTokens` is implementation-internal to the Slice stage.

---

### KindDiversity() → **Core analytics (`Wollax.Cupel` or `Wollax.Cupel.Analytics`)**

**Verdict: Core analytics, not testing-only.**

Rationale: Identical to `BudgetUtilization` — production-useful for understanding output composition. An agent that adapts its pipeline configuration based on "did I get diverse context or did one kind dominate?" needs this in production code. 3-line implementation. No new types.

---

### ExcludedItemCount(Func<ContextItem, bool>) → **Cupel.Testing only**

**Verdict: Testing vocabulary, not core analytics.**

Rationale: In production code, `report.Excluded.Count(pred)` is the idiomatic LINQ expression. Wrapping this in an extension method adds no clarity for a production caller. The primary value is in an assertion chain where `HaveAtLeastNExclusionsMatching(pred, n)` reads naturally in a test. The predicate-lambda form is most ergonomic in test contexts. Adding it to core analytics adds API surface for a pattern that production code already expresses idiomatically.

**Note:** `HaveAtLeastNExclusionsMatching(Func<ContextItem, bool> predicate, int n)` is the testing-vocabulary form. `ExcludedItemCount(pred)` as a computation helper can be added to `Cupel.Testing` internally to support assertion chain implementation.

---

## Vocabulary candidates for S05

The following ≥10 candidates are the strongest set for S05 to specify. Patterns marked **Ready** require only the exact error message format and edge case coverage. Patterns marked **Needs work** have specific open questions that S05 must resolve before writing the method signature.

| # | Pattern Name | Status | What S05 must specify |
|---|-------------|--------|----------------------|
| 1 | `IncludeItemWithKind(ContextKind)` | ✅ Ready | Error message; `ContextKind.Any` guard (is this valid?) |
| 2 | `IncludeItemMatching(Func<IncludedItem, bool>)` | ✅ Ready | Confirm predicate type is `IncludedItem` (not `ContextItem`) |
| 3 | `ExcludeItemWithReason(ExclusionReason)` | ✅ Ready | Variant matching mechanics (pattern match, not string) |
| 4 | `ExcludeItemMatchingWithReason(Func<ContextItem, bool>, ExclusionReason)` | ✅ Ready | Error message covering partial-match case |
| 5 | `HaveAtLeastNExclusions(int)` | ✅ Ready | Trivial — just `Excluded.Count >= n` |
| 6 | `HaveAtLeastNExclusionsWithReason(ExclusionReason, int)` | ✅ Ready | Variant matching; same pattern as #3 |
| 7 | `PlaceItemAtEdge(Func<IncludedItem, bool>)` | ✅ Ready | "Edge" defined as index 0 or index `count-1`; error message with actual index |
| 8 | `HaveKindCoverageCount(int)` | ✅ Ready | Denominator is `Included.Select(...Kind).Distinct().Count()` |
| 9 | `HaveTokenUtilizationAbove(double, ContextBudget)` | ⚠️ Needs work | Denominator (MaxTokens vs TargetTokens); tolerance spec |
| 10 | `ExcludedItemsAreSortedByScoreDescending()` | ✅ Ready | Score-descending only; tiebreak not assertable from report alone |
| 11 | `HaveNoExclusions()` | ✅ Ready | Trivial; include first-excluded item in error message |
| 12 | `IncludeExactlyNItemsWithKind(ContextKind, int)` | ✅ Ready | N=0 is valid (same as NotIncludeKind) |
| 13 | `PlaceTopNScoredAtEdges(int)` | ⚠️ Needs work | Edge-position mapping (0, count-1, 1, count-2, ...); tie-score handling |
| 14 | `HaveIncludedTokensLessThanBudget(ContextBudget)` | ✅ Ready | Mark as conformance assertion, not policy assertion |
| 15 | `ExcludeItemWithBudgetDetails(Func<ContextItem, bool>, int expectedItemTokens, int expectedAvailable)` | ⚠️ Needs work | Exact-vs-range tolerance; dual-form API |

### Notes for S05

1. **Chain plumbing (~100 lines)**: The `Should()` entry point returns an `AssertionChain<SelectionReport>` object that holds the report and exposes the assertion methods. Chain methods return `this` for chaining. On failure, throw a dedicated `SelectionReportAssertionException` (not `InvalidOperationException`) with the structured error message.

2. **`IncludedItem` vs `ContextItem` as predicate targets**: Every method that takes a predicate should accept `IncludedItem` or `ExcludedItem` (not raw `ContextItem`) so callers can filter on `score` and `reason`, not just item fields. Methods that only care about content can provide a `ContextItem`-based overload as a convenience.

3. **Ordering assertions require Placer documentation**: `PlaceItemAtEdge`, `PlaceTopNScoredAtEdges`, and `PlaceItemsBefore` must be documented with "this assertion is only meaningful for a specific Placer — consult the Placer spec." The assertion library cannot know which Placer was used.

4. **Floating-point assertions**: `HaveTokenUtilizationAbove/Below/InRange` all require a tolerance parameter. Default tolerance of `1e-9` is appropriate. S05 must decide whether to expose this as an optional parameter or bake it in.

5. **Error messages must be fully specified**: Each method's error message should include: what was expected, what was found, and enough context (count, sample items, kinds) for the test author to diagnose without re-running under a debugger.

---

## Downstream Inputs for S05

### DI-1: Chain plumbing entry point
`SelectionReport.Should()` returns `SelectionReportAssertionChain`. The chain is the receiver for all assertion methods. S05 must define the chain type, its constructor, and its failure mechanism (`SelectionReportAssertionException` or similar).

### DI-2: Predicate type decision
All predicate-based methods must consistently accept `IncludedItem` / `ExcludedItem`, not `ContextItem`. Decide this once in S05's intro spec — do not leave it method-by-method.

### DI-3: Budget utilization denominator
`HaveTokenUtilizationAbove/Below/InRange` require a defined denominator. Recommendation: `budget.MaxTokens`. S05 must document this choice with rationale.

### DI-4: PlaceTopNScoredAtEdges specification
Replace the vague `PlaceHighestScoredAtEdges` with `PlaceTopNScoredAtEdges(n)`. S05 must define: (a) how top-N is determined (sort `Included` by score descending, take first N); (b) how edge positions are enumerated (0, count-1, 1, count-2, ... for N items); (c) what happens on score ties (two items with equal score, is either valid at the Nth edge position?).

### DI-5: Extension methods placement
`BudgetUtilization(budget)` and `KindDiversity()` belong in core analytics (not testing-only). S05 can call these internally in the assertion chain implementation. `ExcludedItemCount(pred)` is test-vocabulary only.
