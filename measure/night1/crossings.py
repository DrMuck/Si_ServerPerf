#!/usr/bin/env python3
"""crossings.py RUN --arms off,def,new [--settle 5] [--bin 25] - trickle runs: per arm, fps per lab-unit bin (frames / s
from the 1 Hz Measure samples, settle s after each flip dropped) and the lab-unit count where fps first falls below
30 / 20 / 10 (linear interpolation between bin centres of a running-minimum curve, so a noisy bin cannot undo a crossing)."""
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
ap.add_argument("--bin", type=int, default=25)
a = ap.parse_args()
arms = a.arms.split(",")
smp = R.read_csv(os.path.join(R.one_dir(a.run, "perflab", "samples.csv"), "samples.csv"))
fl = R.read_flips(a.run)
acc = defaultdict(lambda: [0.0, 0.0])
for s in smp:
    if s.get("phase") != "Measure":
        continue
    t = R.parse_t(s["timestamp_iso"])
    k = [i for i, f in enumerate(fl) if f[0] <= t - timedelta(seconds=1)]
    if not k or (t - fl[k[-1]][0]).total_seconds() < a.settle + 1 or (k[-1] + 1 < len(fl) and t > fl[k[-1] + 1][0]):
        continue
    b = int(R.fnum(s["lab_units"], 0) // a.bin) * a.bin
    x = acc[(fl[k[-1]][1], b)]
    x[0] += R.fnum(s["frames"], 0); x[1] += 1
print(f"# fps crossings: `{R.label_of(a.run)}` ({a.bin}-unit lab bins)\n")
print("| arm | below 30 fps at | below 20 fps at | below 10 fps at | bins |")
print("|---|---:|---:|---:|---|")
for ar in arms:
    pts = sorted((b + a.bin / 2, x[0] / x[1]) for (arm, b), x in acc.items() if arm == ar and x[1] >= 3)
    run_min, curve = 1e9, []
    for u, f in pts:
        run_min = min(run_min, f)
        curve.append((u, run_min))
    cells = []
    for thr in (30, 20, 10):
        c = "-"
        for (u0, f0), (u1, f1) in zip(curve, curve[1:]):
            if f0 >= thr > f1:
                c = f"{u0 + (u1 - u0) * (f0 - thr) / (f0 - f1):.0f}"
                break
        if c == "-" and curve and curve[0][1] < thr:
            c = f"<{curve[0][0]:.0f}"
        cells.append(c)
    print(f"| {ar} | " + " | ".join(cells) + " | " + ", ".join(f"{u:.0f}:{f:.0f}" for u, f in pts) + " |")
