#pragma once
#include <cstdint>

namespace ea {
struct FrameRateSample {
  bool ready = false;
  double fps = 0.0;
  double frame_ms = 0.0;
};

// Sample the game's swap count, independently of how often the UI repaints.
class FrameRateTracker {
 public:
  FrameRateTracker(uint64_t frames, double seconds) : frames_(frames), seconds_(seconds) {}
  FrameRateSample Update(uint64_t frames, double seconds) {
    const double elapsed = seconds - seconds_;
    if (elapsed < 0 || frames < frames_) {
      frames_ = frames;
      seconds_ = seconds;
      sample_ = {};
    } else if (elapsed >= 0.5) {
      sample_.ready = true;
      sample_.fps = double(frames - frames_) / elapsed;
      sample_.frame_ms = sample_.fps > 0.0 ? 1000.0 / sample_.fps : 0.0;
      frames_ = frames;
      seconds_ = seconds;
    }
    return sample_;
  }
 private:
  uint64_t frames_;
  double seconds_;
  FrameRateSample sample_;
};
}  // namespace ea
