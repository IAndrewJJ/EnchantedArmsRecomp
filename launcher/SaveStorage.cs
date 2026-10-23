using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace EnchantedArmsPC
{
    // Only launcher-managed saves are imported. Caches, game files and settings
    // never enter the player's save folder. The game must be closed before use.
    static class SaveStorage
    {
        internal static bool Exists(string root)
        {
            return Directory.Exists(Path.Combine(root, "Saves")) ||
                   Directory.Exists(Path.Combine(root, ".local", "runtime", "user")) ||
                   File.Exists(Path.Combine(root, ".local", "save-location.json"));
        }
        internal static string Selected(string root, Preferences preferences)
        {
            return String.IsNullOrWhiteSpace(preferences.SaveLocation)
                       ? Path.Combine(root, "Saves")
                       : Path.GetFullPath(preferences.SaveLocation.Trim());
        }
        internal static string Validate(string root, string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path.Trim()))
                throw new InvalidOperationException("Choose a full folder path for your saves.");
            string target = Uninstall.NormalizeRoot(path.Trim());
            InstallLocation.RegularPath(target);
            if (File.Exists(target) || InstallLocation.Same(target, root) ||
                Uninstall.IsInside(root, target) || InstallLocation.ProjectFolder(target))
                throw new InvalidOperationException("Choose a dedicated save folder, such as the " +
                                                    "Saves folder inside your game folder.");
            foreach (string name in new[] { ".local", ".git", "bin", "bootstrap", "renderer", "src",
                                            "config", "launcher", "tools", "scripts", "generated",
                                            "data", "docs", "licenses", ".github" })
            {
                string reserved = Path.Combine(root, name);
                if (InstallLocation.Same(target, reserved) || Uninstall.IsInside(target, reserved))
                    throw new InvalidOperationException(
                        "Choose a save folder outside the game's tools and program files.");
            }
            return target;
        }
        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (
                var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream));
        }
        static List<string> Files(string directory)
        {
            InstallLocation.RegularPath(directory);
            var files = new List<string>();
            if (!Directory.Exists(directory))
                return files;
            var pending = new Stack<string>();
            pending.Push(directory);
            while (pending.Count != 0)
                foreach (string entry in Directory.GetFileSystemEntries(pending.Pop()))
                {
                    InstallLocation.RegularPath(entry);
                    if (Directory.Exists(entry))
                        pending.Push(entry);
                    else
                        files.Add(entry);
                }
            return files;
        }
        static void Copy(string source, string destination)
        {
            if (InstallLocation.Same(source, destination))
            {
                Files(destination); // Inspect linked contents even when no copy is needed.
                Directory.CreateDirectory(destination);
                return;
            }
            if (Uninstall.IsInside(source, destination) || Uninstall.IsInside(destination, source))
                throw new InvalidOperationException(
                    "Choose a save folder outside your current save folder.");
            var files = Files(source);
            Files(destination);
            // Check every conflict before copying anything. An existing save with
            // different progress must never be replaced or silently selected.
            foreach (string file in files)
            {
                string target = Path.Combine(destination, file.Substring(source.Length + 1));
                InstallLocation.RegularPath(target);
                if (Directory.Exists(target) || (File.Exists(target) && Hash(file) != Hash(target)))
                    throw new InvalidOperationException(
                        "That folder already contains different saved progress. Choose an empty " +
                        "save folder or keep your current location. Your saves have not been " +
                        "overwritten.");
                for (string parent = Path.GetDirectoryName(target);
                     !InstallLocation.Same(parent, destination);
                     parent = Path.GetDirectoryName(parent))
                    if (File.Exists(parent))
                        throw new InvalidOperationException(
                            "A file is blocking the chosen save folder. Choose another folder.");
            }
            Directory.CreateDirectory(destination);
            foreach (string file in files)
            {
                string target = Path.Combine(destination, file.Substring(source.Length + 1));
                if (File.Exists(target))
                {
                    if (Hash(file) != Hash(target))
                        throw new IOException("The destination saves changed while copying. Your " +
                                              "originals are unchanged; choose another folder or " +
                                              "retry after closing other game windows.");
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                string temporary = target + "." + Guid.NewGuid().ToString("N") + ".partial";
                try
                {
                    File.Copy(file, temporary, false);
                    if (Hash(file) != Hash(temporary))
                        throw new IOException(
                            "Could not verify copied saves. Your original saves are unchanged.");
                    File.SetLastWriteTimeUtc(temporary, File.GetLastWriteTimeUtc(file));
                    File.Move(temporary, target);
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }
        }
        internal static string Prepare(string root, Preferences preferences)
        {
            string destination = Validate(root, Selected(root, preferences));
            string state = Path.Combine(root, ".local", "save-location.json");
            InstallLocation.RegularPath(state);
            string legacy = Path.Combine(root, ".local", "runtime", "user");
            string source;
            bool first = !File.Exists(state);
            if (!first)
            {
                var previous =
                    Program.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(state));
                source = Selected(root, new Preferences { SaveLocation = Convert.ToString(
                                                              previous["location"]) });
                Validate(root, source);
                // Missing external drives must not reset the active save location.
                if (!Directory.Exists(source))
                    throw new IOException("Your current save folder is unavailable. Reconnect " +
                                          "its drive before changing the save location.");
            }
            else
                source = Directory.Exists(legacy) ? Uninstall.ResolveDirectory(legacy)
                                                  : Path.Combine(root, "Saves");
            InstallLocation.RegularPath(source);
            if (first && InstallLocation.Same(source, legacy) &&
                InstallLocation.Same(destination, Path.Combine(root, "Saves")) &&
                !Directory.Exists(destination))
            {
                Files(source);
                // Same-volume directory rename keeps all original bytes and dates,
                // and cannot leave a half-moved save tree if setup is interrupted.
                Directory.Move(source, destination);
            }
            else
                Copy(source, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(state));
            string text = Program.Json.Serialize(
                new { location =
                          String.IsNullOrWhiteSpace(preferences.SaveLocation) ? "" : destination });
            if (!File.Exists(state) || File.ReadAllText(state) != text)
            {
                string temporary = state + ".partial";
                InstallLocation.RegularPath(temporary);
                File.WriteAllText(temporary, text);
                if (File.Exists(state))
                    File.Replace(temporary, state, null);
                else
                    File.Move(temporary, state);
            }
            return destination;
        }
    }
}
