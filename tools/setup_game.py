"""Local-only ISO validation, extraction, pinned toolchain setup and recompilation."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import time
import urllib.request
import zipfile

# The embeddable Python distribution isolates imports from the script directory.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from inspect_disc import Disc, inspect_xex

PROJECT = Path(__file__).resolve().parents[1]
XEX_SHA256 = "79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22"


def say(message):
    print(message, flush=True)


def digest(path, algorithm="sha256"):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, algorithm).hexdigest()


def checked_member(destination, name):
    name = name.replace("\\", "/")
    parts = PurePosixPath(name).parts
    if not parts or name.startswith("/") or any(p in ("..", ".") or ":" in p for p in parts):
        raise ValueError("Unsafe archive member: " + name)
    target = destination.joinpath(*parts).resolve()
    if not target.is_relative_to(destination.resolve()):
        raise ValueError("Archive member escapes destination: " + name)
    return target


def unpack_zip(archive, destination, strip_contents=False):
    destination = Path(destination)
    with zipfile.ZipFile(archive) as source:
        # Check every name before writing anything.
        for member in source.infolist():
            checked_member(destination, member.filename)
        for member in source.infolist():
            name = member.filename.replace("\\", "/")
            if strip_contents:
                if not name.startswith("Contents/"):
                    continue
                name = name[len("Contents/"):]
            if not name or member.is_dir():
                continue
            target = checked_member(destination, name)
            target.parent.mkdir(parents=True, exist_ok=True)
            with source.open(member) as reader, target.open("wb") as writer:
                shutil.copyfileobj(reader, writer, 2 * 1024 * 1024)


def download(package, cache):
    target = cache / package["name"]
    try:
        cached_digest = digest(target, package["algorithm"]) if target.exists() else None
    except OSError as error:
        raise ValueError("Windows refused access to the cached download " + target.name +
                         ". The file may be blocked by Windows security. Setup stopped without changing security settings.") from error
    if cached_digest == package["digest"]:
        say("Verified cached " + target.name)
        return target
    temporary = target.with_name(target.name + ".partial")
    request = urllib.request.Request(package["url"], headers={"User-Agent": "EnchantedArmsRecomp/0.1"})
    say("Downloading " + target.name)
    count, last = 0, time.monotonic()
    hasher = hashlib.new(package["algorithm"])
    with urllib.request.urlopen(request, timeout=60) as response, temporary.open("wb") as output:
        while block := response.read(2 * 1024 * 1024):
            output.write(block)
            hasher.update(block)
            count += len(block)
            if time.monotonic() - last > 5:
                say(f"  {count // 1048576} / {package['bytes'] // 1048576} MiB")
                last = time.monotonic()
    if count != package["bytes"] or hasher.hexdigest() != package["digest"]:
        raise ValueError("Download verification failed: " + target.name)
    temporary.replace(target)
    return target


def prepare_toolchain(local):
    say("STAGE: Preparing verified build tools")
    root = local / "toolchain"
    cache = root / "downloads"
    cache.mkdir(parents=True, exist_ok=True)
    packages = json.loads((PROJECT / "toolchain.lock.json").read_text(encoding="utf-8"))
    for package in packages:
        # Completion receipts prevent re-extracting large packages on a retry.
        receipt = root / ".receipts" / (package["name"] + ".json")
        probe = root / package["probe"]
        if receipt.exists() and probe.exists() and receipt.read_text() == package["digest"]:
            say("Ready: " + package["name"])
            continue
        if package["name"].startswith("rexglue-sdk-") and probe.exists():
            # Existing development installations are usable without their original archive.
            # Let Windows enforce access to the executable; never alter security settings.
            sdk = root / package["destination"] / "win-amd64"
            if (sdk / "lib/cmake/rexglue/rexglueConfig.cmake").exists():
                version = subprocess.run([str(probe), "--version"], capture_output=True, text=True, check=True).stdout.strip()
                if version == "0.10.0":
                    say("Using installed ReXGlue SDK " + version)
                    continue
        archive = download(package, cache)
        destination = root / package["destination"]
        if package["kind"] == "file":
            destination.mkdir(parents=True, exist_ok=True)
            shutil.copy2(archive, destination / package["name"])
        elif package["kind"] == "zip":
            unpack_zip(archive, destination, package.get("strip_contents", False))
        else:
            extractor = root / package["extractor"]
            subprocess.run([str(extractor), "x", "-y", "-bd", "-bso0", "-bsp0",
                            "-o" + str(destination), str(archive)], check=True)
        if not probe.exists():
            raise ValueError("Package did not produce expected file: " + package["probe"])
        receipt.parent.mkdir(parents=True, exist_ok=True)
        receipt.write_text(package["digest"])


def validate_disc(disc):
    entries = disc.inventory()
    executable = next((e for e in entries if e["path"].casefold() == "default.xex" and not e["directory"]), None)
    if executable is None or executable["bytes"] > 32 * 1024 * 1024:
        raise ValueError("The ISO does not contain a supported default.xex")
    data = disc.read(executable["iso_offset"], executable["bytes"])
    actual = hashlib.sha256(data).hexdigest()
    if actual != XEX_SHA256:
        raise ValueError("Unsupported game revision. This preview requires the USA/Europe Xbox 360 "
                         "release (title 555307E6, media 41637F65). Found XEX SHA-256: " + actual)
    info = inspect_xex(data)["execution_info"]
    say("Supported Enchanted Arms revision: " + info["title_id"] + " / " + info["media_id"])
    return entries


def extract_game(disc, entries, local):
    say("STAGE: Extracting and verifying your game (ISO opened read-only)")
    destination = local / "game"
    expected_bytes = sum(e["bytes"] for e in entries if not e["directory"])
    # Existing verified files need no second allocation. Leave room for compiler outputs/tools.
    needed = sum(e["bytes"] for e in entries if not e["directory"] and
                 not destination.joinpath(*PurePosixPath(e["path"]).parts).exists())
    if shutil.disk_usage(local).free < needed + 8 * 1024**3:
        raise ValueError("At least 8 GiB free beyond the extracted game is needed for build tools and compilation.")
    records, completed, last = [], 0, time.monotonic()
    for index, entry in enumerate(entries):
        record = disc.extract(entry, destination)
        if record:
            records.append({"path": entry["path"], "bytes": record["bytes"], "sha256": record["sha256"]})
            completed += entry["bytes"]
        if time.monotonic() - last > 3:
            say(f"  {completed * 100 // expected_bytes}% verified ({index + 1}/{len(entries)} entries)")
            last = time.monotonic()
    (local / "extraction.json").write_text(json.dumps({"xex_sha256": XEX_SHA256, "files": records}, indent=2))
    say(f"Verified {len(records)} files, {completed:,} bytes")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--iso", type=Path)
    parser.add_argument("--validate-only", action="store_true")
    parser.add_argument("--tools-only", action="store_true")
    parser.add_argument("--jobs", type=int, choices=range(1, 9), default=2)
    args = parser.parse_args()
    if not args.tools_only and not args.iso:
        parser.error("Select your Enchanted Arms Xbox 360 ISO")
    local = PROJECT / ".local"
    local.mkdir(exist_ok=True)
    # Only one setup may modify this installation at a time; OS releases lock after a crash.
    import msvcrt
    with (local / "setup.lock").open("a+b") as lock:
        lock.seek(0)
        try:
            msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
        except OSError:
            raise ValueError("Another setup is running for this installation")
        if args.iso:
            disc = Disc(args.iso)
            try:
                say("STAGE: Checking game revision")
                entries = validate_disc(disc)
                if args.validate_only:
                    return
                extract_game(disc, entries, local)
            finally:
                disc.stream.close()
        prepare_toolchain(local)
        if args.tools_only:
            return
        say("STAGE: Generating and compiling native code (this can take a while)")
        powershell = str(Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe")
        subprocess.run([powershell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                        str(PROJECT / "scripts/build.ps1"), "-Jobs", str(args.jobs)], cwd=PROJECT, check=True)
        # Written only after successful staging; existing saves/caches are never removed.
        (local / "installation.json").write_text(json.dumps({"xex_sha256": XEX_SHA256,
            "build": "0.2.0-graphics-preview", "completed_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())}, indent=2))
        say("STAGE: Ready to play")


if __name__ == "__main__":
    try:
        main()
    except (Exception, KeyboardInterrupt) as error:
        say("SETUP FAILED: " + str(error))
        sys.exit(1)
