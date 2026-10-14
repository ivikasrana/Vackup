using System;
using System.Diagnostics;
using System.IO;

namespace Vackup
{
    public static class DiskSpeed
    {
        private const int Chunk = 1024 * 1024;
        private const long MaxBytes = 256L * 1024 * 1024;
        private const long MinBytes = 16L * 1024 * 1024;
        private const double MaxSeconds = 1.5;

        public static double MeasureWriteMbPerSecond(string destination)
        {
            var folder = destination;
            while (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                folder = Path.GetDirectoryName(folder);
            }
            if (string.IsNullOrEmpty(folder))
            {
                return 0;
            }

            var file = Path.Combine(folder, ".vackup-speedtest-" + Guid.NewGuid().ToString("N") + ".tmp");
            var buffer = new byte[Chunk];
            new Random().NextBytes(buffer);
            long written = 0;
            var clock = Stopwatch.StartNew();
            try
            {
                using (var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None, Chunk, FileOptions.WriteThrough))
                {
                    while (written < MaxBytes && (written < MinBytes || clock.Elapsed.TotalSeconds < MaxSeconds))
                    {
                        stream.Write(buffer, 0, buffer.Length);
                        written += buffer.Length;
                    }
                    stream.Flush(true);
                }
                clock.Stop();
            }
            catch (Exception)
            {
                return 0;
            }
            finally
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                {
                }
            }
            var seconds = clock.Elapsed.TotalSeconds;
            return seconds > 0 ? written / 1048576.0 / seconds : 0;
        }
    }
}
