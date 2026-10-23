using System;
using System.Linq;
namespace EnchantedArmsPC
{
    static class CheatsSelfTests
    {
        internal static void Run()
        {
            var p = Program.Json.Deserialize<Preferences>("{\"Scale\":2,\"FrameLimit\":60}");
            p.Normalize();
            if (p.CheatInfiniteHP || p.CheatInfiniteEP || p.CheatInfiniteMoney ||
                p.CheatInfiniteSP || p.CheatMoneyMultiplier != 1 ||
                p.CheatExperienceMultiplier != 1)
                throw new Exception("Older preferences enabled cheats");
            if (p.CheatArguments(false).Length != 0)
                throw new Exception("Cheats sent to unsupported native version");
            foreach (int multiplier in Preferences.CheatMultipliers)
            {
                var selected =
                    new Preferences { CheatInfiniteHP = true, CheatInfiniteEP = true,
                                      CheatInfiniteSP = true, CheatMoneyMultiplier = multiplier,
                                      CheatExperienceMultiplier = multiplier };
                using (var dialog = new SettingsWindow(selected, false, false, SettingsPage.Cheats))
                {
                    dialog.ReadValues();
                    if (Program.Json.Serialize(selected) != Program.Json.Serialize(dialog.Settings))
                        throw new Exception("Settings window changed cheat selections");
                }
                using (var page = new CheatsPage())
                {
                    page.LoadValues(selected);
                    var saved = new Preferences();
                    page.ReadValues(saved);
                    saved = Program.Json.Deserialize<Preferences>(Program.Json.Serialize(saved));
                    if (!saved.CheatInfiniteHP || !saved.CheatInfiniteEP ||
                        !saved.CheatInfiniteSP || saved.CheatMoneyMultiplier != multiplier ||
                        saved.CheatExperienceMultiplier != multiplier)
                        throw new Exception("Cheat selection did not round-trip");
                }
                if (!selected.CheatArguments(true).Contains("--ea_cheat_money_multiplier=" +
                                                            multiplier))
                    throw new Exception("Reward multiplier not forwarded");
                selected.CheatInfiniteMoney = true;
                if (!selected.CheatArguments(true).Contains("--ea_cheat_money_multiplier=1"))
                    throw new Exception("Infinite money combined with a reward multiplier");
            }
            p.CheatMoneyMultiplier = -5;
            p.CheatExperienceMultiplier = 999;
            p.Normalize();
            if (p.CheatMoneyMultiplier != 1 || p.CheatExperienceMultiplier != 1)
                throw new Exception("Invalid cheat multipliers accepted");
        }
    }
}
