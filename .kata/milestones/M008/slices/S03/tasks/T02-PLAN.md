---
estimated_steps: 6
estimated_files: 3
---

# T02: Add Rust spec section, update CHANGELOG, validate R058

**Slice:** S03 — Crate packaging, spec addendum, and R058 validation
**Milestone:** M008

## Description

This task closes M008 by: (1) appending a Rust-specific section to `spec/src/integrations/opentelemetry.md`, (2) updating `CHANGELOG.md` under `[Unreleased]`, (3) marking R058 validated in `.kata/REQUIREMENTS.md`, and (4) confirming no regressions. All three edits use `edit` — never overwrite the existing content.

## Steps

1. Read the current tail of `spec/src/integrations/opentelemetry.md` to identify the exact last line (the Conformance Notes section ends the file) — this is the insertion point.
2. Append a `## Rust (cupel-otel)` section to `spec/src/integrations/opentelemetry.md` after the Conformance Notes. Include:
   - A note that the Rust `cupel-otel` crate uses source name `"cupel"` (distinct from .NET `"Wollax.Cupel"` — D163); callers must configure `"cupel"` in their OTel SDK tracer provider
   - Cargo.toml snippet: `cupel-otel = "0.1"` under `[dependencies]`
   - Minimal usage example: import `CupelOtelTraceCollector` and `CupelVerbosity`, create the collector, pass to `pipeline.run_traced`
   - Three implementation notes:
     - Explicit `.end()` is mandatory — opentelemetry 0.27 `Span` does not auto-end on drop (D169)
     - `SpanData` import path: `opentelemetry_sdk::export::trace::SpanData` (not `opentelemetry_sdk::trace::SpanData`)
     - Unknown `ExclusionReason` variants in `cupel.exclusion.reason` use `"Unknown"` via a `_ => "Unknown"` match arm — forward-compatible with future spec variants
3. Append bullets to `CHANGELOG.md` under `[Unreleased] ### Added`:
   - `Rust: \`cupel-otel\` crate — \`CupelOtelTraceCollector\` implementing \`TraceCollector\` with three verbosity tiers (StageOnly, StageAndExclusions, Full); emits \`cupel.pipeline\` root span and five \`cupel.stage.*\` child spans with exact \`cupel.*\` attributes`
   - `Rust: \`TraceCollector::on_pipeline_completed\` hook — defaulted no-op method on the trait; called by \`Pipeline::run_traced\` at completion; provides \`StageTraceSnapshot\` slice for structured end-of-run handoff`
   - `Spec: Rust-specific section in \`spec/src/integrations/opentelemetry.md\` documenting \`cupel-otel\` source name, Cargo.toml snippet, and implementation notes`
4. In `.kata/REQUIREMENTS.md`, change R058 `Status: active` → `Status: validated` and add a `Validation:` field. Reference: `crates/cupel-otel/tests/integration.rs` — 5 integration tests (source_name_is_cupel, hierarchy_root_and_five_stage_spans, stage_only_no_events, stage_and_exclusions_emits_exclusion_events, full_emits_included_item_events_on_place); `cd crates/cupel-otel && cargo test --all-targets` passes; `cd crates/cupel && cargo test --all-targets` passes; `cd crates/cupel-otel && cargo package --no-verify` exits 0.
5. Run `cd crates/cupel-otel && cargo test --all-targets` — must pass with 5 tests, 0 failures.
6. Run `cd crates/cupel && cargo test --all-targets` — must pass with 0 regressions. Run `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` — must exit 0.

## Must-Haves

- [ ] `spec/src/integrations/opentelemetry.md` has a `## Rust (cupel-otel)` section with source name `"cupel"` documented and a Cargo.toml snippet; existing .NET content is unchanged
- [ ] The three implementation notes are present in the Rust section: explicit `.end()`, correct `SpanData` import path, `_ => "Unknown"` arm
- [ ] `CHANGELOG.md` has ≥2 new bullets under `[Unreleased] ### Added` covering `cupel-otel` crate and `on_pipeline_completed` hook
- [ ] R058 in `.kata/REQUIREMENTS.md` has `Status: validated` and a `Validation:` field referencing `crates/cupel-otel/tests/integration.rs`
- [ ] `cd crates/cupel-otel && cargo test --all-targets` passes (5 tests, 0 failures)
- [ ] `cd crates/cupel && cargo test --all-targets` passes (0 regressions)
- [ ] `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` exits 0

## Verification

- `grep -q 'Rust (cupel-otel)' spec/src/integrations/opentelemetry.md` → exits 0
- `grep -q '"cupel"' spec/src/integrations/opentelemetry.md` → exits 0 (Rust source name present)
- `grep -q 'Wollax.Cupel' spec/src/integrations/opentelemetry.md` → exits 0 (existing .NET content preserved)
- `cd crates/cupel-otel && cargo test --all-targets` → 5 tests pass
- `cd crates/cupel && cargo test --all-targets` → all tests pass, 0 failures
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exit 0
- Visual check: R058 block in REQUIREMENTS.md shows `Status: validated`

## Observability Impact

- Signals added/changed: None (documentation and metadata changes only)
- How a future agent inspects this: `grep 'Status: validated' .kata/REQUIREMENTS.md` for R058 validation state; `grep 'Rust (cupel-otel)' spec/src/integrations/opentelemetry.md` for spec addendum presence; `cargo test --all-targets` for regression baseline
- Failure state exposed: if any edit accidentally breaks a Rust/spec syntax reference, `cargo test` or `cargo clippy` will surface it immediately with file:line precision

## Inputs

- `spec/src/integrations/opentelemetry.md` — 139 lines; ends at Conformance Notes; edit-only, preserve all existing content
- `CHANGELOG.md` — `[Unreleased] ### Added` section has existing M007 bullets; append after them
- `.kata/REQUIREMENTS.md` — R058 block; change Status field and add Validation field
- T01-PLAN.md / T01 output — `cargo package --no-verify` already passes; this task adds the documentation and validation closure
- S02-SUMMARY.md forward intelligence — source name `"cupel"`, explicit `.end()` (D169), `SpanData` at `opentelemetry_sdk::export::trace::SpanData`, `_ => "Unknown"` arm — all three must appear in the spec section
- D163 — canonical source name `"cupel"` (not `"Wollax.Cupel"`)
- D170 — root span carries only `cupel.budget.max_tokens` and `cupel.verbosity`

## Expected Output

- `spec/src/integrations/opentelemetry.md` — 139 lines of existing .NET content preserved + new `## Rust (cupel-otel)` section appended at end
- `CHANGELOG.md` — `[Unreleased] ### Added` extended with ≥2 bullets about `cupel-otel` and `on_pipeline_completed`
- `.kata/REQUIREMENTS.md` — R058 marked `validated` with a Validation field
- M008 milestone closed — all success criteria met
