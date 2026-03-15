---
title: "Add AllValues collection to ContextKind and ContextSource smart enums"
area: core-models
priority: low
source: pr-review-phase-1
---

# Static AllValues for smart enums

Consider adding `public static IReadOnlyList<ContextKind> AllValues` (and same for ContextSource) to make discovery of well-known values easier for consumers.

**Consideration:** Low priority — well-known values are discoverable via static fields. Useful if/when serialization needs to enumerate known types.
