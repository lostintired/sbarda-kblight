"""Diagnostic HID client for the sbarda keyboard vendor interface (VID 19F5, MI_01).

Talks to the keyboard directly, without KbLight, to see what it really holds.
Protocol notes: docs/PROTOCOL.md. Close sbarda.exe first and do not run this while KbLight
is writing: kbtool does not take KbLight's device mutex. After experiments restore the
user's lighting with `KbLight.exe --apply`.

usage (--pid XXXX before the command picks one keyboard model, e.g. --pid FB2A):
  python kbtool.py list            # VID 19F5 HID interfaces with usage and report sizes
  python kbtool.py info            # cmd 03: firmware version bytes and build date
  python kbtool.py read            # cmd 05: the 32-byte settings block, lighting bytes decoded
  python kbtool.py setmode <effect> [brightness 0..100] [speed 0..4] [dir] [multicolor] [colorindex] [RRGGBB]
"""
import ctypes, ctypes.wintypes as wt, sys, time

setupapi = ctypes.WinDLL("setupapi", use_last_error=True)
hid = ctypes.WinDLL("hid", use_last_error=True)
k32 = ctypes.WinDLL("kernel32", use_last_error=True)

class GUID(ctypes.Structure):
    _fields_ = [("d1", wt.DWORD), ("d2", wt.WORD), ("d3", wt.WORD), ("d4", ctypes.c_ubyte * 8)]

class SP_DEVICE_INTERFACE_DATA(ctypes.Structure):
    _fields_ = [("cbSize", wt.DWORD), ("guid", GUID), ("flags", wt.DWORD), ("reserved", ctypes.c_void_p)]

class HIDD_ATTRIBUTES(ctypes.Structure):
    _fields_ = [("Size", wt.ULONG), ("VendorID", wt.USHORT), ("ProductID", wt.USHORT), ("Version", wt.USHORT)]

class HIDP_CAPS(ctypes.Structure):
    _fields_ = [("Usage", wt.USHORT), ("UsagePage", wt.USHORT), ("InputReportByteLength", wt.USHORT),
                ("OutputReportByteLength", wt.USHORT), ("FeatureReportByteLength", wt.USHORT),
                ("Reserved", wt.USHORT * 17), ("rest", wt.USHORT * 10)]

class OVERLAPPED(ctypes.Structure):
    _fields_ = [("Internal", ctypes.c_void_p), ("InternalHigh", ctypes.c_void_p),
                ("Offset", wt.DWORD), ("OffsetHigh", wt.DWORD), ("hEvent", wt.HANDLE)]

setupapi.SetupDiGetClassDevsW.restype = wt.HANDLE
setupapi.SetupDiGetClassDevsW.argtypes = [ctypes.POINTER(GUID), wt.LPCWSTR, wt.HWND, wt.DWORD]
setupapi.SetupDiEnumDeviceInterfaces.argtypes = [wt.HANDLE, ctypes.c_void_p, ctypes.POINTER(GUID), wt.DWORD, ctypes.POINTER(SP_DEVICE_INTERFACE_DATA)]
setupapi.SetupDiGetDeviceInterfaceDetailW.argtypes = [wt.HANDLE, ctypes.POINTER(SP_DEVICE_INTERFACE_DATA), ctypes.c_void_p, wt.DWORD, ctypes.POINTER(wt.DWORD), ctypes.c_void_p]
setupapi.SetupDiDestroyDeviceInfoList.argtypes = [wt.HANDLE]
k32.CreateFileW.restype = wt.HANDLE
k32.CreateFileW.argtypes = [wt.LPCWSTR, wt.DWORD, wt.DWORD, ctypes.c_void_p, wt.DWORD, wt.DWORD, wt.HANDLE]
k32.CreateEventW.restype = wt.HANDLE
k32.CreateEventW.argtypes = [ctypes.c_void_p, wt.BOOL, wt.BOOL, wt.LPCWSTR]
k32.ResetEvent.argtypes = [wt.HANDLE]
k32.WriteFile.argtypes = [wt.HANDLE, ctypes.c_void_p, wt.DWORD, ctypes.POINTER(wt.DWORD), ctypes.POINTER(OVERLAPPED)]
k32.ReadFile.argtypes = [wt.HANDLE, ctypes.c_void_p, wt.DWORD, ctypes.POINTER(wt.DWORD), ctypes.POINTER(OVERLAPPED)]
k32.GetOverlappedResult.argtypes = [wt.HANDLE, ctypes.POINTER(OVERLAPPED), ctypes.POINTER(wt.DWORD), wt.BOOL]
k32.WaitForSingleObject.argtypes = [wt.HANDLE, wt.DWORD]
k32.CancelIo.argtypes = [wt.HANDLE]
k32.CloseHandle.argtypes = [wt.HANDLE]
hid.HidD_GetAttributes.argtypes = [wt.HANDLE, ctypes.POINTER(HIDD_ATTRIBUTES)]
hid.HidD_GetPreparsedData.argtypes = [wt.HANDLE, ctypes.POINTER(ctypes.c_void_p)]
hid.HidD_FreePreparsedData.argtypes = [ctypes.c_void_p]
hid.HidP_GetCaps.argtypes = [ctypes.c_void_p, ctypes.POINTER(HIDP_CAPS)]

