# Phase 20: Serde Feature Flag - Research

**Researched:** 2026-03-15
**Confidence:** HIGH (serde is well-documented; all patterns verified against official docs and codebase reading)

## Standard Stack

| Dependency | Version | Feature Flags | Purpose |
|---|---|---|---|
| `serde` | `1` | `derive` | Serialize/Deserialize derives and manual impls |
| `chrono` (existing) | `0.4` | `serde` (conditional) | DateTime<Utc> serialization via chrono's built-in serde support |
| `serde_json` (dev) | `1` (already present) | - | Roundtrip tests for serde implementations |

No new crate dependencies. `serde` moves from dev-dependency to optional dependency. `chrono`'s `serde` feature activates conditionally.

### Cargo.toml Feature Configuration

```toml
[features]
default = []
serde = ["dep:serde", "chrono/serde"]

[dependencies]
chrono = "0.4"
serde = { version = "1", features = ["derive"], optional = true }
thiserror = "2"
```

Key detail: Using `"dep:serde"` syntax (Rust 1.60+, edition 2024 already in use) prevents implicit `cfg(feature = "serde")` from the dependency name alone -- this is the correct modern pattern. The `chrono/serde` activates chrono's serde feature only when cupel's serde feature is enabled.

**Confidence: HIGH** -- verified from Cargo reference and existing research in `.planning/research/FEATURES.md`.

## Architecture Patterns

### Pattern 1: Conditional Derives via `cfg_attr`

For types that can use standard derive (no validation needed on deserialization):

```rust
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Hash)]
#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]
pub enum OverflowStrategy {
    #[default]
    Throw,
    Truncate,
    Proceed,
}
```

Use `serde::Serialize` / `serde::Deserialize` (fully-qualified paths) in the derive -- this avoids needing `use serde::{Serialize, Deserialize}` imports that would fail when the feature is off.

**Applies to:** `OverflowStrategy` only (the sole type with no validation).

**Confidence: HIGH** -- standard serde pattern, verified in official docs.

### Pattern 2: Newtype String Serialization for ContextKind / ContextSource

These are `struct ContextKind(String)` newtypes with validation in `::new()`. They should serialize as bare strings (not `{"0": "Message"}`).

**Serialize:** Implement manually to delegate to the inner string.
**Deserialize:** Implement manually to route through `::new()` for validation.

```rust
#[cfg(feature = "serde")]
impl serde::Serialize for ContextKind {
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.serialize_str(&self.0)
    }
}

#[cfg(feature = "serde")]
impl<'de> serde::Deserialize<'de> for ContextKind {
    fn deserialize<D: serde::Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        let s = String::deserialize(deserializer)?;
        ContextKind::new(s).map_err(serde::de::Error::custom)
    }
}
```

The `map_err(serde::de::Error::custom)` converts `CupelError` to serde's error type. This works because `CupelError` implements `Display` (via `thiserror`).

**Applies to:** `ContextKind`, `ContextSource` (identical pattern, different type).

**Confidence: HIGH** -- standard serde newtype-as-string pattern; validated against serde.rs docs.

### Pattern 3: Custom Deserializer via Intermediate Raw Struct (ContextBudget)

For structs where the constructor does cross-field validation, use a private "raw" intermediate struct that derives `Deserialize`, then convert through the validating constructor:

```rust
#[cfg(feature = "serde")]
impl serde::Serialize for ContextBudget {
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        use serde::ser::SerializeStruct;
        let mut s = serializer.serialize_struct("ContextBudget", 5)?;
        s.serialize_field("max_tokens", &self.max_tokens)?;
        s.serialize_field("target_tokens", &self.target_tokens)?;
        s.serialize_field("output_reserve", &self.output_reserve)?;
        s.serialize_field("reserved_slots", &self.reserved_slots)?;
        s.serialize_field("estimation_safety_margin_percent", &self.estimation_safety_margin_percent)?;
        s.end()
    }
}

#[cfg(feature = "serde")]
impl<'de> serde::Deserialize<'de> for ContextBudget {
    fn deserialize<D: serde::Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        #[derive(serde::Deserialize)]
        #[serde(deny_unknown_fields)]
        struct Raw {
            max_tokens: i64,
            target_tokens: i64,
            output_reserve: i64,
            #[serde(default)]
            reserved_slots: HashMap<ContextKind, i64>,
            estimation_safety_margin_percent: f64,
        }
        let raw = Raw::deserialize(deserializer)?;
        ContextBudget::new(
            raw.max_tokens,
            raw.target_tokens,
            raw.output_reserve,
            raw.reserved_slots,
            raw.estimation_safety_margin_percent,
        ).map_err(serde::de::Error::custom)
    }
}
```

