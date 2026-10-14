using System;
using System.Windows;
using Microsoft.Win32;

namespace Vackup
{
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; }

        public static event Action Changed;

        public static bool Resolve(string saved)
        {
            if (saved == "dark")
            {
                return true;
            }
            if (saved == "light")
            {
                return false;
            }
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            var dict = new ResourceDictionary { Source = new Uri("Themes/" + (dark ? "Dark" : "Light") + ".xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries[0] = dict;
            Changed?.Invoke();
        }
    }
}
