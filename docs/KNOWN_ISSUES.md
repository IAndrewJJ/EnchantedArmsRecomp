# Known issues

## EA-001 — Invisible NPC models in the opening cafeteria

Status: first reported October 21, 2026; reproduced in a new playthrough on October 22. Investigating; no fix or workaround confirmed.

Observed behavior:

- In the cafeteria at the beginning of the game, the NPC named **The Diet Doctor** has no visible character model.
- Its shadow remains visible.
- Pressing Xbox A still starts interaction with that NPC.
- The tester reports that the other nearby NPCs are visible.

Expected behavior: the NPC's character model is visible along with its shadow and interaction prompt.

Reproduction: load an early-game cafeteria save, approach The Diet Doctor, inspect the model and shadow, and press A. The report does not yet establish whether reloading, viewing from another angle, or changing internal resolution affects the result.

Environment at report: Windows x64, ReXGlue SDK 0.10.0, Direct3D 12, AMD Radeon RX 6700 XT, supported USA/Europe executable. The launcher recorded Enhanced (2×) selected, but the running session started before the fix for late-registered renderer arguments; the effective scale of that session is unconfirmed. A fresh Original (1×) comparison is still needed.

Triage: interaction and shadow rendering remain functional, which narrows the symptoms to the NPC's visible model or its draw path; this does not identify a root cause. The session contains repeated invalid texture-fetch warnings earlier in gameplay. Their connection to this NPC is unproven. Do not treat suppressing those warnings, replacing the model, or changing a global renderer setting as a verified fix.

Local logs, build identity and a copy of the available save files are retained privately for investigation. They are excluded from the public launcher kit.

October 22 retest: the player started a new playthrough using launcher 0.4.1 / native build 0.3.2 and reports a steady 30 FPS with Original FPS selected. **The Diet Doctor** and **Flirtatious TFC Member 063** are confirmed invisible in the opening cafeteria; shadows remain visible and interaction works. More affected NPCs are suspected but are not yet confirmed. This expands the reproduction targets without establishing that both NPCs share one underlying defect.

The launch receipt records 2× internal scale, 1920×1080 output, CAS at 100% sharpness, FXAA strong, 16× AF, Crisp texture detail, Automatic rendering, background shader compilation enabled and no resolve readback. These are selected settings; the current session's effective renderer flags have not yet been read from its buffered log. A new playthrough does not imply a cleared shader cache. The steady FPS report establishes the observed cadence in this scene, not the cause of the missing models.

## EA-002 — Smiling Salesgirl model is invisible at the festival

Status: reported by the first tester on October 21, 2026; investigating. No fix or workaround confirmed.

The NPC named **Smiling Salesgirl** is invisible at the festival. Whether her shadow and interaction remain functional, and whether the issue persists after reloading at 1×, have not yet been established. A shared cause with EA-001 is possible but unconfirmed. Keep this location as a second reproduction case when testing character rendering.

## EA-003 — Dark or black patches on multiple NPC models

Status: reported October 22, 2026; investigating. No fix confirmed.

The tester reports that parts of multiple NPC character models appear black where another color is expected, especially **Toya and Makoto** in the opening sequence and some NPCs at the festival. Whether patches move with the camera is not yet established. This symptom is distinct from the two invisible NPC reports; a shared renderer cause is unconfirmed. The current installation records Original 1x scaling, game filtering, zero texture-detail bias and original frame pacing, so the initial investigation must include a baseline renderer comparison rather than assuming the new detail/FPS settings caused it.

## EA-004 — Experimental FPS modes can accelerate gameplay

Current launcher 0.5.1 offers only **30 FPS · Stable** and **60 FPS · Stable**.
The player now reports about three hours of steady upscaled 60 FPS gameplay with
few first-use ability/effect dips and no visual issues in that span. Higher caps
have been removed from the launcher. Full campaign timing validation continues.
The history below describes the original defect and earlier testing.

Status: **60 FPS preview, partially validated in 0.4.2 / native 0.3.3**. Earlier builds only removed the console throttle and could accelerate gameplay, matching [upstream reports](https://github.com/xenia-canary/game-compatibility/issues/2). The new build corrects exploration/interface/menu steps and independent texture UV scrolling. Player testing in the opening area reports normal character movement, the menu background and runic-wall animation at 60 FPS, followed by an observation of no speedup or very little.

This is not campaign-wide validation. Battles, scripted sequences, transitions and higher rates remain unverified. Original remains the default and fallback if timing looks wrong. Movies retain their source cadence. Existing players must rebuild their native game; a launcher-only update cannot fix the old executable. See [timing details](TIMING.md).

## EA-005 — Launcher process runs without a visible window

Status: reproduced and fixed in 0.3.2. The launcher started on a build/sandbox Windows desktop. Session-scoped instance events forwarded later interactive launches to that hidden instance, which acknowledged them without displaying a window on the user's desktop. Version 0.3.2 includes the window station and desktop in instance identity. The updated launcher was opened and visually verified on the interactive desktop with Play enabled; a second launch restored that same window and left the game launch record unchanged. The game still starts only from Play. Versions 0.3.1 and older should be updated if affected.
