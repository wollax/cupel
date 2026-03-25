# S03: Crate packaging, spec addendum, and R058 validation — UAT

**Milestone:** M008
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S03 is a library packaging and documentation slice with no runtime service. All verification surfaces are command-line outputs (cargo package, cargo test, grep) and file content checks. No user-facing UI or live service to exercise.

## Preconditions

- `crates/cupel-otel/` is on a branch with README.md and LICENSE present
- Rust toolchain installed (`cargo`, `rustc`)
- Working directory is the project root

## Smoke Test

```
cd crates/cupel-otel && cargo package --no-verify
```

Expected: exits 0, output includes "Packaged 8 files".

## Test Cases

### 1. Packaging — file manifest is complete

```bash
cd crates/cupel-otel && cargo package --list
```

**Expected:** exits 0; output includes `README.md` and `LICENSE` on separate lines.

### 2. Packaging — no-verify succeeds

```bash
cd crates/cupel-otel && cargo package --no-verify
```

**Expected:** exits 0; output includes "Packaged N files".

### 3. Integration tests — all 5 pass

```bash
cd crates/cupel-otel && cargo test --all-targets
```

**Expected:** `test result: ok. 5 passed; 0 failed`.

### 4. Core cupel — no regressions

```bash
cd crates/cupel && cargo test --all-targets
```

**Expected:** all test suites report `ok. N passed; 0 failed`.

### 5. Clippy clean

```bash
cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings
```

**Expected:** exits 0 with no warnings or errors.

### 6. Spec addendum present

```bash
grep -c '"cupel"' spec/src/integrations/opentelemetry.md
```

**Expected:** output ≥ 1.

```bash
grep 'Rust (cupel-otel)' spec/src/integrations/opentelemetry.md
```

**Expected:** line found (exits 0).

### 7. R058 validated in REQUIREMENTS.md

```bash
grep -A2 'R058' .kata/REQUIREMENTS.md | grep 'Status: validated'
```

**Expected:** line found (exits 0).

## Edge Cases

### Packaging fails with "does not appear to exist"

If `cargo package --list` exits non-zero with "readme does not appear to exist" or "LICENSE does not appear to exist":
- Check that `crates/cupel-otel/README.md` and `crates/cupel-otel/LICENSE` exist on disk
- Check that `include` in `crates/cupel-otel/Cargo.toml` lists both files

**Expected:** files exist and `cargo package --list` exits 0.

## Failure Signals

- `cargo package --list` exits non-zero → README.md or LICENSE missing or not in `include`
- `cargo package --no-verify` exits non-zero → Cargo.toml metadata issue (description, license field, etc.)
- `cargo test --all-targets` in cupel-otel reports failures → S02 integration regression
- `cargo test --all-targets` in cupel reports failures → core crate regression
- `grep` for `"cupel"` in opentelemetry.md returns 0 → spec addendum not written or source name wrong

## Requirements Proved By This UAT

- R058 — proves that `cupel-otel` is packageable (`cargo package --no-verify` exits 0), that the Rust-specific spec section exists with the correct source name, and that all 5 integration tests pass across all three verbosity tiers

## Not Proven By This UAT

- Publishing to crates.io (requires `cargo publish`; deferred until release decision)
- Runtime behavior when connected to a live OTel collector (e.g., Jaeger, Honeycomb) — only the in-memory SDK exporter is exercised by tests
- Cross-version compatibility with opentelemetry crates other than the pinned version in Cargo.toml

## Notes for Tester

- Use `--no-verify` for `cargo package` — the path dep on `cupel = { path = "../cupel" }` is not resolvable from the tarball, which would cause the default `--verify` pass to fail. This is expected and correct for local development.
- The 5 integration tests in `crates/cupel-otel/tests/integration.rs` are the primary regression surface; their names map directly to the three verbosity tiers and two structural properties (source name, span hierarchy).
