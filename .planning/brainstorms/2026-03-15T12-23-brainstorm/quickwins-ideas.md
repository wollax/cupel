# Quick-Win Ideas: Cupel v1.2+

**Explorer:** explorer-quickwins
**Date:** 2026-03-15

---

## Proposal 1: Batch-Close the Open Issues Backlog

**Name:** Issues Closure Sprint

**What:** Group the 74 open issues into 3-5 themed cleanup phases and execute them. Many issues are tiny (1-5 line changes): XML doc fixes, enum integer assignments, `#[non_exhaustive]` annotations, benchmark naming consistency, `/// <inheritdoc />` additions, scratchpad comments in conformance vectors. A single focused sprint targeting ~40 high-signal/low-effort items could close the majority of the backlog.

**Why:** 74 open issues signals incomplete work to external contributors and users. Closing them publicly demonstrates a mature, maintained project. It also eliminates the cognitive overhead of re-reading the same issues repeatedly across sessions. No new features required — just execution.

**Scope:** Medium (batching + execution for 40+ micro-fixes across both .NET and Rust)

**Risks:** Some issues require design decisions (e.g., ScoredItem score NaN validation — invariant or convention?). Risk of scope creep if design-deciding issues get pulled in. Mitigation: pre-filter to purely mechanical items.

---

## Proposal 2: `available_tokens()` / `AvailableTokens` Computed Helper

**Name:** ContextBudget Available Tokens Helper

**What:** Add `int AvailableTokens` property (.NET) and `fn available_tokens() -> u32` method (Rust) to `ContextBudget`. Formula: `max_tokens - output_reserve - reserved_slots.values().sum()`. This computation is currently duplicated in `classify.rs` and `slice.rs` (Rust) and potentially multiple pipeline stages (.NET).

