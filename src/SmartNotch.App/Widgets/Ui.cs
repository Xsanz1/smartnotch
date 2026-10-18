using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartNotch.App.Widgets;

public interface INotchWidget
{
    string Title { get; }
    FrameworkElement View { get; }
    void OnShown();
}

internal static class Ui
{
    public static Brush B(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    public static readonly Brush Dim = B("#99FFFFFF");
    public static readonly Brush Accent = B("#FF7C5CFF");

    public static TextBlock T(string s, double size = 13, bool bold = false, Brush? fg = null,
        HorizontalAlignment ha = HorizontalAlignment.Stretch, VerticalAlignment va = VerticalAlignment.Stretch, bool wrap = false) => new()
    {
        Text = s,
        HorizontalAlignment = ha,
        VerticalAlignment = va,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = fg ?? Brushes.White,
        FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    public static Button Btn(string content, Action click, double size = 16, string? bg = null)
    {
        var b = new Button
        {
            Content = content,
            FontSize = size,
            Style = (Style)Application.Current.FindResource("NotchButton"),
        };
        if (bg != null) b.Background = B(bg);
        b.Click += (_, _) => click();
        return b;
    }

    public static string Bytes(double kbs) => kbs >= 1024 ? $"{kbs / 1024:0.0} MB/s" : $"{kbs:0} KB/s";
}
