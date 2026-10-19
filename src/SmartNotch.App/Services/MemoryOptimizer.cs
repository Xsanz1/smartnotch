using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmartNotch.App.Services;

/// <summary>
/// Mengoptimalkan penggunaan RAM dengan memicu Garbage Collection dan memangkas Working Set (EmptyWorkingSet),
/// sehingga konsumsi memori idle SmartNotch tetap sangat hemat (~35-45 MB).
/// </summary>
public static class MemoryOptimizer
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    public static void Trim()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        catch (Exception ex)
        {
            Log.Warn("Gagal memangkas working set", ex);
        }
    }

    public static void ScheduleDelayedTrim(int delayMs = 3000)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs);
                Trim();
            }
            catch { }
        });
    }
}
