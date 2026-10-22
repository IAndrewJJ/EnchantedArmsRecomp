# Changelog

## 0.4.2 — Normal-speed 60 FPS preview

- Convert elapsed frame time into the engine's 30 Hz units for exploration, interface, menu scenes and alternate event/script updates. Keep Original timing as the default.
- Correct texture UV scroll velocity separately, including animated menu backgrounds and surface effects such as the opening area's runic wall.
- Preserve forwarded arguments and avoid applying the correction twice. Bound loading/pause catch-up without changing the audio or host clocks.
- Enable corrections automatically in native build 0.3.3. Label 60 FPS as Preview; require an updated native build for high FPS instead of silently using a legacy executable.
- Add portable timing/UV regression checks and a CI job without game data. Player testing reports normal movement, menu background and runic-wall animation at 60 FPS in the opening area; campaign timing and higher rates remain unverified.
- Renderer remains at 0.3.0. Existing players must rebuild their native game to receive timing fixes; saves and caches are preserved.

## 0.4.1 — Setup recovery for external playtesters

- Retry temporary file-access failures, including `Invalid argument` while finalizing a verified SDK download, and interrupted connections with bounded delays.
- Recover completed, checksum-verified `.partial` downloads on retry; reject incomplete/tampered files and retain existing cached files until a verified replacement is ready.
- Prepare tools before extracting the game, check SDK headers/libraries and executable version, and repair incomplete SDK installations rather than trusting one executable.
- Handle deep Windows SDK archive paths without requiring administrator access or a system long-path setting change.
- Record the failing operation, OS error codes and full exception chain; show the local setup-log location in the launcher's error dialog.
- Explain missing SDK runtime DLL failures and document the Microsoft Visual C++ x64 runtime requirement.
- Add regression tests, a fresh Windows toolchain installation CI check, setup guidance and a dedicated setup issue form.
- Native integration and renderer remain at 0.3.2 and 0.3.0. Existing working installations do not need a native rebuild.

## 0.4.0 — Launcher redesign and maintenance

- New Enchanted Arms inspired interface with original vector artwork, a Play button and grouped Settings pages.
- Removed report, log, capture-folder and save-management buttons; local saves, captures and diagnostics remain available in the installation folder.
- Cached backdrop rendering, event-driven game-exit monitoring, buffered setup output and bounded setup logs. No campaign-wide FPS gain is claimed.
- Fixed settings-save error handling, cancellation failure detection and launch receipts written before process creation succeeded.
- Centralized launcher/native release versions, refreshed player/contributor documentation and added archive integrity/exclusion tests.
- Native game build and renderer remain at 0.3.2 and 0.3.0 respectively. Existing 0.3.2 players do not need a native rebuild for this launcher update.

## Earlier previews

- [0.3.2](docs/RELEASE-0.3.2.md): Windows desktop activation fix, F8 capture, frame-interval diagnostics and renderer comparisons.
- [0.3.1](docs/RELEASE-0.3.1.md): F6 FPS counter and launcher-first startup.
- [0.3.0](docs/RELEASE-0.3.0.md): mip detail and experimental FPS limits.
- [0.2.0](docs/RELEASE-0.2.0.md): spatial FSR 1/CAS, render scaling, filtering and presentation settings.

Historical release notes describe the interface available at that version. See the README for current navigation.