**Critical detail:** The `Raw` struct's `reserved_slots` field is `HashMap<ContextKind, i64>`. For this to work, `ContextKind` must implement `Deserialize` BEFORE `ContextBudget`'s deserializer is compiled. Since `ContextKind` deserializes as a bare string, it naturally works as a HashMap key (serde serializes HashMap keys as strings in JSON).

**reserved_slots default:** Use `#[serde(default)]` so that omitting `reserved_slots` from JSON produces an empty HashMap (matching the common case where no reserved slots are needed).

**Applies to:** `ContextBudget`, `QuotaEntry` (same pattern, simpler -- only 3 fields).

**Confidence: HIGH** -- the "raw struct intermediary" pattern is documented on serde.rs and widely used in the ecosystem.

### Pattern 4: ContextItem Deserialization via Builder

ContextItem has many optional fields and validates via `ContextItemBuilder`. The deserializer should:
1. Deserialize into a raw struct with all optional fields defaulted
2. Route through `ContextItemBuilder::new(content, tokens).kind(...).source(...)...build()`

```rust
#[cfg(feature = "serde")]
impl<'de> serde::Deserialize<'de> for ContextItem {
    fn deserialize<D: serde::Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        #[derive(serde::Deserialize)]
        #[serde(deny_unknown_fields)]
        struct Raw {
            content: String,
            tokens: i64,
            #[serde(default)]
            kind: Option<ContextKind>,
            #[serde(default)]
            source: Option<ContextSource>,
            #[serde(default)]
            priority: Option<i64>,
            #[serde(default)]
            tags: Vec<String>,
            #[serde(default)]
            metadata: HashMap<String, String>,
            #[serde(default)]
            timestamp: Option<DateTime<Utc>>,
            #[serde(default)]
            future_relevance_hint: Option<f64>,
            #[serde(default)]
            pinned: bool,
            #[serde(default)]
            original_tokens: Option<i64>,
        }
        let raw = Raw::deserialize(deserializer)?;
        let mut builder = ContextItemBuilder::new(raw.content, raw.tokens);
        if let Some(kind) = raw.kind { builder = builder.kind(kind); }
        if let Some(source) = raw.source { builder = builder.source(source); }
        if let Some(priority) = raw.priority { builder = builder.priority(priority); }
        if !raw.tags.is_empty() { builder = builder.tags(raw.tags); }
        if !raw.metadata.is_empty() { builder = builder.metadata(raw.metadata); }
        if let Some(ts) = raw.timestamp { builder = builder.timestamp(ts); }
        if let Some(hint) = raw.future_relevance_hint { builder = builder.future_relevance_hint(hint); }
        if raw.pinned { builder = builder.pinned(true); }
        if let Some(ot) = raw.original_tokens { builder = builder.original_tokens(ot); }
        builder.build().map_err(serde::de::Error::custom)
    }
}
```

For serialization, since all fields are private with accessors, implement `Serialize` manually using `serialize_struct` (same approach as ContextBudget).

**Applies to:** `ContextItem` only.

**Confidence: HIGH** -- builder routing is the correct approach given the existing API surface.

### Pattern 5: Serialize for ContextItem (manual, field-by-field)

ContextItem has private fields with accessor methods. Manual `Serialize` impl:

```rust
#[cfg(feature = "serde")]
impl serde::Serialize for ContextItem {
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        use serde::ser::SerializeStruct;
        // Count non-None optional fields for efficiency, or just use max field count
        let mut s = serializer.serialize_struct("ContextItem", 11)?;
        s.serialize_field("content", &self.content)?;
        s.serialize_field("tokens", &self.tokens)?;
        s.serialize_field("kind", &self.kind)?;
        s.serialize_field("source", &self.source)?;
        s.serialize_field("priority", &self.priority)?;
        s.serialize_field("tags", &self.tags)?;
        s.serialize_field("metadata", &self.metadata)?;
        s.serialize_field("timestamp", &self.timestamp)?;
        s.serialize_field("future_relevance_hint", &self.future_relevance_hint)?;
        s.serialize_field("pinned", &self.pinned)?;
        s.serialize_field("original_tokens", &self.original_tokens)?;
        s.end()
    }
}
```

