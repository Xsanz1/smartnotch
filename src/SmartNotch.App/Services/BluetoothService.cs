using System.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace SmartNotch.App.Services;

/// <summary>
/// Layanan pemantau koneksi Bluetooth (Headphone/AirPods/TWS, Speaker, Gamepad, Mouse, dll.)
/// Menampilkan notifikasi Peek saat perangkat terhubung atau terputus ala iOS Dynamic Island & Nusa.
/// </summary>
public sealed class BluetoothService
{
    private DeviceWatcher? _watcher;
    private readonly HashSet<string> _connectedDevices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _nameCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;
    private DateTime _lastAudioNotify = DateTime.MinValue;

    public event Action<string, bool, string>? DeviceConnectionChanged; // (name, isConnected, icon)

    public void Start()
    {
        try
        {
            // Filter perangkat Bluetooth yang terpasang / paired
            string aqs = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            _watcher = DeviceInformation.CreateWatcher(
                aqs,
                new[] { "System.Devices.Aep.IsConnected", "System.ItemNameDisplay" }
            );

            _watcher.Added += OnDeviceAdded;
            _watcher.Updated += OnDeviceUpdated;
            _watcher.Removed += OnDeviceRemoved;
            _watcher.EnumerationCompleted += (_, _) => _initialized = true;

            _watcher.Start();

            // Juga pantau pergantian default audio render device (Headset / Headphone / TWS)
            MediaDevice.DefaultAudioRenderDeviceChanged += OnDefaultAudioChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BluetoothService init error: {ex.Message}");
        }
    }

    private void OnDefaultAudioChanged(object? sender, DefaultAudioRenderDeviceChangedEventArgs args)
    {
        if (!_initialized) return;
        // Hindari duplikasi notifikasi dalam jendela 2 detik
        if ((DateTime.UtcNow - _lastAudioNotify).TotalSeconds < 2.0) return;
        _lastAudioNotify = DateTime.UtcNow;

        _ = CheckAudioDeviceAsync(args.Id);
    }

    private async Task CheckAudioDeviceAsync(string deviceId)
    {
        try
        {
            var info = await DeviceInformation.CreateFromIdAsync(deviceId);
            if (info != null && !string.IsNullOrWhiteSpace(info.Name))
            {
                // Abaikan speaker bawaan laptop generik (Realtek, High Definition Audio Device)
                string n = info.Name.ToLowerInvariant();
                if (n.Contains("speaker") && (n.Contains("realtek") || n.Contains("high definition") || n.Contains("internal")))
                    return;

                string icon = GuessDeviceIcon(info.Name);
                Ui(() => DeviceConnectionChanged?.Invoke(info.Name, true, icon));
            }
        }
        catch { }
    }

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation info)
    {
        if (!string.IsNullOrWhiteSpace(info.Name))
        {
            _nameCache[info.Id] = info.Name;
        }

        bool isConnected = false;
        if (info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var val) && val is bool b)
        {
            isConnected = b;
        }

        if (isConnected)
        {
            _connectedDevices.Add(info.Id);
            if (_initialized && !string.IsNullOrWhiteSpace(info.Name))
            {
                Notify(info.Name, true);
            }
        }
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (update.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var val) && val is bool isConnected)
        {
            bool wasConnected = _connectedDevices.Contains(update.Id);
            if (isConnected && !wasConnected)
            {
                _connectedDevices.Add(update.Id);
                if (_initialized)
                {
                    _ = FetchAndNotifyAsync(update.Id, true);
                }
            }
            else if (!isConnected && wasConnected)
            {
                _connectedDevices.Remove(update.Id);
                if (_initialized)
                {
                    _ = FetchAndNotifyAsync(update.Id, false);
                }
            }
        }
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_connectedDevices.Remove(update.Id) && _initialized)
        {
            _ = FetchAndNotifyAsync(update.Id, false);
        }
    }

    private async Task FetchAndNotifyAsync(string id, bool connected)
    {
        if (_nameCache.TryGetValue(id, out var cachedName) && !string.IsNullOrWhiteSpace(cachedName))
        {
            Notify(cachedName, connected);
            return;
        }

        try
        {
            var dev = await DeviceInformation.CreateFromIdAsync(id);
            string name = dev?.Name ?? "Perangkat Bluetooth";
            if (!string.IsNullOrWhiteSpace(name))
            {
                _nameCache[id] = name;
            }
            Notify(name, connected);
        }
        catch
        {
            Notify("Perangkat Bluetooth", connected);
        }
    }

    private void Notify(string name, bool connected)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        string icon = GuessDeviceIcon(name);
        Ui(() => DeviceConnectionChanged?.Invoke(name, connected, icon));
    }

    private static string GuessDeviceIcon(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("headphone") || n.Contains("headset") || n.Contains("buds") ||
            n.Contains("airpod") || n.Contains("earphone") || n.Contains("wh-") ||
            n.Contains("wf-") || n.Contains("tws") || n.Contains("audio") || n.Contains("anc"))
            return "🎧";
        if (n.Contains("speaker") || n.Contains("soundbar") || n.Contains("jbl") || n.Contains("bose") || n.Contains("harman"))
            return "🔊";
        if (n.Contains("mouse")) return "🖱️";
        if (n.Contains("keyboard") || n.Contains("keychron")) return "⌨️";
        if (n.Contains("controller") || n.Contains("gamepad") || n.Contains("xbox") || n.Contains("dualshock") || n.Contains("dualsense"))
            return "🎮";
        return "📶";
    }

    private static void Ui(Action a) => Application.Current?.Dispatcher.InvokeAsync(a);
}
