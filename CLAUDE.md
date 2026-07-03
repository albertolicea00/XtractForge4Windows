# XtractForge for Windows — Agent & Developer Documentation

Native Windows media downloader. WinUI 3 (Windows App SDK) + C#/.NET 8, no web stack.
This is a ground-up native rewrite of the old Tauri app (see `../old/` for reference
only — do not copy its architecture, plugin system, or theme system).

---

## Product Scope

One-window app. The user gets media onto their machine in three gestures:

1. **Drag & drop** a URL (or text containing URLs) onto the window.
2. **Paste** (Ctrl+V anywhere in the window, or the paste button).
3. That's it. Info is fetched, options shown inline, download queued.

Everything else (settings, queue, history) lives inside that same window — settings as
a modal `ContentDialog` or flyout page, not a multi-tab shell like the old app.

### Explicitly out of scope (do NOT build unless asked)

- **No plugin system.** Downloaders are compiled in. Adding one = code change + app update.
  Never add dynamic loading of `.js`/scripts/assemblies.
- **No theme system.** Appearance is System / Light / Dark only, via `ElementTheme`
  (`Default | Light | Dark`) + Mica backdrop. No custom colors, no CSS-like variables,
  no accent overrides beyond the OS accent color.
- **No remote intake yet.** Chrome extension, Telegram bot, and mobile-sync intake are
  planned future features. The only forward-compat allowance: keep URL intake funneled
  through a single `IntakeService.Submit(url)` entry point so a `xtractforge://`
  protocol handler can be added later without refactoring. Do not implement the
  protocol, server, or any sync now.

---

## Tech Stack

- C# / .NET 8, WinUI 3 via Windows App SDK (latest stable), Windows 10 1809+ target.
- MVVM with `CommunityToolkit.Mvvm` (source-generated `[ObservableProperty]`,
  `[RelayCommand]`). Keep third-party dependencies to that toolkit; prefer BCL otherwise.
- Child processes via `System.Diagnostics.Process`; async stdout/stderr line streaming.
- Tests: xUnit for all pure logic (routing, args, parsing, staging paths).

## Project Layout

Three projects. All download logic lives in `XtractForge.Core` (plain net8.0,
ZERO Windows dependencies) so the tests run on any OS; the WinUI app project is
a thin native shell over it.

```
XtractForge.sln
XtractForge.Core/               # net8.0 class library — pure logic, fully testable
├── Models/                     # Models.cs (MediaInfo, Command, OptionField, …), AppSettings.cs
├── Downloaders/
│   ├── IDownloader.cs          # interface + DownloaderBase + DownloaderRegistry (routing order)
│   ├── YtDlp.cs, Lux.cs, GalleryDl.cs, SpotDl.cs, FFmpegTool.cs, Curl.cs
└── Engine/
    ├── DownloadManager.cs      # queue state + DownloadItem; raises plain .NET events
    ├── ProcessRunner.cs        # spawn (ArgumentList, CreateNoWindow), stream lines, kill tree
    ├── Staging.cs              # temp-dir staging + move-on-success + organize
    └── Intake.cs               # pure URL extraction from dropped/pasted text
XtractForge/                    # WinUI 3 app (net8.0-windows10.0.19041, unpackaged dev build)
├── App.xaml(.cs)               # startup, single-instance redirect (AppInstance)
├── MainWindow.xaml(.cs)        # the one window: Mica, custom title bar, MenuBar
├── Views/
│   ├── MainPage.xaml(.cs)      # drop zone + clipboard InfoBar + queue ListView
│   ├── OptionsDialog.xaml(.cs) # per-download options (dynamic fields, built in code)
│   └── SettingsDialog.xaml(.cs)# General + per-downloader sections (Appearance = View menu)
├── ViewModels/                 # MainViewModel, DownloadItemViewModel (DispatcherQueue marshalling)
└── Services/                   # SettingsService (JSON), ThemeService, NotificationService, IntakeService
XtractForge.Tests/              # xUnit against Core — runs on macOS/Linux too
```

## Downloader Interface (fixed, compiled-in)

