# Count-Quota Angles — Explorer Mode

*Explorer: count-quota-explorer | Date: 2026-03-21T09:00*
*This file is raw explorer output. Ideas are uncensored and unfiltered.*
*Locked decisions are not filtered — they appear in report.md.*

---

## Framing: Decision 6 Context (quoted from highvalue-report.md)

> **Decision 6: Count-Based Quotas — Design Phase Only**
> The design phase produces a spec decision record covering:
> 1. Count quota algorithm for `QuotaSlice` / `GreedySlice` integration
> 2. Tag-based count quota semantics: if an item has tags `["critical", "urgent"]`, does it count toward `RequireAtLeast(tag: "critical")` and `RequireAtLeast(tag: "urgent")` simultaneously? (Non-exclusivity is the likely answer, but must be specified.)
> 3. Pinned item + count quota interaction: pinned items satisfy their own count quotas; if pinned items alone don't meet the minimum and budget can't supply enough selectable items, the outcome must be specified (throw? proceed? log warning?)
> 4. Run-time vs. build-time conflict detection: `RequireAtLeast(2).Cap(1)` is a build-time catch; `RequireAtLeast(2)` with only 1 candidate in the item set is a run-time condition. Both must be specified.
> 5. Count quota + `KnapsackSlice` interaction: mandatory item inclusion in knapsack is a constrained variant. Determine if this requires a separate implementation path or a clean generalization, before committing to either.

The five open questions provide the section structure below. A sixth section covers post-v1.2 exclusion diagnostic angles that were not possible to reason about before `SelectionReport` shipped in Rust.

---

## Open Question 1: Count Quota Algorithm — Integration with QuotaSlice/GreedySlice

The existing `DISTRIBUTE-BUDGET` pseudocode in `spec/src/slicers/quota.md` operates entirely on percentages and token masses. Count-based quotas express constraints in a fundamentally different unit (items, not tokens). How do these two worlds integrate?

### Idea 1A: Parallel CountQuotaSlice, No Integration with QuotaSlice

Introduce `CountQuotaSlice` as a sibling slicer, separate from `QuotaSlice`. `CountQuotaSlice` accepts `CountQuotaEntry { kind, require_count, cap_count }` and runs its own algorithm: pre-select the top `require_count` items per kind by score, pin them into the result, pass the remainder to the inner slicer with a reduced budget. `QuotaSlice` remains untouched — no cross-contamination between percentage and count semantics.

**Angle:** Avoids polluting `QuotaSlice`'s clean percentage model. Caller can compose `CountQuotaSlice(inner: QuotaSlice(...))` if they want both count and percentage guarantees.

### Idea 1B: Extend QuotaEntry with Optional Count Fields

Add `require_count: Option<u32>` and `cap_count: Option<u32>` to `QuotaEntry`. When count fields are present, the budget distribution phase converts them to a token reservation estimate using the average token size of candidates for that kind. This reservation is subtracted from `unassigned_budget` before the proportional distribution runs.

**Angle:** Reuses the existing `DISTRIBUTE-BUDGET` structure. But the average-token-size conversion is lossy — an item's actual token count can differ significantly from the average, meaning the reserved budget might not fit exactly `require_count` items.

### Idea 1C: Two-Phase Allocation — Count Satisfy First, Then Budget Distribute

Phase 1: Satisfy count constraints. Walk all kinds that have `require_count > 0`. For each such kind, greedily pick the top-scored items up to `require_count`. These items are "committed" — they must appear in the output regardless of budget. Mark their token cost as a reserved amount.

Phase 2: Run `DISTRIBUTE-BUDGET` on the remaining budget (`total_budget - reserved_token_cost`) for all remaining candidates, excluding the already-committed items. The inner slicer fills each kind's proportional allocation from non-committed items.

**Angle:** Count constraints are satisfied deterministically before any proportional reasoning. The reserved cost is exact (actual token counts of the committed items, not an estimate). Downside: if committed items for all kinds collectively exceed the total budget, we need a defined behavior (trim to fit? throw?).

### Idea 1D: Count Constraints as Pre-Sort Priority Injection

