# Phase 19: First Publish & Assay Switchover - Research

**Researched:** 2026-03-15
**Confidence Key:** HIGH = verified with official docs/multiple sources, MEDIUM = single authoritative source, LOW = training data only

---

## Standard Stack

| Component | Choice | Version | Confidence |
|-----------|--------|---------|------------|
| OIDC auth action | `rust-lang/crates-io-auth-action` | v1 (v1.0.3, Nov 2025) | HIGH |
| GitHub Release action | `softprops/action-gh-release` | v2 (already in workflow) | HIGH |
| Checkout action | `actions/checkout` | v4 (already in workflow) | HIGH |

No additional dependencies needed. The OIDC flow uses GitHub's built-in OIDC provider and the official crates.io auth action.

---

## Architecture Patterns

### 1. Token-first, then OIDC (Confidence: HIGH)

crates.io trusted publishing **requires the crate to already be published** before OIDC can be configured. There is no "pending publisher" feature (unlike PyPI). The sequence must be:

1. Create crates.io API token with `publish-new` scope (for creating new crates)
2. Add as `CARGO_REGISTRY_TOKEN` GitHub secret
3. Run `release-rust.yml` (first publish with token)
4. Verify crate is live on crates.io
5. Configure trusted publisher on crates.io settings page
6. Update workflow to use OIDC
7. Test OIDC publish (requires version bump or dry-run verification)
8. Delete `CARGO_REGISTRY_TOKEN` secret

### 2. crates.io API Token Scopes (Confidence: HIGH)

For first publish, the token needs the `publish-new` scope (to create a new crate). For subsequent publishes, `publish-update` suffices. Options:
- **Recommended for first publish:** Create token with `publish-new` + `publish-update` scopes, scoped to crate pattern `cupel`
- Token is created at https://crates.io/settings/tokens/new
- After OIDC is working, delete the token entirely

### 3. OIDC Workflow Configuration (Confidence: HIGH)

The workflow needs these changes for OIDC:

```yaml
permissions:
  contents: write   # For GitHub Release creation
  id-token: write   # Required for OIDC token exchange

jobs:
  publish:
    environment: release  # Already present in current workflow
    steps:
      - uses: rust-lang/crates-io-auth-action@v1
        id: crates-io-auth
      - run: cargo publish --manifest-path crates/cupel/Cargo.toml
        env:
          CARGO_REGISTRY_TOKEN: ${{ steps.crates-io-auth.outputs.token }}
```

Key details:
- `id-token: write` must be at workflow level or job level
- The action outputs a `token` that is set as `CARGO_REGISTRY_TOKEN`
- Token is automatically revoked when the job completes (post step)
- Token is valid for ~30 minutes
- The `environment: release` already exists in the workflow and provides an extra security layer

### 4. Trusted Publisher Configuration on crates.io (Confidence: HIGH)

After first publish, configure on crates.io settings page with:
- **Required fields:**
  - GitHub owner: `wollax`
  - Repository: `cupel`
  - Workflow filename: `release-rust.yml`
- **Optional field:**
  - Environment: `release` (recommended — matches the existing `environment: release` in the workflow)

### 5. Assay Dependency Switchover (Confidence: HIGH)

Current assay workspace Cargo.toml:
```toml
[workspace.dependencies]
cupel = { path = "../cupel/crates/cupel" }
```

Changes to:
```toml
[workspace.dependencies]
cupel = "1.0.0"
```

No code changes needed — `assay-core` already uses `cupel.workspace = true` which will resolve from the registry.

### 6. patch.crates-io for Local Development (Confidence: HIGH)

For developers working on both assay and cupel locally, add to **assay workspace root** Cargo.toml:

```toml
[patch.crates-io]
cupel = { path = "../cupel/crates/cupel" }
```

Key rules:
- `[patch.crates-io]` can **only** be defined in the workspace root Cargo.toml
- Applies transitively to all workspace members
- The local crate version must satisfy the version constraint (e.g., local must be `1.x.y` to match `cupel = "1.0.0"`)
- This section should NOT be committed — it's for local development only

---

## Don't Hand-Roll

| Problem | Use Instead | Why |
|---------|-------------|-----|
| OIDC token exchange | `rust-lang/crates-io-auth-action@v1` | Handles JWT exchange, token revocation, all edge cases |
| Crate name availability check | `cargo publish --dry-run` or crates.io API | Don't try to pre-check name availability manually |
| Version extraction from Cargo.toml | `cargo metadata` (already in workflow) | Parses TOML correctly, handles edge cases |

---

## Common Pitfalls

### CRITICAL: `--locked` flag will fail (Confidence: HIGH)

The current workflow uses `cargo publish --manifest-path crates/cupel/Cargo.toml --locked`. However:
- `Cargo.lock` is in `.gitignore` (confirmed in repo)
- `Cargo.lock` is **not** tracked by git
- After `actions/checkout`, no `Cargo.lock` will exist
- `--locked` **errors when Cargo.lock is missing**: "error: the lock file needs to be updated but --locked was passed to prevent this"

**Fix:** Remove `--locked` from the `cargo publish` command. For a library crate, `--locked` is not meaningful anyway — downstream consumers resolve their own dependency versions. The `--locked` flag is primarily useful for binary crates where reproducibility matters.

### Crate size limit (Confidence: HIGH)

crates.io enforces a **10MB maximum** for the `.crate` package file. The existing `include` list in Cargo.toml is well-scoped (`src/**/*.rs`, `tests/**/*.rs`, `conformance/**/*.toml`, `Cargo.toml`, `LICENSE`, `README.md`). The `cargo package --list` step in the test job already verifies this.

