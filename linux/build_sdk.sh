#!/usr/bin/env bash
set -euo pipefail
project=$(cd -- "$(dirname -- "$0")/.." && pwd)
work=${1:?Supply an empty build workspace}
mkdir -p "$work"
work=$(cd -- "$work" && pwd)
source="$work/rexglue-sdk"
prefix="$work/tools/sdk"
if [[ ! -d "$source/.git" ]]; then
  git clone --depth 1 --branch v0.10.0 https://github.com/rexglue/rexglue-sdk.git "$source"
fi
test "$(git -C "$source" rev-parse HEAD)" = f5337cdc947ff6d4c4196737e2c807a48f2a1fc2
git -C "$source" submodule update --init --depth 1 --jobs 4 -- \
  thirdparty/cli11 thirdparty/libmspack thirdparty/FFmpeg thirdparty/tomlplusplus \
  thirdparty/simde thirdparty/xxHash thirdparty/spdlog thirdparty/fmt thirdparty/utfcpp \
  thirdparty/imgui thirdparty/sdl3 thirdparty/tracy thirdparty/inja thirdparty/o1heap \
  thirdparty/glslang thirdparty/vulkan-headers thirdparty/vulkan-memory-allocator \
  thirdparty/spirv-tools thirdparty/spirv-headers
python3 "$project/linux/patch_sdk.py" "$source"
cmake -S "$source" -B "$work/sdk-build" -G Ninja \
  -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="$prefix" \
  -DCMAKE_C_COMPILER=clang-20 -DCMAKE_CXX_COMPILER=clang++-20 \
  '-DCMAKE_CXX_FLAGS=-DREX_HAS_FIDELITYFX_SDK=1 -march=x86-64-v2' \
  '-DCMAKE_C_FLAGS=-march=x86-64-v2' \
  -DREXGLUE_USE_D3D12=OFF -DREXGLUE_USE_VULKAN=ON \
  -DREXGLUE_ENABLE_FIDELITYFX=OFF -DREXGLUE_BUILD_TESTS=OFF
cmake --build "$work/sdk-build" --target install --parallel 2
# The SDK's Vulkan public headers include RenderDoc's API header; upstream's
# Linux install rule currently omits that header.
cp "$source/thirdparty/renderdoc/renderdoc_app.h" "$prefix/include/renderdoc_app.h"
if [[ ${EA_SDK_ONLY:-0} == 1 ]]; then exit 0; fi
python3 "$project/linux/package_toolchain.py" "$work/tools"
cmake -S "$project/linux/probe" -B "$work/probe-build" -G Ninja \
  -DCMAKE_BUILD_TYPE=Release -DCMAKE_PREFIX_PATH="$prefix" \
  -DCMAKE_CXX_COMPILER=clang++-20 -DCMAKE_EXE_LINKER_FLAGS=-fuse-ld=lld
cmake --build "$work/probe-build" --parallel 2
LD_LIBRARY_PATH="$prefix/lib:$work/tools/lib" "$work/probe-build/ea_linux_probe" "$work/capture.png"
python3 -c 'import struct,sys; p=open(sys.argv[1],"rb").read(); assert p[:8]==b"\x89PNG\r\n\x1a\n"; assert struct.unpack(">II",p[16:24])==(2,2)' "$work/capture.png"
LD_LIBRARY_PATH="$prefix/lib:$work/tools/lib" xvfb-run -a "$work/probe-build/ea_vulkan_probe" \
  --present_effect=fsr --present_fsr_sharpness_reduction=0.4 \
  --no-vulkan_allow_present_mode_immediate --no-vulkan_allow_present_mode_mailbox \
  --no-vulkan_allow_present_mode_fifo_relaxed --ea_texture_lod_bias=-0.5
LD_LIBRARY_PATH="$prefix/lib:$work/tools/lib" xvfb-run -a "$work/probe-build/ea_vulkan_probe" \
  --present_effect=cas --present_cas_additional_sharpness=0.8 \
  --no-vulkan_allow_present_mode_immediate --no-vulkan_allow_present_mode_mailbox \
  --no-vulkan_allow_present_mode_fifo_relaxed --ea_texture_lod_bias=-0.5
python3 "$project/linux/make_host_fixture.py" "$work/host-fixture"
cmake -S "$work/host-fixture" -B "$work/host-build" -G Ninja \
  -DCMAKE_BUILD_TYPE=Release -DCMAKE_PREFIX_PATH="$prefix" \
  -DCMAKE_CXX_COMPILER=clang++-20 -DCMAKE_EXE_LINKER_FLAGS=-fuse-ld=lld
cmake --build "$work/host-build" --parallel 2
echo 'PASS: Linux host sources, native Vulkan FSR/CAS pipelines, screenshots and portable compiler.'
