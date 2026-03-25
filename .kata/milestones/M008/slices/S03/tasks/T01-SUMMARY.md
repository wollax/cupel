---
id: T01
parent: S03
milestone: M008
provides:
  - crates/cupel-otel/README.md — intro, dev-dep snippet, usage example, verbosity tier table
  - crates/cupel-otel/LICENSE — MIT license copied from crates/cupel/LICENSE
  - cargo package --list exits 0; README.md and LICENSE present in tarball manifest
  - cargo package --no-verify exits 0; packaging gate unblocked for cupel-otel
key_files:
  - crates/cupel-otel/README.md
  - crates/cupel-otel/LICENSE
key_decisions:
  - Used --no-verify for cargo package because cupel-otel depends on cupel via path = "../cupel"; the default verifier builds from the tarball where the relative path is not resolvable; --no-verify is the correct workaround per plan
patterns_established:
  - companion-crate README follows same structure as cupel-testing/README.md — intro paragraph, dev-dep snippet, usage example, API/verbosity table, license
observability_surfaces:
  - cargo package --list is the definitive inspection surface; exits non-zero with explicit "file does not appear to exist" when a declared include entry is absent
duration: 5min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T01: Create cupel-otel README.md and LICENSE, verify cargo package exits 0

**README.md and LICENSE added to crates/cupel-otel/; cargo package --list and --no-verify both exit 0.**

## What Happened

`crates/cupel-otel/Cargo.toml` declared `readme = "README.md"` and `include = [..., "README.md", "LICENSE"]` but neither file existed, causing `cargo package --list` to exit 101.

Created `crates/cupel-otel/README.md` modelled on `crates/cupel-testing/README.md` with:
- Intro paragraph describing cupel-otel as a companion crate to cupel
- Dev-dependencies Cargo.toml snippet
- Usage example showing `CupelOtelTraceCollector::new(CupelVerbosity::StageOnly)` passed to `pipeline.run_traced`
- Verbosity tier table: StageOnly (production), StageAndExclusions (staging), Full (development only)
- Source name `"cupel"` documented per SOURCE_NAME constant in lib.rs

Copied `crates/cupel/LICENSE` (MIT, Copyright 2026 Wollax) to `crates/cupel-otel/LICENSE`.

## Verification

```
$ cd crates/cupel-otel && cargo package --list
.cargo_vcs_info.json
Cargo.lock
Cargo.toml
Cargo.toml.orig
LICENSE
README.md
src/lib.rs
tests/integration.rs
EXIT: 0

$ cd crates/cupel-otel && cargo package --no-verify
   Packaging cupel-otel v0.1.0
    Packaged 8 files, 54.5KiB (14.7KiB compressed)
EXIT: 0
```

Both checks passed. `README.md` and `LICENSE` appear in the tarball manifest.

## Diagnostics

`cargo package --list` is the definitive check — exits non-zero with an explicit "readme does not appear to exist" or "LICENSE does not appear to exist" message if either file is absent. No additional tooling needed.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel-otel/README.md` — new file; intro + dev-dep snippet + usage example + verbosity tier table
- `crates/cupel-otel/LICENSE` — new file; MIT license text copied from crates/cupel/LICENSE
