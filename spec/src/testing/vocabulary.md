# Cupel.Testing Vocabulary

## Overview

The Cupel.Testing vocabulary is a **language-agnostic set of named assertion patterns over `SelectionReport`**. Each pattern has a precise semantic specification: what it asserts, which fields of `SelectionReport` it reads, the exact comparison operator used, and the exact structure of the error message emitted on failure. The vocabulary is a contract document — it defines the observable surface that conforming implementations must provide, without prescribing internal structure.

### What Cupel.Testing is

- A named vocabulary of 13 assertion patterns, each fully specified with no undefined qualifiers
- A chain-based API: `SelectionReport.Should()` returns a `SelectionReportAssertionChain`; each assertion method returns `this` for fluent chaining; failures throw `SelectionReportAssertionException` with a structured message
- The prerequisite for the `Wollax.Cupel.Testing` NuGet package implemented in M003 (requirement R021)
- Language-agnostic: pattern semantics are defined at the field-access level; language bindings follow the field-access semantics of `SelectionReport`, `IncludedItem`, and `ExcludedItem` as specified in [SelectionReport](../diagnostics/selection-report.md)

### What Cupel.Testing is not

- **Not a FluentAssertions dependency** (D041): the chain plumbing is in-house (~100 lines); no dependency on `FluentAssertions`, `ApprovalTests`, or any third-party assertion framework
- **Not snapshot-based** (D041): snapshot assertions are explicitly deferred. `SelectionReport.excluded` is sorted score-descending with an insertion-order tiebreak that is not observable from the report itself. Snapshot outputs are therefore non-deterministic across test runs unless the caller controls the complete input set and all scores are distinct. This precondition is not currently guaranteed by the spec.
- **Not an implementation**: this chapter specifies the vocabulary; the implementation lives in M003
- **Not a runtime analytics module**: `BudgetUtilization(budget)` and `KindDiversity()` are extension methods on `SelectionReport` that belong in `Wollax.Cupel` core (D045), not in `Wollax.Cupel.Testing`. The testing vocabulary may call these methods internally but does not define them.

---

## Pre-decisions

The following four design choices are **locked** and apply uniformly across all 13 patterns. They are stated here once to prevent inconsistency in per-pattern specs.

### PD-1: Predicate type — `IncludedItem` / `ExcludedItem`, not raw `ContextItem`

All predicate-bearing methods accept **`IncludedItem`** (for Inclusion-group methods) or **`ExcludedItem`** (for Exclusion-group methods) as the predicate parameter type. The predicate receives the full pair — item, score, and reason — not just the raw `ContextItem`.

Rationale: predicates over `IncludedItem`/`ExcludedItem` are strictly more powerful than predicates over `ContextItem`; callers can filter on `score` or `reason` as well as item fields. Implementations MAY provide convenience overloads that accept a `ContextItem`-based predicate for callers who only need to inspect content or kind, but such overloads are not part of this vocabulary spec.

### PD-2: `BudgetUtilization` denominator — `budget.MaxTokens`

`BudgetUtilization` is defined as `sum(included[i].item.tokens) / budget.MaxTokens`.

The denominator is `budget.MaxTokens` (the hard token ceiling), **not** `budget.TargetTokens`.

Rationale: `MaxTokens` is the single authoritative ceiling callers control at construction time and the ceiling that matters for context-window safety. `TargetTokens` is a Slice-stage-internal soft target — it is not a public capacity metric exposed by `ContextBudget` and is not appropriate as a utilization denominator (D045, DI-3). Using `TargetTokens` as denominator would produce utilization values > 1.0 for reports where the pipeline filled the full max-token budget, which is misleading.

### PD-3: Floating-point threshold comparisons — exact operator, no epsilon

Floating-point threshold comparisons use the **exact** operator: `>=` for lower-bound thresholds (e.g. `HaveBudgetUtilizationAbove`), `<=` for upper-bound. No default epsilon is baked into any method (D064).

Rationale: a baked-in epsilon hides real threshold violations and couples the spec to a particular floating-point implementation. Test authoring is the caller's responsibility: callers must choose threshold values that avoid floating-point boundary cases (e.g. `0.799` instead of `0.8` when the computed ratio may drift by one ULP). The comparison operator is stated explicitly in each pattern spec.

### PD-4: Chain plumbing — `SelectionReport.Should()`, `SelectionReportAssertionChain`, `SelectionReportAssertionException`

- **Entry point**: `SelectionReport.Should()` — a method (or extension method) on `SelectionReport` that returns a `SelectionReportAssertionChain` wrapping the report
- **Chaining**: each assertion method on `SelectionReportAssertionChain` returns `this` so calls can be chained fluently
- **Failure**: on failure, each assertion method throws `SelectionReportAssertionException` (not `InvalidOperationException`, not `AssertionException`) with a structured message containing: assertion name, what was expected, and what was actually found
- **No side effects on success**: assertion methods that pass do not modify any state and return `this` unchanged

