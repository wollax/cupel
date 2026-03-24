# Requirements

This file is the explicit capability and coverage contract for the project.

## Active

### R050 — SelectionReport structural equality
- Class: core-capability
- Status: validated
- Description: `SelectionReport`, `IncludedItem`, and `ExcludedItem` implement structural equality in both languages — `PartialEq` (Rust, not `Eq` due to f64 fields per D109) + `IEquatable<T>` (.NET). Exact f64 comparison (no epsilon). Enables programmatic comparison of reports for fork diagnostics and snapshot testing.
- Why it matters: Without structural equality, PolicySensitivityReport cannot diff reports programmatically, and snapshot testing cannot assert expected vs actual report equivalence. This is a load-bearing prerequisite for R051 and R053.
- Source: brainstorm (March 21 — M003 candidate list)
- Primary owning slice: M004/S01
- Supporting slices: none
- Validation: validated — Rust: PartialEq derived on 6 diagnostic structs + ContextBudget; 15 equality tests in `crates/cupel/tests/equality.rs`; `cargo test --all-targets` 143 passed. .NET: IEquatable on ContextItem (collection-aware), IncludedItem, ExcludedItem (null-safe DeduplicatedAgainst), SelectionReport (SequenceEqual for all lists); 28 equality tests; `dotnet test` 764 passed; PublicAPI.Unshipped.txt updated; `dotnet build` 0 warnings
- Notes: Rust uses PartialEq only (not Eq) because f64 fields prevent it (D109). .NET record compiler generates operator ==/!= from custom Equals automatically (D111). ContextBudget also received PartialEq as a transitive dependency (D110).

### R051 — PolicySensitivityReport — fork diagnostic
- Class: differentiator
- Status: validated
- Description: `RunPolicySensitivity(items, [(label, pipeline)])` executes multiple pipeline configurations over the same item set and returns both labeled `SelectionReport`s and a structured diff showing items that moved between included/excluded across variants. Both languages.
- Why it matters: Developer-time tool for answering "which items swing when I change my pipeline configuration?" — the most common question when tuning context selection policies. Thin orchestration over existing `dry_run`; ~80 lines per language.
- Source: brainstorm (March 15 — radical survivor; March 21 — promoted to M003-ready)
- Primary owning slice: M004/S02
- Supporting slices: none
- Validation: validated — Rust: `policy_sensitivity` free function in `analytics.rs` returning `PolicySensitivityReport` with `ItemStatus`, `PolicySensitivityDiffEntry`; content-keyed diff via HashMap; 2 integration tests in `policy_sensitivity.rs`; `cargo test --all-targets` 145 passed. .NET: `PolicySensitivity` static extension method in `PolicySensitivityExtensions.cs` using internal `DryRunWithBudget`; `PolicySensitivityReport`, `PolicySensitivityDiffEntry`, `ItemStatus` types; minimum-variants guard; 3 tests; `dotnet test` 767 passed; PublicAPI.Unshipped.txt updated; `dotnet build` 0 warnings
- Notes: Depends on R050 (equality) for diff computation. `dry_run` is live in both languages. Returns `[(label, SelectionReport)]` plus a structured diff (items that changed status between variants). Content-keyed matching (D113). .NET uses DryRunWithBudget (D114).

### R052 — IQuotaPolicy abstraction + QuotaUtilization
- Class: core-capability
- Status: validated
- Description: Extract a shared `IQuotaPolicy` interface from `QuotaSlice` and `CountQuotaSlice` that exposes per-kind quota constraints. Add `QuotaUtilization(report, policy)` extension method returning per-kind utilization data. Both languages.
- Why it matters: Callers tuning quota configurations need to know how close each kind is to its cap/require thresholds. Without a shared abstraction, each quota type needs its own utilization API.
- Source: brainstorm (March 21 — `QuotaUtilization` candidate) + user (chose `IQuotaPolicy` over direct `CountQuotaSlice` config)
- Primary owning slice: M004/S03
- Supporting slices: none
- Validation: validated — Rust: `QuotaPolicy` trait with `quota_constraints()` method; implemented by `QuotaSlice` (percentage mode) and `CountQuotaSlice` (count mode); `quota_utilization` free function in `analytics.rs`; `KindQuotaUtilization` struct; 4 integration tests in `quota_utilization.rs`; `cargo test --all-targets` 149 passed. .NET: `IQuotaPolicy` interface with `GetConstraints()`; implemented by both slicers; `QuotaUtilization` extension method on `SelectionReport`; `QuotaConstraint`, `QuotaConstraintMode`, `KindQuotaUtilization` types; 5 tests; PublicAPI.Unshipped.txt updated; `dotnet test` 772 passed; `dotnet build` 0 warnings; no breaking changes
- Notes: `IQuotaPolicy` must be backward-compatible — `QuotaSlice` and `CountQuotaSlice` implement it without breaking changes. Rust equivalent is a `QuotaPolicy` trait. D105 (abstraction over direct config), D116 (f64 for both modes), D117 (explicit budget parameter).

