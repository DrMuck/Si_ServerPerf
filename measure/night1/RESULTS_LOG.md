# night1 results log (rig agent; one section per finished run, same text as sent to main)

## qc2 (b10, 04:16-04:30, quick-check trickle, arms def,agg,off 30 s; qc1 order was off,def,agg)
Stopped at the fps floor at 573 lab units (qc1: ~625). 0 exceptions, nothing FAILED.

| arm | < 30 fps qc1 / qc2 | < 20 fps qc1 / qc2 | < 10 fps qc1 / qc2 | fit work@300 qc1 / qc2 | fit work@500 qc1 / qc2 |
|---|---|---|---|---|---|
| off | 434 / 431 | 457 / 474 | 570 / 558 | 10.6 / 8.7 ms | 67.2 / 63.7 ms |
| def | 469 / 492 | 509 / 523 | - / - | 7.9 / 6.9 ms | 47.2 / 41.8 ms |
| agg | 484 / 483 | 521 / 525 | - / - | 5.7 / 5.7 ms | 44.1 / 39.7 ms |

Spread qc1 vs qc2: crossings within 3-23 units; fits at 500 units: def vs off -30 % (qc1) / -34 % (qc2), agg vs off -34 % / -38 %. At 300 units the off fit differs 18 % between the two runs (fit rmse 6-8 ms), so single fit points are good to ~±15 %; the crossings are the more stable summary.
## b17_deepprobe (b17, 04:31-04:37, melee plateau 550 (lab avg 484), arm P fixed, 230 s of PerfProbe intervals, 30.4 fps, work 32.9 ms; probe overhead est 21 ms/s; 101 methods patched, 0 failed; `Silica.AI.AIPathfinding` type not found (QueueFindPath + the base UpdateQueuedFindPaths dropped); BTChaseEnemy / BTTakeCover 0 calls; 0 exceptions)

Totals are inclusive, self = exclusive (PerfProbe gives both). Decision values:
| probe | total ms/s | self ms/s | calls/s | us/call | threshold | verdict |
|---|---:|---:|---:|---:|---|---|
| TargetFinder:CollectPreFilteredEnemies | 32.5 | 32.5 | 159 | 204 | >= 35 | just under (32.5) |
| LineOfSightManager:RequestQueryToTarget | 6.8 | 6.8 | 11 391 | 0.6 | >= 15 | no |
| path/order: AIOrderProcessor:IssueOrder / BTMoveTo / BTPlayerSetTarget | 0.2 / 0.1 / 0.6 | 0.2 / 0.1 / 0.5 | 31 / 463 / 645 | | >= 15 self | no (QueueFindPath not probed: type not found) |
| CreatureDecapod:GetAIPathPursuitPoint (creature path) | 24.1 | 24.1 | 295 | 82 | (path) | **notable: 24 ms/s self** |
| CreatureAttack:CanHitTarget | 1.2 | 0.8 | 455 | 2.7 | >= 25 | no |
| ProjectileBasic:NonBatchedRaycastP1 | 5.9 | 5.9 | 1 251 | 4.8 | >= 30 | no |
| ProjectileBasic:HandleHit | 53.8 | 28.0 | 642 | 84 | - | **notable: 28 ms/s self** |

Other probes: AIAiming:FindBestTargetAsyncFinish 35.5 total (self 0.0) = Unit:FinishGetBestTarget 39.0 / Vehicle 16.1 / CreatureDecapod 15.6 total (self 0.1 each; they contain CollectPreFilteredEnemies); GetWantsUrgentReevaluation 4.3 self (170/s, 25 us); DamageManager:ApplyDamage 3.0; CreatureDecapod:UpdateAttacks 2.3 total (4 439/s); CreatureAttack:ApplyDamage 1.6; BTShoot 1.7 total / 1.1 self; DropHandles 0.4 (859/s); SetAim 0.5 (1 069/s); everything else < 0.5 ms/s.
## qc_fixed20 (b10, 04:37-05:00, quick-check trickle, arms def vs def20 = def + Physics.FixedStep_Override 20 Hz, 30 s blocks)
Census confirms the flips: fixedDt 0.04 <-> 0.05, steps/s 25.0 <-> 20.0, maxStepsPerFrame 2 (def) / 1 (def20). Climb reached 959 lab units before the floor (this start ran much lighter than qc1/qc2: def crosses 30 fps at 516 vs 469-492). 0 exceptions.

| | def | def20 |
|---|---:|---:|
| < 30 fps at (lab units) | 516 | 548 |
| < 20 fps at | 577 | 620 |
| < 10 fps at | 912 | not reached (10.4 fps at 900-999) |
| fit work @500 / @800 | 30.7 / 86.4 ms | 25.9 / 77.9 ms (-15 % / -10 %) |

100-unit bins (fps / work ms, def vs def20): 400-499 46 / 21.5 vs 60 / 16.5 (-24 %); 500-599 22.5 / 45.4 vs 31.3 / 32.6 (-28 %); 600-699 15.9 / 64.5 vs 18.3 / 56.5 (-12 %); 700-999 -6 to -8 %.
PhysX at 1000-1200 world units: per frame -13 to -14 %; per fixed step def 21.1 vs def20 23.0 ms (+9 % per step, 20 % fewer steps) -> about -30 ms/s at ~12 fps.
Behaviour (1 Hz, per block, 19-20 blocks): deaths/s, kills/s, shots/unit-s, hits/s, with-target all within ±3 % (|t| < 1). Behaviour-changing item (step-count tied logic, see TimeStep.cs): information only.
## qcgod1 (b10, 05:00-05:21, quick-check geometry in GOD mode (no damage, no deaths, no respawn), arms off/def/agg 30 s)
Climb stopped at the fps floor at 880 lab units (world 925: no corpses). Engagement: with-target 0.97, 1.59 shots+attacks per unit-s, 0.73 deaths/s (stragglers). 0 exceptions.

| arm | < 30 fps at | < 20 fps | < 10 fps | same arm, sustain qc1 / qc2 / b15_best_qc (< 30 / < 10) |
|---|---:|---:|---:|---|
| off | 607 | 665 | 812 | 434 / 431 / 447 ; 570 / 558 / 608 |
| def | 668 | 743 | not reached | 469 / 492 / 478 ; - / - / 683 |
| agg | 688 | 743 | not reached | 484 / 483 / - |

Damage + deaths + respawn (and the 150-200 corpses they keep in the world) move the off-arm 30 fps point down by ~165 lab units (607 -> 431-447) and the 10 fps point by ~200-250. Defaults gain +61 units at the 30 fps point in god mode vs +35-61 with damage. The per-arm quadratic fits of this run are poorly constrained (off: 23 ms at 300 but 30 fps only at 607); use the crossings.
## b18_check (rig 2; build b20 md5 82F1D0C7..., 05:22-05:28, melee 450, fixed arm N + CollectPreFiltered_Snapshot + WheelJointWriteGuard, Targeting SelfCheckFraction 0.5, dumps every 45 s)
PASSED: both new items patched and ON from startup (WheelJointWriteGuard 1/1, CollectPreFiltered_Snapshot 1/1), nothing NOT APPLIED / FAILED (ab_report: "none"), CollectPreFiltered_Snapshot 19 274 checks / mismatch total 0, all other Targeting mismatch totals 0, 0 exceptions, scenario row ok. Both items stay in new2.

| item | checks total | mismatch total | rate during the plateau |
|---|---:|---:|---|
| CollectPreFiltered_Snapshot | 19 274 | 0 | q/s 130-157, checks/s 62-79 = 47-51 % of served (the configured 50 %; no factor-10 gap on this path) |
| CollectEnemies_Snapshot / SnapshotScore / FastAdd | 83 479 each | 0 | |
| FriendlyFire_Grid | 59 960 | 0 | |
| Rank_Sort | 88 935 | 0 | |
| UrgentClose_Grid | 128 | 0 | |
| FriendlyFire_Flat (off at runtime) | 0 | 0 | |

- **CollectPreFiltered_Snapshot path:** visited/q 232-285 of job entries 263-304 (84-94 %), "to vanilla logic/q" about the same as visited, entries refilled/s 0-1.4.
- **WheelJointWriteGuard**, per 10 s at the plateau:
  - drive-mode writes: 317-332 k skipped, 0-6 written.
  - rotation writes: 22-64 skipped, 80-90 k written.
  - So almost the whole skip comes from the drive-mode write; the rotation guard almost never finds an equal value.
- **Plateau:** 427 lab units on average, 55.7 fps, work 17.6 ms (p95 31.8), deaths 10.5/s.
- Diag_LogExceptions: 0 in every window.

Log: night1\b18_check\Latest.log. NOW: b18_items (05:30). It will be followed by b18_phys_guard, b18_phys_guard_idle, b20_ff_check, b20_ff, b19_pscan_check, b19_pscan, b20_best, b19_spread, b15_eveh and b15_snap_check4.


