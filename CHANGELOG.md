# Changelog

## 0.5.6 — Tidier installation and save locations

- Keep the recommended build beside the launcher, with a visible Saves folder.
  Add a Save location picker plus links to restore the default locations.
- Accept existing project folders and resolve an extracted download's outer
  wrapper to the project inside. Preserve unrelated files, destination saves
  and settings when preparing or retrying a build.
- Move legacy saves automatically; copy progress safely when choosing another
  save folder, retaining originals and rejecting conflicting progress.
- Keep screenshots independent of save locations in native build 0.3.6. Existing
  players rebuild from their ISO after the launcher update; gameplay fixes remain.
- Exclude visible saves from source control, update payloads and release kits.

## 0.5.5 — Choose your build folder

- Add a Build location field and folder picker directly below Choose ISO.
- Build the complete portable game in the selected folder, keeping its launcher,
  game data, tools and saves together. Reopen the launcher there after successful
  setup, without starting the game. Existing matching desktop shortcuts follow it.
- Copy existing saves and texture packs into a new build folder while retaining
  the originals. Preserve destination progress on retries and reject unrelated
  nonempty folders and linked paths.
- Keep existing installations in place unless another location is chosen.
  Native build 0.3.5 is unchanged; no rebuild is needed to update the launcher.

## 0.5.4 — Simpler launcher settings

- Remove graphics presets and the Advanced tab. Keep individual picture controls
  and existing saved preferences.
- Label display synchronization as V-Sync with clear On and Off choices.
- Refine spacing, headings and navigation; simplify texture and update descriptions.
  Dim window-size controls when using borderless fullscreen.
- Keep native build 0.3.5, saves and game settings. No game rebuild is required
  when updating from 0.5.3.

## 0.5.3 — Opening battle fix

- Fix the Game Over / Retry loop after Ice Giga Zone in the opening Queen of
  Ice fight. Keep Atsuma, Toya and Makoto at 1 HP after otherwise lethal attacks
  in that encounter, allowing the original story sequence to continue.
- Require the updated native build at both 30 and 60 FPS. Existing saves and
  settings are kept when rebuilding from the ISO.
- Confirm opening-story progression at 60 FPS in a player test, with runtime
  logs verifying 1 HP for all three characters. Add regression checks for
  encounter scope, party membership and damage accounting. Later fights retain
  their normal rules; full campaign validation continues.

## 0.5.2

- Fix the fresh-install SDK download path using a checksum-pinned package built
  from the matching public source, with only the runtime configuration needed.
- Identify Windows security blocks hidden by Python's `Invalid argument` error.
- Add a compile/link/load check to fresh-toolchain validation. Existing native
  0.3.4 games, settings and saves are unchanged.

## 0.5.1 — Confirmed uninstall

- Simplify launcher FPS choices to 30 FPS · Stable and 60 FPS · Stable. Default to 30; preserve 60 FPS preferences and migrate retired higher/unlimited caps to 60.
- Embed the approved original gauntlet/crystal application icon, with ten Windows icon sizes. Launcher windows and generated desktop shortcuts use it automatically.

- Add Settings → Game → Uninstall EnchantedArmsPC with an explicit folder/save-deletion popup and Cancel selected by default.
- Remove the current installation and matching standard shortcuts after the launcher closes; clean up the temporary helper afterward.
- Refuse protected/shared roots, changed directory identities and active game/launcher processes. Never traverse linked external folders or scan for external ISOs/other installations.
- Test deletion boundaries, external links, long/read-only paths, shortcut matching and helper cleanup with disposable fixtures. Native build 0.3.4 and renderer 0.3.0 are unchanged; no game rebuild is required.

## 0.5.0 — Controls, texture packs and update recovery

- Add keyboard/controller remapping, trigger swaps, stick dead zones, camera response/inversion and mouse controls. Assign reconnected pads to guest player 0.
- Add display-aware supersampling with FXAA, an optional F6 frame-time graph and shader worker controls. Retain existing timing corrections and shader caches.
- Install/select local preconverted DDS/XPR2 texture packs through separate NTFS game overlays; keep original disc files intact. No enhanced texture assets are bundled.
- Add verified GitHub update downloads, transactional file replacement, interrupted-update recovery and rollback preserving private saves/settings/native builds/cache.
- Extend portable input, launcher settings, archive validation, update recovery and texture-pack tests. Add a local frame-log analysis tool without report/log buttons.
- Native integration is 0.3.4; renderer remains 0.3.0. Opening NPC rendering defects, full campaign timing and higher FPS validation remain open. See [release details](docs/RELEASE-0.5.0.md).

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
