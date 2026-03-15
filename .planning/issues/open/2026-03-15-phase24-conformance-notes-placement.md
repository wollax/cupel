---
created: 2026-03-15T00:00
title: "Phase 24: Conformance Notes placement after optional sections in trace-collector.md"
area: docs
provenance: local
files:
  - spec/book/trace-collector.md
---

## Problem

In `trace-collector.md`, the Conformance Notes section appears after the optional Observer Callback section. This placement makes mandatory requirements easy to overlook when readers stop reading before optional content.

## Solution

Move the Conformance Notes section before any optional sections so that mandatory requirements are encountered before optional implementation details.
