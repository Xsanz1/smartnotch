using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using Windows.Media.Control;

namespace SmartNotch.App.Services;

/// <summary>Now Playing via Windows SMTC (Spotify, browser, VLC, dll.).</summary>
public sealed class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _mgr;
    private GlobalSystemMediaTransportControlsSession? _s;
    private string _key = "";
    private bool _first = true;
    private TimeSpan _pos;
    private DateTimeOffset _posAt;

    public bool HasSession { get; private set; }
    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public bool IsPlaying { get; private set; }
    public ImageSource? Art { get; private set; }
    public TimeSpan Duration { get; private set; }

    /// <summary>Warna gelap turunan dari sampul album, dipakai sebagai gradasi panel.</summary>
    public Color Tint { get; private set; } = Color.FromRgb(0x10, 0x1C, 0x3E);

    /// <summary>Warna cerah turunan dari sampul album untuk equalizer bar.</summary>
    public Color VibrantColor { get; private set; } = Color.FromRgb(0x3D, 0xDC, 0x97);

    public event Action? Changed;
    public event Action? TrackChanged;

    public TimeSpan Position
    {
        get
        {
            if (!HasSession) return TimeSpan.Zero;
            var p = _pos;
            if (IsPlaying)
            {
                var el = DateTimeOffset.UtcNow - _posAt;
                if (el > TimeSpan.Zero && el < TimeSpan.FromHours(6)) p += el;
            }
            if (Duration > TimeSpan.Zero && p > Duration) p = Duration;
            return p < TimeSpan.Zero ? TimeSpan.Zero : p;
        }
    }

    public async Task InitAsync()
    {
        try
        {
            _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _mgr.CurrentSessionChanged += (_, _) => Ui(Attach);
            Attach();
        }
        catch { /* SMTC tidak tersedia */ }
    }

    private static void Ui(Action a) => Application.Current?.Dispatcher.InvokeAsync(a);

    private void Attach()
    {
        if (_s != null)
        {
            _s.MediaPropertiesChanged -= OnProps;
            _s.PlaybackInfoChanged -= OnProps;
            _s.TimelinePropertiesChanged -= OnProps;
        }
        _s = _mgr?.GetCurrentSession();
        if (_s == null)
        {
            HasSession = false; Title = Artist = ""; IsPlaying = false; Art = null; Duration = TimeSpan.Zero;
            Changed?.Invoke();
            return;
        }
        _s.MediaPropertiesChanged += OnProps;
        _s.PlaybackInfoChanged += OnProps;
        _s.TimelinePropertiesChanged += OnProps;
        _ = RefreshAsync();
    }

    private void OnProps(GlobalSystemMediaTransportControlsSession s, object a) => Ui(() => _ = RefreshAsync());

    private async Task RefreshAsync()
    {
        var s = _s;
        if (s == null) return;
        try
        {
            var props = await s.TryGetMediaPropertiesAsync();
            var pb = s.GetPlaybackInfo();
            var tl = s.GetTimelineProperties();

            HasSession = true;
            Title = props.Title ?? "";
            Artist = props.Artist ?? "";
            IsPlaying = pb.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            Duration = tl.EndTime - tl.StartTime;
            _pos = tl.Position;
            _posAt = tl.LastUpdatedTime;

            var key = Title + "|" + Artist;
            bool trackChanged = key != _key;
            if (trackChanged)
            {
                _key = key;
                Art = null;
                if (props.Thumbnail != null)
                {
                    try
                    {
                        using var rs = await props.Thumbnail.OpenReadAsync();
                        var ms = new MemoryStream();
                        await rs.AsStreamForRead().CopyToAsync(ms);
                        ms.Position = 0;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        Art = bmp;
                        var (tint, vib) = ExtractColors(bmp);
                        Tint = tint;
                        VibrantColor = vib;
                    }
                    catch { Art = null; Tint = Color.FromRgb(0x10, 0x1C, 0x3E); VibrantColor = Color.FromRgb(0x3D, 0xDC, 0x97); }
                }
            }

            Changed?.Invoke();
            if (trackChanged && Title.Length > 0)
            {
                if (!_first) TrackChanged?.Invoke();
                _first = false;
            }
        }
        catch { }
    }

    public async void TogglePlayPause() { try { if (_s != null) await _s.TryTogglePlayPauseAsync(); } catch { } }
    public async void Next() { try { if (_s != null) await _s.TrySkipNextAsync(); } catch { } }
    public async void Previous() { try { if (_s != null) await _s.TrySkipPreviousAsync(); } catch { } }

    private static (Color Tint, Color Vibrant) ExtractColors(BitmapSource src)
    {
        try
        {
            var fc = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            var tb = new TransformedBitmap(fc, new ScaleTransform(8.0 / fc.PixelWidth, 8.0 / fc.PixelHeight));
            int w = tb.PixelWidth, h = tb.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            tb.CopyPixels(px, stride, 0);
            long r = 0, g = 0, b = 0; int n = w * h;
            for (int i = 0; i < n; i++) { b += px[i * 4]; g += px[i * 4 + 1]; r += px[i * 4 + 2]; }
            byte avgR = (byte)(r / n), avgG = (byte)(g / n), avgB = (byte)(b / n);

            const double k = 0.42;
            var tint = Color.FromRgb((byte)(avgR * k), (byte)(avgG * k), (byte)(avgB * k));

            double max = Math.Max(avgR, Math.Max(avgG, avgB));
            double boost = max < 100 ? (190.0 / Math.Max(1, max)) : 1.3;
            var vib = Color.FromRgb(
                (byte)Math.Clamp(avgR * boost, 80, 255),
                (byte)Math.Clamp(avgG * boost, 80, 255),
                (byte)Math.Clamp(avgB * boost, 80, 255));

            return (tint, vib);
        }
        catch { return (Color.FromRgb(0x10, 0x1C, 0x3E), Color.FromRgb(0x3D, 0xDC, 0x97)); }
    }
}
