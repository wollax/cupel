# S03: Crate packaging, spec addendum, and R058 validation — Research

**Date:** 2026-03-24
**Domain:** Rust crate packaging, spec editing, R058 closure
**Confidence:** HIGH

## Summary

S03 is a low-risk documentation and packaging slice. All implementation work (S01 + S02) is complete and verified. The slice has exactly three deliverables: (1) fix `cargo package` so it exits 0 for `cupel-otel`, (2) add a Rust-specific section to `spec/src/integrations/opentelemetry.md`, and (3) update `CHANGELOG.md` and mark R058 validated in `REQUIREMENTS.md`.

The only genuine blocker is that `cargo package --list` currently exits 101 because `README.md` is listed in `Cargo.toml` but the file does not exist in `crates/cupel-otel/`. Creating it is the critical-path first task. Once README.md exists, `cargo package --list` (and the full `cargo package` run) should succeed; the `include` glob in `Cargo.toml` already covers all necessary files.

The spec addendum is straightforward editorial work. `spec/src/integrations/opentelemetry.md` is currently .NET-centric — it documents `"Wollax.Cupel"` as the source name and uses .NET code examples. A new section must document the Rust `cupel-otel` crate: correct source name (`"cupel"`, not `"Wollax.Cupel"`), Cargo.toml snippet, and a minimal usage example. Three implementation notes (from S02 forward intelligence) belong in the spec: the mandatory `.end()` call (D169), `SpanData` import path, and the `_ => "Unknown"` arm for future `ExclusionReason` variants.

## Recommendation

Execute in order: (1) create `crates/cupel-otel/README.md`, (2) run `cargo package --list` to verify all files are found, (3) run `cargo package --no-verify` to produce the `.crate` tarball (or `cargo package` with `--no-verify` if the cupel path-dep triggers the verifier), (4) add the Rust section to the spec, (5) update CHANGELOG.md under `[Unreleased]`, (6) mark R058 validated in `REQUIREMENTS.md`, (7) run `cargo test --all-targets` in both crates to confirm no regressions, (8) commit.

Use `--no-verify` for `cargo package` because `cupel-otel` depends on `cupel = { version = "1.1", path = "../cupel" }` — the path dep is fine for the Cargo.toml but `cargo package` by default verifies the package builds from the tarball (which cannot resolve a relative path); `--no-verify` skips that build step.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| README content structure | `crates/cupel-testing/README.md` as template | Direct parallel: same pattern (companion crate, path dep, usage example) — copy and adapt |
| Spec addendum structure | Existing `spec/src/integrations/opentelemetry.md` .NET section | Write a parallel "Rust (`cupel-otel`)" section immediately after the .NET section, matching format |
| CHANGELOG entry format | Existing entries under `[Unreleased]` in `CHANGELOG.md` | Use the same bullet format as M007 entries already there |
| R058 validation entry | R061 validation field in `REQUIREMENTS.md` as example | Follow the exact prose pattern: summarise what was proven, list test file references |

## Existing Code and Patterns

- `crates/cupel-otel/Cargo.toml` — `readme = "README.md"` declared but file missing; this is the blocker for `cargo package`; `include` list already has `"README.md"` and `"LICENSE"`
- `crates/cupel-testing/README.md` — canonical companion-crate README: intro paragraph, quick install table, usage snippet, link to docs.rs; copy this structure
- `crates/cupel-otel/src/lib.rs` — `exclusion_reason_name` is currently `pub` (unintentional, noted in S02 deviations as "moved to private helper" but the code shows `pub fn`); verify and keep as-is or change to `pub(crate)` — not a packaging blocker but worth auditing
- `spec/src/integrations/opentelemetry.md` — 139 lines; ends with "Conformance Notes" section; new Rust section goes at the end or as a new "## Language-Specific Notes" section
- `CHANGELOG.md` — `[Unreleased]` section under `### Added` contains M007 entries; append new entries there
- `REQUIREMENTS.md` — R058 is currently `Status: active`; change to `Status: validated`; add `Validation:` field following R061's format
- `LICENSE` at `crates/cupel/` — `crates/cupel-otel/Cargo.toml` has `license = "MIT"` but no `LICENSE` file in `crates/cupel-otel/`; check if `cargo package --list` requires it (it is in the `include` list, so it must exist); **create `crates/cupel-otel/LICENSE`** by copying from `crates/cupel/LICENSE`

## Constraints

- `cargo package` resolves the `include` glob relative to the crate root (`crates/cupel-otel/`); `README.md` and `LICENSE` must exist at that path, not the repo root
- Path dependency `cupel = { path = "../cupel" }` requires `--no-verify` for `cargo package` to succeed (the verifier tries to build from the tarball where the relative path is gone)
- `spec/src/integrations/opentelemetry.md` exists; do NOT overwrite it — use `edit` to append a new section
- R058 validation note must reference the integration test file `crates/cupel-otel/tests/integration.rs` and both `cargo test` commands used for verification
- The spec Conformance Notes section references `"Wollax.Cupel"` as the ActivitySource name — the new Rust section must clarify the Rust crate uses `"cupel"` (D163); do not change the existing .NET conformance note

## Common Pitfalls

- **Missing `LICENSE` in `crates/cupel-otel/`** — `Cargo.toml` lists `LICENSE` in `include`; if the file is absent, `cargo package --list` will error. Copy from `crates/cupel/LICENSE`.
- **`cargo package` without `--no-verify` fails on path deps** — the verifier tries to build from the tarball; the tarball cannot resolve `../cupel`; always use `--no-verify` here.
- **Overwriting `spec/src/integrations/opentelemetry.md` instead of appending** — the file has 139 lines of .NET content that must be preserved; use `edit` to append only.
- **`exclusion_reason_name` visibility** — currently `pub fn` in `lib.rs`; this is exported in the public API. If S03 changes it to `pub(crate)`, that's a minor API change but not a packaging blocker. Leave as-is unless clippy complains about dead_code.
- **Spec source name mismatch** — the existing Conformance Notes say `"Wollax.Cupel"` is the name for the .NET ActivitySource. The Rust section MUST clearly state the Rust crate uses `"cupel"`. These are different implementations with different source names; do not conflate them.
- **`#[serial]` on integration tests is load-bearing** — do not remove or modify test annotations; any packaging verification that runs tests must preserve them.

## Open Risks

- `crates/cupel-otel/` has no `LICENSE` file yet — almost certain to block `cargo package --list`; confirm immediately and copy it before attempting package commands.
- `exclusion_reason_name` is `pub` — may cause an unintentional public API surface; not a blocker but worth noting in DECISIONS if changed.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust crate packaging | n/a | none needed — standard cargo commands |

## Sources

- `crates/cupel-otel/Cargo.toml` — confirmed `readme = "README.md"` declared; `cargo package --list` exits 101 with "readme does not appear to exist" (local verification)
- `spec/src/integrations/opentelemetry.md` — 139 lines; .NET-only content; no Rust section yet (local verification)
- S02-SUMMARY.md forward intelligence — explicit list of what S03 needs to know: standalone crate, explicit `.end()` (D169), `SpanData` import path, `_ => "Unknown"` arm
- S02-ASSESSMENT.md — spec addendum scope confirmed; root span attributes spec-only (two attributes per D170)
