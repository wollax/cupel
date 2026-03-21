# S05: CI Quality Hardening — Research

**Date:** 2026-03-21

## Summary

S05 owns R003: add `--all-targets` to both CI clippy steps and set `unmaintained = "warn"` in `deny.toml`. Both changes are mechanical and low-risk. The key pre-flight finding is that the baseline is already clean — `cargo clippy --all-targets -- -D warnings` passes with zero warnings on both default and serde feature sets locally. S07 (Rust Quality Hardening, depends on S05) therefore starts from a clean `--all-targets` baseline rather than needing to fix a backlog of pre-existing warnings introduced by the scope expansion.

The slice touches three files: `ci-rust.yml`, `release-rust.yml`, and `deny.toml`. No Rust source code changes. No new Cargo dependencies.

## Recommendation

Make exactly the changes described in the two open issues. Replace the two clippy steps in both CI files (add `--all-targets`) and add one line to `deny.toml`. Verify locally before committing. Nothing clever needed here.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Linting integration tests + examples | `--all-targets` flag on cargo clippy | Built-in; no wrapper needed |
| Unmaintained dep detection | `unmaintained = "warn"` in `[advisories]` | cargo-deny already runs in CI via EmbarkStudios/cargo-deny-action@v2 |

## Existing Code and Patterns

- `.github/workflows/ci-rust.yml` — Two clippy steps (lines ~38–41): `cargo clippy --manifest-path crates/cupel/Cargo.toml -- -D warnings` and `--features serde` variant. Both need `--all-targets` inserted before `--`. The `Deny` step at the bottom uses `EmbarkStudios/cargo-deny-action@v2` with `manifest-path: crates/cupel/Cargo.toml` and will pick up `deny.toml` changes automatically.
- `.github/workflows/release-rust.yml` — Identical clippy steps in the `test` job (lines ~34–37). Issue tracker explicitly asks for consistency here; apply the same `--all-targets` addition.
- `crates/cupel/deny.toml` — `[advisories]` section currently has only `version = 2` and `yanked = "deny"`. One line addition: `unmaintained = "warn"`.
- `crates/cupel/tests/` — `conformance.rs`, `serde.rs` (integration tests that will now be linted)
- `crates/cupel/examples/` — `basic_pipeline.rs`, `quota_slicing.rs`, `serde_roundtrip.rs` (examples that will now be linted)

## Constraints

- MSRV 1.85.0 — `--all-targets` has been stable since Rust 1.0; no version concern.
- `deny.toml` uses `version = 2` schema — the `unmaintained` key is valid in v2.
- No benchmarks directory exists (`crates/cupel/benches/` absent) — `--all-targets` will not break on a missing benches dir; cargo skips it silently.
- `EmbarkStudios/cargo-deny-action@v2` in CI reads `deny.toml` from the `manifest-path` directory automatically; no action input changes needed.

## Common Pitfalls

- **Forgetting `release-rust.yml`** — The issue `2026-03-14-clippy-all-targets.md` explicitly calls out both files. Missing `release-rust.yml` means a release build could pass clippy in a way that diverges from CI.
- **Adding `--all-targets` only to default-features step** — The serde step also needs it; linting `tests/serde.rs` requires `--features serde --all-targets`.
- **`unmaintained = "deny"` instead of `"warn"`** — The issue and R003 both specify `"warn"`. Using `"deny"` would block CI if any transitive dep is ever flagged unmaintained, which is overly aggressive for a library.
- **Ordering in deny.toml** — `unmaintained` belongs under `[advisories]`, not `[bans]`. Placing it elsewhere will produce a parse error or be silently ignored.

## Open Risks

- **Future unmaintained advisory hits** — Once `unmaintained = "warn"` is in place, a new RustSec advisory for an unmaintained crate in the (small) transitive dependency closure could turn a future CI run yellow. This is intended behavior (warn, not deny), but worth noting for S07 planning: check `cargo deny check` output after adding the line.
- **`--all-targets` lint scope expansion** — Zero warnings confirmed locally today (2026-03-21). However, S07 will add new Rust source files; those files must also pass `--all-targets` by the time S07 merges. S05 establishes the gate; S07 must not violate it.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| GitHub Actions / cargo-deny | none needed | n/a — changes are mechanical YAML/TOML edits |

## Sources

- Issue `2026-03-14-clippy-all-targets.md` — exact file/line targets and scope (both CI files)
- Issue `2026-03-14-cargo-deny-unmaintained-warn.md` — exact key and value for deny.toml
- Local verification: `cargo clippy --all-targets -- -D warnings` and `cargo clippy --all-targets --features serde -- -D warnings` both exit 0 on current HEAD (2026-03-21)
- `cargo deny check` output: advisories ok, bans ok, licenses ok, sources ok — no existing issues
