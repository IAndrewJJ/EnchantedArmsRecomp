# EnchantedArmsPC 0.3.1 — F6 FPS counter

Press **F6** during play to show or hide a compact FPS and frame-interval counter in the top-right corner. The counter starts hidden, updates every half second, and does not capture game input. Holding F6 toggles once instead of flickering. Modified shortcuts such as Alt+F6 keep their normal behavior.

The launcher now always opens first. Click **Play Enchanted Arms** to boot the game; old `--play` shortcuts also open only the launcher. Reopening an existing instance restores its window and confirms receipt of the request. Launcher closure exits its process cleanly, and `.local/logs/launcher.log` records activation/startup/exit diagnostics. This addresses a reported invisible launcher instance blocking later launch requests.

The counter measures the game's swap submissions, independently of UI repaint frequency and the selected FPS limit. Its milliseconds value is the average frame interval, not GPU execution time. It displays zero when no new game frames arrive and resets its sample when reopened.

**Download EnchantedArmsPC-0.3.1-windows-x64.zip under Assets**, extract the whole ZIP, then run **EnchantedArmsLauncher.exe**. Each player supplies their own supported ISO. Existing users should close the game and launcher, back up saves, unpack over their project folder preserving `.local`, and choose **Build from ISO** once. The native update stages separately in `bin/graphics-0.3.1`.

Validation: native compilation; counter tests for 30/120 FPS, half-second sampling, idle/reset behavior and game/UI frame independence; F6 repeat/modifier filtering; ImGui rendering and input-capture checks; user confirmation that F6 shows/hides in game; launcher settings and synthetic setup tests; repeated launcher activation with legacy `--play` verified to leave the game launch record unchanged. Full campaign coverage remains incomplete. Invisible NPCs and newly reported dark surfaces on Toya, Makoto and other NPCs remain under investigation.

**Use Original FPS for normal play.** [Upstream Enchanted Arms testing](https://github.com/xenia-canary/game-compatibility/issues/2) reports game speed tied to FPS. The experimental limits can accelerate gameplay; normal-speed 60/120+ FPS requires an engine timing patch that is not implemented. The counter and host limiter are working, but that does not establish correct simulation timing.

The game-independent renderer is unchanged from 0.3.0. **ReXGlueSpatial-0.3.0-source.tar.gz** remains its corresponding source archive. The full Windows kit includes the renderer and checksum-pinned Python bootstrap. No game assets, translated game code, compiled game executable or saves are distributed.