### R053 — Snapshot testing in Cupel.Testing
- Class: quality-attribute
- Status: active
- Description: Add snapshot assertion methods to `Wollax.Cupel.Testing` that serialize `SelectionReport` to JSON, compare against stored `.json` snapshot files, and support `CUPEL_UPDATE_SNAPSHOTS=1` environment variable for in-place snapshot updates. .NET only (Rust has `insta` crate).
- Why it matters: Reduces test authoring cost for callers — instead of writing 10 chained assertions, take a snapshot and diff. Previously blocked on tiebreaker rule (shipped in M003/S06) and structural equality (R050).
- Source: brainstorm (March 21 — unblocked after tiebreak shipped)
- Primary owning slice: M004/S04
- Supporting slices: none
- Validation: unmapped
- Notes: JSON format. Tiebreaker rule already shipped (D099). Depends on R050 for meaningful equality comparisons. Snapshot files stored alongside test files. Update workflow via env var, not CLI tool.

### R054 — Rust budget simulation parity
- Class: core-capability
- Status: active
- Description: Implement `get_marginal_items` and `find_min_budget_for` in the Rust `cupel` crate matching the .NET `BudgetSimulationExtensions` API. `find_min_budget_for` returns `Option<i32>`. Monotonicity guard for `QuotaSlice`/`CountQuotaSlice` inner slicers.
- Why it matters: .NET has budget simulation since M003/S06; Rust callers are second-class without parity. Agent orchestrators using the Rust crate need "what's the minimum budget to include this item?" for adaptive budget strategies.
- Source: brainstorm (March 21 — Rust parity orphan from M003/S06)
- Primary owning slice: M004/S05
- Supporting slices: none
- Validation: unmapped
- Notes: .NET implementation in `src/Wollax.Cupel/BudgetSimulationExtensions.cs`. Spec chapter in `spec/src/analytics/budget-simulation.md`. D069 (explicit budget param), D098 (API shape), D099 (tiebreak contract) all locked.

### R040 — Count-based quota design resolution
- Class: differentiator
- Status: validated
- Description: Resolve the 5 open design questions for count-based quotas in `QuotaSlice`: (1) algorithm integration with GreedySlice/KnapsackSlice, (2) tag non-exclusivity semantics for items with multiple tags, (3) pinned item interaction with minimum-count guarantees, (4) run-time vs build-time conflict detection rules, (5) KnapsackSlice compatibility path. Output: design decision record + spec-ready pseudocode. No implementation.
- Why it matters: Percentage-based quotas solve "at least 20% messages" but not "at least 3 tool results". Count-based quotas are required for agent memory scenarios where absolute minimum counts matter more than budget percentages. The design cannot be deferred further without blocking v1.3 implementation.
- Source: user
- Primary owning slice: M002/S03
- Supporting slices: M002/S01
- Validation: validated — `.planning/design/count-quota-design.md` exists with all five decision areas present; DI-1 (separate decorator), DI-2 (non-exclusive tags), DI-3 (ScarcityBehavior::Degrade + quota_violations), DI-4 (5E KnapsackSlice guard), DI-5 (slicer-scoped caps + pinned decrement), DI-6 (backward-compat audit) all settled; COUNT-DISTRIBUTE-BUDGET pseudocode written; `grep -ci "\bTBD\b"` → 0; cargo test (35 passed) and dotnet test (583 passed) both green
- Notes: Explicitly deferred from M001 brainstorm (March 15). Tag non-exclusivity and knapsack path are the two hardest sub-problems.