INVALID = wt.HANDLE(-1).value
GENERIC_RW = 0x80000000 | 0x40000000
SHARE_RW = 1 | 2
OPEN_EXISTING = 3
FILE_FLAG_OVERLAPPED = 0x40000000


def hid_paths():
    g = GUID()
    hid.HidD_GetHidGuid(ctypes.byref(g))
    hdi = setupapi.SetupDiGetClassDevsW(ctypes.byref(g), None, None, 0x12)  # PRESENT|DEVICEINTERFACE
    out = []
    i = 0
    while True:
        did = SP_DEVICE_INTERFACE_DATA(); did.cbSize = ctypes.sizeof(did)
        if not setupapi.SetupDiEnumDeviceInterfaces(hdi, None, ctypes.byref(g), i, ctypes.byref(did)):
            break
        need = wt.DWORD()
        setupapi.SetupDiGetDeviceInterfaceDetailW(hdi, ctypes.byref(did), None, 0, ctypes.byref(need), None)
        buf = ctypes.create_string_buffer(need.value)
        ctypes.c_uint32.from_buffer(buf).value = 8 if ctypes.sizeof(ctypes.c_void_p) == 8 else 6
        if setupapi.SetupDiGetDeviceInterfaceDetailW(hdi, ctypes.byref(did), buf, need, None, None):
            out.append(ctypes.wstring_at(ctypes.addressof(buf) + 4))
        i += 1
    setupapi.SetupDiDestroyDeviceInfoList(hdi)
    return out


def caps_of(h):
    pp = ctypes.c_void_p()
    if not hid.HidD_GetPreparsedData(h, ctypes.byref(pp)):
        return None
    c = HIDP_CAPS()
    hid.HidP_GetCaps(pp, ctypes.byref(c))
    hid.HidD_FreePreparsedData(pp)
    return c


def settings_interfaces(pid=None):
    """VID 19F5 interfaces with 65-byte input and output reports: the settings channel (MI_01 on ZH99 HE)."""
    out = []
    for p in hid_paths():
        low = p.lower()
        if "vid_19f5" not in low or (pid and f"pid_{pid.lower()}" not in low):
            continue
        h = k32.CreateFileW(p, 0, SHARE_RW, None, OPEN_EXISTING, 0, None)
        if h in (INVALID, None):
            continue
        c = caps_of(h)
        k32.CloseHandle(h)
        if c and c.InputReportByteLength == 65 and c.OutputReportByteLength == 65:
            out.append(p)
    return out


