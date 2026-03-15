# Quick-Win Ideas for Cupel MVP

**Session**: 2026-03-10T15-06
**Author**: explorer-quickwins
**Focus**: Low-effort, high-impact ideas that ship fast and prove value

---

## Idea 1: Pre-built Policy Templates ("Batteries Included")

**Name**: Policy Presets

**What**: Ship 4 static factory methods as named presets:
- `CupelPolicy.ChatSession()` — recency-heavy, drops old turns first
- `CupelPolicy.DocumentQA()` — pinned system prompt + scored chunks
- `CupelPolicy.AgentLoop()` — tool results and working memory with quota slicing
- `CupelPolicy.CodeReview()` — priority on recent diff hunks, recency on context

Each returns a fully configured `CupelPolicy` ready to use, covering the 4 most common LLM patterns.

**Why**: First-5-minute DX determines adoption. If a dev can paste 2 lines and have correct context management, they'll try it. Without presets, they must understand the full pipeline before getting value — that's a churn cliff. Presets also serve as living documentation of idiomatic usage.

**Scope**: ~2 days. Each preset is just a composed configuration; no new architecture needed. Add XML docs and the work is done.

**Risks**:
- Presets become stale or opinionated — mitigation: treat them as starting points, easily overridable.
- Poor naming leads to misuse (wrong preset for the use case) — mitigation: XML docs with "use when..." guidance.

---

## Idea 2: Tokenizer-Agnostic Token Budget via Delegate

**Name**: Pluggable Token Counting

**What**: Accept a `Func<string, int>` token counter at the `ContextBudget` or `ContextItem` construction boundary. Users wire in their own tokenizer (tiktoken, cl100k, LLM-specific encoders) with zero coupling. Provide a simple `WordCount` and `CharDiv4` approximations as built-ins for tests and prototyping.

**Why**: Token counting is the biggest DX friction point for a context management library. If Cupel can't count tokens without a tokenizer dependency, every integration requires boilerplate. A delegate contract: zero new dependencies, works with any tokenizer the consuming app already has, makes Cupel testable with trivial approximations. This is the right architectural call — and shipping it early bakes in the right contract before breaking changes become costly.

**Scope**: ~1 day. It's an interface/delegate decision, not implementation work. The hard part is deciding the contract boundary (item level? budget level? both?).

**Risks**:
- Callers passing incorrect counters silently produces wrong budgets — mitigation: debug-mode validation that spot-checks consistency.
- Per-item delegate invocation overhead at scale — mitigation: cache computed token counts on `ContextItem`.

---

## Idea 3: Fluent Builder API

**Name**: Fluent Policy Builder

**What**: A chainable builder so policies read like specs:

```csharp
var policy = CupelPolicy
    .WithBudget(maxTokens: 8000, outputReserve: 1000)
    .Score(RecencyScorer.Default, weight: 0.6)
    .Score(PriorityScorer.Default, weight: 0.4)
    .Slice(SliceStrategy.Greedy)
    .Pin(ContextKind.SystemPrompt)
    .Build();
```

**Why**: The declarative pipeline is conceptually simple but assembling it through constructors is verbose and requires knowing all 5 pipeline stages. A fluent API makes the happy path obvious and self-documenting. It also lowers the barrier to custom scorer composition, which is a key differentiator vs. rolling your own.

**Scope**: ~2-3 days. Pure façade — wraps existing types, no new logic. Can be done incrementally (start with Budget + Score + Slice, add the rest later).

**Risks**:
- Builder diverges from direct construction, creating two APIs to maintain — mitigation: builder delegates entirely to the same underlying types, no duplication.
- Builder hides important configuration options — mitigation: keep direct construction as the power-user path, document both.

---

## Idea 4: SelectionReport / Dry-Run Mode

**Name**: Transparent Scoring Report

**What**: A `DryRun()` method on the pipeline that returns a `SelectionReport` instead of the final `ContextItem[]`. The report shows:
- Per-item scores (by scorer) and final composite score
- Why each item was included or excluded (budget, score threshold, dedup)
- Total tokens used vs. budget
- Placement order rationale

```csharp
var report = pipeline.DryRun(candidates, budget);
// report.Included, report.Excluded, report.ScoreBreakdown
```

**Why**: "Zero LLM opinions, transparent scoring" is in the success criteria. Without dry-run, transparency is a marketing claim; with it, it's a feature. This is also the #1 debugging tool users will want — when their LLM produces a bad response, they'll blame the context selection. A report lets them verify the math. Differentiates Cupel from ad-hoc token-trimming hacks immediately.

**Scope**: ~2 days. The pipeline already computes scores — dry-run just captures the intermediate state instead of discarding it.

