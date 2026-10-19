using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartNotch.App.Services;

namespace SmartNotch.App;

/// <summary>Jendela pengaturan (dibangun lewat kode, tema gelap). Menyimpan ke settings.json dan menerapkan langsung.</summary>
public sealed class SettingsWindow : Window
{
    private static SettingsWindow? _instance;

    private static readonly Brush Bg = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
    private static readonly Brush Card = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2D));
    private static readonly Brush Dim = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA8));
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0x5C, 0xFF));

    private readonly AppSettings _s;
    private bool _autostart;

    private readonly CheckBox _music = Chk("Musik & Lirik"), _call = Chk("Panggilan"), _shelf = Chk("File Shelf"),
        _notif = Chk("Pesan & Notifikasi"), _timer = Chk("Timer Fokus"), _water = Chk("Pengingat Minum"), _system = Chk("Pemantau Sistem");
    private readonly TextBox _pomo = Box(), _waterInterval = Box(), _waterGoal = Box(), _collapse = Box(), _hotkey = Box(), _apps = Box(360), _opacityBox = Box(60);
    private readonly CheckBox _timerSound = Chk("Bunyi saat timer selesai"), _hideFs = Chk("Sembunyikan saat aplikasi fullscreen"),
        _notifPeek = Chk("Tampilkan notifikasi Windows di notch"), _notifSound = Chk("Bunyi bel saat ada pesan / notifikasi masuk"),
        _autoStartChk = Chk("Mulai bersama Windows"), _updates = Chk("Periksa pembaruan saat aplikasi dibuka");
    private readonly List<RadioButton> _monitors = new();

    public static void ShowSingle()
    {
        if (_instance != null) { _instance.Activate(); return; }
        _instance = new SettingsWindow();
        _instance.Closed += (_, _) => _instance = null;
        _instance.Show();
        _instance.Activate();
    }

    private SettingsWindow()
    {
        // Edit salinan; baru diterapkan saat "Simpan"
        _s = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(SettingsService.Current)) ?? new AppSettings();

        Title = "Pengaturan SmartNotch";
        Width = 480; Height = 680;
        MinWidth = 420; MinHeight = 420;
        Background = Bg;
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var panel = new StackPanel { Margin = new Thickness(18, 14, 18, 8) };
        panel.Children.Add(new TextBlock { Text = "Pengaturan", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });

        panel.Children.Add(Section("Widget di panel", Wrap(_music, _call, _shelf, _notif, _timer, _water, _system)));
        panel.Children.Add(Section("Timer fokus",
            Row("Durasi default (menit)", _pomo), _timerSound));
        panel.Children.Add(Section("Pengingat minum",
            Row("Interval pengingat (menit)", _waterInterval), Row("Target harian (ml)", _waterGoal)));
        panel.Children.Add(Section("Monitor", BuildMonitorList()));
        panel.Children.Add(Section("Perilaku & Tampilan",
            _hideFs, _notifPeek, _notifSound,
            Row("Opasitas notch (%) [70 - 100]", _opacityBox),
            Row("Jeda menutup (ms)", _collapse),
            Row("Hotkey: Ctrl+Alt+", _hotkey)));
        panel.Children.Add(Section("Deteksi panggilan",
            Hint("Nama aplikasi dipisah koma (cocok dengan nama proses). Browser tidak disertakan agar tidak salah deteksi."),
            _apps));
        panel.Children.Add(Section("Umum", _autoStartChk, _updates,
            Buttons(("Periksa pembaruan sekarang", CheckUpdateNow), ("Buka folder log", OpenLogs))));

        Content = new DockPanel
        {
            Children =
            {
                Docked(BuildFooter(), System.Windows.Controls.Dock.Bottom),
                new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel }
            }
        };

        Load();
    }

    // ----- Binding sederhana -----
    private void Load()
    {
        _music.IsChecked = _s.ShowMusic; _call.IsChecked = _s.ShowCall; _shelf.IsChecked = _s.ShowShelf;
        _notif.IsChecked = _s.ShowNotif;
        _timer.IsChecked = _s.ShowTimer; _water.IsChecked = _s.ShowWater; _system.IsChecked = _s.ShowSystem;
        _pomo.Text = _s.PomodoroMinutes.ToString();
        _timerSound.IsChecked = _s.TimerSound;
        _waterInterval.Text = _s.WaterIntervalMinutes.ToString();
        _waterGoal.Text = _s.WaterGoalMl.ToString();
        _hideFs.IsChecked = _s.HideOnFullscreen;
        _notifPeek.IsChecked = _s.ShowNotificationPeek;
        _notifSound.IsChecked = _s.NotificationSound;
        _opacityBox.Text = _s.NotchOpacityPercent.ToString();
        _collapse.Text = _s.CollapseDelayMs.ToString();
        _hotkey.Text = _s.HotkeyKey;
        _apps.Text = string.Join(", ", _s.CallApps);
        _updates.IsChecked = _s.CheckUpdates;
        _autostart = App.IsAutostart();
        _autoStartChk.IsChecked = _autostart;
    }

    private bool Save()
    {
        if (!TryInt(_pomo, "Durasi timer", out var pomo) || !TryInt(_waterInterval, "Interval minum", out var wi) ||
            !TryInt(_waterGoal, "Target minum", out var wg) || !TryInt(_collapse, "Jeda menutup", out var cd) ||
            !TryInt(_opacityBox, "Opasitas notch", out var op))
            return false;

        var key = _hotkey.Text.Trim();
        if (key.Length != 1 || !char.IsLetterOrDigit(key[0]))
        {
            MessageBox.Show(this, "Hotkey harus satu huruf atau angka.", "Pengaturan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _s.ShowMusic = _music.IsChecked == true; _s.ShowCall = _call.IsChecked == true; _s.ShowShelf = _shelf.IsChecked == true;
        _s.ShowNotif = _notif.IsChecked == true;
        _s.ShowTimer = _timer.IsChecked == true; _s.ShowWater = _water.IsChecked == true; _s.ShowSystem = _system.IsChecked == true;
        _s.PomodoroMinutes = pomo; _s.TimerSound = _timerSound.IsChecked == true;
        _s.WaterIntervalMinutes = wi; _s.WaterGoalMl = wg;
        _s.HideOnFullscreen = _hideFs.IsChecked == true;
        _s.ShowNotificationPeek = _notifPeek.IsChecked == true;
        _s.NotificationSound = _notifSound.IsChecked == true;
        _s.NotchOpacityPercent = Math.Clamp(op, 70, 100);
        _s.CollapseDelayMs = cd;
        _s.HotkeyKey = key;
        _s.CallApps = _apps.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        _s.CheckUpdates = _updates.IsChecked == true;
        _s.MonitorIndex = _monitors.FindIndex(r => r.IsChecked == true) - 1; // item 0 = monitor utama (-1)

        // Asupan air bisa berubah saat jendela terbuka: ambil nilai terbaru
        _s.WaterIntakeMl = SettingsService.Current.WaterIntakeMl;
        _s.WaterDate = SettingsService.Current.WaterDate;

        if (_autoStartChk.IsChecked != _autostart) App.SetAutostart(_autoStartChk.IsChecked == true);

        SettingsService.Apply(_s);
        return true;
    }

    private bool TryInt(TextBox box, string label, out int value)
    {
        if (int.TryParse(box.Text.Trim(), out value) && value > 0) return true;
        MessageBox.Show(this, $"{label} harus berupa angka lebih dari 0.", "Pengaturan", MessageBoxButton.OK, MessageBoxImage.Warning);
        box.Focus();
        return false;
    }

    // ----- Aksi -----
    private async void CheckUpdateNow()
    {
        var info = await UpdateService.CheckAsync();
        if (info == null)
        {
            MessageBox.Show(this, "Anda sudah memakai versi terbaru (atau pengecekan gagal, lihat log).", "Pembaruan", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(this, $"Versi {info.Tag} tersedia. Buka halaman unduhan?", "Pembaruan", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo(info.Url) { UseShellExecute = true });
    }

    private void OpenLogs()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Log.Dir);
            Process.Start(new ProcessStartInfo(Log.Dir) { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Warn("Gagal membuka folder log", ex); }
    }

    // ----- Pembangun UI -----
    private UIElement BuildMonitorList()
    {
        var host = new StackPanel();
        var screens = System.Windows.Forms.Screen.AllScreens;
        _monitors.Add(Radio("Monitor utama", "mon"));
        for (int i = 0; i < screens.Length; i++)
        {
            var sc = screens[i];
            _monitors.Add(Radio($"Monitor {i + 1}  ({sc.Bounds.Width}×{sc.Bounds.Height}){(sc.Primary ? " • utama" : "")}", "mon"));
        }
        int sel = _s.MonitorIndex >= 0 && _s.MonitorIndex < screens.Length ? _s.MonitorIndex + 1 : 0;
        _monitors[sel].IsChecked = true;
        foreach (var r in _monitors) host.Children.Add(r);
        return host;
    }

    private UIElement BuildFooter()
    {
        var save = FlatButton("Simpan", true);
        save.Click += (_, _) => { if (Save()) Close(); };
        var apply = FlatButton("Terapkan", false);
        apply.Click += (_, _) => Save();
        var cancel = FlatButton("Batal", false);
        cancel.Click += (_, _) => Close();

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x18)),
            Padding = new Thickness(18, 10, 18, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { cancel, apply, save }
            }
        };
    }

    private static UIElement Docked(UIElement e, Dock d) { DockPanel.SetDock(e, d); return e; }

    private static Border Section(string title, params UIElement[] items)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = AccentBrush, Margin = new Thickness(0, 0, 0, 6)
        });
        foreach (var i in items) sp.Children.Add(i);
        return new Border
        {
            Background = Card, CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10), Child = sp
        };
    }

    private static CheckBox Chk(string text) => new()
    {
        Content = text, Foreground = Brushes.White, Margin = new Thickness(0, 3, 14, 3), VerticalContentAlignment = VerticalAlignment.Center
    };

    private static RadioButton Radio(string text, string group) => new()
    {
        Content = text, GroupName = group, Foreground = Brushes.White, Margin = new Thickness(0, 3, 0, 3)
    };

    private static TextBox Box(double width = 80) => new()
    {
        Width = width, Padding = new Thickness(6, 3, 6, 3), Background = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3C)),
        Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x56)),
        CaretBrush = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left
    };

    private static UIElement Row(string label, TextBox box)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var t = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(box, 1);
        g.Children.Add(t);
        g.Children.Add(box);
        return g;
    }

    private static UIElement Hint(string text) => new TextBlock
    {
        Text = text, Foreground = Dim, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6)
    };

    private static UIElement Wrap(params UIElement[] items)
    {
        var w = new WrapPanel();
        foreach (var i in items) w.Children.Add(i);
        return w;
    }

    private static UIElement Buttons(params (string text, Action act)[] items)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var (text, act) in items)
        {
            var b = FlatButton(text, false);
            b.Click += (_, _) => act();
            sp.Children.Add(b);
        }
        return sp;
    }

    private static Button FlatButton(string text, bool primary)
    {
        var b = new Button
        {
            Content = text, Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0),
            Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Background = primary ? AccentBrush : new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3E))
        };
        b.Template = new ControlTemplate(typeof(Button))
        {
            VisualTree = Factory()
        };
        return b;

        static FrameworkElementFactory Factory()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            return border;
        }
    }
}
