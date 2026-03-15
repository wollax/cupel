# Inconsistent test assertion style in CustomScorerTests

**Source:** Phase 9 PR review
**Priority:** Low
**Area:** tests

## Description

`CustomScorerTests.cs` uses `Assert.ThrowsAsync<T>(() => Task.FromResult(...))` pattern to wrap synchronous methods, while `ValidationTests.cs` and `ErrorMessageTests.cs` use the cleaner `await Assert.That(action).Throws<T>()` pattern.

Should be consistent across all JSON test files.

## Files

- `tests/Wollax.Cupel.Json.Tests/CustomScorerTests.cs` (lines 60-61, 90-91, 103-104, etc.)
