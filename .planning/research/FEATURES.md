# Cupel v1.2 — Features Research: Rust Diagnostics Parity & Quality Hardening

**Date**: 2026-03-15
**Dimension**: Features — Rust diagnostics/explainability + quality hardening
**Milestone**: v1.2 (subsequent to v1.1 Rust crate migration)
**Context**: The Rust `cupel-rs` crate is published on crates.io without a diagnostics
system. The .NET implementation has a full diagnostics stack (`ITraceCollector`,
`SelectionReport`, `ExclusionReason`, `DiagnosticTraceCollector`) that represents the
primary production-debuggability differentiator. This milestone closes that gap and
batches 74+ open quality issues.

---

## Baseline: What Exists vs. What Is Missing

### What the Rust Crate Has (as of v1.1)

- Complete 6-stage pipeline (`Pipeline`, `PipelineBuilder`) — synchronous, `run()` returns
  `Result<Vec<ContextItem>, CupelError>`
- 8 scorer implementations, 3 slicer implementations, 2 placer implementations
- `serde` feature flag (opt-in serialization of model types)
- Zero external runtime dependencies (only `chrono` and `thiserror`)
- 28 required conformance tests passing

### What the Rust Crate Lacks (the parity gap)

The .NET diagnostics stack — the "gold standard" — consists of:

| .NET Type | Role | In Rust? |
|-----------|------|----------|
| `ITraceCollector` (interface) | Gate + collect trace events | No |
| `NullTraceCollector` (singleton) | Zero-overhead disabled path | No |
| `DiagnosticTraceCollector` (buffered) | In-memory event collection | No |
| `TraceDetailLevel` (enum) | Stage vs. item verbosity | No |
| `TraceEvent` (record struct) | Stage event + duration | No |
| `PipelineStage` (enum) | Classify/Score/Deduplicate/Sort/Slice/Place | No |
| `SelectionReport` (record) | Per-item inclusion/exclusion reasons | No |
| `IncludedItem` / `ExcludedItem` | Item + score + reason | No |
| `InclusionReason` / `ExclusionReason` (enums) | Why included/excluded | No |
| `DryRun` capability | Simulated run without side-effects | No |

### What the .NET Crate Has Beyond Diagnostics (also missing in Rust)

- `ContextBudget.UnreservedCapacity` computed property (quick win)
- `ContextKind` convenience constructors (quick win)
- `#[non_exhaustive]` on `CupelError` and `OverflowStrategy` (API hardening)
- `KnapsackSlice` DP table size guard (safety)
- `Debug`, `Clone`, `Copy` derives on concrete slicer/placer types

---

## Ecosystem Research: Rust Library Diagnostics Patterns

### How Production Rust Libraries Expose Observability

**Confidence**: HIGH (verified against `tracing`, `tower`, and Rust API Guidelines)

**Pattern 1 — Trait-based collector with `is_enabled()` gate (dominant)**

The `tracing` crate family established the idiomatic Rust pattern: a subscriber/collector
trait with an `enabled()` predicate that allows call sites to short-circuit expensive
event construction before calling the collection method. The library does not depend on
`tracing` itself but mirrors the gating pattern.

Key property: `NullTraceCollector` as a zero-sized type (ZST) allows the compiler to
monomorphize the null path away entirely when the concrete type is statically known.
Generic-over-collector approaches (`T: TraceCollector`) enable this; `&dyn TraceCollector`
does not (it goes through vtable regardless).

**Pattern 2 — `tower`'s `Service` middleware pattern (does not apply)**

Tower instruments services by wrapping them in middleware layers. Cupel's pipeline is a
fixed-stage design, not a middleware chain — this pattern does not transfer.

**Pattern 3 — `tracing::instrument` proc-macro (does not apply to library internals)**

Proc-macro instrumentation is appropriate for user-facing async services. Cupel's pipeline
is synchronous library code. Applying `#[instrument]` to internal pipeline stages would
force a `tracing` dependency that contradicts the zero-external-dep constraint.

**Pattern 4 — `log` facade with feature flag (rejected for this use case)**

The `log` crate provides a global facade with runtime-configurable verbosity. It is
appropriate for debug logging (a write-and-forget concern), not for structured
explainability data (a collect-and-inspect concern). Cupel's `SelectionReport` contains
per-item structured data (`IncludedItem`, `ExcludedItem`, scores, reasons) that `log`
cannot represent.

