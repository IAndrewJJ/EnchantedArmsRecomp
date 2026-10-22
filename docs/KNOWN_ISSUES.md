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
