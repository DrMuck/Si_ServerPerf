# Si_ServerPerf night 1 (2026-11-16 20:37 to 2026-11-17 morning): results and decisions

Lab only (`C:\tmp\silica_ds_lab`, Silica 1.0.17 headless, 0 clients, 120 fps cap). Builds `release\b10` to `release\b20`.
Version of this report: **09:15** (final; every run of the night, the last one finished 08:59; per-run details in
`night1\RESULTS_LOG.md`). The rig's earlier draft is kept as `NIGHT1_rig_draft.md`.

Words used throughout:
* **lab units** = living units the test mod spawned. **World units** additionally count ~41-45 base/starter units and,
  in fights with damage, the **dead bodies waiting for disposal** (vanilla keeps every dead unit for 15 s).
* **frame work** = main-thread CPU time per frame without the fps-cap wait, in ms. When the main thread is full
  (from ~330 lab units in the melee), fps = 1000 / frame work. A cut of 33 % in frame work then means about +50 % fps,
  a cut of 50 % about +100 %, a cut of 65 % about +185 %.
* **Melee** = all three teams in one 45 m fight at the map centre, damage on, dead units replaced after 2 s. The worst
  case. **450 / 550 plateau** = the melee held at a target of 450 / 550 lab units (it actually holds ~425 / ~523).
* **Spread** = four separate fronts, damage on, dead units replaced (the `ramp_sustain` geometry).

---

## 1. Summary

**Headline: dense three-way melee**, two samples (`b20_best` / `b20_best2`, build b20; all arms switched inside one
running server, 30 s blocks, 3-4 per arm; fps of the two runs, then the two-sample mean of the paired frame-work change
vs off):

| melee plateau | mod off | shipped defaults | + new invisible items | + prefiltered replay ("new2") | + worker-thread scans ("all") |
|---|---:|---:|---:|---:|---:|
| ~425 lab units | 35.3 / 32.6 fps | 54.7 / 50.7 fps (-36.8 %) | 63.8 / 57.1 fps (-47.9 %) | 63.7 / 58.7 fps (-46 / -37 %) | 68.7 / 68.8 fps (-52.6 %) |
| ~522 lab units | 11.9 / 11.3 fps | 15.6 / 15.5 fps (-27.1 %) | 21.2 / 24.6 fps (-51.4 %) | 22.9 / 24.2 fps (-51 / -54 %) | **31.3 / 32.1 fps (-65.4 %)** |

* At ~522 the two samples agree within a few points (all vs off -65.5 / -65.2 %, all vs new -30.9 / -25.1 %). At ~425
  single paired values differ between the runs by up to ~10 points, more than their in-run s.e.: all vs off -49 to
  -56 %, all vs new -8 to -19 % (mean -13.6 %), new2 vs off -37 to -46 %.

* **New invisible items** = the three exact target-scan items (`CollectEnemies_Snapshot`, `_SnapshotScore`, `_FastAdd`)
  + `DeadAgentHalt` (dead units' AI stops) with the aim kept. **new2** adds `CollectPreFiltered_Snapshot` (exact; no
  measurable gain on its own). **all** adds `ParallelScan` round 2 + `ParallelScan_Prefiltered` (target scans on
  worker threads; exact). No arm here removes wrecks early.
* **Faster wreck removal (visible)**, fps-proportional form on top of "all" (`b20_dispose_paired2`, melee ~592 lab
  units, strictly alternating 90 s blocks): **21.1 -> 30.0 fps, -31.6 % frame work (s.e. 5.0, n 4)**; dispose time
  6.2 s (5.0-7.1), dead share of world units 20.0 -> 11.1 %. Where the other items had already restored fps (35-47 fps
  at ~514-518, `b20_dispose_paired`) the control stayed at 9-13.5 s and gained -10.5 % in the melee (s.e. 1.3, 2 blocks,
  not interleaved) and nothing in the spread layout (-2.2 %, s.e. 7.9). Earlier always-5 s sample (`b15_best_plat`,
  ~521): new 27.3 -> 40.1 fps.
* Separate starts differ: "new" at the ~550 plateau ran 21.2 / 24.6 fps (`b20_best` / `b20_best2`), 25.2 (`b19_pscan`,
  new + prefiltered replay) and 27.3 (`b15_best_plat`). Compare arms only within one run.

**Spread four-front battle with damage** (`b19_spread`, b20; the layout closest to real rounds):

| plateau | off | defaults | new | all | new vs off | new vs defaults | all vs new |
|---|---:|---:|---:|---:|---:|---:|---:|
| ~422 lab units | 50.3 fps | 67.2 | 77.0 | 76.4 | -40.0 % (2.0) | -17.1 % (1.7) | +1.9 % (2.5) |
| ~518 | 19.3 | 33.2 | 38.6 | 43.3 | -52.3 % (1.6) | -15.5 % (9.5) | -5.1 % (7.1) |
| ~614 | 10.6 | 13.9 | 17.8 | 19.0 | -41.1 % (1.6) | -20.7 % (2.3) | -8.1 % (2.5) |

The new items hold in the spread layout. The worker-thread scans add little there (only the ~614 value is outside the
noise) against -30.9 % in the melee: **ParallelScan is a dense-battle item.**

**Unit count where each arm first drops below 30 / 20 / 10 fps** (melee trickle, +1 unit/s, `b15_best_qc`, lab units):

| arm | < 30 fps | < 20 fps | < 10 fps |
|---|---:|---:|---:|
| mod off | 447 | 461 | 608 |
| shipped defaults | 478 | 535 | 683 |
| + new invisible items | 505 | 550 | not reached (12 fps at 675-699; the climb ended there) |

Without damage (`qcgod1`, same geometry, no deaths) mod off holds 30 fps up to 607 lab units: deaths, respawns and
corpses cost ~165 units. The 1.0.15 AI rounds (600 units = 31 fps) behave like a battle with few deaths at a time.

**Parked vehicles:** cause found and fixed. Parked AI vehicles stay awake because the game rewrites the steering wheels
every physics step, which re-wakes the vehicle. `ParkedWheelHold` + `IdleForceSleep` + `AvoidanceSkipOntoResting` +
`AvoidanceNoIdleDetour`: 300 parked vehicles -31.3 % frame work (s.e. 0.4), PhysX 133 -> 30 ms/s, 98-100 % asleep, at
most 4.4 cm creep, response to move orders unchanged. On spaced, sloped lots (`b20_parked_spaced`, mean 10-13°, up to
18-29°) asleep 44 -> 99 % and 14 -> 95 %, PhysX -75 % / -70 %, frame work -27 % (s.e. 20) / -22 % (s.e. 5), drift at
most 6 mm. Never seen on a client.

**Multi-core:** yes, for the AI's target scans. Round 2 of `ParallelScan` serves 94-96 % of full scans from worker
threads, exact in 13 649 + 9 803 self-checks (round 1: 19 571), and is what takes the melee from 21-23 to 31 fps. The
workers-off control (round 1) gained nothing, so the gain is the extra cores. The largest remaining block, the PhysX
step (~34 % of the main thread), is not moved by any item; lowering vehicle solver iterations did not change it.

**Proven / not proven:**
* Proven in the lab: all numbers above are paired within one server; 0 exceptions in every arm of every run; nothing
  FAILED; self-checks with 0 mismatches: target-scan items 83 479 each (b18_check), prefiltered replay 19 274, worker
  scans 13 649 + 9 803 (+19 571 round 1), friendly-fire flat check 2 000 812.
* Not proven: nothing ran with a client. The grid-pruned scan path (used in spread late rounds) is still the thinnest
  (estimated ≥ ~500 checks in `b15_snap_check4`, see 6.1). ParallelScan's emulated type-value path was never triggered
  in game. Shots, deaths and hits rise with fps in the faster arms (+12 % deaths, +9 % hits for "all" vs off at ~523);
  it looks like an fps effect, not changed behaviour, but a client look is advisable.

---

## 2. Decisions for you

The recommendations here are the managing agent's, not measured facts.

