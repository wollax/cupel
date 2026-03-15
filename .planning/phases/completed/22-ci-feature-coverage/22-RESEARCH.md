# Phase 22: CI Feature Coverage — Research

## Current State

### CI Workflow (`ci-rust.yml`)
- **Test step** (line 41): `cargo test --manifest-path crates/cupel/Cargo.toml` — runs default features only
- **Clippy step** (line 38): `cargo clippy --manifest-path crates/cupel/Cargo.toml -- -D warnings` — lints default features only
- **Format step**: Feature-agnostic, no change needed
- **Deny step**: Feature-agnostic, no change needed

### Release Workflow (`release-rust.yml`)
- **Test step** (line 42): identical to CI — default features only
- **Clippy step** (line 39): identical to CI — default features only

### Feature Inventory
- Only feature: `serde` (enables `dep:serde` + `chrono/serde`)
- No default features (`default = []`)
- `tests/serde.rs` is gated with `#![cfg(feature = "serde")]` — **33 tests silently skipped** on every CI run
- `examples/serde_roundtrip.rs` has `required-features = ["serde"]`
- 15 `#[cfg(feature = "serde")]` blocks in `src/` across 5 files — none linted by Clippy in CI

## Standard Stack

Use `cargo test --all-features` and `cargo clippy --all-features`. **Confidence: HIGH**

No additional tools, crates, or GitHub Actions needed. The existing workflow structure supports this with single-line changes.

## Architecture Patterns

### Pattern: Additive feature-matrix steps

Add `--all-features` variants as **separate steps** rather than replacing the default-features steps. This catches:
1. Regressions in the default (no-feature) build
2. Regressions in feature-gated code
3. Accidental unconditional imports of optional dependencies

**Confidence: HIGH** — This is the standard Rust CI pattern for library crates with optional features.

```yaml
# Existing step (keep as-is)
- name: Test (default features)
  run: cargo test --manifest-path crates/cupel/Cargo.toml

# New step
- name: Test (all features)
  run: cargo test --all-features --manifest-path crates/cupel/Cargo.toml
```

Same pattern for Clippy:

```yaml
# Existing step (keep as-is)
- name: Clippy (default features)
  run: cargo clippy --manifest-path crates/cupel/Cargo.toml -- -D warnings

# New step
- name: Clippy (all features)
  run: cargo clippy --all-features --manifest-path crates/cupel/Cargo.toml -- -D warnings
```

### Pattern: Naming convention

Use parenthetical suffixes for clarity in GitHub Actions UI: `Test (default features)` / `Test (all features)`. Rename the existing step from `Test` to `Test (default features)` for symmetry.

**Confidence: HIGH**

## Don't Hand-Roll

| Problem | Use Instead |
|---------|-------------|
| Feature matrix via matrix strategy | Separate named steps — only 2 feature sets, matrix is overkill |
| Custom scripts to enumerate features | `--all-features` flag — covers all declared features |
| Conditional step execution | Always run both; they're fast for a small crate |

## Common Pitfalls

| Pitfall | Mitigation |
|---------|------------|
| Replacing default test with all-features test instead of adding | Keep both steps — default-features test catches unconditional dependency leaks |
| Forgetting to update Clippy alongside test | Update both Clippy and test in both workflows |
| Cache invalidation when adding `--all-features` | `Swatinem/rust-cache` handles this automatically; feature flags change the compilation fingerprint within the same target dir |
| Forgetting release workflow | Both `ci-rust.yml` and `release-rust.yml` need identical changes |

## Code Examples

### ci-rust.yml changes (4 edits)

Rename existing Clippy step and add all-features variant:
```yaml
      - name: Clippy (default features)
        run: cargo clippy --manifest-path crates/cupel/Cargo.toml -- -D warnings

      - name: Clippy (all features)
        run: cargo clippy --all-features --manifest-path crates/cupel/Cargo.toml -- -D warnings
```

Rename existing Test step and add all-features variant:
```yaml
      - name: Test (default features)
        run: cargo test --manifest-path crates/cupel/Cargo.toml

      - name: Test (all features)
        run: cargo test --all-features --manifest-path crates/cupel/Cargo.toml
```

### release-rust.yml changes (identical 4 edits)

Same pattern applied to the `test` job.

## Scope Assessment

**Minimal scope.** Two YAML files, 4 line renames + 4 line additions each. No Rust code changes. No new dependencies. No new workflow files.

**Estimated complexity: LOW** — Straight-line edits with clear verification (CI run shows 33 additional tests passing).

## Verification Strategy

After changes, a CI run should show:
- `Test (default features)`: same test count as before (28 conformance + unit tests)
- `Test (all features)`: previous count + 33 serde tests
- `Clippy (all features)`: clean pass (no new warnings from feature-gated code)
