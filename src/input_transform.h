#pragma once
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <string_view>

namespace ea {
constexpr std::array<uint16_t, 14> button_bits = {0x1000, 0x2000, 0x4000, 0x8000, 0x10, 0x20, 0x100,
                                                  0x200,  0x40,   0x80,   1,      2,    4,    8};

// Values identify the physical source for each game action. Only permutations
// are accepted, so a malformed configuration cannot lose the confirm button.
inline std::array<unsigned, 14> ParseButtonMap(std::string_view text) {
  std::array<unsigned, 14> identity{}, result{};
  for (unsigned i = 0; i < 14; ++i) identity[i] = i;
  uint32_t seen = 0;
  size_t pos = 0;
  for (unsigned i = 0; i < 14; ++i) {
    size_t end = text.find(',', pos);
    auto token = text.substr(pos, end == text.npos ? text.size() - pos : end - pos);
    if (token.empty() || token.size() > 2) return identity;
    unsigned value = 0;
    for (char c : token) {
      if (c < '0' || c > '9') return identity;
      value = value * 10 + unsigned(c - '0');
    }
    if (value >= 14 || (seen & (1u << value))) return identity;
    seen |= 1u << value;
    result[i] = value;
    if (i < 13 && end == text.npos) return identity;
    if (i == 13 && end != text.npos) return identity;
    pos = end + 1;
  }
  return result;
}
inline uint16_t MapButtons(uint16_t input, const std::array<unsigned, 14>& map) {
  uint16_t result = input & 0x0C00;  // preserve reserved/guide bits
  for (unsigned action = 0; action < 14; ++action)
    if (input & button_bits[map[action]]) result |= button_bits[action];
  return result;
}
inline void AdjustStick(int16_t& x, int16_t& y, double deadzone, double gain, bool invert_y) {
  if (deadzone == 0 && gain == 1 && !invert_y) return;
  double fx = double(x) / 32767.0, fy = double(y) / 32767.0;
  const double length = std::hypot(fx, fy);
  deadzone = std::clamp(deadzone, 0.0, 0.4);
  if (length <= deadzone) {
    x = y = 0;
    return;
  }
  const double amount =
      std::clamp((std::min(length, 1.0) - deadzone) / (1.0 - deadzone) * gain, 0.0, 1.0);
  auto quantize = [](double value) {
    return int16_t(std::clamp(std::lround(value * 32767.0), -32767L, 32767L));
  };
  x = quantize(fx / length * amount);
  y = quantize(fy / length * amount * (invert_y ? -1.0 : 1.0));
}
}  // namespace ea
