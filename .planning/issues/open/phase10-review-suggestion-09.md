---
title: "Add Tiktoken+DI end-to-end consumption test"
area: tests
priority: low
source: phase-10-pr-review
---

# Add Tiktoken+DI End-to-End Consumption Test

There is no consumption test that wires `TiktokenTokenCounter` into the DI pipeline and runs a full `CupelPipeline` execution end-to-end. This gap means the integration between the Json companion package and core DI machinery is only exercised implicitly.

## Suggestion

Add a consumption test that:

1. Creates a `ServiceCollection`
2. Calls `AddCupel(...)` with `TiktokenTokenCounter` registered
3. Resolves `CupelPipeline` from the container
4. Runs a pipeline execution with real `ContextItem` inputs
5. Asserts that token counts were computed and the selection result is correct

This validates the full DI registration path, not just unit-level component behavior.

## Files

- `tests/Wollax.Cupel.Json.Tests/` (new consumption test file or class)
