# S03: CountQuotaSlice — Rust + .NET Implementation

**Goal:** Implement `CountQuotaSlice` in both Rust and .NET: a decorator slicer enforcing absolute minimum/maximum item counts per `ContextKind`, with the two-phase COUNT-DISTRIBUTE-BUDGET algorithm, two new `ExclusionReason` variants, `CountRequirementShortfalls` on `SelectionReport`, 5 conformance vectors, and full test coverage in both languages.

**Demo:** A caller can construct `CountQuotaSlice(inner: GreedySlice, entries: [RequireCount("tool", 2), CapCount("tool", 4)])` in both .NET and Rust; when run with a candidate pool, the top-2 tool items are committed in Phase 1, remaining items distributed in Phase 2, items exceeding the cap are excluded with `CountCapExceeded`, unmet minimums are reported in `CountRequirementShortfalls`; all 5 conformance vectors pass; `cargo test --all-targets` and `dotnet test` both exit 0.

## Must-Haves

- `CountQuotaSlice` struct (Rust) and class (.NET) implement `Slicer`/`ISlicer`
- `ScarcityBehavior` enum with `Degrade` (default) and `Throw` variants in both languages
- `CountQuotaEntry` struct/class with `kind`, `require_count`, `cap_count` in both languages
- `CountCapExceeded { kind, cap, count }` variant added to `ExclusionReason` in Rust
- `CountRequireCandidatesExhausted { kind }` variant added to `ExclusionReason` in Rust
- `CountCapExceeded` and `CountRequireCandidatesExhausted` added to `ExclusionReason` enum in .NET
- `count_requirement_shortfalls: Vec<CountRequirementShortfall>` field on Rust `SelectionReport` (non-required, default empty, serde-safe)
- `CountRequirementShortfalls` non-required property (default `[]`) on .NET `SelectionReport`
- KnapsackSlice guard: construction throws/errors when inner slicer is `KnapsackSlice`
- `is_knapsack() -> bool` default method on `Slicer` trait (false by default, overridden in `KnapsackSlice`)
- 5 TOML conformance vectors in all three locations: `spec/conformance/required/slicing/`, `conformance/required/slicing/`, `crates/cupel/conformance/required/slicing/`
- `"count_quota"` arm in `build_slicer_by_type` in `conformance.rs`
- Conformance harness extended to verify exclusion reasons and shortfalls for count_quota tests
- `cargo test --all-targets` passes; `dotnet test` passes
- Drift guard `diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing` exits 0
- `PublicAPI.Unshipped.txt` updated for all new public types and members

## Proof Level

- This slice proves: contract (unit tests + conformance vectors exercise the slicer directly)
- Real runtime required: no (slicer interface; no pipeline-level integration needed for this slice)
- Human/UAT required: no

## Verification

```bash
# Rust — all tests including new count_quota conformance and unit tests
cargo test --all-targets 2>&1 | tail -5

# Rust — drift guard (must exit 0, no output)
diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing

# Rust — count_quota specific tests
cargo test -- count_quota --nocapture

# .NET — full suite
dotnet test

# .NET — PublicAPI check
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep -c " error " | xargs -I{} test {} -eq 0

# Exclusion reasons emitted
grep -c "CountCapExceeded\|CountRequireCandidatesExhausted" crates/cupel/src/slicer/count_quota.rs
grep -c "CountCapExceeded\|CountRequireCandidatesExhausted" src/Wollax.Cupel/Slicing/CountQuotaSlice.cs

# All 5 vectors present
ls spec/conformance/required/slicing/count-quota-*.toml | wc -l
```

## Observability / Diagnostics

