---
estimated_steps: 5
estimated_files: 2
---

# T02: Write OTel Verbosity Spec Chapter

**Slice:** S06 — Future Features Spec Chapters
**Milestone:** M002

## Description

Create `spec/src/integrations/` directory and write `spec/src/integrations/opentelemetry.md` — the OTel verbosity spec chapter for the `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package. Add a `# Integrations` section to `spec/src/SUMMARY.md`.

All attribute names, tier definitions, and stage counts are locked (D043, and the 5 stage Activities per events.md Sort-omission precedent). The primary work is producing precise, complete attribute tables per tier and the mandatory cardinality table.

## Steps

1. **Confirm stage count** — Read `spec/src/diagnostics/events.md` lines around "Sort is omitted" to confirm the canonical PipelineStage enum excludes Sort. OTel will cover exactly 5 stage Activities: `classify`, `score`, `deduplicate`, `slice`, `place`. Stage names in `cupel.stage.{name}` attribute values are lowercase-only (matching `PipelineStage` value lowercasing).

2. **Write `spec/src/integrations/opentelemetry.md`** with these sections:
   - **Overview**: `Wollax.Cupel.Diagnostics.OpenTelemetry` is a companion package (separate NuGet assembly); the core `Wollax.Cupel` assembly has zero dependency on OpenTelemetry (R032/D039). The companion bridges `ITraceCollector`/`TraceCollector` to .NET `ActivitySource`. AttributeSource name: `"Wollax.Cupel"` — callers use this name in `AddSource()` configuration.
   - **Pre-Stability Disclaimer**: `cupel.*` attribute names are pre-stable and subject to change as the OpenTelemetry LLM semantic conventions stabilize. Do not build alert rules or dashboards that hard-code specific attribute names.
   - **Activity Hierarchy**: Root `cupel.pipeline` Activity spans the full pipeline run. Five child `cupel.stage.{name}` Activities, one per pipeline stage, where `{name}` ∈ {`classify`, `score`, `deduplicate`, `slice`, `place`}. Sort is an internal step with no diagnostic boundary; it does not receive its own Activity (consistent with `PipelineStage` enum omission in events.md).
   - **Verbosity Tiers**: `CupelVerbosity` enum with three values — describe each tier precisely:
     - **`StageOnly`** (production-safe): Root Activity attributes: `cupel.budget.max_tokens` (int) — the pipeline's max token budget; `cupel.verbosity` (string) — the verbosity tier name. Each stage Activity attributes: `cupel.stage.name` (string) — stage name lowercase; `cupel.stage.item_count_in` (int) — items entering the stage; `cupel.stage.item_count_out` (int) — items leaving the stage. No `duration_ms` attribute (duration is provided by the Activity's own start/end timestamps).
     - **`StageAndExclusions`** (staging environments): All `StageOnly` attributes plus: per-excluded-item `cupel.exclusion` Event on the stage Activity where the exclusion occurred, with event attributes `cupel.exclusion.reason` (string — ExclusionReason variant name, e.g. `"BudgetExceeded"`), `cupel.exclusion.item_kind` (string), `cupel.exclusion.item_tokens` (int); plus `cupel.exclusion.count` (int) summary attribute on the stage Activity (total exclusions in that stage). Note: `cupel.exclusion.reason` values are open-ended — new ExclusionReason variants may appear in future spec versions without a schema change.
     - **`Full`** (development only): All `StageAndExclusions` attributes plus: per-included-item `cupel.item.included` Event after the Place stage, with event attributes `cupel.item.kind` (string), `cupel.item.tokens` (int), `cupel.item.score` (float64). No placement-position attribute.
   - **Cardinality Table**: Mandatory section.

     | Verbosity | Events / Pipeline Run | Recommended Environment |
     |---|---|---|
     | `StageOnly` | ~10 (5 stage Activities + root) | Production |
     | `StageAndExclusions` | ~10 + 0–300 (depends on exclusion count) | Staging |
     | `Full` | ~10 + 0–1000 (depends on item count) | Development only |

     **Warning:** `Full` verbosity can produce high cardinality traces with large item sets. Do not enable in production without a sampling strategy.
   - **Attribute Reference Table**: Complete flat table of all `cupel.*` attribute names, types, tiers, and semantics in one place for quick lookup.
   - **Conformance Notes**: `cupel.exclusion.reason` values map to ExclusionReason variant names; implementations MUST use the canonical variant name string. New ExclusionReason variants in future spec versions will appear as new attribute values — trace backends must not hard-code the set.

