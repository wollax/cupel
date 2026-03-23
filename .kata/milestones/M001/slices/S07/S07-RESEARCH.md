# S07: Rust Quality Hardening — Research

**Date:** 2026-03-21

## Summary

S07 owns two requirements: R002 (KnapsackSlice DP guard, primary) and R005 (Rust quality hardening). Both are straightforward in intent but the DP guard introduces a **non-trivial architectural question**: `Slicer::slice` currently returns `Vec<ContextItem>`, not `Result`. Adding a fallible OOM guard requires either changing the trait signature (semver-breaking for downstream `Slicer` implementors) or finding an alternative propagation path.

The backlog triage across `.planning/issues/open/` identified ~15 high-signal Rust items. The CompositeScorer cycle detection is the most conceptually important: the DFS is genuinely ineffective (owned `Box` types make structural cycles impossible), but the attempted cycle detection also pollutes the public `Scorer` trait with a `#[doc(hidden)] fn as_any()` that every downstream implementor must provide. The UShapedPlacer `.expect()` is sound but untested at edge cases (0, 1, 2 items). The QuotaSlice `.expect()` on sub-budget construction benefits naturally from the Slicer-trait-Result change if that path is taken.

**Recommended approach**: Change `Slicer::slice` to `Result<Vec<ContextItem>, CupelError>`, accept the semver-breaking change in v1.2.0 (all three built-in impls are in-crate; the break is bounded and discoverable at compile time), add `CupelError::TableTooLarge`, remove the ineffective cycle detection and `as_any` from `Scorer`, refactor `UShapedPlacer` to eliminate `Vec<Option>`, and batch ~10 test additions covering scorer edge cases and pipeline boundaries.

## Recommendation

**Work in two streams:**

**Stream 1 — Correctness (3–4 tasks):**
- Change `Slicer::slice` to return `Result<Vec<ContextItem>, CupelError>` (required for all slicer error propagation)
- Add `CupelError::TableTooLarge` variant; add guard to `KnapsackSlice::slice` (`capacity * n > 50_000_000` where `capacity = target_tokens / bucket_size`)
- Update `QuotaSlice` to propagate `?` instead of `.expect()` on sub-budget construction
- Update `pipeline::slice::slice_items` and both `run` / `run_traced` call sites to propagate the new `Result`
- While in knapsack.rs: replace `Vec<Vec<bool>>` keep table with flat `Vec<bool>` (single allocation)

**Stream 2 — Clarity + quality (2–3 tasks):**
- Remove `CompositeScorer` cycle detection entirely; remove `fn as_any` from `Scorer` trait; update all 8 `impl Scorer` blocks; add doc comment explaining why cycles are impossible with owned `Box`
- Refactor `UShapedPlacer` to use explicit left/right vecs (eliminates `Vec<Option>` + `.expect()`); add unit tests for 0, 1, 2, 3, 4 items
- Batch test additions: scorer unit tests (TagScorer case-insensitive, zero-total-weight; PriorityScorer zero-to-one range; ScaledScorer degenerate; ReflexiveScorer NaN/Inf); pipeline unit tests (single item, all-negative-token items); remove `detect_cycles_dfs` dead code; scope `release-rust.yml` permissions to job level

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| DP table OOM guard threshold | D006: `50M cells = capacity × items`; see .NET impl at `src/Wollax.Cupel/KnapsackSlice.cs:119-125` | Exact guard: `(capacity as u64) * (n as u64) > 50_000_000`; message format mirrors .NET: `"KnapsackSlice DP table exceeds the 50,000,000-cell limit: candidates={n}, capacity={capacity}, cells={cells}"` |
| Error message field format | D031 (Rust version): use only public API names in error messages | `candidates=N, capacity=C, cells=K` (no type names) |
| CupelError backward compat | `#[non_exhaustive]` already on `CupelError` (`error.rs`) | Adding `TableTooLarge` variant is non-breaking for match exhaustiveness |
| Flat 2D DP table | phase12 suggestion #6 | `keep[i * (capacity + 1) + w]` instead of `keep[i][w]`; single `Vec<bool>` allocation |

## Existing Code and Patterns