**Why:** Both the .NET issue tracker (issue #003) and the Rust phase 12 review (#4 in that list) independently identified this duplication. It's a low-risk, high-readability improvement with an obvious implementation. Eliminates fragile manual math at call sites. Makes the budget's "available headroom" a first-class concept.

**Scope:** Small (< 20 lines total across both codebases, plus tests)

**Risks:** None meaningful. The formula is deterministic and already tested indirectly through pipeline tests.

---

## Proposal 3: CI Build Caching (NuGet + Cargo)

**Name:** CI Dependency Caching

**What:** Add `actions/cache` for NuGet packages in `.github/workflows/ci.yml` and `release.yml`, keyed on `*.csproj` + `Directory.Packages.props`. Similarly, add Cargo registry/build caching for Rust workflows via `Swatinem/rust-cache`. Issue phase10-review-suggestion-01 already identified the NuGet gap; Rust caching is the natural parallel.

**Why:** Every CI run currently re-downloads all dependencies. With NuGet Central Package Management + multiple packages, this is meaningful wall-clock time on every PR. Faster CI = faster feedback loop = better contributor experience. This is a set-and-forget improvement with recurring benefit.

**Scope:** Small (add ~10 YAML lines per workflow file)

**Risks:** Cache invalidation complexity is low since both ecosystems have mature cache-key strategies. Risk of stale caches causing mysterious build failures — mitigated by using content-hash keys.

---

## Proposal 4: Rust Ergonomics — ContextKind Factory Methods

**Name:** ContextKind Convenience Constructors

**What:** Add `pub fn message() -> Self`, `pub fn system_prompt() -> Self`, `pub fn document() -> Self`, etc. on `ContextKind` in the Rust crate. Currently callers write `ContextKind::new("message").unwrap()` everywhere, which is verbose and panic-prone. The phase12 review explicitly called this out (#1 in that list). Optionally, also implement `TryFrom<&str>` for a standard Rust ergonomic conversion pattern.

**Why:** Every downstream user of the crate hits this immediately. Removing `unwrap()` from idiomatic usage makes the crate feel professional and Rust-native. It's pure additive API — no breakage, no spec changes. Given Cupel is published on crates.io, first impressions matter.

**Scope:** Small (add ~10 methods to `context_kind.rs`, update doctests)

**Risks:** These factory methods must be consistent with the case-normalization logic (lowercase canonical form). Slight risk of inconsistency if normalization rules change, but that would be a semver-breaking change regardless.

---

## Proposal 5: `#[non_exhaustive]` on OverflowStrategy + Derived Trait Sweep

**Name:** Rust API Future-Proofing Pass

**What:** Mark `OverflowStrategy` as `#[non_exhaustive]` in the Rust crate to prevent downstream match exhaustion from becoming a breaking change when new variants are added. In the same sweep: add `Debug`, `Clone`, and `Copy` derives to `GreedySlice`, `KnapsackSlice`, `UShapedPlacer`, `ChronologicalPlacer` (phase12 suggestion #15). Add `Clone` to `CompositeScorer` and `ScaledScorer` via a `dyn_clone`-style approach if feasible.

**Why:** `#[non_exhaustive]` is a zero-cost Rust idiom for enum extensibility — it's almost always a mistake to ship a public enum without it. The derive additions are equally low-effort and make pipeline types composable (e.g., testable by cloning in test harnesses). These are semver-safe additions.

**Scope:** Small (< 30 lines of attribute additions + one possible new dependency)

**Risks:** `dyn_clone` for trait objects adds a new dependency. Could skip that part and only do the concrete type derives. `#[non_exhaustive]` technically breaks downstream match completeness checks but is the right trade to make before more users depend on the crate.

---

## Proposal 6: Spec Conformance Vector Cleanup

**Name:** Spec Comment Accuracy Pass

**What:** Fix the known-inaccurate comments in conformance vector TOML files (identified in issue `2026-03-14-conformance-vector-comment-quality.md`): remove the abandoned `expensive/cheap-a/cheap-b` scenario from `knapsack-basic.toml`, strip the `# Wait — let me recompute priority` scratchpad from `composite-weighted.toml`, clarify density-sort in `pinned-items.toml`, and fix the `right[N]`/`left[N]` pointer ambiguity in U-shaped placement vectors. Must update both spec/ and crates/cupel/tests/ vendored copies simultaneously.

**Why:** The spec is publicly served as an mdBook. Incorrect comments (especially the knapsack one that claims greedy would only select `small-a` when it actually selects both) will confuse implementers of new language bindings. This is a reputational issue — the spec should be authoritative.

**Scope:** Small (targeted edits in ~5 TOML files, two copies each)

**Risks:** Dual-location edit requirement (spec/ + crate vendored copies) means forgetting one location. A test that checks for parity between spec and crate vectors would catch this — but that test doesn't exist yet (separate issue).

---

## Proposal 7: Rust KnapsackSlice OOM Guard

**Name:** Knapsack DP Table Size Limit

**What:** Add an explicit size check to `KnapsackSlice` before allocating the DP table: `n * budget` cells. Phase12 review issue #7 identified that the current implementation allocates `Vec<Vec<bool>>` unboundedly — with large N and large budgets, this OOM-kills the process rather than returning a graceful error. Add a configurable `max_table_cells: usize` with a sane default (e.g., 10M cells) and return a `CupelError::BudgetTooLarge` if exceeded.

**Why:** Production agentic stacks regularly have large context histories and 128K+ token budgets. `10,000 items × 128,000 tokens = 1.28B cells` would allocate ~1.28GB just for the bool table. This will silently OOM with no diagnostic. A guard turns a crash into a recoverable error. Users can then fall back to `GreedySlice`.

**Scope:** Small-Medium (add guard + new error variant + test, update spec if the limit becomes normative)

**Risks:** Adding a new `CupelError` variant is a breaking change in a `#[non_exhaustive]`-free enum (ties back to Proposal 5). Adding it now while crate is young (v1.1) is the right time. Spec implications: if the spec is silent on this, the limit is purely an implementation detail.
