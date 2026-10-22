# Changelog

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
