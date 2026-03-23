# Count-Quota Angles — Challenger Report

*Explorer: count-quota-explorer | Challenger: count-quota-challenger | Date: 2026-03-21T09:00*
*Source: count-quota-ideas.md (2 passes)*

---

## Summary

28 proposals across 6 topic areas entered debate. The challenger's primary job here is to scope design questions precisely and discard implementation detail. Several ideas that appear to be design questions are actually implementation choices once the correct design frame is identified. The hardest open question — tag non-exclusivity — produces more sub-questions than answers, which is the expected and valid output for S01.

D040 is applied explicitly throughout. Any idea that re-opens the build-time vs. run-time debate is redirected with "D040 locked" to what S03 should specify *within* that constraint.

---

## Section 1: Count Quota Algorithm Integration

### Idea 1A: Parallel CountQuotaSlice, No Integration with QuotaSlice

**Challenge:** Is "separate slicer vs. extended slicer" a design question or an implementation question?

At this level, it is a design question — specifically: is count-quota a distinct semantic model or a generalization of percentage-quota? If it is distinct (different unit, different distribution logic), a separate slicer is correct. If it is a generalization (count is just a different constraint on the same distribution problem), integration makes more sense.

**Response:** Count-quota and percentage-quota operate in different units with different guarantee semantics. Percentage quotas guarantee a fraction of total budget; count quotas guarantee a number of items. These are not the same constraint and composing them (count floor, then percentage distribution on remainder) is a meaningful and composable design. A separate `CountQuotaSlice` preserves this distinction cleanly.

**Verdict: Accepted as design input.** The question for S03: "Should count-quota be a separate slicer (`CountQuotaSlice`) or a generalization of `QuotaSlice`?" The answer hinges on whether callers will commonly want both count and percentage constraints simultaneously on the same kind. S03 must specify this.

---

### Idea 1B: Extend QuotaEntry with Optional Count Fields

**Challenge:** The average-token-size conversion is identified as a loss function in the proposal itself. This is not a design flaw to debate — it is an implementation defect that makes the approach incorrect by construction. The reserved budget might not fit exactly `require_count` items.

**Response:** Acknowledged — this approach is architecturally unsound if token reservation accuracy matters. It might be acceptable for approximate use cases but cannot guarantee count-quota semantics.

**Verdict: Rejected as implementation detail with a fundamental flaw.** Adding optional count fields to `QuotaEntry` couples two conceptually distinct constraint systems into a single type. The token-estimate approach undermines the determinism that count quotas should provide. S03 should not consider this path.

---

### Idea 1C: Two-Phase Allocation — Count Satisfy First, Then Budget Distribute

**Challenge:** "If committed items collectively exceed the total budget, what happens?" is a real design question. This is not an edge case — it is the case where the caller has configured count requirements that are collectively unbounded relative to the budget.

**Response:** The two-phase structure itself is sound. The undefined behavior (total committed cost > budget) is a run-time condition, not a build-time contradiction, because token counts per item are not known at construction time. This is a D040 run-time case: "require_count satisfied, but token cost exceeds budget." S03 must specify this outcome.

**Verdict: Accepted as design input.** The two-phase structure (count-satisfy → budget-distribute) is the most concrete algorithm sketch for count-quota. S03 must specify: (a) what happens when committed token cost exceeds budget; (b) whether committed items are selected score-first or by some other ordering.

---

### Ideas 1D (Score Injection) and 1E (GreedySlice RequiredSet)

**Challenge (1D):** Score manipulation to encode quota semantics is philosophically wrong — it couples "item relevance" with "policy requirement." If an item's score encodes quota boost, the `SelectionReport` score field no longer means "relevance to the query." Any debug analysis of scores becomes unreliable.

**Challenge (1E):** Item-ID based `required_set` requires stable, unique item IDs — not a current Cupel model requirement. Introducing this as an API dependency would be a breaking change to the item model.

