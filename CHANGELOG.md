# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-11-22

### Added

- Exclusion filters: leave tags out of every feed from the filters panel or a tag's context menu in the preview.
  Up to ten one-word tags are kept in the settings.
- Light animations: the flyout content fades in and slides up as it opens, the preview slides up, thumbnails fade
  in once loaded and grow slightly on hover, and chips and tabs ease between states. They follow the Windows
  animation effects setting.

### Changed

- Refresh shuffles wallpapers randomly: the first pages of a sorted feed load in a random order and the
  wallpapers on each page are shuffled; Random also shuffles each page.
- More transparent glass, over a lighter desktop acrylic, with text kept legible by the blur.
- Smoother scrolling: thumbnails are prepared further ahead of the visible area.

### Fixed

- Light slivers of plain acrylic in the corners of the flyout on Windows 11.
- A short line above the refresh button.

## [1.1.0] - 2026-11-22

### Added

- Silent automatic updates: a downloaded release is applied at the next start, which restarts straight into the
  new version, as well as on exit from the tray menu.
- Running copies check for updates every six hours, not only at startup.

### Changed

- Refresh shows a new set of wallpapers: Random draws a new seed, other feeds jump to another of their first
  ten pages.
- New tray icon matching the app icon.
- README lists typical memory use (50–120 MB).

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

[Unreleased]: https://github.com/tampleer981/salt.er/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/tampleer981/salt.er/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/tampleer981/salt.er/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/tampleer981/salt.er/releases/tag/v1.0.0
