# Releasing

Releases are cut by pushing a version tag. The release workflow does the rest.

## Versioning

Salt.er follows [Semantic Versioning](https://semver.org/). Versions look like
`1.2.0` or, for a pre-release, `1.3.0-rc.1`. Tags are the version prefixed
with `v`. A pre-release is published as a GitHub pre-release, and installed
apps update to stable releases only.

## Steps

1. Make sure `main` is green.
2. In `CHANGELOG.md`, rename `Unreleased` content into a new
   `## [x.y.z] - YYYY-MM-DD` section, leave an empty `Unreleased` section above
   it, and add comparison links if the file has them. The release workflow
   fails if the section for the tag is missing or empty; its body becomes the
   release notes.
3. Commit as `chore: release x.y.z` and push to `main`.
4. Tag and push:

   ```powershell
   git tag vx.y.z
   git push origin vx.y.z
   ```

## What the release workflow does

`.github/workflows/release.yml` runs on `v*.*.*` tags.

1. **Pack** runs `build/pack.ps1` on a Windows runner for x64 and ARM64: a
   self-contained publish, the portable ZIP, the Velopack installer and update
   feed, and a per-architecture `SHA256SUMS.txt`.
2. **Publish release** verifies each architecture's checksums, merges the files
   into one set with a single `SHA256SUMS.txt`, extracts the notes from
   `CHANGELOG.md`, and creates the GitHub release.

A manual run of the workflow packs the given version as a dry run and uploads
the files as workflow artifacts; it never publishes.

## Release files

| File | Purpose |
| --- | --- |
| `Salt.er-win-<arch>-Setup.exe` | Installer |
| `Salt.er-<version>-win-<arch>-portable.zip` | Portable build |
| `Salt.er-<version>-win-<arch>-full.nupkg` | Full update package |
| `Salt.er-<version>-win-<arch>-delta.nupkg` | Delta update package, when a previous release exists |
| `releases.win-<arch>.json`, `RELEASES-win-<arch>` | Update feed for each architecture |
| `SHA256SUMS.txt` | SHA-256 of every file above |

Installed apps read the feed and packages from the GitHub release. Velopack
builds a delta package only when it can find the previous release's full
package in the output directory, which the workflow does not download, so
updates currently use the full package. Delta packages are an optimization,
not a requirement.

## Verifying a release

```powershell
Get-FileHash .\Salt.er-win-x64-Setup.exe -Algorithm SHA256
```

Compare the hash with the matching line in `SHA256SUMS.txt`. On Linux or
macOS, `sha256sum --check --ignore-missing SHA256SUMS.txt` does it for every
downloaded file.

Releases are not code-signed, so SmartScreen warns on first run. See
[ADR 0003](adr/0003-unsigned-unpackaged-distribution.md).