**Conclusion for Cupel's Rust design:**

The correct pattern is a **trait with generic monomorphization** (not `&dyn`) for the
zero-overhead path, with a concrete `DiagnosticTraceCollector` for the hot collection
path. The `PipelineBuilder::with_trace(T: TraceCollector)` hookup stores the collector
generically, making `Pipeline<T>` typed over the collector. This adds one type parameter
to `Pipeline` but is the idiomatic zero-cost approach.

The alternative — `Box<dyn TraceCollector>` stored in `Pipeline` — is simpler to use
(no type parameter) but pays a vtable dispatch per event even when the collector is the
null case. For the null path, this is measurable overhead. For a library whose headline
feature is sub-millisecond pipeline execution, this matters.

**The `&dyn` vs. generic decision is spec-level.** The brainstorm identified this
explicitly: once the API ships on crates.io, it becomes a semver commitment. The spec
chapter for Rust diagnostics must specify the ownership model before implementation
begins.

### What crates.io Quality Standards Require

**Confidence**: HIGH (verified against Cargo Book, API Guidelines, and well-maintained
crate inspection: `serde`, `tokio`, `tracing`, `anyhow`)

| Requirement | Evidence | Priority |
|-------------|----------|----------|
| `#[non_exhaustive]` on all public enums that may grow | Rust API Guidelines C-NON-EXHAUSTIVE; any enum without it is a semver hazard | Critical |
| `Debug` derive on all public types | API Guidelines C-DEBUG; needed for `assert!` macros and test output | Required |
| `Clone` on value types (where logical) | API Guidelines C-COMMON-TRAITS; expected by users composing types | Expected |
| Rustdoc examples that actually compile and run | `cargo test` runs doctests; broken examples are broken tests | Required |
| CHANGELOG.md per SemVer spec | Informal standard; most top-1000 crates maintain one | Recommended |
| `rust-version` field in Cargo.toml | Machine-readable MSRV declaration; Cargo resolver uses it | Recommended |
| Minimal feature surface on optional deps | `serde`, `chrono` features gated correctly | Required |
| No `unwrap()` in public-path library code | Community expectation; `expect()` with context is acceptable | Expected |
| `#[must_use]` on Result-returning functions | Prevents silent ignored errors | Recommended |

**Quality anti-patterns that harm crates.io reputation:**

- Panic on invalid user input where `Result` is feasible
- Missing error context in `CupelError::InvalidBudget(String)` variant (opaque strings)
- Undocumented panics (must appear in `# Panics` rustdoc section)
- `expect()` in library code without a proof comment

---

## Table Stakes (Must Have for Rust Diagnostics Parity)

These features are the minimum for the Rust crate to provide production-debuggable
pipeline behavior equivalent to the .NET implementation.

### TS-01: `TraceCollector` Trait + `NullTraceCollector`

**What:** A `TraceCollector` trait in a new `cupel::diagnostics` module, plus a
`NullTraceCollector` zero-sized type that implements it with no-op methods.

**Rust-specific design from .NET gold standard:**

The .NET `ITraceCollector` interface has two methods: `RecordStageEvent` and
`RecordItemEvent`. The split enables `DiagnosticTraceCollector` to filter item-level
events by `TraceDetailLevel`. The Rust port should mirror this split.

```rust
pub trait TraceCollector {
    fn is_enabled(&self) -> bool;
    fn record_stage_event(&mut self, event: TraceEvent);
    fn record_item_event(&mut self, event: TraceEvent);
}

pub struct NullTraceCollector;

impl TraceCollector for NullTraceCollector {
    fn is_enabled(&self) -> bool { false }
    fn record_stage_event(&mut self, _: TraceEvent) {}
    fn record_item_event(&mut self, _: TraceEvent) {}
}
```

**Key constraint:** `NullTraceCollector` must be zero-sized. Verify with
`assert_eq!(std::mem::size_of::<NullTraceCollector>(), 0)` in a test.

**Key constraint:** The spec chapter must decide `&mut self` vs. `&self` before
implementation. `&mut self` is more honest (collection is mutation) but makes the trait
object `&mut dyn TraceCollector` which requires unique access. `&self` + interior
mutability (`RefCell` in `DiagnosticTraceCollector`) is the common workaround for shared
collectors. The pipeline is synchronous and single-threaded per call — `&mut self` with
a mutable reference threaded through stages is the cleanest approach.

