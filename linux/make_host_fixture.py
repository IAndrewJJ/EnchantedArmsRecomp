"""Compile original host integration with synthetic, never game-derived, imports."""
from pathlib import Path
import re
import shutil
import sys

root = Path(__file__).resolve().parents[1]
target = Path(sys.argv[1])
target.mkdir(parents=True, exist_ok=True)
shutil.copytree(root / 'src', target / 'src', dirs_exist_ok=True)
header = target / 'generated/default/enchantedarmsrecomp_init.h'
header.parent.mkdir(parents=True, exist_ok=True)
imports = sorted(set(re.findall(r'\b__imp_sub_[0-9A-Fa-f]+\b', '\n'.join(p.read_text() for p in (root / 'src').glob('*') if p.suffix in {'.cpp', '.h'}))))
text = '#pragma once\n#include <rex/ppc.h>\n#include <rex/rex_app.h>\n'
text += '#define REX_IMAGE_BASE 0x82000000u\n#define REX_IMAGE_SIZE 4096u\n'
text += 'inline const rex::PPCImageInfo PPCImageConfig{};\n'
text += '\n'.join('extern "C" REX_FUNC(' + name + ');' for name in imports)
header.write_text(text + '\n')
(target / 'imports.cpp').write_text('#include "generated/default/enchantedarmsrecomp_init.h"\n' +
    '\n'.join('extern "C" REX_FUNC(' + name + ') {}' for name in imports) + '\n')
(target / 'CMakeLists.txt').write_text('''cmake_minimum_required(VERSION 3.25)
project(ea_host_fixture LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 23)
find_package(rexglue 0.10.0 EXACT REQUIRED CONFIG)
find_package(ZLIB REQUIRED)
file(GLOB host_sources CONFIGURE_DEPENDS src/*.cpp)
add_executable(ea_host_fixture ${host_sources} imports.cpp)
target_include_directories(ea_host_fixture PRIVATE ${CMAKE_CURRENT_SOURCE_DIR} src)
target_compile_definitions(ea_host_fixture PRIVATE REX_HAS_FIDELITYFX_SDK=1)
target_link_libraries(ea_host_fixture PRIVATE rex::runtime ZLIB::ZLIB)
rexglue_configure_target(ea_host_fixture GPU_PLUGINS xenos)
''')
