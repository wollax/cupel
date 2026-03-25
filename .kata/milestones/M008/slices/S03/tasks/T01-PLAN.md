---
estimated_steps: 5
estimated_files: 2
---

# T01: Create cupel-otel README.md and LICENSE, verify cargo package exits 0

**Slice:** S03 — Crate packaging, spec addendum, and R058 validation
**Milestone:** M008

## Description

`cargo package --list` currently exits 101 because `crates/cupel-otel/Cargo.toml` declares `readme = "README.md"` and lists both `README.md` and `LICENSE` in `include`, but neither file exists in `crates/cupel-otel/`. This task creates both files and verifies `cargo package --no-verify` exits 0. The `--no-verify` flag is required because `cupel-otel` depends on `cupel = { path = "../cupel" }` — the default verifier tries to build the package from the tarball where the relative path is no longer resolvable.

## Steps

1. Read `crates/cupel-testing/README.md` to understand the companion-crate README structure used in this repo.
2. Write `crates/cupel-otel/README.md` with:
   - Intro paragraph: what `cupel-otel` is and that it's a companion crate to `cupel`
   - Dev-dependencies Cargo.toml snippet (`cupel-otel = { version = "0.1" }`)
   - Minimal usage example showing tracer provider registration and `CupelOtelTraceCollector::new(CupelVerbosity::StageOnly)` passed to `pipeline.run_traced(&items, &budget, &mut collector)`
   - Verbosity tier table: `StageOnly` (production), `StageAndExclusions` (staging), `Full` (development only)
3. Copy `crates/cupel/LICENSE` to `crates/cupel-otel/LICENSE` (the MIT license text, same copyright holder).
4. Run `cd crates/cupel-otel && cargo package --list` and confirm exit 0; output must include `README.md` and `LICENSE`.
5. Run `cd crates/cupel-otel && cargo package --no-verify` and confirm exit 0.

## Must-Haves

- [ ] `crates/cupel-otel/README.md` exists and is non-empty; contains a dev-dependencies Cargo.toml snippet and at least one code block showing `CupelOtelTraceCollector::new(CupelVerbosity::StageOnly)`
- [ ] `crates/cupel-otel/LICENSE` exists (copy of `crates/cupel/LICENSE`)
- [ ] `cd crates/cupel-otel && cargo package --list` exits 0; output lists `README.md` and `LICENSE`
- [ ] `cd crates/cupel-otel && cargo package --no-verify` exits 0

## Verification

- `cd crates/cupel-otel && cargo package --list` → exits 0; grep for `README.md` and `LICENSE` in output
- `cd crates/cupel-otel && cargo package --no-verify` → exits 0; no error output

## Observability Impact

- Signals added/changed: None (no runtime behavior changed)
- How a future agent inspects this: `cargo package --list` is the definitive check — it enumerates exactly which files will be included in the tarball; a non-zero exit with an explicit "file does not appear to exist" message identifies any remaining missing declared files
- Failure state exposed: `cargo package` exits non-zero with a precise error naming the missing file; this is sufficient to localize the problem without additional tooling

## Inputs

- `crates/cupel-otel/Cargo.toml` — declares `readme = "README.md"`, `license = "MIT"`, and `include = [..., "README.md", "LICENSE"]`; these declarations cause packaging to fail when the files are absent
- `crates/cupel-testing/README.md` — canonical companion-crate README template for this repo (parallel crate, same pattern)
- `crates/cupel/LICENSE` — source MIT license to copy
- S02-SUMMARY.md forward intelligence — `exclusion_reason_name` is `pub` currently; README should not document it as part of the public API; `is_enabled()` unconditionally returns `true`; usage example should show `on_pipeline_completed` being called via `pipeline.run_traced`

## Expected Output

- `crates/cupel-otel/README.md` — new file; intro + dev-dep snippet + usage example + verbosity tier table
- `crates/cupel-otel/LICENSE` — new file; MIT license text copied from `crates/cupel/LICENSE`
- `cargo package --no-verify` exits 0 from `crates/cupel-otel/` — the packaging gate is unblocked
