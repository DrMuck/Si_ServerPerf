## battle3-300 (lab objects planned 384, alive at end 288 units + 84 structures, projectiles avg 531)

| arm | s samples | world units | projectiles | fps median | fps p5 | work avg ms | probe n | frame avg / p95 / p99 ms | main busy ms/s |
|---|---:|---:|---:|---:|---:|---:|---:|---|---:|
| A | 104 | 329 | 547 | 108.0 | 103.2 | 6.61 | 5 | 9.3 / 16.1 / 16.6 | 708 |
| B | 129 | 329 | 523 | 112.0 | 107.0 | 5.76 | 7 | 9.0 / 15.5 / 16.0 | 633 |
| C | 103 | 329 | 499 | 114.0 | 110.0 | 4.99 | 5 | 8.9 / 15.7 / 16.4 | 564 |

| section (ms per wall-second) | A | B | B-A | C | C-A |
|---|---:|---:|---:|---:|---:|
| subsystem: Update.ScriptRunBehaviourUpdate | 351.0 | 317.3 | -33.7 | 245.7 | -105.3 |
| subsystem: TimeUpdate.WaitForLastPresentationAndUpdateTime | 292.0 | 366.3 | +74.2 | 435.9 | +143.9 |
| subsystem: FixedUpdate.PhysicsFixedUpdate | 131.0 | 125.0 | -6.1 | 126.4 | -4.7 |
| subsystem: PreLateUpdate.ScriptRunBehaviourLateUpdate | 126.4 | 129.1 | +2.7 | 130.0 | +3.6 |
| section: Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 100.8 | 102.8 | +1.9 | 65.2 | -35.6 |
| subsystem: FixedUpdate.ScriptRunBehaviourFixedUpdate | 56.0 | 54.3 | -1.7 | 55.1 | -0.9 |
| section: Ordered Update Manager - update: Soldier.OrderedUpdate | 40.2 | 38.3 | -1.8 | 28.6 | -11.6 |
| subsystem: PreLateUpdate.DirectorUpdateAnimationBegin | 34.9 | 0.5 | -34.5 | 0.5 | -34.4 |
| section: Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 31.7 | 31.1 | -0.6 | 32.0 | +0.2 |
| section: Ordered Update Manager - update: GameManager.SensorTickPart1 | 26.3 | 28.8 | +2.5 | 5.8 | -20.6 |
| section: Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 25.3 | 25.2 | -0.1 | 26.0 | +0.7 |
| section: Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 24.5 | 25.4 | +0.9 | 27.1 | +2.6 |
| section: GameManager.Update: FogOfWar.OnUpdate | 22.0 | 3.9 | -18.1 | 3.9 | -18.0 |
| section: Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 17.7 | 18.3 | +0.5 | 18.7 | +0.9 |
| section: Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 14.1 | 13.6 | -0.5 | 13.7 | -0.4 |
| section: Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate | 13.7 | 12.9 | -0.9 | 13.2 | -0.6 |

**A (window ending 14:09:58)**

**B (window ending 14:10:28)**
- Targeting.DetectedGrid: grids 3 | builds/s 1.8, entries/build 208 | added/s 0.8, dirty/s 0.0 | index desync total 0 | Centauri bypass (few queries per build) D 204 cost 0.02 q/frame 0.3 grid share 2% | Sol bypass (few queries per build) D 218 cost 0.01 q/frame 0.4 grid share 1% | Alien bypass (few queries per build) D 204 cost 0.03 q/frame 0.7 grid share 18%
- Targeting.CollectEnemies_Grid: q/s 81 (grid 3, bypass 78, vanilla 0) grid share 4% | visited/q 0.8 of D 206.3 (0.4%), to vanilla logic/q 0.2
- Targeting.FriendlyFire_Grid: q/s 17244 (grid 0, bypass 17244, vanilla 0) grid share 0% | slot 1 bypass (few queries per snapshot) cell L 128 near/L 0.06 q/frame 0.3 | slot 4 bypass (few queries per snapshot) cell L 128 near/L 0.01 q/frame 96.5 | slot 5 bypass (few queries per snapshot) cell L 129 near/L 0.01 q/frame 69.2 | builds/s 0.0
- Targeting.FriendlyFire_StaleGuard: q/s 0 (grid 0, bypass 0, vanilla 0) | grid = guarded calls in a stale window, vanilla = grid walks that met a destroyed target with the guard off
- UpdateTiers.Tier_IdleSoldiers: wake-ups: damage by enemy (WakeAfterDamageSeconds), enemy aiming at it, new enemy detection within WakeOnDetectRadius, any order/path/target  | calls/s run 2880 + early 7020 | skipped 1620 (14%) | interval 4, classified 96 | early by cause /s: destroyed 7020 | since start: aimTarget 2049683, path 16730, destroyed 11056, attackTarget 4699, pathQueued 2436, detectWake 1582 | viewInput limit 0.5 deg/call, mean |input| of rejected 1.712 (n 5)