**Verdict: Both rejected as design inputs.** 1D pollutes the scoring semantic. 1E requires a model change that is not in scope. Neither should appear in S03's design space.

---

## Section 2: Tag Non-Exclusivity Semantics

**Challenger's overall position:** This is the single hardest design question in the count-quota space. Tag non-exclusivity is not an implementation question — it defines the fundamental semantic contract that callers will reason about and build policies around. Getting it wrong will require a breaking change.

### Idea 2A: Count Toward All Matching Tags (Non-Exclusive)

**Challenge:** "One item inflates multiple quotas" is not just a risk — it is the primary semantic. A caller who sets `RequireCount(tag: "critical", n: 2)` AND `RequireCount(tag: "urgent", n: 2)` would be satisfied by 2 items tagged `["critical", "urgent"]` even though only 2 unique items were selected. Is this the intended semantics?

**Response:** If "critical" and "urgent" represent overlapping but distinct concerns, then yes — an item that is both critical and urgent satisfies both requirements. The alternative (exclusive) semantics would require the caller to have 2 critical-only + 2 urgent-only items even when critical-urgent items are the best candidates.

**Verdict: Accepted as design input — specifically, as the most-likely correct semantics, but requiring explicit caller documentation.** S03 must decide whether non-exclusive is the default, mandatory, or configurable semantics. The concrete question: "If I have 2 items tagged `['critical', 'urgent']`, does `RequireCount('critical', 2).RequireCount('urgent', 2)` consider both requirements satisfied?"

---

### Idea 2B: Count Toward Only the First Matching Tag (Exclusive, Ordered)

**Challenge:** "First tag on the item" is non-deterministic if item construction does not guarantee tag order. Without a language-level guarantee that tags are stored in insertion order (which Rust `Vec<String>` does preserve but `HashSet<String>` does not), this semantics is fragile.

**Verdict: Rejected as primary semantics.** If tag order is not formally part of the Cupel spec for `ContextItem`, this approach is non-deterministic. S03 should note this constraint if exclusive semantics are desired: tag ordering must be specified before an exclusive-by-position rule can work.

---

### Idea 2C: Count Toward Highest-Priority Tag Quota (Priority-Ordered)

**Challenge:** This adds significant configuration complexity (a priority ordering over all tags) for a use case that callers can handle themselves by not assigning overlapping quotas to items they want to count exclusively. The "CSS specificity" analogy is appealing but adds API surface.

**Verdict: Reshaped.** S03 should consider whether the priority mechanism is necessary given that callers can model exclusive behavior through careful tag design. The design question is: "Is there a use case that cannot be served by non-exclusive semantics AND cannot be served by caller-side tag design?" If the answer is no, priority ordering is premature generalization.

---

### Idea 2D: Count Toward All With Dedup Guard

**Challenge:** As identified in the explorer, this reduces to Idea 2A in all practical cases. The dedup guard only matters for duplicate constraints keyed on the same tag, which is not a meaningful configuration.

**Verdict: Rejected — equivalent to 2A in practice.** S03 should not spend design effort on the dedup guard. If 2A is selected, the dedup guard is implicit.

---

### Idea 2E: Primary Tag Field / metadata Convention

**Challenge:** Introducing `primary_tag` as a new `ContextItem` field or metadata convention sidesteps the problem by reducing multi-tag items to single-tag items for quota purposes. This is a valid design choice, but it is an *avoidance* strategy, not a resolution. It punts the multi-tag problem to callers.

**Verdict: Reshaped.** The `metadata["cupel:primary_tag"]` convention is worth documenting as a caller-side workaround if non-exclusive semantics create problems. But it should not be the *recommended* path — count-quota semantics should work naturally with multi-tag items without requiring callers to pre-designate primary tags.

---

## Section 3: Pinned Item + Count Quota Interaction

### Idea 3A: Pinned Items Satisfy Count Quotas Automatically

**Challenge:** This is the intuitive semantics, but "automatically satisfy" must be precisely specified. When does the pinned item's contribution to quota count get computed? Before the slicer runs (pre-check) or during the committed-items phase of the two-phase algorithm (Idea 1C)?

