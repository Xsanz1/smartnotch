using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace SmartNotch.App.Services;

#region Core Audio COM Interop
[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int NotImpl1();
    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
    [PreserveSig] int GetChannelCount(out uint pnChannelCount);
    [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
    [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
    [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
    [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
}
#endregion

/// <summary>
/// Layanan pengatur dan pemantau master volume sistem Windows melalui Core Audio API.
/// </summary>
public sealed class AudioVolumeService
{
    private IAudioEndpointVolume? _volumeEndpoint;
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private float _lastVolume = -1f;
    private bool _lastMute;
    private bool _internalChange;

    public float Volume { get; private set; } = 0.5f; // 0.0 to 1.0
    public bool IsMuted { get; private set; }

    public event Action<float, bool>? VolumeChanged;
    public event Action<float, bool>? ExternalVolumeChanged; // Dipicu saat volume berubah dari tombol keyboard / fisik

    public AudioVolumeService()
    {
        Init();
        _pollTimer.Tick += (_, _) => CheckVolume();
        _pollTimer.Start();
    }

    private void Init()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            // eRender = 0, eMultimedia = 1
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out var dev) == 0 && dev != null)
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                if (dev.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var ep) == 0 && ep is IAudioEndpointVolume vol)
                {
                    _volumeEndpoint = vol;
                    CheckVolume(true);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AudioVolumeService init error: {ex.Message}");
        }
    }

    private void CheckVolume(bool initial = false)
    {
        if (_volumeEndpoint == null)
        {
            Init();
            if (_volumeEndpoint == null) return;
        }

        try
        {
            if (_volumeEndpoint.GetMasterVolumeLevelScalar(out float val) == 0 &&
                _volumeEndpoint.GetMute(out bool mute) == 0)
            {
                Volume = Math.Clamp(val, 0f, 1f);
                IsMuted = mute;

                bool changed = initial || Math.Abs(Volume - _lastVolume) > 0.008f || IsMuted != _lastMute;
                if (changed)
                {
                    bool wasInternal = _internalChange;
                    _internalChange = false;

                    _lastVolume = Volume;
                    _lastMute = IsMuted;
                    VolumeChanged?.Invoke(Volume, IsMuted);

                    if (!initial && !wasInternal)
                    {
                        ExternalVolumeChanged?.Invoke(Volume, IsMuted);
                    }
                }
            }
        }
        catch
        {
            _volumeEndpoint = null; // Endpoint mungkin berganti (misal saat pasang/lepas headphone)
        }
    }

    public void SetVolume(float level)
    {
        if (_volumeEndpoint == null) Init();
        if (_volumeEndpoint == null) return;

        level = Math.Clamp(level, 0f, 1f);
        try
        {
            _internalChange = true;
            var ctx = Guid.Empty;
            _volumeEndpoint.SetMasterVolumeLevelScalar(level, ref ctx);
            Volume = level;
            _lastVolume = level;
            VolumeChanged?.Invoke(Volume, IsMuted);
        }
        catch { }
    }

    public void ToggleMute()
    {
        if (_volumeEndpoint == null) Init();
        if (_volumeEndpoint == null) return;

        try
        {
            _internalChange = true;
            var ctx = Guid.Empty;
            bool newMute = !IsMuted;
            _volumeEndpoint.SetMute(newMute, ref ctx);
            IsMuted = newMute;
            _lastMute = newMute;
            VolumeChanged?.Invoke(Volume, IsMuted);
        }
        catch { }
    }
}
