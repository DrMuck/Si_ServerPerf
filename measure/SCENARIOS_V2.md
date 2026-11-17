# Si_PerfLab 0.2.0 scenarios (for the rig)

DLL: `C:\Users\schwe\Projects\Silica_Performance\Si_PerfLab\bin\Release\netstandard2.1\Si_PerfLab.dll` (0.2.0, built
against 1.0.17, refcheck clean). Copy it to `C:\tmp\silica_ds_lab\_modsets\perflab\` yourself; nothing was deployed.
The 0.1.1 files (`scenarios_ab.json`, `scenarios_flip*.json`, `scenarios_battle.json`, ...) run unchanged. They load
through the new loader without errors, spawn exactly as before, and their CSVs only gain columns at the end.

All files: GreatErg (the lab preset), `auto_run`, `stop_server_when_done`, `exit_on_config_error`. Run them like
any scenario file, e.g.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File run_lab.ps1 -Mode lab -Label quick_1 -OutRoot v2 `
  -Scenarios scenarios_quickcheck.json -ServerPerfDll <dll> -RuntimeJson runtime_all_default.json `
  -FlipStates runtime_all_off.json,runtime_bat_B.json,runtime_bat_C.json -FlipEveryS 30 -TimeoutMin 40
python ramp_report.py v2\quick_1 --settle 5 --md v2\quick_1\RAMP.md --csv v2\quick_1\ramp
```

`run_lab.ps1` defaults to a 60 min timeout in lab mode. Pass `-TimeoutMin` as given in the table.

## The files

| file | scenario(s) | what it does | duration (excl. ~2 min boot) | held constant | -TimeoutMin |
|---|---|---|---|---|---|
| `scenarios_quickcheck.json` | `quick-trickle` | **The quick check.** Each team spawns 250 m from the map centre on its own side and attack-moves into a 3-way fight at the centre. Starts at 100 units, then +5 per team every 20 s, up to 1000, until the 20 s median fps < 10. Damage on, deaths respawned 2 s later 250 m back (sustain). Base 0.04 structures per unit, built completely before measuring. | settle 0.5 min + climb (800 units after 15.7 min, 1000 after 20 min, or earlier at the fps floor) + 0.5 min final hold, about 17-21 min | composition, roles (100 % front), the growth rate. Unit count grows on purpose: fit load against U per arm | 40 |
| `scenarios_v2_ramp_god.json` | `ramp-god` (rows `ramp-god@200` ... `@1000`) | Plateaus 200/300/400/500/600/700/800/1000 lab units, each held 270 s after a 30 s settle. Climbs by ADDING units. 20 % idle in base lots, 15 % in transit (base to front and back), 65 % fighting on 4 fronts (the battle_b9 cluster centres K1-K4, 366-479 m apart). Base of real structures (0.12 per unit, game placement rules). God mode: `DamageCheatMultiplier = 0` as in 0.1.1. | 8 x 305 s, about 41 min (less if fps < 10 ends it early) | per plateau: unit count (no deaths), composition, roles, structures | 60 |
| `scenarios_v2_ramp_sustain.json` | `ramp-sustain` (rows `ramp-sustain@...`) | The same with damage ON. Every dead unit is respawned 2 s later in its spawn zone (front units 90 m behind their front, walking back in), destroyed structures 20 s later at their slot. Exercises damage, deaths, wrecks, disposal and target churn. | about 41 min | per plateau: the count within about deaths/s x 2 s (simulated: 0.5 %/s deaths gives about 1 % dip, 2 %/s gives about 4 %), composition, roles | 60 |
| `scenarios_v2_parked.json` | `parked-spaced-flat`, `parked-spaced-slope`, `parked-tight-WORSTCASE` | 300 Sol vehicles near the Sol base, idle. Spaced: 4 lots x 75, 5 mixed prefabs per lot, rows of 10, capsule circles >= 4 m apart, 6 m aisles, headings +/-35 deg, on flat ground (steepest sample < 4 deg) or a slope (mean 6-16 deg). WORSTCASE: the 0.1.1 tight grid (= `phys-idle-vehicles-300`). | 3 x (20 s settle + 540 s measure + cleanup), about 30 min | count, prefab mix, idle state | 45 |
| `scenarios_v2_order_probe.json` | `probe-idle-mixed` | Idle at the 3 bases: 100 Sol + 60 Cent parked vehicles (spaced, flat), 50 Riflemen, 50 Troopers, 50 Crabs, 20 Hunters. 60 s measured idle, then 4 move orders (30 m out, back, out, back). First order 150 s after settle start, 90 s idle between, 30 s timeout. | about 9 min | everything idle before each order | 20 |