**Response:** The computation is definitionally pre-phase: pinned items are included before any slicer logic. Their contribution to `require_count` is known before the slicer starts. The slicer only needs to satisfy `require_count - pinned_count_for_kind` additional items.

**Verdict: Accepted as design input with a precision requirement.** S03 must specify: "Pinned items of kind K decrement the `require_count` for K before slicer execution. The slicer is responsible for satisfying the residual count only." Edge case: if `pinned_count_for_kind > cap_count`, is this a build-time error, run-time error, or graceful handling? S03 must answer.

---

### Ideas 3B (Pinned items orthogonal) and 3C (Configurable override)

**Challenge (3B):** This produces counter-intuitive behavior that will surprise most callers. A pinned SystemPrompt item should, by any reasonable interpretation, count toward a `RequireCount("SystemPrompt", 1)`.

**Challenge (3C):** The `count_pinned: false` override adds API surface for a rare edge case. If 3A is the default, callers who need the orthogonal behavior can model it by subtracting pinned items from their `require_count` at policy construction time.

**Verdict (3B): Rejected — counter-intuitive semantics.** 
**Verdict (3C): Reshaped — caller-side workaround is sufficient.** S03 should not add `count_pinned` flag unless a concrete use case is identified.

---

### Idea 3D: Run-Time Scarcity Outcome Options (Degrade / Report / Throw)

**Challenge:** This is a real design question, not an implementation choice. The three outcome options (degrade, report, throw) have meaningfully different behavioral contracts. The right default depends on how count-quota is used in production:
- Agents with dynamic item sets (most common) need degraded-mode behavior — they cannot guarantee candidate availability.
- Policy validation flows (dev/test) benefit from throw-on-violation to catch misconfiguration early.

**Verdict: Accepted as design input.** S03 must specify: (a) the default scarcity behavior; (b) whether callers can override per-quota; (c) how scarcity is represented in `SelectionReport` when degraded mode is active (feeds into Section 6 below).

---

## Section 4: Run-Time vs. Build-Time Conflict Detection

### Idea 4A: Build-Time Detection — Static Contradictions Only

**D040 locked.** The build-time vs. run-time distinction is a hard requirement. The design question within D040: what exactly constitutes a "static contradiction" at construction time?

`RequireCount(2).CapCount(1)` is clearly build-time: `require > cap` for the same kind is algebraically contradictory regardless of the item set. This is confirmed.

**Verdict: Accepted.** S03 must enumerate which configurations are build-time checks. The minimal set: `require_count > cap_count` for same kind. S03 should consider whether zero-cap (`cap_count: 0`) with nonzero require is also a build-time error or just a degenerate run-time case.

---

### Idea 4B: Cross-Kind Conflicts at Build-Time

**D040 locked.** Cross-kind conflicts require budget knowledge, which is a run-time parameter. They cannot be caught at construction time without changing the API to accept budget at construction.

**Verdict: Rejected as build-time.** Cross-kind scarcity is run-time by definition (D040 scope). S03 should document this explicitly: "Cross-kind count-quota violations are run-time conditions. Only within-kind `require > cap` is detectable at build-time."

---

### Ideas 4C and 4D: Scarcity Behavior — Degrade/Throw vs. Tolerance

**Challenge (4D):** `tolerance: 0.5` is expressive but introduces a floating-point parameter where a binary enum is sufficient. The tolerance value adds a surface that callers must reason about without clear semantics: is 0.5 tolerance measured by count (round down) or by score (top 50% of require_count)?

**Verdict (4C): Accepted as design input.** The `ScarcityBehavior::Degrade | ScarcityBehavior::Throw` binary is the right abstraction level. 
**Verdict (4D): Rejected.** Numeric tolerance adds complexity not justified by the use case.

S03 must specify: (a) default scarcity behavior; (b) whether `ScarcityBehavior` is per-entry or per-slicer; (c) what "degraded" means precisely — specifically, does the `SelectionReport` reflect the shortfall and how.

