"""Frida capture of sbarda.exe HID traffic (protocol research, see docs/PROTOCOL.md).

usage:
  python capture.py spawn <seconds> <out.jsonl> [app args...]   # kills a running sbarda.exe, starts a new one
  python capture.py attach <seconds> <out.jsonl>                 # attaches to the running sbarda.exe
  python summ.py <out.jsonl> [--reads]                           # then summarize the keyboard traffic

Requires `pip install frida` (tested with Frida 17.22). The captured sbarda.exe keeps running
afterwards: close it, then restore the user's lighting with `KbLight.exe --apply`.
"""
import frida, json, sys, time, os, subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = r"C:\Program Files (x86)\sbarda\sbarda.exe"
APPDIR = os.path.dirname(EXE)

mode = sys.argv[1]
seconds = float(sys.argv[2])
out_path = sys.argv[3]
extra_args = sys.argv[4:]

log = open(out_path, "w", encoding="utf-8")
def on_message(msg, data):
    if msg["type"] == "send":
        log.write(json.dumps(msg["payload"]) + "\n")
    else:
        log.write(json.dumps({"api": "error", "msg": msg}) + "\n")
    log.flush()

dev = frida.get_local_device()
if mode == "spawn":
    subprocess.run(["taskkill", "/F", "/IM", "sbarda.exe"], capture_output=True)
    time.sleep(1.0)
    pid = dev.spawn([EXE] + extra_args, cwd=APPDIR)
else:
    pid = next(p.pid for p in dev.enumerate_processes() if p.name.lower() == "sbarda.exe")
sess = dev.attach(pid)
script = sess.create_script(open(os.path.join(HERE, "hook.js"), encoding="utf-8").read())
script.on("message", on_message)
script.load()
if mode == "spawn":
    dev.resume(pid)
print("pid", pid, "capturing", seconds, "s ->", out_path, flush=True)
time.sleep(seconds)
log.flush()
print("done", flush=True)
os._exit(0)   # don't wait on script.unload(): it hangs; process exit detaches Frida