**Complexity:** Low. Trait definition + ZST + enum definitions.
**Depends on:** Spec chapter (TS-05).
**Blocks:** All other diagnostics features.

---

### TS-02: `TraceEvent`, `PipelineStage`, `TraceDetailLevel`

**What:** Data types that carry trace event information, mirroring the .NET types.

From the .NET implementation:
- `TraceEvent` — stage, duration, item count, optional message
- `PipelineStage` — Classify, Score, Deduplicate, Sort, Slice, Place (6 values; note Sort
  is in Rust but not in the .NET `PipelineStage` enum — the spec chapter must reconcile)
- `TraceDetailLevel` — Stage = 0, Item = 1

**Rust representation:**

```rust
#[non_exhaustive]
pub struct TraceEvent {
    pub stage: PipelineStage,
    pub duration: std::time::Duration,
    pub item_count: usize,
    pub message: Option<String>,
}

#[non_exhaustive]
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum PipelineStage {
    Classify,
    Score,
    Deduplicate,
    Sort,
    Slice,
    Place,
}
```

Note: `TraceEvent` is `#[non_exhaustive]` as a struct to allow adding fields without
breaking matches. Duration measurement requires `std::time::Instant` at stage entry —
the pipeline stages must capture `Instant::now()` and compute elapsed duration before
recording.

**Complexity:** Low. Data types only.
**Depends on:** TS-01 (trait definition establishes where these types live).
**Blocks:** TS-03, TS-04.

---

### TS-03: `DiagnosticTraceCollector` (Buffered Collector)

**What:** A concrete `TraceCollector` implementation that buffers events in a
`Vec<TraceEvent>` and respects `TraceDetailLevel` filtering.

From the .NET implementation:
- `bool IsEnabled => true` (unlike `NullTraceCollector`)
- Filters `RecordItemEvent` based on `TraceDetailLevel`
- Optional callback (not required for Rust port in v1.2)
- Not thread-safe — one instance per pipeline call

**Design notes for Rust:**

No callback in the initial Rust port. The `.NET DiagnosticTraceCollector` has an
`Action<TraceEvent>? callback` that fires on each event. In Rust this would be
`Option<Box<dyn FnMut(&TraceEvent)>>`. This is a v1.3 differentiator — the common
use case (in-memory buffering for post-run inspection) does not need it.

```rust
pub struct DiagnosticTraceCollector {
    events: Vec<TraceEvent>,
    detail_level: TraceDetailLevel,
}

impl DiagnosticTraceCollector {
    pub fn new(detail_level: TraceDetailLevel) -> Self { ... }
    pub fn events(&self) -> &[TraceEvent] { &self.events }
}
```

**Complexity:** Low. Buffer + filter logic.
**Depends on:** TS-01, TS-02.
**Blocks:** TS-04 (SelectionReport built from events).

---

### TS-04: `SelectionReport`, `IncludedItem`, `ExcludedItem`, `InclusionReason`, `ExclusionReason`

**What:** The per-item inclusion/exclusion reporting types. This is the core
explainability output — the answer to "why was this item dropped?"

From the .NET gold standard:

```
SelectionReport {
    events: Vec<TraceEvent>,
    included: Vec<IncludedItem>,   // item + score + InclusionReason
    excluded: Vec<ExcludedItem>,   // item + score + ExclusionReason + deduplicated_against?
    total_candidates: usize,
    total_tokens_considered: i64,
}

InclusionReason: Scored | Pinned | ZeroToken
ExclusionReason: BudgetExceeded | ScoredTooLow | Deduplicated | QuotaCapExceeded
               | QuotaRequireDisplaced | NegativeTokens | PinnedOverride | Filtered
```

**Rust-specific considerations:**

- Both enums must be `#[non_exhaustive]` — the .NET versions lack this but adding new
  exclusion reasons (e.g., for `KnapsackSlice` DP table overflow) must not be breaking
- `ExcludedItem::deduplicated_against: Option<ContextItem>` is a potential allocation;
  for a diagnostics path this is acceptable
- `SelectionReport` should implement `Debug` and optionally `serde::Serialize` under the
  existing `serde` feature flag

**How the report is produced:** The `DiagnosticTraceCollector` accumulates item-level
events during the pipeline run. After `Pipeline::run()` completes, the caller calls
`collector.build_report()` (or equivalent) to produce the structured `SelectionReport`
from the raw event log.

