# Architecture Research: Rust Diagnostics Integration

**Scope**: Adding `TraceCollector` trait and `SelectionReport` to the existing `cupel` Rust crate.
**Constraint**: No external dependencies beyond existing (`chrono`, `serde` optional, `thiserror`).
**Source read**: All 30 source files across `model/`, `pipeline/`, `scorer/`, `slicer/`, `placer/` plus
the .NET `Diagnostics/` namespace as the reference implementation.

---

## 1. Current Architecture Summary

The Rust pipeline is a pure function: `Pipeline::run(&self, items: &[ContextItem], budget: &ContextBudget) -> Result<Vec<ContextItem>, CupelError>`.

Internally, six private module functions are called in sequence:

```
classify::classify(items, budget)
  -> (Vec<ContextItem>, Vec<ContextItem>)          // (pinned, scoreable)
score::score_items(&scoreable, scorer)
  -> Vec<ScoredItem>
deduplicate::deduplicate(scored, enabled)
  -> Vec<ScoredItem>
sort::sort_scored(deduped)
  -> Vec<ScoredItem>
slice::slice_items(&sorted, budget, pinned_tokens, slicer)
  -> Vec<ContextItem>
place::place_items(&pinned, &sliced, &sorted_scored, budget, overflow_strategy, placer)
  -> Result<Vec<ContextItem>, CupelError>
```

Key structural facts:
- `Pipeline` owns its strategies as `Box<dyn Scorer>`, `Box<dyn Slicer>`, `Box<dyn Placer>` — stored in the `Pipeline` struct.
- `Scorer`, `Slicer`, `Placer` traits are all `Send + Sync` (`Scorer` also `: Any`).
- All pipeline-internal functions are `pub(crate)`. Only `Pipeline::run` and the `PipelineBuilder` are public.
- `ScoredItem` is a public struct with public fields `item: ContextItem` and `score: f64`.
- `ContextItem` is `Clone` + `Debug` + `PartialEq`. `ScoredItem` is `Clone` + `Debug`.
- The crate has zero runtime dependencies beyond `chrono` and `thiserror`. `serde` is feature-gated.

---

## 2. .NET Reference: What Diagnostics Does

The .NET `ITraceCollector` is threaded through `Pipeline::Execute` as an optional parameter (defaulting to `NullTraceCollector.Instance`). It has two concerns:

1. **Stage-level tracing**: Records a `TraceEvent` (stage, duration, item count) after each of the 5 named stages (Classify, Score, Deduplicate, Slice, Place). The .NET version uses `Stopwatch` for wall-clock timing.

2. **Item-level tracing**: Records individual inclusion/exclusion decisions. This is the richer data needed to build a `SelectionReport`. The `ReportBuilder` accumulates `IncludedItem` and `ExcludedItem` records across all stages and is only active when a `DiagnosticTraceCollector` (not `NullTraceCollector`) is passed.

The .NET pipeline detects whether diagnostics are active via `ITraceCollector.IsEnabled` to avoid allocating event payloads on disabled code paths.

The `SelectionReport` is returned as part of `ContextResult` (alongside `Items`) when a `DiagnosticTraceCollector` was active.

---

## 3. Ownership Model Decision: `&mut dyn TraceCollector`

### Options considered

**Option A: `&dyn TraceCollector` (shared reference)**
- Pros: Zero cost, no cloning, consistent with how `scorer`/`slicer`/`placer` are passed internally (`as_ref()`).
- Cons: Methods must take `&self`, which means internal mutation requires `RefCell` or similar interior mutability. A buffering collector (the diagnostic case) needs to accumulate events — requiring `RefCell<Vec<TraceEvent>>` inside.
- Verdict: Viable but adds complexity inside the collector implementation. `RefCell` is not `Sync`, which matters if the collector is also used across threads.

**Option B: `&mut dyn TraceCollector` (exclusive mutable reference)**
- Pros: Methods take `&mut self`. A `Vec<TraceEvent>` buffer in the diagnostic collector is straightforward — no interior mutability. Idiomatic for single-threaded accumulation.
- Cons: The caller must hold a `&mut` reference, meaning the collector cannot be shared across concurrent pipeline runs. This mirrors the .NET guidance ("not thread-safe; create one per execution").
- Verdict: **Recommended.** Matches the .NET design intent (one collector per invocation), avoids `RefCell`, and is the idiomatic Rust pattern for mutable accumulation across a single function call.

