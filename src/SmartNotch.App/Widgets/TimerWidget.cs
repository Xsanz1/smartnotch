using System.Windows;
using System.Windows.Controls;
using SmartNotch.App.Services;

namespace SmartNotch.App.Widgets;

public sealed class TimerWidget : INotchWidget
{
    private readonly PomodoroService _p;
    private readonly TextBlock _time = Ui.T("25:00", 48, true);
    private readonly Button _go;

    public string Title => "Timer";
    public FrameworkElement View { get; }

    public TimerWidget(PomodoroService p)
    {
        _p = p;
        _time.HorizontalAlignment = HorizontalAlignment.Center;

        var presets = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var m in new[] { 5, 15, 25, 50 })
        {
            var min = m;
            presets.Children.Add(Ui.Btn($"{m} mnt", () => _p.SetMinutes(min), 12, "#1AFFFFFF"));
            ((Button)presets.Children[^1]).Margin = new Thickness(3, 0, 3, 0);
        }

        _go = Ui.Btn("▶  Mulai", _p.Toggle, 14, "#FF7C5CFF");
        _go.Padding = new Thickness(22, 6, 22, 6);
        var reset = Ui.Btn("↺", _p.Reset, 16, "#26FFFFFF");
        reset.Padding = new Thickness(14, 6, 14, 6);
        var ctrl = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        ctrl.Children.Add(_go);
        ctrl.Children.Add(reset);
        reset.Margin = new Thickness(8, 0, 0, 0);

        View = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _time, presets, ctrl } };
        p.Tick += Update;
        Update();
    }

    public void OnShown() => Update();

    private void Update()
    {
        _time.Text = PomodoroService.Format(_p.Remaining);
        _go.Content = _p.Running ? "⏸  Jeda" : "▶  Mulai";
    }
}