**Complexity:** Medium. Data types are straightforward; the builder logic that converts
raw events into structured `IncludedItem`/`ExcludedItem` lists requires careful stage
sequencing to attribute reasons correctly.
**Depends on:** TS-01, TS-02, TS-03.
**Blocks:** D-01 (DryRun uses SelectionReport as its return type).

---

### TS-05: Spec Chapter: Rust Diagnostics API Contract

**What:** A language-agnostic spec chapter in `/spec/` covering the diagnostics system
API contract, ownership model, and guarantees. This is not a code deliverable — it is a
design document that blocks implementation.

**Why this is table stakes, not a differentiator:** The brainstorm explicitly established
that spec-first sequencing is mandatory for Rust diagnostics. Implementing before
speccing risks biasing the API toward Rust lifetime ergonomics rather than semantic
clarity. Once published on crates.io, the API is a semver commitment. Writing spec after
implementation is backwards.

**What the spec chapter must cover:**
1. Ownership model: generic `T: TraceCollector` vs. `&dyn TraceCollector` vs. stored
   `Box<dyn TraceCollector>` — decision with rationale
2. Mutability: `&mut self` vs. `&self` + interior mutability for event recording
3. Whether `Pipeline` becomes `Pipeline<T: TraceCollector>` or retains its current
   monomorphic form with a default `NullTraceCollector` type parameter
4. Null path zero-cost guarantee: how verified (cargo-asm, criterion)
5. `PipelineStage` enum reconciliation: Sort exists in Rust pipeline but not in .NET
   `PipelineStage` — spec must add Sort or document the divergence
6. Error reporting integration: does `CupelError::TableTooLarge` (TS-QH-05 below) get
   recorded in the trace before the error is returned?

**Complexity:** Medium (design work, not implementation). Blocks all TS-01 through TS-04.
**Depends on:** Nothing (this is the root).
**Blocks:** TS-01, TS-02, TS-03, TS-04.

---

### TS-06: `Pipeline::run_with_trace()` (or Generic Pipeline)

**What:** A way for callers to execute the pipeline with a trace collector attached.

Two shapes are possible (the spec chapter decides which):

**Shape A — separate method:**
```rust
// Existing: no trace
pub fn run(&self, items: &[ContextItem], budget: &ContextBudget)
    -> Result<Vec<ContextItem>, CupelError>

// New: with trace
pub fn run_with_trace<T: TraceCollector>(
    &self,
    items: &[ContextItem],
    budget: &ContextBudget,
    trace: &mut T,
) -> Result<(Vec<ContextItem>, SelectionReport), CupelError>
```

**Shape B — generic Pipeline:**
```rust
pub struct Pipeline<T: TraceCollector = NullTraceCollector> { ... }

impl<T: TraceCollector> Pipeline<T> {
    pub fn run(&self, items: &[ContextItem], budget: &ContextBudget)
        -> Result<RunResult, CupelError>
}

pub struct RunResult {
    pub items: Vec<ContextItem>,
    pub report: Option<SelectionReport>,  // None when T = NullTraceCollector
}
```

Shape A is simpler to use for the common case. Shape B is more idiomatic for zero-cost
generic dispatch. The spec chapter must choose.

**Complexity:** Low-Medium depending on Shape chosen.
**Depends on:** TS-01, TS-04, TS-05.
**Blocks:** D-01.

---

## Differentiators (Competitive Advantage)

These features go beyond .NET parity and add value specific to the Rust crate.

### D-01: DryRun API

**What:** Execute the pipeline without returning items — produce only the `SelectionReport`.
Mirrors the .NET `CupelPipeline.DryRun()` capability.

```rust
impl Pipeline {
    pub fn dry_run<T: TraceCollector>(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
        trace: &mut T,
    ) -> Result<SelectionReport, CupelError>
}
```

**Why a differentiator:** DryRun enables policy tuning without side effects. Users can
call `dry_run()` with candidate items to see what the pipeline would select, then adjust
scorer weights or budget parameters before committing to a production call. The .NET
implementation has this; the Rust crate currently has no equivalent.

**Complexity:** Low. Thin wrapper over `run_with_trace` that discards the item list.
**Depends on:** TS-06.
**Blocks:** D-02 (budget simulation uses DryRun).

---

### D-02: Budget Simulation Helpers (`unreserved_capacity`, `GetMarginalItems`)

