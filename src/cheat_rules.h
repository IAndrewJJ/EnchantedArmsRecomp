#pragma once

#include <algorithm>
#include <cstdint>
#include <limits>

#include "opening_battle.h"

namespace ea::cheats {
constexpr uint32_t kPartyManager = 0x82766B1C;
constexpr uint32_t kMoneyOffset = 0x260, kMoneyCap = 9999999;
constexpr uint32_t kRosterData = 0x208, kRosterCount = 0x20C, kRecordSize = 0x47C;
constexpr uint32_t kSpOffset = 0x1C0, kSpCap = 99999;
struct Options {
  bool hp = false, ep = false, money = false, sp = false;
  int money_multiplier = 1, experience_multiplier = 1;
  bool Passive() const { return hp || ep || money || sp; }
};
inline int Multiplier(int value) { return value == 2 || value == 5 || value == 10 ? value : 1; }
inline int32_t Reward(int32_t amount, int multiplier, uint32_t current, uint32_t cap) {
  if (amount <= 0 || current > cap || Multiplier(multiplier) == 1) return amount;
  return int32_t(std::min<int64_t>(int64_t(amount) * Multiplier(multiplier), cap - current));
}
template <class Memory>
bool Owner(Memory& memory, uint32_t& owner) {
  return memory.Read32(kPartyManager, owner) && owner && !(owner & 3) &&
         memory.Writable(owner, kMoneyOffset + 4);
}
template <class Memory>
bool Roster(Memory& memory, uint32_t owner, uint32_t& data, uint32_t& count) {
  using opening_battle::Field;
  return Field(memory, owner, kRosterData, data) && Field(memory, owner, kRosterCount, count) &&
         data && !(data & 3) && count > 0 && count <= 256 &&
         memory.Writable(data, count * kRecordSize);
}
template <class Memory>
bool OwnedRecord(Memory& memory, uint32_t record) {
  uint32_t owner, data, count;
  return Owner(memory, owner) && Roster(memory, owner, data, count) && record >= data &&
         uint64_t(record) < uint64_t(data) + count * kRecordSize &&
         (record - data) % kRecordSize == 0;
}
template <class Memory>
bool PlayerGroup(Memory& memory, uint32_t& units, uint32_t& count) {
  using namespace opening_battle;
  uint32_t config, side, facade, battle, group;
  return memory.Read32(kSideConfiguration, config) && Field(memory, config, 208, side) &&
         side <= 1 && memory.Read32(kBattleFacade, facade) && Field(memory, facade, 0, battle) &&
         Field(memory, battle, (side + 1) * 4, group) && Field(memory, group, 324, units) &&
         Field(memory, group, 328, count) && count > 0 && count <= 8 && units && !(units & 3);
}
template <class Memory>
bool PlayerStats(Memory& memory, uint32_t stats) {
  if (stats < 320 || (stats & 3) || opening_battle::IsOpeningEncounter(memory)) return false;
  uint32_t units, count;
  if (!PlayerGroup(memory, units, count)) return false;
  for (uint32_t i = 0; i < count; ++i) {
    uint32_t unit;
    if (!opening_battle::Field(memory, units, i * 4, unit)) return false;
    if (unit && uint64_t(unit) + 320 == stats) return true;
  }
  return false;
}
template <class Memory>
bool Refill(Memory& memory, uint32_t stats, uint32_t current_offset) {
  uint32_t hp, current, maximum, address;
  using namespace opening_battle;
  if (!Field(memory, stats, 48, hp) || !hp || !Field(memory, stats, current_offset, current) ||
      !Field(memory, stats, current_offset + 4, maximum) || !maximum || maximum > 999999 ||
      current > maximum || !Add(stats, current_offset, address) || !memory.Writable(address, 4))
    return false;
  if (current != maximum) memory.Write32(address, maximum);
  return true;
}
template <class Memory>
void Refresh(Memory& memory, const Options& options) {
  if (!options.Passive()) return;
  uint32_t owner;
  if ((options.money || options.sp) && Owner(memory, owner)) {
    uint32_t current;
    if (options.money && opening_battle::Field(memory, owner, kMoneyOffset, current) &&
        current < kMoneyCap)
      memory.Write32(owner + kMoneyOffset, kMoneyCap);
    uint32_t data, count;
    if (options.sp && Roster(memory, owner, data, count)) {
      for (uint32_t i = 0; i < count; ++i) {
        const uint32_t record = data + i * kRecordSize;
        uint32_t level, points;
        if (opening_battle::Field(memory, record, 0x1B4, level) && level > 0 && level <= 999 &&
            opening_battle::Field(memory, record, kSpOffset, points) && points < kSpCap)
          memory.Write32(record + kSpOffset, kSpCap);
      }
    }
  }
  if ((options.hp || options.ep) && !opening_battle::IsOpeningEncounter(memory)) {
    uint32_t units, count;
    if (!PlayerGroup(memory, units, count)) return;
    for (uint32_t i = 0; i < count; ++i) {
      uint32_t unit, stats;
      if (!opening_battle::Field(memory, units, i * 4, unit) ||
          !opening_battle::Add(unit, 320, stats))
        continue;
      if (options.hp) Refill(memory, stats, 48);
      if (options.ep) Refill(memory, stats, 56);
    }
  }
}
struct Attack {
  uint32_t stats = 0, result = 0, before = 0, maximum = 0;
};
template <class Memory>
Attack PrepareAttack(Memory& memory, const Options& options, uint32_t stats, uint32_t result,
                     bool prediction) {
  Attack guard;
  uint32_t hp, maximum, hp_address, flags, delta;
  using namespace opening_battle;
  if (!options.hp || prediction || !PlayerStats(memory, stats) || !Field(memory, stats, 48, hp) ||
      !Field(memory, stats, 52, maximum) || !hp || hp > maximum || maximum > 999999 ||
      !Add(stats, 48, hp_address) || !Add(result, 12, flags) || !Add(result, 52, delta) ||
      !memory.Writable(hp_address, 4) || !memory.Writable(flags, 4) || !memory.Writable(delta, 4))
    return guard;
  return Attack{stats, result, hp, maximum};
}
template <class Memory>
bool FinishAttack(Memory& memory, const Attack& guard) {
  if (!guard.stats) return false;
  uint32_t hp, flags, delta;
  using namespace opening_battle;
  if (!Field(memory, guard.stats, 48, hp) || hp >= guard.maximum ||
      !Field(memory, guard.result, 12, flags) || !Field(memory, guard.result, 52, delta))
    return false;
  memory.Write32(guard.stats + 48, guard.maximum);
  memory.Write32(guard.result + 12, flags & ~kDefeated);
  // Match the actual restored health, keeping signed damage arithmetic modulo 2^32.
  memory.Write32(guard.result + 52, delta + guard.maximum - hp);
  return true;
}
}  // namespace ea::cheats
