# 0.4.2 — Normal-speed 60 FPS preview

Selecting 60 FPS now applies engine timing corrections in native build 0.3.3.
The previous limiter could double game speed. This update corrects exploration,
interface and menu scene steps, plus the separate texture-scroll increment used
by animated surfaces.

The opening-area player comparison reports normal character movement, menu
background and runic-wall animation at about 60 FPS. A follow-up describes no
speedup or very little. This does not establish complete campaign timing:
battles, cutscenes, scripted transitions and higher limits need more testing.
Original remains the default, and limits above 60 remain experimental. Movies
retain their original cadence.

## Install or update

Download `EnchantedArmsPC-0.4.2-windows-x64.zip` from this release's Assets and
extract the complete kit into a writable folder. Existing players should close
the game/launcher, preserve `.local` and `bin`, and overlay the new kit.

Open the launcher, then **Settings → Game → Rebuild game** using your own
supported ISO. This native rebuild is required for the timing fixes. Saves and
shader caches are preserved. Select **Settings → Display → Frame limit →
60 FPS · Preview**, then click Play. The launcher detects a legacy native build
and offers an update before high-FPS play.

## Validation and limits

- Native compilation, launcher settings/lifecycle checks and portable timing/UV
  checks are included in validation. Tests cover equal elapsed game time across
  caps, missed deadlines, loading bounds, nested scaling and UV scroll speed.
- The measured exploration path advanced about 30 engine-frame units per second
  while rendering around 60 FPS in the local opening-area test.
- NPC visibility/dark surfaces remain separate known issues. No campaign-wide
  performance improvement or flawless high-FPS timing is claimed.
- [Timing implementation and testing](TIMING.md) describes the corrected paths
  and remaining checks.

The kit contains no ISO, native game executable, assets, translated game source
or saves. Each player builds locally. The renderer is unchanged from 0.3.0;
its corresponding source archive is `ReXGlueSpatial-0.3.0-source.tar.gz`.
