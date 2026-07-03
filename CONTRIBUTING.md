# Contributing to XtractForge for Windows

Thanks for helping forge a better downloader. This document covers workflow and
ground rules; architecture lives in [CLAUDE.md](CLAUDE.md), design rationale in
[DESIGN.md](DESIGN.md).

## Prerequisites

- Windows 10 1809+ with Visual Studio 2022 (WinUI 3 / Windows App SDK workload),
  or just the .NET 8 SDK for Core work — `XtractForge.Core` and the tests build
  and run on macOS/Linux too.
- The tools you want to test against: `winget install yt-dlp.yt-dlp Gyan.FFmpeg`,
  `pip install gallery-dl spotdl`, `scoop install lux`.

## Build, run, test

```powershell
dotnet build                          # build the solution
dotnet test                           # xUnit suite — must be green before every PR
dotnet run --project XtractForge     # run the app (Windows only)
```

## Ground rules (scope)

These are deliberate product decisions, not oversights — PRs that reverse them
will be declined:

- **No plugin system.** Downloaders are compiled in. Adding one is a code change
  that ships with a release.
- **No theme system.** Appearance is System / Light / Dark via `ElementTheme` only.
- **One window.** No NavigationView shells, tabs, or dashboards.
- **Minimal dependencies.** Windows App SDK + CommunityToolkit.Mvvm; Core is
  dependency-free.

## Adding or changing a downloader

1. Subclass `DownloaderBase` in `XtractForge.Core/Downloaders/`.
2. Register it in `DownloaderRegistry.All` **before** yt-dlp (the catch-all stays last).
3. Cover `CanHandle`, `BuildArgs`, and `ParseProgress` with tests in
   `XtractForge.Tests/` — use real output lines from the tool.
4. Update `SettingsDialog` if the tool has options, and `AppSettings` for new keys.
5. Keep Core free of Windows APIs so the tests keep running everywhere.

## Commits & PRs

- **Conventional Commits**: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`.
  Imperative mood, ≤72-char subject, body explains *why* when it isn't obvious.
- Small, focused PRs. One logical change per PR.
- `dotnet test` green is a hard requirement; add tests for anything with logic.
- Fill in the PR template; link related issues.

## Reporting bugs / requesting features

Use the issue templates. For bugs, include Windows version, tool versions
(`yt-dlp --version` etc.), the URL type (not necessarily the URL), and the
failing output line if visible.

## Code of Conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md). Be excellent
to each other.
