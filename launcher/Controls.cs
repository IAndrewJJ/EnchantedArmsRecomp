using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;

namespace EnchantedArmsPC
{
    public sealed partial class Preferences
    {
        public static readonly string[] ControlNames = {
            "A / Confirm",       "B / Back",       "X",
            "Y / Menu",          "Start",          "Back",
            "Left shoulder",     "Right shoulder", "Left stick press",
            "Right stick press", "D-pad up",       "D-pad down",
            "D-pad left",        "D-pad right",    "Left trigger",
            "Right trigger",     "Move up",        "Move down",
            "Move left",         "Move right",     "Camera up",
            "Camera down",       "Camera left",    "Camera right"
        };
        internal static readonly string[] ControlFlags = { "a",
                                                           "b",
                                                           "x",
                                                           "y",
                                                           "start",
                                                           "back",
                                                           "left_shoulder",
                                                           "right_shoulder",
                                                           "lstick_press",
                                                           "rstick_press",
                                                           "dpad_up",
                                                           "dpad_down",
                                                           "dpad_left",
                                                           "dpad_right",
                                                           "left_trigger",
                                                           "right_trigger",
                                                           "lstick_up",
                                                           "lstick_down",
                                                           "lstick_left",
                                                           "lstick_right",
                                                           "rstick_up",
                                                           "rstick_down",
                                                           "rstick_left",
                                                           "rstick_right" };
        public static readonly string[] DefaultKeys = { "Space,Semicolon",
                                                        "Backspace,Quote",
                                                        "L",
                                                        "P",
                                                        "Return,X",
                                                        "Tab,Z",
                                                        "1",
                                                        "3",
                                                        "F",
                                                        "K",
                                                        "Shift+Up",
                                                        "Shift+Down",
                                                        "Shift+Left",
                                                        "Shift+Right",
                                                        "Q,I",
                                                        "E,O",
                                                        "W",
                                                        "S",
                                                        "A",
                                                        "D",
                                                        "Up",
                                                        "Down",
                                                        "Left",
                                                        "Right" };
        public string[] Keys = (string[])DefaultKeys.Clone();
        public int[] PadButtons = Enumerable.Range(0, 14).ToArray();
        public int LeftDeadzone = 0, RightDeadzone = 0, CameraResponse = 100,
                   MouseSensitivity = 100;
        public bool InvertCameraY = false, MouseCamera = false, SwapTriggers = false;
        static readonly HashSet<string> NamedKeys = new HashSet<string>(
            ("Backtick Minus Plus Comma Period Semicolon Slash Backslash LBracket RBracket Quote " +
             "Escape Return Space Tab Backspace Delete Insert Home End PageUp PageDown Left " +
             "Right Up Down NumpadEnter NumpadPlus NumpadMinus NumpadStar NumpadSlash " +
             "PrintScreen Pause CapsLock NumLock ScrollLock LMB RMB MMB")
                .Split(' '));
        public static bool ValidBinding(string binding)
        {
            if (binding == null || binding.Length > 100)
                return false;
            if (binding.Length == 0)
                return true;
            foreach (string raw in binding.Split(','))
            {
                string[] parts = raw.Trim().Split('+');
                if (parts.Length > 4 || parts.Length == 0)
                    return false;
                for (int i = 0; i < parts.Length - 1; i++)
                    if (!new[] { "Shift", "Ctrl", "Control", "Alt" }.Contains(parts[i]))
                        return false;
                string key = parts[parts.Length - 1];
                if (key == "F2" || key == "F6" || key == "F8")
                    return false;
                if (!NamedKeys.Contains(key) &&
                    !Regex.IsMatch(key, @"^(?:[A-Z0-9]|F(?:[1-9]|1[0-9]|2[0-4])|Numpad[0-9])$"))
                    return false;
            }
            return true;
        }
        internal void NormalizeControls()
        {
            if (Keys == null || Keys.Length != DefaultKeys.Length)
                Keys = (string[])DefaultKeys.Clone();
            for (int i = 0; i < Keys.Length; i++)
                if (!ValidBinding(Keys[i]))
                    Keys[i] = DefaultKeys[i];
            if (PadButtons == null || PadButtons.Length != 14 ||
                PadButtons.Any(x => x < 0 || x >= 14) || PadButtons.Distinct().Count() != 14)
                PadButtons = Enumerable.Range(0, 14).ToArray();
            LeftDeadzone = Math.Max(0, Math.Min(40, LeftDeadzone));
            RightDeadzone = Math.Max(0, Math.Min(40, RightDeadzone));
            CameraResponse = Math.Max(25, Math.Min(200, CameraResponse));
            MouseSensitivity = Math.Max(1, Math.Min(1000, MouseSensitivity));
        }
        internal bool CustomNativeControls
        {
            get {
                NormalizeControls();
                return LeftDeadzone != 0 || RightDeadzone != 0 || CameraResponse != 100 ||
                       InvertCameraY || SwapTriggers || MouseCamera ||
                       !PadButtons.SequenceEqual(Enumerable.Range(0, 14));
            }
        }
        internal string[] ControlArguments(bool currentNative)
        {
            NormalizeControls();
            var result = new List<string>();
            for (int action = 0; action < Keys.Length; action++)
            {
                // Native remapping runs after keyboard/pad merging. Route keyboard
                // actions through the inverse map so pad remaps don't change them.
                int source = currentNative && action < 14 ? PadButtons[action] : action;
                if (currentNative && SwapTriggers && action >= 14 && action <= 15)
                    source = 29 - action;
                result.Add("--keybind_" + ControlFlags[source] + "=" + Keys[action]);
            }
            result.Add(MouseCamera ? "--mnk_mouse" : "--no-mnk_mouse");
            result.Add("--mnk_sensitivity=" +
                       (MouseSensitivity / 100.0).ToString("0.##", CultureInfo.InvariantCulture));
            if (currentNative)
            {
                result.Add("--ea_pad_map=" + String.Join(",", PadButtons));
                result.Add("--ea_left_deadzone=" +
                           (LeftDeadzone / 100.0).ToString("0.##", CultureInfo.InvariantCulture));
                result.Add("--ea_right_deadzone=" +
                           (RightDeadzone / 100.0).ToString("0.##", CultureInfo.InvariantCulture));
                result.Add("--ea_camera_response=" +
                           (CameraResponse / 100.0).ToString("0.##", CultureInfo.InvariantCulture));
                result.Add(InvertCameraY ? "--ea_invert_camera_y" : "--no-ea_invert_camera_y");
                result.Add(SwapTriggers ? "--ea_swap_triggers" : "--no-ea_swap_triggers");
            }
            return result.ToArray();
        }
    }