Note: Field access is directly on `self.content` etc. (not through accessors) since the impl is inside the module that owns the struct. This is important -- the serde impl code must live in the same module as the struct definition, or use `pub(crate)` accessors.

**Confidence: HIGH**

### Pattern 6: QuotaEntry Deserialization

Simple 3-field struct with validation:

```rust
#[cfg(feature = "serde")]
impl<'de> serde::Deserialize<'de> for QuotaEntry {
    fn deserialize<D: serde::Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        #[derive(serde::Deserialize)]
        #[serde(deny_unknown_fields)]
        struct Raw {
            kind: ContextKind,
            require: f64,
            cap: f64,
        }
        let raw = Raw::deserialize(deserializer)?;
        QuotaEntry::new(raw.kind, raw.require, raw.cap).map_err(serde::de::Error::custom)
    }
}
```

**Confidence: HIGH**

## Don't Hand-Roll

| Problem | Use Instead | Why |
|---|---|---|
| DateTime serialization | `chrono/serde` feature | chrono's built-in serde produces RFC 3339 strings, which is the standard |
| HashMap<ContextKind, i64> key serialization | serde's default + ContextKind's Serialize (as string) | serde automatically uses Serialize for HashMap keys in JSON when key is a string type |
| Feature-flag gating | `#[cfg(feature = "serde")]` and `#[cfg_attr(...)]` | Standard Cargo feature mechanism; no proc-macro tricks needed |
| Error conversion in deserializers | `serde::de::Error::custom` | Converts any Display type to serde errors; CupelError already has Display via thiserror |
| Enum variant name casing | serde defaults (PascalCase for externally-tagged enums) | OverflowStrategy variants are already PascalCase (`Throw`, `Truncate`, `Proceed`), which is serde's default for enum variants -- no `rename_all` needed |

## Discretionary Recommendations

### ScoredItem: INCLUDE
- Simple struct with two public fields (`item: ContextItem`, `score: f64`). No validation.
- With ContextItem already serializable, ScoredItem is trivial: `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]`.
- Useful for consumers who want to inspect/log scoring results.

### CupelError: EXCLUDE
- Error types are typically not serialized via serde in Rust. They implement `Display` and `Error`.
- Consumers who need error serialization use `.to_string()` or their own error mapping.
- Including it adds complexity (thiserror enums with data fields) for minimal value.

### ContextItemBuilder: EXCLUDE
- Builders are construction-time utilities, not data transfer objects.
- Consumers serialize the built `ContextItem`, not the builder state.

### Newtype Representation (ContextKind, ContextSource): BARE STRING
- Serialize as `"Message"` not `{"value": "Message"}`.
- This is the natural JSON representation and matches how consumers will construct JSON payloads.
- Case is preserved on serialization (write what was stored), validated on deserialization (reject empty/whitespace-only).

### OverflowStrategy Variant Casing: SERDE DEFAULT (PascalCase)
- Variants are `Throw`, `Truncate`, `Proceed` -- already PascalCase.
- Serde's default external tagging serializes as `"Throw"`, `"Truncate"`, `"Proceed"`.
- No `rename_all` attribute needed. This is clean and Rust-idiomatic.

### ContextBudget reserved_slots Key Serialization: PRESERVE CASE
- `ContextKind` serializes as its stored string value. A key `"Message"` stays `"Message"` in JSON.
- Case-insensitive equality is a runtime property of `ContextKind`, not a serialization concern.
- Normalizing to lowercase on serialization would lose information and surprise users.

### Error Messages on Deserialization Failure: USE CUPEL ERROR TEXT
- `serde::de::Error::custom(cupel_error)` automatically uses `CupelError`'s `Display` impl.
- This produces messages like `"maxTokens must be >= 0"` which are the same messages users see from the constructor.
- Consistent, no extra work needed.

### Unknown Field Handling: DENY
- Use `#[serde(deny_unknown_fields)]` on all raw intermediary structs.
- Catches typos in JSON keys early (e.g., `"maxTokens"` instead of `"max_tokens"`).
- This is the safer default for data types with validation invariants.

