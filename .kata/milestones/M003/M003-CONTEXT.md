# M003: v1.3 Implementation Sprint — Context

**Gathered:** 2026-03-23
**Status:** Ready for planning

## Project Description

Cupel is a dual-language (.NET + Rust) context management library for coding agents. It selects
the optimal context window from candidate items given a token budget, using a fixed 5-stage
pipeline (Classify → Score → Deduplicate → Slice → Place) with full explainability via
`SelectionReport`. Published to nuget.org (4 packages) and crates.io.

## Why This Milestone

M002 produced complete, TBD-free spec chapters for six features. Every feature has a locked
design and spec-level conformance vectors. M003 is the pure implementation sprint against those
specs — no design work is needed; the choices are already locked.

The deferred requirements (R020, R021, R022) are now unblocked:
- R020 (DecayScorer): full spec + Rust TimeProvider trait shape locked (D042, D047)
- R021 (Cupel.Testing): vocabulary spec with 13 assertion patterns locked (D041, D065, D066)
- R022 (OTel bridge): 5-Activity hierarchy, exact attribute names, 3 verbosity tiers locked (D043, D068)

Additionally: CountQuotaSlice (R040 impl), MetadataTrustScorer (R042 impl), budget simulation
analytics (GetMarginalItems, FindMinBudgetFor), and core analytics extension methods
(BudgetUtilization, KindDiversity, TimestampCoverage) are all spec-ready.

## User-Visible Outcome

### When this milestone is complete, the user can:

- Construct a `DecayScorer` with any of three decay curves (Exponential, Window, Step) and inject
  a `TimeProvider` for deterministic testing; use it in any pipeline that previously used `RecencyScorer`
- Use `SelectionReport.Should().IncludeItemWithKind(ContextKind.Message)...` chains in tests without
  writing manual LINQ predicates; install `Wollax.Cupel.Testing` from nuget.org
- Bridge a Cupel pipeline to OpenTelemetry via `Wollax.Cupel.Diagnostics.OpenTelemetry`, choosing
  `StageOnly`, `StageAndExclusions`, or `Full` verbosity; see `cupel.*` activities in Jaeger/Honeycomb/etc.
- Call `GetMarginalItems(items, budget, slackTokens)` and `FindMinBudgetFor(items, budget, target, ceiling)`
  directly on any pipeline instance via extension methods
- Compose a `CountQuotaSlice` wrapping any inner slicer to enforce absolute minimum/maximum counts
  per `ContextKind` (e.g., "at least 3 tool results")
- Score items via `MetadataTrustScorer` using the `cupel:trust` float convention

### Entry point / environment

- Entry point: NuGet packages (`Wollax.Cupel`, `Wollax.Cupel.Testing`, new `Wollax.Cupel.Diagnostics.OpenTelemetry`) + crates.io (`cupel`)
- Environment: .NET 10 + Rust projects that already use Cupel v1.2
- Live dependencies involved: none (companion packages bring OpenTelemetry and xUnit — out-of-scope for core)

## Completion Class

- Contract complete means: all unit tests pass (cargo test + dotnet test); all new conformance vectors pass; all must-haves in each slice verified by grep + test output
- Integration complete means: OTel bridge registered in a test pipeline produces real Activity/Event output readable by OpenTelemetry SDK
- Operational complete means: none (library — no service lifecycle)

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- `cargo test` → all tests pass; new DecayScorer, MetadataTrustScorer, CountQuotaSlice, and BudgetUtilization tests included and green
- `dotnet test` → all tests pass; Cupel.Testing assertion chains, OTel bridge ActivitySource output, and budget simulation extension methods all verified
- NuGet `Wollax.Cupel.Testing` package builds and can be installed standalone; `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package produces real OTel trace output in a test harness
- Conformance test vectors for DecayScorer and MetadataTrustScorer pass in both languages

## Risks and Unknowns

- **OTel ActivitySource companion package structure**: Creating a new NuGet package (`Wollax.Cupel.Diagnostics.OpenTelemetry`) requires CI/CD config changes (new csproj, new release job). Risk is in project wiring, not algorithm — the spec is locked. — Retire in S05 by producing the companion package and verifying it installs independently.
- **Rust `chrono` dependency for DecayScorer**: `DateTime<Utc>` requires the `chrono` crate. Check if `chrono` is already in the dependency graph; if not, adding it is a manifest change that needs version pinning and MSRV check. — Retire in S01.
- **CountQuotaSlice KnapsackSlice guard**: D052 mandates build-time rejection of `CountQuotaSlice + KnapsackSlice` combination. Ensuring the guard is implemented correctly without false positives for valid inner-slicer composition needs care. — Retire in S03.
- **Cupel.Testing package standalone installability**: The `Wollax.Cupel.Testing` package needs the `Wollax.Cupel` core package as a dependency. CI must build and pack it separately from Core/DI/Tiktoken/Json. — Retire in S04.

## Existing Codebase / Prior Art

- `spec/src/scorers/decay.md` — Locked spec chapter; DecayScorer algorithm, 3 curve factories, conformance vector outlines
- `spec/src/scorers/metadata-trust.md` — Locked spec chapter; MetadataTrustScorer, `cupel:trust` convention, 5 conformance vectors
- `spec/src/testing/vocabulary.md` — Locked spec chapter; 13 assertion patterns, SelectionReportAssertionChain entry point
- `spec/src/integrations/opentelemetry.md` — Locked spec chapter; 5-Activity hierarchy, exact attribute names, cardinality table
- `spec/src/analytics/budget-simulation.md` — Locked spec chapter; GetMarginalItems, FindMinBudgetFor, DryRun determinism
- `.planning/design/count-quota-design.md` — Locked design; COUNT-DISTRIBUTE-BUDGET pseudocode, 6 DI rulings
- `crates/cupel/src/scorer/` — Existing scorer implementations (RecencyScorer, PriorityScorer, etc.) to follow for DecayScorer structure
- `src/Wollax.Cupel/Scorers/` — Existing .NET scorer implementations to follow for MetadataTrustScorer
- `src/Wollax.Cupel/Slicers/QuotaSlice.cs` — Pattern for building CountQuotaSlice decorator
- `crates/cupel/src/slicer/` — Existing Rust slicer implementations for CountQuotaSlice structure
- `src/Wollax.Cupel/` — Core package; BudgetUtilization, KindDiversity, TimestampCoverage extension methods go here
- `crates/cupel/conformance/` — Existing conformance vectors (TOML); new vectors for DecayScorer and MetadataTrustScorer added here
- `.github/workflows/release-dotnet.yml` — Release workflow to extend for new packages

> See `.kata/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R020 — DecayScorer implementation (spec in M002/S06)
- R021 — Cupel.Testing package implementation (vocabulary in M002/S05)
- R022 — OTel bridge companion package implementation (spec in M002/S06)
- R040 — CountQuotaSlice implementation (design in M002/S03; was design-only in M002)
- R042 — MetadataTrustScorer implementation (spec in M002/S04; was spec-only in M002)

