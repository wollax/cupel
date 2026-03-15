---
created: 2026-03-15T00:00
title: "Phase 24: no JSON example for multi-field reserved variants in exclusion-reasons.md"
area: docs
provenance: local
files:
  - spec/book/exclusion-reasons.md
---

## Problem

Multi-field reserved variants such as `QuotaCapExceeded` in `exclusion-reasons.md` have no JSON example. Without a concrete example, implementors must infer the serialized structure from the field descriptions alone, which increases the risk of interoperability issues.

## Solution

Add a JSON example block for each multi-field reserved variant to make the expected serialized form unambiguous.