```csharp
public interface IDownloader
{
    string Id { get; }            // "yt-dlp", "lux", "gallery-dl", "spotdl", "ffmpeg", "curl"
    string Name { get; }
    string BinaryDefault { get; } // overridable path in Settings
    string InstallHint { get; }   // e.g. "winget install yt-dlp"

    Task<DependencyStatus> CheckDependencyAsync(AppSettings s); // runs `--version`
    bool CanHandle(Uri url);
    Task<MediaInfo> GetInfoAsync(Uri url, AppSettings s);
    Command BuildArgs(Uri url, DownloadOptions options, AppSettings s); // binary + args
    ProgressUpdate? ParseProgress(string line);
}
```

**Routing** (`DownloaderRegistry.Route(url)`): first match wins, most specific first —
`spotdl → gallery-dl → lux → ffmpeg → curl → yt-dlp`. yt-dlp's `CanHandle` always
returns `true` (catch-all). Downloaders disabled in Settings are skipped.
Each downloader's URL matching, arg building, and progress-line regexes are ported
from `../old/src/plugins/*.ts`. One deliberate deviation: lux no longer claims
youtube.com/youtu.be/twitter/x/instagram — YouTube belongs to yt-dlp, and
twitter/instagram were already captured by gallery-dl (which precedes lux).

**Per-download options:** `MediaInfo` may carry an option schema (format/quality/audio-only
etc., same idea as old `_downloadOptions`) rendered by `OptionsDialog`; simple sources set
`SimpleDownload = true` and skip the dialog.

## Download Engine

- `DownloadManager` is the single source of truth for the queue; UI binds via MVVM.
  Marshal progress updates to the `DispatcherQueue`, throttled (~10/s max).
- **Staging** (default on): child process runs in
  `<downloadFolder>\.xtractforge-tmp\<urlHash>\`. Exit 0 → move files to final folder
  applying `organize` (`none | type | source`), delete temp dir. Failure → leave temp
  dir in place so the tool can resume later.
- **Cancel:** kill the full process tree (`Process.Kill(entireProcessTree: true)`).
- **Pause/Resume:** Windows has no SIGSTOP. Pause = kill the process (staging dir
  survives); Resume = relaunch with the tool's continue flag (yt-dlp `-c` etc.).
  Downloaders whose tool can't resume simply don't expose pause.
- Spawn with `CreateNoWindow = true` — never flash console windows.
- Completion: toast notification (AppNotificationManager) with "Open folder" action.

## Settings (JSON in `%LOCALAPPDATA%\XtractForge\config.json`)

Keys mirror the old `config.json` where still relevant:
`downloadFolder`, `speedLimit`, `embedSubtitles`, `sponsorBlock`, `stageToTemp`,
`organize`, `disabledDownloaders[]`, per-downloader binary paths and options
(`luxMultiThread`, `spotdlFormat`, `spotdlBitrate`, `galleryDlCookies`, …),
`appearance` (`system | light | dark`), `watchClipboard` (opt-in: offer clipboard URL
on window activation).

## Native Integration (this is the point of the rewrite)

- **MenuBar** in the title-bar area: File (Paste URL Ctrl+V, Open Downloads Folder,
  Exit), View (Appearance: System/Light/Dark), Help (About). Keyboard accelerators
  throughout.
- Fluent design as-is: Mica window backdrop, `ExtendsContentIntoTitleBar`, standard
  WinUI controls, Segoe Fluent Icons, OS accent color. No custom chrome, no custom
  palette.
- Taskbar progress (`ITaskbarList3`-equivalent via window APIs) while downloading;
  toast on completion.
- Single-instance app: a second launch forwards its arguments/clipboard intent to the
  running instance via `AppInstance` redirection.
- Distribution: MSIX package (unsigned dev / signed for release). No auto-update
  machinery for now — updates ship as new releases.

## Development Workflow

```powershell
dotnet build                                   # build solution
dotnet test                                    # run xUnit suite
# Run/debug via Visual Studio 2022 (WinUI 3 tooling) or `dotnet run` on the app project
```

- Development happens on macOS + a Windows machine/VM for build & run; pure-logic code
  and tests must stay platform-neutral so `dotnet test` runs anywhere.
- Run tests after every commit; suite must be green before any push.

## Git Rules

- **Conventional Commits** (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`),
  imperative, ≤72-char subject. Commit in small meaningful units as you work.
- **No co-author trailers.** Never add `Co-Authored-By` or any generated-with footer.
- Never push, tag, or release unless explicitly asked. Commits stay local.
