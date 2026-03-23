# Brainstorm Summary: Post-v1.2 Sprint

**Session:** 2026-03-21T09-00
**Pairs:** 4 (count-quota-angles, testing-vocabulary-angles, future-features-angles, post-v1-2-ideas)
**Rounds:** 2 each
**Total proposals:** ~80 entered → ~55 survived debate

| Pair | Ideas File | Report File | Focus Area |
|------|-----------|-------------|------------|
| 1 | `count-quota-ideas.md` (238 lines) | `count-quota-report.md` (376 lines) | S03 — Count-based quota algorithm design |
| 2 | `testing-vocabulary-ideas.md` (247 lines) | `testing-vocabulary-report.md` (412 lines) | S05 — Cupel.Testing assertion vocabulary |
| 3 | `future-features-ideas.md` (462 lines) | `future-features-report.md` (524 lines) | S06 — DecayScorer, OTel, Budget Simulation spec chapters |
| 4 | `post-v1-2-ideas.md` | `post-v1-2-report.md` | Deferred re-evaluation + post-v1.2-specific ideas |

---

## Downstream Inputs (for M002 slices)

### S03: Count-Based Quota Design

Source: `count-quota-report.md` — 6 downstream inputs, 28 proposals evaluated (16 accepted/reshaped, 12 rejected).

**Top design questions S03 must answer:**

1. **DI-1 — Algorithm architecture**: Separate `CountQuotaSlice` (wrapping any inner slicer) vs. extension of `QuotaSlice` unifying count and percentage constraints. Decision hinges on whether callers commonly need both count AND percentage constraints simultaneously on the same kind. Survey M002 downstream slices and spec examples to answer.

2. **DI-2 — Tag non-exclusivity semantics** ⚠️ (hardest question): When an item with tags `["critical", "urgent"]` is included, does it satisfy both `RequireCount(critical, 2)` and `RequireCount(urgent, 2)` partially (non-exclusive) or only one (exclusive)? Non-exclusive is the challenger's recommendation, but this defines a public contract that cannot be changed after count-quota ships without a breaking version bump. S03 must spend disproportionate time here.

3. **DI-3 — Run-time scarcity behavior and SelectionReport representation**: Default behavior when `require_count > available_candidates` (degrade or throw). Diagnostic mechanism: `TraceEvent::CountQuotaScarcity` vs. `SelectionReport.quota_violations` dedicated field. Requires backward-compatibility audit of `SelectionReport` (DI-6).

4. **DI-4 — KnapsackSlice compatibility**: Recommended safe default for v1: reject combination with a build-time error (5E). Promote to pre-processing path (5A) if implementation proves straightforward. Separate `CountConstrainedKnapsackSlice` in a subsequent milestone if demand is established.

5. **DI-5 — Pinned item + overshoot edge case**: Does the count-quota cap apply to total included count (pinned + selected) or only to slicer-selected count? Pinned-items-exceed-cap is degenerate — define run-time warning behavior (trace event).

6. **DI-6 — `ExclusionReason` extension precondition**: Verify `#[non_exhaustive]` on `SelectionReport` (Rust) and `ExclusionReason` (Rust) before designing new variants. Precondition for DI-3 diagnostic mechanism choice.

**Additional inputs from post-v1-2-report.md (B3):**
- `CountCapExceeded { ContextKind Kind, int Cap, int Count }` — accepted new `ExclusionReason` variant (per-item cause)
- `CountRequireCandidatesExhausted { ContextKind Kind, int Require, int Available }` — accepted new variant (items existed but were excluded before quota was satisfied)
- `SelectionReport.CountRequirementShortfalls` field — report-level diagnostic for unmet requirements (not a per-item `ExclusionReason`)

---

### S05: Cupel.Testing Vocabulary Design

Source: `testing-vocabulary-report.md` — 5 downstream inputs, 15 vocabulary candidates produced, 12 ready/needs-work status assessed.

**15 vocabulary candidates (in priority order):**

| # | Method | Status |
|---|--------|--------|
| 1 | `IncludeItemWithKind(ContextKind)` | ✅ Ready |
| 2 | `IncludeItemMatching(Func<IncludedItem, bool>)` | ✅ Ready |
| 3 | `ExcludeItemWithReason(ExclusionReason)` | ✅ Ready |
| 4 | `ExcludeItemMatchingWithReason(Func<ContextItem, bool>, ExclusionReason)` | ✅ Ready |
| 5 | `HaveAtLeastNExclusions(int)` | ✅ Ready |
| 6 | `HaveAtLeastNExclusionsWithReason(ExclusionReason, int)` | ✅ Ready |
| 7 | `PlaceItemAtEdge(Func<IncludedItem, bool>)` | ✅ Ready |
| 8 | `HaveKindCoverageCount(int)` | ✅ Ready |
| 9 | `HaveTokenUtilizationAbove(double, ContextBudget)` | ⚠️ Needs work |
| 10 | `ExcludedItemsAreSortedByScoreDescending()` | ✅ Ready |
| 11 | `HaveNoExclusions()` | ✅ Ready |
| 12 | `IncludeExactlyNItemsWithKind(ContextKind, int)` | ✅ Ready |
| 13 | `PlaceTopNScoredAtEdges(int)` | ⚠️ Needs work |
| 14 | `HaveIncludedTokensLessThanBudget(ContextBudget)` | ✅ Ready |
| 15 | `ExcludeItemWithBudgetDetails(Func<ContextItem, bool>, int, int)` | ⚠️ Needs work |