**What (Tier 1 — Quick Win):** `ContextBudget::unreserved_capacity()` method — already
identified as a quick win in the brainstorm. Removes duplicated arithmetic from internal
pipeline stages.

```rust
pub fn unreserved_capacity(&self) -> i64 {
    self.max_tokens - self.output_reserve - self.reserved_slots.values().sum::<i64>()
}
```

**What (Tier 2 — Requires DryRun):** `Pipeline::marginal_items()` — items that would be
excluded at `(budget.max_tokens - slack_tokens)`. Requires a second DryRun call and diff.
This is the Rust port of the .NET `GetMarginalItems` design.

**Tier 1 complexity:** Trivial. Single method, ~5 lines.
**Tier 2 complexity:** Medium. Requires DryRun (D-01) to be stable.
**Depends on:** Tier 1 standalone; Tier 2 depends on D-01.

---

### D-03: `serde` Support for Diagnostics Types

**What:** Extend the existing `serde` feature flag to cover `SelectionReport`,
`IncludedItem`, `ExcludedItem`, `InclusionReason`, `ExclusionReason`, `TraceEvent`,
and `PipelineStage`.

This enables callers to serialize `SelectionReport` for logging, dashboards, or
offline policy analysis.

**Why a differentiator:** The .NET crate has full JSON serialization via `System.Text.Json`.
The Rust crate's `serde` feature already covers model types. Extending it to diagnostics
types completes the serialization story. No new dependencies required — `serde` is already
an optional dep.

**Rust-specific note:** `TraceEvent::duration` is `std::time::Duration`, which serde does
not serialize by default. Use `#[serde(with = "serde_duration")]` or serialize as
nanoseconds (`u64`). The latter is simpler and stable.

**Complexity:** Low. Adding `#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]`
to new types, plus Duration serialization handling.
**Depends on:** TS-01 through TS-04 (types must exist).
**Blocks:** Nothing.

---

### D-04: Rust API Future-Proofing (Quick Wins Batch)

**What:** Five small improvements that collectively harden the crate's API surface:

1. `#[non_exhaustive]` on `CupelError` — adding `TableTooLarge` (TS-QH-05) or future
   variants without this is a breaking change.
2. `#[non_exhaustive]` on `OverflowStrategy` — currently 3 variants; future strategies
   must not break exhaustive matches in user code.
3. `Debug + Clone + Copy` derives on `GreedySlice`, `KnapsackSlice`, `UShapedPlacer`,
   `ChronologicalPlacer` — enables cloning in test harnesses.
4. `ContextKind` convenience constructors (`ContextKind::message()`,
   `ContextKind::system_prompt()`, etc.) + `TryFrom<&str>`.
5. `ContextSource` convenience constructors (`ContextSource::chat()`, `ContextSource::rag()`,
   `ContextSource::tool()`).

**Why a differentiator:** These are "first-impression" improvements for crates.io users.
Writing `ContextKind::new("message").unwrap()` at every call site is a friction point
that no other context management library has. Named constructors surface in IDE
autocomplete. `#[non_exhaustive]` is the difference between a crate that can grow without
breaking users and one that cannot.

**Complexity:** All trivial. Attribute additions and derive macros. Zero logic changes.
**Depends on:** Nothing.
**Blocks:** TS-QH-05 (requires `#[non_exhaustive]` on `CupelError` first).

---

### D-05: `ContextTrace` / Stage Timing (Perf Visibility)

**What:** The `TraceEvent` type (TS-02) includes `duration: std::time::Duration`. This
means every stage records its wall-clock elapsed time. Callers inspecting `SelectionReport`
can see which pipeline stage consumed the most time — Classify, Score (most likely for
complex composite scorers), or Slice (most likely for `KnapsackSlice` with large inputs).

**Why a differentiator:** No other context management library in the Rust ecosystem
exposes per-stage timing. For users tuning composite scorer complexity or comparing
`GreedySlice` vs. `KnapsackSlice` performance on their specific item distributions,
per-stage timing is a direct debugging primitive.

**Implementation note:** Stage timing is a consequence of `TraceEvent` including
`Duration`, which is already part of the .NET gold standard. This is a "free" differentiator
that comes with the diagnostics implementation — no extra work beyond ensuring
`Instant::now()` capture happens correctly at stage entry.

**Complexity:** Effectively zero — emerges from TS-02 implementation.
**Depends on:** TS-02.

---

