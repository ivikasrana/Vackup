# Vackup

[![License: Dual](https://img.shields.io/badge/license-MIT%20%2B%20Commercial-blue.svg)](LICENSE)

Vackup is a manual folder backup tool for Windows. Pick the folders to protect, choose a destination drive or network share, and start the backup with live progress.

## Features

- Back up multiple source folders in one run.
- Destination can be a local or external drive (`E:\Backups`) or a network share (`\\server\share`).
- Skip unchanged files to make repeat backups fast.
- Optional dated backup folder for each run.
- Speed limit with disk speed detection, so a backup does not saturate the drive or network.
- Progress with percentage, bytes copied, current file, and copied, skipped and failed counts.
- Cancel a running backup at any time.
- Light and dark themes.
- Folders, destination and options are remembered between sessions in `%APPDATA%\Vackup\settings.json`.

## Technology

- C# with WPF on .NET Framework 4.8
- Solution file: `Vackup.slnx`

## Project layout

- `MainWindow.xaml` / `.cs` - main window and backup workflow
- `AddFolderDialog.xaml` / `.cs` - folder picker dialog
- `BackupService.cs` - copy engine, progress reporting and throttling
- `DiskSpeed.cs` - disk speed detection
- `Settings.cs` - persisted settings
- `ThemeManager.cs` and `Themes/` - light and dark themes
- `assets/Vackup.ico` - application icon, rendered by `tools/IconGen`

## Build

    dotnet build Vackup.slnx -c Release

## License

Dual-licensed, see [LICENSE](LICENSE).

- **MIT License:** free for personal, educational, and non-commercial use.
- **Commercial License:** required for business or commercial use. Contact
  [vikasrulez@gmail.com](mailto:vikasrulez@gmail.com).

## Release

Current version: 0.1.0. See CHANGELOG.md.
