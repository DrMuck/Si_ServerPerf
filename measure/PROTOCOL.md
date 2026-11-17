# Si_ServerPerf measurement protocol (lab server, Silica 1.0.17)

Everything runs on the lab copy `C:\tmp\silica_ds_lab` (port 27911/27910, server password set in the lab copy's own config, not recorded here; headless, no
client), started through its own `launch.bat`. Never the live install, never the staging copy, never the watchdog.

## Fixed setup (do not change between baseline and treatment)

| item | value |
|---|---|
| game | Release:1.0.17, `SilicaCore.dll` md5 `16A1B0C5A0F007A43D36570248E1CAEA` (mirrored from the live install 2026-11-16) |
| base mod set | `C:\tmp\silica_ds_lab\_modsets\base\`: Si_AdminMod 2.14.01, Si_RTS_AI 0.94.14 (1.0.17 build from live `ModsToUpdate`), Si_ServerFpsCap 1.0.0 (MaxFPS 120), Si_ServerOptimizer 0.7.1, Si_PerfProbe 0.1.4 (rebuilt vs 1.0.17) |
| lab-only | `_modsets\perflab\Si_PerfLab.dll` 0.1.0 (rebuilt vs 1.0.17); NOT loaded in the AI round (it disables the AI commanders) |
| PerfProbe config | `probe_lab.json` (targeted, deep off, 10 s intervals) for lab passes, `probe_round.json` (same probes, 20 s) for the round; `runTag` = run label |
| lab scenarios | `scenarios_ab.json` (21 scenarios, ~37 min incl. boot) |
| round | Si_RTS_AI preset `lab-3way` (= live `headless-all-factions` with map GreatErg, telemetry port 0, planner log off), first round only, stopped when the round ends or after 61 min of round time |

`run_lab.ps1` enforces the mod set on every run: it deletes every DLL in `Mods\` that is not base (+ PerfLab in lab
mode) (+ the given Si_ServerPerf DLL), so a baseline run can never accidentally carry the mod.

## One command per set

From `C:\Users\schwe\Projects\Si_ServerPerf\measure`:

```powershell
# treatment: 2 lab passes + the AI round with Si_ServerPerf, diffed against the baseline (~2 h 20 min)
powershell -NoProfile -ExecutionPolicy Bypass -File run_set.ps1 -Prefix perf -OutRoot treat_all -Passes 2 -Round `
  -ServerPerfDll C:\Users\schwe\Projects\Si_ServerPerf\bin\Release\netstandard2.1\Si_ServerPerf.dll `
  -RuntimeJson <runtime.json with the switches for this set> -CompareTo baseline_1017

# lab passes only (~75 min), no round
powershell -NoProfile -ExecutionPolicy Bypass -File run_set.ps1 -Prefix perf -OutRoot treat_x -Passes 2 -ServerPerfDll <dll> -CompareTo baseline_1017

# the baseline itself (what produced baseline_1017)
powershell -NoProfile -ExecutionPolicy Bypass -File run_set.ps1 -Prefix base -OutRoot baseline_1017 -Passes 2 -Round
```

Outputs per set folder: one sub-folder per run (`manifest.json` with mod hashes/configs/status, `Latest.log`,
`perflab\<run>\`, `perfprobe\<run>\`, `cfg_*` copies), `LAB.md`, `ROUND.md`, machine-readable
`<prefix>_lab_runs.csv` / `_lab_summary.csv` / `_lab_sections.csv` / `_round_bins.csv` / `_round_levels.csv` /
`_round_intervals.csv`, and with `-CompareTo`: `LAB_vs_baseline_1017.md`, `ROUND_vs_baseline_1017.md`, `vs_*.csv`.

Single runs: `run_lab.ps1 -Mode lab|round -Label <unique> [-ServerPerfDll ...] [-RuntimeJson ...] [-OutRoot ...]`.
Reports by hand: `python ab_report.py lab --a <base runs> --b <treat runs>`, `python ab_report.py round --a <base round> --b <treat round>`.

## Flipping runtime switches inside one server (interleaved A/B)

Si_ServerPerf polls `UserData\cfg\Si_ServerPerf\runtime.json`. `run_lab.ps1` can alternate two versions of it every
N seconds while a pass runs and logs each flip (`flips.csv`, ms timestamps); the file is replaced atomically
(write `.tmp`, move). Use the stationary long-window scenario file:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File run_lab.ps1 -Mode lab -Label flip_<item> -OutRoot flips `
  -Scenarios scenarios_flip.json -ServerPerfDll <dll> -FlipA runtime_<item>_off.json -FlipB runtime_<item>_on.json -FlipEveryS 20