- `crates/cupel/src/slicer/knapsack.rs` — `KnapsackSlice::slice`; capacity computed as `(budget.target_tokens() / self.bucket_size) as usize`; `keep` table is `vec![vec![false; capacity + 1]; n]`; guard must go before this allocation at `Step 4`
- `crates/cupel/src/slicer/mod.rs` — `Slicer` trait definition; `slice` returns `Vec<ContextItem>` today; must change to `Result<Vec<ContextItem>, CupelError>`; all 3 built-in impls are in-crate (`GreedySlice`, `KnapsackSlice`, `QuotaSlice`)
- `crates/cupel/src/pipeline/slice.rs` — `slice_items` is `pub(crate)` and calls `slicer.slice()`; needs to propagate `?`; `compute_effective_budget` uses `.expect()` on valid sub-budget (fine — budget arithmetic cannot fail there)
- `crates/cupel/src/pipeline/mod.rs:148,287` — two call sites for `slice_items` in `run` and `run_traced`; both already return `Result<_, CupelError>` so `?` propagation is clean
- `crates/cupel/src/slicer/quota.rs:194-195` — `QuotaSlice` calls `.expect("sub-budget should be valid...")` on `ContextBudget::new()`; with `Slicer::slice → Result`, this can become `ContextBudget::new(...).map_err(|e| e)?` or a `?` propagation
- `crates/cupel/src/scorer/composite.rs:88-90` — `scorer_identity` uses data pointer comparison; `detect_cycles_dfs` uses this for DFS; both functions exist only to support a check that cannot fire (owned `Box` prevents cycles structurally); `as_any` on the `Scorer` trait is the root cause
- `crates/cupel/src/scorer/mod.rs:105` — `fn as_any(&self) -> &dyn Any;` on the public `Scorer` trait; `#[doc(hidden)]` but still public and requires boilerplate in all 8 impls; removal requires also removing `CycleDetected` variant from `CupelError` (or leaving it as a dead variant with a doc note)
- `crates/cupel/src/placer/u_shaped.rs:52-58` — `Vec<Option<ContextItem>>` with `.expect()`; invariant is correct (left+right pointers fill all n slots; `if right == 0 { break }` prevents usize underflow) but untested at edge cases (0, 1, 2, odd-n items)
- `crates/cupel/src/error.rs` — `CupelError` enum; `#[non_exhaustive]`; add `TableTooLarge { candidates: usize, capacity: usize, cells: u64 }` variant with `#[error("...")]` format
- `crates/cupel/tests/conformance/slicing.rs` — existing knapsack tests: `knapsack_basic` and `knapsack_zero_tokens`; no error-path test exists; add `knapsack_table_too_large` unit test in `src/slicer/knapsack.rs` under `#[cfg(test)]`
- `.github/workflows/release-rust.yml` — `permissions: contents: write` declared at workflow level (grants write to both `test` and `publish` jobs); move to job level: `test` → `read`, `publish` → `write`

## Constraints

- **MSRV 1.85.0, Edition 2024** — no new external dependencies; standard library only
- **Slicer trait is public** — changing `slice` return type is semver-breaking for any downstream `Slicer` implementor; acceptable for v1.2.0 given all built-in impls are in-crate and the break is compile-time-visible (no silent breakage)
- **`cargo clippy --all-targets -- -D warnings` must stay clean** — S05 confirmed zero warnings baseline; removing `as_any` and `detect_cycles_dfs` removes dead code that would otherwise require `#[allow(dead_code)]`
- **`CupelError` is `#[non_exhaustive]`** — adding `TableTooLarge` does not break existing match exhaustiveness (callers already need a catch-all); no semver concern here
- **Zero external deps in core** — DP table optimization and guard are pure stdlib; `KnapsackSlice` stays zero-dep
- **Conformance vectors unchanged** — no new conformance vectors for S07; the TableTooLarge guard is a unit test concern, not a pipeline-vector concern (it fires before the pipeline stage completes normally)

## Common Pitfalls

