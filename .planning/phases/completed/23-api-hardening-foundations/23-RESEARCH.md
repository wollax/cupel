# Phase 23: API Hardening Foundations — Research

**Researched:** 2026-03-15
**Confidence key:** HIGH = verified against codebase / official docs | MEDIUM = confident from direct inspection | LOW = reasoning only

---

## Standard Stack

All work is Rust-only except RAPI-05 (which touches .NET `ContextBudget.cs` as well).

| Concern | Tool / Pattern |
|---------|----------------|
| Attribute placement | `#[non_exhaustive]` outer attribute on `enum` |
| Trait derivation | `#[derive(...)]` proc-macro |
| Conversion trait | `impl TryFrom<&str> for ContextKind` (std) |
| Custom error type | Standalone struct + `thiserror::Error` (already in use) |
| Computed property | Plain `fn`, `i64` arithmetic, saturating subtraction |
| `#[must_use]` | Built-in attribute, place on pure computed-value methods |

**Toolchain:** Rust 1.85 (edition 2024). All features used here have been stable for many releases. No new dependencies needed.

---

## Architecture Patterns

### RAPI-01 — `#[non_exhaustive]` on enums

Place `#[non_exhaustive]` as an outer attribute above the `#[derive(...)]` line:

```rust
#[non_exhaustive]
#[derive(Debug, Clone, thiserror::Error)]
pub enum CupelError { ... }
```

```rust
#[non_exhaustive]
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Hash)]
pub enum OverflowStrategy { ... }
```

**Effect:** Downstream crates cannot write exhaustive `match` on these enums without a `_` arm. This is a semver-safe requirement before adding new variants (Phase 32 adds `CupelError::TableTooLarge`). Adding `#[non_exhaustive]` to an already-published enum is technically a breaking change in strict semver terms, but v1.2 is the declared milestone for this hardening; it is deliberate and expected.

`CupelError` is `#[derive(Debug, Clone, thiserror::Error)]` — no obstacles.
`OverflowStrategy` is `#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Hash)]` — no obstacles.

**Confidence:** HIGH (direct codebase inspection)

---

### RAPI-02 — `Debug + Clone + Copy` on concrete slicer/placer structs

**Target structs and current state:**

| Struct | Location | Current derives | Fields | Copy supportable? |
|--------|----------|-----------------|--------|-------------------|
| `GreedySlice` | `slicer/greedy.rs` | _(none)_ | Unit struct | YES |
| `KnapsackSlice` | `slicer/knapsack.rs` | _(none)_ | `bucket_size: i64` | YES |
| `UShapedPlacer` | `placer/u_shaped.rs` | _(none)_ | Unit struct | YES |
| `ChronologicalPlacer` | `placer/chronological.rs` | _(none)_ | Unit struct | YES |

All four structs are zero-allocation value types. `Copy` is fully supportable on all of them. `Clone` is required by `Copy`.

