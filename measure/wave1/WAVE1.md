# Si_ServerPerf wave 1: lab and AI-round measurements (2026-11-16)

Lab server `C:\tmp\silica_ds_lab`, Silica 1.0.17, headless, 0 clients, 120 fps cap, Si_ServerOptimizer 0.7.1 loaded in
every arm (the "lab set as it is"); setup, mod set and the no-mod baseline are in `..\baseline_1017\BASELINE.md`,
the method in `..\PROTOCOL.md`. Every table names the Si_ServerPerf build it comes from (`release\b<n>`; b1 = `bin\Release` 03:52).

How to read the numbers:
* **work** = Si_PerfLab frame CPU work in ms per frame, without the frame-cap wait (lower is better).
* **Paired flip**: one server process; `runtime.json` alternates between two (or more) states every 20 s (lab) or
  30 s (AI round); the first 3 s after each flip are dropped; each block is compared with its neighbouring blocks of
  the reference state. "s.e." = standard error of those paired differences. This removes drift and run-to-run noise;
  an effect is clear when it exceeds about 3 s.e.
* **Separate-pass noise** (two baseline passes without the mod): 0-7 % on stationary scenarios, 5-18 % on fights.
* `"*": false` switches every item off except the startup-only `Headless.Particles_Disable`, which stays on in all
  arms (as instructed). Default-off items are not patched at all unless a run's config enabled them.

## 0. Summary

* **Large battles (the real question): Si_ServerPerf at defaults raises fps by about half once the server leaves
  the cap.** Synthetic three-way battles, b9, paired flips in one process, damage disabled so the count holds:
  | world units | fps "*" off | fps defaults | fps aggressive |
  |---:|---:|---:|---:|
  | ~330 | 108 | 112 | 114 |
  | ~480 | 78 | 90 | 92 |
  | ~620 | 39 | 60 | 63 |
  | ~800 | 15 | 22 | 25 |

  Frame work drops 14 / 27 / 37 / 37 % (defaults) and 24 / 29 / 39 / 41 % (aggressive) at these four sizes. The
  paired s.e. is 2-8 % of each effect. At ~620 units the defaults keep 60 fps where vanilla has 39. PhysX is now the
  largest remaining block and no default item touches it.
* **Lab scenarios (paired flips, defaults vs everything off):**
  * idle soldiers: -38 %
  * mixed late-game set: -31 %
  * structures: -20 %
  * moving soldiers: -15 %
  * moving vehicles: -5 %
  * idle vehicles: nothing
  * dense vehicle fight: a +8.5 % cost in b4, fixed in b5 (now -7 %)

  The separate full pass against the two no-mod baseline passes agrees (idle soldiers -41 %, mixed -30 %).
* **AI rounds** stayed below 320 units (the human AI stalls; the control round without the mod stalls the same way).
  Below the cap the gain is headroom, not fps:
  * round #1 at ~280 units: main-thread load 619 -> 492 ms/s (-20.5 %) with the defaults; fit -23 % at 300 units
  * round #2 at ~150 units: -12 % (defaults) and -21 % (aggressive)

  fps stayed at the 120 cap in all arms. Nothing above ~320 units has been measured in a real round.
* **Correctness:** 0 mismatches in every self-check:
  * Targeting (b4 and b7 code): FriendlyFire 5418 + 3066 checks, Rank 735 + 254
  * Services verify: 407 k index lookups, 255 k terrain lookups
  * Register_O1: 85 k registrations
  * CommanderAI_ClusterRebuild: 660 rebuilds, ours 28 ms vs vanilla 84 ms
  * MultiCore maps: 4470 team-frames
  * DetectedGrid desync 0; no item FAILED; 0 exceptions in every arm of both AI rounds and the battles
* **Recommended defaults** as shipped (Headless, UpdateTiers without Tier_IdleAgents, Targeting exact items,
  Services, Physics census + catch-up cap). "Aggressive" extras that measurably help: SensorPipeline_Rate,
  LosTopK, FriendlyFire_Cache; they are approximate, and only their time was measured. Keep off:
  * Tier_IdleAgents (costs)
  * MultiCore.SensorTeamMaps_Job (does not pay)
  * the Physics sleep items: below the author's estimate in b7/b9; the b10 slot (idle-test fix) was not completed;
    order-response latency unmeasured

## 1. Headline: sustained three-way battles at 330-800 units (`battle_b9`, 14:01-14:33, build b9)

Two AI rounds stayed below 320 units, so the high-unit range was measured with synthetic battles instead
(`make_battle_scenarios.py` -> `scenarios_battle.json`):
* **Layout:** four battle clusters (a three-way at the map centre and three two-way fronts ~350 m apart), 10 % of
  each team patrolling in transit, and 28 structures per team.
* **Composition:** Sol riflemen / heavies / LightStriker / HeavyStriker / PulseTruck; Centauri troopers / juggernauts /
  AssaultCar / StrikeTank / CombatTank; aliens Crab / CrabHorned / Hunter / Scorpion / Squid.
* **Orders:** attack-move legs of 60 m toward the map centre.
* **Arms:** A `"*"` off, B defaults, C defaults + SensorPipeline_Rate + LosTopK + FriendlyFire_Cache. 30 s blocks,
  4-5 blocks per arm, 390 s per size.
* **Damage off:** the count only held after Si_PerfLab 0.1.1 disabled all damage during such scenarios (the game's
  `DamageManager.DamageCheatMultiplier = 0`); two earlier attempts lost 60 % of the units within two minutes and were
  aborted. Units target, aim and fire (530-1450 projectiles in flight), but damage application and deaths are not part
  of these numbers.

| world units | arm | fps median / p5 | frame work ms (paired change vs A, s.e.) | frame avg / p95 / p99 ms | main busy ms/s |
|---:|---|---|---|---|---:|
| 329 | A off | 108 / 103 | 6.59 | 9.3 / 16.1 / 16.6 | 708 |
| | B defaults | 112 / 107 | 5.75 (**-14 %**, 0.04) | 9.0 / 15.5 / 16.0 | 633 |
| | C aggressive | 114 / 110 | 4.99 (**-24 %**, 0.02) | 8.9 / 15.7 / 16.4 | 564 |
| 477 | A | 78 / 64 | 12.9 | 12.8 / 22.4 / 25.3 | 966 |
| | B | 90 / 78 | 9.95 (**-27 %**, 0.50) | 11.1 / 20.7 / 21.5 | 863 |
| | C | 92 / 85 | 9.16 (**-29 %**, 0.16) | 11.0 / 20.7 / 21.6 | 830 |
| 617 | A | **39** / 32 | 25.9 | 25.7 / 39.0 / 43.7 | 1000 |
| | B | **60** / 45 | 17.2 (**-37 %**, 0.67) | 16.2 / 30.3 / 34.7 | 987 |
| | C | **63** / 49 | 15.7 (**-39 %**, 0.24) | 15.9 / 30.0 / 34.4 | 968 |
| 800 | A | **15** / 13 | 70.1 | 69.1 / 93.7 / 102 | 1000 |
| | B | **22** / 19 | 45.2 (**-37 %**, 2.1) | 46.3 / 72.0 / 80.1 | 1000 |
| | C | **25** / 21 | 41.7 (**-41 %**, 0.9) | 41.4 / 63.0 / 79.3 | 999 |

