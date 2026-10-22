#include "generated/default/enchantedarmsrecomp_init.h"
#include <rex/cvar.h>
#include <rex/hook.h>
#include <array>

REXCVAR_DEFINE_BOOL(ea_trace_input, false, "Enchanted Arms",
                   "Log host key events and guest controller transitions");

// Observe the original XInput wrapper without altering its return value or data.
REX_HOOK_RAW(sub_82621788) {
  const auto user = ctx.r3.u32;
  const auto state = ctx.r4.u32;
  __imp__sub_82621788(ctx, base);
  if (!REXCVAR_GET(ea_trace_input) || user >= 4) {
    return;
  }
  const auto status = ctx.r3.u32;
  const auto buttons = status == 0 && state ? REX_LOAD_U16(state + 4) : 0;
  struct Snapshot { bool seen = false; uint32_t status = 0; uint16_t buttons = 0; };
  static thread_local std::array<Snapshot, 4> previous;
  auto& last = previous[user];
  if (!last.seen || last.status != status || last.buttons != buttons) {
    REXLOG_INFO("Guest input: user={}, status={:08X}, buttons={:04X}",
                user, status, buttons);
    last = {true, status, static_cast<uint16_t>(buttons)};
  }
}
