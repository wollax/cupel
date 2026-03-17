# Decisions Register

<!-- Append-only. Never edit or remove existing rows.
     To reverse a decision, add a new row that supersedes it.
     Read this file at the start of any planning or research phase. -->

| # | When | Scope | Decision | Choice | Rationale | Revisable? |
|---|------|-------|----------|--------|-----------|------------|
| D001 | M001 | arch | TraceCollector ownership model | Per-invocation (passed at call time, not stored on pipeline) | Avoids thread-safety concerns; each run gets isolated diagnostic context; prevents concurrent runs from interfering | No |
| D002 | M001 | api | run_traced vs unified run method | `run_traced()` coexists with `run()` — additive, not replacing | Avoids breaking changes for existing callers; keeps the non-diagnostic path zero-cost | No |
| D003 | M001 | api | NullTraceCollector type | Zero-sized type (ZST) that monomorphizes to no-ops | Rust monomorphization eliminates dead code; ZST has zero size, zero runtime cost | No |
| D004 | M001 | arch | ExclusionReason variants | Data-carrying enum variants (not fieldless) | Enables programmatic inspection without parsing message strings; see spec rationale in `spec/src/diagnostics/exclusion-reasons.md` | No |
| D005 | M001 | arch | Reserved ExclusionReason variants | Must be defined even if never emitted by built-in stages | Forward-compat: reserved variants allocate type-system space for future spec versions without breaking changes | No |
| D006 | M001 | api | KnapsackSlice DP guard threshold | 50 million cells (capacity × items) | Matches requirement RAPI-06; prevents OOM without affecting typical workloads (<500 items at <4096 tokens) | Yes — if workloads grow |
| D007 | M001 | convention | Diagnostics conformance vector authoring | Author vectors in `spec/conformance/` first; drift guard syncs to `crates/cupel/conformance/` | Spec-first is the established pattern; drift guard ensures implementations can't diverge silently | No |
| D008 | M001 | scope | QH issue batch scope | ~15-20 high-signal .NET issues and ~10-15 Rust issues per quality slice | Exhaustive triage not worth the overhead; high-signal = correctness, panic paths, misleading behavior, test gaps. Cosmetic/low-impact issues stay open. | Yes — scope per slice |
| D009 | prior | arch | Fixed pipeline over middleware | Fixed 5-stage pipeline (Classify → Score → Deduplicate → Slice → Place) | Silent-drop failure mode of call-next middleware is worse than predictable fixed-stage behavior | No |
| D010 | prior | arch | Ordinal-only scoring invariant | Scorers rank but never eliminate | KnapsackSlice is provably correct only on complete candidate sets | No |
| D011 | prior | arch | Per-invocation tokenizer | Token counting is caller's responsibility | Keeps pipeline dependency-free; works with any tokenizer | No |
| D012 | prior | arch | Crate name `cupel` | Standalone name on crates.io | Shorter, cleaner; name was available | No |
| D013 | prior | arch | Standalone Cargo.toml | No workspace | Single-crate repo avoids workspace complexity | No |
| D014 | prior | arch | Validation-on-deserialize for ContextBudget | Custom deserializer routes through constructor | Prevents serde from bypassing constructor invariants | No |
| D015 | prior | arch | #[non_exhaustive] on CupelError and OverflowStrategy | Additive enum evolution without breaking downstream | Standard Rust pattern for library-owned enums | No |
| D016 | S01 planning | arch | S01 verification strategy | Contract-level only (compile + cargo test + doc + clippy + drift guard) | Diagnostic types have no runtime behavior to test; harness coverage of `expected.diagnostics.*` is a S03 concern; TOML vector correctness is manually verified against diagnostics-budget-exceeded.toml schema | No |
| D017 | S01 planning | convention | ExclusionReason serde deferred to S04 | S01 stubs `cfg_attr` annotations with `// custom serde impl in S04` comment | Adjacent-tagged wire format cannot be derived; implementing it in S01 would violate task scope and risk S02 rework if field shapes change | No |
