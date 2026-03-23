# Testing Vocabulary — Explorer Mode

*Task: T02 | Explorer pass: uncensored generation*
*Date: 2026-03-21 | Brainstorm session: 2026-03-21T09-00*

---

## Context and Key Semantics

Before proposing patterns, anchor everything to the authoritative field semantics:

- `SelectionReport.Included` — final placed order (Placer determines order, NOT score order)
- `SelectionReport.Excluded` — sorted score-descending, stable by insertion-order on ties
- `SelectionReport.TotalTokensConsidered` — sum of tokens across ALL items (included + excluded)
- `SelectionReport.TotalCandidates` — `len(included) + len(excluded)` exactly
- Pinned items: score = `0.0`; items excluded before Score stage (e.g., NegativeTokens): score = `0.0`
- `ExcludedItem.Reason` is a data-carrying variant — `BudgetExceeded` carries `item_tokens` and `available_tokens`; `Deduplicated` carries `deduplicated_against`

---

## Category 1: Item Presence — What Is in `Included`

### P01 — IncludeItemWithKind
Assert that at least one item of a specified `ContextKind` appears in `Included`.
Example: "the report must have included at least one `ContextKind.SystemPrompt`."

### P02 — IncludeExactlyNItemsWithKind
Assert that exactly N items of a specified `ContextKind` appear in `Included`.
Example: "the report included exactly 2 `ContextKind.Message` items."

### P03 — IncludeItemMatching
Assert that at least one item in `Included` satisfies a caller-supplied predicate (e.g., content substring, token range, custom field).
Example: `report.Should().IncludeItemMatching(i => i.Content.Contains("system context"))`.

### P04 — IncludeItemsInOrder
Assert that two specified items appear in `Included` in a given relative order (item A before item B).
Since `Included` is final-placed-order, this tests Placer behavior directly.
Example: pinned item at position 0, scored item at position 1.

### P05 — IncludeAtLeastNItems
Assert that `Included.Count >= N`. Useful for "pipeline selected something meaningful."

### P06 — AllIncludedItemsMatchPredicate
Assert that every item in `Included` satisfies a predicate.
Example: "no included item has zero tokens."

---

## Category 2: Item Absence — What Is in `Excluded` with What Reason

### A01 — ExcludeItemWithReason
Assert that at least one item in `Excluded` has a specific `ExclusionReason` variant (e.g., `BudgetExceeded`, `Deduplicated`, `NegativeTokens`).

### A02 — ExcludeItemMatchingWithReason
Assert that a specific item (by predicate) in `Excluded` carries a specific reason.
Example: "the large ToolOutput item was excluded due to `BudgetExceeded`."

### A03 — ExcludeItemWithBudgetDetails
Assert a `BudgetExceeded` exclusion and verify the payload: `item_tokens` equals expected, `available_tokens` is within range.
The payload fields are what make `ExclusionReason` programmatically inspectable — assertions should validate them.

### A04 — ExcludeItemWithDeduplicationTarget
Assert that a `Deduplicated` exclusion names a specific `deduplicated_against` content reference.
Example: "the duplicate item's exclusion record names the original."

### A05 — HaveAtLeastNExclusions
Assert that `Excluded.Count >= N`. Useful for "budget was tight enough to drop items."

### A06 — HaveAtLeastNExclusionsWithReason
Assert that at least N items in `Excluded` have a specified reason variant.
Example: "at least 3 items were excluded for `BudgetExceeded`."

### A07 — ExcludedItemsAreSortedByScoreDescending
Assert that `Excluded` is ordered score-descending. This is a conformance assertion — useful in test utilities that validate the collector's sort guarantee before building assertions on top of it.

---

## Category 3: Kind Coverage — Which `ContextKind` Values Appear

### K01 — HaveKindInIncluded
Assert that a specific `ContextKind` value appears at least once in `Included`.
Simpler than P01 (P01 is the underlying primitive; K01 would be the readable alias for the pattern).

### K02 — HaveAllKindsInIncluded
Assert that every `ContextKind` in a provided list appears in `Included`.
Example: "both `Message` and `SystemPrompt` are represented."

### K03 — HaveKindCoverageCount
Assert that `Included` contains items from at least N distinct `ContextKind` values.
This tests diversity without enumerating exact kinds.

