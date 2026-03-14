---
status: complete
phase: 08-policy-system-named-presets
source: 08-01-SUMMARY.md, 08-02-SUMMARY.md, 08-03-SUMMARY.md
started: 2026-03-14T04:30:00Z
updated: 2026-03-14T04:45:00Z
---

## Current Test

[testing complete]

## Tests

### 1. CupelPolicy rejects invalid construction
expected: CupelPolicy with empty scorers throws ArgumentException; with knapsackBucketSize on Greedy slicer throws ArgumentException
result: pass

### 2. ScorerEntry validates weight and type-specific config
expected: ScorerEntry with zero/negative/NaN weight throws; Tag type without TagWeights throws; valid entries construct successfully
result: pass

### 3. QuotaEntry validates percent ranges
expected: QuotaEntry with neither min nor max throws; out-of-range values throw; min > max throws
result: pass

### 4. Seven named presets exist with [Experimental] attributes
expected: CupelPresets.Chat() through .Debugging() each return valid CupelPolicy; calling without #pragma warning disable produces experimental diagnostic
result: pass

### 5. CupelOptions intent-based lookup
expected: AddPolicy("chat", policy) then GetPolicy("CHAT") returns same policy (case-insensitive); GetPolicy("unknown") throws KeyNotFoundException
result: pass

### 6. PipelineBuilder.WithPolicy() builds working pipeline
expected: builder.WithPolicy(policy).WithBudget(budget).Build() succeeds; pipeline.Execute(items) returns non-empty result within budget
result: pass

### 7. Policy-built pipeline matches manual-built
expected: A policy with Recency(1), Greedy, Chronological produces identical output to manually-built equivalent with same config
result: pass

### 8. Each preset builds a working pipeline
expected: All 7 presets (Chat, CodeReview, Rag, DocumentQa, ToolUse, LongRunning, Debugging) build pipelines that execute successfully with realistic items
result: pass

### 9. WithPolicy replaces previous scorers
expected: AddScorer().WithPolicy() uses only the policy's scorers, not a blend of both — Build() does not throw "Cannot mix"
result: pass

### 10. Policy with quotas enforces constraints
expected: Policy with QuotaEntry(Message, minPercent: 40) ensures message tokens >= 40% of target budget in output
result: pass

## Summary

total: 10
passed: 10
issues: 0
pending: 0
skipped: 0

## Gaps