## b18_items (rig 2; build b20, 05:30-05:48, melee plateaus 450/550 [lab avg 429 / 525], arms N / R / J / RJ / V, 30 s blocks, 3 rotated cycles of 5 per plateau, 5 s settle; V also with 10 s settle)
Verdict: no b18 item gives a frame-work gain outside the noise.
- R (CollectPreFiltered_Snapshot) does cut its own method by about a third: 11.0 → 6.7 ms/s at 450 and 16.0 → 10.9 ms/s at 550. That saving of 4-5 ms/s is below what 30 s blocks can resolve.
- J and V change nothing measurable: physics time per step and VehicleWheeled time are the same in every arm.
- Guard metrics move within block noise.
- 0 exceptions, 2/2 rows ok, nothing FAILED.

| plateau | arm | fps | work ms | paired vs N (s.e., n) | CollectPreFilteredEnemies ms/s / calls/s / us/call | BTEnemyTargetDetected ms/s | CheckRiskOfFriendlyFire ms/s (calls/s) |
|---|---|---:|---:|---|---|---:|---|
| 450 | N | 72.7 | 12.98 | | 11.0 / 83 / 132 | 84.1 | 64.1 (10 657) |
| 450 | R | 71.2 | 13.40 | +2.3 % (1.2 %, 3) | 6.7 / 77 / 87 | 85.8 | 60.2 |
| 450 | J | 72.4 | 13.15 | +0.3 % (3.6 %, 3) | 11.0 / 79 / 140 | 90.7 | 58.3 |
| 450 | RJ | 73.2 | 13.03 | -0.1 % (1.6 %, 4) | 8.9 / 97 / 91 | 81.0 | 63.3 |
| 450 | V | 73.5 | 12.91 | +0.4 % (2.1 %, 3); 10 s settle +0.7 % (2.7 %) | 12.2 / 87 / 140 | 90.8 | 61.1 |
| 550 | N | 35.1 | 29.17 | | 16.0 / 93 / 172 | 132.6 | 44.2 (5 711) |
| 550 | R | 35.4 | 28.63 | -0.4 % (5.4 %, 3) | 10.9 / 94 / 116 | 124.8 | 43.8 |
| 550 | J | 32.3 | 31.63 | +10.5 % (7.2 %, 3) | 16.0 / 86 / 186 | 140.4 | 36.0 |
| 550 | RJ | 37.0 | 27.48 | -4.5 % (5.2 %, 3) | 9.8 / 93 / 105 | 129.1 | 40.0 |
| 550 | V | 34.8 | 29.21 | +1.8 % (7.8 %, 3); 10 s settle +3.2 % (8.1 %) | 16.0 / 97 / 166 | 130.0 | 39.0 |

Method numbers rest on only 30-50 s of whole 10 s intervals per arm.

### Physics per arm

These are PerfProbe sections in ms/s, with ms per call in brackets. "Physics step" is the FixedUpdate.PhysicsFixedUpdate subsystem, i.e. PhysX simulate, 25 steps/s.

| plateau | arm | FixedUpdate phase | Physics step (ms/step) | OUM fixed all | VehicleWheeled.OrderedFixedUpdate (calls/s) |
|---|---|---:|---:|---:|---:|
| 450 | N | 336.8 | 249.9 (9.94) | 86.2 | 21.0 (4 872) |
| 450 | R | 351.3 | 260.3 (10.35) | 90.2 | 22.2 |
| 450 | J | 337.1 | 249.7 (9.93) | 86.7 | 21.3 |
| 450 | RJ | 334.4 | 247.9 (9.87) | 85.8 | 21.6 |
| 450 | V | 335.4 | 250.5 (9.97) | 84.1 | 21.3 |
| 550 | N | 443.7 | 322.7 (12.84) | 120.2 | 28.2 (6 021) |
| 550 | R | 437.4 | 317.4 (12.61) | 119.2 | 27.8 |
| 550 | J | 443.3 | 320.2 (12.76) | 122.3 | 27.7 |
| 550 | RJ | 438.8 | 318.9 (12.67) | 119.0 | 27.6 |
| 550 | V | 445.9 | 326.0 (12.96) | 119.0 | 28.0 |

### Item counters

**wheeljoint** (median per 10 s line):
- 450: driveSkip 311-324 k, driveWrite 6-12, rotSkip 36-53, rotWrite 83-87 k.
- 550: driveSkip 398-400 k, driveWrite 2, rotSkip 42-51, rotWrite 105 k.
- About 35 k native drive-mode writes per second are skipped, yet VehicleWheeled.OrderedFixedUpdate stays at 4.3-4.7 us/call. The skipped native call is evidently too cheap to show.

**solveriter** (V blocks):
- target 4/0, default 6/1.
- 450: 169 vehicles / 1 278 bodies; 550: 209 vehicles / 1 595 bodies. restored about 21 per 10 s (vehicles that died or whose control was switched).
- PhysX ms/step is unchanged (12.96 vs 12.84 ms at 550). Lower solver iterations on ~200 AI wheeled vehicles do not show in the simulate time of this melee, which is dominated by soldiers and creatures.

### Guard (1 Hz samples)

| plateau | arm | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |
|---|---|---:|---:|---:|---:|---:|
| 450 | N | 9.77 | 9.00 | 0.580 | 150.2 | 0.508 |
| 450 | R | 9.53 | 8.72 | 0.577 | 137.8 | 0.486 |
| 450 | J | 10.04 | 9.40 | 0.514 | 137.3 | 0.486 |
| 450 | RJ | 9.31 | 8.60 | 0.577 | 141.9 | 0.525 |
| 450 | V | 10.16 | 9.45 | 0.589 | 140.1 | 0.509 |
| 550 | N | 11.05 | 10.37 | 0.464 | 144.1 | 0.479 |
| 550 | R | 11.69 | 10.76 | 0.517 | 156.2 | 0.477 |
| 550 | J | 11.38 | 10.56 | 0.458 | 148.6 | 0.467 |
| 550 | RJ | 12.00 | 11.05 | 0.473 | 159.3 | 0.466 |
| 550 | V | 11.78 | 10.95 | 0.453 | 151.6 | 0.479 |

- J's shots at 450 are -11 % vs N, but J's shots at 550 and RJ's at both plateaus are within ±2 %. This reads as block noise: J only skips writes of equal values.
- V changes vehicle physics (approximate), but its guard values sit within the N spread.

Files: night1\b18_items\ (report_1_step_table.txt, report_3_methods_by_arm.txt, report_4_guard_by_arm.txt, report_5_step_table.txt). The physics table comes from a new tool, `night1\phys_sections_by_arm.py b18_items`.


## b18_phys_guard + b18_phys_guard_idle (rig 2; build b20, 05:48-06:03)
Verdict: on 300 moving LightStrikers, neither J (WheelJointWriteGuard) nor V (VehicleSolverIterations 6 → 4) changes main-thread work or PhysX simulate time.
- Work: J -0.1 %, V -0.4 % vs A (s.e. about 1 %).
- PhysX simulate: 215.9 / 214.5 / 215.9 ms/s for A / J / V.
- Correctness: 9 310 orders ok, 0 failed; 300/300 alive at the end; no vehicle asleep while moving in any arm.
- H idle: 99.7 % of wheeled bodies asleep at steady state. The `wheelhold` line shows syncs = 0 and syncSkipped = 0 from ~45 s into the measure on.
- 0 exceptions, nothing FAILED or NOT APPLIED.

**(a) phys-move-vehicles 300** (move_patrol; arms A defaults / J / V, 60 s blocks A,J,V,V,J,A + repeat, 20 s settle):
- The server ran at the 101 fps frame cap in every arm (frame-cap wait about 507 ms/s), so only work ms can show a difference.

| arm | work ms (n s) | paired vs A (s.e., n) | PhysX simulate ms/s | FixedUpdate phase | OUM fixed all | CollisionAvoidance fixed | wheeled asleep (census) |
|---|---:|---|---:|---:|---:|---:|---:|
| A | 4.87 (78) | | 215.9 | 320.5 | 104.0 | 35.5 | 0 % |
| J | 4.88 (102) | -0.1 % (1.2 %, 3) | 214.5 | 317.5 | 102.4 | 33.8 | 0 % |
| V | 4.89 (91) | -0.4 % (0.8 %, 3) | 215.9 | 320.7 | 104.2 | 35.8 | 0 % |

- **J counters** (per 10 s): driveSkip 602 k, driveWrite 6, rotSkip 3.5, rotWrite 301 k. That is about 60 k skipped native drive-mode writes per second with no visible saving.
- **V counters:** target 4/0, default 6/1, 306 vehicles / 2 730 bodies switched, restored 0. Even on a pure-vehicle island, PhysX time is the same at 4 iterations.

Scenario totals over the whole 420 s:
- 9 310 orders ok, 0 failed; spawned 300, spawn failed 0, alive 300/300 at the end.
- The census shows 0 % of wheeled bodies asleep in every arm (dynAsleep 40 of 2 756 = static/other bodies).
- PerfLab logs no per-arm order or arrival split for move_patrol and has no flip/stuck counter. So "no flips/stuck" rests only on: 0 failed orders, all 300 alive, the same patrol order rate across the run, and 0 exceptions.