- Runtime signals: `CountCapExceeded { kind, cap, count }` and `CountRequireCandidatesExhausted { kind }` on `ExcludedItem.reason`; `CountRequirementShortfall` entries on `SelectionReport.count_requirement_shortfalls`
- Inspection surfaces: `SelectionReport.count_requirement_shortfalls` (non-empty = degraded); `SelectionReport.excluded` filtered by reason variant
- Failure visibility: construction-time `CupelError::SlicerConfig` / `ArgumentException` for impossible constraints; scarcity recorded in `count_requirement_shortfalls` with `required_count` and `satisfied_count`
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `Slicer` trait / `ISlicer` interface; `ExclusionReason` enum; `SelectionReport` struct/record; `ContextKind`, `ScoredItem`, `ContextBudget` model types
- New wiring introduced in this slice: `CountQuotaSlice` exported from `slicer/mod.rs` and `lib.rs`; new `ExclusionReason` variants integrated into serde; `is_knapsack()` default method on `Slicer` trait; `count_requirement_shortfalls` added to `SelectionReport` serde custom Deserialize
- What remains before the milestone is truly usable end-to-end: S04 (analytics), S05 (OTel), S06 (budget simulation)

## Tasks

- [x] **T01: Implement CountQuotaSlice in Rust** `est:45m`
  - Why: Core Rust implementation — new data model types, `is_knapsack` trait method, `ExclusionReason` variants, `SelectionReport` field, and `CountQuotaSlice` slicer with two-phase algorithm
  - Files: `crates/cupel/src/slicer/count_quota.rs` (new), `crates/cupel/src/slicer/mod.rs`, `crates/cupel/src/slicer/knapsack.rs`, `crates/cupel/src/diagnostics/mod.rs`, `crates/cupel/src/lib.rs`
  - Do: Follow the locked pseudocode in `.planning/design/count-quota-design.md`; add `fn is_knapsack(&self) -> bool { false }` default to `Slicer` trait and override `true` in `KnapsackSlice`; add `CountCapExceeded { kind: String, cap: usize, count: usize }` and `CountRequireCandidatesExhausted { kind: String }` to `ExclusionReason`; add `count_requirement_shortfalls: Vec<CountRequirementShortfall>` to `SelectionReport` (update both the struct field and `RawSelectionReport` with `#[serde(default)]`); implement `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall` structs; implement `CountQuotaSlice::new` with validation (require > cap = error, cap_count == 0 with require_count > 0 = error, inner is KnapsackSlice = error); implement `Slicer::slice` with Phase 1 (count-satisfy per-kind score-desc), Phase 2 (delegate to inner with residual budget), Phase 3 (count cap enforcement via `CountCapExceeded`); re-export all new public types from `slicer/mod.rs` and `lib.rs`
  - Verify: `cargo test --all-targets` passes; `cargo doc --no-deps 2>&1 | grep -c warning` shows no new warnings; `grep -c "is_knapsack" crates/cupel/src/slicer/mod.rs` > 0
  - Done when: `cargo test --all-targets` exits 0; `CountQuotaSlice`, `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall` are in `cupel` crate public API; `ExclusionReason::CountCapExceeded` and `CountRequireCandidatesExhausted` are accessible; `SelectionReport` has `count_requirement_shortfalls` field

- [x] **T02: Author 5 conformance vectors and extend Rust harness** `est:40m`
  - Why: Establishes the verifiable contract for CountQuotaSlice behavior across 5 scenarios (baseline satisfaction, cap exclusion, scarcity degrade, tag non-exclusivity, shortfall shortfall reporting); extends harness to verify exclusion reasons and shortfalls beyond selected_contents
  - Files: `spec/conformance/required/slicing/count-quota-*.toml` (5 new), `conformance/required/slicing/count-quota-*.toml` (5 copies), `crates/cupel/conformance/required/slicing/count-quota-*.toml` (5 copies), `crates/cupel/tests/conformance.rs`, `crates/cupel/tests/conformance/slicing.rs`
  - Do: Write 5 TOML vectors matching the 5 scenario sketches from design doc (baseline satisfaction, cap exclusion, scarcity degrade with shortfalls, tag non-exclusivity, require+cap combined); add `[expected.count_cap_exceeded]` and `[expected.shortfalls]` sections to relevant vectors; extend `build_slicer_by_type` in `conformance.rs` with a `"count_quota"` arm that parses `entries` array (each entry has `kind`, `require_count`, `cap_count`) and `scarcity_behavior`; add a `run_count_quota_slicing_test` function in `conformance/slicing.rs` that also checks excluded-item reasons and shortfalls in addition to selected_contents; write 5 `#[test]` functions; copy all 5 vectors verbatim to all THREE locations (spec/, conformance/, crates/); run drift guard
  - Verify: `cargo test -- count_quota --nocapture` shows 5 tests passing; `diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing` exits 0 with no output; `ls spec/conformance/required/slicing/count-quota-*.toml | wc -l` outputs 5
  - Done when: All 5 `count_quota_*` test functions pass in `cargo test --all-targets`; drift guard clean; vectors present in all 3 locations