## Log markers (Latest.log, prefix `[LAB]`)

```
RAMP SETUP scenario=<n> climb=steps|trickle mode=god|sustain|attrition respawn=<b> teams=... plateaus=...|start=... expected_min=...
RAMP TEAM <team> hq=(x,z) base_lot=(x,z) transit_from=(x,z) fronts=<i,...>
RAMP FRONT <i> center=(x,z) teams=... staging=<team>(x,z) ...
RAMP CLIMB scenario=<n> [plateau=<k>] target_units=<N> target_structures=<M> adding units=<a> structures=<s> alive units=<u> iso=<t>
RAMP CLIMB DONE scenario=<n> [plateau=<k>] target_units=<N> lab_units=<u> lab_structures=<s> failed=<f> base game/lab/failed=<g>/<l>/<x> world_units=<w> spawn_ms_total=<ms>
PLATEAU BEGIN scenario=<n> plateau=<k> name=<n>@<N> target_units=<N> target_structures=<M> lab_units=<u> lab_structures=<s> world_units=<w> mode=<m> iso=<t> sig=<crc>
PLATEAU END scenario=<n> plateau=<k> name=<n>@<N> target_units=<N> lab_units_avg=<a> min=<x> max=<y> dev_max_pct=<p> fps=<f> work_avg_ms=<w> work_p95_ms=<w95> deaths=<d> deaths_per_s=<r> respawns_per_s=<r> status=ok|fps_abort [Sol:shots=..,att=..,hits=..,kills=.. ...] iso=<t>
TRICKLE BEGIN scenario=<n> start_units=<N> lab_units=<u> +<a>/team every <s> s up to <max> mode=<m> iso=<t> sig=<crc>
TRICKLE STOP scenario=<n> reason=fps_floor (median ..)|max_units|time_cap target_units=<N> lab_units=<u> world_units=<w> iso=<t> final_hold_s=<s>
RAMP END scenario=<n> climb=steps|trickle reason=last_plateau|fps_floor (..)|fps_abort|max_units|... ...
LOAD scenario=<n> phase=Settle|Measure [plateau=<k>] target_units=<N> lab_units=<u> lab_structures=<s> world_units=<w> world_structures=<ws> fps=<f> work_avg_ms=<w> deaths=<cum> respawns=<cum> pending_respawns=<p> sig=<crc> iso=<t>   (every 10 s)
FRONT STALL scenario=<n> front=<i> no lab hits for 30 s: <k> units re-ordered at the enemy centroid (stall #<j>)
parking <scenario>: <planned>/<n> slots, lot (x, z) W x D m, terrain flat|slope|any mean <a> max <b> deg, capsule radii <prefab>=<r>/..., min capsule clearance <c> m, ...
PROBE ORDER scenario=<n> cycle=<k> units=<u> order_ok=<a> order_failed=<b> iso=<t> sig=<crc>
PROBE RESULT scenario=<n> cycle=<k> team=<T> kind=<kind> n=<u> order_failed=<f> first_move n=.. ok=.. never=.. p50=.. p95=.. max=.. | reach n=.. ok=.. never=.. p50=.. p95=.. max=..
FATAL <reason> -> stopping the server (exit code 3)
```

Steps mode cuts by plateau: every plateau is its own `scenarios.csv` row (`<name>@<target>`). Its 1 Hz samples carry
the same scenario name with phase `Measure`, so `ab_report.py flip`, `battle_report.py` and the new `ramp_report.py`
all cut by plateau without changes. The climbs between plateaus are phase `Spawning`/`Settle` and are excluded.
Trickle mode writes one row for the whole climb. Use the samples (1 Hz) or the LOAD lines to fit load against unit
count.

## Output columns

