# Vackup Code Review, Refactor and Performance Report

Scope: all C# sources (`BackupService.cs`, `MainWindow.xaml.cs`, `AddFolderDialog.xaml.cs`, `Settings.cs`, `DiskSpeed.cs`, `ThemeManager.cs`).

**Verification status:** not compiled or run. The review environment has no .NET SDK, and the app is WPF on net48 (Windows only). Build and smoke-test on Windows before merging. Code was kept to C# 7.0 to match `LangVersion`.

## 1. Bugs found and fixed

| # | Area | Problem | Fix |
|---|------|---------|-----|
| 1 | `BackupService` | Sources with the same folder name (`C:\a\Docs`, `D:\Docs`) merged into one target folder, mixing files. | `UniqueName` gives `Docs`, `Docs (2)`, ... |
| 2 | `BackupService` | Junctions/symlinks could loop back into the tree (e.g. `Application Data`), causing endless recursion. | Reparse-point directories are skipped during the scan. |
| 3 | `BackupService` | `CreateEmptyDirectories` used `GetDirectories(AllDirectories)`, which throws on the first unreadable folder and aborts folder creation for the whole source. | Directories are collected during the single scan; unreadable folders are reported only. |
| 4 | `BackupService` | A failed or cancelled copy left a truncated file that a later "skip unchanged" run could trust. | Partial target is deleted on any exception. |
| 5 | `Throttle` | After many skipped files, elapsed time banked credit, allowing an unthrottled burst. | Window resets when more than 1 s behind. Wait is also clamped to `int` range. |
| 6 | `MainWindow` | `Path.GetFullPath` on an invalid destination threw outside the `try` in an `async void` handler, crashing the app. | Validation moved into a `try/catch` with an error dialog. |
| 7 | `MainWindow` | Closing the window mid-backup did not cancel the copy. | `Closing` now cancels the token. |
| 8 | `MainWindow` | Dead code: `if (Slider.Value > max)` can never be true, since WPF clamps on `Maximum`. | Removed. |
| 9 | `Settings` | Non-atomic save could corrupt `settings.json` on crash. | Write to `.tmp`, then `File.Replace`/`Move`. |
| 10 | `AddFolderDialog` | Pasted paths were not normalised, so duplicates differing by trailing `\` or case slipped through. | `NormalizePath` plus case-insensitive `Distinct`. |

## 2. Refactoring

- Duplicate folder-picker code in `MainWindow` and `AddFolderDialog` consolidated into `FolderPicker.cs`; the unused `System.Windows.Forms` usings were removed.
- The scan now produces a `FileJob { Source, Target, Size }` record instead of key/value string pairs.
- `CopyFile`, `IsUnchanged` and `ReportFile` take the job record or explicit parameters, and `Scan` and `UniqueName` were extracted from the long `Run` method.

## 3. Performance

- **Single directory walk:** it previously ran twice (file scan plus empty-folder pass).
- **Single size read:** sizes come from `DirectoryInfo.GetFiles()` during the scan. Before, `FileInfo.Length` was read again for every file in the scan, again in the copy loop, and again in `IsUnchanged`.
- **UI progress rate-limited:** per-file reports are limited to about one every 100 ms, with the last file always reported. Previously every file posted to the dispatcher, which flooded the UI queue on large trees. Errors are still reported immediately.
- **Copy I/O:** 256 KB buffers (was 128 KB) with `FileOptions.SequentialScan`, and a `Stopwatch` replaces repeated `DateTime.UtcNow` calls.

## 4. Known remaining items (not changed)

- Speed and ETA are computed over total elapsed time, which includes scan time and skipped bytes, so the early estimate is skewed.
- Paths over 260 characters still fail on net48 without long-path opt-in.
- Starting a backup while a speed test is running is not blocked, so the two can overlap.
- Files are copied serially. Parallel copy would help many-small-file workloads but conflicts with the single speed limiter.
- No automated tests exist. The scan, naming and throttle logic are good candidates for unit tests.
