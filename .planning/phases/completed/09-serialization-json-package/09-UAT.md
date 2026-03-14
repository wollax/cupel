# Phase 9: Serialization & JSON Package — UAT

**Date:** 2026-03-14
**Status:** Passed (10/10)

## Tests

| # | Test | Status | Notes |
|---|------|--------|-------|
| 1 | Minimal CupelPolicy serializes to valid JSON and deserializes back with identical properties | PASS | All defaults preserved (Greedy, Chronological, Dedup=true, Throw) |
| 2 | Full CupelPolicy (all scorers, quotas, metadata, knapsack, UShaped) round-trips | PASS | 3 scorers, kindWeights, tagWeights, quotas, name, description all survive |
| 3 | ContextBudget with reservedSlots round-trips via DeserializeBudget | PASS | Equals=true, all 5 properties verified individually |
| 4 | Enums appear as camelCase strings in JSON output (recency, greedy, uShaped, truncate) | PASS | No PascalCase in output |
| 5 | Null optional fields (quotas, name, description) are omitted from JSON output | PASS | WhenWritingNull correctly omits nulls |
| 6 | Unknown JSON properties in input are silently ignored | PASS | Forward-compatible deserialization |
| 7 | RegisterScorer stores factory and HasScorerFactory returns true | PASS | Fluent API, GetScorerFactory, RegisteredScorerNames all work |
| 8 | Unknown scorer type in JSON throws descriptive error listing all known types | PASS | Lists all 6 built-in types + suggests RegisterScorer() |
| 9 | Empty/null/malformed JSON inputs produce clear errors (not NullReferenceException) | PASS | ArgumentNullException for null, JsonException for empty/malformed/null-literal |
| 10 | Constructor validation errors (negative weight, empty scorers) produce JsonException with context | PASS | Wraps ArgumentException in JsonException with $: prefix |
