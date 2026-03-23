---
id: S03
parent: M003
milestone: M003
provides:
  - CountQuotaSlice Rust struct + .NET class implementing Slicer/ISlicer with two-phase COUNT-DISTRIBUTE-BUDGET algorithm
  - CountQuotaEntry with require_count/cap_count validation and construction guards in both languages
  - ScarcityBehavior enum (Degrade=default, Throw) in both languages
  - CountRequirementShortfall type on SelectionReport for unmet require_count reporting
  - ExclusionReason::CountCapExceeded and CountRequireCandidatesExhausted variants in both languages
  - is_knapsack() default method on Rust Slicer trait; KnapsackSlice overrides to true
  - 5 TOML conformance vectors in all 3 locations (spec/, conformance/, crates/cupel/conformance/)
  - count_quota arm in build_slicer_by_type + run_count_quota_full_test harness
  - 17 Rust unit tests + 5 conformance tests + 13 .NET TUnit tests
  - SelectionReport.CountRequirementShortfalls non-required property (default = [])
  - CountQuotaSlice.LastShortfalls property for test-accessible shortfall inspection (.NET)
requires:
  - slice: S01
    provides: Established Slicer trait pattern (Rust) and ISlicer interface pattern (.NET)
affects:
  - S04
  - S05
  - S06
key_files:
  - crates/cupel/src/slicer/count_quota.rs
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/slicer/knapsack.rs
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/diagnostics/trace_collector.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/conformance.rs
  - crates/cupel/tests/conformance/slicing.rs
  - spec/conformance/required/slicing/count-quota-baseline.toml
  - spec/conformance/required/slicing/count-quota-cap-exclusion.toml
  - spec/conformance/required/slicing/count-quota-scarcity-degrade.toml
  - spec/conformance/required/slicing/count-quota-tag-nonexclusive.toml
  - spec/conformance/required/slicing/count-quota-require-and-cap.toml
  - src/Wollax.Cupel/Slicing/CountQuotaSlice.cs
  - src/Wollax.Cupel/Slicing/CountQuotaEntry.cs
  - src/Wollax.Cupel/Slicing/ScarcityBehavior.cs
  - src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs
  - src/Wollax.Cupel/Diagnostics/ExclusionReason.cs
  - src/Wollax.Cupel/Diagnostics/SelectionReport.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs
key_decisions:
  - "is_knapsack() default false on Slicer trait avoids Any/downcast for KnapsackSlice guard at construction"
  - "CountRequirementShortfall defined in diagnostics/mod.rs (Rust) — owned by the report, not the slicer"
  - "RawSelectionReport removes deny_unknown_fields to allow #[serde(default)] on new field — backward compat preserved"
  - "Cap enforcement post-filters Phase 2 inner slicer output — no wrapper slicer needed for v1"
  - "LastShortfalls property on .NET CountQuotaSlice (not ISlicer) is test inspection surface — ITraceCollector lacks RecordShortfall in v1"
  - "ITraceCollector.RecordExcluded does not exist in .NET; cap exclusions signalled via RecordItemEvent; full ExcludedItem surfacing deferred to pipeline wiring"
  - "Shortfall count verification in conformance harness: recomputed from vector data rather than Pipeline::dry_run to avoid DiagnosticTraceCollector always returning empty shortfalls"
  - "tag-nonexclusive scenario uses two separate ContextKind entries (not multi-tag items) since CountQuotaSlice operates per-kind"
patterns_established:
  - "Two-phase slicer decorator: Phase 1 commits required items, Phase 2 delegates residual to inner slicer, Phase 3 cap-filters inner output"
  - "SelectionReport backward-compatible field extension: #[serde(default)] + remove deny_unknown_fields from RawSelectionReport (Rust)"
  - "ReferenceEqualityComparer.Instance for committedSet in Phase 1 (.NET) — ContextItem identity is reference-based"
  - "run_count_quota_full_test pattern: direct slicer.slice() + arithmetic recomputation for shortfall/cap counts from TOML vector"
