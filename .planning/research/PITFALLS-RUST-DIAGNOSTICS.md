# Pitfalls: Rust Diagnostics Port and Quality Hardening

**Date**: 2026-03-15
**Scope**: Adding `TraceCollector` / `SelectionReport` / diagnostics to the published `cupel` crate (v1.1.0 → v1.2+), plus quality hardening pass (`#[non_exhaustive]`, derive additions, conformance vector cleanup)
**Confidence key**: HIGH = verified against official Rust Reference / API Guidelines / crates.io behavior, MEDIUM = verified via multiple sources or direct codebase inspection, LOW = single source or reasoned inference

---

## 1. Semver and Published API Risks

### 1.1 Adding a trait method breaks all downstream implementations [HIGH]

**What goes wrong**: `Scorer`, `Slicer`, and `Placer` are public traits with no default methods. If a diagnostics-related method (e.g., `fn name(&self) -> &str` or `fn describe(&self) -> String`) is added to any of these traits without a default implementation, every downstream crate that implements `Scorer`, `Slicer`, or `Placer` fails to compile. This is a semver-major breaking change.

**Warning signs**: A PR adds `fn stage_name(&self) -> &'static str` to `Scorer` for trace event labeling. The crate's own built-in scorers compile fine. Downstream custom scorer implementations break on `cargo update`.

**Prevention**:
- Never add a non-defaulted method to `Scorer`, `Slicer`, or `Placer` in a semver-minor release.
- If a method with a default is added, the default must be universally correct (e.g., `fn name(&self) -> &str { std::any::type_name::<Self>() }`). Verify the default doesn't require `Self: Sized`, which would break trait object usage.
- Diagnostics context (stage labels, event types) should come from the pipeline orchestrator, not the trait implementors. The pipeline knows which stage it is running — it does not need to ask the scorer.
- Any method with `where Self: Sized` is excluded from trait objects (`Box<dyn Scorer>`) and is therefore safe to add without breaking object users, but it cannot be called through a trait object at all — not useful for trace events that fire during `pipeline.run()`.

**Which phase**: API freeze review — before any diagnostics method is proposed against a public trait.

---

### 1.2 Adding `#[non_exhaustive]` to an existing enum is NOT breaking — but requires timing [HIGH]

**What goes wrong**: Adding `#[non_exhaustive]` to `CupelError` or `OverflowStrategy` after downstream crates have already written exhaustive `match` arms is a breaking change for those crates. Their code won't compile until they add a wildcard arm.

However: the crate was published as v1.1.0 on 2026-03-15. The window to add `#[non_exhaustive]` without impacting real consumers is very short. Waiting until after v1.2's diagnostics work (which adds a new `CupelError::TableTooLarge` variant) means the variant addition itself is the breaking change — you can't simultaneously add the variant and the attribute without a major bump.

**The correct sequence**:
1. Add `#[non_exhaustive]` to `CupelError` and `OverflowStrategy` in v1.2.0 (or a patch 1.1.x release). This is technically a breaking change per strict semver but is conventionally shipped as a minor version because the breakage only affects downstream exhaustive matches. Document it clearly in CHANGELOG.
2. Add the new variant (`TableTooLarge`, trace-related variants) in the same release or later — both changes are now non-breaking.

**Warning signs**: The quick-wins brainstorm (2026-03-15) explicitly identified this dependency: KnapsackSlice OOM guard (Proposal 5) is blocked on `#[non_exhaustive]` being on `CupelError` first (Proposal 1). If Proposal 5 is implemented before Proposal 1, adding `TableTooLarge` is a semver-major break.

**Critical detail from the Rust Reference**: Outside the defining crate, `#[non_exhaustive]` enums cannot be matched exhaustively — a wildcard arm is required. Inside the defining crate, exhaustive matches still work. Searching the codebase confirms no exhaustive match on `CupelError` or `OverflowStrategy` in `crates/cupel/` — zero internal breakage from adding the attribute.

**Prevention**:
- Ship `#[non_exhaustive]` on `CupelError` and `OverflowStrategy` before any new variant is added. These are the Quick Win Proposal 1 items.
- Add this to the phase-ordering constraint: Proposal 1 (future-proofing) must merge and publish before Proposal 5 (OOM guard) and before any diagnostics `TraceEvent` enum is defined.
- Document in CHANGELOG: "Adding `#[non_exhaustive]` is a minor behavioral change. If you have an exhaustive `match` on `CupelError`, add a `_ => {}` arm."

**Which phase**: Quality hardening (Phase A / Quick Wins) — first, before any diagnostics work.

---

### 1.3 Adding a new public type or re-export is NOT breaking, but new items in the module namespace can shadow [MEDIUM]

**What goes wrong**: Adding `SelectionReport`, `TraceEvent`, `TraceCollector`, `NullTraceCollector`, `DiagnosticTraceCollector` to the public API (`pub use` in `lib.rs`) introduces names into the crate's namespace. If a downstream consumer has a local type named `TraceEvent` or `SelectionReport`, Rust will not break (they use their own type), but this is a code clarity risk that generates compiler warnings in some configurations.

More critical: adding items to the `crate::pipeline` or `crate::model` public modules changes what `use cupel::*` expands to. Glob imports are rare in library code but do exist in test harnesses.

