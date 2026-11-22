# Use WinUI 3 for the user interface

- Status: Accepted
- Date: 2026-11-21

## Context and Problem Statement

Salt.er is a tray app whose flyout should look native on Windows 11: acrylic
backdrop, rounded corners, system theme and accent. Which UI framework should
it use?

## Decision Drivers

- Native Windows 11 look and behavior, including light and dark themes.
- Small, self-contained distribution without a runtime to install.
- Maintainable by one person with a conventional MVVM structure.
- Testable logic on non-Windows CI.

## Considered Options

- WinUI 3 with the Windows App SDK
- WPF
- WinForms
- Electron or another web-based shell

## Decision Outcome

Chosen option: WinUI 3, because it is the framework that gives the Fluent look,
acrylic materials and theming of Windows 11 without custom drawing, and it
supports unpackaged, self-contained deployment.

The interaction logic lives in a separate `net10.0` project,
`Salter.Presentation`, so that it is tested on Linux CI; the WinUI project only
holds views and Windows integrations.

### Consequences

- Good: native appearance; system theme and accent for free; no web runtime.
- Good: view models are covered by tests on every OS.
- Bad: the app project builds on Windows only, and the XAML compiler is not
  available on Linux, so CI needs a Windows job to build and package it.
- Bad: self-contained builds are larger than a framework-dependent app.
- Bad: tray icon and flyout positioning need Win32 interop, because WinUI 3
  has no built-in notification area support.

## Pros and Cons of the Options

### WPF

- Good: mature, familiar.
- Bad: no Fluent or acrylic without third-party libraries and custom work.

### WinForms

- Good: small and simple.
- Bad: dated look; glass and rounded flyouts are hard to build.

### Electron or another web shell

- Good: cross-platform, rich styling.
- Bad: far larger footprint for a tray utility; not native.
