# Running the Suite

This chapter describes how to parse and execute conformance test vectors in any programming language.

## General Approach

1. **Discover** test vector files by scanning the `conformance/required/` and `conformance/optional/` directories.
2. **Parse** each `.toml` file into a structured representation.
3. **Construct** the appropriate inputs (items, budget, scorer/slicer/placer configuration) from the parsed data.
4. **Execute** the algorithm under test.
5. **Compare** the actual output against the expected output using the appropriate comparison mode (set or ordered).

## Pseudocode: Conformance Runner

```text
RUN-CONFORMANCE-SUITE(vectorDir):
    passed <- 0
    failed <- 0
    errors <- empty list

    files <- GLOB(vectorDir, "**/*.toml")

    for each file in files:
        vector <- PARSE-TOML(file)
        testName <- vector.test.name
        stage <- vector.test.stage

        try:
            if stage = "scoring":
                RUN-SCORING-TEST(vector)
            else if stage = "slicing":
                RUN-SLICING-TEST(vector)
            else if stage = "placing":
                RUN-PLACING-TEST(vector)
            else if stage = "pipeline":
                RUN-PIPELINE-TEST(vector)
            else:
                APPEND(errors, "Unknown stage: " + stage)
                continue

            passed <- passed + 1
        catch error:
            failed <- failed + 1
            APPEND(errors, testName + ": " + error.message)

    REPORT(passed, failed, errors)
```

### Running a Scoring Test

```text
RUN-SCORING-TEST(vector):
    items <- BUILD-ITEMS(vector.items)
    scorer <- BUILD-SCORER(vector.test.scorer, vector.config)
    epsilon <- vector.tolerance.score_epsilon or 1e-9

    for i <- 0 to length(vector.expected) - 1:
        expectedContent <- vector.expected[i].content
        expectedScore <- vector.expected[i].score_approx

        // Find the corresponding input item by content
        item <- FIND-BY-CONTENT(items, expectedContent)
        actualScore <- scorer.Score(item, items)

        if abs(actualScore - expectedScore) >= epsilon:
            ERROR("Score mismatch for '" + expectedContent + "': " +
                  "expected " + expectedScore + ", got " + actualScore +
                  " (epsilon=" + epsilon + ")")
```

### Running a Slicing Test

```text
RUN-SLICING-TEST(vector):
    scoredItems <- BUILD-SCORED-ITEMS(vector.scored_items)
    budget <- BUILD-BUDGET(vector.budget)
    slicer <- BUILD-SLICER(vector.test.slicer, vector.config)

    selected <- slicer.Slice(scoredItems, budget)
    actualContents <- SET(item.content for item in selected)
    expectedContents <- SET(vector.expected.selected_contents)

    if actualContents != expectedContents:
        ERROR("Selection mismatch: expected " + expectedContents +
              ", got " + actualContents)
```

### Running a Placing Test

```text
RUN-PLACING-TEST(vector):
    items <- BUILD-SCORED-ITEMS-FOR-PLACER(vector.items)
    placer <- BUILD-PLACER(vector.test.placer)

    placed <- placer.Place(items)
    actualContents <- [item.content for item in placed]
    expectedContents <- vector.expected.ordered_contents

    if actualContents != expectedContents:
        ERROR("Placement order mismatch: expected " + expectedContents +
              ", got " + actualContents)
```

### Running a Pipeline Test

```text
RUN-PIPELINE-TEST(vector):
    items <- BUILD-ITEMS(vector.items)
    budget <- BUILD-BUDGET(vector.budget)
    config <- BUILD-PIPELINE-CONFIG(vector.config)

    output <- PIPELINE-RUN(items, budget, config)
    actualContents <- [item.content for item in output]
    expectedContents <- [e.content for e in vector.expected_output]

    if actualContents != expectedContents:
        ERROR("Pipeline output mismatch: expected " + expectedContents +
              ", got " + actualContents)
```

## Score Comparison

Score comparisons MUST use epsilon tolerance, never exact floating-point equality:

```text
SCORES-MATCH(actual, expected, epsilon):
    return abs(actual - expected) < epsilon
```

The default epsilon is `1e-9`. This tolerance accounts for:

- IEEE 754 representation differences across platforms
- Intermediate rounding differences in division operations
- Language-specific floating-point arithmetic behavior

Conformance test vectors are designed so that correct implementations produce scores well within this tolerance. If an implementation fails a score comparison by a small margin (e.g., `1e-10`), it is likely correct. If the difference is large (e.g., `> 0.01`), the algorithm implementation is likely incorrect.

## Set vs. Ordered Comparison

| Stage | Comparison Mode | Rationale |
|---|---|---|
| Scoring | Per-item epsilon | Scores are floating-point values |
| Slicing | Set (unordered) | Slicers select items but do not determine order |
| Placing | Ordered list | Placers determine the exact presentation order |
| Pipeline | Ordered list | The pipeline produces a fully ordered output |

## TOML Libraries

The following TOML parsing libraries are recommended for common implementation languages:

| Language | Library | Notes |
|---|---|---|
| C# | `Tomlyn` | NuGet package |
| Rust | `toml` | Cargo crate (serde-based) |
| Go | `github.com/BurntSushi/toml` | Standard Go TOML library |
| Python | `tomllib` (stdlib) | Built-in since Python 3.11 |
| TypeScript | `@iarna/toml` or `smol-toml` | npm packages |
| Java | `com.moandjiezana.toml:toml4j` | Maven artifact |
| Swift | `TOMLDecoder` | Swift Package Manager |
| Kotlin | `cc.ekblad.toml4k` | Gradle/Maven |

## Tips for Implementors

1. **Start with scoring vectors.** They test individual algorithms in isolation and are the easiest to debug.
2. **Verify your TOML parser handles datetimes.** Some TOML libraries parse RFC 3339 timestamps into local time; ensure you compare temporal instants (UTC).
3. **Use reference identity for self-exclusion.** FrequencyScorer and ScaledScorer use reference identity (`is`) to identify the current item in `allItems`. Ensure your implementation does not use structural equality for this check.
4. **Slicer output order does not matter.** Only the set of selected items is tested. Do not fail a slicer test because items are in a different order than the expected list.
5. **Pipeline vectors are the final gate.** If all scoring, slicing, and placing vectors pass but a pipeline vector fails, the issue is likely in stage integration (classify, deduplicate, sort, merge, overflow handling).
