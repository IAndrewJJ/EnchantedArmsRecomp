#include <cassert>
#include <cmath>
#include <cstring>
#include <limits>
#include <stdexcept>

#include "../../src/frame_step.h"

int main() {
  ea::FrameStepClock clock;
  // UV increments must advance at the same speed at both frame rates, including
  // negative scrolling. Neither the accumulated UV nor its wrap period changes.
  float velocity30[4] = {0.01f, -0.02f, 0.005f, 0.0f};
  float velocity60[4];
  std::memcpy(velocity60, velocity30, sizeof(velocity30));
  ea::ScaleTextureScroll(velocity60, 0.5);
  for (int i = 0; i < 4; ++i) {
    if (std::abs(double(velocity30[i]) * 300 - double(velocity60[i]) * 600) > 1e-6)
      throw std::runtime_error("Texture scrolling depends on FPS");
  }
  float unchanged[4];
  std::memcpy(unchanged, velocity30, sizeof(velocity30));
  ea::ScaleTextureScroll(unchanged, 1.0);
  if (std::memcmp(unchanged, velocity30, sizeof(velocity30)) != 0)
    throw std::runtime_error("Original texture scroll changed");
  for (int fps : {30, 60, 90, 120, 144, 165, 180, 240, 360}) {
    double simulated = 0.0;
    for (int i = 0; i < fps * 10; ++i) simulated += clock.Advance(1.0 / fps, fps);
    if (std::abs(simulated - 300.0) > 1e-6) throw std::runtime_error("Simulation depends on FPS");
  }
  if (clock.Advance(10.0, 60) != 0.5 || clock.Advance(0.0, 60) != 0.5 ||
      clock.Advance(std::numeric_limits<double>::quiet_NaN(), 60) != 0.5)
    throw std::runtime_error("Pause/startup correction is unsafe");
  if (std::abs(clock.Advance(1.0 / 40, 60) - 0.75) > 1e-9)
    throw std::runtime_error("Actual frame cadence ignored");
  if (clock.Advance(0.2, 60) != 2.0 || clock.Advance(0.2, 0) != 1.0)
    throw std::runtime_error("Stall bound or Original timing changed");
  // A 60 FPS cap can miss individual deadlines. Simulation must follow the
  // elapsed intervals rather than accelerating or slowing with the cap alone.
  double elapsed = 0.0, simulated = 0.0;
  for (int i = 0; i < 10000; ++i) {
    const double interval = (i % 3 == 0) ? 1.0 / 40 : 1.0 / 60;
    elapsed += interval;
    simulated += clock.Advance(interval, 60);
  }
  if (std::abs(simulated - elapsed * 30.0) > 1e-6)
    throw std::runtime_error("Uneven frame intervals change game speed");
  if (clock.Advance(1.0 / 240, -1) != clock.Advance(1.0 / 240, 60))
    throw std::runtime_error("Unlimited mode ignores elapsed time");
  unsigned depth = 0;
  {
    ea::FrameStepScope outer(depth);
    if (!outer.outer()) throw std::runtime_error("Missing root scale");
    {
      ea::FrameStepScope inner(depth);
      if (inner.outer()) throw std::runtime_error("Nested scale applied twice");
    }
    if (depth != 1) throw std::runtime_error("Nested scope not restored");
  }
  if (depth != 0) throw std::runtime_error("Scope not restored");
}
