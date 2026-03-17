# Quick-Wins Report: Cupel v1.2+

**Session:** 2026-03-15T12-23-brainstorm
**Participants:** explorer-quickwins + challenger-quickwins
**Debate rounds:** 2
**Date:** 2026-03-15

---

## Summary

5 concrete quick-win proposals emerged from debate, plus 1 process recommendation and 1 task to file. Ordered by recommended execution sequence.

---

## Recommended — Ship in v1.2

### 1. Rust API Future-Proofing Pass *(do first — unblocks #5)*

**What:** Add `#[non_exhaustive]` to `OverflowStrategy` **and** `CupelError` in the Rust crate. Add `Debug`, `Clone`, and `Copy` derives to concrete slicer/placer structs (`GreedySlice`, `KnapsackSlice`, `UShapedPlacer`, `ChronologicalPlacer`).

**Why:** `CupelError` and `OverflowStrategy` are public enums with no `#[non_exhaustive]` — adding a variant is currently a breaking change. Adding it now at v1.1 (before real downstream users) is the cheapest possible moment. The derive additions make pipeline types testable by cloning in test harnesses, a common need. No external dependencies required.

**Scope:** Small — attribute additions and derive macros only. Zero logic changes.

**Scoping decisions from debate:**
- `#[non_exhaustive]` applies to both `OverflowStrategy` and `CupelError`
- `Clone` on `CompositeScorer`/`ScaledScorer` is explicitly **cut** — they hold `Vec<Box<dyn Scorer>>` and cannot derive `Clone` without `dyn-clone` (an external dep nobody has requested). Document this as intentional.
- No exhaustive `match` on `CupelError` exists anywhere in the repo — zero breakage inside the codebase from this change.

**Files:** `crates/cupel/src/model/overflow_strategy.rs`, `crates/cupel/src/error.rs`, `crates/cupel/src/slicer/*.rs`, `crates/cupel/src/placer/*.rs`

---

### 2. Spec Conformance Vector Cleanup

**What:** Fix incorrect and misleading comments in 5 conformance vector TOML files. Must update both `spec/conformance/required/` and the vendored copies in `crates/cupel/tests/conformance/` simultaneously.

**Specific fixes:**
- `knapsack-basic.toml` lines 6-28: Remove the abandoned `expensive/cheap-a/cheap-b` first scenario and correct the greedy comparison (the spec claims greedy would select only `small-a`, but greedy actually selects both `small-a` and `small-b`)
- `composite-weighted.toml` lines 24-27: Strip the `# Wait — let me recompute priority` scratchpad text
- `pinned-items.toml` lines 22-27: Add explicit density-sort step to greedy fill trace; clarify that `new(score=1.0)` sources from RecencyScorer
- `u-shaped-basic.toml` + `u-shaped-equal-scores.toml`: Replace ambiguous `right[N]`/`left[N]` pointer notation with `result[N]`

**Why:** The spec is publicly served as an mdBook. The knapsack error specifically will mislead implementers writing new language bindings — it describes the wrong algorithm behavior. This is a reputational risk for a library whose core value proposition is algorithmic correctness.

**Scope:** Small — targeted edits in ~5 TOML files, two copies each.

**Captured follow-up issue:** Add a CI step that diffs `spec/conformance/` against `crates/cupel/tests/conformance/` and fails on divergence. Without this, future spec edits will inevitably miss the vendored copy again.

---

### 3. ContextKind Convenience Constructors (Rust)

**What:** Add `pub fn message() -> Self`, `pub fn system_prompt() -> Self`, `pub fn document() -> Self`, `pub fn tool_output() -> Self`, `pub fn memory() -> Self` factory methods to `ContextKind`. Also implement `TryFrom<&str>` for idiomatic `?`-operator usage in constructors.

**Why:** Current users write `ContextKind::new("message").unwrap()` at every call site. Factory methods eliminate the `unwrap()` and surface all known kinds in IDE autocomplete — the primary crates.io first-impression concern. `TryFrom<&str>` is orthogonal but standard Rust idiom that costs nothing to add at the same time.

**Scope:** Small — ~10 methods on `context_kind.rs`, update doctests.

**Important:** Document in the factory method docs that this is a **DX improvement, not a safety improvement**. `ContextKind::new("message")` cannot panic on non-empty strings — the `unwrap()` was always vacuously safe. Factory methods exist for ergonomics and discoverability, not to prevent panics.

