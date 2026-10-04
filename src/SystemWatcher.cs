using System.Runtime.InteropServices;

namespace KbLight;

/// <summary>Hidden top-level window that reports keyboard (re)connects and wake-ups from sleep.</summary>
sealed class SystemWatcher : NativeWindow, IDisposable
{
    const int WmDeviceChange = 0x0219, WmPowerBroadcast = 0x0218;
    const int DbtDeviceArrival = 0x8000, DbtDevtypDeviceInterface = 5;
    const int PbtApmResumeSuspend = 0x07, PbtApmResumeAutomatic = 0x12;
    const int NameOffset = 28; // DEV_BROADCAST_DEVICEINTERFACE.dbcc_name

    IntPtr _notification;

    public event Action? KeyboardArrived;
    public event Action? Resumed;

    public SystemWatcher()
    {
        CreateHandle(new CreateParams { Caption = "KbLight" });

        var filter = new DevBroadcastDeviceInterface
        {
            Size = Marshal.SizeOf<DevBroadcastDeviceInterface>(),
            DeviceType = DbtDevtypDeviceInterface,
            ClassGuid = Native.HidInterfaceGuid,
        };
        IntPtr buffer = Marshal.AllocHGlobal(filter.Size);
        try
        {
            Marshal.StructureToPtr(filter, buffer, false);
            _notification = RegisterDeviceNotification(Handle, buffer, 0 /* DEVICE_NOTIFY_WINDOW_HANDLE */);
            if (_notification == 0)
                Log.Write(Text.NotificationFailed(Marshal.GetLastPInvokeError()));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmDeviceChange && m.WParam == DbtDeviceArrival && m.LParam != 0
            && Marshal.ReadInt32(m.LParam, 4) == DbtDevtypDeviceInterface
            && Keyboard.IsInteresting(Marshal.PtrToStringUni(m.LParam + NameOffset) ?? ""))
        {
            KeyboardArrived?.Invoke();
        }
        else if (m.Msg == WmPowerBroadcast && (m.WParam == PbtApmResumeAutomatic || m.WParam == PbtApmResumeSuspend))
        {
            Resumed?.Invoke();
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_notification != 0) UnregisterDeviceNotification(_notification);
        _notification = 0;
        DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DevBroadcastDeviceInterface
    {
        public int Size, DeviceType, Reserved;
        public Guid ClassGuid;
        public short Name;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr RegisterDeviceNotification(IntPtr recipient, IntPtr filter, int flags);

    [DllImport("user32.dll")]
    static extern bool UnregisterDeviceNotification(IntPtr handle);
}
