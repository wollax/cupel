# Radical Ideas — Consolidated Report

> Brainstorm session: 2026-03-10T15-06
> Authors: explorer-radical + challenger-radical
> Rounds: 3 (initial proposals + 2 debate rounds)

---

## Executive Summary

Seven radical proposals were debated across two rounds. Four survived with refinements, one new idea emerged from debate, and three were deferred or rejected. The surviving proposals are all architectural constraints or metadata additions — none require new dependencies or violate "policy not storage."

---

## Adopted Proposals

### 1. Ordinal-Only Scoring Invariant (from Idea #6 — ConstraintSolvedContext)

**What**: Establish a hard architectural invariant across the pipeline:

> **Scorers rank. Slicers drop. Placers position.**

Scorers must never eliminate items — they assign weights. Any `MinScore` threshold filtering violates separation of concerns: it's eliminatory logic masquerading as scoring. Only the Slicer has the authority to exclude items, because only the Slicer sees the full budget picture.

**Why it matters**: The current pipeline "leaks" — an item that scores poorly at step 2 never reaches the Slicer's holistic budget logic at step 4. KnapsackSlice's selection is provably correct only if it receives the complete candidate set. Without this invariant, even the best Slicer makes suboptimal decisions on biased input.

**Corollary**: `Pinned` items bypass the entire Scorer → Slicer sequence and go directly to the Placer. Pinned items are guaranteed-included; scoring them would distort the ordinal ranking of unpinned candidates.

**Corollary**: If callers want to hard-exclude items before passing to Cupel, that's their responsibility — filter before calling, not inside the pipeline.

**Scope**: Constraint on component contract, not a code addition. Implement by auditing existing scorers for threshold filtering and removing it. Update contributing docs with the invariant.

---

### 2. OriginalTokens Metadata + CompressionRatio (from Idea #1 — Cupel-as-Compressor)

**What**: Add `OriginalTokens` as an optional field on `ContextItem`. Add `CompressionRatio` as a derived read-only property (`OriginalTokens / Tokens`). Fold compression efficiency into existing scorer weight calculations: a 200-token item that replaced 2,000 tokens earns its slot differently than a raw 200-token item.

Compression itself is the **caller's responsibility** — Cupel does not compress. The `ICompressor` concept belongs above Cupel (in Smelt or caller code). Cupel's job: score compressed items correctly by being aware of their information density.

**Why it matters**: Real-world context is full of redundancy (three tool calls returning "no results"). Pre-compression is already a common practice. Cupel is currently blind to it — a 200-token compressed item scores identically to a 200-token raw fragment, even though the former carries 10x more information.

**Scope**: Small. One new field on `ContextItem`, one derived property, scorer weight adjustments. Zero new dependencies.

**Note**: Cupel trusts caller-supplied `OriginalTokens` — inflating it to game the scorer is the caller's problem. The library is not responsible for adversarial callers.

---

### 3. Intent-Based Policy Convenience Layer (from Idea #5 — PolicyNegotiation)

**What**: Add `CupelOptions.AddPolicy(string intent, CupelPolicy policy)` via a builder/IServiceCollection extension. Ship 7 well-tuned built-in named policies. Users can override or extend via `AddPolicy()`.

Built-in policies (v1):
- `chat` — balanced recency/priority, moderate U-curve placement
- `code-review` — deprioritizes recency (stable files matter), boosts `Kind=Document`
- `rag` — weights retrieved chunks, aggressive deduplication, source diversity
- `document-qa` — large stable documents pinned to edges (U-curve), questions at center
- `tool-use` — heavy recency weighting, fast depreciation of old tool results
- `long-running` — anti-recency for planning artifacts, preserves early reasoning
- `debugging` — high priority for errors/stack traces, dense token tolerance, recent tool results first

**Design constraints**:
- No runtime discovery, no NuGet marketplace, no fuzzy intent matching
- `CupelOptions`-scoped, not a static global registry (no threading/test-isolation issues)
- Policies are registered on the options object, consistent with .NET DI conventions
- Adding a custom policy: `options.AddPolicy("my-task", myPolicy)` — one line, no fork

**Why it matters**: Most developers don't want to configure pipelines. The ergonomics gap between "configure scorers manually" and `CupelOptions.UsePolicy("debugging")` is the adoption barrier. Named policies lower the floor without raising the ceiling.

**Scope**: Medium. Policy authoring + DI integration + documentation. Core engine unchanged.

---

### 4. FutureRelevanceHint Field (from Idea #7 — ReflexiveCupel)

