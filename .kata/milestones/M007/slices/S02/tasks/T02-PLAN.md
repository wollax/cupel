---
estimated_steps: 7
estimated_files: 2
---

# T02: Extract run_with_components helper and implement Policy + PolicyBuilder

**Slice:** S02 — Rust Policy struct and dry_run_with_policy
**Milestone:** M007

## Description

This is the core implementation task. It has two distinct phases separated by a mandatory regression check:

**Phase A — Refactor (risk-managed):** Extract the `run_traced` body into a private `run_with_components` helper that accepts injected `&dyn Scorer/Slicer/Placer` references plus flags. Update `run_traced` to delegate to this helper using `self.*` fields. Run `cargo test --all-targets` to confirm zero regressions before adding any new types.

**Phase B — New API:** Add `Policy` struct, `PolicyBuilder` fluent builder, and `Pipeline::dry_run_with_policy` method. Export from `lib.rs`.

The research (S02-RESEARCH.md) recommends extracting `run_with_components` as the cleanest approach because `Pipeline` fields are private and `run_traced` is a `&self` method — there is no other way to inject policy components without duplicating ~280 lines of stage logic.

## Steps

1. **Extract `run_with_components`:** In `crates/cupel/src/pipeline/mod.rs`, add a private method:
   ```rust
   fn run_with_components<C: TraceCollector>(
       &self,
       items: &[ContextItem],
       budget: &ContextBudget,
       scorer: &dyn Scorer,
       slicer: &dyn Slicer,
       placer: &dyn Placer,
       deduplication: bool,
       overflow_strategy: OverflowStrategy,
       collector: &mut C,
   ) -> Result<Vec<ContextItem>, CupelError>
   ```
   Move the entire body of `run_traced` into this method (replacing `self.scorer.as_ref()` → `scorer`, `self.slicer.as_ref()` → `slicer`, `self.placer.as_ref()` → `placer`, `self.deduplication` → `deduplication`, `self.overflow_strategy` → `overflow_strategy`).

2. **Update `run_traced`** to delegate: replace its body with a call to `self.run_with_components(items, budget, self.scorer.as_ref(), self.slicer.as_ref(), self.placer.as_ref(), self.deduplication, self.overflow_strategy, collector)`.

3. **Regression check (mandatory):** Run `cargo test --all-targets` — must exit 0 before proceeding. If any test fails, fix the refactor before continuing.

4. **Add `Policy` struct** in `pipeline/mod.rs` (near `PipelineBuilder`):
   ```rust
   pub struct Policy {
       pub(crate) scorer: Arc<dyn Scorer>,
       pub(crate) slicer: Arc<dyn Slicer>,
       pub(crate) placer: Arc<dyn Placer>,
       pub(crate) deduplication: bool,
       pub(crate) overflow_strategy: OverflowStrategy,
   }
   ```
   Add a manual `Debug` impl (trait objects are not `Debug` by default unless the trait requires it — use a struct-like debug output with field names and `is_some`-style placeholders, or add `+ std::fmt::Debug` bounds to `Scorer`/`Slicer`/`Placer` traits if they already have it; check first).

5. **Add `PolicyBuilder`** mirroring `PipelineBuilder`:
   - Fields: `scorer: Option<Arc<dyn Scorer>>`, `slicer: Option<Arc<dyn Slicer>>`, `placer: Option<Arc<dyn Placer>>`, `deduplication: bool`, `overflow_strategy: OverflowStrategy`
   - `fn new() -> Self` with defaults matching `PipelineBuilder` (deduplication=true, overflow_strategy=default)
   - Fluent `scorer(Arc<dyn Scorer>)`, `slicer(Arc<dyn Slicer>)`, `placer(Arc<dyn Placer>)`, `deduplication(bool)`, `overflow_strategy(OverflowStrategy)` methods
   - `fn build(self) -> Result<Policy, CupelError>` using `CupelError::PipelineConfig("scorer is required")`, `"slicer is required"`, `"placer is required"`

6. **Add `Pipeline::dry_run_with_policy`:**
   ```rust
   pub fn dry_run_with_policy(
       &self,
       items: &[ContextItem],
       budget: &ContextBudget,
       policy: &Policy,
   ) -> Result<SelectionReport, CupelError> {
       let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
       self.run_with_components(
           items, budget,
           policy.scorer.as_ref(), policy.slicer.as_ref(), policy.placer.as_ref(),
           policy.deduplication, policy.overflow_strategy,
           &mut collector,
       )?;
       Ok(collector.into_report())
   }
   ```

7. **Update `lib.rs`:** Change `pub use pipeline::{Pipeline, PipelineBuilder};` to `pub use pipeline::{Pipeline, PipelineBuilder, Policy, PolicyBuilder};`. Run `cargo clippy --all-targets -- -D warnings`; if `type_complexity` fires on `Arc<dyn Scorer>` field types, add type aliases `type ArcScorer = Arc<dyn Scorer>` etc. following D021 pattern.

## Must-Haves

- [ ] `run_with_components` is private; its signature is not part of the public API
- [ ] `run_traced` produces identical behavior as before (proven by regression check in step 3)
- [ ] `Policy` fields `scorer`, `slicer`, `placer` use `Arc<dyn Scorer/Slicer/Placer>` (not `Box`) — D149
- [ ] `PolicyBuilder::build()` uses `CupelError::PipelineConfig("scorer is required")` etc. — same strings as `PipelineBuilder`
- [ ] `Policy` and `PolicyBuilder` exported from `lib.rs`
- [ ] `cargo test --all-targets` exits 0
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0

## Verification

```bash
# Step 3 regression check (run before adding new types):
cargo test --all-targets

# Final verification:
cargo build --all-targets
cargo test --all-targets
cargo clippy --all-targets -- -D warnings
```

## Observability Impact

- Signals added/changed: `dry_run_with_policy` returns `Result<SelectionReport, CupelError>` — identical error surface to `dry_run`; `run_with_components` is private and adds no new error types
- How a future agent inspects this: `cargo test --test dry_run_with_policy` exercises the new methods; `SelectionReport` fields are the inspection surface
- Failure state exposed: `CupelError` variants propagate unchanged through `run_with_components` (slice/place errors surface identically)

## Inputs

- `crates/cupel/src/pipeline/mod.rs` — `run_traced` body to extract (lines ~213–490); `PipelineBuilder` pattern to mirror for `PolicyBuilder`
- `crates/cupel/src/lib.rs` — existing `pub use pipeline::{Pipeline, PipelineBuilder}` to extend
- S02-RESEARCH.md — `run_with_components` recommendation; D149 (`Arc<dyn Trait>`); D021 (`TraceEventCallback` type alias pattern for clippy)
- T01-PLAN.md expected output — `dry_run_with_policy.rs` test file (compilation target for new types)

## Expected Output

- `crates/cupel/src/pipeline/mod.rs` — `run_with_components` private helper; `Policy`; `PolicyBuilder`; `Pipeline::dry_run_with_policy`
- `crates/cupel/src/lib.rs` — `Policy` and `PolicyBuilder` added to `pub use pipeline::{...}`
- `cargo test --all-targets` exits 0; `cargo clippy --all-targets -- -D warnings` exits 0
