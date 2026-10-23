#include "../../src/opening_battle.h"

#include <array>
#include <iostream>
#include <map>
#include <stdexcept>

namespace {
using namespace ea::opening_battle;

void Require(bool condition, const char* message) {
  if (!condition) throw std::runtime_error(message);
}

struct Memory {
  std::map<uint32_t, uint8_t> bytes;
  uint32_t read_only = 0;
  void Allocate(uint32_t address, uint32_t size) {
    for (uint32_t i = 0; i < size; ++i) bytes[address + i] = 0;
  }
  bool Read32(uint32_t address, uint32_t& value) {
    if (!Contains(address, 4)) return false;
    value = 0;
    for (uint32_t i = 0; i < 4; ++i) value = (value << 8) | bytes.at(address + i);
    return true;
  }
  bool Read16(uint32_t address, uint16_t& value) {
    if (!Contains(address, 2)) return false;
    value = (uint16_t(bytes.at(address)) << 8) | bytes.at(address + 1);
    return true;
  }
  bool Contains(uint32_t address, uint32_t size) const {
    if (!address || uint64_t(address) + size > 0x100000000ULL) return false;
    for (uint32_t i = 0; i < size; ++i)
      if (!bytes.count(address + i)) return false;
    return true;
  }
  bool Writable(uint32_t address, uint32_t size) const {
    return address != read_only && Contains(address, size);
  }
  void Write32(uint32_t address, uint32_t value) {
    Require(Writable(address, 4), "Write escaped writable guest memory");
    for (uint32_t i = 0; i < 4; ++i) bytes.at(address + i) = uint8_t(value >> (24 - i * 8));
  }
  uint32_t Get(uint32_t address) {
    uint32_t result;
    Require(Read32(address, result), "Invalid fixture read");
    return result;
  }
};

struct Battle {
  Memory memory;
  static constexpr uint32_t manager = 0x10000, config = 0x20000, facade = 0x30000;
  static constexpr uint32_t battle = 0x40000, party = 0x50000, enemy = 0x60000;
  static constexpr uint32_t player_units = 0x70000, enemy_units = 0x80000;
  static constexpr uint32_t result = 0x90000;
  static constexpr std::array<uint32_t, 4> units = {0xA0000, 0xB0000, 0xC0000, 0xD0000};

  explicit Battle(uint32_t player_side = 0) {
    for (uint32_t global : {kEventManager, kSideConfiguration, kBattleFacade})
      memory.Allocate(global, 4);
    for (uint32_t object :
         {manager, config, facade, battle, party, enemy, player_units, enemy_units, result})
      memory.Allocate(object, 10000);
    for (uint32_t unit : units) memory.Allocate(unit, 2000);
    memory.Write32(kEventManager, manager);
    // Match the failed in-game capture: the queued request is reset, while the
    // active battle retains the encounter. This previously disabled the guard.
    memory.Write32(manager + 9896, 0);
    memory.Write32(manager + 9944, 0xFFFFFFFF);
    memory.Write32(kSideConfiguration, config);
    memory.Write32(config + 120, kEncounter);
    memory.Write32(config + 168, kSystem);
    memory.Write32(config + 208, player_side);
    memory.Write32(kBattleFacade, facade);
    memory.Write32(facade, battle);
    memory.Write32(battle + (player_side + 1) * 4, party);
    memory.Write32(battle + (2 - player_side) * 4, enemy);
    memory.Write32(party + 324, player_units);
    memory.Write32(party + 328, 3);
    memory.Write32(enemy + 324, enemy_units);
    memory.Write32(enemy + 328, 1);
    for (uint32_t i = 0; i < 4; ++i) {
      memory.Write32(units[i] + 324, ((i + 1) * 10) << 16);
      memory.Write32(units[i] + 368, 500);
      memory.Write32(units[i] + 372, 500);
      memory.Write32(i < 3 ? player_units + i * 4 : enemy_units, units[i]);
    }
  }
  std::optional<Protection> PrepareFor(uint32_t unit = units[0], bool prediction = false) {
    return Prepare(memory, unit + 320, result, prediction);
  }
  void Lethal(uint32_t unit, uint32_t old_hp) {
    memory.Write32(unit + 368, 0);
    memory.Write32(result + 12, kDefeated | 0x00800002);
    memory.Write32(result + 32, uint32_t(-9999));
    memory.Write32(result + 52, uint32_t(-old_hp));
    memory.Write32(result + 104, 0x80000010);
  }
};
}  // namespace

