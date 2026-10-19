using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SmartNotch.App.Interop;
using SmartNotch.App.Services;
using SmartNotch.App.Widgets;

namespace SmartNotch.App;

public enum NotchState { Idle, Peek, Expanded }

public partial class NotchWindow : Window
{
    public static NotchWindow Instance { get; private set; } = null!;

    // Ukuran notch sesuai rancangan sistem (tidak terpotong)
    private static readonly Size IdleSize = new(200, 32);         // Idle: [🟢 Ring] + [HH:mm Clock] + [Equalizer Wave / 🔴 Ring]
    private static readonly Size IdleCallSize = new(240, 32);     // Idle saat telepon aktif (1:1 iOS Dynamic Island Phone Call Pill)
    private static readonly Size PeekSize = new(340, 40);         // Live activity peek
    private static readonly Size MessagePeekSize = new(400, 70);  // Preview pesan masuk (pengirim + isi 2 baris)
    private Size _peekSize = PeekSize;
    private static readonly Size ExpSize = new(640, 190);         // Expanded panel penuh sesuai rancangan (tidak terpotong)
    private const double HitMargin = 12;
    private const int CollapseDelayMs = 400;
    private const int HotkeyId = 0x534E; // "SN"

    private NotchState _state = NotchState.Idle;
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _peekTimer = new();
    private readonly DispatcherTimer _fsTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _lastInside = DateTime.MinValue;
    private IntPtr _hwnd;
    private bool _hiddenByUser, _hiddenByFullscreen;
    private bool IsHidden => _hiddenByUser || _hiddenByFullscreen;

    public bool Pinned { get; set; }

    // Services
    public MediaService Media { get; } = new();
    public BatteryService Battery { get; } = new();
    public StatsService Stats { get; } = new();
    public NotificationService Notif { get; } = new();
    public CalendarService Calendar { get; } = new();
    public PomodoroService Pomodoro { get; } = new();
    public WaterReminderService Water { get; } = new();
    public BluetoothService Bluetooth { get; } = new();
    public UsbDeviceService Usb { get; } = new();
    public AudioVolumeService Audio { get; } = new();
    public WhatsAppCallService WhatsApp { get; } = new();

    // Widgets
    private readonly List<INotchWidget> _widgets = new();
    private ShelfWidget _shelf = null!;
    private MediaWidget _mediaWidget = null!;
    private CallWidget _callWidget = null!;
    private TimerWidget _timerWidget = null!;
    private WaterWidget _waterWidget = null!;
    private SystemWidget _systemWidget = null!;
    private NotifWidget _notifWidget = null!;
    private System.Drawing.Rectangle _screenBounds;
    private bool _positioning;
    private int _tab = -1;
    private readonly List<ScaleTransform> _idleBars = new();
    private readonly List<DoubleAnimationUsingKeyFrames> _idleAnimations = new();
    private readonly List<ScaleTransform> _callBars = new();
    private readonly List<DoubleAnimationUsingKeyFrames> _callAnimations = new();
    private bool _barsAnimating;

    public NotchWindow()
    {
        Instance = this;
        InitializeComponent();
        BuildIdleBars();
        BuildCallIdleBars();
        BuildWidgets();
        WireServices();

        CallIdleContent.MouseLeftButtonDown += (_, e) =>
        {
            WhatsAppCallService.FocusWhatsAppWindow();
            e.Handled = true;
        };

        SettingsService.Changed += OnSettingsChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            Usb.Stop();
            Win32.UnregisterHotKey(_hwnd, HotkeyId);
            SettingsService.Changed -= OnSettingsChanged;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        };
        _poll.Tick += (_, _) => Tick();
        _peekTimer.Tick += (_, _) =>
        {
            _peekTimer.Stop();
            if (_state == NotchState.Peek) SetState(NotchState.Idle);
        };
        _fsTimer.Tick += (_, _) => CheckFullscreen();
        _clockTimer.Tick += (_, _) => UpdateIdle();
        _clockTimer.Start();

