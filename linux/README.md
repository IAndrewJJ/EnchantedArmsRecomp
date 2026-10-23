# EnchantedArmsPC for Linux — preview

A native Linux launcher and Vulkan build of EnchantedArmsPC. Players supply their
own supported USA/Europe Xbox 360 ISO. Wine and Proton are not used.

## Play

1. Download **EnchantedArmsPC-linux-0.1.0-x86_64.AppImage** from the Linux preview release.
2. In your file manager's properties, allow the AppImage to run as a program, then open it.
3. Open **Settings → Game**, choose your ISO and build folder, then **Build game**.
4. Choose your graphics settings and press **Play Enchanted Arms**.

The launcher opens first and waits for Play. The AppImage includes the compiler
and game-independent runtime; the game is compiled on the player's own computer.
It does not contain the ISO, extracted game files, or translated game code.

Use an x86-64 Linux desktop (x86-64-v2 / SSE4.2) with glibc 2.35 or newer, a Vulkan-capable GPU and
working Vulkan drivers. Ubuntu 22.04 or newer meets the glibc requirement.
Allow about **20 GB of free space beyond the ISO** for extraction, build tools,
and compilation. First setup takes several minutes and uses two build jobs by default.
ARM Linux is not included in this preview.

If your distribution cannot mount AppImages through FUSE, run the AppImage with
`--appimage-extract-and-run`, or use `--appimage-extract` and open `squashfs-root/AppRun`.

## Your folders

The default build folder is **EnchantedArmsPC** beside the AppImage. You can select
another location. **Saves** inside that game folder is the default save location;
the launcher also lets you choose another save folder. Existing progress is copied
without replacing different saves. The original save folder is retained.

Launcher preferences are kept in `~/.config/EnchantedArmsPC/launcher.json`, or the
equivalent `XDG_CONFIG_HOME` folder. Game-specific logs, shader caches, build tools
and extracted files remain under `.local` inside the selected game folder.
Screenshots use `.local/runtime/captures`. Preserve your complete save folder
when moving between systems; do not rename the Xbox profile and title folders inside it.

The Linux launcher does not alter your ISO or a separate Windows installation.
Open an existing game folder or choose the same save folder explicitly to reuse
its progress. Close other copies of the game before sharing a save folder.

## Included controls

- Resolution scaling, AMD FSR 1, FidelityFX CAS, FXAA, texture filtering and detail.
- 30 and 60 FPS choices with the project's existing timing corrections.
- V-Sync always enabled; there is no launcher switch to disable it.
- Controller and keyboard support, keyboard bindings, mouse camera and stick settings.
- F6 toggles the FPS counter; F8 takes a screenshot; F2 releases the mouse camera.
- A desktop shortcut that opens the launcher.

## Preview validation

Linux CI builds the Vulkan runtime and original host integration using synthetic
imports, checks the spatial upscaler pipelines and PNG output, runs save/settings
tests, and starts the packaged launcher without launching a game.
**An actual Enchanted Arms playthrough on Linux hardware has not been verified.**
Windows gameplay results do not establish Linux compatibility or performance.
Please report the distribution, GPU/driver, launcher version and relevant local
log excerpt if you encounter a problem. Do not upload ISOs, saves, extracted files
or translated game code.

Texture-pack management and an in-place AppImage updater are not included in this
first Linux launcher. The Updates page links to Linux releases; replacing the
AppImage preserves the separately stored game and saves.

## Build the AppImage

The build steps are in `.github/workflows/linux-runtime.yml` and
`.github/workflows/linux-appimage.yml`. They use Ubuntu 22.04, Clang 20, the pinned
ReXGlue 0.10.0 source, and `linux/requirements.txt`.

`linux/build_sdk.sh` builds the Vulkan runtime with spatial FSR/CAS enabled and
applies `linux/patch_sdk.py`. `linux/package_toolchain.py` stages a portable
compiler, development sysroot and dependency notices. `linux/package_appimage.py`
packages the Qt launcher with an explicit public-source allowlist and rejects
game-data file types. Neither workflow needs a game ISO.

The accompanying runtime-source archive contains the pinned SDK and its selected
submodules. Source and dependency notices remain available inside an extracted
AppImage alongside the dynamically linked libraries and launcher source.
