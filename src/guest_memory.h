#pragma once
#include <rex/memory.h>

#include <cstdint>
namespace ea {
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
}  // namespace ea
