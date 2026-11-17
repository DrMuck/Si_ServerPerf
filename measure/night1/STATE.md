# night1 STATE (rig agent, 2026-11-16/17)

Rules recap: lab copy only (C:\tmp\silica_ds_lab); run_lab.ps1 only; no new server run after **09:30** (changed from 06:30
by the user at ~20:50), 0 own Silica processes by **10:00**; never touch a Silica process we did not start.

## NIGHT QUEUE FINISHED (rig 2, 09:00)
- Last run b20_dispose_paired2 DONE 08:58:57; all drivers (night_driver4..7) and chains have exited; no Silica process is running (checked 09:00); no STOP_CHAIN, no current_run.txt. Nothing else is queued; nothing will start.
- Rig-2 runs (all on b20 unless noted): b18_check, b18_items, b18_phys_guard, b18_phys_guard_idle, b20_ff_check, b20_ff, b19_pscan_check, b19_pscan, b20_best, b19_spread, b15_eveh (b15), b15_snap_check4 (b15), b20_dispose_paired, b20_parked_spaced, b20_best2, b20_dispose_paired2. All reported to main and appended to RESULTS_LOG.md. Nothing dropped.

## RIG 2 (second rig agent, from 04:35) - b18/b19/b20 queue; this block is current for everything after qcgod1
- ALL rig-2 runs use build **b20** (release\b20, md5 82F1D0C78CAB9A9426B03C48B0F3DDCA = b18 items + b19 MultiCore round 2 +
  Targeting.FriendlyFire_Flat); run names kept. Coordinator change 04:45.
- Driver **night1\night_driver5.ps1** (PID 3228, detached, restarted 04:46): waits for the `BEGIN qcgod1` line, then creates
  STOP_CHAIN (chain_plan3 chain ends after qcgod1, night_driver4 exits), removes STOP_CHAIN, then runs in order:
  1. `chain_plan_b18a.json`  b18_check (melee 450, N+R+J, Targeting fraction 0.5)            - EXISTS
  2. `chain_plan_b18b.json`  b18_items (N/R/J/RJ/V), b18_phys_guard (move 300 A/J/V), b18_phys_guard_idle (idle 300 H)
  3. `chain_plan_b20a.json`  b20_ff_check (450, N+FriendlyFire_Flat, Grid on ~first half then FriendlyFire_Grid off)
  4. `chain_plan_b20b.json`  b20_ff (450/550, N/F)
  5. `chain_plan_b19a.json`  b19_pscan_check (450, new+R+ParallelScan+Prefiltered, fractions 0.2/0.2)
  6. `chain_plan_b19b.json`  b19_pscan (450/550, B=new2 / P / PP)
  7. `chain_plan_b19c.json`  b20_best (off/def/new/new2/all) + b19_spread (4-front sustain 450/550/650, off(+census)/def/new/all)
  8. `chain_plan_rest.json`  b15_eveh, b15_snap_check4 (latest_start 09:00)                   - EXISTS
  Plans 2-7 are written by the rig after the preceding check:
  `cd night1; python b18_cfg\mk_arms.py --r R --j J --f F --pp PP --write-plans <names>` (R/J = CollectPreFiltered_Snapshot /
  WheelJointWriteGuard passed b18_check, F = FriendlyFire_Flat passed b20_ff_check, PP = b19_pscan_check passed; it rewrites
  configs + rt20_* arms; new2 = new + passed exact items; all = new2 + ParallelScan (+ Prefiltered if PP)). The driver
  waits up to 25 min per missing plan file, then skips it; it skips nothing else. Files: night1\b18_cfg (cfg_b20*.json,
  rt20_*.json, rt20p_*.json, melee_b18/b19/ff/ffcheck, spread_b19, scenarios_b18_move/idle, probe_b18 = probe_lab +
  CollectPreFilteredEnemies); obsolete b18/b19-dll arm files in b18_cfg\_obsolete_b18b19.
- 04:40 note: a first driver5 start falsely matched its own log text and created STOP_CHAIN during qc_fixed20; killed and
  STOP_CHAIN removed within 20 s (chain unaffected); fixed driver restarted.
