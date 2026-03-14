# Phase 10: Companion Packages & Release — UAT

## Tests

| # | Test | Status |
|---|------|--------|
| 1 | DI: AddCupel registers options, AddCupelPipeline resolves keyed pipeline | ✅ pass — 12/12 DI tests pass |
| 2 | DI: Pipeline resolved via keyed service executes and returns results | ✅ pass — Execute() returns ContextResult with 1 item |
| 3 | DI: Missing policy produces clear, actionable error message | ✅ pass — InvalidOperationException with "Call AddCupel(o => o.AddPolicy(...))" |
| 4 | Tiktoken: CreateForModel("gpt-4o") counts tokens accurately | ✅ pass — 13/13 Tiktoken tests pass, "quick brown fox" = 9 tokens |
| 5 | Tiktoken: WithTokenCount preserves all ContextItem properties except Tokens | ✅ pass — Kind, Source, Priority, Tags, Metadata, Timestamp, FutureRelevanceHint, Pinned, OriginalTokens all preserved |
| 6 | Tiktoken: Invalid model/encoding/null inputs throw appropriate exceptions | ✅ pass — NotSupportedException, ArgumentException, ArgumentNullException |
| 7 | Consumption: All 4 packages work from packed .nupkg (not ProjectReference) | ✅ pass — 5/5 consumption tests pass from local .nupkg source |
| 8 | PublicAPI: All Shipped.txt populated, all Unshipped.txt empty | ✅ pass — 301+19+5+7 lines shipped, all unshipped = "#nullable enable" only |
| 9 | CI: ci.yml valid YAML, triggers on push/PR, runs build+test | ✅ pass — YAML valid, push+PR triggers, build+test steps present |
| 10 | Release: release.yml valid YAML, manual dispatch, OIDC publish, branch guard | ✅ pass — YAML valid, workflow_dispatch, NuGet/login OIDC, main branch guard, project-specific version |

## Result

**10/10 tests passed** — All Phase 10 deliverables verified.

Test suite: 597 unit tests + 5 consumption tests = 602 total, 0 failures.
