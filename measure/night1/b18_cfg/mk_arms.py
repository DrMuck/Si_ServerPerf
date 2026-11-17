"""mk_arms.py --r 0|1 --j 0|1 --f 0|1 --pp 0|1 [--write-plans b18b,b20a,b20b,b19a,b19b,b19c] (rig 2, 2026-11-17)
ALL runs on build b20 (coordinator 04:45; b20 = b18 items + b19 MultiCore + Targeting.FriendlyFire_Flat).
Writes the startup configs, the arm files whose content depends on the check results and, on request, the chain plan
files night1\\chain_plan_<name>.json that night_driver5.ps1 waits for.
  --r  : CollectPreFiltered_Snapshot passed b18_check      (-> new2)
  --j  : WheelJointWriteGuard passed b18_check             (-> new2)
  --f  : FriendlyFire_Flat passed b20_ff_check             (-> new2)
  --pp : b19_pscan_check passed -> all = new2 + ParallelScan + ParallelScan_Prefiltered; 0 -> all = new2 + ParallelScan
Every arm names every patched non-default item explicitly ("*": true would switch on whatever is patched)."""
import argparse, copy, json, os
ap = argparse.ArgumentParser()
ap.add_argument("--r", type=int, required=True)
ap.add_argument("--j", type=int, required=True)
ap.add_argument("--f", type=int, default=1)
ap.add_argument("--pp", type=int, default=1)
ap.add_argument("--write-plans", default="")
a = ap.parse_args()
N1 = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))          # ...\measure\night1
C = os.path.join(N1, "b18_cfg") + "\\"
B = "C:\\Users\\schwe\\Projects\\Si_ServerPerf\\"
DLL = B + "release\\b20\\Si_ServerPerf.dll"


def w(name, obj):
    with open(C + name, "w") as f:
        json.dump(obj, f, indent=2)


# ---------------------------------------------------------------- startup configs (b20)
MC = {"Items": {"ParallelScan": True, "ParallelScan_Prefiltered": True},
      "Settings": {"ParallelScanJobs": 6, "ParallelScanMaxDirty": 128, "ParallelScanWaitMicros": 150, "ParallelScanMinScans": 4,
                   "ParallelScanMinFrameMs": 0, "ParallelScanSkipAfterWasted": 2, "ParallelScanProbeEvery": 4,
                   "ParallelScanInline": False, "SelfCheckFraction": 0}}
cfg = {"Modules": {
    "Targeting": {"Items": {"CollectEnemies_Snapshot": True, "CollectEnemies_SnapshotScore": True, "CollectEnemies_FastAdd": True,
                            "CollectPreFiltered_Snapshot": True, "FriendlyFire_Flat": True, "FriendlyFire_Cache": False, "Scan_Diag": True},
                  "Settings": {"SelfCheckFraction": 0}},
    "Physics": {"Items": {"DeadAgentHalt": True, "DeadNoUnitContact": True, "DeadSettle": True, "DeadDisposeEarly": True,
                          "WheelJointWriteGuard": True, "VehicleSolverIterations": True},
                "Settings": {"DeadSettleRemoveAvoidance": False, "DeadNoContactCreatures": True, "DeadNoContactVehicles": False,
                             "DeadNoContactSoldiers": False, "DeadAgentHaltClearAim": False, "DisposeEarlyFpsLow": 25,
                             "DisposeEarlyFpsHigh": 45, "DisposeEarlyFpsSmoothSeconds": 10, "DisposeEarlySlewSecondsPerSecond": 0.5,
                             "DisposeEarlySeconds": 5, "VehicleSolverIterations": 4, "VehicleSolverVelocityIterations": 0}},
    "MultiCore": MC}}
w("cfg_b20.json", cfg)
for name, tf, mf in (("cfg_b20_chk18.json", 0.5, 0), ("cfg_b20_ffchk.json", 0.5, 0), ("cfg_b20_pschk.json", 0.2, 0.2)):
    c = copy.deepcopy(cfg)
    c["Modules"]["Targeting"]["Settings"]["SelfCheckFraction"] = tf
    c["Modules"]["MultiCore"]["Settings"]["SelfCheckFraction"] = mf
    w(name, c)
