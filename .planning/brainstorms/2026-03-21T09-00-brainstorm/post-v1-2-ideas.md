# Post-v1.2 Ideas — Explorer Mode

**Session:** 2026-03-21T09-00
**Pair:** 4 of 4 (post-v1-2-ideas)
**Mode:** Explorer (uncensored, generative)

---

## Section A — Deferred Items Re-evaluation

### A1: Fork Diagnostic (`ForkDiagnosticReport`)

**What changed since March 15:**
`dry_run` is now concrete in both .NET (`SelectionReport DryRun(IEnumerable<ContextItem>, ContextBudget)`) and Rust (`fn dry_run(items: &[ContextItem], budget: ContextBudget) -> SelectionReport`). The March 15 radical-report described fork diagnostic as a developer-time tool for policy sensitivity analysis — running N policy variants and comparing `SelectionReport` outputs. That orchestration pattern is now trivial to implement as caller-side code.

**Fresh angles:**

- The simplest fork diagnostic is already possible today: callers can call `dry_run` N times with different `ISlicingPolicy` configurations and compare the resulting `SelectionReport` objects. No new API is required in Cupel core.
- The remaining question is: does Cupel provide a convenience wrapper `ForkDiagnosticReport` that takes a list of `(string label, ISlicingPolicy policy)` pairs plus an item set, runs `dry_run` for each, and returns a structured comparison? Or is this Smelt's territory?
- The comparison report could include: items included in all variants, items excluded in all variants, items that "swing" between variants, per-variant budget utilization.
- Implementation path for M003: define a `PolicySensitivityReport` type (not `ForkDiagnosticReport` — the word "fork" implies divergent execution, not policy comparison). The type wraps a `Dictionary<string, SelectionReport>` and adds comparison helpers. The `RunPolicySensitivity(items, [(label, policy)])` entry point calls `dry_run` internally. Total implementation: ~80 lines.
- M003 feasibility: high. `dry_run` exists, `SelectionReport` comparison is structural (equality on item sets), no new pipeline stages. The only spec work is defining the `PolicySensitivityReport` type and its comparison semantics.
- Scope risk: if `PolicySensitivityReport` includes aggregated statistics (average utilization across variants), it could expand into a benchmarking tool. Keep the v1 scope to "which items swing" and "per-variant SelectionReport."

**Assessment:** This is clearly M003 scope. `dry_run` in both languages was the missing prerequisite. The implementation is thin orchestration — 60–100 lines per language.

---

### A2: `SelectionReport` Extension Methods (`BudgetUtilization`, `KindDiversity`, `ExcludedItemCount`)

**What changed since March 15:**
T02 produced a definitive placement verdict. No new implementation signal is needed — this is a re-statement and confirmation.

**T02 verdict (echoed):**
- `BudgetUtilization(budget)` → belongs in **core analytics** (`Wollax.Cupel.Analytics` in .NET, inline in `cupel` crate in Rust). Production callers logging to telemetry or making adaptive budget decisions need this without importing `Cupel.Testing`.
- `KindDiversity()` → same. Returns `double` (distinct kinds / total included). Useful in production dashboards.
- `ExcludedItemCount(predicate)` → **test vocabulary only** (`Wollax.Cupel.Testing`). Production callers use `report.Excluded.Count(pred)` directly. A wrapper has no production utility.

**Angles on whether to put them in `Wollax.Cupel` core vs. `Wollax.Cupel.Analytics`:**
- If analytics are in a separate package, callers who want `BudgetUtilization` must take an extra dependency. Is the dependency cost worth the separation?
- Counter-argument: `SelectionReport` already lives in `Wollax.Cupel`. Extension methods on `SelectionReport` are the natural extension point. Putting them in `Wollax.Cupel` directly means zero extra import for callers. The "analytics" framing is useful as a documentation concept, not necessarily as a package boundary.
- Strongest argument for a separate analytics package: OTel integration (`Wollax.Cupel.OpenTelemetry`) needs `BudgetUtilization` to emit utilization metrics. If it's in core, core takes no new dependency. If it's in a separate analytics package, OTel takes a package dependency on analytics.
- Explorer conclusion: Put `BudgetUtilization` and `KindDiversity` in `Wollax.Cupel` itself (no separate package needed for these two methods). Keep `ExcludedItemCount` in `Wollax.Cupel.Testing` only.

