# S06: Future Features Spec Chapters — UAT

**Milestone:** M002
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S06 produces three spec chapters — no runtime components, no UI, no API surface is deployed. The proof is that the right words are in the right files, reachable from SUMMARY.md, and contain no unresolved gaps. Artifact-driven review (reading the chapters for clarity, internal consistency, and completeness) is both necessary and sufficient for this slice. The human review gate is the final step before M002 DoD is declared complete.

## Preconditions

- `spec/src/scorers/decay.md` exists
- `spec/src/integrations/opentelemetry.md` exists
- `spec/src/analytics/budget-simulation.md` exists
- All three chapters have `grep -ci "\bTBD\b"` → 0
- `spec/src/SUMMARY.md` links all three chapters (decay, opentelemetry, budget-simulation under Integrations, Analytics sections)
- `cargo test --manifest-path crates/cupel/Cargo.toml` passes
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes

## Smoke Test

```bash
grep -q "DECAY-SCORE" spec/src/scorers/decay.md && \
grep -q "StageOnly" spec/src/integrations/opentelemetry.md && \
grep -q "GetMarginalItems" spec/src/analytics/budget-simulation.md && \
echo "All three chapters present and contain key content"
```

Expected: prints `All three chapters present and contain key content`

## Test Cases

### 1. DecayScorer spec chapter completeness

1. Open `spec/src/scorers/decay.md`
2. Verify the following sections are present and fully specified:
   - Overview contrasting with RecencyScorer
   - TimeProvider section with mandatory-injection mandate, .NET `System.TimeProvider` reference, and Rust trait declaration
   - DECAY-SCORE pseudocode with negative-age clamping noted
   - Exponential curve factory with `halfLife > Duration::ZERO` precondition
   - Step curve factory with `windows` ordering, strict `>` comparison, throw-at-construction rules
   - Window curve factory with half-open `[0, maxAge)` interval, `age == maxAge → 0.0` explicit
   - nullTimestampScore section with default 0.5 and neutral-semantics rationale
   - Edge Cases table
   - 5 conformance vector outlines with fixed referenceTime
3. **Expected:** All sections present with no gaps, no "TBD", no undefined references

### 2. OTel verbosity spec chapter completeness

1. Open `spec/src/integrations/opentelemetry.md`
2. Verify:
   - Pre-stability disclaimer for `cupel.*` namespace is prominent
   - Zero-dependency core guarantee (companion package pattern)
   - ActivitySource name `"Wollax.Cupel"` stated
   - Activity hierarchy: root `cupel.pipeline` + 5 child Activities (classify, score, deduplicate, slice, place); Sort explicitly omitted
   - `StageOnly` tier: `cupel.budget.max_tokens`, `cupel.verbosity`, stage-level `cupel.stage.*` attributes — no item-level data
   - `StageAndExclusions` tier: adds `cupel.exclusion` Events with `cupel.exclusion.reason`, `cupel.exclusion.item_kind`, `cupel.exclusion.item_tokens`; `cupel.exclusion.count` summary on stage Activity
   - `Full` tier: adds `cupul.item.included` Events with `cupel.item.kind`, `cupel.item.tokens`, `cupel.item.score`
   - Cardinality table with environment-tier recommendations
   - Note that `cupel.exclusion.reason` values are open-ended
3. **Expected:** All tiers fully specified with exact attribute names and types; no ambiguous entries

### 3. Budget simulation spec chapter completeness

1. Open `spec/src/analytics/budget-simulation.md`
2. Verify:
   - DryRun determinism invariant stated as normative MUST; hash-map iteration order called out as non-conformant
   - `GetMarginalItems` signature includes explicit `ContextBudget budget` and `int slackTokens` parameters
   - Reduced-budget formula stated as `budget.MaxTokens - slackTokens`
   - Diff direction stated as `primary \ margin`
   - QuotaSlice guard with `InvalidOperationException` message text present
   - GET-MARGINAL-ITEMS pseudocode present
   - `FindMinBudgetFor` signature with `int?` return type
   - Both `ArgumentException` preconditions present (targetItem in items, searchCeiling >= targetItem.Tokens)
   - Binary search stop condition `high - low <= 1` present
   - Both QuotaSlice and CountQuotaSlice named in guard
   - FIND-MIN-BUDGET-FOR pseudocode present
   - SweepBudget out-of-scope note present
   - Rust parity deferred note present
