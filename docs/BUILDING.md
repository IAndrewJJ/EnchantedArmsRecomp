# Building and validation

The repository contains original host integration, discovery hints, setup tools and the Windows Forms launcher. It contains no translated game source, game executable or assets. The public CI checks the launcher without a game ISO.

## Launcher only

On Windows 10/11 x64 with .NET Framework 4.8:

```powershell
./scripts/build-launcher.ps1
$check = Start-Process ./EnchantedArmsLauncher.exe --self-test -Wait -PassThru
if ($check.ExitCode -ne 0) { throw 'Launcher checks failed' }
./EnchantedArmsLauncher.exe
```

The script generates compiled version constants and assembly metadata from `release.json`. Generated files stay in `.local/launcher-build`. Use `-OutputPath` to build a candidate while the installed launcher is open. Native integration remains at the separately recorded `native_version`.

## Native game

Players should use the complete release kit and **Settings → Game → Build game**. The source ZIP and CI launcher artifact lack the enhanced renderer and bootstrap archive.

For command-line setup:

```powershell
./scripts/setup.ps1 -Iso 'D:/MyGames/Enchanted Arms.iso' -Jobs 2
```

For an existing supported extraction and toolchain, `scripts/build.ps1` regenerates and builds native code. It stages the enhanced build in `bin/graphics-<native_version>`. Tools, extracted files, build outputs, settings, saves and caches remain local. `scripts/run.ps1` opens the launcher; Play starts the game.

Tool URLs, sizes and digests are pinned in `toolchain.lock.json` and `launcher/python.lock.json`. They are downloaded into the project rather than installed system-wide. An existing supported SDK can be reused after required-file and version checks. Setup retries temporary access/network failures, reuses verified partial downloads, and records persistent failures with the operation and exception chain. See [setup help](SETUP_TROUBLESHOOTING.md).

## Checks

```powershell
python tools/test_setup.py -v
python tools/test_release.py -v
```

The separate network integration check requires Windows and a **new** directory. It downloads, verifies, extracts and runs the official SDK without an ISO or existing toolchain:

```powershell
python tools/check_toolchain.py --sdk-only --directory '.local/fresh SDK check'
```

Omit `--sdk-only` to install/check every pinned build tool. CI installs all pinned tools, runs the SDK version check, then checks reuse in a fresh Windows runner folder with spaces.

Launcher `--self-test` additionally checks settings migration/round trips, culture-independent arguments, desktop-scoped instance identity, bounded logging, process-exit callbacks and compiled release metadata. Its windowless child fixture never launches a game.

`tools/graphics-probe` contains native probes for FSR/CAS initialization, pacing, FPS overlay and PNG/frame-interval diagnostics. Most require the prepared SDK. The portable timing probe can also be compiled directly with C++17 or newer and needs no game data. These checks do not establish complete campaign compatibility. See [timing validation](TIMING.md).

## Full release kit

Build the [spatial renderer](../renderer/README.md) and place its verified binaries/capability manifest in `renderer/`. Supply the checksum-pinned Python bootstrap under `bootstrap/`, then:

```powershell
python tools/package_release.py
python tools/validate_package.py dist/EnchantedArmsPC-0.4.2-windows-x64.zip
```

The package uses an explicit allowlist, stable ZIP metadata and a per-file hash ledger. Identical input files produce identical ZIPs; this does not imply that separate compiler invocations produce identical executables. The validator rejects duplicate/unlisted members, hash mismatches, private paths and unexpected executables. The packager derives the kit version from `release.json`.

Publish the Windows kit with `SHA256SUMS.txt` and the corresponding renderer source archive. The unchanged renderer in this release uses `ReXGlueSpatial-0.3.0-source.tar.gz`. Never publish `.local`, `bin`, ISOs, XEX files, saves, dumps, shader caches or translated game code.
