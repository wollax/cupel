# Phase 11: Language-Agnostic Specification - Research

**Researched:** 2026-03-14
**Confidence:** HIGH (based on codebase analysis + established tooling documentation)

---

## Standard Stack

### Publication Tooling: mdBook
**Confidence: HIGH**

Use **mdBook** (Rust-based, maintained by rust-lang) for spec authoring and GitHub Pages publication.

- **Why mdBook over alternatives:**
  - GitHub has an official starter workflow for mdBook deployment (`actions/starter-workflows/pages/mdbook.yml`)
  - Native Markdown authoring (no transpilation step from a custom format)
  - Built-in search, ToC sidebar, print page, dark/light themes
  - Preprocessor ecosystem for custom processing (mermaid diagrams, etc.)
  - Used by Rust Reference, Cargo Book, and many language-spec-grade documents
  - Single static binary, no Node.js/Ruby/Python dependency chain

- **Rejected alternatives:**
  - **Docusaurus/Hugo/Jekyll**: Heavier dependency chain, designed for docs sites not specs
  - **Typst**: Beautiful typesetting but no built-in web rendering; adds a compilation step
  - **Raw GitHub wiki/README**: No structured navigation, no versioned rendering

### Directory Structure
**Confidence: HIGH**

Place the spec inside this repo at `spec/` to keep spec and reference implementation co-located.

```
spec/
  book.toml              # mdBook configuration
  src/
    SUMMARY.md           # Chapter navigation
    introduction.md      # Overview and scope
    data-model.md        # ContextItem, ContextBudget, enums
    pipeline.md          # Pipeline overview (6 stages)
    scoring.md           # All scorer algorithms
    slicing.md           # All slicer algorithms
    placing.md           # All placer algorithms
    conformance.md       # Conformance levels and test format
    changelog.md         # Spec version history
  theme/                 # Optional custom CSS
conformance/
  README.md              # How to run conformance tests
  required/              # Required test vectors (TOML)
    scoring/
    slicing/
    placing/
    pipeline/
  optional/              # Optional edge-case vectors (TOML)
    scoring/
    slicing/
    placing/
    pipeline/
```

### Test Vector Format: TOML
**Confidence: HIGH** (locked decision from CONTEXT.md)

TOML for test fixtures. Each `.toml` file is one test case with input, configuration, and expected output.

### GitHub Pages Deployment
**Confidence: HIGH**

Use GitHub's official mdBook starter workflow with `actions/deploy-pages@v4`.

```yaml
# .github/workflows/spec.yml
name: Deploy Spec to Pages
on:
  push:
    branches: [main]
    paths: ['spec/**', 'conformance/**']
  workflow_dispatch:
permissions:
  contents: read
  pages: write
  id-token: write
concurrency:
  group: "pages"
  cancel-in-progress: false
jobs:
  build:
    runs-on: ubuntu-latest
    env:
      MDBOOK_VERSION: 0.4.43
    steps:
      - uses: actions/checkout@v4
      - name: Install mdBook
        run: |
          curl --proto '=https' --tlsv1.2 https://sh.rustup.rs -sSf -y | sh
          rustup update
          cargo install --version ${MDBOOK_VERSION} mdbook
      - name: Setup Pages
        uses: actions/configure-pages@v5
      - name: Build with mdBook
        run: mdbook build spec
      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: ./spec/book
  deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    needs: build
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

---

## Architecture Patterns

### Spec Document Structure Pattern
**Confidence: HIGH**

Follow the dual-audience pattern from CONTEXT.md: each chapter opens with a conceptual overview (for readers) followed by precise algorithmic specification (for implementors).

Per-chapter structure:
1. **Overview** - 2-3 paragraph conceptual explanation, motivation, invariants
2. **Definitions** - Formal terms, types, constraints
3. **Algorithm** - Pseudocode with prose annotations
4. **Edge Cases** - Explicitly listed behaviors for boundary conditions
5. **Conformance Notes** - Which test vectors exercise this section

### Pseudocode Notation Convention
**Confidence: HIGH**

Use CLRS-style pseudocode (indentation-based, keyword-driven) with these conventions:
- Keywords in **bold lowercase**: **for**, **if**, **return**, **while**
- Variables in `monospace italic` or plain lowercase
- Assignment via `<-` (not `=`, to avoid confusion with equality)
- Array indexing 0-based (matching all target languages)
- Comments via `//`
- Types annotated in prose before the algorithm, not inline

