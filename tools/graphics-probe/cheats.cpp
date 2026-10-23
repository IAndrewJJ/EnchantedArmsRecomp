#include <iostream>
#include <map>
#include <stdexcept>
#include <vector>

#include "../../src/cheat_rules.h"
namespace {
using namespace ea::cheats;
void Require(bool value, const char* message) {
  if (!value) throw std::runtime_error(message);
}
struct Memory {
  std::map<uint32_t, uint32_t> words;
  std::vector<std::pair<uint32_t, uint32_t>> ranges;
  uint32_t denied = 0;
  bool Writable(uint32_t address, uint32_t size) {
    if (!address || !size || uint64_t(address) + size > 0x100000000ULL ||
        (denied >= address && uint64_t(denied) < uint64_t(address) + size))
      return false;
    for (auto [base, length] : ranges)
      if (address >= base && uint64_t(address) + size <= uint64_t(base) + length) return true;
    return false;
  }
  void Allocate(uint32_t address, uint32_t size) {
    ranges.emplace_back(address, size);
    for (uint32_t i = 0; i < size; i += 4) words[address + i] = 0;
  }
  bool Read32(uint32_t address, uint32_t& value) {
    auto found = words.find(address);
    if (found == words.end()) return false;
    value = found->second;
    return true;
  }
  void Write32(uint32_t address, uint32_t value) {
    Require(Writable(address, 4), "Cheat write escaped writable memory");
    words.at(address) = value;
  }
  uint32_t Get(uint32_t address) { return words.at(address); }
};
struct Fixture {
  Memory memory;
  static constexpr uint32_t owner = 0x10000, roster = 0x20000, config = 0x30000, facade = 0x40000;
  static constexpr uint32_t battle = 0x50000, party = 0x60000, enemy = 0x70000, list = 0x80000;
  static constexpr uint32_t unit = 0x90000, enemy_unit = 0xA0000, result = 0xB0000,
                            stats = unit + 320;
  Fixture(uint32_t side = 0) {
    using namespace ea::opening_battle;
    for (auto global : {kPartyManager, kSideConfiguration, kBattleFacade})
      memory.Allocate(global, 4);
    for (auto object :
         {owner, roster, config, facade, battle, party, enemy, list, unit, enemy_unit, result})
      memory.Allocate(object, 0x1000);
    memory.Write32(kPartyManager, owner);
    memory.Write32(owner + kRosterData, roster);
    memory.Write32(owner + kRosterCount, 2);
    memory.Write32(owner + kMoneyOffset, 300);
    for (uint32_t record : {roster, roster + kRecordSize}) {
      memory.Write32(record + 0x1B4, 8);
      memory.Write32(record + kSpOffset, 40);
    }
    memory.Write32(kSideConfiguration, config);
    memory.Write32(config + 208, side);
    memory.Write32(config + 120, 100);
    memory.Write32(config + 168, 109);
    memory.Write32(kBattleFacade, facade);
    memory.Write32(facade, battle);
    memory.Write32(battle + (side + 1) * 4, party);
    memory.Write32(battle + (2 - side) * 4, enemy);
    memory.Write32(party + 324, list);
    memory.Write32(party + 328, 1);
    memory.Write32(list, unit);
    memory.Write32(enemy + 324, list + 4);
    memory.Write32(enemy + 328, 1);
    memory.Write32(list + 4, enemy_unit);
    for (auto current : {stats, enemy_unit + 320}) {
      memory.Write32(current + 48, 20);
      memory.Write32(current + 52, 100);
      memory.Write32(current + 56, 3);
      memory.Write32(current + 60, 50);
    }
  }
};
}  // namespace
int main() {
  try {
    for (uint32_t side : {0u, 1u}) {
      Fixture f(side);
      auto before = f.memory.words;
      Refresh(f.memory, {});
      Require(f.memory.words == before, "Disabled cheats changed the game");
      Options o;
      o.money = o.sp = o.hp = o.ep = true;
      Refresh(f.memory, o);
      Require(f.memory.Get(f.owner + kMoneyOffset) == kMoneyCap, "Infinite TB did not fill wallet");
      for (auto r : {f.roster, f.roster + kRecordSize})
        Require(f.memory.Get(r + kSpOffset) == kSpCap, "SP did not cover recruited reserves");
      Require(f.memory.Get(f.stats + 48) == 100 && f.memory.Get(f.stats + 56) == 50,
              "Party HP/EP not filled");
      Require(f.memory.Get(f.enemy_unit + 368) == 20 && f.memory.Get(f.enemy_unit + 376) == 3,
              "Enemy benefited from cheats");
      Require(OwnedRecord(f.memory, f.roster) && OwnedRecord(f.memory, f.roster + kRecordSize),
              "Owned XP records rejected");
      Require(!OwnedRecord(f.memory, f.roster + 4) && !OwnedRecord(f.memory, f.enemy_unit),
              "XP accepted non-roster data");
      const auto settled = f.memory.words;
      Refresh(f.memory, o);
      Require(f.memory.words == settled, "Cheat refresh was not idempotent");
    }
    Require(Reward(30, 5, 100, kMoneyCap) == 150, "Money multiplier incorrect");
    Require(Reward(-30, 10, 100, kMoneyCap) == -30, "Purchases were multiplied");
    Require(Reward(30, 1, 100, kMoneyCap) == 30 && Reward(30, 3, 100, kMoneyCap) == 30,
            "Normal/invalid multiplier changed rewards");
    Require(Reward(30, 10, kMoneyCap - 4, kMoneyCap) == 4, "Money overflowed game cap");
    Require(Reward(0x7FFFFFFF, 10, 0, 0x7FFFFFFF) == 0x7FFFFFFF, "XP overflowed signed range");
    Require(Reward(20, 5, 0x7FFFFFF0, 0x7FFFFFFF) == 15, "XP existing balance was ignored");
    Fixture f;
    Options o;
    o.hp = o.ep = true;
    auto attack = PrepareAttack(f.memory, o, f.stats, f.result, false);
    Require(attack.stats == f.stats, "Player attack protection absent");
    f.memory.Write32(f.stats + 48, 0);
    f.memory.Write32(f.result + 12, ea::opening_battle::kDefeated | 8);
    f.memory.Write32(f.result + 52, uint32_t(-20));
    Require(FinishAttack(f.memory, attack), "Lethal effect was not repaired");
    Require(f.memory.Get(f.stats + 48) == 100 && f.memory.Get(f.result + 12) == 8 &&
                f.memory.Get(f.result + 52) == 80,
            "HP repair disagrees with damage result");
    Require(!PrepareAttack(f.memory, o, f.stats, f.result, true).stats,
            "Damage prediction was modified");
    Require(!PrepareAttack(f.memory, o, f.enemy_unit + 320, f.result, false).stats,
            "Enemy attack target protected");
    f.memory.denied = f.result + 52;
    Require(!PrepareAttack(f.memory, o, f.stats, f.result, false).stats,
            "Read-only result accepted");
    f.memory.denied = 0;
    f.memory.Write32(f.stats + 48, 0);
    Refresh(f.memory, o);
    Require(f.memory.Get(f.stats + 48) == 0, "Dead party member was silently resurrected");
    for (auto corrupt : {0u, 257u, 0xFFFFFFFFu}) {
      Fixture invalid;
      Options points;
      points.sp = true;
      invalid.memory.Write32(invalid.owner + kRosterCount, corrupt);
      auto original = invalid.memory.words;
      Refresh(invalid.memory, points);
      Require(invalid.memory.words == original, "Corrupt roster was written");
    }
    Fixture opening;
    opening.memory.Write32(opening.config + 120, ea::opening_battle::kEncounter);
    opening.memory.Write32(opening.config + 168, ea::opening_battle::kSystem);
    auto original = opening.memory.words;
    Refresh(opening.memory, o);
    Require(opening.memory.words == original &&
                !PrepareAttack(opening.memory, o, opening.stats, opening.result, false).stats,
            "Cheats changed the opening story fight");
    std::cout << "PASS: default-off cheats, party/enemy isolation, scripted fight, SP reserves, "
                 "invalid memory, caps and reward arithmetic.\n";
    return 0;
  } catch (const std::exception& error) {
    std::cerr << error.what() << '\n';
    return 1;
  }
}
