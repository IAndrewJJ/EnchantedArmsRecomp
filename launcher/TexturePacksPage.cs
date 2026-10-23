using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    public sealed partial class Preferences
    {
        public string TexturePack = "";
        public bool FrameTimeGraph = false;
        public int ShaderWorkers = 0;
        internal void NormalizeExperience()
        {
            if (TexturePack == null ||
                !Regex.IsMatch(TexturePack, @"^(?:|[a-z0-9][a-z0-9-]{0,47}-[0-9a-f]{16})$"))
                TexturePack = "";
            ShaderWorkers = Math.Max(0, Math.Min(16, ShaderWorkers));
        }
    }

    sealed class TexturePacksPage : UserControl
    {
        readonly ComboBox selection = Theme.Choice(450);
        readonly List<string> tokens = new List<string>();
        readonly Label status = Theme.Label(
            "Install a compatible local texture pack, then select it for your next launch.", 460);
        readonly EtherButton install = Theme.Button("Install texture pack…", 230);
        readonly bool running;
        public TexturePacksPage(bool gameRunning)
        {
            var cancellation = new System.Threading.CancellationTokenSource();
            Disposed += (s, e) => cancellation.Cancel();
            running = gameRunning;
            AutoSize = true;
            Dock = DockStyle.Top;
            BackColor = Theme.Background;
            var layout =
                new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top,
                                      FlowDirection = FlowDirection.TopDown, WrapContents = false,
                                      Padding = new Padding(6, 0, 2, 12) };
            Controls.Add(layout);
            layout.Controls.Add(Theme.Heading("Texture packs"));
            layout.Controls.Add(selection);
            layout.Controls.Add(install);
            layout.Controls.Add(status);
            layout.Controls.Add(Theme.Label(
                "Choose a compatible texture pack to change the game's appearance. Select " +
                    "Original textures to return to the original look. No texture packs are " +
                    "included.",
                460));
            layout.Controls.Add(Theme.Label(
                "Texture packs are experimental and require an NTFS drive. Your original " +
                    "game files and saves are kept. The first launch with a pack may take longer.",
                460));
            install.Enabled = !running;
            install.Click += async (s, e) =>
            {
                using (
                    var dialog =
                        new OpenFileDialog { Filter = "EnchantedArmsPC texture pack (*.zip)|*.zip",
                                             Title = "Install a local texture pack" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    install.Enabled = false;
                    status.Text = "Checking textures and installing…";
                    try
                    {
                        var pack = await LocalTools.Run("texture_packs.py", cancellation.Token,
                                                        "install", "--root", Program.Root,
                                                        "--archive", dialog.FileName);
                        LoadSelection(Convert.ToString(pack["token"]));
                        status.Text = "Installed " + Convert.ToString(pack["name"]) +
                                      ". Apply settings, then click Play.";
                    }
                    catch (Exception error)
                    {
                        if (!IsDisposed)
                            status.Text = error.Message;
                    }
                    finally
                    {
                        if (!IsDisposed)
                            install.Enabled = !running;
                    }
                }
            };
        }
        internal void LoadSelection(string token)
        {
            tokens.Clear();
            selection.Items.Clear();
            tokens.Add("");
            selection.Items.Add("Original textures");
            string root = Path.Combine(Program.Local, "texture-packs");
            if (Directory.Exists(root))
                foreach (string directory in Directory.GetDirectories(root))
                {
                    try
                    {
                        string name = Path.GetFileName(directory);
                        if (!Regex.IsMatch(name, @"^[a-z0-9][a-z0-9-]{0,47}-[0-9a-f]{16}$"))
                            continue;
                        var receipt = Program.Json.Deserialize<Dictionary<string, object>>(
                            File.ReadAllText(Path.Combine(directory, "receipt.json")));
                        tokens.Add(name);
                        selection.Items.Add(Convert.ToString(receipt["name"]));
                    }
                    catch (IOException)
                    {
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (KeyNotFoundException)
                    {
                    }
                }
            int selected = tokens.IndexOf(token);
            selection.SelectedIndex = selected < 0 ? 0 : selected;
            if (selected < 0 && !String.IsNullOrEmpty(token))
                status.Text = "The selected pack is unavailable. Original textures will be used " +
                              "when you apply settings.";
        }
        internal string SelectedToken
        {
            get {
                return selection.SelectedIndex >= 0 ? tokens[selection.SelectedIndex] : "";
            }
        }
    }
}
