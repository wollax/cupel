# M006: Count-Based Quotas — Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

## Project Description

Cupel is a context-selection library (Rust + .NET) that selects items for LLM context windows using a five-stage pipeline (Classify → Score → Deduplicate → Slice → Place). M006 implements `CountQuotaSlice` — a decorator slicer that enforces absolute item-count requirements and per-kind caps before delegating to an inner slicer.

## Why This Milestone

`QuotaSlice` controls what fraction of the token budget each kind receives. `CountQuotaSlice` controls what *number of items* of each kind are included. These are complementary constraint systems: a caller may need "at least 2 tool results AND tool results capped at 30% of budget". Without count quotas, callers hand-roll requirement enforcement in their pipeline setup code.

The design is fully settled (`.planning/design/count-quota-design.md`). Both the .NET and Rust skeletons partially exist. The spec chapter exists at `spec/src/slicers/count-quota.md`. This milestone is implementation-and-test, not design.

## User-Visible Outcome

### When this milestone is complete, the user can:

- Construct a `CountQuotaSlice(inner: GreedySlice, entries: [RequireCount("tool", 2), CapCount("system", 1)])` and pass it to `Pipeline::builder()` in both Rust and .NET
- Run the pipeline and observe that: the required item counts appear in `SelectionReport.Included`, cap violations appear as `ExclusionReason::CountCapExceeded` on excluded items, and scarcity violations (when fewer candidates exist than required) appear in `SelectionReport.count_requirement_shortfalls`
- Compose `CountQuotaSlice` wrapping `QuotaSlice` for combined count + percentage constraints
- Observe construction-time errors for contradictory constraint configurations (`require > cap`, inner slicer is `KnapsackSlice`)

### Entry point / environment

- Entry point: `Pipeline::builder()` in both Rust (`crates/cupel`) and .NET (`src/Wollax.Cupel`)
- Environment: unit + integration tests; no live dependencies
- Live dependencies involved: none

## Completion Class

- Contract complete means: unit tests and integration tests pass for all five conformance scenarios; build clean; clippy clean; `PublicAPIAnalyzers` green in .NET
- Integration complete means: `CountQuotaSlice` wired into the pipeline and exercised end-to-end via `run_traced()` / `dry_run()`; `SelectionReport.count_requirement_shortfalls` and `ExclusionReason::CountCapExceeded` appear in real pipeline output
- Operational complete means: none (library crate)

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- A real `Pipeline` with `CountQuotaSlice` inner `GreedySlice` runs `run_traced()` / `dry_run()` and returns a `SelectionReport` where required items are included, capped items carry `CountCapExceeded` exclusion reason, and shortfalls appear in `count_requirement_shortfalls`
- `CountQuotaSlice + QuotaSlice` composition compiles and produces a combined report in both languages
- `CountQuotaSlice + KnapsackSlice` fails at construction time with a clear error message

## Risks and Unknowns

- **Existing skeleton completeness** — both `crates/cupel/src/slicer/count_quota.rs` (776 lines) and `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` (256 lines) already exist. The actual implementation completeness of these files is unknown — they may be full implementations, stubs, or partial. The first task in each slice must audit the existing code rather than assuming it is missing.
- **SelectionReport extension wiring** — `count_requirement_shortfalls` already appears in the Rust `SelectionReport` struct. Verifying that the pipeline actually populates this field from `CountQuotaSlice` output is an integration concern, not just a compilation concern.
- **QuotaUtilization analytics compatibility** — `CountQuotaSlice` implements `QuotaPolicy` (Rust) / `IQuotaPolicy` (.NET) for `quota_utilization`. Verifying this integration doesn't regress existing quota utilization tests is a must-have.

## Existing Codebase / Prior Art

- `crates/cupel/src/slicer/count_quota.rs` — Rust implementation file (776 lines; completeness unknown)
- `crates/cupel/src/slicer/mod.rs` — exports `CountQuotaSlice`, `CountQuotaEntry`, `ScarcityBehavior`; `Slicer` trait has `is_count_quota()` defaulted method
- `crates/cupel/src/diagnostics/mod.rs` — `CountCapExceeded` variant on `ExclusionReason`; `count_requirement_shortfalls` field on `SelectionReport`; `CountRequirementShortfall` struct
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — .NET implementation (256 lines; completeness unknown)
- `src/Wollax.Cupel/Slicing/CountQuotaEntry.cs` — .NET entry type
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — `QuotaViolations` / `count_requirement_shortfalls` field
- `tests/Wollax.Cupel.Tests/Slicing/CountQuotaSliceTests.cs` — .NET test file (268 lines; may be complete or stubbed)
- `.planning/design/count-quota-design.md` — the authoritative design doc with pseudocode, all DI rulings, and 5 conformance vector outlines. Read this at the start of every task.
- `spec/src/slicers/count-quota.md` — spec chapter

> See `.kata/DECISIONS.md` for all architectural and pattern decisions. Relevant decisions: D040, D046, D052–D057, D084–D087.

## Relevant Requirements

- R061 — CountQuotaSlice: count-based quota enforcement (primary owner M006)

## Scope

### In Scope

- `CountQuotaSlice` full implementation in both Rust and .NET: two-phase algorithm, `require_count`, `cap_count`, `ScarcityBehavior`
- `CountCapExceeded` exclusion reason on excluded items (both languages)
- `count_requirement_shortfalls` / `QuotaViolations` on `SelectionReport` (both languages)
- Construction-time guards: `require > cap`, `KnapsackSlice` inner slicer rejection
- Pinned-item decrement rule (residual `require_count` after pinned items are accounted for)
- Tag non-exclusivity semantics (multi-tag items count toward all matching constraints)
- Integration tests covering all 5 conformance vector scenarios from the design doc
- `QuotaPolicy` / `IQuotaPolicy` implementation so `quota_utilization` works with `CountQuotaSlice`
- Spec chapter completeness check (no new spec work — chapter already exists)

### Out of Scope / Non-Goals

- `CountConstrainedKnapsackSlice` — deferred to M007+ if demand confirmed (D052)
- Per-entry `ScarcityBehavior` override — per-slicer granularity is sufficient for v1 (D056)
- `CountRequireCandidatesExhausted` per-item `ExclusionReason` variant — scarcity is reported at report level (D046)
- Any new spec chapters — spec chapter already exists
- OTel / tracing integration for count-quota events

## Technical Constraints

- `Slicer::slice` in Rust does not accept a `TraceCollector` — `CountCapExceeded` exclusions are not observable at the Rust slicer level (D086). This is a known limitation; do not attempt to thread a collector through the slicer interface.
- `.NET CountQuotaSlice.LastShortfalls` is the test inspection surface for shortfalls, not `ISlicer.Slice` return value (D087).
- `SelectionReport` positional deconstruction is explicitly unsupported (D057) — adding `QuotaViolations` as a non-required property with empty-list default is non-breaking for property-access callers.
- `#[non_exhaustive]` on Rust `SelectionReport` and `ExclusionReason` — extensions are safe (design doc Section 6).

## Integration Points

- `Pipeline::builder()` — `CountQuotaSlice` is passed as the slicer; must wire through correctly
- `SelectionReport` — two new fields; must serialize correctly under `serde` feature
- `QuotaPolicy` / `IQuotaPolicy` — `quota_utilization` must work with `CountQuotaSlice` without regression
- `quota_utilization` tests — must remain green after `CountQuotaSlice` ships

## Open Questions

- None — design is fully settled. All questions answered in `.planning/design/count-quota-design.md`.