ph = {"Modules": {"Physics": {"Items": {"IdleForceSleep": True, "AvoidanceSkipOntoResting": True, "AvoidanceNoIdleDetour": True,
                                        "ParkedWheelHold": True, "WheelJointWriteGuard": True, "VehicleSolverIterations": True,
                                        "FixedWakeTrace": False, "IdleSleepDiag": False, "WheeledSleepDiag": False,
                                        "AvoidanceIdlePairSkip": False, "WakeProbe": False},
                              "Settings": {"VehicleSolverIterations": 4, "VehicleSolverVelocityIterations": 0}},
                  "MultiCore": {"Items": {"ParallelScan": False, "ParallelScan_Prefiltered": False}, "Settings": copy.deepcopy(MC["Settings"])}}}
w("cfg_b20_phys.json", ph)

# ---------------------------------------------------------------- arms
X = ["Targeting.CollectEnemies_Snapshot", "Targeting.CollectEnemies_SnapshotScore", "Targeting.CollectEnemies_FastAdd",
     "Targeting.CollectPreFiltered_Snapshot", "Targeting.FriendlyFire_Flat", "Physics.DeadAgentHalt", "Physics.DeadNoUnitContact",
     "Physics.DeadSettle", "Physics.DeadDisposeEarly", "Physics.WheelJointWriteGuard", "Physics.VehicleSolverIterations",
     "MultiCore.ParallelScan", "MultiCore.ParallelScan_Prefiltered"]
NEW = ["Targeting.CollectEnemies_Snapshot", "Targeting.CollectEnemies_SnapshotScore", "Targeting.CollectEnemies_FastAdd", "Physics.DeadAgentHalt"]
R, J, F = "Targeting.CollectPreFiltered_Snapshot", "Physics.WheelJointWriteGuard", "Targeting.FriendlyFire_Flat"
PS, PP = "MultiCore.ParallelScan", "MultiCore.ParallelScan_Prefiltered"
EX = ([R] if a.r else []) + ([J] if a.j else []) + ([F] if a.f else [])
NEW2 = NEW + EX
ex_txt = " + ".join(s.split(".")[1] for s in EX) or "(no exact item passed)"
TOP = NEW2 + ([PS, PP] if a.pp else [PS])


def arm(name, readme, on, seq=None, extra=None):
    it = {"*": True}
    for x in X:
        it[x] = x in on
    if extra:
        it.update(extra)
    o = {"_readme": "b20 " + readme, "items": it, "dump": True}
    if seq:
        o["seq"] = seq
    w("rt20_" + name + ".json", o)


w("rt20_off.json", {"_readme": "b20 off: every item off except the exception counter", "items": {"*": False, "Headless.Diag_LogExceptions": True}, "dump": True})
w("rt20_offc.json", {"_readme": "b20 off + Physics.Census (read-only body census, so deadU is visible in the off arm)",
                     "items": {"*": False, "Headless.Diag_LogExceptions": True, "Physics.Census": True}, "dump": True})
