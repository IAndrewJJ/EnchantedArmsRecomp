# Development priorities

The goal is a dependable native PC edition that players can build from their own supported disc image. The opening is playable; full campaign compatibility remains unverified.

## Delivered in the graphics preview

- Local ISO validation/extraction and native compilation, with resumable extraction and pinned tools.
- Launcher-managed spatial FSR 1, CAS, 1–4x internal scale, FXAA, anisotropic filtering, sharpening and display settings.
- Import/export graphics profiles, desktop shortcut, private save backups and local log/report access.
- Pinned SDL controller mappings, renderer source/build instructions and game-data-free public packages.

## Next compatibility milestones

1. Reproduce EA-001 and EA-002 from the same locations with Original and enhanced settings. Capture effective renderer flags and draw diagnostics before changing global behavior.
2. Complete two-machine installation and playtesting, including controller behavior, save/reload, battle transitions, videos and later areas. Record clean-machine toolchain failures separately from game crashes.
3. Measure stutter with repeatable routes and warm shader caches. Separate shader compilation, pipeline creation, guest timing and streaming costs before choosing an optimization. Keep game pacing intact until timing dependencies have been tested.
4. Expand AMD/NVIDIA/Intel coverage and verify graphics presets across gameplay, effects, UI and cutscenes. Adjust defaults using measured performance and image quality.
5. Investigate deeper presentation improvements only after reliable frame delivery. Temporal reconstruction, frame generation, arbitrary frame-rate unlocks, widescreen changes and texture replacement require separate engineering and testing; they are not implemented by exposing existing spatial settings.

Additional creatures, areas and other content mods are deferred until base-game compatibility is established.

## Reference projects

[LostOdysseyRecomp](https://github.com/freefrank/LostOdysseyRecomp) and [SpartanPort / God of War III](https://github.com/FelipePrado0/SpartanPort) are references for approachable setup, useful graphics controls, input support and clear troubleshooting. Their runtime architectures and compatibility work differ. This roadmap uses their player experience as inspiration; their features are not a promise of compatibility or implementation in Enchanted Arms.
