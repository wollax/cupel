# Requirements

This file is the explicit capability and coverage contract for the project.

## Active

### R058 — Rust OpenTelemetry bridge (cupel-otel crate)
- Class: operability
- Status: validated
- Description: `cupel-otel` companion crate implementing `TraceCollector` that bridges pipeline execution data to the `opentelemetry` API. Emits `cupel.pipeline` root span + 5 `cupel.stage.*` child spans at three verbosity tiers (StageOnly, StageAndExclusions, Full) matching the spec in `spec/src/integrations/opentelemetry.md`. Requires additive `on_pipeline_completed` hook added to `TraceCollector` trait in core `cupel` crate.
- Why it matters: Rust callers have no OTel equivalent to `Wollax.Cupel.Diagnostics.OpenTelemetry`. Production Rust services using Cupel cannot observe pipeline execution in Jaeger, Honeycomb, or any OTel-compatible backend. Closes the parity gap.
- Source: user
- Primary owning slice: M008/S01 (core hook), M008/S02 (crate), M008/S03 (packaging + spec)
- Supporting slices: none
- Validation: `crates/cupel-otel/tests/integration.rs` — 5 integration tests (source_name_is_cupel, hierarchy_root_and_five_stage_spans, stage_only_no_events, stage_and_exclusions_emits_exclusion_events, full_emits_included_item_events_on_place); `cd crates/cupel-otel && cargo test --all-targets` passes; `cd crates/cupel && cargo test --all-targets` passes; `cd crates/cupel-otel && cargo package --no-verify` exits 0.
- Notes: Separate crate (not feature flag). Direct `opentelemetry` API (not `tracing` bridge). Full 3-tier parity with .NET. Core cupel stays zero-dep. Canonical source name: `"cupel"`.

### R061 — CountQuotaSlice: count-based quota enforcement
- Class: core-capability
- Status: validated
- Description: `CountQuotaSlice` decorator slicer enforcing absolute item-count requirements (`require_count`) and per-kind item caps (`cap_count`) before delegating to an inner slicer. Two-phase algorithm: Phase 1 commits top-N candidates by score for each required kind; Phase 2 distributes the residual budget via the inner slicer with cap enforcement. Non-exclusive tag semantics (multi-tag items count toward all matching constraints). Scarcity degrades gracefully by default (`ScarcityBehavior::Degrade`). Both Rust and .NET.
- Why it matters: `QuotaSlice` controls token budget fractions; `CountQuotaSlice` controls item cardinality. Callers with hard cardinality requirements (e.g., "always include at least 2 tool results") cannot express this with percentage constraints alone. Composable with `QuotaSlice` for combined count + percentage constraints.
- Source: user
- Primary owning slice: M006/S01 (Rust), M006/S02 (.NET)
- Supporting slices: M006/S03
- Validation: validated — Rust: 5 conformance integration tests in crates/cupel/tests/conformance.rs; CountCapExceeded in report.excluded + count_requirement_shortfalls in report via dry_run() proven in S01; CountQuotaSlice+QuotaSlice composition proven in crates/cupel/tests/count_quota_composition.rs; cargo test --all-targets passes. .NET: 5 conformance integration tests in CountQuotaIntegrationTests.cs; CountCapExceeded + CountRequirementShortfalls in DryRun() report proven in S02; CountQuotaSlice+QuotaSlice composition proven in CountQuotaCompositionTests.cs; dotnet test --solution Cupel.slnx passes; PublicAPI.Unshipped.txt complete; dotnet build 0 warnings
- Notes: Design doc at `.planning/design/count-quota-design.md` is authoritative. D040, D046, D052–D057, D084–D087 are all locked. `CountConstrainedKnapsackSlice` deferred to later milestone (D052).

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
- Status: validated
- Description: Add snapshot assertion methods to `Wollax.Cupel.Testing` that serialize `SelectionReport` to JSON, compare against stored `.json` snapshot files, and support `CUPEL_UPDATE_SNAPSHOTS=1` environment variable for in-place snapshot updates. .NET only (Rust has `insta` crate).
- Why it matters: Reduces test authoring cost for callers — instead of writing 10 chained assertions, take a snapshot and diff. Previously blocked on tiebreaker rule (shipped in M003/S06) and structural equality (R050).
- Source: brainstorm (March 21 — unblocked after tiebreak shipped)
- Primary owning slice: M004/S04
- Supporting slices: none
- Validation: validated — `MatchSnapshot(string name)` method on `SelectionReportAssertionChain` using `[CallerFilePath]` for snapshot path resolution; `SnapshotMismatchException` with structured fields (snapshotName, snapshotPath, expected, actual); JSON serialization via `System.Text.Json` (camelCase, indented, enum strings); `CUPEL_UPDATE_SNAPSHOTS=1` env var triggers in-place rewrite; snapshots at `{callerDir}/__snapshots__/{name}.json`; 5 lifecycle tests in `SnapshotTests.cs` prove create→match→fail→update→no-update cycle; `dotnet test` 777 passed; PublicAPI.Unshipped.txt updated; `dotnet build` 0 warnings
- Notes: JSON format. Tiebreaker rule already shipped (D099). Depends on R050 for meaningful equality comparisons. Snapshot files stored alongside test files. Update workflow via env var, not CLI tool. D119 (internal MatchSnapshotCore for testability), D120 (System.Text.Json, no Cupel.Json dep), D121 ([NotInParallel] for env var isolation).

