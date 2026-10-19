using System.Diagnostics;
using System.IO;
using System.Text;
using SmartNotch.App.Interop;

namespace SmartNotch.App.Services;

/// <summary>
/// Layanan untuk memfokuskan jendela aplikasi notifikasi (mis. WhatsApp, Telegram, Discord, dll.)
/// ke layar depan atau menjalankannya jika aplikasi belum terbuka / terminimalkan ke tray.
/// </summary>
public static class AppLauncherService
{
    public static bool FocusOrLaunch(NotifItem notif)
    {
        return FocusOrLaunch(notif.App, notif.PrimaryId);
    }

    public static bool FocusOrLaunch(string appName, string? primaryId = null)
    {
        try
        {
            Log.Info($"Mengarahkan ke aplikasi notifikasi: App='{appName}', PrimaryId='{primaryId}'");

            // 1. Coba cari jendela yang sudah terbuka dan bawa ke depan
            if (TryBringWindowToFront(appName, primaryId))
            {
                return true;
            }

            // 2. Jika jendela belum ada / app di background / tray / belum jalan, coba buka via URI Scheme
            if (TryLaunchUriScheme(appName))
            {
                WaitAndFocusAsync(appName, primaryId);
                return true;
            }

            // 3. Coba buka via AUMID (Windows Store App / Package Family Name)
            if (!string.IsNullOrWhiteSpace(primaryId))
            {
                if (TryLaunchAumid(primaryId))
                {
                    WaitAndFocusAsync(appName, primaryId);
                    return true;
                }
            }

            // 4. Coba jalankan executable yang cocok
            if (TryLaunchKnownProcess(appName))
            {
                WaitAndFocusAsync(appName, primaryId);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Gagal mengarahkan ke aplikasi '{appName}'", ex);
        }
        return false;
    }

    private static void WaitAndFocusAsync(string appName, string? primaryId)
    {
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 6; i++)
            {
                await Task.Delay(500);
                if (TryBringWindowToFront(appName, primaryId)) break;
            }
        });
    }

    public static bool TryBringWindowToFront(string appName, string? primaryId)
    {
        var targetProcesses = GetCandidateProcessNames(appName);
        IntPtr foundHwnd = IntPtr.Zero;

        // A. Cek MainWindowHandle dari proses yang sesuai
        foreach (var pName in targetProcesses)
        {
            try
            {
                var procs = Process.GetProcessesByName(pName);
                foreach (var p in procs)
                {
                    if (p.MainWindowHandle != IntPtr.Zero && Win32.IsWindowVisible(p.MainWindowHandle))
                    {
                        foundHwnd = p.MainWindowHandle;
                        break;
                    }
                }
            }
            catch { }
            if (foundHwnd != IntPtr.Zero) break;
        }

        // B. Jika belum ketemu, enumerasi seluruh top-level windows yang cocok dengan judul atau PID proses kandidat
        if (foundHwnd == IntPtr.Zero)
        {
            var pids = new HashSet<int>();
            foreach (var pName in targetProcesses)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(pName))
                        pids.Add(p.Id);
                }
                catch { }
            }

            var sb = new StringBuilder(256);
            Win32.EnumWindows((h, _) =>
            {
                if (!Win32.IsWindowVisible(h)) return true;

                Win32.GetWindowThreadProcessId(h, out uint pid);
                if (pids.Contains((int)pid))
                {
                    foundHwnd = h;
                    return false; // Berhenti mencari
                }

                sb.Clear();
                Win32.GetWindowText(h, sb, sb.Capacity);
                string title = sb.ToString();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    if (title.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                        (appName.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase) && title.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase)))
                    {
                        foundHwnd = h;
                        return false;
                    }
                }

                return true;
            }, IntPtr.Zero);
        }

        if (foundHwnd != IntPtr.Zero)
        {
            ActivateWindow(foundHwnd);
            return true;
        }

        return false;
    }

    public static void ActivateWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            if (Win32.IsIconic(hwnd))
            {
                Win32.ShowWindow(hwnd, 9 /* SW_RESTORE */);
            }
            else
            {
                Win32.ShowWindow(hwnd, 5 /* SW_SHOW */);
            }

            Win32.BringWindowToTop(hwnd);

            // Trik simulasi tombol Alt (VK_MENU) agar Windows mengizinkan pergantian foreground process
            Win32.keybd_event(0x12 /* VK_MENU */, 0, 0, 0);
            Win32.keybd_event(0x12, 0, 2 /* KEYEVENTF_KEYUP */, 0);

            Win32.SetForegroundWindow(hwnd);
        }
        catch (Exception ex)
        {
            Log.Warn("Gagal memfokuskan jendela", ex);
        }
    }

    public static List<string> GetCandidateProcessNames(string appName)
    {
        var list = new List<string>();
        string clean = (appName ?? "").Trim();
        if (string.IsNullOrEmpty(clean)) return list;

        if (clean.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("WhatsApp");
            list.Add("WhatsApp.Root");
        }
        else if (clean.Contains("Telegram", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("Telegram");
            list.Add("TelegramDesktop");
        }
        else if (clean.Contains("Discord", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("Discord");
            list.Add("DiscordCanary");
            list.Add("DiscordPTB");
        }
        else if (clean.Contains("Teams", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("ms-teams");
            list.Add("Teams");
            list.Add("msteams");
        }
        else if (clean.Contains("Slack", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("slack");
        }
        else if (clean.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("Spotify");
        }
        else if (clean.Contains("Outlook", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("OUTLOOK");
            list.Add("olk");
        }
        else if (clean.Contains("Zoom", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("Zoom");
        }
        else if (clean.Contains("Skype", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("Skype");
        }
        else if (clean.Contains("Line", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("LINE");
        }

        if (!list.Contains(clean, StringComparer.OrdinalIgnoreCase))
            list.Add(clean);

        return list;
    }

    public static string? GetUriScheme(string appName)
    {
        if (appName.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase)) return "whatsapp:";
        if (appName.Contains("Telegram", StringComparison.OrdinalIgnoreCase)) return "tg:";
        if (appName.Contains("Discord", StringComparison.OrdinalIgnoreCase)) return "discord:";
        if (appName.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) return "spotify:";
        if (appName.Contains("Teams", StringComparison.OrdinalIgnoreCase)) return "msteams:";
        return null;
    }

    private static bool TryLaunchUriScheme(string appName)
    {
        string? uri = GetUriScheme(appName);
        if (uri != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                return true;
            }
            catch { }
        }
        return false;
    }

    private static bool TryLaunchAumid(string aumid)
    {
        try
        {
            if (File.Exists(aumid))
            {
                Process.Start(new ProcessStartInfo(aumid) { UseShellExecute = true });
                return true;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{aumid}",
                UseShellExecute = true
            });
            return true;
        }
        catch { return false; }
    }

    private static bool TryLaunchKnownProcess(string appName)
    {
        foreach (var p in GetCandidateProcessNames(appName))
        {
            try
            {
                Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
                return true;
            }
            catch { }
        }
        return false;
    }
}
