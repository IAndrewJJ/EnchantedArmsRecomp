#include <rex/cvar.h>
#include <rex/hook.h>
#include <rex/input/input_system.h>

#include <array>

#include "generated/default/enchantedarmsrecomp_init.h"
#include "input_controls.h"
#include "input_transform.h"

REXCVAR_DEFINE_STRING(ea_pad_map, "0,1,2,3,4,5,6,7,8,9,10,11,12,13", "Enchanted Arms/Input",
                      "Physical controller button source for each game action")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);
REXCVAR_DEFINE_DOUBLE(ea_left_deadzone, 0.0, "Enchanted Arms/Input",
                      "Additional radial left-stick dead zone")
    .range(0.0, 0.4);
REXCVAR_DEFINE_DOUBLE(ea_right_deadzone, 0.0, "Enchanted Arms/Input",
                      "Additional radial right-stick dead zone")
    .range(0.0, 0.4);
REXCVAR_DEFINE_DOUBLE(ea_camera_response, 1.0, "Enchanted Arms/Input",
                      "Right-stick response multiplier")
    .range(0.25, 2.0);
REXCVAR_DEFINE_BOOL(ea_invert_camera_y, false, "Enchanted Arms/Input",
                    "Invert right-stick camera Y");
REXCVAR_DEFINE_BOOL(ea_swap_triggers, false, "Enchanted Arms/Input", "Swap the physical triggers");

namespace ea {
void ConfigureInput(rex::input::InputSystem& input) {
  // This is a single-player title. Newly connected pads may have a new ordinal;
  // share them with guest player 0 instead of stranding them on player 2/3/4.
  input.SetDeviceAssignment(std::make_unique<rex::input::SharedAssignment>());
}
}  // namespace ea

REXCVAR_DEFINE_BOOL(ea_trace_input, false, "Enchanted Arms",
                    "Log host key events and guest controller transitions");

// Preserve the original wrapper's status and packet, then transform successful
// input samples. Defaults leave all button and stick values unchanged.
REX_HOOK_RAW(sub_82621788) {
  const auto user = ctx.r3.u32;
  const auto state = ctx.r4.u32;
  __imp__sub_82621788(ctx, base);
  if (ctx.r3.u32 == 0 && state && user < 4) {
    static const auto mapping = ea::ParseButtonMap(REXCVAR_GET(ea_pad_map));
    const auto buttons = REX_LOAD_U16(state + 4);
    REX_STORE_U16(state + 4, ea::MapButtons(buttons, mapping));
    int16_t lx = int16_t(REX_LOAD_U16(state + 8)), ly = int16_t(REX_LOAD_U16(state + 10));
    int16_t rx = int16_t(REX_LOAD_U16(state + 12)), ry = int16_t(REX_LOAD_U16(state + 14));
    ea::AdjustStick(lx, ly, REXCVAR_GET(ea_left_deadzone), 1.0, false);
    ea::AdjustStick(rx, ry, REXCVAR_GET(ea_right_deadzone), REXCVAR_GET(ea_camera_response),
                    REXCVAR_GET(ea_invert_camera_y));
    REX_STORE_U16(state + 8, uint16_t(lx));
    REX_STORE_U16(state + 10, uint16_t(ly));
    REX_STORE_U16(state + 12, uint16_t(rx));
    REX_STORE_U16(state + 14, uint16_t(ry));
    if (REXCVAR_GET(ea_swap_triggers)) {
      auto left = REX_LOAD_U8(state + 6), right = REX_LOAD_U8(state + 7);
      REX_STORE_U8(state + 6, right);
      REX_STORE_U8(state + 7, left);
    }
  }
  if (!REXCVAR_GET(ea_trace_input) || user >= 4) {
    return;
  }
  const auto status = ctx.r3.u32;
  const auto buttons = status == 0 && state ? REX_LOAD_U16(state + 4) : 0;
  struct Snapshot {
    bool seen = false;
    uint32_t status = 0;
    uint16_t buttons = 0;
  };
  static thread_local std::array<Snapshot, 4> previous;
  auto& last = previous[user];
  if (!last.seen || last.status != status || last.buttons != buttons) {
    REXLOG_INFO("Guest input: user={}, status={:08X}, buttons={:04X}", user, status, buttons);
    last = {true, status, static_cast<uint16_t>(buttons)};
  }
}
