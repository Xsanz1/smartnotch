using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SmartNotch.App.Services;

namespace SmartNotch.App.Widgets;

/// <summary>
/// Pemutar musik 1:1 identik dengan Nusa dari Instagram Reel (Player di kiri, lirik di kanan).
/// </summary>
public sealed class MediaWidget : INotchWidget
{
    private readonly MediaService _m;
    private readonly LyricsService _lyrics;
    private readonly Image _art = new() { Stretch = Stretch.UniformToFill };
    private readonly TextBlock _ph = Ui.T("♪", 24, false, Ui.B("#44FFFFFF"), HorizontalAlignment.Center, VerticalAlignment.Center);
    private readonly TextBlock _title = Ui.T("Tidak ada media", 16, true, Brushes.White);
    private readonly TextBlock _artist = Ui.T("Putar lagu di Spotify atau browser", 12, false, Ui.B("#A0A0A0"));
    private readonly TextBlock _cur = Ui.T("00:00", 10, false, Ui.B("#8E99A8"));
    private readonly TextBlock _rem = Ui.T("-00:00", 10, false, Ui.B("#8E99A8"), HorizontalAlignment.Right);
    private readonly ColumnDefinition _fill = new() { Width = new GridLength(0.0001, GridUnitType.Star) };
    private readonly ColumnDefinition _rest = new() { Width = new GridLength(1, GridUnitType.Star) };
    private readonly Ellipse _thumb = new()
    {
        Width = 7, Height = 7,
        Fill = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, -3.5, 0)
    };
    private readonly Button _play;
    private readonly Button _lyricBtn;
    private readonly Grid _lyricPanel = new();
    private readonly TextBlock _lPrev = Ui.T("", 12, false, Ui.B("#70FFFFFF"), wrap: true);
    private readonly TextBlock _lCur = Ui.T("", 16, true, Brushes.White, wrap: true);
    private readonly TextBlock _lNext = Ui.T("", 12, false, Ui.B("#70FFFFFF"), wrap: true);
    private readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMilliseconds(200) };

    private List<LyricLine>? _lines;
    private string _lyricKey = "";
    private string _lyricStatus = "";
    private int _lineIdx = -2;
    private bool _showLyrics = true;

    public string Title => "Musik";
    public FrameworkElement View { get; }

    public MediaWidget(MediaService m, LyricsService lyrics, AudioVolumeService audio)
    {
        _m = m; _lyrics = lyrics;

        _play = Ui.Btn("▶", m.TogglePlayPause, 18);
        _play.Width = 44; _play.Height = 36;
        _lyricBtn = Ui.Btn("💬", ToggleLyrics, 13);
        _lyricBtn.Width = 32; _lyricBtn.Height = 30;

        // Album Art dengan sudut lengkung 14px (1:1 Nusa Reel)
        var artBox = new Border
        {
            Width = 56, Height = 56, CornerRadius = new CornerRadius(14),
            Background = Ui.B("#18FFFFFF"),
            BorderBrush = Ui.B("#22FFFFFF"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Grid { Children = { _ph, _art } }
        };
        var artClip = new RectangleGeometry(new Rect(0, 0, 56, 56), 14, 14);
        artBox.Child.SetValue(UIElement.ClipProperty, artClip);

        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _artist.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.Margin = new Thickness(0, 2, 0, 2);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        info.Children.Add(_title);
        info.Children.Add(_artist);

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.Children.Add(artBox);
        Grid.SetColumn(info, 1);
        head.Children.Add(info);

        // Progress bar dengan circular thumb knob persis di reel
        var barTrack = new Grid { Height = 3, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0), ClipToBounds = false };
        barTrack.ColumnDefinitions.Add(_fill);
        barTrack.ColumnDefinitions.Add(_rest);

        var restB = new Border { Background = Ui.B("#25FFFFFF"), CornerRadius = new CornerRadius(1.5) };
        var fillB = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(1.5),
            ClipToBounds = false,
            Child = _thumb
        };
        Grid.SetColumn(restB, 1);
        barTrack.Children.Add(restB);
        barTrack.Children.Add(fillB);

        var prog = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        prog.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        prog.ColumnDefinitions.Add(new ColumnDefinition());
        prog.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _cur.VerticalAlignment = VerticalAlignment.Center;
        _rem.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(barTrack, 1);
        Grid.SetColumn(_rem, 2);
        prog.Children.Add(_cur); prog.Children.Add(barTrack); prog.Children.Add(_rem);

        // Controls bar: 💬 (kiri) | ⏮ ▶ ⏭ (tengah) persis di video
        var ctrlGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        _lyricBtn.HorizontalAlignment = HorizontalAlignment.Left;
        _lyricBtn.VerticalAlignment = VerticalAlignment.Center;

        var midCtrl = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        midCtrl.Children.Add(Ui.Btn("⏮", m.Previous, 15));
        midCtrl.Children.Add(_play);
        midCtrl.Children.Add(Ui.Btn("⏭", m.Next, 15));

        ctrlGrid.Children.Add(_lyricBtn);
        ctrlGrid.Children.Add(midCtrl);

        // Volume Control Row: 🔈 [──●─────] 🔊  45% (Interaktif: Drag, Klik, Scroll Roda Mouse, Mute)
        var volRow = new Grid { Margin = new Thickness(0, 8, 0, 0), Cursor = Cursors.Hand };
        volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var volIcon = Ui.T("🔈", 11, false, Ui.B("#8E99A8"), va: VerticalAlignment.Center);
        volIcon.Cursor = Cursors.Hand;
        volIcon.MouseLeftButtonDown += (_, _) => audio.ToggleMute();

        var volFill = new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) };
        var volRest = new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) };
        var volThumb = new Ellipse
        {
            Width = 7, Height = 7, Fill = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, -3.5, 0)
        };
        var volBarTrack = new Grid { Height = 3, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0), ClipToBounds = false };
        volBarTrack.ColumnDefinitions.Add(volFill);
        volBarTrack.ColumnDefinitions.Add(volRest);

        var volRestB = new Border { Background = Ui.B("#25FFFFFF"), CornerRadius = new CornerRadius(1.5) };
        var volFillB = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(1.5), ClipToBounds = false, Child = volThumb };
        Grid.SetColumn(volRestB, 1);
        volBarTrack.Children.Add(volRestB);
        volBarTrack.Children.Add(volFillB);

        var volText = Ui.T("50%", 10, false, Ui.B("#8E99A8"), ha: HorizontalAlignment.Right, va: VerticalAlignment.Center);
        volText.Width = 28;

        Grid.SetColumn(volIcon, 0);
        Grid.SetColumn(volBarTrack, 1);
        Grid.SetColumn(volText, 2);

        volRow.Children.Add(volIcon);
        volRow.Children.Add(volBarTrack);
        volRow.Children.Add(volText);

        void UpdateVolUi(float v, bool muted)
        {
            volIcon.Text = muted ? "🔇" : (v <= 0.01f ? "🔈" : (v < 0.5f ? "🔉" : "🔊"));
            volIcon.Foreground = muted ? Ui.B("#FF453A") : Ui.B("#8E99A8");
            volText.Text = muted ? "Mute" : $"{(int)(v * 100)}%";
            volFill.Width = new GridLength(Math.Clamp(v, 0.0001, 1.0), GridUnitType.Star);
            volRest.Width = new GridLength(Math.Clamp(1.0 - v, 0.0001, 1.0), GridUnitType.Star);
        }

        UpdateVolUi(audio.Volume, audio.IsMuted);
        audio.VolumeChanged += (v, m) => Application.Current?.Dispatcher.InvokeAsync(() => UpdateVolUi(v, m));

        void SetVolFromPoint(Point pt)
        {
            double w = volBarTrack.ActualWidth;
            if (w <= 0) return;
            double p = Math.Clamp(pt.X / w, 0.0, 1.0);
            audio.SetVolume((float)p);
        }

        volBarTrack.MouseLeftButtonDown += (_, e) =>
        {
            volBarTrack.CaptureMouse();
            SetVolFromPoint(e.GetPosition(volBarTrack));
        };
        volBarTrack.MouseMove += (_, e) =>
        {
            if (volBarTrack.IsMouseCaptured)
                SetVolFromPoint(e.GetPosition(volBarTrack));
        };
        volBarTrack.MouseLeftButtonUp += (_, _) => volBarTrack.ReleaseMouseCapture();

        volRow.MouseWheel += (_, e) =>
        {
            float step = e.Delta > 0 ? 0.03f : -0.03f;
            audio.SetVolume(audio.Volume + step);
            e.Handled = true;
        };

        var player = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Width = 270,
            Margin = new Thickness(0, 0, 20, 0)
        };
        player.Children.Add(head);
        player.Children.Add(prog);
        player.Children.Add(ctrlGrid);
        player.Children.Add(volRow);

        // Panel Lirik Sinkron 3-Baris Vertikal (Nusa 1:1)
        _lPrev.MaxHeight = 26; _lNext.MaxHeight = 26; _lCur.MaxHeight = 44;
        _lPrev.Margin = new Thickness(0, 0, 0, 3);
        _lCur.Margin = new Thickness(0, 3, 0, 3);
        _lNext.Margin = new Thickness(0, 3, 0, 0);

        var lstack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        lstack.Children.Add(_lPrev);
        lstack.Children.Add(_lCur);
        lstack.Children.Add(_lNext);
        _lyricPanel.Children.Add(lstack);

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(player);
        Grid.SetColumn(_lyricPanel, 1);
        g.Children.Add(_lyricPanel);
        View = g;

        m.Changed += Refresh;
        _t.Tick += (_, _) => { UpdateProgress(); UpdateLyrics(); };
        Refresh();
    }

    public void OnShown() { Refresh(); _t.Start(); }
    public void OnHidden() => _t.Stop();

    private void ToggleLyrics()
    {
        _showLyrics = !_showLyrics;
        _lyricPanel.Visibility = _showLyrics ? Visibility.Visible : Visibility.Collapsed;
        _lyricBtn.Opacity = _showLyrics ? 1.0 : 0.4;
    }

    private void Refresh()
    {
        _title.Text = _m.HasSession && _m.Title.Length > 0 ? _m.Title : "Tidak ada media";
        _artist.Text = _m.HasSession ? (_m.Artist.Length > 0 ? _m.Artist : "—") : "Putar lagu di Spotify atau browser";
        _art.Source = _m.Art;
        _ph.Visibility = _m.Art == null ? Visibility.Visible : Visibility.Collapsed;
        _play.Content = _m.IsPlaying ? "⏸" : "▶";
        _lyricBtn.Opacity = _showLyrics ? 1.0 : 0.4;

        var key = _m.Title + "|" + _m.Artist;
        if (key != _lyricKey)
        {
            _lyricKey = key;
            _lines = null;
            _lineIdx = -2;
            if (_m.HasSession && _m.Title.Length > 0)
            {
                _lyricStatus = "Mencari lirik...";
                _ = LoadLyricsAsync(key);
            }
            else _lyricStatus = "";
        }
        UpdateProgress();
        UpdateLyrics();
    }

    private async Task LoadLyricsAsync(string key)
    {
        var res = await _lyrics.GetAsync(_m.Title, _m.Artist, _m.Duration.TotalSeconds);
        if (key != _lyricKey) return;
        _lines = res;
        _lyricStatus = res == null ? "Lirik tidak ditemukan" : "";
        _lineIdx = -2;
        UpdateLyrics();
    }

    private void UpdateProgress()
    {
        var d = _m.Duration.TotalSeconds;
        var p = _m.Position.TotalSeconds;
        double r = d > 0 ? Math.Clamp(p / d, 0, 1) : 0;
        _fill.Width = new GridLength(Math.Max(r, 0.0001), GridUnitType.Star);
        _rest.Width = new GridLength(Math.Max(1 - r, 0.0001), GridUnitType.Star);
        _cur.Text = Fmt(_m.Position);
        _rem.Text = d > 0 ? "-" + Fmt(_m.Duration - _m.Position) : "-00:00";
    }

    private void UpdateLyrics()
    {
        if (!_showLyrics) return;

        if (_lines == null)
        {
            if (_lineIdx == -3) return;
            _lineIdx = -3;
            _lPrev.Text = ""; _lNext.Text = "";
            _lCur.Text = _lyricStatus;
            _lCur.Opacity = 0.5;
            return;
        }

        var pos = _m.Position + TimeSpan.FromMilliseconds(250);
        int idx = -1;
        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Time <= pos) idx = i; else break;
        }
        if (idx == _lineIdx) return;
        _lineIdx = idx;

        _lPrev.Text = idx - 1 >= 0 ? _lines[idx - 1].Text : "";
        _lCur.Text = idx >= 0 ? _lines[idx].Text : "♪";
        _lNext.Text = idx + 1 < _lines.Count ? _lines[idx + 1].Text : "";
        _lCur.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(240)));
    }

    private static string Fmt(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    }
}
