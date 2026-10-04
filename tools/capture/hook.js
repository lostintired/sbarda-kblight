'use strict';

const kb = Process.getModuleByName('KERNELBASE.dll');
const tracked = {};           // handle -> path
const pendingReads = {};      // overlapped ptr -> {buf, len, h}
const t0 = Date.now();

function ts() { return Date.now() - t0; }
function hex(p, n) {
  if (p.isNull() || n <= 0) return '';
  const a = new Uint8Array(p.readByteArray(Math.min(n, 1024)));
  return Array.from(a).map(b => ('0' + b.toString(16)).slice(-2)).join('');
}
function caller(ra) {
  const m = Process.findModuleByAddress(ra);
  return m ? m.name + '+0x' + ra.sub(m.base).toString(16) : ra.toString();
}
function emit(o) { o.t = ts(); send(o); }

function hookCreate(name, reader) {
  Interceptor.attach(kb.getExportByName(name), {
    onEnter(args) { this.path = reader(args[0]); },
    onLeave(ret) {
      const p = (this.path || '').toLowerCase();
      if (p.indexOf('vid_') >= 0 || p.indexOf('hid') >= 0) {
        const h = ret.toString();
        if (!tracked[h] || tracked[h] !== this.path) {
          tracked[h] = this.path;
          emit({ api: name, path: this.path, h: h, caller: caller(this.returnAddress) });
        }
      }
    }
  });
}
hookCreate('CreateFileW', p => p.isNull() ? '' : p.readUtf16String());
hookCreate('CreateFileA', p => p.isNull() ? '' : p.readAnsiString());

Interceptor.attach(kb.getExportByName('CloseHandle'), {
  onEnter(args) {
    const h = args[0].toString();
    if (tracked[h]) { emit({ api: 'CloseHandle', h: h, path: tracked[h] }); delete tracked[h]; }
  }
});

Interceptor.attach(kb.getExportByName('WriteFile'), {
  onEnter(args) {
    const h = args[0].toString();
    if (!tracked[h]) return;
    const n = args[2].toInt32();
    emit({ api: 'WriteFile', h: h, len: n, data: hex(args[1], n), caller: caller(this.returnAddress) });
  }
});

Interceptor.attach(kb.getExportByName('ReadFile'), {
  onEnter(args) {
    const h = args[0].toString();
    if (!tracked[h]) return;
    this.h = h; this.buf = args[1]; this.len = args[2].toInt32(); this.nread = args[3]; this.ov = args[4];
  },
  onLeave(ret) {
    if (!this.h) return;
    if (ret.toInt32() !== 0) {
      const n = this.nread.isNull() ? this.len : this.nread.readU32();
      emit({ api: 'ReadFile', h: this.h, len: n, data: hex(this.buf, n) });
    } else if (!this.ov.isNull()) {
      pendingReads[this.ov.toString()] = { buf: this.buf, len: this.len, h: this.h };
    }
  }
});

Interceptor.attach(kb.getExportByName('GetOverlappedResult'), {
  onEnter(args) { this.ov = args[1].toString(); this.nb = args[2]; },
  onLeave(ret) {
    const pr = pendingReads[this.ov];
    if (!pr) return;
    if (ret.toInt32() !== 0) {
      const n = this.nb.isNull() ? pr.len : this.nb.readU32();
      emit({ api: 'ReadDone', h: pr.h, len: n, data: hex(pr.buf, n) });
      delete pendingReads[this.ov];
    }
  }
});

Interceptor.attach(kb.getExportByName('DeviceIoControl'), {
  onEnter(args) {
    const h = args[0].toString();
    if (!tracked[h]) return;
    this.h = h; this.code = args[1].toUInt32();
    this.inb = args[2]; this.inl = args[3].toInt32();
    this.outb = args[4]; this.outl = args[5].toInt32(); this.ret = args[6];
    this.ra = this.returnAddress;
  },
  onLeave(ret) {
    if (!this.h) return;
    const n = (!this.ret.isNull()) ? this.ret.readU32() : this.outl;
    emit({ api: 'DeviceIoControl', h: this.h, code: '0x' + this.code.toString(16), ok: ret.toInt32(),
           in: hex(this.inb, this.inl), out: hex(this.outb, Math.min(n, this.outl)), caller: caller(this.ra) });
  }
});

// hid.dll feature/output report helpers (resolved lazily by the app via GetProcAddress)
function hookHid() {
  const hid = Process.findModuleByName('hid.dll');
  if (!hid) return false;
  ['HidD_SetFeature', 'HidD_GetFeature', 'HidD_SetOutputReport', 'HidD_GetInputReport'].forEach(name => {
    const addr = hid.findExportByName(name);
    if (!addr) return;
    Interceptor.attach(addr, {
      onEnter(args) { this.h = args[0].toString(); this.buf = args[1]; this.len = args[2].toInt32(); this.ra = this.returnAddress;
        if (name.indexOf('Set') >= 0) emit({ api: name, h: this.h, len: this.len, data: hex(this.buf, this.len), caller: caller(this.ra) }); },
      onLeave(ret) {
        if (name.indexOf('Get') >= 0) emit({ api: name, h: this.h, ok: ret.toInt32(), len: this.len, data: hex(this.buf, this.len), caller: caller(this.ra) });
      }
    });
  });
  emit({ api: 'info', msg: 'hid.dll hooked' });
  return true;
}
if (!hookHid()) {
  const iv = setInterval(() => { if (hookHid()) clearInterval(iv); }, 50);
}
emit({ api: 'info', msg: 'hooks installed' });
