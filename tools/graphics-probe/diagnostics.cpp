#include "../../src/frame_capture.h"
#include "../../src/frame_time_window.h"
#include <iostream>
#include <limits>

int main(int argc, char** argv) {
  if (argc != 2) return 1;
  ea::FrameTimeWindow window;
  for (int i = 0; i < 99; ++i) window.Add(10.0);
  window.Add(100.0);
  window.Add(std::numeric_limits<double>::quiet_NaN()); window.Add(-1.0);
  auto s = window.Summary();
  if (s.intervals != 100 || s.p50_ms != 10 || s.p95_ms != 10 || s.p99_ms != 10 ||
      s.max_ms != 100 || s.over_50ms != 1 || std::abs(s.mean_ms - 10.9) > 0.00001) return 2;
  for (int i = 0; i < 3000; ++i) window.Add(20.0);
  s = window.Summary();
  if (s.intervals != 3100 || s.percentile_samples != 2048 || s.p99_ms != 20 || s.max_ms != 100) return 3;
  window.Reset();
  if (window.Summary().intervals != 0 || window.Summary().max_ms != 0) return 4;
  using rex::ui::KeyEvent; using rex::ui::VirtualKey;
  const KeyEvent bare(nullptr, VirtualKey::kF8, 1, false, false, false, false, false);
  const KeyEvent alt(nullptr, VirtualKey::kF8, 1, false, false, false, true, false);
  if (!ea::IsFrameCaptureKey(bare) || ea::IsFrameCaptureKey(alt)) return 5;
  rex::ui::RawImage image;
  std::string error;
  if (ea::WriteCapturePng(image, argv[1], error) || error.empty()) return 6;
  image.width = 2; image.height = 2; image.stride = 12;
  // RGBX red, green / blue, white. Unspecified alpha must not become transparent.
  // Last row is deliberately unpadded, matching the SDK RawImage contract.
  image.data = {255,0,0,0, 0,255,0,0, 7,7,7,7, 0,0,255,0, 255,255,255,0};
  if (!ea::WriteCapturePng(image, argv[1], error)) { std::cerr << error; return 7; }
  std::cout << "PASS: frame interval percentiles, bounded sampling, stalls/reset, F8 modifiers, PNG encoding from padded RGBX.\n";
}
