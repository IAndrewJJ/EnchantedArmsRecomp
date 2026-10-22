# 0.5.0 — Controls, texture packs and update recovery

This preview keeps the simplified Play/Settings launcher and the existing
normal-speed 60 FPS corrections. It adds:

- **Controls:** keyboard capture, controller button swaps, trigger swaps,
  radial stick dead zones, camera response, inverted camera Y and mouse camera
  sensitivity. F2 releases/captures mouse look. Newly connected controllers are
  assigned to the single player's input slot.
- **Graphics:** a Supersampling + FXAA option that chooses a render scale above
  the selected display size, in addition to the existing FSR 1/CAS, MSAA,
  filtering and mip-detail controls. It has a substantial GPU cost.
- **Display:** an optional recent frame-time graph with the F6 counter. It shows
  game swap intervals, p95 and peak times rather than GPU execution time.
- **Advanced:** an adjustable shader compilation worker count. Known shader
  caches are retained and prepared before gameplay; new effects can still stutter
  or briefly omit draws with background compilation enabled.
- **Textures:** experimental, local, preconverted DDS/XPR2 pack installation and
  selection. Original disc files remain intact. No enhanced textures are bundled.
  See [pack support and its limits](TEXTURE_PACKS.md).
- **Updates:** download/checksum verification from this project's GitHub
  releases, per-file validation, transactional installation and rollback of the
  previous kit. Updates restart the launcher and preserve saves, settings,
  native game builds and shader caches.

**Native build 0.3.4 is required for the new controller transforms and frame-time
graph.** Rebuild once under Settings → Game. Saves and caches are preserved.
The renderer remains 0.3.0; its corresponding source archive is unchanged.

## Updating and recovery

For the first update from 0.4.2 or earlier, close the game/launcher and unpack the
complete 0.5.0 Windows kit over the existing folder while preserving `.local`
and `bin`. Later releases can be checked under **Settings → Updates**. Install
restarts only the launcher; Play remains the only way to boot the game.

The updater verifies the release's SHA-256 ledger over HTTPS, verifies every kit
member and rejects private/unsafe paths. These are integrity checks, not a code
signing system. Backups and recovery journals stay in `.local/updates`. Rollback
restores replaced public files; it does not roll back your saves or native game.
An interrupted transaction must be restored before applying another update.

If the launcher cannot open after an interrupted update, close it and run:

```powershell
& ./.local/python/python.exe ./tools/update_game.py rollback --root . --restart
```

## Validation and remaining work

Portable timing/input checks, synthetic update interruption/rollback tests,
texture-pack validation tests, launcher settings/lifecycle checks and native
compilation cover this release. These checks do not establish a full playthrough
or physical-controller coverage on every device.

A full 0.4.2-to-0.5.0 kit installation and rollback were checked in an isolated,
deep Windows folder with private save/settings/native files preserved. All 344
original DDS/XPR headers passed the format checks; a local no-change pack also
exercised real file overlay creation/reuse without altering its originals. That
smoke test validates file routing, not the visual quality of third-party packs.

The opening area's 60 FPS comparison remains the extent of player timing
validation. Battles, later scripts and higher rates need playtesting. No fix is
confirmed for the opening invisible NPCs or dark patches on Toya/Makoto. Later
NPCs appearing normally does not resolve those original reports. No measured
campaign-wide FPS gain is claimed.