arm("def", "def: shipped b10 defaults", [])
arm("N", "new: def + 3 snapshot items + DeadAgentHalt (ClearAim false)", NEW)
arm("R", "R: N + CollectPreFiltered_Snapshot", NEW + [R])
arm("J", "J: N + WheelJointWriteGuard", NEW + [J])
arm("RJ", "RJ: N + CollectPreFiltered_Snapshot + WheelJointWriteGuard", NEW + [R, J])
arm("V", "V: N + VehicleSolverIterations (approximate, experiment)", NEW + ["Physics.VehicleSolverIterations"])
arm("F", "F: N + FriendlyFire_Flat", NEW + [F])
arm("k18a", "b18_check arm: N + R + J (seq a)", NEW + [R, J], 501)
arm("k18b", "b18_check arm: N + R + J (seq b)", NEW + [R, J], 502)
arm("kffa", "ff_check arm: N + FriendlyFire_Flat, Grid on (seq a)", NEW + [F], 701)
arm("kffb", "ff_check arm: N + FriendlyFire_Flat, Grid on (seq b)", NEW + [F], 702)
arm("kffg1", "ff_check arm 2nd half: N + FriendlyFire_Flat, FriendlyFire_Grid OFF (seq a)", NEW + [F], 711, {"Targeting.FriendlyFire_Grid": False})
arm("kffg2", "ff_check arm 2nd half: N + FriendlyFire_Flat, FriendlyFire_Grid OFF (seq b)", NEW + [F], 712, {"Targeting.FriendlyFire_Grid": False})
CHK = NEW + ([R] if a.r else []) + [PS, PP]
arm("kpsa", "pscan_check: new + CollectPreFiltered_Snapshot(if passed) + ParallelScan + ParallelScan_Prefiltered (seq a)", CHK, 601)
arm("kpsb", "pscan_check: same (seq b)", CHK, 602)
arm("new2", "new2 = B: new + " + ex_txt, NEW2)
arm("P", "P: new2 + ParallelScan (round 2)", NEW2 + [PS])
arm("PP", "PP: P + ParallelScan_Prefiltered", NEW2 + [PS, PP])
arm("all", "all: new2 + ParallelScan" + (" + ParallelScan_Prefiltered" if a.pp else " (Prefiltered left out)"), TOP)
PX = ["Physics.IdleForceSleep", "Physics.AvoidanceSkipOntoResting", "Physics.AvoidanceNoIdleDetour", "Physics.ParkedWheelHold",
      "Physics.WheelJointWriteGuard", "Physics.VehicleSolverIterations"]


def parm(name, readme, on):
    it = {"*": True}
    for x in PX:
        it[x] = x in on
    w("rt20p_" + name + ".json", {"_readme": "b20 phys " + readme, "items": it, "dump": True})


parm("A", "A: defaults", [])
parm("J", "J: defaults + WheelJointWriteGuard", ["Physics.WheelJointWriteGuard"])
parm("V", "V: defaults + VehicleSolverIterations", ["Physics.VehicleSolverIterations"])
parm("H", "H: IdleForceSleep + AvoidanceSkipOntoResting + AvoidanceNoIdleDetour + ParkedWheelHold", PX[:4])

# ---------------------------------------------------------------- plans
MB = ("TargetFinder.CollectPreFilteredEnemies,BehaviorTrees.BTEnemyTargetDetected.ActualExecute,TargetFinder.CollectDetectedEnemies,"
      "TargetFinder.RequestScan,Silica.AI.AIBaseAgent.OrderedUpdateAllAgents,VehicleTurret.OrderedUpdateAllTurrets,Target.CheckRiskOfFriendlyFire")


def run(label, exp, tmo, scen, cfgf, rt0, seq, every, reports, prefix="rt20_", fmap=None, probe=True):
    r = {"label": label, "expect_min": exp, "timeout_min": tmo, "dll": DLL, "scenarios": scen, "config": C + cfgf, "runtime": C + prefix + rt0 + ".json"}
    if probe:
        r["probe"] = C + "probe_b18.json"
    if seq:
        r.update({"flips": [C + prefix + (fmap or {}).get(s, s) + ".json" for s in seq], "flip_labels": seq, "flip_every_s": every})
    r["reports"] = reports
    return r


def st(arms, settle=5):
    return "night1\\step_table.py {run} --arms %s --settle %g" % (arms, settle)


