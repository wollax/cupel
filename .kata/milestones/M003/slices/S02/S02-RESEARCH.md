# S02: MetadataTrustScorer — Research

**Date:** 2026-03-23

## Summary

MetadataTrustScorer is a simple absolute scorer: it reads `metadata["cupel:trust"]`, parses it as float64, handles key-absence / parse-failure / non-finite by falling back to `defaultScore`, and clamps to [0.0, 1.0]. The algorithm has zero design unknowns — the spec chapter is locked with a complete pseudocode listing and 5 conformance vector outlines.

The primary implementation risk is the **TOML conformance vector format**: the existing `build_items` function in `conformance.rs` does not parse `metadata` from TOML items. It currently handles `content`, `tokens`, `kind`, `timestamp`, `priority`, `tags`, `futureRelevanceHint`, and `pinned` — but not `metadata`. This means the conformance harness needs a small extension before the 5 TOML vectors can be run. This is straightforward (5-10 lines) but easy to miss if planning assumes the harness already handles metadata.

The .NET side has an additional complication: `metadata` is `IReadOnlyDictionary<string, object?>` and callers may store `cupel:trust` as a native `double` or as a `string`. The scorer must handle both (D059). The `ReflexiveScorer` (FutureRelevanceHint passthrough with clamp) is the closest analog pattern in both languages.

## Recommendation

Follow the `ReflexiveScorer` pattern for the core logic. Follow the `DecayScorer` pattern for construction validation (throw/return `CupelError::ScorerConfig` naming the parameter). Add metadata parsing to `build_items` in `conformance.rs` as part of T01 (Rust implementation), before authoring the TOML vectors. The 5 spec-outlined conformance scenarios map cleanly to TOML vectors with a `[config]` block holding `default_score` and `[[items]]` entries with a `metadata.cupel:trust` field.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Float clamping | `f64::clamp(v, 0.0, 1.0)` (Rust) / `Math.Clamp(v, 0.0, 1.0)` (.NET) | Already used in ReflexiveScorer; no need for manual min/max |
| Non-finite check | `f64::is_finite()` / `double.IsFinite()` | Used in ReflexiveScorer and TagScorer; handles NaN and ±Infinity in one call |
| String → float parse | `str::parse::<f64>()` (Rust) / `double.TryParse()` (.NET) | Standard parsing that correctly handles NaN / Infinity strings and returns Err/false on failure |
| Conformance harness | existing `build_scorer_by_type` in `conformance.rs` | Add a `"metadata_trust"` arm following the `"decay"` arm pattern |

## Existing Code and Patterns

- `crates/cupel/src/scorer/decay.rs` — Primary template: typed construction with `CupelError::ScorerConfig` naming offending parameter; `Box<dyn TimeProvider>` becomes `f64 default_score`; `impl Scorer` with simple field dispatch. MetadataTrustScorer is simpler (no stored provider, no curve).
- `crates/cupel/src/scorer/reflexive.rs` (inferred from tests) — Closest algorithmic analog: absolute scorer that reads a single item field, handles null, handles non-finite, clamps to [0.0, 1.0]. Inspect for exact `is_finite` + `clamp` pattern.
- `crates/cupel/tests/conformance.rs` — `build_items` needs a `metadata` parsing block added (currently only parses content/tokens/kind/timestamp/priority/tags/futureRelevanceHint/pinned). The `FixedTimeProvider`/`"decay"` arm shows how to add a new scorer type to `build_scorer_by_type`.
- `src/Wollax.Cupel/Scoring/ReflexiveScorer.cs` — .NET template: single-method scorer, `Math.Clamp`, `double.IsFinite`. MetadataTrustScorer adds: constructor with `defaultScore` validation, metadata key lookup with dual string/double handling (D059).
- `crates/cupel/src/model/context_item.rs` — `metadata()` returns `&HashMap<String, String>`. Trust value is always a string in Rust; parse with `str::parse::<f64>()`.
- `src/Wollax.Cupel/ContextItem.cs` — `Metadata` is `IReadOnlyDictionary<string, object?>`. Callers may store trust as `double` or `string`; must handle both (D059). Other numeric types → treat as parse failure → `defaultScore`.

## Constraints

