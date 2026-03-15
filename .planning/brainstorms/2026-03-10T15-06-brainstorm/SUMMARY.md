# Cupel Brainstorm Summary

**Date**: 2026-03-10 | **Pairs**: 3 | **Rounds**: 2-3 per pair | **Model**: Sonnet

---

## Session Overview

Three explorer/challenger pairs debated Cupel's design across three lenses: quick wins (MVP scope), high-value architecture, and radical/contrarian directions. Each pair produced initial proposals, 2-3 rounds of debate, and a consolidated report.

---

## Quick Wins — MVP in ~5.5 Days

4 ideas survived pressure-testing. 3 were deferred or re-homed.

| # | Idea | Effort | Status |
|---|------|--------|--------|
| 1 | **OverflowStrategy enum + callback** — explicit behavior when pinned items exceed budget | 0.5d | Ship |
| 2 | **Token counting factory** — `Func<string, int>` delegate, caller pre-computes, pipeline never touches raw content | 1d | Ship |
| 3 | **SelectionReport / dry-run** — `DryRun()` returns included/excluded items with reasons (no per-scorer breakdown in v1) | 2d | Ship |
| 4 | **Policy presets** — `ChatSession()`, `DocumentQA()`, `AgentLoop()`, `CodeReview()` with `[Experimental]` attribute | 2d | Ship |

**Deferred**: Fluent builder API (post-MVP), Markdown frontmatter factory (separate package), JSON policy serialization (needs scorer registry).

**Open decision**: Does `ContextItem` retain `Content` after construction? Recommendation: nullable `string?`, retained by default, explicit `DiscardContent()`.

[Full report](quickwins-report.md)

---

## High-Value Architecture — 7 Decisions

5 proposals survived with significant scoping. 1 replaced by simpler alternative. 1 split (core kept, adapters deferred).

| Phase | Decision | Summary |
|-------|----------|---------|
| 1a | **ContextResult return type + ContextTrace** | `Apply()` returns `ContextResult(Items, Trace?)` — must land before API hardens. Trace construction gated, no AsyncLocal. |
| 1b | **TokenCountProvider delegate** | `Func<ContextItem, int>?` on policy + `EstimationSafetyMarginPercent` on budget. Probabilistic model rejected. |
| 1b | **Semantic quotas (percentage-only)** | `Require(Kind, minPercent)` / `Cap(Kind, maxPercent)` on slicer. Count-based deferred. |
| 1b | **IContextSource in core** | `IAsyncEnumerable<ContextItem>` abstraction. Adapters owned by consumers, not Cupel. |
| 1c | **CompositeScorer** | Nested composites replace scorer DAG. `ScaledScorer` wrapper for non-0-1 scorers. |
| 1d | **Fluent builder** | Fixed pipeline, substitutable implementations. No call-next middleware (silent-drop risk). |
| 1d+ | **Policy serialization (incremental)** | `[JsonPropertyName]` on all types from day 1. Serialize stable subsets first. No YAML. |

**Key invariants surfaced**:
- IScorer output conventionally 0.0-1.0 (clamped by CompositeScorer, not enforced by type)
- ContextResult is the return type from day 1
- Trace event construction gated (not just collection)
- No AsyncLocal for trace propagation
- Pinned item + quota interaction is specified, not discovered
- IContextSink does not exist — Cupel selects, consumers convert

**Rejected**: Full scorer DAG, probabilistic TokenCount, IContextSink, Cupel.Adapters.* packages, YAML, hot reload, call-next middleware.

[Full report](highvalue-report.md)

---

## Radical Ideas — 5 Adopted, 2 Deferred, 2 Rejected

Paradigm-shifting proposals that challenge stated assumptions.

| # | Idea | Effort | Impact |
|---|------|--------|--------|
| 1 | **Ordinal-only scoring invariant** — "Scorers rank. Slicers drop. Placers position." No scorer eliminates items. | XS | High — correctness guarantee |
| 2 | **OriginalTokens + CompressionRatio** — metadata for density-aware scoring of pre-compressed items | XS | Medium |
| 3 | **Intent-based named policies** — 7 built-in policies via `CupelOptions.AddPolicy("debugging", ...)` | M | High — adoption |
| 4 | **FutureRelevanceHint field** — caller-supplied forward-looking relevance signal | XS | Medium |
| 5 | **IPlacer interface** — pluggable placement, U-shaped as default not mandate | S | Medium |

**Deferred**: CupelSession push-model wrapper (belongs above Cupel), cross-language algorithm spec (document as spec in README, CLI when demand exists).

**Rejected**: AdaptiveScorer (gradient-boosted on small N is worse than tuned heuristics), Cupel-as-compressor (recursive LLM dependency, nondeterministic).

**Principles surfaced**:
1. Scorers rank, Slicers drop, Placers position
2. Pinned items bypass scoring entirely
3. Trust the caller — compression, future relevance, feedback all live above Cupel
4. No global mutable state — config on DI-scoped options objects
5. Defaults are principled, not opinionated — every component replaceable

[Full report](radical-report.md)

---

## Cross-Cutting Themes

**1. Separation of concerns is load-bearing.** All three pairs converged on strict boundaries: scorers don't eliminate, Cupel doesn't compress, adapters live in consumers. The pipeline's value comes from its predictability.

**2. Explainability is the killer feature.** Dry-run/SelectionReport (quickwins), ContextTrace (highvalue), and the ordinal-only invariant (radical) all point the same direction: users need to understand *why* items were included or excluded. This is what differentiates Cupel from ad-hoc prompt stuffing.

**3. Named policies lower the floor.** Both quickwins (4 presets) and radical (7 named policies) independently proposed pre-configured policies as the primary adoption lever. The exact number and naming differ, but the insight is the same.

**4. Content retention is a cross-cutting decision.** Quickwins flagged it for dry-run fidelity, highvalue needs it for trace data. Resolve before implementation: `Content` as nullable `string?`, retained by default.

**5. Wire format stability from day 1.** Highvalue's `[JsonPropertyName]` mandate and quickwins' JSON serialization concerns both require attributing public types before any consumer starts serializing directly.

---

## Recommended Sequencing

Based on dependencies across all three reports:

1. **Resolve Content retention decision** (architectural, 0 code)
2. **Ordinal-only invariant** (constraint, audit existing code)
3. **ContextResult + ContextTrace** (most breaking change)
4. **OverflowStrategy enum** (error handling contract)
5. **TokenCountProvider + EstimationSafetyMargin** (unblocks real usage)
6. **ContextItem metadata fields** (OriginalTokens, FutureRelevanceHint)
7. **IPlacer interface refactor**
8. **CompositeScorer + ScaledScorer**
9. **Semantic quotas (percentage-only)**
10. **IContextSource interface**
11. **SelectionReport / dry-run**
12. **Fluent builder**
13. **Named policy presets** (depends on all above)
14. **`[JsonPropertyName]` on all public types** (concurrent with type definition)
