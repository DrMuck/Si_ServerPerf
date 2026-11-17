#!/usr/bin/env python3
"""ramp_report.py RUN [--settle 5] [--units lab|world] [--bin 50] [--md out.md] [--csv prefix]

Report for Si_PerfLab 0.2.0 runs (SCENARIOS_V2.md) made with run_lab.ps1, flips or not:

  1. Plateaus (ramp, climb=steps): one scenarios.csv row per plateau ("<name>@<target>") plus, per plateau and arm,
     the 1 Hz samples in Measure (first --settle s after each flip dropped): frame work (frame-weighted mean), fps
     median, lab units, and the paired difference of each X block to its neighbouring A blocks.
  2. Continuous (ramp, climb=trickle): per arm a least-squares fit  work_ms = a + b*U + c*U^2  over the 1 Hz Measure
     samples (U = lab units or world units), predictions at 300/500/800/1000 units and their difference to arm A,
     plus 50-unit bins per arm.  Also fits frame time (1000/fps) the same way.
  3. Engagement (engagement.csv) per window, team and kind: share with a target, shots per unit-second, kills.
  4. Order probe (order_response_summary.csv) per cycle and kind, with the arm active at the order.
Arms come from flips.csv (state labels); without flips.csv they come from the samples' runtime_sig column, mapped
to the cfg_*.json copies in the run folder by CRC32 (the same checksum Si_PerfLab writes). A FAILED file in the
perflab run folder is reported first."""
import argparse
import glob
import math
import os
import statistics as st
import sys
import zlib
from collections import defaultdict
from datetime import timedelta

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ab_report as R  # noqa: E402


def fit_quadratic(xs, ys, ws=None):
    """Weighted least squares y = a + b x + c x^2 (normal equations, x scaled by 1000). None if under-determined."""
    n = len(xs)
    if n < 6 or len(set(round(x) for x in xs)) < 3:
        return None
    ws = ws or [1.0] * n
    S = [[0.0] * 3 for _ in range(3)]
    T = [0.0] * 3
    for x, y, w in zip(xs, ys, ws):
        u = x / 1000.0
        v = [1.0, u, u * u]
        for i in range(3):
            T[i] += w * v[i] * y
            for j in range(3):
                S[i][j] += w * v[i] * v[j]
    # Gaussian elimination with partial pivoting
    M = [S[i][:] + [T[i]] for i in range(3)]
    for c in range(3):
        p = max(range(c, 3), key=lambda r: abs(M[r][c]))
        if abs(M[p][c]) < 1e-12:
            return None
        M[c], M[p] = M[p], M[c]
        for r in range(3):
            if r != c:
                f = M[r][c] / M[c][c]
                for k in range(c, 4):
                    M[r][k] -= f * M[c][k]
    a, b, c = (M[i][3] / M[i][i] for i in range(3))
    b, c = b / 1000.0, c / 1e6
    res = [y - (a + b * x + c * x * x) for x, y in zip(xs, ys)]
    rmse = math.sqrt(sum(r * r for r in res) / n)
    return dict(a=a, b=b, c=c, rmse=rmse, n=n, umin=min(xs), umax=max(xs))


def pred(f, u):
    return float("nan") if not f else f["a"] + f["b"] * u + f["c"] * u * u


def arm_mapper(run, flips, settle):
    """Returns arm_of(sample) -> label or None (sample dropped)."""
    if flips:
        def arm_of(s):
            t = R.parse_t(s.get("timestamp_iso"))
            if not t:
                return None
            t0 = t - timedelta(seconds=1)
            idx = max((i for i, f in enumerate(flips) if f[0] <= t0), default=None)
            if idx is None or (t0 - flips[idx][0]).total_seconds() < settle:
                return None
            if idx + 1 < len(flips) and t > flips[idx + 1][0]:
                return None
            return flips[idx][1], idx
        return arm_of
    sigmap = {}
    for p in glob.glob(os.path.join(run, "cfg_*.json")):
        with open(p, "rb") as f:
            sigmap["%08x" % (zlib.crc32(f.read()) & 0xFFFFFFFF)] = os.path.basename(p)[4:-5]

    def arm_of2(s):
        sig = s.get("runtime_sig", "")
        return (sigmap.get(sig, sig or "all"), sig)
    return arm_of2


