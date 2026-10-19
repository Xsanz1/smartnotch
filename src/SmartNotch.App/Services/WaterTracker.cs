namespace SmartNotch.App.Services;

/// <summary>Logika asupan air harian (tanpa timer/UI agar mudah diuji). Asupan di-reset otomatis tiap ganti hari.</summary>
public sealed class WaterTracker
{
    private readonly AppSettings _s;

    public WaterTracker(AppSettings s, DateTime? today = null)
    {
        _s = s;
        RolloverIfNewDay(today ?? DateTime.Now);
    }

    public int IntakeMl => _s.WaterIntakeMl;
    public int GoalMl => Math.Max(1, _s.WaterGoalMl);

    public bool RolloverIfNewDay(DateTime now)
    {
        var key = now.ToString("yyyy-MM-dd");
        if (_s.WaterDate == key) return false;
        _s.WaterDate = key;
        _s.WaterIntakeMl = 0;
        return true;
    }

    public void Drink(int ml, DateTime? now = null)
    {
        RolloverIfNewDay(now ?? DateTime.Now);
        _s.WaterIntakeMl = Math.Max(0, _s.WaterIntakeMl + ml);
    }

    public void Reset() => _s.WaterIntakeMl = 0;
}
