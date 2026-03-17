# Radical Ideas for Cupel — Paradigm-Shifting Proposals

**Explorer:** explorer-radical
**Date:** 2026-03-15
**Status:** Initial draft — pending challenger review

---

## Framing

Cupel today: a fixed 6-stage pipeline that selects and orders context items from a flat candidate list within a token budget. Clean, deterministic, heuristic-driven, zero-dependency, language-agnostic.

These proposals challenge the fundamental assumptions: what a context window IS, who asks for it, when it's built, and what the pipeline is even for.

---

## Idea 1: The Context Protocol — Cupel as Inter-Agent Context Exchange Standard

### What

Transform Cupel from a library into a **standardized protocol for context negotiation between agents**. Like HTTP is to document transfer, Cupel becomes the protocol for context exchange in multi-agent systems.

Each agent exposes a `ContextEndpoint` with:
- A **context manifest**: what it currently has, with token costs, kinds, scores
- A **negotiation interface**: "I need X tokens of tool output context, you have it — send me your top-scored items"
- A **delta protocol**: "I already have items A, B, C — what do you have that's new?"

The Cupel spec would define:
1. A wire format for context item serialization (already partially done with Rust serde + JSON)
2. A negotiation protocol: request/response schema for "give me your best N tokens of kind K"
3. A merge algorithm: when two agents have overlapping context, who wins, how is scoring reconciled
4. A routing manifest: given M agents each with budget B, partition total candidate context optimally

The library implementations (Rust, .NET) become the reference client/server for this protocol.

### Why