### K04 — NotIncludeKind
Assert that no item in `Included` has a specific `ContextKind`.
Example: "no `ToolOutput` was included under this tight budget."

---

## Category 4: Budget and Utilization Metrics

### B01 — HaveTokenUtilizationAbove
Assert that `(sum of tokens in Included) / budget.MaxTokens >= threshold`.
Example: `report.Should().HaveTokenUtilizationAbove(0.80)`.
Note: requires `budget` as a parameter since utilization requires a reference denominator.

### B02 — HaveTokenUtilizationBelow
Inverse of B01. Assert under-utilization — useful for "pipeline didn't pack too aggressively."

### B03 — HaveTokenUtilizationInRange
Assert that utilization is within `[low, high]`. Handles floating-point by requiring explicit tolerance.

### B04 — HaveIncludedTokensLessThanBudget
Assert that `sum(Included[i].tokens) <= budget.MaxTokens`. Sanity check — pipeline must not overpack.

### B05 — HaveTotalTokensConsideredEqualTo
Assert that `TotalTokensConsidered == expected`. Since this is a derived field, this is mostly a conformance check but useful when testing specific input sets.

---

## Category 5: Placement and Ordering

### O01 — PlaceItemAtFront
Assert that a specific item (by predicate or identity) is at position 0 in `Included`.
Important: `Included` is placed-order — "first" is a Placer decision, not a score decision.

### O02 — PlaceItemAtBack
Assert that a specific item is at the last position in `Included`.

### O03 — PlaceItemAtEdge
Assert that a specific item is at either the first or last position (for U-shaped placer assertions).
Variant: assert that a specific item is at a specific index N.

### O04 — PlaceItemsBefore
Assert that item A appears at an index strictly less than item B's index in `Included`.
(Relative ordering, not absolute position.)

### O05 — PlaceHighestScoredAtEdges
Assert that the top-N-scored items (by `IncludedItem.score`) appear at index 0 or index N-1.
⚠️ This is the slippery "high-scoring" pattern — requires exact definition in report. Generating it here as an idea.

### O06 — PlaceAllPinnedBeforeScored
Assert that all `InclusionReason.Pinned` items precede all `InclusionReason.Scored` items.
Depends on Placer contract for pinned ordering — must be specified.

---

## Category 6: Count-Based — Min/Max Exclusions and Inclusions

### C01 — HaveExactlyNCandidates
Assert that `TotalCandidates == N`. Validates that the correct input set was passed.

### C02 — HaveIncludedCount
Assert `Included.Count == N` or `>= N`.

### C03 — HaveExcludedCount
Assert `Excluded.Count == N` or `>= N`.

### C04 — HaveNoExclusions
Assert `Excluded.Count == 0`. The entire candidate set was selected.

### C05 — HaveNoInclusions
Assert `Included.Count == 0`. Edge case for empty result (e.g., zero-budget scenario).

---

## Category 7: Diversity

### D01 — HaveKindDiversityAtLeast
Assert that `Included` contains items from at least N distinct `ContextKind` values.
(Same as K03 above but named from the "diversity" perspective.)

### D02 — HaveAllCandidateKindsRepresented
Assert that every `ContextKind` present in the input candidate set also appears in `Included`.
A strong assertion — only valid when budget is generous enough to include everything.

### D03 — HaveDominantKind
Assert that a specific `ContextKind` accounts for more than 50% of included items (or more than a given percentage).
Example: "messages dominate the included set."

---

## SelectionReport Extension Methods — Re-evaluation

*From Decision 4 (March 15 brainstorm): `BudgetUtilization`, `KindDiversity`, `ExcludedItemCount` were reshaped from `ContextPressure` wrapper type into three extension methods. They were not assigned to any M002 slice. This section produces arguments for each possible placement.*

### The Three Methods

```csharp
report.BudgetUtilization(budget)                    // float: tokens_used / tokens_available
report.KindDiversity()                              // int: distinct ContextKind values in Included
report.ExcludedItemCount(Func<ContextItem, bool>)   // int: excluded items matching predicate
```

### Placement Option A: Cupel.Testing Vocabulary (S05)

