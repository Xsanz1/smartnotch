using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using SmartNotch.App.Interop;
using Windows.Devices.Enumeration;

namespace SmartNotch.App.Services;

/// <summary>
/// Layanan deteksi perangkat USB, Type-C, dan Flashdisk (Drive Arrival & Removal).
/// Menampilkan notifikasi Peek saat flashdisk, HP, atau periferal USB dicolok / dicabut.
/// </summary>
public sealed class UsbDeviceService
{
    private IntPtr _hNotify = IntPtr.Zero;
    private readonly HashSet<string> _recentlyNotified = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastClean = DateTime.UtcNow;

    public event Action<string, bool>? DeviceChanged; // (text, isArrival)

    public void Start(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || _hNotify != IntPtr.Zero) return;

        try
        {
            var iface = new Win32.DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_size = Marshal.SizeOf(typeof(Win32.DEV_BROADCAST_DEVICEINTERFACE)),
                dbcc_devicetype = Win32.DBT_DEVTYP_DEVICEINTERFACE
            };

            IntPtr pIface = Marshal.AllocHGlobal(iface.dbcc_size);
            Marshal.StructureToPtr(iface, pIface, true);

            _hNotify = Win32.RegisterDeviceNotification(
                hwnd,
                pIface,
                Win32.DEVICE_NOTIFY_WINDOW_HANDLE | Win32.DEVICE_NOTIFY_ALL_INTERFACE_CLASSES
            );

            Marshal.FreeHGlobal(pIface);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UsbDeviceService register error: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (_hNotify != IntPtr.Zero)
        {
            try { Win32.UnregisterDeviceNotification(_hNotify); } catch { }
            _hNotify = IntPtr.Zero;
        }
    }

    public void HandleDeviceChange(IntPtr wParam, IntPtr lParam)
    {
        if (lParam == IntPtr.Zero) return;

        int eventType = wParam.ToInt32();
        if (eventType != Win32.DBT_DEVICEARRIVAL && eventType != Win32.DBT_DEVICEREMOVECOMPLETE)
            return;

        bool isArrival = eventType == Win32.DBT_DEVICEARRIVAL;

        try
        {
            var hdr = Marshal.PtrToStructure<Win32.DEV_BROADCAST_HDR>(lParam);

            // 1. Flashdisk / Penyimpanan Eksternal (Volume)
            if (hdr.dbch_devicetype == Win32.DBT_DEVTYP_VOLUME)
            {
                var vol = Marshal.PtrToStructure<Win32.DEV_BROADCAST_VOLUME>(lParam);
                char driveLetter = GetDriveLetter(vol.dbcv_unitmask);
                if (driveLetter >= 'A' && driveLetter <= 'Z')
                {
                    HandleVolumeChange(driveLetter, isArrival);
                }
            }
            // 2. Perangkat USB / Type-C (Smartphone, Hub, Periferal, DAC)
            else if (hdr.dbch_devicetype == Win32.DBT_DEVTYP_DEVICEINTERFACE)
            {
                var dev = Marshal.PtrToStructure<Win32.DEV_BROADCAST_DEVICEINTERFACE>(lParam);
                string path = dev.dbcc_name ?? "";
                if (!string.IsNullOrWhiteSpace(path))
                {
                    HandleInterfaceChange(path, isArrival);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UsbDeviceService error: {ex.Message}");
        }
    }

    private void HandleVolumeChange(char driveLetter, bool isArrival)
    {
        string key = $"VOL_{driveLetter}_{isArrival}";
        if (IsDuplicate(key)) return;

        string label = "USB Drive";
        if (isArrival)
        {
            try
            {
                var d = new DriveInfo(driveLetter.ToString());
                if (!string.IsNullOrWhiteSpace(d.VolumeLabel))
                    label = d.VolumeLabel;
                else if (d.DriveType == DriveType.Removable)
                    label = "Flashdisk";
            }
            catch { }
            Ui(() => DeviceChanged?.Invoke($"💾 {label} ({driveLetter}:)  •  Terhubung", true));
        }
        else
        {
            Ui(() => DeviceChanged?.Invoke($"💾 Flashdisk ({driveLetter}:)  •  Dicabut", false));
        }
    }

    private void HandleInterfaceChange(string path, bool isArrival)
    {
        // Abaikan endpoint internal audio bawaan yang sering kirim event
        string pUpper = path.ToUpperInvariant();
        if (pUpper.Contains("HDAUDIO") || pUpper.Contains("DISPLAY#")) return;

        string key = $"IFACE_{path}_{isArrival}";
        if (IsDuplicate(key)) return;

        // Coba ekstrak info perangkat secara asinkron
        _ = ResolveDeviceInfoAsync(path, isArrival);
    }

    private async Task ResolveDeviceInfoAsync(string path, bool isArrival)
    {
        string name = "";
        string icon = "🔌";

        try
        {
            var info = await DeviceInformation.CreateFromIdAsync(path);
            if (info != null && !string.IsNullOrWhiteSpace(info.Name))
            {
                name = info.Name;
            }
        }
        catch { }

        if (string.IsNullOrWhiteSpace(name))
        {
            string p = path.ToLowerInvariant();
            if (p.Contains("wpd") || p.Contains("mtp") || p.Contains("phone") || p.Contains("android") || p.Contains("apple"))
            {
                name = "Smartphone";
                icon = "📱";
            }
            else if (p.Contains("hub") || p.Contains("typec") || p.Contains("type-c"))
            {
                name = "USB-C Hub";
                icon = "🔌";
            }
            else if (p.Contains("kbd") || p.Contains("keyboard"))
            {
                name = "Keyboard USB";
                icon = "⌨️";
            }
            else if (p.Contains("mouse") || p.Contains("mou"))
            {
                name = "Mouse USB";
                icon = "🖱️";
            }
            else if (p.Contains("usbstor") || p.Contains("disk"))
            {
                // Akan ditangani oleh event Volume (agar tidak duplikat)
                return;
            }
            else
            {
                name = "Perangkat USB / Type-C";
                icon = "🔌";
            }
        }
        else
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("phone") || n.Contains("galaxy") || n.Contains("iphone") || n.Contains("pixel") || n.Contains("xiaomi") || n.Contains("oppo") || n.Contains("vivo"))
                icon = "📱";
            else if (n.Contains("hub") || n.Contains("dock"))
                icon = "🔌";
            else if (n.Contains("mouse"))
                icon = "🖱️";
            else if (n.Contains("keyboard"))
                icon = "⌨️";
            else if (n.Contains("controller") || n.Contains("gamepad"))
                icon = "🎮";
            else if (n.Contains("camera") || n.Contains("webcam"))
                icon = "📷";
        }

        string action = isArrival ? "Terhubung" : "Dicabut";
        Ui(() => DeviceChanged?.Invoke($"{icon} {name}  •  {action}", isArrival));
    }

    private static char GetDriveLetter(int mask)
    {
        for (int i = 0; i < 26; i++)
        {
            if ((mask & (1 << i)) != 0)
                return (char)('A' + i);
        }
        return '\0';
    }

    private bool IsDuplicate(string key)
    {
        if ((DateTime.UtcNow - _lastClean).TotalSeconds > 10)
        {
            _recentlyNotified.Clear();
            _lastClean = DateTime.UtcNow;
        }

        return !_recentlyNotified.Add(key);
    }

    private static void Ui(Action a) => Application.Current?.Dispatcher.InvokeAsync(a);
}
