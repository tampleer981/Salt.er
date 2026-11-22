# Security Policy

## Supported versions

Only the latest minor release receives security fixes.

| Version        | Supported |
| -------------- | --------- |
| Latest minor   | Yes       |
| Anything older | No        |

## Reporting a vulnerability

Do not open a public issue. Use GitHub Private Vulnerability Reporting:
open the repository's **Security** tab, choose **Report a vulnerability**, and
describe the problem with steps to reproduce and the affected version.

If you cannot use GitHub, e-mail <tampleer980@gmail.com> instead. Do not include
details in a public place.

Response targets:

- Acknowledgement within 7 days.
- An assessment and a plan (fix, mitigation, or reason for declining) within 30 days.
- Coordinated disclosure: the advisory is published together with the fixed
  release, and reporters are credited unless they prefer otherwise.

These are targets for a volunteer-maintained project, not guarantees.

## Scope

In scope:

- Storage and handling of the wallhaven API key. The key is kept in Windows
  Credential Manager, never in `settings.json` or the logs, and is sent only to
  `wallhaven.cc` in the `X-API-Key` header.
- Downloading and writing image files (path handling, file names, partial files).
- The installer and update mechanism.

Out of scope:

- Vulnerabilities in wallhaven.cc itself; report those to wallhaven.
- Issues that require an attacker who already has code execution as the same
  Windows user.
- Releases are unsigned, so SmartScreen warnings are expected. Verify downloads
  against the published SHA-256 checksums.
