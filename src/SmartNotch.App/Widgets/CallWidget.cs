using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SmartNotch.App.Services;

namespace SmartNotch.App.Widgets;

/// <summary>
/// Widget panggilan WhatsApp interaktif (1:1 iOS Dynamic Island Phone Call Card).
/// Menampilkan kontak penelepon, durasi waktu real-time, visualisasi suara,
/// tombol Buka WhatsApp, dan tombol Mute/Unmute mikrofon.
/// </summary>
public sealed class CallWidget : INotchWidget
{
    private readonly WhatsAppCallService _call;
    private readonly TextBlock _callerName = Ui.T("Panggilan WhatsApp", 18, true, Brushes.White);
    private readonly TextBlock _callSub = Ui.T("Panggilan Suara Berlangsung", 12, false, Ui.B("#A0E0B0"));
    private readonly TextBlock _timerText = Ui.T("00:00", 22, true, Ui.B("#30D158"));
    private readonly Button _focusBtn;
    private readonly Button _muteBtn;
    private readonly Border _avatarPulse;
    private readonly StackPanel _waveBars = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };

    public string Title => "Panggilan";
    public FrameworkElement View { get; }

    public CallWidget(WhatsAppCallService call)
    {
        _call = call;

        // Avatar Penelepon dengan Pulse Halo Ring Hijau
        _avatarPulse = new Border
        {
            Width = 62, Height = 62, CornerRadius = new CornerRadius(31),
            Background = Ui.B("#2030D158"), BorderBrush = Ui.B("#4030D158"), BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };

        var avatarInner = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23),
            Background = Ui.B("#FF25D366"), // WhatsApp Brand Green
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Child = Ui.T("📞", 20, false, Brushes.White, HorizontalAlignment.Center, VerticalAlignment.Center)
        };

        var avatarContainer = new Grid { Width = 72, Height = 72, Margin = new Thickness(0, 0, 16, 0) };
        avatarContainer.Children.Add(_avatarPulse);
        avatarContainer.Children.Add(avatarInner);

        // Tombol Aksi: Buka WhatsApp & Mute Mikrofon
        _focusBtn = Ui.Btn("💬 Buka WhatsApp", WhatsAppCallService.FocusWhatsAppWindow, 12, "#18FFFFFF");
        _focusBtn.Height = 32; _focusBtn.Padding = new Thickness(14, 0, 14, 0); _focusBtn.Margin = new Thickness(0, 0, 8, 0);

        _muteBtn = Ui.Btn("🎙 Mute Mic", () => _call.ToggleMicrophoneMute(), 12, "#18FFFFFF");
        _muteBtn.Height = 32; _muteBtn.Padding = new Thickness(12, 0, 12, 0);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        actions.Children.Add(_focusBtn);
        actions.Children.Add(_muteBtn);

        // Visual Gelombang Suara Panggilan (4 bar harmonik hijau)
        for (int i = 0; i < 4; i++)
        {
            var bar = new Border
            {
                Width = 3.5, Height = 14, CornerRadius = new CornerRadius(1.75),
                Background = Ui.B("#FF30D158"), Margin = new Thickness(2, 0, 2, 0)
            };
            _waveBars.Children.Add(bar);
        }

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(_callerName);
        infoStack.Children.Add(_callSub);
        infoStack.Children.Add(actions);

        var timeStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(20, 0, 0, 0)
        };
        timeStack.Children.Add(_timerText);
        timeStack.Children.Add(_waveBars);

        var mainGrid = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(avatarContainer, 0);
        Grid.SetColumn(infoStack, 1);
        Grid.SetColumn(timeStack, 2);

        mainGrid.Children.Add(avatarContainer);
        mainGrid.Children.Add(infoStack);
        mainGrid.Children.Add(timeStack);

        View = mainGrid;

        _call.CallUpdated += UpdateUi;
        _call.CallStarted += UpdateUi;
        _call.MicMuteChanged += isMuted => Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            _muteBtn.Content = isMuted ? "🔇 Unmute Mic" : "🎙 Mute Mic";
            _muteBtn.Background = isMuted ? Ui.B("#50FF453A") : Ui.B("#18FFFFFF");
        });
    }

    public void OnShown()
    {
        UpdateUi(_call.CurrentCall);
        StartPulseAnimation();
    }

    private void UpdateUi(WhatsAppCallInfo info)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (info.State == WhatsAppCallState.None)
            {
                _callerName.Text = "WhatsApp";
                _callSub.Text = "Tidak ada panggilan aktif";
                _timerText.Text = "00:00";
                _waveBars.Visibility = Visibility.Collapsed;
            }
            else
            {
                _callerName.Text = string.IsNullOrWhiteSpace(info.CallerName) ? "Panggilan WhatsApp" : info.CallerName;
                _callSub.Text = info.State == WhatsAppCallState.Incoming
                    ? (info.IsVideo ? "📹 Panggilan Video Masuk..." : "📞 Panggilan Suara Masuk...")
                    : (info.IsVideo ? "📹 Panggilan Video Berlangsung" : "● Panggilan WhatsApp Berlangsung");

                TimeSpan d = info.Duration;
                _timerText.Text = d.Hours > 0 ? $"{d.Hours:00}:{d.Minutes:00}:{d.Seconds:00}" : $"{d.Minutes:00}:{d.Seconds:00}";
                _waveBars.Visibility = Visibility.Visible;
            }

            bool isMuted = info.IsMicMuted;
            _muteBtn.Content = isMuted ? "🔇 Unmute Mic" : "🎙 Mute Mic";
            _muteBtn.Background = isMuted ? Ui.B("#50FF453A") : Ui.B("#18FFFFFF");
        });
    }

    private void StartPulseAnimation()
    {
        var da = new DoubleAnimation(0.3, 1.0, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        _avatarPulse.BeginAnimation(UIElement.OpacityProperty, da);
    }
}
