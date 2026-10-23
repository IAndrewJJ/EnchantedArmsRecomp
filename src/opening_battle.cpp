#include "opening_battle.h"

#include <rex/hook.h>
#include <rex/memory.h>

#include <atomic>

#include "generated/default/enchantedarmsrecomp_init.h"

namespace {
class GuestMemory {
 public:
  explicit GuestMemory(rex::memory::Memory* memory) : memory_(memory) {}

  bool Read32(uint32_t address, uint32_t& value) {
    if (!Accessible(address, 4, false)) return false;
    const auto* bytes = memory_->TranslateVirtual(address);
    value = (uint32_t(bytes[0]) << 24) | (uint32_t(bytes[1]) << 16) | (uint32_t(bytes[2]) << 8) |
            uint32_t(bytes[3]);
    return true;
  }
  bool Read16(uint32_t address, uint16_t& value) {
    if (!Accessible(address, 2, false)) return false;
    const auto* bytes = memory_->TranslateVirtual(address);
    value = (uint16_t(bytes[0]) << 8) | uint16_t(bytes[1]);
    return true;
  }
  bool Writable(uint32_t address, uint32_t size) { return Accessible(address, size, true); }
  void Write32(uint32_t address, uint32_t value) {
    auto* bytes = memory_->TranslateVirtual(address);
    bytes[0] = uint8_t(value >> 24);
    bytes[1] = uint8_t(value >> 16);
    bytes[2] = uint8_t(value >> 8);
    bytes[3] = uint8_t(value);
  }

 private:
  bool Accessible(uint32_t address, uint32_t size, bool write) {
    if (!memory_ || !address || !size || uint64_t(address) + size > 0x100000000ULL) return false;
    auto* heap = memory_->LookupHeap(address);
    if (!heap || heap != memory_->LookupHeap(address + size - 1)) return false;
    const auto access = heap->QueryRangeAccess(address, address + size - 1);
    const auto required =
        write ? rex::memory::PageAccess::kReadWrite : rex::memory::PageAccess::kReadOnly;
    return (int(access) & int(required)) == int(required);
  }
  rex::memory::Memory* memory_;
};
}  // namespace

// Resolve the original attack first, including special lethal effects. Repair
// only its first-Queen player result before the caller handles defeat. This
// leaves the event's turn counter, dialogue and RequestEndBattle path intact.
REX_HOOK_RAW(sub_82199188) {
  GuestMemory memory(rex::system::kernel_state()->memory());
  const auto protection =
      ea::opening_battle::Prepare(memory, ctx.r5.u32, ctx.r6.u32, ctx.r7.s32 == 1);
  // Bounded context records let a failed campaign retest identify a mismatched
  // encounter/party layout without requiring the player to enable diagnostics.
  if (ctx.r7.s32 != 1) {
    uint32_t config, encounter, system;
    if (memory.Read32(ea::opening_battle::kSideConfiguration, config) &&
        ea::opening_battle::Field(memory, config, 120, encounter) &&
        ea::opening_battle::Field(memory, config, 168, system)) {
      static thread_local uint64_t previous = ~uint64_t(0);
      static thread_local unsigned records = 0;
      const uint64_t current = (uint64_t(encounter) << 32) | system;
      if (current != previous && records < 32) {
        REXLOG_INFO("EA battle context: encounter={}, system={}", encounter, system);
        previous = current;
        ++records;
      }
      if (encounter == ea::opening_battle::kEncounter && system == ea::opening_battle::kSystem) {
        static thread_local unsigned attacks = 0;
        uint32_t hp = 0;
        uint16_t identity = 0;
        uint32_t identity_address;
        ea::opening_battle::Field(memory, ctx.r5.u32, 48, hp);
        if (ea::opening_battle::Add(ctx.r5.u32, 4, identity_address))
          memory.Read16(identity_address, identity);
        if (attacks < 24) {
          REXLOG_INFO("EA opening Queen attack: unit={}, HP={}, party_guard={}", identity, hp,
                      bool(protection));
          ++attacks;
        }
      }
    }
  }
  __imp__sub_82199188(ctx, base);
  if (protection && ea::opening_battle::Finish(memory, *protection)) {
    static std::atomic<uint32_t> logged{0};
    if (logged.fetch_add(1, std::memory_order_relaxed) < 24) {
      REXLOG_INFO("EA opening Queen survival: unit={}, HP={}->1, encounter={}, system={}",
                  protection->identity, protection->previous_hp, ea::opening_battle::kEncounter,
                  ea::opening_battle::kSystem);
      rex::FlushLogging();
    }
  }
}