**Angles on `ExcludedItemCount`:**
- Could there be a production use case? A caller who wants to log "how many items were excluded by budget" post-run. But they can write `report.Excluded.Count(i => i.Reason == ExclusionReason.BudgetExceeded)` directly. The wrapper saves ~20 characters. Not worth a public API surface.
- What if the predicate is over `ExclusionReason` only (not `ExcludedItem`)? A `CountExclusionsWithReason(ExclusionReason)` pattern. Marginally useful — but still test-vocabulary territory. Keep in `Cupel.Testing`.

**No new decision required.** T02's verdict stands. This is a placement confirmation, not a re-evaluation.

---

### A3: `IntentSpec` as Preset Selector

**What changed since March 15:**
Nothing changed. `dry_run` adds no signal for `IntentSpec`. The March 15 rejection was: "the use case requires a context catalog that Assay (not Cupel) should own; 20 lines of caller-side code is sufficient."

**Explorer's fresh look:**
- The concrete use case: a caller has 5 different `ISlicingPolicy` configurations for different "contexts" (code review, debugging, code generation). They want to select the right policy based on metadata about the current task. This is `IntentSpec` — a metadata-driven policy selector.
- Is there a post-v1.2 signal? Possibly: `ContextItem.metadata` is now live. A caller could store `"cupel:task_type"` metadata on items and select a policy based on dominant metadata values. This is a richer version of the March 15 scenario.
- But the implementation is still trivial: a `Dictionary<string, ISlicingPolicy>` lookup keyed on `"cupel:task_type"` values. 10–20 lines. Cupel providing a built-in `IntentSpec` class would need to know how to parse task metadata, which is caller-specific.
- The only new angle: if `IntentSpec` is positioned as a `ISlicingPolicy` factory (takes metadata, returns policy), callers avoid boilerplate. But the factory logic is always domain-specific.
- Still 20 lines of caller-side code. No Cupel-owned catalog of intents. No new signal from v1.2.

**Assessment:** Still deferred to v1.3+ or drop. No new signal from v1.2. `dry_run` + `metadata` don't change the fundamental blocker (no catalog ownership in Cupel).

---

### A4: `ProfiledPlacer` Companion Package

**What changed since March 15:**
Nothing. The March 15 radical-report deferred `ProfiledPlacer` to v1.3+ because: no Cupel-owned attention profiles, caller-defined profiles require LLM usage statistics (not available to Cupel), and the staleness risk (profiles go stale as model attention patterns change) requires a companion package with separate versioning.

**Explorer's fresh look:**
- v1.2 shipped `dry_run` and `KnapsackSlice` OOM guard. Neither of these touches placement.
- `ProfiledPlacer` requires: (1) caller-provided attention profiles, (2) a placement algorithm that maps high-priority items to high-attention positions, (3) a companion package with profile schemas.
- No new implementation capability in v1.2 reduces the cost of implementing `ProfiledPlacer`.
- Is there any demand signal? Not visible from the codebase or spec changes.

**Assessment:** Confirmed still deferred to v1.3+. No change since March 15. The analysis is unchanged.

---

### A5: Snapshot Testing in `Cupel.Testing`

**What changed since March 15:**
The March 15 highvalue-report blocked snapshot testing because `SelectionReport.Included` ordering for items with equal scores is not guaranteed by the spec. The March 15 verdict: "Snapshot testing is blocked until SelectionReport ordering is stable for ties."

**Explorer's fresh look:**
- `SelectionReport` ordering for tied items: still determined by the inner `ISlicingPolicy`'s internal sort stability and the `IPlacer`'s behavior. Neither is specified in the v1.2 spec.
- Is there a path to making tie ordering deterministic? Yes:
  - Option 1: Add a tie-breaking rule to the spec. If scores are equal, sort by `ContextItem.id` ascending (lexicographic). This makes `SelectionReport.Included` ordering fully deterministic for any input.
  - Option 2: Add a `SelectionReport.Included` ordering guarantee only when using `GreedySlice` with a specific comparator. But this is placer-dependent, not report-dependent.
  - Option 3: Make snapshot testing bypass the ordering issue by comparing sets (not lists). `HaveIncludedItems([...], ordered: false)`. But this loses the placement ordering assertion entirely.
