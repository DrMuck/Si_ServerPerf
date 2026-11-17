#!/usr/bin/env python3
"""dispose_by_arm.py RUN [--settle 60] (rig 2) - per measure window and flip arm, only the part >= settle s after a flip:
[Physics] deaddispose target / targetMin / targetMax / fps (mean, min, max over the 10 s lines), census dead share
(deadU / (deadU + aliveU)), world units and lab units from the 1 Hz samples."""
import argparse, csv, glob, os, re
from collections import defaultdict
from datetime import datetime, timedelta

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("--settle", type=float, default=60.0)
a = ap.parse_args()
P = lambda s: datetime.fromisoformat(s.replace(" ", "T"))
fl = [(P(r["timestamp_iso"]), r["state"]) for r in csv.DictReader(open(os.path.join(a.run, "flips.csv")))]
lab = glob.glob(os.path.join(a.run, "perflab", "*"))[0]
wins = [(r["scenario"], P(r["measure_start_iso"]), P(r["measure_end_iso"])) for r in csv.DictReader(open(os.path.join(lab, "scenarios.csv"))) if r.get("measure_start_iso")]
day = wins[0][1].date().isoformat()


def where(t):
    k = [i for i, f in enumerate(fl) if f[0] <= t]
    if not k or (t - fl[k[-1]][0]).total_seconds() < a.settle:
        return None
    w = next((s for s, b, e in wins if b <= t <= e), None)
    return (w, fl[k[-1]][1]) if w else None


dd = defaultdict(lambda: defaultdict(list))
for line in open(os.path.join(a.run, "Latest.log"), encoding="utf-8", errors="replace"):
    m = re.match(r"^\[(\d\d:\d\d:\d\d\.\d+)\].*\[Physics\] (deaddispose|census) (.*)$", line.rstrip())
    if not m:
        continue
    key = where(datetime.fromisoformat(f"{day}T{m.group(1)}"))
    if not key:
        continue
    kv = dict(re.findall(r"(\w+)=(-?[\d.]+)", m.group(3).split("| dead ")[-1] if m.group(2) == "census" else m.group(3)))
    if m.group(2) == "deaddispose":
        for k in ("target", "targetMin", "targetMax", "fps", "trackedNow", "lowered"):
            if k in kv:
                dd[key][k].append(float(kv[k].rstrip("s")))
    elif "deadU" in kv and "aliveU" in kv:
        dd[key]["deadshare"].append(float(kv["deadU"]) / max(1.0, float(kv["deadU"]) + float(kv["aliveU"])))
        dd[key]["deadU"].append(float(kv["deadU"]))
for r in csv.DictReader(open(os.path.join(lab, "samples.csv"))):
    if r.get("phase") != "Measure":
        continue
    key = where(P(r["timestamp_iso"]))
    if key:
        dd[key]["world"].append(float(r["units"] or 0)); dd[key]["lab"].append(float(r["lab_units"] or 0))
print("| window | arm | lines | target mean (min-max of targetMin..targetMax) | smoothed fps | trackedNow | deadU | dead share | world units | lab units |")
print("|---|---|---:|---|---:|---:|---:|---:|---:|---:|")
for (w, arm), x in sorted(dd.items()):
    mean = lambda k: (sum(x[k]) / len(x[k])) if x[k] else float("nan")
    tg = f"{mean('target'):.2f} ({min(x['targetMin']):.2f}-{max(x['targetMax']):.2f})" if x["target"] else "-"
    print(f"| {w} | {arm} | {len(x['target'])} | {tg} | {mean('fps'):.1f} | {mean('trackedNow'):.0f} | {mean('deadU'):.0f} | {100 * mean('deadshare'):.1f} % | {mean('world'):.0f} | {mean('lab'):.0f} |")
