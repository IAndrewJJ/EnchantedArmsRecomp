"""Prepare an owned, separate portable installation without moving original data."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
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
    protected = {Path(destination.anchor), Path.home(), Path.home() / "Desktop",
                 Path.home() / "Documents", Path.home() / "Downloads"}
    for name in ("SystemRoot", "ProgramFiles", "ProgramFiles(x86)", "APPDATA", "LOCALAPPDATA", "TEMP"):
        if os.environ.get(name):
            protected.add(Path(os.environ[name]))
    if destination in protected:
        raise ValueError("Choose a dedicated EnchantedArmsPC folder, rather than a shared system or personal folder.")
    if destination == source.parent and project_folder(source):
        destination = source
    elif not project_folder(destination) and destination.is_dir():
        projects = [child for child in destination.iterdir() if child.is_dir() and project_folder(child)]
        if len(projects) > 1:
            raise ValueError("This folder contains several copies. Choose the EnchantedArmsPC folder you want to use.")
        if projects:
            destination = projects[0]
    if not str(destination).isascii():
        raise ValueError("The current build tools need a folder path without accented or non-Latin characters. Choose a folder such as C:\\Games\\EnchantedArmsPC.")
    if source == destination:
        return source, destination
    if related(source, destination):
        raise ValueError("Choose a separate folder outside the current launcher folder.")
    if destination.exists():
        if not destination.is_dir():
            raise ValueError("The build location must be a folder.")
        if any(destination.iterdir()) and not (project_folder(destination) and not (destination / OWNER).exists()):
            try:
                canonical(destination / OWNER)
                canonical(source / IDENTITY)
                owner = json.loads((destination / OWNER).read_text(encoding="utf-8"))
                own_id = json.loads((source / IDENTITY).read_text(encoding="utf-8"))["id"]
                if owner["project"] != PROJECT or owner["source_id"] != own_id:
                    raise ValueError("different installation")
            except (OSError, ValueError, KeyError, TypeError) as error:
                raise ValueError("Choose this launcher's folder, an existing EnchantedArmsPC project, or an empty game folder.") from error
    return source, destination


def project_folder(path):
    try:
        for name in ("release.json", "enchantedarmsrecomp_manifest.toml", "scripts/setup.ps1"):
            canonical(path / name)
        release = json.loads((path / "release.json").read_text(encoding="utf-8-sig"))
        return (bool(re.fullmatch(r"\d+\.\d+\.\d+", release["version"])) and
                (path / "scripts/setup.ps1").is_file() and
                'name = "enchantedarmsrecomp"' in (path / "enchantedarmsrecomp_manifest.toml").read_text(encoding="utf-8-sig"))
    except (OSError, ValueError, KeyError, TypeError):
        return False


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


def copy_private_tree(source, destination):
    if not source.exists():
        return
    canonical(source)
    for path in sorted(source.rglob("*")):
        canonical(path)
        if path.is_file():
            copy_file(path, destination / path.relative_to(source), preserve=True)


def import_private(source, destination, settings):
    # Saves and packs are copied, never moved or overwritten. Shader/build caches
    # contain machine- and path-specific state and are regenerated at the new location.
    copy_private_tree(source / ".local/texture-packs", destination / ".local/texture-packs")
    # An existing installation's own save selection and progress take priority.
    # In particular, do not seed Saves from the source over an older target's
    # legacy save tree, which the updated launcher will migrate on startup.
    if (destination / ".local/launcher.json").exists() or any(
        root.exists() and any(root.iterdir())
        for root in (destination / "Saves", destination / ".local/runtime/user")
    ):
        return
    custom = settings.get("SaveLocation", "")
    if custom:
        # An explicitly selected external save folder stays where the player put
        # it. A custom folder inside the source follows the portable installation.
        save_root = canonical(Path(custom))
        if save_root.is_relative_to(source):
            relative = save_root.relative_to(source)
            if not relative.parts or relative.parts[0] in {".local", "bin", ".git"}:
                raise ValueError("Choose a dedicated save folder outside program files.")
            copy_private_tree(save_root, destination / relative)
            settings["SaveLocation"] = str(destination / relative)
    else:
        save_root = source / "Saves"
        if not save_root.exists():
            save_root = source / ".local/runtime/user"
        copy_private_tree(save_root, destination / "Saves")


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
    settings = (json.loads(preferences.read_text(encoding="utf-8-sig")) if preferences.exists() else {})
    if not owner.get("user_imported"):
        import_private(source, destination, settings)
        owner["user_imported"] = True
    if not (destination / ".local/launcher.json").exists():
        # The destination is now a complete portable installation. Deriving its
        # own location from the EXE keeps it portable if the folder is moved later.
        settings["BuildLocation"] = ""
        atomic_json(destination / ".local/launcher.json", settings)
    atomic_json(owner_path, owner)
    return destination


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--resolve-only", action="store_true")
    args = parser.parse_args()
    try:
        if args.resolve_only:
            print(validate_destination(args.source, args.destination)[1], flush=True)
            return 0
        print("STAGE: Preparing your build folder", flush=True)
        destination = prepare(args.source, args.destination)
        print("Build folder: " + str(destination), flush=True)
    except (Exception, KeyboardInterrupt) as error:
        print("SETUP FAILED: " + str(error), flush=True)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