---

## Section 5: Count Quota + KnapsackSlice Interaction

### Ideas 5A (Pre-processing path) and 5D (Separate slicer)

**Challenge (5A):** The sub-optimality of pre-processing is real but may be acceptable. The question is whether callers who use `KnapsackSlice` specifically for its global optimization properties will be satisfied with a pre-processing approach. If the pre-selected items are high-score items (selected by score first), the token waste from the pre-processing phase is bounded by the score-weighted item costs.

**Challenge (5D):** `CountConstrainedKnapsackSlice` as a separate slicer is the cleanest solution. The DP state extension (Idea 5B) is tractable for small K and R but the OOM risk is real. A separate slicer can choose its own state representation.

**Verdict (5A): Accepted as design input — with a precision requirement.** S03 must specify: does the pre-processing path select required items by score (greedy) or by token efficiency (smallest token cost that satisfies count)? These produce different results when budget is tight.

**Verdict (5D): Accepted as design input.** The "separate slicer" path is architecturally cleaner. S03 must decide: is the design goal "extend knapsack to support count constraints" or "provide a separate knapsack-like slicer for constrained use cases"?

---

### Idea 5B: Constrained Knapsack DP State Extension

**Challenge:** State space explosion with K constrained kinds is a real OOM risk. The existing `CupelError::TableTooLarge` guard in `KnapsackSlice` is specifically designed to prevent this class of problem. Adding count constraint dimensions to the DP state would require the OOM guard to account for the count-dimension multiplier.

**Verdict: Reshaped — implementation detail, not design question.** The DP state design is an implementation concern once S03 decides whether to pursue constrained knapsack at all. The design question for S03 is: "Is constrained-knapsack in scope for count-quota v1, or is pre-processing (5A) sufficient for the first release?" If constrained knapsack is in scope, state representation is an implementation detail.

---

### Idea 5C: Two-Stage Knapsack

**Challenge:** This is a creative approach but adds significant implementation complexity. The "minimum-cost feasible set" in Stage 1 is itself a constrained optimization problem. This is not simpler than 5D (separate slicer) in practice.

**Verdict: Rejected as overly complex.** S03 should choose between 5A (pre-processing, simpler), 5D (separate slicer, cleaner), or 5E (explicit rejection, safest). Two-stage knapsack does not offer enough over 5D to justify its complexity.

---

### Idea 5E: Build-Time Rejection of KnapsackSlice + CountQuotaSlice Combination

**Challenge:** This is the most conservative path. It is in the spirit of `FindMinBudgetFor`'s existing `QuotaSlice` guard. The question is whether the combination is truly unsupportable or merely hard.

**Verdict: Accepted as design input — specifically as the *safe default* that S03 should evaluate first.** If S03 cannot reach consensus on 5A or 5D within the slice's scope, 5E provides a well-specified fallback: document the limitation explicitly and provide the build-time guard. Users who need the combination can use `CountQuotaSlice(inner: GreedySlice(...))` until a constrained knapsack slicer ships.

**The hardest question for S03 (elaborated in Downstream Inputs below).**

---

## Section 6: Post-v1.2 Exclusion Diagnostic Variants

### Ideas 6A and 6B: ExclusionReason Variants

**Challenge (6A):** `CountCapExceeded { kind, cap, count }` is well-specified and fits naturally into the existing `Excluded` list. Items excluded by count cap are real items with scores — they belong in `Excluded`. This is accepted.

**Challenge (6B):** `CountRequireUnmet` as a phantom item in `Excluded` is architecturally wrong. `Excluded` represents actual items that were considered and rejected. A "missing slot" is not an item. It has no score, no content, no token count. Putting a phantom in `Excluded` breaks the invariant that `SelectionReport.Excluded` items are real candidates. The explorer's own analysis (Idea 6E) confirms this.