**Risks**:
- Report schema becomes a breaking surface if pipeline internals change — mitigation: keep report as a value type/record with clear versioning intent.
- Performance overhead if callers run dry-run in production — mitigation: document as a dev/debug feature; it's not on the hot path.

---

## Idea 5: JSON-Serializable Policy Config

**Name**: Policy as Config File

**What**: Make `CupelPolicy` fully serializable to/from JSON (System.Text.Json, no extra deps). Policies stored as `.cupel.json` files, checked into version control, loaded at runtime:

```json
{
  "budget": { "maxTokens": 8000, "outputReserve": 1000 },
  "scorers": [
    { "type": "recency", "weight": 0.6 },
    { "type": "priority", "weight": 0.4 }
  ],
  "slicer": "greedy"
}
```

**Why**: Ops/ML teams want to tune context behavior without redeploying code. JSON config enables A/B testing of policies, environment-specific overrides (dev vs. prod budgets), and non-developer contribution to context strategy. It also forces the architecture to remain serializable — a healthy constraint that prevents accidental coupling to runtime state.

**Scope**: ~2 days. System.Text.Json with discriminated union support for scorer types. The tricky part is custom scorer plugins — defer those to v2, ship built-ins first.

**Risks**:
- Custom scorers can't serialize without a plugin registry — mitigation: v1 only serializes built-in scorer types; custom scorers remain code-only.
- JSON policy drift from code reality — mitigation: provide a validation step that catches unknown fields and type mismatches at load time.

---

## Idea 6: ContextItem Markdown Frontmatter Factory

**Name**: Markdown-Native Context Items

**What**: `ContextItem.FromMarkdown(string content)` parses YAML frontmatter to populate metadata:

```markdown
---
kind: document
priority: high
tags: [auth, security]
source: docs/auth-flow.md
---
# Auth Flow
JWT tokens are issued at login...
```

This eliminates the most tedious part of integrating Cupel with document pipelines: mapping document metadata to `ContextItem` fields.

**Why**: Most LLM document workflows already have markdown with frontmatter (Obsidian, MDX, GitHub wikis, docusaurus). Making Cupel speak that format natively means zero integration code for a huge class of use cases. It also positions Cupel as document-workflow-native, which is a real market segment.

**Scope**: ~1 day. Parse frontmatter (a simple YAML header) with a lightweight parser or YamlDotNet (single dep). Map known keys to `ContextItem` fields; put remaining keys in tags.

**Risks**:
- Adding a YAML dependency conflicts with the "minimal dependencies" non-goal — mitigation: implement a tiny bespoke frontmatter parser (frontmatter is simple enough to parse with 50 lines of regex/string splitting). No YamlDotNet needed.
- Frontmatter conventions vary (Jekyll vs. Hugo vs. Obsidian) — mitigation: support the common intersection (key: value pairs) and document deviations.

---

## Idea 7: Budget Overflow Callbacks / Diagnostics Hook

**Name**: OnBudgetExceeded Callback

**What**: An optional `Action<BudgetOverflowContext>` callback on `CupelPolicy` triggered when pinned or required items alone exceed the budget. `BudgetOverflowContext` exposes what was pinned, what was requested, and the token delta. The default behavior (silent drop) remains — the callback is opt-in.

**Why**: Silent drops in production are debugging nightmares. "Why did my LLM forget the system prompt?" is a real support ticket category. A callback lets callers log, alert, or fallback gracefully — without Cupel taking an opinion on what to do. This is the difference between a library that's trustworthy in production and one that's fine for demos.

**Scope**: ~0.5 day. One delegate on the policy, one call site in the pipeline. Trivial to add, high diagnostic value.

**Risks**:
- Callback is invoked on the hot path — mitigation: it's opt-in; null-check gates the call.
- Callers throw in callbacks, corrupting pipeline state — mitigation: wrap callback invocation in try/catch, log and continue.

---

## Summary Table

| # | Idea | Effort | Impact | Confidence |
|---|------|--------|--------|------------|
| 1 | Policy Presets | 2d | High — DX/adoption | High |
| 2 | Pluggable Token Counting | 1d | High — core correctness | High |
| 3 | Fluent Builder API | 2-3d | Medium-High — DX | Medium |
| 4 | SelectionReport / Dry-Run | 2d | High — transparency | High |
| 5 | JSON Policy Serialization | 2d | Medium — ops teams | Medium |
| 6 | Markdown Frontmatter Factory | 1d | Medium — document workflows | Medium |
| 7 | Budget Overflow Callback | 0.5d | High — production reliability | High |

**Top 3 recommendations**: Ideas 2, 7, and 4 — in that order. They're fastest to ship, hardest to retrofit later, and speak directly to the success criteria (budget compliance, transparency, sub-millisecond overhead). Presets and the fluent builder should come right after as the DX layer.