**Prevention**:
- Namespace the new diagnostic types under a dedicated sub-module: `cupel::diagnostics`. Re-export via `pub use diagnostics::{TraceCollector, SelectionReport, ...}` at the top level only what is ergonomically required (the trait and the two concrete collectors are likely candidates; `TraceEvent` may stay under `diagnostics::` only).
- Do not add diagnostic types to the current flat re-export pattern in `lib.rs` without deliberate review. The existing API surface (`ContextItem`, `Pipeline`, `Scorer`, etc.) is already large.
- Check: does `SelectionReport` belong in `model` or `diagnostics`? It is output data, similar to `ScoredItem`. But unlike `ScoredItem`, it is only produced when diagnostics are enabled. Putting it in `model` implies it is always relevant; `diagnostics` is more honest.

**Which phase**: Spec chapter review — decide module placement before implementation.

---

### 1.4 Adding `Send + Sync` bounds to existing public structs is breaking [HIGH]

**What goes wrong**: `Pipeline` holds `Box<dyn Scorer>`, `Box<dyn Slicer>`, `Box<dyn Placer>`. The `Scorer` trait already has `Send + Sync` bounds. `Placer` also has `Send + Sync`. `Slicer` has `Send + Sync`. This is good — `Pipeline` is implicitly `Send + Sync`.

If `TraceCollector` is added to `Pipeline` (via `PipelineBuilder::with_trace(collector)`) and `TraceCollector` does NOT have `Send + Sync` bounds, then `Pipeline` loses its `Send + Sync` impl. This breaks every consumer that sends a `Pipeline` across threads or stores it in an `Arc`.

Conversely: if `TraceCollector` requires `Send + Sync`, then `NullTraceCollector` (a zero-sized type) trivially satisfies this, but `DiagnosticTraceCollector` (which buffers events) must use interior mutability (e.g., `Mutex<Vec<TraceEvent>>`) to be `Sync`. The ownership model of `DiagnosticTraceCollector` is constrained by this bound.

**Prevention**:
- The `TraceCollector` trait must have `Send + Sync` bounds, matching `Scorer`, `Slicer`, and `Placer`. This is mandatory to preserve `Pipeline: Send + Sync`.
- `DiagnosticTraceCollector` must use `Mutex<Vec<TraceEvent>>` (or equivalent) internally to implement `Sync`. This is a design consequence, not a choice.
- Verify with a compile-time assertion after implementation: `const _: fn() = || { fn assert_send<T: Send>() {} assert_send::<Pipeline>(); };`.

**Which phase**: Spec chapter — the `TraceCollector` trait definition must specify `Send + Sync` requirement.

---

### 1.5 The `serde` feature and new diagnostic types [HIGH]

**What goes wrong**: New types (`TraceEvent`, `SelectionReport`) added without `#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]` create a gap: the serde feature covers `ContextItem`, `ScoredItem`, `OverflowStrategy`, `ContextBudget`, `ContextKind`, `ContextSource` — consumers who enable `serde` expect that pipeline output types are serializable. `SelectionReport` is explicitly called out in the brainstorm as requiring optional serde.