**C (window ending 14:09:29)**
- Targeting.DetectedGrid: grids 3 | builds/s 1.2, entries/build 207 | added/s 0.6, dirty/s 0.0 | index desync total 0 | Centauri bypass (few queries per build) D 207 cost 0.02 q/frame 1.2 grid share 1% | Sol bypass (few queries per build) D 218 cost 0.02 q/frame 1.5 grid share 1% | Alien bypass (few queries per build) D 197 cost 0.05 q/frame 0.4 grid share 12%
- Targeting.CollectEnemies_Grid: q/s 82 (grid 2, bypass 81, vanilla 0) grid share 2% | visited/q 0.9 of D 205.0 (0.4%), to vanilla logic/q 0.0
- Targeting.FriendlyFire_Grid: q/s 1943 (grid 0, bypass 1943, vanilla 0) grid share 0% | slot 1 bypass (few queries per snapshot) cell L 85 near/L 0.09 q/frame 0.2 | slot 4 bypass (few queries per snapshot) cell L 128 near/L 0.16 q/frame 8.4 | slot 5 bypass (few queries per snapshot) cell L 129 near/L 0.01 q/frame 6.8 | builds/s 0.0
- Targeting.FriendlyFire_StaleGuard: q/s 0 (grid 0, bypass 0, vanilla 0) | grid = guarded calls in a stale window, vanilla = grid walks that met a destroyed target with the guard off
- Targeting.LosTopK: q/s 144 (grid 57, bypass 0, vanilla 0) grid share 40% | LOS queries saved/s 967.6
- Targeting.FriendlyFire_Cache: q/s 18250 (grid 16306, bypass 0, vanilla 1943) grid share 89% | hits/s 16306.1
- MultiCore.SensorPipeline_Rate: interval 50 ms  | frames/s 114 | pipeline runs/s 19.0 (on deletions 0.0, forced 0) | mean dt per run 53.2 ms | time in pipeline: part1 5.71 ms/s, part2+3 3.29 ms/s
- UpdateTiers.Tier_IdleSoldiers: wake-ups: damage by enemy (WakeAfterDamageSeconds), enemy aiming at it, new enemy detection within WakeOnDetectRadius, any order/path/target  | calls/s run 2707 + early 7367 | skipped 761 (7%) | interval 4, classified 96 | early by cause /s: aimTarget 7316, attackTarget 51 | since start: aimTarget 1843849, path 15997, attackTarget 4491, pathQueued 2436, detectWake 1582, aimedAt 324 | viewInput limit 0.5 deg/call, mean |input| of rejected 3.081 (n 2)

Exceptions/warnings counted per arm in this scenario: A 0, B 0, C 0

## battle3-450 (lab objects planned 532, alive at end 436 units + 84 structures, projectiles avg 881)

| arm | s samples | world units | projectiles | fps median | fps p5 | work avg ms | probe n | frame avg / p95 / p99 ms | main busy ms/s |
|---|---:|---:|---:|---:|---:|---:|---:|---|---:|
| A | 104 | 477 | 874 | 78.0 | 64.2 | 12.95 | 5 | 12.8 / 22.4 / 25.3 | 966 |
| B | 128 | 477 | 916 | 90.0 | 78.0 | 9.99 | 6 | 11.1 / 20.7 / 21.5 | 863 |
| C | 104 | 477 | 918 | 92.0 | 85.0 | 9.17 | 4 | 11.0 / 20.7 / 21.6 | 830 |

