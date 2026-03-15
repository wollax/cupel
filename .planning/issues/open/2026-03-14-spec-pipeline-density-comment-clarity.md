---
created: 2026-03-14T00:00
title: Spec pipeline test vector density comment clarity
area: docs
provenance: local
files:
  - conformance/required/pipeline/greedy-chronological.toml:23
---

## Problem

Inline comment for jan's density says `density = 0.0/200 = 0.0`. While correct, it could mislead implementors into thinking zero-score is a special case. It's the normal density path (only zero tokens triggers MAX_FLOAT).

## Solution

Clarify in the comment that this is the normal density calculation path.
