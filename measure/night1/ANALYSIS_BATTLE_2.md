# Dense-battle analysis, round 2: what is left in arm P at ~521 lab units (night 1, builds b13-b17)

Written 2026-11-17 ~04:30, read-only (no server started, nothing written to the lab, no source changed).
Scripts: `C:\Users\schwe\AppData\Local\Temp\claude\525e89c771cb4f7a\82b33cdb-60b5-4fc8-a510-991f35788a4f\scratchpad\attr3.py`
(+ `leaves_mod.py`), same leaf definitions as round 1 (`ANALYSIS_BATTLE.md`).

## 0. Data

| run | arms | used for |
|---|---|---|
| `b17_pscan` (b17, melee plateaus 450 / 550 = lab 421-423 / 518, W 644-653 / 758-760) | S3 = new (defaults + 3 snapshot items + DeadAgentHalt), P = S3 + `MultiCore.ParallelScan`, 30 s flips | section tables, census |
| `b17_pscan_inline` (ParallelScanJobs 0) | S3 / P inline | P inline -2.7 % (s.e. 1.6): the b17 gain is the worker offload |
| `b15_best_plat` | off / def / new / max | context: fps 15.6 / 20.7 / 27.3 / 40.1 at 550 |
| `b13_dead`, `b14_dead` | DeadSettle, DeadAgentHalt, dispose | physics of dead vs live units |
| not yet complete | `b17_dispose_prop` (running), `b17_dispose_always`, `qc2`, `qc_fixed20`, `b15_eveh` ... | - |

Arm P at the 550 plateau: 3 clean PerfProbe intervals (30 s; the others start < 2 s after a flip), lab 518, W 758,
**32.0 fps, 31.3 ms/frame** (PerfLab 1 Hz, 83 s: 31.9 fps, 31.9 ms; paired P - S3 -6.2 ms, -16.3 %, s.e. 1.8).
Main thread saturated (1000 ms/s). Probe overhead in these intervals: 125-150 k calls / 10 s, est. 62-76 ms per 10 s
(0.6-0.8 % of wall; ~0.5 us per probed call incl. Harmony glue, calibrated core cost 0.057 us).

## 1. Where the time goes in arm P at ~521 (Q1)

### 1a. Ranked blocks (b17_pscan, melee-pscan@550, arm P; T = time-driven, ms/s independent of fps; F = frame-driven)

