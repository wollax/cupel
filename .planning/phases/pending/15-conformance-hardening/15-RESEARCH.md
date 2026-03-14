# Phase 15: Conformance Hardening - Research

**Researched:** 2026-03-14
**Domain:** TOML conformance test vector authoring and byte-exact verification
**Confidence:** HIGH

## Summary

This phase is a content-authoring task, not a library/framework integration task. The deliverable is 28 TOML conformance test vector files in `spec/conformance/required/`, organized into 4 subdirectories (scoring, slicing, placing, pipeline). These vectors must be byte-exact copies of the ones already passing in the Rust crate (`assay-cupel`), but authored independently from the spec documentation to validate the spec's self-sufficiency.

The format is fully specified in `spec/src/conformance/format.md`. Every vector follows the same structural pattern: a `[test]` header identifying the stage, input data tables, configuration tables, and expected output tables. The existing 28 vectors in the Rust crate total ~1,483 lines of TOML and are well-commented with step-by-step computation traces.

**Primary recommendation:** Author each vector by reading the spec's algorithm description, constructing inputs and computing expected outputs from first principles, then diff against the Rust crate's copy to verify byte-exact parity. Do NOT copy-paste.

## Standard Stack

### Core

| Tool | Purpose | Why Standard |
|------|---------|--------------|
| `diff` (coreutils) | Byte-exact comparison of authored vs vendored vectors | Standard Unix tool, no dependencies |
| TOML spec (toml.io) | Reference for TOML syntax rules | Vectors are TOML files |

### Supporting

| Tool | Purpose | When to Use |
|------|---------|-------------|
| `xxd` or `hexdump` | Binary inspection for encoding issues | When `diff` shows differences but content looks identical |
| `wc -l` / `wc -c` | Quick parity check before full diff | Smoke test before byte-exact verification |
| `find` + `diff -r` | Recursive directory comparison | Final verification of all 28 vectors at once |

### Alternatives Considered

None. This is a file-authoring task. No libraries or frameworks are involved.

**Installation:** No packages needed. All tools are available in the base macOS environment.

## Architecture Patterns

### Required Directory Structure

```
spec/
  conformance/
    required/
      scoring/           # 13 vectors
        recency-basic.toml
        recency-null-timestamps.toml
        priority-basic.toml
        priority-null.toml
        reflexive-basic.toml
        reflexive-null.toml
        kind-default-weights.toml
        kind-unknown.toml
        tag-basic.toml
        tag-no-tags.toml
        frequency-basic.toml
        composite-weighted.toml
        scaled-basic.toml
      slicing/           # 6 vectors
        greedy-density.toml
        greedy-exact-fit.toml
        greedy-zero-tokens.toml
        knapsack-basic.toml
        knapsack-zero-tokens.toml
        quota-basic.toml
      placing/           # 4 vectors
        chronological-basic.toml
        chronological-null-timestamps.toml
        u-shaped-basic.toml
        u-shaped-equal-scores.toml
      pipeline/          # 5 vectors
        composite-greedy-chronological.toml
        greedy-chronological.toml
        greedy-ushaped.toml
        knapsack-chronological.toml
        pinned-items.toml
    optional/            # empty (out of scope)
```

### Pattern 1: Scoring Vector Structure

**What:** All scoring vectors follow identical structural layout.
**When to use:** All 13 files in `scoring/`.

```toml
[test]
name = "Human-readable description of what's being tested"
stage = "scoring"
scorer = "recency"  # one of: recency, priority, kind, tag, frequency, reflexive, composite, scaled

# Comment block explaining the computation step-by-step
# Using the exact values from the items below

# Optional config section (scorer-specific)
[config]
use_default_weights = true  # KindScorer only

[[items]]
content = "item-name"
tokens = 100
timestamp = 2024-01-01T00:00:00Z  # optional, scorer-dependent
# Other optional fields: priority, kind, tags, futureRelevanceHint

[[expected]]
content = "item-name"
score_approx = 0.5

[tolerance]
score_epsilon = 1e-9
```

### Pattern 2: Slicing Vector Structure

**What:** All slicing vectors use pre-scored items and set comparison.
**When to use:** All 6 files in `slicing/`.

```toml
[test]
name = "Description"
stage = "slicing"
slicer = "greedy"  # one of: greedy, knapsack, quota

# Optional config (slicer-specific)
[config]
inner_slicer = "greedy"  # QuotaSlice only

[budget]
target_tokens = 300

[[scored_items]]
content = "item-name"
tokens = 100
score = 0.8
kind = "Message"  # optional, QuotaSlice only

[expected]
selected_contents = ["item-a", "item-b"]  # SET comparison
```

### Pattern 3: Placing Vector Structure

