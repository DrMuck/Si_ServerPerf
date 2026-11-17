#!/usr/bin/env python3
"""methods_by_arm.py RUN --arms A,B [--settle 5] [--methods TargetFinder.CollectDetectedEnemies,...]

Si_PerfProbe methods.csv per measure window (plateau) and arm: ms per wall-second, calls per second and us per call,
over the 10 s intervals that lie completely inside one flip block (>= settle s after the flip) and one measure window."""
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
ap.add_argument("--methods", default="TargetFinder.CollectDetectedEnemies,BehaviorTrees.BTEnemyTargetDetected.ActualExecute,"
                "TargetFinder.FindBestTarget,TargetFinder.RequestScan,Silica.AI.AIBaseAgent.OrderedUpdateAllAgents,"
                "LineOfSightManager.PublishResults,AIManager.TickAI")
a = ap.parse_args()
arms = a.arms.split(",")
want = a.methods.split(",")
G = R.round_load(a.run)
flips = R.read_flips(a.run)
sp = R.split_arms(G, flips, a.settle)
lab = R.one_dir(a.run, "perflab", "scenarios.csv")
wins = [(r["scenario"], R.parse_t(r["measure_start_iso"]), R.parse_t(r["measure_end_iso"])) for r in R.read_csv(os.path.join(lab, "scenarios.csv")) if r.get("measure_start_iso")]
meth = defaultdict(dict)
for m in R.read_csv(os.path.join(G["probe"], "methods.csv")):
    k = m["class"] + "." + m["method"]
    d = meth[m["timestamp"]]
    c, t = d.get(k, (0.0, 0.0))
    d[k] = (c + R.fnum(m["calls"], 0), t + R.fnum(m["total_ms"], 0))
acc = defaultdict(lambda: defaultdict(lambda: [0.0, 0.0]))
wall = defaultdict(float)
for arm in arms:
    for x in sp.get(arm, {}).get("frames", []):
        t1 = R.parse_t(x["timestamp"])
        t0 = t1 - timedelta(seconds=x["_i"])
        sc = next((s for s, ws, we in wins if ws <= t0 and t1 <= we), None)
        if not sc:
            continue
        wall[(sc, arm)] += x["_i"]
        for k, (c, t) in meth.get(x["timestamp"], {}).items():
            acc[(sc, arm)][k][0] += c
            acc[(sc, arm)][k][1] += t
print(f"# Methods per arm: `{R.label_of(a.run)}` (PerfProbe methods.csv; ms/s, calls/s, us/call)\n")
for sc in sorted({s for s, _ in acc}):
    print(f"## {sc}\n")
    print("| method | " + " | ".join(f"{ar} ms/s | {ar} calls/s | {ar} us/call" for ar in arms) + " |")
    print("|---|" + "---:|" * (3 * len(arms)))
    for k in want:
        cells = []
        for ar in arms:
            w = wall.get((sc, ar), 0)
            c, t = acc[(sc, ar)].get(k, [0, 0]) if w else (0, 0)
            cells += ([f"{t / w:.1f}", f"{c / w:.0f}", f"{1000 * t / c:.1f}" if c else "-"] if w else ["-"] * 3)
        print(f"| {k} | " + " | ".join(cells) + " |")
    print("\nintervals (s): " + ", ".join(f"{ar} {wall.get((sc, ar), 0):.0f}" for ar in arms) + "\n")