## Anti-Features (Deliberately Not Built)

### AF-01: `tracing` Crate Integration in Core

**Why not:** Adding `tracing` as an optional dependency to the core `cupel` crate
creates a transitive dep that cannot be easily removed. The `tracing` subscriber ecosystem
(fmt, jaeger, opentelemetry) is designed for long-running services, not in-process
explainability. Cupel's diagnostics need is structured data (`SelectionReport`), not log
streams. A `tracing` bridge belongs in a companion crate (`cupel-tracing`) that takes
both `cupel` and `tracing` as dependencies — it does not belong in core.

**Ecosystem note:** OpenTelemetry integration is a planned high-value feature for .NET
(`Wollax.Cupel.Diagnostics.OpenTelemetry`). The same pattern — companion crate bridging
`ITraceCollector` to OTEL ActivitySource — applies to Rust. This is a v1.3+ item.

---

### AF-02: `Arc<dyn TraceCollector>` (Shared Ownership)

**Why not:** `Arc<dyn TraceCollector>` enables sharing a collector across concurrent
pipeline calls without lifetime parameters. The cost is a heap allocation per pipeline
construction and an atomic reference count on every `clone()`. For a library that benchmarks
sub-millisecond pipeline execution, this overhead on the diagnostics path is unjustified.
The pipeline is synchronous and single-threaded per call — exclusive mutable access is
correct. `&mut dyn TraceCollector` (or generic `T: TraceCollector`) is the right shape.

---

### AF-03: `async fn run()` / Async Pipeline

**What not to build:** Making `Pipeline::run()` async requires choosing a runtime
(tokio, async-std, smol) or careful runtime-agnostic design. The pipeline is purely
in-memory CPU-bound work. The synchronous API is correct. This is an explicit carry-over
from the v1.1 anti-features list.

**Status:** Unchanged from v1.1. Still an anti-feature.

---

### AF-04: `tracing::instrument` on Pipeline Internals

**Why not:** `#[instrument]` is a proc-macro that attaches `tracing::Span` creation to
every instrumented function. At item-level granularity (Score called once per item), this
produces thousands of span allocations per pipeline call. The cost of span creation in
tracing is measurable (~100-300ns per span). For 500 items through the Score stage, that
is 50-150µs of overhead just for span creation — 10-50% of the target total pipeline
latency. Do not instrument internal pipeline stages with `#[instrument]`.

---

### AF-05: Serializable Pipeline Configuration

**Why not:** The .NET crate has `CupelPolicy` (declarative config), `CupelPolicies`
(named presets), and JSON policy serialization. Scorers, slicers, and placers are trait
objects (`Box<dyn Scorer>`) — general trait object serialization in Rust requires
`typetag` or similar. This introduces design complexity and a dependency that contradicts
the zero-external-dep goal. Policy serialization is a post-v1.2 item.

---

### AF-06: Per-Item Callback on `DiagnosticTraceCollector`

**Why not (for v1.2):** The .NET `DiagnosticTraceCollector` has an
`Action<TraceEvent>? callback` that fires synchronously on each recorded event. In Rust
this is `Option<Box<dyn FnMut(&TraceEvent)>>`. The common use case — in-memory buffering
for post-run `SelectionReport` inspection — does not need it. Adding it in v1.2 commits
a closure storage pattern to the API surface before understanding the real use cases.
Defer to v1.3.

---

## Quality Hardening Features (Batch)

The 76 open issues in `.planning/issues/open/` are the raw material. The brainstorm
identified them as a dedicated quality hardening phase, not individual milestone items.
The features below represent categories, not individual issues.

### TS-QH-01: XML/Rustdoc Documentation Gaps

**What:** Every public item in `cupel-rs` must have a non-empty doc comment. Current
gaps identified in issues (e.g., `007-contextitem-xml-docs.md`): field-level docs on
model types, `# Errors` sections on fallible functions, `# Panics` sections on methods
that can panic, `# Examples` on high-traffic entry points.

**Rust-specific standard:** `cargo doc --no-deps` must produce no warnings when built
with `#[warn(missing_docs)]` enabled. The `.NET` standard is XML summary on every public
member.

**Complexity:** Low per item, medium in aggregate (40+ items).

---

### TS-QH-02: `#[must_use]` Audit

**What:** Every `Result`-returning public function and every builder method must carry
`#[must_use]`. Currently `Pipeline::builder()`, `PipelineBuilder::build()`, and all model
constructors lack it.