**(b) phys-idle-vehicles 300, arm H fixed** (IdleForceSleep + AvoidanceSkipOntoResting + AvoidanceNoIdleDetour + ParkedWheelHold; 30 s settle + 150 s):
- **Wheeled asleep:** 1 851 / 2 730 at the first census, 2 576 after 30 s, then 2 702-2 721 (99.0-99.7 %) from 06:01:05 to the end. fps 119.5 (cap), work 2.1 ms (p95 4.4).
- **wheelhold:**
  - Settle phase: heldVehicles 94 / wheelsSlept 372 / syncs 26 in the first 10 s, then 1-11 held / syncs 1-11 per 10 s until 06:01:05.
  - Steady state (06:01:15-06:02:55, 11 lines): heldVehicles 0, syncs 0, syncSkipped 0.
  - syncSkipped stayed 0 for the whole run. In this scenario the new gate never had to skip a sync; the syncs that happen are the ones before a real re-sleep, and in steady state there are none.
- **idlesleep:** forcedNow 313-316, reSleep 0-5 per 10 s at steady state (one burst of 34 and 18 at 06:01:15-25), released 0-5. blocked about 250, falling to 0-5 in the last 40 s.
- leftAwakeFast / leftAwakeForce: 0 throughout.

Files: night1\b18_phys_guard\ (report_1_physics_report.txt, report_2_ab_report.txt; paired from `night1\step_table.py b18_phys_guard --arms A,J,V --settle 20 --bin 1000`) and night1\b18_phys_guard_idle\Latest.log. The idle run has no flips, so its physics_report is empty; the numbers above come from the log lines.


## b20_ff_check (rig 2; build b20, 06:03-06:11, melee 450, fixed arm N + FriendlyFire_Flat, Grid and StaleGuard on, Cache off (not patched), Targeting SelfCheckFraction 0.5, dumps every 45 s; FriendlyFire_Grid switched off at 06:08:20)
PASSED: FriendlyFire_Flat mismatch total 0 over 2 000 812 checks, with about 0.83 M checks in the Grid-on half and 1.08 M in the Grid-off half. other -> vanilla 0, stale -> vanilla 0. Flat served exactly the calls the Grid bypassed. 0 exceptions, nothing FAILED. FriendlyFire_Flat goes into new2.

The measure window (06:05:35-06:11:05) split into 165 s with Grid on and 165 s with Grid off. Plateau: 424 lab units on average, 54.0 fps, work 18.4 ms, deaths 11.9/s.

| window end | Grid | calls q/s | Grid served (share) | Flat served | in range / cone-skipped / to vanilla tail per call | fills/s | Flat checks/s (% of served) | Flat checks total | mismatch |
|---|---|---:|---|---:|---|---:|---|---:|---:|
| 06:05:20 | on | 12 169 | 2 (0 %) | 12 167 | 86.5 / 84.5 / 1.97 | 28 731 | 6 088 (50.0 %) | 87 611 | 0 |
| 06:06:06 | on | 10 863 | 355 (3 %) | 10 509 | 35.5 / 33.2 / 2.30 | 21 444 | 5 286 (50.3 %) | 352 750 | 0 |
| 06:06:50 | on | 12 859 | 4 541 (35 %) | 8 318 | 49.3 / 44.6 / 4.69 | 19 401 | 4 160 (50.0 %) | 513 806 | 0 |
| 06:07:36 | on | 12 579 | 7 082 (56 %) | 5 497 | 22.5 / 21.3 / 1.17 | 16 064 | 2 749 (50.0 %) | 740 591 | 0 |
| 06:08:20 | on | 13 828 | 5 960 (43 %) | 7 869 | 33.7 / 30.9 / 2.79 | 17 832 | 3 928 (49.9 %) | 917 985 | 0 |
| 06:09:06 | off | 13 844 | - | 13 844 (100 %) | 33.8 / 31.0 / 2.80 | 29 323 | 6 949 (50.2 %) | 1 197 070 | 0 |
| 06:09:50 | off | 12 116 | - | 12 116 | 33.1 / 30.1 / 3.01 | 26 031 | 6 090 (50.3 %) | 1 501 549 | 0 |
| 06:10:36 | off | 13 068 | - | 13 068 | 37.0 / 34.5 / 2.48 | 27 842 | 6 535 (50.0 %) | 1 788 543 | 0 |
| quit | | | | | | | | 2 000 812 | 0 |

- **Grid served share:** in the Grid-on half, the Grid's own share grew from 0 % to 35-56 % as the cluster spread out (slots 4/5 switched to "GRID (pays)", builds/s 38-56). Flat's grid count equals the Grid's bypass count in every window.
- **Mode line:**
  - visited/q 48-93 of a cell of 154-164 (31-59 %); to vanilla logic/q 1.2-2.8.
  - answered before (cache/grid) 4-27 753 per 5 s, while the Grid was on.
  - stale skipped 0, stale -> vanilla 0, other -> vanilla 0 in every window.
- **FriendlyFire_Grid:** 297 575 checks / 0 mismatches. StaleGuard: q/s 2-8, no checks.
- **Check rate:** the Targeting self-check samples at the configured 50 % on this path (no factor-10 gap).
- Diag_LogExceptions: 0 in all 11 windows and at quit.

Log: night1\b20_ff_check\Latest.log. NOW: b20_ff (N/F, 450/550) since 06:12.


## b20_ff (rig 2; build b20, 06:12-06:22, melee plateaus 450/550 [lab avg 428 / 524], arms N / F = N + FriendlyFire_Flat, 30 s blocks alternating, 4 F blocks per plateau, 5 s settle, SelfCheckFraction 0)
Verdict: FriendlyFire_Flat gives no measurable gain.
- **Frame work, F vs N:** +1.4 % (s.e. 2.6 %) at 450 and +3.6 % (s.e. 7.9 %) at 550, both inside the noise.
- **The method itself:** Target.CheckRiskOfFriendlyFire is unchanged within noise: 54.7 → 57.1 ms/s at 450 and 28.3 → 25.1 ms/s at 550.
- **Its parent:** VehicleTurret.OrderedUpdateAllTurrets 92.7 → 91.3 and 49.1 → 48.3 ms/s.
- **The item is active:** in the F blocks it serves 100 % of the Grid-bypassed calls.
- **Why there is little to save:** in this run vanilla CheckRiskOfFriendlyFire costs only 4.1-4.9 us/call, against the 7.4 us in the ~521-unit evidence. With the item's own work counted, nothing measurable is left. Note that PerfProbe's timing wraps the method including the item's prefix.

| plateau | arm | fps | work ms | F vs N paired (s.e., n) | CheckRiskOfFriendlyFire ms/s / calls/s / us/call | VehicleTurret.OrderedUpdateAllTurrets ms/s | BTEnemyTargetDetected ms/s | AIBaseAgent ms/s |
|---|---|---:|---:|---|---|---:|---:|---:|
| 450 | N | 66.9 | 14.45 | | 54.7 / 13 498 / 4.1 | 92.7 | 82.1 | 109.1 |
| 450 | F | 67.3 | 14.46 | +1.4 % (2.6 %, 4) | 57.1 / 12 310 / 4.6 | 91.3 | 84.2 | 111.1 |
| 550 | N | 27.0 | 37.80 | | 28.3 / 5 730 / 4.9 | 49.1 | 131.6 | 149.3 |
| 550 | F | 27.1 | 37.57 | +3.6 % (7.9 %, 4) | 25.1 / 6 199 / 4.0 | 48.3 | 125.7 | 143.7 |

Method numbers rest on 30-50 s of whole intervals per arm.

**Item Info / Mode lines, F blocks:**
- **450** (last window; Grid served 58 % here as the cluster spread):
  - q/s 12 606: Flat served 5 328 (the Grid-bypassed calls) and the Grid served 7 277.
  - per served call: in range 11.2, cone-skipped 9.8, to vanilla tail 1.33.
  - fills/frame 190; visited/q 39.
  - stale skipped 0, stale -> vanilla 0, other -> vanilla 0.
- **550:**
  - q/s 5 034-7 026, Flat share 100 % (Grid bypass 0 %).
  - per served call: in range 21.9-26.8, cone-skipped 18.8-22.7, to vanilla tail 3.1-4.1.
  - fills/frame 425-521; visited/q 68-80.
  - other -> vanilla 0.

**Guard (1 Hz).** No sign of a changed friendly-fire veto:

| plateau | arm | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |
|---|---|---:|---:|---:|---:|---:|
| 450 | N | 10.09 | 9.45 | 0.551 | 154.8 | 0.755 |
| 450 | F | 10.43 | 9.74 | 0.550 | 147.5 | 0.746 |
| 550 | N | 11.87 | 10.87 | 0.539 | 187.2 | 0.723 |
| 550 | F | 12.54 | 11.70 | 0.547 | 184.0 | 0.731 |

