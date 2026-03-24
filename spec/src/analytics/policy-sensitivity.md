# Policy Sensitivity

Policy sensitivity runs multiple policy configurations over the same item set and computes a structured diff showing which items changed inclusion status across those configurations. It is the primary tool for comparing how different scorer, slicer, or placer choices affect selection — without constructing N full pipeline objects.

---

## API

### .NET

**`CupelPipeline.DryRunWithPolicy`** — run a single policy configuration without constructing a new pipeline:

```csharp
ContextResult DryRunWithPolicy(
    IReadOnlyList<ContextItem> items,
    ContextBudget budget,
    CupelPolicy policy)
```

Defined on `CupelPipeline`. Internally constructs a temporary pipeline using `CreateBuilder().WithPolicy(policy).WithBudget(budget)` and calls `DryRunWithBudget`. The returned `ContextResult.Report` is a `SelectionReport` covering all included and excluded items.

**`PolicySensitivityExtensions.PolicySensitivity` (policy overload)** — run multiple policy configurations and diff them:

```csharp
// Policy-accepting overload
PolicySensitivityReport PolicySensitivity(
    IReadOnlyList<ContextItem> items,
    ContextBudget budget,
    params (string Label, CupelPolicy Policy)[] variants)
```

Extension method on `CupelPipeline` in `Wollax.Cupel.Diagnostics`. The pipeline-based overload (accepting `(string Label, CupelPipeline Pipeline)[]`) remains available for callers needing full pipeline control.

Throws `ArgumentException` if fewer than 2 variants are supplied.

### Rust

**`cupel::analytics::policy_sensitivity`** — run multiple `Policy` configurations:

```rust
pub fn policy_sensitivity(
    items: &[ContextItem],
    budget: &ContextBudget,
    variants: &[(impl AsRef<str>, &Policy)],
) -> Result<PolicySensitivityReport, CupelError>
```

In `cupel::analytics`. Returns `Err(CupelError::PipelineConfig(...))` if fewer than 2 variants are provided.

**`cupel::analytics::policy_sensitivity_from_pipelines`** — pipeline-based variant:

```rust
pub fn policy_sensitivity_from_pipelines(
    items: &[ContextItem],
    budget: &ContextBudget,
    variants: &[(impl AsRef<str>, &Pipeline)],
) -> Result<PolicySensitivityReport, CupelError>
```

Use when `Policy` cannot express the required slicer (e.g., `CountQuotaSlice` — see Language Notes below).

---

## Types

### `PolicySensitivityReport`

```rust
// Rust
pub struct PolicySensitivityReport {
    /// Per-variant selection results, in input order.
    pub variants: Vec<(String, SelectionReport)>,
    /// Items whose inclusion status differed across at least two variants.
    pub diffs: Vec<PolicySensitivityDiffEntry>,
}
```

```csharp
// .NET
public sealed class PolicySensitivityReport
{
    public IReadOnlyList<(string Label, SelectionReport Report)> Variants { get; init; }
    public IReadOnlyList<PolicySensitivityDiffEntry> Diffs { get; init; }
}
```

### `PolicySensitivityDiffEntry`

```rust
// Rust
pub struct PolicySensitivityDiffEntry {
    /// The content string identifying the item.
    pub content: String,
    /// One entry per variant, in input order.
    pub statuses: Vec<(String, ItemStatus)>,
}
```

```csharp
// .NET
public sealed class PolicySensitivityDiffEntry
{
    public string Content { get; init; }
    public IReadOnlyList<(string Label, ItemStatus Status)> Statuses { get; init; }
}
```

### `ItemStatus`

```rust
// Rust
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ItemStatus {
    Included,
    Excluded,
}
```

```csharp
// .NET
public enum ItemStatus { Included, Excluded }
```

---

## Diff Semantics

**Content-keyed matching.** Items are identified by their content string. Two items from different variant runs are considered the same item if their `content` fields are equal (ordinal comparison). `ContextItem` object identity is not used.

**Swing-only filter.** Only items where at least two variants disagree on inclusion appear in `diffs`. An item included by all variants, or excluded by all variants, does not appear in `diffs`.

**`variants` preserves input order.** `PolicySensitivityReport.variants` reflects the input `variants` slice/array in the same order it was supplied.

**`diffs` order is unspecified.** The order of entries in `PolicySensitivityReport.diffs` is implementation-defined. Callers must not rely on a particular ordering of diff entries.

