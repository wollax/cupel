---
title: "Consider ITokenCounter interface in core package"
area: api-design
priority: low
source: phase-10-pr-review
---

# Consider ITokenCounter Interface in Core Package

There is currently no `ITokenCounter` abstraction in the core package. Token counting is a concrete concern tied to the Json companion package. Adding an interface to core would improve testability and allow consumers to swap implementations without taking a dependency on the Json package.

## Suggestion

Add `ITokenCounter` to `Wollax.Cupel` (core):

```csharp
public interface ITokenCounter
{
    int CountTokens(string text);
}
```

`TiktokenTokenCounter` in `Wollax.Cupel.Json` would then implement this interface. Consumers writing unit tests could supply a `FakeTokenCounter` without referencing Tiktoken.

## Trade-offs

- Core package gains a new public surface
- Companion package becomes optional at the abstraction level, not just at the DI level
- Enables future alternative implementations (e.g., a HuggingFace tokenizer package)

## Files

- `src/Wollax.Cupel/` (new interface file)
- `src/Wollax.Cupel.Json/TiktokenTokenCounter.cs` (implement the interface)