observability_surfaces:
  - "SelectionReport::count_requirement_shortfalls (Rust) / SelectionReport.CountRequirementShortfalls (.NET) — non-empty = degraded"
  - "CountQuotaSlice.LastShortfalls (.NET) — post-run shortfall inspection in tests"
  - "ExclusionReason::CountCapExceeded on excluded items — cap-blocked items (when wired through pipeline in future)"
  - "CupelError::SlicerConfig at construction — names the violated constraint"
  - "cargo test -- count_quota --nocapture — runs all 17 unit tests + 5 conformance tests"
  - "diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing — drift guard"
drill_down_paths:
  - .kata/milestones/M003/slices/S03/tasks/T01-SUMMARY.md
  - .kata/milestones/M003/slices/S03/tasks/T02-SUMMARY.md
  - .kata/milestones/M003/slices/S03/tasks/T03-SUMMARY.md
duration: ~115min (T01: 45min, T02: 25min, T03: 45min)
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
---

# S03: CountQuotaSlice — Rust + .NET Implementation

**Full CountQuotaSlice decorator in both Rust and .NET: two-phase COUNT-DISTRIBUTE-BUDGET algorithm, 5 conformance vectors, 35+ tests, new ExclusionReason variants, and SelectionReport shortfall reporting — cargo test 117 passed, dotnet test 682 passed.**

## What Happened

This slice implemented `CountQuotaSlice` end-to-end across three tasks.

**T01 (Rust implementation):** Added `is_knapsack()` default method to the `Slicer` trait so `CountQuotaSlice::new` can reject KnapsackSlice inner slicers at construction without runtime downcasting. Extended `ExclusionReason` with `CountCapExceeded { kind, cap, count }` and `CountRequireCandidatesExhausted { kind }` variants (serde-compatible with the existing `#[serde(tag = "reason")]` format). Added `CountRequirementShortfall` to `diagnostics/mod.rs` and the matching `count_requirement_shortfalls` field to `SelectionReport` using `#[serde(default)]`. Removed `deny_unknown_fields` from `RawSelectionReport` to allow the default — all 49 existing serde tests continue to pass. The `CountQuotaSlice` slicer (~680 lines) implements three phases: Phase 1 commits top-N items by score per kind, Phase 2 delegates residual budget to the inner slicer, Phase 3 cap-filters inner slicer output. 17 unit tests + 2 doctests.

**T02 (Rust conformance vectors):** Authored 5 TOML vectors covering: baseline satisfaction (require=2/cap=4), cap exclusion (cap=1), scarcity degrade (require=3/candidates=1 → shortfall=1), tag non-exclusivity (two independent kinds satisfy independently), and require+cap combined (require=2/cap=2/candidates=4 → 2 selected, 2 cap-excluded). Extended `build_slicer_by_type` with a `count_quota` arm and added `run_count_quota_full_test` verifying selected_contents plus arithmetic-recomputed shortfall/cap counts. All 5 vectors copied verbatim to all three locations; drift guard clean.

**T03 (.NET implementation):** Extended `ExclusionReason` enum with `CountCapExceeded = 8` and `CountRequireCandidatesExhausted = 9`. Created `CountRequirementShortfall` sealed positional record, `ScarcityBehavior` enum, `CountQuotaEntry` with validated construction, and `CountQuotaSlice` implementing `ISlicer` with the same three-phase algorithm. Added `SelectionReport.CountRequirementShortfalls` as a non-required `IReadOnlyList<CountRequirementShortfall>` (default `[]`). Added `LastShortfalls` property on the slicer class for test inspection (necessary because `ITraceCollector` lacks a `RecordShortfall` method in v1). Updated 22 entries in `PublicAPI.Unshipped.txt`. Fixed two existing enum-count tests that hardcoded `8`. 13 TUnit tests.

## Verification

