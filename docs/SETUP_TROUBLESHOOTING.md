# Setup help

Use the complete **EnchantedArmsPC-0.5.2-windows-x64.zip** from [release Assets](https://github.com/IAndrewJJ/EnchantedArmsRecomp/releases/tag/v0.5.2). The GitHub source ZIP and launcher-only CI artifact are not complete player kits. Extract the ZIP before opening the launcher.

## Requirements

- Your own supported **USA/Europe Enchanted Arms Xbox 360 ISO**. Game files are not included.
- **Windows 10 or 11, 64-bit**, with **.NET Framework 4.8**.
- The [Microsoft Visual C++ v14 x64 Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist).
- A **Direct3D 12-capable GPU**. Higher graphics settings need more GPU power.
- Roughly **20 GB of free space** in addition to the ISO, and an internet connection for the first setup. Setup downloads about **800 MB** of build tools and takes several minutes. Python and Visual Studio do not need to be installed separately.

The supported game revision has title ID `555307E6`, media ID `41637F65`, and XEX version `0.0.0.1`. Its game executable has SHA-256:

```text
79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22
```

The launcher checks this before extraction and reads your ISO without modifying it. Other revisions are not currently supported.

## Updating after a failed setup

1. Close the launcher and game. Extract the new kit, then copy the contents of its `EnchantedArmsRecomp` folder over your existing project folder. Replace the project files when prompted. Avoid nesting a second project folder inside the first.
2. Keep your existing `.local` and `bin` folders. Do not delete the installation to retry: `.local` contains extracted files, tools, settings and any saves.
3. Open **EnchantedArmsLauncher.exe**, select your supported ISO under **Settings → Game**, and click **Build game** (or **Rebuild game**).

Setup checks the game revision first, prepares the build tools, then verifies/extracts your game and compiles it. It reuses verified downloads and extracted files. If a download completed but its final rename failed, the next attempt verifies and reuses that `.partial` file too. An incomplete or altered download is downloaded again. Checksums remain mandatory.

## `Invalid argument` downloading the ReXGlue SDK

**Fixed download path in 0.5.2.** A fresh-install reproduction of the 0.5.1 error
found that Windows security blocked the upstream SDK archive. Windows reported
error 225; Python reduced it to `errno=22`, hiding the reason in the popup.
The SDK has an [upstream report](https://github.com/rexglue/rexglue-sdk/issues/485)
about the same release archive.

Download the complete 0.5.2 kit and follow the update steps above, keeping `.local`
and `bin`. Setup now uses a separate, checksum-pinned package built from the
matching public source. It contains the optimized code generator and the runtime
configuration used by this project, without the unused Release/Debug variants.
The old blocked SDK folder is not reused. Your ISO and saves need not be deleted.
Existing playable native 0.3.4 builds do not need a rebuild.

If security software blocks a file in the new kit, setup stops and identifies
the security block. Check **Windows Security → Virus & threat protection →
Protection history** for the detection name and include that name with the setup
log in a report. The installer does not change antivirus settings or install a
blocked file. [Package provenance and build instructions](SETUP_SDK.md).

The following notes cover older reports and other file-access failures:

This error was reported with `rexglue-sdk-0.10.0-win-amd64.zip`. The old summary did not identify the failing file operation, so it is not sufficient to determine the underlying Windows cause. The ordinary path and doubled backslash in an escaped log do not, by themselves, mean the folder name is invalid.

Version 0.4.1 retries temporary file-access errors during cache checks, download finalization and extraction, with a short, bounded delay. It also retries interrupted connections. A persistent refusal stops setup with the exact operation, path and available Windows error codes; the complete exception chain is written to the local setup log. Retrying does not disable Windows protection or change access permissions.

If the error persists, confirm the project folder is writable and close programs holding the named file. A short local folder such as `C:/Games/EnchantedArmsRecomp` is a useful comparison if your folder is synced or deeply nested; move the whole installation and recreate its desktop shortcut after moving it. Administrator mode and disabling protection are not required by setup. Check Windows Security's Protection history if Windows reports a security block, and report it for investigation.

The official SDK has been freshly downloaded, checksum-verified, extracted and run during 0.4.1 development. CI now checks SDK installation on a fresh Windows runner. These checks do not establish the cause of every access refusal on another PC.

The full setup test also found a separate path-length failure while extracting the Windows SDK in a deep project folder. Version 0.4.1 uses extended Windows paths for ZIP extraction; it does not require a registry change or administrator mode.

## Reporting a remaining failure

The launcher error gives the file under `.local/logs/setup-*.log`. Open the newest setup log and include the last stage, the `SETUP FAILED` line and nearby exception lines in a [setup issue](https://github.com/IAndrewJJ/EnchantedArmsRecomp/issues/new?template=setup.yml). Include your kit version, Windows version, free disk space and whether the folder is local, synced or on a network drive. Remove your username and personal paths before sharing excerpts. Do not attach your ISO, extracted game files, saves or generated code.

Other common failures:

- **Unsupported game revision:** only the USA/Europe Xbox 360 executable listed under Requirements above is supported. Renaming an ISO does not change its revision.
- **Insufficient space:** allow roughly 20 GB beyond the ISO for the extracted game, tools and compilation.
- **Another setup is running:** let that attempt finish or cancel it in its launcher before retrying. An old `setup.lock` file alone does not hold the lock after its process exits.
- **Checksum mismatch:** no unverified tool is installed. Retry with a stable connection; report repeated failures with the package name.
- **Required DLL missing / exit code `0xC0000135`:** the SDK depends on the Microsoft Visual C++ runtime. Install or repair the **X64** v14 Redistributable from [Microsoft's official downloads](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist), then retry. This is separate from Visual Studio; setup does not install or modify that system runtime automatically.