- Results of rig 2 go to night1\RESULTS_LOG.md and to main.
- 07:30 DONE + reported: b18_check (pass), b18_items, b18_phys_guard(+_idle), b20_ff_check (pass), b20_ff, b19_pscan_check (pass; emulation path not exercised), b19_pscan, b20_best, b19_spread. RUNNING: b15_eveh (07:26, chain_plan_rest), then b15_snap_check4; after that the queue is empty (driver5 ends).
- 07:30 coordinator added 3 runs: **night_driver6.ps1** (PID 17160) waits for driver5 (3228) to exit, then runs `chain_plan_b20c.json` = b20_dispose_paired (melee 550 + spread 550, all/allD 120 s, evaluate last 60 s), b20_parked_spaced (A/H 60 s), b20_best2 (headline repeat, other rotation). Built by b18_cfg\mk_plan_b20c.py. Then a final message to main.
- 08:17 coordinator added b20_dispose_paired2 (melee 620, all/allD alternating 90 s, latest start 09:10): **night_driver7.ps1** (PID 22988) waits for driver6 (17160), then runs `chain_plan_b20d.json` (b18_cfg\mk_plan_b20d.py).
- 05:55 decisions: b18_check PASSED (R, J); b18_items no gain. Coordinator: J (WheelJointWriteGuard) OUT of new2/all,
  VehicleSolverIterations closed. Use `mk_arms.py --r 1 --j 0 --f 0 --pp <pscan_check>` from now on (06:25: FriendlyFire_Flat passed but no gain -> OUT of new2, coordinator).
  b20a + b19a plans written already; b20b (after b20_ff_check), b19b (after b19_pscan_check), b19c still to write.

## Status (rewritten 04:32, amended 04:50 / 04:55; this block is the truth, the log below is history)
- 04:55 HANDOVER: rig 1 (this agent) owns the chain only through b17_deepprobe, qc_fixed20 and qcgod1 (all in night_driver4 plans). **After `DONE qcgod1` the chain belongs to rig 2** (b18_check / b18_items / b18_phys_guard / b18_best, then b15_eveh, b15_snap_check4); rig 2 inserts its plan via STOP_CHAIN while qcgod1 runs + its own driver. Rig 1 starts or reorders nothing after qcgod1.
- 04:50: NIGHT1.md is OWNED BY A SEPARATE WRITER AGENT - the rig does not edit it (rig draft kept as NIGHT1_rig_draft.md). The rig appends one section per finished run to `night1\RESULTS_LOG.md` (same text as the table sent to main) and keeps this block current. Queue order now: qc2 (running) -> b17_deepprobe -> qc_fixed20 -> qcgod1 -> b18 runs (when sent; insert before b15_eveh via STOP_CHAIN + new driver) -> b15_eveh -> b15_snap_check4. The rig agent context is long: a fresh rig may take the b18 runs from this file.
- RUNNING: **qc2** (b10, started 04:16, ~26 min) in chain `chain.ps1 -Plan chain_plan.json` started by night_driver3
  (PID 25192). `night1\STOP_CHAIN` exists ON PURPOSE: the chain stops after qc2, night_driver3 exits, and
  **night_driver4** (`night1\night_driver4.ps1`, PID 26724) deletes STOP_CHAIN and runs:
  1. `chain_plan_deep.json`: **b17_deepprobe** (b17, melee 550, arm P = new + ParallelScan fixed, 240 s, PerfProbe config
     `night1\b16_cfg\probe_deep.json` = probe_lab + 37 methods from ANALYSIS_BATTLE_2.md section 3). Table to main ASAP
     (decision values: CollectPreFilteredEnemies >= 35 ms/s?, RequestQueryToTarget >= 15?, path/order probes >= 15 self?,
     CreatureAttack:CanHitTarget >= 25?, NonBatchedRaycastP1 >= 30?).
  2. `chain_plan3.json`: qc_fixed20 (b10, def vs def + FixedStep_Override 20 Hz), qcgod1 (b10, god quick check),
     b15_eveh (b15), b15_snap_check4 (b15, spread god 800, fraction 0.5) - lowest priority last.
  probe_F2, probe_A2, qc3 DROPPED (folders with SKIPPED.txt). b18 expected ~05:30-06:00: insert via STOP_CHAIN + new driver
  (same pattern) before the chain_plan3 runs that are left.
- Current chain PID `night1\chain.pid`; current run `night1\current_run.txt`; progress `night1\chain_progress.log`.
  Cut-offs: no new run after 09:30, nothing running at 10:00 (chain.ps1 enforces). NIGHT1.md complete by 09:00.
