using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace Vackup
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> sources = new ObservableCollection<string>();
        private CancellationTokenSource cts;
        private string themeSetting = "";
        private readonly Throttle throttle = new Throttle(0);
        private readonly Stopwatch clock = new Stopwatch();

        public MainWindow()
        {
            InitializeComponent();
            SourceList.ItemsSource = sources;

            var settings = AppSettings.Load();
            foreach (var s in settings.Sources)
            {
                sources.Add(s);
            }
            DestinationBox.Text = settings.Destination;
            SkipUnchangedBox.IsChecked = settings.SkipUnchanged;
            DatedFolderBox.IsChecked = settings.DatedFolder;
            SpeedLimitSlider.Maximum = Math.Max(100, settings.SpeedLimitMb);
            SpeedLimitSlider.Value = Math.Max(settings.SpeedLimitMb, 0);
            throttle.SetLimit(LimitBytes());
            themeSetting = settings.Theme ?? "";
            ThemeManager.Changed += () => ThemeButton.Content = ThemeManager.IsDark ? Glyph(0xE706) : Glyph(0xE708);
            ThemeManager.Apply(ThemeManager.Resolve(themeSetting));
            StateChanged += (s, e) =>
            {
                MaxButton.Content = WindowState == WindowState.Maximized ? Glyph(0xE923) : Glyph(0xE922);
                Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            };
            DestinationBox.LostFocus += (s, e) => { _ = DetectSpeedAsync(); };
            Loaded += (s, e) => { _ = DetectSpeedAsync(); };
            Closing += (s, e) => SaveSettings();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            themeSetting = ThemeManager.IsDark ? "light" : "dark";
            ThemeManager.Apply(!ThemeManager.IsDark);
            SaveSettings();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private string detectedFor = "";
        private bool detecting;

        private async Task DetectSpeedAsync(bool force = false)
        {
            var path = DestinationBox.Text.Trim();
            if (detecting || cts != null || path.Length == 0 || (!force && path == detectedFor))
            {
                return;
            }
            detecting = true;
            DetectButton.IsEnabled = false;
            SpeedDetectText.Text = "Testing destination speed...";
            double speed;
            try
            {
                var free = GetFreeSpace(path);
                speed = free >= 0 && free < 512L * 1024 * 1024 ? 0 : await Task.Run(() => DiskSpeed.MeasureWriteMbPerSecond(path));
            }
            finally
            {
                detecting = false;
                DetectButton.IsEnabled = cts == null;
            }
            detectedFor = path;
            if (speed <= 0)
            {
                SpeedDetectText.Text = "Could not measure the destination speed.";
                return;
            }
            var max = Math.Ceiling(speed / 10) * 10;
            var step = max >= 1000 ? 25 : max >= 200 ? 10 : max >= 50 ? 5 : 1;
            SpeedLimitSlider.TickFrequency = step;
            SpeedLimitSlider.SmallChange = step;
            SpeedLimitSlider.LargeChange = step * 5;
            SpeedLimitSlider.Maximum = max;
            if (SpeedLimitSlider.Value > max)
            {
                SpeedLimitSlider.Value = 0;
            }
            SpeedDetectText.Text = "Destination writes about " + speed.ToString("0") + " MB/s";
        }

        private void DetectButton_Click(object sender, RoutedEventArgs e)
        {
            _ = DetectSpeedAsync(true);
        }

        private long LimitBytes() => (long)(SpeedLimitSlider.Value * 1024 * 1024);

        private void SpeedLimitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpeedLimitText == null)
            {
                return;
            }
            SpeedLimitText.Text = SpeedLimitSlider.Value <= 0 ? "Unlimited" : SpeedLimitSlider.Value.ToString("0") + " MB/s";
            throttle.SetLimit(LimitBytes());
        }

        private static string Glyph(int code) => ((char)code).ToString();

        private void SaveSettings()
        {
            new AppSettings
            {
                Sources = sources.ToList(),
                Destination = DestinationBox.Text.Trim(),
                SkipUnchanged = SkipUnchangedBox.IsChecked == true,
                DatedFolder = DatedFolderBox.IsChecked == true,
                SpeedLimitMb = SpeedLimitSlider.Value,
                Theme = themeSetting
            }.Save();
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            var i = 0;
            while (value >= 1024 && i < units.Length - 1)
            {
                value /= 1024;
                i++;
            }
            return value.ToString(i == 0 ? "0" : "0.#") + " " + units[i];
        }

        private string PickFolder(string description)
        {
            using (var dialog = new FolderBrowserDialog { Description = description, ShowNewFolderButton = true })
            {
                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddFolderDialog(DestinationBox.Text.Trim()) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }
            foreach (var path in dialog.Sources)
            {
                if (!sources.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    sources.Add(path);
                }
            }
            if (dialog.Destination.Length > 0)
            {
                DestinationBox.Text = dialog.Destination;
                _ = DetectSpeedAsync();
            }
            SaveSettings();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in SourceList.SelectedItems.Cast<string>().ToList())
            {
                sources.Remove(item);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFolder("Select the backup destination");
            if (path != null)
            {
                DestinationBox.Text = path;
                _ = DetectSpeedAsync();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
            CancelButton.IsEnabled = false;
            StatusText.Text = "Cancelling...";
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var destination = DestinationBox.Text.Trim();
            if (sources.Count == 0)
            {
                Alert("Add at least one folder to back up.");
                return;
            }
            if (destination.Length == 0)
            {
                Alert("Choose a destination.");
                return;
            }
            var fullDestination = Path.GetFullPath(destination).TrimEnd('\\') + "\\";
            foreach (var s in sources)
            {
                var fullSource = Path.GetFullPath(s).TrimEnd('\\') + "\\";
                if (fullDestination.StartsWith(fullSource, StringComparison.OrdinalIgnoreCase))
                {
                    Alert("The destination cannot be inside a source folder:\n" + s);
                    return;
                }
            }

            var options = new BackupOptions
            {
                Sources = sources.ToList(),
                Destination = destination,
                SkipUnchanged = SkipUnchangedBox.IsChecked == true,
                DatedFolder = DatedFolderBox.IsChecked == true,
                Throttle = throttle
            };

            SetBusy(true);
            Progress.Value = 0;
            PercentText.Text = "0%";
            FileText.Text = "";
            StatsText.Text = "";
            clock.Restart();
            SaveSettings();
            cts = new CancellationTokenSource();
            var token = cts.Token;
            var reporter = new System.Progress<BackupProgress>(OnProgress);

            try
            {
                Directory.CreateDirectory(destination);
                var result = await Task.Run(() => new BackupService().Run(options, reporter, token));
                var summary = $"{result.Copied} copied, {result.Skipped} skipped, {result.Failed} failed";
                StatusText.Text = result.Cancelled ? "Cancelled - " + summary : "Done - " + summary;
                if (!result.Cancelled)
                {
                    Progress.Value = 100;
                    PercentText.Text = "100%";
                }
                FileText.Text = "";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Backup failed";
                Alert(ex.Message);
            }
            finally
            {
                cts.Dispose();
                cts = null;
                SetBusy(false);
            }
        }

        private void OnProgress(BackupProgress p)
        {
            if (p.Total > 0)
            {
                var percent = p.BytesTotal > 0 ? p.BytesDone * 100.0 / p.BytesTotal : p.Done * 100.0 / p.Total;
                Progress.Value = percent;
                PercentText.Text = percent.ToString("0") + "%";
                StatusText.Text = $"{Math.Min(p.Done + 1, p.Total)} of {p.Total} files  -  {FormatBytes(p.BytesDone)} of {FormatBytes(p.BytesTotal)}";
                FileText.Text = p.CurrentFile;
                UpdateStats(p);
            }
            else
            {
                StatusText.Text = p.Message;
            }
        }

        private void UpdateStats(BackupProgress p)
        {
            var seconds = clock.Elapsed.TotalSeconds;
            if (seconds < 1 || p.BytesDone <= 0)
            {
                return;
            }
            var speed = p.BytesDone / seconds;
            var remaining = Math.Max(p.BytesTotal - p.BytesDone, 0);
            var eta = TimeSpan.FromSeconds(speed > 0 ? remaining / speed : 0);
            var text = $"{FormatBytes((long)speed)}/s  -  ETA {(int)eta.TotalHours:00}:{eta.Minutes:00}:{eta.Seconds:00}";
            var free = GetFreeSpace(DestinationBox.Text.Trim());
            if (free >= 0)
            {
                text += $"  -  Destination free {FormatBytes(free)}";
            }
            StatsText.Text = text;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetDiskFreeSpaceEx(string directory, out ulong available, out ulong total, out ulong totalFree);

        private static long GetFreeSpace(string path)
        {
            try
            {
                ulong available, total, totalFree;
                if (path.Length > 0 && GetDiskFreeSpaceEx(path, out available, out total, out totalFree))
                {
                    return (long)available;
                }
            }
            catch (Exception)
            {
            }
            return -1;
        }

        private void SetBusy(bool busy)
        {
            StartButton.IsEnabled = !busy;
            CancelButton.IsEnabled = busy;
            AddButton.IsEnabled = !busy;
            RemoveButton.IsEnabled = !busy;
            BrowseButton.IsEnabled = !busy;
            DestinationBox.IsEnabled = !busy;
            DetectButton.IsEnabled = !busy;
        }

        private void Alert(string text)
        {
            System.Windows.MessageBox.Show(this, text, "Vackup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
