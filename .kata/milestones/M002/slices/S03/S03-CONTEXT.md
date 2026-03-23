---
id: S03
milestone: M002
status: ready
---

# S03: Count-Based Quota Design — Context

## Goal

Produce `.planning/design/count-quota-design.md` that resolves all 5 open count-quota design questions — algorithm integration, tag non-exclusivity edge cases, pinned item interaction, conflict detection, and KnapsackSlice compatibility — with pseudocode and no remaining TBD fields.

## Why this Slice

Count-based quotas are the only M003 feature blocked on a design question rather than implementation time. The March 15 brainstorm explicitly deferred this with "the interaction with pinned items and token budgets not fully designed." Until this record exists, M003 can't implement `RequireCount`/`CapCount` without risking locking in semantics that need revision. S03 unblocks S06 (budget simulation API references count-quota interaction) and eventually unblocks the M003 implementation.

## Scope

### In Scope

- A complete `.planning/design/count-quota-design.md` covering:
  1. **Algorithm**: `RequireCount(kind, minCount)` and `CapCount(kind, maxCount)` constraints — how they interact with the existing `DISTRIBUTE-BUDGET` subroutine or replace it with `COUNT-DISTRIBUTE-BUDGET`
  2. **Tag non-exclusivity edge cases**: the direction is settled (non-exclusive — an item with multiple tags counts toward all matching `RequireCount` constraints simultaneously); S03 documents edge cases (e.g. what if satisfying both `RequireCount("A", 2)` and `RequireCount("B", 2)` with the same item puts total count above what the budget allows)
  3. **Pinned item interaction**: whether pinned items satisfy or reduce count minimums and how this interacts with the run-time scarcity behavior
  4. **Conflict detection**: build-time (`RequireCount(2).CapCount(1)` → throw at construction, per D040); run-time scarcity → degrade gracefully (include all available candidates even if minimum unmet; no exception; SelectionReport shows what was included)
  5. **KnapsackSlice compatibility**: preprocessing path vs constrained-knapsack variant — whether count minimums can be enforced as a pre-selection step before the DP or require a fundamentally different algorithm
- Pseudocode for `COUNT-DISTRIBUTE-BUDGET` subroutine (or equivalent) showing the algorithm concretely
- Explorer/challenger debate format: 2 rounds per unresolved sub-problem; escalate to user via `ask_user_questions` after 2 rounds if still unresolved
- Close any corresponding `.planning/issues/open/` issue files related to count-quota design if they exist (check at execution time)

### Out of Scope

- No spec chapter in `spec/src/` for count-quota — the design record in `.planning/design/` is the S03 deliverable; the spec chapter is an M003 concern written alongside implementation
- No implementation code (D039 is locked)
- No ExclusionReason variants for count-quota (those are implementation concerns for M003)
- `QuotaSlice`/`CountQuotaSlice` API naming decisions — the design record specifies semantics; naming is an implementation concern
- Cross-kind constraints (e.g. `RequireCount` spanning multiple kinds simultaneously) — explicitly out of scope per D040 ("revisit if extended to cross-kind constraints")

## Constraints

- **Run-time scarcity behavior is settled**: `RequireCount(2)` with only 1 matching candidate → include what's available, no exception. The design record must specify this explicitly and describe what the caller can inspect in `SelectionReport` to detect the shortfall.
- **Build-time conflict detection is a hard requirement** (D040): `RequireCount(2).CapCount(1)` MUST throw at policy construction. No runtime fallback for statically-detectable contradictions.
- **Tag non-exclusivity direction is settled**: document edge cases, not the direction. Do not re-debate whether non-exclusivity is correct.
- **Escalation threshold**: 2 debate rounds per sub-problem. If explorer and challenger still disagree after round 2, surface the impasse to the user with a concrete either/or question before writing the design record. Do not write ambiguous TBD fields to avoid asking.
- **No TBD fields**: the design record is the done condition. If any question remains unresolved after debates and escalation, the slice is not done.
- **Pseudocode required**: the algorithm must be concrete enough that an implementor in any language can build it without re-designing. Follow the pseudocode style used in `spec/src/slicers/quota.md` (labeled `text` fenced blocks, named subroutines).
- **No implementation code**: this is a design record, not a spec chapter; no `spec/src/` files are created or modified

## Integration Points

### Consumes

- `spec/src/slicers/quota.md` — `DISTRIBUTE-BUDGET` pseudocode and `QuotaSlice` algorithm; the existing percentage-based design is the baseline that count-quota extends or replaces
- `spec/src/slicers/knapsack.md` — KnapsackSlice DP algorithm; prerequisite for reasoning about constrained-knapsack vs preprocessing path
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` (Decision 6) — the 5 open questions as explicitly stated at deferral time; count-quota angles from S01 brainstorm also feed this
- `.kata/DECISIONS.md` — D040 (build-time vs run-time conflict detection), D039 (design-only) are hard constraints; read before starting
- S01 brainstorm output (`.planning/brainstorms/2026-03-21T<time>-brainstorm/count-quota-angles-report.md`) — fresh angles from the post-v1.2 brainstorm

### Produces

- `.planning/design/count-quota-design.md` — complete design decision record with no TBD fields:
  - Algorithm section with `COUNT-DISTRIBUTE-BUDGET` pseudocode
  - Tag non-exclusivity ruling with edge case specifications
  - Pinned item interaction ruling
  - Conflict detection rules (build-time vs run-time with run-time behavior specified)
  - KnapsackSlice compatibility path ruling
- S06 can reference this record for the `FindMinBudgetFor` + count-quota interaction note

## Open Questions

- **KnapsackSlice compatibility path** — the hardest sub-problem. Preprocessing (reserve N items before the DP) is simpler to implement but may not produce optimal solutions when count minimums and token budget interact. Constrained knapsack (modify the DP to enforce inclusion) is theoretically cleaner but substantially harder. The debate in S03 must produce a ruling on which path to specify. Working assumption: preprocessing path unless the debate surfaces a correctness argument for constrained knapsack.
- **Run-time scarcity in SelectionReport** — the design record must specify what signal is available for callers to detect a shortfall. Working assumption: no new field in `SelectionReport` for M002; the caller can count included items of the relevant kind. Verify whether a new `ExclusionReason` variant or summary field is needed at design time (even if it's deferred to M003 implementation). If the design requires a new diagnostic signal, name it in the design record so M003 implementors know what to build.
- **Interaction between tag non-exclusivity and KnapsackSlice** — if an item counts toward multiple `RequireCount` constraints simultaneously and is selected once, does it reduce the remaining requirement for all matching tags? Working assumption: yes (selecting the item counts it toward all matching requires). The design record must state this explicitly.
