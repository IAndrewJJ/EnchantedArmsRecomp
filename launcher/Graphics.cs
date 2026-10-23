using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace EnchantedArmsPC
{
    public sealed partial class Preferences
    {
        public string Iso = "";
        public string BuildLocation = "";
        public string SaveLocation = "";
        public int Scale = 1;
        public int Anisotropy = -1;
        public int Width = 1920;
        public int Height = 1080;
        public int Jobs = 2;
        public int Schema = 5;
        public int TextureDetail = 0;
        public int FrameLimit = 30;
        public static readonly int[] FrameLimits = { 30, 60 };
        public string Upscaler = "bilinear";
        public string PostAA = "none";
        public int Sharpness = 80;
        public bool Fullscreen = true;
        public bool Dither = false;
        public bool KeepAspect = true;
        public bool NativeMsaa = true;
        public string RenderPath = "auto";
        public string ResolveReadback = "none";
        public bool AsyncShaders = true;

        public void Normalize()
        {
            Scale = Math.Max(1, Math.Min(4, Scale));
            Anisotropy = Math.Max(-1, Math.Min(5, Anisotropy));
            Sharpness = Math.Max(0, Math.Min(100, Sharpness));
            Jobs = Math.Max(1, Math.Min(8, Jobs));
            TextureDetail = Math.Max(0, Math.Min(2, TextureDetail));
            if (!FrameLimits.Contains(FrameLimit))
                FrameLimit =
                    new[] { -1, 90, 120, 144, 165, 180, 240, 360 }.Contains(FrameLimit) ? 60 : 30;
            if (!new[] { "bilinear", "fsr", "cas" }.Contains(Upscaler))
                Upscaler = "bilinear";
            if (!new[] { "none", "fxaa", "fxaa_extreme", "ssaa" }.Contains(PostAA))
                PostAA = "none";
            int[] widths = { 1280, 1920, 2560, 3840 };
            int index = Array.IndexOf(widths, Width);
            if (index < 0)
                index = 1;
            Width = widths[index];
            Height = new[] { 720, 1080, 1440, 2160 }[index];
            if (PostAA == "ssaa")
                Scale = Math.Max(Scale, Math.Min(4, Width / 1280 + 1));
            if (Iso == null)
                Iso = "";
            if (BuildLocation == null)
                BuildLocation = "";
            if (SaveLocation == null)
                SaveLocation = "";
            if (!new[] { "auto", "rov", "rtv" }.Contains(RenderPath))
                RenderPath = "auto";
            if (!new[] { "none", "fast", "some", "full" }.Contains(ResolveReadback))
                ResolveReadback = "none";
            NormalizeControls();
            NormalizeExperience();
            Schema = 5;
        }
        public Preferences Copy()
        {
            var json = new JavaScriptSerializer();
            return json.Deserialize<Preferences>(json.Serialize(this));
        }
        public string[] RendererArguments()
        {
            Normalize();
            var args = new List<string> {
                "--resolution_scale=" + Scale, "--draw_resolution_scale_x=" + Scale,
                "--draw_resolution_scale_y=" + Scale, "--anisotropic_override=" + Anisotropy,
                "--swap_post_effect=" + (PostAA == "ssaa" ? "fxaa" : PostAA),
                "--present_effect=" + Upscaler, "--window_width=" + Width,
                "--window_height=" + Height, "--ea_fps_limit=" + FrameLimit,
                "--ea_texture_lod_bias=" +
                    (-TextureDetail * 0.5).ToString("0.#", CultureInfo.InvariantCulture),
                "--video_mode_refresh_rate=60",
                "--render_target_path_d3d12=" + (RenderPath == "auto" ? "" : RenderPath),
                "--readback_resolve=" + ResolveReadback,
                AsyncShaders ? "--async_shader_compilation" : "--no-async_shader_compilation",
                "--no-vsync", Fullscreen ? "--fullscreen" : "--no-fullscreen",
                Dither ? "--present_dither" : "--no-present_dither",
                KeepAspect ? "--present_letterbox" : "--no-present_letterbox",
                // The previous V-Sync On option disabled host tearing. Keep that
                // presentation behavior fixed; guest timing stays on ea_fps_limit.
                "--no-d3d12_allow_variable_refresh_rate_and_tearing",
                NativeMsaa ? "--native_2x_msaa" : "--no-native_2x_msaa"
            };
            args.Add("--d3d12_pipeline_creation_threads=" +
                     (ShaderWorkers == 0 ? -1 : ShaderWorkers));
            // Spatial FSR uses the actual guest frame size. Temporal quality-mode flags
            // do not change the game's rendering resolution and are deliberately omitted.
            if (Upscaler == "fsr")
            {
                args.Add("--present_fsr_sharpness_reduction=" +
                         (2.0 - Sharpness / 50.0).ToString("0.##", CultureInfo.InvariantCulture));
                args.Add("--present_fsr_max_upsampling_passes=4");
            }
            if (Upscaler == "cas")
                args.Add("--present_cas_additional_sharpness=" +
                         (Sharpness / 100.0).ToString("0.##", CultureInfo.InvariantCulture));
            return args.ToArray();
        }
    }

}
