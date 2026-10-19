namespace SmartNotch.App.Services;

/// <summary>Mencocokkan nama proses / entri registry dengan daftar aplikasi panggilan (WhatsApp, Zoom, Teams, dst).</summary>
public static class CallAppMatcher
{
    /// <summary>Kembalikan kata kunci yang cocok (versi dari daftar), atau null.</summary>
    public static string? Match(string? name, IEnumerable<string> apps)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var a in apps)
            if (!string.IsNullOrWhiteSpace(a) && name.Contains(a, StringComparison.OrdinalIgnoreCase))
                return a;
        return null;
    }

    /// <summary>Nama tampilan: kata kunci yang cocok dengan huruf awal kapital.</summary>
    public static string Display(string app) =>
        string.IsNullOrEmpty(app) ? app : char.ToUpperInvariant(app[0]) + app[1..];
}