| # | decision | evidence | recommendation (managing agent) |
|---:|---|---|---|
| 1 | Make the three target-scan items default (`Targeting.CollectEnemies_Snapshot`, `_SnapshotScore`, `_FastAdd`; exact, invisible) | 83 479 self-checks each, 0 mismatches (b18_check) plus earlier runs; -23.0 % at ~521 (b13_snap); part of "new" (-47.5 % vs off melee, -40 to -52 % vs off spread). Thinnest: grid-pruned path (≥ ~500 checks in check4 plus earlier ones). | **Default ON now**, as a set. |
| 2 | Make `Physics.DeadAgentHalt` default with `DeadAgentHaltClearAim = false` (invisible) | three independent samples -5 to -19 % (b14_dead -4.9 / -15.7; b15 guards -10.3 / -11.8; b15_eveh -19.2 / -12.7); guard with aim kept: all metrics |t| < 1 (11 blocks); spread layout: "new" holds, behaviour within ±4 % at ~422. A halt-only guard on a second scenario was not run. | **Default ON now**, aim kept. |
| 3 | `DeadAgentHaltClearAim = true` | shots -7.0 % (t -1.6), hits -4.7 % (t -1.7) | **Keep false.** |
| 4 | `Targeting.CollectPreFiltered_Snapshot` (exact replay for the re-evaluation scans) | 19 274 checks, 0 mismatches; cuts its method by about a third but saves only 4-5 ms/s, inside frame noise | **Optional, harmless**; needed as the fallback path when `ParallelScan_Prefiltered` is on. |
| 5 | `MultiCore.ParallelScan` + `ParallelScan_Prefiltered` (worker-thread scans, exact) | melee: -30.4 / -31.5 % vs new2 (b19_pscan), -30.9 / -25.1 % vs new at ~522 (b20_best / b20_best2), -8 to -19 % at ~425; spread: +1.9 / -5.1 / -8.1 % vs new; 13 649 + 9 803 round-2 checks + 19 571 round-1, all identical; emulated type-value path never triggered in game (offline tests only) | **Opt-in for big battles now**; candidate default after a longer soak and a client session. |
| 6 | Parked-vehicle set: `IdleForceSleep` + `AvoidanceSkipOntoResting` + `AvoidanceNoIdleDetour` + `ParkedWheelHold` | -31.3 % (300 idle, s.e. 0.4), -37.9 % (300 mixed, 1 pair); 99.0-99.7 % asleep again in b18_phys_guard_idle; creep ≤ 4.4 cm; order response unchanged. Spaced, sloped lots (`b20_parked_spaced`): 95-99 % asleep, -22 % / -27 %, drift ≤ 6 mm. | **Client test first**, then default. |
| 7 | Faster wreck removal `Physics.DeadDisposeEarly` (VISIBLE) | always 5 s: -24 % / -41.5 % (b14), 40.1 vs 27.3 fps (b15); on/off valve cycles ~2.2 times per minute; fps-proportional settles (7.9-10.3 s, b17). Paired on top of "all": overloaded melee ~592 lab units **-31.6 % (s.e. 5.0), 21.1 -> 30.0 fps**, dispose time 6.2 s; at 35-47 fps it stays at 9-13.5 s: melee -10.5 % (2 blocks, not interleaved), spread -2.2 % (noise). | **Your choice** (the one visible item). A large lever only when the server is overloaded, worth little once the other items have restored fps; the proportional form does exactly that automatically. If on, that form; do not ship the valve. |
| 8 | 20 Hz physics step `Physics.FixedStep_Override` (behaviour change) | -24 % / -28 % frame work at 400-499 / 500-599 lab units (fits -15 % at 500, -10 % at 800); deaths, kills, shots, hits, with-target within ±3 % (|t| < 1, 19-20 blocks) | **Information only**; would need a client (vehicle handling, fast aircraft). |
| 9 | Close as "built, verified, no gain", keep off: `WheelJointWriteGuard`, `VehicleSolverIterations`, `FriendlyFire_Flat`, `DeadSettle`, `DeadNoUnitContact`, `UpdateTiers.Tier_IdleAgents` | sections 6.2, 6.6, 7 | **Keep off.** |
| 10 | Approximate set `Targeting.LosTopK`, `Targeting.FriendlyFire_Cache`, `MultiCore.SensorPipeline_Rate` | LosTopK nets -6 to -7 % despite +10 % extra rescans (not ruled out); the set adds 1-5 points with damage, 8-15 in god mode; behaviour unmeasured | **Keep off**; optional "aggressive" set. |
| 11 | S4 placement-search budget | still not built, on purpose | **Your call.** |
| 12 | Si_RTS_AI issues from yesterday: `AIGroup.OnAttackOrder` prefixes (`AlienShrimpAntiAttack.cs`, `ScoutPlanner.cs`) can empty a group mid-loop -> `ArgumentOutOfRangeException` in vanilla `AITask_Attack.IssueOrders`; lab AI rounds on 1.0.17 stall at 180-320 units | no new data tonight (generated scenarios only) | Open. The first is a fix in Si_RTS_AI; the second only matters for real rounds above 320 units in the lab. |

---

## 3. How to read the numbers

* **"Mod off"** is not vanilla. Si_ServerPerf is loaded with every item switched off at runtime (`"*": false`); only
  `Headless.Diag_LogExceptions` (counts exceptions) and the startup-only `Headless.Particles_Disable` stay on (in
  `b19_spread` also the read-only `Physics.Census`). The other lab mods run in every arm: Si_AdminMod, Si_PerfLab
  (scenario driver), Si_PerfProbe (profiler), Si_RTS_AI, Si_ServerFpsCap and **Si_ServerOptimizer 0.7.1**.
* **Paired arms.** Every comparison switches arms inside one running server through `runtime.json` (every 30 or 60 s),
  at the same unit count, dropping the first 5-30 s after each switch. "Paired" = each block against its neighbouring
  blocks of the reference arm; s.e. over those pairs, in % of the reference arm's work. Items off at startup are not
  patched at all, so new items were on at startup and switched off for the reference arms.
* **Only in-run comparisons are reliable.** Separate starts of the same scenario differ much more:
  * new at the ~550 plateau: 21.2 fps (`b20_best`), 25.2 (`b19_pscan`, new + prefiltered replay), 26.9 (`b17_pscan`),
    27.3 (`b15_best_plat`), 29.5 (`b17_pscan_inline`), and 31.7 falling to 14.4 within three minutes in
    `b17_dispose_always` with world units steady at 710-740;
  * off at the ~550 plateau: 11.9 (`b20_best`) vs 15.6 (`b15_best_plat`); defaults at ~424: 58.2 vs 46.6 fps in the
    two halt guards; arm P at ~424: 55.5 vs 73.8 fps in the two round-1 checks;
  * the defaults arm's < 30 fps point in the trickle: 469 / 492 / 478 / 516 lab units (`qc1` / `qc2` / `b15_best_qc` /
    `qc_fixed20`).
  Typical spread about ±15 %, worst pairs 25-30 % apart. A melee is not repeatable in detail (which units die where).
* **Frame work vs fps.** Below saturation fps sits at the cap and savings show only as headroom. Above it, fps is what
  players feel. Percent changes are frame work unless they say fps.
* **Scenarios:**
  * *Quick check* (trickle): the melee from ~100 lab units, +~1 unit/s until the 20 s median fps falls under 10; arms
    rotate every 30 s. Read crossings and per-arm fits, not single bins (arms sit 10-30 units apart inside a bin).
  * *Melee plateaus*: the same melee held at 450 or 550 target lab units for 4-6 min.
  * *Ramps / spread plateaus*: four fronts, 20 % of units in the base, 15 % in transit, 65 % at the fronts; `god` = no
    damage, `sustain` = damage on with respawn.
  * *Parked sets*: 300 vehicles standing still in a tight grid (idle LightStrikers, or four prefab grids "mixed"),
    spaced variants on slopes, a moving-vehicles guard, and an order probe timing move-order response.

---

## 4. Comparison with real rounds

fps of the **off** arm by unit count. "Live units" = lab units + ~45 non-lab units; dead bodies not counted.

| live units | (a) AI rounds 1.0.15, DEV_REPORT §1 | (b) wave 1 god battles (b9), 4 clusters | (c) melee trickle `qc1`, damage | (d) `ramp_god`, 4 fronts, no damage | (e) `ramp_sustain`, 4 fronts, damage |
|---:|---:|---:|---:|---:|---:|
| ~100 | 115 (100) | | | | |
| ~240-270 | 109 (250) | | 118 (lab 200-249) | 116 (238) | 113 (231) |
| ~330 | 97 (300) | 108 (329) | 91-106 (lab 250-299) | 105 (337) | 98 (325) |
| ~430 | 73 (400) | | 71 / 59 (lab 300-399) | 88 (434) | 70 (419) |
| ~480-530 | 65 (500) | 78 (477) | 41 (~470) / 18 (~520) | 65 (530) | 30 (512) |
| ~600-630 | 31 (600) | 39 (617) | 11 (~570) / 9 (~620) | 38 (627) | 13 (608) |
| ~700-730 | 21 (700) | | below 10 from ~615 | 21 (726) | 9.1 (710) |
| ~800-830 | | 15 (800) | | 13 (824) | 7.2 (812) |

**Same melee with and without deaths** (lab units where off / defaults first drop below a level):

| | off < 30 | off < 10 | defaults < 30 |
|---|---:|---:|---:|
| melee with damage (`qc1` / `qc2` / `b15_best_qc`) | 434 / 431 / 447 | 570 / 558 / 608 | 469 / 492 / 478 |
| melee in god mode, no deaths (`qcgod1`) | 607 | 812 | 668 |

