# Radical Ideas for Cupel — Explorer-Radical

> Brainstorm session: 2026-03-10T15-06
> Author: explorer-radical
> Purpose: Challenge assumptions, propose paradigm shifts

---

## Idea 1: Semantic Compression as a First-Class Citizen

**Name**: Cupel-as-Compressor

**What**: Cupel should not just *select* context items — it should *compress* them. Instead of a binary include/exclude decision, items could be summarized, deduplicated by semantic similarity (embeddings), or merged when they cover overlapping ground. The library would accept a `ICompressor` delegate and call it as a pre-slice step.

**Why**: The current model treats tokens as fixed — you either take the item or don't. But real-world context is full of redundancy: three tool call results that all say "no results found", five assistant turns that recapitulate the same plan. A compressor that shrinks 4,000 tokens to 800 without losing signal is worth more than any scoring heuristic. This is the actual frontier problem.

**Scope**: Large. Requires async pipeline support (compressor calls may be network-bound — e.g. a summarizer LLM). Core pipeline change. Could be opt-in via `CupelPolicy.Compressor`.

**Risks**:
- Compression is lossy — hard to make deterministic/testable
- Creates a dependency on another LLM call (latency, cost)
- Blurs the line between "policy library" and "AI middleware"
- Scope creep: now Cupel IS the magic it was supposed to avoid

---

## Idea 2: The Inverse API — Push Model Instead of Pull

**Name**: ContextStream (Push-Based Cupel)

**What**: Instead of the current "give me a list, I'll return a selection" pull model, Cupel exposes a streaming push API. Callers `Emit()` context events as they happen (tool called, message received, document loaded). Cupel maintains a live window internally and exposes `IAsyncEnumerable<ContextSnapshot>` — the caller subscribes and gets notified whenever the optimal context changes.

**Why**: The pull model forces callers to batch and re-submit everything on each turn. A push model enables *incremental* optimization: Cupel knows what changed and can cheaply recompute. This matches how LLM orchestration actually works — events flow in a stream. Smelt (the orchestrator above Cupel) would benefit enormously from this model.

**Scope**: Medium-large. Requires internal mutable state (contradicts "not storage"?), async event loop, change diffing.

**Risks**:
- "Policy not storage" principle is *directly violated* — Cupel would hold state
- Concurrency complexity
- Harder to reason about determinism and reproducibility
- Breaks the clean functional API

---

## Idea 3: Abandon .NET — Make Cupel a Language-Agnostic Spec + CLI

**Name**: Cupel Protocol

**What**: The core insight of Cupel — scoring, slicing, placing — is universal. Why tie it to .NET? Instead: define a language-agnostic JSON specification for `CupelPolicy` + input/output format, ship a standalone CLI binary (`cupel run --policy policy.json --input items.json`), and provide thin SDK wrappers (NuGet, npm, PyPI) that are just JSON serializers + process wrappers.

**Why**: The LLM ecosystem is polyglot. Python and TypeScript are the dominant AI languages. A .NET-only library has a ceiling. A protocol spec + CLI is language-agnostic, embeds in any stack, and can be compiled to WASM for browser/edge use. The Wollax stack is .NET but Cupel's *ideas* transcend it.

**Scope**: Very large. Requires protocol design, cross-language testing, binary distribution. Or: start with the spec doc and generate wrappers lazily.

**Risks**:
- Explodes scope dramatically
- CLI adds latency (process spawn per LLM call is expensive)
- Maintenance burden across language SDKs
- Premature generalization before core .NET version is proven

---

## Idea 4: Learned Scoring via Outcome Feedback

**Name**: AdaptiveScorer

**What**: Every context selection is a hypothesis: "these items will help the model succeed." What if Cupel could learn which hypotheses were right? Add an optional `IOutcomeSink` interface — callers report back a signal (success/failure, quality score, user rating). Cupel accumulates this into a local lightweight model (gradient-boosted decision tree, no neural nets) that adjusts scorer weights automatically. Policy becomes a *starting point* that gets refined.

