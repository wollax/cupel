# M002: v1.3 Design Sprint — Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

## Project Description

Cupel is a dual-language (.NET + Rust) context management library for coding agents. v1.2 shipped Rust diagnostics parity (`SelectionReport`, `run_traced`, `dry_run`) and quality hardening across both codebases. M002 is a pure design sprint: it produces spec chapters and design decision records that unblock v1.3 implementation work. No production code ships from this milestone.

## Why This Milestone

Several v1.3 features are blocked on unresolved design questions:

- **Count-based quotas** cannot be implemented until the algorithm, tag non-exclusivity, pinned item interaction, and KnapsackSlice compatibility path are all resolved. The brainstorm (March 15) explicitly deferred these to a design phase.
- **Cupel.Testing package** cannot be built until the assertion vocabulary is designed — shipping assertions with ambiguous semantics locks in a bad API surface from day one.
- **DecayScorer, OpenTelemetry, budget simulation** all need spec chapters before implementation, per the spec-first principle established for Rust diagnostics in M001.
- **Spec editorial debt** (~8-10 open issues in `.planning/issues/open/`) makes the mdBook misleading for new language binding implementors.
- **Fresh brainstorm** is warranted because the last session (March 15) predates `SelectionReport` and `run_traced` shipping — the landscape has shifted.

This milestone closes all design gates in one focused sprint, leaving M003 (v1.3 implementation) with a clean, fully-specified backlog.

## User-Visible Outcome

### When this milestone is complete, the user can:

- Read the Cupel spec and find no ambiguous event ordering, misleading algorithm descriptions, or unlabeled normative sections
- Read the count-quota design record and understand exactly how count-based `Require(kind, minCount)` interacts with tags, pinned items, and KnapsackSlice — with pseudocode
- Read the `DecayScorer` spec chapter and implement it in any language without ambiguity about null-timestamp policy, curve semantics, or `TimeProvider` injection
- Read the `Cupel.Testing` vocabulary doc and know exactly what `report.Should().HaveKindInIncluded(...)` asserts (including tolerance, ties, error message format)
- Find the `"cupel:<key>"` namespace reserved in the spec with first-class conventions for `cupel:trust` and `cupel:source-type`
- Review a brainstorm summary capturing new ideas from the post-v1.2 perspective

### Entry point / environment

- Entry point: spec mdBook (`spec/src/`) + design decision records (`.planning/design/` or inline spec sections) + brainstorm output (`.planning/brainstorms/`)
- Environment: static documents; verified by human review and spec linting
- Live dependencies involved: none

## Completion Class

- Contract complete means: all spec chapters are written, all open spec issues are closed, the design records resolve every open question (no "TBD" remaining in the design outputs), and the brainstorm summary is committed
- Integration complete means: n/a (no runtime components)
- Operational complete means: n/a

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- Every open spec issue listed in R041 is closed (issue file removed from `.planning/issues/open/` or annotated as resolved)
- Count-quota design record answers all 5 open questions with no remaining "TBD" fields
- All new spec chapters (DecayScorer, OTel, budget simulation, metadata conventions, Cupel.Testing vocabulary) exist in `spec/src/` and are reachable from `SUMMARY.md`
- Brainstorm output committed to `.planning/brainstorms/` with SUMMARY.md
- `cargo test` and `dotnet test` still pass (no spec file changes should break test infrastructure)

## Risks and Unknowns

- **Count-quota algorithm complexity** — Tag non-exclusivity and KnapsackSlice compatibility may require multiple design iterations. The brainstorm flagged this as "explicitly unresolved." Retiring this is S03's job; if the explorer/challenger debate doesn't converge, escalate with `ask_user_questions` before writing the design record.
- **Cupel.Testing vocabulary precision** — "What counts as high-scoring?" and similar questions have real answers but require careful thought about tolerance, ties, and edge cases. If the vocabulary document leaves these undefined, the testing package will have unstable semantics.
- **Brainstorm scope creep** — Fresh brainstorms tend to surface implementation ideas as well as design ideas. Keep S01 output focused on ideas that are either (a) new to the backlog or (b) refine existing deferred items. Do not let S01 expand M002's scope.

## Existing Codebase / Prior Art