---

## Chain Plumbing

`SelectionReportAssertionChain` is the type returned by `SelectionReport.Should()`. It wraps a `SelectionReport` and exposes the 13 assertion methods listed in the Vocabulary section below.

### Type shape

```
SelectionReportAssertionChain:
    report: SelectionReport          // the wrapped report; read-only after construction

    // Entry point
    SelectionReport.Should() → SelectionReportAssertionChain

    // Each assertion method returns `this` for chaining
    // On failure: throws SelectionReportAssertionException

    // Inclusion group (3 methods)
    IncludeItemWithKind(kind: ContextKind) → SelectionReportAssertionChain
    IncludeItemMatching(predicate: Func<IncludedItem, bool>) → SelectionReportAssertionChain
    IncludeExactlyNItemsWithKind(kind: ContextKind, n: int) → SelectionReportAssertionChain

    // Exclusion group (4 methods)
    ExcludeItemWithReason(reason: ExclusionReason) → SelectionReportAssertionChain
    ExcludeItemMatchingWithReason(predicate: Func<ContextItem, bool>, reason: ExclusionReason) → SelectionReportAssertionChain
    ExcludeItemWithBudgetDetails(predicate: Func<ContextItem, bool>, expectedItemTokens: int, expectedAvailableTokens: int) → SelectionReportAssertionChain
    HaveNoExclusionsForKind(kind: ContextKind) → SelectionReportAssertionChain

    // Aggregate group (2 methods)
    HaveAtLeastNExclusions(n: int) → SelectionReportAssertionChain
    ExcludedItemsAreSortedByScoreDescending() → SelectionReportAssertionChain

    // Budget group (1 method)
    HaveBudgetUtilizationAbove(threshold: double, budget: ContextBudget) → SelectionReportAssertionChain

    // Coverage group (1 method)
    HaveKindCoverageCount(n: int) → SelectionReportAssertionChain

    // Ordering group (2 methods)
    PlaceItemAtEdge(predicate: Func<IncludedItem, bool>) → SelectionReportAssertionChain
    PlaceTopNScoredAtEdges(n: int) → SelectionReportAssertionChain
```

### Error message contract

`SelectionReportAssertionException` carries a structured message. Each pattern's spec (written in T02 and T03) defines the exact message template. All messages must include:

1. **Assertion name** — the method name as a label (e.g. `"IncludeItemWithKind"`)
2. **What was expected** — the caller's assertion parameter(s) stated explicitly
3. **What was actually found** — the actual field values from the report that caused the failure, with enough context (item counts, score values, kind lists) for the test author to diagnose without re-running under a debugger

No message should reference internal type names or implementation details (D032).

### Implementation cost

The chain plumbing is approximately 100 lines of code. The vocabulary spec does not prescribe the internal structure — only the entry point, method signatures, return type, and failure mechanism.

---

## Vocabulary

The Cupel.Testing vocabulary defines 13 named assertion patterns organized into five groups. Each pattern is fully specified in the sections below (T02 covers patterns 1–7; T03 covers patterns 8–13).

### Pattern summary table

| # | Group | Method | One-line description |
|---|-------|--------|----------------------|
| 1 | Inclusion | `IncludeItemWithKind(ContextKind)` | At least one included item has the given Kind |
| 2 | Inclusion | `IncludeItemMatching(Func<IncludedItem, bool>)` | At least one included item satisfies the predicate |
| 3 | Inclusion | `IncludeExactlyNItemsWithKind(ContextKind, int n)` | Exactly N included items have the given Kind (N=0 is valid) |
| 4 | Exclusion | `ExcludeItemWithReason(ExclusionReason)` | At least one excluded item carries the given ExclusionReason |
| 5 | Exclusion | `ExcludeItemMatchingWithReason(Func<ContextItem, bool>, ExclusionReason)` | At least one excluded item satisfies the predicate and carries the given reason |
| 6 | Exclusion | `ExcludeItemWithBudgetDetails(Func<ContextItem, bool>, int, int)` | An excluded item matching the predicate has BudgetExceeded with the exact token counts |
| 7 | Exclusion | `HaveNoExclusionsForKind(ContextKind)` | No excluded item has the given Kind |
| 8 | Aggregate | `HaveAtLeastNExclusions(int n)` | At least N items in the Excluded list |
| 9 | Aggregate | `ExcludedItemsAreSortedByScoreDescending()` | Excluded list is sorted score-descending (conformance assertion) |
| 10 | Budget | `HaveBudgetUtilizationAbove(double threshold, ContextBudget)` | sum(included tokens) / budget.MaxTokens >= threshold |
| 11 | Coverage | `HaveKindCoverageCount(int n)` | At least N distinct ContextKind values appear in the Included list |
| 12 | Ordering | `PlaceItemAtEdge(Func<IncludedItem, bool>)` | An included item matching the predicate is at position 0 or position count−1 |
| 13 | Ordering | `PlaceTopNScoredAtEdges(int n)` | The N highest-scored included items occupy the N outermost positions |