Do not modify the budget distribution algorithm at all. Instead, inject count constraints at the scoring/sort phase: items that are needed to satisfy `require_count` receive a score boost that guarantees they rank above all other items of their kind. `DISTRIBUTE-BUDGET` then runs as normal, and the inner slicer naturally includes the boosted items first.

**Angle:** No algorithm changes. But coupling count-quota semantics into the score domain conflates "how relevant is this item?" with "is this item required?" — philosophically muddy. The score of an item should not carry information about quota policy.

### Idea 1E: GreedySlice Extension — RequiredSet Parameter

Skip `QuotaSlice` entirely for count-quota. Extend `GreedySlice` with an optional `required_set: HashSet<ItemId>`. Items in `required_set` are included unconditionally (up to budget). Count constraints are resolved before calling `GreedySlice` by finding the top-scored items per kind that satisfy `require_count`, adding them to `required_set`. Budget remaining after required items are placed is used for the standard greedy pass.

**Angle:** Leverages `GreedySlice`'s existing greedy logic for non-required items. But `required_set` is an item-ID mechanism, which requires item IDs to be stable and unique — not currently a Cupel model requirement.

---

## Open Question 2: Tag Non-Exclusivity Semantics

An item can have multiple tags. `RequireCount(tag: "critical")` says "include at least N items tagged 'critical'". If an item is tagged both `["critical", "urgent"]`, which tags does it satisfy?

### Idea 2A: Count Toward All Matching Tags (Non-Exclusive)

A `["critical", "urgent"]` item increments the counter for both `RequireCount(tag: "critical")` and `RequireCount(tag: "urgent")`. This is the most natural interpretation — if an item qualifies as "critical", it is critical.

**Angle:** Maximally permissive. One item can satisfy multiple quotas simultaneously. Risk: a single highly-tagged item can "inflate" satisfaction of multiple quotas, possibly allowing a narrow slice of items to satisfy many requirements.

### Idea 2B: Count Toward Only the First Matching Tag (Exclusive, Ordered)

An item counts toward exactly one tag quota — the first tag in the item's tag list that has a quota configured. Other matching quotas are not incremented. Tag order on the item is the disambiguation rule.

**Angle:** Prevents double-counting. But "first tag" is fragile — if tag order is not guaranteed stable, the behavior is non-deterministic. And the concept of "first tag wins" is surprising to callers.

### Idea 2C: Count Toward Only the Highest-Priority Tag Quota (Priority-Ordered)

Define a priority ordering over tags at quota configuration time: `CountQuotaSet.priority(["critical", "urgent", "normal"])`. An item with multiple matching tags counts toward the highest-priority quota only.

**Angle:** Caller controls resolution explicitly. More expressive than "first tag on item" but adds configuration surface. This is closer to how CSS specificity works — most specific wins.

### Idea 2D: Count Toward All Matching Tags With a Dedup Guard

Same as 2A (non-exclusive), but each item is counted at most once per quota *constraint*. If `RequireCount(tag: "critical", n: 2)` is configured, a single `["critical", "urgent"]` item increments the "critical" satisfied count by 1 (not by its number of matching tags). The item is not double-counted within the same constraint.

**Angle:** This is the same as 2A in the single-constraint case. The dedup guard only matters if there are multiple independent quotas both keyed on "critical", which is not a meaningful configuration. Idea 2D reduces to Idea 2A in practice.

### Idea 2E: Tag Matching Uses a Separate "Primary Tag" Field

Add a `primary_tag: Option<String>` field to `ContextItem` (or as a metadata convention). Count-quota semantics apply to `primary_tag` only. Multi-tag items have a designated primary; count-quota ignores secondary tags. This sidesteps the non-exclusivity question entirely by reducing to a single-tag semantics.

**Angle:** Clean but introduces a new field concept. Metadata conventions (`ContextItem.metadata`) are the existing extensibility point — a `primary_tag` could be encoded as `metadata["cupel:primary_tag"]` instead of a new field, keeping the model clean.

---

## Open Question 3: Pinned Item + Count Quota Interaction

