# Phase 12: Rust Crate (Assay) — Research

**Researched:** 2026-03-14
**Confidence baseline:** HIGH (spec is local and fully read; target repo inspected; Rust ecosystem well-known)

---

## Standard Stack

All crates below are already workspace dependencies in `wollax/assay`, which simplifies adoption.

| Concern | Crate | Version | Confidence | Notes |
|---|---|---|---|---|
| TOML parsing (conformance vectors) | `toml` | 0.8 | HIGH | Already in workspace. Serde-based, handles RFC 3339 datetimes natively via `toml_datetime`. |
| DateTime handling | `chrono` | 0.4 (with `serde` feature) | HIGH | Already in workspace. `DateTime<Utc>` maps directly to spec's UTC timestamp. Supports RFC 3339 parsing, temporal comparison via `Ord`. |
| Error types | `thiserror` | 2 | HIGH | Already in workspace. Derive macro for `std::error::Error`. |
| Serialization framework | `serde` | 1 (with `derive` feature) | HIGH | Already in workspace. Required for TOML deserialization of conformance vectors. Optional feature flag for user-facing serde support on data model types. |
| Test assertions | `assert_approx_eq` or inline | N/A | HIGH | Epsilon comparison for f64 scores. Simple enough to inline: `(a - b).abs() < epsilon`. No external crate needed. |

### Crates NOT needed

| Concern | Why not |
|---|---|
| `ordered-float` | Spec uses IEEE 754 f64 directly. `f64` with manual NaN handling suffices. Sorts can use `f64::total_cmp()` (stable since Rust 1.62). |
| `unicase` | Spec requires ASCII case folding only, not full Unicode. `str::eq_ignore_ascii_case()` and `str::to_ascii_lowercase()` in std suffice. |
| `indexmap` | Insertion-order maps not needed. `HashMap` with sorted iteration or `BTreeMap` with case-folded keys covers all use cases. |
| `itertools` | Nice to have but not necessary. Keep dependencies minimal for a library crate. |

---

## Architecture Patterns

### Crate Location and Naming

The crate lives at `crates/assay-cupel/` within the existing `wollax/assay` workspace. The crate name is `assay-cupel` (not `assay` — that name is reserved for the top-level binary/lib if ever published). The workspace `Cargo.toml` already uses `members = ["crates/*"]`, so adding a new directory under `crates/` is automatic.

**Confidence:** HIGH — verified workspace layout.

### Module Structure

Follow the spec's natural decomposition:

```
crates/assay-cupel/
├── Cargo.toml
├── src/
│   ├── lib.rs              # Public API re-exports
│   ├── model/
│   │   ├── mod.rs
│   │   ├── context_item.rs  # ContextItem struct
│   │   ├── context_budget.rs # ContextBudget struct with validation
│   │   ├── scored_item.rs   # ScoredItem tuple struct
│   │   ├── context_kind.rs  # ContextKind (case-insensitive string newtype)
│   │   ├── context_source.rs # ContextSource (case-insensitive string newtype)
│   │   └── overflow_strategy.rs # OverflowStrategy enum
│   ├── pipeline/
│   │   ├── mod.rs           # Pipeline orchestrator (6-stage run)
│   │   ├── classify.rs
│   │   ├── score.rs
│   │   ├── deduplicate.rs
│   │   ├── sort.rs
│   │   ├── slice.rs
│   │   └── place.rs
│   ├── scorer/
│   │   ├── mod.rs           # Scorer trait
│   │   ├── recency.rs
│   │   ├── priority.rs
│   │   ├── kind.rs
│   │   ├── tag.rs
│   │   ├── frequency.rs
│   │   ├── reflexive.rs
│   │   ├── composite.rs
│   │   └── scaled.rs
│   ├── slicer/
│   │   ├── mod.rs           # Slicer trait
│   │   ├── greedy.rs
│   │   ├── knapsack.rs
│   │   └── quota.rs
│   ├── placer/
│   │   ├── mod.rs           # Placer trait
│   │   ├── chronological.rs
│   │   └── u_shaped.rs
│   └── error.rs             # Error types
└── tests/
    └── conformance/
        ├── mod.rs            # Test vector loader + runner
        ├── scoring.rs
        ├── slicing.rs
        ├── placing.rs
        └── pipeline.rs
```

**Confidence:** HIGH — maps 1:1 to spec structure.

### Data Model Patterns

#### ContextItem — Immutable struct with builder