Multi-agent architectures are becoming dominant. Today, each agent has its own independent context window with no coordination — leading to redundant context (same system prompt in 10 agents) and critical gaps (agent A has the error log, agent B needs it but doesn't know it exists). Cupel already has a language-agnostic spec and dual implementations. Making it a protocol is the natural evolution: from "library you embed" to "standard you implement."

### Scope

**Large** — requires:
- Protocol spec extension (new spec chapters)
- Wire format definition (extends existing serde work)
- Reference server implementation
- Client SDKs in .NET and Rust
- Negotiation algorithm design (novel distributed algorithm)

### Risks

- Protocol design is hard; v1 protocols are almost always wrong
- Adoption requires buy-in from other agent frameworks (not just Cupel users)
- Could dilute Cupel's identity as a pure selection library
- Network concerns (latency, availability) violate the zero-dependency philosophy
- May be better as a separate protocol library that *uses* Cupel, not Cupel itself

---

## Idea 2: Inverse Context Synthesis — Goal-Driven Context Planning

### What

Flip the pipeline entirely. Today: "Here are N candidate items → select the best ones for this budget."

Proposed: "Here is a goal/intent → compute the ideal context that would maximize success, then tell the caller what items to retrieve."

The **Inverse Cupel Pipeline**:
1. **Intent Parse**: Accept a goal expression (tags, kind priorities, or a structured intent object — no LLM calls)
2. **Context Blueprint**: Generate a specification of what ideal context would look like — "I need: 2000 tokens of recent tool outputs, 500 tokens of system prompt, 1000 tokens of high-priority messages with tag 'error'"
3. **Demand Projection**: Given a known corpus of available items (their token costs and metadata, but not content), predict which items best satisfy the blueprint
4. **Gap Report**: Identify what's missing — "You have no items tagged 'error' in the last 5 minutes — you should retrieve them"
5. **Retrieval Hints**: Return an ordered list of retrieval instructions, not selected items

This is Cupel running in *planning mode* before items are even loaded.

### Why

Current agentic pipelines operate reactively: the LLM generates a tool call, the tool runs, the output is added to context, repeat. This means context is always built from what happened, never from what *should* be in it. Inverse synthesis enables **proactive context construction** — an agent can ask "what do I need to know to accomplish X?" before taking any action. Addresses the 65% enterprise AI failure rate from context drift by making context needs explicit and plannable.

### Scope

**Medium** — requires:
- New `IntentSpec` type (composable: `kind + tag + recency + token budget`)
- Blueprint generator (heuristic, not ML)
- Demand projection against a lightweight item metadata catalog (not full content)
- Gap analysis report type
- New preset profiles for common intent patterns

Could be shipped as a companion library, keeping core zero-dependency.

### Risks

- Intent specification language design is hard — too expressive = complexity, too simple = useless
- Without content, gap analysis is approximate (metadata may not capture actual relevance)
- Callers may not have a "metadata catalog" — they'd have to build one separately
- Risk of overlapping with RAG retrieval systems (which Cupel explicitly avoids)
- The "no LLM calls" constraint means intent parsing is limited to structured input, not natural language

---

## Idea 3: Adversarial Context Hardening — Trust-Aware Selection Pipeline

### What

Add a **trust scoring dimension** to context selection. Every item gets two scores: `relevance` (today's score) and `trust` (new). Trust is computed by a new family of hardening scorers that detect:

- **Injection patterns**: Items containing instruction-like content directed at the agent (e.g., "Ignore previous instructions...")
- **Coherence violations**: Items that contradict a "trusted core" set (e.g., pinned system prompt says X, a tool output claims not-X)
- **Provenance decay**: Items from external/uncontrolled sources get lower base trust than items generated by the agent itself
- **Amplification attacks**: Items that reference many other items or try to establish authority ("As previously established...")

The pipeline extension:
1. New `IHardeningScorer` interface: `Score(item, trustedCore) → TrustScore`
2. Trust threshold as a budget constraint: items below `minTrust` are excluded regardless of relevance score
3. `HardenedReport`: extend SelectionReport with trust scores and rejection reasons
4. `TrustPolicy`: configurable rules (strict = exclude below 0.7, permissive = warn only)

This is entirely heuristic — no ML, no LLM calls. Pattern matching, structural analysis, provenance tracking.

### Why

Enterprise AI failures from context manipulation are real and growing. Today's context selection libraries (including Cupel) assume all candidate items are trustworthy — they optimize for relevance, not integrity. As agents operate in less-controlled environments (processing external web content, user-uploaded files, third-party tool outputs), the attack surface grows. Making trust a first-class selection dimension transforms Cupel from an optimization tool into a **context security layer**. This aligns with the transparency/auditability philosophy — every exclusion has a reason, including security-motivated ones.

### Scope

**Medium** — requires:
- New `TrustScore` type and `IHardeningScorer` interface
- 3-4 initial hardening scorers (injection pattern, coherence check, provenance, amplification)
- Integration point in Classify stage (pre-scoring trust check) or as Stage 1.5
- New `TrustPolicy` configuration on `CupelPolicy`
- Extended `SelectionReport` with trust analysis
- New conformance test vectors for hardening behavior

Keeping it heuristic/deterministic preserves the no-ML, zero-dep philosophy.

### Risks

- False positives would silently drop legitimate context — very bad for reliability
- Heuristic injection detection is a cat-and-mouse game; sophisticated attacks will bypass it
- "Coherence violation" detection requires semantic understanding that pure heuristics can't provide
- Could give false security: users may trust Cupel's hardening as comprehensive when it's not
- Adds significant complexity to the spec — conformance becomes harder
- Provenance tracking requires callers to annotate sources accurately (trust the annotator?)

---

## Idea 4: Cross-Model Attention Profiling — Model-Specific Context Placement

### What

The U-shaped placer is based on the general "lost in the middle" phenomenon. But different LLMs have measurably different attention profiles — Claude pays more attention to the beginning, GPT-4 has stronger recency bias, Gemini's attention is more uniform at shorter contexts and degrades differently at longer ones.

**Cupel Attention Profiles**:
- A new `AttentionProfile` type: a piecewise function mapping position (0.0 = start, 1.0 = end) to attention weight
- Built-in profiles for known models, empirically measured and versioned (e.g., `AttentionProfile.Claude3_5`, `AttentionProfile.Gpt4o`)
- A `ProfiledPlacer`: instead of U-shape, places items to maximize `Σ(score × attention_weight_at_position)`
- This is a constrained optimization: given N items with scores, arrange them to maximize weighted coverage
- Profiles ship as data (not code), updatable without library version bumps
- Callers can provide custom profiles for models we don't have data on

The placement optimization is solvable analytically for simple attention functions (linear, exponential) and via DP for piecewise functions.

### Why

The U-shaped placer is a one-size-fits-all approximation. As context windows grow (1M tokens), the performance degradation curves become dramatically model-specific. The 17-point MRCR drop at 1M tokens is an average — actual curves vary wildly. Every percentage point of attention efficiency = meaningful improvement in agent reasoning quality. This is a concrete, measurable improvement over current placement with clear benchmark opportunities. It also opens a new research direction: Cupel as an empirical measurement platform for LLM attention behavior.

### Scope

**Medium** — requires:
- `AttentionProfile` type and serialization
- `ProfiledPlacer` with optimization algorithm
- 3-5 initial empirically-measured profiles (requires benchmark study — scope creep risk)
- Profile data versioning system
- Benchmark harness for measuring profile accuracy
- Companion `Wollax.Cupel.Profiles` package (keeps data out of zero-dep core)

### Risks

- Empirical measurement of attention profiles is expensive and may not be reproducible
- Profiles become stale as model versions change (GPT-4o today ≠ GPT-4o in 6 months)
- Optimization is NP-hard for arbitrary piecewise attention functions; approximations may underperform U-shaped
- Legal/TOS issues measuring proprietary model attention patterns
- False precision: users may over-trust profiles that are actually imprecise
- Maintaining profile data is ongoing ops work (different from library maintenance)

---

## Idea 5: Context Forking — Epistemic Uncertainty Through Parallel Context Variants

### What

Instead of one canonical context window, produce **N parallel context variants** optimized for different hypotheses. The `CupelFork` API:

```
ForkResult Fork(candidates, budget, forkStrategies)
```

Returns multiple context windows simultaneously:
- **Recency fork**: optimize heavily for recency (what just happened matters most)
- **Priority fork**: optimize heavily for explicit priority signals
- **Coverage fork**: maximize unique kind diversity within budget
- **Conservative fork**: only include high-confidence items (high score, high token density)
- **Expansive fork**: use a relaxed scoring threshold to include more "uncertain" items

Each fork has its own SelectionReport explaining why items were included/excluded under that strategy.

Downstream uses:
1. **Hallucination detection**: Send the same prompt with different context forks to the same LLM. If answers diverge significantly, context is the likely differentiator — flag for human review.
2. **A/B context strategy testing**: Without ground truth labels, measure which fork strategy leads to better downstream outcomes.
3. **Ensemble context**: Merge the N forks back into a ranked "super-context" where items appearing in multiple forks get higher composite confidence.
4. **Uncertainty quantification**: Items present in all forks are "consensus context"; items present in only one fork are "contested context" — valuable signal.

### Why

Current context selection is treated as a solved problem once the pipeline runs. But context selection is inherently uncertain — the "right" context depends on what the LLM will do with it, which is unknown. Embracing this uncertainty rather than hiding it enables new capabilities. Hallucination detection is a $100M problem in enterprise AI today, and context uncertainty is an underexplored contributor. The forking approach is computationally cheap (N pipeline runs with different configs) and requires no LLM calls. It's Cupel's natural extension of its existing "transparent, composable" philosophy — now applied to the meta-question of "which strategy is correct?"

### Scope

**Small-Medium** — requires:
- `ForkStrategy` type (essentially a `CupelPolicy` variant)
- `ForkResult` return type (dictionary of strategy → ContextWindow + SelectionReport)
- `CupelPipeline.Fork()` API
- Built-in named fork strategies matching existing presets
- Divergence metric between forks (token Jaccard, item intersection)
- New `Wollax.Cupel.Forking` package or extension to main library

This is largely a thin orchestration layer over existing pipeline runs — most complexity is already built.

### Risks

- N pipeline runs = N× compute cost; for large item sets, could be slow
- Downstream system must be built to consume N context windows — adds significant complexity to callers
- Hallucination detection via fork divergence is a heuristic with no proven accuracy
- "Ensemble context" merging back from N forks is a new algorithmic problem not yet scoped
- Risk of over-engineering: most users just want one good context window, not five
- Could confuse the mental model: Cupel always returned one window, now it returns N?

---

## Summary Table

| Idea | Paradigm Shift | Scope | Risk Level |
|------|---------------|-------|------------|
| Context Protocol | Library → Standard | Large | High |
| Inverse Synthesis | Reactive → Proactive | Medium | Medium |
| Adversarial Hardening | Optimization → Security | Medium | Medium-High |
| Cross-Model Profiles | Empirical → Theoretically-Grounded Placement | Medium | Medium |
| Context Forking | Deterministic → Probabilistic | Small-Medium | Low-Medium |

---

*These ideas are intentionally radical. The challenger's job is to stress-test them.*
