---
created: 2026-03-15T12:00
title: Clarify included ordering when expected_output is absent
area: conformance
provenance: github:wollax/cupel#79
files:
  - spec/src/conformance/format.md:170-172
---

## Problem

The Ordering subsection states `included` entries appear in placed order "matching the order of `[[expected_output]]` entries". The Optionality section allows diagnostics without output assertions, but doesn't explain what ordering guarantee holds when `[[expected_output]]` is omitted.

## Solution

Add clarifying sentence: "When `[[expected_output]]` is omitted, `included` entries still appear in placed order."
