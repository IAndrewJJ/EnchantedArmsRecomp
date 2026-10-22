# Spatial FidelityFX renderer

The complete 0.3.0 launcher kit bundles five game-independent renderer files and a hash manifest here. They are release assets rather than tracked binaries. No translated game code is included.

The renderer is built from ReXGlue SDK commit `f5337cdc947ff6d4c4196737e2c807a48f2a1fc2` (v0.10.0), with its checked-in FSR EASU/RCAS and CAS shader paths enabled consistently using `REX_HAS_FIDELITYFX_SDK=1`. The optional temporal FidelityFX backend stays disabled. This enables spatial FSR 1 and CAS; it does not implement FSR 2/3 or frame generation.

To rebuild using the project's portable compiler environment, run from the repository in PowerShell after the normal toolchain is available:

```powershell
. ./scripts/environment.ps1
python ./tools/build_spatial_renderer.py --git git --rc "$taskSdkBinDirectory/rc.exe" --mt "$taskSdkBinDirectory/mt.exe"
```

The script fetches the pinned SDK and its pinned Windows dependencies, handles Git's Windows representation of libmspack symlinks, compiles the native runtime/plugin with SSE4.1 enabled, and writes DLLs, import libraries and their SHA-256 manifest into this directory. It never reads a game ISO. The original SDK installation is preserved; `tools/prepare_spatial_sdk.py` verifies this package and creates a separate SDK overlay for native game compilation.

`tools/graphics-probe` tests the actual renderer flag parser and creates the D3D12 presenter, which initializes the spatial shader pipelines without loading a game or opening a game window. On the development RX 6700 XT, both the FSR and CAS probes passed with the expected sharpening values, 2× rendering flags, 16× anisotropic filtering and FXAA selected.

License and source notices are in `THIRD_PARTY_NOTICES.md` and `licenses/`. Rebuildable upstream source and pinned submodule revisions are available from the SDK commit linked above; an accompanying source archive is provided with the 0.3.0 release.

To use that archive without Git downloads, unpack it and pass `--offline-source --source 'D:/Source/ReXGlueSpatial-0.3.0'` to the build command above. The archive records the exact submodule revisions, materializes libmspack links and adjusts upstream's Git-metadata check for an archive without `.git` directories. `tools/package_renderer_source.py` creates the corresponding source asset from a verified checkout.

The 0.3.0 renderer adds `ea_texture_lod_bias` through the version-pinned `renderer/patches/texture-detail.json`. The patch adds a sampler-cache eligibility bit and an optional D3D12 sampler bias. Only linear-filtered surfaces with usable mipmaps are eligible; point filtering and base-map requests retain their original behavior. Zero is the default. The builder applies the patch idempotently, and the corresponding source archive includes the patched files and patch recipe. No texture-replacement loader or upscaled texture assets are included.