* **Thresholds:** 120 fps is lost below ~330 units in every arm. 60 fps: A between 477 and 617, B at ~617, C just
  above it. 30 fps: every arm between 617 and 800.
* **Where it comes from** (ms per frame at 617 units, A -> B -> C):
  * Update scripts 9.8 -> 6.7 -> 5.8; ordered update list 7.1 -> 5.3 -> 4.3; late-update scripts 4.1 -> 3.3 -> 3.2
  * VehicleTurret turrets 2.63 -> 1.80 -> 1.34
  * Soldier.OrderedUpdate 1.16 -> 0.82 -> 0.63; AIBaseAgent agents 1.09 -> 0.75
  * fog 0.68 -> 0.08; animators 0.94 -> 0.01; SensorTickPart1 0.40 -> 0.39 -> 0.13
  * PhysX per fixed step essentially unchanged (11.7 -> 11.2; at 800: 14.2 -> 15.0). Physics is the largest
    remaining block and no default item touches it.
* **Targeting grid in real use for the first time** (arm B):
  * 329 units: all teams bypass ("few queries per build").
  * 477: FriendlyFire_Grid GRID ("pays"), share 100 %, a query visits 9 % of a 178-entry cell.
  * 617: Alien DetectedGrid GRID (D 461), FriendlyFire GRID, visits 9 % of 224.
  * 800: Centauri and Alien DetectedGrid GRID (D 465 / 596), Sol bypass "no pruning" (D 522), CollectEnemies grid
    share 41 % (visits 10 % of D 533), DetectedGrid 11 builds/s x 513 entries.
  * C at 800: LosTopK saves 3107 LOS queries/s; FriendlyFire_Cache 6200 hits/s of 10 703;
    SensorPipeline_Rate 13 runs/s at 24 fps.
* Exceptions: 0 in every arm and size. No item FAILED. Main-thread busy saturates at ~600 units in A, so fps and frame
  time are the measures there. Per-frame costs of dt-driven work grow at low fps, which favours the faster arms a
  little at 800 units.

## 2. Lab scenarios: all defaults vs everything off (paired flip, build b4)

Run `flip_all` (05:00-05:26, b4 = b2 + unpatched MultiCore + ClusterRebuild, which is idle in the lab because
Si_PerfLab disables the vanilla commanders). 7 stationary scenarios, 180 s measure each, 4-5 blocks per arm.

| scenario | off (ms) | defaults (ms) | saving ms | saving % of off | s.e. | verdict |
|---|---:|---:|---:|---:|---:|---|
| idle-soldiers-600 | 3.19 | 1.97 | **1.21** | 38 % | 0.01 | clear |
| move-soldiers-300 | 3.05 | 2.57 | **0.47** | 15 % | 0.01 | clear |
| move-vehicles-300 | 5.57 | 5.32 | 0.26 | 5 % | 0.04 | clear, small |
| mixed-lategame (200 panels + 400 soldiers) | 3.20 | 2.22 | **1.00** | 31 % | 0.03 | clear |
| structures-built-30 | 1.01 | 0.82 | 0.20 | 20 % | 0.005 | clear |
| fight-soldiers 300 v 300 (units die: 185/600 alive at end) | 5.44 | 4.08 | 0.82 | 15 % | 0.26 | likely (3 s.e.) |
| fight-vehicles 150 v 150 (invulnerable, 300/300 alive) | 14.29 | 15.34 | **-1.22** | **-8.5 % (costs)** | 0.35 | clear regression in b4, **fixed in b5** (section 4) |

Sanity anchor "mod loaded, all off" vs "no mod" (separate passes, so within the 0-7 % pass noise only):
idle-soldiers-600 3.19 vs 3.33, mixed 3.20 vs 3.27, move-vehicles-300 5.57 vs 5.84, structures-built 1.01 vs 0.99.
Loading the mod with everything off does not make the server slower. No A/A null flip was run (time); the paired
s.e. column is the noise figure of the flip method.

## 3. UpdateTiers (build b2)

Leave-one-out flip `flip_no_UpdateTiers` (04:40-04:50): A = all defaults, B = defaults without `UpdateTiers.*`.
The run's driver died at 04:50 (my mistake while re-ordering the chain); the flips were reconstructed from the mod's
own "runtime.json:" apply lines, so only the first three scenarios carry A/B data.

| scenario | defaults (ms) | without UpdateTiers (ms) | UpdateTiers saves | s.e. |
|---|---:|---:|---:|---:|
| idle-soldiers-600 | 2.01 | 2.64 | **0.62 ms (24 %)** | 0.04 |
| idle-vehicles-300 | 2.32 | 2.24 | -0.04 (inside noise) | 0.05 |
| move-soldiers-300 (flipped for 2 of 2.7 min) | 2.58 | 2.60 | 0.02 (inside noise) | 0.01 |

* The b2 fix works: in b1, Tier_IdleSoldiers skipped 4 % of the idle riflemen's updates ("early 26 693/s"). In b2 it
  shows "early 0/s, skipped 75 %" for 600 idle riflemen, with an empty why-not-quiet breakdown ("early by cause /s: -";
  mean rejected input 0, n 0).
* Tier_Structures, Tier_OrderProcessors and Tier_StaticNetTransforms skip 75-80 % in the lab. `classified` > 0 for
  every tier, so Mono's `Delegate.Method` resolves (the open question is closed).
* No vehicle tier exists, so idle vehicles gain nothing from UpdateTiers. Moving soldiers are never idle, so no gain
  there either, as designed.
* In the AI verify segment (b5), Tier_OrderProcessors skipped only 15 % (orders are live) and Tier_Structures 72 %.
* Register_O1: b2's "out-of-step" counter stayed 0 in every run. Verified 85 251 (lab) and 443 (AI) registrations
  with 0 mismatches. dup/miss counts are benign by the author's definition.

## 4. Targeting (builds b4 and b5)

* **b4: costs in dense fights.** In fight-vehicles 150 v 150 (all units within one 100 m cell), FriendlyFire_Grid ran
  ~18 000 queries/s that each visited 48-70 % of a 152-entry cell, plus 53-66 grid rebuilds/s. CollectEnemies fell back
  to vanilla for 50-70 % of its queries. Net: +1.22 ms/frame (+8.5 %), concentrated in
  `VehicleTurret.OrderedUpdateAllTurrets` (172 vs 138 ms/s).
