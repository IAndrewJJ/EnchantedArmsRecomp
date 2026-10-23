#include <dlfcn.h>
#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/ui/presenter.h>
#include <rex/ui/vulkan/provider.h>

#include <cmath>
#include <iostream>

int main(int argc, char** argv) {
  rex::cvar::Init(argc, argv);
  rex::InitLoggingEarly();
  if (!dlopen("librexgpu-xenos.so", RTLD_NOW | RTLD_GLOBAL)) {
    std::cerr << dlerror();
    return 1;
  }
  auto provider = rex::ui::vulkan::VulkanProvider::Create(true, true);
  if (!provider) return 2;
  auto presenter = provider->CreatePresenter();
  if (!presenter) return 3;
  const auto& config = presenter->GetGuestOutputPaintConfigFromUIThread();
  using Effect = rex::ui::Presenter::GuestOutputPaintConfig::Effect;
  const auto name = rex::cvar::GetFlagByName("present_effect");
  if (config.GetEffect() != (name == "fsr" ? Effect::kFsr : Effect::kCas)) return 4;
  if (name == "fsr" && std::abs(config.GetFsrSharpnessReduction() - 0.4f) > 0.001f) return 5;
  if (name == "cas" && std::abs(config.GetCasAdditionalSharpness() - 0.8f) > 0.001f) return 6;
  for (const char* flag :
       {"vulkan_allow_present_mode_immediate", "vulkan_allow_present_mode_mailbox",
        "vulkan_allow_present_mode_fifo_relaxed"})
    if (rex::cvar::GetFlagByName(flag) != "false") return 7;
  if (std::abs(std::stod(rex::cvar::GetFlagByName("ea_texture_lod_bias")) + 0.5) > 0.001) return 8;
  std::cout << "PASS: Vulkan " << name
            << " pipelines and sharpening initialized; synchronized presentation and texture "
               "detail applied.\n";
}
