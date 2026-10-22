using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace EnchantedArmsPC
{
    sealed class UninstallDialog : Form
    {
        internal UninstallDialog(string root)
        {
            Theme.Window(this);
            Text = "Uninstall EnchantedArmsPC?";
            ClientSize = new Size(660, 370);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            var content =
                new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24),
                                      FlowDirection = FlowDirection.TopDown, WrapContents = false };
            Controls.Add(content);
            var heading = Theme.Label("Permanently delete this installation?", 600);
            heading.Font = new Font("Segoe UI Semibold", 15);
            heading.ForeColor = Theme.Text;
            content.Controls.Add(heading);
            content.Controls.Add(new TextBox { Text = root, ReadOnly = true, Multiline = true,
                                               Width = 600, Height = 56, BackColor = Theme.Panel,
                                               ForeColor = Theme.Text });
            content.Controls.Add(
                Theme.Label("All files in this folder will be deleted, including saved games, " +
                                "settings, extracted game data, texture packs, caches and build " +
                                "tools. This cannot be undone.",
                            600));
            content.Controls.Add(
                Theme.Label("Shortcuts pointing to this launcher will be removed. ISOs outside " +
                                "this folder and the contents of linked external folders will be " +
                                "kept. Close the game before uninstalling.",
                            600));
            var actions =
                new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
                                      WrapContents = false };
            content.Controls.Add(actions);
            var cancel = Theme.Button("Cancel", 130, true);
            cancel.DialogResult = DialogResult.Cancel;
            var remove = Theme.Button("Delete installation and saves", 300);
            remove.DialogResult = DialogResult.OK;
            actions.Controls.Add(cancel);
            actions.Controls.Add(remove);
            AcceptButton = cancel;
            CancelButton = cancel;
            ActiveControl = cancel;
        }
    }

    sealed class UninstallRequest
    {
        public string Root, Identity, Token, LauncherHash;
        public int ParentPid;
        public long ParentStartTicks;
    }

    static class Uninstall
    {
        const uint DirectoryAttribute = 0x10, ReparseAttribute = 0x400,
                   InvalidAttributes = 0xffffffff;
        [StructLayout(LayoutKind.Sequential)]
        struct FileInfo
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct FindData
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint SizeHigh, SizeLow, Reserved0, Reserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string AlternateName;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(string path, uint access, uint share,
                                                 IntPtr security, uint creation, uint flags,
                                                 IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfo info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path,
                                                     uint size, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFileAttributesW(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SetFileAttributesW(string path, uint attributes);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool DeleteFileW(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool RemoveDirectoryW(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr FindFirstFileW(string path, out FindData data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool FindNextFileW(IntPtr handle, out FindData data);
        [DllImport("kernel32.dll")]
        static extern bool FindClose(IntPtr handle);

        internal static string Extended(string path)
        {
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
                return path;
            return path.StartsWith(@"\\", StringComparison.Ordinal)
                       ? @"\\?\UNC\" + path.Substring(2)
                       : @"\\?\" + path;
        }
        internal static string NormalizeRoot(string root)
        {
            string path = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                                                         Path.AltDirectorySeparatorChar);
            if (Path.GetDirectoryName(path) == null ||
                String.Equals(path, Path.GetPathRoot(path).TrimEnd('\\'),
                              StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A drive or share root cannot be uninstalled.");
            foreach (Environment.SpecialFolder folder in Enum.GetValues(
                         typeof(Environment.SpecialFolder)))
            {
                string protectedPath = Environment.GetFolderPath(folder);
                if (!String.IsNullOrEmpty(protectedPath) &&
                    String.Equals(path, protectedPath.TrimEnd('\\'),
                                  StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Move this installation into its own folder before uninstalling. A " +
                        "shared Windows/user folder cannot be deleted.");
            }
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (String.Equals(path, downloads, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "The Downloads folder cannot be deleted. Move the installation into its own " +
                    "folder first.");
            if (String.Equals(path, Path.GetTempPath().TrimEnd('\\'),
                              StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "The shared temporary folder cannot be uninstalled.");
            return path;
        }
        static string Snapshot(string path, out string final, out uint attributes)
        {
            using (var handle = CreateFileW(Extended(path), 0x80, 7, IntPtr.Zero, 3, 0x02200000,
                                            IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot inspect " + path);
                FileInfo info;
                if (!GetFileInformationByHandle(handle, out info))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var resolved = new StringBuilder(32768);
                uint length =
                    GetFinalPathNameByHandleW(handle, resolved, (uint)resolved.Capacity, 0);
                if (length == 0 || length >= resolved.Capacity)
                    throw new Win32Exception(Marshal.GetLastWin32Error(),
                                             "Cannot resolve installation path");
                final = resolved.ToString().TrimEnd('\\');
                attributes = info.Attributes;
                return info.Volume.ToString("X8") + ":" + info.IndexHigh.ToString("X8") +
                       info.IndexLow.ToString("X8");
            }
        }
        internal static string ValidateRoot(string root, bool requireMarkers = true)
        {
            root = NormalizeRoot(root);
            string resolved;
            uint attributes;
            string identity = Snapshot(root, out resolved, out attributes);
            if ((attributes & DirectoryAttribute) == 0 || (attributes & ReparseAttribute) != 0 ||
                !String.Equals(Extended(root), resolved, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Uninstall requires a real installation " +
                                                    "folder, without redirected parent folders.");
            if (requireMarkers &&
                (!File.Exists(Path.Combine(root, "EnchantedArmsLauncher.exe")) ||
                 !File.Exists(Path.Combine(root, "release.json")) ||
                 !File.ReadAllText(Path.Combine(root, "enchantedarmsrecomp_manifest.toml"))
                      .Contains("name = \"enchantedarmsrecomp\"")))
                throw new InvalidOperationException(
                    "This folder is not an EnchantedArmsPC installation.");
            return identity;
        }
        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (
                var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter
                .ToString(hash.ComputeHash(stream))
                .Replace("-", "")
                .ToLowerInvariant();
        }
        internal static bool IsInside(string path, string root)
        {
            return path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
        }
        internal static void CheckProcesses(string root, int ignorePid)
        {
            foreach (string name in new[] { "enchantedarmsrecomp", "EnchantedArmsLauncher" })
                foreach (var process in Process.GetProcessesByName(name))
                    using (process)
                    {
                        if (process.Id == ignorePid)
                            continue;
                        try
                        {
                            if (IsInside(process.MainModule.FileName, root))
                                throw new InvalidOperationException(
                                    "Close all game and launcher windows for this installation, " +
                                    "then try uninstalling again.");
                        }
                        catch (Win32Exception)
                        {
                            throw new InvalidOperationException(
                                "Close any other Enchanted Arms windows before uninstalling; " +
                                "Windows could not inspect a running process.");
                        }
                        catch (ArgumentException)
                        {
                        } // Process exited during inspection.
                    }
        }
        internal static void Start()
        {
            string root = NormalizeRoot(Program.Root);
            string identity = ValidateRoot(root);
            CheckProcesses(root, Process.GetCurrentProcess().Id);
            string token = Guid.NewGuid().ToString("N");
            string temporary =
                Path.Combine(Path.GetTempPath(), "EnchantedArmsPC-uninstall-" + token);
            Directory.CreateDirectory(temporary);
            File.Copy(Application.ExecutablePath,
                      Path.Combine(temporary, "EnchantedArmsUninstall.exe"));
            File.Copy(Path.Combine(root, "scripts", "uninstall-cleanup.ps1"),
                      Path.Combine(temporary, "cleanup.ps1"));
            var request = new UninstallRequest {
                Root = root,
                Identity = identity,
                Token = token,
                LauncherHash = Hash(Application.ExecutablePath),
                ParentPid = Process.GetCurrentProcess().Id,
                ParentStartTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks
            };
            string receipt = Path.Combine(temporary, "request.json");
            File.WriteAllText(receipt, Program.Json.Serialize(request));
            Process.Start(
                new ProcessStartInfo(Path.Combine(temporary, "EnchantedArmsUninstall.exe"),
                                     Program.Join("--uninstall-helper", receipt)) {
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = temporary
                });
        }
        static void WaitForParent(UninstallRequest request)
        {
            try
            {
                using (var parent = Process.GetProcessById(request.ParentPid))
                {
                    if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStartTicks)
                        throw new InvalidOperationException(
                            "The launcher process changed. Open the launcher and try " +
                            "uninstalling again.");
                    if (!parent.WaitForExit(60000))
                        throw new InvalidOperationException(
                            "The launcher did not close. No files were deleted.");
                }
            }
            catch (ArgumentException)
            {
            } // Already exited.
        }
        static void Retry(Func<bool> operation, string path)
        {
            int error = 0;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (operation())
                    return;
                error = Marshal.GetLastWin32Error();
                if (error == 2 || error == 3)
                    return;
                Thread.Sleep(100);
            }
            throw new Win32Exception(error, "Could not remove " + path +
                                                ". Close programs using this folder and retry.");
        }
        static void RemoveEntry(string path, string root)
        {
            if (!String.Equals(path, root, StringComparison.OrdinalIgnoreCase) &&
                !IsInside(path, root))
                throw new InvalidOperationException(
                    "Deletion escaped the confirmed installation folder.");
            uint attributes = GetFileAttributesW(Extended(path));
            if (attributes == InvalidAttributes)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 2 || error == 3)
                    return;
                throw new Win32Exception(error);
            }
            bool directory = (attributes & DirectoryAttribute) != 0;
            if ((attributes & ReparseAttribute) != 0)
            {
                Retry(() => directory ? RemoveDirectoryW(Extended(path))
                                      : DeleteFileW(Extended(path)),
                      path);
                return;
            }
            string resolved;
            uint actual;
            Snapshot(path, out resolved, out actual);
            if (!String.Equals(resolved, Extended(root), StringComparison.OrdinalIgnoreCase) &&
                !IsInside(resolved, Extended(root)))
                throw new InvalidOperationException(
                    "Resolved deletion path escaped the installation.");
            if ((actual & ReparseAttribute) != 0)
                throw new InvalidOperationException(
                    "A folder link changed during uninstall. Retry after closing other programs.");
            if (directory)
            {
                FindData data;
                IntPtr find = FindFirstFileW(Extended(path + "\\*"), out data);
                if (find == new IntPtr(-1))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 2)
                        throw new Win32Exception(error);
                }
                else
                    try
                    {
                        do
                        {
                            if (data.Name == "." || data.Name == "..")
                                continue;
                            if (data.Name.IndexOfAny(new[] { '\\', '/' }) >= 0)
                                throw new InvalidOperationException("Unsafe directory entry");
                            // Keep launcher/identity markers until all other files are removed,
                            // so a partially blocked uninstall can be retried from the launcher.
                            if (path == root &&
                                new[] { "EnchantedArmsLauncher.exe", "release.json",
                                        "enchantedarmsrecomp_manifest.toml" }
                                    .Contains(data.Name, StringComparer.OrdinalIgnoreCase))
                                continue;
                            RemoveEntry(path + "\\" + data.Name, root);
                        } while (FindNextFileW(find, out data));
                        int last = Marshal.GetLastWin32Error();
                        if (last != 18)
                            throw new Win32Exception(last);
                    }
                    finally
                    {
                        FindClose(find);
                    }
                if (path == root)
                    foreach (string name in new[] { "EnchantedArmsLauncher.exe", "release.json",
                                                    "enchantedarmsrecomp_manifest.toml" })
                        RemoveEntry(path + "\\" + name, root);
            }
            if ((attributes & 1) != 0 && !SetFileAttributesW(Extended(path), attributes & ~1u))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Retry(() => directory ? RemoveDirectoryW(Extended(path)) : DeleteFileW(Extended(path)),
                  path);
        }
        internal static void DeleteInstallation(string root, string expectedIdentity,
                                                bool requireMarkers = true)
        {
            root = NormalizeRoot(root);
            if (ValidateRoot(root, requireMarkers) != expectedIdentity)
                throw new InvalidOperationException(
                    "The installation folder changed after confirmation. No files were deleted.");
            RemoveEntry(root, root);
        }
        internal static bool ShortcutTargets(string shortcut, string target)
        {
            object shell = null, link = null;
            try
            {
                var type = Type.GetTypeFromProgID("WScript.Shell");
                shell = Activator.CreateInstance(type);
                link =
                    type.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod,
                                      null, shell, new object[] { shortcut });
                string actual = Convert.ToString(link.GetType().InvokeMember(
                    "TargetPath", System.Reflection.BindingFlags.GetProperty, null, link, null));
                return !String.IsNullOrEmpty(actual) &&
                       String.Equals(Path.GetFullPath(actual), Path.GetFullPath(target),
                                     StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (link != null)
                    Marshal.FinalReleaseComObject(link);
                if (shell != null)
                    Marshal.FinalReleaseComObject(shell);
            }
        }
        static void RemoveShortcuts(string root)
        {
            foreach (var folder in new[] { Environment.SpecialFolder.DesktopDirectory,
                                           Environment.SpecialFolder.CommonDesktopDirectory,
                                           Environment.SpecialFolder.Programs,
                                           Environment.SpecialFolder.CommonPrograms })
            {
                string directory = Environment.GetFolderPath(folder);
                if (String.IsNullOrEmpty(directory))
                    continue;
                string path = Path.Combine(directory, "EnchantedArmsPC.lnk");
                if (File.Exists(path) &&
                    ShortcutTargets(path, Path.Combine(root, "EnchantedArmsLauncher.exe")))
                    File.Delete(path);
            }
        }
        internal static int RunHelper(string receipt)
        {
            string temporary = Path.GetDirectoryName(Application.ExecutablePath);
            UninstallRequest request = null;
            try
            {
                if (!String.Equals(Path.GetFullPath(receipt),
                                   Path.Combine(temporary, "request.json"),
                                   StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Invalid uninstall request location");
                request = Program.Json.Deserialize<UninstallRequest>(File.ReadAllText(receipt));
                Guid token;
                if (!Guid.TryParseExact(request.Token, "N", out token) ||
                    !String.Equals(temporary,
                                   Path.Combine(Path.GetTempPath(),
                                                "EnchantedArmsPC-uninstall-" + request.Token),
                                   StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(Path.GetFileName(Application.ExecutablePath),
                                   "EnchantedArmsUninstall.exe",
                                   StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Invalid uninstall helper folder");
                if (Hash(Application.ExecutablePath) != request.LauncherHash)
                    throw new InvalidOperationException("Uninstall helper changed");
                WaitForParent(request);
                string root = NormalizeRoot(request.Root);
                CheckProcesses(root, Process.GetCurrentProcess().Id);
                if (Hash(Path.Combine(root, "EnchantedArmsLauncher.exe")) != request.LauncherHash)
                    throw new InvalidOperationException("The installed launcher changed after " +
                                                        "confirmation. No files were deleted.");
                DeleteInstallation(root, request.Identity);
                RemoveShortcuts(root);
                return 0;
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    error.Message +
                        ("\r\n\r\nIf files remain, close programs using the installation and " +
                         "retry. External linked folders are never followed."),
                    "Uninstall could not finish", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                if (request != null &&
                    String.Equals(temporary,
                                  Path.Combine(Path.GetTempPath(),
                                               "EnchantedArmsPC-uninstall-" + request.Token),
                                  StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(
                            Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.System),
                                "WindowsPowerShell", "v1.0", "powershell.exe"),
                            Program.Join(
                                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                                "-File", Path.Combine(temporary, "cleanup.ps1"), "-TaskCleanupRoot",
                                temporary, "-TaskToken", request.Token, "-TaskWaitPid",
                                Process.GetCurrentProcess().Id.ToString(), "-TaskStartTicks",
                                Process.GetCurrentProcess()
                                    .StartTime.ToUniversalTime()
                                    .Ticks.ToString())) { UseShellExecute = false,
                                                          CreateNoWindow = true,
                                                          WorkingDirectory = Path.GetTempPath() });
                    }
                    catch (Exception)
                    {
                    } // Never recreate the deleted installation to log cleanup.
                }
            }
        }
    }
}