* **b5: fixed by the never-worse gate.** Same scenario (`smoke_mc_b5`, 3-4 blocks per arm): both teams in bypass
  ("no pruning (dense cluster)", near/L 0.91, ~250 q/frame), DetectedGrid bypass ("no pruning" / "few queries per
  build"), grid share 0-2 %. Frame work: defaults 10.36 ms vs "*" off 11.10 ms, i.e. the defaults now save
  ~0.7 ms (unpaired means of 3 blocks each; the paired estimate is noisy because the third arm sits between them).
  FriendlyFire_Grid therefore stays on.
* In every other lab scenario the detected lists are 0-3 entries long, so Targeting has nothing to act on there.
  It stayed in bypass in both AI rounds as well; it only switches to GRID and pays in the 450-800-unit battles
  (section 1).

## 5. Correctness

**Lab pass `correctness`** (05:26-05:40, b4; not for timing). Scenarios: idle soldiers/vehicles 300, fight-soldiers
150 v 150 and 300 v 300, fight-vehicles 150 v 150, fight-creatures 150 v 150, structures-built-30, mixed-lategame,
unit-churn-300. Self-checks: Targeting SelfCheckFraction 0.05, Services VerifyAgainstVanilla, UpdateTiers
RegisterVerifyEveryN 1, plus WakeProbe.

| check | result |
|---|---|
| Targeting self-check | **0 mismatches**: CollectEnemies 215 checks, Rank_Sort 254, UrgentClose 25, FriendlyFire 3066, Nearest/Alarm 0 (unused). DetectedGrid index desync total 0 |
| Services VerifyAgainstVanilla | Detected_IndexLookups 407 281 checked / **0 mismatches**; TerrainMaterial_Cache 255 109 / **0** |
| UpdateTiers Register_O1 verify every call | 85 251 verified / **0 mismatches**, out-of-step 0 |
| FAILED / NOT APPLIED | none. Exception: the diagnostic `Physics.WakeProbe` was "ON 13/13" but `Soldier.OrderedFixedUpdatePartTwo(Single)` was NOT APPLIED ("HarmonyException: IL Compile Error"); fixed in b6 |
| Diag_LogExceptions | 0 exceptions/warnings, also at quit |

Targeting's code paths changed in b5 (never-worse gate). The b5+ re-check is the `targeting_check` run (section 8).

**AI verify segment `verify_ai_b5`** (05:49-06:01, b5, 11 min of a headless 3-way round, Services
VerifyAgainstVanilla on; arms = defaults with and without MultiCore). Services.CommanderAI_ClusterRebuild:
**"VERIFY: checked 660, mismatches 0, vanilla 83.6 ms vs ours 28.1 ms over 660 calls"**, 1 280 967 vanilla pair
checks avoided. MultiCore self-check: 3336 team-frames, 3333 identical, 3 "stale" (by the author's definition the
check and the map fill saw a list that changed in between, the same way vanilla would), 0 different.
One exception in the segment (`ArgumentOutOfRangeException` in vanilla `Silica.AI.AITask_Attack.IssueOrders` <-
`CommitAttack` <- `UpdateStaging`, 05:55:50). It fired while the defaults + MultiCore arm was active, 28 s after its
flip. Arm attribution in the full rounds: section 9.

## 6. MultiCore (b5)

`MultiCore.SensorTeamMaps_Job` smoke (`smoke_mc_b5`, SensorMapsMinEntries lowered to 16, SelfCheckFraction 0.02):
6/6 targets patched. Job path active in the vehicle fight: team-frames/s 567 (worker 354, sync small 213), sensor jobs
chained/s 338, late 0. Self-check 1134 identical, 0 different. Own estimate "vanilla 4.00 ms/s (59 ns/entry) -> net
main gain ~2.31 ms/s". No exception; units kept fighting. Watch point from the smoke: in the 3-arm flip the MC arm
was +1.26 ms/frame slower than the same defaults without MC (s.e. 0.54, 3-4 blocks). That is far more than its own
counters account for, and that arm also carried the self-check and the lowered entry threshold. AI round #1 has it
with the self-check off; result in section 9.

## 7. Physics diagnostics and sleep candidates (b4 diagnostics, b7 and b9 arms, b10 not completed)

**Diagnostics.**
* Census (b4/b5, lab, no flips): idle soldiers and creatures are 100 % asleep in vanilla 1.0.17. Parked LightStrikers:
  ~9 dynamic bodies each, 42 % asleep after a long settle (correctness run). Fighting or moving vehicles: 0-3 % asleep.
* WakeProbe (b4; one target NOT APPLIED until b6) found almost no wake-ups inside the probed methods: avoidance woke
  1-8 bodies per 10 s, ApplyAvoidanceForces woke 12 of 20 971 pushes. Parked vehicles are not being woken; they simply
  never fall asleep.
* WheeledSleepDiag (b7), LightStriker, idle-vehicles-300, sums over 10 samples: awake 1696-2977 of 3000 vehicle-samples.
  Of the awake ones: "timer" 60-78 %, "pushed" 75-87 %, "slope" 88-100 %, "fast" only 19-24 % (wheel angular speed;
  fastPushed ~ fast), condMet 2-16 %, input ~0. The mixed set (HeavyStriker, AATruck, BarrageTruck) shows the same
  pattern. Reading: neither avoidance alone (only ~20 % are above the fast thresholds), nor AI input, nor the
  sleep-timer handshake (condMet low). Vehicles parked on slopes keep a small residual wheel motion that keeps
  resetting vanilla's sleep timer.

**Four-arm flip `physics_cands`** (12:50-13:13, b7, 3/3 ok; the first attempt at 06:30 loaded 0 scenarios because of
a JSON serialisation error of mine and is kept as `physics_cands_FAILED_0scenarios`). 60 s blocks, first 30 s after
each flip dropped, 2 blocks per arm. Arms cycle A-B-C-D, so only B and D have paired estimates against A; C uses
unpaired means.

| scenario | arm | wheeled asleep | work ms | change vs A | PhysX ms/s | CollisionAvoidance fixed ms/s |
|---|---|---:|---:|---:|---:|---:|
| idle-vehicles-300 | A defaults | 2 % | 3.85 | | 160 | 29.5 |
| | B AvoidanceIdlePairSkip | 30 % | 3.57 | -0.28 (s.e. 0.16) | 139 | 32.9 |
| | C IdleForceSleep | 59 % | 3.20 | -0.65 (unpaired) | 113 | 17.8 |
| | D both | 60 % | 3.17 | **-0.68 (-17.7 %), s.e. 0.09** | 114 | 18.8 |
| parked mixed 300 | A | 2 % | 4.06 | | 172 | 31.0 |
| | B | 6 % | 4.12 | +0.06 (s.e. 0.03) | 172 | 35.2 |
| | C | 28 % | 3.66 | -0.40 (unpaired) | 154 | 24.8 |
| | D | 34 % | 3.67 | **-0.39 (-9.7 %), s.e. 0.03** | 147 | 24.4 |
| move-vehicles-300 (guard) | A / B / C / D | 0 % in all | 5.56 / 5.65 / 5.71 / 5.68 | 1 block each: inconclusive | 233-237 | 37-41 |

* Counters (idle-vehicles, median per 10 s): B avoidSkip 20 440 of 184 139 calls; C reSleep 18 200,
  sleepBlockedByForce 6 747, forcedNow 250; D avoidSkip 9 470 of 85 062, forcedNow 246.
* Move guard: patrolling vehicles never sleep in any arm (sleepBlockedByForce 0 while moving). avoidSkip is 0 in D
  while moving (B alone: 892 per 10 s, 0.6 %). All 5561 orders succeeded, all vehicles alive. The constant forcedNow
  16 belongs to the 6 base vehicles outside the lab group.
* Caveat: arm A's 2 % is lower than settled vanilla (42 %), because each switch to A releases the forced sleepers and
  they do not settle again within a 60 s block. The time comparison holds anyway: A's 3.85 ms equals the no-mod
  baseline (3.78) and the defaults pass (3.79).
* **IdleForceSleep does the work** (AvoidanceIdlePairSkip adds nothing measurable on top). **Not measured: per-vehicle
  order-response latency and arrival times**; Si_PerfLab cannot time those. The physics items therefore stay default
  OFF until tested with a client. They are in round #2's aggressive arm only.

**b9 follow-up (`physics_b9`, 14:34-15:05).** b9 fixed IdleForceSleep's partial re-sleep and added
AvoidanceSkipOntoResting plus four diagnostic lines. Cycle A (30-60 s after a release) - A2 (60-120 s, "settled")
- C IdleForceSleep - A - A2 - E (AvoidanceSkipOntoResting + IdleForceSleep) - P (AvoidanceIdlePairSkip alone,
diagnostic); 60 s blocks, 1-2 blocks per arm.

| scenario | A / A2 asleep | C asleep | E asleep | work A / C / E ms | PhysX A / C / E ms/s |
|---|---|---:|---:|---|---|
| idle-vehicles-300 | 1 % / 7 % | 45 % | 51 % | 4.53 / 3.76 / 4.29 | 197 / 143 / 141 |
| parked mixed 300 | 5 % / 6 % | 24 % | 24 % | 4.37 / 4.16 / 4.30 | 187 / - / - |
| move-vehicles-300 | 0-1 % in every arm | | | 6.05-6.20 | |

* **Below the author's estimate** (80-90 % / 55-70 % asleep, -21 % frame work). The dominant blocker is that the
  "idle" parked AI vehicles report non-zero AI input:
  * idlesleep, C: notIdle input = 10 627, speed = 531
  * avoidresting, E: receiverNotIdle input = 311 992 of 359 772 calls, skip 12 %
  * avoidpairs, P: missSelf input = 72 447, skip 13 %
  * Rewake causes: C "force" 13 169, E "scripts" 12 827. partialFix never triggered.
* A in this run (4.53 ms, PhysX 197) is heavier than in the b7 run (3.85, 160) and the no-mod baseline (3.78). The b9
  diagnostics probably cost something in every arm; that remains to be checked.
* Vanilla does not settle within one block either (A2 7 %); the 42 % of the correctness run took minutes.
* **parkeddrift:** force-slept vehicles 0 cm. **Awake parked vehicles drift by decimetres in plain vanilla (arm A):**
  AATruck mean 96 cm horizontal (max 206) and 24 cm vertical (max 84) per 10 s window; HeavyStriker, BarrageTruck and
  LightStriker up to ~1.5-1.7 m horizontal and ~0.4 m vertical. That is existing vanilla behaviour on slopes, not
  caused by the items. No jump at wake-up is visible.
* Move guard: patrolling vehicles stay awake (moveOrder excluded ~14 300 times), avoidSkip ~0 while moving, all 9420
  orders ok.
* **Physics sleep items: keep default OFF.** IdleForceSleep would only pay if parked vehicles with meaningless
  residual AI input counted as idle.

**b10 slot (`physics_b10`, b10): NOT COMPLETED.** b10 changed the idle test to vanilla's rule (a parked vehicle keeps
its last steering angle, which the module had counted as input), added `AvoidanceNoIdleDetour`, and moved the
diagnostics to default-off items. The slot (arms A / C / F, 3 blocks each) was started at 15:29 and stopped at 15:33
before its first measure block, because the user needed the machine. It has no data, is marked
`INCOMPLETE_CONTAMINATED.txt` and is listed under "remaining" (section 14).

