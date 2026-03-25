# S03: Crate packaging, spec addendum, and R058 validation

**Goal:** Make `cargo package --no-verify` exit 0 for `cupel-otel`; add a Rust-specific section to `spec/src/integrations/opentelemetry.md`; update `CHANGELOG.md`; mark R058 validated.
**Demo:** `cargo package --no-verify` succeeds from `crates/cupel-otel/`; the spec has a `## Rust (cupel-otel)` section with correct source name `"cupel"`, Cargo.toml snippet, and usage example; R058 status is `validated` in `REQUIREMENTS.md` — M008 milestone complete.

## Must-Haves

- `crates/cupel-otel/README.md` exists with intro, dev-dependency snippet, usage example (`CupelOtelTraceCollector::new(CupelVerbosity::StageOnly)`), and verbosity tier summary
- `crates/cupel-otel/LICENSE` exists (copy of `crates/cupel/LICENSE`)
- `cd crates/cupel-otel && cargo package --list` exits 0 and output includes `README.md` and `LICENSE`
- `cd crates/cupel-otel && cargo package --no-verify` exits 0
- `spec/src/integrations/opentelemetry.md` has a new `## Rust (cupel-otel)` section documenting: source name `"cupel"` (distinct from .NET `"Wollax.Cupel"`), Cargo.toml dependency snippet, minimal usage example, explicit `.end()` requirement (D169), `SpanData` correct import path, `_ => "Unknown"` arm for unknown `ExclusionReason` variants
- `CHANGELOG.md` has ≥2 new bullets under `[Unreleased] ### Added` covering the `cupel-otel` crate and `TraceCollector::on_pipeline_completed` hook
- R058 `Status` changed from `active` to `validated` in `.kata/REQUIREMENTS.md` with a `Validation:` field referencing `crates/cupel-otel/tests/integration.rs`
- `cd crates/cupel-otel && cargo test --all-targets` passes (5 integration tests, 0 regressions)
- `cd crates/cupel && cargo test --all-targets` passes (0 regressions)
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` exits 0

## Proof Level

- This slice proves: final-assembly
- Real runtime required: no (library crate; no running service)
- Human/UAT required: no

## Verification

- `cd crates/cupel-otel && cargo package --list` exits 0; output includes `README.md` and `LICENSE`
- `cd crates/cupel-otel && cargo package --no-verify` exits 0
- `cd crates/cupel-otel && cargo test --all-targets` → 5 integration tests pass, 0 failures
- `cd crates/cupel && cargo test --all-targets` → all tests pass, 0 regressions
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exit 0
- `grep -c '"cupel"' spec/src/integrations/opentelemetry.md` → ≥1 (Rust source name documented)
- `grep 'Status: validated' .kata/REQUIREMENTS.md | grep R058` → matches (or visual check of R058 block)

## Observability / Diagnostics

- Runtime signals: none (library packaging — no runtime signals)
- Inspection surfaces: `cargo package --list` enumerates included files and exits non-zero with explicit "file not found" message when a declared `include` entry is absent; `cargo test` names each failing test function; `cargo clippy` names each lint violation with file:line
- Failure visibility: `cargo package` exits 101 with "readme does not appear to exist" or "LICENSE does not appear to exist"; both are unambiguous and actionable
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `crates/cupel-otel/` (S02 output — crate compiles, 5 integration tests pass); `spec/src/integrations/opentelemetry.md` (139 lines .NET content; new section appended, existing content preserved); `CHANGELOG.md` (existing `[Unreleased]` section)
- New wiring introduced in this slice: none (packaging and documentation; no runtime wiring)
- What remains before the milestone is truly usable end-to-end: nothing — this slice closes M008

## Tasks

- [x] **T01: Create cupel-otel README.md and LICENSE, verify cargo package exits 0** `est:20m`
  - Why: `cargo package` currently exits 101 because `README.md` and `LICENSE` are listed in Cargo.toml's `include` but do not exist in `crates/cupel-otel/`; both files must exist before packaging can succeed
  - Files: `crates/cupel-otel/README.md` (new), `crates/cupel-otel/LICENSE` (new)
  - Do: Read `crates/cupel-testing/README.md` for structure template; write `crates/cupel-otel/README.md` with intro paragraph, `[dev-dependencies]` Cargo.toml snippet (`cupel-otel = { version = "0.1", path = "../cupel-otel" }`), usage example showing tracer provider setup and `CupelOtelTraceCollector::new(CupelVerbosity::StageOnly)` passed to `pipeline.run_traced`, verbosity tier table (`StageOnly` / `StageAndExclusions` / `Full` with recommended environments); copy `crates/cupel/LICENSE` to `crates/cupel-otel/LICENSE`; run `cargo package --list` and then `cargo package --no-verify` from `crates/cupel-otel/`
  - Verify: `cd crates/cupel-otel && cargo package --list` exits 0 with `README.md` and `LICENSE` in output; `cd crates/cupel-otel && cargo package --no-verify` exits 0
  - Done when: `cargo package --no-verify` exits 0 with no errors

- [x] **T02: Add Rust spec section, update CHANGELOG, validate R058** `est:20m`
  - Why: M008 Definition of Done requires the spec to document `cupel-otel`, CHANGELOG to be updated, and R058 marked validated; without these three changes the milestone is not closed
  - Files: `spec/src/integrations/opentelemetry.md`, `CHANGELOG.md`, `.kata/REQUIREMENTS.md`
  - Do: (1) Append a `## Rust (cupel-otel)` section to `spec/src/integrations/opentelemetry.md` (after Conformance Notes) documenting: source name is `"cupel"` (D163) — distinct from .NET `"Wollax.Cupel"`, Cargo.toml snippet, minimal usage example, three implementation notes: explicit `.end()` is mandatory (D169), `SpanData` imports from `opentelemetry_sdk::export::trace::SpanData`, unknown `ExclusionReason` variants return `"Unknown"` via `_ => "Unknown"` match arm; (2) Append bullets to `CHANGELOG.md [Unreleased] ### Added` for: `cupel-otel` crate, `TraceCollector::on_pipeline_completed` hook, and spec Rust section; (3) In `.kata/REQUIREMENTS.md` change R058 `Status: active` to `Status: validated` and add a `Validation:` field referencing `crates/cupel-otel/tests/integration.rs` and both `cargo test --all-targets` commands; (4) Run `cargo test --all-targets` in `crates/cupel-otel` and `crates/cupel`; (5) Run `cargo clippy --all-targets -- -D warnings` in `crates/cupel-otel`
  - Verify: `grep -q '"cupel"' spec/src/integrations/opentelemetry.md`; R058 block in REQUIREMENTS.md shows `Status: validated`; `cd crates/cupel-otel && cargo test --all-targets` passes; `cd crates/cupel && cargo test --all-targets` passes; clippy exits 0
  - Done when: spec has Rust section, CHANGELOG updated, R058 validated, all tests pass, clippy clean

## Files Likely Touched

- `crates/cupel-otel/README.md` (new)
- `crates/cupel-otel/LICENSE` (new)
- `spec/src/integrations/opentelemetry.md`
- `CHANGELOG.md`
- `.kata/REQUIREMENTS.md`
