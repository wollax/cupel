# Phase 25: Conformance Infrastructure & Drift Guard — Research

**Phase:** 25 — Conformance Infrastructure & Drift Guard
**Researched:** 2026-03-15
**Status:** Ready for planning

---

## Standard Stack

### CI Drift Guard (CONF-02, CI-03)

- **Tool:** `diff -rq` (GNU diffutils, available on `ubuntu-latest` GitHub Actions runners by default — no install step needed)
  - Confidence: HIGH — `diff` is part of GNU coreutils on every ubuntu-latest image
- **Pattern:** Single `run:` step in the existing `ci-rust.yml` `check` job, placed before the test steps
- **GitHub Actions `paths:` filter:** The `paths:` array is additive (OR logic) — each entry is a separate glob. Adding `'spec/**'` alongside `'crates/**'` is correct; any match in either tree triggers the workflow
  - Confidence: HIGH — verified against GitHub Actions documentation pattern; paths filter is well-documented AND-less OR behavior

### TOML Schema for Diagnostics Vectors (SPEC-02)

- **Approach:** Extend `[expected]` table on pipeline vectors with a nested `[expected.diagnostics]` sub-table
  - TOML allows `[expected.diagnostics]` as dotted-key sub-table under `[expected]`; this is valid TOML 1.0 and does not conflict with the existing `[[expected_output]]` array-of-tables on pipeline vectors (different key names)
  - Confidence: HIGH — TOML spec §4.5 (tables) and §4.6 (arrays of tables): `[expected]` and `[[expected_output]]` are completely separate keys
- **No external schema tooling:** The spec is documentation-only; TOML schema is expressed in `format.md` tables + example TOML, exactly as the other vector schemas are documented

---

## Architecture Patterns

### CI Step Placement

Place the drift guard step immediately before the `Test` steps, after the Rust toolchain steps. Rationale: toolchain setup and cache warm already ran; drift guard is a pure file-system check with no Rust dependency, so it does not need the cache. Placing it before tests means drift is caught without waiting for compilation.

Recommended step structure:
```yaml
- name: Conformance drift guard
  run: |
    if [ -d spec/conformance/required ] && [ -d crates/cupel/conformance/required ]; then
      diff -rq spec/conformance/required crates/cupel/conformance/required \
        || { echo "Conformance drift detected in required/"; exit 1; }
    fi
    if [ -d spec/conformance/optional ] && [ -d crates/cupel/conformance/optional ]; then
      diff -rq spec/conformance/optional crates/cupel/conformance/optional \
        || { echo "Conformance drift detected in optional/"; exit 1; }
    fi
    echo "Conformance vectors in sync."
```