- Option 1 is the cleanest path: add a tiebreak rule to the spec's `DISTRIBUTE-BUDGET` pseudocode. Items with equal scores sort by `id` ascending. This is a spec addition, not a breaking change (callers cannot rely on tie order today since it is unspecified).
- Does v1.2 add any tie-resolution mechanism? No. `RecencyScorer` in Rust uses rank-based scoring, not raw timestamps, so ties are still possible when two items have the same rank.

**Fresh angles on unblocking snapshot testing:**
- If `KnapsackSlice` OOM guard was added in v1.2, the slicer internals were touched. Did that add any ordering guarantee? Check if `KnapsackSlice` sort order was stabilized.
- A `DeterministicTiebreakerPlacer` wrapper: a `IPlacer` that, after standard placement, re-sorts equal-score consecutive items by `id`. This is a pure add-on that doesn't change the primary placement but makes snapshots stable for callers who adopt it.
- Explorer conclusion: The cleanest path to unblocking snapshots is adding a tiebreak spec rule + `DeterministicTiebreakerPlacer` as a `Cupel.Testing` utility. The former is a spec PR; the latter is ~30 lines.

**Assessment:** Still blocked in M002. The path to unblocking requires a spec change (tiebreak rule) which is not in scope for M002. Add to M003 backlog with the specific spec change identified.

---

## Section B — New Post-v1.2 Ideas

### B1: `QuotaUtilization` Metric

Post-v1.2, callers using `QuotaSlice` with `RequireCount`/`CapCount` entries want to know how much of each kind's quota was actually used. Example: "required 3 `critical` items, got 3 (100%); capped at 5 `verbose` items, included 2 (40%)."

**Angles:**

- Where does this live? Three candidates:
  1. **`SelectionReport` field**: `QuotaViolations` was already proposed in T01 as a DI-3 option. A sibling `QuotaUtilization` field (list of `(ContextKind, required, cap, actual)` structs) is a natural extension. But it requires `SelectionReport` to know about quota configuration, which couples the report to a specific slicer.
  2. **Extension method on `SelectionReport`**: `report.QuotaUtilization(quotaPolicy)` takes the policy configuration as input and computes utilization by matching included item kinds against the policy entries. This is a `Wollax.Cupel.Analytics` function — no coupling in `SelectionReport` itself.
  3. **`Cupel.Testing` assertion**: `HaveQuotaUtilizationAbove(ContextKind, double threshold, QuotaConfig)`. This is a test-only assertion, not a production metric.

- Strongest placement: extension method in `Wollax.Cupel.Analytics`. The production use case (logging to OTel) is real. The test use case can call the analytics extension method internally.

- What does `QuotaUtilization` return for `QuotaSlice` vs `CountQuotaSlice`?
  - For `QuotaSlice`: percentage-based utilization (included tokens / budgeted tokens per kind).
  - For `CountQuotaSlice` (if S03 ships it): count-based utilization (included count / require_count or cap_count per kind).
  - These have different denominators and different interpretations. A unified `QuotaUtilization` return type would need to handle both cases or be overloaded.

- Explorer's rough API: `IReadOnlyList<KindUtilization> QuotaUtilization(this SelectionReport, IQuotaPolicy policy)` where `IQuotaPolicy` is a new interface. `KindUtilization` contains `{ ContextKind Kind, double Required, double Cap, double Actual, UtilizationType Type }` where `Type` is `Percentage | Count`.

- Risk: if `IQuotaPolicy` is a new interface that both `QuotaSlice` and `CountQuotaSlice` implement, that interface definition is a design decision for S03. This idea feeds S03's algorithm design.

- Feed: S03 (count-quota design) and S06 (analytics extension methods, OTel metrics).

---

### B2: Timestamp Coverage as a Context Health Signal

With `ContextItem.timestamp` live, a simple post-run metric "what fraction of included items have timestamps?" is a health signal for whether time-based scoring (`DecayScorer`, `RecencyScorer`) is well-supported by the caller's data.

