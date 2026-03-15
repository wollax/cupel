# Research Summary: Cupel v1.2 — Rust Parity & Quality Hardening

**Date**: 2026-03-15
**Synthesized from**: STACK.md (Rust sections), FEATURES.md, ARCHITECTURE-RUST-DIAGNOSTICS.md, PITFALLS-RUST-DIAGNOSTICS.md
**Consumer**: kata-roadmapper agent
**Milestone**: v1.2 (Rust Diagnostics Parity + Quality Hardening)

---

## Executive Summary

The Rust `cupel` crate (published as v1.1.0 on 2026-03-15) is complete at the pipeline level — 6-stage pipeline, 8 scorers, 3 slicers, 2 placers, 28 conformance tests passing — but lacks the diagnostics system that is the primary production-debuggability differentiator of the .NET implementation. The v1.2 milestone closes that gap and batch-addresses 74+ open quality issues.

The diagnostics gap is not superficial. The .NET crate exposes `ITraceCollector`, `DiagnosticTraceCollector`, `SelectionReport`, `IncludedItem`/`ExcludedItem`, `InclusionReason`/`ExclusionReason`, and `DryRun()`. Together these answer "what was dropped from context and why" — the question that 65% of enterprise AI context failures hinge on. Without this, the Rust crate cannot be used in production diagnostic workflows.

Three research dimensions are HIGH-confidence with clear, actionable conclusions. One meta-constraint is non-negotiable: **spec-first sequencing is mandatory**. The Rust lifetime system creates implementation pressure that will bias API shape toward ergonomic shortcuts rather than semantic correctness. Any API published on crates.io is a semver commitment. The spec chapter for Rust diagnostics must be reviewed and merged before any implementation PR opens.

The single most consequential unresolved decision — requiring spec chapter resolution — is the ownership model: `&mut dyn TraceCollector` per-call injection vs. `Pipeline<T: TraceCollector = NullTraceCollector>` generic type parameter. Both are architecturally defensible. The choice determines whether the zero-overhead null path is guaranteed at compile time (generic) or relies on runtime branch prediction (dyn with `is_enabled()` gate). The research recommends the generic approach for the zero-cost guarantee, but acknowledges the API simplicity tradeoff. This is the single decision the roadmapper must flag as requiring spec-chapter resolution before issue assignment.

---

## Key Findings by Research Area

### Features (FEATURES.md)

**Parity gap is precisely quantified.** Ten .NET types are absent from the Rust crate. They decompose into a strict dependency chain: Spec chapter (TS-05) → Trait (TS-01) → Data types (TS-02) → Buffered collector (TS-03) → SelectionReport (TS-04) → Pipeline integration (TS-06) → DryRun API (D-01). Nothing in this chain can be parallelized — each layer depends on the previous one.

**Quick wins are genuinely quick.** Five API hardening items (D-04) — `#[non_exhaustive]` on `CupelError` and `OverflowStrategy`, `Debug`/`Clone`/`Copy` derives on concrete slicer/placer structs, `ContextKind` and `ContextSource` convenience constructors — have zero logic impact and can be implemented before the spec chapter is complete. These must be first because `#[non_exhaustive]` on `CupelError` is a prerequisite for adding `CupelError::TableTooLarge` (TS-QH-05, the KnapsackSlice OOM guard). Inverting this order makes the variant addition a semver-major break.

**DryRun is a thin wrapper, not a separate feature.** Once `SelectionReport` and `run_with_trace` exist (TS-04 + TS-06), `DryRun` is approximately 10 lines that discard the item result and return only the report. It should be planned and sized as a consequence of TS-06, not as a separate effort.

**Stage timing is a free differentiator.** Because `TraceEvent` includes `duration: std::time::Duration` (matching the .NET gold standard), per-stage wall-clock timing is produced automatically by the diagnostics implementation. No other Rust context library exposes this. No extra implementation effort required beyond correct `Instant::now()` capture in `run_traced`.

**Anti-features are load-bearing.** The decision not to integrate `tracing` crate into core, not to use `Arc<dyn TraceCollector>`, not to add `async fn run()`, and not to add `#[instrument]` on pipeline internals are all well-justified and should be held firmly. A `cupel-tracing` companion crate for OTEL/tracing bridge is the correct pattern for v1.3+.