Example style:
```
GREEDY-SLICE(scored_items, target_tokens):
  densities <- empty array of (density, index) pairs
  for i <- 0 to length(scored_items) - 1:
    tokens <- scored_items[i].item.tokens
    if tokens = 0:
      densities[i] <- (MAX_FLOAT, i)
    else:
      densities[i] <- (scored_items[i].score / tokens, i)
  STABLE-SORT-DESCENDING(densities, by density)
  remaining <- target_tokens
  selected <- empty list
  for i <- 0 to length(densities) - 1:
    item <- scored_items[densities[i].index].item
    if item.tokens = 0:
      APPEND(selected, item)
    else if item.tokens <= remaining:
      APPEND(selected, item)
      remaining <- remaining - item.tokens
  return selected
```

### Algorithm Precision: Behavioral Equivalence
**Confidence: HIGH** (locked decision)

- Spec defines which items are selected and in what order
- Floating-point scores may differ within epsilon (IEEE 754 64-bit doubles mandated)
- Conformance tests compare selected item sets and ordering, not exact score values
- Score comparison in test vectors uses `abs(actual - expected) < 1e-9` tolerance

### Tiebreaking: Prescribed via Stable Sort
**Confidence: HIGH** (recommendation)

Prescribe tiebreaking rules rather than leaving implementation-defined. The C# implementation already uses stable sort with `(Score, Index)` tuples everywhere. This is simple to specify and removes ambiguity:

- **Sort tiebreaker**: When scores are equal, preserve original input order (stable sort by ascending index)
- **Deduplication tiebreaker**: When duplicate items have equal scores, keep the one with the lower original index

This avoids conformance test non-determinism.

---

## Algorithms to Document (Extracted from C# Implementation)

### Pipeline Stages (6 stages, fixed order)

#### 1. CLASSIFY
- Partition items into `pinned` and `scoreable` lists
- Skip items with `tokens < 0` (excluded, reason: NegativeTokens)
- Validate pinned items fit within `maxTokens - outputReserve`

#### 2. SCORE
- For each scoreable item: `score <- scorer.Score(item, all_scoreable_items)`
- Produces `ScoredItem(item, score)` pairs
- Score is IEEE 754 double, conventionally [0.0, 1.0]

#### 3. DEDUPLICATE (optional, enabled by default)
- Group by `item.content` (exact string match, ordinal comparison)
- For duplicates: keep the one with highest score
- Tiebreak: keep the one with lowest original index

#### 4. SORT
- Stable sort descending by score
- Tiebreak: ascending by original index (preserves input order for equal scores)
- Implementation: build `(score, index)` pairs, sort, reconstruct

#### 5. SLICE
- Compute effective budget: `effectiveMax = max(0, maxTokens - outputReserve - pinnedTokens)`, `effectiveTarget = min(max(0, targetTokens - pinnedTokens), effectiveMax)`
- Delegate to slicer strategy with adjusted budget
- Re-associate slicer output with original scores

#### 6. PLACE
- Merge pinned items (score = 1.0) with sliced items
- Handle overflow detection (Throw / Truncate / Proceed strategies)
- Delegate to placer strategy for final ordering

### Scorer Algorithms (7 scorers)