- [x] **T03: Implement CountQuotaSlice in .NET** `est:45m`
  - Why: .NET parity — matching `CountQuotaEntry`, `ScarcityBehavior`, `CountQuotaSlice`, new `ExclusionReason` variants, `CountRequirementShortfalls` on `SelectionReport`, TUnit tests covering all 5 conformance scenarios
  - Files: `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` (new), `src/Wollax.Cupel/Slicing/CountQuotaEntry.cs` (new), `src/Wollax.Cupel/Slicing/ScarcityBehavior.cs` (new), `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs`, `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`, `src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs` (new), `src/Wollax.Cupel/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs` (new)
  - Do: Add `CountCapExceeded` and `CountRequireCandidatesExhausted` to `ExclusionReason` enum; add `CountRequirementShortfalls IReadOnlyList<CountRequirementShortfall> { get; init; } = [];` (NON-required, default empty) to `SelectionReport`; create `CountRequirementShortfall` record with `Kind`, `RequiredCount`, `SatisfiedCount`; create `ScarcityBehavior` enum with `Degrade` and `Throw`; create `CountQuotaEntry` class with validated `Kind`, `RequireCount`, `CapCount` (throw `ArgumentException` if require > cap or cap == 0 with require > 0); create `CountQuotaSlice` implementing `ISlicer` with KnapsackSlice guard (check `typeof(innerSlicer) == typeof(KnapsackSlice)`, throw with exact message from design doc), Phase 1 count-satisfy, Phase 2 delegate to inner with residual budget, Phase 3 cap enforcement emitting `CountCapExceeded` via `traceCollector.RecordExcluded`; update `PublicAPI.Unshipped.txt` with all new public types and members; write `CountQuotaSliceTests.cs` with at minimum 5 tests covering: baseline satisfaction, cap exclusion (checks ExcludedItem reason), scarcity degrade (checks CountRequirementShortfalls), tag non-exclusivity, KnapsackSlice guard throws
  - Verify: `dotnet test` passes; `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep " error "` returns nothing; `grep -c "CountQuotaSlice\|CountQuotaEntry\|ScarcityBehavior\|CountRequirementShortfall" src/Wollax.Cupel/PublicAPI.Unshipped.txt` > 0
  - Done when: `dotnet test` exits 0 with all CountQuotaSlice tests passing; `dotnet build` exits 0; all new types present in PublicAPI.Unshipped.txt; `SelectionReport.CountRequirementShortfalls` is non-required with default `[]`

## Files Likely Touched

- `crates/cupel/src/slicer/count_quota.rs` — new
- `crates/cupel/src/slicer/mod.rs` — add `mod count_quota; pub use count_quota::...`
- `crates/cupel/src/slicer/knapsack.rs` — add `is_knapsack() override`
- `crates/cupel/src/diagnostics/mod.rs` — add `CountCapExceeded`, `CountRequireCandidatesExhausted` variants; add `count_requirement_shortfalls` field + RawSelectionReport update
- `crates/cupel/src/lib.rs` — re-export new types
- `crates/cupel/tests/conformance.rs` — add `"count_quota"` arm
- `crates/cupel/tests/conformance/slicing.rs` — extend harness + 5 tests
- `spec/conformance/required/slicing/count-quota-*.toml` — 5 new
- `conformance/required/slicing/count-quota-*.toml` — 5 copies
- `crates/cupel/conformance/required/slicing/count-quota-*.toml` — 5 copies
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — new
- `src/Wollax.Cupel/Slicing/CountQuotaEntry.cs` — new
- `src/Wollax.Cupel/Slicing/ScarcityBehavior.cs` — new
- `src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs` — new
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — add 2 variants
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — add `CountRequirementShortfalls`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — new entries
- `tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs` — new