### R054 — Rust budget simulation parity
- Class: core-capability
- Status: validated
- Description: Implement `get_marginal_items` and `find_min_budget_for` in the Rust `cupel` crate matching the .NET `BudgetSimulationExtensions` API. `find_min_budget_for` returns `Option<i32>`. Monotonicity guard for `QuotaSlice`/`CountQuotaSlice` inner slicers.
- Why it matters: .NET has budget simulation since M003/S06; Rust callers are second-class without parity. Agent orchestrators using the Rust crate need "what's the minimum budget to include this item?" for adaptive budget strategies.
- Source: brainstorm (March 21 — Rust parity orphan from M003/S06)
- Primary owning slice: M004/S05
- Supporting slices: none
- Validation: validated — `Pipeline::get_marginal_items` and `Pipeline::find_min_budget_for` implemented as `impl Pipeline` methods; `is_quota()` and `is_count_quota()` defaulted trait methods on `Slicer` with overrides in `QuotaSlice` and `CountQuotaSlice`; content-based matching (D113); binary search checks both low and high boundaries (matching .NET); 9 integration tests in `crates/cupel/tests/budget_simulation.rs`; `cargo test --all-targets` 158 passed; `cargo clippy --all-targets -- -D warnings` clean
- Notes: .NET implementation in `src/Wollax.Cupel/BudgetSimulationExtensions.cs`. Spec chapter in `spec/src/analytics/budget-simulation.md`. D069 (explicit budget param), D098 (API shape), D099 (tiebreak contract), D122-D125 (S05 implementation decisions) all locked.

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

## Validated (M005)

### R060 — cupel-testing crate: Rust testing vocabulary
- Class: core-capability
- Status: validated
- Description: Separate `cupel-testing` crate published to crates.io implementing the 13 spec assertion patterns from `spec/src/testing/vocabulary.md`. Fluent chain API: `report.should().include_item_with_kind(kind)`. Panics on failure (standard Rust test convention). No snapshot support — callers use `insta` directly.
- Why it matters: .NET has `Wollax.Cupel.Testing` with 13 assertions + snapshots. Rust callers currently hand-roll assertions or use raw `assert!()` — there is no idiomatic testing vocabulary for `SelectionReport`. This closes the parity gap for Rust callers writing pipeline tests.
- Source: user
- Primary owning slice: M005/S02
- Supporting slices: M005/S01, M005/S03
- Validation: validated — all 13 spec assertion patterns implemented on `SelectionReportAssertionChain` with 26+1 integration tests (26 per-pattern + 1 chained end-to-end); `cargo package` exits 0 for `cupel-testing`; both crates `cargo test --all-targets` + clippy clean; M005/S03 complete
- Notes: Separate crate (not feature flag). Fluent chain (`report.should()`). Panic on failure. No snapshots (D107 — Rust callers use `insta`). D126 (separate crate), D127 (fluent chain), D128 (panic on failure).

## Active

### R062 — CountConstrainedKnapsackSlice: count-constrained knapsack selection
- Class: core-capability
- Status: active
- Description: `CountConstrainedKnapsackSlice` slicer implementing count requirements and caps using a two-phase pre-processing approach: Phase 1 commits the top-N items per constrained kind by score descending (satisfying `require_count`), Phase 2 runs standard `KnapsackSlice` on the remaining candidates with the residual budget, and Phase 3 enforces `cap_count` by dropping over-cap items from the knapsack output. Construction-time validation of `require_count <= cap_count`. Scarcity degradation (default) or throw. Both Rust and .NET. Spec chapter required.
- Why it matters: D052 deferred this as the `CountQuotaSlice` + `KnapsackSlice` upgrade path. Callers who need globally-optimal token packing AND count guarantees cannot get them today — `CountQuotaSlice` rejects `KnapsackSlice` as inner slicer. This delivers count constraints over the knapsack optimizer without the state-explosion risk of a full constrained-DP approach.
- Source: user (M009 discussion)
- Primary owning slice: M009/S01 (Rust), M009/S02 (.NET)
- Supporting slices: M009/S03 (spec)
- Validation: partial — Rust: `CountConstrainedKnapsackSlice` implemented with 3-phase algorithm; 5 conformance integration tests passing; re-exported from crate root; `cargo test --all-targets` 175 passed; clippy clean. .NET: pending S02.
- Notes: Pre-processing path chosen (D052 upgrade path 5A, not 5D full constrained-DP). Re-uses `CountCapExceeded` and `CountRequirementShortfall` diagnostics from R061. `KnapsackSlice` guard in `CountQuotaSlice` remains — `CountConstrainedKnapsackSlice` is a separate slicer, not a fix to that guard. D180: Phase 2 output must be re-sorted by score descending before Phase 3 cap enforcement.

