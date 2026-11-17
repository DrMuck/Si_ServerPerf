# Smoke b21 (Si_ServerPerf 0.2.0), 2026-11-17

DLL `release\b21\Si_ServerPerf.dll`, md5 `CCF56DEE59B75CA980C10EFFEB5A7AA7`. Game 1.0.17 (SilicaCore md5
`16A1B0C5A0F007A43D36570248E1CAEA` in live, lab and stage). A foreign Silica **client** (PID 27736,
`C:\SteamLibrary\steamapps\common\Silica\Silica.exe`) ran the whole time; not touched, no port overlap
(27910/27911, 26910/26911, 8775 were free), lab run therefore started with `-AllowOthers` (numbers not comparable,
not needed for a smoke).

## Step 1 - lab, fresh config (PASS)

Command (from `measure\`):
`run_lab.ps1 -Mode lab -Label step1_lab_b21 -OutRoot smoke_b21 -Scenarios smoke_b21\scenario_smoke_melee450.json -ServerPerfDll release\b21\Si_ServerPerf.dll -TimeoutMin 12 -AllowOthers`

- Scenario `scenario_smoke_melee450.json` = night1 `melee_check450.json` (3 teams, centre front, damage on, respawn
  2 s) with `hold_s` 180. Plateau 450 target, 409-441 lab units (avg 426), 2041 deaths, fps 22-60 (plateau avg 29.4).
  Run 13:19:37-13:25:34, server quit by itself (status `exited`).
- Lab config backup (b20-era files, not restored): `smoke_b21\lab_backup\` (`Si_ServerPerf.json` md5
  613d167b..., `runtime.json`, `runtime_state.json`, plus the b20 DLL that was in lab `Mods`). `run_lab.ps1` deleted
  the JSON before the start, so the mod wrote a fresh one (`step1_lab_b21\end_Si_ServerPerf.json`, md5 bac5c164...;
  same file now in the lab).
- Lab `Mods\Si_ServerPerf.dll` is now b21 (md5 CCF56DEE...). `_modsets\` has no Si_ServerPerf entry (run_lab adds the
  DLL via `-ServerPerfDll`), so nothing to change there.

Observed in `step1_lab_b21\Latest.log`:
- `==== Si_ServerPerf 0.2.0 startup ====`; 44 items ON (Headless 14, Physics 4, Services 8, UpdateTiers 6, Targeting
  12, MultiCore 0); no FAILED / NOT APPLIED / partial; only SKIPPED: `Headless.LightGrid_Skip` (Si_ServerOptimizer
  0.7.1 already patches LightGrid.Update).
- New defaults ON and patched: `Targeting.CollectEnemies_Snapshot` 1/1, `_SnapshotScore` 1/1, `_FastAdd` 1/1,
  `Physics.DeadAgentHalt` 2/2, `Physics.DeadDisposeEarly` 1/1.
- Written JSON: those five `true`, `DisposeEarlySeconds` 5, `DisposeEarlyFpsLow` 0, `DisposeEarlyFpsHigh` 0,
  `DeadAgentHaltClearAim` false, `SelfCheckFraction` 0.
- `[Physics] deaddispose` target 15.00 s at GreatErg load (13:20:09) -> 12.14 -> 7.06 -> 5.00 s at 13:20:47 and 5.00 s
  in every line after that (`low=0 high=0`, wanted 5.00 s). The slide took ~38 s wall instead of ~20 s because it
  ran during the scene load at 0.2-0.7 fps (slew uses min(dt, 0.5 s) per frame); expected behaviour.
- Dead units really go after ~5 s (inferred, Little's law): in the 200 s plateau `trackedNow` (dead units currently
  tracked) was 29-61 at a death rate of 9.4-12.5/s (from the PerfLab `deaths=` counter) = mean residence 2.4-6.5 s,
  ~4.5 s on average. With vanilla 15 s the tracked population would be ~150. No per-unit timestamp was checked.
- `DeadAgentHalt` active: `deadagents haltedNow=60 ... halted=99 frozenAim=93` etc.
- `[Diag]` lines: 0 logged exceptions/warnings at every scene load and at quit (`GreatErg (quit): 0 logged
  exceptions/warnings, 0 distinct`). No line with `Exception` in the whole log.
- Other warnings (6), none from Si_ServerPerf: Admin Mod no admins.json, RTS_AI QUEEN no Nest, PerfProbe
  `AIPathfinding` probe type not found (+ the AccessTools line), RTS_AI harness `FakeTeamJoin: giving up` +
  `AutoStart ... retry 1/6` (recovered: fake join fired at t+17 s; seen in 1 of 54 night1 logs as well).

## Step 2 - staging, full mod set + b21 (PASS)

Stage state before: 49 DLLs in `Mods` (no Si_ServerPerf; note: `Si_EloMatchmaking.dll` differs from live, it is
the local 0.8.2 test build), no `UserData\cfg\Si_ServerPerf`, 280 files in `UserData`. Hash lists:
`stage_state\before_*.sha256`. Full copy of `Mods`, `UserData` and the three `Silica_Data\*Settings.xml`:
`C:\tmp\silica_stage_backup_smoke_b21\` (moved out of the project so no server config lands in git).

Command (pipeline smoke runner, from `Projects\Si_UpdatePipeline`), Mods/UserData from the backed-up stage state so
the set is exactly the stage's, game files not re-mirrored (SilicaCore identical to live):
`python smoke\smoke_test.py --mods <backup>\Mods --overlay <scratch>\overlay_b21 --userdata <scratch>\ud_src --no-mirror --baseline smoke_b21\stage_reference --out smoke_b21\step2_stage_b21 --soak 600 --timeout 1100`
- `ud_src` = stage UserData + `cfg\Si_ServerPerf\Si_ServerPerf.json` = `stage_initial_Si_ServerPerf.json`
  (`Targeting.Settings.SelfCheckFraction` 0.05, `Services.Settings.VerifyAgainstVanilla` true; all other keys
  missing, so the mod filled in the 0.2.0 defaults; one start sufficed).
- Reference = the previous stage run with the same 49 DLLs (2026-11-16 19:46, smoke-harness round, 10 min):
  `stage_reference\` (ML log from the stage's `MelonLoader\Logs`, game log from `Documents\Silica`).
- Run: launch 13:25:44 (Silica.exe directly, as the runner does; no watchdog), 50 mods loaded +12 s, ready +54 s,
  round (vanilla AI commanders on all 3 teams, IndustrialQuarter, layout 2v1, HUMANS_VS_HUMANS_VS_ALIENS) started
  13:27:14, soak 600 s, stopped 13:37:14 by PID (TerminateProcess). Runner verdict **PASS** (exit 0).
- Status dumps forced with `runtime.json` `{"seq":n,"items":{},"dump":true}` at round +4.7 min and +9.5 min
  (`dump_window0.txt`, `dump_window1.txt`; "no change" to switches).

Observed:
- Startup report (`startup_report.txt`): identical to the lab - 44 ON, 0 FAILED / NOT APPLIED / partial, only SKIPPED
  `Headless.LightGrid_Skip` (yields to Si_ServerOptimizer 0.7.1, the only item that yields). Note line: "self-check
  ON: 5 % of calls also run vanilla and compare".
- Self-check / verify counters at round +9.5 min (cumulative, all **0 mismatches**; 0 `SELF-CHECK MISMATCH` lines):
  | item | checks |
  |---|---:|
  | Targeting.CollectEnemies_Snapshot / _SnapshotScore / _FastAdd | 8186 each (5.6 % of 451 q/s) |
  | Targeting.Rank_Sort | 1899 |
  | Targeting.UrgentClose_Grid | 1 |
  | Targeting.CollectEnemies_Grid, NearestTarget, AlarmProximity, FriendlyFire_Grid, FriendlyFire_StaleGuard | 0 (calls took the bypass path: short lists, light fighting) |
  | Services.Detected_IndexLookups (VERIFY) | 43126 |
  | Services.TerrainMaterial_Cache (VERIFY) | 17723 |
  | Services.CommanderAI_ClusterRebuild (VERIFY) | 567 |
  | UpdateTiers.Register_O1 (verified) | 114 |
- `Physics.DeadDisposeEarly`: target 15 -> 5.00 s by 13:27:40, 5.00 s afterwards; only 43 dead units shortened in
  the round (light fighting, ~180 units). `DeadAgentHalt` max halted 7.
- Exceptions: 0 in the ML log, game log and Unity player log. `[Diag]` 0 logged exceptions/warnings in every period.
- Mentions Si_ServerPerf or a method it patches (message or stack): 0 warnings/errors (`classified_errors.txt`; the
  only hits are its own `[Diag] 0 logged exceptions` status lines).
- Others: 31 ML warnings in 19 kinds, all of kinds present in the reference except harmless variants (NetPulse
  STALLs only during map setup, 6 vs 14 in the reference; PerfProbe "overhead > 15 %: dropped methods" with a
  different method list; one NetPulse RELAY Attempting). Missing vs reference: MapBalance BAD BASE lines (this round
  drew layout 2v1, "all 3 bases clear") and the RTS_AI AutoStart retry. Game log: only suppressed vanilla noise
  (278 GetGroundSlopeClearJobified, 3 GameInput, 2 Sell action), same as reference (120 in the reference's shorter
  log span).
- No Harmony conflict / "already patched" / transpiler Problem lines (only PerfProbe's own "already patched 0").
- No runtime flip was needed (nothing looked wrong).

Vanilla methods patched by Si_ServerPerf and another loaded mod (static: SiCecil `modinfo` [HarmonyPatch] attributes +
reflective lookups + `Patch_<Type>_<Method>` class names of the 49 DLLs, against `Core|Modules\*\targets.txt`;
`static_patch_overlap.txt`):
- `LightGrid.Update` - Si_ServerOptimizer; Si_ServerPerf yields (SKIPPED).
- `AICommander.Think` - Si_RTS_AI (`Patch_AICommander_Think`, `Patch_Think_Window`) and Services.CommanderAI_*;
  ran together (ThinkStats counted the thinks, ClusterRebuild VERIFY 567/0). No owner probe resolves with this
  Si_RTS_AI build, same as in every lab log, so `SkipOwnedTeams` skips nothing.
- `MusicJukeboxHandler.OnGameStarted` / `OnGameEnded` (round hooks, postfixes) - Admin Mod, BasicTeamBalance,
  EloMatchmaking, Leaderboard, Logging, MapBalance, Mapcycle, ReplayLogging, ServerControl, SurrenderCommand.
- Not a shared patch but an indirect interaction (inferred): Si_UnitBalance prefixes `HumanStructure.UpdateHealDecay`
  / `AlienStructure.UpdateHealDecay`, which vanilla calls from `OrderedUpdate`, which `UpdateTiers.Tier_Structures`
  runs at a reduced rate; the method only syncs the AutoHeal/Decay enable flags, so the effect is a delay of a few
  frames. `NetworkGameServer.GetServerPlayer` is only looked up by Si_UnitBalance for invoking, not patched.
- Limits: patches through `TargetMethod()`, computed names or manual `Harmony.Patch` with runtime-built lookups can
  be missed; no runtime `Harmony.GetAllPatchedMethods` dump was taken.

Restore (13:37): `robocopy /MIR` of the backed-up `Mods` and `UserData`, the three Settings XMLs copied back, this
run's ML log removed from the stage `MelonLoader\Logs` and `Latest.log` put back to the 2026-11-16 copy (this run's
log is in `step2_stage_b21\`). Verified: `stage_state\after_*.sha256` byte-identical to `before_*` (Mods 49 files,
UserData 280 files, 3 XMLs) and identical directory trees; no `Si_ServerPerf.dll`, no `UserData\cfg\Si_ServerPerf`.
Not restored: the game's shared `Documents\Silica\Server_2026_11_17__13_25.log` (copied here as `game_server.log`).

Processes at the end: only the foreign client (PID 27736 + its crash handler 6648); none of ours.

## Verdict

Step 1 PASS, step 2 PASS. Nothing seen on the server side argues against a first supervised session on the owner's
server with a player connected. Untested: anything a client sees, which is what that session is for. Specifically the
5 s wreck removal (visible), and the README's own "Never run with a client connected" line, which is still in place.
Thin in this smoke: the grid-path self-checks (0 samples, light fighting in the AI round; the lab night-1 runs cover
them) and the dead-unit 5 s time on stage (43 units).
