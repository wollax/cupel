# Brainstorm Summary: Cupel Next Milestone

**Session:** 2026-03-15T12-23
**Pairs:** 3 (quick-wins, high-value, radical)
**Rounds:** 2 each
**Total proposals:** 18 entered → 13 survived debate

---

## Quick Wins (5 proposals survived)

Low-effort, high-impact improvements. [Full report](quickwins-report.md)

| # | Proposal | Scope | Key Constraint |
|---|----------|-------|----------------|
| 1 | Rust API future-proofing (`#[non_exhaustive]`, derives) | Small | Cut Clone on CompositeScorer/ScaledScorer (needs dyn-clone) |
| 2 | Spec conformance vector cleanup (5 TOML files) | Small | Must update both spec/ and vendored copies simultaneously |
| 3 | ContextKind convenience constructors (Rust) | Small | DX improvement only — existing `new()` was never unsafe |
| 4 | `UnreservedCapacity` helper on ContextBudget | Small | Both languages; docs must clarify pinned items not included |
| 5 | KnapsackSlice DP table size guard | Small | Blocked on #1 for CupelError variant; only fires on small bucket_size |

**Process recommendation:** Batch 40+ mechanical issues (XML docs, naming, test helpers) into a dedicated quality hardening phase.

**Dropped:** CI dependency caching — file as standalone task, not a milestone feature.

---

## High-Value Features (5 proposals survived, 1 design-only)

Substantial features worth significant investment. [Full report](highvalue-report.md)

| # | Proposal | Scope | Key Constraint |
|---|----------|-------|----------------|
| 1 | Rust diagnostics parity (TraceCollector, SelectionReport) | Medium-Large | Spec-first mandatory; `&dyn` vs `Arc<dyn>` is spec-level decision |
| 2 | `Cupel.Testing` package (fluent assertion chains) | Medium | Vocabulary design phase first (10-15 named patterns with specs) |
| 3 | OpenTelemetry integration package | Medium | Stage-only default; item-level opt-in; `cupel.*` namespace pre-stable |
| 4 | Budget simulation (`GetMarginalItems`, `FindMinBudgetFor`) | Medium | `FindMinBudgetFor` requires monotonicity guard against QuotaSlice |
| 5 | `DecayScorer` with `TimeProvider` injection | Medium | `TimeProvider` mandatory (no silent default); `IDecayCurve` internal |
| 6 | Count-based quotas — design phase only | Design | Pinned interaction, tag non-exclusivity, knapsack path all unresolved |

**Key architectural decisions from debate:**
- Spec-first for Rust diagnostics (lifetime constraints bias API if spec follows implementation)
- `TimeProvider` injection over constructor-captured time (staleness in long-lived pipelines)
- Extension methods over wrapper types for SelectionReport analytics
- `SweepBudget` belongs in Smelt, not Cupel

**Reshaped:** `ContextPressure` wrapper type → 3 extension methods (`BudgetUtilization`, `KindDiversity`, `ExcludedItemCount`)
**Moved to Smelt:** `SweepBudget` (linear budget stepping encodes consumer-specific assumptions)

---

## Radical Ideas (3 proposals survived)

New directions and paradigm shifts. [Full report](radical-report.md)

| # | Proposal | Scope | Key Constraint |
|---|----------|-------|----------------|
| 1 | Metadata convention system (`"cupel:<key>"` namespace) | Small | Spec addition + `MetadataTrustScorer`; no trust gates (scoring only) |
| 2 | `ProfiledPlacer` — caller-provided attention profiles | Medium | Companion package; no Cupel-owned profiles; staleness warnings |
| 3 | Context fork diagnostic — policy sensitivity analysis | Small-Medium | Developer-time tool only, not runtime; thin orchestration over DryRun |

**Rejected:**
- **Context Protocol** — distributed agent negotiation is Smelt's job, not context selection
- **Inverse Context Synthesis** — requires context catalog (Assay's job); `IntentSpec` as preset selector may be worth 20 lines later

**Shared theme:** All surviving radical ideas are additive — no new pipeline stages, no violation of ordinal-only principle, no external dependencies in core.

---

## Cross-Cutting Themes

1. **Spec-first development** — Multiple proposals (Rust diagnostics, testing vocabulary, count quotas, metadata conventions) require spec work before implementation. The spec is becoming a load-bearing artifact.

2. **Rust parity gap** — The Rust crate lacks diagnostics/explainability, the core .NET differentiator. Closing this gap is the highest-impact work for Rust adoption.

3. **Companion package ecosystem** — Several proposals (Testing, OpenTelemetry, Profiles) extend Cupel via companion packages without touching core. The package architecture from v1.0 is paying off.

4. **Smelt boundary clarity** — Debate consistently identified features that belong in Smelt (SweepBudget, Context Protocol, Inverse Synthesis). The Cupel/Smelt boundary is well-understood.

5. **Quality debt** — 74 open issues, mostly mechanical. A dedicated hardening phase would clean the foundation before adding features.

---

## Recommended Sequencing

**Near-term (v1.2 candidates):**
- Quick wins 1-5 (Rust API hardening, spec cleanup, convenience APIs, safety guards)
- Quality hardening phase (batch mechanical issues)
- Metadata convention system (small, high ecosystem value)
- DecayScorer (standalone scorer, well-specified)
- SelectionReport extension methods (3 methods, no new types)

**Medium-term (v1.2 or v1.3):**
- Rust diagnostics (spec-first, then implementation)
- Cupel.Testing vocabulary design + implementation
- OpenTelemetry integration
- Budget simulation APIs
- Fork diagnostic (developer tool)

**Longer-term (v1.3+):**
- ProfiledPlacer companion package
- Count-based quota implementation (after design phase)

---

*3 pairs, 2 rounds each, 18 proposals → 13 survived. 2 reshaped. 3 rejected as Smelt/Assay concerns.*
