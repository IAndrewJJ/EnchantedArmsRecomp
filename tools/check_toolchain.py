"""Install verified build tools in a NEW directory, without an ISO or existing SDK.

This network integration check is separate from the offline synthetic tests.
It never launches the game and does not modify a player's installation.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
import setup_game


def check_sdk_link(toolchain, local, prefix=None):
    """Compile, link and run a tiny SDK client without any game files or window."""
    env = os.environ.copy()
    sdk = toolchain / "windows-sdk/microsoft.windows.sdk.cpp/c"
    libraries = toolchain / "windows-sdk/microsoft.windows.sdk.cpp.x64/c"
    msvc = toolchain / "msvc/VC/Tools/MSVC/14.44.35207"
    version = "10.0.26100.0"
    llvm = toolchain / "llvm-20.1.8/bin"
    cmake = toolchain / "cmake/cmake-4.4.4-windows-x86_64/bin/cmake.exe"
    env['INCLUDE'] = ';'.join(str(p) for p in [msvc/'include', sdk/f'Include/{version}/ucrt',
        sdk/f'Include/{version}/shared', sdk/f'Include/{version}/um', sdk/f'Include/{version}/winrt'])
    env['LIB'] = ';'.join(str(p) for p in [msvc/'lib/x64', libraries/'um/x64', libraries/'ucrt/x64'])
    build = local / 'SDK client probe'
    prefix = prefix or toolchain / 'rexglue-setup-0.10.0-1/win-amd64'
    subprocess.run([str(cmake), '-S', str(setup_game.PROJECT/'tools/graphics-probe'), '-B', str(build),
        '-G', 'Ninja', '-DCMAKE_BUILD_TYPE=RelWithDebInfo', '-DCMAKE_CXX_COMPILER='+(llvm/'clang++.exe').as_posix(),
        '-DCMAKE_MAKE_PROGRAM='+(toolchain/'ninja/ninja.exe').as_posix(), '-DCMAKE_PREFIX_PATH='+prefix.as_posix(),
        '-DCMAKE_EXE_LINKER_FLAGS=-fuse-ld=lld', '-DCMAKE_RC_COMPILER:FILEPATH='+(sdk/f'bin/{version}/x64/rc.exe').as_posix(),
        '-DCMAKE_MT:FILEPATH='+(sdk/f'bin/{version}/x64/mt.exe').as_posix()], env=env, check=True)
    subprocess.run([str(cmake), '--build', str(build), '--target', 'ea_sdk_link_probe'], env=env, check=True)
    env['PATH'] = str(prefix/'bin') + os.pathsep + env.get('PATH', '')
    subprocess.run([str(build/'ea_sdk_link_probe.exe')], env=env, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True, help="Must not already exist")
    parser.add_argument("--sdk-only", action="store_true", help="Download/check only the 43 MiB ReXGlue SDK")
    args = parser.parse_args()
    local = args.directory.resolve()
    local.mkdir(parents=True, exist_ok=False)
    if args.sdk_only:
        packages = json.loads((setup_game.PROJECT / "toolchain.lock.json").read_text(encoding="utf-8"))
        package = next(p for p in packages if p.get("component") == "rexglue")
        setup_game.install_package(package, local / "toolchain")
        # Exercise verification/reuse of the cache as well as the initial download.
        setup_game.download(package, local / "toolchain/downloads")
    else:
        setup_game.prepare_toolchain(local)
        setup_game.prepare_toolchain(local)
        check_sdk_link(local / "toolchain", local)
    print("Fresh toolchain check passed: " + str(local), flush=True)


if __name__ == "__main__":
    main()
