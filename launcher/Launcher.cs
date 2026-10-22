// Original launcher code. Game assets and translated game code stay on the player's PC.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    static class Program
    {
        internal static readonly string Root =
            AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        internal static readonly string Local = Path.Combine(Root, ".local");
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        internal static System.Threading.EventWaitHandle PlayRequest;
        internal static System.Threading.EventWaitHandle ActivateRequest;
        internal static System.Threading.EventWaitHandle RequestAcknowledged;
        static readonly object DiagnosticLock = new object();
        [DllImport("user32.dll")]
        static extern IntPtr GetProcessWindowStation();
        [DllImport("user32.dll")]
        static extern IntPtr GetThreadDesktop(uint threadId);
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value,
                                                    int length, out int needed);
        static string UserObjectName(IntPtr handle)
        {
            var value = new StringBuilder(512);
            int needed;
            if (handle == IntPtr.Zero ||
                !GetUserObjectInformation(handle, 2, value, value.Capacity * 2, out needed))
                return "unknown-process-" + Process.GetCurrentProcess().Id;
            return value.ToString();
        }
        static string DesktopContext()
        {
            return UserObjectName(GetProcessWindowStation()) + "\\" +
                   UserObjectName(GetThreadDesktop(GetCurrentThreadId()));
        }
        internal static string InstanceName(string root, string desktop)
        {
            // A build/sandbox desktop must never claim the user's interactive
            // launch request. Local kernel objects alone are scoped to session,
            // not desktop. Keep activation within this installation AND desktop.
            using (var hasher = System.Security.Cryptography.SHA256
                                    .Create()) return "Local\\EnchantedArmsPC." +
                BitConverter
                    .ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(
                        root.ToUpperInvariant() + "|" + desktop.ToUpperInvariant())))
                    .Replace("-", "");
        }
        internal static void Diagnostic(string message)
        {
            try
            {
                lock (DiagnosticLock)
                {
                    string directory = Path.Combine(Local, "logs");
                    Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, "launcher.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                        File.Copy(path, path + ".previous", true);
                    if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                        File.WriteAllText(path, "");
                    File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " [" +
                                                 Process.GetCurrentProcess().Id + "] " + message +
                                                 Environment.NewLine);
                }
            }
            catch
            {
            }
        }
        internal const string Version = ReleaseInfo.Version + "-" + ReleaseInfo.Channel;
        internal static string CurrentNativeDirectory
        {
            get {
                return Path.Combine(Root, "bin", "graphics-" + ReleaseInfo.NativeVersion);
            }
        }
        internal static bool TimingUpdateRequired(Preferences p, string nativeDirectory)
        {
            return ((p.FrameLimit != 0 && p.FrameLimit != 30) || p.CustomNativeControls ||
                    p.FrameTimeGraph) &&
                   !String.Equals(Path.GetFullPath(nativeDirectory),
                                  Path.GetFullPath(CurrentNativeDirectory),
                                  StringComparison.OrdinalIgnoreCase);
        }
        internal static string NativeDirectory
        {
            get {
                string diagnostics = CurrentNativeDirectory;
                if (File.Exists(Path.Combine(diagnostics, "enchantedarmsrecomp.exe")))
                    return diagnostics;
                string timing = Path.Combine(Root, "bin", "graphics-0.3.3");
                if (File.Exists(Path.Combine(timing, "enchantedarmsrecomp.exe")))
                    return timing;
                string prior = Path.Combine(Root, "bin", "graphics-0.3.2");
                if (File.Exists(Path.Combine(prior, "enchantedarmsrecomp.exe")))
                    return prior;
                string counter = Path.Combine(Root, "bin", "graphics-0.3.1");
                if (File.Exists(Path.Combine(counter, "enchantedarmsrecomp.exe")))
                    return counter;
                string newest = Path.Combine(Root, "bin", "graphics-0.3.0");
                if (File.Exists(Path.Combine(newest, "enchantedarmsrecomp.exe")))
                    return newest;
                string enhanced = Path.Combine(Root, "bin", "graphics-0.2.0");
                return File.Exists(Path.Combine(enhanced, "enchantedarmsrecomp.exe"))
                           ? enhanced
                           : Path.Combine(Root, "bin");
            }
        }

        // CommandLineToArgvW-compatible quoting, including paths ending in a backslash.
        internal static string Quote(string argument)
        {
            return "\"" +
                   Regex.Replace(Regex.Replace(argument, "(\\\\*)\"", "$1$1\\\""), "(\\\\+)$",
                                 "$1$1") +
                   "\"";
        }
        internal static string Join(params string[] args)
        {
            return String.Join(" ", args.Select(Quote));
        }
        internal static string[] GameArguments(Preferences p, string log, string gameRoot = null)
        {
            bool current = String.Equals(Path.GetFullPath(NativeDirectory),
                                         Path.GetFullPath(CurrentNativeDirectory),
                                         StringComparison.OrdinalIgnoreCase);
            return new[] { "--game_data_root=" + (gameRoot ?? Path.Combine(Local, "game")),
                           "--user_data_root=" + Path.Combine(Local, "runtime", "user"),
                           "--cache_root=" + Path.Combine(Local, "runtime", "cache"),
                           "--log_file=" + log,
                           "--log_level=info",
                           "--log_max_file_size_mb=16",
                           "--log_max_files=3",
                           "--no-ea_trace_input",
                           "--mnk_mode" }
                .Concat(p.RendererArguments())
                .Concat(p.ControlArguments(current))
                .Concat(current ? new[] { p.FrameTimeGraph ? "--ea_frame_graph"
                                                           : "--no-ea_frame_graph" }
                                : new string[0])
                .ToArray();
        }
        internal static void Shortcut(string directory)
        {
            Directory.CreateDirectory(directory);
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember(
                "CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { Path.Combine(directory, "EnchantedArmsPC.lnk") });
            try
            {
                Type type = shortcut.GetType();
                Action<string, object> set = (name, value) => type.InvokeMember(
                    name, BindingFlags.SetProperty, null, shortcut, new object[] { value });
                set("TargetPath", Path.Combine(Root, "EnchantedArmsLauncher.exe"));
                set("Arguments", "");
                set("WorkingDirectory", Root);
                set("Description", "Open the EnchantedArmsPC launcher");
                set("IconLocation", Path.Combine(Root, "EnchantedArmsLauncher.exe") + ",0");
                type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--uninstall-helper")
                return Uninstall.RunHelper(args[1]);
            if (args.Length == 2 && args[0] == "--uninstall-preview")
            {
                Application.EnableVisualStyles();
                using (var dialog = new UninstallDialog(@"D:\Games\EnchantedArmsPC"))
                {
                    dialog.StartPosition = FormStartPosition.Manual;
                    dialog.Location = new Point(-20000, -20000);
                    dialog.Show();
                    Application.DoEvents();
                    using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
                    {
                        dialog.DrawToBitmap(bitmap,
                                            new Rectangle(0, 0, dialog.Width, dialog.Height));
                        bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                return 0;
            }
            if (args.Length == 1 && args[0] == "--uninstall-wait-fixture")
            {
                System.Threading.Thread.Sleep(2000);
                return 0;
            }
            if (args.Length == 1 && args[0] == "--lifecycle-fixture")
            {
                System.Threading.Thread.Sleep(100);
                return 0;
            }
            if (args.Length == 2 && args[0] == "--launcher-preview")
            {
                Application.EnableVisualStyles();
                using (var form = new Launcher(true))
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-20000, -20000);
                    form.Show();
                    Application.DoEvents();
                    form.PerformLayout();
                    var surface = form.Controls[0];
                    using (var bitmap = new Bitmap(surface.Width, surface.Height))
                    {
                        surface.DrawToBitmap(bitmap,
                                             new Rectangle(0, 0, surface.Width, surface.Height));
                        bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                    }
                    form.Close();
                }
                return 0;
            }
            if (args.Length == 1 && args[0] == "--launch-context")
            {
                Directory.CreateDirectory(Path.Combine(Local, "logs"));
                File.WriteAllText(
                    Path.Combine(Local, "logs", "launch-context.json"),
                    Json.Serialize(new { desktop = DesktopContext(),
                                         session = Process.GetCurrentProcess().SessionId,
                                         interactive = Environment.UserInteractive }));
                return 0;
            }
            if ((args.Length == 2 && args[0] == "--graphics-preview") ||
                (args.Length == 3 && args[0] == "--settings-preview"))
            {
                int page = 0;
                if (args.Length == 3 &&
                    (!Int32.TryParse(args[1], out page) || page < 0 || page > 6))
                    return 2;
                Application.EnableVisualStyles();
                using (var dialog = new GraphicsDialog(
                           new Preferences { Upscaler = "fsr", PostAA = "fxaa", Scale = 2,
                                             Anisotropy = 5, Dither = true, TextureDetail = 1,
                                             FrameLimit = 60 },
                           false, false, page))
                {
                    dialog.StartPosition = FormStartPosition.Manual;
                    dialog.Location = new Point(-20000, -20000);
                    dialog.Show();
                    Application.DoEvents();
                    dialog.PerformLayout();
                    using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
                    {
                        dialog.DrawToBitmap(bitmap,
                                            new Rectangle(0, 0, dialog.Width, dialog.Height));
                        bitmap.Save(args[args.Length - 1], System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                return 0;
            }
            if (args.Length == 2 && args[0] == "--shortcut-test")
            {
                Shortcut(args[1]);
                return 0;
            }
            if (args.Length == 1 && args[0] == "--self-test")
                return LauncherSelfTests.Run();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
            {
                Diagnostic("UI exception: " + e.Exception);
                MessageBox.Show(e.Exception.Message + "\r\nDetails: .local/logs/launcher.log",
                                "EnchantedArmsPC", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Diagnostic("Unhandled exception: " + e.ExceptionObject);
            string desktop = DesktopContext();
            string instance = InstanceName(Root, desktop);
            bool created;
            using (var mutex = new System.Threading.Mutex(true, instance, out created))
            {
                if (!created)
                {
                    try
                    {
                        using (
                            var ack = System.Threading.EventWaitHandle.OpenExisting(
                                instance +
                                ".Ack")) using (var request =
                                                      System.Threading.EventWaitHandle.OpenExisting(
                                                          instance + ".Activate"))
                        {
                            ack.Reset();
                            request.Set();
                            if (ack.WaitOne(5000))
                            {
                                Diagnostic("Existing launcher acknowledged Activate");
                                return 0;
                            }
                        }
                    }
                    catch (System.Threading.WaitHandleCannotBeOpenedException error)
                    {
                        Diagnostic("Existing launcher request failed: " + error.Message);
                    }
                    Diagnostic("Existing launcher did not acknowledge the request");
                    MessageBox.Show(
                        "The existing launcher is not responding. Close it and open " +
                            "EnchantedArmsLauncher.exe again. Your game and saves are " +
                            "unchanged.\r\nDetails: .local/logs/launcher.log",
                        "EnchantedArmsPC", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                using (
                    PlayRequest = new System.Threading.EventWaitHandle(
                        false, System.Threading.EventResetMode.AutoReset,
                        instance +
                            ".Play")) using (ActivateRequest =
                                                   new System.Threading.EventWaitHandle(
                                                       false,
                                                       System.Threading.EventResetMode.AutoReset,
                                                       instance +
                                                           ".Activate")) using (RequestAcknowledged =
                                                                                      new System
                                                                                          .Threading
                                                                                          .EventWaitHandle(
                                                                                              false,
                                                                                              System
                                                                                                  .Threading
                                                                                                  .EventResetMode
                                                                                                  .AutoReset,
                                                                                              instance +
                                                                                                  ("." +
                                                                                                   "A" +
                                                                                                   "c" +
                                                                                                   "k")))
                {
                    Diagnostic("Launcher started " + Version + ", desktop=" + desktop +
                               ", session=" + Process.GetCurrentProcess().SessionId);
                    Application.Run(new Launcher());
                    Diagnostic("Launcher window closed; exiting");
                }
            }
            // Finish even if an outstanding child-process monitor still has a
            // CLR worker alive. The independently running game is not stopped.
            Environment.Exit(0);
            return 0;
        }
    }

}