### Crate name is available (Confidence: MEDIUM)

Verified via crates.io API — `cupel` returns 404 (not found), meaning the name is available. However, this could change before publish time. First-come, first-served.

### Publishing is permanent (Confidence: HIGH)

Published versions **cannot be deleted or overwritten**. Only `cargo yank` is available, which prevents new dependencies but doesn't remove code. The dry-run verification step is critical.

### README rendering (Confidence: MEDIUM)

crates.io renders the README from the published `.crate` file. The `readme = "README.md"` in Cargo.toml must point to a file included in the package. Since `README.md` is in the `include` list, this should work. However, relative links in the README (images, other docs) will be broken on crates.io — verify after publish.

### Token scope for first publish (Confidence: HIGH)

The token **must** have `publish-new` scope to create a new crate. A token with only `publish-update` will fail on first publish because the crate doesn't exist yet.

### `id-token: write` permission scope (Confidence: HIGH)

The current workflow has `permissions: contents: write` at the workflow level. Adding `id-token: write` here (workflow level) means both the `test` and `publish` jobs get it. This is fine — the test job won't use it, and requesting the permission doesn't grant any access by itself. Alternatively, it can be set at the job level on `publish` only.

### Trusted publisher workflow filename must be exact (Confidence: HIGH)

When configuring the trusted publisher on crates.io, the workflow filename must match exactly (e.g., `release-rust.yml`). Wildcards are not supported. If the workflow file is renamed, the trusted publisher configuration must be updated.

---

## Code Examples

### Example 1: OIDC-enabled release workflow (target state)

```yaml
name: Publish to crates.io

on:
  workflow_dispatch:
    inputs:
      dry-run:
        description: 'Dry run — test and verify without publishing'
        required: false
        type: boolean
        default: false

permissions:
  contents: write
  id-token: write

jobs:
  test:
    # ... unchanged ...

  publish:
    runs-on: ubuntu-latest
    needs: test
    if: ${{ inputs.dry-run != true }}
    environment: release
    steps:
      - name: Verify running from main
        if: github.ref != 'refs/heads/main'
        run: |
          echo "::error::Release workflow must be run from the main branch"
          exit 1

      - name: Checkout
        uses: actions/checkout@v4
        with:
          ref: ${{ github.sha }}
          fetch-depth: 0

      - name: Setup Rust
        uses: dtolnay/rust-toolchain@1.85.0

      - name: Get version
        id: version
        run: |
          VERSION=$(cargo metadata --manifest-path crates/cupel/Cargo.toml --format-version 1 --no-deps | jq -r '.packages[0].version')
          echo "version=$VERSION" >> $GITHUB_OUTPUT

      - name: Authenticate with crates.io
        uses: rust-lang/crates-io-auth-action@v1
        id: crates-io-auth

      - name: Publish to crates.io
        run: cargo publish --manifest-path crates/cupel/Cargo.toml
        env:
          CARGO_REGISTRY_TOKEN: ${{ steps.crates-io-auth.outputs.token }}

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          tag_name: rust-v${{ steps.version.outputs.version }}
          name: cupel rust-v${{ steps.version.outputs.version }}
          generate_release_notes: true
          make_latest: true
```

### Example 2: Assay Cargo.toml after switchover

```toml
[workspace.dependencies]
cupel = "1.0.0"
```

### Example 3: CONTRIBUTING.md patch.crates-io section

```markdown
## Working with Local cupel

If you're developing both `assay` and `cupel` simultaneously, override the registry dependency
with your local checkout by adding this to assay's root `Cargo.toml`:

\```toml
[patch.crates-io]
cupel = { path = "../cupel/crates/cupel" }
\```

**Do not commit this change.** The patch is for local development only.

The local `cupel` version must satisfy the workspace version constraint (e.g., `1.x.y`).
Remove the `[patch.crates-io]` section before committing or opening a PR.
```

---

## Discretionary Recommendations

### 1. OIDC workflow configuration (Decision area from CONTEXT.md)

**Recommendation:** Add `id-token: write` at the workflow-level `permissions` block alongside the existing `contents: write`. This is simpler than job-level permissions and the test job having the permission is harmless.

### 2. Fix roadmap "cupel-rs" references (Decision area from CONTEXT.md)

**Recommendation:** Skip in this phase. The roadmap references are cosmetic and don't affect execution. A separate housekeeping task is more appropriate. This phase already has enough moving parts (first publish, OIDC setup, assay switchover).

### 3. CONTRIBUTING.md section structure (Decision area from CONTEXT.md)

**Recommendation:** Add a `## Working with Local cupel` section to the existing CONTRIBUTING.md in assay. Keep it minimal — just the patch.crates-io snippet, a "do not commit" warning, and the version matching note. Do NOT add a commented-out example in Cargo.toml itself — it clutters the workspace manifest and developers should reference CONTRIBUTING.md.

---

## Open Questions

1. **Token creation is manual:** The `CARGO_REGISTRY_TOKEN` secret must be created by a human on crates.io and added to GitHub secrets before the first publish can run. This is a prerequisite, not an automatable step.

2. **OIDC configuration is manual:** After first publish, the trusted publisher must be configured through the crates.io web UI. This is also a prerequisite for the OIDC workflow update.

3. **OIDC verification without a version bump:** After switching the workflow to OIDC, there's no way to test it without publishing a new version (there's no OIDC dry-run). The first OIDC-based publish will be the real test. Consider whether to bump to 1.0.1 for an OIDC verification publish or accept the risk and verify on the next natural release.

---

*Phase: 19-first-publish--assay-switchover*
*Research completed: 2026-03-15*