| section (ms per wall-second) | A | B | B-A | C | C-A |
|---|---:|---:|---:|---:|---:|
| subsystem: Update.ScriptRunBehaviourUpdate | 427.8 | 377.8 | -50.1 | 338.0 | -89.8 |
| subsystem: FixedUpdate.PhysicsFixedUpdate | 220.9 | 206.0 | -14.9 | 206.3 | -14.5 |
| subsystem: PreLateUpdate.ScriptRunBehaviourLateUpdate | 173.5 | 183.1 | +9.6 | 189.4 | +15.9 |
| section: Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 125.8 | 107.2 | -18.6 | 83.1 | -42.7 |
| subsystem: FixedUpdate.ScriptRunBehaviourFixedUpdate | 93.6 | 90.2 | -3.4 | 91.5 | -2.2 |
| section: Ordered Update Manager - update: Soldier.OrderedUpdate | 52.2 | 45.2 | -7.0 | 36.8 | -15.4 |
| subsystem: PreLateUpdate.DirectorUpdateAnimationBegin | 44.4 | 0.4 | -44.0 | 0.4 | -44.0 |
| section: Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 41.0 | 41.6 | +0.6 | 43.1 | +2.1 |
| section: Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 35.1 | 35.1 | +0.0 | 37.5 | +2.4 |
| subsystem: TimeUpdate.WaitForLastPresentationAndUpdateTime | 34.2 | 136.6 | +102.5 | 169.8 | +135.7 |
| section: Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 33.3 | 34.6 | +1.4 | 34.9 | +1.6 |
| section: GameManager.Update: FogOfWar.OnUpdate | 24.9 | 4.3 | -20.6 | 4.4 | -20.5 |
| section: Ordered Update Manager - update: GameManager.SensorTickPart1 | 23.6 | 27.9 | +4.4 | 6.9 | -16.7 |
| section: Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 23.2 | 24.9 | +1.7 | 25.5 | +2.4 |
| section: Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 22.2 | 21.1 | -1.1 | 21.3 | -0.9 |
| section: Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 18.7 | 20.0 | +1.4 | 19.7 | +1.0 |

**A (window ending 14:17:29)**

**B (window ending 14:17:59)**
- Targeting.DetectedGrid: grids 3 | builds/s 1.2, entries/build 336 | added/s 0.4, dirty/s 0.0 | index desync total 0 | Centauri bypass (few queries per build) D 334 cost 0.01 q/frame 1.3 grid share 1% | Sol bypass (few queries per build) D 358 cost 0.01 q/frame 3.2 grid share 1% | Alien bypass (few queries per build) D 307 cost 0.04 q/frame 1.4 grid share 20%
- Targeting.CollectEnemies_Grid: q/s 67 (grid 1, bypass 67, vanilla 0) grid share 1% | visited/q 1.8 of D 346.0 (0.5%), to vanilla logic/q 0.5
- Targeting.FriendlyFire_Grid: q/s 24501 (grid 24482, bypass 18, vanilla 0) grid share 100% | slot 1 bypass (few queries per snapshot) cell L 181 near/L 0.18 q/frame 0.3 | slot 4 GRID (pays) cell L 178 near/L 0.21 q/frame 152.0 | slot 5 GRID (pays) cell L 179 near/L 0.26 q/frame 114.3 | visited/q 16.5 of cell 178.4 (9.3%), to vanilla logic/q 0.0 | builds/s 91.8
- Targeting.FriendlyFire_StaleGuard: q/s 0 (grid 0, bypass 0, vanilla 0) | grid = guarded calls in a stale window, vanilla = grid walks that met a destroyed target with the guard off
- UpdateTiers.Tier_IdleSoldiers: wake-ups: damage by enemy (WakeAfterDamageSeconds), enemy aiming at it, new enemy detection within WakeOnDetectRadius, any order/path/target  | calls/s run 3019 + early 8376 | skipped 682 (6%) | interval 4, classified 216 | early by cause /s: aimTarget 7121, destroyed 1253, viewInput 2 | since start: aimTarget 4472991, path 36095, destroyed 15300, attackTarget 10449, pathQueued 7390, detectWake 5166 | viewInput limit 0.5 deg/call, mean |input| of rejected 2.152 (n 11)

