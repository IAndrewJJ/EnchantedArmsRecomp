using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    sealed class GraphicsDialog : Form
    {
        public Preferences Settings;
        public bool BuildRequested;
        readonly ComboBox preset =
            Choice("Custom", "Original", "Balanced", "Enhanced", "High", "Ultra");
        readonly ComboBox scale = Choice("1× · Native", "2×", "3×", "4×");
        readonly ComboBox scaler = Choice("Original scaling", "AMD FSR 1", "AMD FidelityFX CAS");
        readonly ComboBox aa = Choice("Original", "FXAA", "FXAA strong");
        readonly ComboBox filtering = Choice("Original", "Off", "1×", "2×", "4×", "8×", "16×");
        readonly ComboBox detail = Choice("Original", "Crisp", "Strong");
        readonly ComboBox fps =
            Choice("Original · Recommended", "30 FPS · Experimental", "60 FPS · Experimental",
                   "90 FPS · Experimental", "120 FPS · Experimental", "144 FPS · Experimental",
                   "165 FPS · Experimental", "180 FPS · Experimental", "240 FPS · Experimental",
                   "360 FPS · Experimental", "Unlimited · Experimental");
        readonly ComboBox display = Choice("Windowed", "Borderless fullscreen");
        readonly ComboBox size = Choice("1280 × 720", "1920 × 1080", "2560 × 1440", "3840 × 2160");
        readonly ComboBox renderPath =
            Choice("Automatic", "ROV · Accuracy comparison", "RTV · Performance comparison");
        readonly ComboBox readback = Choice("Disabled", "Delayed · Every frame",
                                            "Delayed · On demand", "Immediate · Slow diagnostic");
        readonly TrackBar sharpness =
            new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 20,
                           Width = 270, Height = 38,   BackColor = Theme.Background };
        readonly Label sharpnessValue = Theme.Label("80%", 60);
        readonly CheckBox dither = Check("Reduce gradient banding");
        readonly CheckBox aspect = Check("Preserve aspect ratio");
        readonly CheckBox tearing = Check("Allow variable refresh rate / tearing");
        readonly CheckBox msaa = Check("Keep the game's native antialiasing");
        readonly CheckBox asyncShaders = Check("Compile shaders in the background");
        readonly TextBox isoPath =
            new TextBox { ReadOnly = true, Width = 505, BackColor = Theme.Panel,
                          ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
        readonly Panel[] pages = new Panel[4];
        readonly EtherButton[] navigation = new EtherButton[4];
        bool loading;
        static ComboBox Choice(params string[] values)
        {
            var c = new EtherCombo { DropDownStyle = ComboBoxStyle.DropDownList,
                                     Width = 325,
                                     ItemHeight = 25,
                                     DrawMode = DrawMode.OwnerDrawFixed,
                                     BackColor = Theme.Panel,
                                     ForeColor = Theme.Text,
                                     Margin = new Padding(0, 4, 0, 4) };
            c.Items.AddRange(values);
            c.DrawItem += (s, e) =>
            {
                if (e.Index < 0)
                    return;
                using (var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0
                                                     ? Color.FromArgb(33, 78, 96)
                                                     : Theme.Panel))
                    e.Graphics.FillRectangle(fill, e.Bounds);
                TextRenderer.DrawText(
                    e.Graphics, c.Items[e.Index].ToString(), c.Font,
                    new Rectangle(e.Bounds.X + 7, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                    Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.DrawFocusRectangle();
            };
            return c;
        }
        static CheckBox Check(string text)
        {
            return new CheckBox { Text = text, AutoSize = true, ForeColor = Theme.Text,
                                  Margin = new Padding(0, 9, 0, 9) };
        }
        static TableLayoutPanel Page(string heading)
        {
            var p = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top,
                                           Padding = new Padding(6, 0, 2, 12),
                                           BackColor = Theme.Background };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var title = Theme.Label(heading, 500);
            title.Font = new Font("Palatino Linotype", 20);
            title.ForeColor = Theme.Text;
            Full(p, title);
            return p;
        }
        static void Full(TableLayoutPanel page, Control control)
        {
            int r = page.RowCount++;
            page.Controls.Add(control, 0, r);
            page.SetColumnSpan(control, 2);
        }
        static void Row(TableLayoutPanel page, string caption, Control control)
        {
            int r = page.RowCount++;
            var label = Theme.Label(caption, 150);
            label.Margin = new Padding(0, 9, 8, 7);
            page.Controls.Add(label, 0, r);
            page.Controls.Add(control, 1, r);
        }
        public GraphicsDialog(Preferences current, bool ready = false, bool running = false,
                              int initialTab = 0)
        {
            Settings = current.Copy();
            Settings.Normalize();
            Theme.Window(this);
            Text = "EnchantedArmsPC · Settings";
            ClientSize = new Size(790, 650);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24),
                                                ColumnCount = 2, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            Controls.Add(layout);
            var intro = Theme.Label("SETTINGS\r\nChanges apply the next time you play.", 690);
            intro.ForeColor = Theme.Accent;
            intro.Font = new Font("Segoe UI", 11);
            intro.Margin = new Padding(4, 10, 0, 0);
            layout.Controls.Add(intro, 0, 0);
            layout.SetColumnSpan(intro, 2);
            var nav =
                new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                                      WrapContents = false };
            layout.Controls.Add(nav, 0, 1);
            var content = new Panel { Dock = DockStyle.Fill };
            layout.Controls.Add(content, 1, 1);
            var names = new[] { "Graphics", "Display", "Advanced", "Game" };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var button = Theme.Button(names[i], 125);
                navigation[i] = button;
                nav.Controls.Add(button);
                pages[i] = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = false };
                content.Controls.Add(pages[i]);
                button.Click += (s, e) => SelectPage(index);
            }
            var quality = Page("Picture quality");
            pages[0].Controls.Add(quality);
            Row(quality, "Preset", preset);
            Row(quality, "Render resolution", scale);
            Row(quality, "Upscaling", scaler);
            Row(quality, "Antialiasing", aa);
            Row(quality, "Texture filtering", filtering);
            Row(quality, "Texture detail", detail);
            var sharpRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            sharpRow.Controls.Add(sharpness);
            sharpRow.Controls.Add(sharpnessValue);
            Row(quality, "Sharpening", sharpRow);
            Full(quality, msaa);
            Full(quality,
                 Theme.Label("Higher render resolutions need more GPU power. Texture detail " +
                                 "sharpens existing textures; Strong may shimmer.",
                             480));
            var screen = Page("Your display");
            pages[1].Controls.Add(screen);
            Row(screen, "Display mode", display);
            Row(screen, "Window resolution", size);
            Row(screen, "Frame limit", fps);
            Full(screen, aspect);
            Full(screen, tearing);
            Full(screen, dither);
            var timing =
                Theme.Label("Use Original FPS for normal gameplay. Higher limits are " +
                                "experimental and can speed " +
                                "up the game; a normal-speed high-FPS patch is not implemented.",
                            480);
            timing.ForeColor = Theme.Gold;
            Full(screen, timing);
            var advanced = Page("Rendering options");
            pages[2].Controls.Add(advanced);
            Row(advanced, "Rendering method", renderPath);
            Row(advanced, "Resolve readback", readback);
            Full(advanced, asyncShaders);
            Full(advanced,
                 Theme.Label("Use these options to compare rendering problems, one change " +
                                 "at a time. ROV may cost performance. Immediate readback can " +
                                 "cause heavy stalls. Background compilation reduces stalls " +
                                 "but can briefly omit new effects. No NPC fix is confirmed.",
                             480));
            var gamePage = Page("Your game");
            pages[3].Controls.Add(gamePage);
            Full(gamePage, Theme.Label("Enchanted Arms · Xbox 360 disc image", 480));
            Full(gamePage, isoPath);
            var choose = Theme.Button("Choose ISO…", 150);
            Full(gamePage, choose);
            choose.Click += (s, e) =>
            {
                using (var dialog =
                           new OpenFileDialog { Filter = "Xbox 360 disc image (*.iso)|*.iso",
                                                Title = "Choose Enchanted Arms" })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        isoPath.Text = dialog.FileName;
                }
            };
            Full(gamePage, Theme.Label("First setup downloads about 800 MB of tools and needs " +
                                           "roughly 20 GB free. Your ISO stays on your PC.",
                                       480));
            var build = Theme.Button(ready ? "Rebuild game" : "Build game", 170, true);
            build.Enabled = !running;
            Full(gamePage, build);
            build.Click += (s, e) =>
            {
                if (!File.Exists(isoPath.Text))
                {
                    MessageBox.Show(this, "Choose an existing ISO first.", "Choose your game",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ReadValues();
                BuildRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            if (running)
                Full(gamePage,
                     Theme.Label("Close the game before rebuilding your installation.", 480));
            var shortcut = Theme.Button("Create desktop shortcut", 230);
            shortcut.Margin = new Padding(0, 24, 0, 4);
            Full(gamePage, shortcut);
            var shortcutStatus = Theme.Label("Open EnchantedArmsPC from your desktop.", 480);
            Full(gamePage, shortcutStatus);
            shortcut.Click += (s, e) =>
            {
                try
                {
                    Program.Shortcut(
                        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
                    shortcutStatus.Text = "Desktop shortcut created.";
                }
                catch (Exception error)
                {
                    Program.Diagnostic("Shortcut failed: " + error);
                    shortcutStatus.Text =
                        "Could not create the shortcut. Check desktop folder access.";
                }
            };
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill,
                                               FlowDirection = FlowDirection.RightToLeft,
                                               Padding = new Padding(0, 12, 0, 0) };
            layout.Controls.Add(footer, 0, 2);
            layout.SetColumnSpan(footer, 2);
            var apply = Theme.Button("Apply", 105, true);
            var cancel = Theme.Button("Cancel", 105);
            cancel.DialogResult = DialogResult.Cancel;
            var reset = Theme.Button("Original settings", 155);
            footer.Controls.Add(apply);
            footer.Controls.Add(cancel);
            footer.Controls.Add(reset);
            AcceptButton = apply;
            CancelButton = cancel;
            apply.Click += (s, e) =>
            {
                ReadValues();
                DialogResult = DialogResult.OK;
                Close();
            };
            reset.Click += (s, e) =>
            {
                Settings.ApplyPreset(1);
                LoadValues();
            };
            preset.SelectedIndexChanged += (s, e) =>
            {
                if (!loading && preset.SelectedIndex > 0)
                {
                    int selected = preset.SelectedIndex;
                    Settings.ApplyPreset(selected);
                    LoadValues();
                    loading = true;
                    preset.SelectedIndex = selected;
                    loading = false;
                }
            };
            sharpness.ValueChanged += (s, e) => sharpnessValue.Text = sharpness.Value + "%";
            scaler.SelectedIndexChanged += (s, e) => sharpness.Enabled = scaler.SelectedIndex > 0;
            LoadValues();
            SelectPage(initialTab);
        }
        void SelectPage(int selected)
        {
            for (int i = 0; i < 4; i++)
            {
                pages[i].Visible = i == selected;
                navigation[i].Selected = i == selected;
                navigation[i].Invalidate();
            }
            pages[selected].BringToFront();
        }
        void LoadValues()
        {
            loading = true;
            preset.SelectedIndex = 0;
            scale.SelectedIndex = Settings.Scale - 1;
            scaler.SelectedIndex =
                Array.IndexOf(new[] { "bilinear", "fsr", "cas" }, Settings.Upscaler);
            aa.SelectedIndex =
                Array.IndexOf(new[] { "none", "fxaa", "fxaa_extreme" }, Settings.PostAA);
            filtering.SelectedIndex = Settings.Anisotropy + 1;
            detail.SelectedIndex = Settings.TextureDetail;
            fps.SelectedIndex = Array.IndexOf(Preferences.FrameLimits, Settings.FrameLimit);
            display.SelectedIndex = Settings.Fullscreen ? 1 : 0;
            size.SelectedIndex = Array.IndexOf(new[] { 1280, 1920, 2560, 3840 }, Settings.Width);
            sharpness.Value = Settings.Sharpness;
            sharpnessValue.Text = sharpness.Value + "%";
            dither.Checked = Settings.Dither;
            aspect.Checked = Settings.KeepAspect;
            tearing.Checked = Settings.AllowTearing;
            msaa.Checked = Settings.NativeMsaa;
            renderPath.SelectedIndex =
                Array.IndexOf(new[] { "auto", "rov", "rtv" }, Settings.RenderPath);
            readback.SelectedIndex =
                Array.IndexOf(new[] { "none", "fast", "some", "full" }, Settings.ResolveReadback);
            asyncShaders.Checked = Settings.AsyncShaders;
            isoPath.Text = Settings.Iso;
            loading = false;
        }
        internal void ReadValues()
        {
            Settings.Scale = scale.SelectedIndex + 1;
            Settings.Upscaler = new[] { "bilinear", "fsr", "cas" }[scaler.SelectedIndex];
            Settings.PostAA = new[] { "none", "fxaa", "fxaa_extreme" }[aa.SelectedIndex];
            Settings.Anisotropy = filtering.SelectedIndex - 1;
            Settings.TextureDetail = detail.SelectedIndex;
            Settings.FrameLimit = Preferences.FrameLimits[fps.SelectedIndex];
            Settings.Fullscreen = display.SelectedIndex == 1;
            Settings.Width = new[] { 1280, 1920, 2560, 3840 }[size.SelectedIndex];
            Settings.Sharpness = sharpness.Value;
            Settings.Dither = dither.Checked;
            Settings.KeepAspect = aspect.Checked;
            Settings.AllowTearing = tearing.Checked;
            Settings.NativeMsaa = msaa.Checked;
            Settings.RenderPath = new[] { "auto", "rov", "rtv" }[renderPath.SelectedIndex];
            Settings.ResolveReadback =
                new[] { "none", "fast", "some", "full" }[readback.SelectedIndex];
            Settings.AsyncShaders = asyncShaders.Checked;
            Settings.Iso = isoPath.Text;
            Settings.Normalize();
        }
    }
}