def wmean(vals):
    num = sum(v * w for v, w in vals)
    den = sum(w for _, w in vals)
    return num / den if den else float("nan")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("run")
    ap.add_argument("--settle", type=float, default=5.0, help="seconds dropped after each flip (>= Si_ServerPerf poll + 1)")
    ap.add_argument("--units", choices=["lab", "world"], default="lab")
    ap.add_argument("--bin", type=int, default=50)
    ap.add_argument("--md")
    ap.add_argument("--csv")
    a = ap.parse_args()
    run = a.run
    P = []
    lab = R.one_dir(run, "perflab", "samples.csv") or R.one_dir(run, "perflab", "FAILED")
    if not lab:
        cand = [d for d in glob.glob(os.path.join(run, "perflab", "*")) if os.path.isdir(d)]
        lab = cand[0] if cand else None
    if not lab:
        print("no perflab run folder under", run)
        return 2
    failed = os.path.join(lab, "FAILED")
    if os.path.exists(failed):
        with open(failed, encoding="utf-8-sig") as f:
            P.append("**FAILED:** " + f.read().strip())
            P.append("")
    rows = R.read_csv(os.path.join(lab, "scenarios.csv"))
    smp = R.read_csv(os.path.join(lab, "samples.csv"))
    flips = R.read_flips(run)
    arm_of = arm_mapper(run, flips, a.settle)
    ucol = "lab_units" if a.units == "lab" else "units"
    csv_plateau, csv_fit, csv_bins = [], [], []

    # ---------------------------------------------------------------- 1. plateaus
    prow = [r for r in rows if r.get("scenario_type") == "ramp" and r.get("plateau", "") not in ("", "-1")]
    if prow:
        P.append("## Plateaus (ramp steps)")
        P.append("")
        P.append("| plateau | status | target units | lab units avg (min-max) | count dev max % | structures | fps | work avg / p95 ms | deaths/s | respawns/s | shots | hits | kills | with target avg | stalls |")
        P.append("|---|---|---:|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|")
        for r in prow:
            P.append(f"| {r['scenario']} | {r['status']} | {r.get('target_units')} | {R.fmt(R.fnum(r.get('lab_units_avg')), 0)} ({r.get('lab_units_min')}-{r.get('lab_units_max')}) | "
                     f"{R.fmt(R.fnum(r.get('count_dev_max_pct')), 1)} | {r.get('lab_structures_alive')} | {R.fmt(R.fnum(r.get('fps')), 1)} | "
                     f"{R.fmt(R.fnum(r.get('work_avg_ms')))} / {R.fmt(R.fnum(r.get('work_p95_ms')))} | {R.fmt(R.fnum(r.get('deaths_per_s')))} | "
                     f"{R.fmt(R.fnum(r.get('respawns_per_s')))} | {r.get('shots')} | {r.get('hits')} | {r.get('kills')} | {R.fmt(R.fnum(r.get('units_with_target_avg')), 0)} | {r.get('front_stalls')} |")
        P.append("")
        P.append("Per plateau and arm (1 Hz Measure samples; paired = each X block minus the mean of its neighbouring A blocks):")
        P.append("")
        P.append("| plateau | arm | s | lab units | fps median | work ms | X-A paired ms | s.e. | n pairs |")
        P.append("|---|---|---:|---:|---:|---:|---:|---:|---:|")
        for r in prow:
            sc = r["scenario"]
            per = defaultdict(list)
            blocks = defaultdict(list)
            for s in smp:
                if s.get("scenario") != sc or s.get("phase") != "Measure":
                    continue
                arm = arm_of(s)
                if not arm:
                    continue
                fr, w = R.fnum(s.get("frames"), 0), R.fnum(s.get("work_avg_ms"))
                if fr <= 0 or math.isnan(w):
                    continue
                per[arm[0]].append((fr, w, R.fnum(s.get(ucol))))
                blocks[arm[1]].append((w, fr, arm[0]))
            bl = sorted((k, wmean([(w, f) for w, f, _ in v]), v[0][2]) for k, v in blocks.items())
            for arm in sorted(per):
                d = per[arm]
                diffs = []
                if arm != "A":
                    for i, (k, m, lab_) in enumerate(bl):
                        if lab_ != arm:
                            continue
                        nb = [bl[j][1] for j in (i - 1, i + 1) if 0 <= j < len(bl) and bl[j][2] == "A"]
                        if not nb:
                            nb = [x[1] for x in bl if x[2] == "A" and abs(x[0] - k) <= 3]
                        if nb:
                            diffs.append(m - sum(nb) / len(nb))
                pd = st.mean(diffs) if diffs else float("nan")
                se = st.stdev(diffs) / math.sqrt(len(diffs)) if len(diffs) > 1 else float("nan")
                work = wmean([(w, f) for f, w, _ in d])
                P.append(f"| {sc} | {arm} | {len(d)} | {R.fmt(st.median([u for *_, u in d]), 0)} | {R.fmt(st.median([f for f, _, _ in d]), 1)} | {R.fmt(work)} | "
                         f"{R.fmt(pd)} | {R.fmt(se)} | {len(diffs)} |")
                csv_plateau.append([sc, arm, len(d), st.median([u for *_, u in d]), st.median([f for f, _, _ in d]), work, pd, se, len(diffs)])
        P.append("")

    # ---------------------------------------------------------------- 2. continuous (trickle)
    trow = [r for r in rows if r.get("scenario_type") == "ramp" and r.get("plateau", "") in ("", "-1") and r.get("status") in ("ok", "stopped")]
    for r in trow:
        sc = r["scenario"]
        P.append(f"## Continuous climb: {sc} (status {r['status']}, notes: {r.get('notes', '')})")
        P.append("")
        per = defaultdict(lambda: ([], [], [], []))     # U, work, frames, dt_ms
        for s in smp:
            if s.get("scenario") != sc or s.get("phase") != "Measure":
                continue
            arm = arm_of(s)
            if not arm:
                continue
            fr, w, u = R.fnum(s.get("frames"), 0), R.fnum(s.get("work_avg_ms")), R.fnum(s.get(ucol))
            if fr <= 0 or math.isnan(w) or math.isnan(u):
                continue
            d = per[arm[0]]
            d[0].append(u); d[1].append(w); d[2].append(fr); d[3].append(1000.0 / fr)
        fits = {arm: (fit_quadratic(d[0], d[1], d[2]), fit_quadratic(d[0], d[3])) for arm, d in per.items()}
        P.append(f"Fit per arm, U = {a.units} units:  work_ms = a + b*U + c*U^2 (frame-weighted); frame_ms = 1000/fps fitted the same way.")
        P.append("")
        P.append("| arm | s | U range | a | b (ms/unit) | c (ms/unit^2) | rmse | work@300 | work@500 | work@800 | work@1000 | frame_ms@800 |")
        P.append("|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
        for arm in sorted(fits):
            f, g = fits[arm]
            if not f:
                P.append(f"| {arm} | {len(per[arm][0])} | too few samples | | | | | | | | | |")
                continue
            P.append(f"| {arm} | {f['n']} | {f['umin']:.0f}-{f['umax']:.0f} | {f['a']:.3f} | {f['b']:.5f} | {f['c']:.2e} | {f['rmse']:.2f} | "
                     f"{R.fmt(pred(f, 300))} | {R.fmt(pred(f, 500))} | {R.fmt(pred(f, 800))} | {R.fmt(pred(f, 1000))} | {R.fmt(pred(g, 800))} |")
            csv_fit.append([sc, arm, f["n"], f["umin"], f["umax"], f["a"], f["b"], f["c"], f["rmse"], pred(f, 300), pred(f, 500), pred(f, 800), pred(f, 1000), pred(g, 800)])
        if "A" in fits and fits["A"][0]:
            P.append("")
            P.append("Difference to arm A from the fits (ms of frame work; negative = arm is cheaper), only inside both arms' U range:")
            P.append("")
            P.append("| arm | @300 | @500 | @800 | @1000 |")
            P.append("|---|---:|---:|---:|---:|")
            fa = fits["A"][0]
            for arm in sorted(fits):
                f = fits[arm][0]
                if arm == "A" or not f:
                    continue
                cells = []
                for u in (300, 500, 800, 1000):
                    inside = max(f["umin"], fa["umin"]) <= u <= min(f["umax"], fa["umax"])
                    cells.append(R.fmt(pred(f, u) - pred(fa, u)) if inside else "-")
                P.append(f"| {arm} | " + " | ".join(cells) + " |")
        # bins
        P.append("")
        P.append(f"{a.bin}-unit bins (frame-weighted work ms, n seconds):")
        P.append("")
        arms = sorted(per)
        P.append("| U bin | " + " | ".join(arms) + " |")
        P.append("|---|" + "---:|" * len(arms))
        bins = sorted({int(u // a.bin) for d in per.values() for u in d[0]})
        for b in bins:
            cells = []
            for arm in arms:
                d = per[arm]
                vals = [(w, f) for u, w, f in zip(d[0], d[1], d[2]) if int(u // a.bin) == b]
                cells.append(f"{R.fmt(wmean(vals))} ({len(vals)})" if vals else "-")
                if vals:
                    csv_bins.append([sc, arm, b * a.bin, wmean(vals), len(vals)])
            P.append(f"| {b * a.bin}-{(b + 1) * a.bin - 1} | " + " | ".join(cells) + " |")
        P.append("")

    # ---------------------------------------------------------------- 3. engagement
    eng = R.read_csv(os.path.join(lab, "engagement.csv"))
    if eng:
        P.append("## Engagement per window (team, kind)")
        P.append("")
        P.append("| window | team | kind | units avg | with target share | shots / unit-s | creature attacks | hits dealt | kills | deaths |")
        P.append("|---|---|---|---:|---:|---:|---:|---:|---:|---:|")
        for e in eng:
            if e.get("team") == "none":
                continue
            P.append(f"| {e['scenario']} | {e['team']} | {e['kind']} | {R.fmt(R.fnum(e.get('units_avg')), 0)} | {R.fmt(R.fnum(e.get('with_target_share')))} | "
                     f"{R.fmt(R.fnum(e.get('shots_per_unit_s')), 3)} | {e.get('creature_attacks')} | {e.get('hits_dealt')} | {e.get('kills')} | {e.get('deaths')} |")
        P.append("")
        idle_kinds = [e for e in eng if e.get("kind") not in ("nonlab", "none", "structure", "turret") and R.fnum(e.get("units_avg"), 0) >= 10
                      and R.fnum(e.get("hits_dealt"), 0) == 0 and R.fnum(e.get("shots"), 0) == 0 and R.fnum(e.get("creature_attacks"), 0) == 0]
        if idle_kinds:
            P.append("**Not fighting** (>= 10 units, no shots, attacks or hits in the window): " +
                     ", ".join(f"{e['scenario']} {e['team']} {e['kind']}" for e in idle_kinds))
            P.append("")

    # ---------------------------------------------------------------- 4. order probe
    pr = R.read_csv(os.path.join(lab, "order_response_summary.csv"))
    if pr:
        P.append("## Order response (probe)")
        P.append("")
        P.append("| scenario | cycle | arm at order | team | kind | n | order failed | never started | never 2 m | first move p50 / p95 / max s | 2 m p50 / p95 / max s |")
        P.append("|---|---:|---|---|---|---:|---:|---:|---:|---|---|")
        for p in pr:
            if p.get("group_by") != "kind":
                continue
            fake = {"timestamp_iso": p.get("order_iso"), "runtime_sig": p.get("runtime_sig", "")}
            if flips:
                t = R.parse_t(p.get("order_iso"))
                idx = max((i for i, f in enumerate(flips) if t and f[0] <= t), default=None)
                arm = flips[idx][1] if idx is not None else "-"
            else:
                arm = (arm_of(fake) or ("-",))[0]
            P.append(f"| {p['scenario']} | {p['cycle']} | {arm} | {p['team']} | {p['key']} | {p['n']} | {p['order_failed']} | {p['never_started']} | {p['never_reached']} | "
                     f"{R.fmt(R.fnum(p.get('first_move_p50_s')))} / {R.fmt(R.fnum(p.get('first_move_p95_s')))} / {R.fmt(R.fnum(p.get('first_move_max_s')))} | "
                     f"{R.fmt(R.fnum(p.get('reach_p50_s')))} / {R.fmt(R.fnum(p.get('reach_p95_s')))} / {R.fmt(R.fnum(p.get('reach_max_s')))} |")
        P.append("")

    if not P:
        P.append("nothing to report (no ramp rows, engagement or probe files in " + lab + ")")
    text = "\n".join(P)
    print(text)
    if a.md:
        with open(a.md, "w", encoding="utf-8") as f:
            f.write(text + "\n")
    if a.csv:
        import csv
        for name, hdr, data in (("plateau_arms", ["scenario", "arm", "samples", "lab_units_median", "fps_median", "work_ms", "paired_diff_ms", "paired_se_ms", "n_pairs"], csv_plateau),
                                ("trickle_fit", ["scenario", "arm", "n", "u_min", "u_max", "a", "b", "c", "rmse", "work_300", "work_500", "work_800", "work_1000", "frame_ms_800"], csv_fit),
                                ("trickle_bins", ["scenario", "arm", "u_bin_start", "work_ms", "n"], csv_bins)):
            with open(f"{a.csv}_{name}.csv", "w", newline="", encoding="utf-8") as f:
                w = csv.writer(f)
                w.writerow(hdr)
                w.writerows(data)
    return 0


if __name__ == "__main__":
    sys.exit(main())
