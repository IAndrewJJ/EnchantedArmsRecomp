#include "fps_counter.h"
#include <rex/cvar.h>
#include <imgui.h>
#include <atomic>
#include <chrono>

REXCVAR_DEFINE_BOOL(ea_show_fps, false, "Enchanted Arms",
    "Show the FPS counter at startup; F6 toggles it during play")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

namespace ea {
namespace {
std::atomic<uint64_t> game_frames{0};
double Now() {
  return std::chrono::duration<double>(std::chrono::steady_clock::now().time_since_epoch()).count();
}
}  // namespace

void RecordGameFrame() { game_frames.fetch_add(1, std::memory_order_relaxed); }
uint64_t GameFrameCount() { return game_frames.load(std::memory_order_relaxed); }

bool IsFpsCounterKey(const rex::ui::KeyEvent& event) {
  return event.virtual_key() == rex::ui::VirtualKey::kF6 && !event.is_shift_pressed() &&
         !event.is_ctrl_pressed() && !event.is_alt_pressed() && !event.is_super_pressed();
}

bool ShouldToggleFpsCounter(const rex::ui::KeyEvent& event) {
  return IsFpsCounterKey(event) && !event.prev_state();
}

FpsCounterDialog::FpsCounterDialog(rex::ui::ImGuiDrawer* drawer)
    : ImGuiDialog(drawer), tracker_(GameFrameCount(), Now()) {}

void FpsCounterDialog::OnDraw(ImGuiIO& io) {
  DrawFpsCounter(tracker_.Update(GameFrameCount(), Now()), io);
}

void DrawFpsCounter(const FrameRateSample& sample, ImGuiIO& io) {
  ImGui::SetNextWindowPos(ImVec2(io.DisplaySize.x - 12.0f, 12.0f), ImGuiCond_Always,
                          ImVec2(1.0f, 0.0f));
  ImGui::SetNextWindowBgAlpha(0.7f);
  ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(10.0f, 7.0f));
  ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 4.0f);
  ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 1.0f, 1.0f, 1.0f));
  const auto flags = ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_AlwaysAutoResize |
      ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoInputs | ImGuiWindowFlags_NoNav |
      ImGuiWindowFlags_NoFocusOnAppearing | ImGuiWindowFlags_NoBringToFrontOnFocus;
  if (ImGui::Begin("##ea_fps_counter", nullptr, flags)) {
    if (!sample.ready) ImGui::TextUnformatted("FPS --  |  -- ms");
    else if (sample.fps > 0.0) ImGui::Text("FPS %.1f  |  %.2f ms", sample.fps, sample.frame_ms);
    else ImGui::TextUnformatted("FPS 0.0  |  -- ms");
  }
  ImGui::End();
  ImGui::PopStyleColor();
  ImGui::PopStyleVar(2);
}
}  // namespace ea
