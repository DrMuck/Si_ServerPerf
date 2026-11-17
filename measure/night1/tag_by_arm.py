#!/usr/bin/env python3
"""tag_by_arm.py RUN TAG [--settle 20] [--segment dead] [--keys a,b,c]

Median per (measure window, arm) of the numeric key=value pairs of `[Module] TAG ...` log lines (e.g. `[Physics]
deadsettle`, `[Physics] census` with --segment dead to read only the `| dead ...` part). Lines count only inside a
measure window and >= --settle s after the last flip."""
import argparse
import os
import re
import statistics as st
import sys
from collections import defaultdict

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import ab_report as R  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("tag")
ap.add_argument("--settle", type=float, default=20.0)
ap.add_argument("--segment", default="")
ap.add_argument("--keys", default="")
a = ap.parse_args()
flips = R.read_flips(a.run)
lab = R.one_dir(a.run, "perflab", "scenarios.csv")
wins = [(r["scenario"], R.parse_t(r["measure_start_iso"]), R.parse_t(r["measure_end_iso"])) for r in R.read_csv(os.path.join(lab, "scenarios.csv")) if r.get("measure_start_iso")]
day = wins[0][1].date().isoformat()
rx = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d+)\].*\] " + re.escape(a.tag) + r" (.*)$")
acc = defaultdict(list)
for line in open(os.path.join(a.run, "Latest.log"), encoding="utf-8", errors="replace"):
    m = rx.match(line.rstrip())
    if not m:
        continue
    t = R.parse_t(f"{day}T{m.group(1)}")
    k = [i for i, f in enumerate(flips) if f[0] <= t]
    arm, since = (flips[k[-1]][1], (t - flips[k[-1]][0]).total_seconds()) if k else ("-", 1e9)
    sc = next((s for s, x, y in wins if x <= t <= y), None)
    if not sc or since < a.settle:
        continue
    body = m.group(2)
    if a.segment:
        seg = [p for p in body.split("|") if p.strip().startswith(a.segment)]
        if not seg:
            continue
        body = seg[0]
    acc[(sc, arm)].append({kk: float(v) for kk, v in re.findall(r"(\w+)=(-?[\d.]+)", body)})
keys = a.keys.split(",") if a.keys else None
for (sc, arm), lst in sorted(acc.items()):
    ks = keys or list(lst[-1].keys())
    vals = ", ".join(f"{kk}={st.median([d[kk] for d in lst if kk in d]):g}" for kk in ks if any(kk in d for d in lst))
    print(f"| {sc} | {arm} | {len(lst)} | {vals} |")
