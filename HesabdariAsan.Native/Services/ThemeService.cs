using System.Windows;

namespace HesabdariAsan.Native.Services;

public static class ThemeService
{
    public static void ApplySavedTheme()
    {
        var theme = new SettingsService().Get("theme", "Light");
        Apply(theme);
    }

    public static void Apply(string theme)
    {
        if (Application.Current is null) return;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var old = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) == true
                                                || d.Source?.OriginalString.Contains("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary
        {
            Source = new Uri(theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };
        if (old is not null)
        {
            var index = dictionaries.IndexOf(old);
            dictionaries[index] = replacement;
        }
        else
        {
            dictionaries.Insert(0, replacement);
        }
    }
}
