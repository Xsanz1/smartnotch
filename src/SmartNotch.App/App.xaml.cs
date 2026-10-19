using System.Windows;
using Microsoft.Win32;
using SmartNotch.App.Services;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace SmartNotch.App;

public partial class App : Application
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private Mutex? _mutex;
    private Forms.NotifyIcon? _tray;
    private string? _updateUrl;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "SmartNotch.SingleInstance", out bool created);
        if (!created) { Shutdown(); return; }

        Log.Cleanup();
        SettingsService.Init();
        HookGlobalErrorHandlers();
        Log.Info($"SmartNotch {UpdateService.Current} dimulai");

        base.OnStartup(e);
        BuildTray();
        if (SettingsService.Current.CheckUpdates) _ = CheckUpdateAsync();
    }

    private void HookGlobalErrorHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Exception UI tidak tertangani", args.Exception);
            args.Handled = true; // jangan matikan notch karena error satu widget
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error($"Exception fatal (terminating={args.IsTerminating})", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Task exception tidak teramati", args.Exception);
            args.SetObserved();
        };
    }

    private async Task CheckUpdateAsync()
    {
        var info = await UpdateService.CheckAsync();
        if (info == null || _tray == null) return;
        _updateUrl = info.Url;
        _tray.ShowBalloonTip(8000, "SmartNotch", $"Versi {info.Tag} tersedia. Klik untuk membuka halaman unduhan.", Forms.ToolTipIcon.Info);
    }

    private void BuildTray()
    {
        var settings = new Forms.ToolStripMenuItem("Pengaturan...");
        settings.Click += (_, _) => SettingsWindow.ShowSingle();

        var autostart = new Forms.ToolStripMenuItem("Mulai bersama Windows") { Checked = IsAutostart(), CheckOnClick = true };
        autostart.CheckedChanged += (_, _) => SetAutostart(autostart.Checked);

        var toggle = new Forms.ToolStripMenuItem("Tampilkan / Sembunyikan");
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
        menu.Items.AddRange(new Forms.ToolStripItem[] { toggle, settings, autostart, openFolder, new Forms.ToolStripSeparator(), quit });
        menu.Opening += (_, _) =>
        {
            toggle.Text = $"Tampilkan / Sembunyikan  (Ctrl+Alt+{SettingsService.Current.HotkeyKey})";
            autostart.Checked = IsAutostart();
        };

        _tray = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "SmartNotch",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => NotchWindow.Instance?.ToggleVisible();
        _tray.BalloonTipClicked += (_, _) =>
        {
            if (_updateUrl == null) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_updateUrl) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Warn("Gagal membuka halaman pembaruan", ex); }
        };
    }

    internal static bool IsAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("SmartNotch") != null;
    }

    internal static void SetAutostart(bool on)
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
        Log.Info("SmartNotch berhenti");
        base.OnExit(e);
    }
}