| Scorer | Algorithm | Inputs Used | Output Range |
|--------|-----------|-------------|--------------|
| **RecencyScorer** | Rank-based: `rank / (countWithTimestamp - 1)`. Rank = count of items with strictly older timestamp. Null timestamps -> 0.0. Single item -> 1.0. | `item.timestamp`, `allItems[*].timestamp` | [0.0, 1.0] |
| **PriorityScorer** | Rank-based: `rank / (countWithPriority - 1)`. Rank = count of items with strictly lower priority. Null priorities -> 0.0. Single item -> 1.0. | `item.priority`, `allItems[*].priority` | [0.0, 1.0] |
| **KindScorer** | Dictionary lookup: `weights[item.kind]` or 0.0 if unknown. Default weights: SystemPrompt=1.0, Memory=0.8, ToolOutput=0.6, Document=0.4, Message=0.2. | `item.kind` | [0.0, configured max] |
| **TagScorer** | Sum matched tag weights normalized: `min(sum(matched) / totalConfiguredWeight, 1.0)`. Case depends on dictionary comparer (spec: case-insensitive). No tags -> 0.0. | `item.tags`, configured `tagWeights` | [0.0, 1.0] |
| **FrequencyScorer** | Proportion of peers sharing any tag: `matchingPeers / (allItems.count - 1)`. Case-insensitive tag comparison. No tags or single item -> 0.0. Self-excluded by reference identity. | `item.tags`, `allItems[*].tags` | [0.0, 1.0] |
| **ReflexiveScorer** | Passthrough: `clamp(item.futureRelevanceHint, 0.0, 1.0)`. Null or non-finite -> 0.0. | `item.futureRelevanceHint` | [0.0, 1.0] |
| **CompositeScorer** | Weighted average: `sum(scorer_i.Score(item) * normalizedWeight_i)`. Weights normalized to sum to 1.0 at construction. DAG cycle detection via DFS at construction. | All child scorers | [0.0, 1.0] (when children are [0,1]) |
| **ScaledScorer** | Min-max normalization: `(raw - min) / (max - min)`. Scans all items for min/max. Degenerate case (all equal) -> 0.5. O(N) per item, O(N^2) total. | Inner scorer, all items | [0.0, 1.0] |

### Slicer Algorithms (4 slicers)

| Slicer | Algorithm |
|--------|-----------|
| **GreedySlice** | Sort by value density (`score/tokens`, zero-token items = MAX_FLOAT density). Greedily fill to `targetTokens`. Zero-token items always included. O(N log N). |
| **KnapsackSlice** | 0/1 knapsack DP. Scores int-scaled (x10000). Weights ceiling-discretized, capacity floor-discretized by `bucketSize` for feasibility guarantee. Zero-token items pre-filtered (always included). Reverse-iteration 1D DP array. |
| **QuotaSlice** | Decorator: partitions by `ContextKind`, computes per-kind budgets from Require% (minimum) and Cap% (maximum) constraints, distributes unassigned budget proportionally by candidate token mass, delegates per-kind to inner slicer. |
| **StreamSlice** | Online/streaming: micro-batch accumulation, sort batch by score descending, greedy fill within batch. Cancels upstream when budget exhausted. (Async-only, streaming pipeline path.) |

### Placer Algorithms (2 placers)

| Placer | Algorithm |
|--------|-----------|
| **ChronologicalPlacer** | Stable sort ascending by timestamp. Null timestamps sort to end. Tiebreak: ascending by original index. |
| **UShapedPlacer** | Stable sort descending by score. Even-indexed (0,2,4...) items fill from left edge, odd-indexed (1,3,5...) fill from right edge. Highest scores at edges, lowest in middle. |

### Data Model (spec must define)

