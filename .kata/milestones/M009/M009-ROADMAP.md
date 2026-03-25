# M009: CountConstrainedKnapsackSlice + MetadataKeyScorer

**Vision:** Ship `CountConstrainedKnapsackSlice` — the promised upgrade from the D052 guard — giving callers count guarantees (require/cap per kind) over a near-optimal knapsack selection, plus `MetadataKeyScorer` for conditional multiplicative score boosting via metadata keys. Both features land in Rust and .NET with spec chapters and conformance vectors.

## Success Criteria

- `CountConstrainedKnapsackSlice` is constructable in both languages, passes 5 conformance integration tests, and appears in public API exports.
- `MetadataKeyScorer` is constructable in both languages, passes 5 conformance tests, and appears in public API exports.
- Spec chapters `spec/src/slicers/count-constrained-knapsack.md` and `spec/src/scorers/metadata-key.md` exist with zero TBD fields.
- `cargo test --all-targets` green across all crates; `dotnet test` green; `cargo clippy --all-targets -- -D warnings` clean.
- `CHANGELOG.md` unreleased section reflects both new types.

## Key Risks / Unknowns

- **Pre-processing sub-optimality at tight budgets** — Phase 1 commits items by score, reducing the residual budget available to knapsack. If required items are token-heavy, the result can be notably sub-optimal vs. unconstrained knapsack. This is a known and accepted trade-off; the spec must describe it.
- **Cap enforcement after knapsack** — The knapsack in Phase 2 does not know about caps; over-cap items from its output are dropped in Phase 3. This can waste budget (the knapsack selected them but they get dropped). Acceptable for v1; must be documented in spec and tested.
- **`is_count_quota()` / `find_min_budget_for` interaction** — `CountConstrainedKnapsackSlice` enforces count constraints, so it should return `true` for `is_count_quota()` to trigger the monotonicity guard in `find_min_budget_for`. Confirm this in S01 before wiring.

## Proof Strategy

- Pre-processing sub-optimality → retire in S01 by writing a test that shows Phase 1 commits required items and Phase 2 runs standard knapsack on residual; document in spec during S03.
- Cap enforcement → retire in S01 by writing a test where knapsack would select N > cap items of a kind and verifying Phase 3 drops the excess.

## Verification Classes

- Contract verification: `cargo test --all-targets`; `dotnet test`; `cargo clippy --all-targets -- -D warnings`; spec TBD-count == 0
- Integration verification: none (library)
- Operational verification: none
- UAT / human verification: none

## Milestone Definition of Done

This milestone is complete only when all are true:

- `CountConstrainedKnapsackSlice` and `MetadataKeyScorer` exported from public API in both languages
- 5 conformance vectors per new type, all passing
- Spec chapters exist for both, zero TBD fields
- `PublicAPI.Approved.txt` (.NET) updated for both new types
- `cargo test --all-targets` green across all crates; `dotnet test` green; `cargo clippy` clean
- `CHANGELOG.md` unreleased section updated
- R062 and R063 validated

## Requirement Coverage

- Covers: R062, R063
- Partially covers: none
- Leaves for later: R055 (ProfiledPlacer — deferred), R057 (TimestampCoverageReport split — deferred)
- Orphan risks: none

## Slices

- [x] **S01: CountConstrainedKnapsackSlice — Rust implementation** `risk:high` `depends:[]`
  > After this: `CountConstrainedKnapsackSlice` exists in Rust, passes 5 conformance integration tests, and is re-exported from `crates/cupel/src/lib.rs`.

- [x] **S02: CountConstrainedKnapsackSlice — .NET implementation** `risk:medium` `depends:[S01]`
  > After this: `CountConstrainedKnapsackSlice` exists in .NET, passes 5 integration tests, `PublicAPI.Approved.txt` updated, `dotnet test` green.

- [x] **S03: Spec chapters — count-constrained-knapsack + metadata-key** `risk:low` `depends:[S01,S02]`
  > After this: `spec/src/slicers/count-constrained-knapsack.md` and `spec/src/scorers/metadata-key.md` exist with zero TBD fields; `cupel:priority` convention documented.

- [x] **S04: MetadataKeyScorer — Rust + .NET implementation** `risk:low` `depends:[S03]`
  > After this: `MetadataKeyScorer` in both languages, 5 conformance tests passing, public API updated, R063 validated.

## Boundary Map

### S01 → S02

Produces:
- `CountConstrainedKnapsackSlice` struct (Rust) — constructor accepting `Vec<CountQuotaEntry>`, `KnapsackSlice`, `ScarcityBehavior`; `Slicer` impl; `is_count_quota() → true`; `is_knapsack() → false`
- Phase 1 algorithm: commits top-N items per constrained kind by score descending; accumulates `pre_allocated_tokens`; records `CountRequirementShortfall` on scarcity
- Phase 2 algorithm: passes residual `(candidates \ committed, budget - pre_allocated_tokens)` to inner `KnapsackSlice`
- Phase 3 algorithm: drops over-cap items from knapsack output using `CountCapExceeded` exclusion reason
- 5 conformance integration tests in `crates/cupel/tests/`
- Re-exported from `crates/cupel/src/lib.rs`
- CHANGELOG.md entry (unreleased)

Consumes: nothing (first slice)

### S01 → S03

Produces: same as S01 → S02 (S03 writes spec from working implementation)

Consumes: nothing (first slice)

### S02 → S03

Produces:
- `CountConstrainedKnapsackSlice` class (.NET) — constructor accepting `IReadOnlyList<CountQuotaEntry>`, `KnapsackSlice`, `ScarcityBehavior`; `ISlicer` impl
- Phase 1 / Phase 2 / Phase 3 matching Rust semantics exactly
- 5 integration tests in `CountConstrainedKnapsackTests.cs`
- `PublicAPI.Approved.txt` updated

Consumes from S01:
- Phase algorithm design (spec-by-example from Rust tests)

### S03 → S04

Produces:
- `spec/src/slicers/count-constrained-knapsack.md` — algorithm pseudocode (3 phases), OOM guard note, scarcity behavior, cap enforcement, composability, 5 conformance vector stubs
- `spec/src/scorers/metadata-key.md` — algorithm pseudocode, `cupel:priority` convention, composability, construction validation, 5 conformance vector stubs
- Both files: zero TBD fields

Consumes from S01, S02:
- Working implementations in both languages to validate spec accuracy

### S04 → (milestone done)

Produces:
- `MetadataKeyScorer` in Rust (`crates/cupel/src/scorer/metadata_key.rs`) — `MetadataKeyScorer::new(key, value, boost)` constructor; `Scorer` impl; boost `> 0.0` validation; neutral `1.0` for non-matching items; re-exported from `lib.rs`
- `MetadataKeyScorer` in .NET (`src/Wollax.Cupel/Scoring/MetadataKeyScorer.cs`) — matching constructor and `IScorer` impl; `PublicAPI.Approved.txt` updated
- 5 conformance vectors covering: match → boost applied; no-match → neutral; missing key → neutral; zero boost → construction error; negative boost → construction error
- R063 validated

Consumes from S03:
- `spec/src/scorers/metadata-key.md` as the implementation contract
