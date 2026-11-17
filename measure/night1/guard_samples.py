#!/usr/bin/env python3
"""guard_samples.py RUN [--settle 15] - behaviour guard per flip block from the 1 Hz Si_PerfLab samples (all lab units
together; flips.csv decides the arm). Per arm and plateau: mean and s.e. over blocks of deaths/s, kills/s, shots per
unit-s, hits/s, with-target share, fps, frame work; plus the arm difference (Welch t) for each metric."""
import argparse
import math
import os
import sys
from collections import defaultdict
from datetime import timedelta

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import ab_report as R  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("--settle", type=float, default=15.0)
a = ap.parse_args()
smp = R.read_csv(os.path.join(R.one_dir(a.run, "perflab", "samples.csv"), "samples.csv"))
fl = R.read_flips(a.run)
blk = defaultdict(lambda: [0.0] * 8)   # (scenario, flip idx) -> n, deaths, kills, shots, hits, units, target, frames, work*frames
arm_of = {}
for s in smp:
    if s.get("phase") != "Measure":
        continue
    t = R.parse_t(s["timestamp_iso"])
    k = [i for i, f in enumerate(fl) if f[0] <= t - timedelta(seconds=1)]
    if not k or (t - fl[k[-1]][0]).total_seconds() < a.settle + 1 or (k[-1] + 1 < len(fl) and t > fl[k[-1] + 1][0]):
        continue
    key = (s["scenario"], k[-1])
    arm_of[key] = fl[k[-1]][1]
    x = blk[key]
    fr = R.fnum(s["frames"], 0)
    x[0] += 1
    for i, c in enumerate(["deaths", "kills", "shots", "hits", "lab_units", "with_target"]):
        x[i + 1] += R.fnum(s.get(c), 0)
    x[7] += fr
    blk[key] = x
    blk[key + ("w",)] = blk.get(key + ("w",), 0) + R.fnum(s["work_avg_ms"], 0) * fr if False else 0
per = defaultdict(lambda: defaultdict(list))
work = defaultdict(lambda: [0.0, 0.0])
for s in smp:
    pass
for key, x in blk.items():
    if len(key) != 2 or x[0] < 20:
        continue
    n = x[0]
    d = per[(key[0], arm_of[key])]
    d["deaths/s"].append(x[1] / n); d["kills/s"].append(x[2] / n); d["shots/unit-s"].append(x[3] / x[5] if x[5] else float("nan"))
    d["hits/s"].append(x[4] / n); d["with-target"].append(x[6] / x[5] if x[5] else float("nan")); d["fps"].append(x[7] / n)


def st(v):
    v = [q for q in v if not math.isnan(q)]
    m = sum(v) / len(v)
    s = (sum((q - m) ** 2 for q in v) / (len(v) - 1)) ** 0.5 if len(v) > 1 else float("nan")
    return m, s, len(v)


metrics = ["deaths/s", "kills/s", "shots/unit-s", "hits/s", "with-target", "fps"]
print(f"# Behaviour guard per flip block (1 Hz samples, >= {a.settle:g} s after each flip, blocks >= 20 s): `{R.label_of(a.run)}`\n")
print("| plateau | arm | blocks | " + " | ".join(metrics) + " |")
print("|---|---|---:|" + "---|" * len(metrics))
scs = sorted({k[0] for k in per})
for sc in scs:
    arms = sorted(ar for s2, ar in per if s2 == sc)
    for ar in arms:
        d = per[(sc, ar)]
        cells = []
        for mtr in metrics:
            m, s, n = st(d[mtr])
            cells.append(f"{m:.3g} ± {s / math.sqrt(n):.2g}" if n > 1 else f"{m:.3g}")
        print(f"| {sc} | {ar} | {len(d['fps'])} | " + " | ".join(cells) + " |")
    if len(arms) == 2:
        a0, a1 = arms
        cells = []
        for mtr in metrics:
            m0, s0, n0 = st(per[(sc, a0)][mtr]); m1, s1, n1 = st(per[(sc, a1)][mtr])
            se = math.sqrt((s0 ** 2) / n0 + (s1 ** 2) / n1) if n0 > 1 and n1 > 1 else float("nan")
            cells.append(f"{100 * (m1 - m0) / m0:+.1f} % (t {(m1 - m0) / se:+.1f})" if se and not math.isnan(se) and m0 else "-")
        print(f"| {sc} | {a1} vs {a0} | | " + " | ".join(cells) + " |")