**Why:** `#[must_use]` on `Result` causes the compiler to emit a warning if the caller
drops the result. This is the difference between a silent logic bug and a compile-time
catch.

**Complexity:** Trivial. Attribute annotation only.

---

### TS-QH-03: Naming Consistency Audit

**What:** Several naming inconsistencies exist between Rust idioms and the current API:
- `ContextItemBuilder::build()` vs. Rust convention of returning `Result<T, E>` (already correct)
- `Pipeline::run()` vs. `execute()` — run is more idiomatic in Rust
- `reserved_slots` field naming (issue `2026-03-14-explicit-enum-integer-assignments.md` area)

Audit and document decisions. Changes to public names are breaking — only rename items
that are clearly wrong before the crate has significant downstream usage.

**Complexity:** Low (audit + documentation), Medium if renames are needed (semver).

---

### TS-QH-04: Defensive Copy on Collection Parameters

**What:** Issue `009-contextitem-defensive-copy-collections.md` identifies that
`ContextItem` holds `Vec<String>` for tags and `HashMap<String, String>` for metadata.
Currently both are cloned from builder input. Verify that no path exposes interior
mutability or aliased references that could allow callers to mutate items after
construction.

**Complexity:** Low. Audit + tests.

---

### TS-QH-05: `KnapsackSlice` DP Table Size Guard

**What:** Add `CupelError::TableTooLarge` variant and a pre-flight check in `KnapsackSlice`
before allocating the DP table. Guard condition: `capacity × n > 50_000_000` cells.

**Why this is quality hardening, not a feature:** The current behavior (silent OOM crash)
is a defect. Making it a recoverable error is the correct fix. The guard fires only on
non-default `bucket_size` configurations.

**Dependency:** Requires `#[non_exhaustive]` on `CupelError` (D-04 item 1) to be
non-breaking.

**Complexity:** Low. One pre-flight check + one new error variant.

---

### TS-QH-06: Scorer Test Gaps

**What:** Issue `scorer-test-gaps.md` identifies missing test coverage for edge cases
in individual scorers. Specifically: `RecencyScorer` with all-null timestamps, `TagScorer`
with empty tag set, `FrequencyScorer` with all items having the same frequency.

**Complexity:** Low. Test additions only.

---

### TS-QH-07: CI-Enforced Conformance Vector Drift Guard

**What:** A CI step that diffs `conformance/required/` against
`crates/cupel/tests/conformance/required/` and fails on divergence. Identified in the
brainstorm as a follow-up to the quick-wins conformance vector cleanup.

**Why:** The vendored copy in `crates/cupel/tests/conformance/` is a known duplication
point. Without an automated guard, spec edits will silently miss the vendored copy.

**Complexity:** Trivial. One CI step, two `diff -r` calls.

---

## Feature Dependency Map for v1.2

```
[TS-05] Spec chapter: Rust diagnostics API contract
    └── [TS-01] TraceCollector trait + NullTraceCollector
          └── [TS-02] TraceEvent, PipelineStage, TraceDetailLevel
                └── [TS-03] DiagnosticTraceCollector
                      └── [TS-04] SelectionReport, IncludedItem, ExcludedItem, Reasons
                            └── [TS-06] Pipeline::run_with_trace() / generic Pipeline
                                  └── [D-01] DryRun API
                                        └── [D-02 Tier 2] Budget simulation helpers

[D-04] API future-proofing (non_exhaustive, derives, constructors)
    └── [TS-QH-05] KnapsackSlice DP table guard (needs CupelError::TableTooLarge)

[D-03] serde for diagnostics types
    └── [TS-04] (types must exist first)

[D-02 Tier 1] unreserved_capacity helper — standalone, no dependencies
[D-05] Stage timing — emerges from TS-02 implementation

[TS-QH-01] Doc gaps — standalone
[TS-QH-02] #[must_use] audit — standalone
[TS-QH-03] Naming audit — standalone
[TS-QH-04] Defensive copy audit — standalone
[TS-QH-06] Scorer test gaps — standalone
[TS-QH-07] CI drift guard — standalone
```

**Critical path for diagnostics:**
TS-05 (spec) → TS-01 → TS-02 → TS-03 → TS-04 → TS-06 → D-01

