# Playtesting preview 0.3.1

Confirmed by the first tester: New Game, opening movies, rendered battle, character control, normal audio, the transition into exploration, character models, Y menus, save creation and save loading. Only the early game has been exercised. Mild stutter remains; a full campaign playthrough is still needed.

Reported defects are tracked in [known issues](KNOWN_ISSUES.md): EA-001 (The Diet Doctor, cafeteria) and EA-002 (Smiling Salesgirl, festival). Neither has a confirmed fix. The original early gameplay results predate the enhanced graphics build.

## Suggested first session

1. Start with Original (1×), create a new save slot, then close and reload it.
2. Test keyboard controls or your controller and record which input device you used.
   Press F6 to show the FPS/frame-interval counter; verify that pressing it again hides it and holding it does not flicker. Its game-swap rate can differ from monitor refresh or the selected FPS cap.
3. Play through exploration, battles, cutscenes and area transitions. Record the last working save/location and the exact action before any failure.
4. Repeat a stuttering section once after restarting. Note whether it improves after shaders have been cached.
5. Try Balanced (FSR 1 / 1×), then Enhanced (FSR 1 / 2×) in the same scene. Compare image quality and stutter. High (3×) and Ultra (4×) use nine and sixteen times the native pixel count and need more GPU time/memory. Return to Original if rendering deteriorates.
6. Export a playtest report in the launcher. Review its path-redacted text logs before attaching it to a GitHub issue. Add GPU/driver, preset, location and reproduction steps. The export contains no saves, disc files or dumps.

## Current limits

- Windows x64 and Direct3D 12 only; initial testing used an AMD Radeon RX 6700 XT. Other GPU vendors and controllers need coverage.
- Only XEX SHA-256 `79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22` is supported. Other regional or patched executables need separate analysis.
- Higher resolution uses existing renderer scaling. Source textures and pre-rendered movies keep their source detail; no texture replacement or AI upscaling is included.
- Spatial FSR/CAS pipeline initialization passed on the RX 6700 XT, with actual resolution, AF, FXAA and sharpening flags verified. That check does not establish correct rendering throughout the game. Test both missing NPC locations after restarting with Original and an enhanced preset.
- Experimental FPS choices remove the console throttle and limit submissions at the original D3D swap boundary. Correct movement, battle, animation and movie/audio timing at higher rates is unverified. Movies retain their source cadence. Test Original against 60 first, using a separate save slot, before trying higher targets. The selected limit is not a promise of achieved FPS.
- Texture detail favors sharper existing mipmaps on eligible filtered surfaces. It is not a higher-resolution texture replacement pack; Strong can introduce shimmer. Compare Original/Crisp/Strong in motion.
- Shader stutter fixes, content mods, achievements and later-game compatibility remain future work.
- A clean-machine installation still needs testing. Windows security blocked reads of the pinned SDK archive on the development PC. The existing supported SDK installation can be reused. Setup stops if required downloads or executables are inaccessible; do not change security settings to force setup through.

Keep the entire installed folder when updating; `.local/runtime/user` holds saves and `.local/runtime/cache` holds shader caches. Back up the user folder before replacing an installation. New releases should be unpacked over the project sources, without deleting `.local`.

Close the game before updating or pressing **Back up saves**. To share a visual configuration use **Graphics settings → Export profile**. Playtest report exports contain the last launch settings separately from the next-launch selection. Include the build, GPU/driver, location, input device and reproduction steps in issues. Avoid attaching game data or private save backups to public issues.
