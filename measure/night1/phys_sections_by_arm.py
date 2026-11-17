#!/usr/bin/env python3
"""phys_sections_by_arm.py RUN [--settle 5] [--names a;b;c] (rig 2) - PerfProbe sections.csv per measure window and flip arm:
ms per wall-second, calls/s and ms per call of selected sections (default: fixed phase, physics steps, PhysX
simulate subsystem, OUM fixed all, VehicleWheeled.OrderedFixedUpdate). Only 10 s intervals that lie completely inside
one flip block (starting >= settle s after the flip) and one measure window count."""
import argparse, csv, glob, os
from collections import defaultdict
from datetime import datetime, timedelta

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("--settle", type=float, default=5.0)
ap.add_argument("--names", default="phase:FixedUpdate;section:GameManager.Update: physics steps (fixed scripts + Physics.Simulate);"
                "subsystem:FixedUpdate.PhysicsFixedUpdate;section:Ordered Update Manager - fixed (all);"
                "section:Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate;"
                "section:Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdatePartTwo")
ap.add_argument("--list", default="")
a = ap.parse_args()
P = lambda s: datetime.fromisoformat(s.replace(" ", "T"))
fl = [(P(r["timestamp_iso"]), r["state"]) for r in csv.DictReader(open(os.path.join(a.run, "flips.csv")))]
sc = glob.glob(os.path.join(a.run, "perflab", "*", "scenarios.csv"))[0]
wins = [(r["scenario"], P(r["measure_start_iso"]), P(r["measure_end_iso"])) for r in csv.DictReader(open(sc)) if r.get("measure_start_iso")]
secf = glob.glob(os.path.join(a.run, "perfprobe", "*", "sections.csv"))[0]
want = [tuple(x.split(":", 1)) for x in a.names.split(";") if x]
if a.list:
    seen = set()
    for r in csv.DictReader(open(secf)):
        if a.list.lower() in r["name"].lower():
            seen.add((r["kind"], r["name"]))
    for k in sorted(seen):
        print(k)
    raise SystemExit
acc = defaultdict(lambda: [0.0, 0.0])
ivs = defaultdict(set)
for r in csv.DictReader(open(secf)):
    if (r["kind"], r["name"]) not in want:
        continue
    t1 = P(r["timestamp"]); t0 = t1 - timedelta(seconds=10)
    k = [i for i, f in enumerate(fl) if f[0] <= t0 - timedelta(seconds=a.settle)]
    if not k:
        continue
    i = k[-1]
    if i + 1 < len(fl) and fl[i + 1][0] < t1:
        continue
    w = next((s for s, b, e in wins if b <= t0 and t1 <= e), None)
    if not w:
        continue
    x = acc[(w, fl[i][1], r["kind"], r["name"])]
    x[0] += float(r["total_ms"]); x[1] += float(r["calls"])
    ivs[(w, fl[i][1])].add(r["timestamp"])
arms = []
for (w, ar, _, _) in acc:
    if ar not in arms:
        arms.append(ar)
for w in sorted({k[0] for k in acc}):
    print(f"\n## {w}\n")
    print("| section | " + " | ".join(f"{ar} ms/s | {ar} calls/s | {ar} ms/call" for ar in arms) + " |")
    print("|---|" + "---:|" * (3 * len(arms)))
    for kind, name in want:
        cells = []
        for ar in arms:
            x = acc.get((w, ar, kind, name)); n = len(ivs[(w, ar)]) * 10.0
            cells += [f"{x[0] / n:.1f}", f"{x[1] / n:.0f}", f"{x[0] / x[1]:.3f}" if x[1] else "-"] if x and n else ["-"] * 3
        print(f"| {kind}:{name[-60:]} | " + " | ".join(cells) + " |")
    print("\nintervals (s): " + ", ".join(f"{ar} {len(ivs[(w, ar)]) * 10:.0f}" for ar in arms))
