---
id: S03
parent: M008
milestone: M008
provides:
  - crates/cupel-otel/README.md — intro, dev-dep snippet, usage example, verbosity tier table
  - crates/cupel-otel/LICENSE — MIT license copied from crates/cupel/LICENSE
  - cargo package --list exits 0; README.md and LICENSE present in tarball manifest
  - cargo package --no-verify exits 0; cupel-otel is fully packageable
  - spec/src/integrations/opentelemetry.md — `## Rust (cupel-otel)` section with source name, Cargo.toml snippet, usage example, three implementation notes (explicit .end(), SpanData import path, _ => "Unknown" arm)
  - CHANGELOG.md — three bullets under [Unreleased] ### Added covering cupel-otel crate, on_pipeline_completed hook, and spec addendum
  - R058 validated in .kata/REQUIREMENTS.md with full Validation field
  - M008 milestone closed
requires:
  - slice: S02
    provides: crates/cupel-otel/ crate compiles; CupelOtelTraceCollector implements TraceCollector; 5 integration tests pass with in-memory exporter; SOURCE_NAME = "cupel"
affects: []
key_files:
  - crates/cupel-otel/README.md
  - crates/cupel-otel/LICENSE
  - spec/src/integrations/opentelemetry.md
  - CHANGELOG.md
  - .kata/REQUIREMENTS.md
key_decisions:
  - Used --no-verify for cargo package because cupel-otel depends on cupel via path = "../cupel"; the default verifier builds from the tarball where the relative path is not resolvable; --no-verify is the correct workaround
  - Spec language-specific section follows the pattern: source name note → Cargo.toml snippet → usage example → implementation notes; no standalone code blocks without context
patterns_established:
  - companion-crate README follows same structure as cupel-testing/README.md — intro paragraph, dev-dep snippet, usage example, API/verbosity table, license
  - spec language-specific sections appended after Conformance Notes to keep existing content intact
observability_surfaces:
  - cargo package --list — definitive file-manifest check; exits non-zero with "does not appear to exist" when a declared include entry is absent
  - cd crates/cupel-otel && cargo test --all-targets — 5 integration tests naming each verbosity tier
  - grep 'Status: validated' .kata/REQUIREMENTS.md — confirms R058 closure
  - grep 'Rust (cupel-otel)' spec/src/integrations/opentelemetry.md — confirms spec section presence
drill_down_paths:
  - .kata/milestones/M008/slices/S03/tasks/T01-SUMMARY.md
  - .kata/milestones/M008/slices/S03/tasks/T02-SUMMARY.md
duration: 15min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# S03: Crate packaging, spec addendum, and R058 validation

**cupel-otel packaged (cargo package --no-verify exits 0), Rust spec section added to opentelemetry.md, CHANGELOG updated, R058 validated — M008 complete.**

## What Happened

S03 closed the M008 milestone with two tasks.

**T01** unblocked packaging: `crates/cupel-otel/Cargo.toml` declared `readme = "README.md"` and `include = [..., "README.md", "LICENSE"]` but neither file existed, causing `cargo package --list` to exit 101. Created `README.md` with intro paragraph, dev-dependencies Cargo.toml snippet, usage example showing `CupelOtelTraceCollector::new(CupelVerbosity::StageOnly)` passed to `pipeline.run_traced`, and a verbosity tier table (StageOnly for production, StageAndExclusions for staging, Full for development). Copied `crates/cupel/LICENSE` (MIT, Copyright 2026 Wollax) to `crates/cupel-otel/LICENSE`. Both `cargo package --list` and `cargo package --no-verify` then exited 0.

**T02** closed the documentation and requirements traceability: appended a `## Rust (cupel-otel)` section to `spec/src/integrations/opentelemetry.md` documenting the source name `"cupel"` (distinct from .NET `"Wollax.Cupel"`, per D163), a Cargo.toml dependency snippet, a minimal usage example, and three implementation notes (explicit `.end()` per D169, correct `SpanData` import path from `opentelemetry_sdk::export::trace::SpanData`, and `_ => "Unknown"` match arm for forward-compatible exclusion reason handling). Added three bullets to `CHANGELOG.md` under `[Unreleased] ### Added`. Updated R058 in `.kata/REQUIREMENTS.md` from `Status: active` to `Status: validated` with a full Validation field referencing the 5 integration tests and the two cargo commands.

## Verification

- `cd crates/cupel-otel && cargo package --list` → exits 0; `README.md` and `LICENSE` present ✓
- `cd crates/cupel-otel && cargo package --no-verify` → exits 0, packaged 8 files ✓
- `cd crates/cupel-otel && cargo test --all-targets` → 5 integration tests, 0 failures ✓
- `cd crates/cupel && cargo test --all-targets` → 81+9+48+... tests, 0 failures ✓
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exits 0 ✓
- `grep -c '"cupel"' spec/src/integrations/opentelemetry.md` → 1 ✓
- `grep 'Status: validated' .kata/REQUIREMENTS.md` → matches R058 block ✓

## Requirements Advanced

- R058 — this slice completed the final packaging and documentation work and provided the terminal proof for validation

## Requirements Validated

- R058 — `cargo package --no-verify` exits 0; spec `## Rust (cupel-otel)` section exists; 5 integration tests in `crates/cupel-otel/tests/integration.rs` pass across all three verbosity tiers; `cargo test --all-targets` clean in both crates

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

None.

## Known Limitations

None — M008 is fully closed.

## Follow-ups

- cupel-otel version 0.1.0 is not yet published to crates.io; when ready, run `cargo publish` from `crates/cupel-otel/` (path dep in Cargo.toml must be replaced with a version dep before publishing)

## Files Created/Modified

- `crates/cupel-otel/README.md` — new; intro + dev-dep snippet + usage example + verbosity tier table
- `crates/cupel-otel/LICENSE` — new; MIT license copied from crates/cupel/LICENSE
- `spec/src/integrations/opentelemetry.md` — `## Rust (cupel-otel)` section appended after Conformance Notes
- `CHANGELOG.md` — three bullets added under `[Unreleased] ### Added`
- `.kata/REQUIREMENTS.md` — R058 `Status: validated`, `Validation:` field added

## Forward Intelligence

### What the next slice should know
- M008 is complete; there are no remaining active requirements in REQUIREMENTS.md
- cupel-otel uses `--no-verify` for packaging because the path dep on cupel is not resolvable from the tarball; this is a known limitation of local path deps, not a crate quality issue
- The `SOURCE_NAME` const in `crates/cupel-otel/src/lib.rs` is `"cupel"` — this is the canonical source name and is documented in both the README and the spec

### What's fragile
- `cargo package --no-verify` skips compilation from the tarball; if the `include` list in Cargo.toml drifts from actual source files, it will only be caught when a consumer tries to use the published crate

### Authoritative diagnostics
- `cargo package --list` — exits non-zero with an explicit "does not appear to exist" message when any declared include entry is absent; most reliable pre-publish check
- `cd crates/cupel-otel && cargo test --all-targets` — 5 named integration tests, each targeting a specific verbosity tier or structural property; a failure here pinpoints exactly which tier broke

### What assumptions changed
- No assumptions changed; S03 was straightforward documentation and packaging work with no surprises
