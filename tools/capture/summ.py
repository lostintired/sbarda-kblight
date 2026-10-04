"""Summarize keyboard (VID_19F5, MI_01) traffic from a capture jsonl."""
import json, sys, collections

path = sys.argv[1]
show_reads = "--reads" in sys.argv
rows = [json.loads(l) for l in open(path, encoding="utf-8") if l.strip()]
live = {}
kb = []
for r in rows:
    if r["api"] in ("CreateFileW", "CreateFileA"):
        live[r["h"]] = r["path"]
    h = r.get("h")
    if h and "19f5" in live.get(h, "").lower() and r["api"] in ("WriteFile", "ReadFile", "ReadDone", "HidD_SetFeature", "HidD_GetFeature", "HidD_SetOutputReport"):
        kb.append(r)
print("rows", len(rows), "kb events", len(kb), "last t", rows[-1]["t"] if rows else None)
print(collections.Counter((e["api"], e["data"][2:6]) for e in kb))
READ_CMDS = {"01", "02", "03", "04", "05", "07", "08", "0a", "a0"}
for e in kb:
    cmd = e["data"][4:6]
    if e["api"] == "WriteFile" and (show_reads or cmd not in READ_CMDS):
        print(e["t"], "W", e["data"][2:2 + 2 * 64])
    elif e["api"] != "WriteFile" and show_reads:
        print(e["t"], "R", e["data"][2:2 + 2 * 64])
