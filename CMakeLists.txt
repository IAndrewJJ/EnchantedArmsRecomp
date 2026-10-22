# enchantedarmsrecomp - ReXGlue Recompiled Project
#
# SDK boilerplate lives in generated/rexglue.cmake.

cmake_minimum_required(VERSION 3.25)
project(enchantedarmsrecomp LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

option(EA_SPATIAL_FSR "Use the locally built spatial FidelityFX renderer" OFF)
if(EA_SPATIAL_FSR)
    # Matches the enhanced runtime/plugin's public presenter types.
    add_compile_definitions(REX_HAS_FIDELITYFX_SDK=1)
endif()

include(generated/rexglue.cmake)

# ReXGlue 0.10.0 emits partitioned declarations but its init header still
# includes an umbrella header that the packaged generator does not write.
# Generate that header in the build tree without editing translated sources.
file(GLOB ENCHANTED_ARMS_DECLARATION_HEADERS CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/generated/default/enchantedarmsrecomp_funcs.*.h")
if(NOT ENCHANTED_ARMS_DECLARATION_HEADERS)
    message(FATAL_ERROR "Run scripts/codegen.ps1 before configuring the project")
endif()
set(ENCHANTED_ARMS_UMBRELLA "#pragma once\n")
foreach(ENCHANTED_ARMS_HEADER IN LISTS ENCHANTED_ARMS_DECLARATION_HEADERS)
    get_filename_component(ENCHANTED_ARMS_HEADER_NAME "${ENCHANTED_ARMS_HEADER}" NAME)
    string(APPEND ENCHANTED_ARMS_UMBRELLA
        "#include \"${ENCHANTED_ARMS_HEADER_NAME}\"\n")
endforeach()
file(MAKE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/compat")
file(GENERATE OUTPUT "${CMAKE_CURRENT_BINARY_DIR}/compat/enchantedarmsrecomp_funcs.h"
    CONTENT "${ENCHANTED_ARMS_UMBRELLA}")
include_directories("${CMAKE_CURRENT_BINARY_DIR}/compat")

# Sources
set(ENCHANTEDARMSRECOMP_SOURCES
    src/main.cpp
    src/audio_runtime.cpp
    src/input_diagnostics.cpp
    src/scene_dispatch.cpp
    src/frame_pacing.cpp
    src/fps_counter.cpp
    src/frame_capture.cpp
)

if(WIN32)
    add_executable(enchantedarmsrecomp WIN32 ${ENCHANTEDARMSRECOMP_SOURCES})
else()
    add_executable(enchantedarmsrecomp ${ENCHANTEDARMSRECOMP_SOURCES})
endif()

rexglue_setup_target(enchantedarmsrecomp GPU_PLUGINS xenos)
if(WIN32)
    target_link_libraries(enchantedarmsrecomp PRIVATE windowscodecs ole32)
endif()
