#include "../../src/frame_pacer.h"
#include <iostream>

int main() {
  ea::FramePacer pacer;
  for (int fps : {30,60,90,120,144,165,180,240,360}) {
    pacer.Wait(0);
    pacer.Wait(fps);
    const auto start = ea::FramePacer::Clock::now();
    for (int frame = 0; frame < fps; ++frame) pacer.Wait(fps);
    const double elapsed = std::chrono::duration<double>(ea::FramePacer::Clock::now() - start).count();
    std::cout << "cap=" << fps << ", measured=" << fps / elapsed << ", elapsed=" << elapsed << "s\n";
    // A loaded CI host can run slowly. Exceeding the cap indicates a pacing bug.
    if (elapsed < 0.98 || elapsed > 1.5) return 1;
  }
  const auto unthrottled = ea::FramePacer::Clock::now();
  for (int i = 0; i < 100; ++i) pacer.Wait(-1);
  if (std::chrono::duration<double>(ea::FramePacer::Clock::now() - unthrottled).count() > 0.1) return 2;
  // A stalled scene must not be followed by a burst of catch-up submissions.
  pacer.Wait(120);
  std::this_thread::sleep_for(std::chrono::milliseconds(30));
  pacer.Wait(120);
  const auto recovered = ea::FramePacer::Clock::now();
  pacer.Wait(120);
  if (std::chrono::duration<double>(ea::FramePacer::Clock::now() - recovered).count() < 0.008) return 3;
}