Pinned items are included unconditionally, before any slicer logic runs. If `RequireCount(kind: "SystemPrompt", n: 2)` is set and 2 SystemPrompt items are already pinned, the requirement is fully satisfied before the slicer even runs. But what if only 1 is pinned and only 1 additional candidate exists?

### Idea 3A: Pinned Items Satisfy Count Quotas Automatically

Pinned items of a given kind decrement the `require_count` for that kind. If `require_count` is 2 and 2 items are pinned, the slicer has nothing to require. If 0 items are pinned, the slicer must find all 2. Pinned items effectively pre-satisfy quotas.

**Angle:** Consistent with the intent of pinning — pinned items are unconditionally included, so they do satisfy any inclusion requirement. Callers who pin an item expect it to count toward all relevant constraints.

### Idea 3B: Pinned Items Are Orthogonal — Count Quotas Apply to Selectable Items Only

The count-quota system counts only selectable (non-pinned) items. Pinned items are outside the quota's scope. `RequireCount(kind: "SystemPrompt", n: 2)` means "include at least 2 selectable SystemPrompts." If only 1 selectable SystemPrompt exists, this is a run-time scarcity condition regardless of how many are pinned.

**Angle:** Clean separation of concerns. But counterintuitive: a caller who pins a SystemPrompt would not expect a `RequireCount("SystemPrompt", 1)` to fail if the pinned item is already included. This framing requires explicit documentation.

### Idea 3C: Pinned Items Satisfy Count Quotas, With Configurable Override

Default: pinned items contribute to count quota satisfaction (Idea 3A). Optional `count_pinned: false` flag on `CountQuotaEntry` for callers who want pinned items to be invisible to count constraints.

**Angle:** Flexible but adds configuration surface. The default (pinned items count) is the more intuitive path. The override handles the rare case where a caller wants "at least N selectable items of kind X regardless of pinned items."

### Idea 3D: Run-Time Scarcity When Pinned Items Partially Satisfy

When pinned items partially satisfy a `require_count` (e.g., 1 pinned, need 2 total), and the remaining candidates after budgeting can only supply 0 additional, this is a run-time scarcity event. The outcome options are:
- **Proceed degraded**: return the best-effort result (1 item instead of 2); log a warning-level trace event.
- **Include in SelectionReport**: emit a `CountRequireUnmet` exclusion diagnostic for the unsatisfied items (see Section 6).
- **Throw**: `CupelError::QuotaViolation` with precise context.

All three behaviors are worth specifying. The throw option is attractive for development/testing but fatal in production where candidate availability is dynamic.

---

## Open Question 4: Run-Time vs. Build-Time Conflict Detection

*Note: D040 establishes build-time vs. run-time as a hard distinction. This section explores the design space WITHIN that locked distinction — what constitutes a build-time error vs. a run-time condition, and how run-time conditions are surfaced.*

### Idea 4A: Build-Time Detection — Only Static Contradictions in the Policy Itself

Build-time errors (at `CountQuotaSlice` construction): any configuration where `require_count > cap_count` for the same kind. This is a static contradiction — no candidate set can simultaneously satisfy both constraints. `RequireCount(2).CapCount(1)` → throw immediately.

Run-time condition: `require_count > available_candidates_for_kind`. This depends on the item set passed at execution time — it is inherently dynamic and must be handled at execution time.

### Idea 4B: Extend Build-Time Detection to Cross-Kind Conflicts

If two kinds are configured such that their combined `require_count` minimums would exceed the total `cap` on their combined budget allocation, this might be a build-time detectable contradiction — but only if we also know the total budget. Since budget is supplied at execution time, cross-kind conflicts cannot be caught at construction time without accepting the budget as a constructor parameter.

**Angle:** Cross-kind conflicts require build-time budget knowledge, which complicates the API. This is likely a run-time condition for cross-kind scenarios, keeping the build-time surface clean.

### Idea 4C: Degraded Mode vs. Exception for Run-Time Scarcity

When `require_count > available` at run time:
- **Degraded mode**: Include all available items, emit a trace event of a new type (`InsufficientCandidates { kind, required: u32, available: u32 }`), continue. The `SelectionReport` reflects what actually happened.
- **Exception mode**: Throw `CupelError::QuotaViolation { kind, required: u32, available: u32 }`. Caller must handle.

