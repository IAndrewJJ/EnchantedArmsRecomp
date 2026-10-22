#pragma once
#include <algorithm>
#include <cmath>

namespace ea {
// Texture scrolling has its own per-frame vector addition. Scale the increment,
// preserving the accumulated coordinates, wrap period and disabled-mode bits.
inline void ScaleTextureScroll(float (&velocity)[4], double scale) {
  if (scale == 1.0) return;
  for (float& component : velocity) component = float(double(component) * scale);
}

// The original engine expresses simulation steps in 30 Hz frames. Keep the
// host/audio clocks intact and convert only arguments at engine update entries.
class FrameStepClock {
 public:
  double Advance(double elapsed_seconds, int requested_fps) {
    if (requested_fps == 0 || requested_fps == 30) return 1.0;
    const double initial = requested_fps > 0 ? 30.0 / requested_fps : 1.0;
    // A load, pause or debugger stop must not become a giant simulation step.
    if (!std::isfinite(elapsed_seconds) || elapsed_seconds <= 0.0 || elapsed_seconds > 0.25)
      return initial;
    return std::clamp(elapsed_seconds * 30.0, 0.000001, 2.0);
  }
};

// An engine entry may forward its already-scaled argument to another entry.
// Scope nesting prevents that argument from receiving a second correction.
class FrameStepScope {
 public:
  explicit FrameStepScope(unsigned& depth) : depth_(depth), outer_(depth++ == 0) {}
  ~FrameStepScope() { --depth_; }
  bool outer() const { return outer_; }
  FrameStepScope(const FrameStepScope&) = delete;
  FrameStepScope& operator=(const FrameStepScope&) = delete;

 private:
  unsigned& depth_;
  bool outer_;
};
}  // namespace ea
