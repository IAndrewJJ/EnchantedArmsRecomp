using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    static class InstallLocation
    {
        internal static string Unavailable;
        static string Pointer
        {
            get {
                return Path.Combine(Program.Local, "installed-location.json");
            }
        }
        internal static string Selected(Preferences preferences)
        {
            return String.IsNullOrWhiteSpace(preferences.BuildLocation)
                       ? Program.Root
                       : Path.GetFullPath(preferences.BuildLocation.Trim());
        }
        static Dictionary<string, object> Read(string path)
        {
            return Program.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
        }
        internal static bool Same(string a, string b)
        {
            return String.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'),
                                 Path.GetFullPath(b).TrimEnd('\\', '/'),
                                 StringComparison.OrdinalIgnoreCase);
        }
        static void RegularPath(string path)
        {
            for (string part = path; !String.IsNullOrEmpty(part);
                 part = Path.GetDirectoryName(part))
                if ((Directory.Exists(part) || File.Exists(part)) &&
                    (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException(
                        "Choose a regular folder without linked or redirected parent folders.");
        }
        internal static bool Owned(string source, string destination)
        {
            try
            {
                RegularPath(destination);
                string identityPath = Path.Combine(source, ".local", "installation-id.json");
                string ownerPath = Path.Combine(destination, ".local", "build-location-owner.json");
                RegularPath(identityPath);
                RegularPath(ownerPath);
                var identity = Read(identityPath);
                var owner = Read(ownerPath);
                return Convert.ToString(owner["project"]) == "EnchantedArmsPC" &&
                       Convert.ToString(owner["source_id"]) == Convert.ToString(identity["id"]);
            }
            catch (Exception)
            {
                return false;
            }
        }
        internal static string Validate(string path, bool building = false)
        {
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path.Trim()))
                throw new InvalidOperationException("Choose a full folder path for your game.");
            string target = Uninstall.NormalizeRoot(path.Trim());
            if (building && System.Text.Encoding.UTF8.GetByteCount(target) != target.Length)
                throw new InvalidOperationException(
                    "The current build tools need a folder path " +
                    "without accented or non-Latin characters. Choose a folder such as " +
                    @"C:\Games\EnchantedArmsPC.");
            RegularPath(target);
            if (Same(target, Program.Root))
                return target;
            if (Uninstall.IsInside(target, Program.Root) ||
                Uninstall.IsInside(Program.Root, target))
                throw new InvalidOperationException(
                    "Choose a separate folder outside the current launcher folder.");
            if (File.Exists(target))
                throw new InvalidOperationException("The build location must be a folder.");
            if (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length != 0 &&
                !Owned(Program.Root, target))
                throw new InvalidOperationException("Choose an empty folder, or the build folder " +
                                                    "previously created by this launcher.");
            return target;
        }
        static bool Complete(string target, bool current)
        {
            var receipt = Read(Path.Combine(target, ".local", "installation.json"));
            string version = Convert.ToString(receipt["build"]);
            if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+$") ||
                (current && version != ReleaseInfo.NativeVersion))
                return false;
            string native = Path.Combine(target, "bin", "graphics-" + version);
            return File.Exists(Path.Combine(target, "EnchantedArmsLauncher.exe")) &&
                   File.Exists(Path.Combine(target, ".local", "game", "default.xex")) &&
                   File.Exists(Path.Combine(native, "enchantedarmsrecomp.exe")) &&
                   File.Exists(Path.Combine(native, "rexruntimerd.dll")) &&
                   File.Exists(Path.Combine(native, "rexgpu-xenosrd.dll"));
        }
        static void Open(string target)
        {
            Process.Start(new ProcessStartInfo(Path.Combine(target, "EnchantedArmsLauncher.exe")) {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = target
            });
        }
        internal static void Activate(string target)
        {
            if (!Owned(Program.Root, target) || !Complete(target, true))
                throw new InvalidOperationException(
                    "The new build is incomplete. Your previous installation is still available.");
            Uninstall.ValidateRoot(target);
            var identity = Read(Path.Combine(Program.Local, "installation-id.json"));
            string temporary = Pointer + ".partial";
            File.WriteAllText(temporary, Program.Json.Serialize(
                                             new { root = target,
                                                   source_id = Convert.ToString(identity["id"]) }));
            if (File.Exists(Pointer))
                File.Replace(temporary, Pointer, null);
            else
                File.Move(temporary, Pointer);
            Open(target);
            // Retarget an existing shortcut belonging to this installation only.
            try
            {
                string desktop =
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string link = Path.Combine(desktop, "EnchantedArmsPC.lnk");
                if (File.Exists(link) &&
                    Uninstall.ShortcutTargets(
                        link, Path.Combine(Program.Root, "EnchantedArmsLauncher.exe")))
                    Program.Shortcut(desktop, target);
            }
            catch (Exception error)
            {
                Program.Diagnostic("Could not update desktop shortcut: " + error.Message);
            }
        }
        internal static void UseCurrentFolder()
        {
            if (File.Exists(Pointer))
                File.Delete(Pointer);
            Unavailable = null;
        }
        internal static bool TryRedirect()
        {
            if (!File.Exists(Pointer))
                return false;
            try
            {
                var pointer = Read(Pointer);
                string target = Convert.ToString(pointer["root"]);
                if (Same(target, Program.Root))
                    return false;
                string identity = Convert.ToString(
                    Read(Path.Combine(Program.Local, "installation-id.json"))["id"]);
                if (Convert.ToString(pointer["source_id"]) != identity ||
                    !Owned(Program.Root, target) || !Complete(target, false))
                    throw new InvalidOperationException("Build folder unavailable");
                Uninstall.ValidateRoot(target);
                Open(target);
                return true;
            }
            catch (Exception error)
            {
                Program.Diagnostic("Could not open build folder: " + error.Message);
                Unavailable = "Your build folder is unavailable. Reconnect its drive or open " +
                              "Settings → Game.";
                return false;
            }
        }
    }
}
