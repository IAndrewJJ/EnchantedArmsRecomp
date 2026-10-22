#include "../../src/fps_counter.h"
#include <imgui.h>
#include <cmath>
#include <iostream>

int main() {
  ea::FrameRateTracker tracker(100, 10.0);
  // Repainting the UI faster must not manufacture guest frames.
  for (int i = 0; i < 100; ++i) {
    if (tracker.Update(100, 10.0 + i * 0.004).ready) return 1;
  }
  auto thirty = tracker.Update(115, 10.5);
  if (!thirty.ready || std::abs(thirty.fps - 30) > 0.001 || std::abs(thirty.frame_ms - 1000.0/30) > 0.001) return 2;
  auto fast = tracker.Update(175, 11.0);
  if (std::abs(fast.fps - 120) > 0.001 || std::abs(fast.frame_ms - 1000.0/120) > 0.001) return 3;
  auto paused = tracker.Update(175, 11.5);
  if (!paused.ready || paused.fps != 0 || paused.frame_ms != 0) return 4;
  if (tracker.Update(0, 12.0).ready) return 5;

  const auto frames = ea::GameFrameCount();
  ea::RecordGameFrame(); ea::RecordGameFrame();
  if (ea::GameFrameCount() - frames != 2) return 6;
  using rex::ui::KeyEvent;
  using rex::ui::VirtualKey;
  const KeyEvent press(nullptr, VirtualKey::kF6, 1, false, false, false, false, false);
  const KeyEvent held(nullptr, VirtualKey::kF6, 10, true, false, false, false, false);
  const KeyEvent modified(nullptr, VirtualKey::kF6, 1, false, false, false, true, false);
  const KeyEvent other(nullptr, VirtualKey::kF4, 1, false, false, false, false, false);
  if (!ea::ShouldToggleFpsCounter(press) || ea::ShouldToggleFpsCounter(held) ||
      ea::ShouldToggleFpsCounter(modified) || ea::ShouldToggleFpsCounter(other)) return 7;

  ImGui::CreateContext();
  auto& io = ImGui::GetIO();
  io.IniFilename = nullptr;
  io.DisplaySize = ImVec2(1280,720);
  io.DeltaTime = 1.0f / 60;
  unsigned char* pixels; int width,height;
  io.Fonts->GetTexDataAsRGBA32(&pixels,&width,&height);
  for (const auto sample : {thirty,fast,paused,ea::FrameRateSample{}}) {
    for (int frame = 0; frame < 2; ++frame) {
      ImGui::NewFrame(); ea::DrawFpsCounter(sample,io); ImGui::Render();
    }
    const auto* data = ImGui::GetDrawData();
    if (!data || data->TotalVtxCount == 0 || io.WantCaptureKeyboard || io.WantCaptureMouse) return 8;
    for (int list = 0; list < data->CmdListsCount; ++list) {
      for (const auto& vertex : data->CmdLists[list]->VtxBuffer) {
        if (vertex.pos.x < 1000 || vertex.pos.x > 1280 || vertex.pos.y < 0 || vertex.pos.y > 80) return 9;
      }
    }
  }
  ImGui::DestroyContext();
  std::cout << "PASS: 30/120 FPS, frame times, idle/reset handling, F6 repeat/modifier filtering, and non-interactive top-right overlay rendering.\n";
}
