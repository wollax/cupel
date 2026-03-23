# Post-v1.2 Report — Challenger Mode

**Session:** 2026-03-21T09-00
**Pair:** 4 of 4 (post-v1-2-report)
**Mode:** Challenger (precision filter, verdict per item)

---

## Section A — Deferred Items: Verdicts

### A1: Fork Diagnostic (`ForkDiagnosticReport`)

**March 15 Verdict:** Surviving radical proposal. Deferred pending `dry_run` availability in both languages.

**Post-v1.2 Verdict: M003 — ready to plan**

**Rationale:** The explorer correctly identifies that `dry_run` was the sole missing prerequisite. With `dry_run` stable in .NET and Rust, the fork diagnostic is now thin orchestration (~80 lines per language) over an existing API. The rename to `PolicySensitivityReport` is accepted — "fork" implies divergent execution, not policy comparison.

Scope of M003 work: (1) `PolicySensitivityReport` type definition in spec; (2) `RunPolicySensitivity(items, [(label, policy)])` entry point; (3) per-language implementation. No new pipeline stages. No external dependencies.

**Scope-creep guard:** The explorer's suggestion to include aggregated statistics (average utilization across variants) is a scope risk. The M003 scope must be "which items swing" + "per-variant SelectionReport." Aggregate statistics belong in a subsequent iteration.

---

### A2: `SelectionReport` Extension Methods

**March 15 Verdict:** Deferred — placement decision required.

**Post-v1.2 Verdict: Reassigned — belongs in S05 (BudgetUtilization, KindDiversity) and S05 testing vocabulary (ExcludedItemCount)**

**Rationale:** T02 produced the definitive placement verdict. The explorer's re-examination confirms it without new angles. The only clarification made here is on package placement:

The explorer argues for putting `BudgetUtilization` and `KindDiversity` in `Wollax.Cupel` itself (not a separate analytics package) because the OTel package would otherwise take a dependency on a hypothetical analytics package. This is sound. `SelectionReport` extension methods that operate only on `SelectionReport` fields with no external dependencies belong in `Wollax.Cupel`.

**Revised placement:**
- `BudgetUtilization(budget)` → `Wollax.Cupel` (extension method on `SelectionReport`; Rust: `cupel` crate)
- `KindDiversity()` → `Wollax.Cupel` (same)
- `ExcludedItemCount(predicate)` → `Wollax.Cupel.Testing` only

This overrides the T02 recommendation to place the first two in `Wollax.Cupel.Analytics` (a separate package). The `Wollax.Cupel.Analytics` package concept is retained for future analytics features that require additional dependencies, but these two methods don't need it.

**Action for S05:** S05's DI-5 (extension methods placement) is updated by this verdict. S05 should implement `BudgetUtilization` and `KindDiversity` in the core `Wollax.Cupel` package, not as a separate package.

---

### A3: `IntentSpec` as Preset Selector

**March 15 Verdict:** Rejected (radical-report.md) — "20 lines of code; no Cupel-owned catalog."

**Post-v1.2 Verdict: M003+ — defer further**

**Rationale:** The explorer correctly finds no new signal from v1.2. `ContextItem.metadata` is live, but it does not provide a catalog of intents — that is still the caller's or Assay's responsibility. The "20 lines of caller-side code" estimate remains accurate.

The `"cupel:task_type"` metadata angle is interesting but does not change the fundamental blocker: Cupel cannot define a canonical set of task types because they are domain-specific. An `IntentSpec` class that takes a `Dictionary<string, ISlicingPolicy>` lookup is essentially a named wrapper around a dictionary lookup — not worth a public API surface.

**No M003 action required.** If concrete demand appears (multiple callers independently implementing the same pattern), reconsider in M003. Until then, document the caller-side pattern in the cookbook, not in the API.

---

### A4: `ProfiledPlacer` Companion Package

**March 15 Verdict:** Surviving radical proposal. Deferred to v1.3+ due to no Cupel-owned profiles and staleness risk.

**Post-v1.2 Verdict: M003+ — defer further**

**Rationale:** The explorer finds no new signal. The analysis is confirmed unchanged. v1.2 shipped `dry_run` and `KnapsackSlice` OOM guard, neither of which bears on placement behavior. The three blockers identified in March — (1) no Cupel-owned attention profiles, (2) caller-defined profiles require LLM usage statistics not available to Cupel, (3) staleness risk requires separate versioning — all remain.

