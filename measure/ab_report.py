#!/usr/bin/env python3
"""
ab_report.py - baseline / treatment comparison for the Si_ServerPerf measurement rig.

Run folders are the ones run_lab.ps1 writes (manifest.json, perflab/<run>/, perfprobe/<run>/, flips.csv).

  python ab_report.py lab   --a RUN [RUN ...] [--b RUN [RUN ...]] [--md out.md] [--csv prefix]
  python ab_report.py round --a RUN [--b RUN] [--levels 300,500] [--md out.md] [--csv prefix]
  python ab_report.py flip  --run RUN [--settle 5] [--md out.md]

lab:   per scenario and run: Si_PerfLab frame work (CPU per frame without the 120 fps cap wait) avg/p95/p99,
       fps, and from Si_PerfProbe intervals inside the measure window: main-thread CPU ms per wall-second,
       process cores busy. Per group (A = baseline passes, B = treatment passes): mean and spread
       (max - min). With --b: delta B - A, delta %, and |delta| / noise where noise = the larger of the two
       groups' spreads (>= 2 = clearly beyond run-to-run noise, < 1 = inside it).
round: 20 s PerfProbe intervals of round 1 (phase game), 50-unit bins [k*50, k*50+50) as in DEV_REPORT
       section 1 (medians); section/subsystem shares in main-thread ms per wall-second at the given unit
       levels (+-15 %, placement-storm intervals > 50 ms/s excluded, as levels_table.py).
flip:  one run with alternating runtime.json states (flips.csv): Si_PerfLab 1 Hz samples in measure phases,
       per flip block mean work, per scenario A vs B mean and the paired block difference with its std error.
--csv writes machine-readable tables (<prefix>_*.csv) so a later treatment run can be diffed row by row.
"""
import argparse
import csv
import glob
import json
import math
import os
import statistics as st
import sys
from collections import defaultdict
from datetime import datetime, timedelta


def fnum(s, d=float("nan")):
    try:
        return d if s in (None, "") else float(s)
    except ValueError:
        return d


def read_csv(p):
    if not p or not os.path.exists(p):
        return []
    with open(p, newline="", encoding="utf-8-sig") as f:
        return list(csv.DictReader(f))


def parse_t(s):
    for fmt in ("%Y-%m-%dT%H:%M:%S.%f", "%Y-%m-%dT%H:%M:%S", "%Y-%m-%d %H:%M:%S"):
        try:
            return datetime.strptime(s, fmt)
        except (ValueError, TypeError):
            pass
    return None


def fmt(v, nd=2):
    if v is None or (isinstance(v, float) and math.isnan(v)):
        return "-"
    return f"{v:.{nd}f}"


def one_dir(run, sub, must):
    ds = [d for d in glob.glob(os.path.join(run, sub, "*")) if os.path.exists(os.path.join(d, must))]
    if not ds:
        return None
    return max(ds, key=lambda d: os.path.getsize(os.path.join(d, must)))


def label_of(run):
    m = os.path.join(run, "manifest.json")
    if os.path.exists(m):
        with open(m, encoding="utf-8-sig") as f:
            return json.load(f).get("label", os.path.basename(run))
    return os.path.basename(os.path.normpath(run))


# ------------------------------------------------------------------ lab
LAB_METRICS = ["work_avg_ms", "work_p95_ms", "work_p99_ms", "fps", "main_ms_s", "cores"]


def lab_run(run):
    lab = one_dir(run, "perflab", "scenarios.csv")
    probe = one_dir(run, "perfprobe", "frames.csv")
    rows = read_csv(os.path.join(lab, "scenarios.csv")) if lab else []
    frames = read_csv(os.path.join(probe, "frames.csv")) if probe else []
    sections = read_csv(os.path.join(probe, "sections.csv")) if probe else []
    s_by_ts = defaultdict(list)
    for s in sections:
        s_by_ts[s["timestamp"]].append(s)
    for fr in frames:
        fr["_t"] = parse_t(fr.get("timestamp"))
        fr["_i"] = fnum(fr.get("interval_s"), 10.0)
    out, secs_out = {}, {}
    for r in rows:
        if r.get("status") != "ok":
            continue
        ms, me = parse_t(r.get("measure_start_iso")), parse_t(r.get("measure_end_iso"))
        fin = [x for x in frames if x["_t"] and ms and me and x["_t"] - timedelta(seconds=x["_i"]) >= ms - timedelta(seconds=1)
               and x["_t"] <= me + timedelta(seconds=1)]
        wall = sum(x["_i"] for x in fin)
        main = sum(fnum(x.get("main_thread_cpu_ms"), 0) for x in fin)
        cores = sum(fnum(x.get("cpu_cores_busy"), 0) * x["_i"] for x in fin)
        d = {k: fnum(r.get(k)) for k in ("work_avg_ms", "work_p50_ms", "work_p95_ms", "work_p99_ms", "work_max_ms", "dt_avg_ms", "fps",
                                         "units_alive_end", "projectiles_avg", "objects_planned")}
        d.update(family=r.get("family", ""), probe_intervals=len(fin), main_ms_s=(main / wall) if wall else float("nan"),
                 cores=(cores / wall) if wall else float("nan"))
        out[r["scenario"]] = d
        acc = defaultdict(float)
        for x in fin:
            for s in s_by_ts.get(x["timestamp"], []):
                acc[(s["kind"], s["name"])] += fnum(s.get("total_ms"), 0)
        secs_out[r["scenario"]] = {k: v / wall for k, v in acc.items()} if wall else {}
    not_ok = [(r["scenario"], r.get("status"), r.get("notes", "")) for r in rows if r.get("status") != "ok"]
    return dict(label=label_of(run), lab=lab, probe=probe, scen=out, secs=secs_out, order=[r["scenario"] for r in rows], not_ok=not_ok)


