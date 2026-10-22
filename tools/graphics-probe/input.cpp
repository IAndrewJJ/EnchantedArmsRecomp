#include <stdexcept>

#include "../../src/input_transform.h"
int main() {
  auto identity = ea::ParseButtonMap("");
  for (unsigned i = 0; i < 14; i++)
    if (ea::MapButtons(ea::button_bits[i], identity) != ea::button_bits[i])
      throw std::runtime_error("Default button mapping changed");
  auto swapped = ea::ParseButtonMap("1,0,2,3,4,5,6,7,8,9,10,11,12,13");
  if (ea::MapButtons(0x1000, swapped) != 0x2000 || ea::MapButtons(0x2000, swapped) != 0x1000)
    throw std::runtime_error("Remap lost face buttons");
  if (ea::ParseButtonMap("1,1,2,3,4,5,6,7,8,9,10,11,12,13") != identity)
    throw std::runtime_error("Duplicate button assignment accepted");
  int16_t x = 12345, y = -23456;
  ea::AdjustStick(x, y, 0, 1, false);
  if (x != 12345 || y != -23456) throw std::runtime_error("Default stick input changed");
  x = 1000;
  y = 1000;
  ea::AdjustStick(x, y, 0.1, 1, false);
  if (x || y) throw std::runtime_error("Drift not removed");
  x = 32767;
  y = 0;
  ea::AdjustStick(x, y, 0.2, 1, false);
  if (x != 32767 || y) throw std::runtime_error("Full stick travel lost");
  x = 0;
  y = -32768;
  ea::AdjustStick(x, y, 0, 1, true);
  if (y != 32767) throw std::runtime_error("Invert overflow");
  x = 10000;
  y = 0;
  ea::AdjustStick(x, y, 0, 0.5, false);
  if (x != 5000) throw std::runtime_error("Camera response wrong");
}
