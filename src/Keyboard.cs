using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace KbLight;

// Lighting protocol of sbarda keyboards (USB VID 19F5), reverse-engineered from sbarda.exe 1.0.0.0.
// Full notes, including what is verified on the device: docs/PROTOCOL.md.
//
// Vendor HID interface MI_01, 65-byte reports with report ID 0. The 64-byte payload:
//   host -> keyboard   55 cmd 00 sum len offLo offHi 00 data[56]
//   keyboard -> host   AA cmd 00 sum len offLo offHi 00 data[56]
//   sum = low byte of the sum of payload bytes 4..63
// Commands used here: 01 open session, 02 close session,
//                     05 read / 06 write the 32-byte settings block at offset 0.
// Settings block: [2..3] AA BB marker, [8] effect, [9] brightness 0..100, [10] 4 - speed (speed 0..4),
//   [11] direction, [12] multicolor, [13] color index, [14..16] R G B. The other bytes hold
//   non-lighting settings (report rate, dead zones...) and go back unchanged, as sbarda.exe does.

enum ApplyStatus { NotFound, AlreadySet, Written, Failed }

sealed record ApplyResult(ApplyStatus Status, string Message, byte[]? Block = null);

readonly record struct LightState(
    byte Effect, byte Brightness, byte Speed, byte Direction, bool Multicolor, byte ColorIndex, Color Color)
{
    const int Offset = 8, Length = 9;

    public static LightState From(LightSettings s)
    {
        var c = s.GetColor();
        return new((byte)Math.Clamp(s.Effect, 0, 255), (byte)Math.Clamp(s.Brightness, 0, 100),
            (byte)Math.Clamp(s.Speed, 0, 4), (byte)(s.ReverseDirection ? 1 : 0), s.Multicolor,
            (byte)Math.Clamp(s.ColorIndex, 0, 255), Color.FromArgb(c.R, c.G, c.B));
    }

    public void WriteTo(Span<byte> block)
    {
        ReadOnlySpan<byte> bytes = [Effect, Brightness, (byte)(4 - Speed), Direction,
            (byte)(Multicolor ? 1 : 0), ColorIndex, Color.R, Color.G, Color.B];
        bytes.CopyTo(block.Slice(Offset, Length));
    }

    public bool Matches(ReadOnlySpan<byte> block)
    {
        Span<byte> wanted = stackalloc byte[Keyboard.BlockLength];
        WriteTo(wanted);
        return block.Slice(Offset, Length).SequenceEqual(wanted.Slice(Offset, Length));
    }

    public static string Describe(ReadOnlySpan<byte> block) =>
        $"effect={block[8]} brightness={block[9]} speed={4 - block[10]} dir={block[11]} " +
        $"multicolor={block[12]} colorIndex={block[13]} rgb={block[14]:X2}{block[15]:X2}{block[16]:X2}";
}

static class Keyboard
{
    public const int BlockLength = 0x20;
    const byte CmdOpen = 0x01, CmdClose = 0x02, CmdRead = 0x05, CmdWrite = 0x06;

    public static bool IsLightingInterface(string devicePath) =>
        devicePath.Contains("vid_19f5", StringComparison.OrdinalIgnoreCase) &&
        devicePath.Contains("&mi_01", StringComparison.OrdinalIgnoreCase);

    /// <param name="force">write even if the keyboard already reports the wanted lighting</param>
    /// <param name="fallbackBlock">base block for the write if the keyboard returns a damaged one</param>
    public static ApplyResult Apply(LightState want, bool force, byte[]? fallbackBlock)
    {
        var results = ForEachInterface(path => ApplyTo(path, want, force, fallbackBlock));
        if (results.Count == 0) return new(ApplyStatus.NotFound, "клавиатура не найдена");
        return results.FirstOrDefault(r => r.Status == ApplyStatus.Failed)
            ?? results.FirstOrDefault(r => r.Status == ApplyStatus.Written)
            ?? results[0];
    }

    /// <summary>Reads the lighting the keyboard holds now, without writing anything.</summary>
    /// <returns>AlreadySet with the block of the first interface that returned a valid one</returns>
    public static ApplyResult Read()
    {
        var results = ForEachInterface(ReadFrom);
        if (results.Count == 0) return new(ApplyStatus.NotFound, "клавиатура не найдена");
        return results.FirstOrDefault(r => r.Status == ApplyStatus.AlreadySet) ?? results[0];
    }

