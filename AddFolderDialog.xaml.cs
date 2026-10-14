using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

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

        private static string Pick(string description)
        {
            using (var dialog = new FolderBrowserDialog { Description = description, ShowNewFolderButton = true })
            {
                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var path = Pick("Select a folder to back up");
            if (path != null)
            {
                SourceBox.Text = (SourceBox.Text.TrimEnd() + Environment.NewLine + path).Trim();
            }
        }

        private void BrowseDestination_Click(object sender, RoutedEventArgs e)
        {
            var path = Pick("Select the backup destination");
            if (path != null)
            {
                DestinationBox.Text = path;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var lines = SourceBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().Trim('"'))
                .Where(l => l.Length > 0)
                .ToList();
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