- Shots/unit-s are equal within 1.5 %. hits/s are -5 % / -2 %, inside the block spread.
- 0 exceptions, nothing FAILED.

Files: night1\b20_ff\ (report_1_step_table.txt, report_3_methods_by_arm.txt, report_4_guard_by_arm.txt).


## b19_pscan_check (rig 2; build b20, 06:22-06:29, melee 450, fixed arm new + CollectPreFiltered_Snapshot + ParallelScan + ParallelScan_Prefiltered, MultiCore and Targeting SelfCheckFraction 0.2, dumps every 45 s)
PASSED, with one gap: the emulated type-value path was not exercised.
- **Comparisons:** 13 649 full-path and 9 803 prefiltered self-checks, all identical; different 0 and type-value state different 0 in both Info lines.
- **Prefiltered path:** served 136-205/s.
- **Gap:** "emulated type values" was 0 in every window, so the new type-value emulation never ran in this melee.
- **Main-thread fallback:** "Unity ran N scans/s on the main thread" was 0 in every plateau window; 7/s only in the final window after cleanup.
- **Health:** no worker errors, Targeting mismatch totals all 0, nothing FAILED / NOT APPLIED, 0 exceptions.

| window | full captured/s | full served/s | prefiltered captured/s | prefiltered served/s | fallback/s (full / pref.) | full checks cum. | pref. checks cum. |
|---|---:|---:|---:|---:|---|---:|---:|
| 06:24:51 | 357 | 268 | 262 | 205 | dpsUnknown 1.8 / dpsUnknown 4 | | |
| 06:25:36 | 301 | 205 | 193 | 146 | dpsUnknown 0.4 / 3.6 | | |
| 06:26:20 | 278 | 181 | 184 | 136 | 0 / 0 | | |
| 06:27:06 | 305 | 210 | 211 | 158 | 0.4 / 0.2 | | |
| 06:27:51 | 263 | 167 | 184 | 140 | 0 / 0 | 10 246 | 7 152 |
| 06:28:37 | 304 | 203 | 186 | 139 | dpsUnknown 1.6 / 0 | 12 304 | 8 729 |
| quit | 60 | 39 | 0 | 0 | 0 | 13 649 | 9 803 |

**Round-2 counters** (06:27:51 / 06:28:37):

*Full path*
- Batches 15-16/s; staged entries 12-12.7 k/s.
- Dirty marks/s: attackers 152-164, current target 111-122, health 0.
- predicted 205-220/s (history skips 20, probes 6); corrected 8/s; rescored entries 21-28/s.
- missed 35-44/s; wasted 12-13/s, all creature (by tree: VehicleAttackGround).
- Main: staging 4.8-4.9 ms/s, serve 0.8-0.9 ms/s. Main-computed scans 4-5/s (0.06-0.18 ms/s). Waits 5-9/s (0.27-0.37 ms/s), timeouts 0. Worker 12.6-16.0 ms/s.
- No tooManyDirty, unsupported or carry* fallbacks appeared (round 1 had tooManyDirty 3-61/s).

*Prefiltered path*
- predicted 113-116/s; corrected 115-124/s; rescored entries 686-762/s.
- missed 17-21/s; wasted 10/s (creature 7, vehicle 2; trees AlienMove / AlienAttack).
- Main: staging 1.8-2.0 ms/s, serve 2.0 ms/s (main-computed 11-12/s, 0.7-0.8 ms/s). Worker 15.2 ms/s.

*Combined*
- Main staging (both batches) 6.7-6.8 ms/s, take-back 0.06 ms/s.

Other self-checks: Targeting CollectPreFiltered_Snapshot 3 323, Snapshot/Score/FastAdd 9 582, FriendlyFire_Grid 105 674, all with mismatch 0. The self-check costs 0.02-0.03 ms/s.

Decision: ParallelScan + ParallelScan_Prefiltered go into "all" (all = new + CollectPreFiltered_Snapshot + ParallelScan + ParallelScan_Prefiltered). Plans b19b (b19_pscan) and b19c (b20_best, b19_spread) are written. Log: night1\b19_pscan_check\Latest.log.


## b19_pscan (rig 2; build b20, 06:30-06:45, melee plateaus 450/550 [lab avg 430 / 527], arms B = new + CollectPreFiltered_Snapshot / P = B + ParallelScan (round 2) / PP = P + ParallelScan_Prefiltered, 30 s blocks in rotated order, 4 P and 4 PP blocks per plateau, 5 s settle, SelfCheckFraction 0)
Verdict: ParallelScan round 2 is clearly positive and stronger than round 1.
- **Frame work, P vs B:** -7.7 % (s.e. 1.5 %) at 450 and -30.4 % (s.e. 2.9 %) at 550; fps 25.2 → 36.0 at 550. Round 1 on b17 gave -2.2 % / -16.3 %.
- **Adding the prefiltered path (PP):** frame work is -5.6 % (s.e. 1.6 %) / -31.5 % (s.e. 4.2 %) vs B. That is not separable from P at frame level, although it takes another 15-18 ms/s off AIBaseAgent.
- tooManyDirty disappeared (0 lines in the whole run).
- Guard metrics are unchanged within noise. 0 exceptions, nothing FAILED.

| plateau | arm | fps | work ms | paired vs B (s.e., n) | RequestScan ms/s | BTEnemyTargetDetected ms/s | CollectDetectedEnemies ms/s (us/call) | CollectPreFilteredEnemies ms/s (us/call) | AIBaseAgent ms/s |
|---|---|---:|---:|---|---:|---:|---|---|---:|
| 450 | B | 66.5 | 14.79 | | 65.4 | 69.4 | 45.7 (99.8) | 14.2 (102.8) | 97.6 |
| 450 | P | 71.2 | 13.64 | -7.7 % (1.5 %, 4) | 31.1 | 35.2 | 5.5 (11.5) | 18.3 (107.7) | 70.0 |
| 450 | PP | 69.8 | 13.93 | -5.6 % (1.6 %, 4) | 16.6 | 20.1 | 5.0 (10.8) | 2.8 (17.0) | 55.3 |
| 550 | B | 25.2 | 40.37 | | 105.6 | 112.0 | 73.1 (130.3) | 24.7 (131.3) | 130.3 |
| 550 | P | 36.0 | 28.24 | **-30.4 % (2.9 %, 4)** | 39.8 | 44.8 | 6.4 (11.0) | 24.3 (132.5) | 73.8 |
| 550 | PP | 36.4 | 27.79 | **-31.5 % (4.2 %, 4)** | 20.1 | 25.1 | 6.3 (11.6) | 2.9 (16.2) | 55.8 |

- VehicleTurret.OrderedUpdateAllTurrets and CheckRiskOfFriendlyFire rise in ms/s in P/PP (550: 60.6 → 78-85 and 36.7 → 48-54 ms/s). This is because there are more frames per second (calls/s 6 276 → 9 453-10 125), not because the per-call cost changed (5.9 → 5.1-5.3 us).
- Method numbers rest on 30-50 s of whole intervals per arm.

**Status lines per mode, per second** (range over the 3-4 windows in each arm and plateau):

| | 450 P full | 450 PP full | 450 PP prefiltered | 550 P full | 550 PP full | 550 PP prefiltered |
|---|---|---|---|---|---|---|
| captured / served | 401-484 / 366-452 | 407-487 / 380-445 | 137-158 / 130-152 | 448-644 / 422-618 | 483-617 / 455-593 | 163-178 / 155-174 |
| served share | 91-94 % | 92-93 % | 95-97 % | 94-96 % | 94-96 % | 95-98 % |
| corrected | | | 76-113 | | | 110-126 |
| emulated type values | 0 | 0 | 0 | 0 | 0 | 0 |
| tooManyDirty / notReady | 0 / 0-0.4 | 0 / 0-0.2 | 0 / 0 | 0 / 0-0.4 | 0 / 0-0.2 | 0 / 0-0.2 |
| missed | 17-22 | 15-27 | 12-24 | 19-22 | 12-23 | 11-15 |
| wasted (all of it) | 12-18 creature | 15-17 creature | 4-9 (soldier 0-2, vehicle 1-3, creature 2-4) | 16-21 creature | 14-19 creature | 3-8 (soldier 1-2, vehicle 1-3, creature 2-4) |
| dirty marks attackers / current target | 139-177 / 113-139 | 153-167 / 121-131 | | 191-213 / 144-160 | 185-212 / 144-166 | |
| main staging / serve ms/s | 5.1-6.0 / 1.1-1.7 | 5.0-5.4 / 1.0-2.3 | 0.9-1.3 / 1.3-1.6 | 5.9-6.7 / 1.1-1.6 | 6.0-7.1 / 1.5-1.7 | 1.3-1.5 / 2.0-2.3 |
| main-computed scans (ms/s) | 4-12/s (0.1-0.4) | 3-28/s (0.04-1.3) | 6-13/s (0.3-0.7) | 4-5/s (0.15-0.25) | 2-4/s (0.07-0.3) | 8-9/s (0.55-0.7) |
| waits (ms/s), timeouts | 6-14/s (0.14-0.71), 0 | 7-10/s (0.21-0.55), 0 | 4-7/s (0.11-0.21) | 4-7/s (0.18-0.34), 0 | 9-11/s (0.45-0.64), 0 | 6-11/s (0.3-0.4) |
| worker ms/s | 24-30 | 26-29 | 8-12 | 34-49 | 36-47 | 14-17 |
| main staging, both batches | 5.1-6.0 | 6.1-6.6 | | 5.9-6.7 | 7.4-8.4 | |

