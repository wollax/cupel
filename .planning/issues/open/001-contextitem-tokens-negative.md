---
title: "Consider validating ContextItem.Tokens is non-negative"
area: core-models
priority: low
source: pr-review-phase-1
---

# ContextItem.Tokens allows negative values

`ContextItem.Tokens` is a plain `int` with no validation. Negative values are technically possible.

**Decision:** By design — token counting is the caller's responsibility per PROJECT.md (API-05). Adding validation here would violate the "caller pre-counts tokens" contract.

**Revisit if:** Users report confusion or bugs from negative token values in practice.