A configurable `on_scarcity: ScarcityBehavior::Degrade | ScarcityBehavior::Throw` on `CountQuotaEntry` would let callers choose. Default `Degrade` aligns with the existing `QuotaSlice` philosophy of best-effort allocation.

### Idea 4D: Scarcity Tolerance as a Numeric Parameter

Instead of binary Degrade/Throw, allow `require_count` to carry a tolerance: `RequireCount(2, tolerance: 0.5)` means "require 2, but tolerate down to 1 (50%)." Below tolerance, throw. At or above tolerance, degrade gracefully.

**Angle:** Expressive but complex. Most callers want either "must have exactly N" or "try for N, proceed with whatever's available." A binary enum covers the 95% case without tolerance arithmetic.

---

## Open Question 5: Count Quota + KnapsackSlice Interaction

`KnapsackSlice` is a 0/1 knapsack DP solver. Items are either included or excluded. Adding count constraints means "at least N items of kind K must be included." This is a constrained variant of 0/1 knapsack — NP-hard in the general case, but tractable for small N.

### Idea 5A: Pre-Processing Path — Satisfy Count Constraints First, Mark Required Items Pinned

Before running `KnapsackSlice`: for each kind with `require_count > 0`, select the top-scored `require_count` items by score. Mark these as "pre-selected." Subtract their total token cost from the budget. Pass the remaining items + reduced budget to `KnapsackSlice`. Append pre-selected items to the result.

**Angle:** Simple and correct for the pre-selected items themselves. But the knapsack result is computed without knowledge of the pre-selected items — no opportunity for joint optimization across constrained and unconstrained items. This can produce sub-optimal results when pre-selected items and knapsack items compete for the same budget.

### Idea 5B: Constrained Knapsack — Inject Count Constraints into the DP Objective

Extend the knapsack DP state to track per-kind item counts. The DP state becomes `dp[tokens][kind_count_vector]` instead of just `dp[tokens]`. For each kind with `require_count > 0`, the DP must reach `count[kind] >= require_count` to be feasible.

**Angle:** Joint optimization over all items. But state space explodes: if there are K constrained kinds each with `require_count` up to R, the state space grows by `O(R^K)`. For K=3, R=5, this is 125× larger. `KnapsackSlice` already has an OOM guard (`CupelError::TableTooLarge`) — this would trigger it more aggressively.

### Idea 5C: Two-Stage Knapsack — Count-Constrained Pre-Pass + Greedy Remainder

Stage 1: Run a count-constrained knapsack only on items from kinds with `require_count > 0`, with a budget equal to the token cost of the smallest feasible set of required items. Find the minimum-cost feasible solution.

Stage 2: With the remaining budget, run standard `KnapsackSlice` on all remaining items (both constrained-kind surplus and unconstrained kinds).

**Angle:** More sophisticated than Idea 5A. Stage 1 finds the cheapest way to satisfy count constraints without burning excess budget. Stage 2 optimizes the remainder. Implementation complexity is higher — requires a separate constrained knapsack sub-solver.

### Idea 5D: Separate CountConstrainedKnapsack Slicer, Not KnapsackSlice Extension

Don't extend `KnapsackSlice` at all. Implement `CountConstrainedKnapsackSlice` as a new slicer that natively supports count constraints in its DP. Callers who need count constraints with knapsack use this slicer. `KnapsackSlice` stays untouched.

**Angle:** Clean separation. `KnapsackSlice` remains simple and its OOM guard is unaffected. `CountConstrainedKnapsackSlice` can choose its own state representation and OOM thresholds tuned for its use case.

### Idea 5E: Reject KnapsackSlice + Count Quota Combination Entirely (Build-Time Error)

At construction time, if a `CountQuotaSlice` wraps a `KnapsackSlice`, throw `CupelError::SlicerConfig("CountQuotaSlice is not compatible with KnapsackSlice inner slicer — use GreedySlice or implement CountConstrainedKnapsackSlice")`.

**Angle:** Explicitly in the spirit of `FindMinBudgetFor`'s `QuotaSlice` guard (D003 equivalent). Forces callers to make an explicit choice. Documents that the combination is unsupported rather than silently producing incorrect results.

