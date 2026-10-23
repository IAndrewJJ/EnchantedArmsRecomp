#pragma once

#include <cstdint>
#include <limits>
#include <optional>

namespace ea::opening_battle {
// Supported executable only (validated by codegen.ps1). These identifiers were
// observed in the failed opening prologue fight and corroborated
// by its event script (party preset 998). Later Queen battles use 7051 / 109
// and 7052 / 110; neither is part of this opening-only survival workaround.
constexpr uint32_t kEncounter = 7054;
constexpr uint32_t kSystem = 101;
constexpr uint32_t kEventManager = 0x82766A84;
constexpr uint32_t kSideConfiguration = 0x82766AA0;
constexpr uint32_t kBattleFacade = 0x82766AA4;
constexpr uint32_t kDefeated = 0x40000000;

inline bool Add(uint32_t address, uint32_t offset, uint32_t& result) {
  if (!address || address > std::numeric_limits<uint32_t>::max() - offset) return false;
  result = address + offset;
  return true;
}

template <typename Memory>
bool Field(Memory& memory, uint32_t object, uint32_t offset, uint32_t& value) {
  uint32_t address;
  return Add(object, offset, address) && memory.Read32(address, value);
}

template <typename Memory>
bool IsOpeningEncounter(Memory& memory) {
  // 0x8217B688 copies the 208-byte request into this active configuration.
  // The event manager's queued request has already been cleared by 0x820EF6C0
  // when attacks resolve, so it must not be used to identify a running fight.
  uint32_t config, encounter, system;
  return memory.Read32(kSideConfiguration, config) && Field(memory, config, 120, encounter) &&
         encounter == kEncounter && Field(memory, config, 168, system) && system == kSystem;
}

// Follow the same side-zero unit list as EM_Event_GetBattleUnitHp.
// The prologue has a fixed three-character roster. Use actual membership;
// do not assume this preset shares the ordinary party's character IDs.
// Membership excludes enemies, reserve units and temporary stat copies.
template <typename Memory>
bool IsOpeningPartyMember(Memory& memory, uint32_t stats, uint16_t& identity) {
  if (stats < 320 || (stats & 3)) return false;
  uint32_t identity_address;
  if (!Add(stats, 4, identity_address) || !memory.Read16(identity_address, identity)) return false;
  uint32_t config, side, facade, battle, group, units, count;
  if (!memory.Read32(kSideConfiguration, config) || !Field(memory, config, 208, side) || side > 1 ||
      !memory.Read32(kBattleFacade, facade) || !Field(memory, facade, 0, battle) ||
      !Field(memory, battle, (side + 1) * 4, group) || !Field(memory, group, 324, units) ||
      !Field(memory, group, 328, count) || count != 3)
    return false;
  for (uint32_t i = 0; i < count; ++i) {
    uint32_t unit;
    if (!Field(memory, units, i * 4, unit)) return false;
    if (unit == stats - 320) return true;
  }
  return false;
}

struct Protection {
  uint32_t stats;
  uint32_t result;
  uint32_t previous_hp;
  uint16_t identity;
};

template <typename Memory>
std::optional<Protection> Prepare(Memory& memory, uint32_t stats, uint32_t result,
                                  bool prediction) {
  if (prediction || !IsOpeningEncounter(memory)) return std::nullopt;
  uint16_t identity;
  uint32_t hp, maximum, hp_address, flags_address, delta_address;
  if (!IsOpeningPartyMember(memory, stats, identity) || !Field(memory, stats, 48, hp) ||
      !Field(memory, stats, 52, maximum) || hp == 0 || hp > maximum ||
      maximum > uint32_t(std::numeric_limits<int32_t>::max()) || !Add(stats, 48, hp_address) ||
      !Add(result, 12, flags_address) || !Add(result, 52, delta_address) ||
      !memory.Writable(hp_address, 4) || !memory.Writable(flags_address, 4) ||
      !memory.Writable(delta_address, 4))
    return std::nullopt;
  return Protection{stats, result, hp, identity};
}

// Called immediately after attack resolution, before its caller consumes the
// damage result or processes death. Keep the original damage display and all
// other effect flags; adjust the actual HP delta by the single surviving HP.
template <typename Memory>
bool Finish(Memory& memory, const Protection& protection) {
  uint32_t hp, flags, delta;
  if (!Field(memory, protection.stats, 48, hp) || hp != 0 ||
      !Field(memory, protection.result, 12, flags) || !(flags & kDefeated) ||
      !Field(memory, protection.result, 52, delta))
    return false;
  memory.Write32(protection.stats + 48, 1);
  memory.Write32(protection.result + 12, flags & ~kDefeated);
  memory.Write32(protection.result + 52, delta + 1);
  return true;
}
}  // namespace ea::opening_battle
