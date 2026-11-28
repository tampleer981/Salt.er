# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.3.0] - 2026-11-28

### Changed

- Windows 11 redesign: the flyout uses the system's Fluent controls, type and
  theme colours, so it follows the light or dark theme and your accent color.
- A clearer, more transparent window: thin desktop acrylic under a very light
  layer, 8 px corners and a 1 px outline, without the custom sheen.
- Title-bar style headers with the app mark, subtle icon buttons and hairline
  dividers; a standard search box that keeps the Ctrl+K hint.
- Genres and filter values are pills with an accent selection; the sort order
  is a Windows tab bar, and the filters panel is a card.
- Wallpapers are rounded cards; hovering one outlines it and shows an accent
  Set button instead of zooming it.
- The preview has a rounded image, subtle tag pills, a segmented display
  choice and an accent Set as wallpaper button.
- Settings are grouped cards with an icon, a title and a description, like
  Windows Settings.
- The confirmation toast is an InfoBar-like pill.

### Fixed

- A crash at startup while Windows animation effects are on.
- A washed-out, light gray window in the dark theme.
- Animations are set up in code only, never in control templates, and are
  skipped if Windows refuses one.

### Tests

- The screenshot workflow turns Windows animation effects on, uses every
  interactive control in both themes and fails if the app crashes or logs a
  fatal error. It also runs on pushes and pull requests that change the UI.
- CI opens the flyout with animation effects on and checks that the app stays
  up.

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

[Unreleased]: https://github.com/tampleer981/salt.er/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/tampleer981/salt.er/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/tampleer981/salt.er/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/tampleer981/salt.er/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/tampleer981/salt.er/releases/tag/v1.0.0
