using System.Windows;

namespace HesabdariAsan.Native.Services;

public static class FontScaleService
{
    public static void Apply(double scale)
    {
        if (Application.Current is null) return;
        scale = Math.Clamp(scale, 1.0, 2.0);
        var r = Application.Current.Resources;
        r["BaseFontSize"] = 14d * scale;
        r["SmallFontSize"] = 12d * scale;
        r["NavFontSize"] = 15d * scale;
        r["TitleFontSize"] = 24d * scale;
        r["StatValueFontSize"] = 23d * scale;
    }

    public static void ApplySavedScale()
    {
        var settings = new SettingsService().Load();
        Apply(settings.UiScale);
    }
}