**What:** All placing vectors use pre-scored items and ordered comparison.
**When to use:** All 4 files in `placing/`.

```toml
[test]
name = "Description"
stage = "placing"
placer = "chronological"  # one of: chronological, u-shaped

[[items]]
content = "item-name"
tokens = 100
score = 0.5
timestamp = 2024-01-01T00:00:00Z  # optional

[expected]
ordered_contents = ["first", "second", "third"]  # ORDER matters
```

### Pattern 4: Pipeline Vector Structure

**What:** End-to-end pipeline vectors with full configuration.
**When to use:** All 5 files in `pipeline/`.

```toml
[test]
name = "Description"
stage = "pipeline"

[budget]
max_tokens = 1000
target_tokens = 400
output_reserve = 0

[config]
slicer = "greedy"
placer = "chronological"
deduplication = true

[[config.scorers]]
type = "recency"
weight = 1.0

[[items]]
content = "item-name"
tokens = 100
kind = "Message"
timestamp = 2024-01-01T00:00:00Z
pinned = true  # optional

[[expected_output]]
content = "first-in-output"

[[expected_output]]
content = "second-in-output"
```

### Pattern 5: Comment Conventions

Every vector file includes detailed computation trace comments between the `[test]` header and the data tables. These comments explain the algorithm step-by-step with concrete numeric values, making the vector self-documenting and independently verifiable. This is a critical part of the byte-exact match requirement.

### Anti-Patterns to Avoid

- **Copy-paste from the Rust crate:** The decision explicitly forbids this. Author from spec, then diff.
- **Missing trailing newline:** Both repos enforce `insert_final_newline = true` via `.editorconfig`.
- **Windows line endings (CRLF):** Both repos enforce `end_of_line = lf`.
- **Trailing whitespace:** Both repos enforce `trim_trailing_whitespace = true`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|------------|-------------|-----|
| TOML validation | Custom parser/validator | `diff` against Rust crate's copies | The Rust crate already validates these vectors via its test suite |
| Algorithm computation | Mental math for expected values | Step-by-step trace in comments | Mistakes in expected values cause false test failures |
| Directory structure | Ad-hoc organization | The exact 4-directory layout from CONTEXT.md | Must match Rust crate's structure for `diff -r` verification |

**Key insight:** This is not a creative authoring task. The vectors must be byte-exact copies of existing files. The "author fresh" requirement is a verification exercise: can you derive the same file from the spec alone? The diff at the end proves it.

## Common Pitfalls

### Pitfall 1: Float Representation in TOML

**What goes wrong:** Writing `0.166666666666667` when the file has `0.166666666666667` or vice versa. Float precision in `score_approx` and `weight` fields must match exactly.
**Why it happens:** Different levels of decimal precision look equivalent but produce different bytes.
**How to avoid:** Always check the exact decimal representation in the expected value. The Rust crate's vectors use specific precision for each value. When authoring from spec, use the precision the algorithm naturally produces.
**Warning signs:** `diff` shows differences on lines containing floats.

### Pitfall 2: TOML Integer vs Float Ambiguity

**What goes wrong:** Writing `score = 0` (integer in TOML) instead of `score = 0.0` (float), or vice versa.
**Why it happens:** TOML distinguishes integers and floats. `0` and `0.0` are different types.
**How to avoid:** Check the existing vectors' convention. Scores use floats (`0.0`, `0.5`, `1.0`). Tokens and priorities use integers (`100`, `10`).
**Warning signs:** TOML parser type mismatch errors when the Rust test runner reads the vector.

### Pitfall 3: Comment Block Formatting

**What goes wrong:** Slightly different wording, spacing, or line breaks in the computation trace comments.
**Why it happens:** Comments are human-written prose, making exact reproduction difficult.
**How to avoid:** The computation trace must follow the exact same reasoning structure. When the diff reveals comment differences, these must be resolved to achieve byte-exact parity.
**Warning signs:** `diff` output showing only comment lines differ.

### Pitfall 4: Table Ordering in TOML

**What goes wrong:** Placing `[config]` before `[test]`, or `[budget]` after `[[scored_items]]`.
**Why it happens:** TOML tables are order-independent semantically, but byte-exact comparison requires identical ordering.
**How to avoid:** Follow the exact table order from the Rust crate's vectors. The consistent pattern is: `[test]` → comments → `[config]` → `[budget]` → `[[items]]`/`[[scored_items]]` → `[[expected]]`/`[expected]`/`[[expected_output]]` → `[tolerance]`.
**Warning signs:** `diff` output showing entire blocks displaced.

### Pitfall 5: Blank Line Conventions

