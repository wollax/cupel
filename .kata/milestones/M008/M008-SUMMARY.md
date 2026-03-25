---
id: M008
provides:
  - TraceCollector::on_pipeline_completed(&mut self, report, budget, stage_snapshots) — defaulted no-op on trait; wired into all 5 stages of run_with_components in core cupel
  - StageTraceSnapshot struct (#[non_exhaustive], Debug, Clone) — stage, item_count_in, item_count_out, duration_ms, excluded (stage-scoped Vec<ExcludedItem>); exported from crates/cupel top level
  - crates/cupel-otel/ companion crate — CupelOtelTraceCollector implementing TraceCollector with CupelVerbosity enum (StageOnly, StageAndExclusions, Full)
  - SOURCE_NAME const = "cupel" — canonical OTel source name for tracer provider configuration
  - cupel.pipeline root span with cupel.budget.max_tokens and cupel.verbosity attributes
  - Five cupel.stage.* child spans (classify, score, deduplicate, slice, place) with cupel.stage.name, cupel.stage.item_count_in, cupel.stage.item_count_out
  - StageAndExclusions tier — cupel.exclusion events with reason/item_kind/item_tokens on stage spans
  - Full tier — cupel.item.included events on the Place stage span with kind/tokens/score attributes
  - 5 integration tests (source_name_is_cupel, hierarchy_root_and_five_stage_spans, stage_only_no_events, stage_and_exclusions_emits_exclusion_events, full_emits_included_item_events_on_place) using opentelemetry-sdk InMemorySpanExporter
  - crates/cupel-otel/README.md and LICENSE; cargo package --no-verify exits 0
  - spec/src/integrations/opentelemetry.md — ## Rust (cupel-otel) section with source name, Cargo.toml snippet, usage example, implementation notes
  - CHANGELOG.md updated under [Unreleased] ### Added
  - R058 validated in .kata/REQUIREMENTS.md
key_decisions:
  - D161 — cupel-otel is a separate crate; core cupel has zero opentelemetry production dependency
  - D162 — Direct opentelemetry API (not tracing bridge); matches .NET ActivitySource approach
  - D163 — SOURCE_NAME = "cupel" (crate name convention, not "Wollax.Cupel")
  - D164 — on_pipeline_completed as defaulted no-op; non-breaking for NullTraceCollector and DiagnosticTraceCollector
  - D165 — StageTraceSnapshot.excluded is stage-scoped; OTel collector emits events directly from snapshot, never re-scans the report
  - D169 — Explicit .end() required on every span; opentelemetry 0.27 never auto-ends on drop
  - D170 — Root span carries only cupel.budget.max_tokens and cupel.verbosity (spec-only; .NET has extra attributes)
  - D171 — ExclusionReason variant name via explicit match returning &'static str; _ => "Unknown" arm for forward compatibility
  - D173 — cargo package --no-verify required; path dep on cupel prevents verifier from building from tarball
patterns_established:
  - Structured end-of-run handoff pattern — companion crates override on_pipeline_completed to emit telemetry from StageTraceSnapshot data; no live span storage during stage execution
  - OTel span hierarchy via Context::current_with_span — root context established once, stage spans created with start_with_context; all spans require explicit .end()
  - InMemorySpanExporter + #[serial] pattern for OTel integration tests — prevents global tracer provider interference between tests
  - companion-crate README follows cupel-testing structure — intro, dev-dep snippet, usage example, API table, license
  - Spec language-specific sections appended after Conformance Notes to preserve existing content
observability_surfaces:
  - cd crates/cupel-otel && cargo test --all-targets — 5 named integration tests, each targeting a specific verbosity tier or structural property; failure names the exact broken assertion
  - exporter.get_finished_spans() — returns Vec<SpanData>; filter by span.name for specific spans; span.attributes for attribute assertions; span.events for event assertions
  - cargo package --list — exits non-zero with "does not appear to exist" when a declared include entry is absent
  - grep 'Status: validated' .kata/REQUIREMENTS.md | grep R058 — confirms R058 closure
  - grep 'Rust (cupel-otel)' spec/src/integrations/opentelemetry.md — confirms spec section presence
requirement_outcomes:
  - id: R058
    from_status: active
    to_status: validated
    proof: "5 integration tests in crates/cupel-otel/tests/integration.rs all pass — source_name_is_cupel (SOURCE_NAME const = 'cupel'), hierarchy_root_and_five_stage_spans (6 spans total with correct parent_span_id wiring), stage_only_no_events (0 events at StageOnly tier), stage_and_exclusions_emits_exclusion_events (cupel.exclusion events with reason/item_kind/item_tokens on tight-budget run), full_emits_included_item_events_on_place (cupel.item.included events on Place stage); cargo test --all-targets in both crates exits 0; cargo package --no-verify exits 0; spec ## Rust (cupel-otel) section present"
duration: ~3h (S01: 45m, S02: 50m, S03: 15m + milestone closure)
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# M008: Rust OpenTelemetry Bridge

**`cupel-otel` companion crate delivering `CupelOtelTraceCollector` with 3 verbosity tiers, `cupel.pipeline` / `cupel.stage.*` span hierarchy, and exact `cupel.*` attributes — Rust callers can now observe Cupel pipelines in any OTel backend without touching the zero-dep core.**

## What Happened

M008 delivered Rust/OTel parity with `Wollax.Cupel.Diagnostics.OpenTelemetry` across three slices that built on each other cleanly.

**S01** solved the design constraint that the Rust `TraceCollector` trait had no end-of-run hook — the only way the OTel bridge can emit structured spans is from a point where all stage data is available at once. The solution was `on_pipeline_completed(&mut self, report, budget, stage_snapshots)` as a defaulted no-op on the trait, plus a new `StageTraceSnapshot` struct that captures per-stage metrics (item counts, duration, stage-scoped excluded items). `run_with_components` in `pipeline/mod.rs` was modified to build one snapshot per stage and call the hook at the end. This was entirely additive — `NullTraceCollector` and `DiagnosticTraceCollector` required no changes. The `SpyCollector` integration test proved the hook fires exactly once with 5 snapshots in the correct stage order.

**S02** created `crates/cupel-otel/` from scratch. The crate pins `opentelemetry = "0.27"` and implements `CupelOtelTraceCollector` as a stateless collector that stores only `CupelVerbosity` and does all work in `on_pipeline_completed`. The implementation creates a root `cupel.pipeline` span, wraps it in `Context::current_with_span`, iterates `stage_snapshots` to create 5 child `cupel.stage.*` spans with the correct parent context, emits `cupel.exclusion` events at `StageAndExclusions` tier and above, and emits `cupel.item.included` events on the Place stage at `Full` tier. The critical discovery was that opentelemetry 0.27 never auto-ends spans on drop — explicit `.end()` is mandatory or spans never appear in the exporter (D169). All 5 `#[serial]` integration tests using `InMemorySpanExporter` passed after this was corrected.

**S03** closed the milestone: created `README.md` and `LICENSE` (both required by `Cargo.toml`'s `include` list), ran `cargo package --no-verify` to verify packaging (`--no-verify` is correct because the path dep on `cupel = { path = "../cupel" }` cannot be resolved from the tarball), appended the `## Rust (cupel-otel)` section to the spec, updated `CHANGELOG.md`, and promoted R058 from `active` to `validated` in REQUIREMENTS.md.

## Cross-Slice Verification

**SC1: `CupelOtelTraceCollector::new(CupelVerbosity::StageOnly)` implements `TraceCollector` and can be passed directly to `pipeline.run_traced`**
- Evidence: `impl TraceCollector for CupelOtelTraceCollector` in `crates/cupel-otel/src/lib.rs`; all 5 integration tests call `pipeline.run_traced(items, budget, &mut collector)` with a real `CupelOtelTraceCollector` — no wrapper ✓

**SC2: In-memory exporter captures root `cupel.pipeline` span with `cupel.budget.max_tokens`, `cupel.verbosity` attributes**
- Evidence: `hierarchy_root_and_five_stage_spans` test asserts span named `"cupel.pipeline"` exists; test verifies `cupel.budget.max_tokens` and `cupel.verbosity` attributes; `exporter.get_finished_spans()` ground truth ✓

**SC3: Each of the 5 stage spans carries `cupel.stage.name`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out`**
- Evidence: `hierarchy_root_and_five_stage_spans` test asserts 5 child spans named `cupel.stage.classify`, `cupel.stage.score`, `cupel.stage.deduplicate`, `cupel.stage.slice`, `cupel.stage.place` with parent_span_id wired to root; all 3 stage attributes verified ✓

**SC4: `StageAndExclusions` tier emits `cupel.exclusion` events with `reason`, `item_kind`, `item_tokens`**
- Evidence: `stage_and_exclusions_emits_exclusion_events` test runs a tight-budget pipeline forcing exclusions; verifies `cupel.exclusion` events exist with all 3 required attributes ✓

**SC5: `Full` tier emits `cupel.item.included` events on the place stage span**
- Evidence: `full_emits_included_item_events_on_place` test verifies events named `"cupel.item.included"` with `kind`, `tokens`, `score` attributes on the Place stage span ✓

**SC6: `StageOnly` emits no events**
- Evidence: `stage_only_no_events` test verifies 0 events across all 6 spans ✓

**SC7: `cargo test --all-targets` passes in both crates; `cargo clippy --all-targets -- -D warnings` clean in both**
- Evidence: cupel-otel → 5 tests passed, 0 failed; cupel → 170 tests passed (81+9+48+1+5+15+2+2+3+4), 0 failed; clippy exits 0 in both ✓

**SC8: `cargo package --dry-run` exits 0 for `cupel-otel`**
- Evidence: `cargo package --no-verify` exits 0, 8 files packaged 54.5KiB — note: `--no-verify` is used (not `--dry-run`) because the path dep to `../cupel` is not resolvable from the tarball; this is the correct and documented approach (D173) ✓

**SC9: Core `crates/cupel` crate has no `opentelemetry` dependency in `[dependencies]`**
- Evidence: `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry` returns empty ✓

**Definition of Done — all criteria met:**
- `TraceCollector::on_pipeline_completed` exists as defaulted no-op; `run_traced` calls it; `StageTraceSnapshot` defined and populated ✓
- `crates/cupel-otel/` exists with `CupelOtelTraceCollector`, `CupelVerbosity` ✓
- All three verbosity tiers implemented and tested with in-memory exporter ✓
- All `cupel.*` attribute names match `spec/src/integrations/opentelemetry.md` ✓
- `cargo test --all-targets` passes in both crates; clippy clean in both ✓
- `cargo package --no-verify` exits 0 for `cupel-otel` ✓
- Core `crates/cupel` has zero `opentelemetry` production dependency ✓
- R058 marked validated in `.kata/REQUIREMENTS.md` ✓
- All 3 slices `[x]` in roadmap; all 3 slice summaries exist ✓

## Requirement Changes

- R058: active → validated — 5 integration tests in `crates/cupel-otel/tests/integration.rs` pass against `opentelemetry-sdk` InMemorySpanExporter; `cargo test --all-targets` clean in both crates; `cargo package --no-verify` exits 0; spec `## Rust (cupel-otel)` section present with correct source name `"cupel"`

## Forward Intelligence

### What the next milestone should know
- `cupel-otel` is at version 0.1.0 and not yet published to crates.io. Before publishing, the `path = "../cupel"` dep must be replaced with a crates.io version dep. The `--no-verify` packaging workaround is only needed for local path deps.
- `CupelOtelTraceCollector` is entirely stateless — all work happens in `on_pipeline_completed`. The `is_enabled()` method returns `true` unconditionally; the global OTel tracer handles the no-op path when no provider is configured. This design means callers who configure no provider silently get no spans (no error), which is correct OTel behavior.
- D170 documents that the root span carries only `cupel.budget.max_tokens` and `cupel.verbosity` (spec-only). The .NET implementation adds `cupel.total_candidates`, `cupel.included_count`, `cupel.excluded_count` beyond the spec. If these are ever added to the Rust bridge, they are available in the `stage_snapshots` union (total_candidates = sum of included + excluded counts from snapshots).
- The `#[serial]` attribute on all integration tests in `cupel-otel` is load-bearing — global tracer provider is process-global; removing it from any test risks span leakage between test runs.

### What's fragile
- `on_pipeline_completed` is gated on `!stage_snapshots.is_empty()` — if a future code path adds an early-return before all 5 stages complete, the hook may receive fewer than 5 snapshots. The `on_pipeline_completed_called_once_with_five_snapshots` integration test catches this.
- `cargo package --no-verify` skips the tarball build — if `include` in `cupel-otel/Cargo.toml` drifts from actual source files, it will only be caught when a consumer tries to use the published crate. `cargo package --list` is the correct verification tool.
- `opentelemetry = "0.27"` is pinned. The 0.x API has been volatile; upgrading to 0.28+ may require changes to `Context::current_with_span`, `.end()` semantics, or `SpanData` import paths.

### Authoritative diagnostics
- `cd crates/cupel-otel && cargo test --all-targets -- --nocapture` — test names name each verbosity tier and structural property exactly; failure messages show expected vs actual key/value pairs from `span.attributes`
- `exporter.get_finished_spans()` is the ground truth for span data; filter by `span.name` then iterate `span.attributes` for key-value assertions
- `cargo package --list` — exits non-zero with an explicit "does not appear to exist" message if any `include` entry is absent

### What assumptions changed
- Assumed `SpanData` would be at `opentelemetry_sdk::trace::SpanData` — it is actually at `opentelemetry_sdk::export::trace::SpanData` in 0.27.1. The spec Rust section documents the correct path.
- Assumed spans auto-end on drop — they do not in opentelemetry 0.27. Explicit `.end()` is mandatory (D169). This is documented in the spec, README, and DECISIONS.md.

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — `StageTraceSnapshot` struct added
- `crates/cupel/src/diagnostics/trace_collector.rs` — `on_pipeline_completed` defaulted no-op added to trait
- `crates/cupel/src/lib.rs` — `StageTraceSnapshot` added to pub use block
- `crates/cupel/src/pipeline/mod.rs` — snapshot collection wired into all 5 stages + `on_pipeline_completed` call
- `crates/cupel/tests/on_pipeline_completed.rs` (new) — `SpyCollector` + 2 integration tests
- `crates/cupel-otel/Cargo.toml` (new) — package manifest with opentelemetry 0.27 deps pinned
- `crates/cupel-otel/src/lib.rs` (new) — full `CupelOtelTraceCollector` implementation with all 3 verbosity tiers
- `crates/cupel-otel/tests/integration.rs` (new) — 5 serial integration tests using `InMemorySpanExporter`
- `crates/cupel-otel/README.md` (new) — intro + dev-dep snippet + usage example + verbosity tier table
- `crates/cupel-otel/LICENSE` (new) — MIT license copied from crates/cupel/LICENSE
- `spec/src/integrations/opentelemetry.md` — `## Rust (cupel-otel)` section appended
- `CHANGELOG.md` — three bullets added under `[Unreleased] ### Added`
- `.kata/REQUIREMENTS.md` — R058 `Status: validated`, full `Validation:` field added
