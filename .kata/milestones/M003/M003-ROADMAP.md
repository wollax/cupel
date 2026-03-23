# M003: v1.3 Implementation Sprint

**Vision:** Implement every feature designed in M002 — DecayScorer, MetadataTrustScorer, CountQuotaSlice, core analytics extension methods, budget simulation, Cupel.Testing vocabulary package, and OTel bridge companion package — in both .NET and Rust (where applicable), with conformance vectors and published NuGet packages. All spec chapters are locked; this milestone executes against them.

## Success Criteria

- `cargo test` passes with new DecayScorer, MetadataTrustScorer, CountQuotaSlice, and analytics extension method tests included and green
- `dotnet test` passes with new Cupel.Testing assertion chain tests, OTel bridge ActivitySource output tests, and budget simulation extension method tests included and green
- `Wollax.Cupel.Testing` NuGet package installs independently and exposes `SelectionReport.Should()` chains
- `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package produces real OTel Activity/Event output at all three verbosity tiers in a test harness
- DecayScorer and MetadataTrustScorer conformance vectors pass in both languages via the existing drift guard
- Tiebreaker rule (score ties → id ascending) is spec-committed and implemented in GreedySlice in both languages

## Key Risks / Unknowns

- `chrono` crate dependency for DecayScorer Rust implementation — may not be in the existing dependency graph
- New NuGet package project wiring for `Wollax.Cupel.Testing` and `Wollax.Cupel.Diagnostics.OpenTelemetry` — requires new csproj files, NuGet metadata, and release-dotnet.yml changes
- CountQuotaSlice `#[non_exhaustive]` audit on ExclusionReason before adding new variants — precondition that must be verified before S03 implementation

## Proof Strategy

- `chrono` dependency risk → retire in S01 (DecayScorer Rust) by verifying Cargo.lock and completing DecayScorer with `cargo test` passing
- New NuGet package wiring risk → retire in S04 (Cupel.Testing) by building and packing the new package; S05 (OTel) builds the second new package confirming the pattern works twice
- ExclusionReason `#[non_exhaustive]` audit → retire in S03 T01 by running `grep -r non_exhaustive` before adding new variants

## Verification Classes

- Contract verification: `cargo test --all-targets`, `dotnet test`, `grep`-based artifact checks, conformance vector drift guard
- Integration verification: OTel bridge ActivitySource output verified in a test harness that registers the source and captures Activities; Cupel.Testing package installs and runs against a real SelectionReport
- Operational verification: none (library — no service lifecycle)
- UAT / human verification: none required; all must-haves are mechanically checkable

## Milestone Definition of Done

This milestone is complete only when all are true:

- All 6 slices are complete with summaries written and verified
- `cargo test` and `dotnet test` both pass with all new feature tests included
- DecayScorer and MetadataTrustScorer conformance vectors committed to spec and crate
- `Wollax.Cupel.Testing` package builds and installs independently
- `Wollax.Cupel.Diagnostics.OpenTelemetry` package produces real OTel output in a test harness
- Tiebreaker rule is committed to the spec and implemented in GreedySlice
- `BudgetUtilization`, `KindDiversity`, `TimestampCoverage` are callable extension methods in both languages
- `GetMarginalItems` and `FindMinBudgetFor` ship in .NET; Rust parity decision documented

## Requirement Coverage

- Covers: R020, R021, R022 (all implementation of M002-designed features)
- Partially covers: R040, R042 (M002 produced designs; M003 provides implementation)
- Leaves for later: none — all deferred requirements are addressed in this milestone
- Orphan risks: Rust budget simulation parity deferred to M003+ (assessment in S06 planning)

## Slices

- [x] **S01: DecayScorer — Rust + .NET implementation** `risk:high` `depends:[]`
  > After this: A `DecayScorer` with Exponential, Window, and Step curves can be constructed in both .NET and Rust, injected with a `TimeProvider`/`TimeProvider` for testing, and produces correct scores verified by conformance vectors and unit tests in both languages.

- [x] **S02: MetadataTrustScorer — Rust + .NET implementation** `risk:medium` `depends:[S01]`
  > After this: A `MetadataTrustScorer` using the `cupel:trust` float convention and `cupel:source-type` string convention can be constructed in both .NET and Rust, producing correct scores verified by conformance vectors and unit tests in both languages.

- [x] **S03: CountQuotaSlice — Rust + .NET implementation** `risk:high` `depends:[S01]`
  > After this: A `CountQuotaSlice` decorator wrapping any inner slicer enforces absolute minimum/maximum counts per ContextKind in both .NET and Rust; CountCapExceeded and CountRequireCandidatesExhausted ExclusionReason variants are emitted; SelectionReport.CountRequirementShortfalls reports unmet minimums; ScarcityBehavior::Degrade is the default.

- [x] **S04: Core analytics + Cupel.Testing package** `risk:medium` `depends:[S01,S02,S03]`
  > After this: `BudgetUtilization(budget)`, `KindDiversity()`, and `TimestampCoverage()` are callable extension methods on SelectionReport in both languages; `Wollax.Cupel.Testing` NuGet package installs independently and exposes all 13 assertion patterns from the vocabulary spec via `SelectionReport.Should()` chains; tests green.

