# EnchantedArmsPC

**An unofficial native PC recompilation of Enchanted Arms for Xbox 360.** Build and play on Windows using your own supported disc image.

[Download the Windows kit](https://github.com/IAndrewJJ/EnchantedArmsRecomp/releases/tag/v0.5.1) · [Known issues](docs/KNOWN_ISSUES.md) · [Setup help](docs/SETUP_TROUBLESHOOTING.md) · [Build from source](docs/BUILDING.md) · [Contribute](CONTRIBUTING.md)

[![Launcher checks](https://github.com/IAndrewJJ/EnchantedArmsRecomp/actions/workflows/launcher.yml/badge.svg)](https://github.com/IAndrewJJ/EnchantedArmsRecomp/actions/workflows/launcher.yml)

![EnchantedArmsPC launcher](docs/images/launcher.png)

## Start playing

1. Download **EnchantedArmsPC-0.5.1-windows-x64.zip** from the release's **Assets** section. Extract the entire ZIP into a dedicated writable local folder and keep its files together.
2. Open **EnchantedArmsLauncher.exe**. Choose **Set up game**, select your Xbox 360 ISO under **Settings → Game**, then **Build game**.
3. When setup finishes, click **Play Enchanted Arms**. The launcher always opens first; shortcuts never start the game automatically.
4. Use **Settings** for graphics, display, controls, local texture packs and updates. **Game → Create desktop shortcut** creates an **EnchantedArmsPC** desktop icon.

Requires **Windows 10/11 x64**, **.NET Framework 4.8**, the [Microsoft Visual C++ v14 x64 Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist) and a **Direct3D 12 GPU**. First setup downloads about **800 MB** of pinned build tools and needs roughly **20 GB free** in addition to the ISO. Compilation takes several minutes. Python and Visual Studio do not need to be installed separately.

The ready-to-run launcher is in the complete release ZIP. GitHub's source ZIP and the CI launcher-only artifact do not include the renderer and bootstrap files needed for player setup. If setup fails, follow [setup help](docs/SETUP_TROUBLESHOOTING.md); retrying reuses verified tools and extracted files.

## Current status

**Early preview.** Opening battles, exploration, audio, menus, save creation and save loading have been exercised. Full campaign compatibility and clean-machine installation still need broader testing.

- Some NPCs are invisible, and Toya, Makoto and other NPCs can have dark surface patches. These remain under investigation.
- Mild stutter has been reported. Repeatable measurements and broader GPU/controller coverage are needed.
- **60 FPS is a preview option with automatic game-speed corrections.** Opening-area character movement, menu background and a runic wall have passed a player comparison. Full campaign timing and higher limits remain unverified; Original stays the default. See [timing details](docs/TIMING.md).

See [known issues](docs/KNOWN_ISSUES.md), [playtesting guidance](docs/PLAYTESTING.md) and the [0.5.0 release notes](docs/RELEASE-0.5.0.md). No campaign-wide performance improvement is claimed.

## Graphics

| Preset | Internal scale | Output effect | Antialiasing | Filtering |
| --- | --- | --- | --- | --- |
| Original | 1× | Bilinear | Game requested | Game requested |
| Balanced | 1× | FSR 1 | FXAA | 8× AF |
| Enhanced | 2× | FSR 1 | FXAA | 8× AF |
| High | 3× | CAS | FXAA | 16× AF |
| Ultra | 4× | CAS | FXAA strong | 16× AF |

Settings apply on the next game launch. Internal resolution is separate from window size. Start with Original to establish a baseline: 2×, 3× and 4× render four, nine and sixteen times the native pixel count.

FSR 1 provides spatial upscaling; CAS provides sharpening. Texture detail selects sharper existing mipmaps, with possible shimmer at Strong. **Supersampling + FXAA** raises the internal scale above the selected display size for cleaner edges at a substantial GPU cost. Source movies keep their original detail/cadence. Temporal FSR and frame generation are not implemented.

**Settings → Textures** installs/selects local preconverted DDS/XPR2 replacement packs. It uses a separate game overlay and preserves disc files; no enhanced texture assets are bundled. See [supported formats, requirements and authoring](docs/TEXTURE_PACKS.md).

**Settings → Advanced** contains ROV/RTV, resolve readback, background shader compilation and worker count controls. Change one option at a time when investigating rendering problems. Immediate readback can stall heavily; these controls are not confirmed NPC fixes. **Display → Frame-time graph** adds recent interval, p95 and peak measurements to F6.

## Controls

| Keyboard | Action |
| --- | --- |
| Enter / X | Start |
| Space / semicolon | A / confirm |
| Backspace / apostrophe | B / back |
| L / P | X / Y (menus) |
| W A S D | Left stick |
| Arrow keys | Right stick |
| Shift + arrows | D-pad |
| F2 | Release/capture mouse camera |
| F6 | Toggle FPS counter |
| F8 | Save screenshot |
| Alt + F4 | Close |

**Settings → Controls** remaps keyboard keys and controller buttons, adjusts stick dead zones, camera response/inversion and mouse sensitivity, and swaps triggers. New/reconnected controllers feed the single player's slot. A pinned SDL controller database is included; physical controllers need broader testing. F6 counts game swap submissions, not monitor refresh or GPU execution time. F8 captures guest output before presenter effects and overlays and can briefly pause rendering.

## Supported game and updates

Only the **USA/Europe Xbox 360 release**, title `555307E6`, media `41637F65`, XEX version `0.0.0.1`, is supported. Required XEX SHA-256:

```text
79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22
```

Setup opens the ISO read-only and rejects unsupported executables before extraction. Files, translated code and compilation stay on the player's PC.

For the first update from an older launcher, close the game and launcher, keep a private backup of `.local/runtime/user`, and unpack the new kit over the existing project while preserving `.local` and `bin`. After 0.5.0, **Settings → Updates** checks/downloads verified releases, installs after closing the launcher, and can restore the previous kit. See [recovery instructions](docs/RELEASE-0.5.0.md).

**Launcher 0.5.1 uses native build 0.3.4.** Players already on native 0.3.4 do not need a rebuild. Older players should rebuild through Settings → Game for the input transforms and frame-time graph. The launcher prompts when selected settings require it. Keep the installation folder in place, or recreate the desktop shortcut after moving it.

Local data lives under `.local`: `runtime/user` for saves, `runtime/cache` for shader caches, `runtime/captures` for F8 images, and `runtime/boot-*.log` plus `logs` for diagnostics. Rebuilding preserves saves and caches. Report/log/save-management buttons have been removed from the simplified interface.

To remove this installation, close the game and use **Settings → Game → Uninstall EnchantedArmsPC…**. A popup shows the folder and asks for confirmation, with Cancel selected by default. Confirming permanently deletes **everything in that folder, including saves**, and matching standard shortcuts. External ISOs and linked external folders are preserved. See [uninstall details](docs/RELEASE-0.5.1.md).

## Development

Built with **ReXGlue SDK 0.10.0**, using a Xenia-derived renderer. See [build and validation instructions](docs/BUILDING.md), [contributor guidance](CONTRIBUTING.md), [changelog](CHANGELOG.md) and [roadmap](docs/ROADMAP.md).

This is an unofficial fan project. No ISO, game executable, game assets, translated game source or saves are distributed. Original contributions use the [MIT license](LICENSE); runtime components retain their own [licenses and notices](THIRD_PARTY_NOTICES.md). Corresponding renderer source accompanies the Windows release.
