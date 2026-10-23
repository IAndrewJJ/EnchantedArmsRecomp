"""Version-pinned Vulkan texture detail support; no Windows renderer changes."""
from pathlib import Path
import sys

def replace(path, before, after):
    text = path.read_text()
    if after in text:
        return
    if text.count(before) != 1:
        raise ValueError('Unsupported SDK source: ' + str(path))
    path.write_text(text.replace(before, after))

def patch(root):
    replace(root / 'include/rex/graphics/vulkan/texture_cache.h',
            'uint32_t mip_base_map : 1;            // 22',
            'uint32_t mip_base_map : 1;            // 22\n      uint32_t detail_eligible : 1;         // 23')
    path = root / 'src/graphics/vulkan/texture_cache.cpp'
    replace(path, 'namespace rex::graphics::vulkan {',
            'REXCVAR_DEFINE_DOUBLE(ea_texture_lod_bias, 0.0, "Enchanted Arms",\n'
            '    "Detail bias for filtered mipmapped textures")\n'
            '    .range(-1.0, 0.0).lifecycle(rex::cvar::Lifecycle::kRequiresRestart);\n\n'
            'namespace rex::graphics::vulkan {')
    replace(path, '  bool has_mips = mip_max_level > mip_min_level;',
            '  bool has_mips = mip_max_level > mip_min_level;\n'
            '  parameters.detail_eligible = has_mips && !parameters.mip_base_map &&\n'
            '      min_mag_linear && mip_filter_bilinear_or_trilinear;')
    replace(path, '  sampler_create_info.sType = VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO;',
            '  sampler_create_info.sType = VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO;\n'
            '  sampler_create_info.mipLodBias = parameters.detail_eligible\n'
            '      ? float(std::clamp(REXCVAR_GET(ea_texture_lod_bias), -1.0, 0.0)) : 0.0f;')

if __name__ == '__main__':
    patch(Path(sys.argv[1]))