- [x] **S05: OTel bridge companion package** `risk:high` `depends:[S04]`
  > After this: `Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet companion package can be installed and configured to bridge any Cupel pipeline to OpenTelemetry's ActivitySource; a test harness verifies real Activity and Event output at all three verbosity tiers (StageOnly, StageAndExclusions, Full) with exact `cupel.*` attribute names matching the spec.

- [x] **S06: Budget simulation + tiebreaker + spec alignment** `risk:low` `depends:[S04]`
  > After this: `GetMarginalItems` and `FindMinBudgetFor` are callable extension methods on the .NET Pipeline; the tiebreaker rule (score ties → id ascending) is committed to the GreedySlice spec chapter and implemented in both languages; all spec cross-references for M003 features are updated (SUMMARY.md, scorers.md index, slicers.md, changelog); Rust budget simulation parity decision is documented.

## Boundary Map

### S01 — DecayScorer

Produces:
- Rust: `DecayScorer` struct in `crates/cupel/src/scorer/decay.rs` implementing `Scorer` trait; `TimeProvider` trait with `SystemTimeProvider` ZST; `DecayCurve` enum with `Exponential(half_life)`, `Window(max_age)`, `Step(windows)` variants; re-exported from `crates/cupel/src/lib.rs`
- Rust: DecayScorer conformance vectors in `crates/cupel/conformance/` and `spec/conformance/`
- .NET: `DecayScorer` class in `src/Wollax.Cupel/Scorers/DecayScorer.cs` implementing `IScorer`; `ITimeProvider` with `SystemTimeProvider` and `FakeTimeProvider` (for tests); `DecayCurve` sealed hierarchy; exported from Wollax.Cupel package
- Both: `chrono` dependency added to Cargo.toml if not present; MSRV verified

Consumes:
- nothing (first scorer slice; uses existing Scorer trait/IScorer interface)

### S02 — MetadataTrustScorer

Produces:
- Rust: `MetadataTrustScorer` struct in `crates/cupel/src/scorer/metadata_trust.rs` implementing `Scorer` trait; re-exported from lib.rs
- Rust: MetadataTrustScorer conformance vectors in `crates/cupel/conformance/` and `spec/conformance/`
- .NET: `MetadataTrustScorer` class in `src/Wollax.Cupel/Scorers/MetadataTrustScorer.cs` implementing `IScorer`; accepts both `double` and `string` for `cupel:trust` value per D059; exported from Wollax.Cupel package

Consumes:
- Established scorer pattern from S01 (Rust `Scorer` trait impl structure, .NET `IScorer` impl structure)

### S03 — CountQuotaSlice

Produces:
- Rust: `CountQuotaSlice` struct in `crates/cupel/src/slicer/count_quota.rs` implementing `Slicer` trait; `ScarcityBehavior` enum; `CountCapExceeded` and `CountRequireCandidatesExhausted` `ExclusionReason` variants; `count_requirement_shortfalls` field on `SelectionReport`; re-exported from lib.rs
- .NET: `CountQuotaSlice` class in `src/Wollax.Cupel/Slicers/CountQuotaSlice.cs` implementing `ISlicer`; `ScarcityBehavior` enum; `CountCapExceeded` and `CountRequireCandidatesExhausted` `ExclusionReason` variants; `CountRequirementShortfalls` property on `SelectionReport`
- Both: KnapsackSlice guard (D052): throw at construction if inner slicer is KnapsackSlice

Consumes:
- Established slicer pattern from existing QuotaSlice / GreedySlice implementations
- ExclusionReason `#[non_exhaustive]` audit (must verify before adding variants)

### S04 — Core analytics + Cupel.Testing

Produces:
- Rust: `BudgetUtilization`, `KindDiversity`, `TimestampCoverage` extension functions on `SelectionReport` in `cupel` crate
- .NET: `BudgetUtilization(ContextBudget)`, `KindDiversity()`, `TimestampCoverage()` extension methods on `SelectionReport` in `Wollax.Cupel`
- .NET: `Wollax.Cupel.Testing` NuGet package — `SelectionReportAssertionChain`, `SelectionReportAssertionException`, all 13 assertion patterns, entry point `SelectionReport.Should()` — new csproj at `src/Wollax.Cupel.Testing/`

Consumes:
- `SelectionReport` stable type (established through M001/M002)
- `CountRequirementShortfalls` from S03 (for any count-quota related assertion patterns if included)

### S05 — OTel bridge companion package

Produces:
- .NET: `Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet companion package — new csproj at `src/Wollax.Cupel.Diagnostics.OpenTelemetry/`; `CupelOpenTelemetryTraceCollector` class implementing `ITraceCollector`; `AddCupelInstrumentation()` extension on `TracerProviderBuilder`; exact `cupel.*` attribute set per spec; 3 verbosity tiers (StageOnly, StageAndExclusions, Full); cardinality warning in README
- .NET: release-dotnet.yml updated to pack and publish both new packages

Consumes:
- `ITraceCollector` / `SelectionReport` from `Wollax.Cupel` core (S04)
- New package project wiring pattern established in S04 (Wollax.Cupel.Testing)

### S06 — Budget simulation + tiebreaker + spec alignment

Produces:
- .NET: `GetMarginalItems(IReadOnlyList<ContextItem>, ContextBudget, int)` and `FindMinBudgetFor(IReadOnlyList<ContextItem>, ContextBudget, ContextItem, int)` extension methods on Pipeline in `Wollax.Cupel`; `int?` return type for FindMinBudgetFor; QuotaSlice guard; monotonicity precondition documented
- Both: Tiebreaker rule implemented in GreedySlice (score ties → id ascending); spec chapter for GreedySlice updated
- Spec: SUMMARY.md, scorers.md, slicers.md updated for CountQuotaSlice, DecayScorer, MetadataTrustScorer; changelog updated; Rust budget simulation parity decision documented

Consumes:
- Pipeline/DryRun APIs from M001/M002 (already stable)
- Analytics extension methods from S04
