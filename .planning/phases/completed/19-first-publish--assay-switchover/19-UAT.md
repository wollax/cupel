# Phase 19 UAT: First Publish & Assay Switchover

**Date:** 2026-03-15
**Status:** PASSED (6/6)

## Tests

| # | Test | Expected | Status |
|---|------|----------|--------|
| 1 | cupel 1.0.0 on crates.io | Crate page shows correct metadata, README rendered | PASS (minor: README refers to "coming in phase 17") |
| 2 | docs.rs build | https://docs.rs/cupel shows generated documentation | PASS |
| 3 | GitHub Release | rust-v1.0.0 release marked as Latest | PASS |
| 4 | OIDC workflow config | release-rust.yml uses crates-io-auth-action, no static token | PASS |
| 5 | assay registry dependency | assay Cargo.toml uses `cupel = "1.0.0"` (not path) | PASS |
| 6 | assay tests pass | cargo test in assay passes against published crate | PASS (716 passed, 3 ignored) |

## Notes

- README has a minor cosmetic issue: references "coming in phase 17" in usage section. Not a blocker — can be fixed in Phase 21 (docs.rs Documentation & Examples).