- **Waste by tree** (run totals): full path VehicleAttackGround 8 704; prefiltered AlienMove 540, AlienAttack 450.
- "Unity ran on the main thread": 0/s in every window.
- **Compared with round 1 (b17, 550):**
  - served share: ~79 % → 94-96 %.
  - wasted: 55/s → 14-21/s.
  - tooManyDirty: 12/s → 0.
  - staging + serve: 6.8-11.7 → 7.0-8.3 ms/s (P).
  - worker: 29-36 → 34-49 ms/s.
- The emulated type-value path was never used, as in the check.

**Guard (1 Hz):**

| plateau | arm | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |
|---|---|---:|---:|---:|---:|---:|
| 450 | B | 9.38 | 8.67 | 0.755 | 160.1 | 0.767 |
| 450 | P | 9.55 | 8.99 | 0.730 | 160.7 | 0.757 |
| 450 | PP | 8.99 | 8.38 | 0.797 | 161.5 | 0.755 |
| 550 | B | 10.51 | 9.85 | 0.598 | 161.8 | 0.742 |
| 550 | P | 10.81 | 10.15 | 0.631 | 172.7 | 0.750 |
| 550 | PP | 10.91 | 10.10 | 0.634 | 169.8 | 0.754 |

- Shots/unit-s vary ±6 % between arms with mixed sign at 450 and +5-6 % in both P arms at 550. The P arms run at higher fps, which plausibly lets more shot ticks happen; kills, deaths and with-target are unchanged.

Files: night1\b19_pscan\ (report_1_step_table.txt, report_3_methods_by_arm.txt, report_4_guard_by_arm.txt; status lines in Latest.log). Headline b20_best (off/def/new/new2/all, all = new2 + ParallelScan + ParallelScan_Prefiltered) has been running since 06:45.


## b20_best — headline (rig 2; build b20 md5 82F1D0C7..., 06:45-07:03, melee plateaus 450/550 [lab avg 427 / 523], 30 s blocks, 3 rotated cycles of 5, 5 s settle)
Arms:
- **off** = every item off.
- **def** = shipped b10 defaults.
- **new** = def + CollectEnemies_Snapshot / SnapshotScore / FastAdd + DeadAgentHalt (ClearAim false).
- **new2** = new + CollectPreFiltered_Snapshot. WheelJointWriteGuard and FriendlyFire_Flat were left out (exact, but no gain).
- **all** = new2 + ParallelScan (round 2) + ParallelScan_Prefiltered.

Verdict: at the 550 plateau, "all" cuts frame work by 65.5 % vs off (s.e. 3.1 %) and by 30.9 % vs new (s.e. 7.4 %), with fps 11.9 → 31.3. At 450 the gain vs new is -7.9 % (s.e. 7.8 %), inside the noise. new2 vs new is -0.2 % / -4.5 %, inside the noise. 0 exceptions, 2/2 rows ok, nothing FAILED.

| plateau | arm | fps | work ms | paired vs off (s.e., n) | paired vs new (s.e., n) |
|---|---|---:|---:|---|---|
| 450 | off | 35.3 | 28.79 | | +88.3 % (6.1 %, 3) |
| 450 | def | 54.7 | 18.12 | -37.6 % (2.1 %, 3) | +19.0 % (6.0 %, 3) |
| 450 | new | 63.8 | 15.25 | -47.7 % (1.8 %, 3) | |
| 450 | new2 | 63.7 | 15.45 | -45.6 % (2.2 %, 4) | -0.2 % (4.1 %, 4) |
| 450 | all | 68.7 | 14.23 | -49.4 % (3.6 %, 3) | -7.9 % (7.8 %, 3) |
| 550 | off | 11.9 | 88.54 | | +86.0 % (3.6 %, 4) |
| 550 | def | 15.6 | 66.46 | -27.0 % (3.5 %, 3) | +37.7 % (7.8 %, 3) |
| 550 | new | 21.2 | 48.17 | -47.5 % (0.7 %, 3) | |
| 550 | new2 | 22.9 | 44.90 | -51.2 % (2.6 %, 3) | -4.5 % (2.2 %, 3) |
| 550 | all | 31.3 | 32.48 | **-65.5 % (3.1 %, 3)** | **-30.9 % (7.4 %, 3)** |

