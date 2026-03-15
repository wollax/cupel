# Quick-Wins Report: Cupel MVP

**Session**: 2026-03-10T15-06
**Participants**: explorer-quickwins, challenger-quickwins
**Rounds**: 2 debate rounds
**Status**: Consolidated and agreed

---

## Executive Summary

After 2 rounds of debate, 4 ideas survived pressure-testing as the Cupel MVP quick-win set. Three ideas were deferred or re-homed. Total estimated effort: ~5.5 days for a production-honest, debuggable, immediately adoptable library.

**One open architectural decision requires early resolution** (see below).

---

## Agreed MVP: 4 Quick Wins

### 1. OverflowStrategy Enum + Callback (0.5 days)

**What**: An explicit `OverflowStrategy` enum on `CupelPolicy` controlling behavior when pinned/required items alone exceed the budget, paired with an optional observer callback:

```csharp
policy.OnBudgetOverflow = OverflowStrategy.Throw; // Throw | Truncate | Proceed
policy.OnBudgetOverflowCallback = ctx => logger.Warn($"Budget exceeded by {ctx.TokenDelta} tokens");
```

The enum **forces a behavioral decision** at configuration time. The callback is pure observability — it never controls flow.

**Why it survived debate**: The original "callback only" design was correctly identified as deferring the design decision to every caller (inconsistent behavior across integrations). Splitting control flow (enum) from observability (callback) resolves this cleanly. Silent drops in production are the most common source of "why did my LLM forget X" bugs.

**Design notes**:
- Default strategy should be `Proceed` for zero-surprise behavior on first use
- `BudgetOverflowContext` exposes: pinned items, requested items, token delta
- Callback is opt-in; null-check gates the call site

---

### 2. Token Counting Factory with Clear Contract (1 day)

**What**: Token counting responsibility belongs to the caller. `ContextItem` stores `Tokens` as a required `int` (pre-computed). A factory utility makes this ergonomic:

```csharp
var item = ContextItem.Create(content, kind: ContextKind.Document, tokenizer: myCounterFunc);
// Convenience approximations for tests/prototyping:
ContextItem.Create(content, kind: ContextKind.Document, tokenizer: Tokenizers.WordCount);
ContextItem.Create(content, kind: ContextKind.Document, tokenizer: Tokenizers.CharDiv4);
```

The pipeline **never touches raw content strings** for token counting. `Func<string, int>` is a utility contract, not a pipeline concern.

**Why it survived debate**: The original proposal had ambiguous contract placement (`ContextBudget` vs. pipeline-time). Resolving it to "caller always pre-computes" eliminates all ambiguity, keeps the pipeline dependency-free, and makes the library usable with any tokenizer (tiktoken, cl100k, Llama-specific encoders) at zero coupling cost.

**Open decision (flag to team)**: Does `ContextItem` retain `Content` after construction, or discard it after token counting? This affects:
- Dry-run output fidelity (can excluded items show their content?)
- Memory footprint for large context sets
- Future extensibility (reranking, truncation)

Recommend: retain `Content` as nullable `string?` — null means "content discarded after counting." Users opt into retention by leaving it set.

---

### 3. Scoped SelectionReport / Dry-Run (2 days)

**What**: A `DryRun()` method returning a `SelectionReport` without executing side effects:

```csharp
var report = pipeline.DryRun(candidates, budget);
// report.Included   → ContextItem[] with scores
// report.Excluded   → ExcludedItem[] with Reason enum
// report.TotalTokens, report.BudgetRemaining
```

`ExclusionReason` enum values (matching actual slicer behavior):
- `InsufficientBudget` — item would fit on its own but budget was exhausted by higher-scoring items
- `Deduplicated` — removed by the deduplication stage
- `Displaced` — ranked lower than items that consumed remaining budget

**Explicitly out of scope for v1**: Per-scorer breakdown. Adding that requires changing the `IScorer` interface to return a result struct instead of `float` — a breaking API change. Per-scorer breakdown is v1.1 after the scorer interface stabilizes.

