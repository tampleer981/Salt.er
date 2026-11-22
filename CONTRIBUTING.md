# Contributing to Salt.er

Thanks for taking the time. Salt.er is deliberately small, so the best
contributions are focused bug fixes and well-scoped improvements. Please follow
the [Code of Conduct](CODE_OF_CONDUCT.md).

## Prerequisites

- Windows 11 (the app itself targets Windows 10 2004 or later).
- Visual Studio 2026 with the **Windows application development** workload, or
  the .NET 10 SDK plus the Windows App SDK workload. `global.json` pins the SDK.

The `Salter.Core` and `Salter.Presentation` projects and their tests also build
on Linux and macOS. Only `Salter.App` needs Windows.

## Build and test

On any OS, build and test the portable projects through the solution filter:

```powershell
dotnet test --solution Salt.er.Portable.slnf
dotnet format Salt.er.Portable.slnf --verify-no-changes
```

On Windows, build the app (x64 unless an architecture is given), or open
`Salt.er.sln`, which contains every project:

```powershell
dotnet build src/Salter.App/Salter.App.csproj
dotnet build src/Salter.App/Salter.App.csproj -r win-arm64 -p:Platform=arm64
```

To build the release files locally on Windows (installer, update feed, portable
ZIP and `SHA256SUMS.txt` in `artifacts/releases/<arch>`), run the packaging
script with PowerShell 7. It installs the Velopack CLI into `artifacts/tools`:

```powershell
pwsh build/pack.ps1 -Version 0.1.0-local -Arch x64
```

CI runs it for both architectures on every push. Releases are published by the
release workflow when a `vX.Y.Z` tag is pushed.

Tests use xUnit v3 on Microsoft.Testing.Platform, which is why the solution is
passed with `--solution`. CI runs the same commands, with warnings treated as
errors.

## Proposing changes

- Bug fixes: open a pull request, linking the issue if there is one.
- Anything larger, or anything touching the non-goals in the README: open an
  issue first and wait for agreement before writing code.
- Architectural decisions are recorded as ADRs in `docs/adr`. A change that
  alters the architecture should add or supersede one.

## Branches and commits

- Branch from `main` and keep pull requests to one logical change.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/):
  `feat:`, `fix:`, `docs:`, `build:`, `ci:`, `test:`, `refactor:`, `chore:`.
  Use the imperative mood and explain why, not only what.
- Rebase on `main` rather than merging it into your branch.

## Code style

Style is defined in `.editorconfig` and enforced by `dotnet format`. Nullable
reference types are on and warnings are errors. User-facing text is English and
lives in `Strings/en-US/Resources.resw`.

## Pull request checklist

- [ ] Tests cover the change and `dotnet test --solution Salt.er.Portable.slnf` passes.
- [ ] `dotnet format Salt.er.Portable.slnf --verify-no-changes` passes.
- [ ] `CHANGELOG.md` has an entry under `Unreleased` for user-visible changes.
- [ ] Documentation is updated where behavior changed.
- [ ] UI changes include a screenshot.
