---
created: 2026-03-14T00:00
title: Add unmaintained advisory warning to deny.toml
area: ci
provenance: github:wollax/cupel#55
files:
  - crates/cupel/deny.toml:3
---

## Problem

`deny.toml` [advisories] section only sets `yanked = "deny"`. Adding `unmaintained = "warn"` would proactively flag dependencies that are no longer maintained, improving supply chain hygiene.

## Solution

Add `unmaintained = "warn"` under `[advisories]` in `crates/cupel/deny.toml`.
