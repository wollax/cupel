# S02: Rust Policy struct and dry_run_with_policy — Research

**Date:** 2026-03-24

## Summary

S02 introduces the first `Policy` struct and `PolicyBuilder` to the Rust crate, plus a `Pipeline::dry_run_with_policy` method. The key architectural decision (D149) is already settled: `Policy` holds `Arc<dyn Scorer/Slicer/Placer>` (not `Box<dyn>`) to enable shared ownership across multiple `policy_sensitivity` variant runs in S03. The implementation is a thin wrapper over the existing `run_traced` / `dry_run` machinery — no pipeline stage logic changes needed.

The existing `policy_sensitivity` function in `analytics.rs` takes `&[(label, &Pipeline)]`. S02 must produce types that S03 can consume with a `&[(label, &Policy)]` signature instead. The Policy-to-Pipeline conversion cannot use `Pipeline` itself (its fields are private `Box<dyn Trait>`) — the implementation must either: (a) add a `dry_run_with_policy` method directly on `Pipeline` that substitutes components at call-time without constructing a new `Pipeline`, or (b) internally construct a temporary `Pipeline` from `Arc`-cloned components via `PipelineBuilder`. Option (b) requires `Box<T: Deref<Arc<dyn Trait>>>` coercion. Option (a) is cleaner — `dry_run_with_policy` calls `run_traced` using policy components instead of `self.scorer/slicer/placer`.

The cleanest path: `dry_run_with_policy` on `Pipeline` borrows the policy's `Arc<dyn Trait>` components and calls the internal pipeline stages directly (or reconstructs a temporary pipeline via `.as_ref()` on the `Arc`). Since `Arc<dyn Scorer>` implements `Deref<Target = dyn Scorer>`, passing `policy.scorer.as_ref()` works wherever `&dyn Scorer` is expected — and `run_traced` already takes `self.scorer.as_ref()`. The simplest approach: replicate the `dry_run` method body but substitute `policy.scorer.as_ref()`, `policy.slicer.as_ref()`, `policy.placer.as_ref()`, `policy.deduplication`, and `policy.overflow_strategy`.

Since `run_traced` is a method on `Pipeline` that uses `self.scorer/slicer/placer`, we cannot simply call `self.run_traced(...)` with substituted components — those fields are private. Two options remain:
1. **Internal helper**: extract a `run_traced_with` that takes explicit trait-object references and flags, called by both `run_traced` and `dry_run_with_policy`.
2. **Temporary Pipeline construction**: `Pipeline { scorer: Box::new(Arc::clone(&policy.scorer)), ... }` — but `Arc<dyn Scorer>` does not impl `Scorer` by default.

The cleanest option is (1): add a `fn run_traced_core` (or inline at the `dry_run_with_policy` body level) that accepts `scorer: &dyn Scorer`, `slicer: &dyn Slicer`, `placer: &dyn Placer`, plus flags. Then both `run_traced` and `dry_run_with_policy` delegate to it.

Alternatively, implement `Scorer`/`Slicer`/`Placer` for `Arc<dyn Scorer>`/`Arc<dyn Slicer>`/`Arc<dyn Placer>` — Rust allows this with blanket impls. This lets `Arc<dyn Scorer>` be used wherever `dyn Scorer` is used, and a `Box::new(Arc::clone(&policy.scorer)) as Box<dyn Scorer>` trick works. But a simpler solution exists: since `Arc<T: Scorer>` dereferences to `T: Scorer`, calling `(*policy.scorer).score(...)` works. So `dry_run_with_policy` can replicate the `run_traced` body substituting `policy.scorer.as_ref()` for `self.scorer.as_ref()` etc.

**Recommendation**: Implement a private `run_with_components` helper that takes `&dyn Scorer`, `&dyn Slicer`, `&dyn Placer`, `bool`, `OverflowStrategy` and drives all 6 stages. Both `run_traced` and `dry_run_with_policy` call it. This is ~30 extra lines and eliminates duplication.

## Recommendation

