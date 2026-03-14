# Phase 10: Companion Packages & Release - Context

**Gathered:** 2026-03-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Ship the complete NuGet package suite for Cupel v1.0: DI integration (`Wollax.Cupel.Extensions.DependencyInjection`), Tiktoken bridge (`Wollax.Cupel.Tiktoken`), consumption tests against packed `.nupkg` files, CI/CD pipelines, NuGet publishing, and `PublicAPI.Shipped.txt` finalization. No new core library features — this phase packages and ships what phases 1–9 built.

</domain>

<decisions>
## Implementation Decisions

### DI registration surface
- Claude's discretion on keyed services vs CupelOptions registry vs both — pick what fits .NET conventions and the existing CupelOptions API
- Claude's discretion on AddCupel() overloads — delegate, IConfiguration binding, or both
- Claude's discretion on auto-registering a default pipeline when a single policy is configured
- Claude's discretion on target framework — .NET 10 only or multi-target with net8.0

### Tiktoken bridge scope
- Claude's discretion on API shape — adapter type, extension methods on builder, or both
- Claude's discretion on whether to provide a pre-processing helper (tokenize items before pipeline) vs validation/counting only
- Claude's discretion on encoding strategy support — multiple built-in vs configurable-only
- Claude's discretion on DI package dependency — standalone, optional DI integration, or both

### Release & publish pipeline
- NuGet publish trigger: **manual workflow_dispatch** (not tag push)
- CI pipeline: **two separate workflows** — PR build+test workflow, and independent publish workflow
- Consumption tests: **publish workflow only** (not on every PR)
- Claude's discretion on version management — single shared version is strongly implied by roadmap's "identical version" requirement

### PublicAPI finalization
- Claude's discretion on whether to audit/review API surface before freezing or ship as-is
- Claude's discretion on named presets [Experimental] status — earlier roadmap decision says they're [Experimental] at launch
- Claude's discretion on whether companion packages get PublicApiAnalyzers — existing project convention is the guide
- No specific API surface concerns from user — phases 1–9 surface is solid

### Claude's Discretion
Broad discretion granted across all areas. Key constraint: decisions should follow .NET conventions, match existing Cupel patterns (from phases 1–9), and respect the roadmap requirements. The three locked decisions are:
1. Manual dispatch for NuGet publishing (not tag-triggered)
2. Separate CI and publish workflows
3. Consumption tests run only in publish workflow

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches. The roadmap success criteria are precise enough to guide implementation:
- `AddCupel()` with `IOptions<CupelOptions>`, keyed services for named pipelines, correct lifetimes
- `Wollax.Cupel.Tiktoken` bridges `Microsoft.ML.Tokenizers`
- Consumption tests against packed `.nupkg` files
- All four packages publish with identical version, SourceLink, NuGet Trusted Publishing via OIDC

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 10-companion-packages-release*
*Context gathered: 2026-03-14*
