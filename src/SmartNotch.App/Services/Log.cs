using System.IO;
using System.Text;

namespace SmartNotch.App.Services;

/// <summary>Logger file sederhana (thread-safe) di %LOCALAPPDATA%\SmartNotch\logs. Tidak pernah melempar exception.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _dir;
    private const int KeepDays = 7;

    public static string Dir
    {
        get => _dir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartNotch", "logs");
        set => _dir = value;
    }

    public static string CurrentFile => Path.Combine(Dir, $"smartnotch-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string msg) => Write("INFO", msg, null);
    public static void Warn(string msg, Exception? ex = null) => Write("WARN", msg, ex);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);

    private static string _lastMsg = "";
    private static DateTime _lastAt = DateTime.MinValue;

    private static void Write(string level, string msg, Exception? ex)
    {
        try
        {
            lock (Gate)
            {
                var key = level + msg;
                if (key == _lastMsg && (DateTime.UtcNow - _lastAt).TotalSeconds < 60) return;
                _lastMsg = key; _lastAt = DateTime.UtcNow;
            }

            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(" [").Append(level).Append("] ").Append(msg);
            if (ex != null) sb.AppendLine().Append(ex);
            sb.AppendLine();

            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(CurrentFile, sb.ToString(), Encoding.UTF8);
            }
        }
        catch { /* logging tidak boleh menjatuhkan aplikasi */ }
    }

    /// <summary>Hapus log yang lebih lama dari <see cref="KeepDays"/> hari.</summary>
    public static void Cleanup()
    {
        try
        {
            if (!Directory.Exists(Dir)) return;
            var limit = DateTime.Now.AddDays(-KeepDays);
            foreach (var f in Directory.GetFiles(Dir, "smartnotch-*.log"))
                if (File.GetLastWriteTime(f) < limit) File.Delete(f);
        }
        catch { }
    }
}
