using System;
using System.IO;
using System.Linq;

namespace EnchantedArmsPC
{
    static class SaveStorageSelfTests
    {
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
        static void Rejected(Action action)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }
            throw new InvalidOperationException("Unsafe save change was accepted");
        }
        internal static void Run()
        {
            string temporary =
                Path.Combine(Path.GetTempPath(), "ea-save-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string root = Path.Combine(temporary, "Game with spaces");
                string legacy = Path.Combine(root, ".local", "runtime", "user");
                string relative =
                    Path.Combine("B13EBABEBABEBABE", "555307E6", "00000001", "save.bin");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(legacy, relative)));
                File.WriteAllText(Path.Combine(legacy, relative), "original progress");
                DateTime stamp = new DateTime(2024, 1, 2, 3, 4, 6, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(Path.Combine(legacy, relative), stamp);
                var preferences = new Preferences();
                string saves = SaveStorage.Prepare(root, preferences);
                Check(File.ReadAllText(Path.Combine(saves, relative)) == "original progress" &&
                          !Directory.Exists(legacy) &&
                          File.GetLastWriteTimeUtc(Path.Combine(saves, relative)) == stamp,
                      "Legacy migration changed progress or timestamps");
                File.WriteAllText(Path.Combine(saves, relative), "new progress");
                SaveStorage.Prepare(root, preferences);
                Check(File.ReadAllText(Path.Combine(saves, relative)) == "new progress",
                      "Migration repeated");
                string moved = root + " moved";
                Directory.Move(root, moved);
                root = moved;
                saves = SaveStorage.Prepare(root, preferences);
                Check(File.ReadAllText(Path.Combine(saves, relative)) == "new progress",
                      "Portable folder lost saves");

                string custom = Path.Combine(temporary, "My saved games");
                preferences.SaveLocation = custom;
                SaveStorage.Prepare(root, preferences);
                Check(File.ReadAllText(Path.Combine(custom, relative)) == "new progress" &&
                          File.Exists(Path.Combine(saves, relative)),
                      "Custom location lost originals");
                File.WriteAllText(Path.Combine(custom, relative), "latest custom progress");
                SaveStorage.Prepare(root, preferences);
                Check(File.ReadAllText(Path.Combine(custom, relative)) == "latest custom progress",
                      "Custom progress reset");

                string conflict = Path.Combine(temporary, "Different saved progress");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(conflict, relative)));
                File.WriteAllText(Path.Combine(conflict, relative), "other player's progress");
                File.WriteAllText(Path.Combine(custom, "missing.txt"), "must not partially copy");
                preferences.SaveLocation = conflict;
                string state = Path.Combine(root, ".local", "save-location.json");
                string before = File.ReadAllText(state);
                Rejected(() => SaveStorage.Prepare(root, preferences));
                Check(File.ReadAllText(Path.Combine(conflict, relative)) ==
                              "other player's progress" &&
                          !File.Exists(Path.Combine(conflict, "missing.txt")) &&
                          File.ReadAllText(state) == before,
                      "Conflict changed saves or active location");

                string next = Path.Combine(temporary, "Next save folder");
                preferences.SaveLocation = next;
                SaveStorage.Prepare(root, preferences);
                Check(File.ReadAllText(Path.Combine(next, relative)) == "latest custom progress",
                      "Relocation used stale saves");
                Directory.Move(next, next + " disconnected");
                preferences.SaveLocation = next + " replacement";
                Rejected(() => SaveStorage.Prepare(root, preferences));
                Check(!Directory.Exists(preferences.SaveLocation),
                      "Missing source created fresh saves");

                foreach (string unsafePath in new[] { root, Path.Combine(root, "bin", "saves"),
                                                      Path.Combine(root, ".local", "game"),
                                                      Path.GetDirectoryName(root), "relative" })
                    Rejected(() => SaveStorage.Validate(root, unsafePath));
                var roundtrip =
                    Program.Json.Deserialize<Preferences>(Program.Json.Serialize(preferences));
                Check(roundtrip.SaveLocation == preferences.SaveLocation,
                      "Save location did not persist");
                Check(Program.GameArguments(preferences, "test.log")
                          .Contains("--user_data_root=" + preferences.SaveLocation),
                      "Game ignored chosen save location");
                string project = Path.Combine(temporary, "Downloaded kit", "EnchantedArmsPC");
                Directory.CreateDirectory(Path.Combine(project, "scripts"));
                File.WriteAllText(Path.Combine(project, "release.json"), "{\"version\":\"0.5.6\"}");
                File.WriteAllText(Path.Combine(project, "enchantedarmsrecomp_manifest.toml"),
                                  "name = \"enchantedarmsrecomp\"");
                File.WriteAllText(Path.Combine(project, "scripts", "setup.ps1"), "# fixture");
                Check(
                    InstallLocation.Same(
                        InstallLocation.Resolve(Path.GetDirectoryName(project), project), project),
                    "Selecting the source download wrapper did not resolve in place");
                Check(InstallLocation.Same(
                          InstallLocation.Resolve(Path.GetDirectoryName(project), root), project),
                      "Selecting another download wrapper did not find its project");
                string linkedRoot = Path.Combine(temporary, "Legacy linked runtime");
                string external = Path.Combine(temporary, "External runtime");
                Directory.CreateDirectory(Path.Combine(linkedRoot, ".local"));
                Directory.CreateDirectory(Path.Combine(external, "user"));
                File.WriteAllText(Path.Combine(external, "user", "progress.bin"),
                                  "linked legacy progress");
                string junction = Path.Combine(linkedRoot, ".local", "runtime");
                UninstallSelfTests.Junction(junction, external);
                try
                {
                    string imported = SaveStorage.Prepare(linkedRoot, new Preferences());
                    Check(File.ReadAllText(Path.Combine(imported, "progress.bin")) ==
                                  "linked legacy progress" &&
                              File.ReadAllText(Path.Combine(external, "user", "progress.bin")) ==
                                  "linked legacy progress",
                          "Legacy linked saves were not safely copied");
                    var linkedPreferences =
                        new Preferences { SaveLocation = Path.Combine(junction, "new-saves") };
                    Rejected(() => SaveStorage.Prepare(linkedRoot, linkedPreferences));
                    Check(!Directory.Exists(Path.Combine(external, "new-saves")),
                          "Save writes followed a junction");
                }
                finally
                {
                    Directory.Delete(junction); // Remove the fixture link itself, never its target.
                }
            }
            finally
            {
                Directory.Delete(temporary, true);
            }
        }
    }
}
