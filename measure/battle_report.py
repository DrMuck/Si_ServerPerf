#!/usr/bin/env python3
"""battle_report.py RUN [--settle 3] [--md out.md] [--csv prefix]
Per lab scenario and flip arm of a run_lab.ps1 flip run:
  * Si_PerfLab 1 Hz samples inside Measure (first --settle s after each flip dropped): fps median and p5 (per-second
    frame counts), frame work avg, units / projectiles / lab objects alive
  * Si_PerfProbe 10 s intervals completely inside one arm block: frame ms avg / p95 / p99 (medians over intervals),
    main-thread busy (1000 - frame-cap wait) ms/s, top sections and their delta to arm A
  * Si_ServerPerf status windows in the scenario: Targeting state per team (DetectedGrid / FriendlyFire lines), item
    counters, exceptions per arm (Diag running count deltas)"""
import argparse
import os
import re
import statistics as st
import sys
from collections import defaultdict
from datetime import timedelta

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ab_report as R  # noqa: E402


def pct(v, p):
    if not v:
        return float("nan")
    v = sorted(v)
    k = (len(v) - 1) * p / 100.0
    f = int(k)
    c = min(f + 1, len(v) - 1)
    return v[f] + (v[c] - v[f]) * (k - f)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("run")
    ap.add_argument("--settle", type=float, default=3.0)
    ap.add_argument("--md")
    ap.add_argument("--csv")
    a = ap.parse_args()
    run = a.run
    flips = R.read_flips(run)
    lab = R.one_dir(run, "perflab", "scenarios.csv")
    rows = [r for r in R.read_csv(os.path.join(lab, "scenarios.csv")) if r.get("status") == "ok"]
    smp = R.read_csv(os.path.join(lab, "samples.csv"))
    G = R.round_load(run)
    arms = R.split_arms(G, flips, a.settle)
    wins = R.parse_windows(run)
    P = []
    out_rows = []

    def arm_at(t):
        k = [i for i, f in enumerate(flips) if f[0] <= t]
        if not k:
            return None, 0.0, None
        nxt = flips[k[-1] + 1][0] if k[-1] + 1 < len(flips) else None
        return flips[k[-1]][1], (t - flips[k[-1]][0]).total_seconds(), nxt

    for r in rows:
        sc = r["scenario"]
        ms, me = R.parse_t(r["measure_start_iso"]), R.parse_t(r["measure_end_iso"])
        per = defaultdict(lambda: defaultdict(list))
        for s in smp:
            if s.get("scenario") != sc or s.get("phase") != "Measure":
                continue
            t = R.parse_t(s.get("timestamp_iso"))
            t0 = t - timedelta(seconds=1)
            arm, since, nxt = arm_at(t0)
            if arm is None or since < a.settle or (nxt and t > nxt):
                continue
            d = per[arm]
            d["fps"].append(R.fnum(s.get("frames")))
            d["work"].append(R.fnum(s.get("work_avg_ms")))
            d["units"].append(R.fnum(s.get("units")))
            d["proj"].append(R.fnum(s.get("projectiles")))
            d["alive"].append(R.fnum(s.get("lab_alive")))
        prob = {}
        for arm, dd in arms.items():
            fr = [x for x in dd["frames"] if R.parse_t(x["timestamp"]) - timedelta(seconds=x["_i"]) >= ms and R.parse_t(x["timestamp"]) <= me]
            wall = sum(x["_i"] for x in fr)
            acc = defaultdict(float)
            for x in fr:
                for k, v in G["secs"].get(x["timestamp"], {}).items():
                    acc[k] += v
            prob[arm] = dict(n=len(fr), avg=st.median([R.fnum(x["avg_ms"]) for x in fr]) if fr else float("nan"),
                             p95=st.median([R.fnum(x["p95_ms"]) for x in fr]) if fr else float("nan"),
                             p99=st.median([R.fnum(x["p99_ms"]) for x in fr]) if fr else float("nan"),
                             secs={k: v / wall for k, v in acc.items()} if wall else {})
        P.append(f"## {sc} (lab objects planned {r.get('objects_planned')}, alive at end {r.get('units_alive_end')} units + "
                 f"{r.get('structures_alive_end')} structures, projectiles avg {R.fmt(R.fnum(r.get('projectiles_avg')), 0)})")
        P.append("")
        P.append("| arm | s samples | world units | projectiles | fps median | fps p5 | work avg ms | probe n | frame avg / p95 / p99 ms | main busy ms/s |")
        P.append("|---|---:|---:|---:|---:|---:|---:|---:|---|---:|")
        for arm in sorted(per):
            d = per[arm]
            pr = prob.get(arm, {"n": 0, "avg": float("nan"), "p95": float("nan"), "p99": float("nan"), "secs": {}})
            busy = 1000 - pr["secs"].get(("phase", "TimeUpdate"), float("nan")) if pr["secs"] else float("nan")
            P.append(f"| {arm} | {len(d['fps'])} | {R.fmt(st.median(d['units']), 0)} | {R.fmt(st.median(d['proj']), 0)} | {R.fmt(st.median(d['fps']), 1)} | "
                     f"{R.fmt(pct(d['fps'], 5), 1)} | {R.fmt(st.mean(d['work']), 2)} | {pr['n']} | {R.fmt(pr['avg'], 1)} / {R.fmt(pr['p95'], 1)} / {R.fmt(pr['p99'], 1)} | {R.fmt(busy, 0)} |")
            out_rows.append([sc, arm, len(d["fps"]), st.median(d["units"]), st.median(d["proj"]), st.median(d["fps"]), pct(d["fps"], 5),
                             st.mean(d["work"]), pr["n"], pr["avg"], pr["p95"], pr["p99"], busy])
        P.append("")
        if "A" in prob and prob["A"]["secs"]:
            base = prob["A"]["secs"]
            keys = sorted([k for k in base if k[0] in ("section", "subsystem") and "(all)" not in k[1]], key=lambda k: -base[k])[:16]
            others = [x for x in sorted(prob) if x != "A"]
            P.append("| section (ms per wall-second) | A | " + " | ".join(f"{x} | {x}-A" for x in others) + " |")
            P.append("|---|---:|" + "---:|---:|" * len(others))
            for k in keys:
                P.append(f"| {k[0]}: {k[1]} | {base[k]:.1f} | " + " | ".join(f"{prob[x]['secs'].get(k, 0):.1f} | {prob[x]['secs'].get(k, 0) - base[k]:+.1f}" for x in others) + " |")
            P.append("")
        # Targeting + item status lines of the last window of each arm inside this scenario
        wsc = [w for w in wins if w["time"] and ms <= w["time"] <= me + timedelta(seconds=2)]
        last = {}
        for w in wsc:
            last[w["arm"]] = w
        for arm in sorted(k for k in last if k):
            w = last[arm]
            P.append(f"**{arm} (window ending {w['time'].strftime('%H:%M:%S')})**")
            for item in ("Targeting.DetectedGrid", "Targeting.CollectEnemies_Grid", "Targeting.FriendlyFire_Grid", "Targeting.FriendlyFire_StaleGuard",
                         "Targeting.LosTopK", "Targeting.FriendlyFire_Cache", "MultiCore.SensorPipeline_Rate", "UpdateTiers.Tier_IdleSoldiers"):
                if item in w["items"]:
                    stt, info = w["items"][item]
                    if stt.startswith("ON"):
                        P.append(f"- {item}: {info[:700]}")
            P.append("")
        # exceptions per arm in the scenario
        exc = defaultdict(int)
        prev = None
        for w in wins:
            v = w["items"].get("Headless.Diag_LogExceptions")
            if not v:
                continue
            mm = re.search(r"this period \(([^)]*)\): (\d+) logged", v[1])
            if not mm:
                continue
            n = int(mm.group(2))
            if prev is not None and w["time"] and ms <= w["time"] <= me + timedelta(seconds=35):
                exc[w["arm"]] += max(n - prev, 0)
            prev = n
        P.append("Exceptions/warnings counted per arm in this scenario: " + (", ".join(f"{k} {v}" for k, v in sorted(exc.items(), key=lambda kv: str(kv[0]))) or "none counted"))
        P.append("")
    text = "\n".join(P)
    print(text)
    if a.md:
        open(a.md, "w", encoding="utf-8").write(text + "\n")
    if a.csv:
        import csv
        with open(a.csv + "_battle.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["scenario", "arm", "samples", "world_units", "projectiles", "fps_median", "fps_p5", "work_avg_ms", "probe_n",
                        "frame_avg_ms", "frame_p95_ms", "frame_p99_ms", "main_busy_ms_s"])
            w.writerows(out_rows)


if __name__ == "__main__":
    main()
