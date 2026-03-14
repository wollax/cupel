---
created: 2026-03-14T17:25
title: RoundTripTests NullOptionalFields doesn't verify innerScorer omission symmetry
area: testing
provenance: github:wollax/cupel#43
files:
  - tests/Wollax.Cupel.Json.Tests/RoundTripTests.cs:190
---

## Problem

`NullOptionalFields_OmittedInJson` asserts `DoesNotContain("\"innerScorer\"")` for a non-Scaled policy, but doesn't verify that a Scaled scorer *does* include `"innerScorer"` in the output. The presence/absence symmetry for `innerScorer` is not explicitly tested in one place.

## Solution

Add a complementary assertion (or a separate test) that verifies a Scaled policy JSON output *does* contain `"innerScorer"`. This is covered separately in `ScaledScorer_RoundTrips` but the symmetry isn't explicit.
