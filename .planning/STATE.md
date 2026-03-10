# Cupel — Project State

## Current Position

Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-03-10 — Milestone v1.0 started

## Accumulated Context

### Decisions
- Content is non-nullable string on ContextItem (brainstorm explored nullable, PROJECT.md specifies non-nullable)
- Ordinal-only scoring invariant confirmed across all brainstorm tracks
- CompositeScorer over scorer DAG (nested composites, ~30 lines vs hundreds)
- Fixed pipeline over middleware (no call-next, no silent-drop)
- IPlacer interface (pluggable, U-shaped as default not mandate)

### Blockers
(None)

### Technical Debt
(None)