**Option C: `Box<dyn TraceCollector>` (owned)**
- Pros: Stored inside `Pipeline` like `scorer`/`slicer`/`placer`.
- Cons: A collector is logically per-invocation, not per-pipeline-configuration. Storing it on `Pipeline` would prevent concurrent calls on the same pipeline (breaks the `Send + Sync` story) and prevents the caller from inspecting the collector after `run` completes.
- Verdict: Wrong abstraction boundary. The .NET version correctly passes the collector at call time, not build time.

**Option D: `Arc<dyn TraceCollector>` (shared ownership)**
- Pros: Cloneable, can be shared across concurrent pipelines.
- Cons: Requires internal `Mutex` or `RwLock` for mutation. This is heavyweight for a primarily single-threaded diagnostic use case.
- Verdict: Over-engineered. Only consider if async/multi-pipeline concurrency is a stated requirement.

### Final recommendation: `&mut dyn TraceCollector` passed to `Pipeline::run`

```rust
pub fn run(
    &self,
    items: &[ContextItem],
    budget: &ContextBudget,
    trace: &mut dyn TraceCollector,
) -> Result<Vec<ContextItem>, CupelError>
```

The `NullTraceCollector` implementation is a unit struct; callers who do not want diagnostics pass `&mut NullTraceCollector`. The diagnostic variant owns a `Vec<TraceEvent>` and a `ReportBuilder`-equivalent that is inspected after `run` returns.

To avoid breaking the current zero-argument call signature, a separate `Pipeline::run_traced` can be introduced initially, with `run` becoming a thin wrapper:

```rust
pub fn run(&self, items: &[ContextItem], budget: &ContextBudget)
    -> Result<Vec<ContextItem>, CupelError>
{
    self.run_traced(items, budget, &mut NullTraceCollector)
}

pub fn run_traced(
    &self,
    items: &[ContextItem],
    budget: &ContextBudget,
    trace: &mut dyn TraceCollector,
) -> Result<Vec<ContextItem>, CupelError>
```

This is a **non-breaking extension** — existing callers of `run` continue to compile unchanged.

---

## 4. New Types Required

### 4.1 `PipelineStage` (enum)

Mirrors .NET `PipelineStage`. Note: .NET omits Sort (it has no user-visible duration worth recording); Rust can do the same.

```rust
pub enum PipelineStage {
    Classify,
    Score,
    Deduplicate,
    Slice,
    Place,
}
```

No `Sort` variant — the sort is internal and not a meaningful diagnostic boundary.

### 4.2 `TraceEvent` (struct)

Rust does not have wall-clock `Stopwatch` in std. Stage duration should use `std::time::Duration` derived from `std::time::Instant`. The .NET version explicitly documents that item-level `TraceEvent` has `Duration::Zero` — the same convention applies here.

```rust
pub struct TraceEvent {
    pub stage: PipelineStage,
    pub duration: std::time::Duration,
    pub item_count: usize,
    pub message: Option<String>,
}
```

**No dependency needed**: `std::time::Instant` and `Duration` are in stdlib.

### 4.3 `ExclusionReason` (enum)

Direct port of the .NET enum, same variants. Rust currently handles the following exclusion cases:
- `classify`: negative tokens (items with `tokens < 0` are silently skipped)
- `classify`: pinned items do not flow through scoring/slicing
- `deduplicate`: content-identical items are removed
- `slice`: items sorted out by budget constraint
- `place/handle_overflow`: items dropped during `Truncate` overflow handling

```rust
pub enum ExclusionReason {
    BudgetExceeded,
    ScoredTooLow,      // reserved, not currently emitted
    Deduplicated,
    QuotaCapExceeded,  // reserved, not currently emitted
    QuotaRequireDisplaced, // reserved
    NegativeTokens,
    PinnedOverride,
    Filtered,          // reserved
}
```

### 4.4 `InclusionReason` (enum)

```rust
pub enum InclusionReason {
    Scored,
    Pinned,
    ZeroToken,
}
```

### 4.5 `ExcludedItem` (struct)

```rust
pub struct ExcludedItem {
    pub item: ContextItem,
    pub score: f64,
    pub reason: ExclusionReason,
    pub deduplicated_against: Option<ContextItem>,
}
```

Owns `ContextItem` (already `Clone`). `deduplicated_against` is `Some` only when `reason == Deduplicated`.

### 4.6 `IncludedItem` (struct)

```rust
pub struct IncludedItem {
    pub item: ContextItem,
    pub score: f64,
    pub reason: InclusionReason,
}
```

### 4.7 `SelectionReport` (struct)

