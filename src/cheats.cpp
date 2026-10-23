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

void TraceBattle(ea::GuestMemory& memory, const ea::cheats::Options& options) {
  if (!options.hp && !options.ep) return;
  uint32_t config, encounter, system, units, count;
  if (!memory.Read32(ea::opening_battle::kSideConfiguration, config) ||
      !ea::opening_battle::Field(memory, config, 120, encounter) ||
      !ea::opening_battle::Field(memory, config, 168, system) ||
      !ea::cheats::PlayerGroup(memory, units, count))
    return;
  static thread_local uint64_t previous = ~uint64_t(0);
  static thread_local unsigned records = 0;
  const uint64_t current = (uint64_t(encounter) << 32) | system;
  if (current == previous || records >= 16) return;
  uint32_t unit, hp, hp_max, ep, ep_max;
  if (!memory.Read32(units, unit) || !ea::opening_battle::Field(memory, unit, 368, hp) ||
      !ea::opening_battle::Field(memory, unit, 372, hp_max) ||
      !ea::opening_battle::Field(memory, unit, 376, ep) ||
      !ea::opening_battle::Field(memory, unit, 380, ep_max) || !hp_max || !ep_max)
    return;
  REXLOG_INFO(
      "EA cheat battle: encounter={}, system={}, party_count={}, HP={}/{}, EP={}/{}, "
      "story_pause={}",
      encounter, system, count, hp, hp_max, ep, ep_max,
      ea::opening_battle::IsOpeningEncounter(memory));
  previous = current;
  ++records;
}
}  // namespace
namespace ea {
void RefreshCheats() {
  const auto options = Options();
  if (!options.Passive()) return;
  auto memory = Memory();
  cheats::Refresh(memory, options);
  TraceBattle(memory, options);
  static thread_local bool logged = false;
  uint32_t owner, data, count, money, points, level;
  if (!logged && cheats::Owner(memory, owner) && cheats::Roster(memory, owner, data, count) &&
      opening_battle::Field(memory, data, 0x1B4, level) && level > 0 &&
      opening_battle::Field(memory, owner, cheats::kMoneyOffset, money) &&
      opening_battle::Field(memory, data, cheats::kSpOffset, points)) {
    REXLOG_INFO(
        "EA cheats: HP={}, EP={}, infinite_TB={}, infinite_SP={}, money_multiplier={}, "
        "experience_multiplier={}, character_records={}, TB={}, first_SP={}",
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
  if (cheats::FinishAttack(memory, attack)) {
    static thread_local unsigned records = 0;
    if (records++ < 16)
      REXLOG_INFO("EA cheat HP: resolved damage repaired, HP={}->{}", attack.before,
                  attack.maximum);
  }
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
    if (ea::cheats::PlayerStats(memory, ctx.r3.u32)) {
      static thread_local unsigned records = 0;
      if (records++ < 16) REXLOG_INFO("EA cheat HP: blocked signed damage {}", ctx.r4.s32);
      ctx.r4.s64 = 0;
    }
  }
  __imp__sub_82193238(ctx, base);
}
REX_HOOK_RAW(sub_821935B0) {
  if (REXCVAR_GET(ea_cheat_ep) && ctx.r4.s32 > 0) {
    auto memory = Memory();
    if (ea::cheats::PlayerStats(memory, ctx.r3.u32)) {
      static thread_local unsigned records = 0;
      if (records++ < 16) REXLOG_INFO("EA cheat EP: blocked cost {}", ctx.r4.s32);
      ctx.r4.s64 = 0;
    }
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
    if (ea::cheats::Refill(memory, stats, 56)) {
      static thread_local unsigned records = 0;
      if (records++ < 16) REXLOG_INFO("EA cheat EP: skill cost restored");
      ctx.r3.s64 = 0;
    }
  }
}
