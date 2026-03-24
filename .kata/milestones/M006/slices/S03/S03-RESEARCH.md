# S03: Integration proof + summaries — Research

**Researched:** 2026-03-24
**Domain:** Rust/C# cross-language slicer composition, PublicAPI analyzer, requirement validation
**Confidence:** HIGH

## Summary

S03 is a low-risk integration and close-out slice. Both `CountQuotaSlice` implementations are fully working: Rust (S01) and .NET (S02) each have 5 passing conformance integration tests. The remaining work is surgical:

1. **Composition tests** (`CountQuotaSlice` wrapping `QuotaSlice` in both languages) — no new implementation code; purely new test files. The composition pattern is already documented in the design doc and both languages already have all the building blocks.
2. **`PublicAPI.Unshipped.txt` audit** — S02 verified the file is unchanged from before M006. All M006 public surface is already listed there. The audit confirms it is complete and correct, not that it needs editing.
3. **R061 validation** in `REQUIREMENTS.md` and **M006 summaries** — prose writing, no code.
4. **Final regression check** — `cargo test --all-targets` + `cargo clippy` in Rust; `dotnet test --solution Cupel.slnx` in .NET. Both are currently green (Rust: 158 passed, .NET: 782 passed).

The primary risk for composition tests is correctly assembling the `CountQuotaSlice(inner: QuotaSlice(inner: GreedySlice))` chain — the inner slicer pipeline is `Box<dyn Slicer>` in Rust and `ISlicer` in .NET, so wrapping is straightforward. The composition just needs a real `DryRun()`/`dry_run()` call to prove the combined report is sane.

## Recommendation

Write two thin integration tests — one per language — that construct a `CountQuotaSlice` wrapping a `QuotaSlice` wrapping `GreedySlice`, run a real pipeline, and assert that both count constraints and percentage constraints have visible effects on the `SelectionReport`. Then do the `PublicAPI.Unshipped.txt` audit, validate R061, and write summaries. Do not introduce new production code — this slice is exclusively new tests and documentation.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Rust `CountQuotaSlice + QuotaSlice` composition test pattern | `crates/cupel/tests/quota_utilization.rs` — already constructs `CountQuotaSlice` against `Pipeline`; mirrors `crates/cupel/tests/conformance.rs` for slicer construction | Identical `Pipeline::builder().with_slicer().build()` + `dry_run()` pattern; copy the item-construction helpers |
| .NET `CountQuotaSlice + QuotaSlice` composition test | `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` `Run()` helper | The `Run()` helper is a near-copy starting point — just swap the inner slicer from `new GreedySlice()` to `new QuotaSlice(new GreedySlice(), quotas)` |
| `PublicAPI.Unshipped.txt` completeness check | `grep` against all M006 types and run `dotnet build Cupel.slnx` | PublicAPI analyzer fails the build if any public surface is undeclared; green build proves completeness |
| Rust `quota_utilization` with `CountQuotaSlice` | `crates/cupel/tests/quota_utilization.rs` L107-143 — already has 2 `CountQuotaSlice` utilization tests | Confirmed passing in S01; only check is regression — no new test code needed |

## Existing Code and Patterns

- `crates/cupel/tests/quota_utilization.rs` — already tests `CountQuotaSlice` + `quota_utilization`; tests use `Pipeline::builder()` with direct `dry_run()` calls; lines 107-143 are the canonical `CountQuotaSlice`-with-policy pattern in Rust
- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — `Run()` static helper pattern: builds pipeline with `CountQuotaSlice(new GreedySlice(), entries, scarcity)`, calls `DryRun()`, returns `ContextResult`; composition test follows same pattern with `QuotaSlice` as inner
- `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs` — shows how `CountQuotaSlice` implements `IQuotaPolicy`/`GetConstraints()` and is exercised via `.QuotaUtilization(policy, budget)` on the report
- `crates/cupel/src/analytics.rs` L101 — `quota_utilization(report, policy, budget)` free function; `CountQuotaSlice` implements `QuotaPolicy` (line 422 in `count_quota.rs`)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — contains all M006 public surface including `CountQuotaSlice`, `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`, `ExclusionReason.CountCapExceeded`, `ExclusionReason.CountRequireCandidatesExhausted`, `SelectionReport.CountRequirementShortfalls`; also includes `CountQuotaSlice.GetConstraints()` for `IQuotaPolicy`
- `crates/cupel/src/slicer/quota.rs` L286 — `is_quota()` returns `true` on `QuotaSlice`; `CountQuotaSlice` has `is_count_quota()` returning `true` (L417 in `count_quota.rs`)