**Why it survived debate**: The challenger correctly identified that "pipeline already computes scores, dry-run just captures state" understated the complexity if per-scorer breakdown was included. Scoping to "final composite score + inclusion/exclusion reason" makes it genuinely a 2-day bolt-on without touching scorer contracts. This is still the most valuable debugging tool the library can ship — "did the right items make it in?" answers 80% of production debugging questions.

**Design notes**:
- `SelectionReport` is a value record — no dependency on pipeline internals
- Dry-run should produce identical selection results to a real run (same determinism guarantee)
- Document explicitly: per-scorer breakdown requires `IScorer` interface changes (v1.1)

---

### 4. Policy Presets — [Experimental] (2 days)

**What**: Four static factory methods covering the most common LLM patterns:

```csharp
CupelPolicy.ChatSession()    // recency-heavy, drops old turns first
CupelPolicy.DocumentQA()     // pinned system prompt + scored document chunks
CupelPolicy.AgentLoop()      // tool results + working memory, quota slicing
CupelPolicy.CodeReview()     // priority on recent diff hunks, recency on context
```

Each returns a fully configured `CupelPolicy` as a starting point.

**Why it survived debate**: The challenger raised valid versioning concerns (what happens when `ChatSession()` default weights change?). Resolution: apply .NET 8's `[Experimental]` attribute to all preset methods + XML doc note: "Preset weights are opinionated defaults subject to change in minor versions. For stable behavior, construct your policy explicitly." The intended upgrade path is copy-paste: users who need stability extract the preset configuration into their own code.

**Design notes**:
- Presets are "living documentation" of idiomatic usage — they also serve as test fixtures
- XML docs must include "use when..." guidance so users pick the right preset
- No `[Obsolete]` churn — if a preset changes substantially, it gets a new name

---

## Deferred / Re-homed Ideas

### Fluent Builder API — Deferred to post-MVP
Building a fluent façade on a still-changing core API creates maintenance debt from day 1. Every core API change cascades to the builder. Correct call: add the fluent builder once the underlying types stabilize in v1.x. Not a quick win at this stage.

### Markdown Frontmatter Factory — Re-homed to `Cupel.Extensions.Markdown`
Core should have zero file-format awareness. `ContextItem.FromMarkdown()` belongs in a separate optional package. The "50 lines of regex" estimate was naive — basic YAML frontmatter has enough edge cases (multiline values, quoted strings with colons, flow sequences) to require either YamlDotNet (dependency) or a carefully scoped bespoke parser (documented limitations). Neither belongs in core.

### JSON Policy Serialization — Descoped, re-framed
The original scope ("arbitrary policy to JSON") has a fatal flaw: custom scorers can't serialize without a plugin registry. Schema versioning must exist on day 1 or v1 ships unmigratable config files into production repos. Re-framed scope for v2: **preset portability only** — serialize the 4 built-in presets with a mandatory `"version": "1"` field. This is a narrower, safer feature that doesn't compromise the custom scorer story.

---

## Open Architectural Decision (Flag to Team Lead)

**Does `ContextItem` retain its `Content` string after construction?**

This is a cross-cutting decision that affects token counting factory design, dry-run output fidelity, memory footprint, and future truncation features. It should be resolved before implementation starts — not discovered during PR review.

**Recommendation**: `Content` is `string?`, nullable, retained by default. Users who want to discard content after counting call `item.DiscardContent()` explicitly. This keeps the common case simple and makes dry-run maximally useful.

---

## Implementation Order

Given dependencies and risk profile, recommend shipping in this order:

1. **OverflowStrategy + callback** (0.5d) — no dependencies, establishes error handling contract
2. **Token counting factory** (1d) — depends on `Content` retention decision
3. **Scoped dry-run** (2d) — depends on stable pipeline execution path
4. **Policy presets** (2d) — depends on all of the above (presets need correct budget behavior)

**Total: ~5.5 days**
