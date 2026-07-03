# XtractForge4Windows
A powerful, modern and modular media engine capable of downloading, extracting and processing content through multiple CLI‑based workflows.

Native Windows app (WinUI 3 + .NET 8, single window, Mica). Drop or paste a
link — video, audio, gallery, stream, or direct file — and XtractForge routes
it to the right tool and downloads it.

Bundled tools (install the ones you use): [yt-dlp](https://github.com/yt-dlp/yt-dlp),
[lux](https://github.com/iawia002/lux), [gallery-dl](https://github.com/mikf/gallery-dl),
[spotDL](https://github.com/spotDL/spotify-downloader), [FFmpeg](https://ffmpeg.org),
and curl (pre-installed on Windows 10+).

```powershell
winget install yt-dlp.yt-dlp Gyan.FFmpeg
pip install gallery-dl spotdl
scoop install lux
```

## Build & run

Requires Windows 10 1809+ and Visual Studio 2022 with the **WinUI 3 / Windows
App SDK** workload (or the .NET 8 SDK for CLI builds).

```powershell
dotnet build                          # build the solution
dotnet test                           # run the xUnit suite (XtractForge.Core — runs on any OS)
dotnet run --project XtractForge     # run the app (Windows only)
```

The solution is split so all download logic lives in `XtractForge.Core`
(plain .NET 8, no Windows dependencies) — that project and its tests build and
run on macOS/Linux too; only the WinUI `XtractForge` app project requires Windows.

## Development

See [CLAUDE.md](CLAUDE.md) for architecture, scope rules, and contribution
conventions, and [CONTRIBUTING.md](CONTRIBUTING.md) for workflow.
