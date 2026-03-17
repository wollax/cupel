# Radical Ideas — Final Consolidated Report

**Session:** 2026-03-15T12-23-brainstorm
**Explorer:** explorer-radical
**Challenger:** challenger-radical
**Rounds:** 2
**Status:** Complete

---

## Process Summary

Five radical proposals were generated, stress-tested across two debate rounds, and pressure-reduced to three surviving ideas with clearly bounded scope. Two proposals were correctly rejected as belonging to the Smelt/Assay layer rather than Cupel. The surviving ideas are all additive: companion packages or spec additions that touch the core pipeline minimally or not at all.

---

## Rejected Proposals

### ❌ The Context Protocol

**Original claim:** Transform Cupel into a standardized inter-agent context negotiation protocol — the "HTTP for context."

**Why rejected:** Context selection is a pure local computation. Network coordination, agent negotiation, and distributed routing are not selection problems. The protocol layer would require building a distributed systems library (network, serialization, routing, negotiation algorithms) that has no meaningful relationship to context selection. The wire format for context items already exists via serde/JSON serialization in the Rust crate.

**Where this belongs:** A `cupel-protocol` library *consuming* Cupel as a dependency, or in Smelt's inter-agent coordination layer. Not Cupel itself.

**What survives in Cupel:** Nothing new — serde serialization already handles the wire format concern.

---

### ❌ Inverse Context Synthesis

**Original claim:** Flip the pipeline — accept a goal/intent and compute what context would be needed before items are loaded.

**Why rejected:** The core implementation requires knowledge of what items *exist* (a metadata catalog of token costs, kinds, and tags across all potentially-relevant items). Owning that catalog makes Cupel a context database, which is Assay's job. The "Gap Report" functionality is a retrieval query, not a selection operation. The distinction between "retrieval specification" and "retrieval execution" is real but the allocation logic still requires knowledge of what's available — a Smelt/Assay-level concern.

**What survives:** `IntentSpec` as a structured preset selector — "given a goal descriptor, select the closest named preset and apply bias adjustments" — is a convenience API worth considering in a future version. It maps structured intent to existing `CupelPolicy` configuration. This is 20 lines of code, not a new pipeline stage.

**Where this belongs:** The full inverse synthesis capability belongs in Smelt/Assay as a context planning layer that calls down to Cupel for selection.

---

## Surviving Proposals

### ✅ 1. Metadata Convention System (`"cupel:<key>"` namespace)

**Original framing:** Adversarial Context Hardening — a new trust-scoring pipeline stage.

**What it became after debate:** A canonical metadata key namespace and a reference scorer that reads trust from it.

**The core insight:** Without a convention for how trust is expressed in `ContextItem.metadata`, a `MetadataTrustScorer` is useless across projects — every caller invents their own schema, the scorer can't be shared, and the ecosystem fragments. Cupel should define a canonical namespace.

**Proposed design:**
- Spec addition: define `"cupel:<key>"` as the reserved metadata namespace for Cupel-defined semantic conventions
- First convention: `"cupel:trust"` (float64, range [0.0, 1.0]) — caller-computed provenance trust score
- Second convention (optional): `"cupel:source-type"` (string enum) — annotates item origin (e.g., `"user"`, `"tool"`, `"external"`, `"system"`)
- New scorer: `MetadataTrustScorer` reads `"cupel:trust"` and contributes to `CompositeScorer` like any other scorer
- Caller computes and sets trust values — Cupel does not own provenance computation
- Trust gates (silent exclusion below threshold) are explicitly **not** part of this proposal; trust is an input to scoring, not a filter

**Why this fits Cupel's philosophy:** Transparent, composable, caller-controlled. Every exclusion traces back to a score, and trust is one scoring dimension among many. The namespace pattern is extensible: future conventions can be added without breaking changes.