**Verdict (6A): Accepted.** `CountCapExceeded { kind, cap, count }` is the correct model for items excluded by count cap. 
**Verdict (6B): Rejected.** Phantom items in `Excluded` break the type invariant.

---

### Idea 6C: CountRequireUnmet as a TraceEvent

**Challenge:** `TraceEvent::CountQuotaScarcity { kind, required, available }` is architecturally sound. It keeps `Excluded` as a list of real items and puts scarcity information in the event log where pipeline-level observations belong.

**Verdict: Accepted as design input.** S03 must decide: is a `TraceEvent` sufficient for scarcity notification, or does the caller need a more prominent signal (Idea 6D: dedicated `SelectionReport` field)?

---

### Idea 6D: New SelectionReport Field — QuotaViolations

**Challenge:** Adding `quota_violations` to `SelectionReport` has API surface implications in both .NET and Rust. In .NET, `SelectionReport` is a record — adding a field is backward-compatible in most use cases. In Rust, it is a struct — adding a field is backward-compatible at the source level if the struct is non-exhaustive (`#[non_exhaustive]`).

Is `SelectionReport` marked `#[non_exhaustive]` in Rust? This is a concrete spec question. If not, adding fields is a breaking change for users who destructure the struct.

**Verdict: Accepted as design input — with a backward-compatibility precondition.** S03 must verify whether `SelectionReport` can be extended in both languages without breaking changes before recommending Idea 6D. If it can, a dedicated `quota_violations` field is the cleanest consumer API.

---

### Ideas 6E (Sort order) and 6F (ExcludedItem flags)

**Challenge (6E):** The existing score-descending sort order handles `CountCapExceeded` items correctly — they are real items with scores and will naturally appear near the top of `Excluded`. No special treatment needed.

**Challenge (6F):** Adding `constraint_hit: ConstraintKind` to `ExcludedItem` provides richer per-item diagnostics but is a breaking API change if `ExcludedItem` is not `#[non_exhaustive]`. The `deficit_report` sub-field is a variation of Idea 6D.

**Verdict (6E): Accepted — no additional work for sort order.** 
**Verdict (6F): Reshaped into Idea 6D.** If a scarcity report field is added to `SelectionReport`, it subsumes the `deficit_report` concept from 6F.

---

## Downstream inputs for S03

The following are the ≥5 most important, precisely-framed design questions that S03 must answer. These are not implementation tasks — they are design decisions that will constrain the count-quota spec chapter.

### DI-1: Algorithm Architecture — Separate Slicer vs. Generalization

**Question:** Should count-quota be implemented as a standalone `CountQuotaSlice` (wrapping any inner slicer), or as an extension to `QuotaSlice` that unifies count and percentage constraints in a single type?

**Relevant proposals:** 1A (separate slicer), 1B (rejected — token estimation flaw), 1C (two-phase, applicable to either architecture).

**Framing for S03:** The decision hinges on whether callers commonly need both count AND percentage constraints simultaneously on the same kind. If the answer is yes, a unified type or composable wrapper is required. If no, separate slicers are cleaner. S03 should survey M002's downstream slices and the spec's example use cases to answer this.

**Not answered here:** This is a design question that requires knowing the target use cases. S03 owns the answer.

---

### DI-2: Tag Non-Exclusivity Semantics — The Definitive Contract

**Question:** When an item has tags `["critical", "urgent"]` and both `RequireCount(tag: "critical", n: 2)` and `RequireCount(tag: "urgent", n: 2)` are configured, does including this item satisfy both requirements partially (count: 1 toward each) or only one?

**Relevant proposals:** 2A (non-exclusive, recommended), 2B (rejected), 2C (reshaped), 2E (caller-side workaround).

**Framing for S03:** Non-exclusive semantics (2A) is the most intuitive but can allow a small set of highly-tagged items to satisfy many requirements simultaneously. S03 must decide:
1. Is non-exclusive the mandatory semantics, or is it the default with an exclusive override?
2. If non-exclusive, must the spec document the "2 critical+urgent items satisfy both RequireCount(2)" case explicitly with a worked example?
3. Is there a production use case where the inflated-satisfaction risk justifies adding priority-based resolution (2C)?