How engaged the units are (lab units only):

| scenario | share with a target | shots + creature attacks per unit-s | deaths/s |
|---|---:|---:|---:|
| (a) AI rounds | not measured (most units idle in bases or moving; fights are local) | - | - |
| (b) wave 1 god battles | not measured; 530-1450 projectiles in flight | - | 0 |
| (c) melee trickle, whole climb | 0.70 | 1.10 | 8.9 |
| melee trickle in god mode (`qcgod1`) | 0.97 | 1.59 | 0.73 (stragglers) |
| (d) `ramp_god` @200 ... @1000 | 0.81-0.87 | 2.3 -> 1.4 | 0 |
| (e) `ramp_sustain` @200 ... @800 | 0.56-0.59 | 1.4 -> 0.55 | 6.5 -> 16.7 |
| melee plateaus 450/550 | 0.47-0.90 (varies by run) | 0.45-1.0 | 9.5-15 |
| spread plateaus `b19_spread` | 0.59-0.62 | 0.7-1.0 shots | 13-18 |

* **Deaths, not density, are the main cost.** Without deaths the same melee holds 30 fps up to 607 lab units; with
  deaths, respawns and the 150-200 corpses they keep in the world, 431-447 (~165 units less; the 10 fps point moves
  ~200-250). With damage, the spread ramp is only slightly lighter than the melee at the 30 fps point (~467 vs 431-447).
* **The 1.0.15 AI rounds sit on the no-death curves**: the god-mode spread ramp tracks them closely (530: 65 vs 65 fps;
  627: 38 vs 31; 726: 21 vs 21), yesterday's god battles too (617: 39), and the god-mode melee reaches 30 fps at about
  the same count (607 lab) as the AI rounds (600 = 31 fps). A real round behaves like a battle with few deaths at a time.
* **So the melee with damage is a worst case**, and the spread plateaus with damage (`b19_spread`) are a hard but
  realistic stand-in for a big late-round fight. In `b19_spread` 24-27 % of world units are dead bodies, 90-96 % of
  them awake, in every arm.

---

## 5. Why the fps knee exists (short)

In the melee the off arm's frame work jumps from 25 to 58 ms between ~425 and ~475 lab units, and fps falls 42 -> 17.
Nothing in particular breaks there (`ANALYSIS_BATTLE.md` section 2): fixed steps stay at 25 per second (never more
than 2 per frame), deaths and disposal are flat across the knee.