---

## Minimum Variants

Implementations MUST require at least 2 variants. Fewer variants MUST return an error:

- **Rust:** `Err(CupelError::PipelineConfig("policy_sensitivity requires at least 2 variants"))`
- **.NET:** `ArgumentException` with message `"At least two variants are required for a sensitivity comparison."`

A single-variant call cannot produce a meaningful diff and is always a caller error.

---

## Explicit Budget Parameter

Both `DryRunWithPolicy` and `policy_sensitivity` take an explicit `budget` parameter. There is no option to inherit a budget from the pipeline or policy.

**Rationale:** A `CupelPolicy` does not carry a budget. An implicit budget would silently apply the pipeline's own stored budget to a different policy configuration — producing surprising results when the pipeline budget was set for a different context size. Making the budget explicit forces the caller to confirm the comparison baseline and eliminates the ambiguity.

---

## Language Notes

**`CupelPolicy` cannot express `CountQuotaSlice`.** The `SlicerType` enum in the .NET `CupelPolicy` has no `CountQuota` variant. Callers requiring count-quota fork diagnostics MUST use the pipeline-based overload:

- **.NET:** `PolicySensitivity(items, budget, params (string Label, CupelPipeline Pipeline)[] variants)`
- **Rust:** `policy_sensitivity_from_pipelines(items, budget, variants: &[(impl AsRef<str>, &Pipeline)])`

This is a known `CupelPolicy` coverage gap, not a limitation of the underlying diff engine.

---

## Examples

### .NET — 2-variant comparison

```csharp
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;

var items = new List<ContextItem>
{
    new ContextItemBuilder("item-a", 40).WithPriority(10).Build(),
    new ContextItemBuilder("item-b", 40).WithPriority(1).Build(),
};
var budget = new ContextBudget(maxTokens: 50, targetTokens: 40);

var policyA = new CupelPolicy { Intent = "code-review" };
var policyB = new CupelPolicy { Intent = "rag" };

var pipeline = CupelPipeline.CreateBuilder().WithBudget(budget).Build();
var report = pipeline.PolicySensitivity(items, budget,
    ("code-review", policyA),
    ("rag", policyB));

Console.WriteLine($"Swinging items: {report.Diffs.Count}");
foreach (var diff in report.Diffs)
{
    Console.WriteLine($"  {diff.Content}:");
    foreach (var (label, status) in diff.Statuses)
        Console.WriteLine($"    {label}: {status}");
}
```

### Rust — 2-variant comparison

```rust
use cupel::{
    ChronologicalPlacer, ContextBudget, ContextItemBuilder, GreedySlice,
    OverflowStrategy, Policy, PolicyBuilder, PriorityScorer, ReflexiveScorer,
    policy_sensitivity,
};
use std::sync::Arc;
use std::collections::HashMap;

let items = vec![
    ContextItemBuilder::new("item-a", 40)
        .priority(10)
        .future_relevance_hint(0.1)
        .build()
        .unwrap(),
    ContextItemBuilder::new("item-b", 40)
        .priority(1)
        .future_relevance_hint(0.9)
        .build()
        .unwrap(),
];
let budget = ContextBudget::new(50, 40, 0, HashMap::new(), 0.0).unwrap();

let policy_priority = PolicyBuilder::new()
    .scorer(Arc::new(PriorityScorer))
    .slicer(Arc::new(GreedySlice))
    .placer(Arc::new(ChronologicalPlacer))
    .overflow_strategy(OverflowStrategy::Throw)
    .deduplication(true)
    .build()
    .unwrap();

let policy_reflexive = PolicyBuilder::new()
    .scorer(Arc::new(ReflexiveScorer))
    .slicer(Arc::new(GreedySlice))
    .placer(Arc::new(ChronologicalPlacer))
    .overflow_strategy(OverflowStrategy::Throw)
    .deduplication(true)
    .build()
    .unwrap();

let variants: Vec<(&str, &Policy)> = vec![
    ("priority", &policy_priority),
    ("reflexive", &policy_reflexive),
];
let report = policy_sensitivity(&items, &budget, &variants).unwrap();

println!("Swinging items: {}", report.diffs.len());
for diff in &report.diffs {
    println!("  {}:", diff.content);
    for (label, status) in &diff.statuses {
        println!("    {}: {:?}", label, status);
    }
}
```