**Quality hardening is a batch, not a feature.** The 74+ open issues decompose into 7 categories (TS-QH-01 through TS-QH-07). Most are parallel — documentation gaps, `#[must_use]` audit, naming consistency, defensive copy audit, scorer test gaps, and CI drift guard. Only the KnapsackSlice DP table guard (TS-QH-05) has a sequencing dependency (requires `#[non_exhaustive]` on `CupelError` first).

### Architecture (ARCHITECTURE-RUST-DIAGNOSTICS.md)

**The recommended ownership model is `&mut dyn TraceCollector` passed to `run_traced`.** This was the conclusion after evaluating four options (shared ref, mutable ref, owned, Arc). Mutable reference per-call matches the .NET design intent (one collector per invocation), avoids `RefCell`, and enables straightforward `Vec<TraceEvent>` buffering in `DiagnosticTraceCollector`. The existing `Pipeline::run()` signature is unchanged — `run_traced` is additive.

**The `TraceCollector` trait should use extended default methods for structured item accumulation.** Rather than limiting the trait to stage events and requiring downcasting to accumulate `IncludedItem`/`ExcludedItem` records, the trait should include `record_included`, `record_excluded`, `set_total_candidates`, and `set_total_tokens_considered` with default no-op implementations. `NullTraceCollector` gets all no-ops for free. `DiagnosticTraceCollector` overrides the accumulation methods. This avoids `Any` downcasting entirely and keeps the trait object-safe.

**Pitfall note**: The PITFALLS research raises a conflict with this recommendation — if `TraceCollector` is stored on `Pipeline` as a `Box<dyn>` or `Arc<dyn>`, it must be `Send + Sync`, which forces `DiagnosticTraceCollector` to use `Mutex<Vec<TraceEvent>>`. However, ARCHITECTURE recommends per-call injection (not stored on Pipeline), which sidesteps this constraint. **The spec chapter must reconcile these two findings.** Per-call injection with `&mut dyn TraceCollector` does not require `Send + Sync` on the trait, because the reference is not stored.

**Stage integration strategy is "pure helpers, diff in orchestrator".** The six private stage functions (`classify`, `score`, `deduplicate`, `sort`, `slice`, `place`) remain pure — they take inputs and return outputs without receiving a trace reference. Exclusion attribution (which items were deduplicated, which were budget-excluded) is computed by set-diff in `Pipeline::run_traced` between stage inputs and outputs. This is O(n) overhead only when `is_enabled()` is true, and keeps all helper functions unchanged.

**Exception**: `place::place_items` overflow handling should be refactored into `run_traced`. The current overflow logic (`handle_overflow`) is inside `place_items` and discards truncated items without exposing them. Moving it to `run_traced` (~50 lines of private logic) enables `PinnedOverride` and `BudgetExceeded` exclusion reasons to be emitted without threading trace through the placer. This is an internal refactor with no public API impact.

**New module: `crate::diagnostics`.** Five source files, all namespaced under `cupel::diagnostics` to avoid polluting the crate root with diagnostic types. `SelectionReport`, `TraceCollector`, and the two concrete collectors are the ergonomic re-export candidates at the crate root level; `TraceEvent` and reasons can stay under `diagnostics::`.

**Build order is four independent phases**, each fully compilable and testable before the next begins: (1) data types (reasons, events, report structs), (2) trait + implementations (NullTraceCollector, DiagnosticTraceCollector), (3) pipeline integration (run_traced, overflow refactor), (4) polish (doc tests, serde gates, re-exports).

**No new Cargo dependencies.** All timing infrastructure uses `std::time::Instant` + `Duration`. `SelectionReport` serde support uses the existing optional `serde` dep.

### Stack / Infrastructure (STACK.md — Rust sections)

The Rust infrastructure decisions from the v1.1 milestone are stable and carry forward unchanged: MSRV `1.85` (edition 2024 minimum), `rust-toolchain.toml` at repo root, `actions-rust-lang/setup-rust-toolchain@v1` with integrated `rust-cache`, separate `ci-rust.yml` workflow with path filters, crates.io Trusted Publishing via OIDC. No stack changes required for v1.2.

The conformance vector path filter (`conformance/**` in `ci-rust.yml`) and the CI conformance drift guard (diff between `spec/conformance/required/` and `crates/cupel/tests/conformance/`) are infrastructure items that must be in place before diagnostics conformance vectors are written. The drift guard does not exist yet — it is a v1.2 CI deliverable, not a future-milestone item.