| rank | block | kind | ms/s | ms/frame | share |
|---:|---|---|---:|---:|---:|
| 1 | PhysX simulate (25.1 steps/s, 0.78 steps/frame, 13.4 ms/step) | T | 336.3 | 10.51 | 33.6 % |
| 2 | `VehicleTurret.OrderedUpdateAllTurrets` (contains most of `CheckRiskOfFriendlyFire`) | F | 74.3 | 2.32 | 7.4 % |
| 3 | `ProjectileManager.LateUpdate` = P2 (job complete, per-projectile loop, sync rays, hits) | T/F mixed | 64.3 | 2.01 | 6.4 % |
| 4 | `BTEnemyTargetDetected` minus sync `FindBestTarget` (= prefiltered scans + rest of the tree) | T | 63.7 | 1.99 | 6.4 % |
| 5 | `CreatureDecapod.OrderedLateUpdate` (creature AI 0.2 s ticks, melee checks, attack updates) | T | 57.5 | 1.80 | 5.7 % |
| 6 | fixed: CollisionAvoidance (unit + manager) | T | 32.4 | 1.01 | 3.2 % |
| 7 | fixed: VehicleWheeled (fixed, part two, batched rays) | T | 32.3 | 1.01 | 3.2 % |
| 8 | `Soldier.OrderedUpdate` | F | 30.1 | 0.94 | 3.0 % |
| 9 | agents: rest of `OrderedUpdateAllAgents` (UpdateAim + LOS every 0.2 s, pathfinding update, ParallelScan prefix) | T | 27.6 | 0.86 | 2.8 % |
| 10 | Soldier late (`OrderedLateUpdateAll` + `Finish`) | T | 22.4 | 0.70 | 2.2 % |
| 11 | `SensorTickPart1` (TargetGrid.Update 14.2 + TickSensors 6.6) | F | 21.0 | 0.66 | 2.1 % |
| 12 | other ordered update handlers | F | 20.4 | 0.64 | 2.0 % |
| 13 | Si_PerfLab OnUpdate (lab respawns; lab artefact) | T | 17.8 | 0.56 | 1.8 % |
| 14 | VehicleWheeled/Hovered.OrderedUpdate | F | 17.0 | 0.53 | 1.7 % |
| 15 | fixed: CreatureDecapod | T | 13.4 | 0.42 | 1.3 % |
| 16 | sync `FindBestTarget` (served full scans; `CollectDetectedEnemies` 9.8 inside) | T | 13.0 | 0.41 | 1.3 % |
| 17 | CreatureTurret late + aim | F | 11.8 | 0.37 | 1.2 % |
| 18 | `AstarPath.Update` | F | 11.5 | 0.36 | 1.1 % |
| 19 | `CreatureDecapod.OrderedUpdate` | F | 11.5 | 0.36 | 1.1 % |
| 20 | `AIOrderProcessor.OrderedUpdate` | F | 10.7 | 0.33 | 1.1 % |
| - | not attributed to any probed section | - | 15.0 | 0.47 | 1.5 % |
| | *cross-cutting (inside rows above):* `Target.CheckRiskOfFriendlyFire` 7092 calls/s x 7.4 us | F | 52.8 | 1.65 | 5.3 % |
| | `TargetFinder.RequestScan` total (1099 calls/s incl. ~430 ParallelScan dry runs) | | 70.3 | 2.20 | 7.0 % |
| | `TargetFinder.CollectDetectedEnemies` (430 calls/s, 22.7 us: served) | | 9.8 | 0.31 | 1.0 % |

**Top 6 (arm P, ~521):** PhysX 336 ms/s (34 %); VehicleTurret 74 (7 %, two thirds of it friendly-fire checks);
projectile P2 64 (6 %); BT rest 64 (6 %); creature late update 58 (6 %); ordered fixed handlers together 114 (11 %;
avoidance 32, wheeled 32).

### 1b. Blocks, S3 vs P (ms/s; ms/frame; share)

| block | S3 (27.3 fps, 36.8 ms) | P (32.0 fps, 31.3 ms) |
|---|---:|---:|
| PhysX simulate | 337 / 12.4 (34 %) | 336 / 10.5 (34 %) |
| ordered fixed handlers | 112 / 4.1 (11 %) | 114 / 3.6 (11 %) |
| turret / sensor / LOS pipeline | 113 / 4.1 (11 %) | 121 / 3.8 (12 %) |
| AI target scans (BTEnemyTargetDetected) | 113 / 4.2 (11 %) | 77 / 2.4 (8 %) |
| projectiles P1 + P2 | 67 / 2.5 (7 %) | 70 / 2.2 (7 %) |
| creature AI + melee | 68 / 2.5 (7 %) | 69 / 2.2 (7 %) |
| soldier per-frame | 49 / 1.8 (5 %) | 52 / 1.6 (5 %) |
| other attributed | 44 / 1.6 (4 %) | 47 / 1.5 (5 %) |
| AI other behaviour-tree work | 18 / 0.7 (2 %) | 28 / 0.9 (3 %) |
| vehicle per-frame + order processors | 25 / 0.9 (2 %) | 28 / 0.9 (3 %) |
| churn (lab respawns, disposal, async instantiate) | 23 / 0.8 (2 %) | 23 / 0.7 (2 %) |
| pathing | 17 / 0.6 (2 %) | 15 / 0.5 (1 %) |
| presentation residue / networking | 5 / 0.2 | 5 / 0.2 |
| not attributed | 14 / 0.5 (1 %) | 15 / 0.5 (2 %) |

