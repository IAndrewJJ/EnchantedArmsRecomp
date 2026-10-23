# Playtesting preview 0.5.1

Confirmed by the first tester: New Game, opening movies, rendered battle, character control, normal audio, the transition into exploration, character models, Y menus, save creation and save loading. The latest report covers about three hours of steady upscaled 60 FPS gameplay with few dips on first use of abilities/effects and no visual issues reported in that span. A full campaign playthrough is still needed; this does not establish a fix for the original missing-NPC reports.

Reported defects are tracked in [known issues](KNOWN_ISSUES.md): EA-001 (The Diet Doctor, cafeteria), EA-002 (Smiling Salesgirl, festival), EA-003 (dark patches on Toya, Makoto and other NPCs) and EA-004 (experimental FPS can accelerate gameplay). No NPC fix is confirmed. The original early gameplay results predate the enhanced graphics build.

## Suggested first session

1. Start with Original (1×), create a new save slot, then close and reload it.
2. Test keyboard controls or your controller and record which input device you used.
   Press F6 to show the FPS/frame-interval counter; verify that pressing it again hides it and holding it does not flicker. Its game-swap rate can differ from monitor refresh or the selected FPS cap.
3. Play through exploration, battles, cutscenes and area transitions. Record the last working save/location and the exact action before any failure.
4. Repeat a stuttering section once after restarting. Note whether it improves after shaders have been cached. Five-second log summaries record mean, p50/p95/p99 and maximum frame intervals plus counts over 50 ms, including pacing. Compare the same route, resolution and Original FPS mode; these are not GPU timings. Exclude loading screens and F8 captures from performance comparisons.
5. Try Balanced (FSR 1 / 1×), then Enhanced (FSR 1 / 2×) in the same scene. Compare image quality and stutter. High (3×) and Ultra (4×) use nine and sixteen times the native pixel count and need more GPU time/memory. Return to Original if rendering deteriorates.
6. Report the launcher/native version, GPU/driver, settings, location and reproduction steps using the GitHub bug form. Optional log excerpts are under `.local/runtime` and `.local/logs`; remove personal paths before sharing. F8 images are under `.local/runtime/captures`. The simplified launcher has no report-export controls.

## Current limits

- Windows x64 and Direct3D 12 only; initial testing used an AMD Radeon RX 6700 XT. Other GPU vendors and controllers need coverage.
- Only XEX SHA-256 `79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22` is supported. Other regional or patched executables need separate analysis.
- Higher resolution uses renderer scaling. Source textures and pre-rendered movies keep their source detail; no enhanced texture artwork is bundled. Local DDS/XPR pack loading is available under Settings → Textures.
- Spatial FSR/CAS pipeline initialization passed on the RX 6700 XT, with actual resolution, AF, FXAA and sharpening flags verified. That check does not establish correct rendering throughout the game. Test both missing NPC locations after restarting with original-resolution settings and with upscaling enabled.
- Use 30 FPS as the timing baseline. Launcher 0.5.1 offers only 30 FPS · Stable and 60 FPS · Stable. The existing engine/UV corrections remain active at 60; test battles, scripts and later transitions separately and report residual speedup or slowdown. Movies retain their source cadence, and the selected cap is not a promise of achieved FPS. Use a separate save slot for timing tests. See [implementation details](TIMING.md).
- Texture detail favors sharper existing mipmaps on eligible filtered surfaces. It is not a higher-resolution texture replacement pack; Strong can introduce shimmer. Compare Original/Crisp/Strong in motion.
- For dark/missing NPCs, start with Original at 1× and compare ROV alone, then Automatic with background compilation off, then readback separately as described in the [rendering comparison guide](RELEASE-0.3.2.md). Restart between changes. F8 saves guest output before presenter effects/overlays to `.local/runtime/captures`. Immediate readback may cause heavy stalls and is not a general performance setting.
- Verified shader stutter fixes, content mods, achievements and later-game compatibility remain future work.
- Independent-PC installation still needs broader testing. The reported SDK `Invalid argument` failure is covered by bounded file-operation retries, verified partial-download recovery and detailed errors in 0.4.1. A fresh SDK download/extraction/version check passes locally, and CI now repeats it on Windows. The original summary did not establish the cause; see [setup help](SETUP_TROUBLESHOOTING.md) if it recurs.

Keep the entire installed folder when updating. From 0.5.6, saves default to the visible `Saves` folder inside the game folder. Older `.local/runtime/user` saves are moved there automatically. You can choose another save folder in **Settings → Game → Save location** with the game closed; current progress is copied without replacing different saves. Back up the entire selected save folder. `.local/runtime/cache` holds shader caches; keep `.local` when updating.

Close the game before updating or copying a private save backup. Include the build, GPU/driver, location, input device and reproduction steps in issues. Avoid attaching game data, translated code, shader caches, dumps or private save backups to public issues.
