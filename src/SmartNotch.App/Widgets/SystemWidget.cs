using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using SmartNotch.App.Services;

namespace SmartNotch.App.Widgets;

public sealed class SystemWidget : INotchWidget
{
    private readonly StatsService _s;
    private readonly BatteryService _b;
    private readonly TextBlock _cpu = Ui.T("0%", 26, true);
    private readonly TextBlock _ram = Ui.T("0%", 26, true);
    private readonly TextBlock _net = Ui.T("0 KB/s", 20, true);
    private readonly TextBlock _netSub = Ui.T("↓ 0   ↑ 0", 11, false, Ui.Dim);
    private readonly Polyline _cpuLine = new() { Stroke = Ui.Accent, StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round };
    private readonly Polyline _netLine = new() { Stroke = Ui.B("#FF3DDC97"), StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round };
    private readonly Border _batFill = new() { Background = Ui.B("#FF3DDC97"), CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _batText = Ui.T("", 12, false, Ui.Dim);
    private readonly ProgressStrip _ramBar = new();
    private bool _visible;

    public string Title => "Sistem";
    public FrameworkElement View { get; }

    private const double SparkW = 150, SparkH = 30;

    public SystemWidget(StatsService s, BatteryService b)
    {
        _s = s; _b = b;

        Border Card(string label, UIElement top, UIElement bottom) => new()
        {
            Background = Ui.B("#14FFFFFF"), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(4, 0, 4, 0),
            Child = new StackPanel { Children = { Ui.T(label, 11, false, Ui.Dim), top, bottom } }
        };

        var cpuSpark = new Canvas { Width = SparkW, Height = SparkH, Margin = new Thickness(0, 6, 0, 0), Children = { _cpuLine } };
        var netSpark = new Canvas { Width = SparkW, Height = SparkH, Margin = new Thickness(0, 6, 0, 0), Children = { _netLine } };

        var ramStack = new StackPanel { Children = { _ramBar } };
        _ramBar.Margin = new Thickness(0, 14, 0, 0);

        var cards = new UniformGrid { Columns = 3, Height = 108, VerticalAlignment = VerticalAlignment.Top };
        cards.Children.Add(Card("CPU", _cpu, cpuSpark));
        cards.Children.Add(Card("RAM", _ram, ramStack));
        var netCard = Card("JARINGAN", _net, netSpark);
        ((StackPanel)netCard.Child).Children.Insert(2, _netSub);
        cards.Children.Add(netCard);

        var batTrack = new Border { Background = Ui.B("#26FFFFFF"), CornerRadius = new CornerRadius(3), Height = 6, Child = _batFill, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        var batRow = new DockPanel { Margin = new Thickness(4, 10, 4, 0) };
        DockPanel.SetDock(_batText, Dock.Right);
        batRow.Children.Add(Ui.T("Baterai", 12, false, Ui.Dim));
        batRow.Children.Add(_batText);
        batRow.Children.Add(batTrack);

        View = new StackPanel { Children = { cards, batRow } };
        batTrack.SizeChanged += (_, _) => UpdateBattery();

        s.Updated += () => { if (_visible) Update(); };
    }

    public void OnShown() { _visible = true; Update(); UpdateBattery(); }
    public void OnHidden() => _visible = false;

    private void Update()
    {
        _cpu.Text = $"{_s.Cpu:0}%";
        _ram.Text = $"{_s.Ram:0}%";
        _ramBar.Set(_s.Ram / 100.0);
        _net.Text = Ui.Bytes(_s.DownKBs + _s.UpKBs);
        _netSub.Text = $"↓ {Ui.Bytes(_s.DownKBs)}   ↑ {Ui.Bytes(_s.UpKBs)}";
        Spark(_cpuLine, _s.CpuHist, 100);
        Spark(_netLine, _s.NetHist, Math.Max(100, _s.NetHist.DefaultIfEmpty(0).Max()));
        UpdateBattery();
    }

    private void UpdateBattery()
    {
        if (!_b.HasBattery) { _batText.Text = "Tidak ada baterai"; _batFill.Width = 0; return; }
        _batText.Text = $"{_b.Percent}%{(_b.Plugged ? "  ⚡ Mengisi" : "")}";
        var track = (_batFill.Parent as Border)?.ActualWidth ?? 0;
        _batFill.Width = track * _b.Percent / 100.0;
        _batFill.Background = Ui.B(_b.Percent <= 20 && !_b.Plugged ? "#FFFF5D5D" : "#FF3DDC97");
    }

    private static void Spark(Polyline line, List<double> data, double max)
    {
        var pts = new PointCollection();
        for (int i = 0; i < data.Count; i++)
        {
            double x = data.Count <= 1 ? 0 : i * SparkW / 39.0;
            double y = SparkH - Math.Clamp(data[i] / max, 0, 1) * SparkH;
            pts.Add(new Point(x, y));
        }
        line.Points = pts;
    }

    private sealed class ProgressStrip : Grid
    {
        private readonly ColumnDefinition _a = new() { Width = new GridLength(0.0001, GridUnitType.Star) };
        private readonly ColumnDefinition _c = new() { Width = new GridLength(1, GridUnitType.Star) };
        public ProgressStrip()
        {
            Height = 6;
            ColumnDefinitions.Add(_a); ColumnDefinitions.Add(_c);
            var rest = new Border { Background = Ui.B("#26FFFFFF"), CornerRadius = new CornerRadius(3) };
            Grid.SetColumn(rest, 1);
            Children.Add(rest);
            Children.Add(new Border { Background = Ui.Accent, CornerRadius = new CornerRadius(3) });
        }
        public void Set(double r)
        {
            r = Math.Clamp(r, 0.0001, 0.9999);
            _a.Width = new GridLength(r, GridUnitType.Star);
            _c.Width = new GridLength(1 - r, GridUnitType.Star);
        }
    }
}