**Challenger's assessment:** Non-exclusive is likely correct. The risk of over-satisfaction is real but acceptable if documented. Priority-based resolution (2C) is premature unless a concrete use case is identified in S03.

**★ This is the question the challenger considers hardest in the count-quota design space.** The reason: it defines the fundamental semantic contract that cannot be changed once count-quota ships without a breaking version bump. The algorithm (DI-1) and integration path (DI-4) can be adjusted with minor behavioral changes; the tag counting semantics is a public contract. Getting it wrong means a v2.0 breaking change. S03 should spend disproportionate time on this question.

---

### DI-3: Run-Time Scarcity Behavior and SelectionReport Representation

**Question:** When `require_count > available_candidates` at execution time (after pinned items are accounted for), what is the default behavior, and how is the shortfall reflected in `SelectionReport`?

**Relevant proposals:** 3D (degrade/report/throw options), 4C (ScarcityBehavior enum), 6C (TraceEvent), 6D (QuotaViolations field), D040 (locked: run-time scarcity is behavioral, not an exception by default).

**Framing for S03:**
1. Default behavior: Degrade (include all available, emit a diagnostic) or Throw? D040 does not prescribe the default — it distinguishes the *class* of condition, not the response.
2. Diagnostic mechanism: `TraceEvent::CountQuotaScarcity` (event log) or `SelectionReport.quota_violations` (dedicated field)? The choice depends on whether `SelectionReport` can be extended backward-compatibly in both languages.
3. Per-entry configurability: Can callers override scarcity behavior per `CountQuotaEntry`? Or is it slicer-level?

**Precondition for S03:** Verify whether `SelectionReport` is `#[non_exhaustive]` in Rust and whether the .NET record supports field addition without breaking callers.

---

### DI-4: KnapsackSlice Compatibility Path

**Question:** Is count-quota + KnapsackSlice a supported combination in v1? If yes, which algorithm path (pre-processing 5A, separate constrained-knapsack slicer 5D)? If no, should there be a build-time guard (5E)?

**Relevant proposals:** 5A (pre-processing), 5D (separate slicer), 5E (explicit rejection as safe default).

**Framing for S03:**
1. Conservative path (5E): Reject the combination with a build-time error. Document that `CountQuotaSlice` requires `GreedySlice` as the inner slicer for v1. Ship `CountConstrainedKnapsackSlice` in a subsequent milestone if demand is established.
2. Pre-processing path (5A): Accept the sub-optimality in exchange for simplicity. Specify score-first selection for required items. Document the sub-optimality explicitly.
3. Separate slicer path (5D): Higher quality outcome but higher implementation cost. Appropriate if S03 identifies concrete use cases that require joint optimization.

**Not answered here:** S03 must weigh the implementation cost against the expected demand for count-quota + knapsack combinations. The challenger recommends starting with 5E and promoting to 5A in the same milestone if the implementation proves straightforward.

---

### DI-5: Pinned Item + Overshoot Edge Case

**Question:** If pinned items for kind K already satisfy or exceed `cap_count(K)`, is this a build-time error, a run-time warning, or graceful continuation?

**Relevant proposals:** 3A (pinned items decrement require_count), 3C (rejected).

**Framing for S03:** The case where `pinned_count_for_kind > cap_count` is degenerate — the caller has pinned more items of kind K than the cap allows for selectable items. Options:
1. Build-time error: Requires the count-quota policy to know the pinned item set at construction time. Not feasible unless pinned items are part of the `CountQuotaSlice` configuration.
2. Run-time warning (trace event): Pin items always win; emit a `CountCapExceededByPinnedItems` trace event. The cap applies only to selectable items after pinned items are accounted for.
3. No special treatment: Pinned items are outside the slicer's scope. The cap applies to items selected by the slicer only. Pinned items beyond cap are not the slicer's responsibility.

