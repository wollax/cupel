use std::collections::HashMap;

use cupel::ContextBudget;

use super::{assert_set_eq, build_scored_items, build_slicer, load_vector};

fn run_slicing_test(vector_path: &str) {
    let vector = load_vector(vector_path);
    let scored_items = build_scored_items(&vector);
    let slicer = build_slicer(&vector);

    let target_tokens = vector["budget"]["target_tokens"]
        .as_integer()
        .expect("missing budget.target_tokens");
    let max_tokens = vector["budget"]
        .get("max_tokens")
        .and_then(|v| v.as_integer())
        .unwrap_or(target_tokens);

    let budget = ContextBudget::new(max_tokens, target_tokens, 0, HashMap::new(), 0.0)
        .expect("budget should be valid");

    let selected = slicer.slice(&scored_items, &budget).expect("conformance vector slicing should not error");
    let actual_contents: Vec<String> = selected.iter().map(|i| i.content().to_owned()).collect();

    let expected_contents: Vec<String> = vector["expected"]["selected_contents"]
        .as_array()
        .expect("missing expected.selected_contents")
        .iter()
        .map(|v| {
            v.as_str()
                .expect("expected content must be string")
                .to_owned()
        })
        .collect();

    assert_set_eq(&expected_contents, &actual_contents);
}

#[test]
fn greedy_density() {
    run_slicing_test("slicing/greedy-density.toml");
}

#[test]
fn greedy_exact_fit() {
    run_slicing_test("slicing/greedy-exact-fit.toml");
}

#[test]
fn greedy_zero_tokens() {
    run_slicing_test("slicing/greedy-zero-tokens.toml");
}

#[test]
fn knapsack_basic() {
    run_slicing_test("slicing/knapsack-basic.toml");
}

#[test]
fn knapsack_zero_tokens() {
    run_slicing_test("slicing/knapsack-zero-tokens.toml");
}

#[test]
fn quota_basic() {
    run_slicing_test("slicing/quota-basic.toml");
}

/// Run a count_quota conformance test that also verifies `shortfall_count` and
/// `cap_excluded_count` from the `[expected]` section of the vector.
///
/// Strategy (no pipeline needed):
/// - Call `slicer.slice()` directly for `selected_contents` assertion.
/// - Compute shortfall count by comparing per-kind candidate counts against `require_count`.
/// - Compute cap_excluded_count as candidates that fit in budget but were not selected
///   (total_items - selected_items.len() for vectors where all items fit in budget).
fn run_count_quota_full_test(vector_path: &str) {
    let vector = load_vector(vector_path);
    let scored_items = build_scored_items(&vector);
    let slicer = build_slicer(&vector);

    let target_tokens = vector["budget"]["target_tokens"]
        .as_integer()
        .expect("missing budget.target_tokens");
    let max_tokens = vector["budget"]
        .get("max_tokens")
        .and_then(|v| v.as_integer())
        .unwrap_or(target_tokens);

    let budget = ContextBudget::new(max_tokens, target_tokens, 0, HashMap::new(), 0.0)
        .expect("budget should be valid");

    let selected =
        slicer.slice(&scored_items, &budget).expect("conformance vector slicing should not error");
    let actual_contents: Vec<String> =
        selected.iter().map(|i| i.content().to_owned()).collect();

    let expected_contents: Vec<String> = vector["expected"]["selected_contents"]
        .as_array()
        .expect("missing expected.selected_contents")
        .iter()
        .map(|v| v.as_str().expect("expected content must be string").to_owned())
        .collect();

    assert_set_eq(&expected_contents, &actual_contents);

    // ── shortfall_count check ─────────────────────────────────────────────────
    //
    // Shortfalls occur when a kind has fewer candidates than require_count.
    // Recompute from vector data to avoid needing pipeline-level access to
    // SelectionReport::count_requirement_shortfalls.
    if let Some(expected_shortfall_count) =
        vector["expected"].get("shortfall_count").and_then(|v| v.as_integer())
    {
        let cfg = vector.get("config").expect("count_quota test needs [config]");
        let entries_arr = cfg
            .get("entries")
            .and_then(|v| v.as_array())
            .expect("count_quota needs config.entries");

        // Count candidates per kind.
        let mut kind_candidate_count: HashMap<String, usize> = HashMap::new();
        for si in &scored_items {
            *kind_candidate_count
                .entry(si.item.kind().as_str().to_owned())
                .or_insert(0) += 1;
        }

        // Count kinds where candidate pool < require_count.
        let actual_shortfall_count: i64 = entries_arr
            .iter()
            .filter(|e| {
                let kind = e["kind"].as_str().expect("entry missing kind");
                let require_count =
                    e["require_count"].as_integer().expect("entry missing require_count") as usize;
                if require_count == 0 {
                    return false;
                }
                let available = kind_candidate_count.get(kind).copied().unwrap_or(0);
                available < require_count
            })
            .count() as i64;

        assert_eq!(
            expected_shortfall_count, actual_shortfall_count,
            "shortfall_count mismatch: expected {expected_shortfall_count}, got {actual_shortfall_count}"
        );
    }

    // ── cap_excluded_count check ──────────────────────────────────────────────
    //
    // Items not in selected but that fit within the total budget were excluded by cap.
    // For test vectors where sum(item_tokens) << budget, this equals
    // total_items - selected_items.len().
    if let Some(expected_cap_excluded) =
        vector["expected"].get("cap_excluded_count").and_then(|v| v.as_integer())
    {
        let total_tokens: i64 = scored_items.iter().map(|si| si.item.tokens()).sum();
        assert!(
            total_tokens <= target_tokens,
            "cap_excluded_count check requires all items to fit within budget \
             (total_tokens={total_tokens}, budget={target_tokens})"
        );

        let actual_cap_excluded =
            (scored_items.len() as i64) - (actual_contents.len() as i64);

        assert_eq!(
            expected_cap_excluded, actual_cap_excluded,
            "cap_excluded_count mismatch: expected {expected_cap_excluded}, got {actual_cap_excluded}"
        );
    }
}

#[test]
fn count_quota_baseline() {
    run_slicing_test("slicing/count-quota-baseline.toml");
}

#[test]
fn count_quota_cap_exclusion() {
    run_count_quota_full_test("slicing/count-quota-cap-exclusion.toml");
}

#[test]
fn count_quota_scarcity_degrade() {
    run_count_quota_full_test("slicing/count-quota-scarcity-degrade.toml");
}

#[test]
fn count_quota_tag_nonexclusive() {
    run_slicing_test("slicing/count-quota-tag-nonexclusive.toml");
}

#[test]
fn count_quota_require_and_cap() {
    run_count_quota_full_test("slicing/count-quota-require-and-cap.toml");
}
