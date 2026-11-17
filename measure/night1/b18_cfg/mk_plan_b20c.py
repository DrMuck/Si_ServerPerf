"""mk_plan_b20c.py (rig 2, 07:35): coordinator's three extra runs on b20 after b15_snap_check4:
b20_dispose_paired (melee 550 + spread 550, all / allD 120 s blocks), b20_parked_spaced (A / H, 60 s), b20_best2 (headline repeat,
other rotation). Writes rt20_allD.json, disp_b20.json, parked_b20.json and night1\\chain_plan_b20c.json."""
import copy, json, os
N1 = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(N1, "b18_cfg") + "\\"
DLL = "C:\\Users\\schwe\\Projects\\Si_ServerPerf\\release\\b20\\Si_ServerPerf.dll"
a = json.load(open(C + "rt20_all.json"))
d = copy.deepcopy(a)
d["_readme"] = "b20 allD: all + DeadDisposeEarly (proportional: 5 s at <= 25 fps, 15 s at >= 45, smooth 10 s, slew 0.5 s/s; startup cfg_b20.json)"
d["items"]["Physics.DeadDisposeEarly"] = True
json.dump(d, open(C + "rt20_allD.json", "w"), indent=2)
m = json.load(open(C + "melee_b18.json"))
s = json.load(open(C + "spread_b19.json"))
ms = m["scenarios"][0]; ms["name"] = "melee-disp"; ms["plateaus"] = [550]; ms["hold_s"] = 500
ss = s["scenarios"][0]; ss["name"] = "spread-disp"; ss["plateaus"] = [550]; ss["hold_s"] = 500
m["_readme"] = "b20_dispose_paired: melee 550 then 4-front spread 550, hold 500 s each (all/allD 120 s blocks, A D D A cycle)"
m["scenarios"] = [ms, ss]
json.dump(m, open(C + "disp_b20.json", "w"), indent=2)
p = json.load(open(os.path.join(N1, "scenarios_n1_parked.json")))
p["_readme"] = "b20_parked_spaced: spaced-any 4x75 and spaced-slope 6x50 (from scenarios_n1_parked.json), measure 270 s each (A/H 60 s blocks)"
for sc in p["scenarios"]:
    sc["measure_s"] = 270
json.dump(p, open(C + "parked_b20.json", "w"), indent=2)
MB = ("TargetFinder.CollectPreFilteredEnemies,BehaviorTrees.BTEnemyTargetDetected.ActualExecute,TargetFinder.CollectDetectedEnemies,"
      "TargetFinder.RequestScan,Silica.AI.AIBaseAgent.OrderedUpdateAllAgents")
SEQB2 = ["new", "all", "off", "def", "new2", "all", "def", "new2", "off", "new", "new2", "off", "new", "all", "def"]
fm = {"new": "N"}
runs = [
    {"label": "b20_dispose_paired", "expect_min": 27, "timeout_min": 40, "dll": DLL, "scenarios": C + "disp_b20.json",
     "config": C + "cfg_b20.json", "runtime": C + "rt20_all.json", "probe": C + "probe_b18.json",
     "flips": [C + "rt20_" + x + ".json" for x in ["all", "allD", "allD", "all"]], "flip_labels": ["all", "allD", "allD", "all"], "flip_every_s": 120,
     "reports": ["night1\\step_table.py {run} --arms all,allD --settle 60", "night1\\guard_by_arm.py {run} 60",
                 "night1\\tag_by_arm.py {run} census --segment dead --settle 60", "night1\\tag_by_arm.py {run} deaddispose --settle 60",
                 "ab_report.py windows --run {run}"]},
    {"label": "b20_parked_spaced", "expect_min": 15, "timeout_min": 25, "dll": DLL, "scenarios": C + "parked_b20.json",
     "config": C + "cfg_b20_phys.json", "runtime": C + "rt20p_A.json",
     "flips": [C + "rt20p_" + x + ".json" for x in ["A", "H", "H", "A"]], "flip_labels": ["A", "H", "H", "A"], "flip_every_s": 60,
     "reports": ["physics_report.py {run} --settle 20", "ab_report.py flip --run {run} --settle 20", "ab_report.py windows --run {run}"]},
    {"label": "b20_best2", "expect_min": 20, "timeout_min": 30, "dll": DLL, "scenarios": C + "melee_b18.json",
     "config": C + "cfg_b20.json", "runtime": C + "rt20_N.json", "probe": C + "probe_b18.json",
     "flips": [C + "rt20_" + fm.get(x, x) + ".json" for x in SEQB2], "flip_labels": SEQB2, "flip_every_s": 30,
     "reports": ["night1\\step_table.py {run} --arms off,def,new,new2,all --settle 5", "night1\\step_table.py {run} --arms new,off,def,new2,all --settle 5",
                 "ab_report.py windows --run {run}", "night1\\guard_by_arm.py {run} 5",
                 "night1\\methods_by_arm.py {run} --arms off,def,new,new2,all --methods " + MB]},
]
json.dump({"_readme": "rig 2 plan b20c (coordinator 07:35): b20_dispose_paired, b20_parked_spaced, b20_best2 - priority order", "runs": runs},
          open(os.path.join(N1, "chain_plan_b20c.json"), "w"), indent=2)
print("ok")