### R041 — Spec quality debt closure
- Class: quality-attribute
- Status: validated
- Description: Close ~8-10 open spec editorial issues: event ordering within pipeline stages, item_count sentinel disambiguation, observer callback normative status, GreedySlice zero-token item ordering note, KnapsackSlice floor vs truncation-toward-zero note, UShapedPlacer pinned edge case table row, CompositeScorer pseudocode storage assignment, ScaledScorer nesting depth warning.
- Why it matters: The spec is publicly served as an mdBook. Ambiguous ordering guarantees block conformance test vector authoring; misleading algorithm descriptions mislead new language binding implementors.
- Source: user
- Primary owning slice: M002/S02
- Supporting slices: none
- Validation: validated — all 20 spec/phase24 issue files closed; 13 spec files updated with ordering rules, normative alignment, algorithm clarifications, and reserved variant examples; cargo test (35 passed) and dotnet test (583 passed) both green; TOML drift guard satisfied
- Notes: Actual issue count was 20 (not ~8-10); `spec-workflow-checksum-verification.md` intentionally deferred (CI security concern, out of S02 scope).

### R042 — Metadata convention system spec
- Class: differentiator
- Status: validated
- Description: Define the `"cupel:<key>"` metadata namespace in the spec, establish first-class conventions (`cupel:trust` float64 [0,1] and `cupel:source-type` string enum), and write the `MetadataTrustScorer` spec chapter with conformance vector outlines. No implementation.
- Why it matters: Without a canonical namespace, every caller invents their own trust-key schema; a `MetadataTrustScorer` built on ad hoc keys is useless across projects. Reserving the namespace now enables the ecosystem to converge before anyone serializes production data with conflicting key names.
- Source: user (brainstorm March 15 — radical ideas, survived 2 rounds)
- Primary owning slice: M002/S04
- Supporting slices: none
- Validation: validated — `spec/src/scorers/metadata-trust.md` exists with `"cupel:"` namespace reserved normatively (MUST NOT); `cupel:trust` (float64 [0,1], string storage, configurable defaultScore, explicit parse-failure/non-finite handling) and `cupel:source-type` (open string, 4 RECOMMENDED values) conventions defined; 5 conformance vector outlines included; no TBD fields; `grep -ci "\bTBD\b"` → 0; `grep -q "metadata-trust" spec/src/SUMMARY.md` passes; `grep -q "MetadataTrustScorer" spec/src/scorers.md` passes; cargo test (35+78 doctests passed) and dotnet test (583 passed) both green; **M003/S02 implementation**: `MetadataTrustScorer` implemented in both Rust and .NET; all 5 conformance vector outlines have passing implementations; drift guard clean; `cargo test --all-targets` → 43 passed; `dotnet test` → 669 passed
- Notes: Trust is a scoring input, not a filter. No trust gates (silent exclusion) in this spec. cupel:source-type is a string convention for callers — no built-in scorer required.

### R043 — Cupel.Testing vocabulary design
- Class: differentiator
- Status: validated
- Description: Define 10-15 named assertion patterns over `SelectionReport` as a vocabulary spec section: what each assertion checks, tolerance/edge cases, error message format on failure. Output is a spec-ready vocabulary document — no implementation. This is the prerequisite for the Cupel.Testing NuGet package (R021).
- Why it matters: The testing vocabulary must be designed before implementation begins — shipping a testing package with ambiguous assertion semantics (e.g. "what is high-scoring?" in `PlaceHighScorersAtEdges`) creates an unstable API surface from day one.
- Source: user (brainstorm March 15 — high-value, design-phase requirement)
- Primary owning slice: M002/S05
- Supporting slices: none
- Validation: validated — `spec/src/testing/vocabulary.md` exists with 13 named assertion patterns (≥10 required); each pattern specifies what it asserts, tolerance/edge cases, tie-breaking behavior, and error message format; no undefined terms remain (`grep -ci "\bTBD\b"` → 0; "high-scoring" → 0); `PlaceItemAtEdge` defines "edge" as position 0 or count−1 exactly; `HaveBudgetUtilizationAbove` denominator locked to `budget.MaxTokens`; all predicate-bearing methods use `IncludedItem`/`ExcludedItem` per PD-1; D041 snapshot prohibition honoured with explicit rationale; `ExcludeItemWithBudgetDetails` language asymmetry (.NET flat enum) documented; chapter reachable via `spec/src/SUMMARY.md`; cargo test (35 passed) and dotnet test (583 passed) both green
- Notes: No FluentAssertions dependency; no snapshot testing (ordering stability not yet guaranteed). Vocabulary design output feeds R021 implementation.

