# Design language

Salt.er looks like a part of Windows 11: a translucent Fluent flyout over the
desktop, built from the WinUI default controls and theme resources. This page
is the reference for the views in `src/Salter.App` (`App.xaml`,
`Styles/Glass.xaml`, `Styles/Controls.xaml` and `Views/`).

## Principles

- **System first.** Use the WinUI default styles and templates (buttons, text
  boxes, toggle switches, check boxes, `SelectorBar`). They carry the Fluent
  visuals, focus visuals and pointer animations. Restyle them through resource
  keys and setters, not new templates.
- **Theme resources, not colours.** Text, fills, strokes, cards and the accent
  come from the WinUI theme resources, so the flyout follows the light or dark
  theme and the user's accent colour. `Styles/Glass.xaml` defines only what the
  system has no resource for.
- **Content over chrome.** The desktop shows through the window; wallpapers are
  the only colourful surfaces. The brand appears in the header's mark and the
  accent dot of the wordmark only.
- **Quiet motion.** Short, one-off transitions that follow the Windows
  animation effects setting; nothing runs continuously except the loading
  placeholder pulse, and only while a page loads.

## Tokens

| Token | Resource | Use |
| --- | --- | --- |
| Primary text | `TextFillColorPrimaryBrush` | Titles, labels, values |
| Secondary text | `TextFillColorSecondaryBrush` | Descriptions, metadata, field labels (`CaptionTextStyle`) |
| Tertiary text | `TextFillColorTertiaryBrush` | Attribution, disclaimer, the Ctrl+K hint |
| Disabled text | `TextFillColorDisabledBrush` | Disabled custom controls |
| Accent | `AccentFillColorDefaultBrush`, `AccentTextFillColorPrimaryBrush`, `TextOnAccentFillColorPrimaryBrush` | Selected pills, primary buttons, the wordmark dot, the open filters toggle |
| Control fill | `ControlFillColorDefaultBrush`, `ControlFillColorSecondaryBrush` | Unselected pills, chips, image placeholders |
| Subtle fill | `SubtleFillColorTransparentBrush`, `SubtleFillColorSecondaryBrush`, `SubtleFillColorTertiaryBrush` | Hover and pressed states of icon buttons, pills and segments; tag pills |
| Segmented track | `ControlAltFillColorSecondaryBrush`; selected `ControlSolidFillColorDefaultBrush` | Display and theme choice |
| Card | `CardBackgroundFillColorDefaultBrush` + `CardStrokeColorDefaultBrush` | Filters panel, settings cards, wallpaper card hairline |
| Control stroke | `ControlStrokeColorDefaultBrush` | Pills, chips, swatches, the segmented track |
| Divider | `DividerStrokeColorDefaultBrush` (`DividerStyle`) | Hairlines under headers and above the detail action |
| Window stroke | `SurfaceStrokeColorDefaultBrush` | The 1 px outline of the flyout |
| Floating surface | `FloatingFillBrush` + `SurfaceStrokeColorFlyoutBrush` | Refresh button and toast over wallpapers |
| Window layer | `GlassFillBrush` | The very light layer over the desktop acrylic |
| On image | `OnImageTextBrush`, `OnImageStrokeBrush`, `ThumbnailOverlayBrush` | Text, hover hairline and gradient on wallpapers |
| Status | `SystemFillColorSuccessBrush`, `SystemFillColorCriticalBrush` | Toast icons |

Type uses the WinUI type ramp: `CaptionTextBlockStyle` (12),
`BodyTextBlockStyle` (14), `BodyStrongTextBlockStyle` (14 SemiBold) and
`SubtitleTextBlockStyle` (20 SemiBold). Body text keeps the ramp's
`XamlAutoFontFamily` (Segoe UI Variable Text on Windows 11); titles use
`DisplayFontFamily` (Segoe UI Variable Display, falling back to Segoe UI).
Icons are Segoe Fluent Icons glyphs (`FontIcon`).

Geometry: the window has 8 px corners (the Windows 11 window radius) and a
1 px stroke; cards and wallpapers use `CardCornerRadius` (8); controls use
`ControlCornerRadius` (4); pills are fully rounded (`PillCornerRadius` 15,
`SmallPillCornerRadius` 13). Side padding is 12 px, gaps between cards 8 px,
headers 48 px high.

## Window

The window keeps the thin desktop acrylic of `GlassAcrylicBackdrop`
(`DesktopAcrylicKind.Thin` with explicit dark and light tint and fallback
colours), under `GlassFillBrush`, a layer light enough for the blurred desktop
to show through. `FallbackWindowBrush` replaces the acrylic when it is
unavailable. The content is clipped to the window's 8 px corners.

## Components

| Component | Built from |
| --- | --- |
| Header | 48 px title-bar row: brand mark (the tray icon's strip, drawn in XAML), name in `BodyStrong` Display, 32 × 32 icon buttons (`TitleBarButtonStyle`, based on `SubtleButtonStyle`) on the right, hairline below |
| Filters toggle | `TitleBarToggleButtonStyle`: subtle; while open, a subtle fill and an accent glyph |
| Search | Default `TextBox` with a leading search glyph and the Ctrl+K hint while empty |
| Genres, filter values | `ChipRadioStyle` pills: control fill and stroke; selected is the accent fill |
| Sort tabs | WinUI `SelectorBar`, hairline below |
| Wallpaper card | `ThumbnailButtonStyle` button holding the image (rounded by its `ImageBrush`), a hover overlay (lighter hairline, resolution, accent Set button) and the card hairline |
| Refresh | `FloatingButtonStyle`: 40 px circle on the floating surface |
| Filters panel | `CardStyle` card with labelled rows; exclusion box and removable chips |
| Details | Preview card, subtle tag pills (right-click to exclude), segmented display choice (`SegmentRadioStyle` in `SegmentedHostStyle`), full-width `PrimaryButtonStyle` (accent) with an icon, metadata in secondary and tertiary text |
| Settings | Group headers in `BodyStrong`; cards with an icon, a title and a description on the left and the control on the right or below; default `ToggleSwitch`, `CheckBox`, `PasswordBox` and outlined buttons with icons |
| Toast | InfoBar-like pill on the floating surface with a status icon |

## Motion

- Page changes fade (the details also slide up 12 px); the flyout content
  fades in and slides up as it opens; wallpaper images fade in once decoded;
  the hover overlay and the toast fade.
- All of these go through `Views/Motion.cs`: it reads
  `UISettings.AnimationsEnabled` on every use (lazily created, in a
  `try`/`catch`), sets the final value at once while animations are off, and
  falls back to the final value if the framework refuses a transition.
- Implicit transitions are set only by `Motion`, only on elements of the
  app's own views, never in markup.
- Custom templates change brushes only: `BrushTransition` (0.083 s, as in the
  WinUI templates) or no transition at all.

**Crash rule.** Never animate `Opacity`, `Scale` or `Translation` of a
control template part, with `Setter`s plus `ScalarTransition` or
`Vector3Transition`, or with `OpacityTransition`. In Windows App SDK 1.8 this
crashes `Microsoft.ui.xaml.dll` (0xc0000005 in
`CUIElement::StartAnimationImpl`) while Windows animation effects are on. CI
turns animation effects on before it drives every control (see
[development.md](development.md#documentation-screenshots)), so such a crash
fails the build.

## Resources

No new caches, no continuous animations, thumbnails decoded at the flyout
width for the display scale (`FeedLayout.DecodePixelWidth`), and no in-app
acrylic brushes: floating surfaces are solid, translucent fills.
