# Dense-battle analysis: where the main thread goes at 450-600 units (night 1, build b10)

Written 2026-11-17 ~00:30 from existing data only (no server started, nothing written to `C:\tmp\silica_ds_lab`, no mod source changed). Scripts that produced the tables: `C:\Users\schwe\AppData\Local\Temp\claude\525e89c771cb4f7a\82b33cdb-60b5-4fc8-a510-991f35788a4f\scratchpad\` (`attr.py`, `knee2.py`, `smp.py`, `piv.py`, reading the runs via `measure\ab_report.py`).

## 0. Data and how to read it

| run | what | status | used for |
|---|---|---|---|
| `night1\qc1` | one 3-way melee (r 45 m, map centre), damage on, respawn after 2 s, +15 lab units / 20 s, arms off / def / agg rotating every 30 s | DONE 21:36 | everything below |
| `night1\ramp_god` | 4 fronts, plateaus 200-1000 lab units, god mode (no damage, no deaths), arms def / agg / off, 30 s | DONE 23:39 | comparison (no deaths, same unit count in all arms of a plateau) |
| `ramp_sustain`, `qc2`, `qc3` | - | not run (chain stopped by `STOP_CHAIN` after ramp_god) | - |

* **Units.** "lab" = alive lab-spawned units (Si_PerfLab `lab_units`, what RAMP.md bins by); "W" = world units (PerfProbe `units`). In qc1 W = lab + 41 base units + **160-250 destroyed units still in the world**. They keep body, agent and update registrations until `DestroyedDisposeTime` = 15 s game time (`BaseGameObject.cs:67, 801-837`). At 600 lab, W is 840-880. ramp_god has no dead units (W = lab + 45).
* **Interval selection.** PerfProbe writes 10 s intervals. Per 30 s block I use the first two intervals; they start 2.7 s after the flip, and flips are confirmed within 0.3-1 s. The third interval overlaps the next block and is dropped. Each arm/level cell is therefore 2 intervals = ~20 s.
* **Arms are not at the same unit count inside one 50-unit bin.** The trickle adds ~1 unit/s, and consecutive blocks are 10-30 lab units apart. Frame work doubles every ~80 units here. Arm comparisons are therefore made at matched counts (25-unit bins, per-arm fit) or labelled with each arm's own count.
* **ms/frame vs ms/s.** From ~330 lab units on, the main thread is saturated in every arm: OS main-thread CPU is 94-97 % of wall time and the frame-cap wait is under 0.1 ms. Then work per frame = 1000 / fps, and all sections sum to 1000 ms per wall-second. ms/s is the column to compare between arms; ms/frame is what the frame time is made of.
* **Unattributed.** The sum of PerfProbe player-loop phases equals PerfLab work within 0.5 %. Inside the phases, probed sections and methods leave **0.7-1.1 ms/frame (1-2 %) unattributed** at every level.

## 1. Where the time goes at 450-600 units (Q1)

### 1a. Blocks per level and arm (qc1, ms per frame, share of frame work)

| block | off lab 405 / W 607 | def lab 420-431 / W 622-647 | off lab 467-473 / W 671-683 | def lab 490-492 / W 702-730 | **off lab 534-539 / W 745-788** | **def lab 551-568 / W 790-800** | agg lab 572-587 / W 801-842 | off lab 593-604 / W 841-880 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| fps | 42.0 | 44.5 | 17.8 | 23.4 | 10.4 | 13.2 | 11.9 | 8.3 |
| fixed steps per s / per frame (max) | 25.1 / 0.60 (2) | 25.1 / 0.56 (2) | 25.1 / 1.41 (2) | 25.1 / 1.07 (2) | 24.0 / 2.31 (3) | 23.1 / 1.75 (2) | 21.9 / 1.83 (2) | 20.3 / 2.44 (3) |
| PhysX ms per step | 11.0 | 11.5 | 12.3 | 12.5 | 13.0 | 14.1 | 15.0 | 14.8 |
| PhysX simulate | 6.6 (28 %) | 6.5 (29 %) | 17.3 (31 %) | 13.4 (31 %) | **30.0 (31 %)** | **24.7 (33 %)** | 27.5 (33 %) | 36.2 (30 %) |
| AI target scans (`BTEnemyTargetDetected`) | 3.1 (13 %) | 3.2 (14 %) | 9.8 (17 %) | 8.3 (19 %) | **20.8 (22 %)** | **16.9 (22 %)** | 21.2 (25 %) | 29.8 (25 %) |
| ordered fixed handlers | 2.2 (9 %) | 2.1 (9 %) | 6.5 (12 %) | 4.6 (11 %) | **12.2 (13 %)** | **9.3 (12 %)** | 10.6 (13 %) | 15.0 (12 %) |
| turret / sensor / LOS pipeline (VehicleTurret, SensorTick 1-3, LOS publish, CreatureTurret) | 2.8 (12 %) | 3.0 (13 %) | 4.5 (8 %) | 3.9 (9 %) | **6.2 (6 %)** | **5.6 (7 %)** | 4.8 (6 %) | 7.5 (6 %) |
| creature AI + melee (CreatureDecapod late + update) | 1.6 (7 %) | 1.6 (7 %) | 3.8 (7 %) | 3.1 (7 %) | **5.6 (6 %)** | **4.8 (6 %)** | 4.6 (5 %) | 5.7 (5 %) |
| projectiles (P1 + P2 incl. hit handling) | 1.3 (5 %) | 1.4 (6 %) | 2.7 (5 %) | 2.2 (5 %) | 4.0 (4 %) | 3.5 (5 %) | 3.3 (4 %) | 5.3 (4 %) |
| soldier per-frame (Update + LateUpdate) | 1.2 (5 %) | 1.3 (6 %) | 2.2 (4 %) | 1.8 (4 %) | 3.4 (4 %) | 2.5 (3 %) | 2.6 (3 %) | 4.2 (3 %) |
| churn: lab respawns, disposal walk, async instantiate | 0.6 (3 %) | 0.5 (2 %) | 1.5 (3 %) | 0.9 (2 %) | 2.9 (3 %) | 1.7 (2 %) | 1.9 (2 %) | 3.4 (3 %) |
| presentation residue (fog, animators) | 1.2 (5 %) | 0.1 (0 %) | 1.9 (3 %) | 0.2 (0 %) | 2.8 (3 %) | 0.3 (0 %) | 0.3 (0 %) | 3.3 (3 %) |
| AI other behaviour-tree work | 0.6 (3 %) | 0.6 (3 %) | 1.0 (2 %) | 0.9 (2 %) | 1.5 (2 %) | 1.3 (2 %) | 1.4 (2 %) | 1.8 (1 %) |
| vehicle per-frame + order processors | 0.7 (3 %) | 0.6 (3 %) | 1.0 (2 %) | 0.9 (2 %) | 1.4 (1 %) | 1.2 (2 %) | 1.4 (2 %) | 1.6 (1 %) |
| pathing (AstarPath.Update, TickAI path queue) | 0.3 (1 %) | 0.2 (1 %) | 1.0 (2 %) | 0.5 (1 %) | 1.2 (1 %) | 0.9 (1 %) | 1.0 (1 %) | 1.3 (1 %) |
| networking (TickNetworkObjects, NetworkGameServer) | 0.2 (1 %) | 0.0 | 0.5 (1 %) | 0.0 | 0.8 (1 %) | 0.0 | 0.0 | 1.1 (1 %) |
| other attributed (other handlers, GameManager rest, melons, small phases) | 1.0 (4 %) | 1.0 (4 %) | 1.9 (3 %) | 1.6 (4 %) | 2.9 (3 %) | 2.3 (3 %) | 2.6 (3 %) | 3.3 (3 %) |
| **not attributed to a probed section** | 0.4 (2 %) | 0.4 (2 %) | 0.7 (1 %) | 0.5 (1 %) | 1.0 (1 %) | 0.8 (1 %) | 0.9 (1 %) | 1.1 (1 %) |
| **frame work, ms** | **23.7** | **22.5** | **56.3** | **42.9** | **96.6** | **76.0** | **84.1** | **120.6** |

GC: 0-1 gen-0 collections per 20 s at every level (incremental GC, no clients), so GC is irrelevant here.

### 1b. ~550 units in detail

off is at lab 534-539 / W 745-788 / 10.4 fps; def at lab 551-568 / W 790-800 / 13.2 fps. def runs ~25 lab / ~30 W units higher, so compare the ms/s columns.

| rank (off) | section / probed method | off ms/frame | off ms/s | off share | def ms/frame | def ms/s | def share |
|---:|---|---:|---:|---:|---:|---:|---:|
| 1 | PhysX simulate (`FixedUpdate.PhysicsFixedUpdate`), 24.0 / 23.1 steps/s | 30.03 | 312 | 31.1 % | 24.69 | 326 | 32.5 % |
| 2 | `BTEnemyTargetDetected` -> sync `AIAiming.FindBestTarget` (full scans) | 14.08 | 146 | 14.6 % | 10.32 | 136 | 13.6 % |
| 3 | `BTEnemyTargetDetected` rest (async/pre-filtered scans, LOS requests, checks) | 6.71 | 70 | 7.0 % | 6.54 | 86 | 8.6 % |
| 4 | `CreatureDecapod.OrderedLateUpdate` (creature AI every 0.2 s, melee attack checks) | 5.27 | 55 | 5.5 % | 4.45 | 59 | 5.9 % |
| 5 | fixed: `CollisionAvoidance` + `CollisionAvoidanceManager.PrepareFixedUpdate` | 3.82 | 40 | 4.0 % | 2.94 | 39 | 3.9 % |
| 6 | fixed: `VehicleWheeled` (fixed, part two, batched rays) | 3.48 | 36 | 3.6 % | 2.62 | 35 | 3.4 % |
| 7 | `ProjectileManager.LateUpdate` (P2: job complete, hits, sync rays) | 3.46 | 36 | 3.6 % | 3.23 | 43 | 4.2 % |
| 8 | `VehicleTurret.OrderedUpdateAllTurrets` | 3.09 | 32 | 3.2 % | 3.27 | 43 | 4.3 % |
| 9 | Si_PerfLab `OnUpdate` (lab respawn spawning; lab artefact) | 2.07 | 22 | 2.1 % | 1.37 | 18 | 1.8 % |
| 10 | Soldier late (`OrderedLateUpdateAll` + `Finish`) | 2.01 | 21 | 2.1 % | 1.39 | 18 | 1.8 % |
| 11 | other agent behaviour-tree work | 1.50 | 16 | 1.6 % | 1.33 | 18 | 1.8 % |
| 12 | `FogOfWar.OnUpdate` | 1.43 | 15 | 1.5 % | 0.31 | 4 | 0.4 % |
| 13 | `Soldier.OrderedUpdate` | 1.39 | 14 | 1.4 % | 1.12 | 15 | 1.5 % |
| 14 | `Update.ScriptRunDelayedDynamicFrameRate` (coroutines) | 1.38 | 14 | 1.4 % | 0.68 | 9 | 0.9 % |
| 15 | fixed: `CreatureDecapod.OrderedFixedUpdate` | 1.36 | 14 | 1.4 % | 1.07 | 14 | 1.4 % |
| 16 | animators (`DirectorUpdateAnimationBegin`) | 1.35 | 14 | 1.4 % | 0.01 | 0 | 0.0 % |
| 17 | `SensorTickPart1` (TargetGrid.Update + TickSensors) | 1.22 | 13 | 1.3 % | 0.95 | 13 | 1.3 % |
| 18 | fixed: Soldier (parts 1-3, spherecasts) | 1.01 | 11 | 1.0 % | 0.73 | 10 | 1.0 % |
| 19 | `LineOfSightManager.PublishResults` | 0.94 | 10 | 1.0 % | 0.64 | 9 | 0.8 % |
| 20 | `AstarPath.Update` | 0.85 | 9 | 0.9 % | 0.65 | 9 | 0.9 % |
| - | `TickNetworkObjects` + fixed `NetworkTransformComponent` | 1.36 | 14 | 1.4 % | 0.29 | 4 | 0.4 % |
| - | `UpdateDestroyedDisposal` + `EarlyUpdate` (async instantiate) | 0.79 | 8 | 0.8 % | 0.34 | 5 | 0.4 % |
| - | `AIManager.TickAI` (A* path queue) | 0.31 | 3 | 0.3 % | 0.24 | 3 | 0.3 % |
| | *cross-cutting, already inside the rows above:* | | | | | | |
| | `TargetFinder.CollectDetectedEnemies` (226-254 µs/call, 600-700 calls/s, D 385-415, ~80 % in range) | 13.75 | 143 | 14.2 % | 10.13 | 134 | 13.3 % |
| | `TargetFinder.RequestScan` total | 19.96 | 208 | 20.7 % | 16.33 | 216 | 21.5 % |
| | `Target.CheckRiskOfFriendlyFire` (6-8 µs/call) | 1.72 | 18 | 1.8 % | 1.96 | 26 | 2.6 % |
| | OUM Register/Unregister (churn) | 1.35 | 14 | 1.4 % | 0.11 | 1.4 | 0.1 % |
| | `Soldier.TryFleeUnanswerableThreat` | 0.83 | 9 | 0.9 % | 0.44 | 6 | 0.6 % |
| | `TargetGrid.Update` | 0.90 | 9 | 0.9 % | 0.67 | 9 | 0.9 % |

**Top 5 at ~550 (ms/frame, share):**

| # | off (lab 534-539, 96.6 ms) | def (lab 551-568, 76.0 ms) |
|---:|---|---|
| 1 | PhysX 30.0 (31 %; 2.31 steps/frame, 13.0 ms/step) | PhysX 24.7 (33 %; 1.75 steps/frame, 14.1 ms/step) |
| 2 | AI target scans 20.8 (22 %), of which sync full scans 14.1 | AI target scans 16.9 (22 %), of which sync full scans 10.3 |
| 3 | ordered fixed handlers 12.2 (13 %): avoidance 3.8, wheeled 3.5, creature 1.4, soldier 1.0 | 9.3 (12 %): avoidance 2.9, wheeled 2.6, creature 1.1 |
| 4 | turret/sensor/LOS 6.2 (6 %): VehicleTurret 3.1, SensorTick1 1.2, LOS 0.9 | 5.6 (7 %): VehicleTurret 3.3, SensorTick1 1.0 |
| 5 | creature AI + melee 5.6 (6 %) | 4.8 (6 %) |

**PhysX and steps.**
- PhysX is 30-33 % of the frame.
- Steps per wall-second stay at 25.0-25.1 up to lab ~520 in every arm; steps per frame rise only because frames get longer.
- Below ~12 fps the max-dt clamp drops steps. def has an 80 ms clamp and runs 23.1 steps/s at 13 fps. off has vanilla's 100 ms clamp and runs 24.0/s at 10.4 fps and 20.3/s at 8.3 fps.
- PhysX per step grows about linearly with W: 9.8 ms at W 517, 15.0 ms at W 841 (exponent 0.9).
- **81 % of dynamic bodies are wheeled vehicles.** The census at lab ~600 shows 2297 of 2842 dynamic bodies on 301 wheeled units, 7.6 each (chassis plus `ConfigurableJoint` wheels, `VehicleWheelSetup`).
- Sleep state: 0 lab wheeled bodies asleep (the 44 asleep are the 8 base vehicles), creatures 19 of 277, soldiers ~110 of ~220.

**Fixed handlers.** 12-13 % of the frame. CollisionAvoidance ~40 ms/s, VehicleWheeled ~35, CreatureDecapod fixed 14, Soldier fixed 10-11, NetworkTransform 7 (off) / 4 (def), TurretAimEnemy 5.

**Targeting and sensors.**
- 28-29 % together: AI target scans 22 % plus turret/sensor/LOS 6-7 %. `CollectDetectedEnemies` alone is 13-14 %.
- The Targeting grids are in bypass ("no pruning", visited/q 307 of D 385) because the whole melee fits inside one query range. The existing grid items cannot help in this battle shape.

**Friendly fire** 1.8-2.6 %. **Projectiles** 4-5 % (P2 includes damage application). **Creature melee** 6 %. **A\*** 1 %. **Networking** 1.4 % off, 0.4 % def (0 clients). **Presentation** 3 % off, ~0 def. **GC** 0. **Unattributed** 1 %.

**Churn.**
- Direct churn is 2-3 %: lab respawns 18-22 ms/s; OUM register/unregister 14 ms/s in off and 1.4 in def (Register_O1); disposal walk 4 / 1.4 ms/s; async instantiate 3-4 ms/s.
- **The indirect effect is much larger.** 160-250 destroyed units wait for disposal; that is 20-28 % of W. Rigidbodies per world unit are 4.0 both in qc1 and in god mode, which has no dead units.

## 2. The knee between ~425 and ~475 lab units (Q2)

### 2a. Evidence (qc1 off arm, per wall-second)

| lab / W | fps | steps/s (max per frame) | PhysX ms/step | fixed phase | AI agents | other rate-driven* | T | T' = T + 0.128·W | F_pure = 8.73 µs·W | deaths/s | dead units (W - lab - 41) | direct churn |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 345 / 517 | 66.1 | 25.1 (2) | 9.8 | 324 | 113 | 183 | 620 | 686 (69 %) | 4.5 ms | 9.6 | ~130 | ~25 |
| 405 / 607 | 42.4 | 25.1 (2) | 11.3 | 376 | 160 | 174 | 709 | 786 (79 %) | 5.3 ms | 12.0 | ~160 | ~25 |
| 473 / 683 | 17.4 | 25.0 (2) | 12.1 | 420 | 193 | 177 | 790 | 877 (88 %) | 6.0 ms | 12.8 | ~170 | ~27 |
| 539 / 745 | 10.3 | 23.8 (3) | 12.8 | 429 | 246 | 156 | 831 | 926 (93 %) | 6.5 ms | 11.7 | ~165 | ~30 |
| 604 / 841 | 8.3 | 20.4 (3) | 15.0 | 431 | 264 | 144 | 840 | 947 (95 %) | 7.3 ms | 10.8 | ~195 | ~28 |

\* "Other rate-driven" = CreatureDecapod late, Soldier late, projectiles P1+P2, FogOfWar.OnUpdate, LOS publish, Si_PerfLab. These sections keep the same ms/s while fps falls 11× in ramp_god (fog 23-27 ms/s, soldier late 32-36 from 88 to 8 fps).

Targeting is the one super-linear term. `BTEnemyTargetDetected` goes 83 → 134 → 175 → 231 → 249 ms/s (about W^2.3). `CollectDetectedEnemies` goes 125 → 154 → 191 → 227 → 254 µs per call, at 367 → 502 → 600 → 696 → 678 calls/s.

### 2b. What the data says

1. **Physics catch-up: no.**
   - Fixed steps per second are pinned at 25.0-25.1 through the knee in every arm, and no frame runs more than 2 steps.
   - Physics runs the same 25 steps per second in fewer frames, so its per-frame cost rising is a consequence, not a cause.
   - Through the knee PhysX ms/s rose 284 → 302 (+6 %) and the fixed phase 376 → 420 (+12 %), for +12.5 % W.
   - CatchUpCap (80 ms) binds only below 12.5 fps and vanilla's 100 ms clamp only below 10 fps; both are inactive at 17-42 fps. The knee is the same in off, where CatchUpCap is off.
2. **Churn: not the trigger.**
   - Deaths (12-13/s) and direct churn (25-30 ms/s) are flat across the knee.
   - The dead-unit population (~160-200, 20-25 % of W) is also steady. It shifts the whole curve to lower lab counts: qc1 at lab 405 matches the god ramp at lab 582 by world units (42 vs 37-39 fps). It does not create the bend.
3. **The quadratic term accounts for about a third.** From lab 405 to 473, T' grew by ~91 ms/s: fixed phase +44 (linear), AI target scans +33 (W^2.3), the rest +14.

**Mechanism: the saturation curve of a mostly time-driven workload.**
- Once the main thread is full, 1000 ms/s = T' + fps × F_pure, so **fps = (1000 − T') / F_pure**.
- T' is the work that scales with simulated seconds, not with frames: 25 Hz fixed steps, ~1500 behaviour-tree ticks per second at any fps, creature AI on 0.2 s timers, projectiles, and a dt-proportional part of the per-frame handlers.
- That last part was fitted as per-frame cost per unit = c0 + c1 × frame time:

  | run | c1 off | c1 def | c1 agg | residual |
  |---|---:|---:|---:|---:|
  | qc1 | 0.128 | 0.112 | 0.116 | ~1 µs |
  | ramp_god | 0.117 | 0.101 | – | ~1 µs |

  (c1 in µs per unit per ms of frame time; the god-ramp agg fit is distorted by one outlier interval.)
- The model reproduces the measured fps: 40 / 21 / 10-11 / 7 predicted vs 42 / 17 / 10.3 / 8.3 measured.
- T' is 79 % of each second at lab 405, 88 % at 473 and 93 % at 539.
- Between 405 and 473, T' rises 12 % (787 → 877) and halves the budget left for frames (213 → 123 ms/s). F_pure grows only 12 % (5.3 → 6.0 ms). fps falls 42 → 17.
- No single section has a knee; the bend is the 1 / (1 − T'/1000) term.

**Consequences for optimisation:**
- **The shipped defaults act almost only on F_pure.** At matched counts T' is the same in off and def (877 vs 872; 926 vs 921), while c0 drops 8.7 → 6.9 µs per unit (−20 %).
- **Time-driven savings are leveraged near saturation.** At def ~560 (T' ~928, F_pure ~5.5 ms, 13 fps), every 10 ms/s removed from T' adds ~1.8 fps.
  - Removing 50 ms/s gives ~22 fps (frame work 76 → ~45 ms).
  - The measured def arm at lab ~490 (T' 872, 23-25 fps) matches this, which is an independent check of the model.

**What would make it decisive per section:** an fps-cap flip at sub-saturation load. Run the qc1 melee as a plateau at ~250 lab units and alternate Si_ServerFpsCap MaxFPS 120 / 40 / 20 every 30 s. For each section, fitting ms/s = a + b × fps separates the time-driven part (a) from the frame-driven part (b). This needs a runtime MaxFPS switch, or `Application.targetFrameRate` set via Si_PerfLab.

## 3. def −41 % at 500-549 vs −28 % at 550-599, and agg (Q3)

**This is a binning artefact.** In the 500-549 bin the off samples sit at lab 534-552 (mean 539) and the def samples mostly at ~509 (7 of 11 s). Matched comparisons from PerfLab 1 Hz samples (frame-weighted work ms; mean lab and sample count in brackets):

| 25-unit bin | off | def | agg | def vs off | agg vs off |
|---|---|---|---|---:|---:|
| 525-549 | 98.1 (540, n 18) | 70.2 (546, n 4) | 54.1 (528, n 6) | −28 % | (unmatched) |
| 550-574 | 99.1 (559, n 7) | 77.8 (564, n 13) | 76.0 (569, n 2) | −21 % | −23 % |
| 575-599 | 123.4 (591, n 11) | 87.3 (585, n 9) | 87.3 (588, n 21) | −29 % | −29 % |

Per-arm log-linear fit over lab 400-620 (n 71-90 s per arm, residual sd 0.12-0.17 in log):

| lab | def vs off | agg vs off |
|---:|---:|---:|
| 400 | −30 % | −39 % |
| 500 | −28 % | −34 % |
| 550 | −27 % | −31 % |
| 600 | −25 % | −28 % |

The defaults' saving is flat, as the model predicts for a saving on per-frame work only. Two small real effects narrow it at the very top:
- Both arms drop fixed steps. def runs 22.4-23.8/s at lab 551-569; off runs 20.3-24.0/s at lab 534-604. off loses more steps at 8 fps, which removes more of its time-driven work.
- The bin composition shifts again in the 550-599 bin.

**Why agg = def at ~587 (87.3 vs 87.3):**
- **SensorPipeline_Rate does nothing below 20 fps.** Its status reads "frames/s 11, runs/s 11.1, mean dt 78.5 ms". SensorTickPart1 per frame is agg 1.03-1.10 vs def 1.02-1.03 ms there, against 0.30-0.34 vs 0.48-0.50 at lab ~445 / 46 fps.
- **FriendlyFire_Cache and LosTopK save per-frame work.** FriendlyFire_Cache answers 1350 of 3012 calls/s. LosTopK saves 15 900 LOS queries/s and ~20 ms/s of RequestScan self time.
- **agg spends more in full scans.** `CollectDetectedEnemies` costs 181-186 ms/s at 723-767 calls/s in agg (W 801-842), against 128-139 ms/s at 567-593 calls/s in def (W 790-800). After adjusting for agg's higher W, agg is still ~5 % above def.
- Whether LosTopK causes rescans is undecided: the off arm scatters over 603-698 calls/s. It needs a counter of null FindBestTarget results per arm.

**ramp_god (no deaths, exactly matched units) shows the same shrinking:**

| lab | fps (agg / def) | agg vs def | def vs off |
|---:|---|---:|---:|
| 582 | 50-65 | 15.2 vs 18.9 ms (−20 %) | −30 % |
| 779 | 19-20 | 51.9 vs 55.7 ms (−7 %) | −32 % |
| 971 | 11 | 94.8 vs 96.7 ms (−2 %) | −24 % |

## 4. Next optimisations (Q4), ranked for this battle shape

**Baseline:** def at lab 551-568 / W 790-800, 13.2 fps, 76 ms/frame, T' ~928 ms/s, F_pure ~5.5 ms. "Direct" = ms/s ÷ 13.2. "Model" = frame work from section 2 after the saving (time-driven savings only, checked against the measured def curve). All estimates are unmeasured.

I checked all 56 existing items in `Modules\*`. None touches destroyed units, the per-candidate scan cost, the creature late update, or the projectile synchronous path. `FixedStep_Override` and `MaxCatchUpSteps` already exist.

### C1. Make destroyed units cheap during their 15 s disposal wait (approximate, small effort) — top candidate

**Vanilla path.**
- `Vehicle.OnDestroyed` turns kinematic mode off (`Vehicle.cs:892-899`), so a wreck keeps chassis plus jointed wheel bodies simulating.
- `Soldier.OnDestroyed` only plays the death animation (`Soldier.cs:3807-3818`); `CreatureDecapod.OnDestroyed` is at `:4961`.
- All registrations stay until `UpdateDestroyedDisposal` destroys the object after 15 s of game time (`BaseGameObject.cs:67, 801-837`; `Unit.CanDispose` `Unit.cs:432-441`).

**Why it costs.**
- 160-250 dead units = 20-28 % of W.
- Census: lab wheeled bodies are 0 % asleep (wrecks included); creatures are 7 % asleep, so corpses are awake.
- Estimated dead share of awake dynamic bodies: ~69 wrecks × 7.7 + ~65 creature corpses ≈ 600 of ~2650 (~22 %).
- Rigidbodies per world unit are 4.0 in qc1 and in the god ramp alike. PhysX per step per world unit is also equal: 17.0 vs 17.2 µs.

**Proposed item `Physics.DeadSettle` (preferred).**
- Postfix on `Unit.OnDestroyed`.
- After 3 s, once the body is slow, set every body kinematic (`Vehicle.RigidBodies`, wheel bodies, `Unit.RigidBody`) and unregister the unit from CollisionAvoidance.
- Keep the colliders, so wrecks still block and take hits.
- `OnSwitch(false)` restores non-kinematic. Guard velocity writes on kinematic bodies the way RestWriteGuards does.
- **What could break:** wrecks can no longer be shoved aside after 3 s, so vehicles may get stuck at chokes; clients see wrecks stop sliding. Check that nothing relies on a dynamic wreck.

**Variant `DestroyedDisposeTime` = 5 s for units.** Visible to all players: corpses and wrecks vanish after 5 s instead of 15.

**Saving.**
- DeadSettle: PhysX elasticity ~0.5 (wave1 b7: 58 % fewer awake bodies gave −29 % PhysX) → ~35 ms/s, plus ~10-20 ms/s of fixed handlers on dead units = **~45-55 ms/s**.
  - Direct ~3.5-4 ms/frame. Model 76 → ~45-50 ms/frame (13 → 20-22 fps).
- 5 s dispose: ~60-85 ms/s, model ~40 ms/frame.
- No gain without deaths (god ramps).

**Measure first:** split the census by `IsDestroyed`; time the fixed handlers of dead units; one on/off flip in qc1.

### C2. Exact, cheaper `CollectDetectedEnemies` (exact, medium-large effort)

**Vanilla path.**
- `AIAiming.FindBestTarget` → `Unit.GetBestTarget` → `RequestScan` → `CollectDetectedEnemies` (`TargetFinder.cs:349-384`). Creatures run two finders (`CreatureDecapod.cs:3331-3397`); soldiers go via `HumanHandsAnimator.GetBestTarget`.
- Each scan walks the whole `DetectedTargets` list (D 385-415).
- Per entry: Unity null and IsDestroyed checks, GetTeamsAreEnemy, structure checks, `WorldLineOfSightCenter` (a TransformPoint), distance, dot product.
- Then `GameAI.CalculateBestTargetScore` (`GameAI.cs:671-800`: ~15 Unity bool checks, `ResolveTargetScoreTypeValues`, `GetIsTargetedBy`, `GetWasDamagedByRecently`).
- Then `AddCandidate`, a linear min search over 24 slots (`:534-557`).

**Why it costs.** ~0.8 µs per in-range candidate, 226-254 µs per call, 600-700 calls/s = 134 ms/s (def), about 13 % of the main thread, growing ~W². The grid cannot prune it here.

**Proposed item `Targeting.CollectEnemies_Snapshot`.**
- Prefix for modes DetectedEnemies and WithinView.
- Once per team per frame, invalidated when the frame or the list changes, build an array of the scanner-independent data: Target, LOS centre, flags, OwnerUnit, IsFlying, functional/critical/score/top speed, ObjectInfo, DamageManager.
- Per scan, only the scanner-dependent terms run.
- Cache the type values per (from-ObjectInfo, target-ObjectInfo, damage type, damage) per frame; keep the top 24 in a heap.
- Scores must be bit-identical; keep the existing sampled self-check against vanilla.
- At 13 fps about 30 scans per team per frame would share one snapshot.
- **Risk:** a missed invalidation if a script moves a target between two scans in the same frame. The self-check would catch it.

**Saving.** 40-60 % of the block = **55-80 ms/s**. Direct 4-6 ms/frame; model 76 → ~38-45 ms. The 40-60 % is a guess: no data splits the 0.8 µs.

**Measure first:** deep probe of `CalculateBestTargetScore`, `ResolveTargetScoreTypeValues`, `get_WorldLineOfSightCenter` and `GetTeamsAreEnemy`. A staged low-effort version that snapshots only the pre-checks and LOS centre would show ~20-30 % if those calls are the expensive part.

### C3. Existing `Physics.FixedStep_Override` 20 Hz (default off, never measured): flip it in this battle

- `GameManager.Start` sets `fixedDeltaTime = 1/25` (`GameManager.cs:645`). The consequences are already listed in `Modules\Physics\TimeStep.cs`:
  - soldier ground checks 8.3 → 6.7 Hz;
  - avoidance broadphase 6.25 → 5 Hz;
  - wheel-spin blend ×0.8;
  - collision-damage impulse gate;
  - softer wheel joints and deeper penetration with a 50 ms step;
  - fewer network-transform checks.
- The fixed phase is ~450 ms/s (45 %) and is purely per step.
- **Saving ~60 ms/s at def ~560** (direct ~4.5 ms/frame, model ~43 ms) and ~85 ms/s at lab 470-490.
- Effort: none, just flip it. Needs a client test (vehicle handling, hit registration of fast units; aircraft are +50 % faster since 1.0.15).

### C4. Overload valve: CatchUpCap `MaxCatchUpSteps = 1` below ~25 fps (existing setting, approximate)

- At def ~560, one step per frame gives fps ≈ (1000 − 479) / (5.5 + 19.4) ≈ 21 and frame work ~48 ms.
- **Game time then runs at ~0.84× real time**: players see slow motion. Vanilla does the same below 10 fps.
- Opt-in "big battle" mode only. It needs an fps-dependent switch; the setting is read once at startup today.

### C5. Projectile P2 synchronous inside-bounds path (exact, medium effort)

- In `ProjectileJobManager.UpdateAllProjectilesP2` (`:262-345`), projectiles that start inside damage bounds (nearly all in a melee) take synchronous rays: `RaycastNonAlloc` when the 4-hit buffer overflows (`:296`), then `NonBatchedRaycastP1/P2` (`:309-332`).
- P2 costs 36-43 ms/s and stays flat per second as fps drops.
- Proposal: batch these rays as `RaycastCommand` in P1, keeping the same hit order.
- Saving ~10-20 ms/s. Needs a counter of sync-path projectiles first.

### Not ranked (measure first)

- **Wheel-joint solver iterations (6 → 4).** Softens joints and contacts, and the iteration count applies per island, so it must cover whole contact islands. Needs a PhysX time split first.
- **`CreatureDecapod.OrderedLateUpdate`** (55-59 ms/s): needs deep probes on `CreatureAttack.CanHitTarget` and `Attack`.
- **Null-result scan cache.**
  - `GetThrottledBestTarget` (`AIAiming.cs:423-430`) only throttles when a non-null target is cached.
  - Nearly every FindBestTarget call is a full scan: 362 µs per call = 1.4 × 254 µs.
  - A null cache would need ≥ 0.8 s to skip anything at ~2 behaviour-tree ticks per second, which means a visible acquisition delay.
- **LosTopK fallback:** only if section 3's open question resolves against LosTopK.

## 5. What could not be determined

- **Repeatability.** qc1 is a single run. qc2, qc3 and ramp_sustain did not run, and arms differ by 10-30 units inside each bin.
- **Frame-driven vs time-driven split per section** is inferred (ramp_god flatness plus a two-parameter fit), not measured. The cap flip in 2b would measure it.
- **Cost of destroyed units** is inferred from counts, rigidbody ratios and asleep shares. The census has no dead/alive split.
- **What makes up the ~0.8 µs per scan candidate.**
- **How PhysX time splits** between broadphase, contacts and the joint solver.
- **Whether LosTopK causes extra rescans.**
- **The behavioural effect** of every candidate.
- `README.md` is stale: most modules are listed as "to do". The item list here comes from `Modules\*\*.cs`.