Implement `Policy` struct with `Arc<dyn Scorer + Send + Sync>`, `Arc<dyn Slicer + Send + Sync>`, `Arc<dyn Placer + Send + Sync>`, `deduplication: bool`, `overflow_strategy: OverflowStrategy`. Implement `PolicyBuilder` as a fluent builder mirroring `PipelineBuilder`. Implement `Pipeline::dry_run_with_policy` by extracting a private `run_with_components` helper from `run_traced` — both methods delegate to it with their respective scorer/slicer/placer references. Export `Policy` and `PolicyBuilder` from `lib.rs`. Add `dry_run_with_policy` tests covering: scorer respected, slicer respected, placer respected (indirect — check report counts), deduplication flag, overflow_strategy.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| 6-stage pipeline execution | `Pipeline::run_traced` + internal stage modules | All stage logic is already correct — just need to accept injected components |
| DiagnosticTraceCollector pattern | `Pipeline::dry_run` pattern | `dry_run_with_policy` follows exact same `DiagnosticTraceCollector::new(TraceDetailLevel::Item)` + `into_report()` pattern |
| Trait object shared ownership | `Arc<dyn Trait>` | Standard Rust pattern; `Arc::clone` is cheap; satisfies `policy_sensitivity`'s need for multi-run variants |
| Builder pattern | `PipelineBuilder` | `PolicyBuilder` mirrors it exactly — same field-by-field fluent pattern |

## Existing Code and Patterns

- `crates/cupel/src/pipeline/mod.rs` — `Pipeline::run_traced` (lines 213–490): contains the full 6-stage loop; `dry_run_with_policy` will call a refactored version of this. The method uses `self.scorer.as_ref()`, `self.slicer.as_ref()`, `self.placer.as_ref()` — these need to become parameters to the inner helper.
- `crates/cupel/src/pipeline/mod.rs` — `Pipeline::dry_run` (lines 479–490): one-liner over `run_traced`; `dry_run_with_policy` will be similarly thin over the shared helper.
- `crates/cupel/src/pipeline/mod.rs` — `PipelineBuilder` (lines 703–790): model for `PolicyBuilder`. Fields: `Option<Box<dyn Scorer>>`, `Option<Box<dyn Slicer>>`, `Option<Box<dyn Placer>>`, `bool`, `OverflowStrategy`. `Policy` fields will use `Arc` instead of `Box`.
- `crates/cupel/src/analytics.rs` — `policy_sensitivity` (lines 192–233): existing free function taking `&[(label, &Pipeline)]`. S03 will add a parallel function taking `&[(label, &Policy)]`. The diff algorithm is reusable — this is the S03 concern, not S02.
- `crates/cupel/src/lib.rs` — re-exports pattern: `pub use analytics::{...}` — `Policy` and `PolicyBuilder` will be added here under `pub use pipeline::{Policy, PolicyBuilder, Pipeline, PipelineBuilder}`.
- `crates/cupel/tests/policy_sensitivity.rs` — integration test pattern to follow for new `dry_run_with_policy` tests.

## Constraints

- `Pipeline` fields (`scorer`, `slicer`, `placer`) are private — cannot be accessed from `Policy` construction or from a standalone free function. `dry_run_with_policy` must be a method on `Pipeline` itself (or the internal stage helper must be `pub(crate)`).
- `Arc<dyn Scorer>` satisfies `Deref<Target = dyn Scorer>` — `policy.scorer.as_ref()` returns `&dyn Scorer`, compatible with all existing stage calls (`score::score_items(&scoreable, self.scorer.as_ref())`).
- `Arc<dyn Slicer>`: similarly satisfies `Deref<Target = dyn Slicer>` — passes cleanly to `slice::slice_items`.
- `Arc<dyn Placer>`: similarly satisfies `Deref<Target = dyn Placer>` — passes to `place::place_items`.
- `Policy` and `PolicyBuilder` must be `pub` (crate root re-export) to satisfy the milestone definition of done ("not hidden behind a feature flag").
- `Send + Sync` bounds: `Scorer`, `Slicer`, `Placer` traits already require `Send + Sync`. `Arc<dyn Scorer + Send + Sync>` is both `Send` and `Sync`. `Policy` will also be `Send + Sync`.
- `PolicyBuilder` requires all three components set before building — `build()` returns `Result<Policy, CupelError>` (same `PipelineConfig` error variant for missing components).
- No serde support for `Policy` (D149 context: trait objects are not serializable; this is by design).
- `OverflowStrategy` derives `Copy` — can be stored by value in `Policy`.