### Optional Field Defaults on ContextItem: MATCH BUILDER DEFAULTS
- `kind` defaults to `ContextKind::default()` ("Message") when absent.
- `source` defaults to `ContextSource::default()` ("Chat") when absent.
- `tags` defaults to empty vec, `metadata` to empty map.
- `pinned` defaults to `false`.
- All other Optional fields default to `None`.
- This matches `ContextItemBuilder::new()` defaults exactly, ensuring deserialization without optional fields produces the same result as building without setting them.

## Common Pitfalls

### 1. Feature Additivity Violation
**Risk:** Code that compiles without `--features serde` breaks, or vice versa.
**Mitigation:** Every `#[cfg(feature = "serde")]` block must be additive. Run `cargo check` (no features) AND `cargo check --features serde` in CI. Never put non-serde logic inside serde cfg blocks.
**Confidence: HIGH**

### 2. Serde Impl Placement (Private Field Access)
**Risk:** Putting serde impls in a separate `serde.rs` module that can't access private fields.
**Mitigation:** Place serde impls in the SAME file as the type definition, gated by `#[cfg(feature = "serde")]`. This gives access to private fields without needing `pub(crate)` accessor pollution.
**Confidence: HIGH**

### 3. HashMap<ContextKind, i64> Key Deserialization
**Risk:** serde's JSON format requires map keys to be strings. If `ContextKind` doesn't deserialize from a bare string, the HashMap won't parse.
**Mitigation:** ContextKind's `Deserialize` impl (Pattern 2) produces a `ContextKind` from a string, which is exactly what serde needs for map key deserialization. Ensure ContextKind's serde impl is defined BEFORE ContextBudget's (compilation order in same crate is by module, so this is natural since `context_kind.rs` is imported before `context_budget.rs`).
**Confidence: HIGH**

### 4. chrono DateTime Serialization Format
**Risk:** `chrono/serde` serializes `DateTime<Utc>` as RFC 3339 string (e.g., `"2024-01-01T00:00:00Z"`). If consumers expect epoch timestamps, there's a mismatch.
**Mitigation:** RFC 3339 is the standard for JSON datetime interchange. Document this in the crate. No custom format needed.
**Confidence: HIGH**

### 5. Optional DateTime in ContextItem
**Risk:** `Option<DateTime<Utc>>` with `chrono/serde` -- need to ensure `None` serializes as JSON `null` and absent fields deserialize as `None`.
**Mitigation:** serde handles `Option<T>` natively. With `#[serde(default)]` on the raw struct field, missing key -> `None`. Present `null` -> `None`. Present string -> parsed DateTime. This works out of the box with chrono's serde.
**Confidence: HIGH**

### 6. Deserialization Bypassing Validation
**Risk:** If a `#[derive(Deserialize)]` is accidentally placed on the main struct instead of a raw intermediary, deserialization bypasses the constructor.
**Mitigation:** NEVER derive `Deserialize` on `ContextBudget`, `ContextItem`, `ContextKind`, `ContextSource`, or `QuotaEntry` directly. Only derive on private `Raw` structs inside the `Deserialize` impl. The `Serialize` derive can also be avoided in favor of manual impl for consistency, but the key constraint is on `Deserialize`.
**Confidence: HIGH**

### 7. ScoredItem Contains ContextItem
**Risk:** If ScoredItem derives `Serialize`/`Deserialize`, it needs ContextItem to also implement them. Since ContextItem has manual impls, this works -- but compilation order matters.
**Mitigation:** ScoredItem's derive will use ContextItem's manual serde impls. Since ScoredItem is in `scored_item.rs` and ContextItem's impls are in `context_item.rs`, both within the same crate, this resolves correctly. Test roundtrip specifically.
**Confidence: HIGH**

### 8. Re-publish Version Bump
**Risk:** Publishing a new feature as a patch version, violating semver.
**Mitigation:** Bump to `1.1.0` (minor version). New features are minor bumps. The feature is purely additive -- no breaking changes to existing API.
**Confidence: HIGH**

## Code Examples

### Cargo.toml Feature Section (Complete)

```toml
[features]
default = []
serde = ["dep:serde", "chrono/serde"]

[dependencies]
chrono = "0.4"
serde = { version = "1", features = ["derive"], optional = true }
thiserror = "2"

[dev-dependencies]
toml = "0.8"
serde = { version = "1", features = ["derive"] }
serde_json = "1"
```

