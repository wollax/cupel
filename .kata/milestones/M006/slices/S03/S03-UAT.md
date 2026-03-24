# S03: Integration proof + summaries — UAT

**Milestone:** M006
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S03 is a final-assembly and documentation slice with no new user-facing runtime behavior. All verification is mechanically checkable via automated tests (`cargo test`, `dotnet test`), build-level analysis (`dotnet build` PublicAPI analyzer), and file existence checks. No UI or human-experience surface was introduced.

## Preconditions

- Rust toolchain available (`cargo`, `clippy`)
- .NET SDK available (`dotnet`)
- Working directory: `crates/cupel/` for Rust commands; project root for .NET commands

## Smoke Test

Run `cargo test --all-targets` from `crates/cupel/` and `dotnet test --solution Cupel.slnx` from the project root. Both should exit 0 with no failures.

## Test Cases

### 1. Rust composition test passes

1. `cd crates/cupel`
2. `cargo test count_quota_composition -- --nocapture`
3. **Expected:** `test count_quota_composition_quota_slice_inner ... ok` — test passes; no panic; output shows `report.excluded` contains `CountCapExceeded { .. }` variant

### 2. .NET composition test passes

1. From project root: `dotnet test --solution Cupel.slnx --filter "CountQuotaComposition"`
2. **Expected:** `CountQuotaCompositionTests` test class runs with 1 test passing; 0 failed

### 3. CountCapExceeded in Rust pipeline output

1. `cd crates/cupel`
2. `cargo test count_quota_composition -- --nocapture 2>&1 | grep -i "CountCapExceeded\|ok\|FAILED"`
3. **Expected:** `ok` line for the test; no `FAILED`; test assertions confirm `CountCapExceeded` is present in `report.excluded`

### 4. PublicAPI build audit clean

1. From project root: `dotnet build Cupel.slnx 2>&1 | grep -E "error|warning" | wc -l`
2. **Expected:** `0` — no errors or warnings from PublicAPI analyzer or any other source

### 5. Full regression green

1. From `crates/cupel/`: `cargo test --all-targets`
2. From project root: `dotnet test --solution Cupel.slnx`
3. **Expected:** Rust: 159 passed, 0 failed; .NET: all tests passed, 0 failed

## Edge Cases

### R061 is validated in REQUIREMENTS.md

1. `grep -A3 "R061" .kata/REQUIREMENTS.md | grep "validated"`
2. **Expected:** Returns at least one line containing `validated`

### M006 summary files exist

1. `ls .kata/milestones/M006/M006-SUMMARY.md .kata/milestones/M006/slices/S01/S01-SUMMARY.md`
2. **Expected:** Both files listed without error

## Failure Signals

- Any test failure in `cargo test --all-targets` or `dotnet test --solution Cupel.slnx`
- Any warning/error in `dotnet build Cupel.slnx` output (PublicAPI analyzer surfaces missing declarations as errors)
- `count_quota_composition_quota_slice_inner` test absent from `cargo test` output
- `CountCapExceeded` not matching in `report.excluded` (assertion panic in Rust test)
- `grep "R061" .kata/REQUIREMENTS.md | grep "validated"` returning no lines

## Requirements Proved By This UAT

- R061 — CountQuotaSlice count-based quota enforcement: composition with QuotaSlice (the one integration not proven in S01 or S02) is exercised in both languages; CountCapExceeded visible in both languages via real dry_run()/DryRun(); PublicAPI complete; full test suite green; R061 marked validated in REQUIREMENTS.md

## Not Proven By This UAT

- Runtime behavior under load or with large item sets — this UAT uses small fixed test fixtures
- Caller ergonomics / developer experience of the CountQuotaSlice API — no human authoring experience is assessed here
- S01 and S02 conformance scenarios individually — those were proven in S01/S02 and are not re-run here (only the composition test is new to S03)

## Notes for Tester

All checks are automated. There is no manual interaction surface for this slice. Running the two test commands and checking `REQUIREMENTS.md` is sufficient to confirm the UAT. The composition tests are intentionally minimal (one test per language) — they prove the wiring works, not exhaustive edge cases (those are in the S01/S02 conformance tests).
