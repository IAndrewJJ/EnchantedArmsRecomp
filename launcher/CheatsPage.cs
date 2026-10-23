using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    public sealed partial class Preferences
    {
        public bool CheatInfiniteHP = false;
        public bool CheatInfiniteEP = false;
        public bool CheatInfiniteMoney = false;
        public bool CheatInfiniteSP = false;
        public int CheatMoneyMultiplier = 1;
        public int CheatExperienceMultiplier = 1;
        internal static readonly int[] CheatMultipliers = { 1, 2, 5, 10 };
        internal void NormalizeCheats()
        {
            if (!CheatMultipliers.Contains(CheatMoneyMultiplier))
                CheatMoneyMultiplier = 1;
            if (!CheatMultipliers.Contains(CheatExperienceMultiplier))
                CheatExperienceMultiplier = 1;
        }
        internal string[] CheatArguments(bool supported)
        {
            NormalizeCheats();
            if (!supported)
                return new string[0];
            return new[] { CheatInfiniteHP ? "--ea_cheat_hp" : "--no-ea_cheat_hp",
                           CheatInfiniteEP ? "--ea_cheat_ep" : "--no-ea_cheat_ep",
                           CheatInfiniteMoney ? "--ea_cheat_money" : "--no-ea_cheat_money",
                           CheatInfiniteSP ? "--ea_cheat_sp" : "--no-ea_cheat_sp",
                           "--ea_cheat_money_multiplier=" +
                               (CheatInfiniteMoney ? 1 : CheatMoneyMultiplier),
                           "--ea_cheat_experience_multiplier=" + CheatExperienceMultiplier };
        }
    }
    sealed class CheatsPage : FlowLayoutPanel
    {
        readonly CheckBox hp = Option("Infinite HP · player party", "InfiniteHP");
        readonly CheckBox ep = Option("Infinite EP · player party", "InfiniteEP");
        readonly CheckBox money = Option("Infinite money · TB", "InfiniteMoney");
        readonly CheckBox sp = Option("Infinite upgrade points · SP", "InfiniteSP");
        readonly ComboBox moneyMultiplier = Theme.Choice(180, "1× · Normal", "2×", "5×", "10×");
        readonly ComboBox experienceMultiplier =
            Theme.Choice(180, "1× · Normal", "2×", "5×", "10×");
        static CheckBox Option(string caption, string name)
        {
            return new CheckBox { Text = caption,           Name = name,
                                  AccessibleName = caption, AutoSize = true,
                                  ForeColor = Theme.Text,   Margin = new Padding(0, 5, 0, 5) };
        }
        public CheatsPage()
        {
            Dock = DockStyle.Top;
            AutoSize = true;
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            Padding = new Padding(6, 0, 2, 12);
            Controls.Add(Theme.Heading("Cheats"));
            Controls.Add(hp);
            Controls.Add(ep);
            Controls.Add(money);
            Controls.Add(sp);
            AddMultiplier("Money earned", moneyMultiplier);
            AddMultiplier("Experience earned", experienceMultiplier);
            money.CheckedChanged += (s, e) => moneyMultiplier.Enabled = !money.Checked;
            var reset = Theme.Button("Turn all cheats off", 200);
            reset.Click += (s, e) => LoadValues(new Preferences());
            Controls.Add(reset);
            var note = Theme.Label(
                "Extra money, SP and earned levels stay in your save after " +
                    "cheats are turned off. Use a separate slot to keep your original progress.",
                520);
            note.Margin = new Padding(0, 12, 0, 8);
            Controls.Add(note);
            Controls.Add(
                Theme.Label("HP/EP cheats pause for the opening Ice Queen story fight.", 520));
        }
        void AddMultiplier(string caption, ComboBox choice)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false,
                                            Margin = new Padding(0, 4, 0, 4) };
            var label = Theme.Label(caption, 230);
            label.AutoSize = false;
            label.Size = new Size(180, 25);
            label.Margin = new Padding(0, 9, 10, 0);
            choice.AccessibleName = caption;
            row.Controls.Add(label);
            row.Controls.Add(choice);
            Controls.Add(row);
        }
        internal void LoadValues(Preferences p)
        {
            p.NormalizeCheats();
            hp.Checked = p.CheatInfiniteHP;
            ep.Checked = p.CheatInfiniteEP;
            money.Checked = p.CheatInfiniteMoney;
            sp.Checked = p.CheatInfiniteSP;
            moneyMultiplier.SelectedIndex =
                Array.IndexOf(Preferences.CheatMultipliers, p.CheatMoneyMultiplier);
            experienceMultiplier.SelectedIndex =
                Array.IndexOf(Preferences.CheatMultipliers, p.CheatExperienceMultiplier);
            moneyMultiplier.Enabled = !money.Checked;
        }
        internal void ReadValues(Preferences p)
        {
            p.CheatInfiniteHP = hp.Checked;
            p.CheatInfiniteEP = ep.Checked;
            p.CheatInfiniteMoney = money.Checked;
            p.CheatInfiniteSP = sp.Checked;
            p.CheatMoneyMultiplier = Preferences.CheatMultipliers[moneyMultiplier.SelectedIndex];
            p.CheatExperienceMultiplier =
                Preferences.CheatMultipliers[experienceMultiplier.SelectedIndex];
        }
    }
}
