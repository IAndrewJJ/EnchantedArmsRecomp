"""Package the source-built, RelWithDebInfo-only SDK used by player setup.

No game files are read. The upstream release ZIP is not an input. The package
contains the code generator and runtime built from the corresponding source
archive, rather than the unused Release/Debug runtime variants.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_spatial_renderer import COMMIT, PROJECT
from package_release import write_member

PACKAGE_NAME = "EnchantedArmsPC-ReXGlue-0.10.0-setup-1.zip"


def portable_install_script(contents):
    # Upstream's generated install script unconditionally registers its prefix in
    # the user's CMake registry. This is a portable package; omit only that block.
    marker = "# Normalize path casing on Windows before hashing to avoid duplicate entries"
    if contents.count(marker) != 1:
        raise ValueError("Upstream install registration block changed")
    before, after = contents.split(marker)
    guard = 'if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)'
    before, removed_guard = before.rsplit(guard, 1)
    if removed_guard.strip():
        raise ValueError("Unexpected registration guard")
    end = '\nendif()\n\nstring(REPLACE ";"'
    registration, tail = after.split(end, 1)
    if 'COMMAND reg add' not in registration or 'file(INSTALL' in registration:
        raise ValueError("Unexpected registration body")
    return before + 'string(REPLACE ";"' + tail


def package(source, build, directory, cmake):
    source, build, directory = source.resolve(), build.resolve(), directory.resolve()
    if (source / "SOURCE_PROVENANCE.json").is_file():
        revision = json.loads((source / "SOURCE_PROVENANCE.json").read_text())["sdk_commit"]
    else:
        revision = subprocess.check_output(["git", "-c", "safe.directory=" + str(source),
                                            "-C", str(source), "rev-parse", "HEAD"], text=True).strip()
    if revision != COMMIT:
        raise ValueError("Unexpected SDK source revision")
    directory.mkdir(parents=True, exist_ok=False)
    prefix = directory / "win-amd64"
    script = directory / "portable-install.cmake"
    script.write_text(portable_install_script((build / "cmake_install.cmake").read_text()), encoding="utf-8")
    subprocess.run([cmake, "-DCMAKE_INSTALL_PREFIX=" + prefix.as_posix(),
                    "-DCMAKE_INSTALL_CONFIG_NAME=RelWithDebInfo", "-P", str(script)], check=True)
    script.unlink()
    # The upstream install rules ship the CLI only for Release. Our optimized
    # code generator uses the same rd runtime as the game instead.
    shutil.copy2(source / "out/win-amd64/rexgluerd.exe", prefix / "bin/rexglue.exe")
    config = prefix / "lib/cmake/rexglue/rexglueConfig.cmake"
    with config.open("a", encoding="utf-8") as output:
        output.write('\nset_target_properties(rex::rexglue PROPERTIES IMPORTED_LOCATION "${PACKAGE_PREFIX_DIR}/bin/rexglue.exe")\n')
    shutil.copytree(PROJECT / "licenses/runtime", prefix / "licenses/runtime")
    shutil.copy2(PROJECT / "licenses/ReXGlue.txt", prefix / "licenses/ReXGlue.txt")
    provenance = {"sdk_commit": COMMIT, "package": "ea-setup-1", "configuration": "RelWithDebInfo",
                  "source_archive": "ReXGlueSpatial-0.3.0-source.tar.gz",
                  "recipe": "tools/build_spatial_renderer.py --setup-sdk",
                  "adjustments": ["Use the source-built optimized rd code generator and runtime.",
                                  "Package RelWithDebInfo only; omit unused Release/Debug variants.",
                                  "Omit global CMake package registration when staging.",
                                  "Expose the code generator's installed executable to CMake."]}
    (prefix / "package-provenance.json").write_text(json.dumps(provenance, indent=2) + "\n")
    blocked_variants = [p for p in prefix.rglob("*") if p.is_file() and
                        p.name in {"rexruntime.dll", "rexruntimed.dll", "rexgpu-xenos.dll", "rexgpu-xenosd.dll"}]
    if blocked_variants:
        raise ValueError("Unexpected non-rd runtime in setup SDK")
    subprocess.run([str(prefix / "bin/rexglue.exe"), "--version"], check=True)
    hashes = {}
    archive = PROJECT / "dist" / PACKAGE_NAME
    archive.parent.mkdir(exist_ok=True)
    with zipfile.ZipFile(archive, "w") as output:
        for path in sorted(prefix.rglob("*")):
            if not path.is_file():
                continue
            name = path.relative_to(directory).as_posix()
            data = path.read_bytes()
            personal_roots = (b"C:" + b"/Users/", b"C:" + bytes([92]) + b"Users" + bytes([92]))
            if path.suffix == ".cmake" and any(value in data for value in personal_roots):
                raise ValueError("Personal path in exported CMake file: " + name)
            hashes[name] = hashlib.sha256(data).hexdigest()
            write_member(output, name, data)
        write_member(output, "SDK-SHA256SUMS.json", (json.dumps(hashes, indent=2) + "\n").encode())
    with archive.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    print(json.dumps({"name": archive.name, "bytes": archive.stat().st_size,
                      "sha256": digest, "files": len(hashes)}), flush=True)
    return archive


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--build", type=Path, required=True)
    parser.add_argument("--directory", type=Path, required=True, help="New staging folder")
    parser.add_argument("--cmake", default="cmake")
    args = parser.parse_args()
    package(args.source, args.build, args.directory, args.cmake)


if __name__ == "__main__":
    main()
