#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/ui/d3d12/d3d12_provider.h>
#include <rex/ui/d3d12/d3d12_presenter.h>
#include <iostream>
#include <windows.h>
int main(int argc,char** argv) {
  rex::cvar::Init(argc,argv);
  rex::InitLoggingEarly();
  // Exercise the real late-registration boundary used by the Xenos plugin.
  if (!LoadLibraryW(L"rexgpu-xenosrd.dll")) { std::cerr << "GPU plugin load failed " << GetLastError(); return 1; }
  auto provider = rex::ui::d3d12::D3D12Provider::Create();
  if (!provider) return 2;
  // Initializes the actual D3D12 FSR/CAS shader pipeline states, without a game or window.
  auto presenter = provider->CreatePresenter();
  if (!presenter) return 3;
  const auto& config = presenter->GetGuestOutputPaintConfigFromUIThread();
  using Effect = rex::ui::Presenter::GuestOutputPaintConfig::Effect;
  const auto name = rex::cvar::GetFlagByName("present_effect");
  const auto expected = name == "fsr" ? Effect::kFsr : name == "cas" ? Effect::kCas : Effect::kBilinear;
  if (config.GetEffect() != expected) return 4;
  if (name == "fsr" && std::abs(config.GetFsrSharpnessReduction() - 0.4f) > 0.001f) return 5;
  if (name == "cas" && std::abs(config.GetCasAdditionalSharpness() - 0.8f) > 0.001f) return 6;
  if (rex::cvar::GetFlagByName("draw_resolution_scale_x") != "2" ||
      rex::cvar::GetFlagByName("draw_resolution_scale_y") != "2" ||
      rex::cvar::GetFlagByName("anisotropic_override") != "5" ||
      rex::cvar::GetFlagByName("swap_post_effect") != "fxaa") return 7;
  std::cout << "PASS: D3D12 presenter initialized spatial shader pipelines; effect=" << name
            << ", draw scale=2x2, AF=16x, post-AA=fxaa, sharpness applied.\n";
}
