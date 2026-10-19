using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;

namespace SmartNotch.App.Services;

/// <summary>
/// Cadangan untuk aplikasi non-paket (tanpa package identity), yang tidak boleh memakai UserNotificationListener.
/// Membaca toast yang baru masuk dari database notifikasi Windows (wpndatabase.db) pada salinan sementara,
/// sehingga isi pesan (mis. WhatsApp: pengirim + teks) bisa ditampilkan di notch.
/// </summary>
public sealed class WpnNotificationReader : IDisposable
{
    private readonly string _src = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "Windows", "Notifications", "wpndatabase.db");
    private readonly string _tmpDir = Path.Combine(Path.GetTempPath(), "SmartNotch-wpn");
    private CancellationTokenSource? _cts;
    private long _lastOrder = -1;

    /// <summary>Dipicu dari thread latar belakang.</summary>
    public event Action<NotifItem>? Received;

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { Poll(); }
                catch (Exception ex) { Log.Warn("Membaca database notifikasi gagal", ex); }
                try { await Task.Delay(1500, ct); } catch (OperationCanceledException) { break; }
            }
        }, ct);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private void Poll()
    {
        if (!File.Exists(_src)) return;

        Directory.CreateDirectory(_tmpDir);
        var dst = Path.Combine(_tmpDir, "wpn.db");
        CopyShared(_src, dst, required: true);
        CopyShared(_src + "-wal", dst + "-wal", required: false);
        CopyShared(_src + "-shm", dst + "-shm", required: false);

        using var con = new SqliteConnection($"Data Source={dst};Mode=ReadWrite;Pooling=False");
        con.Open();

        if (_lastOrder < 0)
        {
            // Mulai dari sekarang: jangan putar ulang notifikasi lama
            using var max = con.CreateCommand();
            max.CommandText = "SELECT COALESCE(MAX([Order]), 0) FROM Notification";
            _lastOrder = Convert.ToInt64(max.ExecuteScalar() ?? 0);
            return;
        }

        using var cmd = con.CreateCommand();
        cmd.CommandText =
            "SELECT n.[Order], h.PrimaryId, n.Payload FROM Notification n " +
            "LEFT JOIN NotificationHandler h ON n.HandlerId = h.RecordId " +
            "WHERE n.[Order] > $o ORDER BY n.[Order]";
        cmd.Parameters.AddWithValue("$o", _lastOrder);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _lastOrder = Math.Max(_lastOrder, r.GetInt64(0));
            var item = Parse(r.IsDBNull(1) ? "" : r.GetString(1), r.IsDBNull(2) ? null : r.GetValue(2));
            if (item != null) Received?.Invoke(item);
        }
    }

    private static void CopyShared(string from, string to, bool required)
    {
        try
        {
            if (!File.Exists(from))
            {
                if (File.Exists(to)) File.Delete(to);
                return;
            }
            using var inp = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var outp = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None);
            inp.CopyTo(outp);
        }
        catch when (!required) { }
    }

    internal static NotifItem? Parse(string primaryId, object? payload)
    {
        try
        {
            string xml = payload switch
            {
                byte[] b => Encoding.UTF8.GetString(b),
                string s => s,
                _ => ""
            };
            xml = xml.TrimStart('\uFEFF', '\0', ' ', '\r', '\n');
            if (!xml.StartsWith("<toast", StringComparison.OrdinalIgnoreCase)) return null;

            var texts = XDocument.Parse(xml).Descendants("text")
                .Select(t => t.Value.Trim()).Where(s => s.Length > 0).ToList();
            if (texts.Count == 0) return null;

            return new NotifItem
            {
                App = FriendlyApp(primaryId),
                PrimaryId = primaryId,
                Title = texts[0],
                Body = string.Join(" ", texts.Skip(1))
            };
        }
        catch { return null; }
    }

    internal static string FriendlyApp(string primaryId)
    {
        if (string.IsNullOrWhiteSpace(primaryId)) return "Aplikasi";
        if (primaryId.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase)) return "WhatsApp";

        var id = primaryId;
        var bang = id.IndexOf('!');
        if (bang >= 0) id = id[..bang];
        var us = id.IndexOf('_');
        if (us >= 0) id = id[..us];
        var dot = id.LastIndexOf('.');
        if (dot >= 0 && dot < id.Length - 1) id = id[(dot + 1)..];
        return id.Length == 0 ? "Aplikasi" : id;
    }
}