**C (window ending 14:16:58)**
- Targeting.DetectedGrid: grids 3 | builds/s 1.6, entries/build 326 | added/s 0.2, dirty/s 0.0 | index desync total 0 | Centauri bypass (few queries per build) D 336 cost 0.01 q/frame 0.9 grid share 1% | Sol bypass (few queries per build) D 349 cost 0.01 q/frame 1.4 grid share 1% | Alien bypass (no pruning) D 272 cost 0.66 q/frame 0.8 grid share 14%
- Targeting.CollectEnemies_Grid: q/s 90 (grid 3, bypass 87, vanilla 0) grid share 3% | visited/q 59.2 of D 314.2 (18.8%), to vanilla logic/q 28.4
- Targeting.FriendlyFire_Grid: q/s 3151 (grid 3127, bypass 24, vanilla 0) grid share 99% | slot 1 bypass (few queries per snapshot) cell L 181 near/L 0.18 q/frame 0.5 | slot 4 GRID (pays) cell L 178 near/L 0.21 q/frame 20.2 | slot 5 GRID (pays) cell L 179 near/L 0.15 q/frame 16.0 | visited/q 18.9 of cell 178.4 (10.6%), to vanilla logic/q 0.0 | builds/s 17.9
- Targeting.FriendlyFire_StaleGuard: q/s 0 (grid 0, bypass 0, vanilla 0) | grid = guarded calls in a stale window, vanilla = grid walks that met a destroyed target with the guard off
- Targeting.LosTopK: q/s 160 (grid 80, bypass 0, vanilla 0) grid share 50% | LOS queries saved/s 1445.3
- Targeting.FriendlyFire_Cache: q/s 24429 (grid 21278, bypass 0, vanilla 3151) grid share 87% | hits/s 21277.8
- MultiCore.SensorPipeline_Rate: interval 50 ms  | frames/s 93 | pipeline runs/s 17.9 (on deletions 0.0, forced 0) | mean dt per run 56.2 ms | time in pipeline: part1 6.70 ms/s, part2+3 6.51 ms/s
- UpdateTiers.Tier_IdleSoldiers: wake-ups: damage by enemy (WakeAfterDamageSeconds), enemy aiming at it, new enemy detection within WakeOnDetectRadius, any order/path/target  | calls/s run 3208 + early 8925 | skipped 697 (5%) | interval 4, classified 216 | early by cause /s: aimTarget 8786, path 70, detectWake 69 | since start: aimTarget 4211852, path 35820, destroyed 14040, attackTarget 9765, pathQueued 7390, detectWake 4652 | viewInput limit 0.5 deg/call, mean |input| of rejected 2.709 (n 8)

Exceptions/warnings counted per arm in this scenario: A 0, B 0, C 0

## battle3-600 (lab objects planned 678, alive at end 576 units + 84 structures, projectiles avg 937)

| arm | s samples | world units | projectiles | fps median | fps p5 | work avg ms | probe n | frame avg / p95 / p99 ms | main busy ms/s |
|---|---:|---:|---:|---:|---:|---:|---:|---|---:|
| A | 104 | 617 | 973 | 39.0 | 32.1 | 26.06 | 8 | 25.7 / 39.0 / 43.7 | 1000 |
| B | 128 | 617 | 935 | 60.0 | 45.0 | 17.31 | 10 | 16.2 / 30.3 / 34.7 | 987 |
| C | 104 | 617 | 923 | 63.0 | 49.0 | 15.88 | 8 | 15.9 / 30.0 / 34.4 | 968 |

| section (ms per wall-second) | A | B | B-A | C | C-A |
|---|---:|---:|---:|---:|---:|
| subsystem: Update.ScriptRunBehaviourUpdate | 383.7 | 389.9 | +6.2 | 363.4 | -20.3 |
| subsystem: FixedUpdate.PhysicsFixedUpdate | 294.2 | 281.8 | -12.3 | 281.3 | -12.9 |
| subsystem: PreLateUpdate.ScriptRunBehaviourLateUpdate | 160.0 | 192.0 | +32.0 | 197.7 | +37.7 |
| subsystem: FixedUpdate.ScriptRunBehaviourFixedUpdate | 123.7 | 121.0 | -2.7 | 123.1 | -0.6 |
| section: Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 103.0 | 105.3 | +2.3 | 84.4 | -18.7 |
| section: Ordered Update Manager - update: Soldier.OrderedUpdate | 45.3 | 48.0 | +2.7 | 39.7 | -5.6 |
| section: Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 42.7 | 43.9 | +1.2 | 46.4 | +3.8 |
| section: Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 39.1 | 42.4 | +3.3 | 43.6 | +4.6 |
| section: Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 38.0 | 43.4 | +5.4 | 45.7 | +7.7 |
| subsystem: PreLateUpdate.DirectorUpdateAnimationBegin | 36.7 | 0.3 | -36.3 | 0.3 | -36.3 |
| section: Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 30.9 | 29.3 | -1.7 | 28.9 | -2.0 |
| section: Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 26.7 | 28.6 | +1.9 | 29.3 | +2.6 |
| section: GameManager.Update: FogOfWar.OnUpdate | 26.4 | 4.8 | -21.6 | 4.9 | -21.5 |
| section: Ordered Update Manager - fixed: CreatureDecapod.OrderedFixedUpdate | 20.1 | 19.4 | -0.7 | 19.7 | -0.4 |
| section: Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 18.9 | 24.8 | +5.9 | 26.3 | +7.4 |
| section: Ordered Update Manager - update: GameManager.SensorTickPart1 | 15.8 | 22.6 | +6.8 | 8.0 | -7.8 |