**What**: Add `FutureRelevanceHint` (float 0–1, optional, caller-supplied) to `ContextItem`. Add a `ReflexiveScorer` component that weights items higher when their hint is elevated. Callers who have planning data (e.g., Smelt knows the next 2 agent steps) populate this field.

**Why it matters**: Greedy context selection causes "context traps" — burning tokens on low-value items early, with no room for critical items arriving later. This field gives callers a channel to express forward-looking relevance without requiring Cupel to simulate future turns.

**Design**: The intelligence stays with the caller. Cupel just respects the hint. If the field is null/unset, `ReflexiveScorer` is a no-op.

**Scope**: Small. One field, one scorer component. No simulation, no speculation.

---

### 5. IPlacer — Pluggable Placement (new, emerged from debate)

**What**: Refactor the Placer into an `IPlacer` interface. Ship `UShapedPlacer` as the default implementation. Allow caller-supplied placement strategies.

**Why it matters**: The U-shaped attention curve (primacy + recency effects) is real but model-dependent, task-dependent, and actively contested. If Cupel hardcodes placement to U-shape, it embeds an assumption that may not hold for newer models or specialized tasks. Every other pipeline component is already composable — the Placer is the last hardcoded decision.

**Scope**: Small refactor. Extract interface, rename existing implementation to `UShapedPlacer`, update pipeline to accept `IPlacer`. Default remains U-shaped.

---

## Deferred (Right Insight, Wrong Timing)

### CupelSession Wrapper (from Idea #2 — ContextStream)

The push-model insight is valid — incremental context updates are better than full recomputation for orchestrators. But this belongs **above** Cupel, not in the core. A thin `CupelSession` wrapper (possibly a separate NuGet package) that maintains state and calls the stateless Cupel core keeps the core clean and testable. If Smelt wants push-model semantics, it builds this wrapper.

**Deferral reason**: Cupel's statelessness is its biggest testability and correctness advantage. Don't compromise it in v1.

### Algorithm Spec Documentation (from Idea #3 — Cupel Protocol)

The insight (context management is universal, Python/TS dominate AI) is correct. But building a CLI binary + cross-language SDKs before v1 .NET ships is premature abstraction, and CLI process-spawn latency (50–200ms per LLM call) kills production use cases.

**Deferral action**: Document the scoring/slicing algorithm as a clear specification in the README — good enough that a Python or TS developer could port it. Ship the CLI only when there is demand evidence. Earns a v2 roadmap slot.

---

## Rejected

### AdaptiveScorer (Idea #4)

Gradient-boosted scoring on small N with caller-defined success signals is statistically inferior to well-tuned heuristics. Cold start means v1 users get bad results. The underlying problem (task-dependent optimal weights) is better solved by explicit named weight parameters in policy config + a tuning guide.

### Cupel-as-Compressor (core version of Idea #1)

A library-internal compressor that calls an LLM creates a recursive dependency (context window needed to compress a context window), makes the pipeline nondeterministic, and turns Cupel into the magic it was designed to avoid. Adopted as: `OriginalTokens` metadata + caller-side `ICompressor` pattern.

---

## Architectural Principles Surfaced by This Debate

These weren't in the original design but should be codified:

1. **Scorers rank, Slicers drop, Placers position.** This is the fundamental separation of concerns. No component crosses this boundary.
2. **Pinned items bypass scoring.** They enter the pipeline at the Placer. Scoring them would distort ordinal ranking of non-pinned candidates.
3. **Trust the caller, not the library.** Compression, future relevance, and outcome feedback all live above Cupel. The library respects caller-supplied signals without trying to generate them.
4. **No global mutable state.** Registry/configuration lives on options objects scoped to DI containers, not static classes.
5. **Defaults should be principled, not opinionated.** U-shaped placement is the right default *today* — but it's a default, not a mandate. Every component should be replaceable.

---

## Recommended v1 Scope Additions

From this brainstorm, the following are low-risk, high-value additions to the current architecture:

| Addition | Effort | Impact |
|----------|--------|--------|
| Ordinal-only invariant + pipeline audit | XS | High — correctness guarantee |
| `OriginalTokens` + `CompressionRatio` on ContextItem | XS | Medium — density-aware scoring |
| `FutureRelevanceHint` on ContextItem | XS | Medium — multi-turn awareness |
| `IPlacer` interface refactor | S | Medium — composability completeness |
| 7 named built-in policies via `CupelOptions.AddPolicy()` | M | High — adoption / DX |

The first four are metadata and interface changes — zero risk to existing API. The named policies are the only significant new surface area.