`scenarios.csv` keeps its 0.1.1 columns and appends: `scenario_type` (groups|ramp), `mode`, `plateau` (-1 =
trickle), `target_units`, `target_structures`, `lab_units_avg/min/max` (1 Hz, Measure), `count_dev_mean_pct`,
`count_dev_max_pct` (|alive - target| / target), `lab_structures_alive`, `deaths`, `deaths_per_s`, `respawns`,
`respawns_per_s` (this window; units + structures), `respawn_failed`, `growth_spawns` (cumulative), `shots`,
`creature_attacks`, `hits`, `kills` (this window, all teams, when engagement counters are on),
`units_with_target_avg`, `front_stalls`, `base_game_placed`, `base_lab_placed`, `base_failed`, `runtime_sig`. For a
ramp row, `sweep_n` = plateau target, `behaviour` = `ramp_<mode>`, `objects_planned` = target units + structures,
and `spawn_failed` / `spawn_ms` are cumulative over the ramp.

`samples.csv` (1 Hz) appends: `lab_units`, `lab_structures`, `target_units`, `plateau`, and per second `deaths`,
`respawns`, `shots`, `hits`, `kills`, plus `with_target` (lab units with a current target) and `runtime_sig`.
`runtime_sig` = CRC32 (hex) of `UserData\cfg\Si_ServerPerf\runtime.json` as the lab last saw it ("none" = no file).
It identifies the arm without `flips.csv`. `ramp_report.py` maps it to the `cfg_*.json` copies in the run folder.

`engagement.csv` (one row per measure window x team x kind; kinds infantry / light / heavy / air / creature /
turret / structure; `nonlab` = objects the lab did not spawn): `units_avg`, `with_target_avg`,
`with_target_share`, `shots` (projectiles initialised), `creature_attacks` (`CreatureDecapod.Attack` starts; melee
fires no projectiles), `hits_dealt`, `hits_taken`, `kills`, `deaths`, `shots_per_unit_s`. Engagement counters are
on in the quick check and the ramp files and off in parked / probe. They cost one Harmony postfix per projectile, one
prefix per creature attack, two GameEvents handlers and a 1 Hz pass over the lab units. That cost is the same in
every arm.

`order_response.csv` (per unit and cycle): `cycle, order_iso, order_rt, team, prefab, kind, unit, order_ok,
first_move_s, reach_s, frames_to_first_move, max_dist_m, ...`. "First move" = speed over a >= 0.1 s window above
0.5 m/s: the detection time, at most 0.1 s + one frame after the start. "Reach" = 2 m from where it stood. Empty =
never within the 30 s timeout. `order_response_summary.csv` (per cycle x team x kind and x prefab): `n,
order_failed, started, reached, never_started, never_reached, first_move_p50/p95/max_s, reach_p50/p95/max_s`.
Percentiles are nearest rank, `a[(int)(p*(n-1))]`, as in FrameClock. Map each cycle to the arm through `order_iso`
and `flips.csv` (or `runtime_sig`).

## Parameters (scenario keys)

Ramp (`"type": "ramp"`): `mode` god|sustain|attrition (attrition = damage on, no respawn); `climb` steps|trickle;
`teams`, `team_weights`; steps: `plateaus`, `settle_s`, `hold_s`; trickle: `trickle {start_units, add_per_team,
every_s, max_units, final_hold_s}`; `fps_floor` (steps: stop after a plateau whose fps is below it; a plateau ends
early with `fps_abort` if the `fps_window_s` median falls below half the floor; trickle: stop climbing when the
`fps_window_s` median is below it); `roles {base, transit, front}`; `fronts` auto|center|[{center:[x,z],
teams:[..]}] (auto = one front per team pair at the HQ midpoint pulled `front_pull` toward the HQ centroid, plus
one 3-way front at the centroid); `front_radius_m`, `front_spawn_back_m`, `front_reorder_s`, `stall_s`;
`base_offset_m`, `transit_from_m`; `composition` lategame|{team: [[prefab, weight], ...]}; `base`
lategame|none|{team: [[prefab, max], ...]} (added round-robin in list order); `structure_ratio`; `base_placement`
game|game_then_lab|lab; `respawn`, `respawn_delay_s`, `respawn_structures`; `log_every_s`; top-level
`engagement_counters` (or per-scenario `engagement`).

Parking (spawn group `"layout": "parking"`): `prefabs` (mixed list, cycled), `count`, `per_row`, `capsule_gap_m`,
`aisle_gap_m`, `heading_jitter_deg`, `terrain` any|flat|slope, `flat_max_deg`, `slope_min_deg`, `slope_max_deg`,
`search_radius_m`, plus the usual `team` / `center` / `offset_m`.