**What goes wrong:** Extra or missing blank lines between sections.
**Why it happens:** Blank lines between TOML tables are stylistic, but byte-exact requires matching.
**How to avoid:** The vectors consistently use single blank lines between array-of-table entries (`[[items]]` blocks) and between major sections. No trailing blank lines at end of file.
**Warning signs:** `diff` showing only blank line differences.

### Pitfall 6: Datetime Format

**What goes wrong:** Using `2024-01-01T00:00:00+00:00` instead of `2024-01-01T00:00:00Z`.
**Why it happens:** Both are valid RFC 3339 UTC representations.
**How to avoid:** The spec and all existing vectors use the `Z` suffix exclusively.
**Warning signs:** Timestamp fields differ in `diff` output.

## Code Examples

### Verification Command: Full Directory Diff

```bash
# After authoring all 28 vectors, verify byte-exact parity
diff -r \
  spec/conformance/required/ \
  /Users/wollax/Git/personal/assay/crates/assay-cupel/tests/conformance/required/

# Expected output: nothing (no differences)
```

### Verification Command: Per-File with Context

```bash
# For debugging individual file differences
diff -u \
  spec/conformance/required/scoring/recency-basic.toml \
  /Users/wollax/Git/personal/assay/crates/assay-cupel/tests/conformance/required/scoring/recency-basic.toml
```

### Verification Command: Byte-Level Comparison

```bash
# When diff shows no output but you want extra assurance
for subdir in scoring slicing placing pipeline; do
  for f in spec/conformance/required/$subdir/*.toml; do
    base=$(basename "$f")
    cmp -s "$f" "/Users/wollax/Git/personal/assay/crates/assay-cupel/tests/conformance/required/$subdir/$base" \
      && echo "OK: $subdir/$base" \
      || echo "MISMATCH: $subdir/$base"
  done
done
```

### Verification Command: Confirm Rust Tests Still Pass

```bash
cd /Users/wollax/Git/personal/assay
rtk cargo test -p assay-cupel --test conformance
```

### File Count Verification

```bash
# Confirm exactly 28 files in the expected structure
find spec/conformance/required -name "*.toml" | wc -l
# Expected: 28

# Per-directory counts
find spec/conformance/required/scoring -name "*.toml" | wc -l   # Expected: 13
find spec/conformance/required/slicing -name "*.toml" | wc -l   # Expected: 6
find spec/conformance/required/placing -name "*.toml" | wc -l   # Expected: 4
find spec/conformance/required/pipeline -name "*.toml" | wc -l  # Expected: 5
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|---|---|---|---|
| Vectors only in Rust crate | Spec is canonical source, Rust vendors a copy | Phase 15 | Enables language-agnostic conformance testing |
| QuotaSlice in optional tier | QuotaSlice in required tier | Spec already updated (levels.md) | All conforming implementations must support QuotaSlice |

**Already completed (no Phase 15 work needed):**
- `levels.md` already lists QuotaSlice under Required tier
- `quota-basic.toml` already exists in Rust crate's `required/` directory
- VERIFICATION.md files exist for both Phase 01 and Phase 09

## Open Questions

1. **Optional directory creation**
   - What we know: The CONTEXT.md says `optional/` is out of scope and should be "(empty)"
   - What's unclear: Should we create the empty directory, or omit it entirely?
   - Recommendation: Create it as an empty directory (with a `.gitkeep` if git won't track it) to match the documented structure. This is a minor decision the planner can lock.

## Sources

### Primary (HIGH confidence)
- `/Users/wollax/Git/personal/cupel/spec/src/conformance/format.md` - Complete schema for all 4 vector types
- `/Users/wollax/Git/personal/cupel/spec/src/conformance/levels.md` - Conformance tier definitions (QuotaSlice already in Required)
- `/Users/wollax/Git/personal/cupel/spec/src/conformance/running.md` - Runner pseudocode and comparison semantics
- `/Users/wollax/Git/personal/assay/crates/assay-cupel/tests/conformance/required/` - All 28 reference vectors (the ground truth for byte-exact parity)
- `/Users/wollax/Git/personal/assay/crates/assay-cupel/tests/conformance.rs` - Rust test runner implementation

### Secondary (MEDIUM confidence)
- `/Users/wollax/Git/personal/cupel/.editorconfig` - Encoding and formatting rules (LF, UTF-8, final newline)
- `/Users/wollax/Git/personal/assay/.editorconfig` - Assay repo formatting rules (must match)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No libraries needed, just file authoring and Unix diff tools
- Architecture: HIGH - Directory structure and file format fully specified in existing docs
- Pitfalls: HIGH - Derived from direct inspection of all 28 existing vectors and both repos' editorconfig

**Research date:** 2026-03-14
**Valid until:** Indefinite (this is internal project structure, not external ecosystem)
