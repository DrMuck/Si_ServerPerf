#!/usr/bin/env python3
"""physics_report.py RUN [--settle 30] - per flip arm and lab scenario: wheeled asleep share from the
[Physics] census lines, the last [Physics] wheeledsleep line, PhysX / FixedUpdate ms/s from PerfProbe intervals,
and the Physics items' counters (avoidCalls/avoidSkip/forcedSleep/reSleep/sleepBlockedByForce/forcedNow)."""
import os
import re
import sys
from collections import defaultdict
from datetime import timedelta

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ab_report as R  # noqa: E402

run = sys.argv[1]
settle = float(sys.argv[sys.argv.index("--settle") + 1]) if "--settle" in sys.argv else 30.0
flips = R.read_flips(run)
labdir = R.one_dir(run, "perflab", "scenarios.csv")
rows = R.read_csv(os.path.join(labdir, "scenarios.csv"))
wins = [(r["scenario"], R.parse_t(r["measure_start_iso"]), R.parse_t(r["measure_end_iso"])) for r in rows if r.get("status") == "ok"]
day = wins[0][1].date().isoformat() if wins else None


def arm_at(t):
    k = [i for i, f in enumerate(flips) if f[0] <= t]
    if not k:
        return None, 0.0
    return flips[k[-1]][1], (t - flips[k[-1]][0]).total_seconds()


def scen_at(t):
    for s, a, b in wins:
        if a <= t <= b:
            return s
    return None


cen = defaultdict(list)
ws = defaultdict(list)
extra = {tag: defaultdict(list) for tag in ("idlesleep", "avoidpairs", "avoidresting", "parkeddrift")}
EXTRA = {tag: re.compile(r"^\[(\d\d:\d\d:\d\d\.\d+)\].*\[Physics\] " + tag + r" (.*)$") for tag in extra}
CEN = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d+)\].*\[Physics\] census (.*)$")
WS = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d+)\].*\[Physics\] wheeledsleep (.*)$")
with open(os.path.join(run, "Latest.log"), encoding="utf-8", errors="replace") as f:
    for line in f:
        line = line.rstrip()
        for rx, store in [(CEN, cen), (WS, ws)] + [(EXTRA[k], extra[k]) for k in extra]:
            m = rx.match(line)
            if not m:
                continue
            t = R.parse_t(f"{day}T{m.group(1)}")
            sc = scen_at(t)
            arm, since = arm_at(t)
            if sc and arm and since >= settle:
                store[(sc, arm)].append((t, m.group(2)))

print(f"# Physics arms in `{R.label_of(run)}` (census/wheeledsleep lines >= {settle:g} s after a flip)\n")
print("| scenario | arm | census lines | wheeled asleep (median of dyn sl/dyn) | all dyn asleep | avoidCalls | avoidSkip | forcedSleep | reSleep | sleepBlockedByForce | forcedNow |")
print("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")


def num(rx, s):
    m = re.search(rx, s)
    return float(m.group(1)) if m else float("nan")


for (sc, arm), lst in sorted(cen.items()):
    wh, al = [], []
    agg = defaultdict(list)
    for t, s in lst:
        m = re.search(r"wheeled u=\d+ dyn=(\d+) sl=(\d+)", s)
        if m and int(m.group(1)):
            wh.append(int(m.group(2)) / int(m.group(1)))
        d, a = num(r" dyn=(\d+) dynAsleep", s), num(r"dynAsleep=(\d+)", s)
        if d:
            al.append(a / d)
        for k in ("avoidCalls", "avoidSkip", "forcedSleep", "reSleep", "sleepBlockedByForce", "forcedNow"):
            agg[k].append(num(k + r"=(\d+)", s))
    med = lambda v: R.st.median(v) if v else float("nan")
    print(f"| {sc} | {arm} | {len(lst)} | {100 * med(wh):.0f} % | {100 * med(al):.0f} % | " +
          " | ".join(f"{med(agg[k]):.0f}" for k in ("avoidCalls", "avoidSkip", "forcedSleep", "reSleep", "sleepBlockedByForce", "forcedNow")) + " |")
print()
for tag, st_ in extra.items():
    if not st_:
        continue
    print(f"## Last {tag} line per scenario and arm\n")
    for (sc, arm), lst in sorted(st_.items()):
        print(f"- **{sc} / {arm}** ({len(lst)} lines): `{lst[-1][1][:1200]}`")
    print()
print("## Last wheeledsleep line per scenario and arm\n")
for (sc, arm), lst in sorted(ws.items()):
    print(f"- **{sc} / {arm}**: `{lst[-1][1][:900]}`")
print()
# PhysX / FixedUpdate ms/s per arm from PerfProbe 10 s intervals
G = R.round_load(run)
arms = R.split_arms(G, flips, settle)
print("## PerfProbe ms per wall-second per arm (intervals fully inside a block, starting >= settle after the flip)\n")
print("| scenario | arm | intervals | PhysX simulate | FixedUpdate phase | ordered fixed (all) | CollisionAvoidance fixed | frame-cap wait |")
print("|---|---|---:|---:|---:|---:|---:|---:|")
for sc, a, b in wins:
    for arm, d in sorted(arms.items()):
        fr = [x for x in d["frames"] if R.parse_t(x["timestamp"]) - timedelta(seconds=x["_i"]) >= a and R.parse_t(x["timestamp"]) <= b]
        wall = sum(x["_i"] for x in fr)
        if not wall:
            continue
        acc = defaultdict(float)
        for x in fr:
            for k, v in G["secs"].get(x["timestamp"], {}).items():
                acc[k] += v
        g = lambda k: acc.get(k, 0.0) / wall
        print(f"| {sc} | {arm} | {len(fr)} | {g(('subsystem', 'FixedUpdate.PhysicsFixedUpdate')):.1f} | {g(('phase', 'FixedUpdate')):.1f} | "
              f"{g(('section', 'Ordered Update Manager - fixed (all)')):.1f} | {g(('section', 'Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate')):.1f} | {g(('phase', 'TimeUpdate')):.1f} |")
