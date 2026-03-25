---
id: T02
parent: S03
milestone: M008
provides:
  - spec/src/integrations/opentelemetry.md — `## Rust (cupel-otel)` section with source name `"cupel"`, Cargo.toml snippet, usage example, and three implementation notes (explicit .end(), SpanData import path, `_ => "Unknown"` arm)
  - CHANGELOG.md — three new bullets under `[Unreleased] ### Added` covering cupel-otel crate, on_pipeline_completed hook, and spec addendum
  - .kata/REQUIREMENTS.md — R058 promoted from `active` to `validated` with full Validation field
  - M008 milestone closed — all success criteria met across S01, S02, S03
key_files:
  - spec/src/integrations/opentelemetry.md
  - CHANGELOG.md
  - .kata/REQUIREMENTS.md
key_decisions:
  - "No new decisions — all spec content derives from D163 (source name 'cupel'), D169 (explicit .end()), D170 (root span attributes), and S02 implementation facts"
patterns_established:
  - "Spec language-specific sections follow the pattern: source name note → Cargo.toml snippet → usage example → implementation notes"
observability_surfaces:
  - "`grep 'Status: validated' .kata/REQUIREMENTS.md` — confirms R058 validation state"
  - "`grep 'Rust (cupel-otel)' spec/src/integrations/opentelemetry.md` — confirms spec addendum presence"
  - "`cd crates/cupel-otel && cargo test --all-targets` — regression baseline (5 tests)"
duration: 10min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T02: Add Rust spec section, update CHANGELOG, validate R058

**Spec addendum, CHANGELOG update, and R058 closure completing M008 — all documentation and validation checks pass.**

## What Happened

Appended a `## Rust (cupel-otel)` section to `spec/src/integrations/opentelemetry.md` after the existing Conformance Notes. The section documents:
- Source name `"cupel"` (distinct from .NET `"Wollax.Cupel"`, D163) with a tracer provider config example
- Cargo.toml snippet (`cupel-otel = "0.1"`)
- Minimal usage example (create `CupelOtelTraceCollector`, call `pipeline.run_traced`, call `on_pipeline_completed`)
- Three implementation notes: explicit `.end()` (D169), correct `SpanData` import path (`opentelemetry_sdk::export::trace::SpanData`), and `_ => "Unknown"` match arm for forward-compatible exclusion reason handling

Added three bullets to `CHANGELOG.md` under `[Unreleased] ### Added`: one for the `cupel-otel` crate, one for the `on_pipeline_completed` hook, one for the spec Rust section.

Updated R058 in `.kata/REQUIREMENTS.md`: changed `Status: active` → `Status: validated`, replaced `Validation: unmapped` with a full validation field referencing the 5 integration tests in `crates/cupel-otel/tests/integration.rs` and the passing `cargo test` and `cargo package --no-verify` commands.

## Verification

- `grep -q 'Rust (cupel-otel)' spec/src/integrations/opentelemetry.md` → exits 0 ✓
- `grep -q '"cupel"' spec/src/integrations/opentelemetry.md` → exits 0 ✓
- `grep -q 'Wollax.Cupel' spec/src/integrations/opentelemetry.md` → exits 0 (existing .NET content preserved) ✓
- `cd crates/cupel-otel && cargo test --all-targets` → 5 tests, 0 failures ✓
- `cd crates/cupel && cargo test --all-targets` → 81+9+48+... tests, 0 failures ✓
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exits 0 ✓
- R058 block in REQUIREMENTS.md shows `Status: validated` ✓

## Diagnostics

- `grep 'Status: validated' .kata/REQUIREMENTS.md` — confirms R058 closed
- `grep 'Rust (cupel-otel)' spec/src/integrations/opentelemetry.md` — confirms spec section present
- `cargo test --all-targets` from either crate — regression baseline

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `spec/src/integrations/opentelemetry.md` — `## Rust (cupel-otel)` section appended; all 139 lines of existing .NET content preserved
- `CHANGELOG.md` — three bullets added under `[Unreleased] ### Added`
- `.kata/REQUIREMENTS.md` — R058 `Status: validated`, `Validation:` field added
