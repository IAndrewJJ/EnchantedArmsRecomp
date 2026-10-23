# Source-built setup SDK

Launcher 0.5.2 pins `EnchantedArmsPC-ReXGlue-0.10.0-setup-1.zip` in
`toolchain.lock.json`. It is built from ReXGlue commit
`f5337cdc947ff6d4c4196737e2c807a48f2a1fc2`, the same source used by the project's
spatial renderer. It contains no Enchanted Arms code or assets.

The upstream 0.10.0 Windows archive was blocked with Windows error 225 during a
fresh download on the development PC. The C runtime mapped the file-open error
to errno 22, making the old installer message misleading. The SDK has a
[matching upstream report](https://github.com/rexglue/rexglue-sdk/issues/485).
[Microsoft documents errors 225 and 226](https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-)
as security detection/removal. A download checksum establishes its identity;
it does not override a security verdict.

The setup SDK includes RelWithDebInfo only, using a source-built optimized
`rexglue.exe` linked to `rexruntimerd.dll`. The upstream Release/Debug runtime
variants are not included. The game retains the previously shipped spatial
renderer through its separately verified overlay. A new SDK/overlay directory
prevents old blocked files or old completion receipts being copied forward.

The package includes headers, CMake exports, libraries, required DLLs, license
notices, `package-provenance.json`, and `SDK-SHA256SUMS.json`. Its release carries
the corresponding `ReXGlueSpatial-0.3.0-source.tar.gz` archive. The public recipe
is in this repository; no official release ZIP is needed as a packaging input.

## Rebuild

Use the matching renderer source archive, extracted to a new folder, and the
portable compiler environment described in [building](BUILDING.md):

```powershell
. ./scripts/environment.ps1
./.local/python/python.exe ./tools/build_spatial_renderer.py --offline-source --source D:/Source/ReXGlueSpatial-0.3.0 --build D:/Build/ReXGlueSetup --setup-sdk --rc "$taskSdkBinDirectory/rc.exe" --mt "$taskSdkBinDirectory/mt.exe"
./.local/python/python.exe ./tools/package_setup_sdk.py --source D:/Source/ReXGlueSpatial-0.3.0 --build D:/Build/ReXGlueSetup --directory D:/Build/NewSetupPackage
```

The package staging directory must be new. The packager uses the generated
RelWithDebInfo install rules, omitting only upstream's global CMake registry
registration block, then installs the optimized CLI at the standard SDK path.
It adds the CLI's generic imported location to the CMake config. These are
packaging adjustments, not runtime behavior changes.

The installer still verifies the complete download's pinned size and digest,
reopens it for disk verification, and only then extracts and probes the tools.
Security settings, exclusions and quarantined files are never modified.
