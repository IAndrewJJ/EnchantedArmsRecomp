#pragma once
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>

namespace ea {
struct FrameTimeSummary {
  uint64_t intervals = 0, over_50ms = 0;
  size_t percentile_samples = 0;
  double mean_ms = 0, p50_ms = 0, p95_ms = 0, p99_ms = 0, max_ms = 0;
};

// Bounded storage, no allocations or file writes at the frame boundary.
// Percentiles use the latest 2048 intervals; mean/max/stalls cover the full window.
class FrameTimeWindow {
 public:
  void Add(double ms) {
    if (!std::isfinite(ms) || ms <= 0) return;
    samples_[count_ % samples_.size()] = ms;
    ++count_; sum_ += ms; max_ = std::max(max_, ms);
    if (ms > 50.0) ++slow_;
  }
  FrameTimeSummary Summary() const {
    FrameTimeSummary s;
    if (!count_) return s;
    s.intervals = count_; s.over_50ms = slow_;
    s.percentile_samples = std::min<uint64_t>(count_, samples_.size());
    s.mean_ms = sum_ / count_; s.max_ms = max_;
    auto sorted = samples_;
    std::sort(sorted.begin(), sorted.begin() + s.percentile_samples);
    auto percentile = [&](double fraction) {
      return sorted[size_t(std::ceil(fraction * s.percentile_samples)) - 1];
    };
    s.p50_ms = percentile(0.50); s.p95_ms = percentile(0.95); s.p99_ms = percentile(0.99);
    return s;
  }
  void Reset() { count_ = slow_ = 0; sum_ = max_ = 0; }
 private:
  std::array<double, 2048> samples_{};
  uint64_t count_ = 0, slow_ = 0;
  double sum_ = 0, max_ = 0;
};
}  // namespace ea
