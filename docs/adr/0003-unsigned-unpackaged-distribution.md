# Distribute unsigned, unpackaged builds with Velopack

- Status: Accepted
- Date: 2026-11-21

## Context and Problem Statement

Salt.er needs an installer, automatic updates and a portable build, published
from GitHub. Code-signing certificates cost money and MSIX distribution
requires a trusted certificate or the Microsoft Store. How should releases be
packaged and delivered?

## Decision Drivers

- No recurring cost for a small open-source project.
- Per-user install without administrator rights.
- Updates that do not interrupt the user.
- Verifiable downloads despite the lack of a signature.
- Nothing to install first (no separate runtime).

## Considered Options

- Unpackaged, self-contained app with Velopack, unsigned
- MSIX package, signed
- MSIX package through the Microsoft Store
- Plain ZIP only

## Decision Outcome

Chosen option: unpackaged, self-contained x64 and ARM64 builds, packed with
Velopack into a per-user installer and update feed, plus a portable ZIP, all
unsigned and published to GitHub Releases with a `SHA256SUMS.txt`.

Velopack installs under `%LOCALAPPDATA%\Salt.er` without elevation and updates
from the GitHub release feed. An installed copy downloads updates in the
background and applies them when the user exits the app from the tray menu;
the portable ZIP does not update.

### Consequences

- Good: no certificate cost, no Store dependency, no administrator prompt.
- Good: updates are automatic for installed copies.
- Bad: SmartScreen shows **Windows protected your PC** for unsigned
  executables until reputation builds. The README documents the steps and
  how to verify the SHA-256 checksum.
- Bad: users must trust the checksums, which come from the same place as the
  files; signing would remove that gap and can be added later without changing
  this design.
- Neutral: the install root and the app data folder are both
  `%LOCALAPPDATA%\Salt.er`; uninstalling removes the data too.

## Pros and Cons of the Options

### Signed MSIX

- Good: clean install and uninstall, no SmartScreen warning with a trusted
  certificate.
- Bad: requires a paid certificate or Store account.

### Plain ZIP only

- Good: simplest.
- Bad: no updates, no start menu entry, no uninstall.
