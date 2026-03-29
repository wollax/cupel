# Cupel Conformance Test Vectors

This directory contains TOML test vectors for validating implementations of the [Cupel Specification](https://wollax.github.io/cupel/).

## Directory Structure

```
conformance/
├── README.md
├── required/           # MUST pass for Cupel conformance
│   ├── scoring/        # Individual scorer algorithm tests
│   ├── slicing/        # Slicer algorithm tests
│   ├── placing/        # Placer algorithm tests
│   └── pipeline/       # End-to-end pipeline tests
└── optional/           # MAY pass for full conformance
    ├── scoring/        # Scorer edge case tests
    ├── slicing/        # Slicer edge case tests
    └── pipeline/       # Pipeline edge case tests
```

## Conformance Tiers

- **Required**: An implementation MUST pass all vectors in `required/` to claim Cupel conformance.
- **Optional**: An implementation MAY pass vectors in `optional/` for full conformance.

See the [Conformance Levels](https://wollax.github.io/cupel/conformance/levels.html) chapter of the specification for details.

## Using the Vectors

Each `.toml` file contains a single test case. The `[test]` table identifies the stage and algorithm under test:

```toml
[test]
name = "RecencyScorer ranks by timestamp"
stage = "scoring"
scorer = "recency"
```

### Comparison Modes

- **Scoring**: Compare each item's actual score against `score_approx` using epsilon tolerance (`abs(actual - expected) < score_epsilon`).
- **Slicing**: Compare selected item contents as a **set** (order does not matter).
- **Placing**: Compare output item contents as an **ordered list** (position matters).
- **Pipeline**: Compare output item contents as an **ordered list** (position matters).

### Parsing

Use any TOML library for your language. See the [Running the Suite](https://wollax.github.io/cupel/conformance/running.html) chapter for recommended libraries and a pseudocode test runner.

## Score Tolerance

All score comparisons use epsilon tolerance (default `1e-9`). Never compare floating-point scores with exact equality.

## TOML Version

These vectors use [TOML 1.1](https://toml.io/en/v1.1.0) features, specifically optional seconds in offset date-times (e.g. `2024-01-01T00:00Z` instead of `2024-01-01T00:00:00Z`). Implementations consuming these vectors **must** use a TOML 1.1-capable parser.

## Version

These vectors correspond to Cupel Specification v1.0.0.
