using System;
using System.IO;
using System.Text;

namespace EnchantedArmsPC
{
    // A bounded, buffered writer shared by the two redirected compiler streams.
    sealed class SetupLog : IDisposable
    {
        readonly object sync = new object();
        readonly StreamWriter writer;
        readonly int limit;
        int written;
        bool truncated, failed;

        internal SetupLog(string path, int limit = 16 * 1024 * 1024)
        {
            this.limit = limit;
            writer = new StreamWriter(
                new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                new UTF8Encoding(false), 64 * 1024);
        }
        internal void WriteLine(string line)
        {
            lock (sync)
            {
                if (failed || truncated)
                    return;
                try
                {
                    int bytes = Encoding.UTF8.GetByteCount(line) + 2;
                    if (bytes > limit - written)
                    {
                        writer.WriteLine(
                            "[Setup log limit reached; further compiler output omitted.]");
                        truncated = true;
                    }
                    else
                    {
                        writer.WriteLine(line);
                        written += bytes;
                    }
                }
                catch (IOException)
                {
                    failed = true;
                }
                catch (UnauthorizedAccessException)
                {
                    failed = true;
                }
            }
        }
        public void Dispose()
        {
            lock (sync)
            {
                try
                {
                    writer.Dispose();
                }
                catch (IOException error)
                {
                    Program.Diagnostic("Could not flush setup log: " + error.Message);
                }
            }
        }
    }
}