**Top design questions S05 must answer (from downstream inputs DI-1 through DI-5):**

- **DI-1**: Define `SelectionReportAssertionChain` type, constructor, and `SelectionReportAssertionException` failure mechanism.
- **DI-2**: All predicate-based methods consistently accept `IncludedItem` / `ExcludedItem` (not `ContextItem`). Decide once in the intro spec.
- **DI-3**: `HaveTokenUtilizationAbove/Below/InRange` denominator = `budget.MaxTokens`. Document rationale.
- **DI-4**: Replace `PlaceHighestScoredAtEdges` with `PlaceTopNScoredAtEdges(n)`. Define: (a) top-N by score descending; (b) edge-position mapping (0, count-1, 1, count-2, ...); (c) tie-score handling.
- **DI-5 (updated by post-v1-2-report A2)**: `BudgetUtilization(budget)` and `KindDiversity()` belong in **`Wollax.Cupel` core** (not `Wollax.Cupel.Analytics` — no separate package needed). `ExcludedItemCount(pred)` is `Wollax.Cupel.Testing` only.

---

### S06: Future Features Spec Chapters

Source: `future-features-report.md` — 3 downstream inputs covering DecayScorer, OpenTelemetry, and Budget Simulation. 21 proposals evaluated across three feature areas.

**S06 must specify — DecayScorer (6 items):**
1. Rust `TimeProvider` trait: `pub trait TimeProvider: Send + Sync { fn now(&self) -> DateTime<Utc>; }` with `SystemTimeProvider` ZST. D042 is locked — mandatory injection.
2. Negative age handling: `age = max(Duration::ZERO, reference_time - item.timestamp)`. Future-dated items score as maximally fresh.
3. Zero half-life precondition: throw at construction with clear error message.
4. `nullTimestampScore` default: 0.5 (neutral). Constructor parameter is explicit per D042 rationale.
5. `TimestampCoverage()` as fourth analytics extension method: `double TimestampCoverage(this SelectionReport)` — placement in `Wollax.Cupel` core (updated by post-v1-2-report A2).
6. D042 is locked: `TimeProvider` injection mandatory at construction; no silent `DateTime.UtcNow` default.