**Angles:**

- T03 already proposed `TimestampCoverage()` as a `SelectionReport` extension method (DI-1 in future-features-report.md). The explorer already debated this. This section confirms it as a post-v1.2 concrete need.
- Additional angle: should timestamp coverage also be computed over the *excluded* items? If 0% of excluded items have timestamps but 80% of included items do, that's a signal that items without timestamps are being preferentially excluded (possibly by `DecayScorer` treating missing timestamps as ancient).
- A `TimestampCoverageReport { double IncludedCoverage, double ExcludedCoverage, int IncludedWithTimestamp, int ExcludedWithTimestamp }` return type would be more informative than a scalar.
- But complexity vs. usefulness: the simple scalar `double TimestampCoverage()` (over `Included` only) covers 90% of the use case. The split report adds noise for callers who just want to know if their items have timestamps.
- OTel attribute: `cupel.context.timestamp_coverage_ratio` as a float attribute on the root `cupel.pipeline` Activity.

- Feed: S06 (DecayScorer spec, OTel attribute catalog).

---

### B3: Count-Quota Exclusion Reason Variants

Post-v1.2, `ExclusionReason` has variants for budget-based exclusions but none for count-based quota violations. S03 will need to define what count-quota exclusions look like in `SelectionReport`.

**Explorer's proposal for new variants:**
- `CountCapExceeded { ContextKind Kind, int Cap, int Count }` — item was excluded because including it would exceed the cap for its kind. `Count` is the number of that kind already included when this item was considered.
- `CountRequireUnmet { ContextKind Kind, int Require, int Actual }` — the kind's `require_count` was not satisfied because fewer items of that kind were available. This is a *post-selection* diagnostic on the `SelectionReport`, not a per-item exclusion reason (the items weren't excluded because of a count requirement — they were already excluded or simply not present).

**Tension:**
- `CountRequireUnmet` is about the *policy not being satisfied*, not about a specific item being excluded. There's no individual item to attach it to. It belongs on `SelectionReport` directly (a `CountRequirements` field, not an `ExclusionReason` variant).
- `CountCapExceeded` is a legitimate per-item exclusion reason: item X was excluded because including it would be the Nth item of kind K, but the cap is N-1. This is exactly the `ExclusionReason` pattern.
- Are there other variants?
  - `CountRequireNotApplied { ContextKind Kind }` — the kind appeared in `require_count` configuration but no items of that kind were present in the input at all. Diagnostic only; not a per-item reason.
  - `CountPolicySatisfied { ContextKind Kind }` — not an exclusion reason at all, positive result.
  - `CountCandidatesExhausted { ContextKind Kind, int Require, int Available }` — similar to `CountRequireUnmet` but scoped to what was *available* vs. what was *required*. The difference from `CountRequireUnmet`: this one fires when items exist but were excluded for *other* reasons before the count-quota was satisfied (e.g., budget exclusion consumed the required items).

**Explorer's preferred set:** `CountCapExceeded` (per-item exclusion) + `CountRequirementShortfall` (report-level diagnostic field, not a per-item reason). Keep the `ExclusionReason` enum focused on per-item causes.

**Feed:** S03 (direct design input for count-quota algorithm and `SelectionReport` extensions).

---

### B4: Multi-Budget `dry_run` Batch API

T03 (future-features-report) already debated and **rejected** `DryRun(items, budgets[])` as a batch variant. The verdict: optimization saves microseconds at the cost of permanent API coupling, and `FindMinBudgetFor` can call single-budget `dry_run` ~10–15 times efficiently.

**Explorer's re-examination with post-v1.2 context:**
- Is there a use case outside of `FindMinBudgetFor`? A/B testing two different budgets in a product path (budget for "focus mode" vs "broad context mode"). The caller currently writes a loop. Is the loop trivial to write? Yes — two `dry_run` calls. No batch API needed.
- A batch API's only real advantage: reducing `ISlicingPolicy` instantiation overhead if the policy is expensive to build. If the policy is stateless (common case), this is not a meaningful savings.
- What about `DryRun(items, policy: ISlicingPolicy, budget: ContextBudget)` where the `ISlicingPolicy` is explicitly passed (not inferred from the `Cupel` instance)? This would allow callers to test a policy they haven't configured yet. Is this a useful pattern?
  - Today's API requires building a `Cupel` instance with a specific policy to call `dry_run`. If you want to test a different policy, you need a different `Cupel` instance.
  - A `Cupel.DryRun(items, policy, budget)` static-style method would decouple `dry_run` from instance configuration. This is useful for the fork diagnostic (A1) use case.