### R044 — Future features spec chapters (DecayScorer, OTel, budget simulation)
- Class: quality-attribute
- Status: validated
- Description: Produce spec chapters for three deferred features: (a) `DecayScorer` — algorithm, TimeProvider injection pattern, null-timestamp policy, three curve factory methods, conformance vector outlines; (b) OpenTelemetry verbosity levels — exact `cupel.*` attributes per verbosity tier, pre-stability disclaimer; (c) budget simulation API contracts — `GetMarginalItems` and `FindMinBudgetFor` with monotonicity precondition spec. No implementation.
- Why it matters: Each spec chapter is the prerequisite blocking implementation. Starting implementation without spec means the API surface gets driven by Rust/C# type system constraints rather than semantic clarity — a pattern explicitly rejected in the M001 brainstorm.
- Source: user (brainstorm March 15 — high-value features)
- Primary owning slice: M002/S06
- Supporting slices: none
- Validation: validated — `spec/src/scorers/decay.md` (DECAY-SCORE pseudocode, 3 curve factories, mandatory TimeProvider per D042/D047, nullTimestampScore, 5 conformance vector outlines, 0 TBD fields), `spec/src/integrations/opentelemetry.md` (5-Activity hierarchy per D068, 3 verbosity tiers with exact cupel.* attribute tables, pre-stability disclaimer per D043, cardinality table, 0 TBD fields), and `spec/src/analytics/budget-simulation.md` (DryRun determinism MUST, GetMarginalItems with explicit budget param per D069, FindMinBudgetFor with binary search + int?/Option<i32> return per D048, QuotaSlice + CountQuotaSlice guards, 0 TBD fields) all exist and are reachable via SUMMARY.md; `grep -ci "\bTBD\b"` → 0 across all three; cargo test (113 passed) and dotnet test (583 passed) both green
- Notes: DecayScorer feeds R020; OTel feeds R022; budget simulation is a new requirement (no prior R-number). TimeProvider is mandatory (not optional) — no silent default to TimeProvider.System.

### R045 — Fresh brainstorm: post-v1.2 ideas
- Class: quality-attribute
- Status: validated
- Description: Run a new explorer/challenger brainstorm session (following the established .planning/brainstorms/ format) against the current v1.2 codebase state. Surface new ideas not yet in the backlog, validate or retire existing deferred ideas in light of v1.2 completion, and produce a refined idea register.
- Why it matters: The last brainstorm (March 15) was conducted before diagnostics parity shipped. With SelectionReport and run_traced now live, the landscape has shifted — new ideas around analytics, testing, and ecosystem become more concrete.
- Source: user
- Primary owning slice: M002/S01
- Supporting slices: none
- Validation: validated — `.planning/brainstorms/2026-03-21T09-00-brainstorm/` committed with SUMMARY.md and future-features-report.md; brainstorm fed design inputs to S03 (count-quota angles) and S06 (DecayScorer curves, OTel verbosity, budget simulation patterns); cargo test and dotnet test both green
- Notes: Brainstorm output fed count-quota design (S03) and future features spec (S06) by surfacing new angles on those problems.

## Validated (M001)

### R001 — Rust diagnostics parity: TraceCollector + SelectionReport
- Class: core-capability
- Status: validated
- Description: The Rust crate must expose a `TraceCollector` trait (with `NullTraceCollector` and `DiagnosticTraceCollector`), `TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, a `run_traced()` pipeline method, and a `DryRun` capability — matching the diagnostics spec chapter
- Why it matters: The .NET implementation has full explainability (SelectionReport, DryRun). Without Rust parity, the Rust crate is a second-class citizen that cannot serve agent orchestration use cases requiring "why was this item excluded?"
- Source: user
- Primary owning slice: M001/S03
- Supporting slices: M001/S01, M001/S02, M001/S04
- Validation: validated — TraceCollector trait + NullTraceCollector ZST + DiagnosticTraceCollector in crate root; Pipeline::run_traced() and Pipeline::dry_run() implemented; all 5 diagnostics conformance vectors pass; cargo test --features serde → 35 passed including SelectionReport round-trip; cargo doc --no-deps → 0 warnings
- Notes: Spec chapter exists in `spec/src/diagnostics/`; conformance vectors in `spec/conformance/`; per-invocation ownership model (not stored on pipeline)

### R002 — KnapsackSlice DP table size guard (Rust + .NET)
- Class: quality-attribute
- Status: validated
- Description: `KnapsackSlice` must validate that `capacity × items` does not exceed 50 million cells before allocating the DP table, returning an error (`CupelError::TableTooLarge`) if exceeded
- Why it matters: Without a guard, a caller can trivially cause an OOM crash with large token budgets and large item counts
- Source: user
- Primary owning slice: M001/S07
- Supporting slices: M001/S06
- Validation: validated — `CupelError::TableTooLarge { candidates, capacity, cells }` added; `KnapsackSlice::slice` returns `Err(TableTooLarge)` when `(capacity as u64) * (n as u64) > 50_000_000`; `knapsack_table_too_large` unit test passes; `Slicer::slice` returns `Result` throughout pipeline; .NET guard validated in S06
- Notes: Rust slice: `crates/cupel/src/slicer/knapsack.rs`; .NET slice: `src/Wollax.Cupel/Slicing/KnapsackSlice.cs`. Requires `CupelError::TableTooLarge` variant (Rust) — `#[non_exhaustive]` on CupelError already in place (RAPI-01).

