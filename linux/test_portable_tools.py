"""Check the player's bundled compiler, SDK and linker after relocation."""
from pathlib import Path
import os
import subprocess
import sys

tools, work = (Path(p).resolve() for p in sys.argv[1:])
project = Path(__file__).resolve().parents[1]
environment = dict(os.environ)
environment['PATH'] = str(tools/'bin') + ':' + environment['PATH']
environment['LD_LIBRARY_PATH'] = str(tools/'lib') + ':' + str(tools/'sdk/lib')

def run(*command):
    subprocess.run([str(p) for p in command], env=environment, check=True)

run(tools/'sdk/bin/rexglue', '--version')
run(tools/'bin/ea-clang++', '--version')
run(sys.executable, project/'linux/make_host_fixture.py', work/'source')
run(tools/'bin/cmake', '-S', work/'source', '-B', work/'build', '-G', 'Ninja',
    '-DCMAKE_BUILD_TYPE=Release', '-DCMAKE_PREFIX_PATH=' + str(tools/'sdk'),
    '-DCMAKE_MAKE_PROGRAM=' + str(tools/'bin/ninja'),
    '-DCMAKE_CXX_COMPILER=' + str(tools/'bin/ea-clang++'),
    '-DCMAKE_CXX_FLAGS=-march=x86-64-v2', '-DCMAKE_EXE_LINKER_FLAGS=-fuse-ld=lld',
    '-DZLIB_INCLUDE_DIR=' + str(tools/'sysroot/usr/include'),
    '-DZLIB_LIBRARY=' + str(tools/'sysroot/usr/lib/x86_64-linux-gnu/libz.so'))
run(tools/'bin/cmake', '--build', work/'build', '--parallel', '2')
print('PASS: relocated compiler, SDK, CMake and Ninja build all original host sources with spaces in paths.')
