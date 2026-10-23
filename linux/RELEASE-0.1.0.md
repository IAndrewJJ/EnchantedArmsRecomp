# EnchantedArmsPC — Linux preview 0.1.0

The first native Linux AppImage of EnchantedArmsPC, with a themed launcher,
ISO importing and local game compilation. Wine and Proton are not required.

Download **EnchantedArmsPC-linux-0.1.0-x86_64.AppImage**, allow it to run as a
program, and open it. Choose your supported Xbox 360 ISO and build folder under
**Settings → Game**, then **Build game**. The launcher waits for **Play Enchanted
Arms** before opening the game.

- Resolution scaling, FSR 1, CAS, antialiasing and texture filtering/detail.
- 30 and 60 FPS options, with V-Sync always enabled.
- A selectable save location; saves default to **Saves** inside the build folder.
- Keyboard/controller settings, F6 FPS counter, F8 screenshots and a desktop shortcut.
- Bundled build tools; the AppImage contains no game files or translated game code.

Requires x86-64 Linux, glibc 2.35 or newer, working Vulkan drivers, and about 20 GB
of free space beyond the ISO. Ubuntu 22.04 or newer meets the glibc requirement.

**This is an experimental Linux preview.** Native compilation, graphics pipeline
checks and packaged launcher tests run on Linux CI. Actual Enchanted Arms gameplay
on Linux hardware has not been verified. We need Linux players to test setup,
graphics, audio, saves and progression before recommending it for normal play.

The Windows 0.5.7 release is unchanged. This preview uses a separate Linux release
channel and does not replace Windows downloads. Keep backups of your saves.

See the included Linux README for folder locations, FUSE alternatives and build
instructions. The runtime source archive and build recipes accompany the AppImage.