### R063 — MetadataKeyScorer: multiplicative metadata-keyed score boost
- Class: differentiator
- Status: active
- Description: `MetadataKeyScorer(key, value, boost)` scorer that applies a multiplicative boost to items where `metadata[key] == value`. Items that do not match receive a neutral multiplier of `1.0`. Boost must be positive. A `defaultMultiplier` parameter controls the value returned for non-matching items (default `1.0`). Composable with `CompositeScorer`. Both Rust and .NET. Spec chapter with `cupel:priority` convention required.
- Why it matters: Enables callers to inject a prioritization signal via metadata without writing a custom scorer. `MetadataTrustScorer` provides absolute trust passthrough; `MetadataKeyScorer` provides conditional relative boosting — different semantic model. The `cupel:priority` convention provides a canonical metadata key for common priority signaling.
- Source: brainstorm (March 21 — B7, accepted; user confirmed M009)
- Primary owning slice: M009/S04 (Rust + .NET)
- Supporting slices: M009/S03 (spec)
- Validation: unmapped
- Notes: Multiplicative semantics chosen (scale-invariant, composable, consistent with ScaledScorer pattern per March 21 report B7). ~40 lines per language. Needs `cupel:priority` spec entry alongside `MetadataKeyScorer` spec chapter.

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
- Status: validated
- Description: `DryRunWithPolicy` — a method on `CupelPipeline` (.NET) and `Pipeline` (Rust) that runs the full pipeline using a caller-supplied policy object instead of the pipeline's own scorer/slicer/placer. Also includes a policy-accepting `PolicySensitivity` overload (.NET) and a `policy_sensitivity` free function (Rust) so fork-diagnostic callers can pass `(label, policy)` tuples instead of pre-built pipelines. Rust gains a new `Policy` struct (with `PolicyBuilder`) and `PolicySensitivityReport` type to achieve parity.
- Why it matters: Fork-diagnostic callers currently must construct N full `CupelPipeline` instances to compare N configurations. `DryRunWithPolicy` makes configuration comparison frictionless — pass a policy, get a report.
- Source: brainstorm (March 21); demand confirmed by R051 usage patterns
- Primary owning slice: M007/S01 (.NET), M007/S02 (Rust), M007/S03 (Rust fork diagnostic + spec)
- Supporting slices: none
- Validation: validated — .NET: CupelPipeline.DryRunWithPolicy (6 tests) and policy-based PolicySensitivity overload (3 tests) in Wollax.Cupel.Tests; dotnet test 679 passed. Rust: Policy + PolicyBuilder + dry_run_with_policy (5 integration tests in dry_run_with_policy.rs); policy_sensitivity (3 integration tests in policy_sensitivity_from_policies.rs, minimum-variants guard); cargo test --all-targets passed; cargo clippy clean. Spec: spec/src/analytics/policy-sensitivity.md exists, TBD-free, linked from SUMMARY.md
- Notes: M007 is the first Rust implementation of PolicySensitivity. CupelPolicy cannot express CountQuotaSlice (enum gap) — documented as a known limitation.

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
| R053 | quality-attribute | validated | M004/S04 | none | validated |
| R054 | core-capability | validated | M004/S05 | none | validated |
| R061 | core-capability | validated | M006/S01, M006/S02 | M006/S03 | validated |
| R062 | core-capability | active | M009/S01, M009/S02 | M009/S03 | unmapped |
| R063 | differentiator | active | M009/S04 | M009/S03 | unmapped |
| R060 | core-capability | validated | M005/S02 | S01, S03 | validated — all 13 patterns, 26+1 tests, cargo package exits 0 |
| R030 | anti-feature | out-of-scope | none | none | n/a |
| R031 | anti-feature | out-of-scope | none | none | n/a |
| R032 | anti-feature | out-of-scope | none | none | n/a |
| R056 | core-capability | validated | M007/S01, M007/S02, M007/S03 | none | validated |
| R058 | operability | validated | M008/S01, M008/S02, M008/S03 | none | validated |
| R040 | differentiator | validated | M002/S03 | M002/S01 | validated |
| R041 | quality-attribute | validated | M002/S02 | none | validated |
| R042 | differentiator | validated | M002/S04 | none | validated |
| R043 | differentiator | validated | M002/S05 | none | validated |
| R044 | quality-attribute | validated | M002/S06 | none | validated |
| R045 | quality-attribute | validated | M002/S01 | none | validated |

## Coverage Summary

- Active requirements: 2 (R062, R063)
- Mapped to slices: 2
- Validated: 33 (R001–R006, R010–R014, R020–R022, R040–R045, R050–R054, R056, R058, R060–R061)
- Unmapped active requirements: 0
