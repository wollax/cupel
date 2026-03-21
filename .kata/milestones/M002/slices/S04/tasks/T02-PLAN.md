---
estimated_steps: 4
estimated_files: 2
---

# T02: Wire chapter into spec navigation and verify regressions

**Slice:** S04 — Metadata Convention System Spec
**Milestone:** M002

## Description

Update `spec/src/SUMMARY.md` and `spec/src/scorers.md` to make the new MetadataTrustScorer chapter reachable and discoverable. Then run regression checks to confirm no test failures were introduced by the spec-only changes.

mdBook silently drops chapters not listed in SUMMARY.md — the SUMMARY.md entry is the reachability gate. The scorers.md Scorer Summary table is the discovery surface for all scorers; omitting MetadataTrustScorer from it would leave callers unable to find it from the index.

## Steps

1. **Update `spec/src/SUMMARY.md`**: Add `  - [MetadataTrustScorer](scorers/metadata-trust.md)` on a new line after the `[ScaledScorer](scorers/scaled.md)` entry (currently line 26). Preserve the 2-space indent used by all other scorer entries.

2. **Update `spec/src/scorers.md` Scorer Summary table**: Add a new row after the ScaledScorer row:
   ```
   | [MetadataTrustScorer](scorers/metadata-trust.md) | `metadata["cupel:trust"]` | [0.0, 1.0] | Passthrough of caller-provided trust value from item metadata |
   ```

3. **Update `spec/src/scorers.md` Absolute Scorers list**: Add MetadataTrustScorer to the Absolute Scorers bullet list under "Scorer Categories" (alongside KindScorer, TagScorer, ReflexiveScorer). Add: `- **MetadataTrustScorer** — passthrough of \`cupel:trust\` metadata value`

4. **Run regression checks**:
   ```bash
   cargo test --manifest-path crates/cupel/Cargo.toml
   dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
   ```

## Must-Haves

- [ ] `spec/src/SUMMARY.md` contains `metadata-trust` link after ScaledScorer
- [ ] `spec/src/scorers.md` Scorer Summary table has MetadataTrustScorer row with correct output range [0.0, 1.0]
- [ ] `spec/src/scorers.md` Absolute Scorers category list includes MetadataTrustScorer
- [ ] `cargo test --manifest-path crates/cupel/Cargo.toml` exits 0
- [ ] `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0

## Verification

```bash
# SUMMARY.md links the chapter
grep -q "metadata-trust" spec/src/SUMMARY.md && echo "PASS: SUMMARY.md linked" || echo "FAIL"

# scorers.md table updated
grep -q "MetadataTrustScorer" spec/src/scorers.md && echo "PASS: scorers.md updated" || echo "FAIL"

# Rust regression
cargo test --manifest-path crates/cupel/Cargo.toml && echo "PASS: Rust tests"

# .NET regression
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj && echo "PASS: .NET tests"
```

## Observability Impact

- Signals added/changed: None (navigation wiring only; no runtime components)
- How a future agent inspects this: `grep -q "metadata-trust" spec/src/SUMMARY.md`; `mdbook build spec/` to verify rendered output includes chapter in sidebar
- Failure state exposed: A missing SUMMARY.md entry would cause mdBook to silently omit the chapter from the built site — detectable by grepping SUMMARY.md or checking the built HTML

## Inputs

- `spec/src/scorers/metadata-trust.md` — must exist (T01 output) before this task runs
- `spec/src/SUMMARY.md` — existing file; ScaledScorer entry is the insertion anchor
- `spec/src/scorers.md` — existing file; ScaledScorer row in Scorer Summary table is the insertion anchor

## Expected Output

- `spec/src/SUMMARY.md` — updated with MetadataTrustScorer entry after ScaledScorer
- `spec/src/scorers.md` — updated Scorer Summary table row and Absolute Scorers category entry
- Test suite output confirming 0 regressions (Rust: 35 tests pass; .NET: 583+ tests pass)
