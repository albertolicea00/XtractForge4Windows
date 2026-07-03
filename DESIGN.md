# XtractForge for Windows — Design

## North Star: "The Invisible Forge"

The old XtractForge chased a "Cyber-Glass" identity — custom palettes, glassmorphism,
themable everything. This app deliberately goes the other way: **it should look and
feel like Microsoft shipped it.** No custom chrome, no brand palette, no theme
engine. The design *is* Fluent; XtractForge's personality lives in how little it
asks of you.

## Experience Principles

1. **Three gestures, one window.** Drop a link, paste a link (Ctrl+V), or accept
   the clipboard suggestion. Everything else — queue, progress, results — happens
   in the same window.
2. **The OS owns the pixels.** Mica backdrop, Segoe Fluent Icons, standard WinUI
   controls, the user's accent color. Appearance is System / Light / Dark, nothing else.
3. **Progress you can trust.** Real tool output drives the UI (percent, speed, ETA
   parsed from each tool's stdout). No fake progress bars.
4. **Fail soft, resume later.** Staged downloads mean a failed or paused download
   never litters the Downloads folder; the temp dir survives so tools can resume.
5. **Quiet by default.** One toast on completion or failure. Nothing else interrupts.

## The One Screen

```
┌────────────────────────────────────────────┐
│ File  View  Help                           │   ← MenuBar (Paste URL Ctrl+V, …)
├────────────────────────────────────────────┤
│  ⌄ Drop a link to download    [Paste URL]  │   ← drop zone (calm, bordered)
├────────────────────────────────────────────┤
│  ℹ Link on clipboard        [Download] [×] │   ← opt-in clipboard InfoBar
├────────────────────────────────────────────┤
│  ⬇ Big Buck Bunny — 42% · 3.2MiB/s · 0:42  │   ← queue rows: icon, title,
│  ✓ Artist - Song.mp3    C:\…\Downloads     │      progress, inline actions
│  ⚠ Failed clip          [retry] [remove]   │
└────────────────────────────────────────────┘
```

- **Options dialog** (ContentDialog) appears only when a download has real choices
  (format/quality); simple sources (galleries, Spotify) skip straight to downloading.
- **Settings** open as a dialog from File → Settings: General + per-downloader
  sections. Appearance lives in the View menu.
- **Windows-specific pause:** there is no SIGSTOP — pause kills the process and
  keeps the staging dir; resume relaunches with the tool's continue flag. Only
  resumable tools (yt-dlp, curl) expose pause.

## Typography & Color

System all the way down: Segoe UI Variable via standard text styles, semantic
theme resources only (`TextFillColorSecondaryBrush`, etc.), the OS accent color.
If a design choice needs a hex code, it's wrong for this app.

## Anti-Goals

- No NavigationView shell, no dashboard density. One screen.
- No theme system, no accent overrides, no acrylic experiments beyond Mica.
- No plugin marketplace UI. Six tools, compiled in.
- No progress theater — if a tool reports nothing, show an indeterminate bar.

## Code Structure

Three projects: `XtractForge.Core` (models, downloaders, engine — pure .NET 8,
tested), `XtractForge` (WinUI 3 app), `XtractForge.Tests` (xUnit). See
[CLAUDE.md](CLAUDE.md) for the full map.
