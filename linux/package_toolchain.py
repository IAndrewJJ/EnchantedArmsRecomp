"""Stage a relocatable compiler/sysroot and SDK dependencies for local ISO builds."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import sys

CORE = {'libc.so.6', 'libm.so.6', 'libdl.so.2', 'libpthread.so.0', 'librt.so.1',
        'ld-linux-x86-64.so.2', 'libresolv.so.2', 'libvulkan.so.1'}

def copy(source, target):
    source, target = Path(source), Path(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    if source.is_dir():
        shutil.copytree(source, target, dirs_exist_ok=True, symlinks=False)
    else:
        shutil.copy2(source, target)

def main(root):
    root.mkdir(parents=True, exist_ok=True)
    copy('/usr/lib/llvm-20/bin/clang', root / 'bin/clang')
    copy('/usr/lib/llvm-20/bin/lld', root / 'bin/ld.lld')
    for alias, original in [('clang++', 'clang'), ('lld', 'ld.lld')]:
        (root / 'bin' / alias).symlink_to(original)
    copy('/usr/lib/llvm-20/lib/clang', root / 'lib/clang')
    copy(shutil.which('cmake'), root / 'bin/cmake')
    cmake_share = next(Path('/usr/share').glob('cmake-[0-9]*'))
    copy(cmake_share, root / 'share' / cmake_share.name)
    copy(shutil.which('ninja'), root / 'bin/ninja')
    copy('/usr/include', root / 'sysroot/usr/include')
    copy('/usr/lib/gcc/x86_64-linux-gnu/13', root / 'sysroot/usr/lib/gcc/x86_64-linux-gnu/13')
    # Linker scripts, startup objects and development libraries are part of the
    # compile-only sysroot. The running game uses the system's glibc and drivers.
    for base in ('/usr/lib/x86_64-linux-gnu', '/lib/x86_64-linux-gnu'):
        folder = Path(base)
        for item in folder.iterdir():
            if item.is_file() and (item.suffix in {'.a', '.o', '.so'} or '.so.' in item.name):
                if item.name.startswith(('libLLVM', 'libclang', 'libvulkan_', 'libnvidia', 'libGLX', 'libGLdispatch')):
                    continue
                copy(item, root / 'sysroot' / str(item).lstrip('/'))
    # Copy runtime dependencies by their SONAME. Do not bundle host GPU drivers
    # or glibc into the runtime library search path.
    binaries = list((root / 'bin').iterdir()) + list((root / 'sdk/bin').glob('*')) + list((root / 'sdk/lib').glob('*.so*'))
    for binary in binaries:
        result = subprocess.run(['ldd', str(binary)], capture_output=True, text=True)
        for line in result.stdout.splitlines():
            fields = line.split()
            if len(fields) >= 3 and fields[1] == '=>' and fields[2].startswith('/') and fields[0] not in CORE:
                copy(fields[2], root / 'lib' / fields[0])
    notices = root / 'licenses'
    for item in Path('/usr/share/doc').iterdir():
        if (item / 'copyright').is_file():
            copy(item / 'copyright', notices / (item.name + '.copyright'))
    (root / 'bundle.json').write_text(json.dumps({'sdk_commit': 'f5337cdc947ff6d4c4196737e2c807a48f2a1fc2',
        'compiler': 'clang-20', 'glibc_minimum': '2.35', 'spatial_fsr': True, 'cas': True,
        'texture_detail': True}, indent=2) + '\n')
    environment = dict(os.environ, LD_LIBRARY_PATH=str(root / 'lib') + ':' + str(root / 'sdk/lib'))
    test = root / 'compiler-test.cpp'
    test.write_text('#include <format>\n#include <iostream>\nint main(){std::cout << std::format("{}",42);}\n')
    subprocess.run([str(root / 'bin/clang++'), '--sysroot=' + str(root / 'sysroot'),
        '--gcc-toolchain=' + str(root / 'sysroot/usr'), '-fuse-ld=lld', '-std=c++23',
        str(test), '-o', str(root / 'compiler-test')], env=environment, check=True)
    assert subprocess.check_output([str(root / 'compiler-test')], env=environment) == b'42'
    test.unlink()
    (root / 'compiler-test').unlink()

if __name__ == '__main__':
    main(Path(sys.argv[1]))
