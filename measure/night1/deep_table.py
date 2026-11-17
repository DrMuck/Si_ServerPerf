#!/usr/bin/env python3
"""deep_table.py RUN PROBECFG - PerfProbe methods (total, self ms/s, calls/s, us/call) over the intervals inside the
measure window, for the probes added in PROBECFG beyond probe_lab.json; plus probe overhead per interval."""
import json
import os
import sys
from collections import defaultdict

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import ab_report as R  # noqa: E402

run, cfg = sys.argv[1], sys.argv[2]
base = set(json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "probe_lab.json")))["probes"])
added = [p for p in json.load(open(cfg, encoding="utf-8-sig"))["probes"] if p not in base]
probe = R.one_dir(run, "perfprobe", "frames.csv")
lab = R.one_dir(run, "perflab", "scenarios.csv")
w = [r for r in R.read_csv(os.path.join(lab, "scenarios.csv")) if r.get("measure_start_iso")][0]
ws, we = R.parse_t(w["measure_start_iso"]), R.parse_t(w["measure_end_iso"])
fr = [x for x in R.read_csv(os.path.join(probe, "frames.csv")) if R.parse_t(x["timestamp"]) and ws <= R.parse_t(x["timestamp"]) - __import__("datetime").timedelta(seconds=R.fnum(x["interval_s"], 10)) and R.parse_t(x["timestamp"]) <= we]
ts = {x["timestamp"] for x in fr}
wall = sum(R.fnum(x["interval_s"], 0) for x in fr)
ov = sum(R.fnum(x.get("overhead_ms_est"), 0) for x in fr)
fps = sum(R.fnum(x["fps"], 0) * R.fnum(x["interval_s"], 0) for x in fr) / wall if wall else float("nan")
acc = defaultdict(lambda: [0.0, 0.0, 0.0])
for m in R.read_csv(os.path.join(probe, "methods.csv")):
    if m["timestamp"] in ts:
        a = acc[m["class"] + ":" + m["method"]]
        a[0] += R.fnum(m["calls"], 0); a[1] += R.fnum(m["total_ms"], 0); a[2] += R.fnum(m["self_ms"], 0)
print(f"# Deep probe `{R.label_of(run)}`: {len(fr)} intervals, {wall:.0f} s inside the plateau, {fps:.1f} fps, probe overhead est {ov / wall:.1f} ms/s\n")
print("| probe | total ms/s | self ms/s | calls/s | us/call (total) |")
print("|---|---:|---:|---:|---:|")
for p in added:
    a = acc.get(p)
    if not a:
        print(f"| {p} | not reported | | | |")
        continue
    print(f"| {p} | {a[1] / wall:.1f} | {a[2] / wall:.1f} | {a[0] / wall:.0f} | {1000 * a[1] / a[0] if a[0] else float('nan'):.1f} |")
