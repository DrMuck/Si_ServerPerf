# Smoke b21 Si_ServerPerf on stage (49+1 mods)

**Verdict: PASS** (exit code 0)


## Run

- Stage: `C:\tmp\silica_ds_stage`; started 2026-11-17T13:25:44; PID 23156; ports game 26911 / query 26910 / RTSAI telemetry 8775
- Live Silica.exe running at start: no; stop: stopped (TerminateProcess)
- Mod set: C:\Users\schwe\Projects\Si_ServerPerf\measure\smoke_b21\stage_backup\Mods + overlay C:\Users\schwe\AppData\Local\Temp\claude\525e89c771cb4f7a\82b33cdb-60b5-4fc8-a510-991f35788a4f\scratchpad\overlay_b21
- Game version: ML header `Release:1.0.17`, game log `Release:1.0.17`; Unity 6000.0.81f1; MelonLoader v0.7.3-ci.2524 Open-Beta
- Milestones (s after first ML line): mods loaded 8.9, server ready 51.6, round start 87.1, last game-log line 686.7
- Maps loaded: IndustrialQuarter; round: MP_Strategy HUMANS_VS_HUMANS_VS_ALIENS
- Baseline: Release:1.0.17, 49 mods loaded
- Logs: ML `C:\Users\schwe\Projects\Si_ServerPerf\measure\smoke_b21\step2_stage_b21\MelonLoader_Latest.log`; game `C:\Users\schwe\Projects\Si_ServerPerf\measure\smoke_b21\step2_stage_b21\game_server.log`

## Mods: 50/50 DLLs registered a Melon (ML says '50 Mods loaded')

<details><summary>Loaded mods (name + version)</summary>

Admin Mod 2.14.01 (`Si_AdminMod.dll`), Si_Alliance 0.4.0 (`Si_Alliance.dll`), Server Announcements 1.2.3 (`Si_Announcements.dll`), Si_AntiCheat 0.3.1 (`Si_AntiCheat.dll`), Anti-Grief 1.6.6 (`Si_AntiGrief.dll`), Basic Banlist 1.5.9 (`Si_BasicBanlist.dll`), Basic Team Balance 1.4.6 (`Si_BasicTeamBalance.dll`), Build Limits 1.2.1 (`Si_BuildLimits.dll`), Buy 1.9.1 (`Si_Buy.dll`), Chat Silence 2.0.3 (`Si_ChatSilence.dll`), Chipmunk Voices 0.9.2 (`Si_ChipmunkVoices.dll`), Commander Management 1.9.14 (`Si_CommManagement.dll`), Crab Cannon 2.8.5 (`Si_CrabCannon.dll`), Early Encounters 1.1.7 (`Si_EarlyEncounters.dll`), Eject Command 1.0.3 (`Si_Eject.dll`), EloMatchmaking 0.8.2 (`Si_EloMatchmaking.dll`), End Round 1.1.0 (`Si_EndRound.dll`), Friendly Fire Limits 1.3.5 (`Si_FriendlyFireLimits.dll`), Heavy Teleporter 1.3.2 (`Si_HeavyTeleporter.dll`), King of the Galactic Teleport 0.5.4 (`Si_KingOfTheHill.dll`), Leaderboard 0.1.4 (`Si_Leaderboard.dll`), Half-Life Logger 1.9.12 (`Si_Logging.dll`), Logistics 0.9.3 (`Si_Logistics.dll`), Map Balance 3.6.0 (`Si_MapBalance.dll`), Mapcycle 1.8.2 (`Si_Mapcycle.dll`), Market 0.6.0 (`Si_Market.dll`), Match Settings Override 2.0.1 (`Si_MatchSettings.dll`), Si_ModUI_Server 1.8.1 (`Si_ModUI_Server.dll`), Si_NetPulse 1.2.0 (`Si_NetPulse.dll`), Packet Chunker 1.2.0 (`Si_PacketChunker.dll`), Si_PerfProbe 0.1.4 (`Si_PerfProbe.dll`), Repair Facility 1.4.0 (`Si_RepairFacility.dll`), Replay Logging 0.6.0 (`Si_ReplayLogging.dll`), Resource Configuration 1.4.6 (`Si_Resources.dll`), Si_RTS_AI 0.94.14 (`Si_RTS_AI.dll`), Server Control 1.0.0 (`Si_ServerControl.dll`), Si_ServerFpsCap 1.0.0 (`Si_ServerFpsCap.dll`), Server Optimizer 0.7.1 (`Si_ServerOptimizer.dll`), Si_ServerPerf 0.2.0 (`Si_ServerPerf.dll`), Si_Spawn 1.2.0 (`Si_Spawn.dll`), Admin Spawn Configs 0.9.4 (`Si_SpawnConfigs.dll`), Spectator 0.1.1 (`Si_Spectator_Server.dll`), Starter Units 1.3.1 (`Si_StarterUnits.dll`), Surrender Command 1.6.4 (`Si_SurrenderCommand.dll`), Tech Glitch Command 1.1.0 (`Si_TechGlitch.dll`), Time of Day 0.9.9 (`Si_TimeOfDay.dll`), Unit Balance 7.14.0 (`Si_UnitBalance.dll`), Si_VersusVote 0.3.2 (`Si_VersusVote.dll`), WaspHarvest 0.3.0 (`Si_WaspHarvest.dll`), Worm Bounty 1.1.4 (`Si_WormBounty.dll`)

</details>

## Errors attributed to mods

Phase counts = load / round-start (ready .. round start + 15 s) / runtime. 'Base' = also seen in the baseline log.

_none_

## Vanilla game errors (no mod frame)

_none_

## Suppressed known noise (noise_patterns.json)

- 278x GetGroundSlopeClearJobified Error: trying to access a query which wasn't started yet -- vanilla AI placement query spam on 1.0.15 already (hundreds per round), no mod frames
- 3x GameInput.SetInputModule: CurrentAsset is null. The GameInputAsset must be set before InputSystemUIInputModule -- vanilla: no input asset on a headless server
- 2x StructureSellComponent: action 'Sell' of map 'Commander' is not registered with GameInput, the sell hotkey wil -- vanilla: input map missing on server, every scene load

## In the baseline, not in this run

_none_


## MelonLoader warnings per mod (count only)

Si_NetPulse.dll: 15, Si_UnitBalance.dll: 8, Si_PerfProbe.dll: 3, Si_EloMatchmaking.dll: 2, Si_AdminMod.dll: 1, Si_CommManagement.dll: 1, Si_RTS_AI.dll: 1

