using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartNotch.App.Services;

namespace SmartNotch.App.Widgets;

public sealed class WaterWidget : INotchWidget
{
    private readonly WaterReminderService _w;
    private readonly TextBlock _amount = Ui.T("0 / 2000 ml", 22, true);
    private readonly TextBlock _pct = Ui.T("0%", 12, false, Ui.Dim);
    private readonly Border _fill = new() { Background = Ui.B("#FF29B6F6"), CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left };

    public string Title => "Minum";
    public FrameworkElement View { get; }

    public WaterWidget(WaterReminderService w)
    {
        _w = w;

        var bar = new Border
        {
            Height = 8,
            Background = Ui.B("#22FFFFFF"),
            CornerRadius = new CornerRadius(4),
            Child = _fill,
            Margin = new Thickness(0, 8, 0, 10),
            Width = 260,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var b250 = Ui.Btn("+250 ml", () => _w.Drink(250), 12, "#2029B6F6");
        var b500 = Ui.Btn("+500 ml", () => _w.Drink(500), 12, "#2029B6F6");
        var bReset = Ui.Btn("↺", _w.Reset, 12, "#20FFFFFF");
        b250.Margin = new Thickness(4, 0, 4, 0);
        b500.Margin = new Thickness(4, 0, 4, 0);
        bReset.Margin = new Thickness(4, 0, 4, 0);
        btns.Children.Add(b250);
        btns.Children.Add(b500);
        btns.Children.Add(bReset);

        var top = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                Ui.T("💧 Pengingat Minum Air", 11, false, Ui.Dim, HorizontalAlignment.Center),
                _amount,
                _pct
            }
        };
        _amount.HorizontalAlignment = HorizontalAlignment.Center;
        _pct.HorizontalAlignment = HorizontalAlignment.Center;

        var root = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { top, bar, btns }
        };

        View = root;
        _w.Changed += Update;
        Update();
    }

    public void OnShown() => Update();

    private void Update()
    {
        _amount.Text = $"{_w.IntakeMl} / {_w.GoalMl} ml";
        double ratio = Math.Clamp((double)_w.IntakeMl / _w.GoalMl, 0, 1);
        _pct.Text = $"{(int)(ratio * 100)}% dari target harian";
        _fill.Width = 260 * ratio;
    }
}