Per-pattern specifications are written in T02 (patterns 1–7) and T03 (patterns 8–13). Each specification includes: assertion semantics, predicate type, edge cases and tolerance, tie-breaking behavior, and error message format.

---

## Inclusion Group

The Inclusion group contains 3 patterns that assert over `SelectionReport.included`. All predicates in this group operate over **`IncludedItem`** (per PD-1), giving callers access to `item`, `score`, and `reason`.

### `IncludeItemWithKind(ContextKind kind)`

**Assertion semantics:** Asserts that `Included.Any(i => i.Item.Kind == kind)` — at least one item in the `included` list has `Kind` equal to `kind`.

**Predicate type:** No predicate parameter. The kind value is matched via direct field equality: `i.item.kind == kind`.

**`ContextKind.Any` sentinel:** `ContextKind.Any` is **not** a valid argument to this assertion. Pass a specific kind (e.g., `Message`, `SystemPrompt`, `ToolOutput`). Passing `ContextKind.Any` is implementation-defined behavior; a conforming implementation MAY throw `ArgumentException` or treat it as always-matching, but the vocabulary does not assign semantics to this case.

**Edge cases:**
- `Included` is empty → assertion fails with `count = 0`.
- `Included` contains items but none match `kind` → assertion fails with `count = 0` for the given kind.

**Tie-breaking:** Not applicable. This is a pure existence check with no ordering dependency.

**Error message format:**
```
IncludeItemWithKind({kind}) failed: Included contained 0 items with Kind={kind}. Included had {count} items with kinds: [{actualKinds}].
```
Where `{actualKinds}` is a comma-separated list of distinct `Kind` values present in `included` (e.g., `Message, ToolOutput`). If `included` is empty, `{actualKinds}` is an empty string and `{count}` is `0`.

---

### `IncludeItemMatching(Func<IncludedItem, bool> predicate)`

**Assertion semantics:** Asserts that `Included.Any(predicate)` — at least one item in the `included` list satisfies the caller-supplied predicate.

**Predicate type:** `IncludedItem` (per PD-1). The predicate receives the full `IncludedItem` pair (fields: `item`, `score`, `reason`), not a raw `ContextItem`. This allows callers to filter on score, inclusion reason, or item content/kind from a single predicate.

**Convenience overloads:** Implementations MAY provide additional overloads that accept a `Func<ContextItem, bool>` predicate for callers who only need to inspect item content or kind. Such overloads are implementation-defined and not part of this vocabulary spec.

**Edge cases:**
- `Included` is empty → assertion fails with `count = 0`, no items to show.
- `Included` is non-empty but the predicate returns `false` for all items → assertion fails and includes a summary of included items (up to 5) in the error message to assist diagnosis.

**Tie-breaking:** Not applicable. Existence check only.

**Error message format:**
```
IncludeItemMatching failed: no item in Included matched the predicate. Included had {count} items.
```
Implementations SHOULD append a summary of up to 5 `IncludedItem` entries from `included` (showing `kind`, `score`, and `reason` for each) to aid diagnosis. The exact format of this optional appendix is implementation-defined, but no additional required fields are added to the base message above.

---

### `IncludeExactlyNItemsWithKind(ContextKind kind, int n)`

**Assertion semantics:** Asserts that `Included.Count(i => i.Item.Kind == kind) == n` — the `included` list contains exactly `n` items with `Kind` equal to `kind`.

**Predicate type:** No predicate parameter. Count is computed by direct field equality: `i.item.kind == kind`.

**N=0 semantics:** `n = 0` is a valid, well-defined argument. It asserts that no included item has the given kind. This is distinct from `HaveNoExclusionsForKind` (pattern 7): `IncludeExactlyNItemsWithKind(kind, 0)` asserts the kind is absent from `included`; `HaveNoExclusionsForKind(kind)` asserts the kind is absent from `excluded`. An item of a given kind could be absent from both lists (never a candidate).

**Edge cases:**
- `Included` is empty and `n = 0` → assertion passes.
- `Included` is empty and `n > 0` → assertion fails with `actual = 0`.
- `Included` has matching items but count does not equal `n` → assertion fails showing both expected and actual counts.

**Tie-breaking:** Not applicable. Count comparison only.