---

## Open Question 6: Post-v1.2 — Exclusion Diagnostic Variants in SelectionReport

With `SelectionReport` now live in both .NET and Rust (post-v1.2), count-quota exclusions need representation in `Excluded` items. The current `ExclusionReason` variants in the Rust codebase cover token-budget and score-based exclusions. Count-quota adds two new exclusion classes.

### Idea 6A: CountCapExceeded { kind, cap, count } as a New ExclusionReason Variant

When an item is excluded because its kind has already reached `cap_count`, emit `ExclusionReason::CountCapExceeded { kind: ContextKind, cap: u32, count: u32 }`. `cap` is the configured maximum, `count` is how many were already included before this item was rejected.

**Angle:** Directly mirrors `BudgetExceeded` in structure. Tells the caller exactly why this item was rejected ("the cap for this kind was already reached") and how many were included.

### Idea 6B: CountRequireUnmet { kind, require, actual } as a New ExclusionReason Variant

For the `SelectionReport` produced by a diagnostic pass (after execution, looking back): emit `ExclusionReason::CountRequireUnmet { kind: ContextKind, require: u32, actual: u32 }` for the *missing* items — the slots that should have been filled but weren't due to candidate scarcity.

**Angle:** This is unusual — `ExclusionReason` currently applies to actual items in the `Excluded` list. A `CountRequireUnmet` without an associated item requires either a phantom item representation or a separate field on `SelectionReport`. The former is architecturally awkward.

### Idea 6C: CountRequireUnmet as a Pipeline-Level Trace Event, Not an Exclusion Reason

Instead of a new `ExclusionReason` variant, model run-time scarcity as a `TraceEvent` (like `PipelineStage` events). `TraceEvent::CountQuotaScarcity { kind, required, available }` is emitted when a kind's `require_count` cannot be satisfied. The `SelectionReport.Events` list carries this information.

**Angle:** Keeps `ExclusionReason` tied to actual items. Scarcity information appears in the event log, not in the excluded items list. Callers querying `Excluded` won't encounter phantom items.

### Idea 6D: New SelectionReport Field — QuotaViolations: Vec<QuotaViolation>

Add a dedicated `quota_violations: Vec<QuotaViolation>` field to `SelectionReport`. `QuotaViolation` carries `{ kind, constraint_type: RequirementNotMet | CapReached, required_or_cap: u32, actual: u32 }`. This is separate from `Excluded` and `Events`.

**Angle:** Cleanest consumer API — callers can check `report.quota_violations.is_empty()` as a single health signal. But adds a new top-level field to `SelectionReport`, which has API surface implications. Not backward-compatible if `SelectionReport` is a record/struct consumers destructure.

### Idea 6E: Sorting Implications for Excluded With Count-Quota Items

Current `SelectionReport.Excluded` is sorted score-descending with insertion-order tiebreak. If `CountCapExceeded` items are added, they are items that would have been included on score merit but were blocked by the count cap. These items should appear near the top of `Excluded` (high score, non-budget reason for exclusion). The existing sort order handles this correctly — no special treatment needed for `CountCapExceeded` items.

`CountRequireUnmet` phantom items (if Idea 6B is taken) have no natural score, which breaks the sort invariant. This is another reason to prefer Idea 6C (TraceEvent) over Idea 6B (phantom ExclusionReason).

### Idea 6F: Diagnostic Metadata on ExcludedItem — RequireUnmetSlot Flag

Rather than a phantom item, mark the `count_cap_exceeded` situation differently: add a `bool require_unmet_slot: bool` flag or `constraint_hit: ConstraintKind` discriminant to `ExcludedItem`. Items that were excluded by count cap carry `constraint_hit: ConstraintKind::CountCap`. Missing items (scarcity) are not represented in `Excluded` at all — instead a separate `deficit_report: Vec<KindDeficit>` field on `SelectionReport` captures kind-level shortfalls.

**Angle:** Middle ground between Idea 6A and 6D. Avoids phantom items while still providing per-item exclusion context for cap-exceeded cases.
