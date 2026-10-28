# Changelog

## Unreleased

- Fix: sources with the same folder name no longer merge into one backup folder.
- Fix: junctions and symlinks are skipped, avoiding endless recursion.
- Fix: one unreadable subfolder no longer aborts creation of the other folders.
- Fix: partial files are deleted after a failed or cancelled copy.
- Fix: invalid destination paths show an error instead of crashing.
- Fix: closing the window cancels a running backup.
- Fix: the speed limit no longer allows an unthrottled burst after skipped files.
- Settings are saved atomically.
- Performance: single directory scan (sizes read once), rate-limited UI progress, larger sequential copy buffers.
- Refactor: shared folder picker, normalised and de-duplicated source paths.

## 0.1.0 - 2026-10-14

- Initial release.
- Select folders to back up and a destination.
- Backup service with disk speed measurement.
- Light and dark themes.
- Application icon and dual MIT and commercial license.
