# kHz Analyzer — Spec v2.1: decisions that amend the Master Specification v2.0

Date: 2026-11-10. Decided by the product owner (DUHZZ). Where this file and
`kHz_Analyzer_Master_Specification.md` disagree, this file wins.

## 1. Product and distribution

| Topic | Decision |
|---|---|
| Price | Free. No licensing, payment, activation or telemetry. |
| Distribution | GitHub releases only. Not the Microsoft Store. |
| Platforms | Windows 10 and Windows 11, x64. |
| Language | English only, in the UI, the reports and the docs. Strings live in resource files (`.resw`, `en-US` default), never hardcoded in XAML or C#. |

## 2. Analysis criterion (replaces "20 kHz+" in the Master Spec)

- A track passes when it has **sustained** spectral content up to **19 000 Hz** or more.
- Engine: full-signal streaming, FFT window 65 536, 50 % overlap, mean aggregation,
  theoretical noise floor per bit depth, 3 dB margin. These parameters are frozen.
- Sampling-based shortcuts are rejected (see `limite-tamanho-janela.md`).
- Verified on 2026-11-10 (`docs/handoff/verificacao.txt`): 10/10 fixtures, streaming
  identical to the original in-memory method on 43 files, identical with 1 and 4 workers.

## 3. Look and feel

| Topic | Decision |
|---|---|
| Default theme | Light. Dark is chosen in Settings. |
| Theme switch | Animated light-to-dark transition (radial reveal), skipped when Windows "animation effects" is off. |
| Glass material | Acrylic (`DesktopAcrylicBackdrop` and in-app acrylic brushes). No CSS `backdrop-filter`, no true refraction. Solid fallback where acrylic is unavailable. |
| Reference | Apple Music layout: sidebar, content list, bottom mini player. |
| Sidebar toggle | Panel collapse/expand icon. |
| Analyzer item | Label and tooltip "Analyzer", investigation-style icon. |
| Icons | Three options each for the app icon, the Analyzer item and the Download & Trim item, for the owner to choose. |

## 4. Navigation

1. **Analyzer** — batch folder analysis, single-file inspection.
2. **Download & Trim** — yt-dlp + FFmpeg.
3. **Snippets** — batch preview generator.
4. **About** — user guide.
5. **Settings** (footer).

## 5. Analyzer

- Batch scan with cache (path + size + modified date + engine version).
- Spectrogram and waveform exist **only here**, for single files (drag one file in,
  or open a row). Detailed spec: `spec-espectrograma-e-analise-musical.md`.
- Mini player at the bottom: double-click a row to play, click-to-seek timeline, volume slider.
- Row context menu: Track information, Show in Explorer, Remove from queue, Play, Show full analysis.
- Playback: WAV, MP3 and M4A through the Windows media pipeline. AIFF playback needs
  verification; if Windows cannot decode it, feed PCM from the app's own AIFF reader.

## 6. Download & Trim

- Input: a YouTube or SoundCloud URL. Shows title and thumbnail before anything is downloaded.
- Trimming is by **time codes** (`HH:MM:SS` start and end). No waveform in this tool.
- Audio export formats: **WAV, M4A, MP3**. AIFF is not offered.
- Video export: best available source resolution, MP4.
- The UI states plainly that streaming sources are lossy: exporting to WAV does not
  restore quality, and such files may fail the 19 kHz check.
- Destination folder is remembered between exports.
- File name field is prefilled with the source title and fully selected; the extension is appended automatically.
- Progress bar 0–100 % in green, with the live yt-dlp/FFmpeg log line in italics; completion check mark over a blurred background.
- Bundled tools: FFmpeg 9.0.1 "essentials" build from gyan.dev (a GPL build, shipped as a
  separate executable with its licence text and a link to its source) and yt-dlp (shipped
  with an in-app "update yt-dlp" action, because site changes break old versions).

## 7. Snippets (port of `GerarSnippets.ps1`)

Behaviour to preserve:

- For each track without a snippet, cut `duration` seconds starting at `start` and save an MP3
  into a `Snippets` subfolder next to the track. Defaults: start 90 s, duration 120 s, 192 kbps.
- Output name: `<track name> [Snippets].mp3`.
- Never modifies, moves or deletes originals.
- Skips tracks that already have a snippet, matching any of the suffixes
  `[Snippets]`, `[Snippet]`, `[PREVIEW]`, `[Preview]`, `[PREV]` in any of the folders
  `Snippets`, `Snippet`, `Preview`, `Previews`, `PREVIEW`.
- Never treats those folders, or folders with `BACKUP` in the name, as sources.
- Source extensions: wav, mp3, aiff, aif, flac, m4a, ogg, wma, alac.
- Options: recursive, dry run, one folder / all saved folders / another folder.
- A result under 10 KB counts as a failure (track shorter than the cut point) and is removed.
- The saved folder list (`pastas.txt` today) becomes an editable list in the app.

## 8. About

Built-in user guide in English: what each tool does, how the 20 kHz check works and
its known limits, shortcuts, bundled third-party software and licences (Inter font,
FFmpeg, yt-dlp, SQLite).

## 9. Out of scope

- Section 2 of the Master Spec (AAS skills, agent orchestration, token strategy) describes
  how an AI agent is configured. It does not affect the product and is not implemented.

## 10. Build order

1. Shell: English resources, light/dark themes, acrylic, navigation with the four tools.
2. Analyzer screen on the new shell, with context menu and mini player.
3. Download & Trim.
4. Snippets.
5. About and Settings.
6. Single-file spectrogram and waveform.
7. Installer and GitHub release.

## 11. Installation (decided 2026-11-10)

- The owner must be able to install to **any folder on any drive** (G:, K:, Y: ...).
  MSIX always installs to a fixed system location, so MSIX is dropped.
- The app is built **unpackaged and self-contained** (`WindowsPackageType=None`,
  `WindowsAppSDKSelfContained=true`, .NET self-contained). Nothing has to be installed first.
- Two deliverables per release, both x64:
  1. **Portable zip** - extract anywhere and run `kHzAnalyzer.exe`.
  2. **Setup program** (Inno Setup) with a "choose destination folder" page, Start menu
     and optional desktop shortcut, and an uninstaller. Per-user by default, so installing
     to a folder the user owns needs no administrator rights.
- User data never lives in the install folder: settings and logs in
  `%LOCALAPPDATA%\kHzAnalyzer`, analysis cache in `%APPDATA%\kHzAnalyzer`.
