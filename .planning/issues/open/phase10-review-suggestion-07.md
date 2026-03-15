---
title: "Upload TRX test results as CI artifacts"
area: ci
priority: low
source: phase-10-pr-review
---

# Upload TRX Test Results as CI Artifacts

The CI workflow runs tests but does not upload structured test result files. Adding TRX artifact upload enables structured failure reports in GitHub Actions and integrates with tooling that consumes the TRX format.

## Suggestion

Add a `dotnet test` `--logger trx` flag and an `actions/upload-artifact` step to the CI workflow:

```yaml
- name: Test
  run: dotnet test --logger trx --results-directory TestResults/

- name: Upload test results
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: test-results
    path: TestResults/**/*.trx
```

The `if: always()` ensures results are uploaded even on test failure, which is when they are most useful.

## Files

- `.github/workflows/ci.yml`