## Constraints

- `CountQuotaSlice` does NOT support `KnapsackSlice` as the inner slicer — construction throws at runtime; the composition test must use `QuotaSlice(GreedySlice)` as the inner, not `QuotaSlice(KnapsackSlice)` (D052)
- In Rust, `Slicer::slice` has no `TraceCollector` parameter — `CountCapExceeded` exclusions are not observable at the Rust slicer level (D086); composition tests in Rust must verify via `dry_run()` at pipeline level, not by inspecting slicer output directly
- `PublicAPI.Unshipped.txt` already contains all M006 public surface (verified in S02 — `git diff` showed no output); the audit is a confirmation step, not an editing step
- `CountRequireCandidatesExhausted = 9` is declared in `PublicAPI.Unshipped.txt` and in the .NET `ExclusionReason.cs` enum; it is NOT currently emitted by the implementation (per D046 — reserved variant); this is correct and expected — no fix needed
- Solution file is `Cupel.slnx` (not `cupel.sln`); all .NET full-solution commands use `dotnet build Cupel.slnx` / `dotnet test --solution Cupel.slnx`
- `.NET` `CountQuotaSlice.LastShortfalls` is `internal` — only accessible within `Wollax.Cupel` assembly; composition tests in `Wollax.Cupel.Tests` must use `DryRun()` report fields to inspect shortfalls, not `LastShortfalls` directly (D087)
- `ReflexiveScorer` is mandatory in `.NET PipelineBuilder` even when `CountQuotaSlice` drives ranking; `FutureRelevanceHint` feeds the score field (pattern from S02)

## Common Pitfalls

- **Forgetting `WithScorer(new ReflexiveScorer())` in .NET** — `PipelineBuilder.Build()` throws if no scorer is registered; this is required even for composition tests (S02 forward intelligence)
- **Asserting exact composition order** — `QuotaSlice` uses percentage of token budget for per-kind allocation; in a composition scenario, `CountQuotaSlice` pre-commits items, then `QuotaSlice` distributes the residual budget under percentage constraints; items excluded by the count cap will not reach `QuotaSlice`; test design must account for the two-phase interaction
- **Checking `CountCapExceeded` in Rust at slicer level** — not possible without a `TraceCollector` parameter on `Slicer::slice` (D086); verify via `dry_run()` report instead: check `report.excluded` for `ExclusionReason::CountCapExceeded { .. }` using discriminant matching or `matches!()`
- **`PublicAPI.Unshipped.txt` must ship to `PublicAPI.Shipped.txt` only on release** — the file naming convention is correct; do not move entries to Shipped during S03; that happens on the actual package release

## Open Risks

- **Composition test outcome is predictable but not pre-verified** — `CountQuotaSlice(inner: QuotaSlice)` has not been exercised end-to-end in any existing test; the composition is architecturally sound (QuotaSlice wraps GreedySlice, CountQuotaSlice wraps any non-Knapsack slicer) but requires one integration run to confirm no unexpected panics or constraint conflicts at the integration point
- **`quota_utilization` with `CountQuotaSlice` as inner + outer policy** — in a composed pipeline, the `IQuotaPolicy`/`QuotaPolicy` passed to `quota_utilization` is either the `CountQuotaSlice` or the `QuotaSlice`; they report different constraint modes (count vs percentage); composition does not combine them into a single policy — caller must call `quota_utilization` twice (once per policy). This is correct behavior per D105 but needs no new test — both are already independently covered

## Current Test Baseline

- Rust: 158 tests passed, 0 failed (`cargo test --all-targets` on latest commit)
- .NET: 782 tests passed, 0 failed (`dotnet test --solution Cupel.slnx`)
- S03 will add: ~1 Rust integration test + ~1 .NET integration test for composition

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust | (none relevant) | none needed |
| .NET / C# | (none relevant) | none needed |

## Sources

- Codebase: `crates/cupel/tests/quota_utilization.rs` — CountQuotaSlice + QuotaPolicy test patterns (HIGH)
- Codebase: `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — .NET integration test pattern (HIGH)
- Codebase: `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — full M006 public surface inventory (HIGH)
- Design doc: `.planning/design/count-quota-design.md` — composition semantics, two-phase algorithm (HIGH)
