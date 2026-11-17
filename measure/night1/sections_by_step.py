#!/usr/bin/env python3
"""sections_by_step.py RUN [--settle 5] [--arms off,def] [--top 25] [--steps N] [--bin 100]

"Where the time still goes": Si_PerfProbe sections (10 s intervals) per unit step and arm, in ms per FRAME
(interval total_ms / interval frames), so arms at different fps compare per frame. Intervals count only when they
lie completely inside one flip block (starting >= settle s after the flip) and, for plateau runs, inside one
plateau's measure window. Trickle runs (one measure row) are binned by the interval's world units (--bin).
Prints the highest --steps steps that have both arms, top --top sections by the first arm."""
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
ap.add_argument("--arms", default="off,def")
ap.add_argument("--top", type=int, default=25)
ap.add_argument("--steps", type=int, default=2)
ap.add_argument("--bin", type=int, default=100)
a = ap.parse_args()
arms_wanted = a.arms.split(",")

G = R.round_load(a.run)
flips = R.read_flips(a.run)
arms = R.split_arms(G, flips, a.settle)
lab = R.one_dir(a.run, "perflab", "scenarios.csv")
rows = [r for r in R.read_csv(os.path.join(lab, "scenarios.csv")) if r.get("measure_start_iso")]
wins = [(r["scenario"], R.parse_t(r["measure_start_iso"]), R.parse_t(r["measure_end_iso"]), r.get("plateau", "")) for r in rows]
trickle = len(wins) == 1 and wins[0][3] in ("-1", "")

acc = defaultdict(lambda: defaultdict(float))     # (step, arm) -> section -> ms
nfr = defaultdict(float)
nint = defaultdict(int)
nfix = defaultdict(float)
wall = defaultdict(float)
for arm, d in arms.items():
    if arm not in arms_wanted:
        continue
    for x in d["frames"]:
        t1 = R.parse_t(x["timestamp"])
        t0 = t1 - timedelta(seconds=x["_i"])
        step = None
        for sc, ws, we, pl in wins:
            if ws and we and ws <= t0 and t1 <= we:
                step = sc
                break
        if step is None:
            continue
        if trickle:
            u = R.fnum(x.get("units"))
            step = int(u // a.bin) * a.bin
        frames = R.fnum(x.get("frames"), 0)
        if not frames:
            continue
        key = (step, arm)
        nfr[key] += frames
        nint[key] += 1
        nfix[key] += R.fnum(x.get("fixed_steps"), 0)
        wall[key] += x["_i"]
        for (k, n), v in G["secs"].get(x["timestamp"], {}).items():
            if k in ("section", "subsystem", "phase"):
                acc[key][(k, n)] += v


def stepkey(s):
    if isinstance(s, int):
        return s
    try:
        return int(str(s).split("@")[-1])
    except ValueError:
        return 0


steps = sorted({k[0] for k in acc if all((k[0], ar) in acc for ar in arms_wanted)}, key=stepkey)[-a.steps:]
print(f"# Where the time goes: `{R.label_of(a.run)}` (PerfProbe sections, ms per frame; arms {', '.join(arms_wanted)}; "
      f"intervals inside one block, >= {a.settle:g} s after the flip{'; trickle binned by world units' if trickle else ''})\n")
for st in steps:
    ref = (st, arms_wanted[0])
    hdr = f"## step {st}{'-' + str(st + a.bin - 1) + ' world units' if trickle else ''}\n"
    print(hdr)
    print("intervals / frames / fps / fixed steps per frame: " + "; ".join(f"{ar} {nint[(st, ar)]} / {nfr[(st, ar)]:.0f} / {nfr[(st, ar)] / wall[(st, ar)]:.1f} / {nfix[(st, ar)] / nfr[(st, ar)]:.2f}" for ar in arms_wanted) + "\n")
    print("(FixedUpdate rows are per frame and scale with fixed steps per frame; divide by that column for ms per fixed step.)\n")
    print("| kind | name | " + " | ".join(arms_wanted) + " | change |")
    print("|---|---|" + "---:|" * len(arms_wanted) + "---:|")
    names = sorted(acc[ref], key=lambda k: -acc[ref][k])
    shown = 0
    for k in names:
        if k[0] == "phase" and shown > 0 and acc[ref][k] / nfr[ref] < 0.3:
            continue
        vals = [acc[(st, ar)].get(k, 0.0) / nfr[(st, ar)] for ar in arms_wanted]
        ch = f"{100 * (vals[-1] - vals[0]) / vals[0]:+.0f} %" if vals[0] > 0 else "-"
        print(f"| {k[0]} | {k[1][:90]} | " + " | ".join(f"{v:.3f}" for v in vals) + f" | {ch} |")
        shown += 1
        if shown >= a.top:
            break
    print()
