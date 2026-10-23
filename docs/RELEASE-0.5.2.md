# 0.5.2 — Fresh-install build fix

Setup now downloads the project's source-built ReXGlue tool package instead of
the upstream 0.10.0 Windows archive, which Windows security blocked during a
fresh-install reproduction. The old popup showed only `Invalid argument`.

The replacement uses the same pinned SDK source revision and the optimized
runtime configuration needed by the game. It excludes unused runtime variants,
records its provenance, and is checked by size and SHA-256. No security settings
are changed. A remaining security block gets a clear message and stops setup.

**To retry:** close the launcher, extract the complete 0.5.2 kit over your
installation, preserve `.local` and `bin`, reopen the launcher, and choose
**Build game**. Verified downloads are reused. Do not uninstall to retry;
uninstalling removes saves.

Native build remains 0.3.4. Existing playable installations do not need a game
rebuild. This release does not change gameplay or graphics settings.

Validation includes security-error regression tests, archive read-back,
fresh extraction, code-generator execution, and compiling/linking/running a
windowless SDK client without any game data. The same SDK client check is part
of the full fresh-toolchain CI run. See [setup help](SETUP_TROUBLESHOOTING.md)
and [package provenance](SETUP_SDK.md).