**Critical path for quality hardening:**
D-04 (#1: `#[non_exhaustive]` on `CupelError`) → TS-QH-05 (table guard)
All other QH items are parallel.

---

## Confidence Levels by Finding

| Finding | Confidence | Basis |
|---------|------------|-------|
| Generic `T: TraceCollector` over `&dyn` for zero-cost null path | HIGH | Rust monomorphization docs, tracing crate architecture |
| `#[non_exhaustive]` required on all growing public enums | HIGH | Rust API Guidelines C-NON-EXHAUSTIVE |
| `tracing` integration belongs in companion crate, not core | HIGH | tracing crate design, Rust ecosystem convention |
| `Arc<dyn>` is wrong for synchronous single-threaded pipeline | HIGH | Basic Rust ownership model |
| Spec-first sequencing for Rust diagnostics | HIGH | Brainstorm decision (non-negotiable) |
| `&mut self` for `record_stage_event` is the honest choice | MEDIUM | Architectural preference; spec chapter may override |
| `TraceEvent` as struct with `#[non_exhaustive]` (not enum per stage) | MEDIUM | Mirrors .NET `TraceEvent` record struct |
| `Option<SelectionReport>` vs. always-present in RunResult | MEDIUM | Spec chapter decision; both shapes are defensible |
| DP table guard threshold of 50M cells | MEDIUM | Heuristic from brainstorm; benchmark to calibrate |

---

## Summary Table

| ID | Feature | Category | Priority | Complexity | Blocks |
|----|---------|----------|----------|------------|--------|
| TS-05 | Spec: Rust diagnostics API contract | Table Stakes | P0 | Medium | TS-01..TS-06 |
| TS-01 | TraceCollector trait + NullTraceCollector | Table Stakes | P0 | Low | TS-02..TS-06 |
| TS-02 | TraceEvent, PipelineStage, TraceDetailLevel | Table Stakes | P0 | Low | TS-03, TS-04 |
| TS-03 | DiagnosticTraceCollector | Table Stakes | P0 | Low | TS-04 |
| TS-04 | SelectionReport + item records + reasons | Table Stakes | P0 | Medium | TS-06 |
| TS-06 | Pipeline::run_with_trace() / generic Pipeline | Table Stakes | P0 | Low-Med | D-01 |
| D-04 | API future-proofing (#[non_exhaustive], derives, constructors) | Differentiator | P1 | Trivial | TS-QH-05 |
| D-01 | DryRun API | Differentiator | P1 | Low | D-02 T2 |
| D-02 (T1) | unreserved_capacity helper | Differentiator | P1 | Trivial | — |
| D-03 | serde for diagnostics types | Differentiator | P2 | Low | — |
| D-05 | Stage timing visibility | Differentiator | P2 | Zero (free from TS-02) | — |
| D-02 (T2) | Budget simulation (marginal items) | Differentiator | P3 | Medium | D-01 |
| AF-01 | tracing crate in core | Anti-Feature | Never | N/A | — |
| AF-02 | Arc<dyn TraceCollector> | Anti-Feature | Never | N/A | — |
| AF-03 | Async pipeline | Anti-Feature | Never | N/A | — |
| AF-04 | #[instrument] on pipeline internals | Anti-Feature | Never | N/A | — |
| AF-05 | Serializable pipeline config | Anti-Feature | Post-v1.2 | High | — |
| AF-06 | Per-item callback on DiagnosticTraceCollector | Anti-Feature | Post-v1.2 | Low | — |
| TS-QH-01 | Rustdoc documentation gaps | Quality Hardening | P1 | Low-Med | — |
| TS-QH-02 | #[must_use] audit | Quality Hardening | P1 | Trivial | — |
| TS-QH-03 | Naming consistency audit | Quality Hardening | P1 | Low | — |
| TS-QH-04 | Defensive copy audit | Quality Hardening | P1 | Low | — |
| TS-QH-05 | KnapsackSlice DP table guard | Quality Hardening | P1 | Low | — |
| TS-QH-06 | Scorer test gaps | Quality Hardening | P1 | Low | — |
| TS-QH-07 | CI conformance drift guard | Quality Hardening | P1 | Trivial | — |

---

*Research completed 2026-03-15. Based on: .NET gold standard code inspection
(`Wollax.Cupel/Diagnostics/`), Rust crate source inspection (`crates/cupel/src/`),
brainstorm decisions (2026-03-15T12-23), Rust API Guidelines, tracing crate architecture,
and 76 open issue analysis.*
