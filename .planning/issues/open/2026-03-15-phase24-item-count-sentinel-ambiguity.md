---
created: 2026-03-15T00:00
title: "Phase 24: item_count on item-level events ambiguous (sentinel vs meaningful)"
area: docs
provenance: local
files:
  - spec/book/events.md
---

## Problem

`item_count: 1` on item-level events is ambiguous — it is unclear whether this is a sentinel value with no semantic meaning or a meaningful count. The spec does not clarify this distinction, which leaves implementors guessing.

## Solution

Add an explicit note in `events.md` stating that `item_count: 1` on item-level events is a sentinel value and carries no meaningful count semantics.
