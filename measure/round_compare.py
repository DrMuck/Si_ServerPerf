#!/usr/bin/env python3
"""round_compare.py LABEL=RTSA_round_log [LABEL=log ...] - gameplay comparison of headless AI rounds from
Si_RTS_AI's observability log (UserData/RTSA/round-*-GreatErg.log): per team cash/units/structures at 5-minute
marks ([CASH] samples), construct/queue requests by result ([CMD]), structures and units spawned ([SPAWN_S]/[SPAWN_U]),
and round length (last [CASH] t). Prints Markdown."""
import re
import sys
from collections import Counter, defaultdict

TEAMS = ["Team_Human_Sol", "Team_Human_Centauri", "Team_Alien"]
SHORT = {"Team_Human_Sol": "Sol", "Team_Human_Centauri": "Cent", "Team_Alien": "Alien"}


def load(path):
    cash = defaultdict(list)
    cmd = Counter()
    kinds = Counter()
    spawn_s, spawn_u = Counter(), Counter()
    tmax = 0
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            if line.startswith("[CASH]"):
                m = re.match(r"\[CASH\] t=(\d+) team=(\S+) .*cash=(-?\d+) units=(\d+) structures=(\d+)", line)
                if m:
                    t = int(m.group(1))
                    if t == 0 and tmax > 0:
                        continue  # end-of-round summary repeats with t=0
                    tmax = max(tmax, t)
                    cash[m.group(2)].append((t, int(m.group(3)), int(m.group(4)), int(m.group(5))))
            elif line.startswith("[CMD]"):
                m = re.search(r"team=(\S+).*what=(.+?) kind=(\S+).*result=(\S+)", line)
                if m:
                    cmd[(m.group(1), m.group(4))] += 1
                    kinds[(m.group(1), m.group(3), m.group(4) == "Success")] += 1
            elif line.startswith("[SPAWN_S]"):
                m = re.search(r"team=(\S+)", line)
                if m:
                    spawn_s[m.group(1)] += 1
            elif line.startswith("[SPAWN_U]"):
                m = re.search(r"team=(\S+)", line)
                if m:
                    spawn_u[m.group(1)] += 1
    return dict(cash=cash, cmd=cmd, kinds=kinds, spawn_s=spawn_s, spawn_u=spawn_u, tmax=tmax)


def at(series, t):
    best = None
    for s in series:
        if s[0] <= t:
            best = s
    return best


runs = []
for arg in sys.argv[1:]:
    label, path = arg.split("=", 1)
    runs.append((label, load(path)))

print("## Round length\n")
for label, r in runs:
    last = {SHORT[t]: (r["cash"][t][-1][2], r["cash"][t][-1][3]) for t in TEAMS if r["cash"].get(t)}
    print(f"- **{label}**: last [CASH] sample at t = {r['tmax']} s ({r['tmax'] / 60:.1f} min); final units/structures "
          + ", ".join(f"{k} {v[0]}/{v[1]}" for k, v in last.items()))
print()
print("## Units / structures / cash per team at 5-minute marks\n")
marks = list(range(0, max(r["tmax"] for _, r in runs) + 1, 300))
print("| min | " + " | ".join(f"{label} {SHORT[t]}" for label, _ in runs for t in TEAMS) + " |")
print("|---:|" + "---|" * (len(runs) * len(TEAMS)))
for mk in marks:
    cells = []
    for label, r in runs:
        for t in TEAMS:
            s = at(r["cash"].get(t, []), mk) if mk <= r["tmax"] else None
            cells.append(f"{s[2]}u {s[3]}s ${s[1]}" if s else "-")
    print(f"| {mk // 60} | " + " | ".join(cells) + " |")
print()
print("## Construct / queue requests ([CMD]) by result\n")
print("| run | team | Success | other results |")
print("|---|---|---:|---|")
for label, r in runs:
    for t in TEAMS:
        ok = r["cmd"].get((t, "Success"), 0)
        other = ", ".join(f"{res} {n}" for (tt, res), n in sorted(r["cmd"].items()) if tt == t and res != "Success")
        print(f"| {label} | {SHORT[t]} | {ok} | {other or '-'} |")
print()
print("## Requests by kind (successful / refused) and spawns\n")
print("| run | team | structure ok / refused | unit ok / refused | other kinds | SPAWN_S | SPAWN_U |")
print("|---|---|---|---|---|---:|---:|")
for label, r in runs:
    for t in TEAMS:
        k = r["kinds"]
        oth = sorted({kk for (tt, kk, _) in k if tt == t and kk not in ("structure", "unit")})
        print(f"| {label} | {SHORT[t]} | {k.get((t, 'structure', True), 0)} / {k.get((t, 'structure', False), 0)} | "
              f"{k.get((t, 'unit', True), 0)} / {k.get((t, 'unit', False), 0)} | "
              + (", ".join(f"{kk} {k.get((t, kk, True), 0)}/{k.get((t, kk, False), 0)}" for kk in oth) or "-")
              + f" | {r['spawn_s'].get(t, 0)} | {r['spawn_u'].get(t, 0)} |")
