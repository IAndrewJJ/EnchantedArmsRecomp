# EnchantedArmsPC 0.3.0 — texture detail and experimental FPS

**Download EnchantedArmsPC-0.3.0-windows-x64.zip under Assets**, extract the whole ZIP, and open **EnchantedArmsLauncher.exe** inside. The repository file browser and GitHub source ZIP show development sources. The Windows ZIP contains the complete launcher, verified Python bootstrap and game-independent renderer.

New Graphics settings:

- **Texture detail boost:** Original / Crisp / Strong. Optional negative mip bias favors sharper existing texture levels on filtered mipmapped surfaces. Point-sampled and base-map requests retain their sampling. Combine with 16x AF or CAS. Strong may shimmer; this is not an HD/AI replacement texture pack.
- **Frame-rate limit:** Original / 30 / 60 / 90 / 120 / 144 / 165 / 180 / 240 / 360 / Unlimited. Explicit limits are experimental. They remove the console vblank throttle and pace the original D3D swap boundary. Original remains the default and restores console pacing. The guest timebase and audio clock stay unchanged.
- Settings persist in saved/imported profiles, the desktop shortcut and playtest reports. Logs include effective detail/timing settings and observed swap-submission rates.

The FPS numbers are limits, not guaranteed performance. Correct movement, animations, battles and movie/audio timing at higher rates remain unverified. Compare Original against 60 using a separate save slot. Return to Original if timing changes. Pre-rendered video retains its source cadence; swap submissions do not establish unique displayed frames.

Validation: native compilation; launcher settings/migration/argument tests; all supported frame limits measured by the native pacing probe, including stalled-scene recovery; real D3D12 FSR/CAS initialization with the texture-detail flag applied; isolated game startup using separate save/cache folders; nine synthetic setup/extraction tests. Full campaign, fresh-machine toolchain setup and additional GPUs/controllers still need coverage. Diet Doctor and Smiling Salesgirl invisibility remain open bugs.

Existing installation: close the game, back up `.local/runtime/user`, unpack over the project folder preserving `.local`, then choose **Build from ISO** once. The native update stages separately in `bin/graphics-0.3.0`. Original texture detail/FPS remain selected in older profiles.

Each player supplies their own supported USA/Europe Xbox 360 ISO and generates the native game locally. No game assets, translated game source, game executable or saves are included. **ReXGlueSpatial-0.3.0-source.tar.gz** contains corresponding renderer source and licenses; **SHA256SUMS.txt** contains download hashes.
