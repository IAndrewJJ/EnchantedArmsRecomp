// Windows-only, revision-specific gameplay options. All defaults preserve the game.
#include "cheats.h"

#include <rex/cvar.h>
#include <rex/hook.h>

#include "generated/default/enchantedarmsrecomp_init.h"
#include "guest_memory.h"

REXCVAR_DEFINE_BOOL(ea_cheat_hp, false, "Enchanted Arms cheats",
                    "Keep living player characters at full HP")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);
REXCVAR_DEFINE_BOOL(ea_cheat_ep, false, "Enchanted Arms cheats", "Keep player EP full")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);
REXCVAR_DEFINE_BOOL(ea_cheat_money, false, "Enchanted Arms cheats", "Keep TB at its game limit")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);
REXCVAR_DEFINE_BOOL(ea_cheat_sp, false, "Enchanted Arms cheats",
                    "Keep recruited characters' SP at its game limit")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);
REXCVAR_DEFINE_INT32(ea_cheat_money_multiplier, 1, "Enchanted Arms cheats",
                     "Positive TB reward multiplier: 1, 2, 5 or 10")
    .range(1, 10)
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);
REXCVAR_DEFINE_INT32(ea_cheat_experience_multiplier, 1, "Enchanted Arms cheats",
                     "Positive experience reward multiplier: 1, 2, 5 or 10")
    .range(1, 10)
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

namespace {
ea::cheats::Options Options() {
  return {REXCVAR_GET(ea_cheat_hp),
          REXCVAR_GET(ea_cheat_ep),
          REXCVAR_GET(ea_cheat_money),
          REXCVAR_GET(ea_cheat_sp),
          REXCVAR_GET(ea_cheat_money_multiplier),
          REXCVAR_GET(ea_cheat_experience_multiplier)};
}
ea::GuestMemory Memory() { return ea::GuestMemory(rex::system::kernel_state()->memory()); }
}  // namespace
namespace ea {
void RefreshCheats() {
  const auto options = Options();
  if (!options.Passive()) return;
  auto memory = Memory();
  cheats::Refresh(memory, options);
  static thread_local bool logged = false;
  uint32_t owner, data, count, money, points, level;
  if (!logged && cheats::Owner(memory, owner) && cheats::Roster(memory, owner, data, count) &&
      opening_battle::Field(memory, data, 0x1B4, level) && level > 0 &&
      opening_battle::Field(memory, owner, cheats::kMoneyOffset, money) &&
      opening_battle::Field(memory, data, cheats::kSpOffset, points)) {
    REXLOG_INFO(
        "EA cheats: HP={}, EP={}, infinite_TB={}, infinite_SP={}, money_multiplier={}, "
        "experience_multiplier={}, recruited={}, TB={}, first_SP={}",
        options.hp, options.ep, options.money, options.sp, options.money_multiplier,
        options.experience_multiplier, count, money, points);
    logged = true;
  }
}
cheats::Attack PrepareCheatAttack(uint32_t stats, uint32_t result, bool prediction) {
  if (!REXCVAR_GET(ea_cheat_hp)) return {};
  auto memory = Memory();
  return cheats::PrepareAttack(memory, Options(), stats, result, prediction);
}
void FinishCheatAttack(const cheats::Attack& attack) {
  if (!attack.stats) return;
  auto memory = Memory();
  cheats::FinishAttack(memory, attack);
}
}  // namespace ea

// EM_Event_AddTablet and shops call the same party-wallet increment/setter.
REX_HOOK_RAW(sub_822471C8) {
  if (REXCVAR_GET(ea_cheat_money_multiplier) > 1 && ctx.r4.s32 > 0 &&
      !REXCVAR_GET(ea_cheat_money)) {
    auto memory = Memory();
    uint32_t owner, current;
    if (ea::cheats::Owner(memory, owner) && uint64_t(owner) + 64 == ctx.r3.u32 &&
        ea::opening_battle::Field(memory, owner, ea::cheats::kMoneyOffset, current))
      ctx.r4.s64 = ea::cheats::Reward(ctx.r4.s32, REXCVAR_GET(ea_cheat_money_multiplier), current,
                                      ea::cheats::kMoneyCap);
  }
  __imp__sub_822471C8(ctx, base);
}
REX_HOOK_RAW(sub_82247198) {
  if (REXCVAR_GET(ea_cheat_money)) {
    auto memory = Memory();
    uint32_t owner;
    if (ea::cheats::Owner(memory, owner) && uint64_t(owner) + 64 == ctx.r3.u32)
      ctx.r4.s64 = ea::cheats::kMoneyCap;
  }
  __imp__sub_82247198(ctx, base);
}
REX_HOOK_RAW(sub_82196D10) {
  if (REXCVAR_GET(ea_cheat_experience_multiplier) > 1 && ctx.r4.s32 > 0) {
    auto memory = Memory();
    uint32_t total, progress;
    if (ea::cheats::OwnedRecord(memory, ctx.r3.u32) &&
        ea::opening_battle::Field(memory, ctx.r3.u32, 0x1B0, total) &&
        ea::opening_battle::Field(memory, ctx.r3.u32, 0x1B8, progress))
      ctx.r4.s64 = ea::cheats::Reward(ctx.r4.s32, REXCVAR_GET(ea_cheat_experience_multiplier),
                                      std::max(total, progress),
                                      uint32_t(std::numeric_limits<int32_t>::max()));
  }
  __imp__sub_82196D10(ctx, base);
}
// HP uses signed additions; EP uses positive costs. Retain all original side effects.
REX_HOOK_RAW(sub_82193238) {
  if (REXCVAR_GET(ea_cheat_hp) && ctx.r4.s32 < 0) {
    auto memory = Memory();
    if (ea::cheats::PlayerStats(memory, ctx.r3.u32)) ctx.r4.s64 = 0;
  }
  __imp__sub_82193238(ctx, base);
}
REX_HOOK_RAW(sub_821935B0) {
  if (REXCVAR_GET(ea_cheat_ep) && ctx.r4.s32 > 0) {
    auto memory = Memory();
    if (ea::cheats::PlayerStats(memory, ctx.r3.u32)) ctx.r4.s64 = 0;
  }
  __imp__sub_821935B0(ctx, base);
}
REX_HOOK_RAW(sub_82193610) {
  const auto stats = ctx.r3.u32;
  bool protect = false;
  if (REXCVAR_GET(ea_cheat_ep)) {
    auto memory = Memory();
    protect = ea::cheats::PlayerStats(memory, stats);
  }
  __imp__sub_82193610(ctx, base);
  if (protect) {
    auto memory = Memory();
    if (ea::cheats::Refill(memory, stats, 56)) ctx.r3.s64 = 0;
  }
}
