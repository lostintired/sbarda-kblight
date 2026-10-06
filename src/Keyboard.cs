using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
//   [11] direction, [12] multicolor, [13] color index, [14..16] R G B. Models with a light box:
//   [24] mode, [25] brightness, [26] 4 - speed, [27] Fn color index (kept), [28] multicolor, [29..31] R G B.
//   The other bytes hold non-lighting settings (report rate, dead zones...) and go back unchanged, as sbarda.exe does.

enum ApplyStatus { NotFound, Unsupported, AlreadySet, Written, Failed }

/// <param name="Model">the keyboard model the result came from (null for NotFound and Unsupported)</param>
/// <param name="UnknownId">VID:PID of the keyboard without a model file, for Unsupported</param>
sealed record ApplyResult(ApplyStatus Status, string Message, byte[]? Block = null, KeyboardModel? Model = null,
    string? UnknownId = null);

/// <param name="Box">light box bytes 24, 25, 26, 28, 29, 30, 31, or null to leave bytes 24..31 as read</param>
readonly record struct LightState(
    byte Effect, byte Brightness, byte Speed, byte Direction, bool Multicolor, byte ColorIndex, Color Color, byte[]? Box)
{
    const int Offset = 8, Length = 9;
    static readonly int[] BoxOffsets = [24, 25, 26, 28, 29, 30, 31]; // byte 27 is the Fn+PgDn color index, not ours

    /// <summary>The bytes for this keyboard: the light box part only if the model has one and the settings hold it.</summary>
    public static LightState From(LightSettings s, KeyboardModel? model)
    {
        var c = s.GetColor();
        return new((byte)Math.Clamp(s.Effect, 0, 255), (byte)Math.Clamp(s.Brightness, 0, 100),
            (byte)Math.Clamp(s.Speed, 0, 4), (byte)(s.ReverseDirection ? 1 : 0), s.Multicolor,
            (byte)Math.Clamp(s.ColorIndex, 0, 255), Color.FromArgb(c.R, c.G, c.B),
            model?.LightBox == true && s.LightBox is { } box ? BoxBytes(box) : null);
    }

    static byte[] BoxBytes(LightBoxSettings box)
    {
        var mode = LightBoxMode.Find(box.Mode);
        var c = box.GetColor();
        bool multicolor = box.Multicolor && mode.Options.HasFlag(EffectOptions.Multicolor);
        return [(byte)mode.Id, (byte)Math.Clamp(box.Brightness, 0, 100), (byte)(4 - Math.Clamp(box.Speed, 0, 4)),
            (byte)(multicolor ? 1 : 0), c.R, c.G, c.B];
    }

    public void WriteTo(Span<byte> block)
    {
        ReadOnlySpan<byte> bytes = [Effect, Brightness, (byte)(4 - Speed), Direction,
            (byte)(Multicolor ? 1 : 0), ColorIndex, Color.R, Color.G, Color.B];
        bytes.CopyTo(block.Slice(Offset, Length));
        if (Box is null) return;
        for (int i = 0; i < BoxOffsets.Length; i++) block[BoxOffsets[i]] = Box[i];
    }

    public bool Matches(ReadOnlySpan<byte> block)
    {
        Span<byte> wanted = stackalloc byte[Keyboard.BlockLength];
        block.CopyTo(wanted);
        WriteTo(wanted);
        return block.SequenceEqual(wanted);
    }

    /// <summary>The state for the log; the light box part whenever the model has one, even if KbLight does not hold it yet.</summary>
    public static string Describe(ReadOnlySpan<byte> block, KeyboardModel? model) =>
        $"effect={block[8]} brightness={block[9]} speed={4 - block[10]} dir={block[11]} " +
        $"multicolor={block[12]} colorIndex={block[13]} rgb={block[14]:X2}{block[15]:X2}{block[16]:X2}" +
        (model?.LightBox == true
            ? $" | box mode={block[24]} brightness={block[25]} speed={4 - block[26]} multicolor={block[28]} " +
              $"rgb={block[29]:X2}{block[30]:X2}{block[31]:X2}"
            : "");
}

static class Keyboard
{
    public const int BlockLength = 0x20;
    const byte CmdOpen = 0x01, CmdClose = 0x02, CmdRead = 0x05, CmdWrite = 0x06;

