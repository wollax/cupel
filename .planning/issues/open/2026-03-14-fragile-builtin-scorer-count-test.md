---
created: 2026-03-14T17:25
title: BuiltInScorerTypes count test hard-codes magic number 7
area: testing
provenance: github:wollax/cupel#36
files:
  - tests/Wollax.Cupel.Json.Tests/CustomScorerTests.cs:47
---

## Problem

`BuiltInScorerTypes_DerivedFromEnum_HasExpectedCount` hard-codes `7`. If a new enum member is added, the test fails for the wrong reason — reads as a regression rather than a reminder to update.

## Solution

Either derive the count from `Enum.GetValues<ScorerType>().Length` with an explanatory comment, or replace with a set-equality check against the full expected set.