```rust
pub struct SelectionReport {
    pub events: Vec<TraceEvent>,
    pub included: Vec<IncludedItem>,
    pub excluded: Vec<ExcludedItem>,
    pub total_candidates: usize,
    pub total_tokens_considered: i64,
}
```

All fields are owned. No lifetime parameters — the report is fully owned and can be returned from `run_traced` or inspected from the collector after the call. The `excluded` vec is sorted by score descending (matching .NET `ReportBuilder` behavior).

### 4.8 `TraceCollector` (trait)

```rust
pub trait TraceCollector {
    fn is_enabled(&self) -> bool;
    fn record_stage_event(&mut self, event: TraceEvent);
    fn record_item_event(&mut self, event: TraceEvent);
}
```

`&mut self` on both `record_*` methods — this is what enables the buffering collector to push into a `Vec` without interior mutability.

`is_enabled()` takes `&self` (non-mutating) so callers can cheaply check before constructing event payloads:

```rust
if trace.is_enabled() {
    trace.record_stage_event(TraceEvent { ... });
}
```

### 4.9 `NullTraceCollector` (unit struct)

```rust
pub struct NullTraceCollector;

impl TraceCollector for NullTraceCollector {
    fn is_enabled(&self) -> bool { false }
    fn record_stage_event(&mut self, _: TraceEvent) {}
    fn record_item_event(&mut self, _: TraceEvent) {}
}
```

Zero-cost. Because `is_enabled` returns `false`, all call sites guarded by `if trace.is_enabled()` short-circuit without constructing any `TraceEvent` values.

### 4.10 `DiagnosticTraceCollector` (struct)

```rust
pub struct DiagnosticTraceCollector {
    events: Vec<TraceEvent>,
    included: Vec<(IncludedItem, usize)>,   // (item, insertion_index)
    excluded: Vec<(ExcludedItem, usize)>,   // for stable sort on build
    total_candidates: usize,
    total_tokens_considered: i64,
    detail_level: TraceDetailLevel,
}
```

Exposes `build_report(self) -> SelectionReport` to consume the collector and produce the report. This avoids keeping the collector alive after `run_traced` returns.

Alternatively, `report(&self) -> SelectionReport` with `Clone` on the item types — but consuming is simpler and avoids a gratuitous `Clone`.

### 4.11 `TraceDetailLevel` (enum)

```rust
pub enum TraceDetailLevel {
    Stage = 0,
    Item = 1,
}
```

Same as .NET. `Item` enables per-item `record_item_event` calls.

---

## 5. Integration Points: Where Each Stage Emits Events

Reading the actual pipeline code (all `pub(crate)` functions in `pipeline/`), here is where trace calls must be added:

### Stage 1: `classify::classify` → `pipeline/mod.rs` call site

The classify function is currently a pure function. The trace call should happen **in `Pipeline::run_traced`** after calling `classify()`, not inside the function itself. This avoids threading `trace` into every helper:

```rust
let (pinned, scoreable) = classify::classify(items, budget)?;
// Item-level: items with negative tokens (currently silently skipped)
// Stage-level:
if trace.is_enabled() {
    trace.record_stage_event(TraceEvent {
        stage: PipelineStage::Classify,
        duration: /* elapsed since stage start */,
        item_count: pinned.len() + scoreable.len(),
        message: None,
    });
}
```

**Problem**: `classify` currently silently drops items with `tokens < 0`. For item-level tracing, we need to know which items were dropped. Options:
- Option A: Modify `classify` to return a third `Vec<ContextItem>` of negatively-skipped items. This changes the function signature.
- Option B: Thread `trace: &mut dyn TraceCollector` into `classify` and emit item events internally.
- Option C: Pre-scan items in `run_traced` before calling `classify`, recording negative-token items into the trace, then call `classify` as before.

**Recommendation**: Option C — pre-scan in `run_traced`. This avoids changing `classify`'s signature and does not thread trace into every helper. The pre-scan is O(n) and adds no meaningful overhead.

### Stage 2: `score::score_items`

Stage event emitted after calling `score_items`. Item-level events (individual scores) emitted in the stage call site loop:

```rust
if trace.is_enabled() && detail_level >= Item {
    for si in &scored {
        trace.record_item_event(TraceEvent {
            stage: PipelineStage::Score,
            duration: Duration::ZERO,
            item_count: 1,
            message: Some(format!("score={:.4}", si.score)),
        });
    }
}
```