```rust
#[derive(Debug, Clone)]
pub struct ContextItem {
    content: String,       // non-empty, validated at construction
    tokens: i64,           // caller-provided, may be negative
    kind: ContextKind,     // default: "Message"
    source: ContextSource, // default: "Chat"
    priority: Option<i64>,
    tags: Vec<String>,     // immutable after construction
    metadata: HashMap<String, serde_json::Value>, // opaque
    timestamp: Option<DateTime<Utc>>,
    future_relevance_hint: Option<f64>,
    pinned: bool,
    original_tokens: Option<i64>,
}
```

Use a **runtime-validated builder** (not typestate) — the spec has only two required fields (`content`, `tokens`), and the builder validation is simple enough. Typestate adds complexity without benefit here.

**Confidence:** HIGH — standard Rust pattern for validated construction.

#### ContextKind / ContextSource — Case-insensitive string newtypes

```rust
#[derive(Debug, Clone)]
pub struct ContextKind(String); // stores original casing

impl PartialEq for ContextKind {
    fn eq(&self, other: &Self) -> bool {
        self.0.eq_ignore_ascii_case(&other.0)
    }
}
impl Eq for ContextKind {}

impl Hash for ContextKind {
    fn hash<H: Hasher>(&self, state: &mut H) {
        for b in self.0.bytes() {
            b.to_ascii_lowercase().hash(state);
        }
    }
}
```

This allows `HashMap<ContextKind, _>` to do case-insensitive lookups naturally.

**Confidence:** HIGH — standard approach for case-insensitive keys in Rust.

#### ContextBudget — Validated at construction

All validation rules from the spec are enforced in a `new()` constructor that returns `Result<ContextBudget, CupelError>`.

#### ScoredItem — Simple struct

```rust
#[derive(Debug, Clone)]
pub struct ScoredItem {
    pub item: ContextItem,
    pub score: f64,
}
```

### Trait Design

#### Scorer, Slicer, Placer — Public traits from day one

```rust
pub trait Scorer {
    fn score(&self, item: &ContextItem, all_items: &[ContextItem]) -> f64;
}

pub trait Slicer {
    fn slice(&self, sorted: &[ScoredItem], budget: &ContextBudget) -> Vec<ContextItem>;
}

pub trait Placer {
    fn place(&self, items: &[ScoredItem]) -> Vec<ContextItem>;
}
```

Make traits public from day one. The spec defines clear interfaces, and users of the crate may want to implement custom scorers/slicers/placers.

**Confidence:** HIGH — spec mandates well-defined interfaces.

### Reference Identity for FrequencyScorer and ScaledScorer

The spec requires `is` (reference identity) checks for self-exclusion in FrequencyScorer and ScaledScorer. In Rust, use pointer equality:

```rust
std::ptr::eq(item, &all_items[i])
```

This works because the scorer receives `&ContextItem` and `&[ContextItem]` — the item reference points into the slice. The pipeline must pass items by reference from the same backing slice.

**Confidence:** HIGH — std::ptr::eq is the idiomatic Rust way to check reference identity.

### Error Modeling

Use a **single error enum** for the crate:

```rust
#[derive(Debug, thiserror::Error)]
pub enum CupelError {
    #[error("content must not be empty")]
    EmptyContent,

    #[error("budget validation failed: {0}")]
    InvalidBudget(String),

    #[error("pinned items ({pinned_tokens} tokens) exceed available budget ({available} tokens)")]
    PinnedExceedsBudget { pinned_tokens: i64, available: i64 },

    #[error("overflow: merged items ({merged_tokens} tokens) exceed target ({target_tokens} tokens)")]
    Overflow { merged_tokens: i64, target_tokens: i64 },

    #[error("scorer configuration error: {0}")]
    ScorerConfig(String),

    #[error("slicer configuration error: {0}")]
    SlicerConfig(String),

    #[error("cycle detected in scorer graph")]
    CycleDetected,
}
```

A single enum is appropriate because the crate's error surface is small and errors cross module boundaries (pipeline errors reference scorer/budget errors).

**Confidence:** HIGH.

### Pipeline Orchestrator

A `Pipeline` struct configured with scorer, slicer, placer, deduplication flag, and overflow strategy. Single `run()` method that executes the 6 stages:

```rust
pub struct Pipeline {
    scorer: Box<dyn Scorer>,
    slicer: Box<dyn Slicer>,
    placer: Box<dyn Placer>,
    deduplication: bool,
    overflow_strategy: OverflowStrategy,
}

impl Pipeline {
    pub fn run(&self, items: &[ContextItem], budget: &ContextBudget) -> Result<Vec<ContextItem>, CupelError> {
        // 1. Classify
        // 2. Score
        // 3. Deduplicate
        // 4. Sort
        // 5. Slice
        // 6. Place
    }
}
```

**Confidence:** HIGH — direct mapping from spec.

---

## Don't Hand-Roll

| Problem | Use Instead | Why |
|---|---|---|
| TOML parsing | `toml` crate with serde | Battle-tested, handles datetimes correctly |
| DateTime comparison/parsing | `chrono::DateTime<Utc>` | RFC 3339 parsing, `Ord` impl, UTC semantics |
| Error boilerplate | `thiserror` derive | Already in workspace, standard practice |
| Case-insensitive ASCII comparison | `str::eq_ignore_ascii_case()` | In std, correct for ASCII fold |
| Stable sort | `slice::sort_by()` | Rust's sort is guaranteed stable (TimSort) |
| IEEE 754 total ordering for sort | `f64::total_cmp()` | Stable since Rust 1.62, handles NaN consistently |

---

## Common Pitfalls

### P1: Sort Stability and Tiebreaking
**Risk:** HIGH
**Mitigation:** Rust's `sort_by` is stable. Use composite key `(score descending, index ascending)` via `f64::total_cmp()` for the score component. The spec mandates `(Score, Index)` tiebreaking at Sort stage, GreedySlice density sort, and both placers.

### P2: Case-Insensitive ContextKind in HashMap
**Risk:** HIGH
**Mitigation:** Custom `Hash` and `PartialEq` impls on `ContextKind` newtype that use ASCII lowercase. This ensures `HashMap<ContextKind, _>` lookups are case-insensitive without any special lookup code at call sites.

### P3: Byte-Exact Deduplication
**Risk:** MEDIUM
**Mitigation:** Rust's `String` comparison is byte-exact by default. No special handling needed — just use `==` on content strings. Do NOT use any Unicode normalization.

### P4: Reference Identity for Self-Exclusion
**Risk:** HIGH
**Mitigation:** Use `std::ptr::eq()` for FrequencyScorer and ScaledScorer self-exclusion. The pipeline must pass items as references into a contiguous slice (not cloned copies) so that pointer comparison works. This is a design constraint that must be maintained through the Score stage.

### P5: KnapsackSlice Discretization Arithmetic
**Risk:** MEDIUM
**Mitigation:** Score scaling: `max(0, (score * 10000.0).floor() as i64)`. Weight discretization: ceiling division `(tokens + bucket_size - 1) / bucket_size`. Capacity: floor division `target_tokens / bucket_size`. The asymmetry (ceil weights, floor capacity) is deliberate.

### P6: QuotaSlice Floor Truncation
**Risk:** MEDIUM
**Mitigation:** All percentage-to-token conversions use `floor()`. This means `(percent / 100.0 * target_tokens).floor() as i64`. Sum of kind budgets may be less than target — this is correct behavior per spec.

### P7: GreedySlice Zero-Token Density
**Risk:** LOW
**Mitigation:** Zero-token items get density `f64::MAX`. They sort first and are always included without consuming budget.

### P8: Overflow Detection Uses Original targetTokens
**Risk:** MEDIUM
**Mitigation:** The Place stage checks overflow against `budget.targetTokens` (the original, not the effective budget computed for slicing). This is a subtle distinction that could cause conformance failures if confused.

### P9: CompositeScorer Weight Normalization
**Risk:** LOW
**Mitigation:** Normalize at construction: `weight_i / sum(all_weights)`. Reject zero/negative/non-finite weights at construction time.

### P10: TOML Datetime Parsing
**Risk:** MEDIUM
**Mitigation:** The `toml` crate parses TOML datetimes into `toml::value::Datetime`, not `chrono::DateTime`. Need custom serde deserialization or post-parse conversion to `chrono::DateTime<Utc>`. The conformance vectors use RFC 3339 format like `2024-01-01T00:00:00Z`.

### P11: Conformance Test Vector Location
**Risk:** LOW
**Mitigation:** Conformance vectors live at `cupel/conformance/required/` and `cupel/conformance/optional/`. Options for consumption: (a) copy TOML files into the assay-cupel crate's test resources, (b) use a path reference during development, (c) git submodule. Copying is simplest for the initial phase.

---

## Code Examples

### Stable Sort with Composite Key

