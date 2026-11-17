#!/usr/bin/env python3
"""cpu_by_arm.py RUN --arms S3,P [--settle 5] - OS CPU per arm and measure window from PerfProbe frames.csv
(process / main thread / other threads CPU ms per wall-second, cores busy), intervals fully inside one block."""
import argparse
import os
import sys
from collections import defaultdict
from datetime import timedelta

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import ab_report as R  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("--arms", required=True)
ap.add_argument("--settle", type=float, default=5.0)
a = ap.parse_args()
G = R.round_load(a.run)
sp = R.split_arms(G, R.read_flips(a.run), a.settle)
lab = R.one_dir(a.run, "perflab", "scenarios.csv")
wins = [(r["scenario"], R.parse_t(r["measure_start_iso"]), R.parse_t(r["measure_end_iso"])) for r in R.read_csv(os.path.join(lab, "scenarios.csv")) if r.get("measure_start_iso")]
acc = defaultdict(lambda: [0.0] * 5)
for ar in a.arms.split(","):
    for x in sp.get(ar, {}).get("frames", []):
        t1 = R.parse_t(x["timestamp"]); t0 = t1 - timedelta(seconds=x["_i"])
        sc = next((s for s, ws, we in wins if ws <= t0 and t1 <= we), None)
        if not sc:
            continue
        v = acc[(sc, ar)]
        v[0] += x["_i"]; v[1] += R.fnum(x.get("process_cpu_ms"), 0); v[2] += R.fnum(x.get("main_thread_cpu_ms"), 0)
        v[3] += R.fnum(x.get("other_threads_cpu_ms"), 0); v[4] += R.fnum(x.get("fps"), 0) * x["_i"]
print("| plateau | arm | s | fps | process CPU ms/s | main thread ms/s | other threads ms/s |")
print("|---|---|---:|---:|---:|---:|---:|")
for (sc, ar), v in sorted(acc.items()):
    w = v[0]
    print(f"| {sc} | {ar} | {w:.0f} | {v[4] / w:.1f} | {v[1] / w:.0f} | {v[2] / w:.0f} | {v[3] / w:.0f} |")