### R003 — CI coverage: clippy --all-targets + cargo-deny unmaintained
- Class: quality-attribute
- Status: validated
- Description: Rust CI must run `cargo clippy --all-targets` to lint integration tests, examples, and benchmarks; `deny.toml` must flag unmaintained crates as warnings
- Why it matters: Current CI misses lint on test/example code; unmaintained crates are a supply-chain risk
- Source: user
- Primary owning slice: M001/S05
- Supporting slices: none
- Validation: validated — all four `cargo clippy` invocations in `ci-rust.yml` and `release-rust.yml` now include `--all-targets`; `deny.toml` has `unmaintained = "workspace"` under `[advisories]`; both local clippy checks (default + serde) and `cargo deny check` exit 0
- Notes: Issues: `2026-03-14-clippy-all-targets.md`, `2026-03-14-cargo-deny-unmaintained-warn.md`. Note: cargo-deny 0.19.0 uses scope values for `unmaintained` (not severity values) — `"workspace"` used instead of `"warn"` (see D030)

### R004 — .NET codebase quality hardening
- Class: quality-attribute
- Status: validated
- Description: Batch-resolve high-signal .NET issues: XML doc gaps, naming inconsistencies (OverflowStrategyValue → OverflowStrategy), defensive coding improvements, test coverage gaps (scorer test coverage, missing edge-case tests)
- Why it matters: 90 open issues; letting them accumulate makes each subsequent release harder
- Source: user
- Primary owning slice: M001/S06
- Supporting slices: none
- Validation: validated — all 20 triage items resolved in S06: OverflowStrategyValue → OverflowStrategy rename, QuotaBuilder epsilon fix, caller-facing error messages, enum integer anchors, ContextItem XML docs, interface contract docs (ITraceCollector constancy, ISlicer sort precondition, ContextResult.Report nullability, SelectionReport reference), 6 new tests (net +5 after duplicate removal); `dotnet build` 0 errors/warnings; 658 tests pass
- Notes: High-signal issues: `2026-03-14-overflow-strategy-value-naming.md`, `scorer-test-gaps.md`, `2026-03-13-phase4-pr-review-suggestions.md`, `007-contextitem-xml-docs.md`, `phase02-review-*.md`

### R005 — Rust codebase quality hardening
- Class: quality-attribute
- Status: validated
- Description: Batch-resolve high-signal Rust issues: CompositeScorer cycle detection is misleading (document or remove), UShapedPlacer/QuotaSlice panic on invariant violation, test coverage gaps, doc comment improvements
- Why it matters: Existing panic paths are correctness concerns; misleading cycle detection creates false confidence
- Source: user
- Primary owning slice: M001/S07
- Supporting slices: none
- Validation: validated — CompositeScorer DFS cycle detection removed; `Scorer::as_any` eliminated from trait and all 8 impls; `UShapedPlacer::place` refactored to explicit left/right vecs with no Vec<Option> or .expect(); 15 new unit tests added (UShapedPlacer, TagScorer, PriorityScorer, ScaledScorer, ReflexiveScorer, Pipeline); `cargo clippy --all-targets -- -D warnings` clean
- Notes: High-signal issues: `2026-03-14-composite-scorer-cycle-detection-ineffective.md`, `2026-03-14-u-shaped-placer-expect-on-option-vec.md`, `2026-03-14-quota-slice-expect-on-sub-budget.md`, `2026-03-14-unbounded-scaled-nesting-depth.md`

