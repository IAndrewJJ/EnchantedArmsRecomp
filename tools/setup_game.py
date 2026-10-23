"""Local-only ISO validation, extraction, pinned toolchain setup and recompilation."""
import argparse
import errno
import hashlib
import http.client
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import time
import traceback
import urllib.error
import urllib.request
import zipfile

# The embeddable Python distribution isolates imports from the script directory.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from inspect_disc import Disc, inspect_xex

PROJECT = Path(__file__).resolve().parents[1]
XEX_SHA256 = "79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22"


class SetupError(ValueError):
    """An actionable setup failure, with the original exception retained in the log."""


def windows_open_error(path):
    """Recover Windows' read-open error lost by the C runtime's errno mapping.

    This only diagnoses an error. It never reads a blocked file, changes security
    settings, or replaces the normal verified download/extraction path.
    """
    if os.name != "nt":
        return None
    import ctypes
    from ctypes import wintypes
    api = ctypes.WinDLL("kernel32", use_last_error=True)
    api.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                               wintypes.LPVOID, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    api.CreateFileW.restype = wintypes.HANDLE
    api.CloseHandle.argtypes = [wintypes.HANDLE]
    api.CloseHandle.restype = wintypes.BOOL
    handle = api.CreateFileW(str(path), 0x80000000, 7, None, 3, 0x80, None)
    if handle == ctypes.c_void_p(-1).value:
        return ctypes.get_last_error()
    api.CloseHandle(handle)
    return None


def file_operation(action, path, operation, attempts=4):
    # Windows can briefly deny access while a newly written download is being inspected
    # or another process holds it. Retry the same operation, never bypass an access rule.
    for attempt in range(attempts):
        try:
            return operation()
        except OSError as error:
            winerror = getattr(error, "winerror", None)
            if winerror is None and error.errno in (errno.EINVAL, errno.EACCES):
                try:
                    winerror = windows_open_error(path)
                except OSError:
                    pass  # Diagnostics must not hide the original failure.
            if winerror in (225, 226):
                raise SetupError(
                    "Windows security software blocked a required build tool: " + str(path) +
                    f" (winerror={winerror}). "
                    "Check Windows Security > Virus & threat protection > Protection history "
                    "for the detection name. Use the latest complete Windows kit, and report "
                    "a remaining block with this setup log. Setup stopped without installing the blocked tool.") from error
            transient = (winerror in (5, 32, 33) or
                         error.errno in (errno.EACCES, errno.EBUSY, errno.EINVAL))
            if transient and attempt < attempts - 1:
                say(f"Retrying {action} ({attempt + 1}/{attempts - 1}): {Path(path).name}")
                time.sleep(1 << attempt)
                continue
            codes = f"errno={error.errno}"
            if winerror is not None:
                codes += f", winerror={winerror}"
            affected = f" Affected file: {error.filename}." if error.filename and str(error.filename) != str(path) else ""
            raise SetupError(f"Could not {action}: {path} ({codes})." + affected + " "
                             "Check that the folder is writable and close programs using this file, then retry Build game. "
                             "Verified downloads and extracted game files will be reused. "
                             "The local setup log contains the underlying error.") from error


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


def filesystem_path(path):
    """Use Windows' extended path syntax for deep SDK archive members.

    The embedded interpreter must work without changing LongPathsEnabled or
    requiring administrator access. Validate containment before calling this.
    """
    path = Path(path).resolve()
    value = str(path)
    if os.name == "nt" and not value.startswith("\\\\?\\"):
        value = "\\\\?\\UNC\\" + value[2:] if value.startswith("\\\\") else "\\\\?\\" + value
        return Path(value)
    return path


def unpack_zip(archive, destination, strip_contents=False):
    destination = Path(destination)
    with zipfile.ZipFile(filesystem_path(archive)) as source:
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
            target = filesystem_path(checked_member(destination, name))
            target.parent.mkdir(parents=True, exist_ok=True)
            with source.open(member) as reader, target.open("wb") as writer:
                shutil.copyfileobj(reader, writer, 2 * 1024 * 1024)


def download(package, cache):
    cache = Path(cache)
    target = cache / package["name"]

    def verified(path):
        def check():
            try:
                size = path.stat().st_size
            except FileNotFoundError:
                return False
            return (("bytes" not in package or size == package["bytes"]) and
                    digest(path, package["algorithm"]) == package["digest"])
        return file_operation("verify cached download", path, check)

    if verified(target):
        say("Verified cached " + target.name)
        return target
    temporary = target.with_name(target.name + ".partial")
    # A previous download may have completed but failed during Windows' final rename.
    # Recheck its size and digest before promotion; never trust the filename alone.
    if verified(temporary):
        say("Recovered verified download " + target.name)
        file_operation("finalize verified download", target, lambda: temporary.replace(target))
        return target
    request = urllib.request.Request(package["url"], headers={"User-Agent": "EnchantedArmsRecomp-Setup"})
    say("Downloading " + target.name)
    for attempt in range(3):
        count, last = 0, time.monotonic()
        hasher = hashlib.new(package["algorithm"])
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                with file_operation("create download", temporary, lambda: temporary.open("wb")) as output:
                    while block := response.read(2 * 1024 * 1024):
                        # A failed write may already have written some bytes; don't replay it.
                        file_operation("write download", temporary, lambda: output.write(block), attempts=1)
                        hasher.update(block)
                        count += len(block)
                        if count > package["bytes"]:
                            raise SetupError("Download exceeded the pinned size: " + target.name)
                        if time.monotonic() - last > 5:
                            say(f"  {count // 1048576} / {package['bytes'] // 1048576} MiB")
                            last = time.monotonic()
        except (urllib.error.URLError, TimeoutError, ConnectionError, http.client.HTTPException) as error:
            retryable = not isinstance(error, urllib.error.HTTPError) or error.code in (408, 429, 500, 502, 503, 504)
            if retryable and attempt < 2:
                say(f"Download interrupted; retrying {target.name} ({attempt + 1}/2)")
                time.sleep(1 << attempt)
                continue
            raise SetupError("Could not download " + target.name + ": " + str(error) +
                             ". Check your internet connection and retry Build game.") from error
        if count != package["bytes"] or hasher.hexdigest() != package["digest"]:
            raise SetupError("Download verification failed: " + target.name +
                             ". No unverified tool was installed. Retry Build game to download it again.")
        break
    # Read back the completed file before it can become an installable cache entry.
    if not verified(temporary):
        raise SetupError("Download verification failed on disk: " + target.name)
    file_operation("finalize verified download", target, lambda: temporary.replace(target))
    return target