3. **Update `spec/src/SUMMARY.md`** — Add `# Integrations` section after the `# Conformance` section (or after `# Testing`; check current structure and place it consistently). Add entry: `- [OpenTelemetry](integrations/opentelemetry.md)`.

4. **Run TBD check** — `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` → must return 0.

5. **Run test suites** — `cargo test --manifest-path crates/cupel/Cargo.toml`; `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`. Spec-only change; both must pass.

## Must-Haves

- [ ] `spec/src/integrations/opentelemetry.md` exists
- [ ] Companion-package zero-dep guarantee stated explicitly (core Wollax.Cupel has no OTel dependency)
- [ ] ActivitySource name `"Wollax.Cupel"` stated explicitly
- [ ] Pre-stability disclaimer for `cupel.*` namespace present
- [ ] Activity hierarchy: root `cupel.pipeline` + 5 stage Activities (Sort omitted)
- [ ] `StageOnly` tier: all 5 attribute names with types listed (cupel.budget.max_tokens, cupel.verbosity, cupel.stage.name, cupel.stage.item_count_in, cupel.stage.item_count_out)
- [ ] `StageAndExclusions` tier: cupel.exclusion Event attributes listed (reason, item_kind, item_tokens) + cupel.exclusion.count summary
- [ ] `Full` tier: cupel.item.included Event attributes listed (kind, tokens, score); no placement attribute
- [ ] Cardinality table present with 3 rows and environment recommendations
- [ ] Open-ended `cupel.exclusion.reason` values noted
- [ ] `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` returns 0
- [ ] `# Integrations` section present in `spec/src/SUMMARY.md`
- [ ] Both test suites pass

## Verification

- `test -f spec/src/integrations/opentelemetry.md`
- `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` → 0
- `grep -q "StageOnly" spec/src/integrations/opentelemetry.md`
- `grep -q "StageAndExclusions" spec/src/integrations/opentelemetry.md`
- `grep -q "Full" spec/src/integrations/opentelemetry.md`
- `grep -q "cupel.budget.max_tokens" spec/src/integrations/opentelemetry.md`
- `grep -q "cupel.exclusion.reason" spec/src/integrations/opentelemetry.md`
- `grep -q "cupel.item.included" spec/src/integrations/opentelemetry.md`
- `grep -q "Wollax.Cupel" spec/src/integrations/opentelemetry.md`
- `grep -q "pre-stable\|pre.stable\|pre.stability\|pre-stability" spec/src/integrations/opentelemetry.md`
- `grep -q "Integrations" spec/src/SUMMARY.md`
- `grep -q "opentelemetry" spec/src/SUMMARY.md`
- `cargo test --manifest-path crates/cupel/Cargo.toml` exits 0
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0

## Observability Impact

- Signals added/changed: None (spec authoring; no code changes)
- How a future agent inspects this: `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` → 0; `grep -q "Integrations" spec/src/SUMMARY.md`; attribute completeness verified by checking each `cupel.*` attribute name against the must-haves list
- Failure state exposed: missing cardinality table means a future implementor may ship `Full` in production without warning; missing pre-stability disclaimer means callers may lock on attribute names that change; TBD count > 0 means an attribute table row was left unresolved

## Inputs

- `spec/src/diagnostics/events.md` — authoritative Sort-omission statement for PipelineStage; confirms 5 diagnostic stages (not 6)
- `spec/src/scorers/metadata-trust.md` — chapter style reference
- `.kata/DECISIONS.md` D043 — locked `cupel.*` namespace decision
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` "S06 must specify — OpenTelemetry" list — authoritative 6-item mandate list for this chapter
- T01 output: `spec/src/SUMMARY.md` with DecayScorer entry already present — add `# Integrations` section after existing sections without disturbing T01's additions

## Expected Output

- `spec/src/integrations/opentelemetry.md` — new file; fully-specified OTel verbosity chapter with 5-Activity hierarchy, three tier attribute tables, cardinality table, pre-stability disclaimer, zero TBD fields
- `spec/src/SUMMARY.md` — `# Integrations` section added with OTel entry
