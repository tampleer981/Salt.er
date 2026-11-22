# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-11-22

### Added

- Tray flyout with browsing by Toplist, Latest, Hot and Random.
- Genre chips: All, Nature, Anime, Space, City, Architecture, Abstract, Cars and Minimal.
- Search with filters for aspect ratio (including vertical), resolution (native, 4K or any) and color.
- Hover Set button on thumbnails; preview with apply to all displays or one display.
- Settings for the download folder, start with Windows, theme (system, light or dark) and content purity.
- Optional wallhaven API key, stored in Windows Credential Manager; safe-for-work only without one.
- Rate limiting (45 requests per minute), retry on HTTP 429 and request timeouts.
- Unsigned installer, portable ZIP and SHA-256 checksums for x64 and ARM64, with automatic updates for
  installed copies.

[Unreleased]: https://github.com/tampleer981/salt.er/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/tampleer981/salt.er/releases/tag/v1.0.0
