# Kata State

**Active Milestone:** M005 — cupel-testing crate
**Active Slice:** S03 — Integration tests + publish readiness
**Phase:** Planning

## Recent Decisions
- D130: S02 verification strategy — integration-level (26 tests, 2 per pattern, real mini-pipelines)
- D131: Pattern 13 index-based approach — `Vec<(f64, usize)>` + `HashSet<usize>` edge positions (f64/Hash constraint resolved)
- D132: ExclusionReason variant matching via `std::mem::discriminant` (not `==`)
- D133: Ordering tests use PriorityScorer + `.priority(n)` for deterministic scores (RecencyScorer requires timestamps)

## Blockers
- None

## Next Action
S02 complete. Begin S03: integration tests on real `Pipeline::run_traced()` output + `cargo package` readiness + publish metadata.

## Milestone Progress (M005)
- [x] S01: Crate scaffold + chain plumbing — DONE
- [x] S02: 13 assertion patterns — 26 tests pass, both crates clippy-clean — DONE
- [ ] S03: Integration tests + publish readiness
