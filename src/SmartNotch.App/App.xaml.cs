using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace SmartNotch.App;

public partial class App : Application
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private Mutex? _mutex;
    private Forms.NotifyIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "SmartNotch.SingleInstance", out bool created);
        if (!created) { Shutdown(); return; }

        base.OnStartup(e);
        BuildTray();
    }

    private void BuildTray()
    {
        var autostart = new Forms.ToolStripMenuItem("Mulai bersama Windows") { Checked = IsAutostart(), CheckOnClick = true };
        autostart.CheckedChanged += (_, _) => SetAutostart(autostart.Checked);

        var toggle = new Forms.ToolStripMenuItem("Tampilkan / Sembunyikan  (Ctrl+Alt+N)");
        toggle.Click += (_, _) => NotchWindow.Instance?.ToggleVisible();

        var openFolder = new Forms.ToolStripMenuItem("Buka folder data");
        openFolder.Click += (_, _) =>
        {
            Services.CalendarService.EnsureFile();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Services.CalendarService.Dir) { UseShellExecute = true });
        };

        var quit = new Forms.ToolStripMenuItem("Keluar");
        quit.Click += (_, _) => Shutdown();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange(new Forms.ToolStripItem[] { toggle, autostart, openFolder, new Forms.ToolStripSeparator(), quit });

        _tray = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "SmartNotch",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => NotchWindow.Instance?.ToggleVisible();
    }

    private static bool IsAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("SmartNotch") != null;
    }

    private static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (k == null) return;
        if (on) k.SetValue("SmartNotch", $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue("SmartNotch", false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
