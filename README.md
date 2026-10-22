# EnchantedArmsRecomp / EnchantedArmsPC

**[Download the Windows launcher kit](https://github.com/IAndrewJJ/EnchantedArmsRecomp/releases/tag/v0.3.0)** — choose `EnchantedArmsPC-0.3.0-windows-x64.zip` under Assets. Extract the entire ZIP and open `EnchantedArmsLauncher.exe` inside. The repository file preview shows source code; the ready-to-run launcher is included in the release ZIP.

An early Windows x64 native recompilation of Enchanted Arms for Xbox 360, built with ReXGlue SDK 0.10.0. The public launcher kit takes your own supported ISO, extracts it locally, generates native C++ and compiles the PC executable. Game files and translated game code are never uploaded by the launcher.

The first tester reached exploration, used the character menus, saw character models, heard normal audio, and created and loaded a save. Mild stutter remains. Full campaign compatibility is still being tested. See [playtesting status](docs/PLAYTESTING.md).

## Install and play

1. Unpack the entire Windows launcher kit into a writable local folder, such as a folder under Documents. Keep all the included files together. Windows 10/11 x64, .NET Framework 4.8 and a Direct3D 12 GPU are required.
2. Open `EnchantedArmsLauncher.exe`.
3. Choose your Enchanted Arms Xbox 360 ISO and press **Build from ISO**. Setup checks the executable revision before extraction, verifies the extracted files, downloads approximately 800 MB of pinned tools and compiles locally. Allow roughly 20 GB of free space in addition to your ISO. Compilation can take several minutes; the launcher shows its output.
4. Choose a visual preset or open **Graphics settings**, then **Play Enchanted Arms**. Start with Original (1x) to establish a baseline. Settings apply at the next launch.
5. Press **Create desktop shortcut** to create **EnchantedArmsPC** on your Windows desktop. That shortcut starts the game using the visual settings you last saved in the launcher. Keep the installation folder in place; recreate the shortcut if you move it.

Only the USA/Europe Xbox 360 executable is currently supported:

| Field | Required value |
| --- | --- |
| Title / media ID | `555307E6` / `41637F65` |
| XEX version | `0.0.0.1` |
| XEX SHA-256 | `79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22` |

The ISO is opened read-only. A different executable revision is rejected rather than compiled with incompatible function hints. After setup, the extracted game is used at launch and the ISO can remain elsewhere.

The complete Windows kit includes a checksum-verified portable Python bootstrap archive. No separate Python installation is required. Subsequent tool downloads use that runtime.

## Visual settings

The complete **0.3.0 detail/FPS preview** kit bundles a rebuilt, game-independent ReXGlue renderer with spatial FidelityFX shaders enabled. Download the Windows ZIP from [Releases](https://github.com/IAndrewJJ/EnchantedArmsRecomp/releases); GitHub's source ZIP and the CI launcher-only artifact do not include that renderer.

| Preset | Internal scale | Output effect | Post AA | Filtering |
| --- | --- | --- | --- | --- |
| Original | 1x | Bilinear | Game AA | Game requested |
| Balanced | 1x | FSR 1 | FXAA | 8x AF |
| Enhanced | 2x (4x pixels) | FSR 1 | FXAA | 8x AF |
| High | 3x (9x pixels) | CAS | FXAA | 16x AF |
| Ultra | 4x (16x pixels) | CAS | FXAA strong | 16x AF |

**Graphics settings** lets you set each option independently: internal scale, bilinear/FSR 1/CAS, FXAA strength, anisotropic filtering, sharpening, window size, borderless fullscreen, dithering, aspect ratio and variable refresh rate/tearing. Native 2x MSAA applies only when requested by the game. Import/export graphics profiles to share settings without sharing your ISO path.

FSR 1 spatially upscales the rendered image to the output size; when upscaling is unnecessary the presenter uses sharpening. CAS provides contrast-adaptive sharpening. These use the runtime's actual D3D12 shader paths. FSR 2/3, temporal antialiasing and frame generation are not implemented. The GPU initialization probes passed on an RX 6700 XT; the new graphics presets still need scene-by-scene playtesting.

The window size is separate from internal rendering resolution. The launcher sets the renderer's actual resolution-scale flags on each launch. Higher scales demand more GPU time and memory and need additional scene coverage. Pre-rendered videos and source textures retain their original detail. Original FPS mode preserves the console pacing. See the experimental options below.

**Texture detail boost** offers Original, Crisp (-0.5 mip bias) and Strong (-1.0). This favors sharper existing mip levels on eligible linear-filtered, mipmapped surfaces; point-filtered and base-map requests keep their existing sampling. It works alongside AF and CAS. It does not create new texture detail or provide an HD/AI texture pack. Strong can shimmer during movement; return to Original if the image becomes unstable.

**Frame-rate limit** offers Original, 30, 60, 90, 120, 144, 165, 180, 240, 360 and Unlimited. Original preserves the console vblank throttle and is the default for existing/new profiles. All explicit limits and Unlimited are **experimental**: they remove that throttle and apply a host limiter at the game's original D3D swap boundary. The guest timebase, video-mode refresh and audio clock are not rescaled. The complete original swap routine still runs.

These controls set an upper limit; they do not guarantee that FPS or correct timing throughout the game. Frame-dependent movement, effects, battle timing and movie/audio synchronization need playtesting. Use a separate save slot and compare against Original. Return to Original if the game runs too fast or timing changes. Pre-rendered movies retain their source cadence. Logs report swap submissions per second, which is not a measurement of unique displayed frames.

## Controls and saves

| Keyboard | Xbox action |
| --- | --- |
| Enter or X | Start |
| Space or semicolon | A / confirm |
| Backspace or apostrophe | B / back |
| L / P | X / Y (Y opens game menus) |
| W / A / S / D | Left stick |
| Arrow keys | Right stick |
| Shift + arrow keys | D-pad |
| F3 / F4 / Backtick | SDK debug / settings / console |
| Alt + F4 | Close |

Briefly hold a key if a very short tap is missed. A pinned SDL controller mapping database is bundled and copied alongside the native build. Physical controller compatibility and button mapping still need broader testing.

Everything generated for your installation stays in `.local/` and `bin/`:

- `.local/game`: extracted game data.
- `.local/toolchain`, `.local/python`, `.local/build`: downloaded tools and native compilation outputs.
- `.local/runtime/user`: saves; **Open saves** opens this folder.
- `.local/runtime/cache`: shader caches, retained between sessions.
- `.local/runtime/boot-*.log` and `.local/logs`: game/setup logs.
- `.local/launcher.json`: your ISO path and visual settings.

Rebuilding preserves saves and caches. Cancel stops this setup process tree; retrying verifies existing extracted files and resumes partial extraction. Conflicting existing game files are left intact and reported. **Back up saves** creates a private ZIP after you close the game. **Open logs** opens local runtime logs. **Export playtest report** writes path-redacted text logs, selected settings and the last launch settings to a ZIP you can review and share. Reports exclude game files, saves and memory dumps. **Updates / issues** opens this repository; nothing is uploaded automatically.

## Troubleshooting

- **Unsupported revision:** the executable hash must match the value above. Other regional versions or patched executables need separate analysis.
- **Windows refused access to a tool/archive:** setup stops without altering security settings. On the development PC, Windows security blocked the official SDK archive. A supported SDK already installed in `.local/toolchain/rexglue-sdk-0.10.0/win-amd64` can be reused after its version is checked. Fresh-machine setup still needs testing; do not disable protection to force setup through.
- **Compilation fails or runs out of memory:** try two build threads, or one on a smaller machine. Export a report and include the failing compiler message.
- **Rendering problem or stutter:** try Original (1x), repeat the same section with a warm shader cache, and report the location, GPU/driver and whether it repeats. Higher scaling is experimental.
- **Shortcut opens setup:** the local build has not completed, or the installation folder moved. Complete setup and recreate the shortcut.

## Build the launcher from source

The repository contains original host integration, function-discovery hints, the ISO reader, setup worker and Windows Forms launcher. It contains no game executable, translated game source or assets. Public CI builds and tests the launcher without a game ISO.

From Windows PowerShell in the repository:

```powershell
./scripts/build-launcher.ps1
./EnchantedArmsLauncher.exe
```

The launcher uses installed .NET Framework components. No Visual Studio installation or administrator account is needed. Build tools are downloaded into the project rather than installed system-wide. Their publisher URLs, sizes and hashes are pinned in `toolchain.lock.json` and `launcher/python.lock.json`.

For command-line setup:

```powershell
./scripts/setup.ps1 -Iso 'D:/MyGames/Enchanted Arms.iso' -Jobs 2
```

For an existing extraction/toolchain, `scripts/build.ps1` regenerates and rebuilds the native application, staging the enhanced build in `bin/graphics-0.3.0`. The launcher prefers this build and prevents another launch/rebuild while its game is running. `scripts/run.ps1` opens the launcher to play with saved settings. `scripts/collect-crash.ps1` optionally collects a matching Windows crash event and symbolizes it using the local build. Its raw diagnostic report can contain local paths; review it before sharing.

Run the synthetic setup tests with Python 3.12 or later:

```powershell
python ./tools/test_setup.py -v
```

Building the full kit from a source checkout also requires building the spatial renderer; see [renderer build instructions](renderer/README.md). Afterward, `python tools/package_release.py` packages an explicit source allowlist, the launcher, and verified game-independent renderer files. It excludes `.local`, `bin`, `audit`, translated code, ISOs, XEXs, saves and dumps. Do not distribute the locally generated native game executable.

The [roadmap](docs/ROADMAP.md) prioritizes campaign compatibility, missing NPC investigation and measured performance work. This remains an early playtesting preview.

This is an unofficial fan project. See [third-party notices](THIRD_PARTY_NOTICES.md) for runtime attribution and [LICENSE](LICENSE) for original project contributions.