Or, encode score differently — `message` is a `String` and somewhat lossy. The `DiagnosticTraceCollector` accumulates the inclusion/exclusion data in a more structured form directly from the pipeline via separate collector methods (`add_included`, `add_excluded`) rather than from `TraceEvent.message`. See Section 6.

### Stage 3: `deduplicate::deduplicate`

This is where `ExclusionReason::Deduplicated` events are emitted. Currently `deduplicate` returns the surviving `Vec<ScoredItem>`. The losers are discarded.

To report which items were deduplicated against which survivors, the function must expose that information. Options:
- Option A: Thread `trace` into `deduplicate`. Clean but spreads the trace dependency.
- Option B: Return both survivors and losers from `deduplicate`. Change signature to `-> (Vec<ScoredItem>, Vec<(ScoredItem, ScoredItem)>)` where the second element is `(loser, winner)` pairs.
- Option C: Accumulate into a trait object passed by mutable reference, calling `trace.add_excluded(...)` inside `deduplicate`.

**Recommendation**: Option B when tracing is active. Implement a new `deduplicate_traced` variant or add a flag. Alternatively, keep `deduplicate` pure and compute the exclusion list in `run_traced` by diffing input and output. The diff approach is O(n) with a `HashSet` on content strings and avoids changing the helper signature:

```rust
let deduped = deduplicate::deduplicate(scored, self.deduplication);
// Trace-only: find which items were removed
if trace.is_enabled() {
    let surviving_contents: HashSet<&str> = deduped.iter()
        .map(|si| si.item.content())
        .collect();
    for si in &pre_dedup_scored {
        if !surviving_contents.contains(si.item.content()) {
            // find the winner for DeduplicatedAgainst
            let winner = deduped.iter()
                .find(|s| s.item.content() == si.item.content());
            // emit exclusion
        }
    }
}
```

This keeps the helper functions pure and minimizes changes to the internal API.

### Stage 4: Sort

No stage event emitted (consistent with .NET — Sort is not a named `PipelineStage`). No item-level events.

### Stage 5: `slice::slice_items`

Stage event after slicing. Item-level exclusions (`BudgetExceeded`) emitted by comparing `sorted` input to `sliced` output — again using a set diff in `run_traced`:

```rust
let sliced = slice::slice_items(&sorted, budget, pinned_tokens, self.slicer.as_ref());
if trace.is_enabled() {
    let sliced_contents: HashSet<&str> = sliced.iter()
        .map(|i| i.content())
        .collect();
    for si in &sorted {
        if !sliced_contents.contains(si.item.content()) {
            // emit BudgetExceeded exclusion
        }
    }
}
```

**Note**: The Rust pipeline does not pass `trace` into `Slicer::slice`. The .NET `ISlicer.Slice` receives `ITraceCollector` — this is because `QuotaSlice` emits warnings about pinned items exceeding quota caps. For the Rust `QuotaSlice`, this warning is currently not implemented. When `QuotaSlice` tracing is added, the `Slicer::slice` signature can be extended with `trace: &mut dyn TraceCollector`. For now, keep `Slicer::slice` unchanged and emit what can be inferred in `run_traced`.

### Stage 6: `place::place_items`

Inclusion events emitted after placement. `BudgetExceeded` or `PinnedOverride` exclusions during `Truncate` overflow are also emitted here. Stage event recorded after `place_items` returns.

The inclusion categorization logic from the .NET pipeline (pinned → `InclusionReason::Pinned`, zero-token → `InclusionReason::ZeroToken`, otherwise → `InclusionReason::Scored`) can be computed in `run_traced` from the returned `Vec<ContextItem>` by checking `item.pinned()` and `item.tokens() == 0`.

**Challenge**: `place_items` currently handles overflow internally (`handle_overflow`). Items dropped during `Truncate` are not returned. To emit `PinnedOverride` or `BudgetExceeded` reasons for truncated items, either:
- Return the truncated items alongside the result, or
- Move overflow handling into `run_traced` where trace is available.

**Recommendation**: Move overflow detection and handling logic from `place::place_items` into `run_traced`, leaving `place::place_items` as a pure placement function (merge + delegate to placer, no overflow). This is a refactor of one private function with no public API impact. The overflow logic is only ~50 lines.

---

## 6. Data Flow for Diagnostics

The recommended data flow is that `DiagnosticTraceCollector` has two distinct accumulation surfaces:

1. **`record_stage_event`** — receives `TraceEvent` structs (stage, duration, count). These are emitted from `run_traced` via `if trace.is_enabled()` guards.

