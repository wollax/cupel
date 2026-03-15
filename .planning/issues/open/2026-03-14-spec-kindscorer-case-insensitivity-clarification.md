---
created: 2026-03-14T00:00
title: Spec KindScorer case-insensitivity source clarification
area: docs
provenance: local
files:
  - spec/src/scorers/kind.md:35
  - spec/src/scorers/kind.md:74
---

## Problem

Spec says KindScorer uses "case-insensitive lookup" implying it's a dictionary option. The actual mechanism is ContextKind's inherent case-insensitive equality. Implementors need to know case-insensitivity is a property of ContextKind equality, not an ad-hoc dictionary feature.

## Solution

Clarify that case-insensitivity comes from ContextKind equality contract (as documented in enumerations.md), not from the dictionary lookup itself.