**Error message format:**
```
IncludeExactlyNItemsWithKind({kind}, {n}) failed: expected {n} items with Kind={kind} in Included, but found {actual}. Included had {count} items total.
```
Where `{actual}` is the count of items in `included` with `Kind == kind`, and `{count}` is the total length of `included`.

---

## Exclusion Group

The Exclusion group contains 4 patterns that assert over `SelectionReport.excluded`. Predicates in this group operate over **`ContextItem`** or **`ExcludedItem`** depending on the method — see each pattern spec for the exact predicate type. Reason matching uses variant discriminant comparison, not string equality.

### `ExcludeItemWithReason(ExclusionReason reason)`

**Assertion semantics:** Asserts that `Excluded.Any(e => e.Reason is <reason variant>)` — at least one item in the `excluded` list carries an exclusion reason of the given variant.

**Reason matching:** Variant discriminant match. In Rust this is a pattern match on the enum variant (e.g., `matches!(e.reason, ExclusionReason::BudgetExceeded { .. })`). In .NET this is an equality comparison on the flat enum value (e.g., `e.Reason == ExclusionReason.BudgetExceeded`). This is **not** a string equality check on the reason name.

**Reserved variants:** All 8 `ExclusionReason` variants are valid arguments, including reserved variants (`ScoredTooLow`, `QuotaCapExceeded`, `QuotaRequireDisplaced`, `Filtered`). Reserved variants are valid arguments even if no built-in pipeline stage currently emits them — custom stage implementations may emit reserved variants, and test authors may assert against them.

**Edge cases:**
- `Excluded` is empty → assertion fails with `count = 0`.
- `Excluded` is non-empty but no item carries the given reason variant → assertion fails showing the actual reason variants present.

**Tie-breaking:** Not applicable. Existence check only.

**Error message format:**
```
ExcludeItemWithReason({reason}) failed: no excluded item had reason {reason}. Excluded had {count} items with reasons: [{reasonList}].
```
Where `{reasonList}` is a comma-separated list of distinct reason variant names present in `excluded` (e.g., `BudgetExceeded, Deduplicated`). If `excluded` is empty, `{reasonList}` is an empty string and `{count}` is `0`.

---

### `ExcludeItemMatchingWithReason(Func<ContextItem, bool> predicate, ExclusionReason reason)`

**Assertion semantics:** Asserts that `Excluded.Any(e => predicate(e.Item) && e.Reason is <reason variant>)` — at least one item in the `excluded` list satisfies both the content predicate and the reason discriminant match.

**Predicate type:** `ContextItem` (the predicate is over the raw item, not the full `ExcludedItem`). Callers use the predicate to filter by content or kind; the `reason` parameter provides the second filter. This split is deliberate: the predicate is the "which item?" filter, and the reason is the "why?" filter.

**Reason matching:** Variant discriminant match. Same rules as `ExcludeItemWithReason` (pattern 4).

**Partial-match count:** The error message distinguishes two failure modes: (a) the predicate matched no excluded items at all, and (b) the predicate matched one or more excluded items but none had the expected reason. This distinction is exposed via `{predicateMatchCount}` in the error message.

**Edge cases:**
- `Excluded` is empty → assertion fails; `predicateMatchCount = 0`.
- Predicate matches zero items in `excluded` → assertion fails; `predicateMatchCount = 0`.
- Predicate matches one or more items but none have the given reason → assertion fails; error shows partial-match count and the actual reasons of the matched items.

**Tie-breaking:** Not applicable. Existence check only.

**Error message format:**
```
ExcludeItemMatchingWithReason(reason={reason}) failed: predicate matched {predicateMatchCount} excluded item(s) but none had reason {reason}. Matched items had reasons: [{actualReasons}].
```
Where `{predicateMatchCount}` is the count of `excluded` items for which `predicate(e.item)` returned `true`, and `{actualReasons}` is a comma-separated list of the distinct reason variant names of those matched items. If `predicateMatchCount = 0`, `{actualReasons}` is an empty string.

---

### `ExcludeItemWithBudgetDetails(Func<ContextItem, bool> predicate, int expectedItemTokens, int expectedAvailableTokens)`

**Assertion semantics:** Asserts that there exists an excluded item `e` such that:
1. `predicate(e.item)` is true
2. `e.reason` is `BudgetExceeded`
3. `e.reason.item_tokens == expectedItemTokens` (exact integer equality)
4. `e.reason.available_tokens == expectedAvailableTokens` (exact integer equality)

**Predicate type:** `ContextItem`. The predicate identifies which item is being asserted about; the reason and token field checks are fixed by the assertion signature.

**Token equality:** Exact integer equality for both `item_tokens` and `available_tokens`. No tolerance. `available_tokens` is defined as `effective_target − sum(sliced_item.tokens)` at the moment of exclusion (D025).

