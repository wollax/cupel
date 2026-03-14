---
status: passed
score: 4/4
---

# Phase 8 Verification

## Must-Haves

### 1. `CupelPolicy` is a declarative config object that fully specifies a pipeline (scorers + weights, slicer, placer, budget, quotas)
**Status:** ✅ Verified
**Evidence:** `src/Wollax.Cupel/CupelPolicy.cs` is a sealed class with constructor-validated properties: `Scorers` (IReadOnlyList<ScorerEntry>), `SlicerType`, `PlacerType`, `DeduplicationEnabled`, `OverflowStrategy`, `KnapsackBucketSize`, `Quotas` (IReadOnlyList<QuotaEntry>), `Name`, and `Description`. All properties carry `[JsonPropertyName]` attributes for Phase 9 serialization readiness. Note: per the locked phase context decision, budget is intentionally excluded from `CupelPolicy` — it remains per-invocation via `WithBudget()`. Quotas control budget allocation percentages per `ContextKind`. Validation at construction: non-empty scorers required; `KnapsackBucketSize` only valid with `SlicerType.Knapsack`; positive bucket size enforced. `PipelineBuilder.WithPolicy(CupelPolicy)` translates the policy into scorer instances, slicer, placer, deduplication, overflow strategy, and quota constraints.

### 2. `CupelOptions.AddPolicy("intent", policy)` enables intent-based lookup; explicit policy construction also works
**Status:** ✅ Verified
**Evidence:** `src/Wollax.Cupel/CupelOptions.cs` implements a runtime registry backed by a case-insensitive `Dictionary<string, CupelPolicy>`. `AddPolicy(string intent, CupelPolicy policy)` returns `this` for fluent chaining. `GetPolicy(string intent)` throws `KeyNotFoundException` for unknown intents. `TryGetPolicy(string intent, out CupelPolicy?)` provides the non-throwing variant. All three methods guard against null/whitespace intents. `CupelOptionsTests.cs` verifies round-trip, case-insensitive lookup, fluent chaining, overwrite semantics, and all guard-clause paths. Explicit policy construction (via `new CupelPolicy(...)`) also works, confirmed by `PolicyIntegrationTests.PolicyBuilt_MatchesManualBuilt()`.

### 3. 7+ named presets exist (chat, code-review, rag, document-qa, tool-use, long-running, debugging) — each marked `[Experimental]`
**Status:** ✅ Verified
**Evidence:** `src/Wollax.Cupel/CupelPresets.cs` defines a `static class CupelPresets` with exactly 7 static factory methods, each decorated with `[Experimental("CUPELxxx")]`: `Chat()` (CUPEL001), `CodeReview()` (CUPEL002), `Rag()` (CUPEL003), `DocumentQa()` (CUPEL004), `ToolUse()` (CUPEL005), `LongRunning()` (CUPEL006), `Debugging()` (CUPEL007). Each method returns a `CupelPolicy` with tuned scorer weights, slicer choice, placer strategy, and description appropriate to the use case. `DocumentQa` uses `SlicerType.Knapsack` with `knapsackBucketSize: 100` and `PlacerType.UShaped`; `Rag` uses `PlacerType.UShaped`; all others use `SlicerType.Greedy` and `PlacerType.Chronological`.

### 4. Named presets compile, produce valid pipelines, and serve as test fixtures — each preset has at least one integration test
**Status:** ✅ Verified
**Evidence:** `tests/Wollax.Cupel.Tests/Pipeline/PolicyIntegrationTests.cs` contains a dedicated `Preset_<Name>_BuildsWorkingPipeline()` test for all 7 presets (Chat, CodeReview, Rag, DocumentQa, ToolUse, LongRunning, Debugging). Each calls the shared helper `VerifyPresetWorks(CupelPolicy)` which builds a pipeline with `CupelPipeline.CreateBuilder().WithBudget(...).WithPolicy(policy).Build()`, executes it against a realistic 10-item fixture, and asserts non-empty results within the token budget. Additional tests cover: policy-built vs manual-built equivalence, U-shaped placer ordering, knapsack budget fitting, and quota enforcement. `CupelPresetsTests.cs` provides 14 structural validation tests covering scorer types and weights for each preset. Build: `dotnet build --warnaserror` succeeded. Tests: 529/529 passed.

## Build & Test Results

- `dotnet build --warnaserror`: **passed** (no warnings treated as errors)
- `dotnet test`: **529 passed, 0 failed, 0 skipped** (435ms)
