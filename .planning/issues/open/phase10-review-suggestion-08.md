---
title: "Add cl100k_base encoding test coverage"
area: tests
priority: low
source: phase-10-pr-review
---

# Add cl100k_base Encoding Test Coverage

The Tiktoken test suite currently covers the default encoding (likely o200k_base). GPT-4 and GPT-3.5-turbo use `cl100k_base`, which has different tokenization behavior. There is no test coverage for this encoding.

## Suggestion

Add a test class or theory covering `cl100k_base` encoding, at minimum validating token counts for known inputs that differ between encodings. A simple sanity check (known string → known count) is sufficient as a regression guard.

Example inputs with known cl100k_base counts:
- `"Hello, world!"` → 4 tokens
- `"The quick brown fox"` → 4 tokens

## Files

- `tests/Wollax.Cupel.Json.Tests/` (new or existing Tiktoken test file)