## 8. Other default-off candidates, Targeting re-check, SensorPipeline_Rate (build b7)

**`flip_candidates`** (07:05-07:23, b7, 5/5 scenarios ok). A = defaults; B = defaults + Tier_IdleAgents,
RestWriteGuards, HitZonePoseGuard, LosTopK, FriendlyFire_Cache (all five together; the two physics sleep items are
off in both arms). Paired, 4-5 blocks per arm:

| scenario | defaults (ms) | + candidates (ms) | change | s.e. | verdict |
|---|---:|---:|---:|---:|---|
| fight-vehicles 150 v 150 (invulnerable) | 12.88 | 11.62 | **-1.38 (-10.7 %)** | 0.13 | clear gain |
| idle-soldiers-600 | 1.90 | 1.93 | +0.035 (+1.8 %) | 0.003 | small cost, clear |
| mixed-lategame | 2.25 | 2.28 | +0.03 (+1.3 %) | 0.015 | small cost, 2 s.e. |
| idle-vehicles-300 | 4.13 | 4.12 | +0.01 | 0.016 | none |
| move-vehicles-300 | 5.59 | 5.61 | +0.02 | 0.03 | none |

* The fight gain comes from the two approximate Targeting items. LosTopK: grid share 78 %, "LOS queries saved/s 4273".
  FriendlyFire_Cache: q/s 25 349, "hits/s 22 503", grid share 89 %. They were not self-checked (approximate by
  design), so their behavioural effect (targets picked, friendly-fire decisions) is unmeasured.
* Tier_IdleAgents skips 75 % of agent updates when units are idle (early 0/s). While they patrol or fight it skips only
  8-9 % (early by cause: path 21 184/s while patrolling, aimTarget 15 651/s in the fight). No measurable gain.
* RestWriteGuards / HitZonePoseGuard are active (census guardSkip up to 311 k, poseSkip up to 225 k per 10 s), with no
  measurable time effect.
* **Exceptions: 4 in arm A (plain defaults), 0 in arm B**, all in the vehicle fight: `NullReferenceException` in the
  Harmony-patched `Target.CheckRiskOfFriendlyFire` <- `TurretWeapon.OrderedUpdate` (first at 07:18:59). The method
  carries the FriendlyFire_Grid patch in both arms. Per the Targeting author this is vanilla's own body throwing on
  a destroyed target (section 12, item 2); 4 vs 0 is consistent with chance.

**`targeting_check`** (07:23-07:32, b7, 4/4 ok): Targeting SelfCheckFraction 0.05 on four fights.
A = defaults + `MultiCore.SensorPipeline_Rate`, B = defaults without it.
* Targeting self-check on the b5+ code: **0 mismatches** (FriendlyFire 5418 checks, Rank_Sort 735, UrgentClose 5,
  CollectEnemies 4). No FAILED, 0 exceptions in either arm.
* **SensorPipeline_Rate smoke passed.** ON 4/4 targets. Pipeline runs 19.8/s at 119 fps and 16.5-17.3/s at 74-84 fps,
  deletion runs 0-1.2/s, forced 0. Units detect and kill normally (fight-vehicles 135/300 alive at the end,
  fight-soldiers 55/300), no NullReferenceException. In the invulnerable vehicle fight, SensorTickPart1 is 4.2 ms/s
  with the item vs 16.1 without, and frame work drops by 0.39 ms (3.4 %, s.e. 0.17).
* The lab's fight-creatures scenario does not engage reliably (0 projectiles and everyone alive in 3 of 5 runs,
  baseline pass 1 included). Its numbers are effectively idle-creature numbers.

## 8b. Full lab pass with the mod at defaults vs the two baseline passes (b7, separate passes)

`perf_lab_p1` (07:33-08:07, 21/21 ok). Frame work avg, baseline mean of p1/p2 -> with mod; noise = baseline spread:

