using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartNotch.App.Services;

public sealed record LyricLine(TimeSpan Time, string Text);

/// <summary>Mengambil lirik tersinkron (format LRC) dari layanan publik LRCLIB. Lirik tidak disimpan di aplikasi.</summary>
public sealed class LyricsService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Regex LineRx = new(@"^\[(\d+):(\d+(?:\.\d+)?)\]\s*(.*)$", RegexOptions.Compiled);
    private readonly Dictionary<string, List<LyricLine>?> _cache = new();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { BaseAddress = new Uri("https://lrclib.net/"), Timeout = TimeSpan.FromSeconds(8) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("SmartNotch/1.0 (Windows notch app)");
        return c;
    }

    public async Task<List<LyricLine>?> GetAsync(string title, string artist, double durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var key = title + "|" + artist;
        if (_cache.TryGetValue(key, out var hit)) return hit;

        List<LyricLine>? result = null;
        try
        {
            string? synced = null;
            var url = $"api/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
            if (durationSeconds > 0) url += $"&duration={(int)Math.Round(durationSeconds)}";

            using (var resp = await Http.GetAsync(url))
            {
                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    synced = Synced(doc.RootElement);
                }
            }

            if (string.IsNullOrEmpty(synced))
            {
                using var resp = await Http.GetAsync($"api/search?q={Uri.EscapeDataString((title + " " + artist).Trim())}");
                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        synced = Synced(el);
                        if (!string.IsNullOrEmpty(synced)) break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(synced)) result = Parse(synced);
        }
        catch { /* offline / diblokir: tampilkan tanpa lirik */ }

        _cache[key] = result;
        return result;
    }

    private static string? Synced(JsonElement el) =>
        el.TryGetProperty("syncedLyrics", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static List<LyricLine>? Parse(string lrc)
    {
        var list = new List<LyricLine>();
        foreach (var raw in lrc.Split('\n'))
        {
            var m = LineRx.Match(raw.Trim());
            if (!m.Success) continue;
            var t = TimeSpan.FromMinutes(int.Parse(m.Groups[1].Value)) +
                    TimeSpan.FromSeconds(double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            var text = m.Groups[3].Value.Trim();
            list.Add(new LyricLine(t, text.Length == 0 ? "♪" : text));
        }
        return list.Count == 0 ? null : list.OrderBy(l => l.Time).ToList();
    }
}