def group_stats(runs, scen, metric):
    vals = [r["scen"][scen][metric] for r in runs if scen in r["scen"] and not math.isnan(r["scen"][scen][metric])]
    if not vals:
        return float("nan"), float("nan"), []
    return st.mean(vals), (max(vals) - min(vals)) if len(vals) > 1 else float("nan"), vals


def cmd_lab(a):
    A = [lab_run(r) for r in a.a]
    B = [lab_run(r) for r in (a.b or [])]
    order = []
    for r in A + B:
        for s in r["order"]:
            if s not in order:
                order.append(s)
    P = []
    P.append(f"A (baseline): {', '.join(r['label'] for r in A)}" + (f"; B: {', '.join(r['label'] for r in B)}" if B else ""))
    P.append("")
    for r in A + B:
        P.append(f"- `{r['label']}`: lab `{r['lab']}`, probe `{r['probe']}`" + (f"; not ok: {r['not_ok']}" if r["not_ok"] else ""))
    P.append("")
    for metric, title in (("work_avg_ms", "frame work avg (ms)"), ("work_p95_ms", "frame work p95 (ms)"),
                          ("main_ms_s", "main-thread CPU (ms per wall-second)"), ("fps", "fps")):
        P.append(f"### {title}")
        P.append("")
        hdr = "| scenario | " + " | ".join(r["label"] for r in A) + " | A mean | A spread | A spread % |"
        sep = "|---|" + "---:|" * (len(A) + 3)
        if B:
            hdr += " " + " | ".join(r["label"] for r in B) + " | B mean | delta | delta % | abs(delta)/noise |"
            sep += "---:|" * (len(B) + 4)
        P.append(hdr)
        P.append(sep)
        for s in order:
            ma, sa, va = group_stats(A, s, metric)
            line = f"| {s} | " + " | ".join(fmt(r["scen"].get(s, {}).get(metric, float("nan"))) for r in A)
            line += f" | {fmt(ma)} | {fmt(sa)} | {fmt(100 * sa / ma if ma else float('nan'), 1)} |"
            if B:
                mb, sb, vb = group_stats(B, s, metric)
                noise = max(x for x in (sa, sb, 0.0) if not math.isnan(x))
                dlt = mb - ma
                line += " " + " | ".join(fmt(r["scen"].get(s, {}).get(metric, float("nan"))) for r in B)
                line += f" | {fmt(mb)} | {fmt(dlt)} | {fmt(100 * dlt / ma if ma else float('nan'), 1)} | {fmt(abs(dlt) / noise if noise > 0 else float('nan'), 1)} |"
            P.append(line)
        P.append("")
    if B:
        P.append("### Largest section changes per scenario (B mean - A mean, main-thread ms per wall-second)")
        P.append("")
        for s in order:
            keys = set()
            for r in A + B:
                keys |= set(r["secs"].get(s, {}))
            dl = []
            for k in keys:
                if k[0] not in ("section", "subsystem") or "(all)" in k[1]:
                    continue
                va = [r["secs"].get(s, {}).get(k, 0.0) for r in A if s in r["secs"]]
                vb = [r["secs"].get(s, {}).get(k, 0.0) for r in B if s in r["secs"]]
                if va and vb:
                    dl.append((k, st.mean(vb) - st.mean(va), st.mean(va)))
            dl.sort(key=lambda t: t[1])
            if dl:
                P.append(f"- **{s}**: " + "; ".join(f"{k[1]} {d:+.1f} (A {base:.1f})" for k, d, base in dl[:6]))
        P.append("")
    text = "\n".join(P)
    print(text)
    if a.md:
        open(a.md, "w", encoding="utf-8").write(text + "\n")
    if a.csv:
        with open(a.csv + "_lab_runs.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            cols = ["work_avg_ms", "work_p50_ms", "work_p95_ms", "work_p99_ms", "work_max_ms", "dt_avg_ms", "fps", "main_ms_s", "cores",
                    "probe_intervals", "units_alive_end", "projectiles_avg", "objects_planned"]
            w.writerow(["group", "run", "scenario", "family"] + cols)
            for g, rs in (("A", A), ("B", B)):
                for r in rs:
                    for s in order:
                        if s in r["scen"]:
                            d = r["scen"][s]
                            w.writerow([g, r["label"], s, d["family"]] + [d[c] for c in cols])
        with open(a.csv + "_lab_summary.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["group", "scenario", "metric", "n", "mean", "spread", "spread_pct"])
            for g, rs in (("A", A), ("B", B)):
                if not rs:
                    continue
                for s in order:
                    for m in LAB_METRICS:
                        mean, spr, vals = group_stats(rs, s, m)
                        w.writerow([g, s, m, len(vals), mean, spr, 100 * spr / mean if mean and not math.isnan(spr) else ""])
        with open(a.csv + "_lab_sections.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["group", "run", "scenario", "kind", "name", "ms_per_s"])
            for g, rs in (("A", A), ("B", B)):
                for r in rs:
                    for s, dd in r["secs"].items():
                        for (k, n), v in sorted(dd.items(), key=lambda kv: -kv[1]):
                            if v >= 0.05:
                                w.writerow([g, r["label"], s, k, n, round(v, 3)])


# ------------------------------------------------------------------ round
def round_load(run, storm=50.0):
    probe = one_dir(run, "perfprobe", "frames.csv")
    frames = [x for x in read_csv(os.path.join(probe, "frames.csv")) if x.get("phase") == "game" and x.get("round_id") == "1"]
    sections = read_csv(os.path.join(probe, "sections.csv"))
    s_by_ts = defaultdict(dict)
    for s in sections:
        if s.get("round_id") not in (None, "", "1"):
            continue
        k = (s["kind"], s["name"])
        s_by_ts[s["timestamp"]][k] = s_by_ts[s["timestamp"]].get(k, 0.0) + fnum(s.get("total_ms"), 0)
    for x in frames:
        x["_i"] = fnum(x.get("interval_s"), 20.0)
        pl = sum(v for (k, n), v in s_by_ts.get(x["timestamp"], {}).items()
                 if k == "section" and "UpdateAllActivePlacementsEnqueueGroundSlopeCheck" in n)
        x["_storm"] = pl / x["_i"] > storm
    return dict(label=label_of(run), probe=probe, frames=frames, secs=s_by_ts)


def bins_of(R):
    b = defaultdict(list)
    for x in R["frames"]:
        u = fnum(x.get("units"))
        if not math.isnan(u):
            b[int(u // 50) * 50].append(x)
    out = {}
    for k, xs in sorted(b.items()):
        med = lambda c: st.median([fnum(x.get(c)) for x in xs if not math.isnan(fnum(x.get(c)))]) if xs else float("nan")
        mt = [fnum(x.get("main_thread_cpu_ms")) / x["_i"] for x in xs]
        ln = [v for v in (norm_load(x, R["secs"]) for x in xs) if not math.isnan(v)]
        out[k] = dict(n=len(xs), fps_median=med("fps"), fps_min=min(fnum(x.get("fps")) for x in xs), avg_ms_median=med("avg_ms"),
                      p95_ms_median=med("p95_ms"), p99_ms_median=med("p99_ms"), max_ms_median=med("max_ms"),
                      main_thread_share_median=med("main_thread_share"), main_ms_s_median=st.median(mt),
                      load_norm_median=st.median(ln) if ln else float("nan"),
                      structures_median=med("structures"), rigidbodies_median=med("rigidbodies"),
                      storm_intervals=sum(1 for x in xs if x["_storm"]))
    return out


def levels_of(R, levels, width=0.15):
    out = {}
    for L in levels:
        sel = [x for x in R["frames"] if not x["_storm"] and L * (1 - width) <= fnum(x.get("units"), -1) <= L * (1 + width)]
        wall = sum(x["_i"] for x in sel)
        if not wall:
            continue
        acc = defaultdict(float)
        for x in sel:
            for k, v in R["secs"].get(x["timestamp"], {}).items():
                acc[k] += v
        d = {k: v / wall for k, v in acc.items()}
        d[("derived", "main-thread busy (1000 - frame-cap wait)")] = 1000 - d.get(("phase", "TimeUpdate"), 0)
        d[("derived", "main-thread CPU (OS thread time)")] = sum(fnum(x.get("main_thread_cpu_ms"), 0) for x in sel) / wall
        meta = dict(n=len(sel), units=st.mean(fnum(x["units"]) for x in sel), structures=st.mean(fnum(x["structures"]) for x in sel),
                    fps=st.mean(fnum(x["fps"]) for x in sel))
        out[L] = (meta, d)
    return out


BIN_COLS = ["n", "fps_median", "fps_min", "avg_ms_median", "p95_ms_median", "p99_ms_median", "max_ms_median",
            "main_thread_share_median", "main_ms_s_median", "load_norm_median", "structures_median", "rigidbodies_median", "storm_intervals"]


def norm_load(x, secs):
    """Load normalised to the 120 fps cap (as REPORT.md section 1): per-frame cost * 120 + per-fixed-step cost * 25,
    ms per wall-second. Unlike OS main-thread time it keeps growing after the main thread saturates."""
    d = secs.get(x["timestamp"], {})
    iv = x["_i"]
    wait = d.get(("phase", "TimeUpdate"))
    fixed = d.get(("phase", "FixedUpdate"), 0.0)
    frames = fnum(x.get("frames"), 0)
    steps = fnum(x.get("fixed_steps"), 0)
    if wait is None or frames <= 0:
        return float("nan")
    busy = iv * 1000.0 - wait
    per_frame = (busy - fixed) / frames
    per_step = fixed / steps if steps > 0 else 0.0
    return per_frame * 120.0 + per_step * 25.0


def split_arms(R, flips, settle):
    """PerfProbe intervals lying completely inside one flip block, starting >= settle s after the flip."""
    arms = defaultdict(list)
    for x in R["frames"]:
        t1 = parse_t(x.get("timestamp"))
        if not t1:
            continue
        t0 = t1 - timedelta(seconds=x["_i"])
        idx = max((i for i, f in enumerate(flips) if f[0] <= t0), default=None)
        if idx is None or (t0 - flips[idx][0]).total_seconds() < settle:
            continue
        if idx + 1 < len(flips) and t1 > flips[idx + 1][0]:
            continue
        arms[flips[idx][1]].append(x)
    out = {}
    for s, xs in sorted(arms.items()):
        out[s] = dict(label=f"{R['label']}:{s}", probe=R["probe"], frames=xs, secs=R["secs"])
    return out


def cmd_round_arms(a, R, flips):
    levels = [int(x) for x in a.levels.split(",")]
    arms = split_arms(R, flips, a.settle)
    names = list(arms)
    P = [f"Round `{R['label']}` split into arms by flips.csv ({len(flips)} flips; intervals fully inside a block and starting "
         f">= {a.settle:g} s after its flip). Arms: " + ", ".join(f"{s} = {len(arms[s]['frames'])} intervals" for s in names), ""]
    bins = {s: bins_of(arms[s]) for s in names}
    allk = sorted(set().union(*[set(b) for b in bins.values()])) if bins else []
    P.append("### Per 50-unit bin: normalised load ms/s (median; per-frame cost x 120 + per-step x 25), fps (median), frame p95 (median); delta vs A")
    P.append("")
    P.append("| units | " + " | ".join(f"n {s}" for s in names) + " | " + " | ".join(f"load {s}" for s in names) + " | " +
             " | ".join(f"{s} vs A %" for s in names if s != "A") + " | " + " | ".join(f"fps {s}" for s in names) + " | " +
             " | ".join(f"p95 {s}" for s in names) + " |")
    P.append("|---:|" + "---:|" * (len(names) * 5 - (1 if "A" in names else 0)))
    for k in allk:
        g = lambda s, c, nd=0: fmt(bins[s][k][c], nd) if k in bins[s] else "-"
        cells = [str(bins[s][k]["n"]) if k in bins[s] else "0" for s in names]
        cells += [g(s, "load_norm_median") for s in names]
        for s in names:
            if s == "A":
                continue
            if k in bins[s] and k in bins.get("A", {}) and bins["A"][k]["load_norm_median"]:
                cells.append(fmt(100 * (bins[s][k]["load_norm_median"] - bins["A"][k]["load_norm_median"]) / bins["A"][k]["load_norm_median"], 1))
            else:
                cells.append("-")
        cells += [g(s, "fps_median", 1) for s in names]
        cells += [g(s, "p95_ms_median", 1) for s in names]
        P.append(f"| {k} | " + " | ".join(cells) + " |")
    P.append("")
    # quadratic fit of main-thread load per arm over every clean interval (placement storms excluded)
    import numpy as np
    P.append("### Fit of the normalised load (per-frame cost x 120 + per-step cost x 25, ms/s; REPORT.md 2.1) = a + b*U + c*U^2 per arm (all clean intervals, storms excluded)")
    P.append("")
    P.append("| arm | n | a | b | c | R2 | at 300 u | at 500 u | at 700 u | vs A at 300 / 500 / 700 |")
    P.append("|---|---:|---:|---:|---:|---:|---:|---:|---:|---|")
    fits = {}
    for s in names:
        xs = [(fnum(x.get("units")), norm_load(x, R["secs"])) for x in arms[s]["frames"] if not x["_storm"]]
        xs = [p for p in xs if not math.isnan(p[0]) and not math.isnan(p[1])]
        if len(xs) < 5:
            continue
        U = np.array([p[0] for p in xs]); Y = np.array([p[1] for p in xs])
        X = np.vstack([np.ones_like(U), U, U * U]).T
        coef, *_ = np.linalg.lstsq(X, Y, rcond=None)
        pred = X @ coef
        r2 = 1 - ((Y - pred) ** 2).sum() / ((Y - Y.mean()) ** 2).sum() if len(Y) > 2 else float("nan")
        fits[s] = (coef, len(xs), r2, U.max())
    for s, (coef, n, r2, umax) in fits.items():
        f_ = lambda u, c=coef: c[0] + c[1] * u + c[2] * u * u
        vs = ""
        if s != "A" and "A" in fits:
            fa = lambda u, c=fits["A"][0]: c[0] + c[1] * u + c[2] * u * u
            vs = " / ".join(fmt(100 * (f_(u) - fa(u)) / fa(u), 1) + " %" for u in (300, 500, 700))
        P.append(f"| {s} | {n} | {coef[0]:.1f} | {coef[1]:.3f} | {coef[2]:.5f} | {r2:.3f} | {f_(300):.0f} | {f_(500):.0f} | {f_(700):.0f} | {vs} |")
    P.append(f"(max units seen: " + ", ".join(f"{s} {fits[s][3]:.0f}" for s in fits) + "; values beyond that are extrapolated)")
    P.append("")
    lv = {s: levels_of(arms[s], levels) for s in names}
    for L in levels:
        have = [s for s in names if L in lv[s]]
        if not have:
            continue
        P.append(f"### ~{L} units (+-15 %, storms excluded): ms per wall-second per arm; " +
                 "; ".join(f"{s}: n={lv[s][L][0]['n']}, units {lv[s][L][0]['units']:.0f}, fps {lv[s][L][0]['fps']:.1f}" for s in have))
        P.append("")
        ref = have[0]
        d0 = lv[ref][L][1]
        keys = [k for k in d0 if k[0] in ("derived", "phase")]
        keys += sorted([k for k in d0 if k[0] == "subsystem"], key=lambda k: -d0[k])[:12]
        keys += sorted([k for k in d0 if k[0] == "section" and "(all)" not in k[1]], key=lambda k: -d0[k])[:25]
        P.append("| kind | name | " + " | ".join(have) + " |")
        P.append("|---|---|" + "---:|" * len(have))
        for k in keys:
            P.append(f"| {k[0]} | {k[1]} | " + " | ".join(f"{lv[s][L][1].get(k, 0.0):.1f}" for s in have) + " |")
        P.append("")
    text = "\n".join(P)
    print(text)
    if a.md:
        open(a.md, "w", encoding="utf-8").write(text + "\n")
    if a.csv:
        with open(a.csv + "_round_arm_bins.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["run", "arm", "units_bin"] + BIN_COLS)
            for s in names:
                for k, dd in bins[s].items():
                    w.writerow([R["label"], s, k] + [dd[c] for c in BIN_COLS])
        with open(a.csv + "_round_arm_levels.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["run", "arm", "level", "n", "units_mean", "kind", "name", "ms_per_s"])
            for s in names:
                for L, (meta, dd) in lv[s].items():
                    for (k, n), v in sorted(dd.items(), key=lambda kv: -kv[1]):
                        if v >= 0.05:
                            w.writerow([R["label"], s, L, meta["n"], round(meta["units"], 1), k, n, round(v, 3)])


def cmd_round(a):
    levels = [int(x) for x in a.levels.split(",")]
    A = round_load(a.a[0])
    flips = read_flips(a.a[0])
    if flips and not a.b and not a.no_split:
        return cmd_round_arms(a, A, flips)
    B = round_load(a.b[0]) if a.b else None
    P = [f"A: `{A['label']}` probe `{A['probe']}`, {len(A['frames'])} game intervals, "
         f"{sum(1 for x in A['frames'] if x['_storm'])} placement-storm intervals (> 50 ms/s)"]
    if B:
        P.append(f"B: `{B['label']}` probe `{B['probe']}`, {len(B['frames'])} game intervals")
    P.append("")
    ba, bb = bins_of(A), (bins_of(B) if B else {})
    P.append("### fps against unit count (round 1, 20 s intervals, medians per 50-unit bin [k*50, k*50+50))")
    P.append("")
    if not B:
        P.append("| units | n | median fps | min fps | frame ms avg / p95 / p99 | main thread busy | main ms/s | structures | storm n |")
        P.append("|---:|---:|---:|---:|---|---:|---:|---:|---:|")
        for k, d in ba.items():
            P.append(f"| {k} | {d['n']} | {fmt(d['fps_median'],1)} | {fmt(d['fps_min'],1)} | {fmt(d['avg_ms_median'],1)} / {fmt(d['p95_ms_median'],0)} / "
                     f"{fmt(d['p99_ms_median'],0)} | {fmt(100*d['main_thread_share_median'],0)} % | {fmt(d['main_ms_s_median'],0)} | {fmt(d['structures_median'],0)} | {d['storm_intervals']} |")
    else:
        P.append("| units | n A/B | fps A | fps B | frame avg A | frame avg B | p95 A | p95 B | main ms/s A | main ms/s B | delta main % |")
        P.append("|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
        for k in sorted(set(ba) | set(bb)):
            x, y = ba.get(k), bb.get(k)
            g = lambda d, c, nd=1: fmt(d[c], nd) if d else "-"
            dm = (100 * (y["main_ms_s_median"] - x["main_ms_s_median"]) / x["main_ms_s_median"]) if x and y and x["main_ms_s_median"] else float("nan")
            P.append(f"| {k} | {x['n'] if x else 0}/{y['n'] if y else 0} | {g(x,'fps_median')} | {g(y,'fps_median')} | {g(x,'avg_ms_median')} | {g(y,'avg_ms_median')} | "
                     f"{g(x,'p95_ms_median',0)} | {g(y,'p95_ms_median',0)} | {g(x,'main_ms_s_median',0)} | {g(y,'main_ms_s_median',0)} | {fmt(dm,1)} |")
    P.append("")
    la, lb = levels_of(A, levels), (levels_of(B, levels) if B else {})
    for L in levels:
        if L not in la:
            P.append(f"(no intervals within 15 % of {L} units)")
            continue
        meta, d = la[L]
        P.append(f"### Main-thread ms per wall-second at ~{L} units (A: n={meta['n']}, units {meta['units']:.0f}, structures {meta['structures']:.0f}, fps {meta['fps']:.1f})")
        P.append("")
        keys = [k for k in d if k[0] in ("derived", "phase")]
        keys += sorted([k for k in d if k[0] == "subsystem"], key=lambda k: -d[k])[:12]
        keys += sorted([k for k in d if k[0] == "section" and "(all)" not in k[1]], key=lambda k: -d[k])[:25]
        keys += [k for k in d if k[0] == "section" and "(all)" in k[1]]
        P.append("| kind | name | A ms/s" + (" | B ms/s | delta |" if B and L in lb else " |"))
        P.append("|---|---|---:|" + ("---:|---:|" if B and L in lb else ""))
        for k in keys:
            line = f"| {k[0]} | {k[1]} | {d[k]:.1f}"
            if B and L in lb:
                v = lb[L][1].get(k, 0.0)
                line += f" | {v:.1f} | {v - d[k]:+.1f} |"
            else:
                line += " |"
            P.append(line)
        P.append("")
    text = "\n".join(P)
    print(text)
    if a.md:
        open(a.md, "w", encoding="utf-8").write(text + "\n")
    if a.csv:
        with open(a.csv + "_round_bins.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["group", "run", "units_bin"] + BIN_COLS)
            for g, R, bb_ in (("A", A, ba), ("B", B, bb)):
                for k, dd in bb_.items():
                    w.writerow([g, R["label"], k] + [dd[c] for c in BIN_COLS])
        with open(a.csv + "_round_levels.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["group", "run", "level", "n", "units_mean", "kind", "name", "ms_per_s"])
            for g, R, ll in (("A", A, la), ("B", B, lb)):
                for L, (meta, dd) in ll.items():
                    for (k, n), v in sorted(dd.items(), key=lambda kv: -kv[1]):
                        if v >= 0.05:
                            w.writerow([g, R["label"], L, meta["n"], round(meta["units"], 1), k, n, round(v, 3)])
        with open(a.csv + "_round_intervals.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            cols = ["timestamp", "t_round_s", "interval_s", "units", "structures", "fps", "avg_ms", "p95_ms", "p99_ms", "max_ms",
                    "main_thread_share", "main_thread_cpu_ms", "cpu_cores_busy", "projectiles", "rigidbodies"]
            w.writerow(["group", "run"] + cols + ["storm"])
            for g, R in (("A", A), ("B", B)):
                if R:
                    for x in R["frames"]:
                        w.writerow([g, R["label"]] + [x.get(c, "") for c in cols] + [int(x["_storm"])])


# ------------------------------------------------------------------ flip
def read_flips(run):
    fl = [(parse_t(r["timestamp_iso"]), r["state"]) for r in read_csv(os.path.join(run, "flips.csv"))]
    return sorted(f for f in fl if f[0])


def cmd_flip(a):
    """Blocks = time between two flips. Per scenario and state: mean frame work over the blocks (Si_PerfLab 1 Hz
    samples in Measure phases, first --settle s after each flip dropped). Paired estimate for every state X != A:
    each X block minus the mean of its neighbouring A blocks (same scenario), mean and std error over X blocks."""
    run = a.run
    lab = one_dir(run, "perflab", "samples.csv")
    smp = read_csv(os.path.join(lab, "samples.csv"))
    flips = read_flips(run)
    blocks = defaultdict(lambda: [0.0, 0.0, 0])
    bstate = {}
    for s in smp:
        if s.get("phase") != "Measure":
            continue
        t = parse_t(s.get("timestamp_iso"))
        if not t:
            continue
        t0 = t - timedelta(seconds=1)
        idx = max((i for i, f in enumerate(flips) if f[0] <= t0), default=None)
        if idx is None or (t0 - flips[idx][0]).total_seconds() < a.settle:
            continue
        if idx + 1 < len(flips) and t > flips[idx + 1][0]:
            continue
        fr, w = fnum(s.get("frames"), 0), fnum(s.get("work_avg_ms"))
        if fr <= 0 or math.isnan(w):
            continue
        key = (s["scenario"], idx)
        blocks[key][0] += w * fr
        blocks[key][1] += fr
        blocks[key][2] += 1
        bstate[key] = flips[idx][1]
    per = defaultdict(dict)
    for (sc, idx), (wf, fr, n) in blocks.items():
        if n >= 3:
            per[sc][idx] = (bstate[(sc, idx)], wf / fr)
    states = sorted({s for _, s in flips})
    others = [s for s in states if s != "A"]
    P = [f"flip run `{label_of(run)}`, {len(flips)} flips, states {states}, first {a.settle:g} s after each flip dropped, "
         f"blocks with < 3 samples dropped", "",
         "| scenario | A ms (n) | " + " | ".join(f"{x} ms (n) | {x}-A paired | s.e. | %" for x in others) + " |",
         "|---|---:|" + "---:|---:|---:|---:|" * len(others)]
    rows = []
    for sc, bl in per.items():
        wa = [w for s, w in bl.values() if s == "A"]
        ma = st.mean(wa) if wa else float("nan")
        line = f"| {sc} | {fmt(ma, 3)} ({len(wa)}) |"
        for x in others:
            wx = [w for s, w in bl.values() if s == x]
            diffs = []
            for idx, (s, w) in bl.items():
                if s != x:
                    continue
                nb = [bl[j][1] for j in (idx - 1, idx + 1) if j in bl and bl[j][0] == "A"]
                if nb:
                    diffs.append(w - st.mean(nb))
            md = st.mean(diffs) if diffs else float("nan")
            se = st.stdev(diffs) / math.sqrt(len(diffs)) if len(diffs) > 1 else float("nan")
            line += f" {fmt(st.mean(wx) if wx else float('nan'), 3)} ({len(wx)}) | {fmt(md, 3)} | {fmt(se, 3)} | {fmt(100 * md / ma if ma else float('nan'), 1)} |"
            rows.append([sc, x, len(wa), ma, len(wx), st.mean(wx) if wx else "", len(diffs), md, se])
        P.append(line)
    text = "\n".join(P)
    print(text)
    if a.md:
        open(a.md, "w", encoding="utf-8").write(text + "\n")
    if a.csv:
        with open(a.csv + "_flip.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["run", "scenario", "state", "n_A_blocks", "work_A_ms", "n_X_blocks", "work_X_ms", "n_pairs", "paired_diff_ms", "paired_se_ms"])
            for r in rows:
                w.writerow([label_of(run)] + r)

# ------------------------------------------------------------------ windows (Si_ServerPerf status dumps)
import re
WIN_RE = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d+)\] \[Si_ServerPerf\] ==== Si_ServerPerf \S+ window (\d+) ENDED")
ITEM_RE = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d+)\] \[Si_ServerPerf\]\s+(\w+\.\w+)\s{2,}(.+?)\s{2,}(\d+/\d+)\s+(\S+)(?:\s+\|\s?(.*))?$")
NUMS = {
    "Targeting.CollectEnemies_Grid": [("q_s", r"q/s ([\d.]+)"), ("grid", r"grid ([\d.]+)"), ("vanilla", r"vanilla ([\d.]+)"),
                                      ("visited_q", r"visited/q ([\d.]+)"), ("D", r"of D ([\d.]+)")],
    "Targeting.DetectedGrid": [("builds_s", r"builds/s ([\d.]+)"), ("entries_build", r"entries/build ([\d.]+)"), ("desync", r"desync total (\d+)")],
    "Targeting.FriendlyFire_Grid": [("q_s", r"q/s ([\d.]+)"), ("grid", r"grid ([\d.]+)")],
    "Targeting.UrgentClose_Grid": [("q_s", r"q/s ([\d.]+)")],
    "Targeting.FriendlyFire_StaleGuard": [("grid", r"grid ([\d.]+)"), ("vanilla", r"vanilla ([\d.]+)")],
    "Targeting.LosTopK": [("q_s", r"q/s ([\d.]+)"), ("los_saved_s", r"saved/s ([\d.]+)")],
    "Targeting.FriendlyFire_Cache": [("q_s", r"q/s ([\d.]+)"), ("hits_s", r"hits/s ([\d.]+)")],
    "UpdateTiers.Tier_IdleAgents": [("run_s", r"run ([\d.]+)"), ("early_s", r"\+ early ([\d.]+)"), ("skipped_pct", r"skipped \d+ \((\d+)%\)")],
    "MultiCore.SensorPipeline_Rate": [("frames_s", r"frames/s ([\d.]+)"), ("runs_s", r"pipeline runs/s ([\d.]+)"), ("part1_ms_s", r"part1 ([\d.]+) ms/s")],
    "Physics.Census": [("asleep_pct", r"dyn asleep (\d+)%")], "Targeting.NearestTarget_Grid": [("q_s", r"q/s ([\d.]+)")],
    "Targeting.AlarmProximity_Grid": [("q_s", r"q/s ([\d.]+)")], "Targeting.Rank_Sort": [("q_s", r"q/s ([\d.]+)")],
    "Services.CommanderAI_ThinkStats": [("round_thinks", r"round: thinks (\d+)"), ("avg_ms", r"avg ([\d.]+) ms"), ("max_ms", r"max ([\d.]+) ms")],
    "UpdateTiers.Tier_IdleSoldiers": [("run_s", r"run ([\d.]+)"), ("early_s", r"\+ early ([\d.]+)"), ("skipped_pct", r"skipped \d+ \((\d+)%\)")],
    "UpdateTiers.Tier_OrderProcessors": [("skipped_pct", r"skipped \d+ \((\d+)%\)")],
    "UpdateTiers.Tier_Structures": [("skipped_pct", r"skipped \d+ \((\d+)%\)")],
}


def parse_windows(run):
    log = os.path.join(run, "Latest.log")
    day = None
    m = os.path.join(run, "manifest.json")
    if os.path.exists(m):
        with open(m, encoding="utf-8-sig") as f:
            day = json.load(f).get("start", "")[:10]
    flips = read_flips(run)
    wins, cur = [], None
    with open(log, encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.rstrip("\n")
            mw = WIN_RE.match(line)
            if mw:
                cur = dict(time=parse_t(f"{day}T{mw.group(1)}"), window=int(mw.group(2)), items={})
                wins.append(cur)
                continue
            if cur is not None and "==== end Si_ServerPerf window" in line:
                cur = None
                continue
            if cur is not None:
                mi = ITEM_RE.match(line)
                if mi:
                    cur["items"][mi.group(2)] = (mi.group(3).strip(), mi.group(6) or "")
    for w in wins:
        st_ = None
        if w["time"]:
            prev = [i for i, f in enumerate(flips) if f[0] <= w["time"]]
            if prev:
                k = prev[-1]
                # a dump within 3 s after a flip was triggered by it: the window that ended ran under the previous state
                if (w["time"] - flips[k][0]).total_seconds() <= 3.0:
                    st_ = flips[k - 1][1] if k >= 1 else None
                else:
                    st_ = flips[k][1]
        w["arm"] = st_
    return wins


def cmd_windows(a):
    wins = parse_windows(a.run)
    P = [f"`{label_of(a.run)}`: {len(wins)} status windows parsed", ""]
    failed = sorted({(k, v[0]) for w in wins for k, v in w["items"].items() if "FAILED" in v[0] or "NOT APPLIED" in v[0]})
    P.append("Items FAILED / NOT APPLIED in any window: " + (", ".join(f"{k} ({s})" for k, s in failed) if failed else "none"))
    P.append("")
    rows = []
    for w in wins:
        for k, (stt, info) in w["items"].items():
            vals = {}
            for name, rx in NUMS.get(k, []):
                mm = re.search(rx, info)
                if mm:
                    vals[name] = float(mm.group(1))
            rows.append((w, k, stt, info, vals))
    by = defaultdict(lambda: defaultdict(list))
    for w, k, stt, info, vals in rows:
        for n, v in vals.items():
            if stt.startswith("ON"):
                by[(w["arm"], k)][n].append(v)
    P.append("| arm | item | windows | metric medians (windows where the item was ON) |")
    P.append("|---|---|---:|---|")
    for (arm, k), d in sorted(by.items(), key=lambda kv: (str(kv[0][0]), kv[0][1])):
        nwin = max(len(v) for v in d.values())
        P.append(f"| {arm} | {k} | {nwin} | " + ", ".join(f"{n} {st.median(v):.2f}" for n, v in d.items()) + " |")
    # exceptions per arm: delta of the Diag running count between consecutive windows, plus the first-N detail lines
    P.append("")
    P.append("### Exceptions / warnings counted by Headless.Diag_LogExceptions, per arm (delta of the running count per window; windows where counting was off are listed separately)")
    P.append("")
    per_arm = defaultdict(lambda: [0, 0, 0])   # arm -> [windows counted, exceptions, windows not counting]
    prev = None
    for w in wins:
        v = w["items"].get("Headless.Diag_LogExceptions")
        if not v:
            continue
        mm = re.search(r"this period \(([^)]*)\): (\d+) logged", v[1])
        if not mm:
            continue
        n = int(mm.group(2)); period = mm.group(1)
        if "counting OFF" in v[1] or not v[0].startswith("ON"):
            per_arm[w["arm"]][2] += 1
        else:
            d = n - prev[1] if prev and prev[0] == period else n
            per_arm[w["arm"]][0] += 1
            per_arm[w["arm"]][1] += max(d, 0)
        prev = (period, n)
    P.append("| arm | windows counting | exceptions+warnings | per window | windows not counting |")
    P.append("|---|---:|---:|---:|---:|")
    for arm, (nw, ne, nc) in sorted(per_arm.items(), key=lambda kv: str(kv[0])):
        P.append(f"| {arm} | {nw} | {ne} | {ne / nw if nw else float('nan'):.2f} | {nc} |")
    P.append("")
    flips = read_flips(a.run)
    day = (wins[0]["time"].date().isoformat() if wins and wins[0]["time"] else None)
    det = []
    with open(os.path.join(a.run, "Latest.log"), encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    for i, line in enumerate(lines):
        mm = re.match(r"^\[(\d\d:\d\d:\d\d\.\d+)\].*\[Diag\] (Exception|Warning) #(\d+)[^:]*: (.*)$", line.rstrip())
        if mm and day:
            tt = parse_t(f"{day}T{mm.group(1)}")
            k = [j for j, fl in enumerate(flips) if fl[0] <= tt]
            arm = flips[k[-1]][1] if k else None
            since = (tt - flips[k[-1]][0]).total_seconds() if k else float("nan")
            where = lines[i + 2].strip() if i + 2 < len(lines) and lines[i + 2].strip().startswith("at ") else ""
            det.append((mm.group(1), arm, since, mm.group(4)[:90], where[:110]))
    if det:
        P.append("Detail lines printed by Diag (first few per round only): time, arm active, s since that arm's flip, message, first game frame")
        P.append("")
        for d in det:
            P.append(f"- {d[0]} arm {d[1]} (+{d[2]:.0f} s): {d[3]} | {d[4]}")
        P.append("")
    text = "\n".join(P)
    print(text)
    if a.md:
        open(a.md, "w", encoding="utf-8").write(text + "\n")
    if a.csv:
        with open(a.csv + "_windows.csv", "w", newline="", encoding="utf-8") as f:
            wr = csv.writer(f)
            wr.writerow(["run", "window", "end_time", "arm", "item", "state", "info"])
            for w, k, stt, info, vals in rows:
                wr.writerow([label_of(a.run), w["window"], w["time"].isoformat() if w["time"] else "", w["arm"], k, stt, info])

def main():
    ap = argparse.ArgumentParser()
    sp = ap.add_subparsers(dest="cmd", required=True)
    for name in ("lab", "round"):
        p = sp.add_parser(name)
        p.add_argument("--a", nargs="+", required=True)
        p.add_argument("--b", nargs="+")
        p.add_argument("--md")
        p.add_argument("--csv")
        if name == "round":
            p.add_argument("--levels", default="300,500")
            p.add_argument("--settle", type=float, default=2.0, help="arms: drop intervals starting < settle s after a flip")
            p.add_argument("--no-split", action="store_true", help="ignore flips.csv")
    p = sp.add_parser("flip")
    p.add_argument("--run", required=True)
    p.add_argument("--settle", type=float, default=5.0)
    p.add_argument("--md")
    p.add_argument("--csv")
    p = sp.add_parser("windows")
    p.add_argument("--run", required=True)
    p.add_argument("--md")
    p.add_argument("--csv")
    a = ap.parse_args()
    {"lab": cmd_lab, "round": cmd_round, "flip": cmd_flip, "windows": cmd_windows}[a.cmd](a)


if __name__ == "__main__":
    main()
