# Phase 15: Conformance Hardening — UAT

## Result: PASS (8/8)

| # | Test | Expected | Status |
|---|------|----------|--------|
| 1 | 28 TOML vectors exist in spec/conformance/required/ | 13 scoring + 6 slicing + 4 placing + 5 pipeline = 28 | PASS |
| 2 | quota-basic.toml is in required tier | File at spec/conformance/required/slicing/quota-basic.toml, NOT in optional/ | PASS |
| 3 | All vectors byte-exact with Rust crate | `diff -r` produces zero output | PASS |
| 4 | Rust conformance test suite passes | 28/28 required tests pass | PASS |
| 5 | Scoring vectors cover all 8 scorer types | recency, priority, kind, tag, frequency, reflexive, composite, scaled | PASS |
| 6 | Pipeline vectors cover pinned items | pinned-items.toml exercises bypass path | PASS |
| 7 | Spec directory structure matches Rust crate | 4 subdirectories: scoring/, slicing/, placing/, pipeline/ | PASS |
| 8 | VERIFICATION.md passes all checks | 6/6 must-haves verified | PASS |

Tested: 2026-03-14
