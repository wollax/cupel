---
title: "Consider exposing ContextKindDictionaryConverter for consumer custom types"
area: serialization
priority: low
source: pr-review-phase-1
---

# ContextKindDictionaryConverter is internal

The converter for `Dictionary<ContextKind, int>` is internal. If consumers define types with similar dictionary properties, they'd need to write their own converter.

**Consideration:** Defer to Phase 9 (Serialization & JSON Package) where the `Wollax.Cupel.Json` package will handle extensible serialization. Internal is correct for now.