**Why**: The hardest problem in context management is that optimal selection is task-dependent. A hardcoded `RecencyScorer` is wrong for code review (need old stable files) vs. debugging (need the most recent error). Learning from outcomes collapses this problem over time without requiring the developer to hand-tune weights.

**Scope**: Large. Requires persistence layer (SQLite?), feedback loop design, and ML model training (even a tiny one). Optional module, not core.

**Risks**:
- Violates "heuristics over magic" principle — this IS magic
- State management contradicts "not storage"
- Feedback signal is hard to define correctly
- Cold start problem: no data initially, heuristics still needed
- Could learn the wrong thing if feedback signal is noisy

---

## Idea 5: CupelPolicy as a Runtime Negotiation, Not a Static Config

**Name**: PolicyNegotiation

**What**: Instead of `CupelPolicy` being a developer-authored static config, make it negotiable at runtime between the caller and a policy registry. Imagine: Cupel ships a set of named policies (`"code-review"`, `"long-context-rag"`, `"chat-summarization"`); the caller declares intent (e.g., `Cupel.ForIntent("debugging")`); Cupel selects + optionally blends the best-matching policy. Policies can be distributed as NuGet packages with semantic metadata.

**Why**: Most developers don't know how to write a good scoring policy. The learning curve is the adoption barrier. If Cupel ships 10 well-tuned named policies and lets callers opt in declaratively, adoption explodes. This turns Cupel into a policy marketplace, not just a framework.

**Scope**: Medium. Core engine unchanged. Adds: policy registry, intent-to-policy matching, community policy packages.

**Risks**:
- Policy marketplace requires curation/trust — who validates them?
- Intent matching is fuzzy — could select wrong policy silently
- Versioning and conflict resolution between policy packages
- Adds a layer of indirection that makes debugging harder

---

## Idea 6: Throw Out the Pipeline — Use a Constraint Solver

**Name**: ConstraintSolvedContext

**What**: The current pipeline (Classify → Score → Deduplicate → Slice → Place) is sequential and order-dependent. What if instead, context selection was framed as a **constraint satisfaction problem**? Express requirements declaratively: "must include system prompt", "total tokens ≤ 8192", "at least 2 recent tool results", "no duplicate topics", "prioritize items tagged `#critical`". Run a solver (ILP or SAT). Get provably optimal results.

**Why**: Pipelines leak — an item that scores poorly in step 2 gets cut before the quota logic in step 4 can save it. A solver sees the whole problem at once. You get optimality guarantees and explainability ("item X was excluded because constraint Y required item Z which used its token budget").

**Scope**: Medium. There are good .NET ILP libraries (OR-Tools has a .NET binding). Constraint DSL needs design work.

**Risks**:
- ILP is NP-hard in the worst case (though real contexts are small, so typically fast)
- Adds Google OR-Tools as a dependency (non-trivial weight)
- Constraint modeling is harder to understand than pipeline config for most devs
- Overkill for simple use cases; over-engineered feel

---

## Idea 7: Make the Output the Input — Recursive Context Optimization

**Name**: ReflexiveCupel

**What**: Cupel currently selects context for *one* LLM call. But in multi-turn agentic loops, the context selected in turn N determines what happens in turn N+1, which determines what context is *available* in turn N+2. What if Cupel could simulate forward? Given a predicted token budget and item evolution model, it pre-selects context not just for the current call but to *preserve optionality* for future calls — preferring items that keep future windows open.

**Why**: Greedy context selection causes "context traps" — the agent burns tokens on low-value items early, then has no room for critical items that arrive later. A lookahead model solves this. This is the difference between a greedy algorithm and dynamic programming.

**Scope**: Very large. Requires a simulation model of item evolution (speculative, model-dependent). Possibly only viable as a research direction.

**Risks**:
- Requires predicting the future — fundamentally speculative
- Computational cost: lookahead is expensive
- Model of item evolution would be wrong for most real sessions
- Interesting research, probably not a v1 library feature