**Files:** `crates/cupel/src/model/context_kind.rs`

---

### 4. `UnreservedCapacity` Helper on ContextBudget

**What:** Add `int UnreservedCapacity` property (.NET) and `pub fn unreserved_capacity(&self) -> u32` method (Rust) to `ContextBudget`.

Formula: `MaxTokens - OutputReserve - sum(ReservedSlots)`

**Why:** This computation is duplicated in multiple pipeline stages in both codebases (confirmed in Rust: `classify.rs` and `slice.rs`; analogous duplication in .NET pipeline). Making it a named, computed property eliminates the fragile arithmetic at call sites and gives the concept a canonical name.

**Scope:** Small — < 20 lines total across both codebases, plus tests.

**Naming rationale:** `UnreservedCapacity` was chosen over `AvailableTokens` (misleading — implies pinned items are already accounted for) and `BudgetableTokens` (too vague). The name is symmetric with existing field names (`OutputReserve`, `ReservedSlots`) — a reader can reconstruct the formula from the name alone.

**Docs must state explicitly:** "This is the capacity available *before* pinned items are accounted for. Pinned items are runtime inputs, not part of the budget configuration, and will reduce effective capacity further."

**Files:** `src/Wollax.Cupel/ContextBudget.cs`, `crates/cupel/src/model/context_budget.rs`

---

### 5. KnapsackSlice DP Table Size Guard *(blocked on #1 for CupelError variant)*

**What:** Add a maximum DP table size check to `KnapsackSlice` in both .NET and Rust before allocating the DP table. If `capacity × n > 50_000_000` (50M cells ≈ 50MB), return a `CupelError::TableTooLarge` (Rust) / throw `InvalidOperationException` with clear message (.NET) rather than silently allocating until OOM.

**Why:** Users who lower `bucket_size` for tighter packing precision can hit dangerous allocations. Example: `bucket_size=1` with 128K token budget × 10K items = 1.28B cells = ~1.28GB just for the bool table. This currently crashes the process with no diagnostic. A guard makes it a recoverable error that the caller can handle by falling back to `GreedySlice`.

**Scope:** Small — one pre-flight check + new error variant.

**Severity framing (corrected from initial proposal):** This is a **small-bucket-size configuration risk**, not a default-config risk. Default `bucket_size=100` discretizes 128K → 1,280 capacity — perfectly safe at any reasonable item count. The guard fires only when users explicitly trade off performance for precision.

**Dependency:** Requires `#[non_exhaustive]` on `CupelError` (Proposal 1) before adding a new variant. Without it, `CupelError::TableTooLarge` is a breaking change.

**Files:** `.NET: src/Wollax.Cupel/KnapsackSlice.cs`, `Rust: crates/cupel/src/slicer/knapsack.rs`, `crates/cupel/src/error.rs`

---

## Process Recommendation — v1.2 Quality Phase

**Issue Backlog Triage:** The project has 74 open issues, the majority of which are purely mechanical (XML doc gaps, enum integer assignments, naming inconsistencies, `#[inheritdoc]` additions, test helper deduplication). These should be batched into a dedicated **v1.2 quality hardening phase** rather than shipped piecemeal.

Exclude from this phase: issues requiring design decisions (cycle detection effectiveness, `overflow-strategy-value-naming`, `quota-slice-expect-on-sub-budget`, `composite-scorer-cycle-detection-ineffective`). These remain in the backlog with explicit design-decision tags.

The phase plan should pre-triage the 40+ mechanical items and group them by file area to minimize context-switching.

---

## Dropped

**CI Dependency Caching** — Demoted from brainstorm proposal to standalone task. The .NET core has zero external dependencies (minimal caching benefit). Rust caching via `Swatinem/rust-cache` is meaningful but trivial to implement in an afternoon. File as a task, not a milestone feature.

---

## Recommended Execution Order

```
v1.2 milestone:
  Phase A: Rust API future-proofing (P1 above) — unblocks P5
  Phase B: Spec conformance vector cleanup (P2) + ContextKind factories (P3) — parallel
  Phase C: UnreservedCapacity helper (P4) — both languages
  Phase D: Knapsack OOM guard (P5) — depends on Phase A
  Ongoing: v1.2 quality hardening phase (process rec)
```

Total estimated scope: 3-4 focused sessions across both codebases.