WIN = "ab_report.py windows --run {run}"
GRD = "night1\\guard_by_arm.py {run} 5"
CHECK450 = N1 + "\\b13_cfg\\melee_check450.json"
SEQ5 = ["N", "R", "J", "RJ", "V", "N", "V", "RJ", "R", "J", "N", "RJ", "V", "J", "R"]
SEQB = ["off", "def", "new", "new2", "all", "off", "all", "new2", "def", "new", "off", "new2", "all", "new", "def"]
SEQ3 = ["B", "P", "PP", "B", "PP", "P", "B", "P", "PP", "B", "PP", "P"]
SEQ4 = ["off", "def", "new", "all", "off", "all", "def", "new", "off", "new", "all", "def"]
plans = {
    "b18a": [run("b18_check", 9, 18, CHECK450, "cfg_b20_chk18.json", "k18a", ["K1", "K2"], 45, [WIN], fmap={"K1": "k18a", "K2": "k18b"})],
    "b18b": [
        run("b18_items", 20, 32, C + "melee_b18.json", "cfg_b20.json", "N", SEQ5, 30,
            [st("N,R,J,RJ,V"), WIN, "night1\\methods_by_arm.py {run} --arms N,R,J,RJ,V --methods " + MB, GRD, st("N,V", 10)]),
        run("b18_phys_guard", 12, 22, C + "scenarios_b18_move.json", "cfg_b20_phys.json", "A", ["A", "J", "V", "V", "J", "A"], 60,
            ["physics_report.py {run} --settle 20", "ab_report.py flip --run {run} --settle 20", WIN], prefix="rt20p_", probe=False),
        run("b18_phys_guard_idle", 7, 15, C + "scenarios_b18_idle.json", "cfg_b20_phys.json", "H", None, 0,
            ["physics_report.py {run} --settle 20"], prefix="rt20p_", probe=False)],
    "b20a": [run("b20_ff_check", 11, 20, C + "melee_ffcheck.json", "cfg_b20_ffchk.json", "kffa",
                 ["K1", "K2", "K1", "K2", "K1", "K2", "G1", "G2", "G1", "G2", "G1", "G2", "G1", "G2", "G1", "G2"], 45, [WIN],
                 fmap={"K1": "kffa", "K2": "kffb", "G1": "kffg1", "G2": "kffg2"})],
    "b20b": [run("b20_ff", 14, 25, C + "melee_ff.json", "cfg_b20.json", "N", ["N", "F"], 30,
                 [st("N,F"), WIN, "night1\\methods_by_arm.py {run} --arms N,F --methods " + MB, GRD])],
    "b19a": [run("b19_pscan_check", 9, 18, CHECK450, "cfg_b20_pschk.json", "kpsa", ["K1", "K2"], 45, [WIN], fmap={"K1": "kpsa", "K2": "kpsb"})],
    "b19b": [run("b19_pscan", 18, 30, C + "melee_b19.json", "cfg_b20.json", "new2", SEQ3, 30,
                 [st("B,P,PP"), WIN, "night1\\methods_by_arm.py {run} --arms B,P,PP --methods " + MB, GRD], fmap={"B": "new2"})],
    "b19c": [
        run("b20_best", 20, 32, C + "melee_b18.json", "cfg_b20.json", "N", SEQB, 30,
            [st("off,def,new,new2,all"), st("new,off,def,new2,all"), WIN, GRD,
             "night1\\methods_by_arm.py {run} --arms off,def,new,new2,all --methods " + MB], fmap={"new": "N"}),
        run("b19_spread", 26, 40, C + "spread_b19.json", "cfg_b20.json", "N", SEQ4, 30,
            [st("off,def,new,all"), st("def,off,new,all"), WIN, GRD, "night1\\tag_by_arm.py {run} census --segment dead --settle 5"],
            fmap={"new": "N", "off": "offc"})],
}
for p in [x for x in a.write_plans.split(",") if x]:
    with open(os.path.join(N1, "chain_plan_%s.json" % p), "w") as f:
        json.dump({"_readme": "rig 2 plan %s on b20 (mk_arms.py --r %d --j %d --f %d --pp %d)" % (p, a.r, a.j, a.f, a.pp), "runs": plans[p]}, f, indent=2)
    print("wrote chain_plan_%s.json: %s" % (p, ", ".join(r["label"] for r in plans[p])))
print("configs + arms written (b20); new2 = new +", ex_txt, "; all = new2 +", "ParallelScan + Prefiltered" if a.pp else "ParallelScan")
