"""Archive the corresponding open-source renderer, excluding Git metadata and build products."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tarfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_spatial_renderer import COMMIT, PROJECT, SUBMODULES


def git(git_exe, root, *args):
    return subprocess.run([git_exe, "-c", "safe.directory=" + str(root), "-C", str(root), *args],
                          check=True, capture_output=True).stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--git", default="git")
    args = parser.parse_args()
    source = args.source.resolve()
    if git(args.git, source, "rev-parse", "HEAD").decode().strip() != COMMIT:
        raise ValueError("Unexpected SDK revision")
    provenance = {"sdk_commit": COMMIT, "submodules": {}, "archive_adjustments": [
        "Materialized libmspack symlinks for Windows.",
        "Replaced the Git-metadata submodule check with a directory check; source archive contains no .git metadata.",
        "Applied renderer/patches/texture-detail.json: optional mipmapped-surface detail bias."]}
    selected = set()
    for root in [source] + [source / "thirdparty" / name for name in SUBMODULES]:
        if root != source:
            name = root.name
            actual = git(args.git, root, "rev-parse", "HEAD").decode().strip()
            expected = git(args.git, source, "rev-parse", "HEAD:thirdparty/" + name).decode().strip()
            if actual != expected:
                raise ValueError("Unexpected submodule revision: " + name)
            provenance["submodules"][name] = actual
        for name in git(args.git, root, "ls-files", "-z").decode().split("\0"):
            if name and (root / name).is_file():
                selected.add(root / name)
    destination = PROJECT / "dist"
    destination.mkdir(exist_ok=True)
    archive = destination / "ReXGlueSpatial-0.3.0-source.tar.gz"
    hashes = {}
    prefix = "ReXGlueSpatial-0.3.0/"
    with tarfile.open(archive, "w:gz", compresslevel=6) as output:
        def add(name, data):
            item = tarfile.TarInfo(prefix + name)
            item.size = len(data)
            item.mode = 0o644
            item.mtime = 0
            output.addfile(item, io.BytesIO(data))
            hashes[name] = hashlib.sha256(data).hexdigest()
        for path in sorted(selected):
            name = path.relative_to(source).as_posix()
            if ".git" in Path(name).parts:
                raise ValueError("Git metadata selected")
            data = path.read_bytes()
            if name == "thirdparty/CMakeLists.txt":
                before = b'if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/${submodule}/.git")'
                if before not in data:
                    raise ValueError("Upstream submodule check changed")
                data = data.replace(before, b'if(NOT IS_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/${submodule}")')
            add(name, data)
        add("SOURCE_PROVENANCE.json", (json.dumps(provenance, indent=2) + "\n").encode())
        add("build_spatial_renderer.py", (PROJECT / "tools/build_spatial_renderer.py").read_bytes())
        add("apply_renderer_patches.py", (PROJECT / "tools/apply_renderer_patches.py").read_bytes())
        add("texture-detail.json", (PROJECT / "renderer/patches/texture-detail.json").read_bytes())
        add("BUILDING-SPATIAL.md", b"""# Spatial renderer corresponding source

This archive contains the ReXGlue SDK and the exact Windows submodule revisions used
for the 0.3.0 runtime. SOURCE_PROVENANCE.json records revisions and archive adjustments.
Original sources and their license files are retained. No game data is included.

Install the pinned compiler environment through the EnchantedArmsRecomp launcher
kit, then run in PowerShell from that kit's directory (replace D:/Source with the
actual extracted archive path):

    . ./scripts/environment.ps1
    ./.local/python/python.exe ./tools/build_spatial_renderer.py --offline-source --source D:/Source/ReXGlueSpatial-0.3.0 --rc "$taskSdkBinDirectory/rc.exe" --mt "$taskSdkBinDirectory/mt.exe"

The source archive needs no Git metadata or network downloads for configuration.
Use the build script from the same release kit. It enables spatial FSR/CAS with
REX_HAS_FIDELITYFX_SDK=1 across all C++ targets, SSE4.1, and the existing D3D12 shader
headers. REXGLUE_ENABLE_FIDELITYFX remains OFF (no temporal backend). It remaps
compiler paths. The texture-detail patch adds a sampler bias only for eligible
filtered mipmapped surfaces; point-sampled and base-map requests keep their behavior.
The build script applies that patch idempotently, including to this archive.
It remaps
compiler paths and the PDB reference to avoid embedding developer home paths.
Build type is RelWithDebInfo; targets are rexruntime and rexgpu-xenos. The complete
compiler command lines are also generated in the local Ninja build directory.

The resulting DLLs/import libraries and capability/hash manifest go to renderer/.
Run scripts/build.ps1 to link a local native game build against the new runtime.
This permits rebuilding or modifying the open-source runtime, including its LGPL
components. Microsoft and compiler tools are obtained separately by the kit.
""")
        add("SHA256SUMS.json", (json.dumps(hashes, indent=2) + "\n").encode())
    print(f"Packaged {len(hashes):,} renderer source files: {archive.name} ({archive.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
