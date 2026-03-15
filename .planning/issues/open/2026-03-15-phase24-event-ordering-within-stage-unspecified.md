---
created: 2026-03-15T00:00
title: "Phase 24: event ordering within a stage unspecified"
area: docs
provenance: local
files:
  - spec/book/events.md
  - spec/book/trace-collector.md
---

## Problem

Neither `events.md` nor `trace-collector.md` defines whether item-level events must precede or follow the stage-level event for the same pipeline stage. Without this ordering guarantee, conformance test vectors cannot reliably verify event ordering.

## Solution

Add an explicit ordering rule: define whether item-level events for a stage appear before or after the corresponding stage-level event, and update conformance test vectors to assert this ordering.
