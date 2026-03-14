# Phase 14 Verification: Policy Type Completeness

**Status:** passed
**Score:** 5/5 must-haves verified
**Build:** zero warnings (`dotnet build --no-incremental -warnaserror` passed)
**Tests:** 641/641 passed, 0 failed, 0 skipped

---

## Must-Have 1: `ScorerType.Scaled` enum value exists; `CupelPolicy` can declare a `ScaledScorer` wrapping any inner scorer

**Status:** verified

- `src/Wollax.Cupel/ScorerType.cs:35-36` — `Scaled` enum member with `[JsonStringEnumMemberName("scaled")]` attribute
- `src/Wollax.Cupel/ScorerEntry.cs:37-39` — `InnerScorer` property (`ScorerEntry?`) with `[JsonPropertyName("innerScorer")]`
- `src/Wollax.Cupel/ScorerEntry.cs:80-90` — Constructor validates: Scaled requires InnerScorer, non-Scaled rejects InnerScorer
- `src/Wollax.Cupel/PipelineBuilder.cs:270-272` — `CreateScorer` Scaled case: `new ScaledScorer(CreateScorer(entry.InnerScorer ...))`
- Test coverage: `tests/Wollax.Cupel.Tests/Policy/ScorerEntryTests.cs:131-178` — ValidConstruction, validation, nested Scaled, and null-default tests

---

## Must-Have 2: `SlicerType.Stream` enum value exists; `CupelPolicy` can declare `StreamSlice` configuration

**Status:** verified

- `src/Wollax.Cupel/SlicerType.cs:18-20` — `Stream` enum member with `[JsonStringEnumMemberName("stream")]` attribute
- `src/Wollax.Cupel/CupelPolicy.cs:44-46` — `StreamBatchSize` property (`int?`) with `[JsonPropertyName("streamBatchSize")]`
- `src/Wollax.Cupel/CupelPolicy.cs:119-136` — Constructor validates: StreamBatchSize only allowed with Stream slicer; positive check; Quotas+Stream combination rejected
- `src/Wollax.Cupel/PipelineBuilder.cs:220-223` — `WithPolicy` Stream case: sets sync fallback via `UseGreedySlice()` then wires `WithAsyncSlicer(new StreamSlice(policy.StreamBatchSize ?? 32))`
- Test coverage: `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs:172-242` — default null, with batch size, null batch size, zero/negative batch size throws, wrong slicer type throws, quotas+stream throws

---

## Must-Have 3: JSON round-trip of policies containing `ScaledScorer` and `StreamSlice` succeeds

**Status:** verified

- `src/Wollax.Cupel.Json/CupelJsonSerializer.cs:16-24` — `BuiltInScorerTypes` is now derived via reflection from `[JsonStringEnumMemberName]` attributes on all `ScorerType` members (enum-derived), automatically including `Scaled`
- Test coverage in `tests/Wollax.Cupel.Json.Tests/RoundTripTests.cs`:
  - `ScaledScorer_RoundTrips` (line 199) — simple Scaled wrapping Recency
  - `NestedScaledScorer_RoundTrips` (line 221) — doubly-nested Scaled→Scaled→Priority
  - `StreamSlice_WithBatchSize_RoundTrips` (line 253) — Stream slicer with explicit batch size
  - `StreamSlice_NullBatchSize_RoundTrips` (line 267) — Stream slicer without batch size
  - `MixedPolicy_ScaledScorerAndStreamSlicer_RoundTrips` (line 282) — both features together in one policy
  - `NullOptionalFields_OmittedInJson` (line 181) — confirms `"innerScorer"` and `"streamBatchSize"` absent when null

---

## Must-Have 4: DI-resolved scorers, slicers, and placers are singletons — verified by reference equality test

**Status:** verified

- `src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs:66` — `AddKeyedSingleton<PolicyComponents>` caches scorer/slicer/placer/asyncSlicer in one singleton record
- `src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs:93` — `AddKeyedTransient<CupelPipeline>` wraps the singleton components per-resolve
- `src/Wollax.Cupel/CupelPipeline.cs:27-32` — `internal` accessor properties (`Scorer`, `Slicer`, `Placer`, `AsyncSlicer`) exposed via `InternalsVisibleTo` in `Wollax.Cupel.csproj:9-11`
- Test coverage in `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs`:
  - `AddCupelPipeline_ComponentsAreSingletons_SameInstanceAcrossResolves` (line 201) — `ReferenceEquals` checks Scorer, Slicer, Placer across two transient pipeline resolves
  - `AddCupelPipeline_ScaledScorerPolicy_ComponentsAreSingletons` (line 224) — same check for a policy using `ScorerType.Scaled`
  - `AddCupelPipeline_StreamPolicy_AsyncSlicerIsSingleton` (line 257) — additionally checks `AsyncSlicer` is non-null and same reference across two resolves

---

## Must-Have 5: All existing tests continue to pass

**Status:** verified

```
Test run summary: Passed!
  total: 641
  failed: 0
  succeeded: 641
  skipped: 0
  duration: 1s 533ms
```

All four test assemblies passed:
- `Wollax.Cupel.Tests` — 641 total (includes unit and integration tests)
- `Wollax.Cupel.Json.Tests` — serialization and round-trip tests
- `Wollax.Cupel.Extensions.DependencyInjection.Tests` — DI lifetime and singleton tests
- `Wollax.Cupel.Tiktoken.Tests` — token counter tests

---

## Observations

- The `BuiltInScorerTypes` array in `CupelJsonSerializer` is derived via `Enum.GetValues<ScorerType>()` reflection at class load — this approach automatically picks up future enum additions without manual maintenance. The "scaled" name is thus auto-included and the unknown-scorer error message will always list current enum members.
- `WithPolicy` Stream case installs a `GreedySlice` as the sync slicer fallback. This means `Execute()` works on Stream-configured pipelines (using greedy), while `ExecuteStreamAsync()` uses the `StreamSlice`. This design is intentional and documented in a `// sync fallback for Execute()` comment at `PipelineBuilder.cs:221`.
- The `AsyncSlicer` property on `CupelPipeline` and the `PolicyComponents` record correctly carry the async slicer through the singleton path, ensuring `StreamSlice` is built once and reused across transient pipeline instances.
