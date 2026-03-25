# S02 Post-Completion Roadmap Assessment

**Assessed:** 2026-03-24
**Result:** Roadmap unchanged — S03 proceeds as planned

## Success Criterion Coverage

| Criterion | Remaining owner |
|-----------|----------------|
| `CupelOtelTraceCollector::new(CupelVerbosity::StageOnly)` implements `TraceCollector`, usable with `pipeline.run_traced` | S03 (packages the working impl) |
| Root `cupel.pipeline` span with `cupel.budget.max_tokens`, `cupel.verbosity` attributes | ✅ proven by S02; S03 packages |
| Each of 5 stage spans carries `cupel.stage.name`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out` | ✅ proven by S02; S03 packages |
| `StageAndExclusions` emits `cupel.exclusion` events with `reason`, `item_kind`, `item_tokens` | ✅ proven by S02; S03 packages |
| `Full` emits `cupel.item.included` events on place stage span | ✅ proven by S02; S03 packages |
| `StageOnly` emits no events | ✅ proven by S02; S03 packages |
| `cargo test --all-targets` passes in both crates; `cargo clippy` clean | ✅ currently passing; S03 must not regress |
| `cargo package --dry-run` exits 0 for `cupel-otel` | S03 |
| Core `crates/cupel` has no `opentelemetry` dependency | ✅ proven by S02 |

**Coverage check: passes.** All criteria have at least one remaining owning slice.

**Note on root span attributes:** The success criterion listed `cupel.total_candidates`, `cupel.included_count`, `cupel.excluded_count` on the root span. S02 correctly omitted these per spec (only `cupel.budget.max_tokens` and `cupel.verbosity` appear in the spec's attribute reference table — D170). The .NET implementation adds extras beyond the spec; the Rust implementation follows the spec. S03's spec addendum should document the Rust root span as spec-only (two attributes), not the .NET superset.

## Risks Retired

Both milestone-level risks are now retired:
- **`TraceCollector` missing `on_pipeline_completed` hook** — retired in S01
- **`opentelemetry` API version pinning** — retired in S02; pinned to 0.27, tested with in-memory SDK exporter

## S03 Scope Assessment

S03 scope is accurate and no adjustments are needed:
- `cargo package --dry-run` will work from `crates/cupel-otel/` directly (standalone crate, no workspace)
- `spec/src/integrations/opentelemetry.md` exists from M002/S06; S03 adds a Rust-specific section documenting the correct source name (`"cupel"`), Cargo.toml snippet, and usage example
- The spec addendum should note: (a) `SpanData` lives at `opentelemetry_sdk::export::trace::SpanData` in 0.27, (b) `_ => "Unknown"` arm in `exclusion_reason_name` handles future variants, (c) explicit `.end()` is mandatory (D169)
- No new risks emerged from S02; `risk:low` classification for S03 remains accurate

## Requirement Coverage

R058 primary coverage: S01 ✅ → S02 ✅ → S03 (validation gate). Coverage remains sound. R058 will be fully validated after S03 runs `cargo package --dry-run`, updates spec and CHANGELOG, and marks the requirement validated.
