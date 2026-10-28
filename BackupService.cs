using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Vackup
{
    public class BackupOptions
    {
        public List<string> Sources { get; set; } = new List<string>();
        public string Destination { get; set; }
        public bool SkipUnchanged { get; set; }
        public bool DatedFolder { get; set; }
        public Throttle Throttle { get; set; }
    }

    public class BackupProgress
    {
        public int Done { get; set; }
        public int Total { get; set; }
        public long BytesDone { get; set; }
        public long BytesTotal { get; set; }
        public string CurrentFile { get; set; }
        public string Message { get; set; }
        public bool IsError { get; set; }
    }

    public class BackupResult
    {
        public int Copied { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public bool Cancelled { get; set; }
    }

    public class BackupService
    {
        private const int BufferSize = 256 * 1024;
        private const int ProgressIntervalMs = 100;

        private class FileJob
        {
            public string Source;
            public string Target;
            public long Size;
        }

        public BackupResult Run(BackupOptions options, IProgress<BackupProgress> progress, CancellationToken token)
        {
            var result = new BackupResult();
            var root = options.Destination;
            if (options.DatedFolder)
            {
                root = Path.Combine(root, "Backup_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            }

            var jobs = new List<FileJob>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            progress.Report(new BackupProgress { Message = "Scanning folders..." });
            foreach (var source in options.Sources)
            {
                var sourceRoot = source.TrimEnd('\\');
                var target = Path.Combine(root, UniqueName(source, usedNames));
                var dirs = new List<string>();
                if (!Scan(source, sourceRoot, target, jobs, dirs, ref totalBytes, progress, token))
                {
                    result.Cancelled = true;
                    return result;
                }
                try
                {
                    Directory.CreateDirectory(target);
                    foreach (var dir in dirs)
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
                catch (Exception ex)
                {
                    progress.Report(new BackupProgress { Message = "Folder error: " + ex.Message, IsError = true });
                }
            }

            var total = jobs.Count;
            var done = 0;
            long bytesDone = 0;
            var throttle = options.Throttle ?? new Throttle(0);
            var reportClock = Stopwatch.StartNew();
            foreach (var job in jobs)
            {
                if (token.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    break;
                }
                done++;
                var current = done;
                var baseBytes = bytesDone;
                try
                {
                    if (options.SkipUnchanged && IsUnchanged(job))
                    {
                        result.Skipped++;
                        bytesDone += job.Size;
                        ReportFile(progress, reportClock, current, total, bytesDone, totalBytes, job.Source, "Skipped ", current == total);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(job.Target));
                    CopyFile(job, token, throttle, copied =>
                        progress.Report(new BackupProgress { Done = current - 1, Total = total, BytesDone = baseBytes + copied, BytesTotal = totalBytes, CurrentFile = job.Source, Message = "Copying " + job.Source }));
                    bytesDone += job.Size;
                    result.Copied++;
                    ReportFile(progress, reportClock, current, total, bytesDone, totalBytes, job.Source, "Copied ", current == total);
                }
                catch (OperationCanceledException)
                {
                    result.Cancelled = true;
                    break;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    bytesDone += job.Size;
                    progress.Report(new BackupProgress { Done = current, Total = total, BytesDone = bytesDone, BytesTotal = totalBytes, CurrentFile = job.Source, Message = "Failed " + job.Source + ": " + ex.Message, IsError = true });
                }
            }
            return result;
        }

        // Per-file progress is rate limited so thousands of small files do not flood the UI queue.
        private static void ReportFile(IProgress<BackupProgress> progress, Stopwatch clock, int done, int total, long bytesDone, long bytesTotal, string file, string verb, bool force)
        {
            if (!force && clock.ElapsedMilliseconds < ProgressIntervalMs)
            {
                return;
            }
            clock.Restart();
            progress.Report(new BackupProgress { Done = done, Total = total, BytesDone = bytesDone, BytesTotal = bytesTotal, CurrentFile = file, Message = verb + file });
        }

        // Two sources with the same folder name (C:\a\Docs, D:\Docs) must not merge into one target folder.
        private static string UniqueName(string source, HashSet<string> used)
        {
            var name = new DirectoryInfo(source).Name;
            if (string.IsNullOrEmpty(name) || name.EndsWith(":\\"))
            {
                name = source.Replace(":", "").Replace("\\", "");
            }
            var candidate = name;
            for (var n = 2; !used.Add(candidate); n++)
            {
                candidate = name + " (" + n + ")";
            }
            return candidate;
        }

        private static bool Scan(string folder, string sourceRoot, string targetRoot, List<FileJob> jobs, List<string> dirs, ref long totalBytes, IProgress<BackupProgress> progress, CancellationToken token)
        {
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                if (token.IsCancellationRequested)
                {
                    return false;
                }
                var current = pending.Pop();
                try
                {
                    var info = new DirectoryInfo(current);
                    foreach (var f in info.GetFiles())
                    {
                        var relative = f.FullName.Substring(sourceRoot.Length).TrimStart('\\');
                        jobs.Add(new FileJob { Source = f.FullName, Target = Path.Combine(targetRoot, relative), Size = f.Length });
                        totalBytes += f.Length;
                    }
                    foreach (var d in info.GetDirectories())
                    {
                        // Junctions and symlinks can loop back into the tree (e.g. "Application Data"), so skip them.
                        if ((d.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }
                        dirs.Add(Path.Combine(targetRoot, d.FullName.Substring(sourceRoot.Length).TrimStart('\\')));
                        pending.Push(d.FullName);
                    }
                }
                catch (Exception ex)
                {
                    progress.Report(new BackupProgress { Message = "Cannot read " + current + ": " + ex.Message, IsError = true });
                }
            }
            return true;
        }

        private static void CopyFile(FileJob job, CancellationToken token, Throttle throttle, Action<long> onBytes)
        {
            var buffer = new byte[BufferSize];
            long copied = 0;
            var clock = Stopwatch.StartNew();
            try
            {
                using (var input = new FileStream(job.Source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, FileOptions.SequentialScan))
                using (var output = new FileStream(job.Target, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                {
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        output.Write(buffer, 0, read);
                        throttle.Wait(read, token);
                        copied += read;
                        if (clock.ElapsedMilliseconds >= ProgressIntervalMs)
                        {
                            clock.Restart();
                            onBytes(copied);
                        }
                    }
                }
                File.SetLastWriteTimeUtc(job.Target, File.GetLastWriteTimeUtc(job.Source));
            }
            catch (Exception)
            {
                // Never leave a truncated file behind that a later "skip unchanged" run could trust.
                try
                {
                    File.Delete(job.Target);
                }
                catch (Exception)
                {
                }
                throw;
            }
        }

        private static bool IsUnchanged(FileJob job)
        {
            var t = new FileInfo(job.Target);
            return t.Exists && t.Length == job.Size
                && Math.Abs((File.GetLastWriteTimeUtc(job.Source) - t.LastWriteTimeUtc).TotalSeconds) < 2;
        }
    }
}

namespace Vackup
{
    public class Throttle
    {
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private long limit;
        private long active;
        private long total;

        public Throttle(long bytesPerSecond)
        {
            limit = bytesPerSecond;
        }

        public void SetLimit(long bytesPerSecond)
        {
            System.Threading.Interlocked.Exchange(ref limit, bytesPerSecond);
        }

        public void Wait(int bytes, System.Threading.CancellationToken token)
        {
            var current = System.Threading.Interlocked.Read(ref limit);
            if (current != active)
            {
                active = current;
                total = 0;
                clock.Restart();
            }
            if (current <= 0)
            {
                return;
            }
            total += bytes;
            var due = total * 1000.0 / current;
            var ahead = due - clock.ElapsedMilliseconds;
            if (ahead < -1000)
            {
                total = bytes;
                clock.Restart();
                ahead = bytes * 1000.0 / current;
            }
            if (ahead > 0)
            {
                token.WaitHandle.WaitOne((int)Math.Min(ahead, int.MaxValue));
                token.ThrowIfCancellationRequested();
            }
        }
    }
}