## Scope

### In Scope

- DecayScorer in Rust and .NET (3 curve types: Exponential, Window, Step; TimeProvider injection; conformance vectors)
- MetadataTrustScorer in Rust and .NET (`cupel:trust` float convention; `cupel:source-type` string convention; conformance vectors)
- CountQuotaSlice decorator in Rust and .NET (COUNT-DISTRIBUTE-BUDGET algorithm; ScarcityBehavior::Degrade default; CountCapExceeded + CountRequireCandidatesExhausted ExclusionReason variants; SelectionReport.CountRequirementShortfalls field)
- Core analytics extension methods in Wollax.Cupel and cupel crate: `BudgetUtilization(budget)`, `KindDiversity()`, `TimestampCoverage()`
- Budget simulation extension methods on Pipeline: `GetMarginalItems(items, budget, slackTokens)`, `FindMinBudgetFor(items, budget, target, ceiling)` — .NET only first (Rust parity decision in S06 planning)
- `Wollax.Cupel.Testing` NuGet package with 13 assertion patterns from vocabulary spec
- `Wollax.Cupel.Diagnostics.OpenTelemetry` companion NuGet package with 3 verbosity tiers and exact `cupel.*` attribute set
- Conformance test vectors for DecayScorer and MetadataTrustScorer in spec + crate
- Tiebreaker rule for GreedySlice (score ties broken by id ascending) — prerequisite for snapshot testing unlock

### Out of Scope / Non-Goals

- Snapshot testing in Cupel.Testing (deferred; requires tiebreak rule to be stable first, then assessed)
- PolicySensitivityReport / fork diagnostic (M003+ backlog; no confirmed demand yet)
- `QuotaUtilization` extension method (depends on S03 defining `IQuotaPolicy`; assess after CountQuotaSlice ships)
- DryRunWithPolicy core API change (M003+ backlog)
- `ProfiledPlacer` companion package (M003+; no LLM attention profiles available)
- TypeScript / JS binding (no decision made; not in this milestone)
- IntentSpec preset selector (rejected; 20 lines of caller code)
- Rust budget simulation parity (assessment deferred to S06 planning)
- OTel Rust bridge (companion package is .NET only for now; Rust has TraceCollector instead)

## Technical Constraints

- MSRV 1.85 (Rust Edition 2024)
- .NET 10 target framework
- Zero external deps in `Wollax.Cupel` core (D045 placement rule holds — analytics methods on SelectionReport go in core)
- `Wollax.Cupel.Diagnostics.OpenTelemetry` may take `OpenTelemetry` SDK dependency — this is expected for a companion package
- `Wollax.Cupel.Testing` may take xUnit or test framework dependency if needed for assertions — independent NuGet package
- `#[non_exhaustive]` on ExclusionReason was already set; verify before adding CountCapExceeded and CountRequireCandidatesExhausted variants
- D055: Count-quota tag semantics are non-exclusive and non-configurable — do not add a per-policy toggle
- D056: ScarcityBehavior::Degrade is default; Throw is opt-in

## Integration Points

- OTel SDK (`OpenTelemetry` / `System.Diagnostics.ActivitySource`) — companion package wraps `ITraceCollector` bridge into real `ActivitySource`; cardinality warning in README
- NuGet publish — two new packages require OIDC trusted publishing entries in release-dotnet.yml and new project files
- crates.io — DecayScorer and MetadataTrustScorer ship in the same `cupel` crate; no new crate published

## Open Questions

- Should `FindMinBudgetFor` and `GetMarginalItems` ship in Rust in M003? — Current thinking: defer Rust parity to M003+; the .NET implementation proves the API surface; add to scope if S06 planning reveals it's straightforward
- Is `SelectionReport.CountRequirementShortfalls` the right field name in both languages, or does Rust use `count_requirement_shortfalls`? — Follow existing snake_case convention in Rust; confirm in S03 planning
