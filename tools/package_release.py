"""Package an allowlisted, game-data-free Windows launcher kit."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys
import zipfile

# Portable embedded Python does not add the script directory to sys.path.
sys.path.insert(0, str(Path(__file__).resolve().parent))

ROOT = Path(__file__).resolve().parents[1]
FILES = ["README.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "CONTRIBUTING.md", "CHANGELOG.md",
         ".gitignore", ".gitattributes", ".editorconfig", ".clang-format", "release.json", "CMakeLists.txt",
         "CMakePresets.json", "enchantedarmsrecomp_manifest.toml", "toolchain.lock.json",
         "generated/rexglue.cmake", "renderer/README.md", "bootstrap/README.md", "docs/images/launcher.png",
         "launcher/branding/EnchantedArmsPC.png", "launcher/branding/EnchantedArmsPC.ico"]
BINARY_ARTWORK = {"docs/images/launcher.png", "launcher/branding/EnchantedArmsPC.png", "launcher/branding/EnchantedArmsPC.ico"}
DIRECTORIES = ["src", "config", "launcher", "scripts", "tools", "docs", "licenses", "data", ".github", "renderer/patches"]
ALLOWED_SUFFIXES = {".md", ".txt", ".cpp", ".h", ".cs", ".manifest", ".ps1", ".py", ".toml", ".json", ".yml", ".yaml", ".cmake"}


def source_files(root=ROOT):
    result = [root / name for name in FILES]
    for name in DIRECTORIES:
        directory = root / name
        if directory.exists():
            for path in directory.rglob("*"):
                if path.is_file() and "__pycache__" not in path.parts and path.suffix in ALLOWED_SUFFIXES:
                    result.append(path)
    return sorted(set(result))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--launcher", type=Path, help="Compiled launcher to package (may be staged while the installed launcher is open)")
    options = parser.parse_args()
    destination = ROOT / "dist"
    destination.mkdir(exist_ok=True)
    release = json.loads((ROOT / "release.json").read_text(encoding="utf-8"))
    if not re.fullmatch(r"\d+\.\d+\.\d+", release["version"]):
        raise ValueError("Invalid release version")
    launcher = options.launcher.resolve() if options.launcher else ROOT / "EnchantedArmsLauncher.exe"
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
        relative = Path("EnchantedArmsLauncher.exe") if path == launcher else path.relative_to(ROOT)
        if any(part in {".local", "audit", "bin", "default", "private", "assets", "__pycache__"} for part in relative.parts):
            raise ValueError("Private path selected: " + str(relative))
        if path not in {launcher,python_archive} and relative.as_posix() not in BINARY_ARTWORK and path.suffix not in {".dll", ".lib"}:
            contents = path.read_text(encoding="utf-8-sig")
            if ("C:" + chr(92) + "Users" + chr(92)) in contents or ("C:" + "/Users/") in contents:
                raise ValueError("Personal absolute path in source: " + str(relative))
    archive = destination / f"EnchantedArmsPC-{release['version']}-windows-x64.zip"
    hashes = {}
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for path in sorted(paths, key=lambda p: "EnchantedArmsLauncher.exe" if p == launcher else p.relative_to(ROOT).as_posix()):
            name = "EnchantedArmsLauncher.exe" if path == launcher else path.relative_to(ROOT).as_posix()
            data = path.read_bytes()
            hashes[name] = hashlib.sha256(data).hexdigest()
            write_member(z, "EnchantedArmsRecomp/" + name, data)
        write_member(z, "EnchantedArmsRecomp/SHA256SUMS.json", (json.dumps(hashes, indent=2) + "\n").encode())
    from validate_package import validate
    validate(archive)
    with archive.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    (destination / "SHA256SUMS.txt").write_text(digest + "  " + archive.name + "\n", encoding="utf-8")
    print(f"Packaged {len(paths)} allowlisted files: {archive.name} ({archive.stat().st_size:,} bytes)")


def write_member(archive, name, data):
    # Identical input files produce the same archive, regardless of packaging time.
    member = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
    member.compress_type = zipfile.ZIP_DEFLATED
    member.create_system = 3
    member.external_attr = 0o100644 << 16
    archive.writestr(member, data, compresslevel=9)


if __name__ == "__main__":
    main()
