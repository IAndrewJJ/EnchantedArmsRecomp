using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    sealed class Launcher : Form
    {
        readonly EtherBackdrop backdrop = new EtherBackdrop { Dock = DockStyle.Fill };
        readonly EtherButton play = Theme.Button("PLAY ENCHANTED ARMS", 420, true);
        readonly EtherButton settings = Theme.Button("Settings", 170);
        readonly EtherButton cancel = Theme.Button("Cancel setup", 170);
        readonly Label status = Theme.Label("Ready to play", 430);
        readonly Label summary = Theme.Label("", 435);
        readonly ProgressBar progress = new ProgressBar { Height = 3, Visible = false };
        EventHandler gameExitHandler;
        readonly Timer stateTimer = new Timer { Interval = 2000 };
        System.Threading.RegisteredWaitHandle playWait, activateWait;
        Preferences preferences;
        Process worker, game;
        string setupStage = "Preparing your game…", lastFailure;
        bool busy, cancelRequested, failed, closeWhenStopped;
        readonly bool preview;
        bool preferencesCanSave = true;
        bool uninstalling;
        public Launcher(bool preview = false)
        {
            this.preview = preview;
            Theme.Window(this);
            Text = "EnchantedArmsPC";
            ClientSize = new Size(1000, 620);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Controls.Add(backdrop);
            Directory.CreateDirectory(Program.Local);
            preferences = LoadPreferences();
            preferences.Normalize();
            if (!preview)
                foreach (Process candidate in Process.GetProcessesByName("enchantedarmsrecomp"))
                {
                    try
                    {
                        if (candidate.MainModule.FileName.StartsWith(
                                Path.Combine(Program.Root, "bin") + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            TrackGame(candidate);
                            break;
                        }
                    }
                    catch
                    {
                    }
                    candidate.Dispose();
                }
            status.ForeColor = Theme.Accent;
            status.Font = new Font("Segoe UI", 11);
            status.AutoSize = false;
            summary.Font = new Font("Segoe UI", 9);
            summary.AutoSize = false;
            play.Font = new Font("Segoe UI Semibold", 12);
            play.Height = 66;
            backdrop.Controls.Add(status);
            backdrop.Controls.Add(play);
            backdrop.Controls.Add(settings);
            backdrop.Controls.Add(cancel);
            backdrop.Controls.Add(summary);
            backdrop.Controls.Add(progress);
            var keys = Theme.Label("F6  FPS counter     ·     F8  Screenshot", 450);
            keys.Font = new Font("Segoe UI", 9);
            keys.AutoSize = false;
            backdrop.Controls.Add(keys);
            var version = Theme.Label("v" + ReleaseInfo.Version, 120);
            version.TextAlign = ContentAlignment.MiddleRight;
            version.AutoSize = false;
            version.Font = new Font("Segoe UI", 9);
            backdrop.Controls.Add(version);
            Action layout = () =>
            {
                float x = backdrop.Width / 1000f, y = backdrop.Height / 620f;
                status.SetBounds((int)(56 * x), (int)(340 * y), (int)(430 * x), (int)(28 * y));
                play.SetBounds((int)(52 * x), (int)(382 * y), (int)(420 * x), (int)(66 * y));
                settings.SetBounds((int)(52 * x), (int)(466 * y), (int)(170 * x), (int)(42 * y));
                cancel.SetBounds((int)(238 * x), (int)(466 * y), (int)(170 * x), (int)(42 * y));
                summary.SetBounds((int)(56 * x), (int)(522 * y), (int)(435 * x), (int)(28 * y));
                progress.SetBounds((int)(54 * x), (int)(452 * y), (int)(416 * x), 3);
                keys.SetBounds((int)(52 * x), (int)(582 * y), (int)(450 * x), (int)(24 * y));
                version.SetBounds((int)(835 * x), (int)(582 * y), (int)(120 * x), (int)(24 * y));
            };
            backdrop.Resize += (s, e) => layout();
            layout();
            play.Click += async (s, e) =>
            {
                if (Ready() && !Program.TimingUpdateRequired(preferences, Program.NativeDirectory))
                    await LaunchGame();
                else
                    await OpenSettings(3);
            };
            settings.Click += async (s, e) => await OpenSettings(0);
            cancel.Click += async (s, e) => await CancelSetup();
            // Exit events replace idle polling. This timer is only a fallback
            // if Windows denies an event subscription for an adopted process.
            stateTimer.Tick += (s, e) =>
            {
                if (game != null && !GameRunning())
                    CompleteGame(game);
            };
            Shown += (s, e) =>
            {
                if (preview)
                    return;
                if (game != null && !GameRunning())
                    CompleteGame(game);
                Program.Diagnostic("Launcher window shown");
                playWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(
                    Program.PlayRequest, (state, timedOut) => DispatchRequest(), null, -1, false);
                activateWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(
                    Program.ActivateRequest, (state, timedOut) => DispatchRequest(), null, -1,
                    false);
            };
            FormClosed += (s, e) =>
            {
                if (playWait != null)
                    playWait.Unregister(null);
                if (activateWait != null)
                    activateWait.Unregister(null);
                ReleaseGame();
                stateTimer.Dispose();
            };
            FormClosing += OnClosing;
            RefreshState();
        }
        void DispatchRequest()
        {
            try
            {
                if (IsDisposed || Disposing || !IsHandleCreated)
                    return;
                BeginInvoke(new Action(() =>
                                       {
                                           Show();
                                           if (WindowState == FormWindowState.Minimized)
                                               WindowState = FormWindowState.Normal;
                                           Activate();
                                           Program.Diagnostic("Activate request received");
                                           Program.RequestAcknowledged.Set();
                                       }));
            }
            catch (InvalidOperationException error)
            {
                Program.Diagnostic("Request dispatch failed: " + error.Message);
            }
        }
        Preferences LoadPreferences()
        {
            string path = Path.Combine(Program.Local, "launcher.json");
            if (!File.Exists(path))
                return new Preferences();
            try
            {
                return Program.Json.Deserialize<Preferences>(
                           File.ReadAllText(Path.Combine(Program.Local, "launcher.json"))) ??
                       new Preferences();
            }
            catch (Exception error)
            {
                Program.Diagnostic("Could not load settings: " + error.Message);
                try
                {
                    File.Copy(path,
                              path + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"),
                              false);
                }
                catch (Exception backupError)
                {
                    preferencesCanSave = false;
                    Program.Diagnostic("Could not preserve invalid settings: " +
                                       backupError.Message);
                }
                return new Preferences();
            }
        }
        bool SavePreferences()
        {
            if (!preferencesCanSave)
            {
                failed = true;
                lastFailure = "Settings could not be read. Check folder access and restart.";
                RefreshState();
                return false;
            }
            preferences.Normalize();
            try
            {
                string path = Path.Combine(Program.Local, "launcher.json"),
                       temporary = path + ".partial";
                string text = Program.Json.Serialize(preferences);
                if (File.Exists(path) && File.ReadAllText(path) == text)
                    return true;
                File.WriteAllText(temporary, text);
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
                return true;
            }
            catch (Exception error)
            {
                Program.Diagnostic("Could not save settings: " + error);
                failed = true;
                lastFailure = "Settings could not be saved. Check folder access.";
                RefreshState();
                return false;
            }
        }
        async Task OpenSettings(int tab)
        {
            if (busy)
                return;
            using (var dialog = new GraphicsDialog(preferences, Ready(), GameRunning(), tab))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                if (dialog.UninstallRequested)
                {
                    if (GameRunning())
                    {
                        MessageBox.Show(this, "Close the game before uninstalling.");
                        return;
                    }
                    try
                    {
                        Uninstall.Start();
                        uninstalling = true;
                        Close();
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(this, error.Message, "Uninstall could not start",
                                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }
                preferences = dialog.Settings;
                failed = false;
                lastFailure = null;
                if (!SavePreferences())
                    return;
                RefreshState();
                if (dialog.UpdateStage != null || dialog.RollbackRequested)
                {
                    if (GameRunning())
                    {
                        MessageBox.Show(this, "Close the game before installing updates.");
                        return;
                    }
                    try
                    {
                        LocalTools.ApplyUpdate(dialog.UpdateStage, dialog.RollbackRequested);
                        Close();
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(this, error.Message, "Update could not start");
                    }
                    return;
                }
                if (dialog.BuildRequested)
                    await Setup();
            }
        }
        bool Ready()
        {
            return File.Exists(Path.Combine(Program.NativeDirectory, "enchantedarmsrecomp.exe")) &&
                   File.Exists(Path.Combine(Program.NativeDirectory, "rexruntimerd.dll")) &&
                   File.Exists(Path.Combine(Program.NativeDirectory, "rexgpu-xenosrd.dll")) &&
                   File.Exists(Path.Combine(Program.Local, "game", "default.xex")) &&
                   File.Exists(Path.Combine(Program.Local, "installation.json"));
        }
        internal bool HasTrackedGame
        {
            get {
                return game != null;
            }
        }
        internal void TrackGame(Process process)
        {
            if (game != null)
                ReleaseGame();
            game = process;
            gameExitHandler = (s, e) =>
            {
                if (IsDisposed || Disposing || !IsHandleCreated)
                    return;
                try
                {
                    BeginInvoke(new Action(() => CompleteGame(process)));
                }
                catch (InvalidOperationException)
                {
                }
            };
            process.Exited += gameExitHandler;
            try
            {
                process.EnableRaisingEvents = true;
            }
            catch (Exception error)
            {
                Program.Diagnostic("Game exit monitor fallback: " + error.Message);
                stateTimer.Start();
            }
        }
        internal bool UsesProcessPolling
        {
            get {
                return stateTimer.Enabled;
            }
        }
        void CompleteGame(Process process)
        {
            if (!Object.ReferenceEquals(game, process))
                return;
            try
            {
                int exit = process.ExitCode;
                failed = exit != 0;
                Program.Diagnostic("Game exited: 0x" + exit.ToString("X8"));
                if (failed)
                    lastFailure = "The game closed unexpectedly. You can try again.";
            }
            catch (InvalidOperationException)
            {
            }
            ReleaseGame();
            RefreshState();
        }
        void ReleaseGame()
        {
            stateTimer.Stop();
            if (game == null)
                return;
            if (gameExitHandler != null)
                game.Exited -= gameExitHandler;
            game.Dispose();
            game = null;
            gameExitHandler = null;
        }
        bool GameRunning()
        {
            try
            {
                return game != null && !game.HasExited;
            }
            catch
            {
                return false;
            }
        }
        void RefreshState()
        {
            bool running = GameRunning();
            play.Enabled = !busy && !running;
            settings.Enabled = !busy;
            cancel.Visible = busy && worker != null;
            cancel.Enabled = busy && !cancelRequested;
            progress.Visible = busy;
            progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            play.Text = busy      ? "PREPARING YOUR GAME"
                        : running ? "GAME RUNNING"
                        : Ready()
                            ? (Program.TimingUpdateRequired(preferences, Program.NativeDirectory)
                                   ? "UPDATE GAME"
                                   : "PLAY ENCHANTED ARMS")
                            : "SET UP GAME";
            status.Text = busy      ? setupStage
                          : running ? "Your journey is in progress"
                          : failed ? (lastFailure ?? "Unable to start. Open Settings to try again.")
                          : Ready()
                              ? (Program.TimingUpdateRequired(preferences, Program.NativeDirectory)
                                     ? "Rebuild your game to use these settings"
                                     : "Ready to play")
                              : "Choose your game disc to begin";
            summary.Text = busy ? "First setup may take a while."
                                : preferences.Scale + "× resolution  ·  " +
                                      (preferences.Upscaler == "fsr"   ? "FSR 1"
                                       : preferences.Upscaler == "cas" ? "CAS"
                                                                       : "Original scaling") +
                                      "  ·  " +
                                      (preferences.Fullscreen ? "Fullscreen" : "Windowed");
        }
        void UpdateStage(string message)
        {
            if (message.StartsWith("STAGE: "))
            {
                setupStage = message.Substring(7).Replace(" (ISO opened read-only)", "");
                RefreshState();
            }
        }
        async Task Setup()
        {
            if (busy || GameRunning())
                return;
            if (!File.Exists(preferences.Iso))
            {
                MessageBox.Show(
                    this, "Choose an existing Enchanted Arms Xbox 360 ISO in Settings → Game.",
                    "Choose your game", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!SavePreferences())
                return;
            busy = true;
            failed = false;
            lastFailure = null;
            cancelRequested = false;
            setupStage = "Preparing your game…";
            RefreshState();
            try
            {
                string logs = Path.Combine(Program.Local, "logs");
                Directory.CreateDirectory(logs);
                string setupLog = Path.Combine(
                    logs, "setup-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + ".log");
                string powershell =
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                                 "WindowsPowerShell", "v1.0", "powershell.exe");
                var start = new ProcessStartInfo(
                    powershell,
                    Program.Join("-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                                 Path.Combine(Program.Root, "scripts", "setup.ps1"), "-Iso",
                                 preferences.Iso, "-Jobs", preferences.Jobs.ToString())) {
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Program.Root,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                string failureReason = null;
                using (var output = new SetupLog(setupLog)) using (
                    var process = new Process { StartInfo = start })
                {
                    worker = process;
                    DataReceivedEventHandler receive = (s, e) =>
                    {
                        if (e.Data == null)
                            return;
                        output.WriteLine(e.Data);
                        if (e.Data.StartsWith("SETUP FAILED: "))
                            failureReason = e.Data.Substring(14);
                        if (!e.Data.StartsWith("STAGE: ") || IsDisposed || Disposing)
                            return;
                        try
                        {
                            BeginInvoke(new Action<string>(UpdateStage), e.Data);
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    };
                    process.OutputDataReceived += receive;
                    process.ErrorDataReceived += receive;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await Task.Run(() => process.WaitForExit());
                    if (!cancelRequested && process.ExitCode != 0)
                    {
                        failed = true;
                        lastFailure = "Setup could not finish. Open Settings to try again.";
                        MessageBox.Show(
                            this,
                            (failureReason ?? "Game setup could not finish. Check ISO " +
                                                  "access and free disk space, then try again.") +
                                "\n\nDetails: .local/logs/" + Path.GetFileName(setupLog),
                            "Setup incomplete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception error)
            {
                failed = true;
                Program.Diagnostic("Setup failed: " + error);
                MessageBox.Show(this, error.Message, "Setup incomplete", MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            }
            finally
            {
                busy = false;
                worker = null;
                RefreshState();
                if (closeWhenStopped)
                    Close();
            }
        }
        async Task CancelSetup()
        {
            if (worker == null || !busy || cancelRequested)
                return;
            cancelRequested = true;
            setupStage = "Stopping setup…";
            RefreshState();
            try
            {
                Process process = worker;
                var start = new ProcessStartInfo(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                                 "taskkill.exe"),
                    "/PID " + process.Id + " /T /F") { UseShellExecute = false,
                                                       CreateNoWindow = true };
                using (Process killer = Process.Start(start))
                {
                    await Task.Run(() => killer.WaitForExit());
                    if (killer.ExitCode != 0 && Object.ReferenceEquals(worker, process) &&
                        !process.HasExited)
                        throw new InvalidOperationException("Windows could not stop setup.");
                }
            }
            catch (Exception error)
            {
                Program.Diagnostic("Could not stop setup: " + error);
                cancelRequested = false;
                closeWhenStopped = false;
                RefreshState();
            }
        }
        async Task LaunchGame()
        {
            if (busy || GameRunning() || !Ready() || !SavePreferences())
                return;
            try
            {
                if (Program.TimingUpdateRequired(preferences, Program.NativeDirectory))
                    throw new InvalidOperationException(
                        "These settings need the updated game build. Open Settings → Game and " +
                        "rebuild from your ISO.");
                if (preferences.Upscaler != "bilinear" &&
                    !File.Exists(
                        Path.Combine(Program.NativeDirectory, "renderer-capabilities.json")))
                    throw new InvalidOperationException(
                        "This installation needs an update. Open " +
                        "Settings → Game and rebuild from your ISO.");
                string gameRoot = null;
                if (!String.IsNullOrEmpty(preferences.TexturePack))
                {
                    busy = true;
                    setupStage = "Preparing your textures…";
                    RefreshState();
                    var prepared =
                        await LocalTools.Run("texture_packs.py", "prepare", "--root", Program.Root,
                                             "--token", preferences.TexturePack);
                    gameRoot = Convert.ToString(prepared["game_root"]);
                }
                if (closeWhenStopped)
                    return;
                string runtime = Path.Combine(Program.Local, "runtime");
                Directory.CreateDirectory(runtime);
                string logPath = Path.Combine(
                    runtime, "boot-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + ".log");
                var start = new ProcessStartInfo(
                    Path.Combine(Program.NativeDirectory, "enchantedarmsrecomp.exe"),
                    Program.Join(Program.GameArguments(preferences, logPath, gameRoot))) {
                    WorkingDirectory = Program.NativeDirectory, UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process launched = Process.Start(start);
                TrackGame(launched);
                failed = false;
                lastFailure = null;
                RefreshState();
                Program.Diagnostic("Native game PID " + launched.Id + ", executable " +
                                   start.FileName);
                // A receipt failure must not report a successfully started game as a failed launch.
                try
                {
                    File.WriteAllText(Path.Combine(runtime, "latest-boot.txt"), logPath);
                    File.WriteAllText(
                        Path.Combine(Program.Local, "last-launch.json"),
                        Program.Json.Serialize(
                            new { utc = DateTime.UtcNow.ToString("o"), settings = preferences,
                                  build = Program.Version, native = ReleaseInfo.NativeVersion }));
                }
                catch (IOException error)
                {
                    Program.Diagnostic("Could not write launch receipt: " + error.Message);
                }
                catch (UnauthorizedAccessException error)
                {
                    Program.Diagnostic("Could not write launch receipt: " + error.Message);
                }
            }
            catch (Exception error)
            {
                failed = true;
                Program.Diagnostic("Could not start game: " + error);
                RefreshState();
                MessageBox.Show(this, error.Message, "Unable to start game", MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            }
            finally
            {
                busy = false;
                RefreshState();
                if (closeWhenStopped)
                    Close();
            }
        }
        async void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (preview || uninstalling)
                return;
            if (busy)
            {
                e.Cancel = true;
                closeWhenStopped = true;
                await CancelSetup();
            }
            else
            {
                SavePreferences();
                Program.Diagnostic("Launcher closing");
            }
        }
    }
}
