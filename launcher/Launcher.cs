// Original launcher code. Game assets and translated game code stay on the player's PC.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnchantedArmsPC {
    static class Program {
        internal static readonly string Root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        internal static readonly string Local = Path.Combine(Root, ".local");
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        internal static System.Threading.EventWaitHandle PlayRequest;
        internal const string Version = "0.3.0-detail-fps-preview";
        internal static string NativeDirectory {
            get {
                string newest = Path.Combine(Root,"bin","graphics-0.3.0");
                if (File.Exists(Path.Combine(newest,"enchantedarmsrecomp.exe"))) return newest;
                string enhanced = Path.Combine(Root,"bin","graphics-0.2.0");
                return File.Exists(Path.Combine(enhanced,"enchantedarmsrecomp.exe")) ? enhanced : Path.Combine(Root,"bin");
            }
        }

        // CommandLineToArgvW-compatible quoting, including paths ending in a backslash.
        internal static string Quote(string argument) {
            return "\"" + Regex.Replace(Regex.Replace(argument, "(\\\\*)\"", "$1$1\\\""), "(\\\\+)$", "$1$1") + "\"";
        }
        internal static string Join(params string[] args) { return String.Join(" ", args.Select(Quote)); }
        internal static string[] GameArguments(Preferences p, string log) {
            return new [] {
                "--game_data_root=" + Path.Combine(Local, "game"),
                "--user_data_root=" + Path.Combine(Local, "runtime", "user"),
                "--cache_root=" + Path.Combine(Local, "runtime", "cache"),
                "--log_file=" + log, "--log_level=info",
                "--log_max_file_size_mb=16", "--log_max_files=3",
                "--no-ea_trace_input", "--mnk_mode"
            }.Concat(p.RendererArguments()).ToArray();
        }
        internal static void Shortcut(string directory) {
            Directory.CreateDirectory(directory);
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { Path.Combine(directory, "EnchantedArmsPC.lnk") });
            try {
                Type type = shortcut.GetType();
                Action<string, object> set = (name, value) => type.InvokeMember(name, BindingFlags.SetProperty, null, shortcut, new object[] { value });
                set("TargetPath", Path.Combine(Root, "EnchantedArmsLauncher.exe"));
                set("Arguments", "--play");
                set("WorkingDirectory", Root);
                set("Description", "Play Enchanted Arms PC with your saved visual settings");
                set("IconLocation", Path.Combine(Root, "EnchantedArmsLauncher.exe") + ",0");
                type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            } finally {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
        [STAThread]
        static int Main(string[] args) {
            if (args.Length == 2 && args[0] == "--graphics-preview") {
                Application.EnableVisualStyles();
                using (var dialog = new GraphicsDialog(new Preferences { Upscaler="fsr",PostAA="fxaa",Scale=2,Anisotropy=5,Dither=true,TextureDetail=1,FrameLimit=60 })) {
                    dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-20000,-20000);
                    dialog.Show(); Application.DoEvents(); dialog.PerformLayout();
                    using (var bitmap = new Bitmap(dialog.Width,dialog.Height)) {
                        dialog.DrawToBitmap(bitmap,new Rectangle(0,0,dialog.Width,dialog.Height)); bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                return 0;
            }
            if (args.Length == 2 && args[0] == "--shortcut-test") { Shortcut(args[1]); return 0; }
            if (args.Length == 1 && args[0] == "--self-test") {
                Preferences p = new Preferences { Scale = 2, Anisotropy = 4 };
                string[] command = GameArguments(p, "test.log");
                if (!command.Contains("--draw_resolution_scale_x=2") ||
                    !command.Contains("--draw_resolution_scale_y=2") ||
                    !command.Contains("--anisotropic_override=4") ||
                    !command.Contains("--no-ea_trace_input")) return 1;
                if (Quote("C:\\test folder\\") != "\"C:\\test folder\\\\\"") return 2;
                // Older launcher JSON keeps independent render/filter settings.
                var migrated = Json.Deserialize<Preferences>("{\"Scale\":2,\"Anisotropy\":4,\"Width\":2560,\"Height\":1440}");
                migrated.Normalize();
                if (migrated.Scale != 2 || migrated.Anisotropy != 4 || migrated.Upscaler != "bilinear" || migrated.Width != 2560) return 3;
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                try {
                    System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                    migrated.Upscaler = "fsr"; migrated.Sharpness = 80;
                    if (!migrated.RendererArguments().Contains("--present_fsr_sharpness_reduction=0.4")) return 4;
                    migrated.Upscaler = "cas";
                    if (!migrated.RendererArguments().Contains("--present_cas_additional_sharpness=0.8")) return 5;
                } finally { System.Threading.Thread.CurrentThread.CurrentCulture = culture; }
                var roundtrip = Json.Deserialize<Preferences>(Json.Serialize(migrated));
                if (roundtrip.Upscaler != "cas" || roundtrip.Sharpness != 80) return 6;
                var invalid = new Preferences { Scale=99,Sharpness=-4,Anisotropy=42,Upscaler="unknown",PostAA="unknown",Width=-1 };
                invalid.Normalize();
                if (invalid.Scale != 4 || invalid.Sharpness != 0 || invalid.Anisotropy != 5 || invalid.Upscaler != "bilinear" || invalid.PostAA != "none" || invalid.Width != 1920) return 7;
                if (migrated.FrameLimit != 0 || migrated.TextureDetail != 0) return 8;
                foreach (int limit in Preferences.FrameLimits) {
                    var selected = new Preferences { FrameLimit=limit, TextureDetail=1 };
                    var arguments = selected.RendererArguments();
                    if (!arguments.Contains("--ea_fps_limit=" + limit) || !arguments.Contains("--ea_texture_lod_bias=-0.5") ||
                        !arguments.Contains(limit == 0 ? "--vsync" : "--no-vsync") || !arguments.Contains("--video_mode_refresh_rate=60")) return 9;
                    var saved = Json.Deserialize<Preferences>(Json.Serialize(selected));
                    if (saved.FrameLimit != limit || saved.TextureDetail != 1) return 10;
                }
                invalid.FrameLimit=999; invalid.TextureDetail=9; invalid.Normalize();
                if (invalid.FrameLimit != 0 || invalid.TextureDetail != 2) return 11;
                return 0;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string instance;
            using (var hasher = System.Security.Cryptography.SHA256.Create())
                instance = "Local\\EnchantedArmsPC." + BitConverter.ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(Root.ToUpperInvariant()))).Replace("-", "");
            bool created;
            using (var mutex = new System.Threading.Mutex(true, instance, out created)) {
                if (!created) {
                    if (args.Contains("--play")) {
                        try { using (var request = System.Threading.EventWaitHandle.OpenExisting(instance + ".Play")) request.Set(); }
                        catch (System.Threading.WaitHandleCannotBeOpenedException) { }
                    }
                    return 0;
                }
                using (PlayRequest = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, instance + ".Play"))
                    Application.Run(new Launcher(args.Contains("--play")));
            }
            return 0;
        }
    }

    sealed class Launcher : Form {
        readonly TextBox iso = new TextBox { Dock = DockStyle.Fill };
        readonly ComboBox preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
        readonly ComboBox output = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        readonly Label graphicsSummary = new Label { AutoSize = true, MaximumSize = new Size(920,0), Margin = new Padding(0,5,0,5) };
        bool syncingGraphics;
        readonly NumericUpDown jobs = new NumericUpDown { Minimum = 1, Maximum = 8, Value = 2, Width = 55 };
        readonly Button install = MakeButton("Build from ISO", 145);
        readonly Button play = MakeButton("Play Enchanted Arms", 190);
        readonly Button cancel = MakeButton("Cancel setup", 125);
        readonly Button browse = MakeButton("Choose ISO…", 125);
        readonly Button backup = MakeButton("Back up saves",135);
        readonly Label status = new Label { AutoSize = true, ForeColor = Color.FromArgb(25, 72, 97), Margin = new Padding(0, 12, 0, 10) };
        readonly TextBox log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, Font = new Font("Consolas", 9), BackColor = Color.FromArgb(246, 248, 250) };
        readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Height = 6, Style = ProgressBarStyle.Blocks };
        readonly object logLock = new object();
        Preferences preferences;
        Process worker;
        Process game;
        string setupLog;
        bool busy;
        bool cancelRequested;
        bool failed;
        readonly bool autoPlay;
        readonly Timer stateTimer = new Timer { Interval = 2000 };
        static Button MakeButton(string text, int width) {
            return new Button { Text = text, Width = width, Height = 36, FlatStyle = FlatStyle.System, Margin = new Padding(0, 3, 10, 3) };
        }
        static Label Caption(string text) { return new Label { Text = text, AutoSize = true, Margin = new Padding(0, 8, 15, 5) }; }
        static FlowLayoutPanel Row() { return new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 3, 0, 3) }; }

        public Launcher(bool immediatelyPlay) {
            autoPlay = immediatelyPlay;
            Text = "EnchantedArmsPC • Playtest Launcher";
            Font = new Font("Segoe UI", 10);
            Size = new Size(1010, 815); MinimumSize = new Size(920, 740);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Directory.CreateDirectory(Program.Local);
            preferences = LoadPreferences();
            preferences.Normalize();
            // Reopening the launcher while its game continues must not allow another
            // game or a rebuild to access the same installation and save files.
            foreach (Process candidate in Process.GetProcessesByName("enchantedarmsrecomp")) {
                try {
                    if (candidate.MainModule.FileName.StartsWith(Path.Combine(Program.Root,"bin") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { game = candidate; break; }
                } catch { }
                candidate.Dispose();
            }
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(25, 18, 25, 18), ColumnCount = 1, RowCount = 10 };
            for (int i = 0; i < 10; ++i) layout.RowStyles.Add(new RowStyle(i == 8 ? SizeType.Percent : SizeType.AutoSize, i == 8 ? 100 : 0));
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "EnchantedArmsPC", Font = new Font("Segoe UI", 25, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(24, 48, 72) }, 0, 0);
            layout.Controls.Add(new Label { Text = "Build from your Xbox 360 ISO. Play natively on Windows. Detail / FPS preview 0.3.0", AutoSize = true, Margin = new Padding(0, 0, 0, 8) }, 0, 1);
            layout.Controls.Add(status, 0, 2);
            var source = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); source.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            source.Controls.Add(iso, 0, 0); source.Controls.Add(browse, 1, 0); iso.Text = preferences.Iso;
            layout.Controls.Add(source, 0, 3);
            var build = Row(); build.Controls.Add(install); build.Controls.Add(cancel); build.Controls.Add(Caption("Build threads")); build.Controls.Add(jobs);
            jobs.Value = Math.Max(1, Math.Min(8, preferences.Jobs));
            layout.Controls.Add(build, 0, 4);
            var graphics = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 8, 0, 8) };
            var choices = Row(); choices.Controls.Add(Caption("Visual preset")); choices.Controls.Add(preset); choices.Controls.Add(Caption("Window size")); choices.Controls.Add(output);
            preset.Items.AddRange(new object[] { "Custom / saved settings", "Original • 1x", "Balanced • FSR 1 / 1x", "Enhanced • FSR 1 / 2x", "High • CAS / 3x", "Ultra • CAS / 4x" });
            preset.SelectedIndex = 0;
            output.Items.AddRange(new object[] { "1280 × 720", "1920 × 1080", "2560 × 1440", "3840 × 2160" });
            int[] widths = {1280,1920,2560,3840}; int selectedWidth = Array.IndexOf(widths, preferences.Width); output.SelectedIndex = selectedWidth < 0 ? 1 : selectedWidth;
            graphics.Controls.Add(choices);
            var graphicsButton = MakeButton("Graphics settings…",180); choices.Controls.Add(graphicsButton);
            graphics.Controls.Add(graphicsSummary); graphicsSummary.Text = preferences.Summary();
            graphics.Controls.Add(Caption("FSR 1 / CAS, antialiasing, filtering, sharpening and display options. Changes apply next launch."));
            layout.Controls.Add(graphics, 0, 5);
            var actions = Row(); actions.Controls.Add(play);
            var shortcut = MakeButton("Create desktop shortcut", 200); actions.Controls.Add(shortcut);
            var saves = MakeButton("Open saves", 120); actions.Controls.Add(saves);
            var report = MakeButton("Export playtest report", 175); actions.Controls.Add(report);
            actions.Controls.Add(backup);
            var logs = MakeButton("Open logs",110); actions.Controls.Add(logs);
            var updates = MakeButton("Updates / issues",145); actions.Controls.Add(updates);
            layout.Controls.Add(actions, 0, 6); layout.Controls.Add(progress, 0, 7); layout.Controls.Add(log, 0, 8);
            layout.Controls.Add(new Label { Text = "First build downloads about 800 MB of tools and needs roughly 20 GB free. ISO stays local. Saves: .local/runtime/user", AutoSize = true, Margin = new Padding(0, 10, 0, 0) }, 0, 9);
            browse.Click += (s,e) => { using (var dialog = new OpenFileDialog { Filter = "Xbox 360 disc image (*.iso)|*.iso", Title = "Choose your Enchanted Arms Xbox 360 ISO" }) {
                if (dialog.ShowDialog(this) == DialogResult.OK) { iso.Text = dialog.FileName; SavePreferences(); }
            }};
            install.Click += async (s,e) => await Setup();
            cancel.Click += async (s,e) => await CancelSetup();
            play.Click += (s,e) => LaunchGame();
            shortcut.Click += (s,e) => Guard(() => { Program.Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); Append("Created desktop shortcut: EnchantedArmsPC (uses your saved visual settings)."); });
            saves.Click += (s,e) => Guard(() => { string path = Path.Combine(Program.Local, "runtime", "user"); Directory.CreateDirectory(path); Process.Start("explorer.exe", Program.Quote(path)); });
            report.Click += (s,e) => Guard(ExportReport);
            backup.Click += (s,e) => Guard(BackupSaves);
            logs.Click += (s,e) => Guard(() => { string path=Path.Combine(Program.Local,"runtime"); Directory.CreateDirectory(path); Process.Start("explorer.exe",Program.Quote(path)); });
            updates.Click += (s,e) => Guard(() => Process.Start(new ProcessStartInfo("https://github.com/IAndrewJJ/EnchantedArmsRecomp") { UseShellExecute=true }));
            preset.SelectedIndexChanged += (s,e) => { if (!syncingGraphics && preset.SelectedIndex > 0) { preferences.ApplyPreset(preset.SelectedIndex); graphicsSummary.Text = preferences.Summary(); SavePreferences(); } };
            graphicsButton.Click += (s,e) => { using (var dialog = new GraphicsDialog(preferences)) {
                if (dialog.ShowDialog(this) == DialogResult.OK) {
                    preferences = dialog.Settings; syncingGraphics = true;
                    preset.SelectedIndex = 0; output.SelectedIndex = Array.IndexOf(new [] {1280,1920,2560,3840},preferences.Width);
                    syncingGraphics = false; graphicsSummary.Text = preferences.Summary(); SavePreferences();
                }
            }};
            output.SelectedIndexChanged += (s,e) => { if (!syncingGraphics) SavePreferences(); }; jobs.ValueChanged += (s,e) => SavePreferences();
            FormClosing += OnClosing;
            stateTimer.Tick += (s,e) => {
                if (!busy) RefreshState();
                if (Program.PlayRequest != null && Program.PlayRequest.WaitOne(0)) { Show(); Activate(); LaunchGame(); }
            };
            stateTimer.Start();
            Shown += (s,e) => { if (autoPlay) LaunchGame(); };
            Append("Choose your USA/Europe Enchanted Arms Xbox 360 ISO, then build. An unsupported revision is rejected before extraction.");
            RefreshState();
        }
        Preferences LoadPreferences() {
            try { return Program.Json.Deserialize<Preferences>(File.ReadAllText(Path.Combine(Program.Local, "launcher.json"))) ?? new Preferences(); }
            catch { return new Preferences(); }
        }
        void SavePreferences() {
            preferences.Iso = iso.Text;
            int index = Math.Max(0, output.SelectedIndex);
            preferences.Width = new [] {1280,1920,2560,3840}[index]; preferences.Height = new [] {720,1080,1440,2160}[index];
            preferences.Jobs = (int)jobs.Value;
            preferences.Normalize();
            graphicsSummary.Text = preferences.Summary();
            try {
                string path=Path.Combine(Program.Local,"launcher.json"), temporary=path + ".partial";
                File.WriteAllText(temporary,Program.Json.Serialize(preferences));
                if (File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
            }
            catch (Exception error) { Append("Could not save settings: " + error.Message); }
        }
        bool Ready() { return File.Exists(Path.Combine(Program.NativeDirectory,"enchantedarmsrecomp.exe")) &&
            File.Exists(Path.Combine(Program.Local,"game","default.xex")) && File.Exists(Path.Combine(Program.Local,"installation.json")); }
        bool GameRunning() { try { return game != null && !game.HasExited; } catch { return false; } }
        void RefreshState() {
            bool running = GameRunning();
            install.Enabled = !busy && !running; browse.Enabled = !busy; iso.Enabled = !busy; jobs.Enabled = !busy;
            cancel.Enabled = busy && !cancelRequested; play.Enabled = !busy && !running && Ready();
            backup.Enabled = !busy && !running && Directory.Exists(Path.Combine(Program.Local,"runtime","user"));
            progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            if (!busy) status.Text = running ? "Game running • visual changes apply on the next launch" :
                failed ? "Last setup or game run failed • see details below" : Ready() ? "Ready to play • campaign testing in progress" : "Setup needed • select your ISO below";
        }
        void Append(string message) {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<string>(Append), message); } catch (InvalidOperationException) {} return; }
            if (log.TextLength > 250000) log.Text = log.Text.Substring(log.TextLength - 180000);
            log.AppendText(message + Environment.NewLine);
            if (message.StartsWith("STAGE: ")) status.Text = message.Substring(7);
        }
        void Guard(Action action) { try { action(); } catch (Exception error) { Append(error.Message); MessageBox.Show(this,error.Message,"EnchantedArmsPC",MessageBoxButtons.OK,MessageBoxIcon.Error); } }
        async Task Setup() {
            if (busy || GameRunning()) return;
            if (!File.Exists(iso.Text)) { Append("Choose an existing ISO file first."); return; }
            SavePreferences(); busy = true; failed = false; cancelRequested = false; RefreshState();
            string logs = Path.Combine(Program.Local,"logs"); Directory.CreateDirectory(logs);
            setupLog = Path.Combine(logs,"setup-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + ".log");
            try {
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
                var start = new ProcessStartInfo(powershell, Program.Join("-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(Program.Root,"scripts","setup.ps1"),"-Iso",iso.Text,"-Jobs",preferences.Jobs.ToString())) {
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Program.Root, RedirectStandardOutput = true, RedirectStandardError = true
                };
                worker = new Process { StartInfo = start };
                DataReceivedEventHandler receive = (s,e) => { if (e.Data != null) { lock(logLock) File.AppendAllText(setupLog,e.Data + Environment.NewLine); Append(e.Data); } };
                worker.OutputDataReceived += receive; worker.ErrorDataReceived += receive;
                worker.Start(); worker.BeginOutputReadLine(); worker.BeginErrorReadLine();
                await Task.Run(() => worker.WaitForExit());
                if (cancelRequested) Append("Setup cancelled. Retry with the same ISO to resume; saves are preserved.");
                else if (worker.ExitCode != 0) { failed = true; Append("Setup failed. Details are saved in .local/logs. Correct the reported problem and retry."); }
                else Append("Build complete. Choose a visual preset and press Play.");
            } catch (Exception error) { failed = true; Append("Setup failed: " + error.Message); }
            finally { busy = false; if (worker != null) worker.Dispose(); worker = null; RefreshState(); }
        }
        async Task CancelSetup() {
            if (worker == null || !busy || cancelRequested) return;
            cancelRequested = true; RefreshState(); Append("Stopping setup and its compiler processes…");
            try {
                // Stop only this launcher's setup tree. No unrelated game/compiler processes are touched.
                int id = worker.Id;
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"taskkill.exe"), "/PID " + id + " /T /F") { UseShellExecute = false, CreateNoWindow = true };
                using (Process killer = Process.Start(start)) { await Task.Run(() => killer.WaitForExit()); }
            } catch (Exception error) { Append("Could not stop setup: " + error.Message); cancelRequested = false; RefreshState(); }
        }
        async void LaunchGame() {
            if (busy || GameRunning()) return;
            if (!Ready()) { Append("Complete Build from ISO before playing."); return; }
            SavePreferences();
            if ((preferences.TextureDetail != 0 || preferences.FrameLimit != 0) &&
                Program.NativeDirectory != Path.Combine(Program.Root,"bin","graphics-0.3.0")) {
                Append("Texture detail and experimental FPS require the 0.3.0 native build. Choose Build from ISO once to update."); return;
            }
            if (preferences.Upscaler != "bilinear" && !File.Exists(Path.Combine(Program.NativeDirectory,"renderer-capabilities.json"))) {
                Append("FSR 1 and CAS require the enhanced renderer. Choose Build from ISO once to update this installation."); return;
            }
            try {
                string runtime = Path.Combine(Program.Local,"runtime"); Directory.CreateDirectory(runtime);
                string logPath = Path.Combine(runtime,"boot-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + ".log");
                File.WriteAllText(Path.Combine(runtime,"latest-boot.txt"),logPath);
                File.WriteAllText(Path.Combine(Program.Local,"last-launch.json"),Program.Json.Serialize(new { utc=DateTime.UtcNow.ToString("o"), settings=preferences, build=Program.Version }));
                var start = new ProcessStartInfo(Path.Combine(Program.NativeDirectory,"enchantedarmsrecomp.exe"),Program.Join(Program.GameArguments(preferences,logPath))) {
                    WorkingDirectory = Program.NativeDirectory, UseShellExecute = false, CreateNoWindow = true
                };
                Process launched = Process.Start(start); game = launched; failed = false; RefreshState();
                Append("Game started: " + preferences.Summary() + ". Hold Enter briefly for Start; Space is A; P is Y.");
                await Task.Run(() => launched.WaitForExit());
                int exit = launched.ExitCode; failed = exit != 0;
                Append("Game exited: 0x" + exit.ToString("X8") + (exit == 0 ? "" : ". Export a playtest report to inspect the failure."));
                if (Object.ReferenceEquals(game,launched)) game = null;
                launched.Dispose(); RefreshState();
            } catch (Exception error) { failed = true; Append("Could not start the game: " + error.Message); RefreshState(); }
        }
        string Sanitize(string content) {
            foreach (string path in new [] { iso.Text, Program.Root, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }.Where(x => !String.IsNullOrEmpty(x)).OrderByDescending(x => x.Length)) {
                content = Regex.Replace(content, Regex.Escape(path), "<local-path>", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, Regex.Escape(path.Replace('\\','/')), "<local-path>", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, Regex.Escape(path.Replace("\\","\\\\")), "<local-path>", RegexOptions.IgnoreCase);
            }
            return content;
        }
        void BackupSaves() {
            if (busy || GameRunning()) { Append("Close the game before creating a consistent save backup."); return; }
            string directory=Path.Combine(Program.Local,"runtime","user");
            using (var dialog = new SaveFileDialog { Filter="Save backup (*.zip)|*.zip",FileName="EnchantedArmsPC-saves-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".zip" }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (Path.GetFullPath(dialog.FileName).StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Choose a backup destination outside the active save folder.");
                using (var destination = new FileStream(dialog.FileName,FileMode.Create,FileAccess.Write))
                using (var archive = new ZipArchive(destination,ZipArchiveMode.Create)) {
                    foreach (string path in Directory.GetFiles(directory,"*",SearchOption.AllDirectories))
                        archive.CreateEntryFromFile(path,path.Substring(directory.Length + 1).Replace('\\','/'),CompressionLevel.Optimal);
                }
                Append("Save backup created. Keep it private; playtest reports never include saves.");
            }
        }
        void ExportReport() {
            using (var dialog = new SaveFileDialog { Filter = "Playtest report (*.zip)|*.zip", FileName = "EnchantedArmsPC-report-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".zip" }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                using (var destination = new FileStream(dialog.FileName,FileMode.Create,FileAccess.Write))
                using (var archive = new ZipArchive(destination,ZipArchiveMode.Create)) {
                    Action<string,string> add = (name,text) => { using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) writer.Write(Sanitize(text)); };
                    add("report.txt","EnchantedArmsPC " + Program.Version + "\r\nUTC: " + DateTime.UtcNow.ToString("o") + "\r\nWindows: " + Environment.OSVersion + "\r\nSelected settings (next launch): " + preferences.Summary() + "\r\n" + Program.Json.Serialize(preferences) + "\r\nDescribe the location, action, and whether the issue repeats when warm.\r\n\r\n" + log.Text);
                    string launch=Path.Combine(Program.Local,"last-launch.json");
                    if (File.Exists(launch)) add("last-launch.json",File.ReadAllText(launch));
                    string last = Path.Combine(Program.Local,"runtime","latest-boot.txt");
                    if (File.Exists(last)) {
                        string path = Path.Combine(Program.Local,"runtime",Path.GetFileName(File.ReadAllText(last).Trim()));
                        if (File.Exists(path)) {
                            // Sharing during gameplay must not lock the writer. Limit export to the last 4 MiB.
                            using (var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
                                stream.Seek(Math.Max(0, stream.Length - 4 * 1024 * 1024),SeekOrigin.Begin);
                                using (var reader = new StreamReader(stream)) add("game.log",reader.ReadToEnd());
                            }
                        }
                    }
                }
                Append("Report exported. It contains settings and path-redacted text logs; no saves, disc files, or memory dumps. Review the ZIP before sharing.");
            }
        }
        async void OnClosing(object sender, FormClosingEventArgs e) {
            if (busy) { e.Cancel = true; await CancelSetup(); Append("Wait for setup to stop, then close the launcher."); }
            else { SavePreferences(); stateTimer.Stop(); stateTimer.Dispose(); }
        }
    }
}
