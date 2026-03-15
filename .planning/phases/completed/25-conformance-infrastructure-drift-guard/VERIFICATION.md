# Phase 25 Verification

**Phase goal:** Establish the CI conformance drift guard, fix misleading comments in 5 existing conformance vector TOML files, and create the diagnostics conformance vector schema with an example vector.

**Verified:** 2026-03-15

---

## Plan 01 — Fix misleading comments in 5 conformance vector files

### Truths

| Truth | Status | Evidence |
|---|---|---|
| All 5 TOML files have accurate, non-misleading comments | PASS | No TBD/TODO/FIXME/misleading markers found in any of the 5 files |
| spec/ and crates/ copies are byte-identical after fixes | PASS | `diff` returned clean for all 5 files |

### Artifacts

| Artifact | Min Lines | Actual Lines | Status |
|---|---|---|---|
| `spec/conformance/required/slicing/knapsack-basic.toml` | 20 | 56 | PASS |
| `spec/conformance/required/scoring/composite-weighted.toml` | 55 | 68 | PASS |
| `spec/conformance/required/pipeline/pinned-items.toml` | 35 | 80 | PASS |
| `spec/conformance/required/placing/u-shaped-basic.toml` | 12 | 40 | PASS |
| `spec/conformance/required/placing/u-shaped-equal-scores.toml` | 12 | 35 | PASS |

### Key Links

- `spec/conformance/required/` ↔ `crates/cupel/conformance/required/`: `diff -rq` returned clean — all files byte-identical.

---

## Plan 02 — CI drift guard

### Truths

| Truth | Status | Evidence |
|---|---|---|
| CI fails when spec/ and crates/ conformance diverge | PASS | Drift guard step uses `diff -rq ... \|\| { echo ...; exit 1; }` — exits 1 on drift |
| spec/** changes trigger CI | PASS | `paths:` block includes `'spec/**'` for both push and pull_request triggers |
| Drift guard runs before test steps | PASS | "Conformance drift guard" step appears before "Test (default features)" and "Test (serde)" steps |

### Artifacts

| Artifact | Min Lines | Actual Lines | Status |
|---|---|---|---|
| `.github/workflows/ci-rust.yml` | 55 | 66 | PASS |

### Key Links

- `spec/conformance/required/` ↔ `crates/cupel/conformance/required/` via `diff -rq`: implemented in drift guard step (lines 38–41).
- `spec/conformance/optional/` ↔ `crates/cupel/conformance/optional/` via `diff -rq` guarded by `-d` flag: implemented in drift guard step (lines 42–45), guarded by `[ -d spec/conformance/optional ]` — correct, as the optional directories do not yet exist.

---

## Plan 03 — Diagnostics conformance vector schema + example

### Truths

| Truth | Status | Evidence |
|---|---|---|
| `[expected.diagnostics]` schema documented in spec conformance format chapter | PASS | format.md lines 152–180 document the full schema table including included items, excluded items with reasons, and summary counts |
| At least one example diagnostics vector exists in both spec/ and crates/ | PASS | `diagnostics-budget-exceeded.toml` present in both locations, byte-identical |
| Schema supports included items, excluded items with reasons, and summary counts | PASS | format.md schema table covers all three sub-tables; example vector exercises all three |

### Artifacts

| Artifact | Min Lines | Actual Lines | Status |
|---|---|---|---|
| `spec/src/conformance/format.md` | 180 | 270 | PASS |
| `spec/conformance/required/pipeline/diagnostics-budget-exceeded.toml` | 40 | 70 | PASS |
| `crates/cupel/conformance/required/pipeline/diagnostics-budget-exceeded.toml` | 40 | 70 | PASS |

### Key Links

- `format.md` → `diagnostics-budget-exceeded.toml`: format.md lines 182–270 embed the complete example vector inline, referencing the same file.
- `spec/` → `crates/` copy: `diff` returned clean — files are byte-identical.

---

## Summary

**PASS — Phase 25 goal achieved.**

All three plans delivered their must-haves. All artifacts exist at or above minimum line counts. All byte-identity requirements between spec/ and crates/ hold. The CI drift guard is correctly wired: spec/** triggers CI, the guard runs before tests, and it exits 1 on divergence. The optional/ guard is correctly gated — the directories do not yet exist, and the guard uses `[ -d ]` checks before diffing.

No gaps found.
