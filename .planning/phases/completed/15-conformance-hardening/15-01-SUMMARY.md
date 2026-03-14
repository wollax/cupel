# 15-01 Summary: Scoring Conformance Vectors

## Result: PASS

All 13 scoring conformance test vectors authored in `spec/conformance/required/scoring/` and verified byte-exact against Rust crate copies.

## Tasks

| # | Task | Commit | Status |
|---|------|--------|--------|
| 1 | Author individual scorer vectors (11 files) | `d2b3e5a` | Done |
| 2 | Author composite and scaled scorer vectors (2 files) | `0a64bde` | Done |

## Artifacts

- `spec/conformance/required/scoring/recency-basic.toml`
- `spec/conformance/required/scoring/recency-null-timestamps.toml`
- `spec/conformance/required/scoring/priority-basic.toml`
- `spec/conformance/required/scoring/priority-null.toml`
- `spec/conformance/required/scoring/kind-default-weights.toml`
- `spec/conformance/required/scoring/kind-unknown.toml`
- `spec/conformance/required/scoring/frequency-basic.toml`
- `spec/conformance/required/scoring/reflexive-basic.toml`
- `spec/conformance/required/scoring/reflexive-null.toml`
- `spec/conformance/required/scoring/tag-basic.toml`
- `spec/conformance/required/scoring/tag-no-tags.toml`
- `spec/conformance/required/scoring/scaled-basic.toml`
- `spec/conformance/required/scoring/composite-weighted.toml`

## Verification

```
find spec/conformance/required/scoring -name "*.toml" | wc -l  → 13
diff -r spec/conformance/required/scoring/ [rust-crate]/scoring/  → no differences
```

## Notes

- All vectors derived from spec algorithm pseudocode (recency, priority, kind, frequency, reflexive, tag, scaled, composite)
- Byte-exact parity confirmed with `cmp -s` for each file individually
- Scorer coverage: 8 scorer types, 13 vectors (recency x2, priority x2, kind x2, frequency x1, reflexive x2, tag x2, scaled x1, composite x1)