    const ushort SbardaVid = 0x19F5;
    static readonly Regex PathIds = new(@"vid_([0-9a-f]{4})&pid_([0-9a-f]{4})(?:&mi_([0-9a-f]{2}))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>A device arrival worth an apply: a known model's settings interface or any sbarda-vendor device.</summary>
    public static bool IsInteresting(string devicePath)
    {
        var ids = ParsePath(devicePath);
        return ids is { } p && (p.Vid == SbardaVid || ModelOf(p, KeyboardModels.Current()) is not null);
    }

    static (ushort Vid, ushort Pid, int? Interface)? ParsePath(string path)
    {
        var m = PathIds.Match(path);
        if (!m.Success) return null;
        return (Convert.ToUInt16(m.Groups[1].Value, 16), Convert.ToUInt16(m.Groups[2].Value, 16),
            m.Groups[3].Success ? Convert.ToInt32(m.Groups[3].Value, 16) : null);
    }

    static KeyboardModel? ModelOf((ushort Vid, ushort Pid, int? Interface) ids, IReadOnlyList<KeyboardModel> models) =>
        models.FirstOrDefault(m => m.Vid == ids.Vid && m.Pid == ids.Pid && m.Interface == ids.Interface);

    /// <param name="force">write even if the keyboard already reports the wanted lighting</param>
    /// <param name="fallbackBlock">base block for the write if the keyboard returns a damaged one</param>
    public static ApplyResult Apply(LightSettings settings, bool force, byte[]? fallbackBlock)
    {
        return ForEachInterface((path, model) => ApplyTo(path, model, LightState.From(settings, model), force, fallbackBlock), results =>
            results.FirstOrDefault(r => r.Status == ApplyStatus.Failed)
            ?? results.FirstOrDefault(r => r.Status == ApplyStatus.Written)
            ?? results[0]);
    }

    /// <summary>Reads the lighting the keyboard holds now, without writing anything.</summary>
    /// <returns>AlreadySet with the block of the first interface that returned a valid one</returns>
    public static ApplyResult Read()
    {
        return ForEachInterface(ReadFrom, results => results.FirstOrDefault(r => r.Status == ApplyStatus.AlreadySet) ?? results[0]);
    }

    /// <summary>
    /// Runs the action on the settings interface of every connected keyboard that has a model file.
    /// Sbarda-vendor keyboards without one are never opened.
    /// </summary>
    static ApplyResult ForEachInterface(Func<string, KeyboardModel, ApplyResult> action, Func<List<ApplyResult>, ApplyResult> combine)
    {
        // Serializes with other KbLight processes (e.g. a one-shot --apply next to the tray instance).
        using var deviceLock = new Mutex(false, @"Local\KbLight.Device");
        bool locked;
        try { locked = deviceLock.WaitOne(TimeSpan.FromSeconds(10)); }
        catch (AbandonedMutexException) { locked = true; }
        try
        {
            var models = KeyboardModels.Current();
            var results = new List<ApplyResult>();
            var unknown = new SortedSet<ushort>();
            foreach (string path in Native.HidInterfaces())
            {
                if (ParsePath(path) is not { } ids) continue;
                if (ModelOf(ids, models) is not { } model)
                {
                    if (ids.Vid == SbardaVid && !models.Any(m => m.Vid == ids.Vid && m.Pid == ids.Pid)) unknown.Add(ids.Pid);
                    continue;
                }
                try { results.Add(action(path, model) with { Model = model }); }
                catch (Exception e) when (e is IOException or TimeoutException or Win32Exception)
                {
                    results.Add(new(ApplyStatus.Failed, e.Message, Model: model));
                }
            }
            if (results.Count == 0)
            {
                if (unknown.Count == 0) return new(ApplyStatus.NotFound, Text.KeyboardNotFound);
                string id = KeyboardModel.FormatId(SbardaVid, unknown.Min);
                return new(ApplyStatus.Unsupported, Text.KeyboardUnknown(id), UnknownId: id);
            }
            foreach (ushort pid in unknown)
                Log.Write(Text.KeyboardSkipped(KeyboardModel.FormatId(SbardaVid, pid)));
            return combine(results);
        }
        finally
        {
            if (locked) deviceLock.ReleaseMutex();
        }
    }

    static ApplyResult ReadFrom(string path, KeyboardModel model)
    {
        using var device = HidDevice.Open(path);
        if (device is null) return new(ApplyStatus.Failed, Text.OpenFailed);

        byte[]? block;
        device.Command(CmdOpen);
        try { block = ReadBlock(device); }
        finally { CloseSession(device); }

        return block is null
            ? new(ApplyStatus.Failed, Text.BadBlockNotTaken)
            : new(ApplyStatus.AlreadySet, Text.Adopted(LightState.Describe(block, model)), block);
    }

    static ApplyResult ApplyTo(string path, KeyboardModel model, LightState want, bool force, byte[]? fallbackBlock)
    {
        using var device = HidDevice.Open(path);
        if (device is null) return new(ApplyStatus.Failed, Text.OpenFailed);

        // Same sequence as sbarda.exe: open, read block, write block, wait, close.
        string before;
        byte[]? current;
        device.Command(CmdOpen);
        try
        {
            current = ReadBlock(device);
            if (current is not null && !force && want.Matches(current))
                return new(ApplyStatus.AlreadySet, Text.AlreadySet(LightState.Describe(current, model)), current);

            byte[] block;
            if (current is not null)
            {
                block = current.ToArray();
            }
            else if (fallbackBlock is { Length: BlockLength })
            {
                Log.Write(Text.DamagedBlock);
                block = fallbackBlock.ToArray();
            }
            else
            {
                return new(ApplyStatus.Failed, Text.BadBlockCancelled);
            }

            before = current is null ? "?" : LightState.Describe(current, model);
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
            return new(ApplyStatus.Failed, Text.NotConfirmed, current);
        return new(ApplyStatus.Written, Text.Written(before, LightState.Describe(after, model)), after);
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
            Log.Write(Text.NotOpened(path, Marshal.GetLastPInvokeError()));
            handle.Dispose();
            return null;
        }
        var (input, output) = Native.ReportLengths(handle);
        if (input != ReportLength || output != ReportLength)
        {
            Log.Write(Text.BadReportSizes(input, output, path));
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
            throw new IOException(Text.NotSent(cmd));

        var input = new byte[ReportLength];
        long deadline = Environment.TickCount64 + 1000;
        while (true)
        {
            int left = (int)(deadline - Environment.TickCount64);
            if (left <= 0 || Transfer(write: false, input, left) < 0)
                throw new TimeoutException(Text.NoAnswer(cmd));
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
