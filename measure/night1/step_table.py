#!/usr/bin/env python3
"""step_table.py RUN [--settle 5] [--bin 50] [--by plateau|units] [--arms off,def,agg]

Per unit step (plateau, or --bin lab-unit bins for a trickle) and arm, from Si_PerfLab 1 Hz samples in Measure
(first --settle s after each flip dropped): fps (frames / seconds), frame work ms (frame-weighted), main-thread work
ms per wall-second (work_avg_ms x frames / s), mean lab units, seconds. Then the change of each arm vs the first."""
import argparse
import os
import sys
from collections import defaultdict
from datetime import timedelta

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import ab_report as R  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("--settle", type=float, default=5.0)
ap.add_argument("--bin", type=int, default=50)
ap.add_argument("--by", default="auto")
ap.add_argument("--arms", default="off,def,agg")
ap.add_argument("--units", choices=["lab", "world"], default="lab")
a = ap.parse_args()
arms = a.arms.split(",")
lab = R.one_dir(a.run, "perflab", "samples.csv")
smp = R.read_csv(os.path.join(lab, "samples.csv"))
flips = R.read_flips(a.run)
plateau_mode = a.by == "plateau" or (a.by == "auto" and any("@" in (s.get("scenario") or "") for s in smp))
acc = defaultdict(lambda: [0.0, 0.0, 0.0, 0.0, 0])   # frames, work*frames, secs, units, n
blk = defaultdict(lambda: [None, 0.0, 0.0])          # (step, flip index) -> arm, frames, work*frames
for s in smp:
    if s.get("phase") != "Measure":
        continue
    t = R.parse_t(s.get("timestamp_iso"))
    if not t or not flips:
        continue
    t0 = t - timedelta(seconds=1)
    idx = max((i for i, f in enumerate(flips) if f[0] <= t0), default=None)
    if idx is None or (t0 - flips[idx][0]).total_seconds() < a.settle:
        continue
    if idx + 1 < len(flips) and t > flips[idx + 1][0]:
        continue
    arm = flips[idx][1]
    fr = R.fnum(s.get("frames"), 0)
    w = R.fnum(s.get("work_avg_ms"), 0)
    u = R.fnum(s.get("lab_units" if a.units == "lab" else "units"), 0)
    if plateau_mode:
        key = s["scenario"]
    else:
        key = int(u // a.bin) * a.bin
    x = acc[(key, arm)]
    x[0] += fr; x[1] += w * fr; x[2] += 1.0; x[3] += u; x[4] += 1
    b = blk[(key, idx)]; b[0] = arm; b[1] += fr; b[2] += w * fr


def sk(k):
    return int(str(k).split("@")[-1]) if plateau_mode else k


keys = sorted({k for k, _ in acc}, key=sk)
print(f"# Steps x arms: `{R.label_of(a.run)}` (Si_PerfLab 1 Hz Measure samples, {a.settle:g} s after each flip dropped)\n")
print("| step | " + " | ".join(f"{ar} fps | {ar} work ms | {ar} main ms/s | {ar} n s" for ar in arms) + " | " + " | ".join(f"{ar} vs {arms[0]} work" for ar in arms[1:]) + " |")
print("|---|" + "---:|" * (4 * len(arms) + len(arms) - 1))
for k in keys:
    cells, works = [], {}
    for ar in arms:
        x = acc.get((k, ar))
        if not x or not x[2]:
            cells += ["-"] * 4
            continue
        fps = x[0] / x[2]
        work = x[1] / x[0] if x[0] else float("nan")
        works[ar] = work
        cells += [f"{fps:.1f}", f"{work:.2f}", f"{work * fps:.0f}", f"{x[4]}"]
    ch = []
    for ar in arms[1:]:
        ch.append(f"{100 * (works[ar] - works[arms[0]]) / works[arms[0]]:+.0f} %" if ar in works and arms[0] in works else "-")
    label = k if plateau_mode else f"{k}-{k + a.bin - 1}"
    print(f"| {label} | " + " | ".join(cells) + " | " + " | ".join(ch) + " |")

# paired: each block of arm X minus the mean of the nearest earlier and later block of the reference arm (same step)
ref = arms[0]
print(f"\nPaired (each block minus its neighbouring `{ref}` blocks in the same step; mean, s.e., n blocks):\n")
print("| step | " + " | ".join(f"{ar} - {ref} ms | % | s.e. | n" for ar in arms[1:]) + " |")
print("|---|" + "---:|" * (4 * (len(arms) - 1)))
for k in keys:
    bl = sorted(((i, v) for (kk, i), v in blk.items() if kk == k and v[1] >= 60), key=lambda z: z[0])
    cells = []
    for ar in arms[1:]:
        d = []
        for j, (i, v) in enumerate(bl):
            if v[0] != ar:
                continue
            prev = next((v2 for i2, v2 in reversed(bl[:j]) if v2[0] == ref), None)
            nxt = next((v2 for i2, v2 in bl[j + 1:] if v2[0] == ref), None)
            nb = [z[2] / z[1] for z in (prev, nxt) if z]
            if nb:
                d.append(v[2] / v[1] - sum(nb) / len(nb))
        x = acc.get((k, ref))
        base = x[1] / x[0] if x and x[0] else float("nan")
        if d:
            mu = sum(d) / len(d)
            se = (sum((q - mu) ** 2 for q in d) / (len(d) - 1)) ** 0.5 / len(d) ** 0.5 if len(d) > 1 else float("nan")
            cells += [f"{mu:+.2f}", f"{100 * mu / base:+.1f}", f"{se:.2f}", str(len(d))]
        else:
            cells += ["-"] * 4
    label = k if plateau_mode else f"{k}-{k + a.bin - 1}"
    print(f"| {label} | " + " | ".join(cells) + " |")