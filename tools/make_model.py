"""Builds a KbLight keyboard model file from the connected keyboard and the sbarda database.

The connected VID 19F5 keyboard gives VID, PID and the settings interface (65-byte reports);
sbarda's database (%LOCALAPPDATA%\\sbarda Files\\db\\*.db, table t_light_data of the active profile)
gives the effect codes and their options (config_func bits, docs/PROTOCOL.md §5).
Effects that need sbarda running (custom per-key light 0x100, music 0x200) are left out.
Only reads: nothing is sent to the keyboard. Standard library only.

usage:
  python make_model.py --name "<model name>" [--pid XXXX] [--db <path to .db>] [--out <file.json>]

By default the file goes to %LOCALAPPDATA%\\KbLight\\models\\<vid>-<pid>.json, where KbLight picks it up
without a restart. Check every effect with `kbtool.py setmode <code>` before relying on the file:
docs/ADDING-A-KEYBOARD.md.
"""
import argparse, glob, json, os, re, sqlite3, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kbtool  # noqa: E402

OPTION_BITS = [
    (0x01, "brightness"),
    (0x02, "speed"),
    (0x10, "color"),
    (0x20, "multicolor"),
    (0x04, "direction-horizontal"),
    (0x08, "direction-vertical"),
    (0x40, "direction-radial"),
    (0x80, "direction-rotation"),
]
NEEDS_SBARDA = 0x100 | 0x200


def find_keyboard(pid):
    paths = kbtool.settings_interfaces(pid)
    found = {}
    for p in paths:
        m = re.search(r"vid_([0-9a-f]{4})&pid_([0-9a-f]{4})&mi_([0-9a-f]{2})", p, re.I)
        if m:
            found.setdefault((m.group(1).upper(), m.group(2).upper()), int(m.group(3), 16))
    if not found:
        sys.exit("no VID 19F5 keyboard with a settings interface found" + (f" for PID {pid}" if pid else ""))
    if len(found) > 1:
        sys.exit("several keyboards found, pick one with --pid: " + ", ".join(f"{v}:{p}" for v, p in found))
    (vid, pid), interface = next(iter(found.items()))
    return vid, pid, interface


def find_db(path):
    if path:
        return path
    dbs = glob.glob(os.path.expandvars(r"%LOCALAPPDATA%\sbarda Files\db\*.db"))
    if len(dbs) != 1:
        sys.exit("sbarda database not found, pass --db <path>" if not dbs else "several databases, pass --db: " + ", ".join(dbs))
    return dbs[0]


def read_effects(db):
    con = sqlite3.connect(f"file:{db}?mode=ro", uri=True)
    try:
        rows = con.execute(
            "select mode, config_func from t_light_data"
            " where profile = (select profile from t_profile_data where status = 1) order by mode").fetchall()
    finally:
        con.close()
    effects = []
    for mode, func in rows:
        if mode == 255 or func & NEEDS_SBARDA or any(e["id"] == mode for e in effects):
            continue
        effects.append({"id": mode, "options": [name for bit, name in OPTION_BITS if func & bit]})
    if not effects:
        sys.exit(f"no effects in t_light_data of the active profile in {db}")
    return effects


def render(model):
    # Same layout as src/models/*.json: one effect per line.
    lines = ["{"]
    for key in ("name", "vid", "pid", "interface"):
        lines.append(f"  {json.dumps(key)}: {json.dumps(model[key], ensure_ascii=False)},")
    lines.append('  "effects": [')
    for i, e in enumerate(model["effects"]):
        comma = "," if i < len(model["effects"]) - 1 else ""
        lines.append(f'    {{ "id": {e["id"]}, "options": {json.dumps(e["options"])} }}{comma}')
    lines += ["  ]", "}", ""]
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--name", required=True, help="model name shown in KbLight's window")
    ap.add_argument("--pid", help="PID of the keyboard if several are connected, e.g. FB2A")
    ap.add_argument("--db", help="path to sbarda's .db file")
    ap.add_argument("--out", help="output file; default %%LOCALAPPDATA%%\\KbLight\\models\\<vid>-<pid>.json")
    a = ap.parse_args()

    vid, pid, interface = find_keyboard(a.pid)
    model = {"name": a.name, "vid": vid, "pid": pid, "interface": interface, "effects": read_effects(find_db(a.db))}
    out = a.out or os.path.expandvars(rf"%LOCALAPPDATA%\KbLight\models\{vid.lower()}-{pid.lower()}.json")
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(render(model))
    print(f"{out}: {model['name']} {vid}:{pid}, interface {interface}, {len(model['effects'])} effects")


if __name__ == "__main__":
    main()