**Why "adversarial hardening" was wrong framing:** Heuristic pattern-matching for injection attacks creates false positives (legitimate content about prompt injection gets dropped silently) and false security (sophisticated attacks don't use obvious patterns). Trust as a scoring dimension — computed by the caller who knows their sources — is the correct decomposition.

**Scope:** Small. Spec paragraph + `MetadataTrustScorer` implementation (both .NET and Rust) + 3-5 conformance test vectors. No new pipeline stage, no new interfaces beyond `IScorer`.

**Risks:** Namespace collision if callers use `"cupel:"` prefix for their own keys before the convention is established. Mitigated by reserving the namespace in the spec from the start.

---

### ✅ 2. `ProfiledPlacer` — Caller-Provided Attention Profiles (Companion Package)

**Original framing:** Cross-Model Attention Profiling with Cupel-maintained empirical profiles per model.

**What it became after debate:** A `ProfiledPlacer` extension that places items to maximize score-weighted attention, using caller-provided (or community-maintained) profiles. No Cupel-owned profiles.

**The core insight:** The U-shaped placer captures the dominant empirical insight (edges matter) but approximates all models with one heuristic. Different LLMs have measurably different attention curves across context lengths and task types. For callers who know their model and have measured its attention behavior, a profile-driven placer offers a more principled placement strategy.

**Proposed design:**
```
AttentionProfile {
  modelId: string
  modelVersion: string           // or hash — whatever the caller uses
  captureDate: DateTime
  taskTypes: string[]            // tasks this profile was measured on
  sampleSize: int                // number of observations
  curve: PiecewiseCurve          // position [0.0, 1.0] → attention weight [0.0, 1.0]
}

ProfiledPlacer(profile: AttentionProfile, currentTaskType: string)
  → places items to maximize Σ(score × attention_weight_at_position)
```

**Explicit failure modes (both required for transparency):**
1. **Staleness warning:** if `DateTime.UtcNow - profile.captureDate > configuredThreshold`, emit warning via `ITraceCollector`
2. **Applicability warning:** if `currentTaskType` is not in `profile.taskTypes[]`, emit applicability warning — profile was measured on different tasks

These warnings don't block execution (callers may knowingly use approximate profiles) but make the failure modes visible, consistent with Cupel's transparency philosophy.

**Optimization:** For piecewise curves with P segments and N items, placement optimization is O(N² × P) in the worst case but sub-millisecond for typical use (N < 500, P < 20). Falls back to U-shaped behavior when profile is unavailable.

**Lives in:** `Wollax.Cupel.Profiles` companion package. Zero changes to core pipeline or conformance spec (ProfiledPlacer is an additional placer, same as adding a new scorer). No Cupel-owned profile data — callers provide profiles, a community repository can emerge separately.

**Why "Cupel maintains profiles" was wrong:** Empirical attention profiles stale as model versions change. A profile measured on MRCR tasks is wrong for code review. Maintaining profiles is ongoing ops work that violates the library's zero-maintenance-burden design. Callers who know their model and tasks own this knowledge best.

**Scope:** Medium. New companion package, `AttentionProfile` type, `PiecewiseCurve` type, `ProfiledPlacer` implementation, warning integration via `ITraceCollector`, documentation on how to measure and express a profile.

---

### ✅ 3. Context Fork Diagnostic — Policy Sensitivity Analysis (Developer Tool)

**Original framing:** `CupelPipeline.Fork()` returns N parallel context windows for runtime use, enabling hallucination detection and ensemble context.

**What it became after debate:** A developer-time diagnostic that runs N pipeline configurations against a representative item corpus to identify which items are sensitive to policy choices.

**The core insight:** The divergence metric is genuinely novel epistemic metadata Cupel cannot currently express. Items present in all N forks = consensus context (your scorer weight choices don't matter for them). Items present in only one fork = contested context (your configuration is making load-bearing decisions that may be miscalibrated). This is actionable: if a critical item is only selected by one strategy, that's a signal to audit your scoring weights.

**Conceded from original proposal:**
- Hallucination detection via fork divergence: rejected — correlation with no proven causality
- Ensemble context merging: rejected — circular (run N pipelines → meta-analysis → run one more)
- Runtime N× overhead framing: wrong use case; this is a development-time tool

**Proposed design:**
```
ForkDiagnostic RunFork(
  sampleItems: ContextItem[],    // representative corpus
  budget: ContextBudget,
  strategies: ForkStrategy[]     // named strategy variants
) → ForkDiagnosticReport {
  perStrategy: Map<ForkStrategy, ContextWindow + SelectionReport>
  divergence: ForkDivergence {
    consensusItems: ContextItem[]   // in all forks
    contestedItems: Map<ContextItem, ForkStrategy[]>  // which strategies select each
    jaccardMatrix: float[][]        // pairwise intersection across strategies
  }
}
```

**Lives in:** The diagnostic ecosystem alongside `.DryRun()` and `DiagnosticTraceCollector` — not a first-class production API. Signals intended use: run before deployment to validate your policy configuration, not in the hot path. Lives in `Wollax.Cupel` as an extension on `CupelPipeline` or in a separate `Wollax.Cupel.Diagnostics` package.

**Built-in fork strategies:** Mirrors existing presets (recency-dominant, priority-dominant, coverage-maximizing, conservative). No new algorithms — each strategy is a `CupelPolicy` variant running the existing pipeline.

**Scope:** Small-medium. Thin orchestration over existing pipeline runs. New `ForkDiagnosticReport` type, `ForkDivergence` type, `ForkStrategy` type (essentially a `CupelPolicy` alias), and set-intersection logic. Most complexity is already built.

**Why runtime production use was wrong:** N× compute at runtime, N× complexity for downstream consumers (who must handle N windows), and no validated advantage over a well-tuned single policy. Development-time use has none of these problems — it's a one-time analysis that improves the single production policy you then run everywhere.

---

## Synthesis: What These Three Ideas Share

All three surviving proposals reinforce Cupel's core philosophy without extending the core pipeline:

1. **Metadata Convention System** — makes trust a first-class scoring concern using the existing scoring model, but standardizes *how* callers express it so the ecosystem can share scorers
2. **ProfiledPlacer** — gives callers who have precise empirical knowledge a more principled placement option, while preserving the existing placers for everyone else
3. **Fork Diagnostic** — gives callers a way to *understand* their policy configuration empirically before deploying, addressing the "presets are educated guesses, not empirically validated" limitation in the current spec

None requires a new pipeline stage. None violates the ordinal-only principle. None adds runtime ML or LLM dependencies. All three are additive and backward-compatible.

---

## Recommended Priority

| Proposal | Milestone | Rationale |
|----------|-----------|-----------|
| Metadata Convention System | v1.2 | Small scope, high ecosystem value, enables `MetadataTrustScorer` as first consumer |
| Fork Diagnostic | v1.2 or v1.3 | Low risk, high developer value, mostly thin orchestration |
| ProfiledPlacer companion package | v1.3+ | Medium scope, niche but principled; blocks on community interest in providing profiles |

---

*Report finalized after 2 debate rounds. Two proposals correctly rejected as Smelt/Assay-layer concerns. Three proposals accepted with bounded scope as additive Cupel features.*
