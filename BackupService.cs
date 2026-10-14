using System;
using System.Collections.Generic;
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
        public BackupResult Run(BackupOptions options, IProgress<BackupProgress> progress, CancellationToken token)
        {
            var result = new BackupResult();
            var root = options.Destination;
            if (options.DatedFolder)
            {
                root = Path.Combine(root, "Backup_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            }

            var jobs = new List<KeyValuePair<string, string>>();
            long totalBytes = 0;
            progress.Report(new BackupProgress { Message = "Scanning folders..." });
            foreach (var source in options.Sources)
            {
                var name = new DirectoryInfo(source).Name;
                if (string.IsNullOrEmpty(name) || name.EndsWith(":\\"))
                {
                    name = source.Replace(":", "").Replace("\\", "");
                }
                var target = Path.Combine(root, name);
                foreach (var file in Enumerate(source, progress))
                {
                    if (token.IsCancellationRequested)
                    {
                        result.Cancelled = true;
                        return result;
                    }
                    var relative = file.Substring(source.TrimEnd('\\').Length).TrimStart('\\');
                    jobs.Add(new KeyValuePair<string, string>(file, Path.Combine(target, relative)));
                    try
                    {
                        totalBytes += new FileInfo(file).Length;
                    }
                    catch (Exception)
                    {
                    }
                }
                try
                {
                    Directory.CreateDirectory(target);
                    CreateEmptyDirectories(source, target);
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
            foreach (var job in jobs)
            {
                if (token.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    break;
                }
                done++;
                long size = 0;
                try
                {
                    size = new FileInfo(job.Key).Length;
                    if (options.SkipUnchanged && IsUnchanged(job.Key, job.Value))
                    {
                        result.Skipped++;
                        bytesDone += size;
                        progress.Report(new BackupProgress { Done = done, Total = total, BytesDone = bytesDone, BytesTotal = totalBytes, CurrentFile = job.Key, Message = "Skipped " + job.Key });
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(job.Value));
                    var baseBytes = bytesDone;
                    CopyFile(job.Key, job.Value, token, throttle, copied => progress.Report(new BackupProgress { Done = done - 1, Total = total, BytesDone = baseBytes + copied, BytesTotal = totalBytes, CurrentFile = job.Key, Message = "Copying " + job.Key }));
                    bytesDone += size;
                    result.Copied++;
                    progress.Report(new BackupProgress { Done = done, Total = total, BytesDone = bytesDone, BytesTotal = totalBytes, CurrentFile = job.Key, Message = "Copied " + job.Key });
                }
                catch (OperationCanceledException)
                {
                    result.Cancelled = true;
                    break;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    bytesDone += size;
                    progress.Report(new BackupProgress { Done = done, Total = total, BytesDone = bytesDone, BytesTotal = totalBytes, CurrentFile = job.Key, Message = "Failed " + job.Key + ": " + ex.Message, IsError = true });
                }
            }
            return result;
        }

        private static void CopyFile(string source, string target, CancellationToken token, Throttle throttle, Action<long> onBytes)
        {
            var buffer = new byte[128 * 1024];
            long copied = 0;
            var last = DateTime.UtcNow;
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    throttle.Wait(read, token);
                    copied += read;
                    if ((DateTime.UtcNow - last).TotalMilliseconds >= 100)
                    {
                        last = DateTime.UtcNow;
                        onBytes(copied);
                    }
                }
            }
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
        }

        private static bool IsUnchanged(string source, string target)
        {
            if (!File.Exists(target))
            {
                return false;
            }
            var s = new FileInfo(source);
            var t = new FileInfo(target);
            return s.Length == t.Length && Math.Abs((s.LastWriteTimeUtc - t.LastWriteTimeUtc).TotalSeconds) < 2;
        }

        private static IEnumerable<string> Enumerate(string folder, IProgress<BackupProgress> progress)
        {
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                string[] files = new string[0];
                string[] dirs = new string[0];
                try
                {
                    files = Directory.GetFiles(current);
                    dirs = Directory.GetDirectories(current);
                }
                catch (Exception ex)
                {
                    progress.Report(new BackupProgress { Message = "Cannot read " + current + ": " + ex.Message, IsError = true });
                }
                foreach (var f in files)
                {
                    yield return f;
                }
                foreach (var d in dirs)
                {
                    pending.Push(d);
                }
            }
        }

        private static void CreateEmptyDirectories(string source, string target)
        {
            foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                var relative = dir.Substring(source.TrimEnd('\\').Length).TrimStart('\\');
                Directory.CreateDirectory(Path.Combine(target, relative));
            }
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
            var ahead = (int)(due - clock.ElapsedMilliseconds);
            if (ahead > 0)
            {
                token.WaitHandle.WaitOne(ahead);
                token.ThrowIfCancellationRequested();
            }
        }
    }
}
