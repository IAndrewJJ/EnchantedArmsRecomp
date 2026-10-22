# Third-party notices

The native runtime and generated CMake integration use [ReXGlue SDK 0.10.0](https://github.com/rexglue/rexglue-sdk/tree/v0.10.0), including code derived from [Xenia](https://github.com/xenia-project/xenia). Its BSD 3-Clause license is reproduced in `licenses/ReXGlue.txt`. The SDK downloads and its bundled dependencies retain their own notices.

The 0.2.0 Windows kit additionally distributes game-independent ReXGlue runtime/plugin DLLs and import libraries compiled from SDK commit `f5337cdc947ff6d4c4196737e2c807a48f2a1fc2`. Spatial shader paths are enabled consistently with `REX_HAS_FIDELITYFX_SDK=1`; the optional temporal FidelityFX backend remains disabled. The original build recipe is `tools/build_spatial_renderer.py`. The accompanying **ReXGlueSpatial-0.2.0-source.tar.gz** release asset contains the corresponding SDK source, pinned Windows submodules, licenses, provenance, build instructions and documented source-archive adjustments. It permits rebuilding and modifying the runtime and its components separately from any game.

AMD [FidelityFX FSR 1](https://github.com/GPUOpen-Effects/FidelityFX-FSR) and [CAS](https://github.com/GPUOpen-Effects/FidelityFX-CAS) spatial shader code is used through ReXGlue's presenter. MIT notices are reproduced in `licenses/AMD-FSR.txt` and `licenses/AMD-CAS.txt`.

Runtime component notices are reproduced in `licenses/runtime/`; the source archive retains the full upstream notices. Components include FFmpeg and libmspack (LGPL components), SDL, Tracy, fmt, spdlog, xxHash, CLI11, simde, toml++, utfcpp, imgui, o1heap, tiny-AES and other SDK dependencies. Licenses accompanying FFmpeg include terms for optional upstream components; their presence does not imply every optional codec was enabled. The SDK's CMake configuration selects the runtime's compiled files. Rebuild instructions and full source are provided with the release.

The [SDL GameControllerDB](https://github.com/mdqinc/SDL_GameControllerDB) mapping database is bundled under its zlib license (`licenses/SDL_GameControllerDB.txt`). `data/controllerdb-provenance.json` records the pinned upstream revision and SHA-256.

The launcher downloads portable build dependencies directly from their publishers, with pinned digests in `toolchain.lock.json` and `launcher/python.lock.json`:

- [Python](https://www.python.org/): the complete kit bundles the official 3.12.10 embedded archive, verified against `launcher/python.lock.json`. Its `LICENSE.txt` includes the PSF license and accompanying dependency notices; setup retains them locally.
- [7-Zip](https://www.7-zip.org/): see the license and unRAR restrictions included in its extracted distribution. The bootstrap 7zr executable is public domain.
- [LLVM](https://github.com/llvm/llvm-project): Apache 2.0 with LLVM exceptions and included third-party notices.
- [CMake](https://github.com/Kitware/CMake): BSD 3-Clause and included notices.
- [Ninja](https://github.com/ninja-build/ninja): Apache 2.0.
- Microsoft C/C++ runtime headers/libraries and Windows SDK: Microsoft license terms included with those packages. They are downloaded for local compilation; these packages are not bundled in this release.

Enchanted Arms and its game content belong to their respective rights holders. This is an unofficial fan project. The public kit includes no ISO, XEX, game assets, translated game source, saves, or compiled game executable. Players supply their own supported Xbox 360 ISO and generate the native build locally.
