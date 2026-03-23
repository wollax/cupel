---
id: S05
parent: M001
milestone: M001
provides:
  - CI now runs `cargo clippy --all-targets` for both default and serde feature sets (integration tests and examples are linted)
  - `cargo-deny` advisory config explicitly covers unmaintained workspace dependencies (`unmaintained = "workspace"`)
  - All four `cargo clippy` invocations in `ci-rust.yml` and `release-rust.yml` include `--all-targets`
  - R003 fully satisfied — clean baseline for S07 Rust Quality Hardening
requires: []
affects:
  - S07
key_files:
  - .github/workflows/ci-rust.yml
  - .github/workflows/release-rust.yml
  - crates/cupel/deny.toml
key_decisions:
  - "D030: cargo-deny 0.19.0 `unmaintained` field uses scope values (all/workspace/transitive/none), not severity values (deny/warn/allow); used `workspace` as the best config-only approximation to the issue intent"
  - "--all-targets placed before --manifest-path in cargo clippy invocations (matches cargo flag ordering convention)"
patterns_established:
  - none
observability_surfaces:
  - "`cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` — lints lib, tests, examples; exits 0 when clean"
  - "`cargo deny check` (from crates/cupel/) — advisories/bans/licenses/sources; exits non-zero if an unmaintained advisory fires for a workspace dep"
drill_down_paths:
  - .kata/milestones/M001/slices/S05/tasks/T01-SUMMARY.md
  - .kata/milestones/M001/slices/S05/tasks/T02-SUMMARY.md
duration: 15min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
---

# S05: CI Quality Hardening

**Expanded CI clippy scope to `--all-targets` across all four invocations in two workflow files, and added explicit `unmaintained = "workspace"` advisory coverage to `deny.toml` — all checks exit 0 with zero warnings.**

## What Happened

S05 made three targeted changes to CI configuration with no code changes:

**T01** added `--all-targets` to all four `cargo clippy` invocations in `ci-rust.yml` and `release-rust.yml` (two steps each: default features and serde feature). Previously, `crates/cupel/tests/` and `crates/cupel/examples/` were excluded from lint coverage. Both local checks exited 0 immediately — no pre-existing warnings were lurking in test or example code.

**T02** added `unmaintained = "workspace"` to the `[advisories]` section of `crates/cupel/deny.toml`. The task plan specified `unmaintained = "warn"` but cargo-deny 0.19.0 changed the field schema from severity values to scope values — `"warn"` is not a valid value. Used `"workspace"` as the closest valid approximation: it explicitly covers direct workspace dependencies and will fail CI loudly if an unmaintained advisory fires. `cargo deny check` exits 0 with advisories/bans/licenses/sources all ok.

## Verification

All slice-level checks passed:

| Check | Result |
|---|---|
| `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` | EXIT 0, no warnings |
| `cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings` | EXIT 0, no warnings |
| `cargo deny check` (from crates/cupel/) | EXIT 0, advisories ok, bans ok, licenses ok, sources ok |
| `ci-rust.yml` — 2 clippy steps updated with `--all-targets` | Confirmed |
| `release-rust.yml` — 2 clippy steps updated with `--all-targets` | Confirmed |
| `deny.toml` — exactly 1 line added under `[advisories]` | Confirmed |

## Requirements Advanced

- R003 — `cargo clippy --all-targets` now runs in CI for both feature sets; `deny.toml` explicitly configures unmaintained advisory coverage

## Requirements Validated

- R003 — All evidence is present: four `--all-targets` insertions in CI YAML, `unmaintained = "workspace"` in deny.toml, local verification exits 0 for all three checks. R003 is fully satisfied by this slice.

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

**T02 used `unmaintained = "workspace"` instead of `unmaintained = "warn"`** (as specified in the plan). cargo-deny 0.19.0 changed the `unmaintained` field to accept scope values (`all/workspace/transitive/none`) rather than severity levels (`deny/warn/allow`). `"workspace"` is the closest valid value: it scopes advisory checks to direct workspace dependencies and will fail CI non-zero on a match. True "warn-but-don't-fail" requires passing `-W unmaintained-advisory` at the CI action invocation level, which is out of scope for S05 (see D030).

## Known Limitations

- True "warn without blocking CI" for unmaintained advisories requires `command-arguments: "--warn unmaintained-advisory"` in the `EmbarkStudios/cargo-deny-action@v2` step. This is not configured — a future unmaintained advisory for a workspace dep will fail CI (non-zero), not just warn.
- `--all-targets` now lints `examples/` — any future example file must be clippy-clean or the CI step fails.

## Follow-ups

- S07 (Rust Quality Hardening) depends on this clean baseline and will fix any pre-existing warnings surfaced by `--all-targets` (none found — the baseline is clean as of S05 completion).
- If true warn-without-fail behavior is needed for unmaintained advisories, extend the CI action with `command-arguments: "--warn unmaintained-advisory"` and remove `unmaintained = "workspace"` from deny.toml (or keep it as belt-and-suspenders).

## Files Created/Modified

- `.github/workflows/ci-rust.yml` — two clippy steps updated with `--all-targets`
- `.github/workflows/release-rust.yml` — two clippy steps updated with `--all-targets`
- `crates/cupel/deny.toml` — `unmaintained = "workspace"` added under `[advisories]`

## Forward Intelligence

### What the next slice should know
- The clippy baseline is clean: `--all-targets` surfaces zero warnings against the current codebase; S07 starts from a genuinely clean slate, not a hidden-warning state.
- cargo-deny 0.19.0 changed the `unmaintained` field schema. If deny.toml is extended further, check the 0.19.x changelog — other fields may have changed too.

### What's fragile
- `unmaintained = "workspace"` will hard-fail CI if any direct workspace dep gets an unmaintained advisory. This is intentional but may surprise if a transitive dep is the concern.

### Authoritative diagnostics
- `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` — run this first when S07 introduces new code; it covers lib + tests + examples in one command.
- `cargo deny check` from `crates/cupel/` — advisory ID and crate name printed on failure; always explicit about which check category failed.

### What assumptions changed
- Original plan assumed `unmaintained = "warn"` would be valid cargo-deny syntax — it is not in 0.19.0. Scope values replaced severity values for this specific field.
