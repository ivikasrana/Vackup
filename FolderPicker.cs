using System.Windows.Forms;

namespace Vackup
{
    public static class FolderPicker
    {
        public static string Pick(string description)
        {
            using (var dialog = new FolderBrowserDialog { Description = description, ShowNewFolderButton = true })
            {
                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }
    }
}
