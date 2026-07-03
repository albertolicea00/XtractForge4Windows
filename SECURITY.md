# Security Policy

## Supported Versions

| Version | Supported |
|---|---|
| Latest (`main`) | ✅ |
| Older releases | ❌ — update to latest |

## Reporting a Vulnerability

**Do not open a public GitHub issue for security vulnerabilities.**

Report vulnerabilities privately via one of these channels:

1. **GitHub Security Advisory** — use the "Report a vulnerability" button in the
   Security tab of this repository (preferred)
2. **Email** — contact the maintainers listed in the repository profile

### What to include

- Description of the vulnerability and its potential impact
- Steps to reproduce
- Affected version(s)
- Any suggested fix or mitigation (optional but appreciated)

### Response timeline

- **Acknowledgement**: within 72 hours
- **Initial assessment**: within 7 days
- **Fix / advisory**: as soon as reasonably possible, typically within 30 days
  for critical issues

## Scope

### In scope

- Vulnerabilities in XtractForge application code (`XtractForge/`, `XtractForge.Core/`)
- Command/argument injection in download argument construction
  (`BuildArgs` in any downloader) — arguments are passed via `ArgumentList`,
  never through a shell; anything that breaks that assumption is a bug
- Path traversal in staging/finalize logic (`Staging.cs`) or in
  user-controlled filenames
- Unsafe handling of untrusted tool output (JSON parsing, progress lines)
- Insecure default configuration that could harm users

### Out of scope

- Vulnerabilities in the third-party tools themselves (yt-dlp, ffmpeg, Lux,
  gallery-dl, spotDL, curl) — report those upstream
- Malicious binaries the user has configured as tool paths (the app runs what
  the user points it at, by design)
- Issues requiring physical access to the machine
- Social engineering

## Security Model

XtractForge is a native WinUI 3 app that spawns user-installed command-line
tools as child processes (`CreateNoWindow`, no shell). There is no web content,
no embedded browser, and no dynamic code loading (no plugin system by design).
Untrusted input is limited to: URLs provided by the user, and stdout/stderr
produced by the tools. Both are treated as data — URLs are passed as single
`ArgumentList` entries, and tool output is only ever parsed, never executed.
