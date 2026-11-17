import sys, os, re
sys.path.insert(0,".")
import ab_report as R
from collections import defaultdict
from datetime import timedelta
run=sys.argv[1]; items=sys.argv[2].split(",")
fl=R.read_flips(run)
lab=R.one_dir(run,"perflab","scenarios.csv"); wins=[(r["scenario"],R.parse_t(r["measure_start_iso"]),R.parse_t(r["measure_end_iso"])) for r in R.read_csv(os.path.join(lab,"scenarios.csv"))]
day=wins[0][1].date().isoformat()
out=defaultdict(list)
for line in open(os.path.join(run,"Latest.log"),encoding="utf-8",errors="replace"):
    m=re.match(r"^\[(\d\d:\d\d:\d\d\.\d+)\] \[Si_ServerPerf\]\s+(\S+)\s+(ON|off \(runtime\))\s.*?\|\s(.*)$",line.rstrip())
    if not m or m.group(2) not in items: continue
    t=R.parse_t(f"{day}T{m.group(1)}")
    k=[i for i,f in enumerate(fl) if f[0]<=t-timedelta(seconds=1)]
    if not k: continue
    arm=fl[k[-1]][1]; t0=fl[k[-1]][0]
    sc=next((s for s,a,b in wins if a<=t0 and t<=b+timedelta(seconds=2)),None)
    if not sc: continue
    out[(sc,arm,m.group(2))].append(m.group(4)[:260])
for k in sorted(out): print(k[0],k[1],k[2],"n=%d"%len(out[k]),"| last:",out[k][-1])