**A (window ending 14:24:58)**

**B (window ending 14:25:28)**
- Targeting.DetectedGrid: grids 3 | builds/s 2.6, entries/build 440 | added/s 0.4, dirty/s 0.0 | index desync total 0 | Centauri bypass (few queries per build) D 379 cost 0.01 q/frame 0.7 grid share 1% | Sol bypass (few queries per build) D 406 cost 0.02 q/frame 1.0 grid share 1% | Alien GRID (pays) D 461 cost 0.05 q/frame 2.4 grid share 100%
- Targeting.CollectEnemies_Grid: q/s 73 (grid 9, bypass 64, vanilla 0) grid share 12% | visited/q 0.2 of D 448.6 (0.0%), to vanilla logic/q 0.0
- Targeting.FriendlyFire_Grid: q/s 24367 (grid 24346, bypass 22, vanilla 0) grid share 100% | slot 1 bypass (few queries per snapshot) cell L 217 near/L 0.03 q/frame 0.7 | slot 4 GRID (pays) cell L 229 near/L 0.11 q/frame 196.7 | slot 5 GRID (pays) cell L 228 near/L 0.03 q/frame 159.2 | visited/q 19.7 of cell 224.0 (8.8%), to vanilla logic/q 0.0 | builds/s 66.8
- Targeting.FriendlyFire_StaleGuard: q/s 0 (grid 0, bypass 0, vanilla 0) | grid = guarded calls in a stale window, vanilla = grid walks that met a destroyed target with the guard off
- UpdateTiers.Tier_IdleSoldiers: wake-ups: damage by enemy (WakeAfterDamageSeconds), enemy aiming at it, new enemy detection within WakeOnDetectRadius, any order/path/target  | calls/s run 3086 + early 8794 | skipped 463 (4%) | interval 4, classified 378 | early by cause /s: aimTarget 8743, path 52 | since start: aimTarget 6619629, path 55031, destroyed 35189, attackTarget 19705, pathQueued 12038, detectWake 7257 | viewInput limit 0.5 deg/call, mean |input| of rejected 4.829 (n 16)

**C (window ending 14:24:29)**
- Targeting.DetectedGrid: grids 3 | builds/s 2.6, entries/build 433 | added/s 1.0, dirty/s 0.0 | index desync total 0 | Centauri bypass (few queries per build) D 369 cost 0.01 q/frame 3.5 grid share 1% | Sol bypass (few queries per build) D 408 cost 0.03 q/frame 2.4 grid share 1% | Alien GRID (pays) D 471 cost 0.06 q/frame 2.9 grid share 64%
- Targeting.CollectEnemies_Grid: q/s 107 (grid 10, bypass 97, vanilla 0) grid share 9% | visited/q 27.9 of D 458.7 (6.1%), to vanilla logic/q 11.2
- Targeting.FriendlyFire_Grid: q/s 3933 (grid 3678, bypass 255, vanilla 0) grid share 94% | slot 1 bypass (few queries per snapshot) cell L 217 near/L 0.03 q/frame 0.4 | slot 4 GRID (pays) cell L 229 near/L 0.13 q/frame 31.6 | slot 5 GRID (pays) cell L 228 near/L 0.13 q/frame 25.5 | visited/q 19.6 of cell 227.6 (8.6%), to vanilla logic/q 0.0 | builds/s 16.3
- Targeting.FriendlyFire_StaleGuard: q/s 0 (grid 0, bypass 0, vanilla 0) | grid = guarded calls in a stale window, vanilla = grid walks that met a destroyed target with the guard off
- Targeting.LosTopK: q/s 193 (grid 111, bypass 0, vanilla 0) grid share 57% | LOS queries saved/s 1939.7
- Targeting.FriendlyFire_Cache: q/s 24320 (grid 20387, bypass 0, vanilla 3933) grid share 84% | hits/s 20386.6
- MultiCore.SensorPipeline_Rate: interval 50 ms  | frames/s 67 | pipeline runs/s 16.3 (on deletions 0.0, forced 0) | mean dt per run 60.8 ms | time in pipeline: part1 7.94 ms/s, part2+3 8.44 ms/s
- UpdateTiers.Tier_IdleSoldiers: wake-ups: damage by enemy (WakeAfterDamageSeconds), enemy aiming at it, new enemy detection within WakeOnDetectRadius, any order/path/target  | calls/s run 3001 + early 8559 | skipped 450 (4%) | interval 4, classified 378 | early by cause /s: aimTarget 8468, path 71, attackTarget 21 | since start: aimTarget 6367356, path 54356, destroyed 35189, attackTarget 19375, pathQueued 12038, detectWake 7257 | viewInput limit 0.5 deg/call, mean |input| of rejected 4.829 (n 16)