```rust
// Sort stage: descending by score, ascending by index
let mut indexed: Vec<(usize, &ScoredItem)> = items.iter().enumerate().collect();
indexed.sort_by(|(idx_a, a), (idx_b, b)| {
    b.score.total_cmp(&a.score)  // descending
        .then(idx_a.cmp(idx_b))  // ascending index tiebreak
});
```

### Case-Insensitive ContextKind

```rust
use std::hash::{Hash, Hasher};

#[derive(Debug, Clone)]
pub struct ContextKind(String);

impl ContextKind {
    pub fn new(value: impl Into<String>) -> Result<Self, CupelError> {
        let s: String = value.into();
        if s.trim().is_empty() {
            return Err(CupelError::EmptyContent); // or a dedicated variant
        }
        Ok(Self(s))
    }

    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl PartialEq for ContextKind {
    fn eq(&self, other: &Self) -> bool {
        self.0.eq_ignore_ascii_case(&other.0)
    }
}
impl Eq for ContextKind {}

impl Hash for ContextKind {
    fn hash<H: Hasher>(&self, state: &mut H) {
        for b in self.0.bytes() {
            b.to_ascii_lowercase().hash(state);
        }
    }
}
```

### Reference Identity Check

```rust
// FrequencyScorer: skip self by pointer equality
fn score(&self, item: &ContextItem, all_items: &[ContextItem]) -> f64 {
    if item.tags.is_empty() || all_items.len() <= 1 {
        return 0.0;
    }

    let matching = all_items.iter()
        .filter(|other| !std::ptr::eq(*other, item))  // reference identity
        .filter(|other| !other.tags.is_empty())
        .filter(|other| shares_any_tag(&item.tags, &other.tags))
        .count();

    matching as f64 / (all_items.len() - 1) as f64
}
```

### Conformance Test Runner Pattern

```rust
#[cfg(test)]
mod conformance {
    use std::fs;
    use toml::Value;

    fn load_vector(path: &str) -> Value {
        let content = fs::read_to_string(path).unwrap();
        content.parse::<Value>().unwrap()
    }

    #[test]
    fn recency_basic() {
        let vector = load_vector("tests/conformance/required/scoring/recency-basic.toml");
        let items = build_items(&vector["items"]);
        let scorer = RecencyScorer;
        let epsilon = vector.get("tolerance")
            .and_then(|t| t.get("score_epsilon"))
            .and_then(|e| e.as_float())
            .unwrap_or(1e-9);

        for expected in vector["expected"].as_array().unwrap() {
            let content = expected["content"].as_str().unwrap();
            let expected_score = expected["score_approx"].as_float().unwrap();
            let item = items.iter().find(|i| i.content() == content).unwrap();
            let actual = scorer.score(item, &items);
            assert!((actual - expected_score).abs() < epsilon,
                "Score mismatch for '{}': expected {}, got {}", content, expected_score, actual);
        }
    }
}
```

### KnapsackSlice DP Core

```rust
fn knapsack_slice(items: &[ScoredItem], target_tokens: i64, bucket_size: i64) -> Vec<ContextItem> {
    let capacity = (target_tokens / bucket_size) as usize;
    if capacity == 0 {
        return zero_token_items(items);
    }

    // Discretize
    let candidates: Vec<_> = items.iter()
        .filter(|si| si.item.tokens() > 0)
        .collect();
    let n = candidates.len();

    let weights: Vec<usize> = candidates.iter()
        .map(|si| ((si.item.tokens() as usize + bucket_size as usize - 1) / bucket_size as usize))
        .collect();
    let values: Vec<i64> = candidates.iter()
        .map(|si| 0i64.max((si.score * 10000.0).floor() as i64))
        .collect();

    // 1D DP + 2D keep table
    let mut dp = vec![0i64; capacity + 1];
    let mut keep = vec![vec![false; capacity + 1]; n];

    for i in 0..n {
        let dw = weights[i];
        let dv = values[i];
        for w in (dw..=capacity).rev() {
            let with_item = dp[w - dw] + dv;
            if with_item > dp[w] {
                dp[w] = with_item;
                keep[i][w] = true;
            }
        }
    }

    // Reconstruct
    let mut selected = Vec::new();
    let mut remaining = capacity;
    for i in (0..n).rev() {
        if keep[i][remaining] {
            selected.push(candidates[i].item.clone());
            remaining -= weights[i];
        }
    }

    // Zero-token items first, then DP-selected
    let mut result = zero_token_items(items);
    result.extend(selected);
    result
}
```

