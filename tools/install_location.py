"""Prepare an owned, separate portable installation without moving original data."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_paths import filesystem_path
from package_release import source_files
from validate_package import validate_name, RENDERER_FILES

PROJECT = "EnchantedArmsPC"
OWNER = Path(".local/build-location-owner.json")
IDENTITY = Path(".local/installation-id.json")


def canonical(path):
    raw = Path(os.path.abspath(path))
    for part in (raw, *raw.parents):
        if part.is_symlink() or (part.exists() and getattr(part, "is_junction", lambda: False)()):
            raise ValueError("Choose a regular folder, without linked or redirected parent folders.")
    return raw


def related(a, b):
    return a == b or a in b.parents or b in a.parents


def digest(path):
    with filesystem_path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def atomic_json(path, value):
    canonical(path)
    path = filesystem_path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".partial")
    canonical(temporary)
    temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def source_id(source):
    path = canonical(source / IDENTITY)
    if path.exists():
        value = json.loads(path.read_text(encoding="utf-8"))["id"]
        uuid.UUID(value)
        return value
    value = str(uuid.uuid4())
    atomic_json(path, {"id": value})
    return value


def validate_destination(source, destination):
    source, destination = canonical(source), canonical(destination)
    if not str(destination).isascii():
        raise ValueError("The current build tools need a folder path without accented or non-Latin characters. Choose a folder such as C:\\Games\\EnchantedArmsPC.")
    if source == destination:
        return source, destination
    if related(source, destination):
        raise ValueError("Choose a separate folder outside the current launcher folder.")
    protected = {Path(destination.anchor), Path.home(), Path.home() / "Desktop",
                 Path.home() / "Documents", Path.home() / "Downloads"}
    for name in ("SystemRoot", "ProgramFiles", "ProgramFiles(x86)", "APPDATA", "LOCALAPPDATA", "TEMP"):
        if os.environ.get(name):
            protected.add(Path(os.environ[name]))
    if destination in protected:
        raise ValueError("Choose a dedicated EnchantedArmsPC folder, rather than a shared system or personal folder.")
    if destination.exists():
        if not destination.is_dir():
            raise ValueError("The build location must be a folder.")
        if any(destination.iterdir()):
            try:
                canonical(destination / OWNER)
                canonical(source / IDENTITY)
                owner = json.loads((destination / OWNER).read_text(encoding="utf-8"))
                own_id = json.loads((source / IDENTITY).read_text(encoding="utf-8"))["id"]
                if owner["project"] != PROJECT or owner["source_id"] != own_id:
                    raise ValueError("different installation")
            except (OSError, ValueError, KeyError, TypeError) as error:
                raise ValueError("Choose an empty folder, or the build folder previously created by this launcher.") from error
    return source, destination


def kit_files(source):
    lock = json.loads((source / "launcher/python.lock.json").read_text(encoding="utf-8"))
    renderer = json.loads((source / "renderer/renderer-capabilities.json").read_text(encoding="utf-8"))
    if set(renderer["files"]) != RENDERER_FILES:
        raise ValueError("Unexpected renderer files.")
    files = source_files(source) + [source / "EnchantedArmsLauncher.exe",
        source / "bootstrap" / lock["name"], source / "renderer/renderer-capabilities.json"]
    files += [source / "renderer" / name for name in sorted(RENDERER_FILES)]
    for path in files:
        validate_name(path.relative_to(source).as_posix())
        canonical(path)
        if not path.is_file():
            raise ValueError("The launcher kit is incomplete: " + path.relative_to(source).as_posix())
    if digest(source / "bootstrap" / lock["name"]) != lock["sha256"]:
        raise ValueError("Bundled Python checksum mismatch.")
    for name, expected in renderer["files"].items():
        if digest(source / "renderer" / name) != expected:
            raise ValueError("Renderer checksum mismatch: " + name)
    return sorted(set(files))


def copy_file(source, target, preserve=False):
    canonical(target)
    target = filesystem_path(target)
    if target.exists():
        if preserve or digest(source) == digest(target):
            return
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(target.name + ".install-partial")
    canonical(temporary)
    shutil.copyfile(filesystem_path(source), temporary)
    temporary.replace(target)


def import_private(source, destination):
    # Saves and packs are copied, never moved or overwritten. Shader/build caches
    # contain machine- and path-specific state and are regenerated at the new location.
    for relative in (Path(".local/runtime/user"), Path(".local/texture-packs")):
        root = source / relative
        if not root.exists():
            continue
        for path in sorted(root.rglob("*")):
            canonical(path)
            if path.is_file():
                copy_file(path, destination / path.relative_to(source), preserve=True)


def prepare(source, destination):
    source, destination = validate_destination(source, destination)
    if source == destination:
        return destination
    files = kit_files(source)  # Check the complete input before claiming a new folder.
    identity = source_id(source)
    owner_path = canonical(destination / OWNER)
    owner = (json.loads(owner_path.read_text(encoding="utf-8")) if owner_path.exists() else
             {"project": PROJECT, "source_id": identity, "user_imported": False})
    destination.mkdir(parents=True, exist_ok=True)
    atomic_json(owner_path, owner)
    for path in files:
        copy_file(path, destination / path.relative_to(source))
    preferences = source / ".local/launcher.json"
    if preferences.exists():
        settings = json.loads(preferences.read_text(encoding="utf-8-sig"))
        # The destination is now a complete portable installation. Deriving its
        # own location from the EXE keeps it portable if the folder is moved later.
        settings["BuildLocation"] = ""
        atomic_json(destination / ".local/launcher.json", settings)
    if not owner.get("user_imported"):
        import_private(source, destination)
        owner["user_imported"] = True
        atomic_json(owner_path, owner)
    return destination


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    args = parser.parse_args()
    try:
        print("STAGE: Preparing your build folder", flush=True)
        destination = prepare(args.source, args.destination)
        print("Build folder: " + str(destination), flush=True)
    except (Exception, KeyboardInterrupt) as error:
        print("SETUP FAILED: " + str(error), flush=True)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
