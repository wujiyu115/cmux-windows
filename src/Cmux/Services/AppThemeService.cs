using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Cmux.Core.Config;
using Cmux.Core.Terminal;

namespace Cmux.Services;

public static class AppThemeService
{
    private static ResourceDictionary? _currentColorDict;

    public static void ApplyTheme(TerminalTheme theme)
    {
        var newDict = BuildColorDictionary(theme);
        var merged = Application.Current.Resources.MergedDictionaries;
        if (_currentColorDict != null)
            merged.Remove(_currentColorDict);
        merged.Add(newDict);
        _currentColorDict = newDict;
    }

    private static ResourceDictionary BuildColorDictionary(TerminalTheme theme)
    {
        var bg = ToColor(theme.Background);
        var fg = ToColor(theme.Foreground);
        var accent = PickAccent(theme);
        var isLight = Luminance(bg) > 128;

        var sidebarBg = Darken(bg, 0.03);
        var hoverBg = Blend(bg, accent, 0.12);
        var selectedBg = Blend(bg, accent, 0.20);
        var fgDim = Blend(fg, bg, 0.55);
        var border = isLight ? Darken(bg, 0.12) : Lighten(bg, 0.10);
        var tabBg = isLight ? Darken(bg, 0.04) : Lighten(bg, 0.04);
        var tabSelected = isLight ? Darken(bg, 0.08) : Lighten(bg, 0.08);
        var divider = isLight ? Darken(bg, 0.14) : Lighten(bg, 0.12);
        var surface = isLight ? Darken(bg, 0.04) : Lighten(bg, 0.03);
        var surfaceHigh = isLight ? Darken(bg, 0.08) : Lighten(bg, 0.07);
        var inputBg = isLight ? Darken(bg, 0.06) : Lighten(bg, 0.05);
        var notification = Darken(accent, 0.05);
        var hoverOverlay = isLight ? Color.FromArgb(0x20, 0, 0, 0) : Color.FromArgb(0x20, 255, 255, 255);
        var pressedOverlay = isLight ? Color.FromArgb(0x30, 0, 0, 0) : Color.FromArgb(0x30, 255, 255, 255);
        var scrollThumb = isLight ? Color.FromArgb(0x40, 0, 0, 0) : Color.FromArgb(0x30, 255, 255, 255);
        var scrollThumbHover = isLight ? Color.FromArgb(0x60, 0, 0, 0) : Color.FromArgb(0x60, 255, 255, 255);
        var accentGlow = Color.FromArgb(0x80, accent.R, accent.G, accent.B);
        var overlayBg = Color.FromArgb(0xF2, bg.R, bg.G, bg.B);

        var errorColor = ToColor(theme.Palette[1]);
        var successColor = ToColor(theme.Palette[2]);
        var warningColor = ToColor(theme.Palette[3]);
        var purpleAccent = ToColor(theme.Palette[5]);
        var tealAccent = ToColor(theme.Palette[6]);
        var pinkAccent = Lighten(ToColor(theme.Palette[5]), 0.15);
        var orangeAccent = ToColor(theme.Palette.Length > 9 ? theme.Palette[9] : theme.Palette[3]);

        var dict = new ResourceDictionary();

        void AddColor(string key, Color c) => dict[key] = c;
        void AddBrush(string key, Brush b) { b.Freeze(); dict[key] = b; }

        AddColor("BackgroundColor", bg);
        AddColor("SidebarBackgroundColor", sidebarBg);
        AddColor("SidebarItemHoverColor", hoverBg);
        AddColor("SidebarItemSelectedColor", selectedBg);
        AddColor("ForegroundColor", fg);
        AddColor("ForegroundDimColor", fgDim);
        AddColor("AccentColor", accent);
        AddColor("NotificationColor", notification);
        AddColor("BorderColor", border);
        AddColor("SurfaceTabBackgroundColor", tabBg);
        AddColor("SurfaceTabSelectedColor", tabSelected);
        AddColor("DividerColor", divider);
        AddColor("ErrorColor", errorColor);
        AddColor("SuccessColor", successColor);
        AddColor("WarningColor", warningColor);
        AddColor("AccentGlowColor", accentGlow);
        AddColor("SurfaceColor", surface);
        AddColor("SurfaceHighColor", surfaceHigh);
        AddColor("PurpleAccent", purpleAccent);
        AddColor("TealAccent", tealAccent);
        AddColor("PinkAccent", pinkAccent);
        AddColor("OrangeAccent", orangeAccent);
        AddColor("InputBackgroundColor", inputBg);
        AddColor("CloseButtonHoverColor", Color.FromRgb(0xE8, 0x11, 0x23));

        AddBrush("BackgroundBrush", new SolidColorBrush(bg));
        AddBrush("SidebarBackgroundBrush", new SolidColorBrush(sidebarBg));
        AddBrush("SidebarItemHoverBrush", new SolidColorBrush(hoverBg));
        AddBrush("SidebarItemSelectedBrush", new SolidColorBrush(selectedBg));
        AddBrush("ForegroundBrush", new SolidColorBrush(fg));
        AddBrush("ForegroundDimBrush", new SolidColorBrush(fgDim));
        AddBrush("AccentBrush", new SolidColorBrush(accent));
        AddBrush("NotificationBrush", new SolidColorBrush(notification));
        AddBrush("BorderBrush", new SolidColorBrush(border));
        AddBrush("SurfaceTabBackgroundBrush", new SolidColorBrush(tabBg));
        AddBrush("SurfaceTabSelectedBrush", new SolidColorBrush(tabSelected));
        AddBrush("DividerBrush", new SolidColorBrush(divider));
        AddBrush("ErrorBrush", new SolidColorBrush(errorColor));
        AddBrush("SuccessBrush", new SolidColorBrush(successColor));
        AddBrush("WarningBrush", new SolidColorBrush(warningColor));
        AddBrush("SurfaceBrush", new SolidColorBrush(surface));
        AddBrush("SurfaceHighBrush", new SolidColorBrush(surfaceHigh));
        AddBrush("InputBackgroundBrush", new SolidColorBrush(inputBg));
        AddBrush("OverlayBackgroundBrush", new SolidColorBrush(overlayBg));
        AddBrush("HoverOverlayBrush", new SolidColorBrush(hoverOverlay));
        AddBrush("PressedOverlayBrush", new SolidColorBrush(pressedOverlay));
        AddBrush("ScrollThumbBrush", new SolidColorBrush(scrollThumb));
        AddBrush("ScrollThumbHoverBrush", new SolidColorBrush(scrollThumbHover));

        var accentGradient = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Darken(accent, 0.08), 0.0),
                new(accent, 0.5),
                new(Lighten(accent, 0.12), 1.0),
            }, new Point(0, 0), new Point(1, 1));
        AddBrush("AccentGradientBrush", accentGradient);

        var sidebarGradient = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(sidebarBg, 0.0),
                new(Blend(sidebarBg, accent, 0.04), 1.0),
            }, new Point(0, 0), new Point(0, 1));
        AddBrush("SidebarGradientBrush", sidebarGradient);

        var tabGlow = new RadialGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(0x30, accent.R, accent.G, accent.B), 0.0),
                new(Color.FromArgb(0x00, accent.R, accent.G, accent.B), 1.0),
            })
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.8,
            RadiusY = 0.8,
        };
        AddBrush("SelectedTabGlow", tabGlow);

        var glowEffect = new DropShadowEffect
        {
            BlurRadius = 16, ShadowDepth = 0, Direction = 0,
            Color = accent, Opacity = 0.5,
        };
        glowEffect.Freeze();
        dict["GlowEffect"] = glowEffect;

        // SystemColors overrides for native WPF controls (Menu, ComboBox, ListBox)
        void AddSysBrush(ResourceKey key, Color c) { var b = new SolidColorBrush(c); b.Freeze(); dict[key] = b; }
        AddSysBrush(SystemColors.MenuBrushKey, surface);
        AddSysBrush(SystemColors.MenuTextBrushKey, fg);
        AddSysBrush(SystemColors.HighlightBrushKey, selectedBg);
        AddSysBrush(SystemColors.HighlightTextBrushKey, fg);
        AddSysBrush(SystemColors.ControlBrushKey, surface);
        AddSysBrush(SystemColors.WindowBrushKey, surface);
        AddSysBrush(SystemColors.MenuBarBrushKey, surface);
        AddSysBrush(SystemColors.MenuHighlightBrushKey, selectedBg);
        AddSysBrush(SystemColors.ControlLightBrushKey, border);
        AddSysBrush(SystemColors.ControlLightLightBrushKey, border);
        AddSysBrush(SystemColors.ControlDarkBrushKey, border);
        AddSysBrush(SystemColors.ControlDarkDarkBrushKey, border);
        AddSysBrush(SystemColors.WindowTextBrushKey, fg);
        AddSysBrush(SystemColors.ControlTextBrushKey, fg);

        return dict;
    }

    private static Color PickAccent(TerminalTheme theme)
    {
        var cursor = ToColor(theme.CursorColor);
        var fg = ToColor(theme.Foreground);
        if (ColorDistance(cursor, fg) < 30 && theme.Palette.Length > 4)
            return ToColor(theme.Palette[4]);
        return cursor;
    }

    private static Color ToColor(TerminalColor c) => Color.FromRgb(c.R, c.G, c.B);

    private static Color Darken(Color c, double amount)
    {
        var factor = 1.0 - amount;
        return Color.FromRgb(
            (byte)(c.R * factor),
            (byte)(c.G * factor),
            (byte)(c.B * factor));
    }

    private static Color Lighten(Color c, double amount)
    {
        return Color.FromRgb(
            (byte)Math.Min(255, c.R + (255 - c.R) * amount),
            (byte)Math.Min(255, c.G + (255 - c.G) * amount),
            (byte)Math.Min(255, c.B + (255 - c.B) * amount));
    }

    private static Color Blend(Color a, Color b, double t)
    {
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static double ColorDistance(Color a, Color b)
    {
        var dr = (double)a.R - b.R;
        var dg = (double)a.G - b.G;
        var db = (double)a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static double Luminance(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
}
