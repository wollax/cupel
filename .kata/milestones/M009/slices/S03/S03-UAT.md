# S03: Spec chapters — count-constrained-knapsack + metadata-key — UAT

**Milestone:** M009
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: This slice produces only documentation artifacts (spec chapters, index updates, CHANGELOG entry). There is no runtime behavior introduced. Completeness is fully verifiable via grep and test-suite regression checks — no human interaction or live system is needed.

## Preconditions

- Repository checked out on `kata/M009/S03` branch (or main after merge)
- `cargo` and `dotnet` toolchains available

## Smoke Test

```bash
grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md  # → 0
grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md               # → 0
```

Both return 0 → spec chapters are complete.

## Test Cases

### 1. count-constrained-knapsack spec chapter completeness

```bash
grep -ci "\bTBD\b" spec/src/slicers/count-constrained-knapsack.md
```

**Expected:** `0`

```bash
grep "^## " spec/src/slicers/count-constrained-knapsack.md
```

**Expected:** 10 section headings including Overview, Algorithm, Scarcity Behavior, Monotonicity, Trade-offs, Conformance Vector Outlines, Complexity, Conformance Notes.

### 2. metadata-key spec chapter completeness

```bash
grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md
```

**Expected:** `0`

```bash
grep "cupel:priority" spec/src/scorers/metadata-key.md
```

**Expected:** At least one match showing the `cupel:priority` convention is documented.

### 3. SUMMARY.md links

```bash
grep -q "count-constrained-knapsack" spec/src/SUMMARY.md && echo "PASS"
grep -q "metadata-key" spec/src/SUMMARY.md && echo "PASS"
```

**Expected:** Both print `PASS`.

### 4. Index tables updated

```bash
grep -q "CountConstrainedKnapsackSlice" spec/src/slicers.md && echo "PASS"
grep -q "MetadataKeyScorer" spec/src/scorers.md && echo "PASS"
```

**Expected:** Both print `PASS`.

### 5. CHANGELOG .NET entry

```bash
grep -q "\.NET.*CountConstrainedKnapsackSlice\|CountConstrainedKnapsackSlice.*\.NET" CHANGELOG.md && echo "PASS"
```

**Expected:** Prints `PASS`.

## Edge Cases

### Regression: test suites still green

```bash
cd crates/cupel && cargo test --all-targets
```

**Expected:** Exit 0, all tests pass (spec-only changes must not break any Rust test).

```bash
dotnet test --solution Cupel.slnx
```

**Expected:** Exit 0, all tests pass (no .NET regressions).

## Failure Signals

- `grep -ci "\bTBD\b"` returning > 0 → spec chapter incomplete
- Missing section headings in either chapter → structural incompleteness
- `grep` for SUMMARY.md/slicers.md/scorers.md/CHANGELOG patterns failing → wiring incomplete
- `cargo test` or `dotnet test` non-zero exit → unintended regression

## Requirements Proved By This UAT

- R062 (CountConstrainedKnapsackSlice spec) — `spec/src/slicers/count-constrained-knapsack.md` exists with zero TBD fields; all required sections present; linked from SUMMARY.md and slicers.md index table
- R063 (MetadataKeyScorer spec) — `spec/src/scorers/metadata-key.md` exists with zero TBD fields; `cupel:priority` convention documented; linked from SUMMARY.md and scorers.md index table; serves as S04 implementation contract

## Not Proven By This UAT

- MetadataKeyScorer runtime behavior — implementation does not exist yet (S04 delivers this)
- MetadataKeyScorer conformance test vectors — no TOML files created in this slice
- `cupel:priority` metadata key being correctly read and boosted at runtime — requires S04
- CountConstrainedKnapsackSlice runtime correctness — already proven by S01 (Rust) and S02 (.NET) test suites

## Notes for Tester

The spec chapters were authored from working implementations — the algorithm descriptions, error messages, and conformance vector outlines all match the actual code. The `cupel:priority` section in `metadata-key.md` introduces a new metadata key convention but does not implement it; callers can start using the key name immediately in their item metadata for use with MetadataKeyScorer once S04 ships.