Key design choices:
- Guard each directory pair with an existence check (`-d`) so the step does not fail when `optional/` does not yet exist (it currently doesn't)
- Use `|| { echo ...; exit 1; }` to emit a human-readable failure message before exiting — otherwise GitHub Actions only shows the raw diff, which is harder to diagnose
- Single step covers both `required/` and `optional/`

### `paths:` Trigger Expansion

Add `'spec/**'` to both `push` and `pull_request` `paths:` arrays:
```yaml
paths:
  - 'crates/**'
  - 'rust-toolchain.toml'
  - '.github/workflows/ci-rust.yml'
  - 'spec/**'
```

This ensures any change to spec conformance vectors triggers the drift guard without disrupting the existing Rust build/test paths.

### Diagnostics Vector TOML Schema

The `[expected.diagnostics]` sub-table composes with `[[expected_output]]` because they use different top-level keys. A diagnostics pipeline vector has both `[[expected_output]]` (ordered output assertion) and `[expected.diagnostics]` (diagnostic assertion) in the same file — they are independent.

Recommended field layout for `[expected.diagnostics]`:

```toml
# Assert on included items (ordered, matches placed order)
[[expected.diagnostics.included]]
content = "item-a"
score_approx = 0.9
inclusion_reason = "Scored"

# Assert on excluded items (ordered by score desc)
[[expected.diagnostics.excluded]]
content = "item-b"
score_approx = 0.4
exclusion_reason = "BudgetExceeded"
item_tokens = 200
available_tokens = 50

# Assert on aggregate counts
[expected.diagnostics.summary]
total_candidates = 3
total_tokens_considered = 450
```

Design rationale:
- `included` and `excluded` use `[[…]]` (array-of-tables) to mirror how the spec's JSON SelectionReport represents them as lists
- `score_approx` (not `score`) signals epsilon tolerance, consistent with the existing scoring vector convention in `format.md`
- Reason discriminator fields (`inclusion_reason`, `exclusion_reason`) are flat strings — mirrors the JSON wire format's `"reason"` discriminator
- Data-carrying fields for exclusion reasons (`item_tokens`, `available_tokens`, `deduplicated_against`) are flat siblings on the same table entry — mirrors the wire format's sibling-fields pattern from `exclusion-reasons.md`
- `[expected.diagnostics.summary]` is optional — vectors can assert on items only, aggregate only, or both
- This is schema documentation for format.md; real Rust test harness implementation is deferred to Phase 29

### Comment Fix Pattern

The 5 files requiring comment fixes share a common pattern: scratchpad thinking that was left in the file after the test was finalised. The fix pattern is:

- **Delete** abandoned computation blocks (knapsack-basic.toml lines 6–28)
- **Remove** self-correction commentary (composite-weighted.toml `# Wait —` paragraph)
- **Add** missing trace steps that other similar vectors include (pinned-items.toml greedy density-sort step)
- **Replace** ambiguous notation with output-indexed notation (u-shaped vectors `right[N]`/`left[N]` → `result[N]`)

Both `spec/conformance/` and `crates/cupel/conformance/` copies must be updated simultaneously. The copies must remain byte-identical after the edit.

---

## Don't Hand-Roll

- **Do not use `rsync`, `git diff`, or checksum scripts** for the drift guard — `diff -rq` is purpose-built for recursive directory comparison and is available on ubuntu-latest without installation
- **Do not create a separate GitHub Actions workflow** for conformance drift — a new step in `ci-rust.yml` keeps it collocated with the tests that rely on the vectors
- **Do not add a TOML schema validator or JSON Schema** for the diagnostics vector format — this phase only documents the schema in `format.md`; real validation will happen via the Rust test harness in Phase 29
- **Do not use `git submodule` or symlinks** to keep the two conformance directories in sync — the current pattern (two independent copies, diff-checked in CI) is intentional per project design

---

## Common Pitfalls

### `diff -rq` on non-existent directory
If `spec/conformance/optional/` doesn't exist but `crates/cupel/conformance/optional/` does (or vice versa), `diff -rq` exits 2 (error), not 1 (differences found). The existence guards prevent this.

**Verification step:** Confirm both existence checks (`-d`) are present for the `optional/` case.

### `paths:` filter on workflow file itself
The existing `paths:` includes `.github/workflows/ci-rust.yml`. When we add `spec/**`, changes to ci-rust.yml itself still trigger the workflow — no regression here. But the workflow file path trigger should remain in the list.

**Verification step:** Confirm `.github/workflows/ci-rust.yml` remains in the `paths:` array after editing.

### TOML `[expected]` vs `[[expected]]` conflict
Scoring vectors use `[[expected]]` (array-of-tables) while slicing/placing vectors use `[expected]` (single table). These are different stages and different files — no conflict. Diagnostics extend the pipeline vector's `[expected]` (dotted sub-table), which is also fine. However, a placement vector file must not mix `[expected]` and `[[expected]]` — they're mutually exclusive in TOML for the same key. Since diagnostics are pipeline-level only, this is not a risk.

**Verification step:** Diagnostics schema section in format.md must clearly state `stage = "pipeline"` only.

### TOML dotted-key and array-of-tables interaction
`[expected.diagnostics]` defines `expected` as a table. `[[expected.diagnostics.included]]` then defines `diagnostics.included` as an array-of-tables. This is valid TOML 1.0 — dotted-key tables can contain array-of-tables sub-keys. The key constraint: you cannot define `[expected]` AND `[[expected]]` in the same file.

**Verification step:** The example diagnostics vector in format.md should validate cleanly against a TOML 1.0 parser (can verify with `taplo` if needed, but this is a documentation task — not a code task).

### `diff -rq` and file permissions
`diff -rq` on GitHub Actions compares file content, not permissions. The vendored copies in `crates/` may have different permission bits than `spec/` depending on how they were added. Use `--no-dereference` for symlinks if any are introduced, but currently there are none.

**Verification step:** The drift guard step does not need any permission flags.

### Greedy fill trace missing density-sort in pinned-items.toml
The current `pinned-items.toml` comment shows the greedy fill pass but skips the density-sort step that all other greedy-slicer vectors show explicitly (see `greedy-density.toml` and `greedy-exact-fit.toml` for the established pattern). The fix is to insert the density calculation and sort line before the fill trace.

**Verification step:** After fix, the greedy section of pinned-items.toml should show: densities → sorted order → fill pass.

### U-shaped `left[N]`/`right[N]` vs `result[N]`
Current `u-shaped-basic.toml` uses `left[0]`, `right[4]`, `left[1]`, etc. This notation is ambiguous because it looks like pointer arithmetic on two independent arrays. The fix replaces with `result[0]`, `result[1]`, etc. to reference positions in the output array.

For `u-shaped-basic.toml` (5 items: A, C, E, D, B):
- Rank 0 (A) → result[0]
- Rank 1 (B) → result[4]
- Rank 2 (C) → result[1]
- Rank 3 (D) → result[3]
- Rank 4 (E) → result[2]

For `u-shaped-equal-scores.toml` (4 items: X, Z, W, Y):
- Rank 0 (X) → result[0]
- Rank 1 (Y) → result[3]
- Rank 2 (Z) → result[1]
- Rank 3 (W) → result[2]

**Verification step:** After replacing notation, the result array constructed from rank→result-position assignments must match `ordered_contents` in `[expected]`.

---

## Code Examples

### CI Drift Guard Step (ci-rust.yml)

```yaml
- name: Conformance drift guard
  run: |
    if [ -d spec/conformance/required ] && [ -d crates/cupel/conformance/required ]; then
      diff -rq spec/conformance/required crates/cupel/conformance/required \
        || { echo "ERROR: Conformance drift detected in required/ — spec and crates copies have diverged. Run: diff -r spec/conformance/required crates/cupel/conformance/required"; exit 1; }
    fi
    if [ -d spec/conformance/optional ] && [ -d crates/cupel/conformance/optional ]; then
      diff -rq spec/conformance/optional crates/cupel/conformance/optional \
        || { echo "ERROR: Conformance drift detected in optional/ — spec and crates copies have diverged. Run: diff -r spec/conformance/optional crates/cupel/conformance/optional"; exit 1; }
    fi
    echo "Conformance vectors in sync."
```

### Example Diagnostics Conformance Vector (SPEC-02)

```toml
[test]
name = "Pipeline diagnostics: BudgetExceeded exclusion reason"
stage = "pipeline"

# TBD: Replace with concrete values once Phase 29 implements run_traced.
#
# Budget: target=TBD, max=TBD, reserve=0.
#
# Items: TBD
#
# Expected diagnostics:
#   included: TBD item(s) with reason Scored
#   excluded: TBD item(s) with reason BudgetExceeded
#   total_candidates: TBD
#   total_tokens_considered: TBD

[budget]
max_tokens = 1000
target_tokens = 200
output_reserve = 0

[config]
slicer = "greedy"
placer = "chronological"
deduplication = false

[[config.scorers]]
type = "recency"
weight = 1.0

[[items]]
content = "fits"
tokens = 150
kind = "Message"
timestamp = 2024-06-01T00:00:00Z

[[items]]
content = "too-big"
tokens = 400
kind = "Message"
timestamp = 2024-01-01T00:00:00Z

[[expected_output]]
content = "fits"

[expected.diagnostics.summary]
total_candidates = 2
total_tokens_considered = 550

[[expected.diagnostics.included]]
content = "fits"
score_approx = 1.0
inclusion_reason = "Scored"

[[expected.diagnostics.excluded]]
content = "too-big"
score_approx = 0.0
exclusion_reason = "BudgetExceeded"
item_tokens = 400
available_tokens = 200
```

### format.md Extension Pattern

The new "Diagnostics Vectors" section in `format.md` should follow the same structure as existing stage sections:

1. Stage overview paragraph
2. Schema table (fields, types, required/optional, description)
3. Compatibility note (composes with `[[expected_output]]` for pipeline vectors)
4. Example TOML vector (the TBD placeholder vector above)
5. Ordering note (`included` in placed order; `excluded` sorted by score desc)

---

## Confidence Summary

| Finding | Confidence | Notes |
|---------|------------|-------|
| `diff -rq` available on ubuntu-latest | HIGH | Part of GNU diffutils/coreutils |
| `paths:` filter is OR-additive | HIGH | Standard GitHub Actions behavior |
| TOML `[expected.diagnostics]` sub-table validity | HIGH | TOML 1.0 spec §4.5 |
| `[[expected.diagnostics.included]]` validity | HIGH | Array-of-tables under dotted-key table |
| knapsack-basic.toml lines to delete | HIGH | Lines 6–28 confirmed by reading the file |
| composite-weighted.toml scratchpad text location | HIGH | Lines 24–28 confirmed by reading the file |
| u-shaped result[N] mapping | HIGH | Derived from the algorithm + existing ordered_contents |
| pinned-items.toml density-sort insertion point | HIGH | Confirmed by comparison with greedy-density.toml pattern |
