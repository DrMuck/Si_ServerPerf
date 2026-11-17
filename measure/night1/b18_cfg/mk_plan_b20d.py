"""mk_plan_b20d.py (rig 2, 08:20): b20_dispose_paired2 = interleaved repeat at melee ~620 lab units, all / allD strictly
alternating, 90 s blocks (evaluate last 40 s), latest start 09:10. Writes melee_disp620.json and night1\\chain_plan_b20d.json."""
import json, os
N1 = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(N1, "b18_cfg") + "\\"
DLL = "C:\\Users\\schwe\\Projects\\Si_ServerPerf\\release\\b20\\Si_ServerPerf.dll"
m = json.load(open(C + "melee_b18.json"))
sc = m["scenarios"][0]; sc["name"] = "melee-disp2"; sc["plateaus"] = [620]; sc["hold_s"] = 600
m["_readme"] = "b20_dispose_paired2: melee plateau 620 only, hold 600 s (all/allD alternating 90 s blocks)"
json.dump(m, open(C + "melee_disp620.json", "w"), indent=2)
run = {"label": "b20_dispose_paired2", "expect_min": 15, "timeout_min": 25, "latest_start": "2026-11-17T09:10:00", "dll": DLL,
       "scenarios": C + "melee_disp620.json", "config": C + "cfg_b20.json", "runtime": C + "rt20_all.json", "probe": C + "probe_b18.json",
       "flips": [C + "rt20_all.json", C + "rt20_allD.json"], "flip_labels": ["all", "allD"], "flip_every_s": 90,
       "reports": ["night1\\step_table.py {run} --arms all,allD --settle 50", "night1\\guard_by_arm.py {run} 50",
                   "night1\\dispose_by_arm.py {run} --settle 50", "ab_report.py windows --run {run}"]}
json.dump({"_readme": "rig 2 plan b20d (coordinator 08:20): b20_dispose_paired2 (interleaved repeat at 620)", "runs": [run]},
          open(os.path.join(N1, "chain_plan_b20d.json"), "w"), indent=2)
print("ok")