**Confirmed deferred.** Revisit if LLM tooling provides stable, published attention profiles for common model families.

---

### A5: Snapshot Testing in `Cupel.Testing`

**March 15 Verdict:** Blocked — `SelectionReport` ordering for tied items is unspecified.

**Post-v1.2 Verdict: M003+ — defer further (with a concrete unblocking path identified)**

**Rationale:** The explorer identifies a concrete unblocking path: (1) add a tiebreak rule to the spec (sort by `ContextItem.id` ascending when scores are equal) and (2) add a `DeterministicTiebreakerPlacer` wrapper. This is accurate and useful — but both require spec changes, and spec changes for M002 are out of scope.

The `DeterministicTiebreakerPlacer` idea is sound and worth ~30 lines. However, calling it in `Cupel.Testing` without it being the default `Placer` means snapshots only work when callers explicitly use this placer. That is a weak guarantee — callers might not know they need it.

**Recommended M003 path:** (1) Add tiebreak rule to spec as a patch to `DISTRIBUTE-BUDGET` pseudocode. (2) Make tiebreak behavior the default in `GreedySlice` (not an opt-in placer). (3) Enable snapshot testing in `Cupel.Testing` only after the default behavior is stable.

**New backlog candidate:** `SpecTiebreakerRule` + `DefaultTiebreakerInGreedySlice` — track these as M003 prerequisites for snapshot testing.

---

## Section B — New Post-v1.2 Ideas: Verdicts

### B1: `QuotaUtilization` Metric

**Verdict: Accepted — feeds S03 (count-quota algorithm design) and S06 (analytics extension methods)**

**Placement:** Extension method in `Wollax.Cupel` (not a separate analytics package, per A2 verdict revision). Return type: `IReadOnlyList<KindUtilization>` where `KindUtilization` carries `{ Kind, Required, Cap, Actual, Type }`.

**Scope boundary:** The `IQuotaPolicy` interface proposed by the explorer is a S03 design decision — S03 must define whether `QuotaSlice` and `CountQuotaSlice` share an interface. This is a downstream input from S03, not a standalone S06 spec task.

**S06 action:** Once S03 defines the quota policy interface, S06 includes `QuotaUtilization` in the analytics extension methods spec chapter. Do not implement until S03 ships.

**Scope-creep risk:** The `UtilizationType` (Percentage | Count) discriminated union in `KindUtilization` implies a unified return type covering both `QuotaSlice` and `CountQuotaSlice`. This is fine for M003 scope but must not be designed before S03 finalizes the count-quota algorithm.

---

### B2: Timestamp Coverage as a Context Health Signal

**Verdict: Accepted — confirmed by T03 DI-1; feeds S06**

T03 DI-1 already accepted `TimestampCoverage()` as a `SelectionReport` extension method. This section is a confirmation, not new content.

The explorer's additional angle (split report over included vs. excluded items) is **rejected** for the initial spec: the scalar `double TimestampCoverage()` over `Included` covers 90% of the use case. A `TimestampCoverageReport` struct adds API surface for a 10% scenario. S06 should implement the scalar first and note the split variant as a follow-on if demand is observed.

**S06 action:** `TimestampCoverage()` extension method in `Wollax.Cupel` (per A2 placement revision). Signature already specified in T03 DI-1.

---

### B3: Count-Quota Exclusion Reason Variants

**Verdict: Accepted for `CountCapExceeded` (per-item); Rejected for `CountRequireUnmet` as ExclusionReason**

**`CountCapExceeded { ContextKind Kind, int Cap, int Count }`:** Accepted. This is a legitimate per-item exclusion reason — item X was excluded because including it would make the Nth item of kind K, which exceeds the cap. Clean `ExclusionReason` variant, consistent with existing pattern.

**`CountRequireUnmet` as `ExclusionReason`:** Rejected. The explorer correctly identifies the semantic problem: `CountRequireUnmet` is about the *policy not being satisfied*, not about a specific item being excluded. There is no item to attach it to. It belongs on `SelectionReport` as a dedicated field (e.g., `SelectionReport.CountRequirementShortfalls: IReadOnlyList<CountRequirementShortfall>`).