### Pitfalls (PITFALLS-RUST-DIAGNOSTICS.md)

**Five pitfalls require action before any diagnostics implementation code is written:**

1. **Spec-first gate** (Critical): No implementation PR may open until the diagnostics spec chapter is merged and reviewed. This is the single most important process constraint identified in research.

2. **`&dyn` vs generic type parameter** (Critical): The ownership model is the most load-bearing API decision. `&mut dyn TraceCollector` per-call injection avoids lifetime cascade into `Pipeline` struct. Storing `&dyn TraceCollector` on `Pipeline` adds a lifetime parameter to the struct that propagates into every container type — architecturally correct but ergonomically catastrophic.

3. **`TraceCollector: Send + Sync` constraint** (High): If the collector is ever stored on `Pipeline` (not recommended), `Pipeline: Send + Sync` requires the trait to bound `Send + Sync`, which forces `DiagnosticTraceCollector` to use `Mutex<Vec<TraceEvent>>` internally. Per-call injection sidesteps this. The spec chapter must make this decision explicit.

4. **`#[non_exhaustive]` on `CupelError` before any new variants** (High): Must be a v1.2.0 early-ship item. Adding `CupelError::TableTooLarge` (KnapsackSlice guard) before `#[non_exhaustive]` is applied is a semver-major break. The brainstorm explicitly identified this as a blocking dependency.

5. **CI conformance drift guard must precede diagnostic conformance vectors** (High): New `trace-event-*` conformance vectors must live in `spec/conformance/` first and be copied to the test directory — never the reverse. Without the CI diff check, this discipline will not hold under implementation pressure.

**Additional pitfalls requiring spec chapter specification (not implementation decisions):**

- `ExclusionReason` should be a data-carrying enum (e.g., `BudgetExceeded { item_tokens: i64, available_tokens: i64 }`) rather than a fieldless port of the C# enum. Making it fieldless initially and adding fields later is a breaking change.
- `SelectionReport` fields should be private with public accessors, following `ContextItem`'s pattern. Public fields prevent future field additions without breaking downstream struct-literal construction.
- `unreserved_capacity()` return type must be `i64` (matching existing field types), not `u32`. Subtraction can underflow if reserved slots exceed max tokens — this should return the signed result or be guarded in the constructor.
- `TraceEvent` and `ExclusionReason` enums must carry `#[non_exhaustive]` from their first crates.io publication. No exceptions for "we know the design is stable."
- The `serde` wire format for `SelectionReport` must be specified in the spec chapter, not discovered during implementation. Retrofitting JSON field names after publication is a semver-breaking change.

**Pitfalls that do NOT require action (for reference):**

- Orphan rule concerns — not applicable; all types are defined in the same crate.
- Adding `Copy` to `GreedySlice`, `KnapsackSlice`, `UShapedPlacer`, `ChronologicalPlacer` — non-breaking additions.
- Adding `Debug`/`Clone` to concrete scorers with `Box<dyn Scorer>` fields — these cannot derive `Clone` without `dyn-clone` (external dep). Do not attempt. Only the concrete non-boxing slicer/placer structs get these derives.

---

## Roadmap Implications

### Recommended Phase Structure for v1.2

**Phase A — Quick Wins & Infrastructure Prerequisites** (parallel-capable, no spec chapter dependency)

These items have no sequencing dependencies on the spec chapter and address the highest-priority CI and API hardening concerns. They can begin immediately.

- A1: `#[non_exhaustive]` on `CupelError` and `OverflowStrategy` — must ship before any new error variants or diagnostic enum publication. Audit spec book and README for exhaustive `match` patterns; add wildcard arms before merging.
- A2: `Debug`/`Clone`/`Copy` derives on `GreedySlice`, `KnapsackSlice`, `UShapedPlacer`, `ChronologicalPlacer`. Do NOT add to `CompositeScorer` or `ScaledScorer` (hold `Box<dyn Scorer>`, not `Clone`-able without external dep).
- A3: `ContextKind` convenience constructors (`ContextKind::message()`, `ContextKind::system_prompt()`, etc.) + `TryFrom<&str>` delegating to `ContextKind::new()`.
- A4: `ContextSource` convenience constructors (`ContextSource::chat()`, `ContextSource::rag()`, `ContextSource::tool()`).
- A5: `ContextBudget::unreserved_capacity() -> i64` — trivial implementation (~5 lines), return type must be `i64` not `u32`.
- A6: CI conformance drift guard — diff step between `spec/conformance/required/` and `crates/cupel/tests/conformance/`, fails CI on divergence. Required before Phase C begins.
- A7: `#[must_use]` audit — all `Result`-returning public functions and builder methods.

