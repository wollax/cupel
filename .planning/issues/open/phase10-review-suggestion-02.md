---
title: "Strengthen token count assertions in consumption tests"
area: tests
priority: low
source: phase-10-pr-review
---

# Strengthen Token Count Assertions in Consumption Tests

Consumption tests currently assert `> 0` for token counts where exact values are knowable from fixed input strings. These assertions should be tightened to exact equality checks.

## Suggestion

For example, `"Hello, world!"` tokenizes to exactly 4 tokens with cl100k_base/o200k_base. Where inputs are fixed literals, assert the precise expected value (e.g., `IsEqualTo(4)`) rather than `IsGreaterThan(0)`.

This makes tests more meaningful as regression guards — a silent tokenizer change would otherwise go undetected.

## Files

- `tests/Wollax.Cupel.Json.Tests/` (consumption test files using token count assertions)
