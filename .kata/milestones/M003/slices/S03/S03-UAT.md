# S03: CountQuotaSlice — Rust + .NET Implementation — UAT

**Milestone:** M003
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: `CountQuotaSlice` is a pure library component with no UI, service lifecycle, or runtime configuration. All behavioral contracts are mechanically verifiable via unit tests, conformance vectors, and build checks. The 5 TOML conformance vectors plus 17 Rust + 13 .NET unit tests cover all 5 design scenarios exhaustively.

## Preconditions

- Rust toolchain available (`cargo --version`)
- .NET SDK available (`dotnet --version`)
- Working directory: repo root (`/Users/wollax/Git/personal/cupel`)

## Smoke Test

```bash
# Rust
cd crates/cupel && cargo test -- count_quota --nocapture 2>&1 | grep -E "passed|failed"

# .NET
dotnet test tests/Wollax.Cupel.Tests/ --treenode-filter "/*/*/CountQuotaSliceTests/*" 2>&1 | tail -5
```

Both commands should show all tests passing with zero failures.

## Test Cases

### 1. Rust: all tests including conformance pass

```bash
cd crates/cupel && cargo test --all-targets 2>&1 | tail -5
```

**Expected:** `117 passed; 0 failed` (or higher as future tests are added); no test failure lines.

### 2. .NET: full suite passes

```bash
dotnet test 2>&1 | tail -5
```

**Expected:** `682 passed, 0 failed` (or higher); no error lines.

### 3. Conformance vectors present in all 3 locations

```bash
ls spec/conformance/required/slicing/count-quota-*.toml | wc -l
ls conformance/required/slicing/count-quota-*.toml | wc -l
ls crates/cupel/conformance/required/slicing/count-quota-*.toml | wc -l
```

**Expected:** Each outputs `5`.

### 4. Drift guard clean

```bash
diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing
```

**Expected:** No output; exit code 0.

### 5. ExclusionReason variants present in both languages

```bash
grep -c "CountCapExceeded\|CountRequireCandidatesExhausted" crates/cupel/src/diagnostics/mod.rs
grep -c "CountCapExceeded\|CountRequireCandidatesExhausted" src/Wollax.Cupel/Diagnostics/ExclusionReason.cs
```

**Expected:** Each outputs `2` (or more).

### 6. PublicAPI.Unshipped.txt updated

```bash
grep -c "CountQuotaSlice\|CountQuotaEntry\|ScarcityBehavior\|CountRequirementShortfall" src/Wollax.Cupel/PublicAPI.Unshipped.txt
```

**Expected:** Output `>= 6` (actual: 22).

## Edge Cases

### KnapsackSlice construction guard (Rust)

```bash
cd crates/cupel && cargo test -- count_quota_knapsack --nocapture
```

**Expected:** Test passes; `CountQuotaSlice::new` returns `Err(SlicerConfig)` when inner slicer is `KnapsackSlice`.

### KnapsackSlice construction guard (.NET)

```bash
dotnet test --treenode-filter "/*/*/CountQuotaSliceTests/KnapsackSliceGuardThrows"
```

**Expected:** Test passes; `ArgumentException` thrown at construction with the documented message.

### Scarcity degrade: shortfall recorded (.NET)

```bash
dotnet test --treenode-filter "/*/*/CountQuotaSliceTests/ScarcityDegrade*"
```

**Expected:** Test passes; `slicer.LastShortfalls.Count > 0` with `SatisfiedCount < RequiredCount`.

## Failure Signals

- Any `failed` tests in `cargo test --all-targets` or `dotnet test`
- `diff -r spec/conformance/required/slicing crates/cupel/conformance/required/slicing` producing output
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` producing error lines
- `grep -c "CountCapExceeded\|CountRequireCandidatesExhausted"` returning `0` in either file

## Requirements Proved By This UAT

- R040 — COUNT-DISTRIBUTE-BUDGET algorithm implemented in both Rust and .NET; all 5 design decisions (DI-1 through DI-6) executed; 5 conformance vectors pass; 35+ tests pass; drift guard clean. R040 is now fully validated.

## Not Proven By This UAT

- `SelectionReport.CountRequirementShortfalls` population via `Pipeline.DryRun` — the standard pipeline does not yet wire slicer shortfalls through `ReportBuilder`; shortfalls only surface via `CountQuotaSlice.LastShortfalls` sidecar (.NET) or direct slicer call (Rust)
- `ExcludedItem.CountCapExceeded` in `SelectionReport.Excluded` — requires pipeline-level wiring of slicer exclusion outputs through `ReportBuilder` (deferred)
- End-to-end integration with OTel bridge (S05) — `CountCapExceeded` and `CountRequireCandidatesExhausted` trace events are not yet visible in ActivitySource output

## Notes for Tester

- `CountRequirementShortfalls` on `SelectionReport` will always be `[]` when accessed via the standard pipeline — this is expected in S03. Use `CountQuotaSlice.LastShortfalls` to inspect shortfalls in .NET tests.
- The tag-nonexclusive conformance scenario tests two independent `ContextKind` entries rather than a single multi-tagged item — this is the correct interpretation since `CountQuotaSlice` operates per-kind.
- Rust `cap_excluded_count` is verified arithmetically in the conformance harness (`total_items - selected_items.len()`), not via `SelectionReport.Excluded`, because cap exclusions are not yet surfaced through the pipeline.