---

## Discretionary Recommendations

These address the "Claude's Discretion" items from CONTEXT.md.

### Builder Pattern: Runtime-validated builder
**Recommendation:** Runtime-validated builder with `ContextItemBuilder::new(content, tokens).kind(...).build()` returning `Result<ContextItem, CupelError>`.
**Rationale:** Only 2 required fields. Typestate would create 4 generic type parameters for minimal safety gain. The Rust ecosystem norm for data-heavy structs is runtime validation (see `reqwest::RequestBuilder`, `tonic::Request`).

### Error Modeling: Single enum
**Recommendation:** One `CupelError` enum for the entire crate.
**Rationale:** Small API surface, errors cross module boundaries. Users catch pipeline errors in one place.

### Trait Extensibility: Public from day one
**Recommendation:** `Scorer`, `Slicer`, `Placer` traits are `pub`.
**Rationale:** The spec defines clear interfaces. Users wanting custom scorers/slicers need public traits. Adding `pub` later is a breaking change.

### Crate Structure: Single crate in workspace
**Recommendation:** Single crate `assay-cupel` under `crates/`. No feature flags needed for v1.0.
**Rationale:** The implementation is small (~2000-3000 lines). Feature flags add complexity without value until serde support or async is added.

### Serde Support: Behind feature flag, but defer to post-v1.0
**Recommendation:** Skip serde derives on data model types for v1.0. Add `serde` feature flag in a follow-up.
**Rationale:** The conformance test runner needs serde for TOML parsing (dev-dependency), but the public API does not need serde for its initial consumer (assay itself).

### Async/Streaming: Sync only for v1.0
**Recommendation:** Sync-only. No StreamSlice equivalent.
**Rationale:** The spec's pipeline is inherently synchronous. The assay project can wrap sync calls in `spawn_blocking` if needed.

### MSRV Policy: Rust 2024 edition (1.85+)
**Recommendation:** Match the existing workspace: `edition = "2024"` (Rust 1.85+).
**Rationale:** The workspace already uses `edition = "2024"` and `rustfmt.toml` specifies it. No reason to differ.

### Conformance Test Consumption: Copy TOML files into crate
**Recommendation:** Copy the 28 required TOML vectors into `crates/assay-cupel/tests/conformance/required/`. Use `include_str!()` or `std::fs::read_to_string()` at test time.
**Rationale:** Simpler than git submodule. Files are small (~1-3KB each). Can be updated by re-copying when spec changes. No build-time dependency on cupel repo.

### Conformance Runner: cargo test only
**Recommendation:** Integration tests under `tests/conformance/`. No standalone binary.
**Rationale:** `cargo test` is the standard Rust way. A standalone binary adds maintenance burden with no benefit for this phase.

### CI/CD: Minimal GitHub Actions
**Recommendation:** The assay repo likely already has CI. Add the new crate to existing workflow. At minimum: `cargo test -p assay-cupel`, `cargo clippy -p assay-cupel`, `cargo fmt -- --check`.
**Rationale:** Leverage existing infra. Don't create a parallel CI pipeline.

---

## Key Spec Constraints Summary

For planner reference — the non-obvious constraints that shape implementation tasks:

1. **Fixed 6-stage pipeline order:** Classify, Score, Deduplicate, Sort, Slice, Place. Cannot reorder or skip.
2. **IEEE 754 f64 throughout:** All scoring math is f64. Use `f64::total_cmp()` for sorts.
3. **Stable sort with (Score desc, Index asc)** at Sort stage, GreedySlice, UShapedPlacer, ChronologicalPlacer.
4. **Byte-exact deduplication:** `String::eq()` in Rust — no normalization.
5. **Case-insensitive ASCII for ContextKind/ContextSource:** Custom `Hash`/`Eq` on newtype.
6. **Case-insensitive ASCII for tag comparison** in FrequencyScorer (but case-sensitive in TagScorer by default).
7. **Reference identity** for self-exclusion in FrequencyScorer and ScaledScorer.
8. **Pinned items score 1.0** when merged in Place stage.
9. **Overflow checks against original targetTokens**, not effective target.
10. **28 required conformance test vectors** across 4 categories: 14 scoring, 6 slicing, 4 placing, 5 pipeline (total verified from file listing: actually 13 scoring, 6 slicing, 4 placing, 5 pipeline = 28).

---

*Phase: 12-rust-crate-assay*
*Research completed: 2026-03-14*
