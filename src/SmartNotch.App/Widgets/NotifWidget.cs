using System.Windows;
using System.Windows.Controls;
using SmartNotch.App.Services;

namespace SmartNotch.App.Widgets;

public sealed class NotifWidget : INotchWidget
{
    private readonly NotificationService _n;
    private readonly StackPanel _list = new();
    private readonly TextBlock _status = Ui.T("", 12, false, Ui.Dim, wrap: true);

    public string Title => "Notif";
    public FrameworkElement View { get; }

    public NotifWidget(NotificationService n)
    {
        _n = n;
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = _list };
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition());
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.Children.Add(scroll);
        Grid.SetRow(_status, 1);
        g.Children.Add(_status);
        View = g;
        n.Changed += Build;
        Build();
    }

    public void OnShown() => Build();

    private void Build()
    {
        _list.Children.Clear();
        _status.Text = "Status: " + _n.Status;
        if (_n.Items.Count == 0)
        {
            _list.Children.Add(Ui.T("Belum ada notifikasi baru", 13, false, Ui.Dim));
            return;
        }
        foreach (var i in _n.Items)
        {
            var row = new Border
            {
                Background = Ui.B("#14FFFFFF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 4),
                Child = new StackPanel
                {
                    Children =
                    {
                        Ui.T($"{i.App} • {i.Time:HH:mm}", 10, false, Ui.Dim),
                        Ui.T(i.Title, 13, true),
                        Ui.T(i.Body, 11, false, Ui.Dim)
                    }
                }
            };
            _list.Children.Add(row);
        }
    }
}