**Phase B — Spec Chapter: Rust Diagnostics API Contract** (blocking all Phase C work)

A single spec chapter in `/spec/` that resolves all load-bearing API decisions before implementation begins. Content requirements (non-negotiable from research):

1. Ownership model decision with rationale: `&mut dyn TraceCollector` per-call injection vs. `Pipeline<T: TraceCollector = NullTraceCollector>` generic
2. Trait receiver mutability decision: `&mut self` (direct buffer) vs. `&self` + `Mutex` interior mutability
3. Whether `TraceCollector` requires `Send + Sync`
4. `PipelineStage` enum: confirm Sort is excluded (consistent with .NET; Sort is internal, not a diagnostic boundary)
5. `ExclusionReason` variant designs — data-carrying or fieldless, with rationale
6. `SelectionReport` field access pattern — private fields + accessors (follow `ContextItem`) vs. public fields
7. `serde` wire format for all diagnostic types (field names, enum variant casing)
8. Module placement: `cupel::diagnostics` namespace, what re-exports at crate root
9. `Pipeline::run()` compatibility guarantee — must remain unchanged; `run_traced` is additive

**Phase C — Diagnostics Implementation** (strict sequential within phase, cannot be parallelized)

- C1: Data types — `diagnostics/reasons.rs` (`ExclusionReason`, `InclusionReason`), `diagnostics/events.rs` (`PipelineStage`, `TraceDetailLevel`, `TraceEvent`), `diagnostics/report.rs` (`IncludedItem`, `ExcludedItem`, `SelectionReport`), `diagnostics/mod.rs` re-exports, `lib.rs` module declaration. Unit tests for report construction (no pipeline needed yet).
- C2: Trait and implementations — `TraceCollector` trait with extended default accumulation methods, `NullTraceCollector` (ZST, verify `size_of() == 0` in test), `DiagnosticTraceCollector` with `Vec<TraceEvent>` buffer and `build_report(self) -> SelectionReport`.
- C3: Pipeline integration — refactor `place::place_items` overflow handling into `run_traced`; implement `Pipeline::run_traced`; modify `Pipeline::run` to delegate to `run_traced(&mut NullTraceCollector)`. Integration tests asserting `SelectionReport` contains correct included/excluded items with accurate reasons.
- C4: Polish — `lib.rs` re-exports, doc tests for `run_traced` usage, `serde` feature gates on all diagnostic data types, serde roundtrip tests for `SelectionReport`, `cargo test --all-features` clean pass.

**Phase D — DryRun + Budget Simulation**

- D1: `Pipeline::dry_run()` — thin wrapper over `run_traced` discarding the item list, returning `SelectionReport`. ~10 lines.
- D2: `Pipeline::marginal_items()` — two `dry_run()` calls diffed against each other. Medium complexity; plan separately from D1.

**Phase E — Quality Hardening Batch** (parallel-capable once Phase A is complete)

- E1: Rustdoc documentation gaps — `#[warn(missing_docs)]` enabled, `cargo doc --no-deps` produces zero warnings. `# Errors`, `# Panics`, `# Examples` sections on all public entry points.
- E2: `KnapsackSlice` DP table size guard — `CupelError::TableTooLarge` variant (requires Phase A1 `#[non_exhaustive]` already shipped), pre-flight check with threshold ~50M cells.
- E3: Naming consistency audit — document decisions; limit renames to clearly wrong pre-adoption-traction items only.
- E4: Defensive copy audit for `ContextItem` collection fields (`Vec<String>` tags, `HashMap<String, String>` metadata).
- E5: Scorer test gap fills — `RecencyScorer` with all-null timestamps, `TagScorer` with empty tag set, `FrequencyScorer` with uniform-frequency items.
- E6: Conformance drift guard (already Phase A6 — cross-reference only).

### Critical Path

The minimum path to diagnostics parity on crates.io:

```
Phase A (A1 only) → Phase B → C1 → C2 → C3 → C4 → publish v1.2.0
```

D1 (DryRun) can be bundled into v1.2.0 if Phase C completes with time to spare — it is genuinely thin. D2 (marginal items) is v1.3 material.