**Arguments for:**
- All three are natural assertion building-blocks. `HaveTokenUtilizationAbove(0.85)` internally calls `BudgetUtilization(budget)`. Putting them in the testing package makes them discoverable exactly where test authors look.
- Test packages commonly expose extension methods that surface computed metrics for assertion purposes — this is a well-understood pattern.
- Keeps core `Wollax.Cupel` dependency-free of analytics.

**Arguments against:**
- These methods compute metrics from `SelectionReport` alone (or with `budget`). They are useful in production code too — e.g., logging utilization, alerting on low diversity. Locking them behind a `testOnly` package makes them unavailable in production.
- If a downstream consumer (Smelt, production agent) wants `BudgetUtilization` for logging, they'd have to add a test-only package as a runtime dependency, which is wrong.

### Placement Option B: Standalone Core Analytics (in `Wollax.Cupel` or `Wollax.Cupel.Analytics`)

**Arguments for:**
- These derivations have use cases beyond testing: runtime observability, adaptive budget logic, health checks.
- Extension methods on `SelectionReport` are additive; they don't change the core API surface.
- `Wollax.Cupel.Analytics` is a clean namespace that signals "derived metrics from pipeline results" without bloating the core namespace.

**Arguments against:**
- Adds to the shipped API surface of the core package — extension methods are still public API.
- `ExcludedItemCount(predicate)` with a caller-supplied predicate has near-zero utility outside of testing — in production, you'd query the list directly.

### Placement Option C: M003 (Deferred)

**Arguments for:**
- None of these methods are needed for the M002 brainstorm outputs or for S05 vocabulary design. Deferring to M003 prevents scope creep in M002.
- The value of `BudgetUtilization` and `KindDiversity` depends on how consumers actually use `SelectionReport` in production. Waiting for M003 means more real-world usage patterns will exist to inform the API shape.

**Arguments against:**
- Decision 4 explicitly shaped these as extension methods — they were already debated and sized. Deferring to M003 re-opens a closed decision without new information.
- `KindDiversity()` and `BudgetUtilization(budget)` are 3-5 line implementations. Deferring trivial additive code to a future milestone implies more risk than it actually carries.

### Synthesis (explorer notes, not final verdict):

The strongest split seems to be: `BudgetUtilization(budget)` and `KindDiversity()` → **core analytics** (they have production utility); `ExcludedItemCount(predicate)` → **Cupel.Testing** (it's a test pattern, not a production metric — production callers would just use `report.Excluded.Count(pred)` directly). But the challenger should stress-test this.

---

## Cross-Language Parity Note: C# vs. Rust

If the `Cupel.Testing` vocabulary is designed for C# `SelectionReport`, the Rust equivalent requires some adaptations:

1. **Fluent chain syntax**: Rust has no fluent `.Should()` pattern. The Rust equivalent would likely use a `SelectionReportExt` trait with assertion methods returning `Result<(), AssertionError>` or panicking via `assert!` macros. The method names can be the same; the chaining idiom differs.

2. **ContextKind comparison**: In C#, `ContextKind` is an enum with named values. Rust uses a Rust enum. Pattern names like `HaveKindInIncluded` apply directly — the argument type differs (`ContextKind` variant vs. Rust enum variant), not the semantic.

3. **Predicate-based assertions** (`IncludeItemMatching(predicate)`): In Rust, closures as predicates are natural. The ergonomics are similar.

4. **Ordering assertions**: `Included` is final-placed-order in both implementations. No change to semantics. However, Rust tests using the assertion library would reference the same Placer behavior — the spec precondition for `PlaceItemAtEdge` is identical.

5. **`BudgetUtilization(budget)`**: The `budget` parameter type differs between languages (C# `ContextBudget` vs. Rust `ContextBudget` struct). Implementation detail only — the semantic is the same.

6. **`ExcludedItem.Reason` variant matching**: In C#, this is a discriminated union via inheritance or `switch`. In Rust, it's an `enum` with `match`. The assertion `ExcludeItemWithReason(ExclusionReason.BudgetExceeded)` maps cleanly to matching on the Rust variant.

**Net assessment**: Vocabulary designed for C# applies to Rust with method-signature adaptations (predicate type, chain idiom) but zero semantic changes. The ordering guarantees, field semantics, and assertion intent are language-agnostic.