Exceptions/warnings counted per arm in this scenario: A 0, B 0, C 0

## battle3-800 (lab objects planned 864, alive at end 759 units + 84 structures, projectiles avg 1312)

| arm | s samples | world units | projectiles | fps median | fps p5 | work avg ms | probe n | frame avg / p95 / p99 ms | main busy ms/s |
|---|---:|---:|---:|---:|---:|---:|---:|---|---:|
| A | 101 | 800 | 1446 | 15.0 | 13.0 | 70.29 | 8 | 69.1 / 93.7 / 102.1 | 1000 |
| B | 128 | 800 | 1366 | 22.0 | 19.0 | 45.50 | 10 | 46.3 / 72.0 / 80.1 | 1000 |
| C | 102 | 800 | 1273 | 25.0 | 21.0 | 42.03 | 8 | 41.4 / 63.0 / 79.3 | 999 |

| section (ms per wall-second) | A | B | B-A | C | C-A |
|---|---:|---:|---:|---:|---:|
| subsystem: FixedUpdate.PhysicsFixedUpdate | 356.3 | 377.1 | +20.8 | 379.8 | +23.5 |
| subsystem: Update.ScriptRunBehaviourUpdate | 300.7 | 298.5 | -2.1 | 293.8 | -6.8 |
| subsystem: FixedUpdate.ScriptRunBehaviourFixedUpdate | 169.4 | 161.3 | -8.1 | 161.9 | -7.5 |
| subsystem: PreLateUpdate.ScriptRunBehaviourLateUpdate | 148.8 | 163.6 | +14.7 | 164.2 | +15.4 |
| section: Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 65.6 | 71.0 | +5.4 | 58.8 | -6.8 |
| section: Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 53.5 | 51.9 | -1.6 | 58.2 | +4.7 |
| section: Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 44.0 | 44.2 | +0.2 | 45.6 | +1.5 |
| section: Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 40.0 | 38.6 | -1.4 | 38.7 | -1.3 |
| section: Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 39.2 | 39.8 | +0.6 | 39.2 | +0.1 |
| section: Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 37.0 | 38.3 | +1.2 | 39.7 | +2.7 |
| section: Ordered Update Manager - update: Soldier.OrderedUpdate | 30.6 | 33.3 | +2.7 | 31.8 | +1.2 |
| section: Ordered Update Manager - fixed: CreatureDecapod.OrderedFixedUpdate | 27.6 | 26.0 | -1.6 | 26.8 | -0.8 |
| section: GameManager.Update: FogOfWar.OnUpdate | 27.2 | 5.4 | -21.8 | 5.6 | -21.6 |
| subsystem: PreLateUpdate.DirectorUpdateAnimationBegin | 26.1 | 0.2 | -25.9 | 0.2 | -25.9 |
| section: Ordered Update Manager - fixed: Silica.AI.CollisionAvoidanceManager.PrepareFixedUpdate | 13.2 | 11.1 | -2.1 | 10.9 | -2.4 |
| section: Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 13.1 | 16.7 | +3.6 | 17.6 | +4.5 |

**A (window ending 14:32:29)**