| scenario | baseline | with mod | change | abs(change)/noise |
|---|---:|---:|---:|---:|
| baseline-empty | 0.92 | 0.75 | -18 % | 2.0 |
| idle-soldiers-100 / 300 / 600 | 1.29 / 2.08 / 3.33 | 0.91 / 1.30 / 1.97 | **-29 / -38 / -41 %** | 4.3 / 6.9 / 9.6 |
| move-soldiers-100 / 300 / 600 | 1.64 / 2.97 / 4.93 | 1.34 / 2.50 / 4.03 | -18 / -16 / -18 % | 9 / 8 / 128 |
| idle-vehicles-100 / 300 | 1.91 / 3.78 | 1.75 / 3.79 | -8 % / **+0.5 %** | 5.6 / 0.2 |
| move-vehicles-100 / 300 | 2.29 / 5.84 | 2.14 / 5.90 | -6 % / **+0.9 %** | 148 / 0.2 |
| idle-creatures-300 | 2.01 | 1.36 | -32 % | 8.2 |
| fight-soldiers-50 / 150 / 300 | 1.31 / 1.95 / 3.11 | 1.04 / 1.51 / 2.20 | -21 / -22 / -29 % | 4.3 / 2.3 / 2.2 |
| fight-vehicles-50 / 150 | 2.18 / 7.24 | 1.89 / 8.55 | -13 % / +18 % (see below) | 2.8 / 1.5 |
| fight-creatures-50 / 150 (did not engage) | 1.15 / 1.61 | 0.99 / 1.42 | -14 / -12 % | 1.0 / 0.6 |
| structures-built-30 | 0.99 | 0.81 | -18 % | 3.4 |
| mixed-lategame | 3.27 | 2.29 | **-30 %** | 124 |

Main-thread CPU (ms per wall-second): idle-soldiers-600 426 -> 279 (-35 %), mixed-lategame 426 -> 316 (-26 %),
move-soldiers-600 626 -> 529 (-16 %). fight-vehicles-150 is +18 % but a different fight: 147 units survived vs
114 / 121 in the baseline passes. That is 1.5 times the pass noise, so inside it; the paired flip with invulnerable
units (sections 4, 8) is the reliable figure for that scenario. **Large vehicle groups (300 idle or moving) gain
nothing from the defaults**; the per-vehicle cost sits in VehicleWheeled updates, PhysX and avoidance, which no default
item addresses.

## 9. AI cycling rounds (round #1 b5, round #2 b8, control round without the mod)

Method: one headless 3-way round on GreatErg with Si_RTS_AI's harness, PerfProbe targeted mode at 10 s intervals.
`runtime.json` cycles the arms every 30 s; the first 3 s after each flip are dropped, and only PerfProbe intervals
lying completely inside one block count. Load metric = REPORT.md's normalised load (per-frame cost x 120 + per-fixed-
step cost x 25, ms per wall-second; 1000 = the 120 fps cap is just held). The startup runtime file is "all defaults",
so the startup-only `Headless.Particles_Disable` is ON in every arm. `Headless.Diag_LogExceptions` stays on in the
"off" arm from 06:05 on, so exceptions can be attributed per arm. Reports: `<run>\ROUND_ARMS.md` (bins, fits,
section shares), `<run>\WINDOWS.md` (item counters and exceptions per arm), `<run>\arms_*.csv`.

### Round #1 (`round1_arms3`, 06:03-06:30, b5, Si_RTS_AI preset `lab-3way` = all factions under the mod)

Arms: A = `"*"` off, B = defaults (without MultiCore), C = defaults + `MultiCore.*`. **The aliens won at t = 25.5 min**
(peak 317 units, 273 structures; Sol and Centauri stalled at about 10 structures). The round therefore only covers
0-317 units, with 26-28 clean intervals per arm (1-2 per arm per bin).

| units | load A (off) | load B (defaults) | load C (+MultiCore) | B vs A | C vs A | fps A / B / C |
|---:|---:|---:|---:|---:|---:|---|
| 0 | 213 | 183 | 197 | -14 % | -8 % | 117.7 / 118.2 / 118.4 |
| 50 | 247 | 229 | 227 | -7 % | -8 % | 116.7 / 117.0 / 117.0 |
| 100 | 366 | 350 | 303 | -4 % | -17 % | 116.3 / 116.3 / 119.3 |
| 150 | 432 | 367 | 389 | -15 % | -10 % | 116.9 / 119.4 / 116.6 |
| 200 | 507 | 426 | 442 | -16 % | -13 % | 116.1 / 116.9 / 117.0 |
| 250 | 632 | 524 | 515 | -17 % | -19 % | 114.8 / 115.8 / 117.0 |
| 300 | 736 | 594 | 589 | -19 % | -20 % | 109.8 / 115.9 / 115.7 |

Fits (all clean intervals, n 21-25 per arm, R2 0.96-0.98, valid up to ~310 units only):
A = 136 + 1.50U + 0.00105U^2 (681 ms/s at 300 units), B = 119 + 1.66U - 0.00099U^2 (526, -23 %),
C = 155 + 1.06U + 0.00088U^2 (552, -19 %).

At about 280 units (n 10 / 6 / 9 intervals), main-thread busy (1000 - cap wait) is **A 619, B 492 (-20.5 %),
C 519 ms/s**. Where the defaults save (A -> B, ms per wall-second):

| section | A | B |
|---|---:|---:|
| Update.ScriptRunBehaviourUpdate (all scripts in Update) | 368 | 288 |
| PreLateUpdate.DirectorUpdateAnimationBegin (animators) | 30.8 | 1.4 |
| PreLateUpdate (rest of the phase) | 33.4 | 3.5 |
| GameManager.Update: FogOfWar.OnUpdate | 30.4 | 5.2 |
| AIManager.TickAI: AI commanders Tick | 17.6 | 14.4 (C 10.5) |
| GameManager.Update: NetworkComponent.TickNetworkObjects | 6.2 | 0.1 |
| AlienStructure.OrderedUpdate / HumanStructure.OrderedUpdate | 6.6 / 4.1 | 1.9 / 0.8 |
| NetworkTransformComponent.OrderedFixedUpdate | 5.0 | 1.8 |
| FixedUpdate.PhysicsFixedUpdate (PhysX) | 72 | 65 |

Item evidence from the status windows:
* **Targeting stayed in bypass the whole round.** DetectedGrid: Sol "few queries per build" (D 258, 0.3 q/frame),
  Centauri "short list" / "few queries" (D 63), Alien "short list" (D 45). Grid share 0-3 %. FriendlyFire_Grid: every
  slot in bypass ("short cell list"; slot 1 "few queries per snapshot", L 227). So round #1 has no Targeting gain
  and no Targeting cost.
* **MultiCore (C vs B): no measurable gain**, if anything 2-5 % worse in the 250-300 bins (inside the noise of 6-9
  intervals). Counters: team-frames/s 914 (worker 343, sync small 571), worker 2.8 ms/s, main snapshot 3.4 ms/s,
  apply 0.1 ms/s, 25 chained vs 566 unchained sensor jobs/s, late 0. SensorTickPart1: 33.9 (C) vs 35.3 (B) vs 33.8 (A).
* UpdateTiers in a real round: Tier_IdleSoldiers skips only 21-33 % (early wake-ups by cause /s: path 206-312,
  aimTarget 88-309, destroyed 88), Tier_OrderProcessors 13-15 %, Tier_Structures 73 %.
* Services: CommanderAI_ThinkStats (B / C): thinks 988 / 1019, avg 12.2 / 12.0 ms, max 55.0 / 55.0 ms per think (the
  item is off in A, so no A figure; the A-arm PerfSection above is the comparison). CommanderAI_ClusterRebuild:
  1019 calls, ours 264 ms total (max 2.13 ms), 23.3 M vanilla pair checks avoided.