The risk: shipping `SelectionReport` without serde initially, then adding serde support later. Adding `Serialize + Deserialize` is not itself breaking, but if the JSON field names are chosen poorly (inconsistent with existing types' `snake_case` convention), consumers who build tooling around the JSON format are stuck.

**Warning signs**: `SelectionReport` ships with public fields but no serde attributes. Users immediately open an issue requesting JSON serialization. The serialization is added quickly without a spec review of field names.

**Prevention**:
- The spec chapter for `SelectionReport` must specify the exact serde wire format: field names, nested types, how `ExclusionReason` enum variants serialize (lowercase `snake_case`? `PascalCase`?).
- Implement serde support for `SelectionReport` and `TraceEvent` in the same PR that introduces them, gated on `feature = "serde"`. Do not ship the types without serde and retrofit later.
- `TraceEvent` is an enum — if it is `#[non_exhaustive]`, downstream serde consumers using `#[serde(deny_unknown_fields)]` on wrapping types will be affected if new variants appear. Document this.
- Follow the existing convention: `#[serde(deny_unknown_fields)]` on structs, `#[serde(rename_all = "camelCase")]` or manual `#[serde(rename = "...")]` on variants — inspect `OverflowStrategy`'s serde derive for consistency.

**Which phase**: Spec chapter must specify wire format. Implementation must include serde in the same PR.

---

## 2. `&dyn TraceCollector` vs `Arc<dyn TraceCollector>` — Ownership and Lifetime Pitfalls

### 2.1 `&dyn TraceCollector` forces a lifetime parameter that cascades into `Pipeline::run()` [HIGH]

**What goes wrong**: If `PipelineBuilder::with_trace(collector: &dyn TraceCollector)` is chosen, the borrow must outlive the `pipeline.run()` call. This is correct for the call site but the lifetime parameter bleeds into every function signature that calls `run()`:

```rust
// &dyn approach — lifetime cascades
fn execute_pipeline<'a>(pipeline: &Pipeline, items: &[ContextItem], budget: &ContextBudget, trace: &'a dyn TraceCollector) -> Result<Vec<ContextItem>, CupelError> { ... }
```

Alternatively, if the collector is stored on `Pipeline` (not passed per-call), the struct requires a lifetime parameter:
```rust
pub struct Pipeline<'a> {
    scorer: Box<dyn Scorer>,
    // ...
    trace: &'a dyn TraceCollector,
}
```

A lifetime-parameterized `Pipeline<'a>` cannot be stored in `Arc<Pipeline<'a>>` without careful bound management. It also cannot be used as a value in `HashMap<K, Pipeline<'a>>` without lifetime annotations propagating to every container. This is the "lifetime parameter cascade" problem — it is functionally correct but ergonomically very bad.

**Warning signs**: The spec is written by modeling C#'s `ITraceCollector` directly. C# has no lifetime parameters. Translating `ITraceCollector` into `&dyn TraceCollector` stored on the pipeline struct adds a lifetime parameter to the struct. Any code example in the spec that stores `Pipeline` in a field or `Arc` will fail to compile in idiomatic Rust.

**Prevention**:
- Do NOT store `&dyn TraceCollector` on `Pipeline`. Two alternatives:
  1. **Per-call injection**: `pipeline.run_with_trace(items, budget, &mut collector)` — the collector is passed to `run()`, not stored on `Pipeline`. `Pipeline` remains lifetime-free. This is the ergonomically cleanest option.
  2. **`Arc<dyn TraceCollector>`**: `Pipeline` stores `Option<Arc<dyn TraceCollector>>`. This has a runtime cost (ref-counting) but keeps `Pipeline` lifetime-free and sendable across threads. The null path (`None`) is cheap.
- The spec chapter must explicitly choose one of these two models and justify the choice. The brainstorm correctly identified that spec-first is mandatory because implementation pressure will bias toward whichever is easiest to code, not whichever has the best consumer ergonomics.
- Recommended: per-call injection for the Rust crate. It maps cleanly to `pipeline.run()` taking an optional trace argument. `NullTraceCollector` as the default satisfies the zero-overhead null path requirement.

**Which phase**: Spec chapter — the ownership model is the most load-bearing decision.

---

### 2.2 Mutable vs immutable trait object for the collector [MEDIUM]

**What goes wrong**: `DiagnosticTraceCollector` must accumulate events (it is a buffer). If the `TraceCollector` trait takes `&self` (immutable receiver), the implementation must use interior mutability (`Mutex`, `RefCell`, etc.). If the trait takes `&mut self`, it cannot be used as `&dyn TraceCollector` in concurrent contexts and requires `&mut` at every call site, which forces `&mut pipeline.run(...)` or `&mut trace` threading through all six stage functions.

**Warning signs**: Defining `fn record(&self, event: TraceEvent)` with `&self` receiver. Works for `NullTraceCollector` (no-op). Fails to compile for `DiagnosticTraceCollector` unless `Mutex<Vec<TraceEvent>>` is used. Using `&mut self` instead to avoid `Mutex` breaks `dyn TraceCollector + Send + Sync` object safety rules in concurrent usage.

**Prevention**:
- Define `fn record(&self, event: TraceEvent)` with `&self` (immutable receiver). This is the correct design for a `Send + Sync` trait object.
- `NullTraceCollector` implements `record` as a no-op with `&self` — trivial.
- `DiagnosticTraceCollector` stores `Mutex<Vec<TraceEvent>>` and locks on each `record` call. The lock contention is irrelevant — `pipeline.run()` is single-threaded; the `Mutex` is needed only to satisfy `Sync`, not to protect concurrent writes.
- Alternatively, if pipeline execution is guaranteed single-threaded: use `RefCell<Vec<TraceEvent>>`. But `RefCell` is not `Sync`, which breaks the `Send + Sync` requirement. Use `Mutex`.

**Which phase**: Spec chapter — receiver mutability must be specified.

---

### 2.3 Zero-overhead null path — verifying `NullTraceCollector` compiles away [HIGH]

**What goes wrong**: The brainstorm requirement is explicit: "Zero-overhead null path — NullTraceCollector must compile away." If the pipeline stages call `trace.record(TraceEvent::Scored { ... })` unconditionally, and `NullTraceCollector::record` is a no-op, rustc should optimize away the no-op and the event construction entirely — but only if the `TraceEvent` construction is not side-effectful and the concrete type is known at monomorphization time.

With `Box<dyn TraceCollector>` or `Arc<dyn TraceCollector>`, the concrete type is NOT known at monomorphization time. Dynamic dispatch prevents inlining of `NullTraceCollector::record`. The event struct is constructed, passed through the vtable, and discarded — not zero overhead.

**Warning signs**: `pipeline.run()` is called with a `Box::new(NullTraceCollector)` passed to `PipelineBuilder::with_trace(...)`. A micro-benchmark shows trace overhead proportional to the number of items even with `NullTraceCollector`.

**Prevention**:
- If the null path must compile away, the trace collector type must be a generic type parameter, not a trait object:
  ```rust
  pub struct Pipeline<T: TraceCollector = NullTraceCollector> {
      trace: T,
      ...
  }
  ```
  With `T = NullTraceCollector`, rustc monomorphizes `Pipeline<NullTraceCollector>` and inlines the no-op, then dead-code-eliminates the event construction entirely (if the events are passed by value and not side-effectful).
- This approach removes the lifetime issue of `&dyn` but adds a type parameter to `Pipeline`. The type parameter is defaulted to `NullTraceCollector`, so existing `Pipeline::builder()` usage remains unchanged at the call site.
- The alternative — per-call `if trace.is_enabled() { ... }` gating — requires adding `fn is_enabled(&self) -> bool` to the `TraceCollector` trait. With a trait object, the `is_enabled()` call itself is a vtable dispatch — not free, but far cheaper than constructing `TraceEvent` structs. This is acceptable if monomorphization is not viable.
- Verify with `cargo asm` or `cargo llvm-ir` that the null path produces no call instruction to `record` and no `TraceEvent` constructor code.

**Which phase**: Spec chapter must choose: generic type parameter (`Pipeline<T>`) or `is_enabled()` guard. Implementation must verify with `cargo asm`.

---

## 3. C#/.NET Pattern Translation Mistakes

### 3.1 `ITraceCollector.IsEnabled` → not a free check in Rust with `dyn` [HIGH]

**What goes wrong**: The .NET diagnostics design gates all trace construction behind `ITraceCollector.IsEnabled`. In C#, this is a simple boolean property access through a vtable — cheap but not free. In Rust, calling `trace.is_enabled()` through `&dyn TraceCollector` involves a vtable lookup on every call. For a pipeline processing 500 items across 6 stages, that is 3,000 vtable dispatches just for the null-path gate.

The C# `NullTraceCollector` implements `IsEnabled => false` — a branch that the JIT inlines and branch-predicts to "always false." The Rust vtable prevents the equivalent inline.

**Warning signs**: Porting the `IsEnabled` pattern directly from C# into Rust and considering it "done." No benchmark verifying the null path cost.

**Prevention**:
- If using trait objects: `is_enabled()` returning a constant `false` for `NullTraceCollector` will be predicted correctly by the branch predictor and is acceptable (3000 vtable dispatches + 3000 predicted-not-taken branches for 500 items). Measure and decide.
- If zero overhead is required: use the generic type parameter approach (see Pitfall 2.3). The `is_enabled()` check becomes unnecessary because the compiler eliminates the dead branch.
- Document the performance model in the crate docs: "When using `NullTraceCollector` as a generic type parameter, trace overhead is zero. When using `Box<dyn TraceCollector>` with `NullTraceCollector`, trace overhead is bounded by vtable dispatch cost (~1ns per event on modern hardware)."

**Which phase**: Implementation — benchmark must be written before the PR merges.

---

### 3.2 `ExclusionReason` as a C# enum → Rust enum design divergence [MEDIUM]

**What goes wrong**: The .NET codebase likely models `ExclusionReason` as a C# enum with integer backing values. Porting this directly as a Rust fieldless enum loses information. Rust enums can carry data: `ExclusionReason::BudgetExceeded { available: i64, required: i64 }` is expressible. The Rust version should be richer, not a simple port.

However: making `ExclusionReason` a data-carrying enum immediately requires `#[non_exhaustive]` (to allow adding new variants) and a well-specified serde wire format. Designing this as a simple port and fixing it later is a semver-breaking change.

**Warning signs**: `ExclusionReason` is defined as a fieldless enum. Consumers cannot determine why an item was excluded (budget? deduplication? scoring below threshold?) without inspecting other fields. Issues are filed requesting more information in exclusion reasons.

**Prevention**:
- Design `ExclusionReason` with data-carrying variants from the start. At minimum:
  - `BudgetExceeded { item_tokens: i64, available_tokens: i64 }` — why slicing dropped it
  - `Deduplicated { kept_score: f64 }` — which copy was kept and its score
  - `BelowScoreThreshold { score: f64 }` — if a minimum score filter is ever added
  - `PinnedExceedsCapacity` — for the classify stage error path
- Apply `#[non_exhaustive]` immediately.
- The spec chapter must define these variants, not the implementation.

**Which phase**: Spec chapter — variant design is load-bearing API surface.

---

### 3.3 C# `record` types → Rust struct field visibility [MEDIUM]

**What goes wrong**: `SelectionReport` in C# is likely a `record` with positional properties — all public by default, immutable. In Rust, the natural equivalent is a struct with public fields. The brainstorm notes that the .NET `SelectionReport` is produced by the pipeline and consumed by callers for inspection. In Rust, if `SelectionReport` has public fields, consumers can construct their own `SelectionReport` instances — which may be undesirable (the report should only be produced by the pipeline).

**Warning signs**: `SelectionReport { included: Vec<ContextItem>, excluded: Vec<ExcludedItem>, ... }` with all-public fields. Consumers write tests by manually constructing `SelectionReport` instances rather than going through the pipeline. API evolution (adding a new field) becomes a breaking change for these test constructions.

**Prevention**:
- Make `SelectionReport` fields public for reading (standard Rust struct), but ensure the constructor (`SelectionReport::new(...)` or a builder) is `pub(crate)`. This prevents downstream construction while allowing field access.
- Alternatively: expose all data through methods (`fn included(&self) -> &[ContextItem]`) with private fields. This is the approach used by `ContextItem` — consistent with existing codebase style.
- Given `ContextItem` uses private fields with accessor methods, `SelectionReport` should follow the same pattern. Check consistency with `ScoredItem`, which has public fields — a mixed approach in the same crate is confusing. Standardize: new types introduced in the diagnostics work should use private fields with accessors.

**Which phase**: Spec chapter + implementation — consistency review against existing types.

---

### 3.4 .NET generic type constraints have no Rust equivalent in some cases [LOW]

**What goes wrong**: If the .NET `ITraceCollector` has generic type constraints that encode type relationships (e.g., `where T : IContextItem`), these do not always translate directly to Rust trait bounds. Rust's orphan rules may prevent implementing a foreign trait for a local type in the diagnostics module.

This is a low-risk pitfall for this codebase because all relevant types (`ContextItem`, `ScoredItem`, etc.) are defined in the same crate — orphan rules are not a concern for within-crate `impl` blocks.

**Prevention**: No specific action needed — orphan rules do not apply within a single crate. Flag if diagnostics are ever extracted into a separate crate.

**Which phase**: Low priority — revisit only if crate splitting is planned.

---

## 4. Spec-First Development Discipline

### 4.1 Implementing before the spec forces API renegotiation [HIGH]

**What goes wrong**: Rust's type system surfaces design choices that the spec does not address. During implementation of `TraceCollector`, the developer encounters the `&dyn` vs generic type parameter question, makes a pragmatic choice (whichever compiles first), and the implementation becomes the de facto spec. The spec chapter is written afterward to document what was built, not to guide what should be built. Later, review reveals the choice was wrong (e.g., `Box<dyn TraceCollector>` on `Pipeline` was chosen for simplicity, but zero-overhead is now unachievable without a semver-breaking API change).

The brainstorm debate was explicit: "Spec-first sequencing is mandatory. Do not co-develop spec and implementation — the Rust lifetime constraints will pressure-shape the API toward Rust ergonomics rather than semantic clarity."

**Warning signs**: The diagnostics spec chapter is a stub ("TODO"). The Rust implementation PR is open. The spec chapter PR is not yet merged.

**Prevention**:
- The gate is: spec chapter reviewed and merged → implementation begins. No exceptions.
- The spec chapter must resolve, at minimum: (a) ownership model (`&dyn` / `Arc<dyn>` / generic type param), (b) `TraceCollector` trait method signature (receiver mutability, `is_enabled()` presence), (c) `TraceEvent` enum variants for all 6 pipeline stages, (d) `SelectionReport` structure, (e) `ExclusionReason` variants, (f) `PipelineBuilder` API changes, (g) serde wire format.
- CI gate: if `spec/` diagnostics chapter has a `TODO` or `DRAFT` marker, CI fails the implementation PR.

**Which phase**: Before any diagnostics implementation work begins.

---

### 4.2 Conformance vector drift between `spec/conformance/` and `crates/cupel/tests/conformance/` [HIGH]

**What goes wrong**: The quick-wins brainstorm (Proposal 2) already identified that the spec conformance vectors and the vendored copies in `crates/cupel/tests/conformance/` must be updated simultaneously. The knapsack greedy comparison error and the composite-weighted scratchpad text exist in both locations. If the spec is fixed without updating the vendored copy, the Rust tests continue to validate against incorrect behavior.

For the diagnostics work: new conformance vectors will be needed for trace event sequences (which events fire for each stage, in what order, with what data). These vectors must be added to `spec/conformance/` first and then vendored. Doing it in the opposite order means the spec reflects the implementation, not the design.

**Warning signs**: A new `trace-scoring-events.toml` test vector appears in `crates/cupel/tests/conformance/` but not in `spec/conformance/`. CI does not diff the two directories.

**Prevention**:
- Add the CI diff check (identified in Quick Wins Proposal 2 as a "captured follow-up issue"): a step that diffs `spec/conformance/required/` against `crates/cupel/tests/conformance/` and fails if they diverge.
- For diagnostic conformance vectors: write them in `spec/conformance/` first, review them independently, then copy to the Rust test directory. Never write them in the test directory first.
- Diagnostic conformance vectors must use a fixed `referenceTime` (if any timing is relevant) — no wall-clock-dependent values.

**Which phase**: CI must be updated before diagnostics implementation begins. Spec vectors must be written before test vectors.

---

## 5. `#[non_exhaustive]` Adoption Pitfalls

### 5.1 `#[non_exhaustive]` on a struct prevents construction outside the crate [HIGH]

**What goes wrong**: The quick-wins brainstorm recommends `#[non_exhaustive]` on `CupelError` and `OverflowStrategy` (enums). If `#[non_exhaustive]` is also applied to structs (e.g., `SelectionReport`, `ScoredItem`, `ContextBudget`), downstream consumers can no longer construct those types via struct literal syntax:

```rust
// This fails outside the defining crate if SelectionReport is #[non_exhaustive]:
let report = SelectionReport { included: vec![], excluded: vec![] };
```

For `ScoredItem` specifically: the struct has public fields (`pub item`, `pub score`) and the codebase docs and examples construct it directly in test code. If `#[non_exhaustive]` were added to `ScoredItem`, all downstream test code using struct literal construction would break.

**Warning signs**: Applying `#[non_exhaustive]` broadly to output types "for future-proofing" without auditing whether downstream construction of those types is part of the intended API.

**Prevention**:
- Only apply `#[non_exhaustive]` to enums where new variants are anticipated: `CupelError`, `OverflowStrategy`, `TraceEvent`, `ExclusionReason`.
- Do not apply to structs unless there is a specific reason to prevent downstream construction. `ScoredItem` is constructed by consumers in Placer tests — do not apply `#[non_exhaustive]` to it.
- New diagnostic struct types (`SelectionReport`) should have private fields with public accessors (following `ContextItem`'s pattern), making `#[non_exhaustive]` unnecessary — private fields already prevent external construction.
- Document the policy: "`#[non_exhaustive]` is applied only to enums in this crate. Structs use private fields + accessors to control construction."

**Which phase**: Quality hardening (Proposal 1) review — enumerate exactly which types get the attribute.

---

### 5.2 Existing tests with exhaustive `CupelError` match must be found before shipping [MEDIUM]

**What goes wrong**: Even though the crate itself contains no exhaustive `match` on `CupelError` (confirmed by direct inspection), adding `#[non_exhaustive]` is a semver-breaking change for downstream consumers. The risk window between v1.1.0 publication and v1.2.0 is short (the crate is freshly published), but any documentation examples or integration test code that is shared publicly (e.g., in the spec book or README) and contains exhaustive `CupelError` matches must be updated.

**Warning signs**: The spec book at `spec/book/` contains code examples with exhaustive `match error { CupelError::EmptyContent => ..., CupelError::EmptyKind => ..., ... }`. These will compile until `#[non_exhaustive]` lands and then break for any reader who copies the example into their project.

**Prevention**:
- Grep the spec book, README, and all documentation examples for exhaustive `match` on `CupelError` and `OverflowStrategy`. Add wildcard arms before shipping `#[non_exhaustive]`.
- In CHANGELOG, document explicitly: "Added `#[non_exhaustive]` to `CupelError` and `OverflowStrategy`. If you have an exhaustive `match` on either type, add a wildcard arm."
- For `CupelError` specifically: the common pattern in Rust error handling is `match err { ... }` inside library code that is NOT exhaustive (uses `_` by convention). Consumer code that exhaustively matches on library errors is unusual but not rare.

**Which phase**: Quality hardening (Proposal 1) — audit before PR merges.

---

### 5.3 `TraceEvent` enum must be `#[non_exhaustive]` from day one [HIGH]

**What goes wrong**: `TraceEvent` will have one variant per pipeline stage (Classify, Score, Deduplicate, Sort, Slice, Place) plus potentially sub-variants for item-level events. This is exactly six variants on v1.2.0 publication. If `#[non_exhaustive]` is not applied from the start, adding a new variant in v1.3 (e.g., `TraceEvent::Validated { ... }` for a future validation stage) is a semver-major break.

**Warning signs**: `TraceEvent` is defined without `#[non_exhaustive]` because "we know all the stages." In v1.3, a new validation stage is added and `TraceEvent::Validated` must be added — but it's a breaking change.

**Prevention**:
- `TraceEvent` ships with `#[non_exhaustive]` from its first publication. No exceptions for "we know the design is stable."
- Same applies to `ExclusionReason` — new exclusion reasons may be added as the pipeline evolves.
- The spec chapter must note: "All event enums are `#[non_exhaustive]`. Consumers must handle unknown variants with a wildcard arm."

**Which phase**: Spec chapter — mark required on the enum definition.

---

## 6. Quality Hardening Pitfalls

### 6.1 Derive macro additions that change `PartialEq`/`Hash` behavior [MEDIUM]

**What goes wrong**: The quick-wins brainstorm recommends adding `Debug`, `Clone`, and `Copy` derives to concrete slicer/placer structs. `Debug` and `Clone` are non-breaking additions (they add capability without removing it). `Copy` is also non-breaking. However, if `PartialEq` or `Hash` is added to a type that implements manual `PartialEq` or `Hash` for semantic reasons (e.g., `ContextKind`'s case-insensitive comparison), the derived implementation will conflict with the manual one and cause a compile error.

**Warning signs**: Adding `#[derive(PartialEq, Hash)]` to `ContextKind` (which already has manual `impl PartialEq` with case-insensitive logic). The derive would conflict with the existing manual impl.

**Prevention**:
- Only add `Debug`, `Clone`, and `Copy` to the concrete structs as specified in Quick Wins Proposal 1: `GreedySlice`, `KnapsackSlice`, `UShapedPlacer`, `ChronologicalPlacer`.
- Do not touch `ContextKind`, `ContextSource`, `ContextItem` — these already have carefully crafted manual or derived impls.
- For `KnapsackSlice`: `Clone` requires that `bucket_size: i64` is `Clone` (it is, as a primitive). `Copy` also applies — a `KnapsackSlice` is just one `i64`. Adding `Copy` is correct.
- For `CompositeScorer` and `ScaledScorer`: the brainstorm explicitly excluded `Clone` because they hold `Vec<Box<dyn Scorer>>`. `Box<dyn Scorer>` is not `Clone`. Do not derive `Clone` on these types — it requires `dyn-clone` (an external dependency).

**Which phase**: Quality hardening (Proposal 1) — review derive additions individually.

---

### 6.2 `TryFrom<&str>` on `ContextKind` must be consistent with `new()` [LOW]

**What goes wrong**: The quick-wins brainstorm recommends adding `TryFrom<&str>` to `ContextKind` for idiomatic `?`-operator usage. If the validation logic in `TryFrom<&str>` diverges from `ContextKind::new()` (e.g., `TryFrom` uses different whitespace trimming behavior), the two construction paths produce different results for edge cases.

**Warning signs**: `ContextKind::new("  ")` returns `Err(CupelError::EmptyKind)`. `ContextKind::try_from("  ")` returns the same error through `TryFrom` — but the error type differs: `CupelError` vs whatever `Error` type `TryFrom` uses. In Rust, `TryFrom::Error` is an associated type. For `TryFrom<&str> for ContextKind`, the error type should be `CupelError` — but this means `CupelError` leaks into the `From`/`Into` trait system.

**Prevention**:
- `impl TryFrom<&str> for ContextKind` with `type Error = CupelError` — consistent with `new()`. The implementation should call `ContextKind::new(value)`.
- Ensure whitespace trimming behavior in `TryFrom` matches `new()` exactly (both call the same internal validation).
- Add a doctest that shows `"  ".try_into()` returning `Err(CupelError::EmptyKind)`.

**Which phase**: Quality hardening (Proposal 3) — small scope, verify behavior in doctests.

---

### 6.3 `UnreservedCapacity` returning the wrong type breaks callers [MEDIUM]

**What goes wrong**: The brainstorm recommends `pub fn unreserved_capacity(&self) -> u32` in Rust. But `max_tokens`, `output_reserve`, and `reserved_slots` values are all `i64` in the current `ContextBudget` implementation. Computing `max_tokens - output_reserve - sum(reserved_slots)` can theoretically produce a negative result if reserved slots and output reserve together exceed max tokens — even though `ContextBudget::new()` validates each individually, it does not validate that `output_reserve + sum(reserved_slots) <= max_tokens`. Returning `u32` would panic on subtraction underflow in debug mode and wrap silently in release mode.

**Warning signs**: `ContextBudget::new(100, 80, 60, { "Message": 50 }, 0.0)` is rejected because `output_reserve (60) <= max_tokens (100)` passes, but `output_reserve + reserved_slot_sum = 60 + 50 = 110 > max_tokens`. If this is not rejected by the constructor, `unreserved_capacity()` underflows.

**Prevention**:
- Return `i64` (matching the existing field types), not `u32`. The docstring explains that a negative result indicates the configuration is over-reserved.
- Alternatively: add a validation rule to `ContextBudget::new()` that rejects configurations where `output_reserve + sum(reserved_slots) > max_tokens`. This is cleaner but is a behavior change that needs a conformance vector.
- The brainstorm docs note: "Docs must state explicitly: 'This is the capacity available before pinned items are accounted for.'"

**Which phase**: Quality hardening (Proposal 4) — validate return type before implementation.

---

## 7. Integration with Existing Pipeline Architecture

### 7.1 Trace events in stage functions require passing the collector without storing it [HIGH]

**What goes wrong**: The six pipeline stages are currently private free functions in `crates/cupel/src/pipeline/{classify, score, deduplicate, sort, slice, place}.rs`. They do not receive any shared state — they take typed inputs and return typed outputs. Adding trace events requires threading the collector through all six functions.

If the collector is a generic type parameter, each stage function becomes generic:
```rust
pub(crate) fn score_items<T: TraceCollector>(items: &[ContextItem], scorer: &dyn Scorer, trace: &T) -> Vec<ScoredItem> { ... }
```

This is correct but means the pipeline's monomorphization doubles: one instantiation per `T`. With `T = NullTraceCollector` and `T = DiagnosticTraceCollector`, this is fine (two concrete types). If the generic type leaks into the public API, consumers face type inference issues.

**Warning signs**: The generic `T: TraceCollector` appears in `pipeline::run()` signature and propagates into `PipelineBuilder`. Code that constructs a `Pipeline` without specifying `T` requires type inference to resolve `T = NullTraceCollector`.

**Prevention**:
- The generic `T` should be on `Pipeline<T: TraceCollector>` with a default: `Pipeline<T = NullTraceCollector>`. `Pipeline::builder()` returns `PipelineBuilder<NullTraceCollector>`. `PipelineBuilder::with_trace(collector: U) -> PipelineBuilder<U>` changes the generic. This is the typestate builder pattern.
- Stage functions are `pub(crate)` — making them generic is internal, no public API impact.
- If per-call injection is chosen instead: `pipeline.run(items, budget)` stays signature-clean; the trace is passed separately. Stage functions receive the collector as a `&dyn TraceCollector` argument.

**Which phase**: Implementation — choose and validate the threading approach before writing any stage-level trace code.

---

### 7.2 `SelectionReport` must be producible from the existing pipeline output without re-running [MEDIUM]

**What goes wrong**: The existing `pipeline.run()` returns `Vec<ContextItem>` — the final placed items. `SelectionReport` must include the items that were excluded (dropped by the slicer, removed by deduplication, etc.). Currently, the excluded items are not returned — they are computed inside the stage functions and then discarded.

To produce `SelectionReport`, the pipeline must either: (a) run with a trace collector that accumulates exclusion decisions from each stage, or (b) change `run()` to return a richer result type. Option (b) is a breaking change to `run()`'s return type.

**Warning signs**: A PR changes `pipeline.run()` to return `Result<(Vec<ContextItem>, Option<SelectionReport>), CupelError>`. This breaks all existing callers of `pipeline.run()`.

**Prevention**:
- Do not change `pipeline.run()` signature. Introduce a new method: `pipeline.run_traced(items, budget, &mut collector)` that returns `Result<Vec<ContextItem>, CupelError>` and populates the collector as a side effect.
- Or: `pipeline.run()` returns the same type; `SelectionReport` is extracted from the `DiagnosticTraceCollector` after the call: `let report = collector.into_report()`.
- The .NET design already resolves this: `SelectionReport` is accessed after pipeline execution, not as a return value. Follow the same pattern in Rust.

**Which phase**: Spec chapter — the `SelectionReport` extraction API must be specified before implementation.

---

## Summary: Phase Assignment

| Pitfall | Severity | Phase |
|---------|----------|-------|
| 1.1 Adding trait method breaks downstream implementations | CRITICAL | API freeze review — before any trait changes |
| 1.2 `#[non_exhaustive]` timing: before new variants, not after | HIGH | Quality hardening (QW Proposal 1) — first |
| 1.3 New diagnostic types in namespace — use `cupel::diagnostics` module | MEDIUM | Spec chapter |
| 1.4 `TraceCollector` must be `Send + Sync` to preserve `Pipeline: Send + Sync` | HIGH | Spec chapter |
| 1.5 `serde` feature must cover new diagnostic types from day one | HIGH | Spec chapter + implementation |
| 2.1 `&dyn TraceCollector` lifetime cascades into `Pipeline` struct | CRITICAL | Spec chapter |
| 2.2 Trait receiver mutability: use `&self` + interior mutability | HIGH | Spec chapter |
| 2.3 Zero-overhead null path: requires generic type param, not `dyn` | HIGH | Spec chapter + implementation benchmark |
| 3.1 `IsEnabled` check is not free through `dyn` vtable | HIGH | Implementation — benchmark before merge |
| 3.2 `ExclusionReason` should be data-carrying enum, not fieldless | HIGH | Spec chapter |
| 3.3 `SelectionReport` fields: private + accessors, not public (follow `ContextItem`) | MEDIUM | Spec chapter + implementation review |
| 3.4 Orphan rule — not applicable (single crate) | LOW | No action needed |
| 4.1 Spec-first gate: spec merged before implementation PR opens | CRITICAL | Project process — enforce before milestone starts |
| 4.2 Conformance vector drift: CI diff must be added before diagnostics work | HIGH | CI — before diagnostics implementation |
| 4.3 (Within 4.2) Diagnostic conformance vectors must be in `spec/` first | HIGH | Spec chapter |
| 5.1 `#[non_exhaustive]` on structs prevents downstream construction | HIGH | Quality hardening (QW Proposal 1) — audit scope |
| 5.2 Audit spec book and README for exhaustive `CupelError` matches | MEDIUM | Quality hardening (QW Proposal 1) — pre-merge |
| 5.3 `TraceEvent` must have `#[non_exhaustive]` from first publication | HIGH | Spec chapter |
| 6.1 Derive additions: don't touch types with manual `PartialEq`/`Hash` | MEDIUM | Quality hardening (QW Proposal 1) |
| 6.2 `TryFrom<&str>` on `ContextKind` must delegate to `new()` | LOW | Quality hardening (QW Proposal 3) |
| 6.3 `unreserved_capacity()` return type must be `i64`, not `u32` | MEDIUM | Quality hardening (QW Proposal 4) |
| 7.1 Trace collector threading through six private stage functions | HIGH | Implementation — choose pattern before coding |
| 7.2 `SelectionReport` extraction: separate method, not changed `run()` return type | HIGH | Spec chapter |

### Highest-priority items (act before any diagnostics implementation code is written)

1. **Spec-first gate** (pitfall 4.1) — No implementation PR may open until the diagnostics spec chapter is merged.
2. **`&dyn` vs generic type parameter ownership model** (pitfall 2.1, 2.3) — The single most consequential API decision. Must be resolved in the spec chapter.
3. **`TraceCollector: Send + Sync` constraint** (pitfall 1.4) — Must appear in the trait definition, not discovered during implementation.
4. **`#[non_exhaustive]` on `CupelError` before any new variants** (pitfall 1.2) — Quick Wins Proposal 1 must merge and publish before Proposal 5 (OOM guard) and before `TraceEvent` is defined.
5. **CI conformance vector diff** (pitfall 4.2) — Must be in CI before diagnostic conformance vectors are written.

---

*Research completed 2026-03-15. Sources: Rust Reference (`non_exhaustive` attribute, pattern matching); Rust API Guidelines (semver compatibility, trait bounds, object safety, future-proofing); direct inspection of `crates/cupel/` source tree v1.1.0; brainstorm report `2026-03-15T12-23-brainstorm/` (highvalue-report.md, quickwins-report.md, SUMMARY.md).*