```
cargo test --all-targets                              → 117 passed, 0 failed
cargo test -- count_quota --nocapture                 → 17 unit + 5 conformance + 2 doctests, 0 failed
diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing  → clean
ls spec/conformance/required/slicing/count-quota-*.toml | wc -l  → 5
dotnet test                                           → 682 passed, 0 failed
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj    → 0 errors, 0 warnings
grep CountCapExceeded/CountRequireCandidatesExhausted in diagnostics/mod.rs  → 2 hits
grep CountCapExceeded/CountRequireCandidatesExhausted in CountQuotaSlice.cs  → 2 hits
PublicAPI.Unshipped.txt has CountQuotaSlice/CountQuotaEntry/ScarcityBehavior/CountRequirementShortfall → 22 entries
```

## Requirements Advanced

- R040 — CountQuotaSlice implementation in both Rust and .NET completes the implementation side of the count-based quota design. The design decisions (DI-1 through DI-6, COUNT-DISTRIBUTE-BUDGET pseudocode) from M002/S03 are now fully executed.

## Requirements Validated

- R040 — Validated: `CountQuotaSlice` implemented in both languages with all design decisions executed; 35+ tests pass; 5 conformance vectors in all 3 locations; drift guard clean; `cargo test --all-targets` → 117 passed; `dotnet test` → 682 passed.

## New Requirements Surfaced

- None discovered during execution.

## Requirements Invalidated or Re-scoped

- None. `SelectionReport.CountRequirementShortfalls` always returns `[]` via the standard pipeline because the pipeline's `ReportBuilder` does not yet have a path for shortfall injection — this is expected and documented; shortfall surfacing through the full pipeline is deferred to a future slice when pipeline wiring is extended.

## Deviations

- **Rust cap enforcement**: The task plan specified `CountCapExceeded` via `traceCollector.RecordExcluded`. In practice, cap enforcement post-filters Phase 2 output silently (no trace call at slicer level); the `CountCapExceeded` reason is reserved for when the slicer is wired into a full diagnostic pipeline. T02 conformance vectors verify cap behavior via item-count assertions.
- **Rust `deny_unknown_fields` removal**: `RawSelectionReport` previously had `deny_unknown_fields` which is incompatible with `#[serde(default)]` on individual fields. Removed; all 49 existing serde tests pass; strictly less restrictive (old payloads still deserialize).
- **.NET `RecordExcluded`**: `ITraceCollector` only has `RecordStageEvent` / `RecordItemEvent`. Cap exclusions use `RecordItemEvent` with structured message. `ExcludedItem.CountCapExceeded` will appear in `SelectionReport.Excluded` once the pipeline is extended through `ReportBuilder` (deferred).
- **tag-nonexclusive scenario**: Design doc described multi-tag item satisfying 2 constraints. `CountQuotaSlice` operates per `ContextKind`, so the TOML vector uses two independent kinds instead — exercises the same non-exclusivity property.
- **Two existing .NET enum-count tests** were hard-coded at `8`; updated to `10` for the two new ExclusionReason values (not mentioned in plan, but straightforward fix).

## Known Limitations

- `SelectionReport.CountRequirementShortfalls` always returns `[]` when accessed via `Pipeline.DryRun` because `ReportBuilder` doesn't yet have a shortfall injection path. Use `CountQuotaSlice.LastShortfalls` for post-run inspection in .NET tests.
- `CountCapExceeded` exclusion reasons are not yet surfaced in `SelectionReport.Excluded` in either language — requires pipeline-level wiring of slicer exclusion outputs through `ReportBuilder`. Deferred to a future slice.
- Rust conformance shortfall/cap count verification is arithmetic-recomputed from vector data (not via `Pipeline::dry_run`) due to the same shortfall propagation gap.

## Follow-ups

- Pipeline wiring: wire `CountQuotaSlice` exclusion outputs (CountCapExceeded, CountRequireCandidatesExhausted) through `ReportBuilder` so `SelectionReport.Excluded` contains them — required before S03's observability story is complete end-to-end.
- `ITraceCollector.RecordShortfall` (or equivalent): adding a dedicated method would allow shortfalls to propagate to `SelectionReport.CountRequirementShortfalls` via the standard pipeline path rather than the `LastShortfalls` sidecar.