(s.e. in % of the reference arm's work.) The main thread is saturated in every arm (973-1051 ms/s), so the gains appear as fps.

**Targeting methods at 550, ms/s:**

| method | off | def | new | new2 | all |
|---|---:|---:|---:|---:|---:|
| RequestScan | 205.0 | 206.7 | 126.4 | 113.9 | 24.9 |
| BTEnemyTargetDetected | 216.0 | 215.2 | 133.7 | 121.5 | 30.8 |
| CollectDetectedEnemies | 120.5 | 126.2 | 76.4 | 78.8 | 9.4 |
| CollectPreFilteredEnemies | 75.0 | 71.0 | 42.2 | 27.1 | 3.8 |
| AIBaseAgent.OrderedUpdateAllAgents | 231.9 | 232.1 | 150.5 | 138.6 | 60.3 |

At 450 the same order: RequestScan 140/152/89/75/20, AIBaseAgent 172/188/121/107/59.

**Guard (1 Hz):**

| plateau | arm | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |
|---|---|---:|---:|---:|---:|---:|
| 450 | off | 11.01 | 10.05 | 0.773 | 189.7 | 0.729 |
| 450 | def | 9.77 | 9.05 | 0.778 | 190.0 | 0.721 |
| 450 | new | 10.65 | 10.00 | 0.777 | 201.6 | 0.737 |
| 450 | new2 | 11.69 | 10.60 | 0.797 | 192.0 | 0.721 |
| 450 | all | 10.99 | 10.08 | 0.810 | 195.2 | 0.742 |
| 550 | off | 11.87 | 11.02 | 0.695 | 206.5 | 0.710 |
| 550 | def | 11.19 | 10.03 | 0.657 | 193.1 | 0.716 |
| 550 | new | 13.08 | 12.08 | 0.649 | 209.1 | 0.713 |
| 550 | new2 | 12.93 | 11.82 | 0.727 | 220.0 | 0.682 |
| 550 | all | 13.36 | 12.36 | 0.709 | 224.4 | 0.739 |

- With-target share 0.68-0.74 and shots/unit-s within ±6 % of off in every arm.
- At 550, "all" (31 fps) shows +12 % deaths and +9 % hits/s vs off (12 fps). This is consistent with more simulation ticks per second at higher fps; it is not a sign of changed target choice (with-target is unchanged).

Note on variation between separate starts: new at 550 ran at 21.2 fps here, against 25.2 (B) in b19_pscan and 27.3 in b15_best_plat. Only the paired comparisons within this run count.

Files: night1\b20_best\ (report_1_step_table.txt vs off, report_2_step_table.txt vs new, report_4_guard_by_arm.txt, report_5_methods_by_arm.txt). NOW: b19_spread (4-front sustain 450/550/650, off(+census)/def/new/all) since 07:03.


## b19_spread (rig 2; build b20, 07:03-07:26; ramp_sustain geometry: 4 fronts, damage on, respawn 2 s, structures respawn; plateaus 450/550/650 [lab avg 422 / 518 / 614]; arms off (+ read-only Physics.Census) / def / new / all = new + CollectPreFiltered_Snapshot + ParallelScan + ParallelScan_Prefiltered; 30 s blocks, 3 rotated cycles of 4, 5 s settle)
Verdict: on the spread layout the new items hold up. new vs off is -40 / -52 / -41 % and new vs def -17 / -16 / -21 % (all paired; s.e. 1.6-2.3 %, except new vs def at 550: 9.5 %). "all" adds less here than in the dense melee:
- **all vs new, paired:** +1.9 % (s.e. 2.5 %) at 450, -5.1 % (s.e. 7.1 %) at 550 and -8.1 % (s.e. 2.5 %) at 650. Only the 650 value is outside the noise.
- **all vs def:** -7.4 % / -23.7 % / -28.4 %.
- **Wrecks are the same in every arm:** deadU is 24-27 % of the census units, so the gain does not come from a smaller wreck population.
- 0 exceptions, 0 front stalls, 3/3 rows ok.

| plateau | arm | fps | work ms | paired vs off (s.e., n) | paired vs def (s.e., n) |
|---|---|---:|---:|---|---|
| 450 | off | 50.3 | 19.95 | | +40.5 % (9.9 %, 3) |
| 450 | def | 67.2 | 14.31 | -27.7 % (7.4 %, 3) | |
| 450 | new | 77.0 | 12.06 | -40.0 % (2.0 %, 3) | -17.1 % (1.7 %, 3) |
| 450 | all | 76.4 | 12.24 | -39.6 % (1.6 %, 4) | -7.4 % (6.1 %, 4) |
| 550 | off | 19.3 | 53.26 | | +68.6 % (10.5 %, 3) |
| 550 | def | 33.2 | 30.67 | -43.4 % (4.5 %, 3) | |
| 550 | new | 38.6 | 26.23 | -52.3 % (1.6 %, 3) | -15.5 % (9.5 %, 3) |
| 550 | all | 43.3 | 23.23 | -54.3 % (5.0 %, 4) | -23.7 % (1.9 %, 4) |
| 650 | off | 10.6 | 98.75 | | +31.8 % (2.1 %, 3) |
| 650 | def | 13.9 | 74.73 | -24.7 % (2.0 %, 4) | |
| 650 | new | 17.8 | 57.85 | -41.1 % (1.6 %, 3) | -20.7 % (2.3 %, 3) |
| 650 | all | 19.0 | 54.11 | -45.3 % (1.4 %, 3) | -28.4 % (2.9 %, 3) |

(s.e. in % of the reference arm's work.)

**Compared with the melee headline (b20_best, 550):** all vs off -65.5 % and all vs new -30.9 % in the melee, against -54.3 % and -5.1 % here. In the 4-front layout fewer agents per front see each other, so the targeting scans that ParallelScan moves to the workers are a smaller share of the frame.

**Wreck population** (census `| dead` segment, median per arm and plateau; share = deadU / (deadU + aliveU)):

| plateau | arm | deadU | aliveU | deadU share | deadDyn (awake) | dead soldier / creature / wheeled |
|---|---|---:|---:|---:|---|---|
| 450 | off | 174 | 467 | 27.1 % | 571 (512) | 42 / 69 / 59 |
| 450 | def | 166 | 467 | 26.2 % | 546 (509) | 42 / 68 / 55 |
| 450 | new | 170 | 465 | 26.7 % | 564 (522) | 41 / 68 / 58 |
| 450 | all | 170 | 463 | 26.9 % | 575 (537) | 38 / 76 / 61 |
| 550 | off | 184 | 562 | 24.6 % | 632 (597) | 48 / 74 / 65 |
| 550 | def | 205 | 562 | 26.7 % | 662 (612) | 56 / 71 / 67 |
| 550 | new | 195 | 555 | 26.0 % | 609 (566) | 50 / 77 / 64 |
| 550 | all | 197 | 569 | 25.7 % | 647 (600) | 52 / 73 / 68 |
| 650 | off | 222 | 666 | 25.0 % | 740 (710) | 51 / 86 / 75 |
| 650 | def | 232 | 659 | 26.0 % | 746 (702) | 57 / 85 / 79 |
| 650 | new | 213 | 659 | 24.4 % | 751 (707) | 53 / 81 / 79 |
| 650 | all | 235 | 652 | 26.5 % | 759 (730) | 60 / 82 / 79 |

- The wreck share stays at 24-27 % in every arm and plateau, and 90-96 % of the dead bodies are awake in every arm.
- None of the arms reduces the wreck population. DeadAgentHalt in new/all only stops the dead agents' AI and leaves the bodies; DeadSettle and DeadDisposeEarly are off in every arm.
- So the gain of new and all over off/def comes from targeting and agent work, not from fewer bodies.
- The `off` arm keeps Physics.Census on (read-only) so that deadU is visible.

**Guard (1 Hz):**

| plateau | arm | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |
|---|---|---:|---:|---:|---:|---:|
| 450 | off | 13.31 | 11.85 | 0.969 | 248.6 | 0.605 |
| 450 | def | 13.37 | 12.00 | 0.972 | 257.0 | 0.610 |
| 450 | new | 12.73 | 11.51 | 0.930 | 243.3 | 0.591 |
| 450 | all | 13.69 | 12.02 | 0.950 | 243.2 | 0.599 |
| 550 | off | 14.40 | 12.89 | 0.805 | 252.1 | 0.605 |
| 550 | def | 15.86 | 14.47 | 0.870 | 276.3 | 0.608 |
| 550 | new | 15.08 | 13.65 | 0.844 | 287.0 | 0.596 |
| 550 | all | 15.59 | 13.89 | 0.920 | 295.0 | 0.615 |
| 650 | off | 15.10 | 13.45 | 0.705 | 277.9 | 0.605 |
| 650 | def | 16.52 | 14.46 | 0.685 | 281.3 | 0.594 |
| 650 | new | 16.83 | 15.21 | 0.742 | 301.9 | 0.608 |
| 650 | all | 17.77 | 16.23 | 0.823 | 308.9 | 0.611 |

- With-target share is 0.59-0.62 in every arm.
- Shots/unit-s and deaths rise with fps at 550/650: off runs at 10-19 fps, all at 19-43 fps. At 450, where all arms run above 50 fps, everything is within ±4 %.

Files: night1\b19_spread\ (report_1_step_table.txt vs off, report_2_step_table.txt vs def, report_4_guard_by_arm.txt, report_5_tag_by_arm.txt census dead). all vs new paired from `night1\step_table.py b19_spread --arms new,all`. NOW: b15_eveh (07:26), then b15_snap_check4.


## b15_eveh (rig 2 reporting; build b15, first rig's plan, 07:26-07:47, melee-dead14 plateaus 450/550 [lab avg 427 / 523], arms A defaults / D = A + DeadAgentHalt / E = D + DeadSettle + DeadNoUnitContact, with DeadNoContactCreatures and DeadNoContactVehicles true; 60 s blocks A,D,E,A,E,D, 20 s settle)
Verdict: the allow-list mask works.
- The applied mask is exactly the 4 allow-list layers, Character / Vehicle Avoidance / Ignore Raycast Avoidance / Invisible Walls Avoidance. CollideWithStatic is gone.
- The vehicles are now included, but E adds no gain over D.
- E vs D, paired: +18.2 % (s.e. 4.7 %, n 4) at 450, i.e. worse, and -1.8 % (s.e. 0.2 %, n 2) at 550.
- D vs A: -19.2 % / -12.7 %. That is in line with b14_dead / b15 (-4.9 % / -15.7 % there; separate starts).
- 0 exceptions, 2/2 rows ok.

| plateau | arm | fps | work ms | paired vs A (s.e., n) | paired vs D (s.e., n) |
|---|---|---:|---:|---|---|
| 450 | A | 49.9 | 20.09 | | |
| 450 | D | 60.3 | 16.36 | -19.2 % (4.2 %, 3) | |
| 450 | E | 52.6 | 18.97 | -3.6 % (6.6 %, 4) | +18.2 % (4.7 %, 4) |
| 550 | A | 16.5 | 62.74 | | |
| 550 | D | 18.8 | 54.91 | -12.7 % (2.5 %, 4) | |
| 550 | E | 18.4 | 56.11 | -10.1 % (2.2 %, 2) | -1.8 % (0.2 %, 2) |

**deadcontact** (E blocks, median per 10 s):

| plateau | appliedUnits | appliedBodies | skippedKind (soldiers) | trackedNow |
|---|---:|---:|---:|---:|
| 450 | 92 | 305 | 17 | 147 |
| 550 | 105 | 351 | 18 | 178 |

restored 0 in both.

**Census dead segment** (median):

| plateau | arm | deadU | aliveU | deadDyn | deadSl | deadAwake |
|---|---|---:|---:|---:|---:|---:|
| 450 | A | 143 | 459 | 442 | 20 | 424 |
| 450 | D | 123 | 468 | 434 | 20 | 411 |
| 450 | E | 145 | 464 | 481 | 55 | 416 |
| 550 | A | 149 | 563 | 464 | 19 | 451 |
| 550 | D | 151 | 567 | 502 | 19 | 480 |
| 550 | E | 166 | 560 | 538 | 60 | 476 |

DeadSettle in E puts only about 35-40 more bodies to sleep (deadSl 20 → 55-60). 87-90 % of dead bodies stay awake.

**Guard:**

| plateau | arm | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |
|---|---|---:|---:|---:|---:|---:|
| 450 | A | 11.12 | 10.01 | 0.580 | 187.1 | 0.749 |
| 450 | D | 10.18 | 9.02 | 0.614 | 179.3 | 0.750 |
| 450 | E | 11.02 | 9.86 | 0.603 | 187.8 | 0.759 |
| 550 | A | 11.73 | 10.32 | 0.489 | 184.7 | 0.722 |
| 550 | D | 12.88 | 11.47 | 0.492 | 200.3 | 0.721 |
| 550 | E | 13.22 | 11.87 | 0.410 | 179.4 | 0.694 |

E at 550 shows shots/unit-s -16 % and with-target 0.694 vs A. This rests on only 2 E blocks (78 s), so I flag it but do not treat it as a finding.

Files: night1\b15_eveh\ (report_1_step_table.txt). E vs D from `night1\step_table.py b15_eveh --arms D,E --settle 20`; guard from `night1\guard_by_arm.py night1\b15_eveh 20` (run from measure\). NOW: b15_snap_check4 (07:47), then b20_dispose_paired, b20_parked_spaced, b20_best2.

## b15_snap_check4 (rig 2 reporting; build b15, first rig's plan, 07:47-07:55, 4-front spread in god mode held at 800 lab units [avg 778], Targeting SelfCheckFraction 0.5, dumps every 45 s)
Verdict: 0 mismatches on every Targeting item (10 670 CollectEnemies_Snapshot checks; 21 156 FriendlyFire_Grid checks in the last plateau window alone). The goal of >= 1 000 checks on the grid-pruned path is probably not met.
- The grid pruned only 4.2-6.0 % of CollectEnemies queries at 800 units (grid 23-36 q/s of 520-691).
- Assuming the check sampling is uniform across paths, that gives about 500 pruned-path checks.
- 0 exceptions, 1/1 row ok. Plateau 24.3 fps, work 41.1 ms.

| window | CollectEnemies q/s | grid-pruned / bypass / vanilla | pruned share | Snapshot checks in window | mismatch total |
|---|---:|---|---:|---:|---:|
| 07:49:47 | 691 | 36 / 647 / 9 | 5.2 % | 1 759 | 0 |
| 07:50:32 | 548 | 23 / 521 / 4 | 4.2 % | 1 318 | 0 |
| 07:51:18 | 528 | 29 / 492 / 7 | 5.5 % | 1 281 | 0 |
| 07:52:02 | 579 | 32 / 541 / 7 | 5.5 % | 1 443 | 0 |
| 07:52:47 | 621 | 27 / 593 / 1 | 4.3 % | 1 561 | 0 |
| 07:53:33 | 520 | 23 / 494 / 2 | 4.4 % | 1 290 | 0 |
| 07:54:18 | 529 | 32 / 491 / 7 | 6.0 % | 1 375 | 0 |
| total (incl. ramp/cleanup) | | | | 10 670 | 0 |

- The b15 status line prints per-interval check counts, not cumulative ones.
- SnapshotScore and FastAdd logged q/s 0 and check 0 here (their counters sit at "grid 21-38 k", but their own self-check never sampled). God mode deals no damage, so the score path's type-value states change little; this run therefore checks only CollectEnemies_Snapshot.

Log: night1\b15_snap_check4\Latest.log. NOW: b20_dispose_paired (07:55), then b20_parked_spaced, b20_best2.

## b20_dispose_paired (rig 2; build b20, 07:55-08:15; melee 550 then 4-front spread 550 [lab avg 514 / 518]; arms all (as in b20_best) / allD = all + DeadDisposeEarly proportional [Seconds 5, FpsLow 25, FpsHigh 45, Smooth 10 s, Slew 0.5 s/s]; 120 s blocks all, allD, allD, all cycled, last 60 s of each block evaluated)
Verdict: proportional DeadDisposeEarly does remove wrecks, but the frame-work effect depends on the layout.
- **Wrecks removed:** deadU 219 → 153 in the melee (-30 %, dead share 28.1 → 21.4 %) and 205 → 171 in the spread (-17 %, 26.9 → 23.6 %).
- **Melee frame work:** allD vs all -10.5 % (s.e. 1.3 %, n 2); fps 34.5 → 41.7.
- **Spread frame work:** -2.2 % (s.e. 7.9 %, n 3), i.e. no measurable gain.
- **Caveat:** the block layout did not interleave as intended. Each scenario saw a pair of one arm followed by a pair of the other (melee: all only 14 s before allD, then allD, allD, all, all; spread: allD, allD, all, all, then 14 s of allD). The melee -10.5 % therefore rests on 2 blocks and is not protected against a time trend within the plateau.
- Guard metrics unchanged. 0 exceptions, 2/2 rows ok.

| plateau | arm | fps | work ms | allD vs all paired (s.e., n) | deaddispose target s: mean (min-max) | smoothed fps (controller) | trackedNow | deadU | dead share | world units |
|---|---|---:|---:|---|---|---:|---:|---:|---:|---:|
| melee 550 | all | 34.5 | 29.44 | | - | - | - | 219 | 28.1 % | 780 |
| melee 550 | allD | 41.7 | 24.33 | -10.5 % (1.3 %, 2) | 10.32 (9.09-11.37) | 35.3 | 153 | 153 | 21.4 % | 713 |
| spread 550 | all | 47.4 | 21.18 | | - | - | - | 205 | 26.9 % | 758 |
| spread 550 | allD | 47.6 | 21.16 | -2.2 % (7.9 %, 3) | 12.51 (11.19-13.51) | 40.0 | 165 | 171 | 23.6 % | 721 |

- deaddispose values are over the 10 s lines in the evaluated last 60 s of the allD blocks (12 / 14 lines).
- The controller sits between its ends: the target follows the smoothed fps of 35-40, i.e. between FpsLow 25 and FpsHigh 45, at 9-13.5 s.
- deadU / world units are means of the census lines and 1 Hz samples in the evaluated parts.

**Guard (1 Hz, evaluated parts):**

| plateau | arm | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |
|---|---|---:|---:|---:|---:|---:|
| melee 550 | all | 16.81 | 15.89 | 0.693 | 215.1 | 0.863 |
| melee 550 | allD | 16.98 | 15.81 | 0.711 | 224.4 | 0.837 |
| spread 550 | all | 15.42 | 14.10 | 0.892 | 296.2 | 0.612 |
| spread 550 | allD | 15.63 | 14.23 | 0.865 | 295.0 | 0.613 |

Reading: in the dense melee the wrecks sit inside the fight, and removing about 66 of them gives a real-looking -10 % in work. In the spread they cost little (consistent with b19_spread, where the same wreck share was in every arm and the fps differences came from targeting). A properly interleaved repeat (more, shorter-but-≥90 s blocks) would be needed before quoting the melee number as a firm effect.

Files: night1\b20_dispose_paired\ (report_1_step_table.txt, report_2_guard_by_arm.txt, report_3/4 tag_by_arm census/deaddispose). The table above comes from the new tool `night1\dispose_by_arm.py b20_dispose_paired --settle 60`. NOW: b20_parked_spaced (08:15), then b20_best2.

## b20_parked_spaced (rig 2; build b20, 08:15-08:27; scenarios_n1_parked spaced-any 4x75 [299 spawned; lots mean 11-13 deg, max 21-29 deg] and spaced-slope 6x50 [300; lots mean 10-11 deg, max 18-19 deg]; arms A defaults / H = IdleForceSleep + AvoidanceSkipOntoResting + AvoidanceNoIdleDetour + ParkedWheelHold; 60 s blocks A,H,H,A cycled, 20 s settle)
Verdict: the parked-vehicle set H works on spaced lots as it did on the tight grid.
- **Wheeled asleep:** 44 → 99 % (any) and 14 → 95 % (slope).
- **PhysX simulate:** -75 % (any) and -70 % (slope).
- **Frame work, paired vs A:** -27 % (s.e. 20 %, n 2-3) and -22 % (s.e. 5 %).
- **Parked drift on the slope lots:** xzMax 6 mm, xzMean < 1 mm, dyMax 1-2 mm over about 310-320 force-slept vehicles. The vehicles stay put.
- **Frame cap:** the server sat at the ~118 fps cap in both arms, so only work ms and section times show the difference.
- 0 exceptions, 2/2 rows ok.

| scenario | arm | work ms (n blocks) | H vs A paired (s.e.) | wheeled asleep | PhysX simulate ms/s | FixedUpdate ms/s | ordered fixed (all) | CollisionAvoidance fixed |
|---|---|---:|---|---:|---:|---:|---:|---:|
| spaced-any | A | 2.71 (2) | | 44 % | 91.6 | 144.7 | 52.5 | 6.6 |
| spaced-any | H | 2.01 (3) | -27.2 % (19.8 %) | 99 % | 22.7 | 64.6 | 27.2 | 5.1 |
| spaced-slope | A | 2.95 (2) | | 14 % | 118.0 | 181.0 | 62.5 | 6.6 |
| spaced-slope | H | 2.24 (3) | -21.6 % (5.0 %) | 95 % | 35.7 | 82.3 | 32.5 | 5.9 |

**H counters** (≥ 20 s after a flip):
- **idlesleep:** forcedNow 303-316, forcedSleep 0-1, reSleep 0-1 per 10 s, blocked about 250, released 0.
- **drift:** xzMax 0.006 m, xzMean 0-0.001 m, dyMax 0.001-0.002 m, n 310-320, identical in both scenarios.
- **wheelhold** (median per 10 s): heldVehicles 0.5, wheelsSlept 1, syncs 0-0.5, syncSkipped 0, leftAwakeFast/Force 0.
- **avoidresting** (last line): any calls 22 221 / skip 22 176 (offNav 45); slope calls 31 970 / skip 31 880 (offNav 50, keptMoving 20).
- A has no drift line: drift is tracked only for force-slept vehicles, as in phys_b10.

Comparison: in the first rig's parked_v2 run (b10), spaced-slope F (without ParkedWheelHold) gave -13.9 % (s.e. 3 %) with 67-73 % asleep. H on b20 reaches 95-99 % asleep and -22 to -27 %.

Files: night1\b20_parked_spaced\ (report_1_physics_report.txt, report_2_ab_report.txt). wheelhold from `night1\tag_by_arm.py night1\b20_parked_spaced wheelhold` (run from measure\). NOW: b20_best2 (08:27), then b20_dispose_paired2 (latest start 09:10).

## b20_best2 — headline repeat (rig 2; build b20, 08:27-08:46, same plan as b20_best with another arm rotation: new, all, off, def, new2 / all, def, new2, off, new / new2, off, new, all, def; melee 450/550 [lab avg 425 / 521])
Verdict: the second sample confirms the headline.
- **all vs off:** -55.8 % / -65.2 % (b20_best: -49.4 % / -65.5 %).
- **all vs new:** -19.2 % / -25.1 % (b20_best: -7.9 % / -30.9 %).
- **new2 vs new:** again inside the noise (+0.9 % / +1.2 %).
- 0 exceptions, 2/2 rows ok.

| plateau | arm | fps | work ms | paired vs off (s.e., n) | paired vs new (s.e., n) |
|---|---|---:|---:|---|---|
| 450 | off | 32.6 | 31.39 | | +82.9 % (10.2 %, 3) |
| 450 | def | 50.7 | 19.90 | -36.0 % (2.9 %, 4) | +14.0 % (5.5 %, 4) |
| 450 | new | 57.1 | 17.38 | -48.1 % (3.1 %, 3) | |
| 450 | new2 | 58.7 | 16.94 | -36.7 % (4.4 %, 3) | +0.9 % (4.8 %, 3) |
| 450 | all | 68.8 | 14.25 | -55.8 % (5.7 %, 3) | -19.2 % (1.2 %, 3) |
| 550 | off | 11.3 | 92.19 | | +120.8 % (3.1 %, 3) |
| 550 | def | 15.5 | 66.84 | -27.1 % (0.3 %, 3) | +59.4 % (1.8 %, 3) |
| 550 | new | 24.6 | 41.57 | -55.3 % (1.1 %, 3) | |
| 550 | new2 | 24.2 | 42.31 | -53.6 % (2.3 %, 3) | +1.2 % (3.3 %, 3) |
| 550 | all | 32.1 | 31.59 | -65.2 % (1.8 %, 4) | -25.1 % (4.0 %, 4) |

### Spread over the two samples (b20_best / b20_best2), paired frame work

| plateau | arm | fps | vs off | vs new |
|---|---|---|---|---|
| 450 | off | 35.3 / 32.6 | | |
| 450 | def | 54.7 / 50.7 | -37.6 / -36.0 % | +19.0 / +14.0 % |
| 450 | new | 63.8 / 57.1 | -47.7 / -48.1 % | |
| 450 | new2 | 63.7 / 58.7 | -45.6 / -36.7 % | -0.2 / +0.9 % |
| 450 | all | 68.7 / 68.8 | -49.4 / -55.8 % | -7.9 / -19.2 % |
| 550 | off | 11.9 / 11.3 | | |
| 550 | def | 15.6 / 15.5 | -27.0 / -27.1 % | +37.7 / +59.4 % |
| 550 | new | 21.2 / 24.6 | -47.5 / -55.3 % | |
| 550 | new2 | 22.9 / 24.2 | -51.2 / -53.6 % | -4.5 / +1.2 % |
| 550 | all | 31.3 / 32.1 | -65.5 / -65.2 % | -30.9 / -25.1 % |

- **At 550 the two samples agree to within a few points:** all vs off -65 %, def vs off -27 %, all vs new -25 to -31 %. new vs off differs by 8 points (-47.5 / -55.3 %).
- **At 450 the spread is larger:** new2 vs off -46 / -37 %, all vs new -8 / -19 %. Paired values for one arm differ between runs by up to about 10 points, more than their within-run s.e. suggests.
- **Two-sample means:**
  - all vs off: -52.6 % at 450, -65.4 % at 550.
  - all vs new: -13.6 % at 450, -28.0 % at 550.
  - new vs off: -47.9 % at 450, -51.4 % at 550.
  - def vs off: -36.8 % at 450, -27.1 % at 550.

**Guard (1 Hz), with-target 0.69-0.77 in every arm:**

| plateau | arm | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |
|---|---|---:|---:|---:|---:|---:|
| 450 | off | 12.33 | 11.45 | 1.077 | 209.4 | 0.765 |
| 450 | def | 11.60 | 10.93 | 1.034 | 223.7 | 0.747 |
| 450 | new | 11.56 | 10.89 | 1.076 | 219.6 | 0.734 |
| 450 | new2 | 11.95 | 11.30 | 1.079 | 219.8 | 0.741 |
| 450 | all | 11.95 | 11.00 | 1.136 | 226.3 | 0.751 |
| 550 | off | 13.25 | 12.67 | 0.824 | 214.3 | 0.693 |
| 550 | def | 14.14 | 13.12 | 0.871 | 237.7 | 0.709 |
| 550 | new | 13.76 | 12.70 | 0.951 | 242.9 | 0.726 |
| 550 | new2 | 14.24 | 13.26 | 0.958 | 243.5 | 0.704 |
| 550 | all | 13.50 | 12.50 | 0.946 | 255.2 | 0.733 |

At 550, shots/unit-s and hits/s are higher in the faster arms (off 11 fps: 0.824 / 214; all 32 fps: 0.946 / 255), the same pattern as b20_best.

Files: night1\b20_best2\ (report_1_step_table.txt vs off, report_2_step_table.txt vs new, report_4_guard_by_arm.txt, report_5_methods_by_arm.txt). NOW: b20_dispose_paired2 (melee 620, all/allD alternating 90 s) since 08:46.

## b20_dispose_paired2 (rig 2; build b20, 08:46-08:59; melee plateau 620 [lab avg 592, all arm at 21 fps = heavy enough, so 680 was not needed]; arms all / allD = all + DeadDisposeEarly proportional [Seconds 5, Low 25, High 45, Smooth 10, Slew 0.5], strictly alternating 90 s blocks, last 40 s of each evaluated)
Verdict: under load, proportional DeadDisposeEarly gives a clear, properly interleaved gain.
- **Frame work:** allD vs all -31.6 % (s.e. 5.0 %, n 4 allD blocks against their neighbouring all blocks); fps 21.1 → 30.0.
- **Controller:** works at its low end: target 6.2 s on average (5.0-7.1 s) at a smoothed fps of 27.3.
- **Wrecks:** deadU halves, 161 → 79 (dead share 20.0 → 11.1 %); world units 802 → 714.
- **Guard:** shots/unit-s and with-target unchanged. deaths/s +11 %, kills/s +13 % and hits/s +6 % in allD, which runs at 42 % higher fps.
- 0 exceptions, 1/1 row ok.

| arm | fps | work ms | allD vs all paired (s.e., n) | target s: mean (min-max) | smoothed fps (controller) | trackedNow | deadU | dead share | world units | lab units |
|---|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| all | 21.1 | 48.54 | | - | - | - | 161 | 20.0 % | 802 | 593 |
| allD | 30.0 | 34.03 | **-31.6 % (5.0 %, 4)** | 6.20 (5.00-7.12) | 27.3 | 79 | 79 | 11.1 % | 714 | 591 |

- Evaluated parts: 116 s of all and 144 s of allD; the last allD block was cut at the measure end, so only 27 s of it count.
- deaddispose values come from the 15 lines in the evaluated allD parts.

**Guard (1 Hz, evaluated parts):**

| arm | deaths/s | kills/s | shots/unit-s | hits/s | with-target share |
|---|---:|---:|---:|---:|---:|
| all | 12.56 | 11.76 | 0.758 | 227.9 | 0.824 |
| allD | 13.97 | 13.32 | 0.755 | 241.7 | 0.812 |

**Compared with b20_dispose_paired at 550** (non-interleaved, 34.5 fps):
- There the controller sat at 9-11 s and removed 30 % of the wrecks for -10.5 %.
- At 620 (21 fps) it goes to 5-7 s, halves the wrecks and gives -31.6 %. The benefit grows with load, as the proportional design intends.

Files: night1\b20_dispose_paired2\ (report_1_step_table.txt, report_2_guard_by_arm.txt, report_3_dispose_by_arm.txt).
