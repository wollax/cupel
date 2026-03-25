---
estimated_steps: 6
estimated_files: 4
---

# T02: Write metadata-key scorer spec chapter and finish wiring

**Slice:** S03 — Spec chapters — count-constrained-knapsack + metadata-key
**Milestone:** M009

## Description

Author `spec/src/scorers/metadata-key.md` using `metadata-trust.md` as the structural template. The chapter documents `MetadataKeyScorer` — a multiplicative scorer that returns `boost` for matching items and `1.0` (neutral multiplier) for non-matching. Introduces the `cupel:priority` convention. This file is the implementation contract for S04. Complete remaining wiring: add SUMMARY.md link, scorers.md table row, and the .NET CountConstrainedKnapsackSlice CHANGELOG entry that S02 deferred.

## Steps

1. Read `spec/src/scorers/metadata-trust.md` to internalize the section structure and pseudocode style. Note what to NOT copy: `defaultScore` parameter semantics, clamp behavior, and the out-of-range construction guard.
2. Create `spec/src/scorers/metadata-key.md` with these sections in order:
   - **Overview** — absolute scorer (allItems ignored); multiplicative semantics: returns `boost` for matching items, `defaultMultiplier` (1.0) for non-matching; contrast with MetadataTrustScorer (absolute passthrough vs multiplicative boost); composable with CompositeScorer.
   - **Metadata Namespace Reservation** — same one-line note: `cupel:` prefix is reserved (copy from metadata-trust.md).
   - **Conventions** — two sub-sections: (a) `cupel:priority`: caller-provided priority signal; key `"cupel:priority"`, RECOMMENDED values `"high"`, `"normal"` (or `"low"`); open string — implementations MUST NOT validate or reject unknown values; typical usage: `MetadataKeyScorer("cupel:priority", "high", 1.5)` boosts high-priority items 1.5×; (b) cross-reference `cupel:trust` and `cupel:source-type` conventions in metadata-trust.md as the other reserved conventions.
   - **Configuration** — constructor parameters table: `key` (string, the metadata key to match), `value` (string, the exact value to match), `boost` (float64 > 0.0, the multiplier returned for matching items). Validation: `boost` MUST be > 0.0; zero, negative, and non-finite values MUST be rejected at construction with `CupelError::ScorerConfig` (Rust) / `ArgumentException` (not `ArgumentOutOfRangeException` — D178) (.NET).
   - **Algorithm** — METADATA-KEY-SCORE pseudocode:
     ```
     METADATA-KEY-SCORE(item, allItems, config):
         if item.metadata does not contain config.key:
             return config.defaultMultiplier  // 1.0

         raw <- item.metadata[config.key]
         if raw != config.value:
             return config.defaultMultiplier  // 1.0

         return config.boost
     ```
     Note: `defaultMultiplier` is a fixed constant of 1.0, not a constructor parameter. The scorer does NOT receive `item.score` as input — it returns a multiplier value used by the downstream scoring stage.
   - **Score Interpretation** — explain multiplicative semantics: the scorer returns `boost` (e.g., 1.5) or `1.0`; when used in a `CompositeScorer`, the composite multiplies scores, so a `MetadataKeyScorer` with `boost=1.5` effectively 1.5× the composite weight for matching items; returned values are NOT clamped.
   - **Edge Cases** — table: key absent → 1.0; key present, value matches → config.boost; key present, value does not match → 1.0; boost = 0.0 at construction → error; boost < 0.0 at construction → error; boost is NaN or Infinity at construction → error.
   - **Conformance Vector Outlines** — 5 narrative outlines: (1) match → boost applied: item with `metadata["cupel:priority"] = "high"`, scorer with boost=1.5 → returns 1.5; (2) no-match → neutral: item with `metadata["cupel:priority"] = "normal"`, scorer checking for "high" → returns 1.0; (3) missing key → neutral: item with no `cupel:priority` key → returns 1.0; (4) zero boost → construction error; (5) negative boost → construction error.
   - **Complexity** — O(1) per item (hash map lookup, string comparison). O(1) auxiliary.
   - **Conformance Notes** — value comparison is string-to-string; metadata stored as non-string in .NET SHOULD be converted to string for comparison; `defaultMultiplier` is always 1.0 and is NOT a constructor parameter; boost validation at construction time (not scoring time).
