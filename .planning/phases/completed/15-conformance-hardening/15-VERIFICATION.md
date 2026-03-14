# Phase 15 Verification: Conformance Hardening

## Status: PASS

All must-have criteria verified against the actual codebase on 2026-03-14.

---

## Criterion 1: 28 conformance vectors in `spec/conformance/required/`

**Result: PASS**

```
scoring/   13 files
slicing/    6 files
placing/    4 files
pipeline/   5 files
Total:     28 files
```

All 28 `.toml` files present under `spec/conformance/required/` across the four subdirectories.

**Verification command:**
```
find spec/conformance/required -name "*.toml" | wc -l  → 28
```

---

## Criterion 2: `quota-basic.toml` in required tier (not optional)

**Result: PASS**

- `spec/conformance/required/slicing/quota-basic.toml` exists.
- `spec/conformance/optional/` directory does not exist; no optional tier was created.
- QuotaSlice is unambiguously in the required conformance tier.

---

## Criterion 3: All 28 vectors byte-exact with Rust crate copies

**Result: PASS**

Spec tree: `spec/conformance/required/`
Rust crate: `/Users/wollax/Git/personal/assay/crates/assay-cupel/tests/conformance/required/`

**Verification command:**
```
diff -r spec/conformance/required/ [rust-crate]/required/  → (no output, exit code 0)
```

Zero differences across all 28 files.

---

## Criterion 4: Phase 01 VERIFICATION.md exists

**Result: PASS**

File present: `.planning/phases/completed/01-project-scaffold-core-models/VERIFICATION.md` (10.2 KB)

This file pre-existed Phase 15 (confirmed in CONTEXT.md) and was not modified by this phase.

---

## Criterion 5: Phase 09 VERIFICATION.md exists

**Result: PASS**

File present: `.planning/phases/completed/09-serialization-json-package/VERIFICATION.md`

This file pre-existed Phase 15 (confirmed in CONTEXT.md) and was not modified by this phase.

---

## Rust Conformance Test Results

**Result: PASS — 28/28 tests passed**

```
cargo test --package assay-cupel conformance
→ 28 passed (2 suites, 0.00s)
```

All required conformance vectors are exercised by the Rust test runner and pass.

---

## Phase Summary

Phase 15 established the spec source tree (`spec/conformance/required/`) as the canonical source for all 28 conformance vectors. The Rust crate copies were already correct; Phase 15 brought the spec tree into parity.

| Deliverable | Status |
|---|---|
| 13 scoring vectors in spec tree | Done (Plan 15-01) |
| 6 slicing vectors in spec tree (incl. quota-basic) | Done (Plan 15-02) |
| 4 placing vectors in spec tree | Done (Plan 15-02) |
| 5 pipeline vectors in spec tree | Done (Plan 15-03) |
| Byte-exact parity with Rust crate | Confirmed |
| Rust conformance test suite: 28/28 pass | Confirmed |
| Phase 01 VERIFICATION.md | Pre-existing, confirmed present |
| Phase 09 VERIFICATION.md | Pre-existing, confirmed present |
