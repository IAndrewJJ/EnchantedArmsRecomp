#include "generated/default/enchantedarmsrecomp_init.h"
#include <rex/hook.h>

// ReXGlue invokes this callback on its host audio worker. The callback's cached
// FPSCR can be zero before its first guest floating-point operation, which would
// unmask x86 precision exceptions when a generated flush-mode update restores it.
// Seed host policy at the host-to-guest boundary, then run the original callback.
REX_HOOK_RAW(sub_822AF680) {
  static thread_local bool logged = false;
  if (!logged) {
    REXLOG_INFO("Audio callback FP state: cached={:08X}, host={:08X}",
                ctx.fpscr.csr, ctx.fpscr.getcsr());
    logged = true;
  }
  ctx.fpscr.InitHost();
  __imp__sub_822AF680(ctx, base);
}
