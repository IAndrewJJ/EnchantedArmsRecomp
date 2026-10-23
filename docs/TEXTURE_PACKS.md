# Local texture packs

Launcher 0.5.0 adds experimental replacement support under **Settings → Textures**.
Install a compatible ZIP, select it, apply settings, then click Play. Choose
**Original textures** to disable it. No enhanced texture assets are included in
the public project. This feature does not automatically upscale or convert art.

Packs replace existing **DDS textures** or **Xbox 360 XPR2 bundles**, prepared by
the pack author for this game. Raw PNG/JPEG files, models, shaders, scripts, BND
archives and executable patches are not accepted. Many character textures live
inside other containers, so this first implementation does not support replacing
every character texture. Texture packs do not fix the invisible NPC defect.

## Installation and storage

The launcher verifies the supported XEX hash, original texture hashes,
replacement hashes, headers, formats and ZIP paths. It stores the verified archive
in `.local/texture-packs`. On Play, it creates a separate game directory under
`.local/texture-overlays`: unchanged files are NTFS hard links and replacements
are independent files. The ISO and extracted originals are not overwritten.
Texture packs do not change your selected save location or shader cache. Saves default to `Saves` inside the game folder; choose another location in **Settings → Game**.

The extracted game and overlay must be on the same NTFS volume. If Windows cannot
create the links, launch stops with a message; select Original to continue.
The first preparation creates file links; later launches verify the selected
textures and reuse the prepared directory. Close the game before installing or
changing packs. Keep the installation folder in place while playing. Do not edit
unchanged linked game files through the overlay directory.

## Authoring format

The ZIP contains `pack.json` plus exactly the payloads it lists. File paths use
forward slashes and are relative to the extracted game or ZIP root. For example:

```json
{
  "schema": 1,
  "id": "example-ui",
  "name": "Example UI textures",
  "game_xex_sha256": "79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22",
  "files": [
    {
      "target": "menu/sprite/chr.dds",
      "file": "textures/chr.dds",
      "original_sha256": "REPLACE_WITH_ORIGINAL_FILE_SHA256",
      "sha256": "REPLACE_WITH_REPLACEMENT_FILE_SHA256"
    }
  ]
}
```

The placeholders must be real SHA-256 values. DDS replacements retain the
original pixel format, channel masks and resource type; larger dimensions up to
16384 are accepted when the payload contains the declared mip data. Supported
DDS formats are legacy DXT1/3/5 and RGB. XPR replacements must be converted XPR2
bundles with valid declared header/data lengths. Structural checks do not prove
that a bundle's internal resources are compatible; test the resulting pack in
the affected scenes before sharing it.

Limits: 4096 replacements, 64 MiB per file and 1 GiB total. Duplicate paths,
unlisted files, path traversal, executables and changed originals are rejected.
The synthetic checks in `tools/test_texture_packs.py` require no game assets.
Only share texture content you are entitled to distribute.
