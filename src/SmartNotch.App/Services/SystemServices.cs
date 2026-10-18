using System.Net.NetworkInformation;
using System.Windows.Threading;
using SmartNotch.App.Interop;

namespace SmartNotch.App.Services;

public sealed class BatteryService
{
    private readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool? _plugged;
    private bool _lowWarned;

    public bool HasBattery { get; private set; }
    public int Percent { get; private set; }
    public bool Plugged { get; private set; }

    public event Action<bool>? PlugChanged;
    public event Action<int>? LowBattery;

    public BatteryService()
    {
        Poll(true);
        _t.Tick += (_, _) => Poll(false);
        _t.Start();
    }

    private void Poll(bool initial)
    {
        if (!Win32.GetSystemPowerStatus(out var s)) return;
        HasBattery = s.BatteryFlag != 128 && s.BatteryFlag != 255;
        Percent = s.BatteryLifePercent == 255 ? 0 : s.BatteryLifePercent;
        Plugged = s.ACLineStatus == 1;

        if (!HasBattery) return;
        if (_plugged != null && _plugged != Plugged && !initial) PlugChanged?.Invoke(Plugged);
        _plugged = Plugged;

        if (!Plugged && Percent <= 20 && !_lowWarned) { _lowWarned = true; LowBattery?.Invoke(Percent); }
        if (Plugged || Percent > 25) _lowWarned = false;
    }
}

public sealed class StatsService
{
    private readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(1) };
    private ulong _pi, _pk, _pu;
    private long _prx, _ptx;
    private DateTime _pt = DateTime.MinValue;

    public double Cpu { get; private set; }
    public double Ram { get; private set; }
    public double DownKBs { get; private set; }
    public double UpKBs { get; private set; }
    public List<double> CpuHist { get; } = new();
    public List<double> NetHist { get; } = new();

    public event Action? Updated;

    public StatsService()
    {
        Sample();
        _t.Tick += (_, _) => Sample();
        _t.Start();
    }

    private void Sample()
    {
        if (Win32.GetSystemTimes(out var i, out var k, out var u))
        {
            if (_pk != 0)
            {
                ulong di = i.Value - _pi, total = (k.Value - _pk) + (u.Value - _pu);
                Cpu = total == 0 ? 0 : (total - di) * 100.0 / total;
            }
            _pi = i.Value; _pk = k.Value; _pu = u.Value;
        }

        var m = new Win32.MEMORYSTATUSEX();
        if (Win32.GlobalMemoryStatusEx(m)) Ram = m.dwMemoryLoad;

        try
        {
            long rx = 0, tx = 0;
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up ||
                    n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var st = n.GetIPv4Statistics();
                rx += st.BytesReceived; tx += st.BytesSent;
            }
            var now = DateTime.UtcNow;
            if (_pt != DateTime.MinValue)
            {
                var dt = Math.Max(0.1, (now - _pt).TotalSeconds);
                DownKBs = Math.Max(0, (rx - _prx) / 1024.0 / dt);
                UpKBs = Math.Max(0, (tx - _ptx) / 1024.0 / dt);
            }
            _prx = rx; _ptx = tx; _pt = now;
        }
        catch { }

        Push(CpuHist, Cpu);
        Push(NetHist, DownKBs + UpKBs);
        Updated?.Invoke();
    }

    private static void Push(List<double> l, double v)
    {
        l.Add(v);
        if (l.Count > 40) l.RemoveAt(0);
    }
}

public sealed class PomodoroService
{
    private readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(1) };
    public TimeSpan Total { get; private set; } = TimeSpan.FromMinutes(25);
    public TimeSpan Remaining { get; private set; } = TimeSpan.FromMinutes(25);
    public bool Running => _t.IsEnabled;

    public event Action? Tick;
    public event Action? Finished;

    public PomodoroService()
    {
        _t.Tick += (_, _) =>
        {
            Remaining -= TimeSpan.FromSeconds(1);
            if (Remaining <= TimeSpan.Zero)
            {
                _t.Stop();
                Remaining = Total;
                Tick?.Invoke();
                Finished?.Invoke();
                return;
            }
            Tick?.Invoke();
        };
    }

    public void SetMinutes(int min) { _t.Stop(); Total = Remaining = TimeSpan.FromMinutes(min); Tick?.Invoke(); }
    public void Toggle() { if (_t.IsEnabled) _t.Stop(); else _t.Start(); Tick?.Invoke(); }
    public void Reset() { _t.Stop(); Remaining = Total; Tick?.Invoke(); }

    public static string Format(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
}

public sealed class WaterReminderService
{
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromMinutes(45) };
    public int IntakeMl { get; private set; } = 500;
    public int GoalMl { get; private set; } = 2000;

    public event Action? Changed;
    public event Action? NeedWater;

    public WaterReminderService()
    {
        _reminderTimer.Tick += (_, _) => NeedWater?.Invoke();
        _reminderTimer.Start();
    }

    public void Drink(int ml = 250)
    {
        IntakeMl += ml;
        Changed?.Invoke();
    }

    public void Reset()
    {
        IntakeMl = 0;
        Changed?.Invoke();
    }
}