    sealed class ControlsPage : UserControl
    {
        readonly TextBox[] keys = new TextBox[Preferences.ControlNames.Length];
        readonly ComboBox[] pads = new ComboBox[14];
        readonly NumericUpDown left = Number(0, 40), right = Number(0, 40),
                               response = Number(25, 200), mouseSpeed = Number(1, 1000);
        readonly CheckBox invert = Check("Invert camera Y"),
                          mouse = Check("Mouse camera · F2 releases/captures mouse"),
                          swap = Check("Swap controller triggers");
        bool changing;
        static CheckBox Check(string text)
        {
            return new CheckBox { Text = text, AutoSize = true, ForeColor = Theme.Text };
        }
        static NumericUpDown Number(int min, int max)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Width = 90,
                                       BackColor = Theme.Panel, ForeColor = Theme.Text };
        }
        public ControlsPage()
        {
            AutoSize = true;
            Dock = DockStyle.Top;
            BackColor = Theme.Background;
            var grid = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top,
                                              ColumnCount = 3, Padding = new Padding(6, 0, 2, 12) };
            Controls.Add(grid);
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            var heading = Theme.Heading("Your controls");
            grid.Controls.Add(heading, 0, grid.RowCount++);
            grid.SetColumnSpan(heading, 3);
            Action<string, Control, Control> row = (name, a, b) =>
            {
                int n = grid.RowCount++;
                grid.Controls.Add(Theme.Label(name, 135), 0, n);
                grid.Controls.Add(a, 1, n);
                if (b != null)
                    grid.Controls.Add(b, 2, n);
            };
            row("Button", Theme.Label("Keyboard", 165), Theme.Label("Controller", 150));
            for (int i = 0; i < keys.Length; i++)
            {
                int index = i;
                keys[i] =
                    new TextBox { Width = 165, BackColor = Theme.Panel, ForeColor = Theme.Text,
                                  ReadOnly = true, BorderStyle = BorderStyle.FixedSingle };
                keys[i].PreviewKeyDown += (s, e) => e.IsInputKey = true;
                keys[i].KeyDown += (s, e) =>
                {
                    e.SuppressKeyPress = true;
                    string token = KeyName(e.KeyCode);
                    if (token == null)
                        return;
                    if (e.KeyCode == System.Windows.Forms.Keys.Delete)
                        token = "";
                    else
                        token = (e.Control ? "Ctrl+" : "") + (e.Alt ? "Alt+" : "") +
                                (e.Shift ? "Shift+" : "") + token;
                    if (Preferences.ValidBinding(token))
                        keys[index].Text = token;
                };
                if (i < 14)
                {
                    pads[i] = Theme.Choice(150, Preferences.ControlNames.Take(14).ToArray());
                    pads[i].SelectedIndexChanged += (s, e) =>
                    {
                        if (changing)
                            return;
                        int selected = pads[index].SelectedIndex;
                        int other = Array.FindIndex(pads, c => c != pads[index] &&
                                                               c.SelectedIndex == selected);
                        if (other < 0)
                            return;
                        int missing = Enumerable.Range(0, 14).First(
                            n => !pads.Any(c => c.SelectedIndex == n));
                        changing = true;
                        pads[other].SelectedIndex = missing;
                        changing = false;
                    };
                }
                row(Preferences.ControlNames[i], keys[i], i < 14 ? pads[i] : null);
            }
            row("Left dead zone %", left, null);
            row("Right dead zone %", right, null);
            row("Camera response %", response, null);
            row("Mouse sensitivity %", mouseSpeed, null);
            foreach (var c in new[] { invert, mouse, swap })
            {
                int n = grid.RowCount++;
                grid.Controls.Add(c, 0, n);
                grid.SetColumnSpan(c, 3);
            }
            int info = grid.RowCount++;
            var help = Theme.Label(
                "Click a keyboard field and press a key combination. Delete clears it. F2/F6/F8 " +
                    "stay reserved. Controller remaps swap assignments automatically. Camera " +
                    "response changes stick input within the game's limits.",
                465);
            grid.Controls.Add(help, 0, info);
            grid.SetColumnSpan(help, 3);
            var reset = Theme.Button("Reset controls", 180);
            int r = grid.RowCount++;
            grid.Controls.Add(reset, 0, r);
            grid.SetColumnSpan(reset, 3);
            reset.Click += (s, e) => LoadValues(new Preferences());
        }
        static string KeyName(System.Windows.Forms.Keys key)
        {
            int code = (int)key;
            if (code >= 65 && code <= 90 || code >= 48 && code <= 57)
                return ((char)code).ToString();
            if (code >= 112 && code <= 135)
                return "F" + (code - 111);
            if (code >= 96 && code <= 105)
                return "Numpad" + (code - 96);
            switch (key)
            {
            case System.Windows.Forms.Keys.Enter:
                return "Return";
            case System.Windows.Forms.Keys.Back:
                return "Backspace";
            case System.Windows.Forms.Keys.Prior:
                return "PageUp";
            case System.Windows.Forms.Keys.Next:
                return "PageDown";
            case System.Windows.Forms.Keys.OemSemicolon:
                return "Semicolon";
            case System.Windows.Forms.Keys.OemQuotes:
                return "Quote";
            case System.Windows.Forms.Keys.Oemcomma:
                return "Comma";
            case System.Windows.Forms.Keys.OemPeriod:
                return "Period";
            case System.Windows.Forms.Keys.OemQuestion:
                return "Slash";
            case System.Windows.Forms.Keys.OemPipe:
                return "Backslash";
            case System.Windows.Forms.Keys.OemMinus:
                return "Minus";
            case System.Windows.Forms.Keys.Oemplus:
                return "Plus";
            case System.Windows.Forms.Keys.OemOpenBrackets:
                return "LBracket";
            case System.Windows.Forms.Keys.OemCloseBrackets:
                return "RBracket";
            case System.Windows.Forms.Keys.Oemtilde:
                return "Backtick";
            }
            string name = key.ToString();
            return Preferences.ValidBinding(name) ? name : null;
        }
        internal void LoadValues(Preferences p)
        {
            p.NormalizeControls();
            changing = true;
            for (int i = 0; i < keys.Length; i++)
                keys[i].Text = p.Keys[i];
            for (int i = 0; i < 14; i++)
                pads[i].SelectedIndex = p.PadButtons[i];
            changing = false;
            left.Value = p.LeftDeadzone;
            right.Value = p.RightDeadzone;
            response.Value = p.CameraResponse;
            mouseSpeed.Value = p.MouseSensitivity;
            invert.Checked = p.InvertCameraY;
            mouse.Checked = p.MouseCamera;
            swap.Checked = p.SwapTriggers;
        }
        internal void ReadValues(Preferences p)
        {
            p.Keys = keys.Select(c => c.Text).ToArray();
            p.PadButtons = pads.Select(c => c.SelectedIndex).ToArray();
            p.LeftDeadzone = (int)left.Value;
            p.RightDeadzone = (int)right.Value;
            p.CameraResponse = (int)response.Value;
            p.MouseSensitivity = (int)mouseSpeed.Value;
            p.InvertCameraY = invert.Checked;
            p.MouseCamera = mouse.Checked;
            p.SwapTriggers = swap.Checked;
            p.NormalizeControls();
        }
    }
}
