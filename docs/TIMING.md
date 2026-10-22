# High FPS timing development

The original engine uses 30 Hz frame units. Removing the console throttle alone
accelerates movement and effects. The timing candidate keeps rendering at the
selected rate and converts elapsed host time to engine steps:

`engine step = elapsed seconds × 30`

At a steady 60 FPS, each update receives half an original frame. Missed deadlines
use their measured elapsed interval, with bounds for long stalls and loading.
The host timebase, audio clock, texture coordinates and texture wrap periods are
not rescaled.

## Status

The corrections introduced in 0.4.2 / native 0.3.3 remain enabled in native
0.3.4. The player reports normal character movement, menu background and
runic-wall animation, followed by about three hours of steady upscaled 60 FPS
gameplay with few first-use effect dips and no visual issues reported in that
span. Full campaign timing remains under test.

Launcher 0.5.1 offers only **30 FPS · Stable** and **60 FPS · Stable**. Default
preferences and old Original selections use 30; existing 60 FPS selections are
preserved and retired higher/unlimited selections become 60. Higher developer
caps remain outside the launcher and are not player release options.

## Integration

`src/timing_correction.cpp` retains the original translated functions. Targeted
instruction hooks are declared in `config/timing_hooks.toml`; code generation
inserts calls without distributing or manually editing translated game code.
Setup verifies the supported XEX before using these revision-specific addresses.

| Boundary | Correction |
| --- | --- |
| Exploration scene update | Replace its local fixed-step value after loading it. |
| Interface dispatcher | Correct its fixed-step entry; retain the entry accepting a supplied step. |
| Animated menu scene | Correct the background model's independent fixed-step value. |
| Texture UV scrolling | Scale the velocity before its vector addition and normal wrapping. |
| Alternate event and script entries | Scale supplied frame units, avoiding repeated scaling of forwarded arguments. |

Nested update scopes prevent applying the correction twice. The countdown leaf
preserves its incoming argument register because callers reuse it for a sibling
update. Shared constants such as `1.0` also serve geometry and remain unchanged.

`ea_timing_correction` defaults to enabled and has no effect in Original/30 FPS.
It can be disabled for a developer comparison. `ea_trace_timing` defaults to off
and enables bounded, local measurements of corrected steps and
active virtual targets. These measurements establish which paths execute and
their step rates; they do not establish visual correctness by themselves.

## Checks

`tools/graphics-probe/timing.cpp` runs without a game or renderer. It checks equal
elapsed game time across caps, missed deadlines, pause/stall bounds, nested
scaling, UV scroll speed and unchanged Original-mode UV increments. Compile it
with a C++17 or newer compiler.

For a game comparison, use a separate installation and copied saves. Compare the
same walk, camera rotation, menu background and animated surface against Original.
Check battles and scripted events separately. F6 counts swap submissions; it does
not prove normal simulation speed, distinct animation poses or monitor refresh.
