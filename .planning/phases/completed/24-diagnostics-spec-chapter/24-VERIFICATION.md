---
status: passed
score: 26/26
---

# Phase 24 Verification: Diagnostics Spec Chapter

## Must-Have Results

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | diagnostics.md exists with overview, ownership model, null-path guarantee, data-flow diagram, and summary table | PASS | diagnostics.md: Overview (L5), Ownership Model (L9), Null-Path Guarantee (L19), Data Flow mermaid diagram (L25), Summary table (L37–48) |
| 2 | trace-collector.md defines TraceCollector contract (is_enabled, record_stage_event, record_item_event), NullTraceCollector, DiagnosticTraceCollector, and TraceDetailLevel enum | PASS | trace-collector.md: Contract table L9–15, NullTraceCollector L17, DiagnosticTraceCollector L25, TraceDetailLevel L42–53 (Stage=0, Item=1) |
| 3 | events.md defines TraceEvent fields table + JSON example, PipelineStage enum (5 values, no Sort), and OverflowEvent fields table + JSON example | PASS | TraceEvent field table L14–18 + two JSON examples L28–47; PipelineStage 5 values L55–63 (Classify, Score, Deduplicate, Slice, Place); OverflowEvent field table L79–83 + JSON example L87–99 |
| 4 | All prose is language-agnostic — no C#, Rust, or language-specific syntax anywhere | PASS | grep for class/struct/impl/fn/pub/var/async Task/IEnumerable/Vec</>/&str found only: "observer interface" (English prose) and "fieldless enum variants" (generic concept) — no language keywords |
| 5 | Each type has both a field/type table and one complete JSON example | PASS | TraceEvent: field table + 2 examples; OverflowEvent: field table + example; ExclusionReason: variant table + 3 examples; InclusionReason: variant table + example; SelectionReport: field table + complete example; IncludedItem/ExcludedItem: field tables + examples |
| 6 | MUST keyword appears only in Conformance Notes sections | PASS | diagnostics.md: 0 occurrences. trace-collector.md: 4 occurrences all in L61 ## Conformance Notes. events.md: 4 occurrences all in L101 ## Conformance Notes. exclusion-reasons.md: 5 occurrences all in L78 ## Conformance Notes. selection-report.md: 5 occurrences all in L130 ## Conformance Notes |
| 7 | snake_case used for all JSON wire format field names | PASS | All JSON fields use snake_case: duration_ms, item_count, tokens_over_budget, overflowing_items, item_tokens, available_tokens, deduplicated_against, total_candidates, total_tokens_considered |
| 8 | Absent fields are omitted in JSON examples (no nulls) | PASS | Stage-level TraceEvent example omits `message` field entirely; all other examples omit variant-irrelevant fields; no null values appear in any example |
| 9 | Inline rationale and rejected-alternative sentences appear for load-bearing decisions | PASS | Rationale and Rejected alternative blocks present in all five files for duration_ms type choice, TraceDetailLevel granularity, data-carrying variants, delivery mechanism, post-call extraction, etc. |
| 10 | duration_ms is specified as float64 (milliseconds) | PASS | events.md L16: `duration_ms \| float64 \| Yes`; rationale at L20: "Float64 accommodates sub-millisecond precision without integer overflow concerns" |
| 11 | PipelineStage omits Sort — with a note that implementations must handle unknown stage values gracefully | PASS | events.md L63: "**Sort is omitted.**" with rationale; L67: "Callers should handle unknown PipelineStage values gracefully"; L106 (Conformance): MUST handle unknown values |
| 12 | OverflowEvent is defined as a data type with delivery mechanism left to implementations | PASS | events.md L73–74: "`OverflowEvent` is a data type only. The spec defines its structure but not its delivery mechanism — implementations choose how to surface the event" |
| 13 | Observer callback described as optional capability without prescribing mechanism | PASS | trace-collector.md L55–59: "Implementations may support an optional observer callback... The spec defines this as a capability, not a mechanism — implementations choose how to expose the callback" |
| 14 | ExclusionReason has exactly 8 DATA-CARRYING variants with fields (not fieldless enums) | PASS | exclusion-reasons.md L13–22: BudgetExceeded, ScoredTooLow, Deduplicated, QuotaCapExceeded, QuotaRequireDisplaced, NegativeTokens, PinnedOverride, Filtered — all with named fields in the Fields column |
| 15 | BudgetExceeded carries item_tokens and available_tokens fields | PASS | exclusion-reasons.md L15: `item_tokens: integer, available_tokens: integer`; JSON example at L29–35 confirms both fields |
| 16 | Reserved variants (ScoredTooLow, QuotaCapExceeded, QuotaRequireDisplaced, Filtered) are documented with 'reserved — not emitted by built-in stages' status | PASS | All 4 show "Reserved" in Status column and "—" in Emitted by column; L24: "Reserved variants are defined but not emitted by any built-in pipeline stage"; L84 Conformance: MUST NOT be emitted by built-in stages |
| 17 | InclusionReason has exactly 3 variants (Scored, Pinned, ZeroToken) | PASS | exclusion-reasons.md L64–68: exactly Scored, Pinned, ZeroToken — 3 rows |
| 18 | SelectionReport has fields: events, included, excluded, total_candidates, total_tokens_considered | PASS | selection-report.md L27–31: all 5 fields present in the field table |
| 19 | Excluded list sort invariant is documented: score descending, stable by insertion order on ties | PASS | selection-report.md L30: "sorted by score descending (stable by insertion order on ties)"; L132 Conformance note repeats and explains the rationale |
| 20 | IncludedItem and ExcludedItem types have field tables and JSON examples | PASS | IncludedItem: field table L79–83 + JSON example L85–92. ExcludedItem: field table L99–103 + two JSON examples (BudgetExceeded L104–112, Deduplicated L114–122) |
| 21 | ExcludedItem JSON example shows deduplicated_against present for Deduplicated and absent for other reasons | PASS | selection-report.md: BudgetExceeded example (L104–112) has item_tokens/available_tokens but no deduplicated_against; Deduplicated example (L114–122) has deduplicated_against; L124 prose explains the invariant |
| 22 | SUMMARY.md updated with Diagnostics chapter entry after Placers block, before # Conformance | PASS | SUMMARY.md L31–33: Placers block; L34–38: Diagnostics block (diagnostics.md + 4 sub-pages); L40: # Conformance |
| 23 | All JSON examples use snake_case field names and omit absent fields (no nulls) | PASS | exclusion-reasons.md and selection-report.md JSON examples all use snake_case; absent fields (e.g., deduplicated_against in BudgetExceeded) are omitted rather than set to null |
| 24 | MUST keyword appears only in Conformance Notes sections | PASS | exclusion-reasons.md: all 5 MUST occurrences in L78 ## Conformance Notes. selection-report.md: all 5 MUST occurrences in L130 ## Conformance Notes |
| 25 | No C#, Rust, or language-specific syntax appears anywhere | PASS | grep across exclusion-reasons.md and selection-report.md found no language-specific keywords |
| 26 | mdBook can build the spec without errors after SUMMARY.md update | PASS | `cd spec && mdbook build` completed with INFO success: "HTML book written to .../spec/book" |

## Gaps Found

None. All 26 must-have requirements are satisfied.

## Summary

Phase 24 (Diagnostics Spec Chapter) passes all 26 must-have requirements across both plans. The five spec files (diagnostics.md, trace-collector.md, events.md, exclusion-reasons.md, selection-report.md) are fully compliant: prose is language-agnostic throughout, MUST keywords are confined to Conformance Notes sections in every file, all JSON examples use snake_case and omit absent fields rather than using nulls, all data types have both field tables and JSON examples, and the ExclusionReason/InclusionReason variant counts match the spec (8 and 3 respectively). The mdBook build succeeds without errors.