## Files Created/Modified

- `crates/cupel/src/slicer/count_quota.rs` — new; ~680 lines; CountQuotaEntry, ScarcityBehavior, CountQuotaSlice + Slicer impl, 17 unit tests, 2 doctests
- `crates/cupel/src/slicer/mod.rs` — is_knapsack() default method; mod count_quota; pub use additions
- `crates/cupel/src/slicer/knapsack.rs` — is_knapsack() override returning true
- `crates/cupel/src/diagnostics/mod.rs` — CountRequirementShortfall; CountCapExceeded + CountRequireCandidatesExhausted on ExclusionReason; count_requirement_shortfalls on SelectionReport + RawSelectionReport
- `crates/cupel/src/diagnostics/trace_collector.rs` — count_requirement_shortfalls: Vec::new() in into_report
- `crates/cupel/src/lib.rs` — re-exports for CountRequirementShortfall, CountQuotaEntry, CountQuotaSlice, ScarcityBehavior
- `crates/cupel/tests/conformance.rs` — count_quota arm in build_slicer_by_type
- `crates/cupel/tests/conformance/slicing.rs` — run_count_quota_full_test + 5 test functions
- `spec/conformance/required/slicing/count-quota-*.toml` — 5 new vectors
- `conformance/required/slicing/count-quota-*.toml` — 5 copies
- `crates/cupel/conformance/required/slicing/count-quota-*.toml` — 5 copies
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — new; ~175 lines; ISlicer implementation
- `src/Wollax.Cupel/Slicing/CountQuotaEntry.cs` — new; ~50 lines; entry with validation
- `src/Wollax.Cupel/Slicing/ScarcityBehavior.cs` — new; ~10 lines; enum
- `src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs` — new; ~8 lines; sealed positional record
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — 2 new enum values
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — 1 new non-required property
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 22 new API entries
- `tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs` — new; 13 TUnit tests
- `tests/Wollax.Cupel.Tests/Diagnostics/TraceEventTests.cs` — enum count updated 8→10
- `tests/Wollax.Cupel.Tests/Diagnostics/OverflowEventTests.cs` — enum count updated 8→10

## Forward Intelligence

### What the next slice should know
- `SelectionReport.CountRequirementShortfalls` needs pipeline wiring before it works end-to-end; don't assume it's populated via standard DryRun — check `LastShortfalls` or extend ReportBuilder
- `ExclusionReason.CountCapExceeded` / `CountRequireCandidatesExhausted` exist and compile but won't appear in `SelectionReport.Excluded` until pipeline wiring extends ReportBuilder to pass slicer exclusion outputs
- `is_knapsack()` on the `Slicer` trait is the approved pattern for build-time inner-slicer guards — use this for any future decorator slicers that need similar guards

### What's fragile
- `RawSelectionReport` no longer has `deny_unknown_fields` — this is intentional for backward compat but means new fields silently default if misspelled; future serde field additions should be tested carefully
- `CountQuotaSlice.LastShortfalls` (.NET) is a sidecar inspection surface, not part of ISlicer — any refactor that moves shortfall data must consider test code that uses this property

### Authoritative diagnostics
- `cargo test -- count_quota --nocapture` — runs all 22+ count_quota tests with scenario names printed
- `diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing` — canonical drift check; should always exit 0 with no output
- `dotnet test --treenode-filter "/*/*/CountQuotaSliceTests/*"` — runs only CountQuota .NET tests

### What assumptions changed
- Initial plan assumed `ITraceCollector.RecordExcluded` existed in .NET — it does not; RecordItemEvent is the available signal for slicer-level diagnostics
- Initial plan assumed shortfalls could be wired through DiagnosticTraceCollector — they can't without a new interface method; LastShortfalls sidecar is the v1 test surface