**Reserved variant note:** This assertion is specific to the `BudgetExceeded` variant. It does not apply to other reason variants.

**Edge cases:**
- No excluded item matches the predicate → assertion fails; report which predicate-matching items exist (if any) with their actual `item_tokens` and `available_tokens`.
- Predicate matches an item but its reason is not `BudgetExceeded` → assertion fails; error shows actual reason.
- Predicate matches a `BudgetExceeded` item but token values differ → assertion fails; error shows both expected and actual token values.

**Tie-breaking:** Not applicable. The first predicate-matching `BudgetExceeded` item is used for error reporting if multiple candidates exist.

**Error message format:**
```
ExcludeItemWithBudgetDetails failed: expected BudgetExceeded with item_tokens={eIT}, available_tokens={eAT}, but found item_tokens={aIT}, available_tokens={aAT}.
```
Where `{eIT}` = `expectedItemTokens`, `{eAT}` = `expectedAvailableTokens`, `{aIT}` = actual `item_tokens` on the matched item, `{aAT}` = actual `available_tokens` on the matched item. If no predicate-matching `BudgetExceeded` item exists, replace the `but found` clause with: `but no matching item had reason BudgetExceeded` (implementation-defined extended form permitted).

> **Language note:** In .NET, `ExclusionReason` is a **flat enum** with no associated data. The `item_tokens` and `available_tokens` fields are not available on `ExcludedItem.Reason` — the .NET `ExclusionReason` enum carries only the variant discriminant (`BudgetExceeded`, `Deduplicated`, etc.) and no per-variant fields. .NET implementations of `Wollax.Cupel.Testing` MAY omit this assertion entirely or surface it differently — for example, as `HaveExcludedItemWithBudgetExceeded(Func<ContextItem, bool> predicate)` without the `expectedItemTokens` and `expectedAvailableTokens` parameters. If a .NET implementation omits the two token-detail parameters, the assertion degenerates to `ExcludeItemMatchingWithReason(predicate, ExclusionReason.BudgetExceeded)` (pattern 5). This difference is a consequence of the language-level representation choice and not a spec deviation.

---

### `HaveNoExclusionsForKind(ContextKind kind)`

**Assertion semantics:** Asserts that `Excluded.All(e => e.Item.Kind != kind)` — no item in the `excluded` list has `Kind` equal to `kind`.

**Predicate type:** No predicate parameter. The kind value is matched via direct field equality: `e.item.kind == kind`.

**Semantic distinction from `IncludeExactlyNItemsWithKind(kind, 0)`:** These two patterns test different lists:
- `HaveNoExclusionsForKind(kind)` asserts the kind is absent from `excluded`.
- `IncludeExactlyNItemsWithKind(kind, 0)` asserts the kind is absent from `included`.
An item of a given kind can be absent from both lists (it was never a candidate in the pipeline run). The two assertions are not equivalent and are not interchangeable.

**Edge cases:**
- `Excluded` is empty → assertion passes (vacuous truth: `All` over an empty set is true).
- `Excluded` contains items of the given kind → assertion fails; error shows count and details of the first matching item.

**Tie-breaking:** Not applicable. The `All` predicate is evaluated over the full `excluded` list; the first matching item is used in the error message for diagnosis.

**Error message format:**
```
HaveNoExclusionsForKind({kind}) failed: found {count} excluded item(s) with Kind={kind}. First: score={score}, reason={reason}.
```
Where `{count}` is the total count of excluded items with `Kind == kind`, and `{score}` and `{reason}` are the `score` and `reason` variant name of the first such item in the `excluded` list (i.e., the highest-scored excluded item of that kind, since `excluded` is sorted score-descending).

---

## Aggregate Counts Group

The Aggregate Counts group contains 2 patterns that assert over aggregate properties of `SelectionReport.excluded` — total count and ordering invariants.

### `HaveAtLeastNExclusions(int n)`

**Assertion semantics:** Asserts that `Excluded.Count >= n` — the `excluded` list contains at least `n` items.

**Predicate type:** No predicate parameter. The comparison is over the total count of the `excluded` list.

**N=0 semantics:** `n = 0` is a valid, well-defined argument. `HaveAtLeastNExclusions(0)` always passes unless the pipeline threw an exception (i.e., unless `SelectionReport` was never produced). There is no separate `HaveNoExclusionsRequired()` pattern — use `HaveAtLeastNExclusions(0)` instead. This is intentionally a no-op assertion useful for establishing "the pipeline ran without error" in minimal smoke tests.

**Edge cases:**
- `Excluded` is empty and `n = 0` → assertion passes.
- `Excluded` is empty and `n > 0` → assertion fails with `actual = 0`.
- `Excluded.Count >= n` → assertion passes.