Note: `serde` appears in both `[dependencies]` (optional) and `[dev-dependencies]` (always). Cargo unifies these correctly -- dev-dependencies always activate during `cargo test`, so tests can always use serde. The optional dependency gates it for consumers.

### Serde Impl Module Pattern (in each source file)

```rust
// At the bottom of context_kind.rs:

#[cfg(feature = "serde")]
mod serde_impl {
    use super::ContextKind;

    impl serde::Serialize for ContextKind {
        fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
            serializer.serialize_str(self.as_str())
        }
    }

    impl<'de> serde::Deserialize<'de> for ContextKind {
        fn deserialize<D: serde::Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
            let s = String::deserialize(deserializer)?;
            ContextKind::new(s).map_err(serde::de::Error::custom)
        }
    }
}
```

Alternative (simpler, no inner module):
```rust
#[cfg(feature = "serde")]
impl serde::Serialize for ContextKind { ... }

#[cfg(feature = "serde")]
impl<'de> serde::Deserialize<'de> for ContextKind { ... }
```

Both work. The inner module approach groups serde code visually but adds nesting. Recommend the flat `#[cfg(feature = "serde")]` on each impl block for simplicity.

### Roundtrip Test Pattern

```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn context_budget_roundtrip() {
        let budget = ContextBudget::new(8000, 6000, 1000, HashMap::new(), 5.0).unwrap();
        let json = serde_json::to_string(&budget).unwrap();
        let deserialized: ContextBudget = serde_json::from_str(&json).unwrap();
        assert_eq!(deserialized.max_tokens(), budget.max_tokens());
        assert_eq!(deserialized.target_tokens(), budget.target_tokens());
        // ... etc
    }

    #[test]
    fn context_budget_rejects_invalid_on_deserialize() {
        let json = r#"{"max_tokens": -1, "target_tokens": 0, "output_reserve": 0, "estimation_safety_margin_percent": 0.0}"#;
        let result: Result<ContextBudget, _> = serde_json::from_str(json);
        assert!(result.is_err());
        assert!(result.unwrap_err().to_string().contains("maxTokens must be >= 0"));
    }
}
```

### Test Strategy

Tests should verify:
1. **Roundtrip** -- serialize then deserialize produces equivalent values (for each type)
2. **Validation on deserialize** -- invalid inputs rejected with correct error messages (for each validating type)
3. **Default handling** -- omitted optional fields get builder defaults (for ContextItem)
4. **Unknown fields rejected** -- extra JSON keys cause errors (for types with deny_unknown_fields)
5. **Feature additivity** -- `cargo test` (no features) and `cargo test --features serde` both pass

## Implementation Order

The types have a dependency ordering that must be respected:

1. **ContextKind** and **ContextSource** (no dependencies on other cupel types)
2. **OverflowStrategy** (no dependencies, simple derive)
3. **ContextItem** (depends on ContextKind, ContextSource, chrono DateTime)
4. **ScoredItem** (depends on ContextItem -- simple derive since fields are public)
5. **ContextBudget** (depends on ContextKind for HashMap key)
6. **QuotaEntry** (depends on ContextKind)

This ordering ensures each type's serde impl is available when downstream types need it.

## Type-by-Type Summary

| Type | Serialize | Deserialize | Pattern | Notes |
|---|---|---|---|---|
| OverflowStrategy | derive | derive | cfg_attr derive | No validation, simple enum |
| ContextKind | manual (as string) | manual (via ::new) | Newtype string | Rejects empty/whitespace |
| ContextSource | manual (as string) | manual (via ::new) | Newtype string | Rejects empty/whitespace |
| ContextItem | manual (SerializeStruct) | manual (Raw + Builder) | Raw intermediary | 11 fields, optional defaults |
| ScoredItem | derive | derive | cfg_attr derive | Public fields, depends on ContextItem |
| ContextBudget | manual (SerializeStruct) | manual (Raw + ::new) | Raw intermediary | Cross-field validation |
| QuotaEntry | manual (SerializeStruct) | manual (Raw + ::new) | Raw intermediary | 3-field validation |

---

*Phase: 20-serde-feature-flag*
*Research completed: 2026-03-15*
