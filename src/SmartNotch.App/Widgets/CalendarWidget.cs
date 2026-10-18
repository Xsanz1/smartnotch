using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SmartNotch.App.Services;

namespace SmartNotch.App.Widgets;

public sealed class CalendarWidget : INotchWidget
{
    private readonly CalendarService _c;
    private readonly Grid _root = new();
    private static readonly CultureInfo Id = new("id-ID");

    public string Title => "Agenda";
    public FrameworkElement View => _root;

    public CalendarWidget(CalendarService c)
    {
        _c = c;
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _root.ColumnDefinitions.Add(new ColumnDefinition());
    }

    public void OnShown() => Build();

    private void Build()
    {
        _root.Children.Clear();
        var events = _c.Load();
        var today = DateTime.Today;
        var first = new DateTime(today.Year, today.Month, 1);
        int offset = ((int)first.DayOfWeek + 6) % 7; // Senin = 0

        var grid = new UniformGrid { Columns = 7, Width = 196 };
        foreach (var h in new[] { "S", "S", "R", "K", "J", "S", "M" })
            grid.Children.Add(Cell(h, false, false, true));
        for (int i = 0; i < offset; i++) grid.Children.Add(new Border());
        for (int d = 1; d <= DateTime.DaysInMonth(today.Year, today.Month); d++)
        {
            var date = new DateTime(today.Year, today.Month, d);
            grid.Children.Add(Cell(d.ToString(), date == today, events.Any(e => e.Start.Date == date), false));
        }

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(Ui.T(today.ToString("MMMM yyyy", Id), 13, true));
        left.Children.Add(grid);

        var right = new StackPanel { Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(Ui.T("Berikutnya", 11, false, Ui.Dim));
        var upcoming = events.Where(e => e.Start >= DateTime.Now).Take(3).ToList();
        if (upcoming.Count == 0) right.Children.Add(Ui.T("Tidak ada acara", 14, false, Ui.Dim));
        foreach (var e in upcoming)
        {
            var row = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            row.Children.Add(Ui.T(e.Title, 14, true));
            row.Children.Add(Ui.T(e.Start.ToString("dddd, d MMM • HH:mm", Id), 11, false, Ui.Dim));
            right.Children.Add(row);
        }
        var open = Ui.Btn("Edit agenda", () =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(CalendarService.FilePath) { UseShellExecute = true }); } catch { }
        }, 11, "#22FFFFFF");
        open.HorizontalAlignment = HorizontalAlignment.Left;
        open.Margin = new Thickness(0, 10, 0, 0);
        right.Children.Add(open);

        _root.Children.Add(left);
        Grid.SetColumn(right, 1);
        _root.Children.Add(right);
    }

    private static UIElement Cell(string text, bool today, bool hasEvent, bool header)
    {
        var tb = Ui.T(text, header ? 9 : 11, today, header ? Ui.Dim : (hasEvent ? Ui.B("#FF9B87FF") : Brushes.White));
        tb.HorizontalAlignment = HorizontalAlignment.Center;
        tb.VerticalAlignment = VerticalAlignment.Center;
        if (hasEvent && !today) tb.FontWeight = FontWeights.Bold;
        return new Border
        {
            Width = 24, Height = 17, CornerRadius = new CornerRadius(8),
            Background = today ? Ui.Accent : Brushes.Transparent,
            Child = tb
        };
    }
}