3. Add `  - [MetadataKeyScorer](scorers/metadata-key.md)` to `spec/src/SUMMARY.md` under the Scorers section, after the `DecayScorer` entry.
4. Add a row for MetadataKeyScorer to the Scorer Summary table in `spec/src/scorers.md`: `| [MetadataKeyScorer](scorers/metadata-key.md) | \`metadata[key]\` | {1.0, boost} | Multiplicative boost for items where metadata key matches a configured value |`. Also add MetadataKeyScorer to the Absolute Scorers list in the Scorer Categories section.
5. Add `.NET CountConstrainedKnapsackSlice` entry to `CHANGELOG.md` unreleased section. The Rust entry from S01 reads: "`Rust: \`CountConstrainedKnapsackSlice\` — 3-phase slicer...`". Add a companion `.NET` line referencing the same feature: `.NET: \`CountConstrainedKnapsackSlice\` — .NET port of the Rust implementation; Phase 1 count-satisfy, Phase 2 KnapsackSlice delegate with score-descending re-sort, Phase 3 cap-enforcement seeded from Phase 1 counts; \`IQuotaPolicy\` implementation; \`ScarcityBehavior.Degrade\` default; pipeline-level shortfall injection and cap-classification via \`CupelPipeline\``
6. Run full regression check: `cargo test --all-targets` and `dotnet test --solution Cupel.slnx`. Both must exit 0.

## Must-Haves

- [ ] `spec/src/scorers/metadata-key.md` exists with all required sections (Overview, Metadata Namespace Reservation, Conventions, Configuration, Algorithm, Score Interpretation, Edge Cases, Conformance Vector Outlines, Complexity, Conformance Notes)
- [ ] Algorithm pseudocode returns `config.defaultMultiplier` (1.0 constant) for non-matching, `config.boost` for matching — NOT a clamped trust value, NOT a defaultScore parameter
- [ ] `cupel:priority` convention section documents key `"cupel:priority"`, recommended values, open-string semantics (implementations MUST NOT reject unknown values)
- [ ] Construction validation documented: `boost > 0.0`; zero/negative/non-finite → `CupelError::ScorerConfig` (Rust) / `ArgumentException` (NOT `ArgumentOutOfRangeException`) (.NET) — D178
- [ ] 5 conformance vector outlines present (match, no-match, missing-key, zero-boost error, negative-boost error)
- [ ] `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md` → 0
- [ ] `grep -q "metadata-key" spec/src/SUMMARY.md` exits 0
- [ ] `grep -q "MetadataKeyScorer" spec/src/scorers.md` exits 0
- [ ] CHANGELOG.md has .NET CountConstrainedKnapsackSlice entry
- [ ] `cargo test --all-targets` exits 0
- [ ] `dotnet test --solution Cupel.slnx` exits 0

## Verification

- `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md` → 0
- `grep -q "metadata-key" spec/src/SUMMARY.md` → exits 0
- `grep -q "MetadataKeyScorer" spec/src/scorers.md` → exits 0
- `grep -q "defaultMultiplier\|default.*multiplier\|1\.0" spec/src/scorers/metadata-key.md` → exits 0 (neutral multiplier present)
- `grep -q "cupel:priority" spec/src/scorers/metadata-key.md` → exits 0
- `grep -qi "ArgumentException\|ScorerConfig" spec/src/scorers/metadata-key.md` → exits 0 (construction error type)
- `grep -q "\.NET" CHANGELOG.md` (CountConstrainedKnapsackSlice .NET entry present)
- `cargo test --all-targets` exits 0
- `dotnet test --solution Cupel.slnx` exits 0

## Observability Impact

- Signals added/changed: None — spec-only changes; regression tests are the signal
- How a future agent inspects this: `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md`; regression test exit codes
- Failure state exposed: TBD-count > 0 → spec incomplete (blocks S04); non-zero test exit → regression introduced by spec edits

## Inputs

- `spec/src/scorers/metadata-trust.md` — structural template; use same section names and tone; do NOT copy `defaultScore` parameter, clamping behavior, or `[0.0, 1.0]` range semantics
- S03-RESEARCH.md — confirmed algorithm pseudocode, `cupel:priority` convention details, construction error types (D178), and the 5 conformance vector narratives
- `CHANGELOG.md` — existing Rust entry for CountConstrainedKnapsackSlice (added in S01) to use as the companion .NET entry format
- `spec/src/SUMMARY.md` — current structure to locate the Scorers section for link insertion
- `spec/src/scorers.md` — Scorer Summary table for new row; Absolute Scorers category list

## Expected Output

- `spec/src/scorers/metadata-key.md` — complete spec chapter serving as S04 implementation contract; zero TBD fields
- `spec/src/SUMMARY.md` — updated with MetadataKeyScorer link under Scorers
- `spec/src/scorers.md` — updated summary table and Absolute Scorers category list
- `CHANGELOG.md` — .NET CountConstrainedKnapsackSlice entry added to unreleased section