## Common Pitfalls

- **`Box<dyn Trait>` vs `Arc<dyn Trait>`**: Don't reach for `Box`. Using `Box` would force `dry_run_with_policy` to consume the policy, and `policy_sensitivity` (S03) needs to call it multiple times. The settled decision (D149) is `Arc`.
- **Clippy `type_complexity`**: `Arc<dyn Scorer + Send + Sync>` as a field type may trigger `type_complexity` if used in a signature. Follow D021 pattern — create a type alias if needed: `type ArcScorer = Arc<dyn Scorer + Send + Sync>`. Only needed if clippy complains.
- **Trait bound duplication**: `Scorer: Send + Sync` is already in the trait definition, so `Arc<dyn Scorer>` is `Arc<dyn Scorer + Send + Sync>` — the trait object includes the supertrait bounds. Check whether `Arc<dyn Scorer>` is sufficient or if `Arc<dyn Scorer + Send + Sync>` is needed explicitly for `Policy` to be `Send + Sync`. Since `Scorer: Send + Sync` is a bound on the trait itself, `Arc<dyn Scorer>` should already be `Send + Sync`.
- **`run_traced` refactor risk**: Extracting a `run_with_components` helper changes `run_traced` internals. Keep the public API identical — only the private dispatch changes. Run `cargo test --all-targets` immediately after the refactor to confirm no regressions before adding `dry_run_with_policy`.
- **Missing `Slicer` supertrait bounds check**: Confirm `Slicer: Send + Sync` is stated in the trait definition (it is — `pub trait Slicer: Send + Sync`). Same for `Placer`. `Arc<dyn Slicer>` and `Arc<dyn Placer>` are therefore `Send + Sync`.
- **`PolicyBuilder.build()` error message**: Use `"scorer is required"`, `"slicer is required"`, `"placer is required"` — same strings as `PipelineBuilder.build()` for consistency. Use `CupelError::PipelineConfig(String)` — same variant as `PipelineBuilder`.
- **PublicAPI surface**: This is Rust — no `.txt` PublicAPI files. Only .NET needs PublicAPI surface management. Rust uses the standard `pub`/`pub(crate)` visibility system.

## Open Risks

- **`run_traced` refactor complexity**: `run_traced` is 280 lines of stage logic with complex CountCapExceeded detection, score lookups, and PinnedOverride detection. Extracting a helper that accepts injected components must not change observable behavior. Mitigation: run the full test suite before and after the refactor; treat the refactor as a separate commit from adding `dry_run_with_policy`.
- **`Arc<dyn Slicer>` trait method access**: `is_knapsack()`, `is_quota()`, `is_count_quota()`, `count_cap_map()` are defaulted methods on `Slicer`. `Arc<dyn Slicer>` delegates these through `Deref`. Confirm that the `run_with_components` helper can call `slicer.is_count_quota()` when `slicer: &dyn Slicer` — it can, since these are trait methods.
- **Test coverage for `deduplication: false` via policy**: The deduplication flag in `Policy` must be independently testable. Build a test that uses a policy with `deduplication: false` and two items with identical content — verify both appear in the report (no deduplication).

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust | (none needed — standard library patterns only) | n/a |

## Sources

- `crates/cupel/src/pipeline/mod.rs` — full `run_traced` implementation, `PipelineBuilder` pattern
- `crates/cupel/src/analytics.rs` — existing `policy_sensitivity` function showing S03 target shape
- `crates/cupel/src/scorer/mod.rs`, `slicer/mod.rs`, `placer/mod.rs` — trait definitions confirming `Send + Sync` bounds
- `crates/cupel/src/model/overflow_strategy.rs` — `OverflowStrategy` derives `Copy`
- `.kata/DECISIONS.md` — D149 (`Arc<dyn Trait>` for Policy fields), D150 (`policy_sensitivity` as free function in S03), D24 (`dry_run` returns `SelectionReport` and discards `Vec<ContextItem>`)
