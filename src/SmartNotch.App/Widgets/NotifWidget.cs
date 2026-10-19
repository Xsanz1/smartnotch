using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartNotch.App.Services;

namespace SmartNotch.App.Widgets;

public sealed class NotifWidget : INotchWidget
{
    private readonly NotificationService _n;
    private readonly StackPanel _list = new();

    public string Title => "Pesan";
    public FrameworkElement View { get; }

    public NotifWidget(NotificationService n)
    {
        _n = n;

        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = Ui.T("💬 Pesan & Notifikasi Masuk", 14, true, Brushes.White);
        var clearBtn = Ui.Btn("Bersihkan", () =>
        {
            _n.Items.Clear();
            Build();
        }, 11, "#18FFFFFF");
        clearBtn.Padding = new Thickness(10, 2, 10, 2);

        header.Children.Add(title);
        Grid.SetColumn(clearBtn, 1);
        header.Children.Add(clearBtn);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 115,
            Content = _list
        };

        var root = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { header, scroll }
        };

        View = root;
        _n.Changed += Build;
        Build();
    }

    public void OnShown() => Build();

    private void Build()
    {
        _list.Children.Clear();

        if (_n.Items.Count == 0)
        {
            var empty = new Border
            {
                Background = Ui.B("#0DFFFFFF"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 18, 16, 18),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        Ui.T("📭 Belum ada pesan atau notifikasi baru", 13, false, Ui.Dim, HorizontalAlignment.Center),
                        Ui.T("Pesan WhatsApp masuk akan tampil otomatis di sini.", 11, false, Ui.B("#55FFFFFF"), HorizontalAlignment.Center)
                    }
                }
            };
            _list.Children.Add(empty);
            return;
        }

        foreach (var i in _n.Items)
        {
            bool isWa = i.App.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
            var icon = isWa ? "💬" : "🔔";
            var badgeBg = isWa ? Ui.B("#2525D366") : Ui.B("#1FFFFFFF");
            var badgeFg = isWa ? Ui.B("#FF25D366") : Brushes.White;

            var card = new Border
            {
                Background = Ui.B("#16FFFFFF"),
                BorderBrush = Ui.B("#1AFFFFFF"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"Klik untuk membuka {i.App}"
            };

            card.MouseEnter += (_, _) =>
            {
                card.Background = Ui.B("#28FFFFFF");
                card.BorderBrush = Ui.B("#3AFFFFFF");
            };
            card.MouseLeave += (_, _) =>
            {
                card.Background = Ui.B("#16FFFFFF");
                card.BorderBrush = Ui.B("#1AFFFFFF");
            };
            card.MouseLeftButtonDown += (_, e) =>
            {
                AppLauncherService.FocusOrLaunch(i);
                e.Handled = true;
            };

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var appLabel = new StackPanel { Orientation = Orientation.Horizontal };
            var badge = new Border
            {
                Background = badgeBg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 6, 0),
                Child = Ui.T($"{icon} {i.App}", 10, true, badgeFg)
            };
            appLabel.Children.Add(badge);
            appLabel.Children.Add(Ui.T(i.Title, 12, true, Brushes.White));

            var timeBox = new StackPanel { Orientation = Orientation.Horizontal };
            timeBox.Children.Add(Ui.T(i.Time.ToString("HH:mm"), 10, false, Ui.Dim));

            var replyToggle = Ui.T(" ↩ Balas", 10, true, Ui.B("#90FFFFFF"));
            replyToggle.Margin = new Thickness(6, 0, 0, 0);
            replyToggle.Cursor = System.Windows.Input.Cursors.Hand;
            timeBox.Children.Add(replyToggle);

            timeBox.Children.Add(Ui.T(" ↗", 10, true, Ui.B("#70FFFFFF")));

            topRow.Children.Add(appLabel);
            Grid.SetColumn(timeBox, 1);
            topRow.Children.Add(timeBox);

            var content = new StackPanel { Children = { topRow } };

            if (!string.IsNullOrWhiteSpace(i.Body))
            {
                var bodyText = Ui.T(i.Body, 11, false, Ui.B("#D0FFFFFF"), wrap: true);
                bodyText.Margin = new Thickness(0, 4, 0, 0);
                bodyText.MaxHeight = 32;
                content.Children.Add(bodyText);
            }

            // Input Balas Cepat (Quick Reply)
            var replyRow = new Grid { Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
            replyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            replyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var replyInput = new TextBox
            {
                FontSize = 11,
                Foreground = Brushes.White,
                Background = Ui.B("#25FFFFFF"),
                BorderBrush = Ui.B("#3AFFFFFF"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                CaretBrush = Brushes.White
            };
            replyInput.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;

            var sendBtn = new Button
            {
                Content = "➤ Kirim",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = isWa ? Ui.B("#FF25D366") : Ui.B("#FF7C5CFF"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            void DoSend()
            {
                string text = replyInput.Text.Trim();
                if (string.IsNullOrEmpty(text)) return;
                try { Clipboard.SetText(text); } catch { }
                AppLauncherService.FocusOrLaunch(i);
                replyInput.Text = "";
                replyRow.Visibility = Visibility.Collapsed;
            }

            sendBtn.Click += (_, e) => { e.Handled = true; DoSend(); };
            replyInput.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    e.Handled = true;
                    DoSend();
                }
            };

            replyToggle.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                bool show = replyRow.Visibility != Visibility.Visible;
                replyRow.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (show) replyInput.Focus();
            };

            replyRow.Children.Add(replyInput);
            Grid.SetColumn(sendBtn, 1);
            replyRow.Children.Add(sendBtn);
            content.Children.Add(replyRow);

            card.Child = content;
            _list.Children.Add(card);
        }
    }
}
