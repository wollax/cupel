use super::{assert_scores_match, build_items, build_scorer, load_vector};
use cupel::MetadataKeyScorer;
use toml::Value;

fn run_scoring_test(vector_path: &str) {
    let vector = load_vector(vector_path);
    let items = build_items(&vector);
    let scorer = build_scorer(&vector);

    let actual_scores: Vec<(String, f64)> = items
        .iter()
        .map(|item| {
            let score = scorer.score(item, &items);
            (item.content().to_owned(), score)
        })
        .collect();

    assert_scores_match(&vector, &actual_scores);
}

fn try_build_metadata_key_scorer_from_vector(
    vector_path: &str,
) -> Result<MetadataKeyScorer, cupel::CupelError> {
    let vector = load_vector(vector_path);
    let config: &Value = &vector["config"];
    let key = config["key"].as_str().expect("config.key");
    let value = config["value"].as_str().expect("config.value");
    let boost = config["boost"].as_float().expect("config.boost");
    MetadataKeyScorer::new(key, value, boost)
}

#[test]
fn recency_basic() {
    run_scoring_test("scoring/recency-basic.toml");
}

#[test]
fn recency_null_timestamps() {
    run_scoring_test("scoring/recency-null-timestamps.toml");
}

#[test]
fn priority_basic() {
    run_scoring_test("scoring/priority-basic.toml");
}

#[test]
fn priority_null() {
    run_scoring_test("scoring/priority-null.toml");
}

#[test]
fn kind_default_weights() {
    run_scoring_test("scoring/kind-default-weights.toml");
}

#[test]
fn kind_unknown() {
    run_scoring_test("scoring/kind-unknown.toml");
}

#[test]
fn tag_basic() {
    run_scoring_test("scoring/tag-basic.toml");
}

#[test]
fn tag_no_tags() {
    run_scoring_test("scoring/tag-no-tags.toml");
}

#[test]
fn frequency_basic() {
    run_scoring_test("scoring/frequency-basic.toml");
}

#[test]
fn reflexive_basic() {
    run_scoring_test("scoring/reflexive-basic.toml");
}

#[test]
fn reflexive_null() {
    run_scoring_test("scoring/reflexive-null.toml");
}

#[test]
fn composite_weighted() {
    run_scoring_test("scoring/composite-weighted.toml");
}

#[test]
fn scaled_basic() {
    run_scoring_test("scoring/scaled-basic.toml");
}

#[test]
fn decay_exponential_half_life() {
    run_scoring_test("scoring/decay-exponential-half-life.toml");
}

#[test]
fn decay_future_dated() {
    run_scoring_test("scoring/decay-future-dated.toml");
}

#[test]
fn decay_null_timestamp() {
    run_scoring_test("scoring/decay-null-timestamp.toml");
}

#[test]
fn decay_step_second_window() {
    run_scoring_test("scoring/decay-step-second-window.toml");
}

#[test]
fn decay_window_at_boundary() {
    run_scoring_test("scoring/decay-window-at-boundary.toml");
}

#[test]
fn metadata_trust_present_valid() {
    run_scoring_test("scoring/metadata-trust-present-valid.toml");
}

#[test]
fn metadata_trust_key_absent() {
    run_scoring_test("scoring/metadata-trust-key-absent.toml");
}

#[test]
fn metadata_trust_unparseable() {
    run_scoring_test("scoring/metadata-trust-unparseable.toml");
}

#[test]
fn metadata_trust_out_of_range_high() {
    run_scoring_test("scoring/metadata-trust-out-of-range-high.toml");
}

#[test]
fn metadata_trust_non_finite() {
    run_scoring_test("scoring/metadata-trust-non-finite.toml");
}

#[test]
fn metadata_key_match_boost() {
    run_scoring_test("scoring/metadata-key-match-boost.toml");
}

#[test]
fn metadata_key_no_match_neutral() {
    run_scoring_test("scoring/metadata-key-no-match-neutral.toml");
}

#[test]
fn metadata_key_absent_neutral() {
    run_scoring_test("scoring/metadata-key-absent-neutral.toml");
}

#[test]
fn metadata_key_zero_boost_construction_error() {
    assert!(
        try_build_metadata_key_scorer_from_vector(
            "scoring/metadata-key-zero-boost-construction-error.toml"
        )
        .is_err()
    );
}

#[test]
fn metadata_key_negative_boost_construction_error() {
    assert!(
        try_build_metadata_key_scorer_from_vector(
            "scoring/metadata-key-negative-boost-construction-error.toml"
        )
        .is_err()
    );
}
