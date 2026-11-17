#!/usr/bin/env python3
"""guard_blocks.py RUN - behaviour guard from engagement.csv split per runtime_sig (Si_PerfLab 0.2.2,
engagement_split_by_sig). Each row name is <scenario>#<sig>; sig -> arm via CRC32 of the run's cfg_*.json copies and
flip-state labels in flips order. Per arm: mean and s.e. over blocks (windows >= 30 s) of deaths/s, kills/s,
shots per unit-s, hits dealt/s, with-target share; per team, per kind, and all lab units."""
import glob
import json
import math
import os
import sys
import zlib
from collections import defaultdict

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import ab_report as R  # noqa: E402

run = sys.argv[1]
man = json.load(open(os.path.join(run, "manifest.json"), encoding="utf-8-sig"))
states = man.get("flip_states") or []
labels = [r["state"] for r in R.read_csv(os.path.join(run, "flips.csv"))]
sig2arm = {}
for i, p in enumerate(states):
    cp = os.path.join(run, "cfg_" + os.path.basename(p))
    src = cp if os.path.exists(cp) else p
    sig = format(zlib.crc32(open(src, "rb").read()) & 0xFFFFFFFF, "08x")
    # label of this state = label used in flips.csv for the i-th state of the cycle
    lab = None
    seq = [l for l in labels if l not in ("S",)]
    if seq:
        lab = seq[i % len(seq)] if len(seq) >= len(states) else None
    sig2arm[sig] = lab or os.path.basename(p)
rows = R.read_csv(os.path.join(R.one_dir(run, "perflab", "engagement.csv"), "engagement.csv"))
blocks = defaultdict(lambda: defaultdict(lambda: [0.0] * 6))   # (block key) -> group -> [units, target, shots+att, hits, kills, deaths]
bmeta = {}
for r in rows:
    if "#" not in r["scenario"] or r["kind"] == "nonlab":
        continue
    sc, sig = r["scenario"].split("#", 1)
    ws = R.fnum(r["window_s"], 0)
    if ws < 30:
        continue
    key = (sc, r["window_start_iso"])
    bmeta[key] = (sig2arm.get(sig, sig), ws)
    for g in (("team", r["team"]), ("kind", r["kind"]), ("all", "all")):
        x = blocks[key][g]
        x[0] += R.fnum(r["units_avg"], 0); x[1] += R.fnum(r["with_target_avg"], 0)
        x[2] += R.fnum(r["shots"], 0) + R.fnum(r["creature_attacks"], 0); x[3] += R.fnum(r["hits_dealt"], 0)
        x[4] += R.fnum(r["kills"], 0); x[5] += R.fnum(r["deaths"], 0)
per = defaultdict(lambda: defaultdict(list))   # (sc, arm, group) -> metric -> list over blocks
for key, gs in blocks.items():
    arm, ws = bmeta[key]
    for g, x in gs.items():
        d = per[(key[0], arm, g)]
        d["deaths/s"].append(x[5] / ws); d["kills/s"].append(x[4] / ws)
        d["shots/unit-s"].append(x[2] / x[0] / ws if x[0] else float("nan")); d["hits/s"].append(x[3] / ws)
        d["with-target"].append(x[1] / x[0] if x[0] else float("nan"))


def ms(v):
    v = [q for q in v if not math.isnan(q)]
    if not v:
        return "-"
    m = sum(v) / len(v)
    se = (sum((q - m) ** 2 for q in v) / (len(v) - 1)) ** 0.5 / len(v) ** 0.5 if len(v) > 1 else float("nan")
    return f"{m:.3g} ± {se:.2g}"


metrics = ["deaths/s", "kills/s", "shots/unit-s", "hits/s", "with-target"]
print(f"# Behaviour guard per arm block: `{R.label_of(run)}` (mean ± s.e. over blocks; sig -> arm {sig2arm})\n")
print("| scenario | group | arm | blocks | " + " | ".join(metrics) + " |")
print("|---|---|---|---:|" + "---|" * len(metrics))
for (sc, arm, g) in sorted(per, key=lambda k: (k[0], k[2][0] != "all", k[2], k[1])):
    d = per[(sc, arm, g)]
    print(f"| {sc} | {g[0]}:{g[1]} | {arm} | {len(d['deaths/s'])} | " + " | ".join(ms(d[m]) for m in metrics) + " |")