2. **Direct accumulation methods** — `add_included`, `add_excluded` — called from `run_traced` to build the `SelectionReport`. These are not part of the `TraceCollector` trait (which stays minimal); they are concrete methods on `DiagnosticTraceCollector`.

This means `run_traced` needs to downcast `&mut dyn TraceCollector` to `&mut DiagnosticTraceCollector` to call the accumulation methods — or `TraceCollector` gains `add_included`/`add_excluded` methods on the trait, or a parallel accumulator is passed.

**Cleanest solution**: Give `TraceCollector` a method that returns an optional `SelectionReportAccumulator`:

```rust
pub trait TraceCollector {
    fn is_enabled(&self) -> bool;
    fn record_stage_event(&mut self, event: TraceEvent);
    fn record_item_event(&mut self, event: TraceEvent);
    // Optional: return a mutable reference to report accumulation, if supported
    fn report_builder(&mut self) -> Option<&mut dyn ReportAccumulator> { None }
}
```

Or more pragmatically, since `Scorer` already uses `as_any` for downcasting, `TraceCollector` can expose `as_any_mut(&mut self) -> Option<&mut dyn Any>` and downcast in `run_traced`. However this is the least clean option.

**Alternative — simplest clean design**: Move `add_included` and `add_excluded` onto the `TraceCollector` trait itself with default no-op implementations:

```rust
pub trait TraceCollector {
    fn is_enabled(&self) -> bool;
    fn record_stage_event(&mut self, event: TraceEvent);
    fn record_item_event(&mut self, event: TraceEvent);
    fn record_included(&mut self, _item: &ContextItem, _score: f64, _reason: InclusionReason) {}
    fn record_excluded(&mut self, _item: &ContextItem, _score: f64, _reason: ExclusionReason, _against: Option<&ContextItem>) {}
    fn set_total_candidates(&mut self, _count: usize) {}
    fn set_total_tokens_considered(&mut self, _tokens: i64) {}
}
```

`NullTraceCollector` gets all these for free (default impls). `DiagnosticTraceCollector` overrides the accumulation methods. No downcasting needed. `run_traced` calls the accumulation methods when `trace.is_enabled()`.

**Recommendation**: Use the extended trait with default no-op methods. This is the most ergonomic design for implementors and avoids `Any` downcasting entirely. The trait remains object-safe because all methods are on `&mut self` and return `()`.

---

## 7. Public API Surface (New and Modified)

### New module: `crate::diagnostics`

```
cupel/src/diagnostics/
  mod.rs           — pub use re-exports
  collector.rs     — TraceCollector trait, NullTraceCollector, DiagnosticTraceCollector
  events.rs        — TraceEvent, PipelineStage, TraceDetailLevel
  report.rs        — SelectionReport, IncludedItem, ExcludedItem
  reasons.rs       — InclusionReason, ExclusionReason
```

All types exported from `crate::diagnostics`. Re-exported in `lib.rs` at the crate root (or under `crate::diagnostics` namespace — prefer namespaced to avoid polluting the root with diagnostic types).

### Modified: `Pipeline::run`

No breaking change. `run` becomes a thin wrapper around `run_traced`:

```rust
pub fn run(&self, items: &[ContextItem], budget: &ContextBudget)
    -> Result<Vec<ContextItem>, CupelError>
```

stays identical. New:

```rust
pub fn run_traced(
    &self,
    items: &[ContextItem],
    budget: &ContextBudget,
    trace: &mut dyn TraceCollector,
) -> Result<Vec<ContextItem>, CupelError>
```

### Modified: `lib.rs`

Add `pub mod diagnostics;` and re-export key types.

### Not modified

`Scorer`, `Slicer`, `Placer` traits — no changes. The .NET `ISlicer.Slice` takes `ITraceCollector` but the Rust equivalent does not need to for the initial diagnostics milestone. `QuotaSlice` trace warnings can be deferred.

`PipelineBuilder` — no changes. The trace collector is per-invocation, not per-configuration.

---

## 8. Timing Without External Dependencies

The .NET version uses `Stopwatch`. Rust has `std::time::Instant` in stdlib. Pattern:

```rust
let stage_start = if trace.is_enabled() {
    Some(std::time::Instant::now())
} else {
    None
};

// ... stage work ...

if let Some(start) = stage_start {
    trace.record_stage_event(TraceEvent {
        stage: PipelineStage::Classify,
        duration: start.elapsed(),
        item_count: pinned.len() + scoreable.len(),
        message: None,
    });
}
```

`Instant::now()` is only called when `is_enabled()` is true. Zero overhead on the null path.

