# 0.5.1 — Confirmed uninstall and two stable FPS options

**Settings → Display → Frame limit** now contains only **30 FPS · Stable** and
**60 FPS · Stable**. New installations default to 30. Existing 60 FPS preferences
stay at 60; old Original preferences become 30 and retired higher/unlimited caps
become 60. The existing native timing corrections are unchanged. Player testing
now includes about three hours of upscaled 60 FPS gameplay with few dips on first
use of effects/abilities and no visual issues reported in that span. Full
campaign testing continues.

The launcher also embeds an original gold-gauntlet/cyan-crystal icon approved by
the project owner. Its ICO contains ten sizes from 16 to 256 pixels for desktop
shortcuts, Explorer and application windows. Existing shortcuts already point
to the executable's icon; use **Settings → Game → Create desktop shortcut** to
recreate/refresh one if Windows still shows a cached icon. The source PNG and
generation prompt are included under `launcher/branding`.

**Settings → Game → Uninstall EnchantedArmsPC…** permanently removes the current
installation. The confirmation shows the exact folder and explains that saves,
settings, extracted game files, texture packs, caches and build tools will be
deleted. **Cancel is selected by default**, and Enter/Escape cancel the initial
popup. Back up anything you want to keep before confirming.

The game must be closed. After confirmation the launcher exits, a temporary
helper removes the installation, and the helper cleans up its own files. No
Python installation or native rebuild is required. Native integration remains
0.3.4 and the renderer remains 0.3.0.

## Removal boundaries

- The entire displayed installation folder is removed, including any extra
  files you placed inside it. Extract the kit into a dedicated folder.
- Matching `EnchantedArmsPC.lnk` shortcuts in the current/public Desktop and
  Programs folders are removed only when their target is this installation's
  launcher. Shortcuts pointing to another installation are kept.
- ISOs and downloaded archives outside the installation folder are preserved.
  The uninstaller does not search the computer for other copies.
- Directory links and symbolic links inside the installation are removed as
  links; their external contents are preserved. An installation whose own root
  or parent path is redirected is refused.
- Drive/share roots and shared Windows/user folders are refused. The helper
  checks the installation's physical directory identity before deleting and
  checks resolved child paths remain within that folder.
- Open game/launcher processes block removal. Locked files produce an error;
  the launcher and identification files are retained until other files have
  been removed, allowing retry after closing the blocking program.

## Validation

Launcher self-tests use newly created temporary fixtures. They check default
Cancel behavior, protected roots, missing/changed installation identity,
long paths, read-only files, external junctions, junction cycles, shared hard
links, shortcut target matching and temporary helper self-cleanup. Tests never
uninstall the development installation or touch real saves. GitHub CI runs the
same Windows tests alongside the existing setup/package/input/timing checks.

Update by unpacking the complete Windows kit over your existing installation,
or use **Settings → Updates** in launcher 0.5.0. No game rebuild is needed.
