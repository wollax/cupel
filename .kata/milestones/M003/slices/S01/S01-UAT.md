# S01: DecayScorer — Rust + .NET Implementation — UAT

**Milestone:** M003
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: DecayScorer is a pure scoring library with no UI, service lifecycle, or external I/O. All behavior is verifiable by running `cargo test` and `dotnet test` with the 5 conformance vectors and unit tests serving as the definitive proof of correctness. Human experience testing adds nothing that the test suite does not already cover.

## Preconditions

- Rust toolchain installed (`cargo` available); run from `crates/cupel/` directory
- .NET 10 SDK installed (`dotnet` available); run from project root
- No uncommitted changes that would affect the test suite

## Smoke Test

Run from project root:

```bash
cd crates/cupel && cargo test --all-targets 2>&1 | grep "test result"
cd ../../ && dotnet test 2>&1 | grep -E "total:|failed:"
```

Expected: all test results show `ok` with 0 failed; dotnet shows `failed: 0`.

## Test Cases

### 1. Rust — all decay conformance tests pass

1. `cd crates/cupel`
2. `cargo test -- decay_ 2>&1`
3. **Expected:** 5 tests reported (`decay_exponential_half_life`, `decay_future_dated`, `decay_null_timestamp`, `decay_step_second_window`, `decay_window_at_boundary`) — all `ok`; exit code 0

### 2. Rust — full test suite unregressed

1. `cd crates/cupel`
2. `cargo test --all-targets 2>&1 | grep "test result"`
3. **Expected:** All `test result: ok`; 0 failed; ≥43 tests passed total

### 3. Drift guard — spec and crate conformance vectors match

1. From project root: `diff -r spec/conformance/required/scoring crates/cupel/conformance/required/scoring`
2. **Expected:** No output (exit 0); any output indicates drift and is a failure

### 4. .NET — all decay scorer tests pass

1. From project root: `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj 2>&1 | tail -10`
2. **Expected:** 663 passed, 0 failed; includes DecayScorerTests for all 5 conformance scenarios

### 5. .NET build — 0 errors (PublicAPI analyzer clean)

1. From project root: `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep -c "error"`
2. **Expected:** `0` (the grep count for "error" is zero)

## Edge Cases

### Future-dated item clamping

1. The `decay-future-dated.toml` conformance vector tests an item whose timestamp is in the future relative to `reference_time`
2. `cargo test -- decay_future_dated --nocapture 2>&1`
3. **Expected:** Test passes with score 1.0 (age clamped to zero → maximum score for all curve types)

### Null timestamp fallback

1. The `decay-null-timestamp.toml` vector tests an item with no timestamp
2. `cargo test -- decay_null_timestamp --nocapture 2>&1`
3. **Expected:** Test passes with score matching `null_timestamp_score` (0.5 in the vector)

### Step curve window fallthrough

1. The `decay-step-second-window.toml` vector tests an item that falls into the second window
2. `cargo test -- decay_step_second_window --nocapture 2>&1`
3. **Expected:** Test passes with score 0.5 (second window score; item age 6h falls in the 24h window)

### Window boundary exclusion

1. The `decay-window-at-boundary.toml` vector tests an item whose age equals maxAge exactly
2. `cargo test -- decay_window_at_boundary --nocapture 2>&1`
3. **Expected:** Test passes with score 0.0 (age == maxAge → excluded from window → score 0.0)

## Failure Signals

- Any `FAILED` line in `cargo test` output → regression in existing tests or new decay tests broken
- `diff` producing output → spec and crate conformance vectors have drifted; the vectors in one location were edited without updating the other
- `dotnet test` showing `failed: N` → .NET DecayScorerTests or existing tests broken
- `grep -c "error"` returning non-zero from dotnet build → PublicAPI analyzer violation (new public type/member not declared in `PublicAPI.Unshipped.txt`) or build error

## Requirements Proved By This UAT

- R020 (DecayScorer with TimeProvider injection) — proves: DecayScorer is implemented in both Rust (.NET; conformance vectors pass in both; injected TimeProvider works correctly for deterministic testing; all three curve types (Exponential, Window, Step) produce correct scores; null timestamp fallback and future-dated clamping work correctly in both languages

## Not Proven By This UAT

- Runtime behavior in production workloads (no service lifecycle; library only)
- Performance under large item sets or high call rates (no benchmarks in this slice)
- Cross-language wire compatibility (each language has independent implementation; no shared serialization in this slice)
- S02 (MetadataTrustScorer), S03 (CountQuotaSlice), S04–S06 — these remain unproven until their respective slices complete

## Notes for Tester

- All test cases are mechanical and repeatable — no human judgment required. If the commands above all exit 0 with the expected outputs, the slice is verified.
- The 5 conformance vectors are the authoritative reference for DecayScorer behavior. If a test fails, compare the vector's `expected_score` against what the scorer returns using `cargo test -- <test_name> --nocapture`.
- The `.Duration()` vs explicit-zero-clamp distinction in .NET is subtle — the correct behavior is already baked into the tests (future-dated item must score 1.0, not some other value). Trust the tests.
