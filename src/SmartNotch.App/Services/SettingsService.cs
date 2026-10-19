using System.IO;
using System.Text.Json;

namespace SmartNotch.App.Services;

/// <summary>Seluruh preferensi pengguna. Disimpan sebagai JSON.</summary>
public sealed class AppSettings
{
    // Widget yang ditampilkan di panel expanded
    public bool ShowMusic { get; set; } = true;
    public bool ShowCall { get; set; } = true;
    public bool ShowShelf { get; set; } = true;
    public bool ShowNotif { get; set; } = true;
    public bool ShowTimer { get; set; } = true;
    public bool ShowWater { get; set; } = true;
    public bool ShowSystem { get; set; } = true;

    // Timer fokus
    public int PomodoroMinutes { get; set; } = 25;
    public bool TimerSound { get; set; } = true;

    // Pengingat minum (asupan disimpan per hari)
    public int WaterIntervalMinutes { get; set; } = 45;
    public int WaterGoalMl { get; set; } = 2000;
    public int WaterIntakeMl { get; set; }
    public string WaterDate { get; set; } = "";

    // Tampilan / perilaku
    /// <summary>-1 = monitor utama, selain itu indeks pada Screen.AllScreens.</summary>
    public int MonitorIndex { get; set; } = -1;
    public bool HideOnFullscreen { get; set; } = true;
    public int CollapseDelayMs { get; set; } = 400;
    /// <summary>Huruf/angka untuk hotkey Ctrl+Alt+[Key].</summary>
    public string HotkeyKey { get; set; } = "N";
    public bool ShowNotificationPeek { get; set; } = true;

    // Deteksi panggilan (cocokkan dengan nama proses / entri registry mikrofon)
    public List<string> CallApps { get; set; } = new() { "WhatsApp", "Zoom", "Teams", "Discord", "Skype" };

    // Pembaruan
    public bool CheckUpdates { get; set; } = true;

    /// <summary>Rapikan nilai di luar rentang agar aman dipakai.</summary>
    public void Normalize()
    {
        PomodoroMinutes = Math.Clamp(PomodoroMinutes, 1, 180);
        WaterIntervalMinutes = Math.Clamp(WaterIntervalMinutes, 1, 480);
        WaterGoalMl = Math.Clamp(WaterGoalMl, 250, 10000);
        WaterIntakeMl = Math.Max(0, WaterIntakeMl);
        MonitorIndex = Math.Max(-1, MonitorIndex);
        CollapseDelayMs = Math.Clamp(CollapseDelayMs, 100, 3000);

        var key = (HotkeyKey ?? "").Trim().ToUpperInvariant();
        HotkeyKey = key.Length == 1 && char.IsLetterOrDigit(key[0]) ? key : "N";

        CallApps = (CallApps ?? new()).Select(a => a.Trim()).Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Minimal satu widget harus aktif
        if (!(ShowMusic || ShowCall || ShowShelf || ShowNotif || ShowTimer || ShowWater || ShowSystem)) ShowMusic = true;
    }
}

/// <summary>Muat/simpan <see cref="AppSettings"/> dari %LOCALAPPDATA%\SmartNotch\settings.json.</summary>
public static class SettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly object Gate = new();

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartNotch");
    public static string DefaultPath => Path.Combine(Dir, "settings.json");

    public static AppSettings Current { get; private set; } = new();

    /// <summary>Dipicu setelah <see cref="Apply"/> dipanggil.</summary>
    public static event Action<AppSettings>? Changed;

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        AppSettings s;
        try
        {
            s = File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Warn("Gagal membaca settings, memakai default", ex);
            s = new AppSettings();
        }
        s.Normalize();
        return s;
    }

    public static bool Save(AppSettings s, string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            s.Normalize();
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
                File.Move(tmp, path, true);
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Gagal menyimpan settings", ex);
            return false;
        }
    }

    public static void Init()
    {
        Current = Load();
        if (!File.Exists(DefaultPath)) Save(Current);
    }

    /// <summary>Jadikan <paramref name="s"/> pengaturan aktif, simpan, lalu beritahu pendengar.</summary>
    public static void Apply(AppSettings s)
    {
        s.Normalize();
        Current = s;
        Save(s);
        Changed?.Invoke(s);
    }

    /// <summary>Simpan tanpa memicu <see cref="Changed"/> (mis. untuk asupan air).</summary>
    public static void SaveCurrent() => Save(Current);
}
