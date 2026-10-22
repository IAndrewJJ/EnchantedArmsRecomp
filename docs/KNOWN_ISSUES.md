# Known issues

## EA-001 — The Diet Doctor model is invisible in the opening cafeteria

Status: reported by the first tester on October 21, 2026; investigating. No fix or workaround confirmed.

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

## EA-002 — Smiling Salesgirl model is invisible at the festival

Status: reported by the first tester on October 21, 2026; investigating. No fix or workaround confirmed.

The NPC named **Smiling Salesgirl** is invisible at the festival. Whether her shadow and interaction remain functional, and whether the issue persists after reloading at 1×, have not yet been established. A shared cause with EA-001 is possible but unconfirmed. Keep this location as a second reproduction case when testing character rendering.

## EA-003 — Dark or black patches on multiple NPC models

Status: reported October 22, 2026; investigating. No fix confirmed.

The tester reports that parts of multiple NPC character models appear black where another color is expected, especially **Toya and Makoto** in the opening sequence and some NPCs at the festival. Whether patches move with the camera is not yet established. This symptom is distinct from the two invisible NPC reports; a shared renderer cause is unconfirmed. The current installation records Original 1x scaling, game filtering, zero texture-detail bias and original frame pacing, so the initial investigation must include a baseline renderer comparison rather than assuming the new detail/FPS settings caused it.

## EA-004 — Experimental FPS modes can accelerate gameplay

Use **Original** for normal play. [Upstream Enchanted Arms testing](https://github.com/xenia-canary/game-compatibility/issues/2) reports that game speed depends on FPS and marks the title `vsync-off-speedup`. Our explicit FPS limits disable that throttle, so they must be treated as timing experiments. A host frame limiter does not decouple animation or simulation from frame rate. Normal-speed 60/120+ FPS needs a verified engine timing patch; that patch is not implemented. Original remains the default. Movies may retain their source cadence even when gameplay is accelerated.