### R006 — Diagnostics serde coverage
- Class: quality-attribute
- Status: validated
- Description: All diagnostic types (`SelectionReport`, `TraceEvent`, `ExclusionReason`, `InclusionReason`) must support Serialize/Deserialize behind the `serde` feature flag
- Why it matters: Consistent with existing crate convention (ContextItem, ContextBudget have serde); callers need to persist or transmit diagnostic reports
- Source: user
- Primary owning slice: M001/S04
- Supporting slices: M001/S01
- Validation: validated — `cargo test --features serde` passes with wire-format assertions for all variants, round-trips for all 8 ExclusionReason and 3 InclusionReason variants, SelectionReport full round-trip, validation-rejection test, and graceful unknown-variant test; validation-on-deserialize pattern applied matching ContextBudget
- Notes: Must follow validation-on-deserialize pattern established for ContextBudget

## Validated (prior milestones)

### R010 — ContextBudget.unreserved_capacity() helper (Rust + .NET)
- Class: core-capability
- Status: validated
- Description: `ContextBudget` exposes `unreserved_capacity()` = `MaxTokens - OutputReserve - sum(ReservedSlots)` in both .NET and Rust
- Why it matters: Callers frequently need to know how much budget is actually available for content
- Source: user
- Primary owning slice: M001 (phase 23, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phase 23-03; `unreserved_capacity()` in Rust crate, `UnreservedCapacity` property in .NET

### R011 — Rust API future-proofing (#[non_exhaustive], derives)
- Class: quality-attribute
- Status: validated
- Description: `CupelError` and `OverflowStrategy` have `#[non_exhaustive]`; concrete slicer/placer structs derive `Debug`, `Clone`, `Copy`
- Why it matters: Enables additive API evolution without breaking downstream code
- Source: user
- Primary owning slice: M001 (phase 23, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phase 23-01

### R012 — ContextKind factory methods and TryFrom<&str>
- Class: core-capability
- Status: validated
- Description: `ContextKind` provides factory methods for all known kinds and implements `TryFrom<&str>` for idiomatic error propagation
- Why it matters: Ergonomic construction; error-propagating parsing instead of panic
- Source: user
- Primary owning slice: M001 (phase 23, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phase 23-02

### R013 — Conformance drift guard + vector quality
- Class: quality-attribute
- Status: validated
- Description: CI drift guard diffs `spec/conformance/` against `crates/cupel/conformance/` and fails on divergence; misleading comments in conformance vectors fixed; diagnostics conformance vector schema documented
- Why it matters: Prevents spec/implementation divergence as spec evolves
- Source: user
- Primary owning slice: M001 (phase 25, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phases 25-01, 25-02, 25-03

### R014 — Diagnostics specification chapter
- Class: core-capability
- Status: validated
- Description: Language-agnostic diagnostics spec chapter exists covering TraceCollector contract, event types, exclusion/inclusion reasons, SelectionReport structure, and ownership model
- Why it matters: Spec-first guarantees cross-language conformance; Rust implementation can be built against verifiable contracts
- Source: user
- Primary owning slice: M001 (phase 24, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phases 24-01, 24-02; lives in `spec/src/diagnostics/`

## Validated (M003)

### R020 — DecayScorer with TimeProvider injection
- Class: core-capability
- Status: validated
- Description: Built-in time-decay scorer with injectable TimeProvider for testability; three curve types: Exponential(halfLife), Window(maxAge), Step(windows); mandatory TimeProvider injection per D042; conformance vectors in spec and crate
- Why it matters: Common use case; RecencyScorer only does ordinal ranking, not true decay curves; time-decay is essential for agent memory scenarios where recency matters in absolute time, not just ordinal position
- Source: user
- Primary owning slice: M003/S01
- Supporting slices: M003/S06
- Validation: validated — DecayScorer implemented in both Rust (`crates/cupel/src/scorer/decay.rs`) and .NET (`src/Wollax.Cupel/Scoring/DecayScorer.cs`); all three curve types (Exponential, Window, Step) with validated constructors; mandatory TimeProvider injection (Rust trait + .NET System.TimeProvider BCL); 5 conformance vectors in spec/conformance/ and crates/cupel/conformance/ (drift guard diff exits 0); `cargo test --all-targets` → 45+38 passed, 0 failed; `dotnet test` → 663 passed, 0 failed
- Notes: Spec chapter designed in M002/S06 (R044); D042 (mandatory TimeProvider), D047 (Rust trait shape), D070 (Step curve semantics), D071 (Window boundary semantics) all locked; D075 (millisecond precision), D076 (age clamping), D077 (protected ctor in PublicAPI) established during implementation

### R021 — Cupel.Testing package
- Class: quality-attribute
- Status: validated
- Description: Fluent assertion chains over SelectionReport for test authoring; 13 named assertion patterns; `SelectionReport.Should()` entry point returning `SelectionReportAssertionChain`; dedicated `SelectionReportAssertionException`; new `Wollax.Cupel.Testing` NuGet package
- Why it matters: Reduces boilerplate in caller tests; without a testing vocabulary, every test must write manual LINQ predicates against SelectionReport fields
- Source: user
- Primary owning slice: M003/S04
- Supporting slices: none
- Validation: validated — `Wollax.Cupel.Testing` NuGet package implemented at `src/Wollax.Cupel.Testing/`; all 13 assertion patterns implemented in `SelectionReportAssertionChain.cs`; `SelectionReport.Should()` entry point via `SelectionReportExtensions.cs`; `SelectionReportAssertionException` seals failure messages; `dotnet pack` produces `Wollax.Cupel.Testing.*.nupkg`; 26 TUnit tests in `Wollax.Cupel.Testing.Tests` pass (2 per pattern); consumption test references package via `PackageReference Version="*-*"` from local feed; `dotnet test` → 708 passed, 0 failed; `cargo test --all-targets` → 124 passed, 0 failed
- Notes: Vocabulary design phase in M002/S05 (R043); D041 (no FluentAssertions, no snapshot testing), D065 (predicate type IncludedItem/ExcludedItem), D066 (Should() entry point + SelectionReportAssertionException) all locked; D090 (pattern 6 degenerate .NET form); D091 (direct SelectionReport construction in tests)

### R022 — OpenTelemetry bridge
- Class: operability
- Status: validated
- Description: Bridge ITraceCollector to ActivitySource for OTel integration; new `Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet companion package; 3 verbosity tiers (StageOnly, StageAndExclusions, Full); exact `cupel.*` attribute set; cardinality warning in README
- Why it matters: Production observability without custom logging; callers using Jaeger/Honeycomb/Aspire can instrument Cupel pipelines without writing their own bridge
- Source: user
- Primary owning slice: M003/S05
- Supporting slices: none
- Validation: validated — `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package implemented at `src/Wollax.Cupel.Diagnostics.OpenTelemetry/`; `CupelOpenTelemetryTraceCollector` implements `ITraceCollector` with structured `OnPipelineCompleted` handoff; `CupelOpenTelemetryVerbosity` enum (StageOnly, StageAndExclusions, Full); `AddCupelInstrumentation()` extension on `TracerProviderBuilder`; `ActivitySource("Wollax.Cupel")` emits `cupel.pipeline` root + 5 `cupel.stage.*` children with exact `cupel.*` attributes; 7 SDK-backed in-memory exporter tests pass; 7 core seam tests pass; consumption smoke test passes from local-feed nupkg; `dotnet test --configuration Release` → 737 passed, 0 failed; redaction enforced (no content/metadata leakage)
- Notes: Companion package, not core — zero-dep constraint on core preserved; D043 (cupel.* namespace, pre-stable), D068 (5 Activities, Sort omitted), D100 (structured completion handoff), D101 (SDK-backed verification), D102 (package-specific verbosity enum) all locked and implemented.

## Deferred

### R055 — ProfiledPlacer companion package
- Class: differentiator
- Status: deferred
- Description: Caller-provided LLM attention profiles for placement optimization; companion package separate from core
- Why it matters: Would allow placement to account for model-specific attention patterns instead of the generic U-shaped heuristic
- Source: brainstorm (March 15 — radical survivor)
- Primary owning slice: none
- Supporting slices: none
- Validation: unmapped
- Notes: Three blockers unchanged: requires LLM attention statistics data, separate versioning story, and no confirmed demand. Revisit when attention profile data is available.

### R056 — DryRunWithPolicy override
- Class: core-capability
- Status: deferred
- Description: Policy-explicit variant of `dry_run` for fork diagnostic callers who want to test arbitrary pipeline configurations without constructing new Pipeline instances
- Why it matters: Would simplify PolicySensitivityReport usage — callers wouldn't need to build separate Pipeline objects
- Source: brainstorm (March 21)
- Primary owning slice: none
- Supporting slices: none
- Validation: unmapped
- Notes: Defer until R051 (fork diagnostic) proves demand for this convenience API.

### R057 — TimestampCoverageReport split form
- Class: quality-attribute
- Status: deferred
- Description: Split `TimestampCoverage()` into separate Included and Excluded coverage metrics
- Why it matters: Gives callers finer-grained timestamp coverage insight per selection group
- Source: brainstorm (March 21)
- Primary owning slice: none
- Supporting slices: none
- Validation: unmapped
- Notes: Follow-on only if demand observed after M003 analytics ship.

## Out of Scope

### R030 — Storage / persistence
- Class: anti-feature
- Status: out-of-scope
- Description: Cupel does not manage conversation history, vector stores, or caches
- Why it matters: Prevents scope creep; storage is the caller's problem
- Source: user
- Primary owning slice: none
- Supporting slices: none
- Validation: n/a
- Notes: Explicitly excluded; caller passes pre-loaded items

### R031 — LLM API integration
- Class: anti-feature
- Status: out-of-scope
- Description: Cupel does not call models; it prepares context
- Why it matters: Keeps library framework-agnostic and zero-dependency
- Source: user
- Primary owning slice: none
- Supporting slices: none
- Validation: n/a
- Notes: Adapters owned by consumers (Smelt, user code)

### R032 — tracing crate integration in core
- Class: anti-feature
- Status: out-of-scope
- Description: The `tracing` crate is a companion concern, never core
- Why it matters: Zero-dep constraint; belongs in a companion crate
- Source: user
- Primary owning slice: none
- Supporting slices: none
- Validation: n/a
- Notes: Per .planning/REQUIREMENTS.md Out of Scope

## Traceability

| ID | Class | Status | Primary owner | Supporting | Proof |
|---|---|---|---|---|---|
| R001 | core-capability | validated | M001/S03 | S01, S02, S04 | validated |
| R002 | quality-attribute | validated | M001/S07 | S06 | validated |
| R003 | quality-attribute | validated | M001/S05 | none | validated |
| R004 | quality-attribute | validated | M001/S06 | none | validated |
| R005 | quality-attribute | validated | M001/S07 | none | validated |
| R006 | quality-attribute | validated | M001/S04 | S01 | validated |
| R010 | core-capability | validated | M001 phase 23 | none | validated |
| R011 | quality-attribute | validated | M001 phase 23 | none | validated |
| R012 | core-capability | validated | M001 phase 23 | none | validated |
| R013 | quality-attribute | validated | M001 phase 25 | none | validated |
| R014 | core-capability | validated | M001 phase 24 | none | validated |
| R020 | core-capability | validated | M003/S01 | M003/S06 | validated |
| R021 | quality-attribute | validated | M003/S04 | none | validated |
| R022 | operability | validated | M003/S05 | none | validated |
| R050 | core-capability | validated | M004/S01 | none | validated |
| R051 | differentiator | validated | M004/S02 | none | validated |
| R052 | core-capability | validated | M004/S03 | none | validated |
| R053 | quality-attribute | active | M004/S04 | none | unmapped |
| R054 | core-capability | active | M004/S05 | none | unmapped |
| R030 | anti-feature | out-of-scope | none | none | n/a |
| R031 | anti-feature | out-of-scope | none | none | n/a |
| R032 | anti-feature | out-of-scope | none | none | n/a |
| R040 | differentiator | validated | M002/S03 | M002/S01 | validated |
| R041 | quality-attribute | validated | M002/S02 | none | validated |
| R042 | differentiator | validated | M002/S04 | none | validated |
| R043 | differentiator | validated | M002/S05 | none | validated |
| R044 | quality-attribute | validated | M002/S06 | none | validated |
| R045 | quality-attribute | validated | M002/S01 | none | validated |

## Coverage Summary

- Active requirements: 2 (R053–R054)
- Mapped to slices: 2 (R053→M004/S04, R054→M004/S05)
- Validated: 27 (R001–R006, R010–R014, R020–R022, R040–R045, R050–R052)
- Unmapped active requirements: 0
