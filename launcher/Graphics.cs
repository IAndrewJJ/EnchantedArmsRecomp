using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnchantedArmsPC {
    public sealed class Preferences {
        public string Iso = "";
        public int Scale = 1;
        public int Anisotropy = 3;
        public int Width = 1920;
        public int Height = 1080;
        public int Jobs = 2;
        public int Schema = 4;
        public int TextureDetail = 0;
        public int FrameLimit = 0;
        public static readonly int[] FrameLimits = {0,30,60,90,120,144,165,180,240,360,-1};
        public string Upscaler = "bilinear";
        public string PostAA = "none";
        public int Sharpness = 80;
        public bool Fullscreen = true;
        public bool Dither = false;
        public bool KeepAspect = true;
        public bool AllowTearing = true;
        public bool NativeMsaa = true;
        public string RenderPath = "auto";
        public string ResolveReadback = "none";
        public bool AsyncShaders = true;

        public void Normalize() {
            Scale = Math.Max(1, Math.Min(4, Scale));
            Anisotropy = Math.Max(-1, Math.Min(5, Anisotropy));
            Sharpness = Math.Max(0, Math.Min(100, Sharpness));
            Jobs = Math.Max(1, Math.Min(8, Jobs));
            TextureDetail = Math.Max(0,Math.Min(2,TextureDetail));
            if (!FrameLimits.Contains(FrameLimit)) FrameLimit = 0;
            if (!new [] { "bilinear", "fsr", "cas" }.Contains(Upscaler)) Upscaler = "bilinear";
            if (!new [] { "none", "fxaa", "fxaa_extreme" }.Contains(PostAA)) PostAA = "none";
            int[] widths = {1280,1920,2560,3840};
            int index = Array.IndexOf(widths, Width);
            if (index < 0) index = 1;
            Width = widths[index]; Height = new [] {720,1080,1440,2160}[index];
            if (Iso == null) Iso = "";
            if (!new [] {"auto","rov","rtv"}.Contains(RenderPath)) RenderPath = "auto";
            if (!new [] {"none","fast","some","full"}.Contains(ResolveReadback)) ResolveReadback = "none";
            Schema = 4;
        }
        public Preferences Copy() {
            var json = new JavaScriptSerializer();
            return json.Deserialize<Preferences>(json.Serialize(this));
        }
        public void ApplyPreset(int index) {
            NativeMsaa = true; KeepAspect = true;
            if (index == 1) { Scale = 1; Anisotropy = -1; Upscaler = "bilinear"; PostAA = "none"; Dither = false; TextureDetail=0; FrameLimit=0; RenderPath="auto"; ResolveReadback="none"; AsyncShaders=true; }
            if (index == 2) { Scale = 1; Anisotropy = 4; Upscaler = "fsr"; PostAA = "fxaa"; Sharpness = 80; Dither = true; }
            if (index == 3) { Scale = 2; Anisotropy = 4; Upscaler = "fsr"; PostAA = "fxaa"; Sharpness = 80; Dither = true; }
            if (index == 4) { Scale = 3; Anisotropy = 5; Upscaler = "cas"; PostAA = "fxaa"; Sharpness = 40; Dither = true; }
            if (index == 5) { Scale = 4; Anisotropy = 5; Upscaler = "cas"; PostAA = "fxaa_extreme"; Sharpness = 35; Dither = true; }
            Normalize();
        }
        public string Summary() {
            string scaler = Upscaler == "fsr" ? "FSR 1 spatial" : Upscaler == "cas" ? "CAS sharpening" : "Bilinear";
            string filter = Anisotropy < 0 ? "game filtering" : Anisotropy == 0 ? "AF off" : (1 << (Anisotropy - 1)) + "x AF";
            return Scale + "x internal • " + scaler + " • " + (PostAA == "none" ? "post AA off" : PostAA == "fxaa" ? "FXAA" : "FXAA strong") +
                " • " + filter + " • detail " + new [] {"original","crisp","strong"}[TextureDetail] +
                " • " + (FrameLimit == 0 ? "original FPS" : FrameLimit < 0 ? "uncapped (experimental)" : FrameLimit + " FPS limit (experimental)") +
                " • " + (Fullscreen ? "borderless fullscreen" : "windowed") +
                (RenderPath == "auto" ? "" : " • " + RenderPath.ToUpperInvariant()) +
                (ResolveReadback == "none" ? "" : " • " + ResolveReadback + " readback") +
                (AsyncShaders ? "" : " • synchronous shaders");
        }
        public string[] RendererArguments() {
            Normalize();
            var args = new List<string> {
                "--resolution_scale=" + Scale, "--draw_resolution_scale_x=" + Scale, "--draw_resolution_scale_y=" + Scale,
                "--anisotropic_override=" + Anisotropy, "--swap_post_effect=" + PostAA,
                "--present_effect=" + Upscaler, "--window_width=" + Width, "--window_height=" + Height,
                "--ea_fps_limit=" + FrameLimit,
                "--ea_texture_lod_bias=" + (-TextureDetail * 0.5).ToString("0.#",CultureInfo.InvariantCulture),
                "--video_mode_refresh_rate=60",
                "--render_target_path_d3d12=" + (RenderPath == "auto" ? "" : RenderPath),
                "--readback_resolve=" + ResolveReadback,
                AsyncShaders ? "--async_shader_compilation" : "--no-async_shader_compilation",
                FrameLimit == 0 ? "--vsync" : "--no-vsync",
                Fullscreen ? "--fullscreen" : "--no-fullscreen",
                Dither ? "--present_dither" : "--no-present_dither",
                KeepAspect ? "--present_letterbox" : "--no-present_letterbox",
                AllowTearing ? "--d3d12_allow_variable_refresh_rate_and_tearing" : "--no-d3d12_allow_variable_refresh_rate_and_tearing",
                NativeMsaa ? "--native_2x_msaa" : "--no-native_2x_msaa"
            };
            // Spatial FSR uses the actual guest frame size. Temporal quality-mode flags
            // do not change the game's rendering resolution and are deliberately omitted.
            if (Upscaler == "fsr") {
                args.Add("--present_fsr_sharpness_reduction=" + (2.0 - Sharpness / 50.0).ToString("0.##", CultureInfo.InvariantCulture));
                args.Add("--present_fsr_max_upsampling_passes=4");
            }
            if (Upscaler == "cas") args.Add("--present_cas_additional_sharpness=" + (Sharpness / 100.0).ToString("0.##", CultureInfo.InvariantCulture));
            return args.ToArray();
        }
    }

    sealed class GraphicsDialog : Form {
        public Preferences Settings;
        readonly ComboBox scale = Choice("1x • native", "2x • four times the pixels", "3x • nine times the pixels", "4x • sixteen times the pixels");
        readonly ComboBox scaler = Choice("Bilinear • original scaling", "AMD FSR 1 • spatial upscaling", "AMD FidelityFX CAS • sharpening");
        readonly ComboBox aa = Choice("Off • keep game AA", "FXAA", "FXAA strong");
        readonly ComboBox filtering = Choice("Game requested filtering", "Anisotropic filtering off", "1x", "2x", "4x", "8x", "16x");
        readonly ComboBox detail = Choice("Original texture detail", "Crisp • sharper mip selection", "Strong • may shimmer in motion");
        readonly ComboBox fps = Choice("Original • recommended", "30 FPS • experimental", "60 FPS • experimental", "90 FPS • experimental", "120 FPS • experimental", "144 FPS • experimental", "165 FPS • experimental", "180 FPS • experimental", "240 FPS • experimental", "360 FPS • experimental", "Unlimited • experimental");
        readonly ComboBox display = Choice("Windowed", "Borderless fullscreen");
        readonly ComboBox size = Choice("1280 x 720", "1920 x 1080", "2560 x 1440", "3840 x 2160");
        readonly ComboBox renderPath = Choice("Automatic • default", "ROV • accuracy comparison", "RTV • performance comparison");
        readonly ComboBox readback = Choice("Disabled • default", "Delayed • copy every frame", "Delayed • copy on demand", "Immediate • slow diagnostic");
        readonly CheckBox asyncShaders = Check("Compile shaders in background • reduces compilation stalls");
        readonly TrackBar sharpness = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10, Width = 300, Height = 42 };
        readonly Label sharpnessValue = new Label { AutoSize = true, Padding = new Padding(0,10,0,0) };
        readonly CheckBox dither = Check("Output dithering • reduce gradient banding");
        readonly CheckBox aspect = Check("Preserve aspect ratio with letterboxing");
        readonly CheckBox tearing = Check("Allow variable refresh rate / tearing");
        readonly CheckBox msaa = Check("Use native 2x MSAA when requested by the game");
        static ComboBox Choice(params string[] values) {
            var control = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 350, Margin = new Padding(0,6,0,6) };
            control.DrawMode = DrawMode.OwnerDrawFixed;
            control.ItemHeight = 23;
            control.DrawItem += (s,e) => {
                e.DrawBackground();
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics,control.Items[e.Index].ToString(),control.Font,e.Bounds,
                    (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : SystemColors.ControlText,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                e.DrawFocusRectangle();
            };
            control.Items.AddRange(values); return control;
        }
        static CheckBox Check(string text) { return new CheckBox { Text = text, AutoSize = true, Margin = new Padding(0,6,0,6) }; }
        public GraphicsDialog(Preferences current) {
            Settings = current.Copy(); Settings.Normalize();
            Text = "EnchantedArmsPC • Graphics settings"; StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI",10); ClientSize = new Size(800,930); MinimumSize = new Size(760,760);
            BackColor = Color.White; MaximizeBox = false; MinimizeBox = false;
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 2, RowCount = 2, AutoScroll = true };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,210)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Controls.Add(table);
            var title = new Label { Text = "Graphics settings", Font = new Font("Segoe UI",18,FontStyle.Bold), AutoSize = true };
            table.Controls.Add(title,0,0); table.SetColumnSpan(title,2);
            var info = new Label { Text = "Changes apply next launch. Use 1x as a baseline when investigating rendering bugs.", AutoSize = true, MaximumSize = new Size(700,0), Margin = new Padding(0,8,0,12) };
            table.Controls.Add(info,0,1); table.SetColumnSpan(info,2);
            Add(table,"Internal render scale",scale); Add(table,"Output scaler / sharpener",scaler);
            Add(table,"Post-process antialiasing",aa); Add(table,"Texture filtering",filtering);
            Add(table,"Texture detail boost",detail); Add(table,"Frame-rate limit",fps);
            var sharpRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            sharpRow.Controls.Add(sharpness); sharpRow.Controls.Add(sharpnessValue); Add(table,"Sharpening strength",sharpRow);
            Add(table,"Display mode",display); Add(table,"Window size",size);
            foreach (var check in new [] { dither,aspect,tearing,msaa }) FullRow(table,check);
            Add(table,"Rendering method",renderPath); Add(table,"Resolve readback",readback);
            FullRow(table,asyncShaders);
            var help = new Label { Text = "FSR 1 upscales lower-resolution frames; the presenter uses sharpening when upscaling is unnecessary. " +
                "Fullscreen uses your desktop resolution. Higher internal scales cost GPU time and memory. " +
                "Detail boost sharpens existing mipmapped surfaces; it does not replace textures. " +
                "Use Original FPS for normal play. Experimental FPS limits can accelerate gameplay; a normal-speed high-FPS timing patch is not implemented. " +
                "Limits do not guarantee that frame rate. Temporal FSR and frame generation are not included. " +
                "For dark/missing models, compare one option at a time after restarting: ROV may improve accuracy at a performance cost; " +
                "Immediate readback can stall heavily. Turning off background compilation waits for every pipeline instead of briefly skipping unready draws. " +
                "These are diagnostic options, not confirmed NPC fixes. Original preset restores defaults. F8 saves a screenshot to Open captures in the launcher.", AutoSize = true,
                MaximumSize = new Size(700,0), ForeColor = Color.FromArgb(75,85,95), Margin = new Padding(0,12,0,12) };
            FullRow(table,help);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var apply = new Button { Text = "Apply", Width = 110, Height = 34 };
            var cancel = new Button { Text = "Cancel", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
            var reset = new Button { Text = "Original preset", Width = 145, Height = 34 };
            buttons.Controls.Add(apply); buttons.Controls.Add(cancel); buttons.Controls.Add(reset); FullRow(table,buttons);
            var profiles = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var import = new Button { Text = "Import profile", Width = 130, Height = 32 };
            var export = new Button { Text = "Export profile", Width = 130, Height = 32 };
            profiles.Controls.Add(import); profiles.Controls.Add(export); FullRow(table,profiles);
            AcceptButton = apply; CancelButton = cancel;
            apply.Click += (s,e) => { ReadValues(); DialogResult = DialogResult.OK; Close(); };
            reset.Click += (s,e) => { Settings.ApplyPreset(1); LoadValues(); };
            import.Click += (s,e) => {
                using (var dialog = new OpenFileDialog { Filter="Graphics profile (*.json)|*.json" }) {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    try {
                        var profile = new JavaScriptSerializer().Deserialize<Preferences>(System.IO.File.ReadAllText(dialog.FileName));
                        if (profile == null) throw new ArgumentException("The profile is empty.");
                        profile.Iso = Settings.Iso; profile.Jobs = Settings.Jobs; profile.Normalize(); Settings = profile; LoadValues();
                    } catch (Exception error) { MessageBox.Show(this,error.Message,"Could not import profile"); }
                }
            };
            export.Click += (s,e) => {
                using (var dialog = new SaveFileDialog { Filter="Graphics profile (*.json)|*.json",FileName="EnchantedArmsPC-graphics.json" }) {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    try { ReadValues(); var profile = Settings.Copy(); profile.Iso=""; profile.Jobs=2;
                        System.IO.File.WriteAllText(dialog.FileName,new JavaScriptSerializer().Serialize(profile));
                    } catch (Exception error) { MessageBox.Show(this,error.Message,"Could not export profile"); }
                }
            };
            sharpness.ValueChanged += (s,e) => sharpnessValue.Text = sharpness.Value + "%";
            scaler.SelectedIndexChanged += (s,e) => sharpness.Enabled = scaler.SelectedIndex > 0;
            LoadValues();
        }
        static void Add(TableLayoutPanel table,string name,Control control) {
            int row = table.RowCount++; table.Controls.Add(new Label { Text = name, AutoSize = true, Margin = new Padding(0,10,10,8) },0,row); table.Controls.Add(control,1,row);
        }
        static void FullRow(TableLayoutPanel table,Control control) {
            int row = table.RowCount++; table.Controls.Add(control,0,row); table.SetColumnSpan(control,2);
        }
        void LoadValues() {
            scale.SelectedIndex = Settings.Scale - 1; scaler.SelectedIndex = Array.IndexOf(new [] {"bilinear","fsr","cas"},Settings.Upscaler);
            aa.SelectedIndex = Array.IndexOf(new [] {"none","fxaa","fxaa_extreme"},Settings.PostAA);
            filtering.SelectedIndex = Settings.Anisotropy + 1; display.SelectedIndex = Settings.Fullscreen ? 1 : 0;
            detail.SelectedIndex = Settings.TextureDetail; fps.SelectedIndex = Array.IndexOf(Preferences.FrameLimits,Settings.FrameLimit);
            size.SelectedIndex = Array.IndexOf(new [] {1280,1920,2560,3840},Settings.Width);
            sharpness.Value = Settings.Sharpness; sharpnessValue.Text = sharpness.Value + "%";
            dither.Checked = Settings.Dither; aspect.Checked = Settings.KeepAspect; tearing.Checked = Settings.AllowTearing; msaa.Checked = Settings.NativeMsaa;
            renderPath.SelectedIndex = Array.IndexOf(new [] {"auto","rov","rtv"},Settings.RenderPath);
            readback.SelectedIndex = Array.IndexOf(new [] {"none","fast","some","full"},Settings.ResolveReadback);
            asyncShaders.Checked = Settings.AsyncShaders;
        }
        void ReadValues() {
            Settings.Scale = scale.SelectedIndex + 1; Settings.Upscaler = new [] {"bilinear","fsr","cas"}[scaler.SelectedIndex];
            Settings.PostAA = new [] {"none","fxaa","fxaa_extreme"}[aa.SelectedIndex]; Settings.Anisotropy = filtering.SelectedIndex - 1;
            Settings.TextureDetail = detail.SelectedIndex; Settings.FrameLimit = Preferences.FrameLimits[fps.SelectedIndex];
            Settings.Fullscreen = display.SelectedIndex == 1; Settings.Width = new [] {1280,1920,2560,3840}[size.SelectedIndex];
            Settings.Sharpness = sharpness.Value; Settings.Dither = dither.Checked; Settings.KeepAspect = aspect.Checked;
            Settings.AllowTearing = tearing.Checked; Settings.NativeMsaa = msaa.Checked;
            Settings.RenderPath = new [] {"auto","rov","rtv"}[renderPath.SelectedIndex];
            Settings.ResolveReadback = new [] {"none","fast","some","full"}[readback.SelectedIndex];
            Settings.AsyncShaders = asyncShaders.Checked; Settings.Normalize();
        }
    }
}