class Keyboard:
    def __init__(self, pid=None):
        cands = settings_interfaces(pid)
        if not cands:
            raise SystemExit("keyboard vendor interface not found" + (f" for PID {pid}" if pid else ""))
        self.path = cands[0]
        self.h = k32.CreateFileW(self.path, GENERIC_RW, SHARE_RW, None, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, None)
        if self.h == INVALID or self.h is None:
            raise SystemExit(f"open failed: {ctypes.get_last_error()}")
        c = caps_of(self.h)
        self.out_len, self.in_len = c.OutputReportByteLength, c.InputReportByteLength
        self.ev = k32.CreateEventW(None, True, False, None)

    def close(self):
        k32.CloseHandle(self.h); k32.CloseHandle(self.ev)

    def _io(self, fn, buf, n, timeout_ms):
        ov = OVERLAPPED(); ov.hEvent = self.ev
        k32.ResetEvent(self.ev)
        got = wt.DWORD()
        ok = fn(self.h, buf, n, None, ctypes.byref(ov))
        if not ok:
            err = ctypes.get_last_error()
            if err != 997:  # ERROR_IO_PENDING
                raise OSError(f"io error {err}")
            if k32.WaitForSingleObject(self.ev, timeout_ms) != 0:
                k32.CancelIo(self.h)
                k32.GetOverlappedResult(self.h, ctypes.byref(ov), ctypes.byref(got), True)
                return None
        k32.GetOverlappedResult(self.h, ctypes.byref(ov), ctypes.byref(got), True)
        return got.value

    def xfer(self, cmd, length=0, offset=0, data=b"", timeout_ms=1000):
        p = bytearray(64)
        p[0], p[1] = 0x55, cmd
        p[4] = length
        p[5], p[6] = offset & 0xFF, (offset >> 8) & 0xFF
        p[8:8 + len(data)] = data
        p[3] = sum(p[4:]) & 0xFF
        out = (ctypes.c_ubyte * self.out_len)(*([0] + list(p) + [0] * (self.out_len - 65)))
        if self._io(k32.WriteFile, out, self.out_len, 1000) is None:
            raise OSError("write timeout")
        deadline = time.time() + timeout_ms / 1000
        while time.time() < deadline:
            inb = (ctypes.c_ubyte * self.in_len)()
            n = self._io(k32.ReadFile, inb, self.in_len, int((deadline - time.time()) * 1000) + 1)
            if not n:
                break
            r = bytes(inb)[1:65]
            if r[0] == 0xAA and r[1] == cmd:
                if (sum(r[4:]) & 0xFF) != r[3]:
                    print(f"  warn: bad checksum in reply to {cmd:02x}")
                return r
        raise OSError(f"no reply to cmd {cmd:02x}")


def main():
    a = sys.argv[1:]
    pid = None
    if a[:1] == ["--pid"] and len(a) > 1:
        pid, a = a[1], a[2:]
    if not a or a[0] == "list":
        for p in hid_paths():
            if "vid_19f5" in p.lower() and (not pid or f"pid_{pid.lower()}" in p.lower()):
                h = k32.CreateFileW(p, 0, SHARE_RW, None, OPEN_EXISTING, 0, None)
                c = caps_of(h) if h not in (INVALID, None) else None
                info = f"page={c.UsagePage:04x} usage={c.Usage:04x} in={c.InputReportByteLength} out={c.OutputReportByteLength} feat={c.FeatureReportByteLength}" if c else "?"
                print(p, info)
                if h not in (INVALID, None): k32.CloseHandle(h)
        return
    kb = Keyboard(pid)
    try:
        if a[0] == "info":
            r = kb.xfer(0x03)
            print(r.hex(" "))
            print(r[8:8 + r[4]])
        elif a[0] == "read":
            kb.xfer(0x01)
            r = kb.xfer(0x05, 0x20, 0)
            kb.xfer(0x02)
            d = r[8:8 + 0x20]
            print("raw :", r[:8].hex(" "), "|", d.hex(" "))
            print(f"mode={d[8]} bright={d[9]} speed(fw)={d[10]} (ui {4 - d[10]}) dir={d[11]} colorful={d[12]} colorindex={d[13]} rgb={d[14]:02x}{d[15]:02x}{d[16]:02x}")
        elif a[0] == "setmode":
            mode = int(a[1], 0)
            kb.xfer(0x01)
            r = kb.xfer(0x05, 0x20, 0)
            d = bytearray(r[8:8 + 0x20])
            d[8] = mode
            if len(a) > 2: d[9] = int(a[2], 0)
            if len(a) > 3: d[10] = 4 - int(a[3], 0)
            if len(a) > 4: d[11] = int(a[4], 0)
            if len(a) > 5: d[12] = int(a[5], 0)
            if len(a) > 6: d[13] = int(a[6], 0)
            if len(a) > 7:
                rgb = bytes.fromhex(a[7]); d[14], d[15], d[16] = rgb[0], rgb[1], rgb[2]
            w = kb.xfer(0x06, 0x20, 0, bytes(d))
            print("write reply:", w[:12].hex(" "))
            time.sleep(0.4)
            kb.xfer(0x02)
            kb.xfer(0x01)
            r = kb.xfer(0x05, 0x20, 0)
            kb.xfer(0x02)
            print("readback:", r[8:8 + 0x20].hex(" "))
    finally:
        kb.close()


if __name__ == "__main__":
    main()
