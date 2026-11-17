#!/usr/bin/env python3
"""engagement_summary.py RUN [RUN ...] - per measure window (plateau or trickle): lab units avg, share of lab units
with a current target, shots + creature attacks per unit-second, deaths/s, kills/s (all arms together; engagement
counters are arm-independent bookkeeping, the arm mix is the same in every window)."""
import os
import sys
from collections import defaultdict

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import ab_report as R  # noqa: E402

print("| run | window | lab units avg | with target share | shots+attacks / unit-s | deaths/s | kills/s |")
print("|---|---|---:|---:|---:|---:|---:|")
for run in sys.argv[1:]:
    lab = R.one_dir(run, "perflab", "engagement.csv")
    if not lab:
        print(f"| {R.label_of(run)} | (no engagement.csv) | | | | | |")
        continue
    acc = defaultdict(lambda: [0.0, 0.0, 0.0, 0.0, 0.0, 0.0])
    order = []
    for r in R.read_csv(os.path.join(lab, "engagement.csv")):
        if r["kind"] == "nonlab":
            continue
        k = r["scenario"]
        if k not in acc:
            order.append(k)
        x = acc[k]
        u = R.fnum(r["units_avg"], 0)
        x[0] += u
        x[1] += R.fnum(r["with_target_avg"], 0)
        x[2] += R.fnum(r["shots"], 0) + R.fnum(r["creature_attacks"], 0)
        x[3] += R.fnum(r["deaths"], 0)
        x[4] += R.fnum(r["kills"], 0)
        x[5] = R.fnum(r["window_s"], 1)
    for k in order:
        u, wt, sh, de, ki, ws = acc[k]
        print(f"| {R.label_of(run)} | {k} | {u:.0f} | {wt / u if u else 0:.2f} | {sh / (u * ws) if u else 0:.2f} | {de / ws:.2f} | {ki / ws:.2f} |")
