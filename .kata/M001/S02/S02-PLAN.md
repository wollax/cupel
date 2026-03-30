# S02: Adopt TOML 1.1 Syntax in Vectors & Document Requirement

**Goal:** Vectors modernized with TOML 1.1 datetime syntax; TOML 1.1 requirement documented; spec mirror in sync.
**Demo:** Vector files use `T00:00Z` instead of `T00:00:00Z`; `conformance/README.md` notes TOML 1.1 minimum; `diff -rq conformance/required/ spec/conformance/required/` shows no differences; `cargo test` still passes all 266 tests.

## Must-Haves

* Vectors with `T00:00:00Z` timestamps simplified to `T00:00Z` (16 required + 5 optional files)
* `conformance/README.md` documents TOML 1.1 minimum parser requirement
* `spec/conformance/required/` is byte-identical to `conformance/required/`
* `cargo test` — 266+ pass, 0 fail
* `cargo clippy` — no new warnings

## Proof Level

* This slice proves: **contract** (266 conformance tests exercise all vectors against the TOML 1.1 parser)
* Real runtime required: no
* Human/UAT required: no

## Verification

* `cargo test` — 266+ pass, 0 fail
* `cargo clippy` — no new warnings
* `grep -r 'T00:00:00Z' conformance/ --include='*.toml'` — no matches (old pattern gone)
* `grep -r 'T00:00Z' conformance/required/` — matches in 16 files (new pattern present)
* `diff -rq conformance/required/ spec/conformance/required/` — no differences
* `grep -i 'TOML 1.1' conformance/README.md` — match found (documentation added)

## Requirement Coverage

| ID | Requirement | Role | How this slice delivers it |
| -- | -- | -- | -- |
| R003 | Adopt TOML 1.1 syntax | Primary | Datetime simplification in 21 vector files |
| R004 | Document TOML 1.1 requirement | Primary | `conformance/README.md` updated with TOML 1.1 minimum note |
| R002 | All tests pass | Supporting | `cargo test` 266+ pass after vector edits confirms no regressions |

## Tasks

- [x] **T01: Simplify datetime timestamps, update README, and sync spec mirror** `est:15m`
  * Done: All timestamps simplified, README updated, spec mirror synced, tests green (266 pass, 0 fail)

## Files Touched

* 16 files in `conformance/required/` (datetime simplification)
* 5 files in `conformance/optional/` (datetime simplification)
* `conformance/README.md` (TOML 1.1 documentation)
* `spec/conformance/required/` (mirror sync via rsync)
