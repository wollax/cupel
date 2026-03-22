---
estimated_steps: 6
estimated_files: 2
---

# T03: Write Budget Simulation Spec Chapter + Full Verification

**Slice:** S06 — Future Features Spec Chapters
**Milestone:** M002

## Description

Create `spec/src/analytics/` directory and write `spec/src/analytics/budget-simulation.md` — the budget simulation API contract chapter. Add a `# Analytics` section to `spec/src/SUMMARY.md`. Then run the full S06 verification pass to confirm all three new chapters meet the slice stopping condition.

This is the most design-heavy chapter: it must resolve the budget-override mechanism (locked as explicit `ContextBudget` parameter per planning decisions), specify the binary-search stopping condition, name the QuotaSlice + CountQuotaSlice guard, state the DryRun determinism invariant as normative text, and carry the Rust-parity-deferred note.

## Steps

1. **Read reference files** — Read `.planning/design/count-quota-design.md` Section 5 for the exact KnapsackSlice guard message pattern to mirror for the QuotaSlice guard. Read `src/Wollax.Cupel/CupelPipeline.cs:86` to confirm the current `.NET` `DryRun(IReadOnlyList<ContextItem> items)` signature and that it uses the pipeline's stored `_budget`. This confirms why an explicit `ContextBudget budget` parameter is needed for budget simulation methods. Read `spec/src/diagnostics/selection-report.md` for the `included` and `total_candidates` field names referenced in pseudocode.

