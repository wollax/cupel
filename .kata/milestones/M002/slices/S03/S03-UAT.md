# S03: Count-Based Quota Design — UAT

**Milestone:** M002
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S03 produces a pure design artifact (`.planning/design/count-quota-design.md`). There is no runtime behavior to exercise, no UI to verify, and no code changes introduced. Correctness is fully verifiable by document completeness checks (grep, section presence), absence of TBD fields, and regression confirmation via `cargo test` + `dotnet test`.

## Preconditions

- `.planning/design/count-quota-design.md` exists (written by T03)
- `cargo test --manifest-path crates/cupel/Cargo.toml` is available
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` is available

## Smoke Test

```bash
test -f .planning/design/count-quota-design.md && \
test $(grep -ci "\bTBD\b" .planning/design/count-quota-design.md) -eq 0 && \
echo "PASS: smoke test"
```

Expected: `PASS: smoke test`

## Test Cases

### 1. File completeness — all five decision areas present

```bash
grep -q "Algorithm Architecture" .planning/design/count-quota-design.md && echo "PASS: algorithm section"
grep -q "Tag Non-Exclusivity" .planning/design/count-quota-design.md && echo "PASS: tag section"
grep -q "Pinned Item" .planning/design/count-quota-design.md && echo "PASS: pinned section"
grep -q "Conflict Detection" .planning/design/count-quota-design.md && echo "PASS: conflict section"
grep -q "KnapsackSlice" .planning/design/count-quota-design.md && echo "PASS: knapsack section"
```

**Expected:** All five lines print PASS.

### 2. Pseudocode present

```bash
grep -q "COUNT-DISTRIBUTE-BUDGET" .planning/design/count-quota-design.md && echo "PASS: pseudocode present"
```

**Expected:** `PASS: pseudocode present`

### 3. Zero TBD fields

```bash
TBD_COUNT=$(grep -ci "\bTBD\b" .planning/design/count-quota-design.md)
echo "TBD count: $TBD_COUNT"
test "$TBD_COUNT" -eq 0 && echo "PASS: no TBD fields"
```

**Expected:** `TBD count: 0` and `PASS: no TBD fields`

### 4. Key design rulings present

```bash
grep -q "Decision:" .planning/design/count-quota-design.md && echo "PASS: Decision lines present"
grep -q "CountCapExceeded" .planning/design/count-quota-design.md && echo "PASS: CountCapExceeded present"
grep -q "quota_violations" .planning/design/count-quota-design.md && echo "PASS: quota_violations present"
```

**Expected:** All three lines print PASS.

### 5. No regressions — test suites green

```bash
cargo test --manifest-path crates/cupel/Cargo.toml 2>&1 | tail -2
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj 2>&1 | tail -3
```

**Expected:** Rust: all tests passed. .NET: 583 passed, 0 failed.

## Edge Cases

### Ambiguous language scan

```bash
grep -in "TBD\|open question\|deferred" .planning/design/count-quota-design.md
```

**Expected:** No output (or only lines that reference deferred items with explicit resolution links, not unresolved TBDs).

### Decision one-liner count

```bash
grep -c "^Decision:" .planning/design/count-quota-design.md
```

**Expected:** Exactly 6 (one per design section DI-1 through DI-6).

## Failure Signals

- Any `PASS` line not printing → the corresponding section or keyword is missing from the design record
- `TBD count` non-zero → incomplete design record; locate the TBD with `grep -n "\bTBD\b" .planning/design/count-quota-design.md`
- Rust or .NET test failures → unexpected code change introduced (should not happen — S03 is spec-only)
- `grep -in "TBD\|open question\|deferred" .planning/design/count-quota-design.md` returns lines with no resolution link → record has unresolved open questions disguised as non-TBD language

## Requirements Proved By This UAT

- R040 — Count-based quota design resolution: all five open questions answered (algorithm architecture, tag non-exclusivity semantics, pinned item interaction, conflict detection rules, KnapsackSlice compatibility path); `COUNT-DISTRIBUTE-BUDGET` pseudocode written; `ExclusionReason`/`SelectionReport` backward-compat audit complete (DI-6); design record has zero TBD fields; both test suites pass confirming no regressions.

## Not Proven By This UAT

- Runtime correctness of the COUNT-DISTRIBUTE-BUDGET algorithm — that requires M003 implementation and its own test vectors.
- `CountQuotaSlice` API ergonomics — the `CountQuotaSet` builder API is sketched in the design record but not formally specified; that belongs to the M003 spec chapter or implementation review.
- The `cupel:primary_tag` workaround's interaction with S04's `cupel:` namespace reservation — if S04 formalizes or excludes `cupel:primary_tag`, UAT for that interaction lives in S04.
- S06's use of this design record (count-quota + `FindMinBudgetFor` incompatibility note) — verified at S06 completion time.

## Notes for Tester

- The `rtk dotnet test` wrapper incompatibility with .NET 10's test runner (exit code 5, "Unknown option --nologo") is a tooling issue in the `rtk` wrapper, not a test failure. Use `dotnet test` directly to verify.
- The scratch file `.planning/design/count-quota-design-notes.md` is retained working notes from T01/T02. It is not the deliverable — `.planning/design/count-quota-design.md` is. Do not evaluate the scratch file as part of UAT.
- Run the "ambiguous language scan" edge case if there is any concern about deferred sub-questions hiding behind non-TBD language.