- Rust `metadata` is `HashMap<String, String>` — trust is always a string; use `str::parse::<f64>()` (returns `Err` for "high", "", "NaN", "Infinity" — wait: `"NaN".parse::<f64>()` actually succeeds in Rust returning `f64::NAN`; the `is_finite()` check must follow the parse to catch NaN and infinities).
- .NET `metadata` is `IReadOnlyDictionary<string, object?>` — handle `double`, `string`, and "any other type → defaultScore" (D059). Use `double.TryParse(s, ...)` for string case; check `IsFinite` on the resulting double.
- `defaultScore` MUST be in [0.0, 1.0]; reject at construction with `CupelError::ScorerConfig` (Rust) / `ArgumentOutOfRangeException` (. NET) naming the parameter.
- The spec outlines 5 conformance vector "narratives" but states the full TOML format requires a `metadata` field extension to the test vector format. The extension itself is straightforward — add `[items.metadata]` or `metadata.cupel:trust` inline to `[[items]]` blocks — but the conformance harness `build_items` must be extended to read and pass metadata to `ContextItemBuilder`.
- No `metadata_trust` arm exists yet in `build_scorer_by_type`; it must be added alongside the new scorer.
- `cupel:source-type` convention is NOT used by MetadataTrustScorer — it is a labeling convention only. Do not score based on it. The spec explicitly states: "This convention is a labeling aid for caller logic and is not used by any scorer in the library."

## Common Pitfalls

- **`"NaN".parse::<f64>()` succeeds in Rust** — `str::parse::<f64>()` on `"NaN"`, `"inf"`, `"-inf"` returns `Ok(f64::NAN)`, `Ok(f64::INFINITY)`, `Ok(f64::NEG_INFINITY)`. The `is_finite()` check MUST come after the parse, not be skipped because parse succeeded. If you check parse success alone, NaN will pass through and produce unexpected behavior (NaN clamps unpredictably).
- **Forgetting `build_items` metadata extension** — The conformance harness does not currently parse `metadata` from TOML items. If you write the TOML vectors first and run the test before extending `build_items`, items will all have empty metadata and tests will fail with unexpected `defaultScore` results, not a clear error. Extend `build_items` before running the vectors.
- **.NET `object?` type dispatch order** — When the metadata value is `object?`, check `double` (or `decimal` if desired) before `string`; do not use `is string s && double.TryParse(s)` only — callers who pass `0.85` as a native `double` would get `defaultScore` instead of `0.85`. The spec order is: if already a double → use directly; if string → parse; else → defaultScore.
- **`defaultScore` constructor naming** — The parameter in the spec pseudocode is `config.defaultScore`; in code use `defaultScore` (camelCase) matching the parameter name convention; the `CupelError::ScorerConfig` message should name the offending parameter (`"defaultScore must be in [0.0, 1.0]"`).
- **PublicAPI.Unshipped.txt** — Adding `MetadataTrustScorer` (a new public sealed class) to `Wollax.Cupel` requires adding 2-3 entries to `PublicAPI.Unshipped.txt` (the class itself, its constructor, and its `Score` method). Missing this blocks the build with an RS0016 error. Run `dotnet build` immediately after adding the class to catch this.

## Open Risks

- TOML conformance vector metadata format: `[[items]]` TOML blocks support inline tables (`metadata = { "cupel:trust" = "0.85" }`) — verify the TOML parser in `build_items` can read an inline table and extract the key-value pairs as `HashMap<String, String>` before committing to this format. The existing vector format uses top-level keys on items, not nested tables; a `metadata` inline table is a safe extension.
- .NET `double.TryParse` culture sensitivity: Use `CultureInfo.InvariantCulture` and `NumberStyles.Float` to ensure `"0.85"` always parses correctly regardless of OS locale settings.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust (scorer pattern) | n/a — established in S01 | installed (pattern established) |

No external skills required. The implementation is pure algorithm + existing codebase patterns.

## Sources

- `spec/src/scorers/metadata-trust.md` — Locked spec chapter; algorithm pseudocode, edge case table, 5 conformance vector outlines, D059 (.NET dual-type handling), D060 (open string convention)
- `crates/cupel/src/scorer/decay.rs` — Construction/validation pattern template (Rust)
- `src/Wollax.Cupel/Scoring/ReflexiveScorer.cs` — Algorithmic pattern template (.NET)
- `crates/cupel/tests/conformance.rs` — Conformance harness: `build_items`, `build_scorer_by_type` (confirmed: no metadata parsing currently)
- `.kata/DECISIONS.md` — D059 (.NET dual-type handling), D060 (open string, not validated at construction)