2. **Write `spec/src/analytics/budget-simulation.md`** with these sections:
   - **Overview**: Budget simulation methods are extension methods on `CupelPipeline` in the .NET implementation. They orchestrate internal `DryRun` calls to answer questions about what the pipeline would select at different token budgets. Scoped to .NET in v1 — **Language Parity Note**: "The budget simulation API is scoped to the .NET implementation in v1.3. Rust parity is deferred to M003+." **`SweepBudget` out-of-scope note**: `SweepBudget` (exhaustive budget sweep) has been assigned to the Smelt project and will not be added to Cupel.
   - **DryRun Determinism Invariant** (normative): "`DryRun` MUST produce identical output for identical inputs. Tie-breaking order MUST be stable across calls — items with equal scores or equal token counts MUST be ordered consistently across repeated invocations with the same inputs. Implementations that depend on non-deterministic ordering (e.g., hash-map iteration order) are non-conformant."
   - **`GetMarginalItems`**: 
     - Purpose: identify which items are included in a full-budget run but excluded when the budget is reduced by `slackTokens`.
     - Signature: `IReadOnlyList<ContextItem> GetMarginalItems(IReadOnlyList<ContextItem> items, ContextBudget budget, int slackTokens)`
     - The `budget` parameter overrides the pipeline's stored budget for both internal `DryRun` calls. This is required because `DryRun` uses the pipeline's fixed budget by construction; the extension method passes a temporary budget for its internal calls.
     - The reduced-budget run uses `budget.MaxTokens - slackTokens` as the max token count (same `outputReserve` and `reservedSlots`).
     - Diff direction: `primary \ margin` — items in the full-budget result (`primary`) that are not in the reduced-budget result (`margin`). These are the items that become available as the budget grows.
     - Monotonicity assumption: assumes that a lower budget never adds new items (monotonic inclusion). This holds for `GreedySlice` and `KnapsackSlice` but not for `QuotaSlice` (percentage shifts can add new kinds at lower budgets).
     - **QuotaSlice guard**: if the pipeline's slicer is `QuotaSlice`, throw `InvalidOperationException` with message: `"GetMarginalItems requires monotonic item inclusion. QuotaSlice produces non-monotonic inclusion as budget changes shift percentage allocations."` 
     - GET-MARGINAL-ITEMS pseudocode (two DryRun calls, set-diff based on item identity):
       ```text
       GET-MARGINAL-ITEMS(pipeline, items, budget, slackTokens):
           reducedBudget <- ContextBudget(maxTokens: budget.maxTokens - slackTokens,
                                          targetTokens: budget.targetTokens - slackTokens,
                                          outputReserve: budget.outputReserve)
           primary <- pipeline.DRY-RUN(items, budget)
           margin  <- pipeline.DRY-RUN(items, reducedBudget)
           return [item in primary.included where item not in margin.included]
       ```
   - **`FindMinBudgetFor`**:
     - Purpose: find the minimum token budget (within a search ceiling) at which `targetItem` would be included in the selection.
     - Signature: `int? FindMinBudgetFor(IReadOnlyList<ContextItem> items, ContextItem targetItem, int searchCeiling)`
     - Preconditions (both throw `ArgumentException` if violated): (a) `targetItem` must be an element of `items`; (b) `searchCeiling >= targetItem.Tokens`.
     - Binary search over `[targetItem.Tokens, searchCeiling]`. Lower bound is `targetItem.Tokens` (optimization: target item cannot be included in a budget smaller than its own token count). Stop condition: `high - low <= 1` (exact minimum found; ~`log2(searchCeiling)` DryRun invocations, typically 10-15 for realistic ceilings).
     - Returns `int?` — `null` means target item is not selectable within `[targetItem.Tokens, searchCeiling]`.
     - **QuotaSlice + CountQuotaSlice guard**: if the pipeline's slicer is `QuotaSlice` or `CountQuotaSlice`, throw `InvalidOperationException` with message: `"FindMinBudgetFor requires monotonic item inclusion. QuotaSlice and CountQuotaSlice produce non-monotonic inclusion as budget changes shift allocations. Use a GreedySlice or KnapsackSlice inner slicer for budget simulation."` Note the general precondition: any slicer whose item inclusion is sensitive to absolute budget value in a non-monotonic way is incompatible with `FindMinBudgetFor`.
     - FIND-MIN-BUDGET-FOR pseudocode:
       ```text
       FIND-MIN-BUDGET-FOR(pipeline, items, targetItem, searchCeiling):
           low  <- targetItem.tokens      // inclusive lower bound
           high <- searchCeiling          // inclusive upper bound

           if targetItem not in items:
               throw ArgumentException("targetItem must be an element of items")
           if searchCeiling < targetItem.tokens:
               throw ArgumentException("searchCeiling must be >= targetItem.Tokens")

           while high - low > 1:
               mid <- low + (high - low) / 2
               midBudget <- ContextBudget(maxTokens: mid, ...)
               report <- pipeline.DRY-RUN(items, midBudget)
               if targetItem in report.included:
                   high <- mid
               else:
                   low <- mid

           // Verify at high (the candidate minimum)
           finalBudget <- ContextBudget(maxTokens: high, ...)
           finalReport <- pipeline.DRY-RUN(items, finalBudget)
           if targetItem in finalReport.included:
               return high
           return null
       ```
   - **Conformance Notes**: `DryRun` is the primitive; budget simulation builds on it. Custom slicers that implement non-monotonic inclusion must not be used with `FindMinBudgetFor` or `GetMarginalItems` without explicit documentation of the monotonicity property.

3. **Update `spec/src/SUMMARY.md`** — Add `# Analytics` section after `# Integrations`. Add entry: `- [Budget Simulation](analytics/budget-simulation.md)`.

4. **Run TBD check for all three chapters** — `grep -ci "\bTBD\b" spec/src/scorers/decay.md`; `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md`; `grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md`. All must return 0.

5. **Run completeness grep checks** — Run all the spec completeness checks from S06-PLAN.md Verification section: SUMMARY.md links, per-chapter required strings. Log any failures before proceeding.

6. **Run test suites** — `cargo test --manifest-path crates/cupel/Cargo.toml`; `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`. Both must pass with no regressions.

## Must-Haves

