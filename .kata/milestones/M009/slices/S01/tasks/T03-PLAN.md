---
estimated_steps: 3
estimated_files: 1
---

# T03: Update CHANGELOG and finalize

**Slice:** S01 — CountConstrainedKnapsackSlice — Rust implementation
**Milestone:** M009

## Description

Add a CHANGELOG.md entry for `CountConstrainedKnapsackSlice` in the unreleased section. This is the final gate for S01 per the milestone Definition of Done, which requires the CHANGELOG unreleased section to reflect both new types (the second type, `MetadataKeyScorer`, lands in S04).

## Steps

1. Open `CHANGELOG.md` and locate the `## [Unreleased]` section (or equivalent); add a new entry under "Added" describing `CountConstrainedKnapsackSlice` — mention it accepts `Vec<CountQuotaEntry>`, `KnapsackSlice`, and `ScarcityBehavior`; briefly describe the 3-phase algorithm; note it re-uses M006's `CountQuotaEntry` and `ScarcityBehavior`

2. Run `rtk cargo test --all-targets` to confirm tests still green after the doc-only change

3. Confirm `grep "CountConstrainedKnapsackSlice" CHANGELOG.md` exits 0

## Must-Haves

- [ ] CHANGELOG.md has a `CountConstrainedKnapsackSlice` entry under the unreleased section
- [ ] `cargo test --all-targets` still green after the change

## Verification

- `grep "CountConstrainedKnapsackSlice" CHANGELOG.md` exits 0
- `rtk cargo test --all-targets` exits 0

## Observability Impact

- Signals added/changed: None
- How a future agent inspects this: `grep -n "CountConstrainedKnapsackSlice" CHANGELOG.md`
- Failure state exposed: None

## Inputs

- `CHANGELOG.md` — existing unreleased section format to match
- Completed T01 + T02 — `CountConstrainedKnapsackSlice` is implemented and tested

## Expected Output

- `CHANGELOG.md` with `CountConstrainedKnapsackSlice` entry in unreleased section
- All tests still passing
