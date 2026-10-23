using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    internal enum SettingsPage
    {
        Graphics,
        Display,
        Game,
        Controls,
        Textures,
        Updates
    }

    sealed class SettingsWindow : Form
    {
        public Preferences Settings;
        public bool BuildRequested;
        public bool UninstallRequested;
        public string UpdateStage;
        public bool RollbackRequested;
        readonly ComboBox scale = Choice("1× · Native", "2×", "3×", "4×");
        readonly ComboBox scaler = Choice("Original scaling", "AMD FSR 1", "AMD FidelityFX CAS");
        readonly ComboBox aa = Choice("Original", "FXAA", "FXAA strong", "Supersampling + FXAA");
        readonly ComboBox filtering = Choice("Original", "Off", "1×", "2×", "4×", "8×", "16×");
        readonly ComboBox detail = Choice("Original", "Crisp", "Strong");
        readonly ComboBox fps = Choice("30 FPS · Stable", "60 FPS · Stable");
        readonly ComboBox display = Choice("Windowed", "Borderless fullscreen");
        readonly ComboBox size = Choice("1280 × 720", "1920 × 1080", "2560 × 1440", "3840 × 2160");
        readonly ComboBox vsync = Choice("Off", "On");
        readonly TrackBar sharpness =
            new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 20,
                           Width = 270, Height = 38,   BackColor = Theme.Background };
        readonly Label sharpnessValue = Theme.Label("80%", 60);
        readonly CheckBox dither = Check("Reduce gradient banding");
        readonly CheckBox aspect = Check("Preserve aspect ratio");
        readonly CheckBox msaa = Check("Keep the game's native antialiasing");
        readonly CheckBox frameGraph = Check("Show frame-time graph with the F6 counter");
        readonly TextBox isoPath =
            new TextBox { ReadOnly = true, Width = 335, BackColor = Theme.Panel,
                          ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
        readonly TextBox buildPath = new TextBox { Width = 335,
                                                   BackColor = Theme.Panel,
                                                   ForeColor = Theme.Text,
                                                   BorderStyle = BorderStyle.FixedSingle,
                                                   AccessibleName = "Build location",
                                                   Name = "BuildLocation" };
        readonly TextBox savePath = new TextBox { ReadOnly = true,
                                                  Width = 335,
                                                  BackColor = Theme.Panel,
                                                  ForeColor = Theme.Text,
                                                  BorderStyle = BorderStyle.FixedSingle,
                                                  AccessibleName = "Save location",
                                                  Name = "SaveLocation" };
        bool defaultSaveLocation;
        readonly Panel[] pages = new Panel[Enum.GetValues(typeof(SettingsPage)).Length];
        readonly EtherButton[] navigation =
            new EtherButton[Enum.GetValues(typeof(SettingsPage)).Length];
        readonly ControlsPage controlsPage = new ControlsPage();
        readonly TexturePacksPage texturePage;
        bool loading;
        internal SettingsPage SelectedPage { get; private set; }
        static ComboBox Choice(params string[] values)
        {
            return Theme.Choice(325, values);
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
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Full(p, Theme.Heading(heading));
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
            var label = Theme.Label(caption, 165);
            label.Margin = new Padding(0, 9, 8, 7);
            control.AccessibleName = caption;
            page.Controls.Add(label, 0, r);
            page.Controls.Add(control, 1, r);
        }
        static FlowLayoutPanel PathRow(TextBox path, Button browse)
        {
            var row =
                new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                                      WrapContents = false, Margin = new Padding(0) };
            path.Margin = new Padding(0, 12, 10, 0);
            row.Controls.Add(path);
            row.Controls.Add(browse);
            return row;
        }
        static Control LocationHeading(string text, string resetText, Action reset, bool enabled)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false,
                                            Margin = new Padding(0) };
            row.Controls.Add(Theme.Label(text, 335));
            var link = new LinkLabel { Text = resetText,
                                       AutoSize = true,
                                       LinkColor = Theme.Accent,
                                       ActiveLinkColor = Theme.Gold,
                                       Margin = new Padding(10, 3, 0, 0),
                                       Enabled = enabled };
            link.LinkClicked += (s, e) => reset();
            row.Controls.Add(link);
            return row;
        }
        void UpdateDefaultSaveLocation()
        {
            if (defaultSaveLocation && !String.IsNullOrWhiteSpace(buildPath.Text))
                savePath.Text = Path.Combine(buildPath.Text.Trim(), "Saves");
        }
        public SettingsWindow(Preferences current, bool ready = false, bool running = false,
                              SettingsPage initialTab = SettingsPage.Graphics)
        {
            Settings = current.Copy();
            Settings.Normalize();
            texturePage = new TexturePacksPage(running);
            Theme.Window(this);
            Text = "EnchantedArmsPC · Settings";
            ClientSize = new Size(830, 650);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24),
                                                ColumnCount = 2, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            Controls.Add(layout);
            var header = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false,
                                               FlowDirection = FlowDirection.TopDown,
                                               Padding = new Padding(4, 0, 0, 0) };
            var title = Theme.Heading("Settings");
            title.Margin = new Padding(0, 0, 0, 0);
            header.Controls.Add(title);
            var intro = Theme.Label("Changes apply the next time you play.", 700);
            intro.Margin = new Padding(0, 4, 0, 0);
            header.Controls.Add(intro);
            layout.Controls.Add(header, 0, 0);
            layout.SetColumnSpan(header, 2);
            var nav =
                new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                                      WrapContents = false };
            layout.Controls.Add(nav, 0, 1);
            var content = new Panel { Dock = DockStyle.Fill };
            layout.Controls.Add(content, 1, 1);
            var names = Enum.GetNames(typeof(SettingsPage));
            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                var button = Theme.Button(names[i], 138);
                navigation[i] = button;
                nav.Controls.Add(button);
                pages[i] = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = false };
                content.Controls.Add(pages[i]);
                button.Click += (s, e) => SelectPage((SettingsPage)index);
            }
            var quality = Page("Picture quality");
            pages[(int)SettingsPage.Controls].Controls.Add(controlsPage);
            pages[(int)SettingsPage.Textures].Controls.Add(texturePage);
            var updates = new UpdatesPage(running);
            pages[(int)SettingsPage.Updates].Controls.Add(updates);
            updates.InstallRequested += (stage, rollback) =>
            {
                if (!ReadAndValidateLocation())
                    return;
                UpdateStage = stage;
                RollbackRequested = rollback;
                DialogResult = DialogResult.OK;
                Close();
            };
            pages[(int)SettingsPage.Graphics].Controls.Add(quality);
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
                 Theme.Label("Higher resolutions and supersampling give a sharper picture, " +
                                 "but need more graphics power.",
                             530));
            var screen = Page("Your display");
            pages[(int)SettingsPage.Display].Controls.Add(screen);
            Row(screen, "Display mode", display);
            Row(screen, "Window size", size);
            Row(screen, "Frame rate", fps);
            vsync.Name = "VSync";
            Row(screen, "V-Sync", vsync);
            Full(screen, aspect);
            Full(screen, dither);
            Full(screen, frameGraph);
            var timing = Theme.Label("Choose 30 FPS for the original presentation or 60 FPS " +
                                         "for smoother motion.",
                                     530);
            timing.ForeColor = Theme.Gold;
            Full(screen, timing);
            Full(screen, Theme.Label("V-Sync prevents screen tearing. Leave it off to allow " +
                                         "variable refresh rate. Borderless fullscreen uses your " +
                                         "desktop resolution.",
                                     530));
            var gamePage = Page("Your game");
            gamePage.Padding = new Padding(6, 0, 2, 0);
            pages[(int)SettingsPage.Game].Controls.Add(gamePage);
            Full(gamePage, Theme.Label("Enchanted Arms · Xbox 360 disc image", 480));
            var choose = Theme.Button("Choose ISO…", 150);
            Full(gamePage, PathRow(isoPath, choose));
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
            Full(gamePage, LocationHeading("Build location", "Use launcher folder",
                                           () => buildPath.Text = Program.Root, !running));
            var chooseFolder = Theme.Button("Choose folder…", 160);
            chooseFolder.Enabled = buildPath.Enabled = !running;
            Full(gamePage, PathRow(buildPath, chooseFolder));
            chooseFolder.Click += (s, e) =>
            {
                using (
                    var dialog =
                        new FolderBrowserDialog {
                            Description =
                                "Use this launcher's folder to keep everything together, or " +
                                "choose an existing EnchantedArmsPC project or empty game folder.",
                            ShowNewFolderButton = true,
                            SelectedPath =
                                Directory.Exists(buildPath.Text) ? buildPath.Text : Program.Root
                        }) if (dialog.ShowDialog(this) == DialogResult.OK) buildPath.Text =
                    dialog.SelectedPath;
            };
            buildPath.TextChanged += (s, e) => UpdateDefaultSaveLocation();
            Full(gamePage, LocationHeading("Save location", "Use default",
                                           () =>
                                           {
                                               defaultSaveLocation = true;
                                               UpdateDefaultSaveLocation();
                                           },
                                           !running));
            var chooseSaves = Theme.Button("Choose folder…", 160);
            chooseSaves.Enabled = savePath.Enabled = !running;
            Full(gamePage, PathRow(savePath, chooseSaves));
            chooseSaves.Click += (s, e) =>
            {
                using (var dialog = new FolderBrowserDialog {
                    Description =
                        "Choose a folder for your Enchanted Arms saves. Existing progress will " +
                        "be copied here without overwriting different saves.",
                    ShowNewFolderButton = true,
                    SelectedPath = Directory.Exists(savePath.Text) ? savePath.Text : Program.Root
                }) if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    defaultSaveLocation = InstallLocation.Same(
                        dialog.SelectedPath, Path.Combine(buildPath.Text, "Saves"));
                    savePath.Text = dialog.SelectedPath;
                }
            };
            Full(gamePage,
                 Theme.Label("Keep the locations shown for one tidy game folder. " +
                                 "Changing the save location copies your progress safely. " +
                                 "Your ISO stays where it is.",
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
                if (!ReadAndValidateLocation(true))
                    return;
                BuildRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            if (running)
                Full(gamePage,
                     Theme.Label("Close the game before rebuilding your installation.", 480));
            var installationActions = new FlowLayoutPanel { AutoSize = true, WrapContents = false,
                                                            Margin = new Padding(0, 18, 0, 0) };
            Full(gamePage, installationActions);
            var shortcut = Theme.Button("Create desktop shortcut", 230);
            installationActions.Controls.Add(shortcut);
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
            var uninstall = Theme.Button("Uninstall EnchantedArmsPC…", 260);
            uninstall.Enabled = !running;
            installationActions.Controls.Add(uninstall);
            Full(gamePage,
                 Theme.Label(
                     "Uninstalling removes this game folder and the saves stored inside it. " +
                         "Saves in a separate location are kept. You will be asked to confirm.",
                     480));
            uninstall.Click += (s, e) =>
            {
                try
                {
                    Uninstall.ValidateRoot(Program.Root);
                    using (var confirmation = new UninstallDialog(Program.Root))
                    {
                        if (confirmation.ShowDialog(this) != DialogResult.OK)
                            return;
                    }
                    UninstallRequested = true;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, error.Message, "Cannot uninstall", MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                }
            };
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill,
                                               FlowDirection = FlowDirection.RightToLeft,
                                               Padding = new Padding(0, 12, 0, 0) };
            layout.Controls.Add(footer, 0, 2);
            layout.SetColumnSpan(footer, 2);
            var apply = Theme.Button("Apply", 105, true);
            var cancel = Theme.Button("Cancel", 105);
            apply.Margin = cancel.Margin = new Padding(10, 0, 0, 0);
            cancel.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(apply);
            footer.Controls.Add(cancel);
            AcceptButton = apply;
            CancelButton = cancel;
            apply.Click += (s, e) =>
            {
                if (!ReadAndValidateLocation())
                    return;
                DialogResult = DialogResult.OK;
                Close();
            };
            sharpness.ValueChanged += (s, e) => sharpnessValue.Text = sharpness.Value + "%";
            scaler.SelectedIndexChanged += (s, e) =>
            {
                sharpness.Enabled = scaler.SelectedIndex > 0;
                sharpnessValue.Enabled = sharpness.Enabled;
            };
            display.SelectedIndexChanged += (s, e) => size.Enabled = display.SelectedIndex == 0;
            aa.SelectedIndexChanged += (s, e) =>
            {
                if (!loading && aa.SelectedIndex == 3)
                    scale.SelectedIndex = Math.Max(scale.SelectedIndex, Math.Min(3, new[] {
                        1280, 1920, 2560, 3840
                    }[Math.Max(0, size.SelectedIndex)] / 1280));
            };
            LoadValues();
            SelectPage(initialTab);
        }
        internal void SelectPage(SettingsPage page)
        {
            int selected = (int)page;
            if (selected < 0 || selected >= pages.Length)
                throw new ArgumentOutOfRangeException("page");
            SelectedPage = page;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = i == selected;
                navigation[i].Selected = i == selected;
                navigation[i].Invalidate();
            }
            pages[selected].BringToFront();
            ActiveControl = navigation[selected];
        }
        void LoadValues()
        {
            loading = true;
            scale.SelectedIndex = Settings.Scale - 1;
            scaler.SelectedIndex =
                Array.IndexOf(new[] { "bilinear", "fsr", "cas" }, Settings.Upscaler);
            aa.SelectedIndex =
                Array.IndexOf(new[] { "none", "fxaa", "fxaa_extreme", "ssaa" }, Settings.PostAA);
            filtering.SelectedIndex = Settings.Anisotropy + 1;
            detail.SelectedIndex = Settings.TextureDetail;
            fps.SelectedIndex = Array.IndexOf(Preferences.FrameLimits, Settings.FrameLimit);
            display.SelectedIndex = Settings.Fullscreen ? 1 : 0;
            size.SelectedIndex = Array.IndexOf(new[] { 1280, 1920, 2560, 3840 }, Settings.Width);
            sharpness.Value = Settings.Sharpness;
            sharpnessValue.Text = sharpness.Value + "%";
            dither.Checked = Settings.Dither;
            aspect.Checked = Settings.KeepAspect;
            // Tearing is the renderer's host-presentation switch; guest frame pacing
            // remains independent so display synchronization cannot change game speed.
            vsync.SelectedIndex = Settings.AllowTearing ? 0 : 1;
            msaa.Checked = Settings.NativeMsaa;
            frameGraph.Checked = Settings.FrameTimeGraph;
            isoPath.Text = Settings.Iso;
            defaultSaveLocation = String.IsNullOrWhiteSpace(Settings.SaveLocation);
            buildPath.Text = InstallLocation.Selected(Settings);
            savePath.Text =
                defaultSaveLocation ? Path.Combine(buildPath.Text, "Saves") : Settings.SaveLocation;
            controlsPage.LoadValues(Settings);
            texturePage.LoadSelection(Settings.TexturePack);
            loading = false;
        }
        internal void ReadValues()
        {
            Settings.Scale = scale.SelectedIndex + 1;
            Settings.Upscaler = new[] { "bilinear", "fsr", "cas" }[scaler.SelectedIndex];
            Settings.PostAA = new[] { "none", "fxaa", "fxaa_extreme", "ssaa" }[aa.SelectedIndex];
            Settings.Anisotropy = filtering.SelectedIndex - 1;
            Settings.TextureDetail = detail.SelectedIndex;
            Settings.FrameLimit = Preferences.FrameLimits[fps.SelectedIndex];
            Settings.Fullscreen = display.SelectedIndex == 1;
            Settings.Width = new[] { 1280, 1920, 2560, 3840 }[size.SelectedIndex];
            Settings.Sharpness = sharpness.Value;
            Settings.Dither = dither.Checked;
            Settings.KeepAspect = aspect.Checked;
            Settings.AllowTearing = vsync.SelectedIndex == 0;
            Settings.NativeMsaa = msaa.Checked;
            Settings.FrameTimeGraph = frameGraph.Checked;
            Settings.TexturePack = texturePage.SelectedToken;
            Settings.Iso = isoPath.Text;
            Settings.BuildLocation =
                InstallLocation.Same(buildPath.Text, Program.Root) ? "" : buildPath.Text.Trim();
            Settings.SaveLocation = defaultSaveLocation ? "" : savePath.Text;
            controlsPage.ReadValues(Settings);
            Settings.Normalize();
        }
        bool ReadAndValidateLocation(bool building = false)
        {
            try
            {
                buildPath.Text = InstallLocation.Validate(buildPath.Text, building);
                SaveStorage.Validate(buildPath.Text, savePath.Text);
                ReadValues();
                return true;
            }
            catch (Exception error)
            {
                SelectPage(SettingsPage.Game);
                MessageBox.Show(this, error.Message, "Choose a build folder", MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                buildPath.Focus();
                return false;
            }
        }
    }
}
