using System;
using System.IO;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    sealed class UpdatesPage : UserControl
    {
        internal event Action<string, bool> InstallRequested;
        public UpdatesPage(bool running)
        {
            var cancellation = new System.Threading.CancellationTokenSource();
            Disposed += (s, e) => cancellation.Cancel();
            AutoSize = true;
            Dock = DockStyle.Top;
            BackColor = Theme.Background;
            var layout = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top,
                                               FlowDirection = FlowDirection.TopDown,
                                               WrapContents = false, Padding = new Padding(6) };
            Controls.Add(layout);
            layout.Controls.Add(Theme.Label("Installed version: " + ReleaseInfo.Version, 460));
            var status = Theme.Label(
                "Check for a newer public preview from the project’s GitHub releases.", 460);
            layout.Controls.Add(status);
            var check = Theme.Button("Check for updates", 210);
            var install = Theme.Button("Install and restart", 210, true);
            var rollback = Theme.Button("Roll back and restart", 230);
            layout.Controls.Add(check);
            layout.Controls.Add(install);
            layout.Controls.Add(rollback);
            install.Enabled = false;
            check.Enabled = !running;
            rollback.Enabled =
                !running && (File.Exists(Path.Combine(Program.Local, "updates", "latest.json")) ||
                             File.Exists(Path.Combine(Program.Local, "updates", "recovery.json")));
            string staged = null;
            check.Click += async (s, e) =>
            {
                check.Enabled = false;
                rollback.Enabled = false;
                install.Enabled = false;
                try
                {
                    status.Text = "Checking the latest preview…";
                    var release = await LocalTools.Run("update_game.py", cancellation.Token,
                                                       "check", "--root", Program.Root);
                    if (!Convert.ToBoolean(release["available"]))
                    {
                        status.Text = "You have the latest available preview.";
                        return;
                    }
                    string latest = Convert.ToString(release["latest"]);
                    status.Text = "Downloading and verifying version " + latest + "…";
                    var result = await LocalTools.Run("update_game.py", cancellation.Token, "stage",
                                                      "--root", Program.Root, "--version", latest);
                    staged = Convert.ToString(result["stage"]);
                    status.Text = "Version " + latest +
                                  (" is verified and ready. Install restarts the launcher. A " +
                                   "native rebuild may be required.");
                    install.Enabled = true;
                }
                catch (Exception error)
                {
                    if (!IsDisposed)
                        status.Text = error.Message;
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        check.Enabled = true;
                        rollback.Enabled =
                            File.Exists(Path.Combine(Program.Local, "updates", "latest.json")) ||
                            File.Exists(Path.Combine(Program.Local, "updates", "recovery.json"));
                    }
                }
            };
            install.Click += (s, e) =>
            {
                if (staged != null && InstallRequested != null)
                    InstallRequested(staged, false);
            };
            rollback.Click += (s, e) =>
            {
                if (InstallRequested != null)
                    InstallRequested(null, true);
            };
            layout.Controls.Add(Theme.Label(
                "Updates verify archive and file checksums, preserve your " +
                    "saves/settings/cache, and back up replaced launcher files. Rollback " +
                    "restores the previous kit. The game must be closed.",
                460));
            string failure = Path.Combine(Program.Local, "updates", "last-error.json");
            if (File.Exists(failure))
            {
                try
                {
                    var error =
                        Program.Json
                            .Deserialize<System.Collections.Generic.Dictionary<string, object>>(
                                File.ReadAllText(failure));
                    status.Text =
                        "The last update could not finish: " + Convert.ToString(error["error"]) +
                        ". You can retry or restore the previous kit.";
                }
                catch (Exception)
                {
                    status.Text = "The last update could not finish. You can retry or restore " +
                                  "the previous kit.";
                }
            }
            if (running)
                status.Text = "Close the game before updating or rolling back.";
        }
    }
}
