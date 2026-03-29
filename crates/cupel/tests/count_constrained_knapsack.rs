/// Integration tests for `CountConstrainedKnapsackSlice`.
///
/// Each test loads a TOML conformance vector from:
///   `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml`
///
/// These tests will panic at runtime with "unknown slicer type: count_constrained_knapsack"
/// until T02 adds `CountConstrainedKnapsackSlice` to the `build_slicer_by_type` dispatch
/// below. After T02, all 5 tests are expected to pass.
///
/// Shortfall and cap_excluded counts are verified using the same logic as
/// `run_count_quota_full_test` in `crates/cupel/tests/conformance/slicing.rs`.
use std::collections::HashMap;
use std::path::Path;

use toml::Value;

use cupel::{
    ContextBudget, ContextItemBuilder, ContextKind, CountConstrainedKnapsackSlice, CountQuotaEntry,
    CountQuotaSlice, GreedySlice, KnapsackSlice, ScarcityBehavior, ScoredItem, Slicer,
};

fn load_vector(relative_path: &str) -> Value {
    let base = Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("conformance")
        .join("required");
    let path = base.join(relative_path);
    let content = std::fs::read_to_string(&path)
        .unwrap_or_else(|e| panic!("failed to read {}: {e}", path.display()));
    // toml 0.9+ changed Value::from_str to parse values, not documents.
    // Use from_str::<Table> for document parsing, then wrap for downstream compatibility.
    let table: toml::Table = toml::from_str(&content)
        .unwrap_or_else(|e| panic!("failed to parse TOML {}: {e}", path.display()));
    Value::Table(table)
}

fn build_scored_items(vector: &Value) -> Vec<ScoredItem> {
    let items_array = vector
        .get("scored_items")
        .and_then(|v| v.as_array())
        .expect("missing [[scored_items]] array");

    items_array
        .iter()
        .map(|item| {
            let content = item["content"].as_str().expect("item missing content");
            let tokens = item["tokens"].as_integer().expect("item missing tokens");
            let score = item["score"]
                .as_float()
                .or_else(|| item["score"].as_integer().map(|i| i as f64))
                .expect("item missing score");

            let mut builder = ContextItemBuilder::new(content, tokens);
            if let Some(kind_val) = item.get("kind").and_then(|v| v.as_str()) {
                builder = builder.kind(ContextKind::new(kind_val).unwrap());
            }
            ScoredItem {
                item: builder.build().expect("failed to build ContextItem"),
                score,
            }
        })
        .collect()
}

fn build_slicer_by_type(slicer_type: &str, config: Option<&Value>) -> Box<dyn Slicer> {
    match slicer_type {
        "greedy" => Box::new(GreedySlice),
        "knapsack" => {
            let bucket_size = config
                .and_then(|c| c.get("bucket_size"))
                .and_then(|v| v.as_integer())
                .unwrap_or(100);
            Box::new(KnapsackSlice::new(bucket_size).unwrap())
        }
        "count_quota" => {
            let cfg = config.expect("count_quota slicer needs config");
            let inner_type = cfg
                .get("inner_slicer")
                .and_then(|v| v.as_str())
                .unwrap_or("greedy");
            let inner = build_slicer_by_type(inner_type, None);

            let scarcity_str = cfg
                .get("scarcity_behavior")
                .and_then(|v| v.as_str())
                .unwrap_or("degrade");
            let scarcity = match scarcity_str {
                "degrade" => ScarcityBehavior::Degrade,
                "throw" => ScarcityBehavior::Throw,
                other => panic!("unknown scarcity_behavior: {other}"),
            };

            let entries_arr = cfg
                .get("entries")
                .and_then(|v| v.as_array())
                .expect("count_quota needs config.entries");

            let entries: Vec<CountQuotaEntry> = entries_arr
                .iter()
                .map(|e| {
                    let kind = e["kind"].as_str().expect("entry missing kind");
                    let require_count = e["require_count"]
                        .as_integer()
                        .expect("entry missing require_count")
                        as usize;
                    let cap_count = e["cap_count"]
                        .as_integer()
                        .expect("entry missing cap_count")
                        as usize;
                    CountQuotaEntry::new(ContextKind::new(kind).unwrap(), require_count, cap_count)
                        .unwrap()
                })
                .collect();

            Box::new(CountQuotaSlice::new(entries, inner, scarcity).unwrap())
        }
        "count_constrained_knapsack" => {
            let cfg = config.expect("count_constrained_knapsack slicer needs config");

            let bucket_size = cfg
                .get("bucket_size")
                .and_then(|v| v.as_integer())
                .unwrap_or(100);
            let knapsack = KnapsackSlice::new(bucket_size).unwrap();

            let scarcity_str = cfg
                .get("scarcity_behavior")
                .and_then(|v| v.as_str())
                .unwrap_or("degrade");
            let scarcity = match scarcity_str {
                "degrade" => ScarcityBehavior::Degrade,
                "throw" => ScarcityBehavior::Throw,
                other => panic!("unknown scarcity_behavior: {other}"),
            };

            let entries_arr = cfg
                .get("entries")
                .and_then(|v| v.as_array())
                .expect("count_constrained_knapsack needs config.entries");

            let entries: Vec<CountQuotaEntry> = entries_arr
                .iter()
                .map(|e| {
                    let kind = e["kind"].as_str().expect("entry missing kind");
                    let require_count = e["require_count"]
                        .as_integer()
                        .expect("entry missing require_count")
                        as usize;
                    let cap_count = e["cap_count"]
                        .as_integer()
                        .expect("entry missing cap_count")
                        as usize;
                    CountQuotaEntry::new(ContextKind::new(kind).unwrap(), require_count, cap_count)
                        .unwrap()
                })
                .collect();

            Box::new(CountConstrainedKnapsackSlice::new(entries, knapsack, scarcity).unwrap())
        }
        other => panic!("unknown slicer type: {other}"),
    }
}

