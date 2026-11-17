# b13 runs (coordinator request 2026-11-17 ~00:20)

Build `C:\Users\schwe\Projects\Si_ServerPerf\release\b13\Si_ServerPerf.dll` (md5 B9755D488C848828A9EDB1FEA1BB4424).
New vs b12 (all default off unless noted): Physics `DeadSettle`, `DeadDisposeEarly` (info only, visible to players),
census `| dead deadU= deadDyn= deadSl= deadAwake= ... aliveU= ... avoidAgents= deadSettled=` segment (on with Census);
Targeting `CollectEnemies_Snapshot` (stage 1), `CollectEnemies_SnapshotScore` (stage 2, needs 1), `CollectEnemies_FastAdd`,
`Scan_Diag` (default ON). Default-off items must be true in the startup JSON, then flipped per arm.
Settings: `Modules.Targeting.Settings.SelfCheckFraction` (default 0), `SnapshotPoseOneCall` (default true).

Files: `night1\b13_cfg\`
* configs: `cfg_b13_check.json` (all 6 new/aggr items patched, SelfCheckFraction 0.05), `cfg_b13_check_pose2.json`
  (same + SnapshotPoseOneCall false), `cfg_b13.json` (patched, SelfCheckFraction 0)
* arms: `rt13_A` defaults, `rt13_B` +DeadSettle, `rt13_C` +DeadSettle+DeadDisposeEarly, `rt13_S1` +Snapshot,
  `rt13_S2` +Snapshot+Score, `rt13_S3` +FastAdd too, `rt13_L` +LosTopK, `rt13_check` all 3 snapshot items, `rt13_off`
* scenarios (melee plateau = quick-check geometry: 3 teams into one centre front r 45 m, staging 250 m back, damage on,
  respawn 2 s, lategame composition, base ratio 0.08 x plateau): `melee_check450.json` (450, hold 240 s),
  `melee_dead.json` (450/550, hold 420), `melee_snap.json` (450/550, hold 330), `melee_best.json` (450/550, hold 390).
  If 550 falls under ~8 fps in off, drop to 400/500.

Plans: `night1\chain_plan_b13a.json` = runs 1+2 (queued by night_driver.ps1). Runs 3+4 go into
`night1\chain_plan_b13b.json`, written by the rig agent after checking run 1; night_driver waits up to 30 min after
plan b13a for that file, otherwise skips to chain_plan.json.

## Run 1 `b13_snap_check` (~9 min) - correctness
Plateau 450, all three snapshot items on, SelfCheckFraction 0.05, no flips. Pass = "mismatch total 0" on
CollectEnemies_Snapshot and CollectEnemies_Grid, no FAILED, no exceptions. If mismatches: record the first logged
difference (path, field), rerun once with `cfg_b13_check_pose2.json` (SnapshotPoseOneCall=false) as
`b13_snap_check2`, send both to main; skip run 3 if it still mismatches.

## Run 2 `b13_dead` (~19 min)
Arms A / B / C, 60 s blocks, cycle A,B,C,A,C,B, evaluate from 20 s after each flip (`step_table.py --settle 20`).
Report per arm and plateau: fps, frame work, PhysX ms/s and ms/step, fixed phase, VehicleWheeled fixed,
CollisionAvoidance (+ manager PrepareFixedUpdate) ms/s; census deadU / world units, deadAwake vs deadDyn, aliveAwake,
avoidAgents; `[Physics] deadsettle` seen, settled, rewoken (per kind), timeLimit, waitNow reasons, fixedSkips,
settleAgeMean, restored after switch-off; `[Physics] deaddispose` shortened (C). Check W - lab - base equal in A and B;
deaths/s, kills, engagement equal across A and B.

## Run 3 `b13_snap` (~15 min)
Arms A / S1 / S2 / S3 / L, 30 s blocks (cycle A,S1,S2,S3,L), cfg_b13.json (SelfCheckFraction 0). Report per arm: fps,
frame work, `TargetFinder.CollectDetectedEnemies` ms/s, us per call, calls/s, `BTEnemyTargetDetected` ms/s; snapshot
counters (entry fills, scans served, mismatches); Scan_Diag FindBestTarget calls/s and null results/s per arm
(question for L: does LosTopK raise the null rate and the number of full scans).

## Run 4 `b13_best` (only items that passed)
Quick-check trickle (`b13_best_qc`, scenarios_quickcheck.json) AND the two plateaus (`b13_best_plat`, melee_best.json)
with arms off / def / new (= defaults + DeadSettle + passed snapshot items). Headline: fps and frame work per matched
unit count (25-unit bins / per-arm fits for the trickle), how far the fps-10 floor moves.

All runs: exceptions and FAILED items per arm; say plainly if too noisy. Compact table to `main` after each b13 run.

# b14 runs (coordinator request ~01:45) - replace b13_best (b13_best_plat / b13_best_qc skipped: folders exist with SKIPPED.txt)

Build `release\b14\Si_ServerPerf.dll` (md5 9F0FDB01B8349B4C77568234EDB8F313). New: Physics `DeadAgentHalt` (+ `DeadAgentHaltClearAim`
default true; log `[Physics] deadagents haltedNow= skips=`), `DeadNoUnitContact` (visible; per kind DeadNoContactCreatures
true / Vehicles false / Soldiers false; log `[Physics] deadcontact ... mask=` must not contain Terrain/Static/Default),
`DeadDisposeEarly` valve (`DisposeEarlyBelowFps` 0 = always, hysteresis 5, smooth 5 s; log `[Physics] deaddispose valve=`).
`DeadSettleRemoveAvoidance` set false explicitly. Files `night1\b14_cfg\` (cfg_b14.json, cfg_b14_valve.json with
DisposeEarlyBelowFps 30, rt14_{A,D,E,C,new,max,valve,off}.json, melee_dead14.json 450/550 hold 540, melee_b14best.json
450/550 hold 270, melee_valve.json 550 hold 300). Runs are at the top of `chain_plan.json` (before qc2).

* Run 5 `b14_dead`: arms A defaults / D + DeadAgentHalt / E D + DeadSettle + DeadNoUnitContact / C defaults +
  DeadDisposeEarly (always), cycle A,D,E,C,A,C,E,D, 60 s, evaluate from 20 s. Report fps, work vs A paired, PhysX,
  fixed phase, per s AIBaseAgent.OrderedUpdateAllAgents, BTEnemyTargetDetected / CollectDetectedEnemies ms/s + calls/s,
  FindBestTarget/s (Scan_Diag), LineOfSightManager.PublishResults, AIManager.TickAI; deadagents haltedNow vs census deadU;
  deadAwake; E deadsettle settledNow / rewoken by kind; deadcontact mask. Guard D vs A: deaths/s, kills, shots/unit-s,
  with-target share per team equal within noise.
* Run 6 `b14_best_plat` (arms off/def/new/max, 30 s) + `b14_best_qc` (trickle, off/def/new, matched 25-unit bins, per-arm
  fits, where each arm crosses 10/20/30 fps). new = defaults + snapshot items that passed check2 + DeadAgentHalt if run 5
  shows a gain and the guard holds (EDIT `b14_cfg\rt14_new.json` and `rt14_max.json` BEFORE b14_best_plat starts if not).
  max = new + DeadDisposeEarly always.
* Run 7 `b14_valve`: plateau 550, new + DeadDisposeEarly with DisposeEarlyBelowFps 30: does the valve open, fps recover,
  oscillate (opens/closes per minute)?
Table to main after each run.
# b15 (02:20): b14_best/valve/halt_guard runs replaced by b15_* (see STATE.md); configs night1\b14_cfg\cfg_b15*.json; arms reuse rt14_*.json. halt_guard1 = ClearAim false (5+5 blocks), halt_guard2 = ClearAim true (4+4). b15_eveh after the b10 queue.