python ab_report.py flip --run flips\flip_<item> --settle 5
```

The flip report uses the Si_PerfLab 1 Hz samples (`samples.csv`, frame work per second) in Measure phases,
drops `--settle` seconds after every flip (set it >= the mod's poll interval + 1 s), averages per flip block,
and gives per scenario A mean, B mean and the paired difference of adjacent blocks with its standard error.
Interleaving cancels slow drift, so this is the most sensitive test for a single switch. The same command
without `-ServerPerfDll` and with `runtime_null_a.json` / `runtime_null_b.json` is the A/A null test (expected
difference 0; its s.e. is the floor of the method).

## How to read an A/B result

* Primary metric: Si_PerfLab `work_avg_ms` (frame CPU work without the 120 fps cap wait) per scenario.
  Secondary: `work_p95_ms`, PerfProbe main-thread CPU ms per wall-second (OS thread time, includes some cap-wait
  overhead; empty server ~140 ms/s).
* Noise: `A spread` = max - min over the baseline passes (BASELINE.md table). Claim an effect only when
  `abs(delta)/noise >= 2` in `LAB_vs_*.md`, i.e. the change is at least twice the run-to-run spread, and the sign
  agrees across scenarios of the same family. Fight scenarios are noisier (units die at different rates).
* Round: compare bins with n >= 5 on both sides; a single AI round is not repeatable (different build orders,
  placement storms), so round deltas below ~10 % at a given bin are not evidence on their own.
* Before every run `run_lab.ps1` refuses to start if any other `Silica.exe` runs (live server or client would
  steal CPU); `-AllowOthers` overrides, but such a run is not comparable.

## Additions during wave 1 (2026-11-16)

* `run_lab.ps1` options added: `-FlipStates a.json,b.json,c.json` (N-state cycle; `-FlipLabels` names them, default
  A, B, C ...), `-RuntimeJson` together with flips = startup file, with state A following after 30 s (so startup-only
  items stay as at startup), `-ServerPerfConfig` (copied to `Si_ServerPerf.json`; empty = the mod writes defaults),
  `-ProbeConfig`, `-RtsPreset` (`lab`, `lab-3way`, `lab-vanilla3way`). Every flip is confirmed through
  `runtime_state.json` (lag and items-on count go into `flips.csv`). A runtime.json left over from an earlier run is
  always deleted first.
* Runtime files with `"dump": true` make Si_ServerPerf write a status block per block (b2+: "window n ENDED", written
  before the switch). `python ab_report.py windows --run <run>` turns those blocks into per-arm item counters and
  exception counts (`WINDOWS.md`, `*_windows.csv`).
* AI round with arms: `wave1_round.ps1 -Label <x> [-Dll ...] [-ServerPerfConfig ...] [-StateB ...] [-Third ...]
  [-RtsPreset lab-vanilla3way]`. Analysis: `python ab_report.py round --a <run> --settle 3` (per-arm 50-unit bins of
  normalised load, quadratic fit per arm, section shares at given levels).
* Big battles: `python make_battle_scenarios.py 300,450,600,800 390` -> `scenarios_battle.json`; run with
  `run_lab.ps1 -Scenarios scenarios_battle.json -RuntimeJson runtime_all_default.json -FlipStates
  runtime_all_off.json,runtime_bat_B.json,runtime_bat_C.json -FlipEveryS 30`; analyse with `python battle_report.py
  <run>`. Needs Si_PerfLab 0.1.1: scenarios with invulnerable groups disable all damage
  (`DamageManager.DamageCheatMultiplier = 0`) for their duration.
* Physics arms: `python physics_report.py <run> --settle 30` (asleep share from the census, wheeledsleep / idlesleep /
  avoid* / parkeddrift lines and PhysX ms/s per arm).
* Gameplay comparison of AI rounds: `python round_compare.py label=<RTSA round log> ...`.