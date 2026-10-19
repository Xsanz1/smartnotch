using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace SmartNotch.App.Services;

public sealed class NotifItem
{
    public string App { get; init; } = "";
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public DateTime Time { get; init; } = DateTime.Now;
}

/// <summary>Notifikasi Windows lewat UserNotificationListener. Butuh izin dan (umumnya) package identity.</summary>
public sealed class NotificationService
{
    private readonly HashSet<uint> _seen = new();
    private readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(3) };
    private UserNotificationListener? _l;
    private WpnNotificationReader? _wpn;

    public string Status { get; private set; } = "Memeriksa izin...";
    public List<NotifItem> Items { get; } = new();

    public event Action<NotifItem>? Received;
    public event Action? Changed;

    public async Task InitAsync()
    {
        try
        {
            _l = UserNotificationListener.Current;
            var access = await _l.RequestAccessAsync();
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                Log.Info($"UserNotificationListener: {access}, memakai pembaca database notifikasi");
                StartFallback();
                return;
            }
            Status = "Aktif";
            await PollAsync(true);
            _t.Tick += async (_, _) => await PollAsync(false);
            _t.Start();
        }
        catch (Exception ex)
        {
            Log.Warn("UserNotificationListener tidak tersedia, memakai pembaca database notifikasi", ex);
            StartFallback();
        }
    }

    private void StartFallback()
    {
        if (_wpn != null) return;
        _wpn = new WpnNotificationReader();
        _wpn.Received += n => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Push(n));
        _wpn.Start();
        Status = "Aktif (membaca database notifikasi Windows)";
        Changed?.Invoke();
    }

    private void Push(NotifItem item)
    {
        Items.Insert(0, item);
        if (Items.Count > 20) Items.RemoveAt(Items.Count - 1);
        Changed?.Invoke();
        Received?.Invoke(item);
    }

    private async Task PollAsync(bool initial)
    {
        try
        {
            if (_l == null) return;
            var list = await _l.GetNotificationsAsync(NotificationKinds.Toast);
            foreach (var n in list.OrderBy(x => x.CreationTime))
            {
                if (!_seen.Add(n.Id) || initial) continue;
                var binding = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                var texts = binding?.GetTextElements().Select(t => t.Text).ToList() ?? new List<string>();
                var item = new NotifItem
                {
                    App = n.AppInfo?.DisplayInfo?.DisplayName ?? "Aplikasi",
                    Title = texts.ElementAtOrDefault(0) ?? "",
                    Body = texts.ElementAtOrDefault(1) ?? ""
                };
                Items.Insert(0, item);
                if (Items.Count > 20) Items.RemoveAt(Items.Count - 1);
                Changed?.Invoke();
                Received?.Invoke(item);
            }
        }
        catch (Exception ex) { Log.Warn("Polling notifikasi gagal", ex); }
    }
}

public sealed record CalEvent(string Title, DateTime Start);

/// <summary>Agenda dibaca dari %LOCALAPPDATA%\SmartNotch\events.json.</summary>
public sealed class CalendarService
{
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartNotch");
    public static string FilePath => Path.Combine(Dir, "events.json");

    private readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly HashSet<string> _reminded = new();

    public event Action<CalEvent, int>? Reminder;

    public CalendarService()
    {
        EnsureFile();
        _t.Tick += (_, _) => CheckReminders();
        _t.Start();
    }

    public static void EnsureFile()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            if (!File.Exists(FilePath))
            {
                var sample = new[] { new { title = "Contoh acara (edit events.json)", start = DateTime.Today.AddDays(1).AddHours(9).ToString("yyyy-MM-ddTHH:mm") } };
                File.WriteAllText(FilePath, JsonSerializer.Serialize(sample, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { }
    }

    public List<CalEvent> Load()
    {
        var res = new List<CalEvent>();
        try
        {
            EnsureFile();
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath),
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var title = el.GetProperty("title").GetString() ?? "";
                var start = el.GetProperty("start").GetDateTime();
                res.Add(new CalEvent(title, start));
            }
        }
        catch (Exception ex) { Log.Warn("events.json tidak dapat dibaca", ex); }
        return res.OrderBy(e => e.Start).ToList();
    }

    private void CheckReminders()
    {
        var now = DateTime.Now;
        foreach (var e in Load())
        {
            var mins = (int)Math.Ceiling((e.Start - now).TotalMinutes);
            var key = e.Title + e.Start.Ticks;
            if (mins is >= 0 and <= 5 && _reminded.Add(key)) Reminder?.Invoke(e, mins);
        }
    }
}
