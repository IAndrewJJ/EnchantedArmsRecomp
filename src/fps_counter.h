#pragma once
#include "frame_rate_tracker.h"
#include <rex/ui/imgui_dialog.h>
#include <rex/ui/ui_event.h>

namespace ea {
void RecordGameFrame();
uint64_t GameFrameCount();
bool IsFpsCounterKey(const rex::ui::KeyEvent& event);
bool ShouldToggleFpsCounter(const rex::ui::KeyEvent& event);
void DrawFpsCounter(const FrameRateSample& sample, ImGuiIO& io);

class FpsCounterDialog final : public rex::ui::ImGuiDialog {
 public:
  explicit FpsCounterDialog(rex::ui::ImGuiDrawer* drawer);
 protected:
  void OnDraw(ImGuiIO& io) override;
 private:
  FrameRateTracker tracker_;
};
}  // namespace ea