**Tie-breaking:** Not applicable. Count comparison only.

**Error message format:**
```
HaveAtLeastNExclusions({n}) failed: expected at least {n} excluded items, but Excluded had {actual}.
```
Where `{actual}` is `Excluded.Count`.

---

### `ExcludedItemsAreSortedByScoreDescending()`

**Assertion semantics:** Asserts that for all `0 <= i < Excluded.Count - 1`: `Excluded[i].Score >= Excluded[i+1].Score`. The `excluded` list must be sorted in non-increasing score order.

> **Conformance assertion:** This assertion tests a behavioral invariant of a correct Cupel pipeline implementation. A pipeline that satisfies the specification (see [SelectionReport](../diagnostics/selection-report.md), D019) will always produce an `excluded` list in score-descending order. This assertion exists to detect non-conforming implementations and to validate hand-constructed test fixtures.

**Score comparison:** `>=` (non-increasing). Equal scores are permitted between adjacent items.

**Tiebreak caveat:** The specification (D019) guarantees that among items with equal scores, insertion order is preserved (the item added earlier appears earlier in `excluded`). However, **this insertion-order tiebreak is not assertable from the report alone** — the report does not expose an insertion index or candidate sequence number. This assertion therefore checks score-descending order only. Callers who need to verify tiebreak behavior must control the complete input set and ensure all scores are distinct.

**Edge cases:**
- `Excluded` is empty → assertion passes (vacuous truth: no adjacent pair to check).
- `Excluded` has exactly one item → assertion passes.
- Any adjacent pair violates non-increasing order → assertion fails at the first violating index.

**Error message format:**
```
ExcludedItemsAreSortedByScoreDescending failed: item at index {i+1} (score={si1}) is higher than item at index {i} (score={si}). Expected non-increasing scores.
```
Where `{i}` is the 0-based index of the first violating position (the item whose successor is higher-scored), `{si}` is `Excluded[i].Score`, and `{si1}` is `Excluded[i+1].Score`.

---

## Budget Group

The Budget group contains 1 pattern that asserts the token utilization of the `included` set relative to the caller-supplied budget.

### `HaveBudgetUtilizationAbove(double threshold, ContextBudget budget)`

**Assertion semantics:** Asserts that `sum(Included[i].Item.Tokens) / budget.MaxTokens >= threshold`.

**Denominator:** `budget.MaxTokens` — the hard token ceiling set at `ContextBudget` construction time. This is the single authoritative capacity ceiling for context-window safety.

`budget.TargetTokens` is **explicitly NOT the denominator**. `TargetTokens` is a Slice-stage-internal soft target; it is not a public capacity metric exposed by `ContextBudget`. Using `TargetTokens` as denominator would produce utilization values > 1.0 for reports where the pipeline filled the full max-token budget, which is misleading. (PD-2, D045, DI-3.)

**Comparison operator:** Exact `>=` with no epsilon (PD-3, D064). Floating-point edge cases are test authoring responsibility — callers must choose threshold values that avoid boundary drift.

**`budget.MaxTokens == 0`:** This is a pipeline error. `ContextBudget` validates `MaxTokens > 0` at construction time; a `ContextBudget` with `MaxTokens == 0` cannot be produced by a conforming implementation. This assertion does not need to handle division-by-zero.

**Edge cases:**
- `Included` is empty → `sum(Included[i].Item.Tokens) = 0`; utilization = `0.0`. Assertion fails unless `threshold <= 0.0`.
- `threshold = 0.0` → assertion passes whenever `Included` is non-empty (utilization > 0.0) and also when `Included` is empty (0.0 >= 0.0 is true). Valid usage.
- `threshold > 1.0` → valid argument; the assertion will fail unless the sum of included tokens exceeds `budget.MaxTokens` (which is an over-budget condition indicating a pipeline defect).

**Tie-breaking:** Not applicable. Arithmetic comparison only.

**Error message format:**
```
HaveBudgetUtilizationAbove({threshold}) failed: computed utilization was {actual:.6f} (includedTokens={includedTokens}, budget.MaxTokens={maxTokens}).
```
Where `{actual}` is the computed utilization value formatted to 6 decimal places, `{includedTokens}` is `sum(Included[i].Item.Tokens)`, and `{maxTokens}` is `budget.MaxTokens`.

---

## Kind Coverage Group

The Kind Coverage group contains 1 pattern that asserts over the diversity of `ContextKind` values in the `included` set.

### `HaveKindCoverageCount(int n)`

**Assertion semantics:** Asserts that `Included.Select(i => i.Item.Kind).Distinct().Count() >= n` — the `included` list contains at least `n` distinct `ContextKind` values.