**Not answered here:** S03 must define the scope of count-quota caps — do they apply to the total included count (pinned + selected) or only to the slicer-selected count?

---

### DI-6 (Bonus): Exclusion Reason API Surface — BackwardCompatibility Precondition

**Question:** Can `SelectionReport` be extended with new fields (`quota_violations`) and `ExclusionReason` be extended with new variants (`CountCapExceeded`) without breaking existing callers in both .NET and Rust?

**Relevant proposals:** 6A, 6C, 6D.

**Framing for S03:** This is a prerequisite for DI-3's diagnostic mechanism choice. S03 must audit:
- Rust: Is `SelectionReport` struct marked `#[non_exhaustive]`? Is `ExclusionReason` enum marked `#[non_exhaustive]`? If not, adding variants is a breaking change for callers that `match` exhaustively.
- .NET: Does `SelectionReport` record have any consumers that pattern-match exhaustively (e.g., switch expressions)? Adding fields is non-breaking for record types in most consumption patterns.

If the answer is "extension is safe," 6A + 6D is the cleanest design. If not, 6C (TraceEvent only) is the backward-compatible fallback.

---

## Summary of Verdicts

| Proposal | Verdict | Disposition |
|----------|---------|-------------|
| 1A — Separate CountQuotaSlice | Accepted | Design input DI-1 |
| 1B — Extend QuotaEntry with count fields | Rejected | Token estimation is architecturally unsound |
| 1C — Two-phase (count-satisfy first) | Accepted | Algorithm basis for DI-1 and DI-3 |
| 1D — Score injection | Rejected | Pollutes scoring semantic |
| 1E — GreedySlice required_set | Rejected | Requires stable item IDs (model change not in scope) |
| 2A — Non-exclusive tag counting | Accepted | DI-2 recommendation |
| 2B — First-matching-tag exclusive | Rejected | Non-deterministic without tag-order spec |
| 2C — Priority-ordered resolution | Reshaped | Premature; revisit if use case identified in S03 |
| 2D — Non-exclusive with dedup guard | Rejected | Reduces to 2A in practice |
| 2E — Primary tag field/convention | Reshaped | Caller-side workaround, document in spec |
| 3A — Pinned items satisfy quotas automatically | Accepted | DI-5 premise |
| 3B — Pinned items orthogonal | Rejected | Counter-intuitive |
| 3C — Configurable override | Reshaped | Caller-side workaround is sufficient |
| 3D — Scarcity outcome options | Accepted | DI-3 framing |
| 4A — Build-time detection: static only | Accepted | DI-3 framing (confirmed) |
| 4B — Cross-kind build-time detection | Rejected (D040 locked) | Cross-kind scarcity is run-time by definition |
| 4C — ScarcityBehavior binary enum | Accepted | DI-3 implementation recommendation |
| 4D — Numeric tolerance | Rejected | Unnecessary complexity |
| 5A — Pre-processing path | Accepted | DI-4 option |
| 5B — Constrained knapsack DP | Reshaped | Implementation detail once DI-4 is decided |
| 5C — Two-stage knapsack | Rejected | Overly complex vs. 5D |
| 5D — Separate CountConstrainedKnapsack slicer | Accepted | DI-4 option |
| 5E — Explicit rejection of Knapsack combination | Accepted | DI-4 safe default |
| 6A — CountCapExceeded ExclusionReason variant | Accepted | DI-6 input (ExclusionReason extension) |
| 6B — CountRequireUnmet as phantom ExclusionReason | Rejected | Breaks Excluded list invariant |
| 6C — CountQuotaScarcity as TraceEvent | Accepted | DI-3/DI-6 input (backward-compatible fallback) |
| 6D — quota_violations field on SelectionReport | Accepted (conditional) | DI-6 input (requires backward-compat audit) |
| 6E — Sort order for CountCapExceeded items | Accepted | No additional work needed; existing sort correct |
| 6F — ExcludedItem constraint_hit flag | Reshaped | Subsumed by 6D |
