# Contributing

Start with [known issues](docs/KNOWN_ISSUES.md) and the [roadmap](docs/ROADMAP.md). Base-game compatibility, reliable installation and measured performance improvements are the current priorities.

## Report a problem

Use the bug-report form and include the launcher version, Windows version, CPU, GPU/driver, input device, graphics settings, location and steps to reproduce. For stutter, repeat the same route with a warm cache and Original FPS. F8 writes a local screenshot to `.local/runtime/captures`.

Logs are optional. Share the relevant excerpt after removing personal paths. Keep disc files, translated game code, saves, shader caches, memory dumps and authentication files out of public issues and pull requests.

## Make a change

1. Follow [build instructions](docs/BUILDING.md). The launcher and synthetic tests build without a game ISO.
2. Keep the home screen focused on Play and Settings. Put graphics controls and installation options in the appropriate Settings page.
3. Preserve existing settings, saves and caches. The launcher must never start a game automatically.
4. For a rendering or performance fix, provide a repeatable scene, baseline settings and before/after evidence. Higher FPS alone does not establish correct game speed.
5. Run the relevant checks below and describe what they establish, along with any untested behavior.

```powershell
python tools/test_setup.py -v
python tools/test_release.py -v
./scripts/build-launcher.ps1
$check = Start-Process ./EnchantedArmsLauncher.exe --self-test -Wait -PassThru
if ($check.ExitCode -ne 0) { throw 'Launcher checks failed' }
```

Use UTF-8 and the repository's `.editorconfig`. C# targets the .NET Framework compiler's C# 5 syntax; no external UI framework or administrator installation is required. `.clang-format` supplies C++ and C# styles.

Original contributions use the repository's MIT license. Retain upstream notices and provide corresponding source for distributed runtime changes. Do not submit game assets, generated translations or locally compiled game executables.
