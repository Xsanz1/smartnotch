using SmartNotch.App.Services;
using Xunit;

namespace SmartNotch.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sn-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var s = SettingsService.Load(FilePath);
        Assert.Equal(25, s.PomodoroMinutes);
        Assert.Equal(-1, s.MonitorIndex);
        Assert.True(s.HideOnFullscreen);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var s = new AppSettings { PomodoroMinutes = 40, ShowWater = false, HotkeyKey = "k", MonitorIndex = 1 };
        Assert.True(SettingsService.Save(s, FilePath));

        var loaded = SettingsService.Load(FilePath);
        Assert.Equal(40, loaded.PomodoroMinutes);
        Assert.False(loaded.ShowWater);
        Assert.Equal("K", loaded.HotkeyKey);
        Assert.Equal(1, loaded.MonitorIndex);
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        var s = SettingsService.Load(FilePath);
        Assert.Equal(25, s.PomodoroMinutes);
    }

    [Fact]
    public void Normalize_ClampsOutOfRangeValues()
    {
        var s = new AppSettings { PomodoroMinutes = 0, WaterIntervalMinutes = 9999, WaterGoalMl = 1, CollapseDelayMs = 5, HotkeyKey = "!!" };
        s.Normalize();
        Assert.Equal(1, s.PomodoroMinutes);
        Assert.Equal(480, s.WaterIntervalMinutes);
        Assert.Equal(250, s.WaterGoalMl);
        Assert.Equal(100, s.CollapseDelayMs);
        Assert.Equal("N", s.HotkeyKey);
    }

    [Fact]
    public void Normalize_KeepsAtLeastOneWidget()
    {
        var s = new AppSettings { ShowMusic = false, ShowCall = false, ShowShelf = false, ShowNotif = false, ShowTimer = false, ShowWater = false, ShowSystem = false };
        s.Normalize();
        Assert.True(s.ShowMusic);
    }

    [Fact]
    public void Normalize_CleansCallApps()
    {
        var s = new AppSettings { CallApps = new() { " Zoom ", "zoom", "", "Teams" } };
        s.Normalize();
        Assert.Equal(new[] { "Zoom", "Teams" }, s.CallApps);
    }
}

public class WaterTrackerTests
{
    [Fact]
    public void Drink_Accumulates()
    {
        var s = new AppSettings();
        var day = new DateTime(2026, 1, 5, 9, 0, 0);
        var t = new WaterTracker(s, day);
        t.Drink(250, day);
        t.Drink(500, day);
        Assert.Equal(750, t.IntakeMl);
    }

    [Fact]
    public void NewDay_ResetsIntake()
    {
        var s = new AppSettings();
        var d1 = new DateTime(2026, 1, 5, 20, 0, 0);
        var t = new WaterTracker(s, d1);
        t.Drink(1000, d1);

        var d2 = d1.AddDays(1);
        Assert.True(t.RolloverIfNewDay(d2));
        Assert.Equal(0, t.IntakeMl);
    }

    [Fact]
    public void SameDay_SurvivesRestart()
    {
        var s = new AppSettings();
        var day = new DateTime(2026, 1, 5, 8, 0, 0);
        new WaterTracker(s, day).Drink(750, day);

        var restarted = new WaterTracker(s, day.AddHours(5));
        Assert.Equal(750, restarted.IntakeMl);
    }

    [Fact]
    public void Reset_ZeroesIntake()
    {
        var s = new AppSettings();
        var t = new WaterTracker(s);
        t.Drink(300);
        t.Reset();
        Assert.Equal(0, t.IntakeMl);
    }
}

public class CallAppMatcherTests
{
    private static readonly string[] Apps = { "WhatsApp", "Zoom", "Teams" };

    [Theory]
    [InlineData("5319275A.WhatsAppDesktop_cv1g1gvanyjgm", "WhatsApp")]
    [InlineData("C:#Users#me#AppData#Roaming#Zoom#bin#Zoom.exe", "Zoom")]
    [InlineData("MSTeams_8wekyb3d8bbwe", "Teams")]
    public void Match_FindsKnownApps(string name, string expected) =>
        Assert.Equal(expected, CallAppMatcher.Match(name, Apps));

    [Theory]
    [InlineData("chrome")]
    [InlineData("")]
    [InlineData(null)]
    public void Match_IgnoresOthers(string? name) =>
        Assert.Null(CallAppMatcher.Match(name, Apps));

    [Fact]
    public void Display_CapitalizesFirstLetter() =>
        Assert.Equal("Zoom", CallAppMatcher.Display("zoom"));
}

public class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("V2.0.0-beta.1", 2, 0, 0)]
    public void ParseTag_Valid(string tag, int major, int minor, int build)
    {
        var v = UpdateService.ParseTag(tag);
        Assert.NotNull(v);
        Assert.Equal(major, v!.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(Math.Max(0, build), Math.Max(0, v.Build));
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData(null)]
    public void ParseTag_Invalid(string? tag) => Assert.Null(UpdateService.ParseTag(tag));

    [Fact]
    public void IsNewer_ComparesIgnoringRevision()
    {
        Assert.True(UpdateService.IsNewer(new Version(1, 2, 0), new Version(1, 1, 0, 0)));
        Assert.False(UpdateService.IsNewer(new Version(1, 1, 0), new Version(1, 1, 0, 0)));
        Assert.False(UpdateService.IsNewer(new Version(1, 0), new Version(1, 1, 0, 0)));
    }
}

public class LogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sn-log-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Error_WritesFileWithException()
    {
        Log.Dir = _dir;
        Log.Error("uji-error-unik", new InvalidOperationException("boom"));

        var text = File.ReadAllText(Log.CurrentFile);
        Assert.Contains("uji-error-unik", text);
        Assert.Contains("boom", text);
    }
}

public class AppLauncherTests
{
    [Theory]
    [InlineData("WhatsApp", "whatsapp:")]
    [InlineData("Telegram", "tg:")]
    [InlineData("Discord", "discord:")]
    [InlineData("Spotify", "spotify:")]
    [InlineData("Teams", "msteams:")]
    public void GetUriScheme_MatchesKnownApps(string app, string expectedUri)
    {
        var uri = AppLauncherService.GetUriScheme(app);
        Assert.Equal(expectedUri, uri);
    }

    [Fact]
    public void GetCandidateProcessNames_ContainsExpectedBinaries()
    {
        var wa = AppLauncherService.GetCandidateProcessNames("WhatsApp");
        Assert.Contains("WhatsApp", wa);
        Assert.Contains("WhatsApp.Root", wa);

        var discord = AppLauncherService.GetCandidateProcessNames("Discord");
        Assert.Contains("Discord", discord);
    }

    [Fact]
    public void NotifItem_StoresPrimaryId()
    {
        var item = new NotifItem
        {
            App = "WhatsApp",
            PrimaryId = "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App",
            Title = "John",
            Body = "Hello"
        };
        Assert.Equal("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", item.PrimaryId);
    }
}