- `spec/src/slicers/quota.md` — QuotaSlice algorithm with percentage-based `Require`/`Cap`. Count-based quotas extend this. Read before S03.
- `spec/src/diagnostics/` — Completed diagnostics spec (events.md, trace-collector.md, exclusion-reasons.md, selection-report.md). Several open spec issues (R041) are in these files.
- `spec/src/scorers/` — All scorer specs. DecayScorer will be a new file here.
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/` — Most recent brainstorm. highvalue-report.md has the count-quota, Cupel.Testing, DecayScorer, and OTel decisions. Read before S03/S05/S06.
- `.planning/brainstorms/2026-03-10T15-06-brainstorm/` — Earlier brainstorm. highvalue-report.md has additional context on ContextResult, CompositeScorer, and QuotaSlice history.
- `.planning/issues/open/` — ~80+ issues. R041 scope = the `spec` and `phase24` prefixed files.
- `spec/src/slicers/knapsack.md` — KnapsackSlice algorithm. Read before S03 (count-quota + knapsack compatibility).

> See `.kata/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R040 — Count-based quota design resolution (primary: M002/S03)
- R041 — Spec quality debt closure (primary: M002/S02)
- R042 — Metadata convention system spec (primary: M002/S04)
- R043 — Cupel.Testing vocabulary design (primary: M002/S05)
- R044 — Future features spec chapters (primary: M002/S06)
- R045 — Fresh brainstorm post-v1.2 (primary: M002/S01)

## Scope

### In Scope

- Brainstorm sprint (explorer/challenger format, 2-3 pairs)
- Spec editorial: close ~8-10 open spec issues (event ordering, item_count sentinel, observer callback normative, greedy zero-token, knapsack floor/truncate, UShapedPlacer pinned edge case, composite pseudocode, ScaledScorer nesting warning)
- Count-based quota: algorithm design, tag semantics, pinned interaction, conflict detection, knapsack path — decision record + pseudocode only
- Metadata convention system: `"cupel:<key>"` namespace reservation, `cupel:trust` + `cupel:source-type` conventions, `MetadataTrustScorer` spec chapter
- Cupel.Testing vocabulary: 10-15 named assertions with precise specs
- Future features spec chapters: DecayScorer, OpenTelemetry verbosity levels, budget simulation API contracts
- Close matching `.planning/issues/open/` issues as each design area completes

### Out of Scope / Non-Goals

- Any implementation code (no .NET or Rust changes)
- v1.3 roadmap (M003) — that comes after this sprint's outputs are complete
- ProfiledPlacer spec (longer-term; not enough signal yet)
- Count-based quota implementation (design only)
- OpenTelemetry package implementation

## Technical Constraints

- Spec files live in `spec/src/` as mdBook markdown. New chapters must be added to `spec/src/SUMMARY.md` to be reachable.
- Design decision records that are too long for a spec chapter can live in `.planning/design/` as standalone markdown files referenced from the spec.
- No code changes in this milestone. If a spec fix requires a conformance vector update, update the vector file and its vendored copy simultaneously (per the drift guard pattern from M001).
- All new spec chapters must follow the established style: pseudocode in labeled `text` fenced blocks, conformance notes in designated sections.

## Integration Points

- `spec/src/SUMMARY.md` — must be updated to include any new spec chapters
- `.planning/issues/open/` — resolved issues should be removed or annotated; track which R041 items are closed per slice
- `.planning/brainstorms/` — S01 output directory (new dated brainstorm folder)
- `spec/conformance/` — if any spec fix requires a new or modified conformance vector, update both `spec/conformance/` and `crates/cupel/conformance/` simultaneously

## Open Questions

- **Count-quota + KnapsackSlice compatibility**: Does count-based minimum inclusion require a fundamentally different KnapsackSlice algorithm (constrained knapsack), or can it be handled in a preprocessing step before the DP? This is the hardest sub-problem in S03.
- **Cupel.Testing error message format**: Should assertion errors include the full SelectionReport in the message body, or just the relevant field? Rich error messages help debugging but create large test output. S05 should specify this.
- **DecayScorer Rust conformance vectors**: Conformance vectors with fixed `referenceTime` are straightforward in .NET (`FakeTimeProvider`) but require thought in Rust (`TimeProvider` equivalent). S06 should specify what `TimeProvider` looks like in the Rust type system.
