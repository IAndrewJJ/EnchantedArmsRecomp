using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace EnchantedArmsPC
{
    static class UninstallSelfTests
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateDirectoryW(string path, IntPtr security);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(string path, uint access, uint share,
                                                 IntPtr security, uint creation, uint flags,
                                                 IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input,
                                           int inputLength, IntPtr output, int outputLength,
                                           out uint returned, IntPtr overlapped);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLinkW(string path, string existing, IntPtr security);
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
        static void Rejected(Action operation)
        {
            try
            {
                operation();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException("Unsafe uninstall was accepted");
        }
        static string Fixture(string parent, string name)
        {
            string root = Path.Combine(parent, name);
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "EnchantedArmsLauncher.exe"),
                              "synthetic launcher");
            File.WriteAllText(Path.Combine(root, "release.json"), "{\"version\":\"0.0.0\"}");
            File.WriteAllText(Path.Combine(root, "enchantedarmsrecomp_manifest.toml"),
                              "[project]\nname = \"enchantedarmsrecomp\"");
            return root;
        }
        static void Junction(string link, string target)
        {
            Directory.CreateDirectory(link);
            byte[] substitute = Encoding.Unicode.GetBytes(@"\??\" + target),
                   print = Encoding.Unicode.GetBytes(target);
            byte[] buffer;
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0xA0000003u);
                writer.Write((ushort)(12 + substitute.Length + print.Length));
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)substitute.Length);
                writer.Write((ushort)(substitute.Length + 2));
                writer.Write((ushort)print.Length);
                writer.Write(substitute);
                writer.Write((ushort)0);
                writer.Write(print);
                writer.Write((ushort)0);
                buffer = stream.ToArray();
            }
            using (var handle = CreateFileW(Uninstall.Extended(link), 0x40000000, 0, IntPtr.Zero, 3,
                                            0x02200000, IntPtr.Zero))
            {
                uint returned;
                Check(!handle.IsInvalid &&
                          DeviceIoControl(handle, 0x900A4, buffer, buffer.Length, IntPtr.Zero, 0,
                                          out returned, IntPtr.Zero),
                      "Could not create synthetic junction: " + Marshal.GetLastWin32Error());
            }
        }
        static void CleanupHelperTest()
        {
            string token = Guid.NewGuid().ToString("N"),
                   temporary =
                       Path.Combine(Path.GetTempPath(), "EnchantedArmsPC-uninstall-" + token);
            Directory.CreateDirectory(temporary);
            try
            {
                string source = Program.Root;
                for (int i = 0;
                     i < 5 &&
                     !File.Exists(Path.Combine(source, "enchantedarmsrecomp_manifest.toml"));
                     i++)
                    source = Path.GetDirectoryName(source);
                File.Copy(Application.ExecutablePath,
                          Path.Combine(temporary, "EnchantedArmsUninstall.exe"));
                File.Copy(Path.Combine(source, "scripts", "uninstall-cleanup.ps1"),
                          Path.Combine(temporary, "cleanup.ps1"));
                File.WriteAllText(Path.Combine(temporary, "request.json"),
                                  "synthetic cleanup request");
                using (var helper = Process.Start(new ProcessStartInfo(
                           Path.Combine(temporary, "EnchantedArmsUninstall.exe"),
                           "--uninstall-wait-fixture") { UseShellExecute = false,
                                                         CreateNoWindow = true }))
                {
                    string arguments = Program.Join(
                        "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File",
                        Path.Combine(temporary, "cleanup.ps1"), "-TaskCleanupRoot", temporary,
                        "-TaskToken", token, "-TaskWaitPid", helper.Id.ToString(),
                        "-TaskStartTicks", helper.StartTime.ToUniversalTime().Ticks.ToString());
                    using (var cleanup = Process.Start(new ProcessStartInfo(
                               Path.Combine(
                                   Environment.GetFolderPath(Environment.SpecialFolder.System),
                                   "WindowsPowerShell", "v1.0", "powershell.exe"),
                               arguments) { UseShellExecute = false, CreateNoWindow = true,
                                            RedirectStandardError = true }))
                    {
                        string error = cleanup.StandardError.ReadToEnd();
                        Check(cleanup.WaitForExit(15000) && cleanup.ExitCode == 0,
                              "Temporary helper cleanup failed: " + error);
                    }
                    helper.WaitForExit();
                }
                Check(!Directory.Exists(temporary), "Temporary helper files remain");
            }
            finally
            {
                string resolved = Path.GetFullPath(temporary);
                Check(Uninstall.IsInside(resolved, Path.GetTempPath().TrimEnd('\\')) &&
                          Path.GetFileName(resolved) == "EnchantedArmsPC-uninstall-" + token,
                      "Unsafe helper fixture cleanup target");
                if (Directory.Exists(temporary))
                    Uninstall.DeleteInstallation(temporary,
                                                 Uninstall.ValidateRoot(temporary, false), false);
            }
        }
        internal static int Run()
        {
            string parent = Path.Combine(Path.GetTempPath(),
                                         "ea-uninstall-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(parent);
            try
            {
                // Every destructive test is confined to this newly created fixture.
                Check(Uninstall.IsInside(parent, Path.GetTempPath().TrimEnd('\\')),
                      "Test scope escaped temporary storage");
                foreach (string root in new[] {
                             Path.GetPathRoot(parent),
                             Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                             Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                             Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                             Path.GetTempPath()
                         })
                    if (!String.IsNullOrEmpty(root))
                        Rejected(() => Uninstall.NormalizeRoot(root));
                string missing = Path.Combine(parent, "missing-markers");
                Directory.CreateDirectory(missing);
                Rejected(() => Uninstall.ValidateRoot(missing));
                string installation = Fixture(parent, "installation");
                string identity = Uninstall.ValidateRoot(installation);
                Rejected(() => Uninstall.DeleteInstallation(installation, identity + "changed"));
                Check(File.Exists(Path.Combine(installation, "release.json")),
                      "Identity rejection changed files");
                Check(!Uninstall.IsInside(installation + "-other\\file", installation),
                      "Sibling prefix accepted");
                using (var dialog = new UninstallDialog(installation))
                {
                    Check(Object.ReferenceEquals(dialog.AcceptButton, dialog.CancelButton),
                          "Enter must cancel uninstall by default");
                    Check(dialog.CancelButton.DialogResult == DialogResult.Cancel,
                          "Escape must cancel uninstall");
                }
                string external = Path.Combine(parent, "external");
                Directory.CreateDirectory(external);
                string sentinel = Path.Combine(external, "keep.iso");
                File.WriteAllText(sentinel, "external original ISO sentinel");
                Junction(Path.Combine(installation, "linked-game"), external);
                Junction(Path.Combine(installation, "cycle"), installation);
                string linkedRoot = Path.Combine(parent, "linked-installation");
                Junction(linkedRoot, installation);
                Rejected(() => Uninstall.ValidateRoot(linkedRoot));
                Check(CreateHardLinkW(Path.Combine(installation, "shared-file.dat"), sentinel,
                                      IntPtr.Zero),
                      "Could not create hardlink fixture");
                string save = Path.Combine(installation, "save.dat");
                File.WriteAllText(save, "synthetic save");
                File.SetAttributes(save, FileAttributes.ReadOnly);
                string deep = installation;
                for (int i = 0; i < 7; i++)
                {
                    deep += "\\" + new string('d', 45);
                    Check(CreateDirectoryW(Uninstall.Extended(deep), IntPtr.Zero),
                          "Could not create deep fixture");
                }
                using (var file = CreateFileW(Uninstall.Extended(deep + "\\cache.dat"),
                                              0x40000000, 7, IntPtr.Zero, 2, 0x80, IntPtr.Zero))
                    Check(!file.IsInvalid, "Could not create deep file");
                Uninstall.DeleteInstallation(installation, identity);
                Check(!Directory.Exists(installation), "Installation folder remains");
                Check(File.ReadAllText(sentinel) == "external original ISO sentinel",
                      "External data was changed");
                string shortcuts = Path.Combine(parent, "shortcuts");
                Program.Shortcut(shortcuts);
                string shortcut = Path.Combine(shortcuts, "EnchantedArmsPC.lnk");
                Check(Uninstall.ShortcutTargets(
                          shortcut, Path.Combine(Program.Root, "EnchantedArmsLauncher.exe")),
                      "Own shortcut not recognized");
                Check(!Uninstall.ShortcutTargets(
                          shortcut, Path.Combine(external, "EnchantedArmsLauncher.exe")),
                      "Other installation shortcut matched");
                CleanupHelperTest();
                return 0;
            }
            catch (Exception error)
            {
                Program.Diagnostic("Uninstall safety fixture failed: " + error);
                return 29;
            }
            finally
            {
                // Resolve/validate the exact owned root before recursive fixture cleanup.
                string resolved = Path.GetFullPath(parent);
                if (!Uninstall.IsInside(resolved, Path.GetTempPath().TrimEnd('\\')) ||
                    !Path.GetFileName(resolved).StartsWith("ea-uninstall-test-",
                                                           StringComparison.Ordinal))
                    throw new InvalidOperationException("Unsafe test cleanup target");
                if (Directory.Exists(parent))
                    Uninstall.DeleteInstallation(parent, Uninstall.ValidateRoot(parent, false),
                                                 false);
            }
        }
    }
}