ParallelScan moved ~36 ms/s of scans off the main thread and added ~9 ms/s to the agent prefix ("AI other") plus
~8 ms/s of extra frame-driven work that comes with the higher fps (turrets, soldiers): net -6 ms/frame.

### 1c. Model: how far from saturation, what 10 ms/s is worth

* Time-driven sections (fixed phase, agents/BT, creature late, soldier late, projectiles, fog, LOS publish, lab):
  **T = 733 ms/s**; the frame-driven rest is 266 ms/s = 8.3 ms/frame at 32 fps.
* dt-proportional part of the "frame-driven" handlers, from the two b17 plateaus per arm (F/W = c0 + c1 x dt):
  P c1 = 0.146, c0 = 6.4 us per unit; S3 c1 = 0.150 (night-1 fits: 0.10-0.13). With c1 = 0.11-0.15 x W 758:
  **T' = 816-847 ms/s (82-85 % of the second; night 1 def at lab ~560: 93 %), F_pure = 4.8-5.7 ms/frame.**
  Check: (1000 - T') / F_pure = 32 fps.
* **Each further 10 ms/s removed (time-driven or frame-driven, first order): +1.75-2.1 fps (+5.5-6.5 %), frame work
  -1.6-1.9 ms.** Time-driven savings keep their size as fps rises; frame-driven savings grow with fps, so both are
  worth the same at the margin now (in night 1, at 13 fps, the time-driven ones were clearly worth more).
* For reference: 40 fps needs ~40-45 ms/s less, 60 fps ~130-160 ms/s less (at F_pure unchanged). The `max` arm's
  40.1 fps (5 s disposal) fits this.

## 2. The three blocks of question 2, by code reading (1.0.17)

### 2a. `BTEnemyTargetDetected` minus `CollectDetectedEnemies` (~67 ms/s in P)

What the method's time contains (`BehaviorTrees\BTEnemyTargetDetected.cs`, `Silica.AI\AIBaseAgent.cs:544-600`):
1. **It times the rest of the behaviour tree too.** `workCompleted(...)` is the parent's callback and runs the next
   nodes synchronously (`Sequence.FinishRunSequenceCallback`, `Selector.FinishRunSelectorCallback`). In the attack
   trees (`BehaviorTreeBuilder.cs`: SoldierAttack / VehicleAttack / AlienAttack) the detect node sits in
   `BTAlwaysFalse(Sequence(Detect, Shoot))`, so the selector then runs TakeCover / ChaseEnemy (soldiers),
   PlayerSetTarget + MoveTo, Shoot - all inside ActualExecute. Path requests (`AIPathfinding.QueueFindPath` from
   ChaseEnemy when the target moved > 5 m, from MoveTo when the destination moved > 5 m) and orders
   (`BTMeleeAttack` -> `AIOrderProcessor.IssueOrder` every tick for AlienStationary) are in here.
2. **The async ("prefiltered") rescan.** Each agent's tree runs every 0.5 s (`m_BTUpdateDelay`, AIBaseAgent.cs:1474-1481).
   When the current target is invalid, not visible / out of range, or a re-evaluation is due (every 1-1.5 s,
   `AIAiming.GetWantsTargetReevaluation` :439-463; immediately on "urgent" = damaged by a new attacker, :465-539),
   Execute calls `FindBestTargetAsyncStart` (Unit.StartGetBestTarget adds a `TeamFilterBestTargetQuery`, Unit.cs:1375-1394)
   and parks the node. After the agent loop, `Team.RunBestTargetTeamFilterJobs` (Team.cs:1787-1817) runs a Burst job
   over every detected transform for every query (range test only) plus `BumpDownTeamJob`, which **only compacts the
   in-range entries to the front - it does not rank or cap them**. `FinishExecution` -> `FindBestTargetAsyncFinish`
   -> `Unit/Vehicle/CreatureDecapod.FinishGetBestTarget` -> `Team.GetBestTargetScores` (Complete() = main-thread wait
   on that job) -> `TargetFinder.RequestScan` in mode PreFilteredEnemies -> **`CollectPreFilteredEnemies`
   (TargetFinder.cs:396-433): for every in-range entry (~250-300 here) a vanilla `CalculateBestTargetScore` +
   `WorldLineOfSightCenter` + `AddCandidate` with its 24-slot linear min search** - the same per-candidate work as the
   full scan, but **none of the Snapshot / SnapshotScore / FastAdd / ParallelScan items cover it** (they patch
   `CollectDetectedEnemies` only; FastAdd only inside that replacement). Then up to 24 LOS handles per scan
   (`LineOfSightManager.RequestQueryToTarget`), the previous scan's TryResolve -> Rank -> Select.
3. Checks per tick: `GetDetectedByTeam`, distances, `GetWantsUnshootableRescan`, urgent re-evaluation (AttackedBy loop,
   `FindUrgentCloseEnemy` every 0.5 s, UrgentClose_Grid 130 q/s), SetAim / SetShooting / PauseMovement, blackboard
   string-key reads, Sequence/Selector dictionary bookkeeping.

**Estimate from the probed totals** (S3, where nothing is served): RequestScan 106.7 ms/s at 656 calls/s, of which
CollectDetectedEnemies 54.0 (442/s) and the sync wrappers <= 1.6 -> **the ~214 non-full scans/s (prefiltered) cost
~50 ms/s, ~230 us each**, i.e. as much as a vanilla full scan. That leaves **~7 ms/s for the rest of the tree** in
BT-rest 57.6 (S3). P: BT rest 63.7, of which prefiltered scans ~55 (the dry runs are in the agent prefix, not here).
Per-call costs by code reading: prefiltered scan ~250 candidates x ~0.6-0.9 us + 24 LOS handle requests (~0.5-1 us
each) + Rank/Select (~5 us); team job wait ~10-50 us per team per frame (~1-5 ms/s); tree plumbing ~2-5 us per tick.

**Removable exactly:** the per-candidate cost of `CollectPreFilteredEnemies` (same snapshot/scoring replay as
CollectDetectedEnemies; range01 is an input taken from vanilla's job output, so nothing Burst-fast-math needs to be
reproduced) and its AddCandidate min search. Not exactly removable: the scan count (it is the re-evaluation design),
the LOS requests (their results are the vanilla visibility decision).

### 2b. `CreatureDecapod.OrderedLateUpdate` (57.5 ms/s, CreatureDecapod.cs:4389-4790)

* Every frame per creature: AI attack trigger (`Attack()` when `m_AIAttack*` and `CanAttack()`), `UpdateAttacks(dt)`
  -> `CreatureAttack.Update` x2 -> `ApplyDamage` on the damage frame (overlap + damage application), attack-curve
  evaluation; `AI_ProcessWaitTime -= dt`.
* Every 0.2 s per creature (~260 creatures -> ~1300 ticks/s): `CanHitTarget` for one attack (alternating, `AI_AttackCheckFlip`),
  `UpdateAIStrayRepath` (may `QueueFindPath`), `GetAIPathPursuitPoint`, `UpdateAILoopCheck`, `UpdateAirIntercept`,
  `GetDoMeleeEngage`, `GetPointInvalidForAI` (navmesh nearest-node query, flying only), submerge checks that call
  `CanHitTarget` again for both attacks, steering maths.
* `CreatureAttack.CanHitTarget` (CreatureAttack.cs): melee = `Physics.OverlapSphereNonAlloc(attackPosition, Radius + move,
  QueryStaticColliders[4096], TRACELAYERMASK_DAMAGE)` then per returned collider `GetDamageManager()`
  (GetBaseGameObject), `Team.GetTeamsAreEnemy`, `damageManager.Owner.Target == target`, and for damage-bounds colliders
  `DamageableKinematicCapsuleCollidersSphereOverlapQuery`; ranged = angle test, `CheckRiskOfFriendlyFire`, `Physics.Linecast`.
  In a dense melee the overlap returns every hit-zone collider around the creature.
* Removable exactly (if the probe shows CanHitTarget dominates): the per-collider managed work (collider ->
  DamageManager cache keyed by collider instance id, invalidated on destroy; early exit order unchanged).
  Removable approximately: CanHitTarget for the second attack when the first already decided.

### 2c. Projectile P2 synchronous path (64.3 ms/s, ProjectileJobManager.cs:262-345)

* `RaycastHitJob.Complete()` (wait for P1's batched spherecasts / inside-bounds raycasts), then per projectile: if
  the inside-bounds hit buffer has a collider -> collect damage managers (`GetDamageManager` per hit, synchronous
  `Physics.RaycastNonAlloc` when the buffer overflowed, :296) and `ProjectileBasic.NonBatchedRaycastP1`
  (ProjectileBasic.cs:1050: synchronous `SphereCastNonAlloc`/`RaycastNonAlloc` all-hits + capsule-ray queries
  against the collected damage managers) and later `NonBatchedRaycastP2` (:1124); else `HandleHit` on a batched hit
  (:1187: terrain material lookup, damage). Then `UpdateLifetime` + `UpdateVisibility` per projectile and the
  transform-sync job (Complete()).
* 500-740 projectiles in flight (frames.csv), ~2.0 ms per frame = ~3.5 us per projectile per frame; ms/s rose with
  fps (S3 60.6 at 27 fps, P 64.3 at 32 fps) -> part frame-driven. The sync share is unknown (not probed).
* Removable exactly: the synchronous all-hits casts can be batched (SpherecastCommand / RaycastCommand with maxHits,
  scheduled in P1 for projectiles already inside bounds), keeping vanilla's hit selection (minimum distance,
  same filters) on the results.

## 3. Deep-probe list for ONE extra run (melee plateau 550, arm P only, no flips, >= 210 s hold)

Format as in `probe_lab.json` "probes": `Type:Method`; every overload with that name declared on the type is
patched (no overload syntax). Keep the existing 57 probes. PerfProbe reports total and self time, so nested probes
split their parents. Estimated calls/s at ~521 in P (from the probed counts and the rates in section 2):

| # | probe | calls/s (est.) | splits |
|---:|---|---:|---|
| 1 | `TargetFinder:CollectPreFilteredEnemies` | 200-300 | prefiltered scan cost (2a.2) |
| 2 | `TargetFinder:DropHandles` | 1 300 | LOS handle disposal |
| 3 | `TargetFinder:Select` | 600 | TryResolve tail |
| 4 | `LineOfSightManager:RequestQueryToTarget` | 10 000-18 000 | LOS requests (scans + UpdateAim) |
| 5 | `AIAiming:FindBestTargetAsyncStart` | 200-350 | async starts |
| 6 | `AIAiming:FindBestTargetAsyncFinish` | 200-350 | async finishes (contains 7-9) |
| 7 | `Unit:FinishGetBestTarget` | 50-150 | soldiers / turret-less units |
| 8 | `Vehicle:FinishGetBestTarget` | 50-150 | per-turret prefiltered scans |
| 9 | `CreatureDecapod:FinishGetBestTarget` | 100-200 | two finders per creature |
| 10 | `Team:GetBestTargetScores` | 300-500 | team job wait (Complete) |
| 11 | `Team:RunBestTargetTeamFilterJobs` | 100-200 | job schedule cost |
| 12 | `AIAiming:GetWantsTargetReevaluation` | 800-1 100 | re-evaluation incl. urgent path |
| 13 | `AIAiming:GetWantsUrgentReevaluation` | 800-1 100 | AttackedBy / damaged-by loops |
| 14 | `AIAiming:SetAim` | 1 000-1 300 | aim switch (AddAttacker etc.) |
| 15 | `AIAiming:SetShooting` | 1 000-1 500 | DetermineAgentState |
| 16 | `BehaviorTrees.BTShoot:Execute` | ~1 000 | rest of tree (use SELF time: totals include later nodes) |
| 17 | `BehaviorTrees.BTChaseEnemy:Execute` | 100-500 | soldier chase + path requests |
| 18 | `BehaviorTrees.BTPlayerSetTarget:Execute` | 300-700 | |
| 19 | `BehaviorTrees.BTMoveTo:Execute` | 300-700 | MoveTo path requests |
| 20 | `BehaviorTrees.BTMeleeAttack:Execute` | 0-300 | alien order spam |
| 21 | `BehaviorTrees.BTRetaliateOnMove:Execute` | 0-300 | |
| 22 | `BehaviorTrees.BTTakeCover:Execute` | 0-200 | soldier cover search |
| 23 | `Silica.AI.AIPathfinding:QueueFindPath` | 100-800 | path requests from BT / creature AI |
| 24 | `Silica.AI.AIOrderProcessor:IssueOrder` | 50-300 | orders from BT |
| 25 | `CreatureAttack:CanHitTarget` | 1 500-3 000 | melee/ranged hit tests (2b) |
| 26 | `CreatureDecapod:UpdateAttacks` | ~8 300 | per-frame attack update |
| 27 | `CreatureAttack:ApplyDamage` | 50-200 | damage frames |
| 28 | `CreatureDecapod:Attack` | 50-200 | attack starts |
| 29 | `CreatureDecapod:UpdateAIStrayRepath` | ~1 300 | |
| 30 | `CreatureDecapod:GetAIPathPursuitPoint` | ~1 300 | |
| 31 | `CreatureDecapod:UpdateAILoopCheck` | ~1 300 | |
| 32 | `CreatureDecapod:UpdateAirIntercept` | ~1 300 | |
| 33 | `CreatureDecapod:GetDoMeleeEngage` | ~1 300 | |
| 34 | `Unit:GetPointInvalidForAI` | 0-500 | navmesh query (flying) |
| 35 | `ProjectileBasic:NonBatchedRaycastP1` | 5 000-19 000 | sync casts (2c) |
| 36 | `ProjectileBasic:HandleHit` | 300-800 | batched-hit handling |
| 37 | `DamageManager:ApplyDamage` | 1 000-3 000 | all damage application |

Total ~45-70 k added calls/s -> **~25-35 ms/s of probe overhead (2.5-3.5 % of the main thread; guard 15 %)**,
on top of ~13 k calls/s now. Expect fps ~5 % lower than b17's P; compare shares inside this run, subtract the
`overhead_ms_est` column from parents. If one probe must go, drop #4 first, then #35 (the two largest counts).
Not probeable: Unity natives (Physics.*, joint setters), Burst jobs; `CalculateBestTargetScore` itself
(~60-90 k calls/s, too many).

What each result points to:
* #1 (+#6-9) >= 35 ms/s -> candidate E1 (snapshot replay for prefiltered scans) is worth building; < 20 -> the
  remainder of BT rest is the tree (#16-24), see next.
* #4 >= 15 ms/s -> LOS request cost matters; candidate: pooled handles / skip LOS for candidates ranked below the
  current best (exact only if Select would never pick them).
* #23 + #24 + #17-20 self >= 15 ms/s -> path/order churn from BT re-issuing; candidate: skip a re-queue whose
  destination equals the pending request (needs the exactness check of QueueFindPath semantics).
* #10 >= 5 ms/s -> team filter job wait; schedule it earlier (needs deferral, timing change) or run inline.
* #25 >= 25 ms/s -> candidate E5 (CanHitTarget per-collider cache); #26/#27 large -> attack damage path instead.
* #35 >= 30 ms/s -> candidate E4 (batch the sync casts); #36/#37 large -> damage application, not casts.

## 4. Ranked next candidates (ms/s at ~521 in arm P; 10 ms/s ~ +1.9 fps)

### Exact first

**E1. Snapshot replay for the prefiltered (async) scans** - `TargetFinder.CollectPreFilteredEnemies`
(TargetFinder.cs:396-433), callers `Unit.FinishGetBestTarget` (Unit.cs:1430-1480), `Vehicle.FinishGetBestTarget`
(:1573+), `CreatureDecapod.FinishGetBestTarget` (:3399+). Reuse CollectEnemies_Snapshot / SnapshotScore / FastAdd:
same per-team entry cache, same type-value cache replay, range01 read from vanilla's `GetBestTargetScores` output
(no Burst float reproduction), `withinView` false, candidate order = bump-down order. Block ~50-55 ms/s; the
snapshot items cut the full scan by ~37 % per call (b13_snap at 550: 184 -> 116 us per call) -> **~18-22 ms/s, effort medium**.
Stage 2, worker version (ParallelScan-style, scores computed on workers as a job dependent on the team job, served
in FinishExecution): ~35-45 ms/s, effort medium-large. Self-check like the existing items.

**E2. Friendly-fire check on plain data** - `Target.CheckRiskOfFriendlyFire` (Target.cs, static, ~662-790): 52.8 ms/s,
7092 calls/s x 7.4 us, frame-driven, FriendlyFire_Grid in "bypass (dense cluster)". Per entry within maxDist
(most of the own team in a 45 m melee) vanilla does `Targets[index]`, Unity equality vs ignoreTarget,
`GetIgnoreFriendlyFireForLevel`, `Owner.PhysicalRadius` (virtual), `Vector3.Angle`, `Atan2`. A per-frame array next to
TargetGrid's native data (instance id, PhysicalRadius, ignore levels) runs box + cone test on plain floats in
vanilla order and calls the vanilla object code only for entries that pass the cone. **~20-30 ms/s, effort medium.**
Exact as long as PhysicalRadius / ignore levels do not change within a frame (self-check).

**E3. Skip the no-op wheel joint write** - `VehicleWheeled.OrderedFixedUpdate` writes
`joint.rotationDriveMode = RotationDriveMode.XYAndZ` for every awake wheel every fixed step (VehicleWheeled.cs:1345),
while the steer limit and target rotation writes next to it are already change-guarded (:1404-1420). Only
`SleepVehicle` sets another mode (:570). Transpiler: write only when the getter differs. Exact while the chassis is
awake (all fighting vehicles); for a sleeping chassis with an awake wheel it no longer wakes the chassis (the
ParkedWheelHold case; arguably a fix). ~1 550 alive wheel bodies x 25 steps = ~39 k native setter calls/s ->
**~10-25 ms/s of fixed-script time + an unknown PhysX share (joint re-configuration), effort small**; decide by one
flip (PhysX ms/step and VehicleWheeled fixed ms/s).

**E4. Batch the projectile synchronous casts** - ProjectileJobManager.cs:262-345, ProjectileBasic.cs:1050-1186.
Only if probe #35 shows the sync path >= 30 ms/s: schedule SpherecastCommand / RaycastCommand (maxHits) in P1 for
projectiles inside bounds, keep vanilla's min-distance selection and filters. **~15-30 ms/s, effort medium.**

**E5. Creature `CanHitTarget` per-collider work** - CreatureAttack.cs (CanHitTarget melee branch): collider ->
DamageManager cache keyed by instance id, invalidated on object destroy. Only if probe #25 >= 25 ms/s.
**~10-20 ms/s, effort small-medium.**

### Approximate (visible consequence stated)

**A1. `DeadDisposeEarly` proportional (b17, running as b17_dispose_prop)** - b15_best_plat: new 27.3 -> max 40.1 fps
at 550 (-31 % frame work) with 5 s disposal. Consequence: corpses and wrecks vanish after 5 s instead of 15 s while
the server is loaded. Largest single lever left; effort done.

**A2. `Physics.FixedStep_Override` 20 Hz (qc_fixed20 queued)** - fixed phase in P = PhysX 336 + handlers 114 =
450 ms/s, all per step: -20 % = **~90 ms/s (model ~32 -> ~48 fps)**. Consequences listed in `TimeStep.cs` (50 ms
integration step: softer wheel joints and contacts, deeper penetration, ground checks 6.7 Hz, avoidance broadphase
5 Hz, collision-damage gate). Needs a client.

**A3. Vehicle solver iterations (new, measure first)** - live vehicles are the PhysX bill: PhysX per step follows
the alive awake bodies (450 plateau: ~1 440 alive awake, 10.4 ms/step; 550: ~1 800, 13.4 ms/step; 7.2-7.5 us per
alive awake body per step), 86 % of them on ~200-260 alive wheeled vehicles (7.5-7.7 jointed bodies each; census
`| dead` segment, b17_pscan). Dead bodies are ~10x cheaper: making ~450 dead bodies kinematic (DeadSettle, b13_dead)
cut PhysX per step only ~3 % (12.74 -> 12.35 ms) and cost more in fixed scripts than it saved. So the remaining PhysX
lever is per live vehicle: `Rigidbody.solverIterations` 6 -> 4 on chassis + wheel bodies (PhysX uses the island
maximum, so all bodies of an island must be lowered). Solver share of PhysX unknown; if ~40 %: **~40-50 ms/s**.
Consequence: softer suspension / joint drift, more penetration in vehicle jams, possible jitter visible to drivers.
Effort small; one flip with PhysX ms/step, then a client check.

**A4. `Targeting.FriendlyFire_Cache` (exists)** - reuse an answer for 0.15 s; in night 1 it answered 45 % of calls ->
~15-25 ms/s of the 52.8 here. Consequence: a friendly-fire veto up to 0.15 s stale. E2 makes it mostly unnecessary.

**A5. Behaviour-tree interval for engaged agents (new)** - `m_BTUpdateDelay = 0.5f` (AIBaseAgent.cs:1481) to 0.75 s for
agents that have a visible aim target: BT ~77 ms/s -> ~-20-25 ms/s. Consequence: re-target / chase / cover
decisions up to 0.25 s later (aim and fire are outside the tree and stay per frame).

Rejected / already answered: DeadSettle (no gain, b13_dead), LosTopK (more null results, ~10 % more scans;
confirmed by b17), ParallelScan inline (-2.7 %, s.e. 1.6: the gain needs the workers), null-result cache (needs >= 0.8 s,
visible).

## 5. Not determined

* Split of `BTEnemyTargetDetected` rest between prefiltered scans and the rest of the tree is inferred from RequestScan
  minus CollectDetectedEnemies (~50 vs ~7 ms/s in S3); the probe run decides.
* Sync share of projectile P2, the CanHitTarget share of the creature late update, LOS request cost: not probed.
* PhysX internals (joint solver vs contacts vs broadphase) and whether the redundant rotationDriveMode write costs
  inside PhysX: not visible to the probe; E3 and A3 need flips.
* Arm P section table rests on 3 intervals (30 s); fps and work from the 83 s PerfLab samples agree (32.0 vs 31.9).
* c1 (dt-proportional per-frame cost) is from two plateaus per arm; T' carries a +-15 ms/s uncertainty.
* Runs not finished when this was written: b17_dispose_prop, b17_dispose_always, qc2, qc_fixed20, b15_eveh.
