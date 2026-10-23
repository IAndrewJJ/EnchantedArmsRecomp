<p align="center">
  <img src="launcher/branding/EnchantedArmsPC.png" width="120" alt="EnchantedArmsPC — a golden gauntlet holding a cyan crystal">
</p>

# EnchantedArmsPC

**Return to Enchanted Arms with sharper visuals, a smoother 60 FPS option, and controls tailored to your PC.**

An unofficial native Windows recompilation of the Xbox 360 RPG. Bring your own supported game ISO, let the launcher build your PC version, and start your adventure.

**[Download for Windows](https://github.com/IAndrewJJ/EnchantedArmsRecomp/releases/tag/v0.5.1)** · [Setup help](docs/SETUP_TROUBLESHOOTING.md) · [Known issues](docs/KNOWN_ISSUES.md) · [What's new](docs/RELEASE-0.5.1.md)

![EnchantedArmsPC launcher](docs/images/launcher.png)

## A more flexible way to play

| Feature | What it brings to your game |
| --- | --- |
| **30 or 60 FPS** | Choose the original 30 FPS cadence or smoother 60 FPS gameplay with game-speed corrections. |
| **Higher resolution** | Render at up to 4× the original resolution scale, with display options up to 4K. |
| **Upscaling and sharpening** | Use FSR 1 spatial upscaling or CAS sharpening to tune image clarity. |
| **Cleaner edges and textures** | Choose FXAA or supersampling, up to 16× anisotropic filtering, and adjustable texture detail. |
| **Your controls** | Play with a controller or keyboard and mouse. Remap buttons, adjust dead zones and camera sensitivity, and invert camera axes. |
| **PC conveniences** | Fullscreen or windowed play, a desktop shortcut, an F6 FPS counter, and F8 screenshots. |
| **One launcher** | Manage graphics presets, controls, optional local texture packs, and project updates in one place. |

Choose **Original**, **Balanced**, **Enhanced**, **High**, or **Ultra**, then fine-tune to suit your PC. Graphics changes apply when you next launch the game. Higher resolution and supersampling need more GPU power; prerecorded movies retain their original detail and frame rate.

Optional [replacement texture packs](docs/TEXTURE_PACKS.md) are supported. Enhanced texture assets are not included in the download.

## Start playing

1. **Download the complete Windows kit.** Open the [release page](https://github.com/IAndrewJJ/EnchantedArmsRecomp/releases/tag/v0.5.1) and choose **EnchantedArmsPC-0.5.1-windows-x64.zip** under **Assets**. Extract the entire ZIP into a dedicated writable folder, such as `C:\Games\EnchantedArmsPC`.
2. **Build from your ISO.** Open **EnchantedArmsLauncher.exe**, choose **Set up game**, select your ISO under **Settings → Game**, and click **Build game**. The launcher downloads the required tools and prepares your game locally.
3. **Make it yours and play.** Choose your graphics and frame rate in **Settings**, then click **Play Enchanted Arms**. Use **Settings → Game → Create desktop shortcut** for easy access next time. The launcher opens first so you can change settings before playing.

Download the named Windows kit above rather than GitHub's **Source code** ZIP. The kit includes the launcher and setup components; Python and Visual Studio do not need to be installed separately.

## What you'll need

- **Your own supported Enchanted Arms Xbox 360 ISO** — the USA/Europe disc version listed below. Game files are not included.
- **Windows 10 or 11, 64-bit**, with **.NET Framework 4.8** and the [Microsoft Visual C++ v14 x64 Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist).
- A **Direct3D 12-capable GPU**. The best resolution preset depends on your hardware.
- An internet connection for first setup, which downloads about **800 MB** of tools, and roughly **20 GB of free space** in addition to your ISO. The initial build takes several minutes.

<details>
<summary>Supported disc version</summary>

The supported USA/Europe release has title ID `555307E6`, media ID `41637F65`, and XEX version `0.0.0.1`. Its game executable has this SHA-256:

```text
79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22
```

The launcher checks compatibility before extraction and reads your ISO without modifying it. Other game revisions are not currently supported.

</details>

## Current playtest status

**Available now as an early public preview.** Exploration, opening battles, audio, menus, saving, and loading have been exercised. The latest playthrough report covers about three hours of steady upscaled 60 FPS gameplay, with only a few dips when effects first appear and no visual issues reported during that stretch.

Full campaign and broader hardware testing are ongoing. Earlier reports of invisible opening-area NPCs and dark patches on character models remain unresolved, and new effects can briefly stutter. See the [known issues](docs/KNOWN_ISSUES.md) before starting. If you encounter a problem, [report it here](https://github.com/IAndrewJJ/EnchantedArmsRecomp/issues/new/choose) to help improve the next build.

## Useful while you play

| Default shortcut | Action |
| --- | --- |
| Enter | Start |
| Space / Backspace | Confirm / back |
| P | Open the in-game menu (Xbox Y) |
| W A S D / arrow keys | Move / camera |
| F2 | Capture or release the mouse camera |
| F6 | Show or hide the FPS counter |
| F8 | Take a screenshot |

Find the full control mappings and remapping options under **Settings → Controls**.

**Updates:** Use **Settings → Updates** in launcher 0.5.0 or newer. Current players on native build 0.3.4 can install launcher 0.5.1 without rebuilding the game. For older installations or a failed setup, see [setup and update help](docs/SETUP_TROUBLESHOOTING.md).

**Your saves:** Saves stay in `.local/runtime/user` inside your installation. Back up that folder to keep a separate copy. Rebuilding preserves saves. The optional **Settings → Game → Uninstall EnchantedArmsPC…** asks for confirmation and permanently removes the installation **including its saves**; an ISO stored outside that folder is preserved.

---

EnchantedArmsPC is an unofficial fan project, unaffiliated with the game's creators or publishers. No game ISO, executable, or game assets are distributed. Built with [ReXGlue](https://github.com/rexglue/rexglue-sdk) and a Xenia-derived renderer. [License](LICENSE) · [Third-party notices](THIRD_PARTY_NOTICES.md)
