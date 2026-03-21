# S05: CI Quality Hardening — UAT

**Milestone:** M001
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S05 makes only CI configuration and cargo-deny config changes — no runtime behavior is introduced. All correctness is verified by local CLI commands that replicate what CI will run. Human review of the YAML diffs and a `cargo deny check` run is the complete proof surface.

## Preconditions

- Rust toolchain installed (`rustup`, stable channel)
- `cargo-deny` installed (`cargo install cargo-deny` or present in PATH)
- Working directory: `/Users/wollax/Git/personal/cupel` (or the repo root)
- Branch: `kata/M001/S05` (or main after merge)

## Smoke Test

```bash
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
```

Expected: exits 0, no warnings printed. If this passes, the core deliverable works.

## Test Cases

### 1. Clippy covers all targets (default features)

```bash
cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings
```

**Expected:** exits 0; no warnings from `src/`, `tests/`, or `examples/`.

### 2. Clippy covers all targets (serde feature)

```bash
cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings
```

**Expected:** exits 0; no warnings from any target under the serde feature set.

### 3. cargo-deny check passes with unmaintained config

```bash
cd crates/cupel && cargo deny check
```

**Expected:** prints `advisories ok, bans ok, licenses ok, sources ok`; exits 0. May print `warning[license-not-encountered]` lines for unused license allowances — these are pre-existing and benign.

### 4. CI YAML diff confirms exactly --all-targets added to clippy steps

```bash
git show HEAD~1 -- .github/workflows/ci-rust.yml | grep "^[+-]" | grep -v "^---\|^+++"
git show HEAD~2 -- .github/workflows/release-rust.yml | grep "^[+-]" | grep -v "^---\|^+++"
```

**Expected:** each file shows exactly 2 changed lines — each adding `--all-targets` to a `cargo clippy` invocation. No other lines changed.

### 5. deny.toml diff confirms exactly one line added

```bash
git show HEAD~1 -- crates/cupel/deny.toml | grep "^[+-]" | grep -v "^---\|^+++"
```

**Expected:** exactly one added line: `+unmaintained = "workspace"` under `[advisories]`.

## Edge Cases

### Unmaintained advisory fires in the future

Simulate by adding a known-unmaintained crate to `Cargo.toml`, then run `cargo deny check`.

**Expected:** exits non-zero; prints the advisory ID, crate name, and version. CI step fails loudly.

### New file added to examples/

Add a file with a clippy lint violation to `crates/cupel/examples/`, run clippy.

**Expected:** `--all-targets` picks it up; exits non-zero; prints the lint name, file, and line. CI step fails — confirming the lint scope expansion is real.

## Failure Signals

- `cargo clippy` exits non-zero: a pre-existing or new warning in `tests/` or `examples/` is now exposed. Check the lint name and file path in the output.
- `cargo deny check` exits non-zero on `advisories`: an unmaintained advisory fired for a workspace dep. Advisory ID and crate name are printed.
- `cargo deny check` exits non-zero on `licenses`: a new dependency introduced a non-allowlisted license.
- CI step "Clippy (default features)" or "Clippy (serde)" fails: same as local clippy failure — inspect the step log for the lint.

## Requirements Proved By This UAT

- R003 — `cargo clippy --all-targets -- -D warnings` runs in CI (both feature sets); `deny.toml` flags unmaintained workspace deps; local execution proves CI will behave identically.

## Not Proven By This UAT

- Whether a real unmaintained advisory will fire in CI in the future (depends on upstream dep advisories — no current violations)
- True "warn-without-fail" behavior for unmaintained deps (requires CI action `command-arguments` config — not implemented in S05)
- S07 lint cleanliness after new Rust code is added (S07 is responsible for verifying no new warnings are introduced)

## Notes for Tester

- The `warning[license-not-encountered]` lines from `cargo deny check` are expected and pre-existing — they indicate allowlisted licenses that no current dependency uses. They are not failures.
- `unmaintained = "workspace"` uses a scope value, not a severity value — this is correct for cargo-deny 0.19.0. The `yanked` field still uses `"deny"` (severity) — both coexist correctly.
- S05 makes zero code changes. All artifacts are YAML and TOML config files. If the local commands pass and the diffs are minimal, the slice is complete.
