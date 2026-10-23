#include <rex/chrono/clock.h>

#include <iostream>

int main() {
  const auto frequency = rex::chrono::Clock::QueryHostTickFrequency();
  const auto tick = rex::chrono::Clock::QueryHostTickCount();
  if (!frequency || !tick) return 1;
  std::cout << "Source-built SDK compile/link/load check passed.\n";
  return 0;
}