**B (window ending 14:32:58)**
- Targeting.DetectedGrid: grids 3 | builds/s 11.3, entries/build 513 | added/s 0.8, dirty/s 0.0 | index desync total 0 | Centauri GRID (pays) D 465 cost 0.02 q/frame 5.6 grid share 23% | Sol bypass (no pruning) D 522 cost 0.65 q/frame 5.4 grid share 43% | Alien GRID (pays) D 596 cost 0.06 q/frame 5.1 grid share 48%
- Targeting.CollectEnemies_Grid: q/s 92 (grid 38, bypass 54, vanilla 0) grid share 41% | visited/q 54.6 of D 533.2 (10.2%), to vanilla logic/q 26.0
- Targeting.FriendlyFire_Grid: q/s 10396 (grid 4369, bypass 6027, vanilla 0) grid share 42% | slot 1 bypass (few queries per snapshot) cell L 271 near/L 0.20 q/frame 1.4 | slot 4 bypass (short cell list) cell L 44 near/L 0.73 q/frame 284.8 | slot 5 GRID (pays) cell L 261 near/L 0.11 q/frame 202.4 | visited/q 24.8 of cell 261.9 (9.5%), to vanilla logic/q 0.0 | builds/s 21.6
- Targeting.FriendlyFire_StaleGuard: q/s 0 (grid 0, bypass 0, vanilla 0) | grid = guarded calls in a stale window, vanilla = grid walks that met a destroyed target with the guard off
- UpdateTiers.Tier_IdleSoldiers: wake-ups: damage by enemy (WakeAfterDamageSeconds), enemy aiming at it, new enemy detection within WakeOnDetectRadius, any order/path/target  | calls/s run 953 + early 2733 | skipped 123 (3%) | interval 4, classified 594 | early by cause /s: aimTarget 2722, moving 11 | since start: aimTarget 7707218, path 73629, destroyed 62043, attackTarget 25158, pathQueued 14763, detectWake 10962 | viewInput limit 0.5 deg/call, mean |input| of rejected 5.05 (n 22)

**C (window ending 14:31:59)**
- Targeting.DetectedGrid: grids 3 | builds/s 2.8, entries/build 568 | added/s 1.2, dirty/s 0.0 | index desync total 0 | Centauri bypass (no pruning) D 452 cost 0.72 q/frame 5.5 grid share 9% | Sol bypass (few queries per build) D 522 cost 0.46 q/frame 5.5 grid share 0% | Alien GRID (pays) D 601 cost 0.05 q/frame 4.6 grid share 100%
- Targeting.CollectEnemies_Grid: q/s 135 (grid 20, bypass 115, vanilla 0) grid share 15% | visited/q 91.4 of D 549.0 (16.7%), to vanilla logic/q 44.0
- Targeting.FriendlyFire_Grid: q/s 4503 (grid 0, bypass 4503, vanilla 0) grid share 0% | slot 1 bypass (few queries per snapshot) cell L 271 near/L 0.15 q/frame 0.8 | slot 4 bypass (few queries per snapshot) cell L 294 near/L 0.17 q/frame 95.5 | slot 5 bypass (few queries per snapshot) cell L 264 near/L 0.12 q/frame 89.2 | builds/s 0.0
- Targeting.FriendlyFire_StaleGuard: q/s 0 (grid 0, bypass 0, vanilla 0) | grid = guarded calls in a stale window, vanilla = grid walks that met a destroyed target with the guard off
- Targeting.LosTopK: q/s 261 (grid 174, bypass 0, vanilla 0) grid share 67% | LOS queries saved/s 3106.9
- Targeting.FriendlyFire_Cache: q/s 10703 (grid 6200, bypass 0, vanilla 4503) grid share 58% | hits/s 6200.3
- MultiCore.SensorPipeline_Rate: interval 50 ms  | frames/s 24 | pipeline runs/s 13.2 (on deletions 0.0, forced 0) | mean dt per run 74.7 ms | time in pipeline: part1 8.62 ms/s, part2+3 11.66 ms/s
- UpdateTiers.Tier_IdleSoldiers: wake-ups: damage by enemy (WakeAfterDamageSeconds), enemy aiming at it, new enemy detection within WakeOnDetectRadius, any order/path/target  | calls/s run 1457 + early 4180 | skipped 185 (3%) | interval 4, classified 594 | early by cause /s: aimTarget 4135, attackTarget 27, path 18 | since start: aimTarget 7601915, path 72934, destroyed 62043, attackTarget 24265, pathQueued 14762, detectWake 10865 | viewInput limit 0.5 deg/call, mean |input| of rejected 5.05 (n 22)

Exceptions/warnings counted per arm in this scenario: A 0, B 0, C 0