**Additional traits to consider (Claude's Discretion):**

- `PartialEq` + `Eq`: All four structs have fields that are `PartialEq`/`Eq` (`i64` and unit). Add for all four. Cost: zero.
- `Hash`: Same reasoning — `i64` is `Hash`, unit structs trivially hash. Add for all four.
- `Default`: `GreedySlice`, `UShapedPlacer`, `ChronologicalPlacer` are unit structs — `Default` is trivial. `KnapsackSlice` is NOT safe to `#[derive(Default)]` because the default `i64` (0) would produce an invalid `bucket_size` (must be > 0). Do not derive `Default` for `KnapsackSlice`. The other three can safely derive it.

**Scorer context:** `CompositeScorer` and `ScaledScorer` hold `Box<dyn Scorer>` — not `Copy`/`Clone` via derive. The brainstorm noted "Cut Clone on CompositeScorer/ScaledScorer (needs dyn-clone)". RAPI-02 is scoped only to the four named slicer/placer structs; do NOT touch scorers in this phase.

**Recommended derive sets:**

```rust
// GreedySlice, UShapedPlacer, ChronologicalPlacer (unit structs)
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]

// KnapsackSlice (has bucket_size: i64, invalid default)
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
```

**Confidence:** HIGH (field inspection complete)

---

### RAPI-03 — `ContextKind` factory methods

Add infallible `const fn` factory methods for the five well-known constants:

```rust
impl ContextKind {
    pub const fn message() -> Self { Self(String::new()) }
    // ...
}
```

**Wait — `const fn` constraint:** `ContextKind(String)` — `String` is heap-allocated, so `const fn` constructors returning `Self(String)` are not stable in Rust 1.85. `const fn` that allocates heap memory is not yet stable.

**Resolution:** Factory methods should be regular `fn`, not `const fn`. They delegate to `from_static` (already `pub(crate)`), which needs to remain or become the implementation vehicle.

```rust
pub fn message() -> Self { Self::from_static(Self::MESSAGE) }
pub fn system_prompt() -> Self { Self::from_static(Self::SYSTEM_PROMPT) }
pub fn document() -> Self { Self::from_static(Self::DOCUMENT) }
pub fn tool_output() -> Self { Self::from_static(Self::TOOL_OUTPUT) }
pub fn memory() -> Self { Self::from_static(Self::MEMORY) }
```

These are infallible, cheap (one `to_owned()` call), and match the .NET static properties (`ContextKind.Message`, `ContextKind.SystemPrompt`, etc.) ergonomically.

**ContextKind additional derives:** Current `ContextKind` has `#[derive(Debug, Clone)]` only. `PartialEq`, `Eq`, and `Hash` are manually implemented (case-insensitive semantics). No additional derives to add — custom impls are correct and already present. `Copy` is NOT derivable because `ContextKind(String)` is not `Copy`. `Default` is manually implemented (returns `message()`). No gaps.

**Confidence:** HIGH (const fn limitation verified against stable Rust features; `from_static` confirmed in codebase)

---

### RAPI-04 — `ContextKind: TryFrom<&str>`

**Error type decision (Claude's Discretion):** Use a dedicated `ParseContextKindError` struct rather than reusing `CupelError`. Rationale:
- `TryFrom<&str>` is a general conversion trait; callers expect a specific, lightweight error type (not a fat pipeline error enum).
- `CupelError::EmptyKind` already exists for the constructor path; conflating parse errors with pipeline errors muddies the API.
- A dedicated error type is idiomatic (see `std::num::ParseIntError` precedent).
- Matches the spirit of keeping API surface minimal but precise.

**Error type design:**

```rust
/// Error returned when a string cannot be parsed as a [`ContextKind`].
#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
#[error("invalid context kind: {0:?}")]
pub struct ParseContextKindError(String);
```

**`TryFrom<&str>` implementation strategy:**

The implementation should accept any non-empty, non-whitespace-only string (matching the existing `ContextKind::new` semantics). `TryFrom<&str>` is functionally identical to `ContextKind::new(&str)` but returns `ParseContextKindError` instead of `CupelError`. This is the idiomatic Rust conversion path.

```rust
impl TryFrom<&str> for ContextKind {
    type Error = ParseContextKindError;

    fn try_from(value: &str) -> Result<Self, Self::Error> {
        if value.trim().is_empty() {
            return Err(ParseContextKindError(value.to_owned()));
        }
        Ok(Self(value.to_owned()))
    }
}
```

**String set:** Accept any non-empty string (same as `ContextKind::new`), not just the five well-known names. This matches .NET's `ContextKind(string value)` constructor which accepts arbitrary strings. Case-preservation (not normalization): `TryFrom<&str>` preserves the input string as-is, same as `new()`.

**Aliases:** Do NOT accept alternate spellings or aliases for well-known names. The type is an extensible enum, not a strict finite set. Accept the string as given.

**`ParseContextKindError` placement:** Define it in `context_kind.rs` alongside `ContextKind`. Re-export from `crate::model` and from `crate` root.

**Confidence:** HIGH

---

### RAPI-05 — `ContextBudget::unreserved_capacity()`

**Formula:** `max_tokens - output_reserve - sum(reserved_slots.values())`

Note: The .NET `ContextBudget` already pre-computes `TotalReservedTokens` (an `internal int` field summing `ReservedSlots`). The Rust `ContextBudget` does not have an equivalent cached field — the sum must be computed on-call or cached at construction.

**Arithmetic approach (Claude's Discretion):** Use **saturating subtraction** (`i64::saturating_sub`). Rationale:
- Budget fields are all `>= 0` by construction (validated in `ContextBudget::new`).
- But the validation does NOT enforce `output_reserve + sum(reserved_slots) <= max_tokens`. It only enforces `output_reserve <= max_tokens` separately.
- So `unreserved_capacity()` can legitimately produce a negative result if the user has a large output reserve and many reserved slots.
- However, returning a negative "capacity" is semantically confusing for callers. **Plain subtraction returning `i64`** is more honest — it signals that the budget is over-committed. Use plain `i64` subtraction (not saturating) and let the caller handle negatives.
- Alternative: return `i64` with plain subtraction, and add `has_capacity() -> bool` as `self.unreserved_capacity() > 0`.

**Decided approach:** Return `i64` via plain subtraction. This is maximally honest (negative result is meaningful — budget is over-committed) and consistent with how all other `ContextBudget` methods return `i64`.

**`#[must_use]` (Claude's Discretion):** Add `#[must_use]` in Phase 23. The method is a pure computed value; discarding it is always a bug. Phase 32's full audit will add it more broadly; doing it on new methods now is free and consistent.

**Companion methods (Claude's Discretion):**
- Add `total_reserved() -> i64` — returns `output_reserve + sum(reserved_slots)`. This is already computed internally by .NET (`TotalReservedTokens`) and is useful to expose. Cost: zero.
- Add `has_capacity() -> bool` — returns `self.unreserved_capacity() > 0`. Cheap, prevents off-by-one logic errors in callers.

**Rust implementation:**

```rust
/// Returns the sum of all [`reserved_slots`](Self::reserved_slots) values.
#[must_use]
pub fn total_reserved(&self) -> i64 {
    self.reserved_slots.values().sum()
}

/// Returns the token capacity not committed to output or reserved slots.
///
/// Computed as `max_tokens - output_reserve - sum(reserved_slots)`.
/// May be negative if the budget is over-committed.
#[must_use]
pub fn unreserved_capacity(&self) -> i64 {
    self.max_tokens - self.output_reserve - self.total_reserved()
}

/// Returns `true` if any unreserved capacity remains.
#[must_use]
pub fn has_capacity(&self) -> bool {
    self.unreserved_capacity() > 0
}
```

**.NET parity (Claude's Discretion — RAPI-05 sequencing):**

Implement both Rust and .NET in Phase 23 (design-both-now). The .NET `ContextBudget` already has `TotalReservedTokens` (internal). Add:

```csharp
/// <summary>Sum of OutputReserve and all ReservedSlots values.</summary>
public int TotalReserved => OutputReserve + TotalReservedTokens;

/// <summary>
/// Token capacity not committed to output or reserved slots.
/// May be negative if the budget is over-committed.
/// </summary>
public int UnreservedCapacity => MaxTokens - OutputReserve - TotalReservedTokens;

/// <summary>Returns true if any unreserved capacity remains.</summary>
public bool HasCapacity => UnreservedCapacity > 0;
```

Note: .NET uses `int`, Rust uses `i64`. This is an existing cross-language type discrepancy, not introduced by this phase.

Cross-language conformance testing: Defer to Phase 25/31 per roadmap sequencing. Phase 23 adds unit tests in each language independently.

**Confidence:** HIGH for formula and placement; MEDIUM for caching decision (no performance requirement documented)

---

## Don't Hand-Roll

| Problem | Use Instead |
|---------|-------------|
| Error trait boilerplate | `thiserror::Error` (already in use) |
| `TryFrom` validation logic | Delegate to existing `trim().is_empty()` check |
| `PartialEq`/`Hash`/`Eq` for primitive-field structs | `#[derive(...)]` |
| `Default` for unit structs | `#[derive(Default)]` |

Do NOT hand-roll:
- Custom `impl PartialEq` for slicer/placer structs (derive is correct)
- Custom `impl TryFrom` that does more than `new()` already does
- Cache field for `total_reserved` in Rust — the `reserved_slots` map is small and iteration is cheap; pre-computation is a premature optimization for this phase

---

## Common Pitfalls

### `#[non_exhaustive]` placement order
The attribute must appear before `#[derive(...)]` and before `pub enum`. Placing it after `#[derive]` is syntactically valid but stylistically inconsistent. Keep outer attributes in this order: `#[non_exhaustive]`, then `#[derive(...)]`, then `#[cfg_attr(feature = "serde", ...)]`.

### `Copy` on `KnapsackSlice` with invalid `Default`
Do NOT derive `Default` for `KnapsackSlice`. `bucket_size = 0` would pass the derive but fail the invariant (`bucket_size > 0`). `with_default_bucket_size()` is the correct factory for a default-like construction.

### `const fn` for factory methods
`String::new()` in `const fn` is possible (empty string), but `str::to_owned()` (which produces a non-empty `String`) is not stable in `const fn` as of Rust 1.85. All five factory methods must be regular `fn`.

### `TryFrom<&str>` vs `FromStr`
`TryFrom<&str>` and `FromStr` are closely related. `FromStr` is for parsing from string slices with a custom error; `TryFrom<&str>` is the more general conversion. Implement `TryFrom<&str>` as specified in RAPI-04. Do NOT also implement `FromStr` in this phase unless the planner explicitly adds it — dual implementations create consistency burden.

### `.NET `TotalReservedTokens` visibility
The .NET `TotalReservedTokens` is currently `internal`. Adding `TotalReserved` as a `public` computed property exposes the sum publicly for the first time. This is intentional for RAPI-05 but worth noting.

### Saturating vs plain subtraction
Saturating `i64` would silently clamp a negative result to 0, hiding over-committed budgets. Use plain subtraction and return `i64` (possibly negative) to preserve diagnostic value.

### `#[must_use]` on `has_capacity()`
Mark `has_capacity()` and `unreserved_capacity()` and `total_reserved()` all with `#[must_use]` — they are all pure computed values.

---

## Code Examples

### RAPI-01 — `#[non_exhaustive]` on `CupelError`

```rust
// In error.rs
#[non_exhaustive]
#[derive(Debug, Clone, thiserror::Error)]
pub enum CupelError { ... }
```

### RAPI-01 — `#[non_exhaustive]` on `OverflowStrategy`

```rust
// In overflow_strategy.rs
#[non_exhaustive]
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Hash)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub enum OverflowStrategy { ... }
```

### RAPI-02 — Full derive on unit slicer/placer

```rust
// GreedySlice, UShapedPlacer, ChronologicalPlacer
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub struct GreedySlice;
```

```rust
// KnapsackSlice (no Default — zero bucket_size is invalid)
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct KnapsackSlice {
    bucket_size: i64,
}
```

### RAPI-03 — Factory methods

```rust
impl ContextKind {
    pub fn message() -> Self { Self::from_static(Self::MESSAGE) }
    pub fn system_prompt() -> Self { Self::from_static(Self::SYSTEM_PROMPT) }
    pub fn document() -> Self { Self::from_static(Self::DOCUMENT) }
    pub fn tool_output() -> Self { Self::from_static(Self::TOOL_OUTPUT) }
    pub fn memory() -> Self { Self::from_static(Self::MEMORY) }
}
```

### RAPI-04 — `ParseContextKindError` and `TryFrom<&str>`

```rust
// In context_kind.rs

#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
#[error("invalid context kind: {0:?}")]
pub struct ParseContextKindError(String);

impl TryFrom<&str> for ContextKind {
    type Error = ParseContextKindError;

    fn try_from(value: &str) -> Result<Self, Self::Error> {
        if value.trim().is_empty() {
            return Err(ParseContextKindError(value.to_owned()));
        }
        Ok(Self(value.to_owned()))
    }
}
```

### RAPI-05 — Rust `ContextBudget` additions

```rust
#[must_use]
pub fn total_reserved(&self) -> i64 {
    self.reserved_slots.values().sum()
}

#[must_use]
pub fn unreserved_capacity(&self) -> i64 {
    self.max_tokens - self.output_reserve - self.total_reserved()
}

#[must_use]
pub fn has_capacity(&self) -> bool {
    self.unreserved_capacity() > 0
}
```

### RAPI-05 — .NET `ContextBudget` additions

```csharp
/// <summary>Sum of OutputReserve and all ReservedSlots token counts.</summary>
[JsonIgnore]
public int TotalReserved => OutputReserve + TotalReservedTokens;

/// <summary>
/// Token capacity not committed to output or reserved slots.
/// Computed as MaxTokens - OutputReserve - sum(ReservedSlots).
/// May be negative if the budget is over-committed.
/// </summary>
[JsonIgnore]
public int UnreservedCapacity => MaxTokens - OutputReserve - TotalReservedTokens;

/// <summary>Returns true if any unreserved capacity remains.</summary>
[JsonIgnore]
public bool HasCapacity => UnreservedCapacity > 0;
```

---

## Sequencing Notes

- RAPI-01 must land before Phase 32 (`CupelError::TableTooLarge`). That's the only hard external dependency.
- RAPI-02 through RAPI-05 have no cross-phase ordering constraints.
- RAPI-05 .NET changes can be co-committed with Rust in the same plan iteration.
- `ParseContextKindError` must be re-exported from `crate::model` and `crate` root (lib.rs) once added.
- Cross-language conformance tests for `unreserved_capacity` defer to Phase 25/31.

---

## Files to Modify

| File | Change |
|------|--------|
| `crates/cupel/src/error.rs` | Add `#[non_exhaustive]` |
| `crates/cupel/src/model/overflow_strategy.rs` | Add `#[non_exhaustive]` |
| `crates/cupel/src/slicer/greedy.rs` | Add derive |
| `crates/cupel/src/slicer/knapsack.rs` | Add derive |
| `crates/cupel/src/placer/u_shaped.rs` | Add derive |
| `crates/cupel/src/placer/chronological.rs` | Add derive |
| `crates/cupel/src/model/context_kind.rs` | Add factory methods, `TryFrom<&str>`, `ParseContextKindError` |
| `crates/cupel/src/model/mod.rs` | Re-export `ParseContextKindError` |
| `crates/cupel/src/lib.rs` | Re-export `ParseContextKindError` |
| `crates/cupel/src/model/context_budget.rs` | Add `total_reserved()`, `unreserved_capacity()`, `has_capacity()` |
| `src/Wollax.Cupel/ContextBudget.cs` | Add `TotalReserved`, `UnreservedCapacity`, `HasCapacity` |
