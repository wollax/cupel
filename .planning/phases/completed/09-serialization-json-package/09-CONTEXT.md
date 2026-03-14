# Phase 9: Serialization & JSON Package - Context

**Gathered:** 2026-03-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Enable `CupelPolicy` and related types to round-trip through JSON via a separate `Wollax.Cupel.Json` package. Source-generated serialization with polymorphic type discriminators. Supports consumer-defined scorer registration for deserialization. JSON is the only serialization format — no YAML or other formats.

</domain>

<decisions>
## Implementation Decisions

### JSON document shape
- **camelCase** property naming throughout (matches STJ convention and existing `[JsonPropertyName]` attributes)
- **Budget is a nested object** (`"budget": { "maxTokens": ..., "marginPercentage": ... }`) — mirrors the CupelPolicy/ContextBudget type hierarchy
- Quota shape and top-level metadata fields (name/description) are Claude's discretion based on existing model shapes

### Type discriminator strategy
- **`$type`** as the polymorphic discriminator property — STJ's native `[JsonDerivedType]` support
- **Short lowercase discriminator values** for built-in types:
  - Scorers: `recency`, `priority`, `kind`, `tag`, `frequency`, `reflexive`, `composite`, `scaled`
  - Slicers: `greedy`, `knapsack`, `stream`
  - Placers: `uShaped`, `chronological`
- **Slicer and placer are single polymorphic objects** (not arrays, not string shorthand) — always `{ "$type": "...", ... }` form
- Custom type discriminator naming convention is Claude's discretion

### Custom scorer registration
- **Scorers only** — no custom slicer/placer registration in this phase
- Registration API shape (options object vs registry) is Claude's discretion based on STJ source-gen patterns
- Factory signature (parameterless vs JsonElement) is Claude's discretion based on how built-in scorers handle config
- **Unknown `$type` fails immediately** with a descriptive exception listing registered types and suggesting `RegisterScorer()`

### Error handling & leniency
- **Unknown JSON properties are silently ignored** — forward-compatibility, STJ default
- **Validation at deserialization time** — invalid values (negative tokens, out-of-range percentages, empty scorer lists) produce immediate errors, not deferred to `Build()`
- **Path-aware error messages** — errors include JSON path to the problem (e.g., `$.scorers[1].weight: must be > 0, got -0.5`)
- Exception type choice (custom vs JsonException) is Claude's discretion based on .NET library conventions

### Claude's Discretion
- Quota JSON shape (array of objects vs kind-keyed map)
- Whether to include optional `name`/`description` metadata fields on the policy root
- Custom type discriminator naming convention (free-form vs prefixed)
- Registration API design (options object vs separate registry)
- Factory signature design (parameterless vs config-aware)
- Exception type (custom `CupelJsonException` vs `JsonException`)

</decisions>

<specifics>
## Specific Ideas

- Error messages for unknown `$type` should list all registered types and suggest the registration API — make onboarding frictionless
- Path-aware errors help users debug large policy files — include `$.path.to.problem` prefix

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 09-serialization-json-package*
*Context gathered: 2026-03-14*
