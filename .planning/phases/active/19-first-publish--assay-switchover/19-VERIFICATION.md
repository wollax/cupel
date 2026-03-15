# Phase 19 Verification: First Publish & Assay Switchover

**Date:** 2026-03-15
**Status:** passed

## Must-Have Verification

| # | Must-Have | Status | Evidence |
|---|-----------|--------|----------|
| 1 | cupel live on crates.io | PASS | `name=cupel version=1.0.0 desc=Context window management pipeline for LLM applications` (crates.io API confirmed) |
| 2 | OIDC trusted publishing configured | PASS | `release-rust.yml` contains `rust-lang/crates-io-auth-action@v1` (line 85) and `id-token: write` (line 14); user confirmed trusted publisher configured on crates.io settings page |
| 3 | assay uses registry dependency | PASS | `assay/Cargo.toml` line 12: `cupel = "1.0.0"` (registry dep, no `path =`) |
| 4 | assay-cupel directory deleted | PASS | `ls /Users/wollax/Git/personal/assay/crates/assay-cupel/` → `DELETED` |
| 5 | patch.crates-io documented | PASS | `assay/CONTRIBUTING.md` lines 26 and 33 contain `[patch.crates-io]` section with usage instructions |

## Summary

Score: 5/5 must-haves verified
Status: passed