- DONE (night1\<label>): shake1-4, failfast, qc1, phys_b10, parked_v2, probe_A/F/C, ramp_god, ramp_sustain (b10);
  fixedwake_b11; phys_b12, phys_b12_HD, probe_H_b12; b13_snap_check, b13_dead, b13_snap, b13_snap_check2; b14_dead,
  b14_snap_check3; b15_best_plat, b15_best_qc, b15_valve, b15_halt_guard1, b15_halt_guard2; b17_pscan_check (incomplete),
  b17_pscan_check2, b17_pscan, b17_pscan_inline, b17_dispose_prop, b17_dispose_always (b17). All reported to main except
  b17_dispose_prop / b17_dispose_always (TO DO now).
- NIGHT1.md: sections 1-5e and 8 written. TO DO: dispose (5e), deepprobe, qc2 spread vs qc1, fixed20, qcgod, eveh, check4,
  section 6 correctness, 7 per-item recommendation, 9 not measured, run-to-run fps variation between separate starts,
  reference ANALYSIS_BATTLE_2.md. Context of the rig agent is getting long; if it dies, a fresh agent can finish NIGHT1.md
  from the run folders + the messages summarised in this file.
- Tools in night1\: step_table.py (paired), sections_by_step.py, methods_by_arm.py, cpu_by_arm.py, guard_samples.py,
  guard_blocks.py, engagement_summary.py, tag_by_arm.py, windows_items.py, crossings.py.

