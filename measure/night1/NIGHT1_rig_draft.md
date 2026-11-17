# Si_ServerPerf night 1 (2026-11-16/17): quick checks, physics slot, order probe, ramps (build b10)

Written during the night (23:10-04:45, updated as runs finished); sections marked PENDING belong to runs that were still queued. Builds: b10 (sections 1-4), b11-b17 (section 5). Companion analyses: `night1\ANALYSIS_BATTLE.md` (knee, where the time goes, by a separate analyst) and `night1\ANALYSIS_BATTLE_2.md` (what is left in arm P at ~521 units, deep-probe list).

Lab server `C:\tmp\silica_ds_lab`, Silica 1.0.17, headless, 0 clients, 120 fps cap, base mod set as in `..\PROTOCOL.md`
(Si_ServerOptimizer 0.7.1 loaded in every arm), Si_PerfLab **0.2.1** (0.2.0 + two shakedown fixes, section 1; 0.2.2 from 02:16 adds `engagement_split_by_sig`, 0.2.3 from 03:27 hashes runtime.json on every poll),
Si_ServerPerf **`release\b10`** in every run. All arms run inside one server process (runtime.json flips), so every
comparison below is within-process. Run folders: `night1\<label>\`; per-run reports `report_<n>_*.txt`, `RAMP.md`.

Arms:
* **off** = `runtime_all_off.json` (`"*": false`, only `Headless.Diag_LogExceptions` on to count exceptions; the
  startup-only `Headless.Particles_Disable` stays on in all arms)
* **def** = `runtime_bat_B.json` (shipped defaults)
* **agg** = `runtime_bat_C.json` (defaults + `MultiCore.SensorPipeline_Rate` + `Targeting.LosTopK` + `Targeting.FriendlyFire_Cache`)
* Physics: **A** = defaults, **C** = A + `Physics.IdleForceSleep`, **F** = C + `AvoidanceSkipOntoResting` +
  `AvoidanceNoIdleDetour`, **FD** = F + `IdleSleepDiag` (diagnostic block only). All diagnostics off in A/C/F.

Metrics: **work** = Si_PerfLab frame CPU work without the cap wait, ms per frame (frame-weighted). **main ms/s** =
work x fps (main-thread work per wall-second; ~1000 = saturated). fps = frames per second.

## 1. Shakedown of Si_PerfLab 0.2.0 (20:39-21:21)

| check | result |
|---|---|
| units spawn where intended | yes. Trickle front `center` = HQ centroid (-325,133) with staging 250 m back per team (SCENARIOS_V2 expected other coordinates; it computes them from the HQs, and HQ positions differ between rounds). Fixed 4-front list: staging 90 m behind each front as designed. 0 spawn failures. |
| bases placed | yes. Game placement 13-34 per run, lab ring fallback 2-14, 0 failed. |
| sides engage (incl. creatures) | yes in sustain: e.g. trickle Alien 4022 creature attacks / 221 kills in 100 s; qc1 per team 0.1-2.9 shots/unit-s, creatures 24 618 attacks. In god mode hits are 0 by construction. |
| sustain holds the count | within 5-6 % on average (plateau avg 283/300, 378/400); dev_max 10-13 % at 3-11 deaths/s (above the 5 % the doc hoped for; same in every arm) |
| ramp reaches targets | yes (CLIMB DONE lab_units = target, failed 0) |
| order probe | sane: 0 order failures, first move 0.2-0.6 s (infantry/creatures, 70 units), reach 2 m 0.6-1.4 s |
| fail fast | yes: wrong value type -> `FATAL` 1 s after load, `FAILED` file, server gone after 9 s |

Defects found and fixed in Si_PerfLab (now 0.2.1, `C:\Users\schwe\Projects\Silica_Performance\Si_PerfLab`):
1. **God-mode front stall loop** (`Ramp.cs` StallCheck, `Engagement.cs`): the stall check only counted hits, and god
   mode deals none, so every front was "stalled" every 30 s and all front units got re-ordered (`FRONT STALL` x4 every
   `stall_s`). Fix: in god-mode ramps shots and creature attacks of front units count as activity. Verified: 0 stalls.
2. `Parking.cs`: the "no lot" warning now reports the flattest lot seen (diagnostic only).

Scenario problems (worked around with night1 variants, originals untouched):
3. **`terrain: "flat"` cannot be satisfied on GreatErg**: no lot with steepest sample < 4 deg (or < 7 deg) within
   900 m of the Sol/Centauri HQ; the flattest 139 x 142 m lot is 12.1 deg max / 7.7 deg mean. The group is then
   silently not spawned (warning only, the scenario runs with 0 vehicles). Variants: `night1\scenarios_n1_parked.json`
   (`parked-spaced-any` = terrain any (< 30 deg), `parked-spaced-slope` as 6 lots x 50; WORSTCASE dropped because it is
   identical to `phys-idle-vehicles` in the physics slot) and `night1\scenarios_n1_order_probe.json` (terrain any).
   Suggestion for Si_PerfLab: a parking group that finds no lot should fail the scenario (or FATAL), not run empty.
4. Quick check base: `structure_ratio` 0.04 applies to `max_units`, so the base is 39 structures (13/team) from the
   start, not "0.04 per unit" of the current count.

**One unexplained server hang** (shake1, 20:45:30-20:46:54, b10 all defaults, sustain ramp, 4 fronts, plateau 300,
lategame base with RadarStations): `FogOfWar.LateUpdate` took 48 s over 42 frames (two `Job.Worker` threads at 100 %,
main thread idle inside `JobHandle.Complete` of vanilla `ExploreAreaJob`), fps 0.9 until the scenario cleanup removed
the objects; then normal. Not reproduced: the same scenario extended to 400 units ran clean with all items OFF
(`shake3_off`) and with defaults (`shake4_def`), and radar bases in god mode (`shake2`) were clean. `ExploreAreaJob`
cost grows ~r^3 for an object whose view radius covers many unqueried pixels (height-obstructed pixels are re-marched
every pass), and at low fps more objects are processed per frame, so it can feed itself. Si_ServerPerf does not patch
`FogOfWar.LateUpdate` or the job; its `Headless.Fog_DetectedTargetLookup` transpiler sits in `FogOfWar.OnUpdate` (where
the job inputs are built) and is in the defaults arm, so it is not excluded. Possibly related: the live-server hang of
2026-11-13 (one core spinning, preceded by a burst on all cores). Evidence: `night1\shake1\perfprobe\*\methods.csv`
(FogOfWar.LateUpdate 48 223 ms self in the 20:46:54 interval), `threads.csv` (Job.Worker 1 and 3 at 48 078 ms CPU).

## 2. Quick check `scenarios_quickcheck.json` (sustain trickle, one 3-way centre front)

### qc1 (21:21-21:36, arm order off -> def -> agg, 30 s blocks)

The climb stopped at the fps floor (20 s median 9.5 fps) at **~625 lab units**, not 1000. One dense 3-way front with
deaths is much heavier than the wave-1 four-cluster god-mode battles (39 fps "off" at 617 world units there).

`python night1\step_table.py night1\qc1` (50-lab-unit bins; a trickle visits each bin with all three arms within
~1-2 min, but each arm sees slightly different counts inside a bin):

| lab units | fps off / def / agg | work ms off / def / agg | def vs off | agg vs off |
|---|---|---|---:|---:|
| 100-149 | 118 / 120 / 120 | 4.61 / 3.29 / 3.27 | -29 % | -29 % |
| 150-199 | 118 / 117 / 116 | 4.87 / 4.72 / 4.66 | -3 % | -4 % |
| 200-249 | 106 / 106 / 113 | 6.93 / 6.84 / 5.22 | -1 % | -25 % |
| 250-299 | 91 / 95 / 106 | 9.82 / 8.69 / 6.69 | -11 % | -32 % |
| 300-349 | 71 / 92 / 92 | 13.80 / 9.36 / 9.01 | -32 % | -35 % |
| 350-399 | 59 / 72 / 72 | 16.71 / 13.34 / 12.67 | -20 % | -24 % |
| 400-449 | 41 / 44 / 45 | 25.00 / 23.03 / 22.56 | -8 % | -10 % |
| 450-499 | 18 / 23 / 35 | 57.93 / 43.93 / 29.05 | -24 % | -50 % |
| 500-549 | 11 / 18 / 20 | 98.07 / 57.76 / 51.61 | -41 % | -47 % |
| 550-599 | 9 / 13 / 12 | 112.72 / 81.45 / 86.20 | -28 % | -24 % |

Exceptions: 0 in every arm (30 status windows). No item FAILED / NOT APPLIED.

### qc2 / qc3 and spread: PENDING

## 3. Physics slot (b10)

### 3a. `phys_b10` (21:36-22:06, `scenarios_physics_b10.json`, cycle A,C,A,F x3 + FD, 60 s blocks, 30 s settle)

`ab_report.py flip --settle 30` (paired: each block minus its neighbouring A blocks) and `physics_report.py`:

| scenario | arm | work ms | vs A (paired, s.e.) | wheeled asleep | PhysX ms/s | CollisionAvoidance ms/s |
|---|---|---:|---:|---:|---:|---:|
| idle LightStriker 300, tight grid | A | 3.84 | | 3 % | 156 | 36.5 |
| | C | 3.47 | -0.33 ms (-8.6 %, 0.14) | 40 % | 130 | 29.1 |
| | F | 3.21 | **-0.72 ms (-18.7 %, 0.06)** | 71 % | 120 | 13.4 |
| parked mixed 300 (4 prefab grids) | A | 4.00 | | 1 % | 167 | 34.5 |
| | C | 3.87 | -0.01 (-0.3 %, 0.06) | 22 % | 157 | 29.1 |
| | F | 3.29 | **-0.62 ms (-15.6 %, 0.08)** | 67 % | 127 | 12.9 |
| move-vehicles 300 (guard) | A / C / F / FD | 5.25 / 5.22 / 5.23 / 5.42 | -0.5 % (C), FD +2.4 % (1 block) | 0-1 % | 224-230 | 38-40 |

* Arm A with the diagnostics off is at the no-mod level again (3.84 ms vs 3.78 baseline; b9's A was 4.53 with
  WheeledSleepDiag on), so b9's extra ~0.7 ms was the diagnostics.
* F's gain comes from AvoidanceSkipOntoResting: avoidresting skipped 333 680 of 333 838 calls (idle) and 281 240 of
  281 730 (mixed); CollisionAvoidance fixed 36.5 -> 13.4 ms/s. b10's idle test fix works: notIdle "input" causes are
  gone (b9: 10 627 input), remaining notIdle = speed only.
* Move guard: patrolling vehicles stay awake in every arm (notIdle moveOrder ~14 300 per window, sleep 0-1 %);
  avoidSkip while moving 0.5 % (636 of 130 128); all orders ok.
* **Creep of force-slept vehicles** (cumulative since their first forced sleep): C mean 0.38-0.57 m, max 2.9-3.1 m,
  |dy| max 0.5 m (they are woken by avoidance pushes and re-slept); **F 0 m**. Arm A drift is not measured in b10
  (the `parkeddrift` line belonged to WheeledSleepDiag, off now); b9 measured ~1 m per 10 s for awake vanilla vehicles.
* FD diagnostic block (IdleSleepDiag): re-wakes of force-slept vehicles = "scripts" 56 051 per window (velocity writes
  in fixed scripts, all bodies), "force" 250, avoidance 0; slope 34 000. `idledetour` (AvoidanceNoIdleDetour):
  ~76 500 passes, 69-15 031 resets per window.

### 3b. `parked_v2` (22:07-22:27, `night1\scenarios_n1_parked.json`, spaced layouts, cycle A,F,A,C, 60 s blocks)

| scenario (terrain actually found) | arm | work ms | vs A (paired, s.e.) | wheeled asleep | PhysX ms/s |
|---|---|---:|---:|---:|---:|
| spaced-any, 4 x 75, mean 14-21 deg | A | 2.91 | | 9 % | 107 |
| | C | 2.89 | -0.7 % (0.4 %) | 57 % | 100 |
| | F | 2.81 | -3.3 % (s.e. 11 %: nothing) | 67 % | 94 |
| spaced-slope, 6 x 50, mean 8-14 deg | A | 3.46 | | 3 % | 149 |
| | C | 3.41 | -2.4 % (1.8 %) | 44 % | 138 |
| | F | 2.98 | **-13.9 % (3.0 %)** | 73 % | 106 |

Realistically spaced vehicles cost less than the tight grid (2.9-3.5 vs 3.8-4.0 ms for 300) and IdleForceSleep alone
(C) buys nothing there; F still saves 14 % on the moderate slope.

### 3c. Order-response probe (22:27-22:57, `night1\scenarios_n1_order_probe.json`, one run per arm, no flips)

100 Sol + 60 Cent parked vehicles (spaced, terrain any), 50 + 50 infantry, 70 creatures; 4 move orders 30 m out and
back. 0 order failures and 0 "never started" in every arm (one Sol rifleman never reached 2 m once in F).
Wheeled asleep just before the orders (cycles 0-3): A 95 / 62 / 22 / 18 %, C 86 / 63 / 45 / 45 %, F 90 / 83 / 72 / 74 %.

Vehicles, p50 over the 4 cycles (range of the cycle p50s), seconds:

| kind | first move A | first move C | first move F | reach 2 m A | reach 2 m C | reach 2 m F |
|---|---|---|---|---|---|---|
| Sol light (59-60) | 0.68-0.84 | 0.26-0.60 | **0.10-0.48** | 1.44-1.56 | 1.40-1.72 | 1.31-1.60 |
| Sol heavy (38-40) | 0.71-0.84 | 0.51-0.75 | **0.11-0.48** | 1.44-1.60 | 1.44-1.62 | 1.44-1.60 |
| Cent light (40) | 1.15-1.24 | 1.03-1.20 | **0.11-1.08** | 1.91-2.08 | 1.92-2.15 | 1.84-2.04 |
| Cent heavy (20) | 1.32-1.56 | 1.28-1.60 | 1.24-1.37 | 2.48-2.96 | 2.42-3.16 | 2.36-2.84 |

Reaching 2 m takes the same time in every arm (p95 and max likewise within the cycle-to-cycle spread), so **sleeping
and skipped vehicles do not respond later**. The earlier "first move" in F/C is a jolt (> 0.5 m/s for 0.1 s) when the
item wakes a slept vehicle for the order, not driving; it does not shorten the time to 2 m. Infantry and creatures:
identical across arms (they are not affected by the items).

## 4. Step ramps (4 fronts, 20 % base / 15 % transit / 65 % front, lategame base 0.12 structures per unit)

Arms flipped every 30 s, 3 cycles per 270 s plateau; `python night1\step_table.py <run> --arms off,def,agg` (paired =
each def/agg block minus its neighbouring off blocks in the same plateau; 3-4 blocks per arm and plateau).

### 4a. `ramp_god` (22:57-23:39, `scenarios_v2_ramp_god.json`, damage OFF, arm order def -> agg -> off)

All 8 plateaus ran (count within 3 % of target, 0 deaths, 0 front stalls).

| lab units | fps off / def / agg | work ms off / def / agg | def vs off (paired, s.e.) | agg vs off (paired, s.e.) |
|---:|---|---|---:|---:|
| 193 | 116 / 119 / 120 | 4.98 / 4.23 / 3.75 | -15.1 % (0.6 %) | -24.4 % (0.4 %) |
| 292 | 105 / 109 / 111 | 7.14 / 5.93 / 5.32 | -17.2 % (0.6 %) | -25.3 % (0.4 %) |
| 389 | 88 / 95 / 99 | 10.33 / 8.53 / 7.44 | -17.4 % (1.0 %) | -28.0 % (0.8 %) |
| 485 | 65 / 77 / 83 | 15.55 / 12.16 / 10.43 | -21.4 % (0.6 %) | -32.5 % (0.4 %) |
| 582 | **38 / 54 / 65** | 26.86 / 18.87 / 15.15 | -29.1 % (1.0 %) | -43.8 % (1.2 %) |
| 681 | 21 / 30 / 37 | 50.34 / 34.63 / 27.22 | -31.4 % (3.8 %) | -46.2 % (2.2 %) |
| 779 | 13 / 19 / 20 | 82.37 / 55.68 / 51.93 | -31.7 % (1.2 %) | -37.4 % (0.6 %) |
| 971 | 8.4 / 10.8 / 11.0 | 127.8 / 96.7 / 94.8 | -23.6 % (0.8 %) | -25.7 % (0.1 %) |

60 fps holds to ~500 lab units in off, ~560 in def, ~600 in agg; 30 fps: off ~620, def ~700, agg ~720. This
reproduces the wave-1 b9 god-mode battles (617 world units: 39 / 60 / 63 fps) closely.

### 4b. `ramp_sustain` (23:44-00:21, `scenarios_v2_ramp_sustain.json`, damage ON + respawn, arm order agg -> off -> def)

Stopped after @800 at the fps floor (8.3 fps < 10), so @1000 did not run. Count 5-8 % under target (deaths 6-17/s,
dev max 7-16 %), the same in every arm.

| lab units (target) | fps off / def / agg | work ms off / def / agg | def vs off (paired, s.e.) | agg vs off (paired, s.e.) | deaths/s |
|---:|---|---|---:|---:|---:|
| 186 (200) | 113 / 116 / 117 | 5.30 / 4.51 / 4.19 | -15.0 % (1.1 %) | -21.1 % (1.5 %) | 6.5 |
| 280 (300) | 98 / 104 / 104 | 8.16 / 6.81 / 6.54 | -16.7 % (1.6 %) | -20.0 % (0.6 %) | 9.2 |
| 374 (400) | 70 / 85 / 86 | 13.97 / 10.29 / 9.87 | -25.9 % (1.6 %) | -27.5 % (1.4 %) | 12.0 |
| 467 (500) | **30 / 50 / 53** | 33.87 / 20.05 / 18.42 | -41.4 % (1.4 %) | -45.6 % (2.0 %) | 15.2 |
| 563 (600) | 13 / 20 / 21 | 79.48 / 50.84 / 49.39 | -36.2 % (1.4 %) | -37.7 % (2.1 %) | 16.7 |
| 665 (700) | 9.1 / 11.6 / 12.1 | 115.8 / 90.5 / 85.9 | -21.5 % (1.0 %) | -25.8 % (1.3 %) | 15.3 |
| 767 (800) | 7.2 / 9.5 / 9.6 | 146.9 / 110.7 / 109.9 | -25.6 % (0.3 %) | -25.9 % (1.3 %) | 14.5 |

With damage on the same layout is about one plateau (100 units) heavier than in god mode: off reaches 30 fps at ~470
instead of ~620 lab units. The relative gain of the defaults is the same or larger (-41 % at 467). agg adds only
1-5 points on top of def here (vs 8-15 points in god mode): with deaths the LOS / friendly-fire work that LosTopK and
FriendlyFire_Cache save is a smaller share.

Exceptions: 0 in every arm of both ramps (status windows: ramp_god 85, ramp_sustain 65); nothing FAILED. The shake1
fog hang did not recur in 85 minutes of 4-front ramps.

Caveat for the lowest fps: below 12.5 fps the defaults' `Physics.CatchUpCap` (maximumDeltaTime 0.08 s, max 2 fixed
steps per frame) slows the game clock (at 10 fps the simulation runs at 0.8 x real time); off has the vanilla 0.1 s /
3 steps and slows below 10 fps. At @1000 (god) both arms are clock-limited (off 0.78 x, def 0.83 x), so the -24 % there
is partly less simulated time per second in both arms; the plateaus at >= 12.5 fps are unaffected.

## 4c. How the scenarios compare with real rounds (OFF arm = everything off)

fps of the off arm per unit count. "Live units" = lab units + ~45 non-lab units (HQs' starter units etc.); the AI-round
and b9 counts are PerfProbe world units of live units. Dead bodies awaiting disposal are not counted as live (in the
sustain scenarios they add another 20-25 %, see b13/b14 census).

| live units | (a) AI rounds 1.0.15, DEV_REPORT §1 (median fps) | (b) WAVE1 b9 god battles, 4 clusters | (c) quick check qc1, one 45 m melee, damage + respawn | (d) ramp_god, 4 fronts, god | (e) ramp_sustain, 4 fronts, damage + respawn |
|---:|---:|---:|---:|---:|---:|
| ~240 | (250: 109) | | 118 (lab 200-249 -> live ~270) | 116 (238) | 113 (231) |
| ~330 | 97 (300) | 108 (329) | 91-106 (live 295-345) | 105 (337) | 98 (325) |
| ~430 | 73 (400) | | 71 / 59 (live 345-445) | 88 (434) | 70 (419) |
| ~480-530 | 65 (500) | 78 (477) | 41 (live ~470) / 18 (~520) | 65 (530) | 30 (512) |
| ~600-630 | 31 (600) | 39 (617) | 11 (~570) / 9 (~620) | 38 (627) | 13 (608) |
| ~700-730 | 21 (700) | | - (floor at ~670) | 21 (726) | 9.1 (710) |
| ~800-830 | | 15 (800) | | 13 (824) | 7.2 (812) |

Engagement per scenario (lab units only; `night1\engagement_summary.py`, all arms together):

| scenario | with-target share | shots + creature attacks per unit-s | deaths/s |
|---|---:|---:|---:|
| (a) AI rounds | not measured (most units idle in bases or moving; fights are local) | - | - |
| (b) b9 god battles | not measured (0.1.1 had no counters); 530-1450 projectiles in flight | - | 0 |
| (c) quick check (whole climb) | 0.70 | 1.10 | 8.9 |
| (d) ramp_god @200 ... @1000 | 0.81-0.87 | 2.3 -> 1.4 | 0 |
| (e) ramp_sustain @200 ... @800 | 0.56-0.59 | 1.4 -> 0.55 | 6.5 -> 16.7 |
| melee plateaus (b13/b14, 450/550) | 0.77 | 0.8-1.0 | 9.5-15 |

Reading:
* The **god-mode 4-front ramp tracks the 1.0.15 AI rounds closely** (530: 65 vs 65 fps; 627: 38 vs 31; 726: 21 vs 21) and
  the wave-1 battles (617: 38 vs 39). The AI rounds' load is roughly what ~85 % engaged units in four spread fights cost.
* **Damage + respawn costs about one step (100 units)**: ramp_sustain at 512 live units runs like ramp_god at ~620.
  Deaths at 10-17/s mean 150-200 bodies awaiting disposal (15 s each), whose agents keep running (b14 DeadAgentHalt) and
  whose bodies stay in PhysX and avoidance (b13 census: ~95 % of dead bodies awake).
* **The quick check's single 45 m melee is the heaviest shape**: at ~470 live units it already runs at 41 fps where the
  spread ramps run at 65-88. All units are within each other's sensor range, so target scans grow ~W^2.3
  (ANALYSIS_BATTLE.md). Its fps-10 floor at ~625 lab units (~670 live) is therefore not comparable to an AI round's 600
  = 31 fps; in a round with the same count the units are spread over the map.
* So load depends on density and engagement (and on deaths), not on the count alone. For a defaults decision, the
  4-front ramps are the closest to real rounds; the melee is a worst case.

**The quick-check knee** (off arm 25 -> 58 ms work between ~425 and ~475 lab units): see `night1\ANALYSIS_BATTLE.md`
section 2. It is not a physics catch-up spiral (fixed steps stay at 25/s, at most 2 per frame), not a jump in death
churn (deaths and dead bodies flat across the knee), but the saturation curve of a mostly time-driven workload: once
the main thread is full, fps = (1000 - T') / F, where T' is work per simulated second (25 Hz fixed steps, ~1500
behaviour-tree ticks/s, timers, projectiles). As T' approaches 1000 ms/s, a few percent more units cost a large
fraction of the frame rate. My own per-interval check of qc1 agrees: from 607 to 683 world units PhysX per fixed step
rose only 11.4 -> 12.2 ms, while fixed steps per frame went 0.59 -> 1.43 because the frame got longer.

## 5. New builds measured during the night (b11-b17)

The coordinator merged new items during the night; each was measured on the lab with the same method (paired flips in
one process). Arm definitions and per-run tables were sent to the coordinator after each run; the full tables are in the
run folders. Melee plateaus = the quick-check geometry (one 3-way centre front, damage + respawn) held at fixed lab
counts (`night1\b13_cfg\melee_*.json`, `night1\b14_cfg\`, `night1\b16_cfg\`).

### 5a. Physics: parked vehicles (b11 FixedWakeTrace, b12 ParkedWheelHold)

* `fixedwake_b11` (diagnostic): the re-wakes of force-slept vehicles are 99.99 % `VehicleWheeled.OrderedFixedUpdate`
  writing the four steering wheels (~50 k per 10 s for 300 vehicles). `night1\fixedwake_b11\FIXEDWAKE.txt`.
* `phys_b12` (A defaults / F IdleForceSleep + AvoidanceSkipOntoResting + AvoidanceNoIdleDetour / H = F + ParkedWheelHold):
  H -31.3 % (s.e. 0.4 %) on 300 idle LightStrikers, -37.9 % (1 pair) on the parked mixed set; wheeled asleep 98 % / 83 %;
  reSleep 0; move guard unchanged (6869 orders ok). HD diagnostic: fixedwake wakes 0, 100 % of wheeled bodies asleep,
  creep max 4.4 cm. Order probe with H: reach-2 m identical to A. Details: `night1\phys_b12\PHYS_B12.md`.

### 5b. Dead units (b13 DeadSettle / DeadDisposeEarly, b14/b15 DeadAgentHalt, DeadNoUnitContact, b15 valve)

Census (b13/b14 arm A): 18-19 % of world units in the melee are dead bodies awaiting disposal, ~95 % of their bodies
awake.

| run | arm | 450: work vs A (paired) | 550: work vs A (paired) | note |
|---|---|---:|---:|---|
| b13_dead | B DeadSettle | +0.8 % | **+8.6 %** (2 blocks) | only ~12 dead units settled at a time (rewoken / time limit) |
| b13_dead | C DeadSettle + DeadDisposeEarly | -19.3 % | -37.2 % | visible (wrecks vanish after 5 s) |
| b14_dead | D DeadAgentHalt (ClearAim true) | -4.9 % (s.e. 3 %) | -15.7 % (s.e. 6 %) | agents: BTEnemyTargetDetected calls -27 % |
| b14_dead | E D + DeadSettle + DeadNoUnitContact | -10.7 % | -18.7 % | visible (creatures not shoved) |
| b14_dead | C DeadDisposeEarly always | -24.1 % | -41.5 % | visible |
| b15_halt_guard1 | D, ClearAim false | -10.3 % (s.e. 2.6 %, 7 blocks) | - | behaviour: all metrics |t| < 1 |
| b15_halt_guard2 | D, ClearAim true | -11.8 % (s.e. 2.1 %) | - | shots/unit-s -7 % (t -1.6), hits -5 % (t -1.7) |

`b15_valve` (550, early disposal only below 30 fps, open/closed with 5 fps hysteresis): fps 37.2 (between "new" 27.3 and
always-on 40.1) but a limit cycle of ~2.2 opens per minute (period 30-60 s, world units swinging 620-740). b17 replaced it
with a proportional dispose time: section 5e.

### 5c. Targeting (b13 CollectEnemies_Snapshot / SnapshotScore / FastAdd, LosTopK question)

`b13_snap` (melee plateaus; paired vs defaults): S1 Snapshot alone -8 % / +3 % (noise); S2 + SnapshotScore -11 % / -13 %;
**S3 + FastAdd -7 % / -23 %** (450 / 550); L LosTopK -6 % / -7 % but it raises FindBestTarget null results (75 -> 80 %) and
CollectDetectedEnemies calls (+10-19 %), i.e. extra full rescans.

Correctness (self-check against vanilla; the snapshot check covers stage 2 and FastAdd too):
* b13_snap_check2 (450, fraction 0.05): ~800 checks, 0 mismatches.
* b14_snap_check3 (fraction 0.5): melee 450 3 591 checks, spread 4-front god 550 4 427 checks, **0 mismatches**. The
  grid-pruned path is only 1-3 % of queries in both layouts (~100-200 checks); a pruned-path-heavy check (800 units,
  `b15_snap_check4`) is queued last: PENDING.
* **The configured `Targeting.SelfCheckFraction` runs at about one tenth of its nominal rate** (0.05 -> 0.5 %, 0.5 ->
  5.3 % of calls). MultiCore's ParallelScan self-check runs at its nominal rate (0.2 -> ~20 %). Reported to the author.
* Display defect: SnapshotScore and FastAdd print `q/s 0` while their `grid` field shows ~100-260 k (Calls counter not
  incremented on those paths).

### 5d. Headline: off / defaults / "new" (b15_best_plat, b15_best_qc)

"new" = shipped defaults + CollectEnemies_Snapshot + SnapshotScore + FastAdd + DeadAgentHalt (ClearAim false); "max" =
new + DeadDisposeEarly always on (5 s, visible).

| melee plateau | fps off / def / new / max | work vs off (paired): def / new / max |
|---|---|---|
| 450 (424 lab) | 44.9 / 59.9 / 66.0 / 75.4 | -26.7 % / **-35.6 %** / -41.8 % |
| 550 (521 lab) | 15.6 / 20.7 / **27.3** / 40.1 | -30.6 % / **-50.1 %** / -62.2 % |

Quick-check trickle (b15_best_qc, 30 s blocks new/off/def; lab units where each arm first falls below an fps level,
`night1\crossings.py`):

| arm | < 30 fps | < 20 fps | < 10 fps |
|---|---:|---:|---:|
| off | 447 | 461 | 608 |
| def | 478 | 535 | 683 |
| new | 505 | 550 | not reached (12 fps at 675-699, climb ended at the off-arm floor) |

Per-arm fits (89-696 units): work at 300 / 500 lab units off 10.2 / 57.5 ms, def 7.2 / 43.4, new 6.0 / 34.9 ms
(-41 % / -39 % vs off).

### 5e. MultiCore.ParallelScan (b16/b17) and proportional disposal (b17): see 5f/5g below (PENDING parts marked)

* `b17_pscan_check2` (450, fraction 0.2): 19 571 ParallelScan self-checks, 0 different, type-value 0; Targeting 0
  mismatches. (`b17_pscan_check` without the MultiCore fraction did not self-check - my config error.)
* `b17_pscan` (S3 = new vs P = new + ParallelScan, jobs 6): -2.2 % at 450 (s.e. 2 %), **-16.3 % at 550** (s.e. 4.8 %,
  fps 26.9 -> 31.9); served 72-79 % of scan calls; main-thread staging + serve ~10 ms/s, kernel 29-36 ms/s on workers;
  net saving ~30-40 ms/s at 550; behaviour unchanged within block noise.
* `b17_pscan_inline` (jobs 0): -2.7 % (s.e. 4.5 %): the gain is the worker threads, not the data layout.
* `b17_dispose_prop` (550, new + DeadDisposeEarly with the proportional dispose time 15 s at >= 45 fps -> 5 s at <= 25
  fps, smoothing 10 s, slew 0.5 s/s): it settles (no limit cycle as with the b15 valve). Last 3 min: target 7.9-10.3 s
  (mean ~9.2, ±1.2 s), world units 652-721 (mean ~672, one sample +7 %), smoothed fps 30.9-35.7; plateau 38.0 fps /
  26.3 ms (separate start; b15: new 27.3, max 40.1, valve 37.2).
* `b17_dispose_always` (Low = High = 0, switched on ~165 s into the plateau): the target slews 15 -> 5 s in ~20 s, and
  ~95 bodies (-13 % world units) leave within one 10 s step, then steady. That start ran much heavier before the switch
  (new: 31 -> 14 fps while world units stayed ~720).

**Run-to-run variation between separate server starts** is large in the melee: the same arm "new" at the 550 plateau
measured 27.3 (b15_best_plat), 26.9-29.5 (b17_pscan S3 / inline S3) and 14-31 fps (b17_dispose_always), and P at 450
55.5 vs 73.8 fps in the two pscan checks. Only paired in-process comparisons (flips) are reliable to a few percent;
absolute fps across starts are good to about ±15-25 % in this scenario.

## 6. Correctness

* **Exceptions:** 0 in every arm of every run tonight (Headless.Diag_LogExceptions per status window, and the `[Diag]`
  count at quit), including all off arms. Nothing FAILED or NOT APPLIED in any run (b10-b17).
* **Self-checks, all 0 mismatches:** Targeting snapshot items (b13 ~800, b14 ~8 000 checks), ParallelScan (b17, 19 571
  checks, type-value state 0), UpdateTiers.Register_O1 verified in every run (e.g. 1 508 in b13_snap_check), Targeting
  grid items (check counters 0 because the snapshot path served the calls).
* **Behaviour guards:** DeadAgentHalt ClearAim false: deaths / kills / shots / hits / with-target within ±2 % (|t| < 1,
  11 blocks); ClearAim true: shots -7 % (t -1.6). ParallelScan: within block noise (shots ±9 % with opposite signs at
  450/550). Order probe (b10 physics arms, b12 H): reach-2 m unchanged, 0 failures.
* **Defects found** (none in Si_ServerPerf game logic): (1) Si_PerfLab 0.2.0 god-mode stall loop (fixed 0.2.1);
  (2) Si_PerfLab flat parking silently spawns nothing (worked around); (3) Si_PerfLab runtime_sig missed flips between
  files with equal mtimes (fixed 0.2.3); (4) Si_ServerPerf display: SnapshotScore / FastAdd `q/s 0`; (5) Targeting
  SelfCheckFraction effective rate ~1/10 of nominal; (6) b14 deadcontact mask contained CollideWithStatic (harmless,
  allow-list in b15); (7) one unreproduced vanilla FogOfWar.LateUpdate hang (section 1); (8) my own config errors:
  b17_pscan_check without the MultiCore fraction, chain report names with a path (fixed).

## 7. Per-item recommendation for defaults (evidence in brackets)

Keep the shipped b10 defaults (Headless, UpdateTiers without Tier_IdleAgents, Targeting exact items, Services, Physics
census + CatchUpCap): paired vs everything off they save 15-17 % up to 300 units and 26-41 % at 470-600 units in the
4-front ramps (sections 4a/4b), 27-31 % on the melee plateaus (5d), with 0 exceptions and 0 mismatches.

| item | recommendation | evidence |
|---|---|---|
| Targeting.CollectEnemies_Snapshot + SnapshotScore + FastAdd | **default ON** (as a set; S1 alone does not pay) | -23 % at 550 melee (S3, s.e. 2.8 %), -7 % at 450; 0 mismatches in ~8 800 checks, pruned path thinly covered (check4 PENDING) |
| Physics.DeadAgentHalt (ClearAim **false**) | **default ON** | -10 to -16 % (b14/b15, 450/550); behaviour guard |t| < 1 at 450; invisible to players |
| Physics.DeadAgentHaltClearAim | **false** | ClearAim true: shots -7 % / hits -5 % (t -1.6/-1.7), a plausible behaviour change |
| Physics.IdleForceSleep + AvoidanceSkipOntoResting + AvoidanceNoIdleDetour + ParkedWheelHold (b12 "H") | **default ON candidate** (needs a client check of parked vehicles) | -31 % / -38 % with 300 parked vehicles, 98-100 % asleep, creep < 5 cm, order response unchanged, move guard clean; without ParkedWheelHold (F) -16 to -19 % |
| Physics.IdleForceSleep alone (C) | not alone | creep 0.4-3 m, -0 to -9 % |
| MultiCore.ParallelScan (jobs 6) | **candidate, opt-in for now** | -16 % at 550 (s.e. 4.8 %), ~0 at 450; 0 self-check differences in 19 571; net ~30-40 ms/s (author's mark 40); inline gives nothing |
| Physics.DeadDisposeEarly | **opt-in only** (visible: wrecks vanish early) | always 5 s: -24 % / -41.5 %; proportional (b17): settles, ~38 fps at 550; b15 valve cycles - do not ship the valve |
| Physics.DeadSettle | **off** | b13 +0.8 % / +8.6 % (no gain); with DeadNoUnitContact (E) only -6 % on top of D |
| Physics.DeadNoUnitContact | off (visible) | part of E; vehicles variant b15_eveh PENDING |
| Targeting.LosTopK | **off** | -6/-7 % here but raises null results 75 -> 80 % and full rescans +10-19 % (approximate item) |
| MultiCore.SensorPipeline_Rate, Targeting.FriendlyFire_Cache ("agg" with LosTopK) | opt-in (approximate) | agg adds 8-15 points in god-mode ramps, 1-5 points with damage (4a/4b) |
| UpdateTiers.Tier_IdleAgents | off | wave 1 (costs) |
| Physics.FixedStep_Override 20 Hz | information only (behaviour change) | qc_fixed20 PENDING |

## 8. Where the time still goes

See `night1\ANALYSIS_BATTLE.md` (ranked section table at 450-600 units per arm, knee mechanism, candidate optimisations).
For the spread layout: `night1\ramp_god\WHERE_TIME_GOES.md` (off vs defaults at @800 and @1000; per frame, with fixed
steps per frame). Summary at ramp_god @800 (defaults, 18 fps, 55.7 ms/frame): PhysX 20.2 ms (14.9 ms per fixed step, the
same as off: no default item reduces PhysX per step), fixed-step scripts 9.5, Update scripts 17.7 (AI agents 5.3,
turrets 3.8, soldier 1.5), LateUpdate scripts 7.8 (creature late update 2.2, soldier late update 1.8). In per-second terms
PhysX is ~37 % of the saturated main thread, the AI agent / target-scan block ~10-20 %, so the next targets are PhysX
(dead bodies and parked vehicles: sections 5a/5b) and the agent scans (5c/5e).

## 9. Not measured / failed

* Nothing with connected clients: hit registration, visible effects of the Physics sleep items and DeadNoUnitContact,
  wreck removal (DeadDisposeEarly) as players see it.
* Real AI rounds (lab rounds on 1.0.17 stall below ~320 units, wave 1); the 4-front ramps stand in (section 4c).
* The grid-pruned Targeting path was covered by only ~100-200 checks (check4 PENDING).
* Second and third quick-check repeats: qc2 (PENDING), qc3 dropped for b18; probe_F2 / probe_A2 dropped.
* `b17_pscan_check` (first) did not self-check ParallelScan (config error); repeated as check2.
* The quick-check fps-10 floor for "new" was not reached (the climb stops at the off arm's floor).
* Shake1 fog hang: not reproduced in two repeats; cause unknown.
* Order-response latency only for idle-to-move orders of parked vehicles, infantry and creatures (not combat).

## 10. Runs still running at the time of writing

PENDING: qc2 (b10), b17_deepprobe (b17), qc_fixed20 (b10), qcgod1 (b10), b15_eveh (b15), b15_snap_check4 (b15), and b18 if
it arrives. Their results are added below as they finish; if this section still says PENDING, the run did not finish
before the write-up deadline.