**S06 must specify — OpenTelemetry (6 items):**
1. Activity hierarchy: flat (root `cupel.pipeline` + 5 stage children). No nested Activities.
2. `StageOnly` attribute set: `cupel.budget.max_tokens`, `cupel.verbosity` (root); `cupel.stage.name`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out` (each stage). No `duration_ms`.
3. `StageAndExclusions` additions: `cupel.exclusion` Event with `cupel.exclusion.reason`, `.item_kind`, `.item_tokens`; `cupel.exclusion.count` summary on stage Activity.
4. `Full` additions: `cupel.item.included` Event with `.kind`, `.tokens`, `.score`. No placement attribute.
5. Cardinality table reproduced verbatim in spec and README.
6. D043 locked: all attribute names begin `cupel.`; namespace pre-stable; README must warn.

**S06 must specify — Budget Simulation (6 items):**
1. No multi-budget `DryRun` variant (explicit spec statement).
2. `GetMarginalItems` diff direction: `Primary \ Margin` (items in full-budget not in reduced-budget result).
3. `FindMinBudgetFor` lower bound: `targetItem.Tokens`. Preconditions for target membership and `searchCeiling`.
4. `FindMinBudgetFor` return type: `int?` / `Option<i32>`. `null`/`None` = not selectable within bounds.
5. `FindMinBudgetFor` monotonicity guard: `InvalidOperationException` for `QuotaSlice`; document general precondition.
6. `DryRun` determinism invariant: must be deterministic for identical inputs; tie-breaking must be stable.

---

## Deferred Items Status

| Idea | March 15 Verdict | Post-v1.2 Verdict | Notes |
|------|-----------------|------------------|-------|
| Fork diagnostic (`PolicySensitivityReport`) | Surviving radical proposal — awaiting `dry_run` | **M003 — ready to plan** | `dry_run` now live in both languages; ~80 lines per language; scope limited to "which items swing" + per-variant `SelectionReport` |
| `SelectionReport` extension methods (`BudgetUtilization`, `KindDiversity`) | Deferred — placement undecided | **Reassigned → `Wollax.Cupel` core (S05)** | Revised from T02's `Wollax.Cupel.Analytics` recommendation — same package as `SelectionReport` to avoid OTel dependency chain |
| `ExcludedItemCount(predicate)` | Deferred — placement undecided | **Reassigned → `Wollax.Cupel.Testing` only (S05)** | Production callers use `report.Excluded.Count(pred)` directly |
| `IntentSpec` as preset selector | Rejected — "20 lines of code" | **M003+ — defer further** | No new signal from v1.2; `ContextItem.metadata` is live but does not provide a Cupel-owned catalog |
| `ProfiledPlacer` companion package | Surviving radical proposal — deferred to v1.3+ | **M003+ — defer further (confirmed)** | No new signal; three original blockers unchanged |
| Snapshot testing in `Cupel.Testing` | Blocked — tie ordering unspecified | **M003+ — defer further (path identified)** | Unblocking path: add tiebreak rule to spec (sort by `id` ascending) + make default in `GreedySlice`; both are M003 prerequisite work |

---

## New Backlog Candidates

Items generated during this session to be tracked for M003 or later. Must not be acted on within M002.

| Candidate | Target | Description |
|-----------|--------|-------------|
| `PolicySensitivityReport` / fork diagnostic | M003 | Thin orchestration over `dry_run`; `RunPolicySensitivity(items, [(label, policy)])` entry point; ~80 lines per language |
| `SpecTiebreakerRule` + default tiebreak in `GreedySlice` | M003 | Spec: sort by `ContextItem.id` ascending on score tie; makes ordering deterministic for snapshot testing |
| `DeterministicTiebreakerPlacer` | M003 | `IPlacer` wrapper for stable tie ordering; ~30 lines; prerequisite for snapshot testing opt-in |
| `QuotaUtilization` extension method | M003 (after S03) | `IReadOnlyList<KindUtilization> QuotaUtilization(this SelectionReport, IQuotaPolicy)` — depends on S03 defining `IQuotaPolicy` |
| `SelectionReport.CountRequirementShortfalls` | M003 (after S03) | Report-level field for unmet count requirements; not a per-item `ExclusionReason` |
| `CountRequireCandidatesExhausted` variant | M003 (after S03) | New `ExclusionReason` when count-quota candidates exhausted by prior exclusions |
| `SelectionReport` structural equality | M003 | `PartialEq, Eq` (Rust) + `IEquatable` (.NET); unblocks fork diagnostic callers |
| `MetadataKeyScorer` + metadata convention spec | M003 | Multiplicative scorer over `ContextItem.metadata`; co-ships with metadata convention spec entry; ~40 lines per language |
| Snapshot testing in `Cupel.Testing` | M003 (after tiebreak) | Blocked until tiebreak rule in spec and `GreedySlice` default stable |
| `TimestampCoverageReport` (split form) | M003+ | Split `TimestampCoverage()` over `Included` and `Excluded`; follow-on only if demand observed |
| `DryRunWithPolicy` override | M003+ | Policy-explicit variant of `dry_run` for fork diagnostic callers; defer until demand established |
| `IntentSpec` as preset selector | v1.3+ or drop | No Cupel-owned catalog; 20 lines of caller code; defer until concrete demand |
| `ProfiledPlacer` companion package | v1.3+ | Requires LLM attention statistics and separate versioning; three original blockers unchanged |

---

## Cross-Cutting Themes

1. **`SelectionReport` is the integration seam**: Four of the six S03 design questions involve `SelectionReport` extensions (`ExclusionReason` variants, `quota_violations` field, `CountRequirementShortfalls`). Pairs 2 and 4 also depend on `SelectionReport` being extensible. The `#[non_exhaustive]` audit (DI-6) is a cross-cutting prerequisite that affects S03, S05, and S06 simultaneously.

2. **Placement consolidation: analytics belong in `Wollax.Cupel` core**: Three pairs independently arrived at analytics extension methods (`BudgetUtilization`, `KindDiversity`, `TimestampCoverage`, `QuotaUtilization`). The post-v1.2 pair produced a placement revision: all three core analytics methods belong in `Wollax.Cupel` itself, not a separate `Wollax.Cupel.Analytics` package. This avoids the OTel-depends-on-Analytics dependency chain. A separate analytics package remains a concept for future analytics requiring additional dependencies.

3. **`dry_run` as an enabler**: `dry_run` landing in both languages unlocked three post-v1.2 angles: fork diagnostic (now M003-ready), `DryRunWithPolicy` override (M003+ backlog), and the rejection of multi-budget `DryRun` batch (confirmed by T03 then re-confirmed here). The API's stability in both languages is a meaningful capability gate.

4. **Spec-first is non-negotiable for S03**: The tag non-exclusivity semantics question (DI-2) is the single most consequential design decision in M002. It defines a public contract that cannot be changed without a breaking version bump. The brainstorm identifies it as the "hardest question" across all four pairs. S03 must treat this as a first-class design decision, not an implementation detail.

5. **M003 shape is emerging**: This session produced a coherent M003 candidate list: `PolicySensitivityReport`, `MetadataKeyScorer`, structural equality for `SelectionReport`, tiebreak rule in spec + `GreedySlice`, snapshot testing in `Cupel.Testing`, and `QuotaUtilization` (after S03). These are small, cohesive additions that build on M002's deliverables without requiring new pipeline stages or external dependencies. M003 planning will have substantial pre-work from this session.