It is a saturation curve. Most of the server's work is paid **per second of game time**, not per frame: 25 physics
steps per second, each agent's behaviour tree twice per second, creature AI every 0.2 s, projectiles. Call that part T'
(ms per second). What is left of each second goes to frames, each costing F ms: fps = (1000 - T') / F. At ~405 lab
units T' is 79 % of the second, at ~473 88 %, at ~539 93 %. T' grew only 12 % from 405 to 473, but the budget left
for frames halved, and fps halved with it. The model reproduces the measured fps (40 / 21 / 10-11 / 7 predicted vs
42 / 17 / 10.3 / 8.3).

What that means for further savings:
* Near saturation every saving is leveraged: at ~13 fps each 10 ms/s removed is worth ~1.8 fps; at ~32 fps (new items +
  worker scans, ~521 units) +1.75 to 2.1 fps (+6 %), whether the saving is per second or per frame.
* From ~32 fps, 40 fps needs ~40-45 ms/s less main-thread work and 60 fps ~130-160 ms/s less (model estimate).
* The shipped defaults mostly cut per-frame work; the new items cut per-second work (dead agents, scans), which is why
  they help most where the knee is.

---

## 6. Results by topic

### 6.1 Target-scan items

What they do: the AI's full target scan walks the whole list of detected enemies (~400 entries) and scores each one.
In the melee the existing grid items cannot prune that list (everything is in range). The new items make each entry
cheaper without changing the answer: stage 1 caches per-object constants, stage 2 replays the scoring on those cached
references with vanilla's own cache logic, FastAdd keeps the lowest of the 24 candidate slots. `CollectPreFiltered_Snapshot`
(b18) applies the same machinery to the "prefiltered" re-evaluation scans that fighting agents run every 1-1.5 s.
A sampled self-check runs vanilla on the same call and compares count, order and score bits.

Timing of the three items (`b13_snap`, b13, paired vs shipped defaults, 2-3 blocks per arm):

| plateau | arm | fps | frame work vs defaults (s.e.) | full scan `CollectDetectedEnemies` ms/s, µs per call | "no target found" share |
|---|---|---:|---:|---|---:|
| ~424 | defaults | 60.2 | | 100, 137 µs | 70 % |
| | + stage 1 | 62.5 | -8.2 % (3.5) | 91, 123 µs | 72 % |
| | + stage 2 | 64.1 | -10.6 % (1.5) | 68, 98 µs | 73 % |
| | + FastAdd (all three) | 63.9 | -6.9 % (3.0) | 65, 90 µs | 74 % |
| | defaults + LosTopK | 63.7 | -6.2 % (1.6) | 111, 128 µs | 68 % |
| ~521 | defaults | 22.1 | | 170, 184 µs | 75 % |
| | + stage 1 | 20.6 | +2.7 % (6.0) | 144, 161 µs | 75 % |
| | + stage 2 | 25.7 | -12.8 % (0.1) | 115, 123 µs | 70 % |
| | **+ FastAdd (all three)** | **29.6** | **-23.0 % (2.8)** | 107, 116 µs | 80 % |
| | defaults + LosTopK | 24.2 | -6.8 % (3.1) | 177, 175 µs | 80 % |

* Stage 1 alone does nothing; stage 2 and FastAdd do the work. In `b20_best` "new" (these three + DeadAgentHalt) is
  about -27 % vs defaults at ~523 (paired: defaults +37.7 % vs new, s.e. 7.8).
* `CollectPreFiltered_Snapshot` (`b18_items`, b20): its method falls by about a third (11.0 -> 6.7 ms/s at ~429, 16.0 ->
  10.9 at ~525); 4-5 ms/s is below what 30 s blocks resolve (frame work +2.3 % / -0.4 %, noise). The deep probe had
  measured 32.5 ms/s for that method in arm P, against the analyst's ~50 ms/s estimate.
* **LosTopK** (approximate: line-of-sight only for the top 6 candidates) causes extra full rescans: a scan whose top 6
  are all blocked returns no target, and vanilla never throttles a "no target" result. "No target" 75 -> 80 %, full
  scans +10 %. It still nets -6 to -7 %.

Correctness (self-check against vanilla):

| run (build) | scenario | sample rate | checks | mismatches | note |
|---|---|---|---:|---:|---|
| `b13_snap_check2` (b13) | melee ~424 | 0.05 | at least ~800 | 0 | old status line (see below) |
| `b14_snap_check3` (b14) | melee ~424 / 4-front god ~536 | 0.5 | at least 3 591 / 4 427 | 0 | pruned path ~1.3 % / ~3 % of queries |
| `b15_snap_check4` (b15) | 4-front god, 800 lab units (avg 778) | 0.5 | at least 10 670 (stage 1) | 0 | pruned path 4.2-6.0 % of queries -> ~500 pruned checks by the rig's estimate |
| **`b18_check` (b20)** | melee ~427 | 0.5 | **83 479 each** (Snapshot / Score / FastAdd); CollectPreFiltered 19 274; FriendlyFire_Grid 59 960; Rank_Sort 88 935 | 0 | fixed status line, cumulative counts |

* **Count display:** in b13-b17 the status line printed the check count of only its last 5 s window, which the rig
  summed per 45 s. Sampling itself was correct (fixed display in b18; `b18_check` shows 47-51 % sampled at 0.5). The
  b13-b15 counts are therefore lower bounds; the real ones are roughly 9x higher (coordinator's figure). The rig's
  "one tenth of the configured rate" was this misreading.
* **Grid-pruned path** (targets spread out, only nearby cells visited) stays the thinnest: even at 800 units in the
  spread god layout only 4.2-6.0 % of scans take it. The rig estimated ~500 pruned-path checks in check4 from the
  printed counts; since check4 ran on b15 with the old per-window display, that is likely a lower bound too.
* In check4 `SnapshotScore` and `FastAdd` logged check 0 (the rig: god mode leaves the scoring state nearly unchanged,
  so that run checks stage 1 only). The b18_check counts cover all three.
* Heavy sampling costs about -9 % fps.

Verdict: the three items are exact in every check and pay where it matters; `CollectPreFiltered_Snapshot` is exact but
small.

### 6.2 Dead units

**Finding (vanilla 1.0.17):** a dead unit stays in the world for 15 s, and its AI keeps running: behaviour tree twice per
second, full target scans, line-of-sight queries, pathfinding, although the unit can no longer act. Its bodies stay
awake: 18-19 % of world units in the melee and 24-27 % in the spread battle are dead bodies, 87-96 % of their
rigidbodies awake. Wheeled wrecks never sleep in vanilla.

**Clients** have no disposal timer: a wreck disappears on every client the moment the server destroys it, with no fade.
So removing sooner is the only server-side way to cut the wreck count, and it is visible.

Everything tried (paired vs A = shipped defaults, melee plateaus):

| run (build) | arm | ~424-430 | ~521-528 | visible? | note |
|---|---|---:|---:|---|---|
| `b13_dead` (b13) | B `DeadSettle` | +0.8 % (0.5) | **+8.6 %** (0.4, 2 blocks) | no | only ~12 dead units asleep at a time |
| `b13_dead` | C `DeadSettle` + `DeadDisposeEarly` 5 s | -19.3 % (1.5) | -37.2 % (0.7) | yes | |
| `b14_dead` (b14) | D `DeadAgentHalt` (ClearAim true) | -4.9 % (3.3) | -15.7 % (5.9) | no | agents' scan calls -27 % |
| `b14_dead` | E D + `DeadSettle` + `DeadNoUnitContact` (creatures) | -10.7 % (1.3) | -18.7 % (0.2) | yes (clipping) | ~36 dead units asleep |
| `b14_dead` | C `DeadDisposeEarly` always 5 s | -24.1 % (0.4) | -41.5 % (1.9) | yes | |
| `b15_halt_guard1` (b15) | D, ClearAim false | -10.3 % (2.6, 7 blocks) | - | no | behaviour guard below |
| `b15_halt_guard2` (b15) | D, ClearAim true | -11.8 % (2.1, 5 blocks) | - | no | |
| `b15_eveh` (b15) | D, ClearAim false | -19.2 % (4.2) | -12.7 % (2.5) | no | |
| `b15_eveh` | E with creatures **and vehicles**, vs D | +18.2 % (4.7, worse) | -1.8 % (0.2, 2 blocks) | yes | allow-list mask confirmed |

**`DeadAgentHalt`** (1 s after death the agent stops; harvesters, alien queen, seated or player units excluded). Saving
comes from the agents (target detection calls -27 %, agent update -19 to -23 %); physics unchanged. Behaviour guard on
the ~424 melee, 60 s blocks (mean ± s.e. over blocks):

| run | arm (blocks) | deaths/s | kills/s | shots/unit-s | hits/s | with-target share | fps |
|---|---|---|---|---|---|---|---|
| guard1, ClearAim false | A (5) | 11.9 ± 0.28 | 11.0 ± 0.28 | 0.863 ± 0.024 | 198 ± 3.1 | 0.894 ± 0.004 | 58.2 ± 0.8 |
| | D (6) | 11.6 ± 0.08 | 10.8 ± 0.09 | 0.869 ± 0.019 | 200 ± 3.7 | 0.893 ± 0.002 | 65.6 ± 0.4 |
| | D vs A | -2.1 % (t -0.9) | -1.9 % (t -0.7) | +0.6 % (t +0.2) | +0.8 % (t +0.3) | -0.1 % (t -0.2) | +12.6 % |
| guard2, ClearAim true | A (5) | 11.6 ± 0.23 | 10.8 ± 0.24 | 0.917 ± 0.039 | 186 ± 4.0 | 0.857 ± 0.003 | 46.6 ± 1.5 |
| | D (4) | 11.9 ± 0.08 | 11.1 ± 0.08 | 0.853 ± 0.009 | 177 ± 3.5 | 0.849 ± 0.006 | 52.1 ± 1.2 |
| | D vs A | +3.0 % (t +1.4) | +2.6 % (t +1.1) | **-7.0 % (t -1.6)** | **-4.7 % (t -1.7)** | -0.9 % (t -1.2) | +11.9 % |

* ClearAim mechanism (author, code reading): a corpse that still holds an aim counts as an attacker on its target, and
  live AI spreads fire away from "attacked" targets. Clearing all corpse aims at once lets the living focus fire more.
  With ClearAim false each corpse keeps its last aim until disposal, the closer match to vanilla (default since b15).
* `b15_eveh` (2-4 blocks per arm) shows D vs A at 450 deaths -8 %, shots +6 %; at 550 deaths +10 %, hits +8 %: mixed
  signs, block noise. A halt-only guard on a second scenario was not run; the spread run `b19_spread` guards the whole
  "new" set (with-target 0.59-0.62 in every arm, all metrics within ±4 % at ~422).

**`DeadSettle`** failed in the melee: live units keep shoving corpses awake (per 10 s at 450 / 550: 87 / 103 seen, 31 / 35
settled, 44 / 54 rewoken, 44 / 53 hit the 12 s give-up limit). Removing their avoidance capsules made live avoidance more
expensive (26.6 -> 32.7 ms/s), so `DeadSettleRemoveAvoidance` is now false by default.

**`DeadNoUnitContact`** (dead bodies stop colliding with live units; shots and hits unchanged; visible as clipping): with
creatures and vehicles (`b15_eveh`) the mask is exactly the four allow-list layers (`Character`, `Vehicle Avoidance`,
`Ignore Raycast Avoidance`, `Invisible Walls Avoidance`); `CollideWithStatic` is gone. DeadSettle then puts only 35-40
more bodies to sleep (asleep dead bodies 20 -> 55-60; 87-90 % stay awake), and E is worse than D at ~427. E at ~523
showed shots/unit-s -16 % on only 2 blocks (flagged, not a finding).

**`DeadDisposeEarly`**, three control forms:

| variant (run) | how it works | result at the ~521 plateau |
|---|---|---|
| always 5 s (`b14_dead`, `b15_best_plat`) | every dead unit disposed after 5 s | -41.5 % frame work vs defaults (b14); 40.1 vs 27.3 fps for new (b15, same run) |
| on/off valve (`b15_valve`, b15) | 5 s while smoothed fps < 30, 15 s again above 35 | 37.2 fps mean, but a limit cycle: 11 opens and 10 closes in 5 min (~2.2 per minute), world units swinging 620-740, wrecks vanishing in bursts every half minute. Author: loop gain ≈ 2.6. |
| fps-proportional (`b17_dispose_prop`, b17) | 15 s at >= 45 fps, linear down to 5 s at <= 25 fps; fps smoothed over 10 s; changes at most 0.5 s per second | settles (author's loop-gain estimate ≈ 0.65): last 3 min dispose time 7.94-10.33 s (mean ~9.2; author's ±1 s pass mark just missed at ±1.2), world units 652-721 (one sample +7 %, the rest ±3 %), smoothed fps 30.9-35.7 without a repeating swing; plateau 37.98 fps (separate start) |
| always, switched on mid-plateau (`b17_dispose_always`, b17) | Low = High = 0, slew still applies | dispose time 15 -> 5 s in ~20 s; world units 709 -> 615 in one 10 s step (**~95 bodies leave at once**), fps 15.6 -> 17-21.5; that start ran heavy before the switch, absolute fps not comparable |
| proportional on top of "all", paired (`b20_dispose_paired`, b20, ~514 melee / ~518 spread, 120 s blocks, not interleaved as planned) | the other items already give 35-47 fps | control at 10.3 s (melee) / 12.5 s (spread); wrecks -30 % / -17 %; frame work -10.5 % (s.e. 1.3, n 2; not protected against a time trend) / -2.2 % (s.e. 7.9, noise) |
| proportional on top of "all", strictly alternating 90 s blocks (`b20_dispose_paired2`, melee ~592 lab units) | overloaded: "all" at 21 fps | **-31.6 % (s.e. 5.0, n 4), 21.1 -> 30.0 fps**; dispose time 6.2 s (5.0-7.1) at smoothed 27.3 fps; dead units 161 -> 79 (20.0 -> 11.1 % of world units); shots and with-target unchanged, deaths +11 %, kills +13 %, hits +6 % at 42 % higher fps (same fps effect as elsewhere) |

The gain grows with load, as the proportional design intends: large when the server is overloaded, small once the
other items have restored fps.

### 6.3 Parked vehicles

**Cause**, found in steps:
1. b9's idle test was stricter than vanilla's (a parked AI vehicle keeps its last steering angle forever); fixed in b10.
   With the diagnostics off arm A is back at no-mod cost (3.84 vs 3.78 ms).
2. Vanilla parked AI vehicles drive themselves forward a little (avoidance "detour point" for a unit without a path,
   0/0 in the braking test): `AvoidanceNoIdleDetour`. Pushes from standing neighbours wake them:
   `AvoidanceSkipOntoResting`.
3. Even then, force-slept vehicles were woken ~17 times per vehicle per second. `fixedwake_b11` put 99.99 % of
   49 000-55 000 wakes per 10 s on `VehicleWheeled.OrderedFixedUpdate`, changing exactly the four steering wheels. The
   game rewrites an awake steering wheel's joint drive mode and rotation every step; the joint write wakes the chassis.
   `ParkedWheelHold` (b12) puts slow steering wheels of held vehicles back to sleep before that code runs.

Timing (paired vs A = shipped defaults; 60 s blocks, 30 s settle; diagnostics off in timing arms):

| scenario | arm | frame work ms | vs A (s.e.) | wheeled bodies asleep | PhysX ms/s | avoidance ms/s |
|---|---|---:|---:|---:|---:|---:|
| 300 idle LightStrikers, tight grid (`phys_b10`) | A | 3.84 | | 3 % | 156 | 36.5 |
| | C `IdleForceSleep` | 3.47 | -8.6 % (3.5) | 40 % | 130 | 29.1 |
| | F = C + 2 avoidance items | 3.21 | -18.7 % (1.5) | 71 % | 120 | 13.4 |
| same (`phys_b12`, b12) | A | 3.39 | | 7 % | 133 | 26.9 |
| | F | 2.45 | -27.8 % (10; F inherited H's sleeping wheels) | 90 % | 62 | 11.2 |
| | **H = F + `ParkedWheelHold`** | **2.12** | **-31.3 % (0.4)** | **98 %** | **30** | 10.0 |
| 300 mixed, 4 prefab grids (`phys_b10`) | C / F | 3.87 / 3.29 | -0.3 % / -15.6 % (1.9) | 22 / 67 % | 157 / 127 | |
| same (`phys_b12`) | **H** | **2.55** | **-37.9 % (1 pair)** | 83 % | 56 | 15.2 |
| spaced, steep lots 14-21° (`parked_v2`, b10) | C / F | 2.89 / 2.81 | -0.7 % / -3.3 % (11: nothing) | 57 / 67 % | 100 / 94 | |
| spaced, moderate slope 8-14° (`parked_v2`) | C / F | 3.41 / 2.98 | -2.4 % / **-13.9 % (3.0)** | 44 / 73 % | 138 / 106 | |
| 300 moving vehicles (guard, `phys_b12`) | A / F / H | 5.27 / 5.25 / 5.20 | -1.6 % (1 pair) | 0-1 % | 221-226 | 39-40 |
| spaced lots, mean 11-13°, max 21-29° (`b20_parked_spaced`, b20) | A / H | 2.71 / 2.01 | -27.2 % (19.8) | 44 / 99 % | 91.6 / 22.7 | 6.6 / 5.1 |
| spaced slope lots, mean 10-11°, max 18-19° (`b20_parked_spaced`) | A / H | 2.95 / 2.24 | -21.6 % (5.0) | 14 / 95 % | 118.0 / 35.7 | 6.6 / 5.9 |

* With H: re-sleeps 0 per 10 s (F: 13 140), trace wakes 0 (F: 49-55 k), 100 % of 2 730 wheeled bodies asleep
  (`phys_b12_HD`). Repeated on b20 with the sync refinement (`b18_phys_guard_idle`): 99.0-99.7 % asleep at steady state,
  re-sleeps 0-5 per 10 s, held vehicles needing a sync 0 at steady state.
* Creep: C (alone) mean 0.38-0.57 m, max ~3 m; F 0; H mean 1 mm, max 4.4 cm; vanilla awake parked vehicles (wave 1)
  ~1 m per 10 s, up to 2 m.

Order response (order probe: 100 Sol + 60 Centauri parked vehicles, 50 + 50 infantry, 70 creatures; 4 move orders 30 m
out and back; p50 per cycle, range over 4 cycles, s):

| kind | reach 2 m, A (b10) | reach 2 m, F (b10) | reach 2 m, H (b12) | first move A / F / H |
|---|---|---|---|---|
| Sol light (60) | 1.44-1.56 | 1.31-1.60 | 1.55-1.64 | 0.68-0.84 / 0.10-0.48 / 0.80-0.88 |
| Sol heavy (40) | 1.44-1.60 | 1.44-1.60 | 1.48-1.56 | 0.71-0.84 / 0.11-0.48 / 0.68-0.80 |
| Cent light (40) | 1.91-2.08 | 1.84-2.04 | 1.92-2.00 | 1.15-1.24 / 0.11-1.08 / 1.08-1.16 |
| Cent heavy (20) | 2.48-2.96 | 2.36-2.84 | 2.32-2.92 | 1.32-1.56 / 1.24-1.37 / 1.24-1.48 |

0 order failures, 0 "never started" in every arm; sleeping vehicles do not respond later. F's early "first move" is a
jolt at wake, not driving; H does not show it. Infantry and creatures identical across arms. One Sol light vehicle in
H took 5.3 s once (p95 1.84 s).

Verdict: solved in the lab. Players would see parked AI groups stop jostling; wrecks, moving units, orders, hits and
players entering still wake them. Needs a client look first.

### 6.4 Worker-thread scans (`MultiCore.ParallelScan`, `ParallelScan_Prefiltered`) and the multi-core question

How it works: at the start of the agent loop it predicts which agents will scan this frame, snapshots the targets as
plain data, computes those scans on Unity's worker threads and hands vanilla the result when it asks. Anything that
changed in between is rescored on the main thread; anything it cannot prove equal falls back to the normal path.
**Round 2** (b19/b20, author): dirty marks only on real changes (round 1 marked ~1 000/s, causing the `tooManyDirty`
fallbacks); the main thread computes a scan no worker has started; formerly refused cases (targets without ObjectInfo,
structures with more than 4 turret targets, type-value cache overflow) are served through an emulation of vanilla's
32-entry type-value cache; a per-agent history gate cuts wasted predictions. `ParallelScan_Prefiltered` moves the
re-evaluation scans to the workers too.

Correctness:
* Round 1 (`b17_pscan_check2`): 19 571 checks, all identical, type-value state different 0.
* Round 2 (`b19_pscan_check`, b20): 13 649 full-path + 9 803 prefiltered checks, all identical, type-value state
  different 0, no worker errors, 0 scans run on the main thread by Unity during the plateau. **Gap:** the emulated
  type-value path was 0 in every window (also in `b19_pscan`), so it is verified only by the author's offline tests.

Timing:

| run (build) | plateau | arms | fps | frame work vs reference (s.e.) |
|---|---|---|---|---:|
| `b17_pscan` (b17, round 1) | ~424 / ~521 | S3 -> P | 64.5 -> 65.9 / 26.9 -> 31.9 | -2.2 % (2.1) / -16.3 % (4.8) |
| `b17_pscan_inline` (round 1, workers off) | ~521 | S3 -> P inline | 29.5 -> 29.9 | -2.7 % (4.5) |
| `b19_pscan` (b20, round 2) | ~430 | B -> P / PP | 66.5 -> 71.2 / 69.8 | -7.7 % (1.5) / -5.6 % (1.6) |
| `b19_pscan` | ~527 | B -> P / PP | 25.2 -> **36.0 / 36.4** | **-30.4 % (2.9) / -31.5 % (4.2)** |
| `b20_best` (b20) | ~427 / ~523 | new -> all | 63.8 -> 68.7 / 21.2 -> 31.3 | -7.9 % (7.8) / **-30.9 % (7.4)** |
| `b20_best2` (b20, repeat) | ~425 / ~521 | new -> all | 57.1 -> 68.8 / 24.6 -> 32.1 | -19.2 % (1.2) / **-25.1 % (4.0)** |
| `b19_spread` (b20, spread) | ~422 / ~518 / ~614 | new -> all | 77.0 -> 76.4 / 38.6 -> 43.3 / 17.8 -> 19.0 | +1.9 % (2.5) / -5.1 % (7.1) / -8.1 % (2.5) |

(S3 / B / new = shipped defaults + target-scan items + DeadAgentHalt, B with the prefiltered replay; P = + ParallelScan;
PP = P + ParallelScan_Prefiltered; all = new + prefiltered replay + both ParallelScan items.)

* Round 1 -> round 2 at ~527: served share ~79 % -> 94-96 %, wasted 55 -> 14-21 per second, `tooManyDirty` 12/s -> 0,
  main-thread staging + serving 7.0-8.3 ms/s, worker time 34-49 ms/s.
* The prefiltered path takes another 15-18 ms/s off the agent update (55.8 vs 73.8 ms/s at ~527), not separable from P
  at frame level.
* In `b20_best` at ~523 the whole targeting chain falls from 205 ms/s (off) / 126 (new) to 25 ms/s ("all").
* Behaviour: kills, deaths and with-target unchanged; shots/unit-s +5-6 % in both P arms at ~527, where they run at
  higher fps.
* **Workers-off control** (round 1): computed inline, the scan itself gets as cheap, but the main thread pays the
  kernel; no gain. The gain is the work moved to other cores.
* **Spread layout:** fewer agents per front see each other, so these scans are a smaller share of the frame; the gain
  shrinks to noise at ~422-518 and -8 % at ~614. **ParallelScan is a dense-battle item.**

**Multi-core answer:** a mod can move work to other cores where it is plain arithmetic on data that can be snapshot once
per frame and shared across many calls (here ~10-25 scans per team per frame share one snapshot). Yesterday's
`SensorTeamMaps_Job` failed because staging cost as much as it moved. What remains on the main thread cannot be moved
this way: the PhysX step (~34 %) is Unity's own; turret, creature, projectile and behaviour-tree work calls Unity
objects that are main-thread only. Lowering vehicle solver iterations did not change PhysX time (6.6).

### 6.5 Shipped defaults: quick checks and ramps (build b10; arms off / def / agg = defaults + LosTopK + FriendlyFire_Cache + SensorPipeline_Rate)

| arm | < 30 fps qc1 / qc2 | < 20 fps qc1 / qc2 | < 10 fps qc1 / qc2 | fit at 300 qc1 / qc2 | fit at 500 qc1 / qc2 |
|---|---|---|---|---|---|
| off | 434 / 431 | 457 / 474 | 570 / 558 | 10.6 / 8.7 ms | 67.2 / 63.7 ms |
| def | 469 / 492 | 509 / 523 | not reached | 7.9 / 6.9 ms | 47.2 / 41.8 ms |
| agg | 484 / 483 | 521 / 525 | not reached | 5.7 / 5.7 ms | 44.1 / 39.7 ms |

* Crossings agree within 3-23 units between the two runs; at 500 units def vs off -30 % / -34 %, agg vs off -34 % /
  -38 %. Single fit points are good to about ±15 %. A 50-unit bin in qc1 showing def -41 % was a binning artefact.
* God-mode trickle (`qcgod1`): off 607 / 665 / 812, def 668 / 743 / not reached, agg 688 / 743 / not reached (lab units
  at < 30 / < 20 / < 10 fps). Defaults gain +61 units at the 30 fps point without deaths vs +35-61 with damage.

Four-front ramps, paired vs off (3-4 blocks per arm and plateau):

| lab units | `ramp_god` fps off / def / agg | def vs off (s.e.) | agg vs off (s.e.) | `ramp_sustain` lab units | fps off / def / agg | def vs off (s.e.) | agg vs off (s.e.) |
|---:|---|---:|---:|---:|---|---:|---:|
| 193 | 116 / 119 / 120 | -15.1 % (0.6) | -24.4 % (0.4) | 186 | 113 / 116 / 117 | -15.0 % (1.1) | -21.1 % (1.5) |
| 292 | 105 / 109 / 111 | -17.2 % (0.6) | -25.3 % (0.4) | 280 | 98 / 104 / 104 | -16.7 % (1.6) | -20.0 % (0.6) |
| 389 | 88 / 95 / 99 | -17.4 % (1.0) | -28.0 % (0.8) | 374 | 70 / 85 / 86 | -25.9 % (1.6) | -27.5 % (1.4) |
| 485 | 65 / 77 / 83 | -21.4 % (0.6) | -32.5 % (0.4) | 467 | **30 / 50 / 53** | -41.4 % (1.4) | -45.6 % (2.0) |
| 582 | **38 / 54 / 65** | -29.1 % (1.0) | -43.8 % (1.2) | 563 | 13 / 20 / 21 | -36.2 % (1.4) | -37.7 % (2.1) |
| 681 | 21 / 30 / 37 | -31.4 % (3.8) | -46.2 % (2.2) | 665 | 9.1 / 11.6 / 12.1 | -21.5 % (1.0) | -25.8 % (1.3) |
| 779 | 13 / 19 / 20 | -31.7 % (1.2) | -37.4 % (0.6) | 767 | 7.2 / 9.5 / 9.6 | -25.6 % (0.3) | -25.9 % (1.3) |
| 971 | 8.4 / 10.8 / 11.0 | -23.6 % (0.8) | -25.7 % (0.1) | (stopped at the 10 fps floor after @800) | | | |

* The approximate set adds only 1-5 points with damage (8-15 in god mode); `SensorPipeline_Rate` does nothing below
  20 fps.
* Below 12.5 fps the defaults' `CatchUpCap` (at most 2 physics steps per frame) slows the game clock (0.8x at 10 fps);
  vanilla's clamp slows it below 10 fps.
* 0 exceptions in all arms; the shakedown fog hang did not recur in 85 min of ramps.

### 6.6 Other items and runs

**20 Hz physics step** (`qc_fixed20`, b10, defaults vs defaults + `FixedStep_Override`; census confirms 25 <-> 20 steps/s
and at most 2 / 1 steps per frame). Information only: it changes step-count-tied game logic (`TimeStep.cs`).

| | defaults | defaults + 20 Hz |
|---|---:|---:|
| < 30 fps at (lab units) | 516 | 548 |
| < 20 fps at | 577 | 620 |
| < 10 fps at | 912 | not reached (10.4 fps at 900-999) |
| fit work @500 / @800 | 30.7 / 86.4 ms | 25.9 / 77.9 ms (-15 % / -10 %) |

100-unit bins: 400-499 -24 %, 500-599 -28 %, 600-699 -12 %, 700-999 -6 to -8 %. PhysX per step +9 % but 20 % fewer steps.
Deaths, kills, shots, hits, with-target within ±3 % (|t| < 1, 19-20 blocks). This start ran much lighter than the
other quick checks (defaults < 30 fps at 516 vs 469-492).

**`WheelJointWriteGuard`** (b18, exact): skips the per-step joint drive-mode write when unchanged and a bitwise-equal
steering-wheel rotation write. The two per-step wheel velocity writes are deliberately left alone (a same-value
velocity write still resets the PhysX wake counter, so skipping it would change when wheels fall asleep). It skips
~31-40 k native writes/s in the melee and ~60 k/s with 300 moving vehicles, with **no measurable saving** anywhere:
melee +0.3 % / +10.5 % (s.e. 3.6 / 7.2, noise), moving vehicles -0.1 % (s.e. 1.2); VehicleWheeled stays at 4.3-4.7 µs
per call. The skipped native call is too cheap to matter.

**`VehicleSolverIterations`** (b18, experiment, approximate): PhysX solver iterations 6 -> 4 on ~200 AI wheeled vehicles
in the melee and on 300 moving vehicles (a pure-vehicle setup): PhysX time unchanged (12.96 vs 12.84 ms per step at
~525; 215.9 vs 215.9 ms/s moving). **No effect.**

**`FriendlyFire_Flat`** (b20, exact): friendly-fire check on plain per-frame arrays. 2 000 812 self-checks, 0 mismatches
(`b20_ff_check`). Frame work +1.4 % / +3.6 % (s.e. 2.6 / 7.9, noise) in `b20_ff`: vanilla's check cost only 4.1-4.9
µs per call in that run, not the 7.4 µs the estimate assumed, so nothing measurable was left. No sign of a changed
friendly-fire veto (shots within 1.5 %).

**Deep probe** (`b17_deepprobe`, b17, 550 plateau at only 484 lab units, arm P, 230 s, 30.4 fps; profiler overhead
~21 ms/s; 0 exceptions). Decision values set before the run:

| method | own time ms/s | calls/s | µs/call | threshold | verdict |
|---|---:|---:|---:|---|---|
| `TargetFinder.CollectPreFilteredEnemies` | 32.5 | 159 | 204 | >= 35 -> E1 | just under; built anyway (b18), small gain |
| `LineOfSightManager.RequestQueryToTarget` | 6.8 | 11 391 | 0.6 | >= 15 | no |
| path / order re-issue | ≤ 0.5 | | | >= 15 | no (`AIPathfinding.QueueFindPath` not probed: type not found) |
| `CreatureAttack.CanHitTarget` | 0.8 | 455 | 2.7 | >= 25 -> E5 | no |
| `ProjectileBasic.NonBatchedRaycastP1` | 5.9 | 1 251 | 4.8 | >= 30 -> E4 | no |
| `CreatureDecapod.GetAIPathPursuitPoint` | **24.1** | 295 | 82 | - | notable |
| `ProjectileBasic.HandleHit` | **28.0** (total printed 53.8) | 642 | 84 | - | notable, see below |

* **HandleHit** (author's follow-up): the printed 53.8 ms/s was double counted (the probe patched two overloads, one only
  wraps the other); real ≈ 405 hits/s, 30.7 ms/s inclusive, 28.0 own, ApplyDamage 2.7. Impact and audio effects cost
  almost nothing on the server (`ImpactEffectManager.RequestImpactEffect` and `AudioEffectManager.RequestAudioEffect` are
  empty stubs in the server DLL, checked in IL). By elimination the cost is `Detonate` on splash hits (10-30 m blast
  radii: overlap sphere, closest point over every damageable collider, line casts): gameplay, not removable exactly.
  Inferred, not probed; probes to confirm: `ProjectileBasic:Detonate`, `HandleRicochet`, `PlayImpactFX`, `UpdateLifetime`.
* **GetAIPathPursuitPoint** (author): the expensive part, snapping path points to rock (26 directions x 2 raycasts per
  snap), is already memoised by vanilla per path version; new cost comes from first-touch points on rebuilt paths. No
  exact cache is possible (result depends on live static colliders without a change signal); an approximate shared
  2-5 s cache is possible, not built. A measurement-only diagnostic `Services.Diag_PursuitSnapShadow` exists in the
  private copy `Si_ServerPerf_work\HotMethods` (not merged) to measure the repeat rate first.

**Dropped for time:** `qc3`, `probe_F2`, `probe_A2` (repeats).

---

## 7. Per-item table

Evidence: **strong** = paired, several blocks, repeated or confirmed by another run; **medium** = paired, one run, 2-4
blocks per arm; **weak** = unpaired or separate start. "Rec." = the managing agent's recommendation.

| item | what it does | exact / approx. | visible | measured effect | evidence | default now | rec. |
|---|---|---|---|---|---|---|---|
| shipped defaults (Headless, UpdateTiers without Tier_IdleAgents, Targeting exact items, Services, Physics Census + CatchUpCap) | as in b10 | mostly exact | no | -15 to -17 % at 200-300 units, -26 to -43 % at 470-600 (ramps, spread); -27 to -38 % (melee) | strong | on | keep |
| `Targeting.CollectEnemies_Snapshot` / `_SnapshotScore` / `_FastAdd` | cheaper full target scan | exact (83 479 checks each) | no | set: -23.0 % at ~521 vs defaults; "new" -47.5 % vs off (melee), -40 to -52 % (spread) | strong | off | **on** |
| `Targeting.CollectPreFiltered_Snapshot` | same for the re-evaluation scans | exact (19 274) | no | own method -1/3 (4-5 ms/s), frame noise | medium | off | optional; on with ParallelScan_Prefiltered |
| `Targeting.FriendlyFire_Flat` | friendly-fire check on plain arrays | exact (2 000 812) | no | +1.4 / +3.6 % (noise) | medium | off | off (no gain) |
| `Targeting.Scan_Diag` | counters | n/a | no | diagnostics | - | on | on |
| `Targeting.FriendlyFire_StaleGuard` (shipped) | skips targets destroyed since the last grid update | exact | no | exercised under load tonight (1-3 guarded calls/s in damage runs); no NullReferenceException in any night log | medium | on | on |
| `Targeting.LosTopK` | LOS only for top 6 candidates | approx. | possibly | -6.2 / -6.8 %; "no target" +5 points, full scans +10 % | medium | off | aggressive set |
| `Targeting.FriendlyFire_Cache` | reuse a friendly-fire answer 0.15 s | approx. | possibly | part of agg | medium | off | aggressive set |
| `MultiCore.SensorPipeline_Rate` | sensors at fixed 20 Hz | approx. | possibly | nothing below 20 fps; part of agg | medium | off | aggressive set |
| `MultiCore.ParallelScan` (round 2) | full scans on worker threads | exact (13 649 + 19 571 r1) | no | melee -30.4 % (vs new2), spread +2 to -8 % | strong (melee) | off | opt-in for big battles |
| `MultiCore.ParallelScan_Prefiltered` | re-evaluation scans on workers | exact (9 803) | no | -15 to -18 ms/s agents; frame level not separable from ParallelScan | medium | off | opt-in with ParallelScan |
| `Physics.DeadAgentHalt` | dead units' AI stops 1 s after death | exact for the living (guard, aim kept) | no | -4.9 to -19.2 % in three runs | strong | off | **on** |
| `Physics.DeadAgentHaltClearAim` | drop corpse aims at halt | changes saturation term | no | shots -7 %, hits -4.7 % | medium | false | false |
| `Physics.DeadDisposeEarly` | dispose sooner (always 5 s, or fps-proportional) | approx. | **yes** | always -24 / -41.5 %; proportional 38.0 fps at ~522, settles; valve cycles | medium (always strong) | off | your choice; proportional |
| `Physics.DeadSettle` | sleep dead bodies once slow | approx. | slightly | +0.8 / +8.6 % | medium | off | off |
| `Physics.DeadNoUnitContact` | dead bodies ignore live units | approx. | **yes** (clipping) | with DeadSettle: E vs D +18.2 % / -1.8 % | medium | off | off |
| `Physics.IdleForceSleep` | sleep idle AI vehicles / creatures after 2 s | approx. | slightly | alone -8.6 / -0.3 %, creep up to 3 m | strong | off | only in the parked set |
| `Physics.AvoidanceSkipOntoResting` | no push onto a parked AI vehicle from a standing unit | approx. | slightly | in F: -18.7 / -15.6 %; spaced slope -13.9 % | strong | off | parked set, client first |
| `Physics.AvoidanceNoIdleDetour` | no detour drive for units without a destination | approx. (removes a vanilla lurch) | slightly | in F; creep 0 | strong | off | parked set, client first |
| `Physics.ParkedWheelHold` | keeps held vehicles' steering wheels asleep | approx. | slightly | H -31.3 % / -37.9 %; 98-100 % asleep; order response unchanged | medium-strong | off | parked set, client first |
| `Physics.WheelJointWriteGuard` | skip unchanged wheel joint / rotation writes | exact | no | ~35-60 k writes/s skipped, no saving | medium | off | off (no gain) |
| `Physics.VehicleSolverIterations` | solver 6 -> 4 on AI vehicles | approx. (experiment) | yes if used | no effect on PhysX time | medium | off | off |
| `Physics.FixedStep_Override` | physics at 20 Hz | approx. | **yes** (physics feel) | -24 / -28 % at 400-599, -6 to -12 % above; behaviour ±3 % | medium | off | information; client needed |
| `Physics.CatchUpCap` (shipped) | ≤ 2 steps per frame | approx. | slow motion below 12.5 fps | clock 0.8x at 10 fps | strong | on | keep |
| `Physics.FixedWakeTrace`, `IdleSleepDiag`, `WheeledSleepDiag` | diagnostics | n/a | no | WheeledSleepDiag cost ~0.7 ms in b9 | - | off | off |
| `UpdateTiers.Tier_IdleAgents` | (wave 1) | approx. | no | costs +3.3 ms/s (wave 1) | strong | off | off |

---

## 8. Defects and side findings

**Si_ServerPerf**
* **Self-check count display** (b13-b17, fixed in b18): the Targeting status line printed the check count of only its
  last 5 s window; summed per 45 s it looked like sampling at one tenth of the configured rate. Sampling is correct;
  b13-b15 check totals are lower bounds (real roughly 9x).
* **Counter display:** `CollectEnemies_SnapshotScore` / `_FastAdd` print `q/s 0` while their `grid` field shows ~100-260 k
  (b13-b17; fixed in b18+, per the Targeting author; b18_check shows per-item check totals). The core shows "ON 13/13" even when a
  second patch on an already patched method failed (yesterday's WakeProbe, fixed there).
* `DeadNoUnitContact` mask contained `CollideWithStatic` in b14 (harmless); fixed by the allow-list in b15, confirmed in
  `b15_eveh`.
* No game-logic defect in any item: 0 exceptions in every arm of every run, nothing FAILED or NOT APPLIED (b10-b20).

**Test tool Si_PerfLab** (`C:\Users\schwe\Projects\Silica_Performance\Si_PerfLab`)
* 0.2.1: god-mode front-stall loop fixed (shots and creature attacks count as activity); "no lot" warning reports the
  flattest lot.
* 0.2.2: `engagement_split_by_sig` (engagement per arm block).
* 0.2.3: `runtime.json` hashed on every poll. Before, flips between files with the same modification time were missed,
  so the per-block engagement split of `b15_halt_guard1/2` covered only 5-6 of ~11 blocks (arm reports from
  `flips.csv` unaffected).
* Open suggestions: a parking group that finds no lot runs silently empty (`terrain: "flat"` is impossible on GreatErg:
  flattest lot within 900 m of the HQs 12.1° max / 7.7° mean); `structure_ratio` applies to the maximum unit count.
* PerfProbe patches every overload of a probed method name, which double-counted `ProjectileBasic.HandleHit`.
* Rig slips (harmless): `b17_pscan_check` without the MultiCore sample rate (rerun); chain report names with a path
  (fixed); the second rig's first driver start matched its own log text and set the stop marker during `qc_fixed20`,
  removed within 20 s (the chain only checks between runs).

**Game (vanilla 1.0.17)**
* Dead units' AI keeps running for the 15 s before disposal; wheeled wrecks never sleep; ~90-96 % of dead bodies awake.
* A dead unit's last aim keeps counting as an attacker on its target.
* Clients have no disposal timer; a wreck vanishes on every client the moment the server destroys it.
* Parked AI vehicles are re-woken every step by steering-wheel joint and rotation writes, besides yesterday's
  avoidance-detour shuffle.
* A "no target found" scan result is never throttled, so target-less agents rescan every tick.
* Impact and audio effect requests are empty stubs on the server; projectile hit cost is splash detonation.
* **One unreproduced server hang** (`shake1`, 20:45:30-20:46:54, b10 defaults, 4 fronts at 300 units, base with radar
  stations): `FogOfWar.LateUpdate` 48 s over 42 frames, two job workers at 100 %, main thread waiting in the vanilla
  `ExploreAreaJob`; fps 0.9 until scenario cleanup. Not reproduced with all items off (`shake3_off`), with defaults
  (`shake4_def`), in god mode with radar bases (`shake2`), nor in 85 min of ramps. Si_ServerPerf does not patch
  `FogOfWar.LateUpdate` or that job, but its default `Headless.Fog_DetectedTargetLookup` sits in `FogOfWar.OnUpdate`,
  where the job inputs are built, so it is not ruled out. Possibly related to the live hang of 2026-11-13. Evidence:
  `night1\shake1\perfprobe\*\methods.csv`, `threads.csv`.

**Source disagreements**
* ParallelScan round-1 s.e.: `ANALYSIS_BATTLE_2.md` "1.8" / "1.6" are ms; the rig's 4.8 % / 4.5 % are the same blocks.
* `ANALYSIS_BATTLE_2.md` (A3) concluded dead bodies cost ~10x less PhysX than live ones, from DeadSettle "making ~450
  dead bodies kinematic". The census shows only ~12-13 dead units asleep at a time in that run. In `b14_dead` early
  disposal removed ~326 awake dead bodies and PhysX fell 272 -> 219 ms/s (~10.9 -> 8.8 ms per step, divided here at
  25 steps/s), ~6.5 µs per dead body per step vs 7.2-7.5 µs per live body: about the same.
* Run-to-run spread: rig draft "±15-25 %", rig report "±15 %"; examples in section 3 reach 25-30 %.
* `b15_snap_check4`: the rig counts ~500 pruned-path checks; that run used the b15 per-window display, so by the
  coordinator's own correction it is likely a lower bound.
* Author estimates vs measurement: DeadSettle (est. 45-55 ms/s saving, measured a cost); ParallelScan round 1 (est.
  85-90 ms/s net, measured 30-40); CollectPreFilteredEnemies block (est. ~50 ms/s, probed 32.5); FriendlyFire check
  (est. 7.4 µs/call, 4.1-4.9 in b20_ff); `VehicleSolverIterations` (est. 40-50 ms/s if the solver is ~40 % of PhysX,
  measured nothing).
* The first rig's closing summary lists LosTopK as ruled out; the data show it nets -6 to -7 % (with extra rescans).

---

## 9. Not measured / open, and what remains to do

Not measured:
* Anything with a client: hit registration, the parked-vehicle items, DeadNoUnitContact clipping, wreck removal
  (pop-out, bursts), 20 Hz physics feel, whether higher fps changes visible combat.
* Real AI rounds at high counts (lab rounds on 1.0.17 stall below ~320 units).
* A DeadAgentHalt-only guard on a second scenario (the spread run guards the whole "new" set).
* The grid-pruned target-scan path at volume; ParallelScan's emulated type-value path in game.
* ParallelScan over long periods or in an AI round.
* The cause of the shakedown fog hang; PhysX internals.

What remains to do, in priority order (proposal):
1. Make the three target-scan items and DeadAgentHalt (aim kept) default.
2. Client session: parked set, both wreck-removal forms, ParallelScan in a big fight, and a look at whether faster
   servers change combat visibly; then decide the parked set and wreck removal.
3. If wreck removal stays of interest: one more interleaved paired run at the ~550 melee (the -10.5 % there rests on
   2 non-interleaved blocks).
4. ParallelScan soak (30+ min, several plateaus, an AI round if possible); trigger the emulated type-value path.
5. A pruned-path-heavy self-check on b20 (correct counts now).
6. Next candidates: `Services.Diag_PursuitSnapShadow` to measure creature pursuit-point repeats (24 ms/s) before an
   approximate cache; probes `Detonate` / `HandleRicochet` / `PlayImpactFX` / `UpdateLifetime` to confirm the projectile
   hit cost. Behaviour-tree interval for engaged agents (A5, approximate) remains an unbuilt idea.
7. 20 Hz step: only if you want to pursue it; client test first.
8. S4 placement budget: your decision.
9. Si_RTS_AI `OnAttackOrder` group-emptying bug (separate fix).
10. Fog hang: keep watching; a long radar-base ramp with PerfProbe if it recurs.

---

## 10. Where things are

Builds (`C:\Users\schwe\Projects\Si_ServerPerf\release\`; each contains the previous; **source in the main project = b20**):

| build | added |
|---|---|
| b10 | base of the night: shipped defaults, physics idle-test fix, `AvoidanceNoIdleDetour`, diagnostics default off |
| b11 | `FixedWakeTrace` (md5 5CF1F6FA...) |
| b12 | `ParkedWheelHold` (md5 36E59181...) |
| b13 | `DeadSettle`, `DeadDisposeEarly` (5 s), census dead/alive split, `CollectEnemies_Snapshot` / `_SnapshotScore` / `_FastAdd`, `Scan_Diag` |
| b14 | `DeadAgentHalt` (ClearAim default true), `DeadNoUnitContact` (sampled mask), dispose valve, `DeadSettleRemoveAvoidance` false |
| b15 | `DeadAgentHaltClearAim` default false + counters, `DeadNoUnitContact` allow-list (md5 BA60082F...) |
| b16 | `MultiCore.ParallelScan` round 1 (md5 56DAB088...; never run, superseded) |
| b17 | b16 + `DeadDisposeEarly` fps-proportional, valve removed (md5 F940BA24...) |
| b18 | `CollectPreFiltered_Snapshot`, `WheelJointWriteGuard`, `VehicleSolverIterations` (experiment), ParkedWheelHold sync refinement, Targeting counter/status fixes |
| b19 | ParallelScan round 2 + `ParallelScan_Prefiltered` |
| b20 | b19 + `FriendlyFire_Flat` (md5 82F1D0C7...); all b18-b20 runs used b20 |

Run folders (`C:\Users\schwe\Projects\Si_ServerPerf\measure\night1\<run>\`; `report_*.txt`, `STEPS.md`, `METHODS.md`,
`SECTIONS.md`, `GUARD_*.md`, `RAMP.md`, `Latest.log`):
* shakedown: `shake1`-`shake4_def`, `failfast`
* shipped defaults: `qc1`, `qc2`, `qcgod1`, `ramp_god` (+ `WHERE_TIME_GOES.md`), `ramp_sustain`, `qc_fixed20`
* parked vehicles: `phys_b10`, `parked_v2`, `probe_A`, `probe_C`, `probe_F`, `fixedwake_b11` (`FIXEDWAKE.txt`),
  `phys_b12` (`PHYS_B12.md`), `phys_b12_HD`, `probe_H_b12`, `b18_phys_guard`, `b18_phys_guard_idle`
* target scans: `b13_snap_check`, `b13_snap`, `b13_snap_check2`, `b14_snap_check3`, `b15_snap_check4`, `b18_check`,
  `b18_items`, `b20_ff_check`, `b20_ff`
* dead units: `b13_dead`, `b14_dead`, `b15_valve`, `b15_halt_guard1`, `b15_halt_guard2`, `b15_eveh`, `b17_dispose_prop`,
  `b17_dispose_always`
* worker threads: `b17_pscan_check`, `b17_pscan_check2`, `b17_pscan`, `b17_pscan_inline`, `b19_pscan_check`, `b19_pscan`
* headline and spread: `b15_best_plat`, `b15_best_qc` (`CROSSINGS.md`), `b20_best`, `b20_best2`, `b19_spread`
* deep probe: `b17_deepprobe`
* late runs: `b20_dispose_paired`, `b20_dispose_paired2`, `b20_parked_spaced`, `b20_best2`; dropped: `qc3`, `probe_F2`,
  `probe_A2`

Documents in `night1\`: this report; `NIGHT1_rig_draft.md`; `ANALYSIS_BATTLE.md` (where the time goes at 450-600
units, the knee model); `ANALYSIS_BATTLE_2.md` (what is left with the new items, deep-probe list, candidates E1-E5 /
A1-A5); `AGENT_REPORTS.md` (verbatim reports up to 04:29; the authors' later reports on b18-b20 are summarised in
6.4 / 6.6 and not in a file); `RESULTS_LOG.md` (per-run results from 04:30, both rigs); `STATE.md`; `chain_progress.log`.
Analysis scripts: `night1\*.py` (`step_table.py`, `crossings.py`, `guard_samples.py`, `methods_by_arm.py`,
`phys_sections_by_arm.py`, ...). Wave 1: `..\wave1\WAVE1.md`. Item list and settings: `Modules\*\*.cs` headers
(`README.md` is stale).