`TraceEvent.duration` is `std::time::Duration`. No dependency needed.

---

## 9. Serde Gating

All new diagnostics types should be `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]` consistent with the existing pattern in `ScoredItem`, `ContextBudget`, etc. `SelectionReport` serialization is useful for logging and testing.

`TraceCollector` itself is not serde-relevant (it is a trait, not data).

---

## 10. Build Order

**Phase 1 — Data types** (no pipeline changes, fully addable without breaking anything)

1. `diagnostics/reasons.rs` — `ExclusionReason`, `InclusionReason`
2. `diagnostics/events.rs` — `PipelineStage`, `TraceDetailLevel`, `TraceEvent`
3. `diagnostics/report.rs` — `IncludedItem`, `ExcludedItem`, `SelectionReport`
4. `diagnostics/mod.rs` — re-exports
5. `lib.rs` — add `pub mod diagnostics`
6. Tests: unit tests for `SelectionReport` construction (no pipeline needed)

**Phase 2 — TraceCollector trait and implementations**

7. `diagnostics/collector.rs` — `TraceCollector` trait with extended default methods
8. `NullTraceCollector` (unit struct)
9. `DiagnosticTraceCollector` with `Vec<TraceEvent>`, accumulation methods, `build_report(self) -> SelectionReport`
10. Tests: verify `NullTraceCollector::is_enabled() == false`, verify `DiagnosticTraceCollector` accumulates events

**Phase 3 — Pipeline integration** (the modification-heavy phase)

11. Refactor `place::place_items` — extract overflow handling into `Pipeline::run_traced`, leaving `place_items` as a pure merge+place function. (This is internal; no API change.)
12. Implement `Pipeline::run_traced` — wires `trace` through the 6 stages, emitting stage events and item-level accumulation calls.
13. Modify `Pipeline::run` to delegate to `run_traced(&mut NullTraceCollector)`.
14. Tests: run pipeline with `DiagnosticTraceCollector`, assert `SelectionReport` contains expected included/excluded items and correct reasons.

**Phase 4 — Public API polish**

15. `lib.rs` re-exports for convenience access to `diagnostics::*` types.
16. Doc tests for `run_traced` showing collector usage.
17. Verify `cargo test --all-features` passes clean.
18. `serde` feature gate on all `diagnostics` data types.
19. Tests: serde roundtrip for `SelectionReport`.

---

## 11. Dependency Impact

No new Cargo dependencies. All types (`Duration`, `Instant`, `HashSet`) are from stdlib. The `thiserror` dependency already present is not needed for diagnostics types (they are not errors).

If `serde` feature is used, `SelectionReport` and friends derive `Serialize`/`Deserialize` via the existing optional `serde` dep — no new dependency.

---

## 12. Non-Goals for This Milestone

- Passing `trace` into `Slicer::slice` — this would be a breaking trait change. Defer until `QuotaSlice` tracing is specifically required.
- Passing `trace` into `Scorer::score` or `Placer::place` — same rationale.
- Async diagnostics — the crate has no async surface; not applicable.
- Metrics/telemetry integration (OpenTelemetry etc.) — external crate, outside the no-new-dependency constraint.

---

## Summary

| Concern | Decision | Rationale |
|---|---|---|
| Ownership model | `&mut dyn TraceCollector` passed to `run_traced` | Idiomatic mutable accumulation; mirrors .NET per-invocation design; avoids `RefCell` |
| API compatibility | `run` unchanged; new `run_traced` method | Zero breaking changes for existing callers |
| Trait design | Extended trait with default no-op accumulation methods | No downcasting; ergonomic for custom implementors; object-safe |
| Zero-cost null path | `NullTraceCollector::is_enabled() = false`; all call sites gated | Identical performance to current pipeline when diagnostics off |
| Timing | `std::time::Instant` + `Duration` (stdlib) | No new dependencies |
| Serde | Feature-gate all new data types consistently | Matches existing pattern |
| Stage helper refactor | Move overflow logic from `place::place_items` to `run_traced` | Keeps helpers pure; enables trace emission without threading trace through private helpers |
| Dedup/slice exclusion reporting | Set-diff in `run_traced` against helper input/output | Keeps helper functions pure; acceptable O(n) overhead only when `is_enabled()` |
| New module location | `crate::diagnostics` | Namespaced; consistent with .NET `Diagnostics` namespace; does not pollute crate root |
| Build order | Data types → trait+impls → pipeline integration → polish | Each phase independently compilable and testable |
