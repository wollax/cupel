# Phase 15 Context — Conformance Hardening

## Decisions

### 1. Conformance Vector Canonical Location

**Decision:** The spec is the canonical source of conformance vectors. Vectors live in `spec/conformance/required/` and `spec/conformance/optional/` in this repo. The Rust crate (assay) vendors a copy; a CI diff guard will be set up in Phase 17 (not this phase).

**Directory structure:**
```
spec/
  conformance/
    required/
      scoring/
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
      slicing/
        greedy-density.toml
        greedy-exact-fit.toml
        greedy-zero-tokens.toml
        knapsack-basic.toml
        knapsack-zero-tokens.toml
        quota-basic.toml
      placing/
        chronological-basic.toml
        chronological-null-timestamps.toml
        u-shaped-basic.toml
        u-shaped-equal-scores.toml
      pipeline/
        composite-greedy-chronological.toml
        greedy-chronological.toml
        greedy-ushaped.toml
        knapsack-chronological.toml
        pinned-items.toml
    optional/
      (empty — creating optional vectors is out of scope for Phase 15)
```

**Authoring approach:** Write vectors fresh in the spec directory, then diff against the Rust crate's vendored copy to verify byte-exact parity. Do NOT copy-paste from the Rust crate — author independently to validate the spec's self-sufficiency.

### 2. Phase 09 VERIFICATION.md Gap Disposition

**Decision:** Accept as-is. The verification file exists, correctly identifies the gap (custom scorer factory not invoked during deserialization, 17/18 pass), and documents it as a known design limitation. The gap is a separate tracked issue, not a Phase 15 concern.

Phase 15's success criterion was "backfill missing verification" — the file is no longer missing. Done.

### 3. Phase 01 VERIFICATION.md

**Decision:** Accept as-is. The verification is thorough (166 lines, PASS status, all 20 artifacts verified). No re-verification needed despite 14 phases of subsequent changes.

### 4. Scope Boundaries

**In scope:**
- Author all required conformance vectors in `spec/conformance/required/`
- Verify byte-exact parity with Rust crate's vendored copies
- Confirm Rust conformance runner still passes all required tests

**Out of scope:**
- Optional conformance vectors (only required tier)
- C# conformance test runner
- CI diff guard between spec and Rust crate (deferred to Phase 17)
- mdBook documentation updates (prose stays as-is, no links to vector files)
- Fixing the Phase 09 custom scorer resolution gap

### 5. Updated Success Criteria Assessment

| Original Criterion | Status | Phase 15 Work |
|---|---|---|
| `quota-basic.toml` moved from optional to required in spec | Spec text already lists QuotaSlice as required; no TOML files exist in spec tree | Author all 28 required vectors in `spec/conformance/required/` |
| Rust conformance test runner updated — all required tests pass | Already passes with 28 required vectors including quota-basic | Verify parity with spec-authored vectors |
| VERIFICATION.md for Phase 01 | Already exists, PASS | None |
| VERIFICATION.md for Phase 09 | Already exists, gaps_found (accepted) | None |

**Net work:** The phase reduces to one main deliverable — establishing the canonical conformance vector directory in the spec source tree with all 28 required vectors, verified against the Rust crate's copies.
