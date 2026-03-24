# S03: Rust policy_sensitivity and spec chapter — UAT

**Milestone:** M007
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All deliverables are library APIs and documentation artifacts. Correctness is fully verified by `cargo test --all-targets` (167 passing integration + unit tests) and `dotnet test` (679 passing). The spec chapter has 0 TBD fields and is linked from SUMMARY.md. No live runtime, UI, or human-experience surface is involved.

## Preconditions

- Rust toolchain installed (`cargo`, `rustc`)
- .NET 10 SDK installed (`dotnet`)
- Working directory: `/Users/wollax/Git/personal/cupel`

## Smoke Test

```bash
cd crates/cupel && cargo test --test policy_sensitivity_from_policies
# → 3 passed (all_items_swing, no_items_swing, partial_swing)
```

## Test Cases

### 1. All items swing (PriorityScorer vs ReflexiveScorer, tight budget)

```bash
cd crates/cupel && cargo test --test policy_sensitivity_from_policies all_items_swing
```

**Expected:** test `all_items_swing` passes; `report.diffs.len() == 2` (both items appear in diffs because the two policies disagree on every item).

### 2. No items swing (identical policies, ample budget)

```bash
cd crates/cupel && cargo test --test policy_sensitivity_from_policies no_items_swing
```

**Expected:** test `no_items_swing` passes; `report.diffs` is empty because both policies produce identical selection.

### 3. Partial swing (2 of 3 items swing)

```bash
cd crates/cupel && cargo test --test policy_sensitivity_from_policies partial_swing
```

**Expected:** test `partial_swing` passes; `report.diffs.len() == 2` (stable item excluded from diffs; swing-relevance and swing-priority differ).

### 4. Pipeline-based variant (rename regression)

```bash
cd crates/cupel && cargo test --test policy_sensitivity
```

**Expected:** 2 tests pass (`policy_sensitivity_two_variants_produces_diff`, `policy_sensitivity_guaranteed_diff`).

### 5. Spec chapter completeness

```bash
grep -c "TBD" spec/src/analytics/policy-sensitivity.md
grep "policy-sensitivity" spec/src/SUMMARY.md
```

**Expected:** first command exits 1 (no matches = 0 count); second command returns 1 line containing the link.

### 6. .NET regression

```bash
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
```

**Expected:** 679 passed, 0 failed.

### 7. Full Rust suite

```bash
cd crates/cupel && cargo test --all-targets
```

**Expected:** all 167 tests pass, 0 failed.

## Edge Cases

### Minimum-variants guard

```rust
let result = policy_sensitivity(&items, &budget, &[(label, &policy)]);
assert!(matches!(result, Err(CupelError::PipelineConfig(_))));
```

**Expected:** `Err(CupelError::PipelineConfig("policy_sensitivity requires at least 2 variants"))` — confirmed by the test suite (behavior tested inline in integration tests).

### Clippy cleanliness

```bash
cd crates/cupel && cargo clippy --all-targets -- -D warnings
```

**Expected:** 0 warnings, exit 0.

## Failure Signals

- Any `cargo test` failure mentioning `policy_sensitivity_from_policies` → behavioral contract broken in new function
- Any `cargo test` failure mentioning `policy_sensitivity.rs` → rename regression
- `grep -c "TBD" spec/src/analytics/policy-sensitivity.md` returns non-zero → spec chapter has unfinished fields
- `grep "policy-sensitivity" spec/src/SUMMARY.md` returns no match → spec chapter not linked
- `dotnet test` failure → .NET regression introduced in documentation task

## Requirements Proved By This UAT

- R056 — `policy_sensitivity` free function exists, accepts `&[(label, &Policy)]`, returns `PolicySensitivityReport` with correct swing diffs, has minimum-variants guard, exports from `lib.rs`; spec chapter at `spec/src/analytics/policy-sensitivity.md` is TBD-free and linked from SUMMARY.md; both language test suites pass (167 Rust, 679 .NET)

## Not Proven By This UAT

- Performance characteristics of `policy_sensitivity` under large item sets or many variants — not tested; library context, expected to be negligible
- Round-trip serialization of `PolicySensitivityReport` via serde — not implemented in this milestone; callers needing JSON serialization would need to add serde derives
- `policy_sensitivity_from_pipelines` with more than 2 variants — current tests use exactly 2; the algorithm is identical to the pre-rename version which also used 2 in its tests

## Notes for Tester

All verification was agent-executed during slice execution. The UAT test cases above map 1:1 to the slice verification commands. Run them in order from the repo root. No seeding, server startup, or environment configuration is required — pure library tests.