Order probe (scenario key `order_probe`): `after_idle_s, idle_s, cycles, move_m, speed_threshold_mps,
move_threshold_m, timeout_s`. It runs after the measure window, before cleanup, and the samples show phase `Probe`.

## Fail fast

The file is validated at load and every prefab and team name again at the first round start. A malformed file
stops the run at once, before anything spawns: not JSON, a non-object top level, `"scenarios"` missing or not an
array (the PowerShell `{"value": [...]}` case gets its own hint), 0 enabled scenarios, an unknown scenario or group
type, layout, behaviour, mode or climb, a wrong value type, duplicate names, non-increasing plateaus, or an unknown
prefab / team. The stop is one `[LAB] FATAL ...` line plus a new `UserData\PerfLab\<run>\FAILED` file (reason; the
scenarios.json copy beside it), then `Application.Quit(3)`, and `Environment.Exit(3)` if the process is still alive
15 s later. `run_lab.ps1` then ends with `status=exited` within about a minute, and `perflab\<run>\FAILED` is in the
archive, so check for that file. Unknown keys are only warnings (keys starting with `_` are comments). Twenty
`Lab.Tick` exceptions within 10 s stop the lab the same way (exit only with `auto_run` + `stop_server_when_done`).
The 0.1.1 bug that idled 35 min with an exception every tick (`Run` refused, the state machine went on) is fixed.

## First run: look at these first

1. `RAMP TEAM` / `RAMP FRONT` lines and `RAMP CLIMB DONE ... failed=`. The quick check's staging points
   (about (-37,-411), (-457,-514), (-469,-181)) and the base lots are computed from the HQs, not tested on the map.
   A high `failed` count, or `spawn_failed` in the rows, means a zone is on water, a cliff or occupied.
2. `base game/lab/failed`: how many structures the game's own placement search
   (`ConstructionPlacement.GetFirstValidPlacementAroundPoint`, with anchor range, fog of war, slope, obstruction
   and access points) placed, versus the lab's ring fallback. Many `lab` placements mean the game search failed
   (fog of war around the HQ, for example). Those structures may be unlinked (no anchor) and decay 1 %/5 s.
3. The sustain ramp's `count_dev_max_pct` and `deaths_per_s` per plateau, and `pending_respawns` in the LOAD lines.
   If the dev exceeds about 5 %, deaths/s x 2 s is larger than expected, or respawns fail (`respawn_failed`).
4. `engagement.csv` / ramp_report's **Not fighting** line, especially Alien creature, and `FRONT STALL` lines
   (each one re-targets the units of a front).
5. Parking: the `parking ...` lines (lot found? slope actually in band? `min capsule clearance` >= 4 m).
   `no slope lot` in notes = no terrain in the band within `search_radius_m`. Widen the band or the radius.
6. Probe: `order_failed` > 0, or `never_started` > 0 in arm A, means a problem with the probe itself, not with the
   arm.

## Known limits

* Spawns use `Game.SpawnPrefab` (finished objects): no unit caps, no production, no construction. Structures
  appear finished. `max_spawned` (1500) caps plateaus + base capacity (the lategame base is 31/31/32).
* No spawn-rate limit in the game. `spawn_per_frame` (30) and at most one structure placement search per frame are
  the lab's own limits (the search can take tens of ms; it runs only in unmeasured Spawning phases).
* Pooling (1.0.17 `PrefabPool`): units / structures / sites keep 2 ready instances per prefab, refilled by async
  `InstantiateAsync` after each take. Destroyed objects are NOT recycled. A burst beyond 2 of one prefab is a plain
  synchronous `Object.Instantiate`, as in a real round when several units of one type pop at once.
* Placement: human structures must lie within `MaximumBaseStructureDistance` of a construction anchor (the HQ, or
  any finished structure whose ObjectInfo has `StructureConstructionAnchor`, default true); alien structures link through a chain of
  anchors to the Nest. The game search enforces this; the lab fallback only keeps them near the HQ.
* Fronts are fixed points (GreatErg). On another map use `"fronts": "auto"` or new coordinates.
* The avoidance capsule is centred on its collider transform; the packing assumes it sits at the unit's origin.
