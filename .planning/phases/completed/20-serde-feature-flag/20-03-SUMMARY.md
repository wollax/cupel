---
phase: "20"
plan: "03"
subsystem: "tests, packaging"
tags: ["serde", "testing", "version-bump", "feature-additivity"]
requires: ["20-01", "20-02"]
provides: ["serde-test-coverage", "v1.1.0-release"]
affects: ["crates/cupel/tests/", "crates/cupel/Cargo.toml"]
tech-stack:
  added: []
  patterns: ["cfg-gated integration tests", "roundtrip serde verification", "validation-on-deserialize rejection"]
key-files:
  created:
    - "crates/cupel/tests/serde.rs"
  modified:
    - "crates/cupel/Cargo.toml"
decisions: []
metrics:
  duration: "~3 min"
  completed: "2026-03-15"
---

# Phase 20 Plan 03: Serde Tests & Version Bump Summary

## Outcome

All tasks completed successfully. Comprehensive serde test suite with 24 tests covering all 7 serializable types. Version bumped to 1.1.0. Feature is purely additive — all existing tests pass without `--features serde`.

## Test Coverage

| Category | Count | Description |
|----------|-------|-------------|
| Roundtrip | 8 | Serialize-deserialize equality for all types |
| Validation rejection | 8 | Proves constructors cannot be bypassed via deserialization |
| Unknown field rejection | 4 | Ensures deny_unknown_fields is enforced |
| Default handling | 1 | Verifies ContextItem defaults survive roundtrip |
| Wire format | 3 | Validates exact JSON shape (bare strings, PascalCase enums, string keys) |

## Verification Matrix

- `cargo test` (no features): 28 passed, serde tests correctly gated
- `cargo test --features serde`: 52 passed (28 existing + 24 serde)
- `cargo clippy --features serde --tests`: clean
- `cargo clippy --tests`: clean
- `cargo doc --features serde --no-deps`: builds without warnings

## Commits

- `3832c90` — test(20-03): comprehensive serde test suite
- `bbcaeea` — chore(20-03): bump version to 1.1.0
