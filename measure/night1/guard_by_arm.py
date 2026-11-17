import sys, os
sys.path.insert(0,".")
import ab_report as R
from collections import defaultdict
from datetime import timedelta
run=sys.argv[1]; settle=float(sys.argv[2])
smp=R.read_csv(os.path.join(R.one_dir(run,"perflab","samples.csv"),"samples.csv")); fl=R.read_flips(run)
acc=defaultdict(lambda:[0.0]*7)
for s in smp:
    if s.get("phase")!="Measure": continue
    t=R.parse_t(s["timestamp_iso"]); k=[i for i,f in enumerate(fl) if f[0]<=t-timedelta(seconds=1)]
    if not k or (t-fl[k[-1]][0]).total_seconds()<settle+1: continue
    x=acc[(s["scenario"],fl[k[-1]][1])]; x[0]+=1
    for i,c in enumerate(["deaths","kills","shots","hits","lab_units","with_target"]): x[i+1]+=R.fnum(s.get(c),0)
print("| plateau | arm | s | lab units | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |")
print("|---|---|---:|---:|---:|---:|---:|---:|---:|")
for k,x in sorted(acc.items()):
    n=x[0]; print(f"| {k[0]} | {k[1]} | {n:.0f} | {x[5]/n:.0f} | {x[1]/n:.2f} | {x[2]/n:.2f} | {x[3]/x[5]:.3f} | {x[4]/n:.1f} | {x[6]/x[5]:.3f} |")