    static List<ApplyResult> ForEachInterface(Func<string, ApplyResult> action)
    {
        // Serializes with other KbLight processes (e.g. a one-shot --apply next to the tray instance).
        using var deviceLock = new Mutex(false, @"Local\KbLight.Device");
        bool locked;
        try { locked = deviceLock.WaitOne(TimeSpan.FromSeconds(10)); }
        catch (AbandonedMutexException) { locked = true; }
        try
        {
            var results = new List<ApplyResult>();
            foreach (string path in Native.HidInterfaces().Where(IsLightingInterface))
            {
                try { results.Add(action(path)); }
                catch (Exception e) when (e is IOException or TimeoutException or Win32Exception)
                {
                    results.Add(new(ApplyStatus.Failed, e.Message));
                }
            }
            return results;
        }
        finally
        {
            if (locked) deviceLock.ReleaseMutex();
        }
    }

    static ApplyResult ReadFrom(string path)
    {
        using var device = HidDevice.Open(path);
        if (device is null) return new(ApplyStatus.Failed, "не удалось открыть интерфейс клавиатуры");

        byte[]? block;
        device.Command(CmdOpen);
        try { block = ReadBlock(device); }
        finally { CloseSession(device); }

        return block is null
            ? new(ApplyStatus.Failed, "клавиатура вернула неожиданный блок настроек, настройки не взяты")
            : new(ApplyStatus.AlreadySet, "первый запуск, взял из клавиатуры: " + LightState.Describe(block), block);
    }

    static ApplyResult ApplyTo(string path, LightState want, bool force, byte[]? fallbackBlock)
    {
        using var device = HidDevice.Open(path);
        if (device is null) return new(ApplyStatus.Failed, "не удалось открыть интерфейс клавиатуры");

        // Same sequence as sbarda.exe: open, read block, write block, wait, close.
        string before;
        byte[]? current;
        device.Command(CmdOpen);
        try
        {
            current = ReadBlock(device);
            if (current is not null && !force && want.Matches(current))
                return new(ApplyStatus.AlreadySet, "уже стоит: " + LightState.Describe(current), current);

            byte[] block;
            if (current is not null)
            {
                block = current.ToArray();
            }
            else if (fallbackBlock is { Length: BlockLength })
            {
                Log.Write("клавиатура вернула повреждённый блок настроек, пишу поверх последнего исправного");
                block = fallbackBlock.ToArray();
            }
            else
            {
                return new(ApplyStatus.Failed, "клавиатура вернула неожиданный блок настроек, запись отменена");
            }

            before = current is null ? "?" : LightState.Describe(current);
            want.WriteTo(block);
            device.Command(CmdWrite, BlockLength, block);
            Thread.Sleep(400); // sbarda.exe waits the same before closing the session
        }
        finally
        {
            CloseSession(device);
        }

        byte[]? after;
        device.Command(CmdOpen);
        try { after = ReadBlock(device); }
        finally { CloseSession(device); }

        if (after is null || !want.Matches(after))
            return new(ApplyStatus.Failed, "клавиатура не подтвердила запись", current);
        return new(ApplyStatus.Written, $"было: {before}; стало: {LightState.Describe(after)}", after);
    }

    /// <returns>the settings block, or null if the reply does not look like one</returns>
    static byte[]? ReadBlock(HidDevice device)
    {
        byte[] reply = device.Command(CmdRead, BlockLength);
        bool valid = reply[4] == BlockLength && reply[5] == 0 && reply[6] == 0
            && reply[3] == HidDevice.Checksum(reply)
            && reply[8 + 2] == 0xAA && reply[8 + 3] == 0xBB;
        return valid ? reply.AsSpan(8, BlockLength).ToArray() : null;
    }

    static void CloseSession(HidDevice device)
    {
        try { device.Command(CmdClose); }
        catch (Exception e) when (e is IOException or TimeoutException or Win32Exception) { }
    }
}

sealed class HidDevice : IDisposable
{
    const int ReportLength = 65;
    const int ErrorIoPending = 997;

    readonly SafeFileHandle _handle;
    readonly ManualResetEvent _done = new(false);

    HidDevice(SafeFileHandle handle) => _handle = handle;

    public static HidDevice? Open(string path)
    {
        var handle = Native.CreateFile(path, Native.GenericRead | Native.GenericWrite,
            Native.FileShareRead | Native.FileShareWrite, 0, Native.OpenExisting, Native.FileFlagOverlapped, 0);
        if (handle.IsInvalid)
        {
            Log.Write($"не открылся {path}: ошибка {Marshal.GetLastPInvokeError()}");
            handle.Dispose();
            return null;
        }
        var (input, output) = Native.ReportLengths(handle);
        if (input != ReportLength || output != ReportLength)
        {
            Log.Write($"неожиданные размеры отчётов {input}/{output} у {path}");
            handle.Dispose();
            return null;
        }
        return new HidDevice(handle);
    }

    public static byte Checksum(ReadOnlySpan<byte> payload)
    {
        int sum = 0;
        foreach (byte b in payload[4..64]) sum += b;
        return (byte)sum;
    }

