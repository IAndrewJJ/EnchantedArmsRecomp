using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace EnchantedArmsPC
{
    static class LocalTools
    {
        internal static string Python
        {
            get {
                return Path.Combine(Program.Local, "python", "python.exe");
            }
        }
        internal static Task<Dictionary<string, object>> Run(string script,
                                                             params string[] arguments)
        {
            return Run(script, CancellationToken.None, arguments);
        }
        internal static async Task<Dictionary<string, object>> Run(string script,
                                                                   CancellationToken cancellation,
                                                                   params string[] arguments)
        {
            if (!File.Exists(Python))
                throw new InvalidOperationException(
                    "Build your game first to prepare the portable update tools.");
            var args = new List<string> { Path.Combine(Program.Root, "tools", script) };
            args.AddRange(arguments);
            var info = new ProcessStartInfo(Python, Program.Join(args.ToArray())) {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Program.Root,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var process = Process.Start(info)) using (
                cancellation.Register(() =>
                                      {
                                          try
                                          {
                                              if (!process.HasExited)
                                                  process.Kill();
                                          }
                                          catch (InvalidOperationException)
                                          {
                                          }
                                          catch (System.ComponentModel.Win32Exception)
                                          {
                                          }
                                      }))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                cancellation.ThrowIfCancellationRequested();
                string text = await output;
                string error = await errors;
                Dictionary<string, object> result = null;
                try
                {
                    result = Program.Json.Deserialize<Dictionary<string, object>>(text);
                }
                catch (ArgumentException)
                {
                }
                if (process.ExitCode != 0 || result == null || result.ContainsKey("error"))
                    throw new InvalidOperationException(
                        result != null && result.ContainsKey("error")
                            ? Convert.ToString(result["error"])
                            : "The operation failed. " +
                                  (error.Length > 500 ? error.Substring(0, 500) : error));
                return result;
            }
        }
        internal static void ApplyUpdate(string stage, bool rollback)
        {
            var args = new List<string> { Path.Combine(Program.Root, "tools", "update_game.py"),
                                          rollback ? "rollback" : "apply",
                                          "--root",
                                          Program.Root,
                                          "--wait-pid",
                                          Process.GetCurrentProcess().Id.ToString(),
                                          "--restart" };
            if (!rollback)
            {
                args.Add("--stage");
                args.Add(stage);
            }
            Process.Start(new ProcessStartInfo(Python, Program.Join(args.ToArray())) {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Program.Root
            });
        }
    }
}