- **Guard uses discretized capacity, not raw token count** — The guard must use `capacity = (budget.target_tokens() / self.bucket_size) as usize`, the same value used for the DP table, not the raw token count. `capacity * n > 50_000_000` matches D006 exactly. Use `u64` arithmetic to avoid overflow: `(capacity as u64) * (n as u64) > 50_000_000`.
- **`QuotaSlice` calls `inner.slice()` recursively** — After `Slicer::slice → Result`, `QuotaSlice::slice` must propagate errors from `self.inner.slice(items, &sub_budget)?`. The `?` operator means `QuotaSlice::slice` will also return an error if the inner `KnapsackSlice` fires the guard. This is correct behavior.
- **Removing `as_any` from Scorer trait requires removing `CycleDetected` from CupelError or leaving it** — `CycleDetected` was only emitted by `detect_cycles_dfs`. Options: (a) remove the variant entirely (breaking for match arms), (b) leave it as a dead variant with a `/// Never emitted — reserved for future use` doc. Since `CupelError` is `#[non_exhaustive]`, removing a variant IS semver-breaking (downstream could match on it). Keep `CycleDetected` but mark as reserved; remove the detection code that emits it.
- **`UShapedPlacer` right-pointer underflow guard** — The `if right == 0 { break; }` in `u_shaped.rs:56-58` is a usize underflow guard (not a logic guard). The alternative implementation using explicit left-vec/right-vec concatenation sidesteps this entirely and is easier to reason about. The issue reports this as fragile; the refactor is the correct fix.
- **Flat `Vec<bool>` index math** — `keep[i * stride + w]` where `stride = capacity + 1`; must pre-allocate `vec![false; n * stride]` and the loop `for w in (dw..=capacity).rev()` over the keep table must use the flat index; reconstruction loop at Step 5 must also use `i * stride + remaining_capacity`.
- **`QuotaSlice` `.expect()` → `?` migration** — The `ContextBudget::new(cap, kind_budget, 0, ...)` call can fail only if `kind_budget > cap` or values are negative. The `kind_budget = kind_budget.min(cap)` defensive guard precedes it, so failure is structurally impossible. But with the trait returning `Result`, the correct code is `.expect(...)` remains acceptable OR convert to `map_err(CupelError::InvalidBudget)?` with an explanatory message. Keep the `.expect()` with an updated comment that explicitly references the `kind_budget.min(cap)` guard above it — this is the D019 pattern (preserve invariant comment).

## Open Risks

- **Semver break for `Slicer::slice → Result`**: Any crate depending on cupel 1.x that implements the `Slicer` trait will fail to compile against v1.2.0. This is acceptable for a small/early crate but planning must explicitly acknowledge it and ensure the changelog/commit message is clear.
- **`CupelError::CycleDetected` removal ambiguity**: If any downstream match arm covers `CycleDetected`, removing it breaks compilation. Keeping it as a reserved/unreachable variant is safer for semver; it will stay in the enum but can never be constructed by user code (construction only happened internally and that code is being removed).
- **`cargo test` after Slicer trait change**: All 3 slicer impls + `slice_items` + conformance tests must compile. The conformance test harness (`tests/conformance/slicing.rs`) calls `slicer.slice()` directly and currently discards the result — it will need `.unwrap()` or `?` after the change.
- **No test for `KnapsackSlice::TableTooLarge` path in conformance vectors**: This is correct — the guard fires before the DP stage, so it cannot be represented as a conformance vector (which is a pipeline-level abstraction). Unit test in `#[cfg(test)]` in `knapsack.rs` is the right home.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust (stdlib, error propagation) | — | No external skill needed |

## Sources

- `.planning/issues/open/2026-03-14-composite-scorer-cycle-detection-ineffective.md` — CompositeScorer DFS is ineffective; owned Box types prevent structural cycles
- `.planning/issues/open/2026-03-14-u-shaped-placer-expect-on-option-vec.md` — UShapedPlacer invariant fragility
- `.planning/issues/open/2026-03-14-quota-slice-expect-on-sub-budget.md` — QuotaSlice sub-budget `.expect()`
- `.planning/issues/open/phase12-review-suggestions.md` — KnapsackSlice OOM guard (#7), flat Vec (#6), zip idiom (#3), zero unit tests in src/ (#18), scorer edge case tests (#19-25)
- `.planning/issues/open/phase05-review-suggestions-tests.md` — GreedySlice, UShapedPlacer, pipeline boundary tests
- `.planning/issues/open/scorer-test-gaps.md` — TagScorer case-insensitive, zero-weight; PriorityScorer range test
- `.planning/issues/open/2026-03-14-scope-release-rust-permissions.md` — release-rust.yml least-privilege
- `crates/cupel/src/slicer/knapsack.rs` — current DP implementation; no guard present
- `crates/cupel/src/slicer/mod.rs` — `Slicer::slice` returns `Vec<ContextItem>`; must change to `Result`
- `crates/cupel/src/scorer/composite.rs` — `scorer_identity`, `detect_cycles_dfs`, `as_any` boilerplate
- `crates/cupel/src/placer/u_shaped.rs` — `Vec<Option<ContextItem>>` + `.expect()`
- `crates/cupel/src/error.rs` — `CupelError`; `#[non_exhaustive]`; no `TableTooLarge` today
- `src/Wollax.Cupel/KnapsackSlice.cs:119-125` — .NET guard implementation (reference for message format and threshold)
- `.kata/DECISIONS.md` — D006 (50M threshold), D008 (QH scope), D031 (no internal types in error messages), D015 (#[non_exhaustive])
- `S05-SUMMARY.md` — confirmed zero clippy warnings baseline; `--all-targets` now enforced