**`CountCandidatesExhausted`:** Accepted as a separate variant from `CountRequireUnmet`. The distinction — items *existed* but were excluded for other reasons before the count-quota was satisfied (budget exclusion consumed them) — is meaningful for diagnostics. Name: `CountRequireCandidatesExhausted { ContextKind Kind, int Require, int Available }` where `Available` is the count of candidates of that kind in the input (before any exclusions).

**Feed:** S03. This is a direct design input for the count-quota algorithm and `SelectionReport` extension design.

**Scope-creep risk:** `ExclusionReason` must be `#[non_exhaustive]` in Rust before adding new variants (T01 DI-6). This is a precondition, not a design question — but S03 must verify it before spec-writing.

---

### B4: Multi-Budget `dry_run` Batch API

**Verdict: Reject batch; Accept `DryRunWith(ISlicingPolicy, ContextBudget)` policy-override as M003 backlog**

The batch `DryRun(items, budgets[])` was already rejected in T03. The re-examination confirms the rejection.

The new angle — `Cupel.DryRunWith(ISlicingPolicy, ContextBudget)` (policy-override, not batch-budget) — is worth tracking. Today's `dry_run` is tied to the `Cupel` instance's configured policy. For fork diagnostics (A1), callers building `PolicySensitivityReport` would benefit from passing the policy explicitly. However:
- This changes the `dry_run` API signature, which is a breaking change if done as an overload in some calling patterns.
- The `PolicySensitivityReport.RunPolicySensitivity` wrapper already provides this by constructing a new `Cupel` instance per policy variant.
- Not worth a core API change for M003. Document the pattern; if it appears in multiple callers independently, reconsider.

**New backlog candidate (M003+):** `DryRunWithPolicy` — policy-explicit variant of `dry_run` for caller-side fork diagnostics.

---

### B5: `SelectionReport` Equality and Structural Comparison

**Verdict: Accepted — M003 quality improvement; no spec chapter needed**

`PartialEq, Eq` derives on `SelectionReport` and `ExclusionReason` in Rust. `IEquatable<SelectionReport>` on the .NET record. Both are quality improvements that unblock fork diagnostic callers.

**Scope boundary:** Do NOT add `Eq` to `ExclusionReason` if it is `#[non_exhaustive]` and contains `f64` fields (floating-point equality is non-reflexive). Check field types before adding `Eq`. `PartialEq` is safe regardless.

**No M002 action.** This is a quality improvement for M003.

---

### B6: `KnapsackSlice` OOM Guard Diagnostic Signal

**Verdict: Accepted if not already structured; file as quality issue against existing implementation**

The explorer is correct: structured error parameters (`item_count`, `bucket_size`, `threshold`) are necessary for callers to diagnose and tune. If the current `CupelError::KnapsackOOMGuard` variant already carries these fields, no action. If it is an opaque string error, it needs structured fields.

**Action:** Inspect the current `CupelError` variant definition. If unstructured, file as a quality fix in M002 S02 (Rust API quality). If S02 is already closed, file as M003 quality item.

**This is not a new idea** — it is a quality diagnostic on an already-shipped feature. Treat as a bug report, not a brainstorm candidate.

---

### B7: `ContextItem.metadata` Scoring — `MetadataKeyScorer`

**Verdict: Accepted — M003 backlog**

The explorer's design is clean: `MetadataKeyScorer(key, value, boost)` with multiplicative boost (1.5 = 50% boost), neutral multiplier (1.0) for items without the key. ~40 lines per language.

**Refinement:** The explorer debates additive vs. multiplicative. The challenger recommends multiplicative because: (1) it is scale-invariant — items with very different base scores still experience proportional boosts; (2) it is composable with `CompositeScorer` without requiring knowledge of other scorers' magnitude ranges; (3) it is consistent with the `ScaledScorer` pattern already in the codebase.

**Scope boundary:** `MetadataKeyScorer` requires the `"cupel:priority"` metadata convention to be documented in the spec (March 15 radical-report proposal 1 — metadata convention system). Both can ship together in M003.

**New backlog candidate:** `MetadataKeyScorer` + metadata convention spec entry.

---

## New Backlog Candidates

The following items should be tracked for M003 or M003+ and must **not** be acted on within M002 slices:

| Candidate | Target | Description |
|-----------|--------|-------------|
| `PolicySensitivityReport` / fork diagnostic | M003 | Thin orchestration over `dry_run`; ~80 lines per language; `RunPolicySensitivity(items, [(label, policy)])` entry point |
| `SpecTiebreakerRule` + default tiebreak in `GreedySlice` | M003 | Spec change: sort by `ContextItem.id` ascending on score tie; enable snapshot testing in `Cupel.Testing` |
| `DeterministicTiebreakerPlacer` | M003 | `IPlacer` wrapper that stabilizes tied-score ordering; ~30 lines; prerequisite for snapshot testing opt-in |
| `QuotaUtilization` extension method | M003 (after S03) | `IReadOnlyList<KindUtilization> QuotaUtilization(this SelectionReport, IQuotaPolicy)` — depends on S03 defining `IQuotaPolicy` |
| `SelectionReport.CountRequirementShortfalls` | M003 (after S03) | Report-level field for `CountRequireUnmet` diagnostics; not a per-item `ExclusionReason` |
| `CountRequireCandidatesExhausted` variant | M003 (after S03) | New `ExclusionReason` variant when count-quota candidates were exhausted by prior exclusions; depends on S03 `ExclusionReason` audit |
| `SelectionReport` structural equality | M003 | `PartialEq, Eq` derives (Rust) + `IEquatable` (.NET); unblocks fork diagnostic callers |
| `DryRunWithPolicy` | M003+ | Policy-explicit variant of `dry_run`; defer until demand is established |
| `MetadataKeyScorer` + metadata convention spec | M003 | Multiplicative scorer over `ContextItem.metadata` values; ~40 lines per language; co-ships with metadata convention spec entry |
| `IntentSpec` as preset selector | v1.3+ or drop | Still 20 lines of caller code; no Cupel-owned catalog; defer until concrete demand |
| `ProfiledPlacer` companion package | v1.3+ | No Cupel-owned profiles; requires LLM attention statistics; staleness risk |
| Snapshot testing in `Cupel.Testing` | M003 (after tiebreak) | Blocked until tiebreak rule is in spec and `GreedySlice` default is stable |
| `TimestampCoverageReport` split (included vs excluded) | M003+ | Follow-on to scalar `TimestampCoverage()`; only if demand observed |

---

## Scope-Creep Guard

The following ideas were generated during this pair but must **not** be assigned to any M002 slice:

1. `QuotaUtilization` → S03 must finalize `IQuotaPolicy` interface first; do not design before S03 ships.
2. `CountRequirementShortfalls` field → S03 must define count-quota algorithm first; do not design `SelectionReport` extensions before the algorithm is specified.
3. `PolicySensitivityReport` aggregate statistics → explicitly excluded from M003 scope; set-comparison is the correct scope for the first iteration.

Any M002 ticket that attempts to implement items from the "New Backlog Candidates" table is out of scope.

---

## Verdict Summary Table

| Idea | Source | Verdict | Target |
|------|--------|---------|--------|
| Fork diagnostic / `PolicySensitivityReport` | Deferred A1 | M003 — ready to plan | M003 |
| `BudgetUtilization`, `KindDiversity` | Deferred A2 | Reassigned → S05 + core Wollax.Cupel | S05 |
| `ExcludedItemCount` | Deferred A2 | Reassigned → S05 (testing only) | S05 |
| `IntentSpec` as preset selector | Deferred A3 | M003+ — defer further | v1.3+ |
| `ProfiledPlacer` | Deferred A4 | M003+ — defer further | v1.3+ |
| Snapshot testing | Deferred A5 | M003+ — defer further (path identified) | M003 |
| `QuotaUtilization` metric | New B1 | Accepted — M003 after S03 | M003 |
| `TimestampCoverage` (scalar) | New B2 | Accepted — confirmed by T03 DI-1 | S06 |
| `CountCapExceeded` variant | New B3 | Accepted — feeds S03 | S03 |
| `CountRequireUnmet` as ExclusionReason | New B3 | Rejected — report-level, not per-item | — |
| `CountRequireCandidatesExhausted` variant | New B3 | Accepted — feeds S03 | S03 |
| Multi-budget `dry_run` batch | New B4 | Rejected (confirmed T03 verdict) | — |
| `DryRunWithPolicy` override | New B4 | M003+ backlog | M003+ |
| `SelectionReport` structural equality | New B5 | Accepted — M003 quality | M003 |
| `KnapsackSlice` OOM error structure | New B6 | File as quality bug if unstructured | M002/M003 |
| `MetadataKeyScorer` | New B7 | Accepted — M003 backlog | M003 |