* Exceptions: 0 in every arm (16 / 17 / 17 windows counted). No item FAILED.

### Round #2 (`round2_arms3`, 13:14-13:46, b8, preset `lab-vanilla3way` = vanilla commanders on all three teams)

Arms: A = `"*"` off, B = defaults, C = "aggressive" (defaults + SensorPipeline_Rate + Tier_IdleAgents + LosTopK +
FriendlyFire_Cache + AvoidanceIdlePairSkip + IdleForceSleep). **Peak only 179 units**: the aliens were eliminated by
~20 min and Centauri won at 30.9 min (Cent 170 / Sol 4 units at the end). The instructed restart (`round2b_arms3`,
13:47) was stopped after 2 minutes on the coordinator's change of plan (big-battle test instead); it has no usable data.

| units | load A | load B | load C | B vs A | C vs A | fps A / B / C |
|---:|---:|---:|---:|---:|---:|---|
| 0 | 184 | 156 | 157 | -15 % | -14 % | 119.3 / 119.9 / 119.5 |
| 50 | 278 | 248 | 236 | -11 % | -15 % | 119.5 / 120.0 / 119.9 |
| 100 | 362 | 325 | 298 | -10 % | -18 % | 119.8 / 119.9 / 119.8 |
| 150 | 408 | 359 | 337 | -12 % | -17 % | 119.7 / 119.7 / 119.8 |

(34 / 36 / 33 clean intervals. Quadratic fits are meaningless over 0-179 units and are not reported.)

At ~150 units (n 21 / 21 / 20), main-thread busy is **A 391, B 344 (-12 %), C 311 (-21 %) ms/s**:

| section (ms/s) | A | B | C | moved by |
|---|---:|---:|---:|---|
| GameManager.SensorTickPart1 | 24.3 | 27.3 | **5.3** | SensorPipeline_Rate |
| VehicleTurret.OrderedUpdateAllTurrets | 21.7 | 18.2 | 13.2 | LosTopK, FriendlyFire_Cache |
| Soldier.OrderedUpdate | 20.8 | 17.9 | 15.5 | UpdateTiers |
| AIBaseAgent.OrderedUpdateAllAgents | 19.3 | 20.0 | **23.3** | Tier_IdleAgents costs (skips only 9 %) |
| FogOfWar.OnUpdate | 14.9 | 3.2 | 3.3 | Headless |
| animators (DirectorUpdateAnimationBegin) | 8.2 | 1.0 | 0.9 | Headless |
| HumanStructure.OrderedUpdate | 5.7 | 1.7 | 1.6 | UpdateTiers |
| NetworkComponent.TickNetworkObjects | 3.7 | 0.2 | 0.2 | Services |
| PhysX simulate | 87 | 84 | 86 | (few parked vehicles in this round) |

Counters: SensorPipeline_Rate 119 frames/s, 20 runs/s, part1 5.2 ms/s; LosTopK 316 q/s (92 LOS saved/s);
FriendlyFire_Cache 2001 q/s (1817 hits/s); Targeting grids in bypass throughout (DetectedGrid entries/build 66, grid
share 0); FriendlyFire_StaleGuard never met a stale window; Tier_IdleAgents run 3893/s + early 10 164/s, skipped 9 %;
ThinkStats (B / C) thinks 627 / 658, avg 1.3 ms, max 22.8 ms (vanilla commanders; round #1's Si_RTS_AI teams had
avg 12 ms, max 55 ms). **Exceptions: 0 in every arm** (21 / 21 / 21 windows), so no friendly-fire NRE in the
plain-vanilla blocks either. No item FAILED.

### Control round without the mod (`control_noperf`, 15:05-15:28)