- [ ] `spec/src/analytics/budget-simulation.md` exists
- [ ] DryRun determinism invariant is MUST-level normative text
- [ ] `GetMarginalItems` signature includes explicit `ContextBudget budget` parameter (not pipeline.Budget)
- [ ] `GetMarginalItems` reduced-budget run uses `budget.MaxTokens - slackTokens`
- [ ] `GetMarginalItems` QuotaSlice guard with exact InvalidOperationException message
- [ ] `FindMinBudgetFor` returns `int?` (`null` = not found within ceiling)
- [ ] `FindMinBudgetFor` preconditions: `targetItem in items` + `searchCeiling >= targetItem.Tokens`
- [ ] `FindMinBudgetFor` binary search with stop condition `high - low <= 1`
- [ ] `FindMinBudgetFor` QuotaSlice + CountQuotaSlice guard naming both types
- [ ] Rust parity deferred to M003+ — explicit one-sentence note
- [ ] `SweepBudget` out-of-scope note (moved to Smelt)
- [ ] GET-MARGINAL-ITEMS pseudocode in `text` fenced block
- [ ] FIND-MIN-BUDGET-FOR pseudocode in `text` fenced block
- [ ] `grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md` returns 0
- [ ] `# Analytics` section present in `spec/src/SUMMARY.md`
- [ ] All three S06 chapters have zero TBD fields
- [ ] Both test suites pass

## Verification

- `test -f spec/src/analytics/budget-simulation.md`
- `grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md` → 0
- `grep -ci "\bTBD\b" spec/src/scorers/decay.md` → 0 (regression check)
- `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` → 0 (regression check)
- `grep -q "GetMarginalItems" spec/src/analytics/budget-simulation.md`
- `grep -q "FindMinBudgetFor" spec/src/analytics/budget-simulation.md`
- `grep -q "QuotaSlice" spec/src/analytics/budget-simulation.md`
- `grep -q "CountQuotaSlice" spec/src/analytics/budget-simulation.md`
- `grep -q "deterministic\|MUST" spec/src/analytics/budget-simulation.md`
- `grep -q "monoton" spec/src/analytics/budget-simulation.md`
- `grep -q "Rust" spec/src/analytics/budget-simulation.md`
- `grep -q "SweepBudget\|Smelt" spec/src/analytics/budget-simulation.md`
- `grep -q "Analytics" spec/src/SUMMARY.md`
- `grep -q "budget-simulation" spec/src/SUMMARY.md`
- `cargo test --manifest-path crates/cupel/Cargo.toml` exits 0
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0

## Observability Impact

- Signals added/changed: None (spec authoring; no code changes per D039)
- How a future agent inspects this: `grep -ci "\bTBD\b" spec/src/analytics/budget-simulation.md` → 0; `grep -q "Analytics" spec/src/SUMMARY.md`; each guard message can be checked by grepping for "GetMarginalItems requires\|FindMinBudgetFor requires" to verify wording matches spec
- Failure state exposed: missing DryRun determinism invariant means the budget simulation implementation may use non-deterministic tie-breaking; missing CountQuotaSlice in the guard means a future caller could erroneously use FindMinBudgetFor with count quotas; TBD count > 0 means a pseudocode section was left incomplete

## Inputs

- `.planning/design/count-quota-design.md` Section 5 — KnapsackSlice guard message pattern; mirror for QuotaSlice guard (public API names only per D032)
- `src/Wollax.Cupel/CupelPipeline.cs:86` — current `DryRun` signature; confirms pipeline uses stored `_budget`; confirms explicit `budget` param is required in extension methods
- `spec/src/diagnostics/selection-report.md` — `included` field name for pseudocode; `total_candidates` for completeness
- `.kata/DECISIONS.md` D044, D048 — locked budget simulation API decisions (no multi-budget DryRun; FindMinBudgetFor returns `int?`)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` "S06 must specify — Budget Simulation" list — authoritative 6-item mandate list
- T01 + T02 outputs: `spec/src/SUMMARY.md` with DecayScorer and Integrations already present

## Expected Output

- `spec/src/analytics/budget-simulation.md` — new file; fully-specified budget simulation chapter with DryRun determinism invariant, GetMarginalItems contract, FindMinBudgetFor contract with binary-search pseudocode, both guards, Rust-parity-deferred note, SweepBudget out-of-scope note, zero TBD fields
- `spec/src/SUMMARY.md` — `# Analytics` section added with budget simulation entry
- Verification log confirming all three chapters pass completeness checks and both test suites pass