def package_ready(package, root):
    required = [package["probe"], *package.get("required", [])]
    if not all((root / name).is_file() for name in required):
        return False
    if "version" in package:
        probe = root / package["probe"]
        try:
            result = subprocess.run([str(probe), "--version"], capture_output=True, text=True,
                                    check=True, timeout=30)
        except (OSError, subprocess.SubprocessError) as error:
            if isinstance(error, subprocess.CalledProcessError) and error.returncode & 0xFFFFFFFF == 0xC0000135:
                raise SetupError("The SDK cannot load a required DLL. Install or repair the Microsoft Visual C++ "
                                 "v14 x64 Redistributable from Microsoft's official download page, then retry Build game. "
                                 "See docs/SETUP_TROUBLESHOOTING.md for the link.") from error
            raise SetupError("The installed SDK could not run: " + str(probe) +
                             ". See the local setup log for the Windows error.") from error
        if result.stdout.strip() != package["version"]:
            raise SetupError("Unexpected SDK version: " + result.stdout.strip())
    return True


def install_package(package, root):
    cache = root / "downloads"
    cache.mkdir(parents=True, exist_ok=True)
    archive = download(package, cache)
    destination = root / package["destination"]
    say("Installing " + package["name"])

    def extract():
        if package["kind"] == "file":
            destination.mkdir(parents=True, exist_ok=True)
            shutil.copy2(archive, destination / package["name"])
        elif package["kind"] == "zip":
            unpack_zip(archive, destination, package.get("strip_contents", False))
        else:
            extractor = root / package["extractor"]
            subprocess.run([str(extractor), "x", "-y", "-bd", "-bso0", "-bsp0",
                            "-o" + str(destination), str(archive)], check=True)

    file_operation("extract tool package", archive, extract)
    if not package_ready(package, root):
        raise SetupError("Package did not produce all required files: " + package["name"])
    receipt = root / ".receipts" / (package["name"] + ".json")
    receipt.parent.mkdir(parents=True, exist_ok=True)
    file_operation("record completed tool installation", receipt,
                   lambda: receipt.write_text(package["digest"], encoding="utf-8"))


def prepare_toolchain(local):
    say("STAGE: Preparing verified build tools")
    root = local / "toolchain"
    cache = root / "downloads"
    cache.mkdir(parents=True, exist_ok=True)
    packages = json.loads((PROJECT / "toolchain.lock.json").read_text(encoding="utf-8"))
    for index, package in enumerate(packages, 1):
        say(f"STAGE: Preparing build tools ({index}/{len(packages)})")
        # Completion receipts prevent re-extracting large packages on a retry.
        receipt = root / ".receipts" / (package["name"] + ".json")
        if receipt.exists() and receipt.read_text() == package["digest"] and package_ready(package, root):
            say("Ready: " + package["name"])
            continue
        if "version" in package and package_ready(package, root):
            # Existing development installations are usable without their original archive.
            # Let Windows enforce access to the executable; never alter security settings.
            say("Using installed ReXGlue SDK " + package["version"])
            continue
        install_package(package, root)


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
                prepare_toolchain(local)
                extract_game(disc, entries, local)
            finally:
                disc.stream.close()
        else:
            prepare_toolchain(local)
        if args.tools_only:
            return
        say("STAGE: Generating and compiling native code (this can take a while)")
        powershell = str(Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe")
        subprocess.run([powershell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                        str(PROJECT / "scripts/build.ps1"), "-Jobs", str(args.jobs)], cwd=PROJECT, check=True)
        # Written only after successful staging; existing saves/caches are never removed.
        release = json.loads((PROJECT / "release.json").read_text(encoding="utf-8"))
        receipt = local / "installation.json"
        temporary = receipt.with_suffix(".json.partial")
        temporary.write_text(json.dumps({"xex_sha256": XEX_SHA256,
            "build": release["native_version"], "launcher": release["version"],
            "completed_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())}, indent=2), encoding="utf-8")
        temporary.replace(receipt)
        say("STAGE: Ready to play")


if __name__ == "__main__":
    try:
        main()
    except (Exception, KeyboardInterrupt) as error:
        traceback.print_exc(file=sys.stdout)
        say("SETUP FAILED: " + str(error))
        sys.exit(1)
