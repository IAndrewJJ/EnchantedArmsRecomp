"""Archive exact runtime sources and initialized submodules without Git metadata."""
from pathlib import Path
import hashlib
import io
import json
import subprocess
import sys
import tarfile

source, output = (Path(p).resolve() for p in sys.argv[1:])
commit = 'f5337cdc947ff6d4c4196737e2c807a48f2a1fc2'

def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args]).decode().strip()

if git(source, 'rev-parse', 'HEAD') != commit:
    raise ValueError('Unexpected runtime source revision')
roots = [source]
provenance = {'sdk_commit': commit, 'submodules': {}, 'adjustments': [
    'Applied linux/patch_sdk.py texture detail patch.',
    'Materialized source symlinks and replaced Git metadata checks for archive builds.',
    'Pinned archive version to the original 0.10.0 release.']}
for line in git(source, 'submodule', 'status').splitlines():
    if not line.startswith('-'):
        parts = line.split()
        root = source/parts[1]
        expected = git(source, 'rev-parse', 'HEAD:' + parts[1])
        if git(root, 'rev-parse', 'HEAD') != expected:
            raise ValueError('Unexpected submodule: ' + parts[1])
        roots.append(root)
        provenance['submodules'][parts[1]] = expected
selected = set()
for root in roots:
    selected.update(root/name for name in git(root, 'ls-files', '-z').split('\0') if name and (root/name).is_file())
hashes = {}
with tarfile.open(output, 'w:xz', preset=3) as archive:
    def add(name, data, mode=0o644):
        item = tarfile.TarInfo('rexglue-sdk/' + name)
        item.size, item.mode, item.mtime = len(data), mode, 0
        archive.addfile(item, io.BytesIO(data))
        hashes[name] = hashlib.sha256(data).hexdigest()
    for path in sorted(selected):
        name = path.relative_to(source).as_posix()
        data = path.read_bytes()
        if '.git' in Path(name).parts:
            raise ValueError('Git metadata selected')
        if name == 'thirdparty/CMakeLists.txt':
            before = b'if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/${submodule}/.git")'
            if before not in data:
                raise ValueError('Upstream submodule check changed')
            data = data.replace(before, b'if(NOT IS_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/${submodule}")')
        if name == 'cmake/rex_version.cmake':
            before = b'function(rex_resolve_version out_var)'
            data = data.replace(before, before + b'\n    set(${out_var} "0.10.0" PARENT_SCOPE)\n    return()')
        add(name, data, 0o755 if path.stat().st_mode & 0o111 else 0o644)
    add('SOURCE_PROVENANCE.json', (json.dumps(provenance, indent=2)+'\n').encode())
    recipe = '''# Rebuild the Linux Vulkan runtime

This is the exact ReXGlue 0.10.0 source and initialized submodules used by the
Linux preview, with the texture-detail patch already applied. No game data is
included. Original licenses are retained. SOURCE_PROVENANCE.json records commits.

Install the dependencies listed in .github/workflows/linux-runtime.yml in the
EnchantedArmsRecomp source release (Ubuntu 22.04, Clang 20, GCC 13 development
libraries, CMake and Ninja). From this extracted source directory run:

    cmake -S . -B ../sdk-build -G Ninja -DCMAKE_BUILD_TYPE=Release \\
      -DCMAKE_INSTALL_PREFIX="$PWD/../sdk" -DCMAKE_C_COMPILER=clang-20 \\
      -DCMAKE_CXX_COMPILER=clang++-20 \\
      '-DCMAKE_CXX_FLAGS=-DREX_HAS_FIDELITYFX_SDK=1 -march=x86-64-v2' \\
      -DCMAKE_C_FLAGS=-march=x86-64-v2 -DREXGLUE_USE_D3D12=OFF \\
      -DREXGLUE_USE_VULKAN=ON -DREXGLUE_ENABLE_FIDELITYFX=OFF -DREXGLUE_BUILD_TESTS=OFF
    cmake --build ../sdk-build --target install --parallel 2
    cp thirdparty/renderdoc/renderdoc_app.h ../sdk/include/renderdoc_app.h

The AppImage keeps these runtime libraries dynamically linked. To use a modified
runtime, extract the AppImage, replace the libraries under
usr/bin/EnchantedArmsLauncher/_internal/tools/sdk/lib, then open AppRun and build
in a fresh game folder. Keep a backup of your saves before testing changes.
'''
    add('BUILDING-LINUX.md', recipe.encode())
    add('SHA256SUMS.json', (json.dumps(hashes, indent=2)+'\n').encode())
print('Runtime source archive:', output.name, output.stat().st_size, 'bytes')