        // Drag file ke notch: otomatis alihkan ke tab Shelf
        Body.PreviewDragEnter += (_, e) =>
        {
            if (_state == NotchState.Expanded && e.Data.GetDataPresent(DataFormats.FileDrop))
                SelectTab(_widgets.IndexOf(_shelf));
        };
        ExpandedLayer.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        ExpandedLayer.Drop += (_, e) =>
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            SelectTab(_widgets.IndexOf(_shelf));
            _shelf.AddFiles(e.Data);
            e.Handled = true;
        };
    }

    private void BuildIdleBars()
    {
        IdleBars.Children.Clear();
        _idleBars.Clear();
        _idleAnimations.Clear();

        // 3 bar equalizer harmonik berirama gelombang (1:1 Nusa Reel & Apple Music Waveform)
        // Ketiga batang berbagi tempo siklus 0.72s yang sama secara sinkron, menciptakan efek gelombang cair (bukan acak)
        var patterns = new[]
        {
            // Bar 0 (Kiri): Gelombang utama
            new[] { (0.00, 0.30), (0.18, 0.85), (0.36, 0.30), (0.54, 0.65), (0.72, 0.30) },
            // Bar 1 (Tengah): Puncak tengah yang lentur
            new[] { (0.00, 0.65), (0.18, 0.30), (0.36, 1.00), (0.54, 0.45), (0.72, 0.65) },
            // Bar 2 (Kanan): Harmonik pengimbang
            new[] { (0.00, 0.45), (0.18, 0.75), (0.36, 0.25), (0.54, 0.90), (0.72, 0.45) }
        };

        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

        for (int i = 0; i < patterns.Length; i++)
        {
            var frames = patterns[i];
            var st = new ScaleTransform(1.0, frames[0].Item2);
            _idleBars.Add(st);

            var rect = new Rectangle
            {
                Width = 2.8,
                Height = 13,
                RadiusX = 1.4,
                RadiusY = 1.4,
                Margin = new Thickness(1.5, 0, 1.5, 0),
                Fill = Ui.B("#F5FFFFFF"),
                VerticalAlignment = VerticalAlignment.Bottom,
                RenderTransform = st,
                RenderTransformOrigin = new Point(0.5, 1.0)
            };
            IdleBars.Children.Add(rect);

            var dkf = new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            foreach (var (t, val) in frames)
            {
                dkf.KeyFrames.Add(new EasingDoubleKeyFrame(val, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t)), ease));
            }

            _idleAnimations.Add(dkf);
        }
    }

    private void BuildCallIdleBars()
    {
        CallIdleBars.Children.Clear();
        _callBars.Clear();
        _callAnimations.Clear();

        var framesList = new[]
        {
            new[] { (0.00, 0.35), (0.15, 0.90), (0.30, 0.40), (0.45, 0.70), (0.60, 0.35) },
            new[] { (0.00, 0.70), (0.15, 0.30), (0.30, 1.00), (0.45, 0.45), (0.60, 0.70) },
            new[] { (0.00, 0.45), (0.15, 0.75), (0.30, 0.30), (0.45, 0.90), (0.60, 0.45) }
        };

        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

        for (int i = 0; i < 3; i++)
        {
            var st = new ScaleTransform(1.0, 0.4);
            _callBars.Add(st);

            var rect = new Rectangle
            {
                Width = 2.4,
                Height = 10,
                RadiusX = 1.2,
                RadiusY = 1.2,
                Fill = Ui.B("#FF30D158"),
                Margin = new Thickness(1.2, 0, 1.2, 0),
                RenderTransform = st,
                RenderTransformOrigin = new Point(0.5, 1.0)
            };
            CallIdleBars.Children.Add(rect);

            var dkf = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            foreach (var (t, val) in framesList[i])
            {
                dkf.KeyFrames.Add(new EasingDoubleKeyFrame(val, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t)), ease));
            }
            _callAnimations.Add(dkf);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, dkf);
        }
    }

    // ---------- Setup Widgets (1:1 Nusa: Navigasi Tombol Geser Kiri/Kanan & Paginasi Dots) ----------
    private void BuildWidgets()
    {
        _callWidget = new CallWidget(WhatsApp);
        _mediaWidget = new MediaWidget(Media, new LyricsService(), Audio);
        _shelf = new ShelfWidget();

        _timerWidget = new TimerWidget(Pomodoro);
        _waterWidget = new WaterWidget(Water);
        _systemWidget = new SystemWidget(Stats, Battery);
        _notifWidget = new NotifWidget(Notif);
        Pomodoro.SetMinutes(SettingsService.Current.PomodoroMinutes);

        NavPrevBtn.Click += (_, _) => PrevTab();
        NavNextBtn.Click += (_, _) => NextTab();

        // Ganti halaman dengan scroll roda mouse di notch
        Body.MouseWheel += (_, e) =>
        {
            if (_state != NotchState.Expanded) return;
            if (e.Delta < 0) NextTab(); else PrevTab();
        };

        RebuildWidgets(SettingsService.Current);
    }

    public void NextTab()
    {
        int next = (_tab + 1) % _widgets.Count;
        SelectTab(next, 1);
    }

    public void PrevTab()
    {
        int prev = (_tab - 1 + _widgets.Count) % _widgets.Count;
        SelectTab(prev, -1);
    }

    public void SelectTab(int i, int? forceDirection = null)
    {
        if (i < 0 || i >= _widgets.Count || i == _tab) return;
        int oldIdx = _tab;
        int newIdx = i;

        int dir = forceDirection ?? (oldIdx == -1 ? 0 : (newIdx > oldIdx ? 1 : -1));

        var oldWidget = oldIdx >= 0 && oldIdx < _widgets.Count ? _widgets[oldIdx] : null;
        var newWidget = _widgets[newIdx];

        (oldWidget as MediaWidget)?.OnHidden();
        (oldWidget as SystemWidget)?.OnHidden();

        _tab = newIdx;
        UpdatePaginationDots();

        var newView = newWidget.View;

        if (dir == 0 || oldWidget == null)
        {
            ContentContainer.Children.Clear();
            newView.BeginAnimation(OpacityProperty, null);
            newView.Opacity = 1.0;
            newView.RenderTransform = null;
            ContentContainer.Children.Add(newView);
            newWidget.OnShown();
            return;
        }

        var oldView = oldWidget.View;
        double shift = 55.0 * dir;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var dur = TimeSpan.FromMilliseconds(260);

        // Slide & fade out view lama
        var oldTt = new TranslateTransform(0, 0);
        oldView.RenderTransform = oldTt;
        oldTt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -shift, dur) { EasingFunction = ease });
        oldView.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });

        // Slide & fade in view baru
        var newTt = new TranslateTransform(shift, 0);
        newView.RenderTransform = newTt;
        newView.Opacity = 0.0;

        if (!ContentContainer.Children.Contains(newView))
        {
            ContentContainer.Children.Add(newView);
        }

        newTt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(shift, 0, dur) { EasingFunction = ease });
        var fadeIn = new DoubleAnimation(0.0, 1.0, dur) { EasingFunction = ease };
        fadeIn.Completed += (_, _) =>
        {
            // Hanya transisi terakhir yang boleh membersihkan; transisi lama (mis. scroll cepat)
            // tidak boleh membuang view yang sekarang sedang ditampilkan.
            var current = _tab >= 0 && _tab < _widgets.Count ? _widgets[_tab].View : null;
            if (current != newView) return;

            for (int k = ContentContainer.Children.Count - 1; k >= 0; k--)
            {
                if (ContentContainer.Children[k] != newView)
                {
                    ContentContainer.Children.RemoveAt(k);
                }
            }
            newView.RenderTransform = null;
        };
        newView.BeginAnimation(OpacityProperty, fadeIn);

        newWidget.OnShown();
    }

    private void UpdatePaginationDots()
    {
        PaginationDots.Children.Clear();
        for (int k = 0; k < _widgets.Count; k++)
        {
            int targetIdx = k;
            if (k == _tab)
            {
                // Active dot: elongated white pill
                var pill = new Rectangle
                {
                    Width = 14, Height = 3.5, RadiusX = 1.75, RadiusY = 1.75,
                    Fill = Brushes.White, Margin = new Thickness(2.5, 0, 2.5, 0),
                    Cursor = Cursors.Hand
                };
                pill.MouseLeftButtonDown += (_, _) => SelectTab(targetIdx);
                PaginationDots.Children.Add(pill);
            }
            else
            {
                // Inactive dot: small translucent circle
                var dot = new Ellipse
                {
                    Width = 3.5, Height = 3.5,
                    Fill = Ui.B("#55FFFFFF"), Margin = new Thickness(2.5, 0, 2.5, 0),
                    Cursor = Cursors.Hand
                };
                dot.MouseLeftButtonDown += (_, _) => SelectTab(targetIdx, targetIdx > _tab ? 1 : -1);
                PaginationDots.Children.Add(dot);
            }
        }
    }


    private void WireServices()
    {
        Media.Changed += () => { UpdateIdle(); UpdateTint(); };
        Media.TrackChanged += () =>
        {
            ShowPeek($"{Media.Title}  •  {Media.Artist}", Media.Art);
            if (_state == NotchState.Expanded) SelectTab(MediaIndex);
        };
        Battery.PlugChanged += plugged =>
            ShowPeek(plugged ? $"⚡ Pengisi daya terhubung  •  {Battery.Percent}%" : $"🔋 Pengisi daya dicabut  •  Sisa {Battery.Percent}%", null, 4000);
        Battery.LowBattery += p => ShowPeek($"🔋 Baterai lemah  •  {p}%", null, 5000);
        Pomodoro.Tick += UpdateIdle;
        Pomodoro.Finished += () => { if (SettingsService.Current.TimerSound) System.Media.SystemSounds.Exclamation.Play(); };
        Pomodoro.Finished += () => ShowPeek("⏱ Timer fokus selesai", null, 6000);
        Water.NeedWater += () => ShowPeek("💧 Waktunya minum air putih!", null, 5000);
        Notif.Received += n =>
        {
            WhatsApp.OnNotificationReceived(n);
            if (!SettingsService.Current.ShowNotificationPeek) return;
            if (WhatsApp.CurrentCall.State == WhatsAppCallState.Incoming) return; // sudah ditangani peek panggilan
            if (ShowMessagePeek(n)) return;
            ShowPeek($"🔔 {n.App}: {n.Title}", null, 4000);
        };
        Calendar.Reminder += (e, mins) => ShowPeek($"📅 {e.Title}  •  {(mins <= 0 ? "sekarang" : mins + " mnt lagi")}", null, 6000);
        Bluetooth.DeviceConnectionChanged += (name, connected, icon) =>
        {
            string status = connected ? "Terhubung" : "Terputus";
            ShowPeek($"{icon} {name}  •  {status}", null, 4000);
        };
        Usb.DeviceChanged += (text, _) => ShowPeek(text, null, 4000);
        Audio.ExternalVolumeChanged += (vol, muted) =>
        {
            string icon = muted ? "🔇" : (vol <= 0.01f ? "🔈" : (vol < 0.5f ? "🔉" : "🔊"));
            string text = muted ? $"{icon} Audio Dibisukan" : $"{icon} Volume  {(int)(vol * 100)}%";
            ShowPeek(text, null, 1800);
        };

        // WhatsApp Call Lifecycle Events (1:1 iOS Dynamic Island Phone Call)
        WhatsApp.CallStarted += info =>
        {
            UpdateIdle();
            if (info.State == WhatsAppCallState.Incoming)
            {
                string icon = info.IsVideo ? "📹" : "📞";
                ShowPeek($"{icon} {info.CallerName}  •  Panggilan {WhatsApp.ActiveApp} Masuk...", null, 6000);
            }
            else if (info.State == WhatsAppCallState.Active)
            {
                if (_state == NotchState.Idle)
                {
                    SetState(NotchState.Idle);
                }
                else if (_state == NotchState.Expanded)
                {
                    SelectTab(_widgets.IndexOf(_callWidget));
                }
            }
        };

        WhatsApp.CallUpdated += info =>
        {
            if (_state == NotchState.Idle)
            {
                CallIdleName.Text = string.IsNullOrWhiteSpace(info.CallerName) ? "WhatsApp" : info.CallerName;
                TimeSpan d = info.Duration;
                CallIdleTimer.Text = $"{d.Minutes:00}:{d.Seconds:00}";
            }
        };

        WhatsApp.CallEnded += duration =>
        {
            UpdateIdle();
            if (_state == NotchState.Idle)
            {
                SetState(NotchState.Idle);
            }
            string durStr = duration.Hours > 0 ? $"{duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}" : $"{duration.Minutes:00}:{duration.Seconds:00}";
            ShowPeek($"🔴 Panggilan {WhatsApp.ActiveApp} Berakhir  •  {durStr}", null, 3000);
        };

        _ = Media.InitAsync();
        _ = Notif.InitAsync();
        Bluetooth.Start();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var ex = Win32.GetWindowLong(_hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(_hwnd, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);


        // Hotkey global Ctrl+Alt+N
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        RegisterHotkey(SettingsService.Current.HotkeyKey);
        PositionOnScreen();
        Dispatcher.BeginInvoke(PositionOnScreen, DispatcherPriority.Loaded);

        SetClickThrough(true);
        UpdateIdle();
        UpdateTint();
        Usb.Start(_hwnd);
        _poll.Start();
        _fsTimer.Start();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            ToggleVisible();
            handled = true;
        }
        else if (msg == Win32.WM_DEVICECHANGE)
        {
            Usb.HandleDeviceChange(wParam, lParam);
        }
        return IntPtr.Zero;
    }

    // ---------- Visibility ----------
    public bool UserHidden => _hiddenByUser;

    public void ToggleVisible()
    {
        _hiddenByUser = !_hiddenByUser;
        ApplyVisibility();
    }

    private void CheckFullscreen()
    {
        bool fs = SettingsService.Current.HideOnFullscreen && IsForegroundFullscreen();
        if (fs == _hiddenByFullscreen) return;
        _hiddenByFullscreen = fs;
        ApplyVisibility();
    }

    private bool IsForegroundFullscreen()
    {
        try
        {
            var h = Win32.GetForegroundWindow();
            if (h == IntPtr.Zero || h == _hwnd) return false;

            Win32.GetWindowThreadProcessId(h, out var pid);
            if (pid == Environment.ProcessId) return false;

            var sb = new System.Text.StringBuilder(64);
            Win32.GetClassName(h, sb, sb.Capacity);
            var cls = sb.ToString();
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;

            if (Win32.IsZoomed(h)) return false;

            if (!Win32.GetWindowRect(h, out var r)) return false;
            var scr = System.Windows.Forms.Screen.FromHandle(h);
            if (scr.DeviceName != TargetScreen.DeviceName) return false;
            var b = scr.Bounds;
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }
        catch { return false; }
    }

    private void ApplyVisibility()
    {
        if (IsHidden)
        {
            _peekTimer.Stop();
            SetState(NotchState.Idle);
            Hide();
        }
        else if (!IsVisible) Show();
    }

    // ---------- Settings, Monitor & Hotkey ----------
    private int MediaIndex => _widgets.IndexOf(_mediaWidget);

    private System.Windows.Forms.Screen TargetScreen
    {
        get
        {
            var all = System.Windows.Forms.Screen.AllScreens;
            int i = SettingsService.Current.MonitorIndex;
            return i >= 0 && i < all.Length ? all[i] : System.Windows.Forms.Screen.PrimaryScreen ?? all[0];
        }
    }

    /// <summary>Letakkan notch di tengah-atas monitor target (piksel fisik, aman untuk DPI berbeda antar monitor).</summary>
    private void PositionOnScreen()
    {
        if (_hwnd == IntPtr.Zero || _positioning) return;
        _positioning = true;
        try
        {
            var b = TargetScreen.Bounds;
            _screenBounds = b;
            const uint flags = Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE;

            // Pindah dulu agar DPI monitor target ikut berlaku, lalu pusatkan memakai lebar piksel sebenarnya
            Win32.SetWindowPos(_hwnd, IntPtr.Zero, b.Left + b.Width / 2 - 10, b.Top, 0, 0, flags);
            var dpi = VisualTreeHelper.GetDpi(this);
            int w = (int)Math.Round(Width * dpi.DpiScaleX);
            Win32.SetWindowPos(_hwnd, IntPtr.Zero, b.Left + (b.Width - w) / 2, b.Top, 0, 0, flags);
        }
        catch (Exception ex) { Log.Warn("Gagal memosisikan notch", ex); }
        finally { _positioning = false; }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        PositionOnScreen();
    }

    private void OnDisplayChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(PositionOnScreen, DispatcherPriority.Background);

    private void RegisterHotkey(string key)
    {
        if (_hwnd == IntPtr.Zero || string.IsNullOrEmpty(key)) return;
        Win32.UnregisterHotKey(_hwnd, HotkeyId);
        if (!Win32.RegisterHotKey(_hwnd, HotkeyId, Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, key[0]))
            Log.Warn($"Hotkey Ctrl+Alt+{key} gagal didaftarkan (kemungkinan dipakai aplikasi lain)");
    }

    private void RebuildWidgets(AppSettings s)
    {
        var prev = _tab >= 0 && _tab < _widgets.Count ? _widgets[_tab] : null;

        _widgets.Clear();
        if (s.ShowMusic) _widgets.Add(_mediaWidget);
        if (s.ShowCall) _widgets.Add(_callWidget);
        if (s.ShowShelf) _widgets.Add(_shelf);
        if (s.ShowNotif) _widgets.Add(_notifWidget);
        if (s.ShowTimer) _widgets.Add(_timerWidget);
        if (s.ShowWater) _widgets.Add(_waterWidget);
        if (s.ShowSystem) _widgets.Add(_systemWidget);
        if (_widgets.Count == 0) _widgets.Add(_mediaWidget);

        int idx = prev != null ? _widgets.IndexOf(prev) : -1;
        if (prev != null && idx < 0)
        {
            (prev as MediaWidget)?.OnHidden();
            (prev as SystemWidget)?.OnHidden();
        }

        _tab = -1;
        ContentContainer.Children.Clear();
        SelectTab(idx >= 0 ? idx : 0);
    }

    private void OnSettingsChanged(AppSettings s)
    {
        RebuildWidgets(s);
        if (!Pomodoro.Running && (int)Pomodoro.Total.TotalMinutes != s.PomodoroMinutes)
            Pomodoro.SetMinutes(s.PomodoroMinutes);
        Water.ApplySettings(s);
        RegisterHotkey(s.HotkeyKey);
        PositionOnScreen();
        CheckFullscreen();
        UpdateIdle();
    }

    // ---------- Hover state machine ----------
    private Size CurrentSize => _state switch
    {
        NotchState.Expanded => ExpSize,
        NotchState.Peek => _peekSize,
        _ => (WhatsApp != null && WhatsApp.IsInCall ? IdleCallSize : IdleSize)
    };

    private void Tick()
    {
        if (IsHidden || !Win32.GetCursorPos(out var p)) return;

        // Hit-test dalam piksel fisik terhadap monitor target
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var b = _screenBounds;
        var size = CurrentSize;
        double cx = b.Left + b.Width / 2.0;
        bool inside = p.Y >= b.Top && p.Y <= b.Top + (size.Height + HitMargin) * scale
                      && Math.Abs(p.X - cx) <= (size.Width / 2 + HitMargin) * scale;

        if (inside || Pinned)
        {
            _lastInside = DateTime.UtcNow;
            if (_state != NotchState.Expanded && inside) { _peekTimer.Stop(); SetState(NotchState.Expanded); }
        }
        else if (_state == NotchState.Expanded &&
                 (DateTime.UtcNow - _lastInside).TotalMilliseconds > SettingsService.Current.CollapseDelayMs)
        {
            SetState(NotchState.Idle);
        }
    }

    public void ShowPeek(string text, ImageSource? icon = null, int ms = 3000)
    {
        if (IsHidden || _state == NotchState.Expanded) return;
        _peekSize = PeekSize;
        PeekLayer.Height = 40;
        PeekBody.Visibility = Visibility.Collapsed;
        PeekText.VerticalAlignment = VerticalAlignment.Center;
        PeekText.Text = text;
        PeekArt.Source = icon;
        PeekArtBox.Visibility = icon == null ? Visibility.Collapsed : Visibility.Visible;
        PeekText.Margin = icon == null ? new Thickness(14, 0, 14, 0) : new Thickness(44, 0, 14, 0);
        PeekText.HorizontalAlignment = icon == null ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        SetState(NotchState.Peek);
        _peekTimer.Stop();
        _peekTimer.Interval = TimeSpan.FromMilliseconds(ms);
        _peekTimer.Start();
    }

    /// <summary>Tampilkan pesan masuk (pengirim + isi). Mengembalikan false jika notifikasi tidak punya isi.</summary>
    private bool ShowMessagePeek(NotifItem n)
    {
        if (string.IsNullOrWhiteSpace(n.Body)) return false;
        if (IsHidden || _state == NotchState.Expanded) return true;

        bool wa = n.App.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
        _peekSize = MessagePeekSize;
        PeekLayer.Height = MessagePeekSize.Height;
        PeekArtBox.Visibility = Visibility.Collapsed;
        PeekArt.Source = null;

        PeekText.Text = $"{(wa ? "💬" : "🔔")} {(wa ? n.Title : n.App + " • " + n.Title)}";
        PeekText.Margin = new Thickness(14, 10, 14, 0);
        PeekText.HorizontalAlignment = HorizontalAlignment.Left;
        PeekText.VerticalAlignment = VerticalAlignment.Top;

        PeekBody.Text = n.Body;
        PeekBody.Margin = new Thickness(14, 30, 14, 0);
        PeekBody.Visibility = Visibility.Visible;

        SetState(NotchState.Peek);
        _peekTimer.Stop();
        _peekTimer.Interval = TimeSpan.FromMilliseconds(6000);
        _peekTimer.Start();
        return true;
    }

    private void SetState(NotchState s)
    {
        var old = _state;
        _state = s;
        bool exp = s == NotchState.Expanded;
        SetClickThrough(!exp);

        var size = CurrentSize;
        var dur = TimeSpan.FromMilliseconds(exp ? 320 : 250);
        IEasingFunction ease = exp || s == NotchState.Peek
            ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.28 }
            : new CubicEase { EasingMode = EasingMode.EaseInOut };

        Body.BeginAnimation(WidthProperty, new DoubleAnimation(size.Width, dur) { EasingFunction = ease });
        Body.BeginAnimation(HeightProperty, new DoubleAnimation(size.Height, dur) { EasingFunction = ease });

        double r = exp ? 24 : s == NotchState.Peek ? 18 : 14;
        Body.CornerRadius = new CornerRadius(0, 0, r, r);
        Tint.CornerRadius = new CornerRadius(0, 0, r, r);

        Fade(IdleLayer, s == NotchState.Idle ? 1 : 0, s == NotchState.Idle ? 200 : 70, s == NotchState.Idle ? 120 : 0);
        Fade(PeekLayer, s == NotchState.Peek ? 1 : 0, s == NotchState.Peek ? 200 : 70, s == NotchState.Peek ? 90 : 0);
        Fade(ExpandedLayer, exp ? 1 : 0, exp ? 250 : 70, exp ? 110 : 0);
        ExpandedLayer.IsHitTestVisible = exp;

        if (exp)
        {
            if (Media.HasSession && Media.IsPlaying && MediaIndex >= 0 && _tab != MediaIndex) SelectTab(MediaIndex);
            _widgets[_tab].OnShown();
        }
        else if (old == NotchState.Expanded)
        {
            (_widgets[_tab] as MediaWidget)?.OnHidden();
            (_widgets[_tab] as SystemWidget)?.OnHidden();
        }
    }

    private static void Fade(UIElement e, double to, int ms, int delay) =>
        e.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { BeginTime = TimeSpan.FromMilliseconds(delay) });

    // ---------- Idle Content & Ambient Tint ----------
    private Color _tint = Colors.Transparent;

    private void UpdateTint()
    {
        var c = Media.HasSession && Media.Art != null ? Media.Tint : Color.FromRgb(0x0e, 0x14, 0x24);
        if (c == _tint) return;
        _tint = c;
        Tint.Background = new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(255, 0, 0, 0), 0.0),
            new GradientStop(Color.FromArgb(230, 0, 0, 0), 0.35),
            new GradientStop(c, 1.0),
        }, new Point(0.5, 0), new Point(0.5, 1));
    }

    private void UpdateIdle()
    {
        // 0. Mode Panggilan WhatsApp Aktif (1:1 iOS Dynamic Island Phone Call Pill)
        if (WhatsApp.IsInCall)
        {
            NormalIdleContent.Visibility = Visibility.Collapsed;
            CallIdleContent.Visibility = Visibility.Visible;

            var info = WhatsApp.CurrentCall;
            CallIdleName.Text = string.IsNullOrWhiteSpace(info.CallerName) ? "WhatsApp" : info.CallerName;
            TimeSpan d = info.Duration;
            CallIdleTimer.Text = $"{d.Minutes:00}:{d.Seconds:00}";

            // Ambient Emerald Green Glow khas Dynamic Island saat telepon aktif
            Tint.Background = new SolidColorBrush(Color.FromArgb(0x28, 0x25, 0xd3, 0x66));

            double targetCallW = IdleCallSize.Width;
            if (_state == NotchState.Idle && Math.Abs(Body.Width - targetCallW) > 1)
            {
                Body.BeginAnimation(WidthProperty,
                    new DoubleAnimation(targetCallW, TimeSpan.FromMilliseconds(220))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
            return;
        }

        NormalIdleContent.Visibility = Visibility.Visible;
        CallIdleContent.Visibility = Visibility.Collapsed;

        bool hasMusic = Media.HasSession && Media.IsPlaying;

        // 1. Kiri: Cincin Baterai Lingkaran Dinamis (1:1 Nusa Circular Battery Ring)
        double batteryPercent = Battery.HasBattery ? ((double)Battery.Percent / 100.0) : 1.0;
        bool isCharging = Battery.HasBattery && Battery.Plugged;
        UpdateBatteryRing(batteryPercent, isCharging);

        // 2. Tengah: Jam Digital (HH:mm) Murni & Bersih
        if (Pomodoro.Running)
        {
            IdleClockText.Text = PomodoroService.Format(Pomodoro.Remaining);
            IdleClockText.Foreground = Ui.B("#FFFF453A");
        }
        else
        {
            IdleClockText.Text = DateTime.Now.ToString("HH:mm");
            IdleClockText.Foreground = Ui.B("#F0FFFFFF");
        }

        // 3. Kanan: Equalizer Bars saat musik berputar ATAU Ring Merah saat idle / fokus
        if (hasMusic)
        {
            IdleBars.Visibility = Visibility.Visible;
            IdleRedBox.Visibility = Visibility.Collapsed;

            if (!_barsAnimating)
            {
                _barsAnimating = true;
                for (int i = 0; i < _idleBars.Count; i++)
                {
                    _idleBars[i].BeginAnimation(ScaleTransform.ScaleYProperty, _idleAnimations[i]);
                }
            }
        }
        else
        {
            IdleBars.Visibility = Visibility.Collapsed;
            IdleRedBox.Visibility = Visibility.Visible;
            IdleRedDot.Fill = Pomodoro.Running ? Ui.B("#FF453A") : Ui.B("#E0FF453A");

            if (_barsAnimating)
            {
                _barsAnimating = false;
                for (int i = 0; i < _idleBars.Count; i++)
                {
                    _idleBars[i].BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    _idleBars[i].ScaleY = 0.25;
                }
            }
        }

        // Pertahankan ukuran compact saat idle
        double targetIdleW = IdleSize.Width;
        if (_state == NotchState.Idle && Math.Abs(Body.Width - targetIdleW) > 1)
        {
            Body.BeginAnimation(WidthProperty,
                new DoubleAnimation(targetIdleW, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }

    private void UpdateBatteryRing(double percent, bool isCharging)
    {
        percent = Math.Clamp(percent, 0.02, 1.0);
        double r = 6.1;
        double cx = 8.0, cy = 8.0;

        var brush = isCharging ? Ui.B("#30D158") : (percent <= 0.2 ? Ui.B("#FF453A") : Ui.B("#30D158"));
        var trackBrush = percent <= 0.2 && !isCharging ? Ui.B("#25FF453A") : Ui.B("#2530D158");

        BatteryTrackRing.Stroke = trackBrush;
        BatteryRingPath.Stroke = brush;

        BatteryChargeBolt.Visibility = isCharging ? Visibility.Visible : Visibility.Collapsed;
        BatteryChargeBolt.Foreground = brush;

        if (percent >= 0.99)
        {
            BatteryRingPath.Data = new EllipseGeometry(new Point(cx, cy), r, r);
        }
        else
        {
            double startAngle = -90.0;
            double sweepAngle = percent * 360.0;
            double endAngle = startAngle + sweepAngle;

            double startRad = startAngle * Math.PI / 180.0;
            double endRad = endAngle * Math.PI / 180.0;

            Point pStart = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
            Point pEnd = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));

            bool isLargeArc = sweepAngle > 180.0;

            var fig = new PathFigure { StartPoint = pStart, IsClosed = false };
            fig.Segments.Add(new ArcSegment(pEnd, new Size(r, r), 0, isLargeArc, SweepDirection.Clockwise, true));

            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            BatteryRingPath.Data = geo;
        }
    }

    // ---------- Win32 ----------
    private void SetClickThrough(bool on)
    {
        if (_hwnd == IntPtr.Zero) return;
        var ex = Win32.GetWindowLong(_hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(_hwnd, Win32.GWL_EXSTYLE, on ? ex | Win32.WS_EX_TRANSPARENT : ex & ~Win32.WS_EX_TRANSPARENT);
    }
}