- **ContextItem**: content (string), tokens (int), kind (string, extensible), source (string, extensible), priority (int?), tags (string[]), metadata (map<string, any>), timestamp (datetime?), futureRelevanceHint (float64?), pinned (bool), originalTokens (int?)
- **ContextBudget**: maxTokens (int >= 0), targetTokens (int >= 0, <= maxTokens), outputReserve (int >= 0, <= maxTokens), reservedSlots (map<kind, int>), estimationSafetyMarginPercent (float64 0-100)
- **ScoredItem**: item (ContextItem), score (float64)
- **ContextKind**: extensible string enum, well-known values: Message, Document, ToolOutput, Memory, SystemPrompt (case-insensitive equality)
- **ContextSource**: extensible string enum, well-known values: Chat, Tool, Rag (case-insensitive equality)
- **OverflowStrategy**: Throw | Truncate | Proceed

---

## Don't Hand-Roll

| Problem | Use Instead |
|---------|-------------|
| Static site generation | mdBook (`cargo install mdbook`) |
| GitHub Pages deployment | Official `actions/deploy-pages@v4` workflow |
| TOML parsing for conformance runner | Language-native TOML library (C#: `Tomlyn`; Rust: `toml` crate) |
| Pseudocode rendering in Markdown | Fenced code blocks with no language tag (renders as plain monospace) |
| Mermaid diagrams (if used) | `mdbook-mermaid` preprocessor |
| Custom numbering/section refs | mdBook's built-in `[section-name](path.md)` cross-references |

---

## Common Pitfalls

### P1: Under-specifying Sort Stability
**Risk: HIGH**

The entire pipeline depends on stable sort with `(Score, Index)` tiebreaking. If the spec says "sort by score descending" without mandating stability, implementations can produce different orderings and fail conformance tests.

**Mitigation:** Explicitly mandate stable sort or equivalently prescribe `(Score descending, OriginalIndex ascending)` as the composite sort key everywhere.

### P2: Floating-Point Comparison in Conformance Tests
**Risk: HIGH**

Test vectors that assert exact floating-point scores will be fragile across languages. Different languages may evaluate `3.0 / (3.0 - 1.0)` slightly differently.

**Mitigation:** Conformance tests assert item selection and ordering, not exact scores. Where score values are checked (per-stage scorer tests), use epsilon tolerance `1e-9`. Document this in the conformance chapter.

### P3: Deduplication Identity Semantics
**Risk: MEDIUM**

C# uses `StringComparer.Ordinal` for content deduplication. The spec must be explicit about string comparison semantics (byte-exact, no Unicode normalization).

**Mitigation:** Spec states: "Content equality uses byte-exact (ordinal) comparison. No Unicode normalization, case folding, or whitespace normalization is applied."

### P4: ContextKind Case-Insensitivity
**Risk: MEDIUM**

`ContextKind` equality is case-insensitive in C# (`OrdinalIgnoreCase`). This is a cross-cutting concern: affects KindScorer lookups, QuotaSlice partitioning, and budget reservedSlots.

**Mitigation:** Spec must explicitly state: "ContextKind comparison is case-insensitive (ASCII fold). 'Message' equals 'message' equals 'MESSAGE'."

### P5: KnapsackSlice Score Scaling
**Risk: MEDIUM**

The C# implementation scales scores by `x10000` and truncates to int for the DP table. This is an implementation detail that affects which items get selected when scores are very close.

**Mitigation:** Spec defines KnapsackSlice behavioral contract (optimal or near-optimal selection within budget) but allows implementations to use different internal precision. Conformance tests for KnapsackSlice should use scores with sufficient separation (>= 0.01 apart) to avoid precision-dependent selection differences.

### P6: QuotaSlice Budget Distribution Rounding
**Risk: MEDIUM**

Integer truncation in per-kind budget calculation (`(int)(percent / 100.0 * targetTokens)`) can cause the sum of kind budgets to be less than targetTokens. This is acceptable behavior but must be documented.

**Mitigation:** Spec states: "Per-kind budget calculation uses floor truncation. The sum of kind budgets may be less than targetTokens due to rounding."

### P7: Spec/Implementation Drift
**Risk: LOW** (but ongoing)

If the C# implementation changes after the spec is written, they can diverge.

**Mitigation:** Include a spec version number. Document that the spec describes algorithm version 1.0 corresponding to Cupel library v1.0.x. Consider running C# tests against conformance vectors as a CI step (discretionary per CONTEXT.md).

---

## Code Examples

### TOML Test Vector Format: Per-Stage Scorer Test

```toml
# conformance/required/scoring/recency-basic.toml
[test]
name = "RecencyScorer ranks by timestamp"
stage = "scoring"
scorer = "recency"

[[items]]
content = "oldest"
tokens = 100
timestamp = 2024-01-01T00:00:00Z

[[items]]
content = "middle"
tokens = 100
timestamp = 2024-06-01T00:00:00Z

[[items]]
content = "newest"
tokens = 100
timestamp = 2024-12-01T00:00:00Z

[[expected]]
content = "oldest"
score_approx = 0.0

[[expected]]
content = "middle"
score_approx = 0.5

[[expected]]
content = "newest"
score_approx = 1.0

[tolerance]
score_epsilon = 1e-9
```

### TOML Test Vector Format: End-to-End Pipeline Test

```toml
# conformance/required/pipeline/greedy-chronological-basic.toml
[test]
name = "Basic pipeline: greedy slice, chronological placement"
stage = "pipeline"

[budget]
max_tokens = 1000
target_tokens = 500
output_reserve = 0

[config]
slicer = "greedy"
placer = "chronological"
deduplication = true

[[config.scorers]]
type = "recency"
weight = 1.0

[[items]]
content = "system prompt"
tokens = 100
kind = "SystemPrompt"
timestamp = 2024-01-01T00:00:00Z

[[items]]
content = "old message"
tokens = 200
kind = "Message"
timestamp = 2024-06-01T00:00:00Z

[[items]]
content = "recent message"
tokens = 200
kind = "Message"
timestamp = 2024-12-01T00:00:00Z

[[items]]
content = "very old message"
tokens = 300
kind = "Message"
timestamp = 2024-03-01T00:00:00Z

# Expected: selected items in placement order (chronological)
[[expected_output]]
content = "old message"

[[expected_output]]
content = "recent message"
```

### TOML Test Vector Format: Slicer Test

```toml
# conformance/required/slicing/greedy-density.toml
[test]
name = "GreedySlice selects by value density"
stage = "slicing"
slicer = "greedy"

[budget]
target_tokens = 300

# Items are pre-scored (slicer receives scored items sorted by score desc)
[[scored_items]]
content = "high score high tokens"
tokens = 400
score = 0.9

[[scored_items]]
content = "medium score low tokens"
tokens = 100
score = 0.7

[[scored_items]]
content = "low score low tokens"
tokens = 100
score = 0.5

[[scored_items]]
content = "zero tokens"
tokens = 0
score = 0.1

# Expected: selected items (order within slicer output is not specified by placement)
[expected]
selected_contents = ["zero tokens", "medium score low tokens", "low score low tokens"]
```

### mdBook book.toml Configuration

```toml
[book]
title = "Cupel Specification"
authors = ["Cupel Contributors"]
description = "Language-agnostic specification for the Cupel context selection algorithm"
language = "en"
src = "src"

[output.html]
default-theme = "light"
preferred-dark-theme = "navy"
smart-punctuation = true
git-repository-url = "https://github.com/wollax/cupel"
edit-url-template = "https://github.com/wollax/cupel/edit/main/spec/{path}"
site-url = "/cupel/"
additional-css = []
```

### mdBook SUMMARY.md Structure

```markdown
# Summary

[Introduction](introduction.md)

# Specification

- [Data Model](data-model.md)
  - [ContextItem](data-model/context-item.md)
  - [ContextBudget](data-model/context-budget.md)
  - [Enumerations](data-model/enumerations.md)
- [Pipeline](pipeline.md)
  - [Stage 1: Classify](pipeline/classify.md)
  - [Stage 2: Score](pipeline/score.md)
  - [Stage 3: Deduplicate](pipeline/deduplicate.md)
  - [Stage 4: Sort](pipeline/sort.md)
  - [Stage 5: Slice](pipeline/slice.md)
  - [Stage 6: Place](pipeline/place.md)
- [Scorers](scorers.md)
  - [RecencyScorer](scorers/recency.md)
  - [PriorityScorer](scorers/priority.md)
  - [KindScorer](scorers/kind.md)
  - [TagScorer](scorers/tag.md)
  - [FrequencyScorer](scorers/frequency.md)
  - [ReflexiveScorer](scorers/reflexive.md)
  - [CompositeScorer](scorers/composite.md)
  - [ScaledScorer](scorers/scaled.md)
- [Slicers](slicers.md)
  - [GreedySlice](slicers/greedy.md)
  - [KnapsackSlice](slicers/knapsack.md)
  - [QuotaSlice](slicers/quota.md)
- [Placers](placers.md)
  - [ChronologicalPlacer](placers/chronological.md)
  - [UShapedPlacer](placers/u-shaped.md)

# Conformance

- [Conformance Levels](conformance/levels.md)
- [Test Vector Format](conformance/format.md)
- [Running the Suite](conformance/running.md)

# Appendix

- [Changelog](changelog.md)
```

---

## Spec Versioning Strategy (Recommendation)
**Confidence: MEDIUM**

Use **SemVer for the spec independently from the library**, with a mapping table:

| Spec Version | Cupel Library Version | Notes |
|---|---|---|
| 1.0.0 | 1.0.x | Initial release |

- Spec MAJOR bumps = breaking changes to algorithm behavior
- Spec MINOR bumps = new optional algorithms/features
- Spec PATCH bumps = clarifications, typo fixes, new test vectors

The spec version is declared in `spec/src/introduction.md` and in the `book.toml` description.

---

## Discretion Recommendations

| Area | Recommendation | Rationale |
|------|---------------|-----------|
| Document format | Markdown via mdBook | Best GitHub Pages integration, zero friction, established ecosystem |
| Single vs multi-document | Multi-document (one chapter per major concept) | Better navigation, focused reading, easier maintenance |
| Diagrams | Yes, Mermaid flowcharts for pipeline overview and U-shaped placer | Mermaid renders natively in GitHub and via mdbook-mermaid |
| Tiebreaking | Prescribed (stable sort, lower index wins) | Eliminates conformance test non-determinism |
| Conformance suite location | Separate `conformance/` directory alongside `spec/` | Keeps TOML fixtures parseable without mdBook, easier to consume from test runners |
| C# validation against conformance suite | Defer to a follow-up task or phase | Keeps this phase focused on authoring the spec; validation is a separate testing concern |
| Spec repo location | In this repo (`cupel/spec/` + `cupel/conformance/`) | Co-location simplifies maintenance and cross-referencing |

---

## Scope Boundaries

### In Scope
- Specification of the synchronous pipeline (Classify -> Score -> Deduplicate -> Sort -> Slice -> Place)
- All 8 scorer algorithms (including CompositeScorer and ScaledScorer)
- All 4 slicer strategies (Greedy, Knapsack, Quota, Stream)
- Both placer strategies (Chronological, UShaped)
- Data model definitions (ContextItem, ContextBudget, ScoredItem, enums)
- Overflow handling strategies (Throw, Truncate, Proceed)
- Conformance test vectors (required + optional tiers)
- GitHub Pages deployment workflow

### Out of Scope
- Streaming/async pipeline path (implementation-specific, not core algorithm)
- Diagnostics/tracing infrastructure (implementation concern)
- DI registration patterns (language-specific)
- JSON serialization format (companion package concern)
- Named policies / CupelPresets (convenience layer, not core algorithm)
- Tiktoken integration (tokenizer is pluggable, not part of spec)
- PipelineBuilder API (language-specific builder pattern)

---

*Phase: 11-language-agnostic-specification*
*Research completed: 2026-03-14*
