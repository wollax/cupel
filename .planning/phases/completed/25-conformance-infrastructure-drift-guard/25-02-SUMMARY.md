# Plan 25-02 Summary: Add CI conformance drift guard to ci-rust.yml

## Outcome

Complete. All must-haves satisfied. Commit: `8b59c41`

## Tasks Completed

| Task | Description | Status |
|------|-------------|--------|
| Task 1 | Expand paths trigger to include `spec/**` | Done |
| Task 2 | Add conformance drift guard step | Done |

## Changes Made

**`.github/workflows/ci-rust.yml`** — 67 lines (was 53)

- Added `'spec/**'` to both `push.paths` and `pull_request.paths` arrays
- Inserted "Conformance drift guard" step between Cache and Format steps
- Step checks `spec/conformance/required` vs `crates/cupel/conformance/required` via `diff -rq`
- Step checks `spec/conformance/optional` vs `crates/cupel/conformance/optional` via `diff -rq`
- Both pairs guarded by `-d` existence checks (optional/ does not yet exist)
- Failure exits with code 1 and prints a remediation hint

**Incidental fix** — pre-existing conformance drift resolved in same commit:
- `crates/cupel/conformance/required/scoring/composite-weighted.toml` — synced to canonical
- `crates/cupel/conformance/required/slicing/knapsack-basic.toml` — synced to canonical

## Must-Haves Verification

- [x] `spec/**` appears in both push.paths and pull_request.paths
- [x] Conformance drift guard step exists between Cache and Format steps
- [x] Step checks both required/ and optional/ directory pairs
- [x] Both directory pairs have `-d` existence guards
- [x] Error messages include remediation hints
- [x] Step exits with code 1 on drift detection
- [x] Original workflow entries (checkout, toolchain, cache, format, clippy, test, deny) are unchanged
- [x] Artifact `.github/workflows/ci-rust.yml` is 67 lines (min_lines: 55)

## Phase Contribution

Satisfies CONF-02 (drift guard mechanism) and CI-03 (drift guard runs in CI). The pre-commit hook (`conformance/required/` vs `crates/cupel/conformance/required/`) and the new CI step (`spec/conformance/required/` vs `crates/cupel/conformance/required/`) now form a two-layer guard: local commits are blocked, and PRs are blocked, when conformance vectors diverge.
