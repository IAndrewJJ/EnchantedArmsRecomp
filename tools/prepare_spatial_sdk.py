"""Overlay verified, game-independent spatial renderer binaries onto the pinned SDK."""
import hashlib
import json
from pathlib import Path
import shutil
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_paths import filesystem_path

PROJECT = Path(__file__).resolve().parents[1]

def prepare():
    renderer = PROJECT / "renderer"
    marker = renderer / "renderer-capabilities.json"
    if not marker.exists():
        raise ValueError("Enhanced renderer files are missing. Use the complete 0.3.0 launcher kit or build them with tools/build_spatial_renderer.py.")
    manifest = json.loads(marker.read_text())
    if manifest.get("sdk_commit") != "f5337cdc947ff6d4c4196737e2c807a48f2a1fc2" or not manifest.get("spatial_fsr") or not manifest.get("texture_detail"):
        raise ValueError("Unsupported enhanced renderer")
    allowed = {"rexruntimerd.dll","rexruntimerd.lib","rexgpu-xenosrd.dll","rexgpu-xenosrd.lib","TracyClientrd.dll"}
    if set(manifest["files"]) != allowed: raise ValueError("Unexpected renderer package contents")
    for name, expected in manifest["files"].items():
        if hashlib.sha256((renderer / name).read_bytes()).hexdigest() != expected: raise ValueError("Renderer checksum mismatch: " + name)
    original = PROJECT / ".local/toolchain/rexglue-setup-0.10.0-1/win-amd64"
    destination = PROJECT / ".local/toolchain/rexglue-spatial-setup-0.10.0-1/win-amd64"
    receipt = destination / "renderer-capabilities.json"
    if receipt.exists() and receipt.read_bytes() == marker.read_bytes(): return destination
    shutil.copytree(filesystem_path(original),filesystem_path(destination),dirs_exist_ok=True)
    for name in allowed:
        target = filesystem_path(destination / ("bin" if name.endswith(".dll") else "lib") / name)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(filesystem_path(renderer / name), target)
    shutil.copy2(marker,receipt)
    return destination

if __name__ == "__main__": print(prepare())