## Done
- 20:37 read PROTOCOL / SCENARIOS_V2 / WAVE1; created night1\
- 20:38 Si_PerfLab 0.2.0 deployed to _modsets\perflab; 0.1.1 backed up in `C:\tmp\silica_ds_lab\_modsets\_backup_perflab_0.1.1\`
- 20:39-20:53 shake1 (PerfLab 0.2.0, b10 defaults, night1\scenarios_shake1.json):
  - trickle (front "center" = HQ centroid (-325,133), not the SCENARIOS_V2 coordinates): works, all 3 teams fight
    (Alien creature attacks 4022, kills 221), respawn keeps the count (150->360), base 13 game / 2 lab placed.
  - steps sustain 4 fronts @150 ok; **@300 server hang**: FogOfWar.LateUpdate 48 s over 42 frames (2 Job.Workers 100 %,
    main thread waiting in JobHandle.Complete of the vanilla ExploreAreaJob), fps 0.86 until cleanup, then normal.
  - god mode: FRONT STALL every 30 s on every front (god mode deals no hits) -> fixed in 0.2.1.
  - parking terrain flat: no lot found (GreatErg) -> those vehicles silently NOT spawned (warning only).
  - order probe: sane numbers (infantry/creature first move p50 0.23-0.56 s, reach 0.6-1.2 s, 0 failed).
- 20:56 Si_PerfLab **0.2.1** built + deployed (md5 EA515F027CC651645867D6D10E6D1CB2); 0.2.0 dll kept as
  `_backup_perflab_0.1.1\Si_PerfLab_0.2.0.dll.bak`. Changes: (1) Engagement.cs/Ramp.cs: in god-mode ramps shots and
  creature attacks of front units count as front activity for the stall check; (2) Parking.cs: the "no lot" warning
  reports the flattest lot seen; (3) version 0.2.1 (PerfLabMod.cs, csproj).
- 20:57 shake2 (all OFF): radar base at 150 god + no-radar base at 300 god: no hang; 0 FRONT STALL (fix ok); flattest
  lot within 900 m of the HQs = 12.1 deg max / 7.7 mean -> "flat" impossible on GreatErg.
- 21:05 shake3_off / 21:13 shake4_def: exact repeat of the shake1 sustain ramp (150/300/400, radar in base), all OFF and
  all defaults: **no hang in either** (off: fps 119/99/69, work 4.0/8.1/13.9 ms; defaults: 119/106/84, 3.3/6.6/10.4 ms).
  The shake1 hang is an unreproduced one-off (open item; see NIGHT1.md).
- 21:20 fail-fast test (structure_ratio as a string): FATAL at load in 1 s, FAILED file written, server exited after 9 s. OK.
- night1 scenario variants: `scenarios_n1_parked.json` (spaced-any 4x75 + spaced-slope 6x50; WORSTCASE dropped as it
  equals phys-idle-vehicles in scenarios_physics_b10.json), `scenarios_n1_order_probe.json` (parking terrain any).
- chain.ps1 first start 21:20 failed at once (PowerShell variable-name collision, nothing ran); fixed, restarted 21:21.

## Decisions
- Quick check base = 4 structures/team (no radar), fixed during the trickle. Ramps keep the original lategame base.
- Order probe: one run per arm (A defaults / F / C), no flips, so each cycle has one arm; F and A repeated at the end.
- Sustain plateaus run 5-6 % under target (dev_max 10-13 %, deaths 3-11/s): accepted, same in every arm; reported.

## Results so far (read from reports)
- 22:10 qc1 DONE (15 min): climb stopped at fps floor at ~625 lab units (not 1000). Bins work ms off/def/agg: 300-349 13.8/9.4/9.0; 450-499 57.9/43.9/29.1; 550-599 112.7/81.5/86.2. 0 exceptions any arm, nothing FAILED. Quadratic fit extrapolations to 800/1000 are meaningless (data ends ~620).
- 22:10 phys_b10 DONE: paired vs A: C -8.6 % (idle LightStriker 300, s.e. 3.5 %) / -0.3 % (mixed); F -18.7 % (s.e. 1.5 %) / -15.6 % (s.e. 1.9 %); move guard ~0. Asleep A 3 % / C 40 % / F 71 % (idle), 1/22/67 % (mixed). Creep of force-slept vehicles: C mean 0.38-0.57 m max ~3 m, F 0. A arm drift not measured in b10 (no parkeddrift line without WheeledSleepDiag). 0 FAILED.
- 23:00 parked_v2 DONE: spaced-any (lots actually steep: mean 14-21 deg) F-A -3.3 % (s.e. 11 %, nothing), C -0.7 %; spaced-slope (mean 8-14 deg) F -13.9 % (s.e. 3 %), C -2.4 %. Asleep A 9/3 %, C 57/44 %, F 67/73 %. Lots were near (-420,-460).. i.e. the Sol HQ was elsewhere this round (HQ positions vary per round).
- 23:00 probes A/F/C DONE: 0 order failures, 0 never-started; reach-2 m p50 identical across arms (vehicles 1.4-3.0 s); F first-move p50 for Sol/Cent light 0.1 s (vs 0.7-1.2 in A) = jolt at wake, not a delay. Wheeled asleep before the orders: A 95/62/22/18 %, F 90/83/72/74 %, C 86/63/45/45 % (cycles 0-3).
- NIGHT1.md partial draft being written (sections for qc1, physics, probe).

- 23:25 COORDINATOR ADDITION: NIGHT1.md needs a section 'How the scenarios compare with real rounds' (OFF-arm fps per unit count: DEV_REPORT ï¿½1 AI rounds, WAVE1 b9 god battles, quick check, ramp_god, ramp_sustain; engagement per scenario: with-target share, shots/unit-s, deaths/s) + explanation of the qc knee (~425-475 lab units) from PerfProbe sections (done in draft: time-driven share = 25 Hz fixed steps + dt-driven agent/late scripts). After the planned repeats: one extra quick check in GOD mode, file night1\scenarios_n1_quickcheck_god.json (run as a 2nd chain plan night1\chain_plan2.json when chain 1 ends). Tools: night1\step_table.py (--units world), night1\sections_by_step.py, night1\knee_tmp.py.

- 23:35 COORDINATOR: diagnostic run fixedwake_b11 on release\b11 (FixedWakeTrace) wanted ASAP. Created night1\STOP_CHAIN -> chain 1 stops after the current run (ramp_god, ~23:45). Then: run fixedwake_b11 (phys-idle-vehicles-300 only, arm F + IdleSleepDiag + FixedWakeTrace at startup, no flips), write night1\fixedwake_b11\FIXEDWAKE.txt, SendMessage main with it, delete STOP_CHAIN and restart chain.ps1 with chain_plan.json (done runs are skipped because their folders exist). Remaining chain runs stay on b10.

- 23:35 started detached night1\fixedwake_then_chain.ps1 (PID 26808): waits for chain 1 to end (STOP_CHAIN), runs fixedwake_b11, writes FIXEDWAKE.txt, deletes STOP_CHAIN, restarts chain.ps1 (new PID in chain.pid). If it dies: do those steps by hand.
- 23:39 ramp_god DONE (8/8 plateaus, 42 min). Paired def vs off: -15/-17/-17/-21/-29/-31/-32/-24 % at 200..1000; agg: -24/-25/-28/-33/-44/-46/-37/-26 %. Off fps 116/105/88/65/38/21/13/8.4. (night1\step_table.py now prints paired block estimates.)
- 23:44 fixedwake_b11 DONE: FIXEDWAKE.txt written, sent to main. Re-wakes of force-slept vehicles = VehicleWheeled.OrderedFixedUpdate (wheel transforms), ~50 k / 10 s; FixedWakeTrace ON 4/4 and UpdateTiers Tier_* ON alongside, 0 exceptions.
- 23:44 chain restarted (PID in chain.pid) with chain_plan.json; done runs skipped; now ramp_sustain, then qc2, probe_F2, probe_A2, qc3. After that: chain_plan2.json (qcgod1).
- 23:45 started detached night1\then_plan2.ps1 (PID 28764): when chain 1 ends it starts chain.ps1 -Plan chain_plan2.json (qcgod1) and writes the new PID to chain.pid.

- 23:48 COORDINATOR: b12 (ParkedWheelHold) physics slot wanted after ramp_sustain. STOP_CHAIN created again (then_plan2.ps1 will see it and NOT start plan2). Files in night1\b12_cfg (config + runtime A/F/H/HD + idle-300 diag scenario). Plan: detached night1\b12_then_rest.ps1 runs chain.ps1 -Plan chain_plan_b12.json (phys_b12 flips A,H,A,F,H,F 60 s; phys_b12_HD diag idle-300; probe_H_b12), then chain_plan.json (remaining: qc2, probe_F2, probe_A2, qc3 on b10), then chain_plan2.json (qcgod1). Then I write night1\phys_b12\PHYS_B12.md and SendMessage main.
- 23:49 started detached night1\b12_then_rest.ps1 (PID 3816). Chain PIDs follow in chain.pid. To stop everything after the current run: create STOP_CHAIN.
- 00:02 COORDINATOR: (1) night1\ANALYSIS_BATTLE.md exists (knee, where time goes, binning artefact) -> reference it in NIGHT1.md, only check its numbers vs qc2/qc3/ramp_sustain; use matched 25-unit bins / per-arm fits for trickle arm comparisons. (2) qc_fixed20 inserted in chain_plan.json after qc2 (b10, def vs def+Physics.FixedStep_Override 20 Hz, 30 s blocks; files night1\fx20_cfg); verify census fixedDt 0.04<->0.05. Behaviour-changing, info only. b13 may follow.
- 00:21 COORDINATOR: b13 (DeadSettle, DeadDisposeEarly, CollectEnemies_Snapshot/SnapshotScore/FastAdd, Scan_Diag, census dead segment) runs wanted after b12, before qc2. Files night1\b13_cfg (cfg_b13*.json, rt13_*.json arms, melee_*.json plateau scenarios 450/550 from the quick-check geometry, base ratio 0.08). Stopped my waiting helpers b12_then_rest (3816) and then_plan2 (28764); new detached night1\night_driver.ps1 (PID 18896): after ramp_sustain -> chain_plan_b12.json -> chain_plan_b13a.json (b13_snap_check, b13_dead) -> waits up to 30 min for night1\chain_plan_b13b.json (I write it after checking snap_check: b13_snap + b13_best runs, or a PoseOneCall=false rerun) -> chain_plan.json (qc2, qc_fixed20, repeats; done runs skipped) -> chain_plan2.json (qcgod1). After each b13 run: compact table to main.
- 00:22 ramp_sustain DONE (7 plateaus; stopped at fps floor after @800: 8.3 fps). Paired def vs off: -15/-17/-26/-41/-36/-22/-26 % at 200..800; agg -21/-20/-28/-46/-38/-26/-26 %. Off fps 113/98/70/30/13/9.1/7.2. Deaths 6-17/s, with-target 0.56-0.59. 0 exceptions, nothing FAILED. No hang. Chain stopped by STOP_CHAIN; night_driver started chain_plan_b12.json at 00:21 (phys_b12 running).

- 01:08 b12 plan DONE (phys_b12, phys_b12_HD, probe_H_b12): H -31 %/-38 %, asleep 98-100 %, wakes/reSleep 0, probe unchanged. night1\phys_b12\PHYS_B12.md written, table sent to main. NOW: chain_plan_b13a.json running (b13_snap_check started 01:06, then b13_dead). NEXT: check snap_check mismatches -> write chain_plan_b13b.json.

- 01:14 b13_snap_check PASSED: mismatch total 0 (cumulative) on CollectEnemies_Grid, _Snapshot, _SnapshotScore, _FastAdd; 0 exceptions, nothing FAILED. Caveat: only startup/quit status blocks (no flips), so per-item check counts during the plateau are not visible; Score/FastAdd q/s at quit 0 (after cleanup). Plateau 450: 424 lab avg, 61.5 fps, 15.9 ms, deaths 11.9/s. Wrote night1\b13_cfg\rt13_new.json (def + DeadSettle + 3 snapshot items) and night1\chain_plan_b13b.json (b13_snap, b13_best_plat, b13_best_qc) -> night_driver runs it after b13_dead. If b13_dead shows DeadSettle problems: edit rt13_new.json before b13_best_plat starts.

- 01:15 COORDINATOR: snap_check not yet a pass (no check counts during the fight). Added b13_snap_check2 to chain_plan_b13b.json after b13_snap: plateau 450, SelfCheckFraction 0.05, no-op flips (seq 101/102) every 45 s -> status dumps with per-interval check counts. Pass = several thousand checks summed, 0 mismatches, grid and bypass paths both covered. Report with the b13_snap table; if check2 mismatches, mark b13_best 'new' accordingly.

- 01:31 b13_dead DONE: B (DeadSettle) +0.8 % (450, s.e. 0.5 %) / +8.6 % (550, s.e. 0.4 %, 2 blocks) = no gain; C (DeadSettle+DeadDisposeEarly) -19.3 % / -37.2 %. Census: dead units ~113-128 = 19-18 % of world units in A, deadAwake 279/320 of deadDyn 295/334 (95 %). rt13_new.json changed to defaults + 3 snapshot items (DeadSettle dropped). chain.ps1 report-file naming fixed (night1\ scripts wrote nothing; ran b13_dead reports by hand: b13_dead\STEPS.md, SECTIONS.md). NOW: b13_snap running (01:30).

- 01:52 b13_snap DONE (S3 -23 % at 550, S2 -13 %, S1 ~0, L -7 % with +10 % scans), b13_snap_check2 DONE (~800 checks, 0 mismatches, pruned path ~0.7 % of queries -> partial evidence only). Tables in b13_snap\STEPS.md / METHODS.md; sent to main. NOW: chain_plan.json running, b14_dead started 01:51 (~23 min). Then b14_best_plat (check rt14_new before ~02:15), b14_best_qc, b14_valve, qc2, qc_fixed20, repeats, plan2. Tools: night1\methods_by_arm.py, windows_items.py.

- 01:53 COORDINATOR: b14_snap_check3 (SelfCheckFraction 0.5; part 1 melee 450 bypass path, part 2 ramp_god 4-front geometry at 550 god = grid-pruned path; 270 s each; no-op flips every 45 s) inserted AFTER b14_dead, BEFORE b14_best_plat: STOP_CHAIN created (chain_plan.json chain stops after b14_dead, night_driver exits), then detached night1\night_driver2.ps1 (PID 13880) removes STOP_CHAIN and runs chain_plan_check3.json -> chain_plan.json (b14_best_plat, b14_best_qc, b14_valve, qc2, qc_fixed20, repeats; done runs skipped) -> chain_plan2.json. Pass = >= ~3000 checks per part, 0 mismatches; report effective sample rate. DECIDE b14_cfg\rt14_new.json / rt14_max.json only after b14_dead AND check3 (if mismatch: new = defaults + DeadAgentHalt only, tell main at once). Rule: always keep one background waiter armed per run while a driver works.

- 02:14 b14_dead DONE + sent: D (DeadAgentHalt) -4.9 % / -15.7 %, E -10.7 / -18.7, C -24 / -41.5; guard D vs A within ~5 % (shots/unit-s -8 % at 450 open); deadcontact mask contains 'CollideWithStatic' (flagged). NOW: b14_snap_check3 (started 02:13, waiter armed). NEXT: decide rt14_new (currently defaults + 3 snapshot + DeadAgentHalt) before b14_best_plat.

- 02:17 COORDINATOR: b14_halt_guard added after b14_valve, before qc2: b14_halt_guard1 (ClearAim true, A/D 60 s, A,D,D,A,A,D,D,A,A,D, seq variants so each block is its own sig) and b14_halt_guard2 (cfg_b14_noclear.json, ClearAim false, A,D alternating x4), plateau 450, evaluate from 15 s. Si_PerfLab 0.2.2 built + deployed (md5 7D3976AA...; 0.2.1 kept as _backup_perflab_0.1.1\Si_PerfLab_0.2.1.dll.bak): new top-level key engagement_split_by_sig writes an engagement.csv window per runtime_sig segment (rows <scenario>#<sig>), so per-team/kind engagement per A/D block is available (segments include the first 15 s after a flip). E stays out of 'new'.

- 02:24 COORDINATOR: build b15 (md5 BA60082F...; DeadAgentHaltClearAim default false, new deadagents counters clearedAim/clearedAssigned/frozenAim, DeadNoUnitContact allow-list DeadNoContactLayers) replaces b14 after check3. chain_plan.json edited: b15_best_plat, b15_best_qc (cfg_b15.json, ClearAim false), b15_valve (cfg_b15_valve.json), b15_halt_guard1 (ClearAim FALSE, cfg_b15.json), b15_halt_guard2 (ClearAim TRUE, cfg_b15_clear.json), then qc2, qc_fixed20, probe_F2, probe_A2, qc3, then b15_eveh (A/D/E with DeadNoContactVehicles true, check mask = 4 allow-list names). new = defaults + snapshot (if check3 passes) + DeadAgentHalt; max = new + DeadDisposeEarly.

- 02:26 b14_snap_check3 DONE + sent: 3591 (melee) / 4427 (spread god) checks, 0 mismatches; pruned path only 1-3 % of queries; effective sample ~5 % at 0.5 (factor ~10). Snapshot items stay in new. NOW: chain_plan.json chain (started 02:25 by night_driver2) running b15_best_plat; waiter armed per run.

- 02:27 COORDINATOR: b15_snap_check4 appended to chain_plan2.json after qcgod1 (lowest priority; drop if clock runs out): ramp_god 4-front god at 800 lab units, fraction 0.5, dumps every 45 s; goal >= 1000 pruned-path checks, 0 mismatches. NIGHT1.md: note self-check runs at ~1/10 of the configured fraction (0.05 -> 0.5 %, 0.5 -> 5.3 %).

- 02:38 b15_best_plat DONE + sent: vs off paired def -27/-31 %, new -36/-50 %, max -42/-62 % (450/550); fps 550: 15.6/20.7/27.3/40.1. NOW b15_best_qc (02:38).

- 02:55 b15_best_qc DONE + sent: crossings 30/20/10 fps off 447/461/608, def 478/535/683, new 505/550/>690; fits new -41 %@300, -39 %@500. NOW b15_valve (02:55). Tool night1\crossings.py.

- 03:03 b15_valve DONE + sent: valve opens below 30, fps 37.2 avg, but oscillates ~2.2 opens/min (period 30-60 s). NOW b15_halt_guard1 (03:02, ~16 min) then halt_guard2.

- 03:06 COORDINATOR: build b16 (md5 56DAB088...; MultiCore.ParallelScan) runs after halt_guard1/2, before the b10 queue. STOP_CHAIN created (chain stops after b15_halt_guard1; night_driver2 then exits). Detached night1\night_driver3.ps1 (PID 25192): removes STOP_CHAIN, runs chain_plan_b16a.json = [b15_halt_guard2, b16_pscan_check (melee 450, P fixed, SelfCheckFraction 0.2, dumps 45 s)], then WAITS up to 30 min for night1\chain_plan_b16b.json (I copy it from night1\b16_cfg\chain_plan_b16b.TEMPLATE.json if pscan_check passes: b16_pscan 450/550 S3/P 30 s + b16_pscan_inline 550 Jobs 0), then chain_plan.json (qc2, qc_fixed20, probe_F2, probe_A2, qc3, b15_eveh; done skipped) and chain_plan2.json (qcgod1, b15_snap_check4). Files night1\b16_cfg. Arms: S3 = rt16_S3 (defaults + 3 snapshot + DeadAgentHalt), P = + ParallelScan.

- 03:07 COORDINATOR: build b17 (md5 F940BA24...; DeadDisposeEarly proportional: 15 s at >= FpsHigh 45 -> 5 s at <= FpsLow 25, smooth 10 s, slew 0.5 s/s; always mode = Low=High=0; BelowFps gone) supersedes b16. chain_plan_b16a.json now runs b17_pscan_check (b17, cfg_b17_check.json); after it passes copy night1\b16_cfg\chain_plan_b17b.TEMPLATE.json to night1\chain_plan_b16b.json (b17_pscan, b17_pscan_inline) - night_driver3 waits for that file name. chain_plan.json: b17_dispose_prop (550, hold 360, new+DeadDisposeEarly proportional fixed, cfg_b17_prop.json) and b17_dispose_always (550, hold 300, Low=High=0, flips new -> max at ~+300 s from launch) inserted before qc2. All b17 configs set Low/High/Smooth 10 explicitly; dispose configs without MultiCore (ParallelScan not patched).

- 03:25 Si_PerfLab 0.2.3 deployed (0.2.2 kept as .bak): runtime_sig is now hashed on every 1 Hz poll. 0.2.0-0.2.2 skipped the read when runtime.json's LastWriteTime was unchanged; run_lab's Copy-Item keeps the source file's mtime, so flips between files with equal mtimes were missed -> in b15_halt_guard1 only 6 of ~11 engagement segments were split (A1/D1 transitions missed). runtime_sig in samples/rows of earlier flip runs can lag for the same reason (flips.csv-based reports are unaffected). halt_guard per-block numbers therefore from 1 Hz samples + flips.csv; per-team/kind only for the detected segments.

- 03:29 b15_halt_guard1/2 DONE + sent: ClearAim false D vs A all |t|<1, work -10 %; ClearAim true shots -7 % (t -1.6). NOW b17_pscan_check (03:28, ~9 min). After it: check -> copy b16_cfg\chain_plan_b17b.TEMPLATE.json to night1\chain_plan_b16b.json if pass.

- 03:36 b17_pscan_check DONE but INCOMPLETE: my config set only Targeting.SelfCheckFraction; ParallelScan uses MultiCore.Settings.SelfCheckFraction, so no ParallelScan self-check ran (Targeting mismatch 0, no FAILED, 0 exceptions, served 223-308/s of ~330 captured). Wrote night1\chain_plan_b16b.json = [b17_pscan_check2 (cfg_b17_check2.json, MultiCore fraction 0.2), b17_pscan, b17_pscan_inline]. IF check2 shows 'different' > 0: create folders night1\b17_pscan and night1\b17_pscan_inline (SKIPPED.txt) before the chain reaches them and send main the difference lines.

- 03:44 b17_pscan_check2 PASSED + sent (19 571 checks identical, 0 different, tv 0). b17_pscan running since 03:43 (~13 min), then b17_pscan_inline, then chain_plan.json (b17_dispose_prop, b17_dispose_always, qc2, qc_fixed20, probe_F2, probe_A2, qc3, b15_eveh), chain_plan2.json (qcgod1, b15_snap_check4).

- 03:54 b17_pscan DONE + sent: P vs S3 -2.2 % @450, -16.3 % @550 (s.e. 4.8); served 72-79 %; net ~30-40 ms/s. NOW b17_pscan_inline (03:53).

- 04:00 b17_pscan_inline DONE + sent: -2.7 % (s.e. 4.5) = gain comes from worker threads. NOW chain_plan.json (night_driver3): b17_dispose_prop (03:59), then b17_dispose_always, qc2, qc_fixed20, probe_F2, probe_A2, qc3, b15_eveh; then chain_plan2.json qcgod1, b15_snap_check4.

- 04:29 dispose_prop/always analysed + sent. NIGHT1.md now has sections 1-10 (6 correctness, 7 recommendations, 9 not measured, 10 PENDING list); remaining: add qc2, deepprobe, fixed20, qcgod1, eveh, check4 (+ b18) results to sections 2/5/7/10.

- 05:23 RIG 1 DONE: qcgod1 finished 05:21 and reported; the chain now belongs to rig 2 (night_driver5 started chain_plan_b18a.json at 05:22, b18_check running, lab Silica PID 14496 is rig 2's). Rig 1 started nothing after qcgod1.
