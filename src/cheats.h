#pragma once
#include "cheat_rules.h"
namespace ea {
#ifdef _WIN32
void RefreshCheats();
cheats::Attack PrepareCheatAttack(uint32_t stats, uint32_t result, bool prediction);
void FinishCheatAttack(const cheats::Attack& attack);
#else
inline void RefreshCheats() {}
inline cheats::Attack PrepareCheatAttack(uint32_t, uint32_t, bool) { return {}; }
inline void FinishCheatAttack(const cheats::Attack&) {}
#endif
}  // namespace ea
