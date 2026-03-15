---
created: 2026-03-15T00:00
title: "Phase 24: Observer Callback normative status unlabeled in trace-collector.md"
area: docs
provenance: local
files:
  - spec/book/trace-collector.md
---

## Problem

The Observer Callback section in `trace-collector.md` does not carry an explicit normative label. It is unclear whether implementations are required to support it (normative) or may choose to support it (non-normative). This ambiguity complicates conformance assessment.

## Solution

Either label the section as non-normative explicitly, or promote it to a conformance note using MAY language to clarify that support is optional.
