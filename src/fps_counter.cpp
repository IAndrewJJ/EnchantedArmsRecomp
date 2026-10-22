#include "fps_counter.h"

#include <imgui.h>
#include <rex/cvar.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <cmath>

REXCVAR_DEFINE_BOOL(ea_show_fps, false, "Enchanted Arms",
                    "Show the FPS counter at startup; F6 toggles it during play")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);
REXCVAR_DEFINE_BOOL(ea_frame_graph, false, "Enchanted Arms",
                    "Include recent game frame intervals in the F6 overlay")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

namespace ea {
namespace {
std::atomic<uint64_t> game_frames{0};
constexpr size_t kGraphSamples = 180;
std::array<std::atomic<float>, kGraphSamples> frame_times{};
std::atomic<uint64_t> graph_sequence{0};
double Now() {
  return std::chrono::duration<double>(std::chrono::steady_clock::now().time_since_epoch()).count();
}
}  // namespace

void RecordGameFrame() {
  game_frames.fetch_add(1, std::memory_order_relaxed);
  static const bool graph_enabled = REXCVAR_GET(ea_frame_graph);
  if (!graph_enabled) return;
  // The swap hook is the sole producer. Atomic entries let the UI read without
  // blocking the guest thread or introducing a data race at ring wraparound.
  static thread_local double last = 0.0;
  const double now = Now();
  if (last != 0.0) {
    const auto index = graph_sequence.load(std::memory_order_relaxed);
    frame_times[index % kGraphSamples].store(float((now - last) * 1000.0),
                                             std::memory_order_relaxed);
    graph_sequence.store(index + 1, std::memory_order_release);
  }
  last = now;
}
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
                     ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoInputs |
                     ImGuiWindowFlags_NoNav | ImGuiWindowFlags_NoFocusOnAppearing |
                     ImGuiWindowFlags_NoBringToFrontOnFocus;
  if (ImGui::Begin("##ea_fps_counter", nullptr, flags)) {
    if (!sample.ready)
      ImGui::TextUnformatted("FPS --  |  -- ms");
    else if (sample.fps > 0.0)
      ImGui::Text("FPS %.1f  |  %.2f ms", sample.fps, sample.frame_ms);
    else
      ImGui::TextUnformatted("FPS 0.0  |  -- ms");
    if (REXCVAR_GET(ea_frame_graph)) {
      const auto sequence = graph_sequence.load(std::memory_order_acquire);
      const auto count = size_t(std::min<uint64_t>(sequence, kGraphSamples));
      if (count) {
        std::array<float, kGraphSamples> values{};
        for (size_t i = 0; i < count; ++i)
          values[i] =
              frame_times[(sequence - count + i) % kGraphSamples].load(std::memory_order_relaxed);
        auto sorted = values;
        std::sort(sorted.begin(), sorted.begin() + count);
        const float p95 = sorted[size_t(std::ceil(count * 0.95)) - 1];
        const float maximum = sorted[count - 1];
        ImGui::Text("Recent p95 %.2f ms  |  peak %.2f ms", p95, maximum);
        ImGui::PlotLines("##ea_intervals", values.data(), int(count), 0, "Game frame intervals",
                         0.0f, std::max(50.0f, maximum), ImVec2(285.0f, 65.0f));
      }
    }
  }
  ImGui::End();
  ImGui::PopStyleColor();
  ImGui::PopStyleVar(2);
}
}  // namespace ea
