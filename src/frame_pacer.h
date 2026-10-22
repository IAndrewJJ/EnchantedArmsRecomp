#pragma once
#include <chrono>
#include <thread>
#if defined(_WIN32)
#include <windows.h>
#endif

namespace ea {
// Host-only cadence. Never rescale the guest timebase, audio clock or animation data.
class FramePacer {
 public:
  using Clock = std::chrono::steady_clock;
  FramePacer() {
#if defined(_WIN32)
    timer_ = CreateWaitableTimerExW(nullptr, nullptr, 0x2 /* high resolution */, TIMER_ALL_ACCESS);
    if (!timer_) timer_ = CreateWaitableTimerW(nullptr, FALSE, nullptr);
#endif
  }
  ~FramePacer() {
#if defined(_WIN32)
    if (timer_) CloseHandle(timer_);
#endif
  }
  FramePacer(const FramePacer&) = delete;
  FramePacer& operator=(const FramePacer&) = delete;
  void Wait(int fps) {
    if (fps <= 0) { fps_ = 0; return; }
    const auto now = Clock::now();
    const auto period = std::chrono::nanoseconds(1000000000LL / fps);
    if (fps != fps_ || now > deadline_ + period) {
      fps_ = fps;
      deadline_ = now;
    }
    // Slow scenes reset their deadline rather than issuing catch-up bursts.
    if (now < deadline_) {
#if defined(_WIN32)
      if (timer_) {
        LARGE_INTEGER due;
        const auto ns = std::chrono::duration_cast<std::chrono::nanoseconds>(deadline_ - now).count();
        due.QuadPart = -((ns + 99) / 100);
        if (SetWaitableTimer(timer_, &due, 0, nullptr, nullptr, FALSE)) WaitForSingleObject(timer_, INFINITE);
        else std::this_thread::sleep_until(deadline_);
      } else
#endif
      { std::this_thread::sleep_until(deadline_); }
    }
    // Retain an absolute cadence so scheduler jitter does not accumulate into
    // a progressively lower frame rate. A whole missed interval resets above.
    deadline_ += period;
  }
 private:
  int fps_ = 0;
  Clock::time_point deadline_{};
#if defined(_WIN32)
  HANDLE timer_ = nullptr;
#endif
};
}  // namespace ea
