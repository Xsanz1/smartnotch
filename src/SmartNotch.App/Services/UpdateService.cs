using System.Net.Http;
using System.Text.Json;

namespace SmartNotch.App.Services;

public sealed record UpdateInfo(Version Latest, string Url, string Tag);

/// <summary>Cek rilis terbaru di GitHub Releases (hanya memberi tahu; pengguna mengunduh sendiri).</summary>
public static class UpdateService
{
    public const string Repo = "Xsanz1/smartnotch";
    public static string ReleasesPage => $"https://github.com/{Repo}/releases/latest";

    public static Version Current =>
        typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0);

    /// <summary>Parse tag seperti "v1.2.3" / "1.2". Null jika tidak valid.</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var t = tag.Trim().TrimStart('v', 'V');
        var dash = t.IndexOfAny(new[] { '-', '+' });
        if (dash >= 0) t = t[..dash];
        return Version.TryParse(t, out var v) ? v : null;
    }

    public static bool IsNewer(Version latest, Version current) =>
        Normalize(latest) > Normalize(current);

    private static Version Normalize(Version v) =>
        new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    /// <summary>Kembalikan info rilis jika ada versi yang lebih baru, selain itu null. Kegagalan jaringan dicatat dan menghasilkan null.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SmartNotch-UpdateCheck");
            var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest", ct);
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString();
            var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            var v = ParseTag(tag);
            if (v == null || !IsNewer(v, Current)) return null;
            return new UpdateInfo(v, url ?? ReleasesPage, tag ?? v.ToString());
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null; // belum ada rilis di GitHub
        }
        catch (Exception ex)
        {
            Log.Warn("Cek pembaruan gagal", ex);
            return null;
        }
    }
}
