#include "generated/default/enchantedarmsrecomp_init.h"
#include <rex/hook.h>
#include <bit>

// Exact translation of 0x821867B8..0x821867D4. ReXGlue 0.10.0 recognizes
// the function-pointer table as a switch but emits case 0 against the loaded
// absolute address. Preserve the guest's register writes and indirect tail call.
// default.xex SHA256: 79e95ea830119123c2fd9c7b08fb890c4cf40cc6b8422b3568a820b6b3779f22
REX_HOOK_RAW(sub_821867B8) {
  ctx.r10.u64 = REX_LOAD_U8(ctx.r3.u32 + 224);
  ctx.r11.s64 = -2106392576;
  ctx.r10.s64 = ctx.r10.s8;
  ctx.r11.s64 += -28868;
  ctx.r10.u64 = std::rotl(ctx.r10.u32, 2) & 0xFFFFFFFCu;
  ctx.r11.u64 = REX_LOAD_U32(ctx.r10.u32 + ctx.r11.u32);
  ctx.ctr.u64 = ctx.r11.u64;
  static thread_local bool logged = false;
  if (!logged) {
    REXLOG_INFO("Scene dispatch: table=82728F3C, index={}, target={:08X}",
                ctx.r10.u32 / 4, ctx.ctr.u32);
    logged = true;
  }
  REX_CALL_INDIRECT_FUNC(ctx.ctr.u32);
}