    /// <returns>the 64-byte reply payload</returns>
    public byte[] Command(byte cmd, int length = 0, ReadOnlySpan<byte> data = default)
    {
        var report = new byte[ReportLength];
        var payload = report.AsSpan(1);
        payload[0] = 0x55;
        payload[1] = cmd;
        payload[4] = (byte)length;
        data.CopyTo(payload[8..]);
        payload[3] = Checksum(payload);
        if (Transfer(write: true, report, 1000) != ReportLength)
            throw new IOException($"команда {cmd:X2} не отправлена");

        var input = new byte[ReportLength];
        long deadline = Environment.TickCount64 + 1000;
        while (true)
        {
            int left = (int)(deadline - Environment.TickCount64);
            if (left <= 0 || Transfer(write: false, input, left) < 0)
                throw new TimeoutException($"клавиатура не ответила на команду {cmd:X2}");
            if (input[1] == 0xAA && input[2] == cmd)
                return input[1..];
        }
    }

    /// <returns>bytes transferred, or -1 on timeout</returns>
    int Transfer(bool write, byte[] buffer, int timeoutMs)
    {
        IntPtr overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
        IntPtr native = Marshal.AllocHGlobal(buffer.Length);
        try
        {
            _done.Reset();
            Marshal.StructureToPtr(new NativeOverlapped { EventHandle = _done.SafeWaitHandle.DangerousGetHandle() },
                overlapped, false);
            if (write) Marshal.Copy(buffer, 0, native, buffer.Length);

            bool ok = write
                ? Native.WriteFile(_handle, native, buffer.Length, 0, overlapped)
                : Native.ReadFile(_handle, native, buffer.Length, 0, overlapped);
            if (!ok && Marshal.GetLastPInvokeError() != ErrorIoPending)
                throw new Win32Exception();
            if (!ok && !_done.WaitOne(timeoutMs))
            {
                Native.CancelIoEx(_handle, overlapped);
                Native.GetOverlappedResult(_handle, overlapped, out _, true); // buffers must outlive the I/O
                return -1;
            }
            if (!Native.GetOverlappedResult(_handle, overlapped, out int transferred, false))
                throw new Win32Exception();
            if (!write) Marshal.Copy(native, buffer, 0, transferred);
            return transferred;
        }
        finally
        {
            Marshal.FreeHGlobal(native);
            Marshal.FreeHGlobal(overlapped);
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
        _done.Dispose();
    }
}

static partial class Native
{
    public static readonly Guid HidInterfaceGuid = new("4D1E55B2-F16F-11CF-88CB-001111000030");

    public const uint GenericRead = 0x80000000, GenericWrite = 0x40000000;
    public const uint FileShareRead = 1, FileShareWrite = 2, OpenExisting = 3, FileFlagOverlapped = 0x40000000;
    const int CrSuccess = 0, CrBufferSmall = 0x1A;
    const int HidpStatusSuccess = 0x00110000;

    public static List<string> HidInterfaces()
    {
        var guid = HidInterfaceGuid;
        while (true)
        {
            if (CM_Get_Device_Interface_List_Size(out int size, ref guid, null, 0) != CrSuccess) return [];
            var buffer = new char[size];
            int cr = CM_Get_Device_Interface_List(ref guid, null, buffer, size, 0);
            if (cr == CrBufferSmall) continue; // a device arrived between the two calls
            if (cr != CrSuccess) return [];
            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }

    public static (int Input, int Output) ReportLengths(SafeFileHandle handle)
    {
        if (!HidD_GetPreparsedData(handle, out IntPtr preparsed)) return (0, 0);
        try
        {
            return HidP_GetCaps(preparsed, out var caps) == HidpStatusSuccess
                ? (caps.InputReportByteLength, caps.OutputReportByteLength)
                : (0, 0);
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HidpCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 27)] public ushort[] Rest;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
    public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteFile(SafeFileHandle file, IntPtr buffer, int length, IntPtr written, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(SafeFileHandle file, IntPtr buffer, int length, IntPtr read, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetOverlappedResult(SafeFileHandle file, IntPtr overlapped, out int transferred, bool wait);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);

    // HidD_* return a one-byte BOOLEAN, not a four-byte BOOL.
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsed);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool HidD_FreePreparsedData(IntPtr preparsed);

    [DllImport("hid.dll")]
    static extern int HidP_GetCaps(IntPtr preparsed, out HidpCaps caps);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_Interface_List_Size(out int length, ref Guid interfaceClass, string? deviceId, int flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_Interface_List(ref Guid interfaceClass, string? deviceId, [Out] char[] buffer,
        int length, int flags);
}
