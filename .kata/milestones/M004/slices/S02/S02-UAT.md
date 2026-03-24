# S02: PolicySensitivityReport — fork diagnostic — UAT

**Milestone:** M004
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: Fork diagnostic is a pure function with no I/O, no UI, and no service lifecycle; correctness is fully exercised by automated integration tests in both languages

## Preconditions

- Rust toolchain installed (`cargo` available)
- .NET 10 SDK installed (`dotnet` available)
- Repository cloned and on the S02 branch

## Smoke Test

Run `cargo test --all-targets --manifest-path crates/cupel/Cargo.toml policy_sensitivity` and `dotnet test --configuration Release --filter PolicySensitivity` — both should show passing tests exercising the fork diagnostic with ≥2 pipeline variants.

## Test Cases

### 1. Rust policy_sensitivity returns labeled variants and diff

1. Run `cargo test --all-targets --manifest-path crates/cupel/Cargo.toml policy_sensitivity`
2. **Expected:** Both `policy_sensitivity_basic` and `policy_sensitivity_guaranteed_diff` tests pass; the guaranteed-diff test asserts 2 variants, 2 diff entries, and items swapping between Included/Excluded

### 2. .NET PolicySensitivity returns labeled variants and diff

1. Run `dotnet test --configuration Release --filter PolicySensitivity`
2. **Expected:** All 3 PolicySensitivity tests pass; `TwoVariants_DifferentScorers_ItemsSwapStatus` asserts diff contains entries with both Included and Excluded statuses for specific items

### 3. .NET guard rejects fewer than 2 variants

1. Run `dotnet test --configuration Release --filter "FewerThanTwoVariants"`
2. **Expected:** Test passes — `ArgumentException` thrown when fewer than 2 variants provided

## Edge Cases

### Single-item swap verification

1. Run the `policy_sensitivity_guaranteed_diff` Rust test
2. **Expected:** Each diff entry has exactly 2 statuses — one Included, one Excluded — proving the content-keyed join is correct

## Failure Signals

- Any `policy_sensitivity` or `PolicySensitivity` test failure
- `cargo clippy --all-targets -- -D warnings` producing warnings in `analytics.rs`
- Missing public API entries in `PublicAPI.Unshipped.txt` causing build warnings

## Requirements Proved By This UAT

- R051 — Fork diagnostic returns labeled SelectionReports plus structured diff for ≥2 pipeline configurations; both languages tested with real pipeline dry_run calls

## Not Proven By This UAT

- Performance under large variant counts (>10 pipelines) — not a requirement
- Thread safety of concurrent policy_sensitivity calls — pure function, inherently safe, but not explicitly tested under contention

## Notes for Tester

This is a library function with no UI. All verification is through `cargo test` and `dotnet test`. The diff algorithm uses item `content()` / `Content` as identity — items with identical content across variants are matched by that key.