3. **Expected:** Both API contracts fully specified with no gaps; guard messages unambiguous; pseudocode complete

### 4. SUMMARY.md navigation

1. Open `spec/src/SUMMARY.md`
2. Verify:
   - `[DecayScorer](scorers/decay.md)` appears under the `# Scorers` section
   - `# Integrations` section exists with `[OpenTelemetry](integrations/opentelemetry.md)` entry
   - `# Analytics` section exists with `[Budget Simulation](analytics/budget-simulation.md)` entry
3. **Expected:** All three chapters are reachable from the book index; section hierarchy is logical

### 5. Test suite regression check

1. Run `cargo test --manifest-path crates/cupel/Cargo.toml`
2. Run `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`
3. **Expected:** Rust: 113 passed, 0 failed; .NET: 583 passed, 0 failed (spec-only changes introduce no regressions)

## Edge Cases

### DecayScorer Step curve boundary precision

1. Read the Step curve section in `spec/src/scorers/decay.md`
2. Confirm: comparison is strict `>` (not `>=`), so `age == window.maxAge` falls through to the next window
3. **Expected:** The spec is unambiguous about boundary ownership; no conformance gap

### Window curve at exact maxAge

1. Read the Window curve section in `spec/src/scorers/decay.md`
2. Confirm: `age == maxAge` returns 0.0 (half-open interval `[0, maxAge)`)
3. **Expected:** Boundary case documented with a single clear statement; implementors cannot misread it

### FindMinBudgetFor not-found case

1. Read the `FindMinBudgetFor` section in `spec/src/analytics/budget-simulation.md`
2. Confirm: return type is `int?` (.NET) / `Option<i32>` (Rust); null/None means not found within `[targetItem.Tokens, searchCeiling]`
3. **Expected:** Null/None return is documented as a valid business outcome, not an error

### OTel pre-stability disclaimer visibility

1. Read `spec/src/integrations/opentelemetry.md`
2. Confirm: the pre-stability disclaimer for `cupel.*` attribute names appears early (not buried in a footnote)
3. **Expected:** Any implementor reading the chapter will encounter the disclaimer before the attribute tables

## Failure Signals

- TBD count > 0 in any chapter means an unfinished section was introduced or left in place
- Missing SUMMARY.md entry means mdBook won't serve the chapter (reachability failure)
- Cargo or dotnet test failures mean an incidental code regression was introduced (should not happen in a spec-only slice)
- Pseudocode references types or fields that don't exist in the current spec (cross-chapter consistency failure)

## Requirements Proved By This UAT

- R044 — UAT proves: three spec chapters exist with zero TBD fields, required sections, and correct SUMMARY.md links; DecayScorer algorithm and curves fully specified; OTel attributes per verbosity tier defined; GetMarginalItems/FindMinBudgetFor API contracts written with monotonicity spec and slicer guards; both test suites pass

## Not Proven By This UAT

- Implementation correctness of DecayScorer, OTel bridge, or budget simulation extension methods — all deferred to M003
- Runtime behavior under actual OTel collector integration
- Performance characteristics of the binary search in FindMinBudgetFor at scale
- Alignment of `cupel.*` attributes with future OTel LLM SIG semantic conventions — deferred until SIG stabilizes

## Notes for Tester

- The pre-stability disclaimer in the OTel chapter is intentional — do not treat it as a gap. The `cupel.*` namespace is owned by Cupel and explicitly not tracking the draft gen_ai.* SIG until it stabilizes.
- The Rust parity deferred note in budget-simulation.md is intentional — only .NET is in scope for v1.3; Rust extension methods are M003+.
- The SweepBudget out-of-scope note is intentional — SweepBudget was moved to the Smelt project.
- DecayScorer TimestampCoverage() analytics method is mentioned in decay.md as a future implementation precondition — this is forward-looking documentation, not a spec gap in S06.
- The `rtk dotnet test` wrapper may misreport exit code; use raw `dotnet test` to confirm the 583-passed result.