Preset lab-3way exactly as round #1, mod set without `Si_ServerPerf.dll`. **The round ended at 21.6 min** (round #1:
25.3 min); Sol was eliminated, and at the end Centauri had 155 units / 19 structures and the aliens 51 / 54. Peak 210
units. Gameplay comparison (`control_noperf\COMPARE.md`, from Si_RTS_AI's round logs), units / structures:

| min | Sol with mod | Sol control | Cent with mod | Cent control | Alien with mod | Alien control |
|---:|---|---|---|---|---|---|
| 5 | 17 / 7 | 15 / 7 | 14 / 9 | 32 / 11 | 25 / 7 | 62 / 49 |
| 10 | 41 / 11 | 43 / 10 | 25 / 11 | 52 / 13 | 88 / 76 | 78 / 59 |
| 15 | 37 / 11 | 14 / 8 | 29 / 15 | 77 / 15 | 194 / 151 | 52 / 48 |
| 20 | 30 / 10 | 5 / 0 | 14 / 13 | 138 / 19 | 232 / 238 | 56 / 58 |

* Human structures plateau at 8-15 (Sol) and 11-19 (Centauri) with and without the mod.
* Build requests show the same refusal pattern in both rounds: TechTierInProduction 21-22, AreaObstructed 4 for
  Centauri, a few InsufficientResource.
* The winner differs (aliens vs Centauri): that is early-game variance of a 3-way.
* **The stall and the short rounds are today's Si_RTS_AI / 1.0.17 behaviour with this minimal mod set, not an effect
  of Si_ServerPerf.**

## 10. Items that failed, did nothing, or cost more than they saved

**Failed:** no item reported FAILED in any run (b1-b9). NOT APPLIED: `Physics.WakeProbe` target
`Soldier.OrderedFixedUpdatePartTwo` in b4/b5 ("IL Compile Error"; the state column still said ON), fixed in b6.

**Did nothing (in what was measured):**
* `Services.CommanderAI_SkipOwnedTeams`: "no owner probe resolved", by design.
* `Targeting.*_Grid` and `DetectedGrid`: in bypass in every lab scenario and in both AI rounds (at most 320 units).
  They neither gained nor cost there. They switch to GRID and pay only in the 450-800-unit battles (section 1).
* `Targeting.FriendlyFire_StaleGuard` (b8+): never met a stale window (0 calls in the rounds; no deaths in the
  battles), so it is untested in practice.
* `MultiCore.SensorTeamMaps_Job`: works (self-check identical, 0 different) but **does not pay**. Round #1 C vs B
  showed no gain; it spends ~3.4 ms/s of main-thread staging to move ~2.8 ms/s to a worker. Stays default off.
* `Physics.AvoidanceIdlePairSkip`: skips only 11-13 % of calls in b7/b9. The cause was the module's own idle test
  (it counted a parked vehicle's leftover steering angle as input), fixed in b10 but not measured. No measurable gain
  on its own.
* `UpdateTiers` has no tier for vehicles: 300 idle or moving vehicles gain nothing from the defaults (section 8b).

**Cost more than it saved:**
* `UpdateTiers.Tier_IdleAgents` (default off): no gain in the lab, and +3.3 ms/s in AI round #2
  (AIBaseAgent.OrderedUpdateAllAgents 23.3 vs 20.0 ms/s, skips 9 %). Leave it off.
* Targeting in b4 cost +1.22 ms/frame (+8.5 %) in a dense vehicle fight. **Fixed in b5** (never-worse gate); the
  same fight then saved ~0.7 ms.
* The default-off candidate set cost +0.035 ms in idle-soldiers-600 (+1.8 %, s.e. 0.003) while saving 1.38 ms in the
  vehicle fight (LosTopK and FriendlyFire_Cache).
* b9 physics diagnostics: the defaults arm was ~0.7 ms heavier in the b9 physics slot than in the b7 one. That points
  at the new always-on diagnostics, but it was not measured directly.

## 11. Not measured / limits

* Real AI rounds above ~320 units: both rounds ended early (aliens won at 25.5 min; Centauri won at 30.9 min with
  179 units). The high-unit range comes only from the synthetic battles, and those run with damage disabled.
* Behaviour of the approximate items (LosTopK, FriendlyFire_Cache, SensorPipeline_Rate): only their time was
  measured, not kill rates, friendly-fire damage or detection latency.
* Per-vehicle order-response latency and arrival times with the physics sleep items (Si_PerfLab cannot time them).
* Anything with connected clients (Services.Net_SkipUpdatesNoClients is only meaningful at 0 clients; network cost
  per player).
* A/A null flip (no time); the paired standard errors are the noise figure of the flip method.
* A configuration without Si_ServerOptimizer: not needed, since only `Headless.LightGrid_Skip` overlaps and is SKIPPED.
* Damage application and deaths in the big battles (Si_PerfLab 0.1.1 god mode), and therefore also
  `Targeting.FriendlyFire_StaleGuard` under load.
* A mod-free 1.0.17 run with exception capture (Unity's player log is not kept on the lab server; the only no-mod
  evidence is PerfProbe's `leakResets` = 0 in the two baseline passes).
* Leave-one-out flips for the Headless, Services and Physics (defaults) modules on their own (dropped for time). Their
  share is visible only as section deltas in the rounds and battles (fog, animators, network tick, structure updates).
* Individual items inside a module: Headless and Services items were never flipped one by one.

## 12. Found along the way (not Si_ServerPerf issues)

1. **Si_RTS_AI bug: OnAttackOrder prefixes can empty a group mid-loop.** Seen once in the AI verify segment (05:55:50):
   `ArgumentOutOfRangeException` at `List.get_Item` <- vanilla `Silica.AI.AITask_Attack.IssueOrders` <- `CommitAttack`
   <- `UpdateStaging`. Coordinator's code analysis: vanilla `IssueOrders` reads `Groups.Count` once before its loop.
   Si_RTS_AI 0.94.14's prefixes on `AIGroup.OnAttackOrder` (`Faction/AlienShrimpAntiAttack.cs`,
   `Planning/ScoutPlanner.cs`) call `RemoveUnit` for Shrimps / scouts inside that call. When that empties a group, the
   group returns to the pool and leaves the task's list, and the next `Groups[k]` is out of range. It did not recur in
   AI rounds #1 and #2 (0 exceptions in every arm), so no per-arm rate could be measured.
2. **Vanilla friendly-fire NRE on a destroyed target.** One event (4 exceptions within 20 s) in `flip_candidates`
   (07:18:59), in the arm with plain defaults: `NullReferenceException` in `Target.CheckRiskOfFriendlyFire` <-
   `TurretWeapon.OrderedUpdate`. Cause per the Targeting author: `Target.OnDestroy` nulls `Owner`, but the target stays
   in `Target.Targets` and the grid cells until the next `TargetGrid.Update`. Plain vanilla throws the same way; the
   cost is one turret update cut short. It was not reproduced in arm A ("*" off, plain vanilla) of either AI round, and
   PerfProbe's `leakResets` stayed 0 in both no-mod baseline passes. b8 added `Targeting.FriendlyFire_StaleGuard`,
   which never met a stale window in the measurements.
3. **Vanilla parked AI vehicles shuffle by decimetres.** `[Physics] parkeddrift` in plain vanilla (arm A): parked
   AATrucks move ~1 m horizontally and ~0.25 m vertically per 10 s on average (max 2 m / 0.9 m); HeavyStriker,
   BarrageTruck and LightStriker up to ~1.5 m. Physics author's code analysis: for a unit without a path, the avoidance
   code compares against a destination of `Vector3.zero` and can set a detour point 50 m ahead for a penetrating
   neighbour. The AI input update then drives toward it (a 0/0 NaN in the braking test makes a stopped vehicle drive),
   the vehicle lurches and brakes, and touches the next capsule. The lab's tight same-heading spawn grid makes it worse.
   b10 adds `Physics.AvoidanceNoIdleDetour` (default off) for this.
4. **Si_PerfLab 0.1.1 god mode.** Health refill (0.1.0's "invulnerable") did not keep units alive in multi-cluster
   battles: one-shot and collision kills removed ~60 % within two minutes. 0.1.1 sets the game's static
   `DamageManager.DamageCheatMultiplier = 0` while a scenario with invulnerable groups runs (restored on cleanup;
   logged as "[LAB] all damage DISABLED / enabled"), plus a zero-damage prefix on `DamageManager.ApplyDamage` for lab
   objects. Consequence for the battle numbers: units target, aim, fire and spawn projectiles normally, but **damage
   application, deaths, wreck spawning, destroyed-object disposal and stale-target windows are excluded**, and the
   count stays fixed. Real battles have that extra cost on top, in every arm. The earlier "invulnerable" flip scenarios
   (b1-b7) ran with 0.1.0's refill only.
5. **Lab AI rounds on 1.0.17 do not get past 180-320 units, with or without the mod.** Round #1 (Si_RTS_AI on all
   factions) peaked at 317 units (aliens won at 25.5 min), round #2 (vanilla commanders) at 179 (Centauri won at 30.9
   min), and the control round without Si_ServerPerf at 210 (ended at 21.6 min). In all three the human factions
   plateau at 8-19 structures (TechTierInProduction refusals, a few AreaObstructed / InsufficientResource). The
   1.0.15 rounds on the live server (full mod set) reached 600-760 units. For high unit counts on this lab set, use
   generated scenarios (section 1), not AI rounds.
## 13. Per-item verdict

Default = state at startup with a fresh config (b9/b10). Exact = same result as vanilla by construction (and
self-checked where marked); approximate = deliberately different (timing or staleness).

| item | default | measured effect | where (build) | exact / approx. | unverified |
|---|---|---|---|---|---|
| Headless presentation skips (Fog_SkipPresentation, Animators_Disable, StructureLights, MeshCombiner, SceneLOD, VehicleEngineVents / Creaking, ConstructionWarningUI, CreatureAttackFX, Particles_Disable) | on (CreatureAttackInflate_Skip off; LightGrid_Skip SKIPPED next to SO 0.7.1) | fog 30 -> 5 ms/s and animators 31 -> 1.4 ms/s at ~280 units (round #1); per frame at 617 units fog 0.68 -> 0.08 and animators 0.94 -> 0.01 ms | rounds b5 / b8, battles b9 | exact for game state (presentation only) | module never flipped alone; per-item shares not separated; hit registration on unanimated soldiers not checked with a client |
| Headless.Fog_DetectedTargetLookup | on | inside the fog reduction | as above | exact | not isolated |
| Headless.SteamDetails_1Hz / PlayerBans_1Hz | on | part of the Update reduction | all | approx. (1 Hz) | not isolated |
| Headless.Diag_LogExceptions | on | diagnostics, ~0 cost | all | n/a | - |
| UpdateTiers.Tier_IdleSoldiers | on | -0.62 ms (24 %) at 600 idle soldiers (b2 fix); skips 21-33 % in AI round #1, 3 % in the battles (aimTarget wake-ups) | flip_no_UpdateTiers b2, rounds, battles | approx. (idle updates every 4th frame; wake-ups by rule) | reaction time (time to first shot) not measured |
| UpdateTiers.Tier_Structures / HealDecay / OrderProcessors / StaticNetTransforms | on | skip 73-80 % in the lab; OrderProcessors 13-15 % and Structures 73 % in rounds; HumanStructure 5.7 -> 1.7 ms/s | lab b2, rounds | approx. (rate tiers) | production enqueue-to-spawn time not measured |
| UpdateTiers.Register_O1 | on | 85 251 registrations verified, 0 mismatches, out-of-step 0 | correctness b4 | exact (verified) | - |
| UpdateTiers.Tier_IdleAgents | off | no gain in the lab; **+3.3 ms/s** in round #2 (skips 9 %) | flip_candidates b7, round #2 b8 | approx. | **keep off** |
| UpdateTiers.FixedLate_StepDt | off | not measured | - | - | all |
| Targeting.DetectedGrid, CollectEnemies / UrgentClose / NearestTarget / AlarmProximity / FriendlyFire_Grid, Rank_Sort | on | bypass (no gain, no cost) up to ~330 units; GRID and paying at 477-800 units in the battles (part of the -27 to -37 %); b4 cost +8.5 % in a dense fight, fixed in b5 | battles b9, smoke b5, flip_all b4 | exact (self-check 0 mismatches on b4 and b7 code) | contribution not isolated from the other modules in the battles |
| Targeting.FriendlyFire_StaleGuard | on (b8+) | never triggered | rounds b8, battles b9 | exact by construction | untested under load (needs deaths) |
| Targeting.LosTopK | off | with FriendlyFire_Cache: -1.38 ms (-10.7 %) in a dense vehicle fight; 3107 LOS queries saved/s at 800 units | flip_candidates b7, battles b9 (arm C) | **approx.** | combat outcome not measured |
| Targeting.FriendlyFire_Cache | off | as above; 6200 of 10 703 calls answered from the cache at 800 units | as above | **approx.** (0.15 s cache) | friendly-fire damage not measured |
| Services.Detected_IndexLookups | on | part of the fog / targeting reductions | correctness b4 | exact (407 281 verified) | not isolated |
| Services.TerrainMaterial_Cache | on | not isolated | correctness b4 | exact (255 109 verified) | not isolated |
| Services.DestroyedDisposal_EveryN | on | skips 3 of 4 walks | smoke b1 | approx. (disposal up to 3 frames later) | not isolated |
| Services.Net_SkipUpdatesNoClients | on | TickNetworkObjects 6.2 -> 0.1 ms/s (round #1) | rounds | exact at 0 clients | nothing with clients |
| Services.Astar_CutPollInterval | on | cut scans ~fps -> 2/s; part of structures-built-30's -20 % | lab b1 / b4 | approx. (0.5 s cut poll) | not isolated |
| Services.CommanderAI_ClusterRebuild | on (b4+) | ours 28 ms vs vanilla 84 ms over 660 rebuilds; "AI commanders Tick" 17.6 -> 14.4 ms/s (round #1) | verify_ai b5, rounds | exact (660 verified) | - |
| Services.CommanderAI_ThinkStats | on | diagnostics (round #1: think avg 12 ms, max 55; round #2: 1.3 / 22.8) | rounds | n/a | - |
| Services.CommanderAI_SkipOwnedTeams | on | does nothing (no owner probe) | all | - | by design |
| Services.Astar_ProbeFlushDebounce | off | not measured | - | - | all |
| Physics.Census / CatchUpCap | on | census diagnostics; maximumDeltaTime 0.1 -> 0.08 s, at most 2 steps per frame | all | approx. (catch-up bound) | not isolated |
| Physics.IdleForceSleep | off | b7: 2 -> 59 % asleep and -0.65 to -0.68 ms (-17 %) with 300 parked vehicles; b9: 45 % / 24 % asleep; force-slept vehicles drift 0 cm | physics_cands b7, physics_b9 | approx. | **order-response latency not measured**; b10 idle-test fix not measured |
| Physics.AvoidanceIdlePairSkip | off | 11-13 % skip, no clear gain (module idle-test bug, fixed in b10) | b7, b9 | approx. | b10 not measured |
| Physics.AvoidanceSkipOntoResting (b9) / AvoidanceNoIdleDetour (b10) | off | b9: 12 % skip, +6 % asleep on top of IdleForceSleep; b10 not measured | physics_b9 | approx. | b10 slot not completed |
| Physics.RestWriteGuards / HitZonePoseGuard | off | active (guardSkip up to 311 k per 10 s), no measurable time effect | flip_candidates b7 | approx. | hit registration not checked |
| Physics diagnostics (WakeProbe, WheeledSleepDiag, IdleSleepDiag) | off from b10 (WheeledSleepDiag on in b7-b9) | the b9 defaults arm was ~0.7 ms heavier than in b7, likely these | physics_b9 | n/a | cost not measured directly |
| MultiCore.SensorTeamMaps_Job | off | works (4470 team-frames self-checked, 0 different) but **no gain** (3.4 ms/s main-thread staging for 2.8 ms/s of worker time) | smoke b5, round #1 b5 | exact | **keep off** |
| MultiCore.SensorPipeline_Rate | off | SensorTickPart1 16.1 -> 4.2 ms/s (lab fight), 27 -> 5 ms/s (round #2); part of arm C's extra 2-10 % in the battles | targeting_check b7, round #2 b8, battles b9 | **approx.** (sensors every 50 ms) | detection latency and combat outcome not measured |

## 14. Remaining when the machine is free (scenario-based only, in priority order)

1. **b10 physics slot.** Arms A defaults / C IdleForceSleep / F IdleForceSleep + AvoidanceSkipOntoResting +
   AvoidanceNoIdleDetour, 3 blocks each, on idle-vehicles-300, the parked mixed set and the move-vehicles guard.
   Diagnostics off in the timing blocks, plus one diagnostic F block with IdleSleepDiag. Everything is ready:
   `scenarios_physics_b10.json`, `cfg_serverperf_physics_b10.json`, `runtime_p10_{A,C,F,FD}.json`, run with
   `run_lab.ps1 -Mode lab -Label physics_b10b -Scenarios scenarios_physics_b10.json -ServerPerfDll <release\b10>
   -ServerPerfConfig cfg_serverperf_physics_b10.json -RuntimeJson runtime_p10_A.json -FlipStates
   <A,C,A,F x3 + FD files> -FlipLabels A,C,A,F,A,C,A,F,A,C,A,F,FD -FlipEveryS 60`. Check that arm A is back at
   ~3.8 ms (the no-mod level) with the diagnostics off.
2. **Repeat of the big-battle test** (`scenarios_battle.json`, same three arms, b9 or newer) for a second sample.
3. **Big battle with damage on and respawn-to-hold-count**, so damage application, deaths, disposal and
   `FriendlyFire_StaleGuard` are exercised. This needs a Si_PerfLab addition: a respawn generator per group that
   replaces dead units at their cluster (the existing churn generator only replaces its own oldest objects).
4. **Parked vehicles in a base with realistic spacing** (mixed prefabs, random headings, 15-30 m spacing instead of
   the tight same-heading grid), defaults vs the physics items. This shows whether the idle-avoidance shuffle and the
   sleep gain survive a realistic layout.
