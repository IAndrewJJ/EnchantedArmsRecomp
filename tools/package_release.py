"""Package an allowlisted, game-data-free Windows launcher kit."""
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
FILES = ["README.md", "LICENSE", "THIRD_PARTY_NOTICES.md", ".gitignore", ".gitattributes", "CMakeLists.txt",
         "CMakePresets.json", "enchantedarmsrecomp_manifest.toml", "toolchain.lock.json",
         "generated/rexglue.cmake", "renderer/README.md", "bootstrap/README.md"]
DIRECTORIES = ["src", "config", "launcher", "scripts", "tools", "docs", "licenses", "data", ".github", "renderer/patches"]
ALLOWED_SUFFIXES = {".md", ".txt", ".cpp", ".h", ".cs", ".manifest", ".ps1", ".py", ".toml", ".json", ".yml", ".yaml", ".cmake"}


def source_files():
    result = [ROOT / name for name in FILES]
    for name in DIRECTORIES:
        directory = ROOT / name
        if directory.exists():
            for path in directory.rglob("*"):
                if path.is_file() and "__pycache__" not in path.parts and path.suffix in ALLOWED_SUFFIXES:
                    result.append(path)
    return sorted(set(result))


def main():
    destination = ROOT / "dist"
    destination.mkdir(exist_ok=True)
    launcher = ROOT / "EnchantedArmsLauncher.exe"
    if not launcher.exists():
        raise SystemExit("Run scripts/build-launcher.ps1 first")
    paths = source_files() + [launcher]
    python_lock = json.loads((ROOT / "launcher/python.lock.json").read_text())
    python_archive = ROOT / "bootstrap" / python_lock["name"]
    if not python_archive.exists():
        cached = ROOT / ".local/downloads" / python_lock["name"]
        if not cached.exists(): raise ValueError("The pinned Python bootstrap archive is required for a complete release kit")
        if hashlib.sha256(cached.read_bytes()).hexdigest() != python_lock["sha256"]: raise ValueError("Python bootstrap checksum mismatch")
        shutil.copy2(cached,python_archive)
    if python_archive.stat().st_size != python_lock["bytes"] or hashlib.sha256(python_archive.read_bytes()).hexdigest() != python_lock["sha256"]:
        raise ValueError("Python bootstrap checksum mismatch")
    paths.append(python_archive)
    renderer = ROOT / "renderer"
    manifest = json.loads((renderer / "renderer-capabilities.json").read_text())
    allowed_renderer_files = {"rexruntimerd.dll","rexruntimerd.lib","rexgpu-xenosrd.dll","rexgpu-xenosrd.lib","TracyClientrd.dll"}
    if set(manifest["files"]) != allowed_renderer_files: raise ValueError("Unexpected renderer binaries")
    for name, expected in manifest["files"].items():
        path = renderer / name
        if hashlib.sha256(path.read_bytes()).hexdigest() != expected: raise ValueError("Renderer checksum mismatch: " + name)
        paths.append(path)
    paths.append(renderer / "renderer-capabilities.json")
    # Explicit allowlist means new audit/generated/runtime files cannot slip into a release.
    for path in paths:
        relative = path.relative_to(ROOT)
        if any(part in {".local", "audit", "bin", "default", "private", "assets", "__pycache__"} for part in relative.parts):
            raise ValueError("Private path selected: " + str(relative))
        if path not in {launcher,python_archive} and path.suffix not in {".dll", ".lib"}:
            contents = path.read_text(encoding="utf-8-sig")
            if ("C:" + chr(92) + "Users" + chr(92)) in contents or ("C:" + "/Users/") in contents:
                raise ValueError("Personal absolute path in source: " + str(relative))
    archive = destination / "EnchantedArmsPC-0.3.0-windows-x64.zip"
    hashes = {}
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for path in paths:
            name = path.relative_to(ROOT).as_posix()
            data = path.read_bytes()
            hashes[name] = hashlib.sha256(data).hexdigest()
            z.writestr("EnchantedArmsRecomp/" + name, data)
        z.writestr("EnchantedArmsRecomp/SHA256SUMS.json", json.dumps(hashes, indent=2) + "\n")
    (destination / "SHA256SUMS.txt").write_text(hashlib.sha256(archive.read_bytes()).hexdigest() + "  " + archive.name + "\n")
    print(f"Packaged {len(paths)} allowlisted files: {archive.name} ({archive.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
