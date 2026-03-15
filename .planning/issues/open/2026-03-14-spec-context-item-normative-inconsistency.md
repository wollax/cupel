---
created: 2026-03-14T00:00
title: Spec context-item content field normative level inconsistency
area: docs
provenance: local
files:
  - spec/src/data-model/context-item.md:9
  - spec/src/data-model/context-item.md:23
---

## Problem

The field table says `content` "Must be non-null and non-empty" but the Constraints section uses "SHOULD reject construction with null or empty content." These are inconsistent normative levels. The C# implementation only enforces non-null (via `required`), not non-empty.

## Solution

Align normative language. Either use MUST everywhere (and note C# is lenient) or use SHOULD everywhere.
