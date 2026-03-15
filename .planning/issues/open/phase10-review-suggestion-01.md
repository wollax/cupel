---
title: "NuGet cache in CI/CD workflows"
area: ci
priority: low
source: phase-10-pr-review
---

# NuGet Cache in CI/CD Workflows

Add `actions/cache` for NuGet packages to both `ci.yml` and `release.yml` to speed up build times by avoiding redundant package downloads on every run.

## Suggestion

Cache the NuGet global packages directory (typically `~/.nuget/packages`) using the `actions/cache` action, keyed on the solution-level `*.csproj` and `Directory.Packages.props` files.

## Files

- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
