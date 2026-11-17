# Baseline, Silica 1.0.17 lab server, no Si_ServerPerf (2026-11-16)

## Setup

| item | value |
|---|---|
| server | `C:\tmp\silica_ds_lab` (port 27911/27910, password in the lab copy's config, not recorded here), headless, 0 clients, 120 fps cap (Si_ServerFpsCap MaxFPS 120), fixed step 40 ms |
| game | Release:1.0.17, SilicaCore md5 `16A1B0C5A0F007A43D36570248E1CAEA`. `Silica_Data` mirrored from the live install 2026-11-16 02:47 (robocopy /MIR; the lab's own `ServerSettings.xml` / `GameSettings.xml` / `MatchSettings.xml` kept). Other top-level files were already identical. |
| host | Ryzen 7 9800X3D, same machine as the 1.0.15 report; no other Silica process during any run |
| mods (lab passes) | Si_AdminMod 2.14.01, Si_PerfLab 0.1.0, Si_PerfProbe 0.1.4, Si_RTS_AI 0.94.14, Si_ServerFpsCap 1.0.0, Si_ServerOptimizer 0.7.1 (md5s in each `manifest.json`). PerfProbe and PerfLab rebuilt against the 1.0.17 refs (no source change needed; refcheck clean). Si_RTS_AI = the 1.0.17 build from the live `ModsToUpdate`. The others = live `Mods` copies (refchecked OK on 1.0.17 by the update pipeline). |
| Si_RTS_AI preset | `lab` (no faction under the mod, harness only starts the round). Si_PerfLab switches the vanilla AI commanders off. |
| PerfProbe | `probe_lab.json`: targeted mode (deep off, 57 explicit probes + game PerfSections + player-loop subsystems), 10 s intervals |
| scenarios | `scenarios_ab.json`: 21 scenarios, settle 20 s (structures 75 s, mixed 45 s) + measure 60 s; fight counts are per side |
| runs | `base_lab_p1` (03:19-03:53), `base_lab_p2` (04:04-04:38). Both 21/21 `ok`, 0 spawn failures, 0 foreign construction. p2 was archived by hand (the runner script lost a log-file write at its end); its data are complete. |

Reproduce: `powershell -File ..\run_set.ps1 -Prefix base -OutRoot baseline_1017 -Passes 2` (lab), see `..\PROTOCOL.md`.
Tables: `LAB.md` (all metrics), `base_lab_runs.csv` / `base_lab_summary.csv` / `base_lab_sections.csv` (per-run, per-scenario and per-section values, the files a treatment run is diffed against).

## Lab scenarios: two baseline passes

work = Si_PerfLab frame CPU work without the 120 fps cap wait (ms per frame). main = PerfProbe main-thread CPU in
ms per wall-second (OS thread time; includes the cap-wait overhead, so the empty server already reads about 155).
spread = |p1 - p2|, % of the mean.

| scenario | work avg p1 / p2 | spread % | work p95 p1 / p2 | spread % | main ms/s p1 / p2 | spread % | 1.0.15 work avg (same scenario) |
|---|---|---:|---|---:|---|---:|---:|
| baseline-empty | 0.88 / 0.96 | 9.1 | 1.34 / 1.67 | 22 | 139 / 173 | 21 | 1.00 |
| idle-soldiers-100 | 1.25 / 1.34 | 6.8 | 1.74 / 2.10 | 19 | 180 / 202 | 11 | 1.37 |
| idle-soldiers-300 | 2.02 / 2.14 | 5.5 | 2.67 / 3.08 | 14 | 282 / 287 | 1.6 | |
| idle-soldiers-600 | 3.26 / 3.40 | 4.3 | 4.18 / 4.54 | 8.2 | 426 / 426 | 0.1 | |
| move-soldiers-100 | 1.62 / 1.66 | 2.0 | 2.49 / 2.74 | 9.6 | 237 / 236 | 0.4 | 1.68 |
| move-soldiers-300 | 2.94 / 3.00 | 2.1 | 4.76 / 5.06 | 6.1 | 386 / 391 | 1.4 | |
| move-soldiers-600 | 4.93 / 4.93 | 0.1 | 8.19 / 8.35 | 1.9 | 625 / 627 | 0.3 | |
| idle-vehicles-100 | 1.90 / 1.92 | 1.5 | 4.87 / 5.00 | 2.7 | 266 / 290 | 8.9 | 2.01 |
| idle-vehicles-300 | 3.82 / 3.73 | 2.2 | 11.60 / 11.45 | 1.3 | 424 / 431 | 1.6 | |
| move-vehicles-100 | 2.29 / 2.29 | 0.0 | 6.27 / 6.30 | 0.4 | 303 / 311 | 2.3 | 2.36 |
| move-vehicles-300 | 5.96 / 5.73 | 3.9 | 17.25 / 16.38 | 5.2 | 553 / 548 | 1.0 | |
| idle-creatures-300 | 1.97 / 2.05 | 3.9 | 2.62 / 2.94 | 11 | 275 / 291 | 5.5 | |
| fight-soldiers-50 (100 units) | 1.34 / 1.28 | 4.7 | 2.15 / 2.22 | 2.8 | 197 / 201 | 2.0 | 1.36 |
| fight-soldiers-150 (300) | 2.04 / 1.85 | 9.8 | 3.88 / 3.35 | 14 | 291 / 246 | 17 | |
| fight-soldiers-300 (600) | 3.31 / 2.90 | 13.4 | 8.03 / 5.93 | 30 | 416 / 364 | 13 | |
| fight-vehicles-50 (100) | 2.13 / 2.23 | 4.7 | 6.84 / 6.43 | 6.3 | 287 / 292 | 1.5 | 2.02 |
| fight-vehicles-150 (300) | 6.81 / 7.67 | 11.8 | 21.41 / 20.44 | 4.6 | 622 / 678 | 8.6 | |
| fight-creatures-50 (100) | 1.22 / 1.07 | 13.4 | 1.93 / 1.87 | 3.1 | 192 / 165 | 15 | 1.36 |
| fight-creatures-150 (300) | 1.75 / 1.46 | 18.3 | 3.02 / 3.05 | 0.8 | 265 / 205 | 26 | |
| structures-built-30 | 0.97 / 1.02 | 5.3 | 1.45 / 1.73 | 17 | 155 / 169 | 8.5 | **1.79** |
| mixed-lategame (600 objects) | 3.27 / 3.28 | 0.2 | 4.71 / 4.99 | 5.8 | 429 / 424 | 1.2 | 3.42 |

fps: 119.1-119.6 everywhere except idle-vehicles-300 (111), move-vehicles-300 (96), fight-vehicles-150 (94),
fight-soldiers-300 p1 (117.7). Those are the only scenarios that leave the cap.

### Run-to-run noise (what an A/B result has to beat)

* **Stationary scenarios (idle, move, mixed, structures): work avg spread 0-7 %** (median about 3 %); p95 spread 1-22 %.
  The idle-soldier scenarios and the empty server were 0.08-0.14 ms higher in p2 across the board, which looks like a
  small machine-state drift rather than scenario noise. Separate-pass comparisons should therefore only claim
  stationary-scenario effects above about **0.15 ms or 7 %**, whichever is larger.
* **Fights: 5-18 % on work avg, up to 30 % on p95**, because units die at different rates (alive at the end differs
  run to run). Separate-pass fight results need more than about 20 % to mean anything; use the invulnerable-unit flip
  scenarios (`scenarios_flip.json`) instead.
* main ms/s: spread below 2 % for 300+ units of a stationary scenario, but 9-21 % for light scenarios (empty,
  100 units) because the OS thread time includes a variable share of the cap wait. Use work for light scenarios.
* The paired flip inside one process (`ab_report.py flip`) avoids most of this; see `..\wave1\WAVE1.md` for its own
  null spread.

### Differences from 1.0.15 (same machine, same harness, same scenario definitions)

* **structures-built-30 (30 anchored solar panels): 1.79 -> 0.99 ms.** The permanent 0.7-0.8 ms/frame of A* navmesh-cut
  graph updates from functional panels in 1.0.15 is gone in 1.0.17. The panels now cost about what 30 idle objects cost.
* The per-unit prices are unchanged within noise: idle soldier about 0.40 ms per 100 per frame (1.0.15: 0.39),
  moving soldier 0.67 (0.71), idle vehicle 0.95 (0.86), moving vehicle 1.37 per 100 at 100 vehicles and 1.64 per 100 at 300
  (1.0.15: 1.28 up to 200). The vehicle cost is non-linear above 100-200 vehicles and is what leaves the cap first.
* Empty server 0.92 ms (1.00), mixed late-game 3.27 (3.42), fight-creatures-50 1.15 (1.36): all slightly lower.
* New in 1.0.17: an `Unit.UpdateFallThroughGround` GameManager section (4 ms per 10 s, negligible) and a static
  `AIBaseAgent.OrderedUpdateAllAgents` (PerfProbe's counter still attaches).
* Si_RTS_AI 0.94.13+ already budgets construction placement (maxStepsPerSecond 100, 2 concurrent searches), i.e. the
  1.0.15 report's M1 is in the AI mod now. Placement storms in AI rounds should therefore be smaller than in 1.0.15.

## AI round

The separate no-mod baseline round of the first brief was replaced (coordinator decision) by cycling arms inside
one round: arm A = Si_ServerPerf loaded with every item off (`"*": false`, only the startup-only Particles_Disable and
the exception counter stay on). The lab passes show that "loaded, all off" costs the same as no mod (WAVE1.md section 1).
Round #1 (`..\wave1\round1_arms3`, 2026-11-16 06:03-06:30, preset lab-3way = all factions under Si_RTS_AI,
GreatErg, 120 fps cap, PerfProbe targeted 10 s) gives the 1.0.17 curve below. **The aliens won at 25.5 min with a
peak of only 317 units**, so nothing above ~310 units exists for 1.0.17 yet. The 1.0.15 rounds on the live server
(full mod set) reached 600-760.

| units (50-unit bin) | intervals | normalised load ms/s (median) | fps (median) | frame p95 ms | 1.0.15 fps (DEV_REPORT section 1) |
|---:|---:|---:|---:|---:|---:|
| 0 | 5 | 213 | 117.7 | 10.4 | |
| 50 | 4 | 247 | 116.7 | 10.8 | 117 |
| 100 | 2 | 366 | 116.3 | 11.2 | 115 |
| 150 | 1 | 432 | 116.9 | 11.7 | 114 |
| 200 | 2 | 507 | 116.1 | 12.3 | 114 |
| 250 | 10 | 632 | 114.8 | 12.8 | 109 |
| 300 | 2 | 736 | 109.8 | 13.8 | 97 |

Fit over the arm-A intervals: load = 136 + 1.50U + 0.00105U^2 ms/s (R2 0.98, n 24). Normalised load = per-frame cost
x 120 + per-fixed-step cost x 25 (REPORT.md section 1.2), 1000 = the 120 fps cap is just held. Section shares at about
280 units (arm A, n 10): main-thread busy 619 ms/s, of which Update scripts 368, late-update scripts 100, PhysX 72,
animators 31, fog 30, AI commanders 18. The fps column runs slightly higher than 1.0.15 at 250-300 units, but these
are different rounds (the 1.0.15 rounds had more structures and the full live mod set); compare like with like only
inside one round.