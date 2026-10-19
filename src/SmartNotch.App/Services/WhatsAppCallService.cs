using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Microsoft.Win32;
using SmartNotch.App.Interop;

namespace SmartNotch.App.Services;

#region WASAPI Audio Session Interop
[Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionManager2
{
    int NotImpl1();
    int NotImpl2();
    [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
}

[Guid("E2F5EE11-2070-4E42-8646-57A41BE42E76"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEnumerator
{
    [PreserveSig] int GetCount(out int SessionCount);
    [PreserveSig] int GetSession(int SessionIndex, out IAudioSessionControl Session);
}

[Guid("F4B04960-6001-4495-9E0C-7301F822CE68"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl
{
    [PreserveSig] int GetState(out int pRetVal);
}

[Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c32"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl2
{
    [PreserveSig] int NotImpl1();
    [PreserveSig] int NotImpl2();
    [PreserveSig] int NotImpl3();
    [PreserveSig] int NotImpl4();
    [PreserveSig] int NotImpl5();
    [PreserveSig] int NotImpl6();
    [PreserveSig] int NotImpl7();
    [PreserveSig] int NotImpl8();
    [PreserveSig] int NotImpl9();
    [PreserveSig] int NotImpl10();
    [PreserveSig] int NotImpl11();
    [PreserveSig] int NotImpl12();
    [PreserveSig] int NotImpl13();
    [PreserveSig] int GetProcessId(out uint pRetVal);
}
#endregion

public enum WhatsAppCallState
{
    None,
    Incoming,
    Active,
    Ended
}

public sealed class WhatsAppCallInfo
{
    public WhatsAppCallState State { get; set; } = WhatsAppCallState.None;
    public string CallerName { get; set; } = "Panggilan WhatsApp";
    public bool IsVideo { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public DateTime? StartTime { get; set; }
    public bool IsMicMuted { get; set; }
}

/// <summary>
/// Layanan pendeteksi dan pengendali panggilan WhatsApp (WhatsApp Desktop UWP / Win32 / Web)
/// melalui WASAPI Core Audio Capture Sessions, Windows CapabilityAccessManager Registry,
/// dan integrasi Notifikasi Windows.
/// </summary>
public sealed class WhatsAppCallService
{
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Stopwatch _callStopwatch = new();
    private WhatsAppCallState _lastState = WhatsAppCallState.None;
    private IAudioEndpointVolume? _micVolumeEndpoint;

    public WhatsAppCallInfo CurrentCall { get; } = new();
    public bool IsInCall => CurrentCall.State == WhatsAppCallState.Active || CurrentCall.State == WhatsAppCallState.Incoming;

    public event Action<WhatsAppCallInfo>? CallStarted;
    public event Action<WhatsAppCallInfo>? CallUpdated;
    public event Action<TimeSpan>? CallEnded;
    public event Action<bool>? MicMuteChanged;

    private string _activeApp = "WhatsApp";
    public string ActiveApp => CallAppMatcher.Display(_activeApp);
    private static IReadOnlyList<string> Apps => SettingsService.Current.CallApps;

    public WhatsAppCallService()
    {
        InitMicEndpoint();
        _pollTimer.Tick += (_, _) => Poll();
        _pollTimer.Start();
    }

    private static bool IsDefaultCaller(string name) =>
        string.IsNullOrWhiteSpace(name) || name.StartsWith("Panggilan ", StringComparison.OrdinalIgnoreCase);

    private static string DefaultCallerFor(string app) => $"Panggilan {CallAppMatcher.Display(app)}";

    private void InitMicEndpoint()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            // eCapture = 1, eCommunications = 2
            if (enumerator.GetDefaultAudioEndpoint(1, 2, out var dev) == 0 && dev != null)
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                if (dev.Activate(ref iid, 1, IntPtr.Zero, out var ep) == 0 && ep is IAudioEndpointVolume vol)
                {
                    _micVolumeEndpoint = vol;
                }
            }
        }
        catch { }
    }

    public void OnNotificationReceived(NotifItem item)
    {
        // Deteksi panggilan masuk dari Windows Toast Notification
        string app = item.App ?? "";
        string title = item.Title ?? "";
        string body = item.Body ?? "";

        bool isWhatsApp = app.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) ||
                         title.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);

        if (!isWhatsApp) return;

        bool isCall = title.Contains("panggilan", StringComparison.OrdinalIgnoreCase) ||
                      title.Contains("call", StringComparison.OrdinalIgnoreCase) ||
                      body.Contains("panggilan", StringComparison.OrdinalIgnoreCase) ||
                      body.Contains("call", StringComparison.OrdinalIgnoreCase) ||
                      body.Contains("berdering", StringComparison.OrdinalIgnoreCase) ||
                      body.Contains("ringing", StringComparison.OrdinalIgnoreCase);

        if (isCall)
        {
            bool isVideo = title.Contains("video", StringComparison.OrdinalIgnoreCase) ||
                           body.Contains("video", StringComparison.OrdinalIgnoreCase);

            string caller = !string.IsNullOrWhiteSpace(title) && !title.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase)
                ? title
                : (!string.IsNullOrWhiteSpace(body) ? body : "Panggilan WhatsApp");

            // Bersihkan teks panggilan
            caller = caller.Replace("Panggilan suara masuk", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("Panggilan video masuk", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("Incoming voice call", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("Incoming video call", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("dari", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("from", "", StringComparison.OrdinalIgnoreCase)
                           .Trim(' ', ':', '-', '•');

            if (string.IsNullOrWhiteSpace(caller)) caller = "Panggilan WhatsApp";

            CurrentCall.CallerName = caller;
            CurrentCall.IsVideo = isVideo;

            if (CurrentCall.State != WhatsAppCallState.Active)
            {
                CurrentCall.State = WhatsAppCallState.Incoming;
                CurrentCall.StartTime = DateTime.Now;
                CallStarted?.Invoke(CurrentCall);
            }
        }
    }

    private void Poll()
    {
        string? detected = DetectCallApp();
        bool isMicActive = detected != null;
        if (detected != null) _activeApp = detected;
        string? windowTitle = detected != null ? FindCallWindowTitle(detected) : null;

        if (detected != null && IsDefaultCaller(CurrentCall.CallerName))
            CurrentCall.CallerName = DefaultCallerFor(detected);

        if (!string.IsNullOrWhiteSpace(windowTitle) && IsDefaultCaller(CurrentCall.CallerName))
        {
            var cleaned = CleanCallerTitle(windowTitle);
            if (!string.IsNullOrWhiteSpace(cleaned)) CurrentCall.CallerName = cleaned;
        }

        // State Machine Transisi Panggilan
        if (isMicActive)
        {
            if (CurrentCall.State != WhatsAppCallState.Active)
            {
                // Mulai panggilan aktif
                CurrentCall.State = WhatsAppCallState.Active;
                if (CurrentCall.StartTime == null) CurrentCall.StartTime = DateTime.Now;
                _callStopwatch.Restart();
                CallStarted?.Invoke(CurrentCall);
            }
            else
            {
                CurrentCall.Duration = _callStopwatch.Elapsed;
                CurrentCall.IsMicMuted = CheckIsMicMuted();
                CallUpdated?.Invoke(CurrentCall);
            }
        }
        else
        {
            if (CurrentCall.State == WhatsAppCallState.Active)
            {
                // Panggilan selesai
                TimeSpan total = _callStopwatch.Elapsed;
                _callStopwatch.Stop();
                CurrentCall.State = WhatsAppCallState.Ended;
                CurrentCall.Duration = total;
                CallEnded?.Invoke(total);

                CurrentCall.State = WhatsAppCallState.None;
                CurrentCall.StartTime = null;
                CurrentCall.Duration = TimeSpan.Zero;
                CurrentCall.CallerName = "Panggilan WhatsApp";
            }
            else if (CurrentCall.State == WhatsAppCallState.Incoming)
            {
                // Jika panggilan masuk tidak diangkat dalam 35 detik, batalkan
                if (CurrentCall.StartTime.HasValue && (DateTime.Now - CurrentCall.StartTime.Value).TotalSeconds > 35)
                {
                    CurrentCall.State = WhatsAppCallState.None;
                    CurrentCall.StartTime = null;
                }
            }
        }

        _lastState = CurrentCall.State;
    }

    /// <summary>
    /// Memeriksa apakah WhatsApp saat ini sedang aktif menangkap mikrofon (WASAPI / Registry).
    /// </summary>
    private string? DetectCallApp()
    {
        // 1. Periksa Registry CapabilityAccessManager (Kernel-level Windows Mic Tracker)
        try
        {
            using var baseKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone");
            if (baseKey != null)
            {
                // Cek Packaged apps (WhatsApp Desktop UWP / WinUI3)
                foreach (var sub in baseKey.GetSubKeyNames())
                {
                    var app = CallAppMatcher.Match(sub, Apps);
                    if (app != null)
                    {
                        using var k = baseKey.OpenSubKey(sub);
                        if (k != null)
                        {
                            var startObj = k.GetValue("LastUsedTimeStart");
                            var stopObj = k.GetValue("LastUsedTimeStop");
                            long start = Convert.ToInt64(startObj ?? 0);
                            long stop = Convert.ToInt64(stopObj ?? 0);

                            // Jika mikrofon sedang digunakan sekarang: stop bernilai 0 atau start > stop
                            if (start > 0 && (stop == 0 || start > stop))
                            {
                                // Pastikan proses WhatsApp masih berjalan
                                if (IsAppProcessRunning(app)) return app;
                            }
                        }
                    }
                }

                // Cek NonPackaged apps (WhatsApp Web di browser / desktop exe biasa)
                using var nonPackaged = baseKey.OpenSubKey("NonPackaged");
                if (nonPackaged != null)
                {
                    foreach (var sub in nonPackaged.GetSubKeyNames())
                    {
                        var app = CallAppMatcher.Match(sub, Apps);
                        if (app != null)
                        {
                            using var k = nonPackaged.OpenSubKey(sub);
                            if (k != null)
                            {
                                long start = Convert.ToInt64(k.GetValue("LastUsedTimeStart") ?? 0);
                                long stop = Convert.ToInt64(k.GetValue("LastUsedTimeStop") ?? 0);
                                if (start > 0 && (stop == 0 || start > stop)) return app;
                            }
                        }
                    }
                }
            }
        }
        catch { }

        // 2. Periksa Audio Session WASAPI jika ada
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            // eCapture = 1, eConsole = 0
            if (enumerator.GetDefaultAudioEndpoint(1, 0, out var dev) == 0 && dev != null)
            {
                var iid = typeof(IAudioSessionManager2).GUID;
                if (dev.Activate(ref iid, 1, IntPtr.Zero, out var obj) == 0 && obj is IAudioSessionManager2 mgr)
                {
                    if (mgr.GetSessionEnumerator(out var sessEnum) == 0 && sessEnum != null)
                    {
                        if (sessEnum.GetCount(out int count) == 0)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if (sessEnum.GetSession(i, out var ctrl) == 0 && ctrl != null)
                                {
                                    ctrl.GetState(out int state);
                                    if (state == 1 /* Active */ && ctrl is IAudioSessionControl2 ctrl2)
                                    {
                                        ctrl2.GetProcessId(out uint pid);
                                        if (pid > 0)
                                        {
                                            try
                                            {
                                                var proc = Process.GetProcessById((int)pid);
                                                var app = CallAppMatcher.Match(proc.ProcessName, Apps);
                                                if (app != null)
                                                {
                                                    return app;
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static bool IsAppProcessRunning(string app)
    {
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try { if (p.ProcessName.Contains(app, StringComparison.OrdinalIgnoreCase)) return true; }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch (Exception ex) { Log.Warn("Gagal membaca daftar proses", ex); }
        return false;
    }

    private string? FindCallWindowTitle(string app)
    {
        string? result = null;
        try
        {
            Win32.EnumWindows((hwnd, _) =>
            {
                if (!Win32.IsWindowVisible(hwnd)) return true;
                Win32.GetWindowThreadProcessId(hwnd, out uint pid);
                try
                {
                    var p = Process.GetProcessById((int)pid);
                    if (p.ProcessName.Contains(app, StringComparison.OrdinalIgnoreCase))
                    {
                        var sb = new StringBuilder(256);
                        Win32.GetWindowText(hwnd, sb, 256);
                        string t = sb.ToString();
                        if (!string.IsNullOrWhiteSpace(t) && !t.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase))
                        {
                            result = t;
                            return false; // Stop
                        }
                    }
                }
                catch { }
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        return result;
    }

    private static string CleanCallerTitle(string title)
    {
        return title.Replace("WhatsApp Call", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Panggilan WhatsApp", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Panggilan suara", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Voice call", "", StringComparison.OrdinalIgnoreCase)
                    .Trim(' ', ':', '-', '•');
    }

    public bool CheckIsMicMuted()
    {
        if (_micVolumeEndpoint == null) InitMicEndpoint();
        if (_micVolumeEndpoint == null) return false;
        try
        {
            if (_micVolumeEndpoint.GetMute(out bool muted) == 0) return muted;
        }
        catch { }
        return false;
    }

    public void ToggleMicrophoneMute()
    {
        if (_micVolumeEndpoint == null) InitMicEndpoint();
        if (_micVolumeEndpoint == null) return;
        try
        {
            if (_micVolumeEndpoint.GetMute(out bool muted) == 0)
            {
                var ctx = Guid.Empty;
                bool newMute = !muted;
                _micVolumeEndpoint.SetMute(newMute, ref ctx);
                CurrentCall.IsMicMuted = newMute;
                MicMuteChanged?.Invoke(newMute);
                CallUpdated?.Invoke(CurrentCall);
            }
        }
        catch { }
    }

    /// <summary>
    /// Membawa jendela WhatsApp ke layar depan (Focus).
    /// </summary>
    public static void FocusWhatsAppWindow()
    {
        try
        {
            Process[] procs = Process.GetProcessesByName("WhatsApp");
            if (procs.Length == 0) procs = Process.GetProcessesByName("WhatsApp.Root");

            if (procs.Length > 0)
            {
                IntPtr hwnd = IntPtr.Zero;
                foreach (var p in procs)
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        hwnd = p.MainWindowHandle;
                        break;
                    }
                }

                if (hwnd == IntPtr.Zero)
                {
                    // Cari window aktif milik proses WhatsApp
                    Win32.EnumWindows((h, _) =>
                    {
                        Win32.GetWindowThreadProcessId(h, out uint pid);
                        foreach (var p in procs)
                        {
                            if (pid == p.Id && Win32.IsWindowVisible(h))
                            {
                                hwnd = h;
                                return false;
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                }

                if (hwnd != IntPtr.Zero)
                {
                    Win32.ShowWindow(hwnd, 9 /* SW_RESTORE */);
                    Win32.SetForegroundWindow(hwnd);
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Simulasi pengujian panggilan (berguna untuk testing langsung saat user tidak sedang ada panggilan).
    /// </summary>
    public void SimulateCall(string caller = "Budi Hartono", bool isVideo = false)
    {
        CurrentCall.CallerName = caller;
        CurrentCall.IsVideo = isVideo;
        CurrentCall.State = WhatsAppCallState.Active;
        CurrentCall.StartTime = DateTime.Now;
        _callStopwatch.Restart();
        CallStarted?.Invoke(CurrentCall);
    }

    public void EndSimulateCall()
    {
        if (CurrentCall.State == WhatsAppCallState.Active)
        {
            TimeSpan d = _callStopwatch.Elapsed;
            _callStopwatch.Stop();
            CurrentCall.State = WhatsAppCallState.Ended;
            CallEnded?.Invoke(d);
            CurrentCall.State = WhatsAppCallState.None;
        }
    }
}