fn build_slicer(vector: &Value) -> Box<dyn Slicer> {
    let slicer_type = vector["test"]["slicer"]
        .as_str()
        .expect("missing test.slicer");
    build_slicer_by_type(slicer_type, vector.get("config"))
}

fn assert_set_eq(expected: &[String], actual: &[String]) {
    let mut exp_sorted = expected.to_vec();
    let mut act_sorted = actual.to_vec();
    exp_sorted.sort();
    act_sorted.sort();
    assert_eq!(
        exp_sorted, act_sorted,
        "selected items mismatch\n  expected: {expected:?}\n  actual:   {actual:?}"
    );
}

/// Run a count_constrained_knapsack conformance test, verifying selected_contents,
/// shortfall_count, and cap_excluded_count from the TOML [expected] section.
///
/// Panics at runtime with "unknown slicer type: count_constrained_knapsack" until T02.
fn run_count_constrained_knapsack_test(vector_path: &str) {
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

    let selected = slicer
        .slice(&scored_items, &budget)
        .expect("conformance vector slicing should not error");
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

    // ── shortfall_count check ─────────────────────────────────────────────────
    if let Some(expected_shortfall_count) = vector["expected"]
        .get("shortfall_count")
        .and_then(|v| v.as_integer())
    {
        let cfg = vector.get("config").expect("test needs [config]");
        let entries_arr = cfg
            .get("entries")
            .and_then(|v| v.as_array())
            .expect("test needs config.entries");

        let mut kind_candidate_count: HashMap<String, usize> = HashMap::new();
        for si in &scored_items {
            *kind_candidate_count
                .entry(si.item.kind().as_str().to_owned())
                .or_insert(0) += 1;
        }

        let actual_shortfall_count: i64 = entries_arr
            .iter()
            .filter(|e| {
                let kind = e["kind"].as_str().expect("entry missing kind");
                let require_count = e["require_count"]
                    .as_integer()
                    .expect("entry missing require_count")
                    as usize;
                if require_count == 0 {
                    return false;
                }
                let available = kind_candidate_count.get(kind).copied().unwrap_or(0);
                available < require_count
            })
            .count() as i64;

        assert_eq!(
            expected_shortfall_count, actual_shortfall_count,
            "shortfall_count mismatch: expected {expected_shortfall_count}, \
             got {actual_shortfall_count}"
        );
    }

    // ── cap_excluded_count check ──────────────────────────────────────────────
    if let Some(expected_cap_excluded) = vector["expected"]
        .get("cap_excluded_count")
        .and_then(|v| v.as_integer())
    {
        let total_tokens: i64 = scored_items.iter().map(|si| si.item.tokens()).sum();
        assert!(
            total_tokens <= target_tokens,
            "cap_excluded_count check requires all items to fit within budget \
             (total_tokens={total_tokens}, budget={target_tokens})"
        );

        let actual_cap_excluded = (scored_items.len() as i64) - (actual_contents.len() as i64);

        assert_eq!(
            expected_cap_excluded, actual_cap_excluded,
            "cap_excluded_count mismatch: expected {expected_cap_excluded}, \
             got {actual_cap_excluded}"
        );
    }
}

/// Baseline: 3 items (2 tool + 1 msg), require_count=2 cap_count=4 for "tool".
/// Phase 1 commits tool-a + tool-b; Phase 2 (knapsack) selects msg-x from residual.
/// All 3 items selected. No shortfalls, no cap exclusions.
#[test]
fn count_constrained_knapsack_baseline() {
    run_count_constrained_knapsack_test("slicing/count-constrained-knapsack-baseline.toml");
}

/// Cap exclusion: 4 tool items, require_count=1 cap_count=2.
/// Phase 1 commits tool-a; Phase 2 (knapsack) picks tool-b; Phase 3 drops tool-c + tool-d.
/// 2 items selected. cap_excluded_count=2.
#[test]
fn count_constrained_knapsack_cap_exclusion() {
    run_count_constrained_knapsack_test("slicing/count-constrained-knapsack-cap-exclusion.toml");
}

/// Scarcity degrade: require_count=3 but only 1 tool candidate.
/// Phase 1 commits tool-a; satisfied=1 < require=3 → shortfall recorded.
/// 1 item selected. shortfall_count=1.
#[test]
fn count_constrained_knapsack_scarcity_degrade() {
    run_count_constrained_knapsack_test("slicing/count-constrained-knapsack-scarcity-degrade.toml");
}

/// Tag non-exclusivity: require 1 "tool" and 1 "memory" independently.
/// Phase 1 satisfies both kinds; Phase 2 picks the extra tool item.
/// All 3 items selected. No shortfalls, no cap exclusions.
#[test]
fn count_constrained_knapsack_tag_nonexclusive() {
    run_count_constrained_knapsack_test("slicing/count-constrained-knapsack-tag-nonexclusive.toml");
}

/// Combined require+cap: require_count=2 cap_count=2 for "tool"; residual knapsack picks all msg items.
/// Phase 1 commits tool-a + tool-b (count=2=cap); Phase 2 selects all msg items.
/// Phase 3: cap already at 2 for tool, msg items unconstrained → all pass.
/// All 5 items selected. No shortfalls, no cap exclusions.
#[test]
fn count_constrained_knapsack_require_and_cap() {
    run_count_constrained_knapsack_test("slicing/count-constrained-knapsack-require-and-cap.toml");
}