**Predicate type:** No predicate parameter. The kind set is computed as `{ i.item.kind | i ∈ included }`.

**No ordering dependency:** This assertion is purely over the set of distinct kinds in `included`; it does not depend on list order or score values.

**N=0 semantics:** `n = 0` is a valid argument; the assertion always passes (an empty set has 0 distinct kinds, which satisfies `>= 0`). There is no distinct `HaveAtLeastOneKind()` pattern — use `HaveKindCoverageCount(1)`.

**Edge cases:**
- `Included` is empty → distinct kind count = 0. Assertion fails unless `n = 0`.
- All included items have the same kind → distinct count = 1.

**Tie-breaking:** Not applicable. Set cardinality comparison only.

**Error message format:**
```
HaveKindCoverageCount({n}) failed: expected at least {n} distinct ContextKind values in Included, but found {actual}: [{actualKinds}].
```
Where `{actual}` is the count of distinct kind values, and `{actualKinds}` is a comma-separated list of those distinct values (e.g., `Message, SystemPrompt`). If `Included` is empty, `{actual}` is `0` and `{actualKinds}` is an empty string.

---

## Conformance Assertions Group

Conformance assertions verify behavioral invariants that a correct Cupel pipeline implementation must always satisfy. They are distinct from functional assertions (which verify caller intent) — a conformance assertion failing indicates either a non-conforming pipeline or a hand-constructed test fixture with invalid ordering.

The `ExcludedItemsAreSortedByScoreDescending()` pattern (pattern 9 above) is the primary conformance assertion in this vocabulary. It is documented in the Aggregate Counts Group because its method name groups naturally with the other aggregate exclusion-count assertions.

---

## Ordering Group

The Ordering group contains 2 patterns that assert over the positions of items in `SelectionReport.included`. Both patterns carry a **Placer dependency caveat** — the meaning of positions in `included` is determined by the Placer used to produce the report.

> **Placer dependency caveat:** These assertions are only meaningful when the caller knows the Placer's ordering contract. For `UShapedPlacer`, position 0 holds the highest-scored item and position `count−1` holds the second-highest. For other Placers, consult the Placer spec. Do not use these assertions against output from an unknown or unspecified Placer.

### `PlaceItemAtEdge(Func<IncludedItem, bool> predicate)`

**Assertion semantics:** Asserts that `predicate(Included[0]) || predicate(Included[Included.Count - 1])` — at least one of the two edge positions (position 0 and position `count−1`) contains an item that satisfies the predicate. "Edge" means exactly position 0 (first) or position `Included.Count − 1` (last). No other positions are "edge" positions.

**Predicate type:** `IncludedItem` (per PD-1). The predicate receives the full `IncludedItem` pair (item, score, reason).

**"Edge" definition:** Position 0 (first) OR position `Included.Count − 1` (last). Nothing more. In particular: if `Included` has only 1 item, position 0 and position `count−1` are the same item; a predicate matching that single item satisfies the assertion.

**Tie-score clarification:** If multiple items share the same score as an edge item, the assertion passes only if the item satisfying the predicate physically occupies position 0 or position `count−1`. The assertion does **not** pass merely because the item has the same score as an edge item while being at a non-edge position. Position is the criterion, not score.

**Empty `Included`:** `Included` being empty is a degenerate case. An empty list has no edge positions; the assertion fails because no item can satisfy the predicate. The "item not found" error message variant applies.

**Edge cases:**
- `Included` is empty → assertion fails; "no item in Included matched the predicate" error variant.
- Predicate matches no item in `Included` → assertion fails; "no item in Included matched the predicate" error variant.
- Predicate matches one or more items, but none are at position 0 or `count−1` → assertion fails; "item matching predicate was at index {actual}" error variant, reporting the index of the first matching item.

> **Placer dependency caveat:** This assertion is only meaningful when the caller knows the Placer's ordering contract. For `UShapedPlacer`, position 0 holds the highest-scored item and position `count−1` holds the second-highest. For other Placers, consult the Placer spec. Do not use this assertion against output from an unknown or unspecified Placer.

**Error message format (item found at wrong index):**
```
PlaceItemAtEdge failed: item matching predicate was at index {actual} (not at edge). Edge positions: 0 and {last}. Included had {count} items.
```
Where `{actual}` is the 0-based index of the first item in `Included` satisfying the predicate, `{last}` is `Included.Count − 1`, and `{count}` is `Included.Count`.

**Error message format (item not in `Included`):**
```
PlaceItemAtEdge failed: no item in Included matched the predicate.
```

---

### `PlaceTopNScoredAtEdges(int n)`

**Assertion semantics:** Asserts that the `n` items with the highest `score` values in `Included` occupy the `n` outermost positions (edges) of the list. The edge position mapping for `n` items is: position 0, position `count−1`, position 1, position `count−2`, … (alternating from the two ends inward).