int main() {
  // Repeated lethal hits and separate targets must survive without accumulating
  // HP, regardless of the battle's side mapping. No frame clock is involved.
  for (uint32_t side : {0, 1}) {
    Battle fixture(side);
    for (uint32_t unit : Battle::units) {
      if (unit == Battle::units[3]) continue;
      for (uint32_t hp : {500, 1, 1}) {
        auto protection = fixture.PrepareFor(unit);
        Require(bool(protection), "Opening protagonist was not protected");
        fixture.Lethal(unit, hp);
        Require(Finish(fixture.memory, *protection), "Lethal hit was not corrected");
        Require(fixture.memory.Get(unit + 368) == 1, "Survivor HP must be exactly one");
        Require(fixture.memory.Get(unit + 372) == 500, "Maximum HP changed");
        Require(fixture.memory.Get(Battle::result + 12) == 0x00800002,
                "Defeat flag retained or unrelated flags changed");
        Require(fixture.memory.Get(Battle::result + 52) == uint32_t(1 - hp),
                "Actual damage accounting disagrees with HP");
        Require(fixture.memory.Get(Battle::result + 32) == uint32_t(-9999) &&
                    fixture.memory.Get(Battle::result + 104) == 0x80000010,
                "Original attack presentation changed");
      }
    }
  }
  for (auto ids :
       {std::array<uint32_t, 2>{7051, 109}, {7052, 110}, {7054, 109}, {7051, 101}, {1000, 1}}) {
    Battle fixture;
    fixture.memory.Write32(Battle::config + 120, ids[0]);
    fixture.memory.Write32(Battle::config + 168, ids[1]);
    Require(!fixture.PrepareFor(), "Survival escaped the first Queen encounter");
    fixture.memory.Write32(Battle::manager + 9896, kEncounter);
    fixture.memory.Write32(Battle::manager + 9944, kSystem);
    Require(!fixture.PrepareFor(), "Queued opening request overrode a different active battle");
  }
  {
    Battle fixture;
    Require(!fixture.PrepareFor(Battle::units[0], true), "Prediction changed live HP");
    fixture.memory.Write32(Battle::units[3] + 324, 10 << 16);
    Require(!fixture.PrepareFor(Battle::units[3]), "Enemy with matching ID was protected");
    // Resource IDs (15/25/35 for the prologue preset) and runtime IDs need not
    // match the normal party's 10/20/30. Protection follows actual membership.
    for (uint32_t id : {0, 1, 15, 25, 35}) {
      fixture.memory.Write32(Battle::units[0] + 324, id << 16);
      Require(bool(fixture.PrepareFor()), "Local unit ID incorrectly disabled protection");
    }
  }
  for (uint32_t remaining : {1, 100, 500}) {
    Battle fixture;
    auto protection = fixture.PrepareFor();
    fixture.memory.Write32(Battle::units[0] + 368, remaining);
    const auto before = fixture.memory.bytes;
    Require(!Finish(fixture.memory, *protection) && before == fixture.memory.bytes,
            "Nonlethal damage, healing or leave-one-HP effect changed");
  }
  {
    Battle fixture;
    fixture.memory.Write32(Battle::units[0] + 368, 0);
    Require(!fixture.PrepareFor(), "Previously defeated character was resurrected");
  }
  for (uint32_t bad : {0u, 1u, 2u, 4u, 17u, 0xFFFFFFFFu}) {
    Battle fixture;
    fixture.memory.Write32(Battle::party + 328, bad);
    Require(!fixture.PrepareFor(), "Invalid party count was followed");
  }
  for (uint32_t global : {kSideConfiguration, kBattleFacade}) {
    for (uint32_t bad : {0u, 0xDEADBEEFu, 0xFFFFFFFCu}) {
      Battle fixture;
      fixture.memory.Write32(global, bad);
      Require(!fixture.PrepareFor(), "Invalid guest pointer was followed");
    }
  }
  for (uint32_t address : {Battle::units[0] + 368, Battle::result + 12, Battle::result + 52}) {
    Battle fixture;
    fixture.memory.read_only = address;
    Require(!fixture.PrepareFor(), "Read-only guest state was accepted");
  }
  {
    Battle fixture;
    Require(!Prepare(fixture.memory, 0xFFFFFFF0, Battle::result, false),
            "Overflowing target was accepted");
    Require(!Prepare(fixture.memory, Battle::units[0] + 320, 0xFFFFFFFC, false),
            "Overflowing result was accepted");
    auto protection = fixture.PrepareFor();
    fixture.memory.Write32(Battle::units[0] + 368, 0);
    const auto before = fixture.memory.bytes;
    Require(!Finish(fixture.memory, *protection) && before == fixture.memory.bytes,
            "Unrelated zero-HP state was rewritten without an attack defeat result");
  }
  std::cout << "Opening battle survival: passed encounter, party, HP, result and memory checks\n";
}
