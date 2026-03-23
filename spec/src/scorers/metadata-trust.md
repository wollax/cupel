# MetadataTrustScorer

The MetadataTrustScorer passes through a caller-provided trust value stored in `metadata["cupel:trust"]` as the score, clamped to [0.0, 1.0].

## Overview

MetadataTrustScorer is an **absolute scorer** — it examines only the item's `metadata` map. The `allItems` parameter is ignored. This scorer enables callers to inject a trust signal into the scoring pipeline by setting a well-known metadata key at item construction time.

### Fields Used

| Field | Source | Purpose |
|---|---|---|
| `metadata` | [ContextItem](../data-model/context-item.md) | Key-value map; the scorer reads the `"cupel:trust"` key |

## Metadata Namespace Reservation

All `ContextItem.metadata` keys with the `"cupel:"` prefix are reserved for library-defined conventions. Callers MUST NOT use this prefix for application-specific keys. This reservation is enforced at the spec level only — there is no runtime validation of key prefixes.

## Conventions

### `cupel:trust`

`cupel:trust` is a caller-computed trust value for the item, represented as a float64 in [0.0, 1.0].

- **Storage format:** In Rust, `metadata` is `HashMap<String, String>` — the trust value MUST be stored as a decimal string (e.g., `"0.85"`). The scorer parses this string to a float64 at scoring time.
- **In .NET**, `metadata` is `IReadOnlyDictionary<string, object?>`, which supports heterogeneous value types. Callers MAY store the trust value as a `double` directly. Implementations MUST handle both `string` and `double` (or boxed numeric) values in the dictionary lookup. The canonical wire format for serialization interop is the decimal string representation.
- The value is caller-computed. The pipeline does not set or modify this key.
- The scorer reads `cupel:trust` as a scoring input only. It does not use the value as a filter or gate — items are never excluded based on this score.

### `cupel:source-type`

`cupel:source-type` is an open string convention for labeling the origin of a context item. The following values are RECOMMENDED:

| Value | Meaning |
|---|---|
| `"user"` | An end-user message |
| `"tool"` | A tool or function call result |
| `"external"` | Retrieved or injected context (e.g., RAG results) |
| `"system"` | A system prompt fragment |

Callers MAY use other values. Values are not validated at construction time. This convention is a labeling aid for caller logic and is not used by any scorer in the library.

## Algorithm

```text
METADATA-TRUST-SCORE(item, allItems, config):
    if item.metadata does not contain "cupel:trust":
        return config.defaultScore

    raw <- item.metadata["cupel:trust"]   // string in Rust; string or double in .NET

    value <- parse_float64(raw)           // or cast if already float64

    if parse failed:
        return config.defaultScore

    if value is not finite:               // NaN, +infinity, -infinity
        return config.defaultScore

    return clamp(value, 0.0, 1.0)
```

Where `clamp(x, lo, hi)` returns `lo` if `x < lo`, `hi` if `x > hi`, and `x` otherwise. `config.defaultScore` is a float64 in [0.0, 1.0] set at construction time.

## Score Interpretation

- The score is a direct passthrough of the caller-provided trust value, clamped to the conventional [0.0, 1.0] range.
- A score of 0.0 reflects that the caller considers the item untrustworthy or low-trust.
- A score of 1.0 reflects that the caller considers the item fully trusted.
- Values outside [0.0, 1.0] are clamped, not rejected.
- When the key is absent or the value is unparseable, `config.defaultScore` is returned. This default is set at scorer construction time and MUST be in [0.0, 1.0].

## Edge Cases

| Condition | Result |
|---|---|
| `cupel:trust` key absent | `config.defaultScore` |
| `cupel:trust` value is unparseable (e.g., `"high"`, `""`) | `config.defaultScore` |
| `cupel:trust` value is `"NaN"` | `config.defaultScore` |
| `cupel:trust` value is `"+Infinity"` or `"-Infinity"` | `config.defaultScore` |
| `cupel:trust` value is `"0.0"` | 0.0 |
| `cupel:trust` value is `"0.75"` | 0.75 |
| `cupel:trust` value is `"1.0"` | 1.0 |
| `cupel:trust` value is `"-0.1"` (below range) | 0.0 (clamped) |
| `cupel:trust` value is `"1.5"` (above range) | 1.0 (clamped) |

## Conformance Vector Outlines

The following scenarios outline the expected behavior for conformance testing. Full TOML conformance vectors require a `metadata` field extension to the test vector format (out of scope for this chapter); these outlines are narrative descriptions of the required behavior.

1. **Present and valid:** An item with `cupel:trust = "0.85"` — the scorer returns `0.85`.

2. **Key absent:** An item whose metadata map does not contain the `cupel:trust` key — the scorer returns `config.defaultScore` (e.g., `0.5` if that is the configured default).

3. **Unparseable value:** An item with `cupel:trust = "high"` (not a valid decimal float) — the scorer returns `config.defaultScore`, not an error.

4. **Out-of-range value (clamped high):** An item with `cupel:trust = "1.5"` — the scorer returns `1.0` (clamped to the upper bound).

5. **Non-finite value:** An item with `cupel:trust = "NaN"` or `cupel:trust = "Infinity"` — the scorer returns `config.defaultScore`, not a clamped boundary value.

## Complexity

- **Time:** O(1) per item (hash map lookup and float parse).
- **Space:** O(1) auxiliary.

## Conformance Notes

- The clamping order is: key-missing → `config.defaultScore`; parse-failure → `config.defaultScore`; non-finite → `config.defaultScore`; then clamp to [0.0, 1.0].
- Non-finite values (NaN, positive infinity, negative infinity) MUST return `config.defaultScore`, not be clamped to a range boundary.
- The finiteness check MUST occur before the clamp. Non-finite values MUST NOT pass through `clamp` (which may have implementation-defined behavior for NaN in some languages).
- In Rust, the metadata value at key `"cupel:trust"` MUST be parsed as a decimal string using standard float64 parsing (e.g., `str::parse::<f64>()`). A value that fails to parse MUST be treated as missing.
- In .NET, implementations MUST handle both `string` and `double` (or other boxed numeric type) when reading the value from `IReadOnlyDictionary<string, object?>`. If the stored value is already a `double`, it MUST be used directly without string conversion. If it is a `string`, it MUST be parsed. If it is any other type, it MUST be treated as a parse failure and return `config.defaultScore`.
- `config.defaultScore` MUST be in [0.0, 1.0]. Implementations SHOULD reject construction of a MetadataTrustScorer with a `defaultScore` outside this range.