**New angle not in T03:** A `Cupel.DryRunWith(ISlicingPolicy, ContextBudget)` override that takes explicit policy + budget, bypassing instance configuration. This is not a batch API — it's a policy-override API. Much more useful than batch budgets.

**Feed:** M003 backlog (not M002 scope — this is an API surface question for a future dry_run revision).

---

### B5: `SelectionReport` Equality and Structural Comparison

With `dry_run` stable in both languages, callers running fork diagnostics (A1) need to compare two `SelectionReport` instances. What does equality mean?

**Angles:**
- Structural equality: two reports are equal iff `Included` (same items in same order), `Excluded` (same items in same order), and `TotalTokensUsed` are equal. This is the natural value-equality semantics.
- Set equality: two reports are "equivalent" iff `Included` contains the same items regardless of order. Useful for ordering-insensitive comparisons.
- Today: Does `SelectionReport` implement `IEquatable<SelectionReport>` in .NET? Does Rust `SelectionReport` derive `PartialEq`? If neither, callers writing fork diagnostics must implement their own comparison logic.
- In Rust: `ExclusionReason` is already marked `#[non_exhaustive]` (from T01 DI-6 context). Does `SelectionReport` derive `PartialEq`? If not, fork diagnostics will be awkward.

**Explorer's proposal:** Add `PartialEq, Eq` derives to `SelectionReport` and `ExclusionReason` in Rust (if `#[non_exhaustive]` permits it — it does; `#[non_exhaustive]` and `PartialEq` are compatible). Add `IEquatable<SelectionReport>` to the .NET record.

**Feed:** M003 backlog (quality improvement; no spec chapter needed, just implementation).

---

### B6: `KnapsackSlice` OOM Guard — Post-Ship Diagnostic Signal

`KnapsackSlice` OOM guard ships in v1.2. Post-ship, callers who hit the guard (when `items.length * bucket_size > threshold`) get a `CupelError::KnapsackOOMGuard` in Rust. But what does the error message say? Does it include the actual values (`items.length`, `bucket_size`, `threshold`)?

**Angles:**
- If the error message only says "KnapsackSlice input too large," callers cannot tune their usage without a debugger.
- A structured error: `KnapsackOOMGuard { item_count: usize, bucket_size: usize, threshold: usize, recommendation: String }` where `recommendation` suggests either reducing `item_count` (pre-filter before `dry_run`) or increasing `bucket_size`.
- In .NET: `CupelException` with a `Data` dictionary or properties for `ItemCount`, `BucketSize`, `Threshold`.

**Feed:** M002 S02 (Rust API quality) if the error isn't already structured. But this may be an existing quality issue, not a new post-v1.2 idea.

---

### B7: `ContextItem.metadata` Scoring Signal

`ContextItem.metadata` is live in v1.2. A `MetadataKeyScorer` (score boost for items with a specific `"cupel:<key>"` metadata value) was proposed in the March 15 radical-report's metadata convention system. Post-v1.2, this is implementable without new infrastructure.

**Angles:**
- `MetadataKeyScorer(key: string, value: string, boost: double)`: items with `metadata["cupel:priority"] == "high"` get score += boost.
- The boost is additive on top of whatever score the primary scorer assigns. Or multiplicative: score *= (1 + boost).
- Does the additive vs. multiplicative choice matter? Yes — if items start with very different scores, additive boosts can be overwhelmed by score differences while multiplicative boosts amplify existing score differentiation.
- Simplest design: multiplicative boost with a positive `boost` value (1.5 = 50% boost). Items without the key get boost = 1.0 (neutral).
- This is a small feature (~40 lines) that enables caller-side priority injection without modifying the scoring pipeline.

**Feed:** M003 backlog (scorer enhancement; no M002 slice covers this).
