---
title: "Phase 8 review suggestions — tests"
source: PR review (Phase 8)
priority: low
area: test-coverage
---

# Phase 8 Test Review Suggestions

Suggestions from the Phase 8 PR review that were not fixed immediately.

## Tests

1. **`CupelPresetsTests` — `Description` property never asserted** — No test verifies the description string on any preset.
2. **`TryGetPolicy` case-insensitive lookup not explicitly tested** — Only `GetPolicy` has a case-insensitivity test.
3. **`PolicyIntegrationTests` preset tests could be parameterized** — 7 structurally identical tests could be collapsed into one `[Arguments]`-parameterized test.
4. **`QuotaEntryTests` boundary values tested together** — `minPercent: 100.0` alone and `maxPercent: 0.0` alone not tested independently.
