# Development

## Prerequisites

- Windows 11 to build and run the app. Windows 10 2004 or later runs it.
- The .NET SDK pinned in `global.json` (.NET 10).
- Visual Studio 2026 with the **Windows application development** workload, or
  the .NET SDK with the Windows App SDK build tools.
- PowerShell 7.2 or later to run `build/pack.ps1`.

`Salter.Core`, `Salter.Presentation` and their tests build and run on Windows,
Linux and macOS. Only `Salter.App` requires Windows.

## Build and test

The portable projects and their tests, on any OS:

```powershell
dotnet test --solution Salt.er.Portable.slnf
dotnet format Salt.er.Portable.slnf --verify-no-changes
```

Tests use xUnit v3 on Microsoft.Testing.Platform, so the solution is passed with
`--solution`. Warnings are errors, and every public member of `Salter.Core` and
`Salter.Presentation` needs an XML documentation comment.

The app, on Windows:

```powershell
dotnet build src/Salter.App/Salter.App.csproj
dotnet build src/Salter.App/Salter.App.csproj -r win-arm64 -p:Platform=arm64
```

`Salt.er.sln` contains every project, including the app, and opens in Visual
Studio.

To produce the release files (installer, update feed, portable ZIP and
checksums) in `artifacts/releases/<arch>`:

```powershell
pwsh build/pack.ps1 -Version 0.1.0-local -Arch x64
```

## Running and debugging

Run `Salter.App` from Visual Studio or `dotnet run --project src/Salter.App`.
The app has no window of its own; look for the icon in the notification area
(it may be in the overflow).

- Logs are in `%LOCALAPPDATA%\Salt.er\logs`. Start there for any failure.
- Delete `%LOCALAPPDATA%\Salt.er\settings.json` to reset settings.
- The API key is the Credential Manager entry `Salt.er/...`; remove it there
  to test the no-key path.
- Update checks are off for builds that Setup did not install, so a debug
  build never updates itself.
- A second launch hands over to the running instance. Exit the tray icon
  before restarting under the debugger.

## Conventions

- C# style is defined in `.editorconfig` and enforced by `dotnet format`.
  Nullable reference types are enabled and warnings are errors.
- Package versions are central, in `Directory.Packages.props`.
- Logic goes in `Salter.Core` or `Salter.Presentation` behind an interface,
  with tests; `Salter.App` stays thin.
- User-facing strings are English and live in
  `src/Salter.App/Strings/en-US/Resources.resw`. View models obtain them
  through `ILocalizer`.
- Commits follow [Conventional Commits](https://www.conventionalcommits.org/).
- Record architectural decisions as ADRs in [adr](adr/README.md).
- UI follows the Windows 11 Fluent design language in [design.md](design.md):
  WinUI default controls and theme resources, and the motion rules there. Never
  animate the opacity, scale or translation of a control template part.

See [CONTRIBUTING.md](../CONTRIBUTING.md) for the pull request process.

## Documentation screenshots

The images in `docs/assets/screenshots` are real captures of the packaged app,
not mockups. The `Screenshots` workflow (`.github/workflows/screenshots.yml`)
runs `tools/screenshots/Capture-Screenshots.ps1` on a Windows runner. The script
launches the app, seeds `settings.json` for each theme, drives the UI through
UI Automation by `AutomationProperties.Name`, saves PNGs and a
`manifest.json`, and the workflow commits them back to the branch. It runs on
manual dispatch, and on pushes to branches other than `main` and pull requests
to `main` that change the app, the presentation layer or the capture tooling.
Pull request runs commit nothing.

The same run is the UI crash test. The script turns Windows animation effects
on first (hosted runners have them off, which hides crashes in animated
controls), and after the captures it uses every interactive control once per
theme. After each state and step it checks that the app is still running and
that its log has no `[FTL]` or `Unhandled` entry; on a crash it prints the app
log and the Windows Application event log entries and fails the run. CI's
build job also opens the flyout with animation effects on for ten seconds.

When the UI changes, update the script if an automation name changed, run the
workflow, and review the new images before merging. Controls need a stable
`AutomationProperties.Name` to be reachable by the script.
