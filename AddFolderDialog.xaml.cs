using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Vackup
{
    public partial class AddFolderDialog : Window
    {
        public List<string> Sources { get; private set; } = new List<string>();
        public string Destination { get; private set; } = "";

        public AddFolderDialog(string destination)
        {
            InitializeComponent();
            DestinationBox.Text = destination ?? "";
            Loaded += (s, e) => SourceBox.Focus();
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var path = FolderPicker.Pick("Select a folder to back up");
            if (path != null)
            {
                SourceBox.Text = (SourceBox.Text.TrimEnd() + Environment.NewLine + path).Trim();
            }
        }

        private void BrowseDestination_Click(object sender, RoutedEventArgs e)
        {
            var path = FolderPicker.Pick("Select the backup destination");
            if (path != null)
            {
                DestinationBox.Text = path;
            }
        }

        // Full path without a trailing separator (except for a drive root), so duplicates compare equal.
        private static string NormalizePath(string path)
        {
            try
            {
                var full = Path.GetFullPath(path);
                return Path.GetPathRoot(full) == full ? full : full.TrimEnd('\\');
            }
            catch (Exception)
            {
                return path;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var lines = SourceBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().Trim('"'))
                .Where(l => l.Length > 0)
                .ToList();
            lines = lines.Select(NormalizePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (lines.Count == 0)
            {
                System.Windows.MessageBox.Show(this, "Paste at least one source folder.", "Vackup");
                return;
            }
            var missing = lines.FirstOrDefault(l => !Directory.Exists(l));
            if (missing != null)
            {
                System.Windows.MessageBox.Show(this, "Folder not found:\n" + missing, "Vackup");
                return;
            }
            Sources = lines;
            Destination = DestinationBox.Text.Trim().Trim('"');
            DialogResult = true;
        }
    }
}
