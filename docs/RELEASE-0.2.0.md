# EnchantedArmsPC 0.2.0 — graphics playtesting preview

Download **EnchantedArmsPC-0.2.0-windows-x64.zip** and unpack it into a writable local folder. Open `EnchantedArmsLauncher.exe`, select your own supported USA/Europe Xbox 360 ISO and build locally. The source ZIP and launcher-only CI artifact are for development and do not contain the complete renderer kit.

This preview adds spatial AMD FSR 1 and CAS through rebuilt native D3D12 shader paths, independent 1–4x internal resolution, FXAA, texture filtering up to 16x AF, sharpening, borderless/windowed display, dithering and aspect options. Graphics profiles can be exported/imported. It also adds private save backups, log access, controller mappings and reports that distinguish selected settings from the last launch.

Validation: native game compilation; nine synthetic extraction/setup tests; launcher settings/argument self-test; visual inspection of the graphics dialog; FSR and CAS GPU pipeline initialization probes on an RX 6700 XT. Earlier user testing confirmed opening exploration, menus, models, audio and save/load. The new presets and full campaign still require playtesting. Diet Doctor and Smiling Salesgirl invisibility remain open bugs. Fresh-machine setup and additional GPUs/controllers need coverage.

FSR 1 is spatial. FSR 2/3, frame generation and frame-rate unlocking are not included. Pre-rendered videos and source textures retain their original detail.

No ISO, game executable, assets, translated game code or saves are included. The accompanying **ReXGlueSpatial-0.2.0-source.tar.gz** is corresponding source for the game-independent renderer and its pinned dependencies. **SHA256SUMS.txt** contains download digests. See the repository README for the supported executable hash and installation details.

For an existing installation, close the game, back up `.local/runtime/user`, unpack the kit over the project folder preserving `.local`, then choose **Build from ISO** once. Existing saves and caches are retained. Settings apply at the next launch.
