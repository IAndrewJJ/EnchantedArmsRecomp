# 0.5.6 — A tidier game folder

Keep the default locations in **Settings → Game** to build beside the launcher.
Your game and launcher stay together, with saves in a visible **Saves** folder.

- **Save location** now has its own folder picker. Choose another folder if you prefer; your current progress is copied there without overwriting different saves.
- **Use launcher folder** and **Use default** restore the recommended build and save locations.
- Selecting the outer folder from an extracted download now finds the project inside it. Existing EnchantedArmsPC project folders can also be used for setup.
- Older saves are moved automatically into **Saves**. Changing locations and retrying setup preserve existing progress and settings.
- Screenshots stay in the game's `.local/runtime/captures` folder, even when saves are elsewhere.

**Updating:** install this launcher update, then choose **Update Game** and rebuild
from your ISO for native build **0.3.6**. Verified tools and extracted game files
are reused. This keeps the existing gameplay fixes and adds support for the new
folder arrangement. Close the game before changing folders.

If you already built in a separate location, open the launcher in that completed
build folder and update there. It is a standalone installation; the original
download is no longer needed. Keep your ISO and any personal files before removing
an old download folder. The update does not automatically delete other folders.

When changing save locations, the old save folder is kept as a backup. If the
selected folder contains different progress, choose an empty folder instead.
External save folders remain intact when uninstalling the game.

Download **EnchantedArmsPC-0.5.6-windows-x64.zip** from Assets and extract it
before opening **EnchantedArmsLauncher.exe**. Supply your own supported game ISO.