All of Phase E is parallelizable against Phase C and can merge incrementally as PRs complete. Phase A items (A2-A7) are all parallelizable with each other and with Phase B/C work.

---

## Confidence Assessment

| Finding | Confidence | Basis |
|---------|------------|-------|
| `&mut dyn TraceCollector` per-call injection as ownership model | HIGH | Both ARCHITECTURE and PITFALLS converge on this recommendation |
| Generic `T: TraceCollector` as the zero-cost alternative | HIGH | PITFALLS §2.3; monomorphization eliminates null path entirely |
| `#[non_exhaustive]` on enums required before new variants | HIGH | Rust Reference; PITFALLS §1.2, §5.3; brainstorm explicit dependency |
| `tracing` integration belongs in companion crate | HIGH | FEATURES §AF-01; Rust ecosystem convention |
| Spec-first gate is non-negotiable | HIGH | PITFALLS §4.1; brainstorm explicit process constraint |
| Extended trait with default accumulation methods | HIGH | ARCHITECTURE §6; avoids downcasting, object-safe |
| Stage helper functions remain pure (diff in orchestrator) | HIGH | ARCHITECTURE §5; minimal helper signature changes |
| `place::place_items` overflow refactor required | HIGH | ARCHITECTURE §5 (Stage 6); only way to emit truncation exclusion reasons |
| CI conformance drift guard precedes diagnostic vectors | HIGH | PITFALLS §4.2; process constraint |
| `ExclusionReason` should be data-carrying | HIGH | PITFALLS §3.2; fieldless port loses diagnostic value |
| `SelectionReport` private fields + accessors | MEDIUM | PITFALLS §3.3; consistent with `ContextItem` but inconsistent with `ScoredItem` — spec must standardize |
| DP table guard threshold (50M cells) | MEDIUM | FEATURES §TS-QH-05; heuristic, should be benchmarked |
| `TraceCollector: Send + Sync` not required with per-call injection | MEDIUM | PITFALLS §1.4; depends on ownership model chosen in spec chapter |
| DryRun as ~10-line wrapper over run_traced | MEDIUM | FEATURES §D-01; thin only if SelectionReport extraction is caller-side |
| `unreserved_capacity()` return type `i64` | MEDIUM | PITFALLS §6.3; constructor validation gap may allow underflow |

### Open Questions Requiring Spec Chapter Resolution

These are not gaps in research confidence — they are decisions that research correctly identifies as requiring explicit resolution before implementation. The research has bounded the option space and assessed trade-offs; the spec chapter must choose.

1. **Ownership model**: `&mut dyn TraceCollector` per-call vs. `Pipeline<T: TraceCollector>` generic. Both are well-understood. Both are viable. The choice is a semver commitment.
2. **Trait receiver mutability**: `&mut self` + direct `Vec` buffer vs. `&self` + `Mutex<Vec>` for `Send + Sync`. Depends on ownership model choice (per-call injection makes `Send + Sync` a non-issue).
3. **`ExclusionReason` variant payload design**: Fieldless (simple port) vs. data-carrying (`BudgetExceeded { item_tokens, available_tokens }`). Data-carrying is richer but must be specced before implementation.
4. **`SelectionReport` field visibility**: Private + accessors (follow `ContextItem`) vs. public (follow `ScoredItem`). Mixed pattern in current codebase; v1.2 should standardize.

---

## Non-Goals for v1.2 (Confirmed Anti-Features)

The following are explicitly out of scope and should not appear in the milestone backlog:

- `tracing` crate integration in `cupel` core
- `Arc<dyn TraceCollector>` shared ownership
- `async fn run()` / async pipeline
- `#[instrument]` on pipeline internals (measurable span allocation overhead at item-level granularity)
- Serializable pipeline configuration (`CupelPolicy` Rust port — requires `typetag` or equivalent, contradicts zero-dep constraint)
- Per-item callback on `DiagnosticTraceCollector` (defer to v1.3; use case not yet established)
- OpenTelemetry integration (companion crate `cupel-tracing`, post-v1.2)

---

*Research synthesized 2026-03-15. Sources: FEATURES.md (2026-03-15), ARCHITECTURE-RUST-DIAGNOSTICS.md (2026-03-15), PITFALLS-RUST-DIAGNOSTICS.md (2026-03-15), STACK.md §§12-16 (2026-03-14). Brainstorm context: 2026-03-15T12-23 session decisions incorporated via research files.*