**Step-by-step evaluation:**
1. Sort `Included` by `score` descending to identify the top-`n` items.
2. Enumerate the first `n` edge positions: `0, count−1, 1, count−2, …`
3. Verify that each of the top-`n` items (by score) occupies one of those edge positions.

**Tie-score handling:** If multiple items share the score of the N-th top item (i.e., there is a tie at the boundary between the top-N set and the remainder), then any item with that tied score is a valid occupant of the N-th edge position. The assertion does not require a specific tied item to be at that position; it only requires that the N-th edge position is occupied by an item with a score ≥ the N-th top score.

**Predicate type:** No predicate parameter. Score comparison is over `IncludedItem.Score` values.

**`n = 0`:** Valid argument; assertion always passes (zero items to check, zero edge positions to verify).

**`n > Included.Count`:** Assertion fails — there are not enough items in `Included` to fill `n` edge positions. The error message should note the mismatch between `n` and `Included.Count`.

**Edge cases:**
- `Included` is empty and `n = 0` → assertion passes.
- `Included` is empty and `n > 0` → assertion fails; no items to place at edges.
- `n = Included.Count` → all items must be at "edge" positions, meaning the assertion checks that the full included list is sorted by edge-in order, not necessarily that it is sorted by score directly.

> **Placer dependency caveat:** This assertion is only meaningful when the caller knows the Placer's ordering contract. For `UShapedPlacer`, position 0 holds the highest-scored item and position `count−1` holds the second-highest, with subsequent positions filling inward. For other Placers, consult the Placer spec. Do not use this assertion against output from an unknown or unspecified Placer.

**Error message format:**
```
PlaceTopNScoredAtEdges({n}) failed: {failCount} of the top-{n} scored items were not at expected edge positions. Top-{n} items (by score): [{topItems}]. Expected edge positions: [{edgePositions}].
```
Where `{failCount}` is the count of top-`n` items not occupying an expected edge position, `{topItems}` is a list of the top-`n` items formatted as `(kind={kind}, score={score}, idx={actualIndex})`, and `{edgePositions}` is the ordered list of expected edge position indices (e.g., `0, 5, 1, 4` for `n=4` with `count=6`).

---

## Notes

### D041: Snapshot assertions deferred

Snapshot-based assertions over `SelectionReport` are explicitly deferred. The `excluded` list is sorted score-descending with an **insertion-order tiebreak** (D019): among items with equal scores, the item added earlier to the candidate set appears earlier in `excluded`. This tiebreak is an implementation detail — it is not observable from the `SelectionReport` alone because the report does not expose a candidate insertion index or sequence number.

Snapshot outputs are therefore **non-deterministic across test runs** unless the test author controls the complete input set AND all scores are distinct. This precondition is not currently guaranteed by the spec. Until `SelectionReport` ordering stability for ties is formally guaranteed and surfaced, snapshot assertions are not part of the Cupel.Testing vocabulary.

This deferral applies to any assertion of the form "the report serializes to exactly this JSON/string". Per-field assertions (the 13 named patterns) are unaffected.

### `TotalTokensConsidered` is not a utilization metric

`TotalTokensConsidered` (if surfaced as a `SelectionReport` field) is a **candidate-set volume metric**:

```
TotalTokensConsidered = sum(included[i].item.tokens) + sum(excluded[i].item.tokens)
```

It measures the total token volume of all items that entered the pipeline's selection decision — it is **not** a utilization metric. Using `TotalTokensConsidered` as a denominator for utilization assertions would produce values far below 1.0 in typical runs (where many candidates are excluded) and would misrepresent budget efficiency.

For utilization assertions, use `HaveBudgetUtilizationAbove(threshold, budget)` (pattern 10), which divides by `budget.MaxTokens` — the authoritative capacity ceiling (PD-2, D045).

### `SelectionReportAssertionException` should be a dedicated type

`SelectionReportAssertionException` must be a **dedicated exception type** — not `InvalidOperationException`, not `AssertionException` from any third-party framework, and not a raw `Exception`. (PD-4.)

Rationale: test frameworks (xUnit, NUnit, MSTest) recognize well-known assertion exception types and format them distinctly in test output. Using a dedicated type allows test runners to display Cupel assertion failures with the structured message they carry, rather than reporting them as unexpected system exceptions. It also allows callers to `catch (SelectionReportAssertionException)` in test utilities without accidentally catching unrelated `InvalidOperationException` instances.

The type must carry at minimum: the structured failure message (as `.Message`). Implementations MAY add structured properties (e.g., `AssertionName`, `Expected`, `Actual`) for programmatic inspection, but these are implementation-defined and not part of the vocabulary spec.
