using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    static class LauncherSelfTests
    {
        static int CheckSettings()
        {
            Preferences p = new Preferences { Scale = 2, Anisotropy = 4 };
            string[] command = Program.GameArguments(p, "test.log");
            if (!command.Contains("--draw_resolution_scale_x=2") ||
                !command.Contains("--draw_resolution_scale_y=2") ||
                !command.Contains("--anisotropic_override=4") ||
                !command.Contains("--no-ea_trace_input"))
                return 1;
            if (Program.Quote("C:\\test folder\\") != "\"C:\\test folder\\\\\"")
                return 2;
            // Older launcher JSON keeps independent render/filter settings.
            var migrated = Program.Json.Deserialize<Preferences>(
                "{\"Scale\":2,\"Anisotropy\":4,\"Width\":2560,\"Height\":1440}");
            migrated.Normalize();
            if (migrated.Scale != 2 || migrated.Anisotropy != 4 ||
                migrated.Upscaler != "bilinear" || migrated.Width != 2560)
                return 3;
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo("fr-FR");
                migrated.Upscaler = "fsr";
                migrated.Sharpness = 80;
                if (!migrated.RendererArguments().Contains("--present_fsr_sharpness_reduction=0.4"))
                    return 4;
                migrated.Upscaler = "cas";
                if (!migrated.RendererArguments().Contains(
                        "--present_cas_additional_sharpness=0.8"))
                    return 5;
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
            var roundtrip = Program.Json.Deserialize<Preferences>(Program.Json.Serialize(migrated));
            if (roundtrip.Upscaler != "cas" || roundtrip.Sharpness != 80)
                return 6;
            var invalid =
                new Preferences { Scale = 99,           Sharpness = -4,     Anisotropy = 42,
                                  Upscaler = "unknown", PostAA = "unknown", Width = -1 };
            invalid.Normalize();
            if (invalid.Scale != 4 || invalid.Sharpness != 0 || invalid.Anisotropy != 5 ||
                invalid.Upscaler != "bilinear" || invalid.PostAA != "none" || invalid.Width != 1920)
                return 7;
            if (migrated.FrameLimit != 0 || migrated.TextureDetail != 0)
                return 8;
            foreach (int limit in Preferences.FrameLimits)
            {
                var selected = new Preferences { FrameLimit = limit, TextureDetail = 1 };
                var arguments = selected.RendererArguments();
                if (!arguments.Contains("--ea_fps_limit=" + limit) ||
                    !arguments.Contains("--ea_texture_lod_bias=-0.5") ||
                    !arguments.Contains(limit == 0 ? "--vsync" : "--no-vsync") ||
                    !arguments.Contains("--video_mode_refresh_rate=60"))
                    return 9;
                var saved = Program.Json.Deserialize<Preferences>(Program.Json.Serialize(selected));
                if (saved.FrameLimit != limit || saved.TextureDetail != 1)
                    return 10;
            }
            invalid.FrameLimit = 999;
            invalid.TextureDetail = 9;
            invalid.Normalize();
            if (invalid.FrameLimit != 0 || invalid.TextureDetail != 2)
                return 11;
            if (migrated.RenderPath != "auto" || migrated.ResolveReadback != "none" ||
                !migrated.AsyncShaders)
                return 12;
            foreach (string path in new[] { "auto", "rov", "rtv" })
                foreach (string readback in new[] { "none", "fast", "some", "full" })
                {
                    var selected = new Preferences { RenderPath = path, ResolveReadback = readback,
                                                     AsyncShaders = false };
                    var arguments = selected.RendererArguments();
                    if (!arguments.Contains("--render_target_path_d3d12=" +
                                            (path == "auto" ? "" : path)) ||
                        !arguments.Contains("--readback_resolve=" + readback) ||
                        !arguments.Contains("--no-async_shader_compilation"))
                        return 13;
                    var saved =
                        Program.Json.Deserialize<Preferences>(Program.Json.Serialize(selected));
                    if (saved.RenderPath != path || saved.ResolveReadback != readback ||
                        saved.AsyncShaders)
                        return 14;
                    saved.ApplyPreset(1);
                    if (saved.RenderPath != "auto" || saved.ResolveReadback != "none" ||
                        !saved.AsyncShaders)
                        return 15;
                }
            invalid.RenderPath = "invalid";
            invalid.ResolveReadback = "invalid";
            invalid.Normalize();
            if (invalid.RenderPath != "auto" || invalid.ResolveReadback != "none")
                return 16;
            if (Program.InstanceName("C:\\game", "WinSta0\\Default") ==
                    Program.InstanceName("C:\\game", "WinSta0\\Build") ||
                Program.InstanceName("C:\\game", "WinSta0\\Default") !=
                    Program.InstanceName("c:\\GAME", "winsta0\\default") ||
                Program.InstanceName("C:\\game", "WinSta0\\Default") ==
                    Program.InstanceName("C:\\other", "WinSta0\\Default"))
                return 17;
            foreach (int limit in Preferences.FrameLimits)
            {
                var selected = new Preferences { Iso = "test.iso",
                                                 Jobs = 5,
                                                 Scale = 3,
                                                 Anisotropy = 5,
                                                 Upscaler = "cas",
                                                 PostAA = "fxaa_extreme",
                                                 TextureDetail = 2,
                                                 Sharpness = 42,
                                                 FrameLimit = limit,
                                                 Width = 2560,
                                                 Fullscreen = false,
                                                 KeepAspect = false,
                                                 AllowTearing = false,
                                                 NativeMsaa = false,
                                                 Dither = true,
                                                 RenderPath = "rov",
                                                 ResolveReadback = "full",
                                                 AsyncShaders = false };
                selected.Normalize();
                using (var dialog = new GraphicsDialog(selected))
                {
                    dialog.ReadValues();
                    if (Program.Json.Serialize(selected) != Program.Json.Serialize(dialog.Settings))
                        return 18;
                }
            }
            return 0;
        }
        internal static int Run()
        {
            int settings = CheckSettings();
            if (settings != 0)
                return settings;

            string path = Path.Combine(Path.GetTempPath(),
                                       "ea-log-test-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                using (var log = new SetupLog(path, 16))
                {
                    log.WriteLine("first");
                    log.WriteLine(new string('x', 1000));
                    log.WriteLine("must-not-appear");
                }
                string contents = File.ReadAllText(path);
                if (!contents.StartsWith("first") || !contents.Contains("limit reached") ||
                    contents.Contains("must-not-appear") || contents.Length > 200)
                    return 19;
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            using (var form = new Launcher(true))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                Application.DoEvents();
                var start = new ProcessStartInfo(Application.ExecutablePath,
                                                 "--lifecycle-fixture") { UseShellExecute = false,
                                                                          CreateNoWindow = true };
                form.TrackGame(Process.Start(start));
                if (form.UsesProcessPolling)
                    return 20;
                var elapsed = Stopwatch.StartNew();
                while (form.HasTrackedGame && elapsed.ElapsedMilliseconds < 5000)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }
                if (form.HasTrackedGame)
                    return 21;
                form.Close();
            }
            if (typeof(Program).Assembly.GetName().Version.ToString(3) != ReleaseInfo.Version)
                return 22;
            return 0;
        }
    }
}
