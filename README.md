# Si_ServerPerf 0.2.1

MelonLoader server mod for the **Silica dedicated server, 1.0.17, Mono, headless** (Steam test branch). It implements
the server recommendations S1-S13 of the Silica performance DEV_REPORT §5 (separate project) as switchable Harmony
patches, grouped in modules. Every patched sub-feature ("item") has its own config boolean and its own runtime switch,
so A/B measurements can flip it inside one running server.

Status: built and statically verified against 1.0.17 (refcheck + TargetCheck + offline kernel tests), measured on a
headless 1.0.17 lab server with generated battle scenarios and no client (builds b10-b20, `measure\night1\NIGHT1.md`).
0.2.0 = build b21: same code as b20 with the new defaults below. 0.2.1 = build b22: same code, worker-thread target
scans on by default as well. Client experience so far: one session on the owner's server with the full public mod set
and one player (0.2.0, 2026-11-17, no errors); nothing more. Not yet run on a populated server.

| Module | DEV_REPORT | State |
|---|---|---|
| `Headless` | S5, S6 (fog lookup), S13 (Steam 1 Hz, ban walk) | implemented |
| `UpdateTiers` | S1, S11, fixed-late dt | implemented |
| `Targeting` | S2 + friendly-fire + Rank + target-scan snapshot | implemented |
| `Physics` | S3 + catch-up cap + dead-unit items | implemented |
| `Services` | S6 index lookups, S8 A*, S9 commander AI, S10 server-only network, #14 disposal walk | implemented (S4 placement budget not built, on purpose) |
| `MultiCore` | S7 / S12: work on Unity's job-worker threads | implemented; target scans on workers on by default since 0.2.1, the rest experimental and off |

## Defaults in 0.2.0 and 0.2.1

New defaults compared with 0.1.0 (decided after the night-1 lab measurements):

| Item | 0.1.0 | 0.2.0 | Visible to players? |
|---|---|---|---|
| `Targeting.CollectEnemies_Snapshot` | off | **on** | no (exact: same targets, same order, same scores) |
| `Targeting.CollectEnemies_SnapshotScore` | off | **on** | no (exact) |
| `Targeting.CollectEnemies_FastAdd` | off | **on** | no (exact) |
| `Physics.DeadAgentHalt` (with `DeadAgentHaltClearAim` = false) | off | **on** | no (server-side AI of dead units; see below) |
| `Physics.DeadDisposeEarly` | off | **on**, fixed 5 s | **yes**: wrecks and corpses vanish 5 s after death instead of 15 s |
| `Physics.Settings.DisposeEarlyFpsLow` / `DisposeEarlyFpsHigh` | 25 / 45 | **0 / 0** (= always `DisposeEarlySeconds`, 5 s) | |
| `MultiCore.ParallelScan`, `MultiCore.ParallelScan_Prefiltered`, `Targeting.CollectPreFiltered_Snapshot` | off | off in 0.2.0, **on since 0.2.1** | no (exact; AI target scans computed on worker threads) |

All other items keep their 0.1.0 default. Every item and its default:

| Module | On by default | Off by default |
|---|---|---|
| `Headless` | `SteamDetails_1Hz`, `PlayerBans_1Hz`, `Fog_SkipPresentation`, `Fog_DetectedTargetLookup`, `Animators_Disable`, `Particles_Disable`, `StructureLights_Skip`, `MeshCombiner_Skip`, `SceneLOD_Skip`, `LightGrid_Skip`, `VehicleEngineVents_Skip`, `VehicleCreaking_Skip`, `ConstructionWarningUI_Skip`, `CreatureAttackFX_Skip`, `Diag_LogExceptions` | `CreatureAttackInflate_Skip` |
| `UpdateTiers` | `Tier_Structures`, `Tier_HealDecay`, `Tier_OrderProcessors`, `Tier_IdleSoldiers`, `Tier_StaticNetTransforms`, `Register_O1` | `Tier_IdleAgents` (no gain), `FixedLate_StepDt` |
| `Targeting` | `DetectedGrid`, `CollectEnemies_Grid`, **`CollectEnemies_Snapshot`**, **`CollectEnemies_SnapshotScore`**, **`CollectEnemies_FastAdd`**, **`CollectPreFiltered_Snapshot`** (0.2.1), `UrgentClose_Grid`, `NearestTarget_Grid`, `AlarmProximity_Grid`, `FriendlyFire_Grid`, `FriendlyFire_StaleGuard`, `Rank_Sort`, `Scan_Diag` (counters) | `FriendlyFire_Flat` (no gain), `LosTopK` and `FriendlyFire_Cache` (approximate) |
| `Physics` | `Census` (read-only), `CatchUpCap`, **`DeadAgentHalt`**, **`DeadDisposeEarly`** | parked-vehicle set `IdleForceSleep` + `ParkedWheelHold` + `AvoidanceSkipOntoResting` + `AvoidanceNoIdleDetour` (client test first), `FixedStep_Override` (20 Hz step, behaviour change), `AvoidanceIdlePairSkip`, `RestWriteGuards`, `HitZonePoseGuard`, `Interpolation_Off`, no gain: `WheelJointWriteGuard`, `VehicleSolverIterations`, `DeadSettle`, `DeadNoUnitContact`; diagnostics: `WakeProbe`, `WheeledSleepDiag`, `IdleSleepDiag`, `FixedWakeTrace` |
| `Services` | `CommanderAI_SkipOwnedTeams`, `CommanderAI_ThinkStats`, `CommanderAI_ClusterRebuild`, `Astar_CutPollInterval`, `Detected_IndexLookups`, `DestroyedDisposal_EveryN`, `TerrainMaterial_Cache`, `Net_SkipUpdatesNoClients` | `Astar_ProbeFlushDebounce` |
| `MultiCore` | **`ParallelScan`**, **`ParallelScan_Prefiltered`** (worker-thread target scans, exact; 0.2.1; pays in dense battles, little in spread ones; short lab blocks only, no long soak yet) | `SensorTeamMaps_Job` (no gain), `SensorPipeline_Rate` (approximate) |

Dependencies: the three `CollectEnemies_Snapshot*` items run inside `CollectEnemies_Grid`'s prefix; with
`CollectEnemies_Grid` off in the config they are SKIPPED ("needs CollectEnemies_Grid patched"). `_SnapshotScore` only
acts while `_Snapshot` is on; `_FastAdd` also works on the grid path alone. `CollectPreFiltered_Snapshot` reuses the
snapshot code but not the `_Snapshot` switch. `DeadAgentHalt` and `DeadDisposeEarly` are independent of each other.
Headless items that a loaded Si_ServerOptimizer already patches are SKIPPED (see "Coexistence" below). The startup
report lists every item with its state (`ON`, `off (config)`, `SKIPPED` + reason, ...).

### Upgrading from 0.1.0: existing config files keep their values

`UserData\cfg\Si_ServerPerf\Si_ServerPerf.json` only gains keys that are missing; **existing values are never
changed**, so a config written by 0.1.0 still has the five items above set to `false` and `DisposeEarlyFpsLow` /
`DisposeEarlyFpsHigh` at 25 / 45. There is no migration. To pick up the 0.2.0 defaults either delete the file (it is
rewritten with all defaults on the next start) or edit these keys by hand:
`Modules.Targeting.Items.CollectEnemies_Snapshot`, `CollectEnemies_SnapshotScore`, `CollectEnemies_FastAdd`,
`Modules.Physics.Items.DeadAgentHalt`, `DeadDisposeEarly` = `true`, and `Modules.Physics.Settings.DisposeEarlyFpsLow` =
`DisposeEarlyFpsHigh` = `0`; for 0.2.1 also `Modules.MultiCore.Items.ParallelScan`, `ParallelScan_Prefiltered` and
`Modules.Targeting.Items.CollectPreFiltered_Snapshot` = `true`. Remember that an item that is `false` at server start is not patched at all, so it cannot
be switched on later through `runtime.json` or `perf on`; it needs the config change and a restart.

### What players can see

- **`Physics.DeadDisposeEarly` (visible).** Corpses and wrecks of units (soldiers, creatures, vehicles; never
  structures) are removed 5 s after death instead of the game's 15 s. They pop out without a fade, stop blocking paths
  and shots and can no longer be used as cover after 5 s; long death effects attached to the unit are cut. After a
  server start, a map change or switching the item on, the time slides from 15 s down to 5 s over about 20 s
  (`DisposeEarlySlewSecondsPerSecond` 0.5). Switching it on during a big fight removes the bodies older than the new
  time within seconds (in one lab run ~95 bodies left in one 10 s step).
  **Back to vanilla 15 s:** set `Modules.Physics.Items.DeadDisposeEarly` to `false` (one key; restart), or at runtime
  `perf off Physics.DeadDisposeEarly`. Per kind: `DisposeEarlySoldiers` / `DisposeEarlyCreatures` /
  `DisposeEarlyVehicles`. fps-dependent instead of fixed: `DisposeEarlyFpsLow` 25 / `DisposeEarlyFpsHigh` 45 (15 s at
  >= 45 fps, 5 s at <= 25 fps), the form the lab report recommended if the item is on.
- **`Physics.DeadAgentHalt`** stops the AI agent of a dead unit 1 s after its death (behaviour tree, target scans, line
  of sight, paths). A dead unit cannot act either way. One detail differs from vanilla: a vanilla corpse keeps
  re-picking targets until disposal and so counts as an "attacker" on whatever it aims at (live AI spreads fire away
  from targets attacked by many); with the halt it keeps its last aim instead. Measured behaviour change: none
  (deaths, kills, shots, hits and with-target share within |t| < 1 over 11 blocks). `DeadAgentHaltClearAim` = true
  would drop those aims (shots -7.0 %, hits -4.7 %, t about -1.6): kept false.
- **The three target-scan items** are exact (same candidates, order and score bits as vanilla; 83 479 self-checks
  each, 0 mismatches). Nothing visible.

## Measured (lab, `measure\night1\NIGHT1.md`)

First run outside the lab (0.2.0 defaults, owner's server, full public mod set, one player, 2026-11-17): a short
on/off comparison inside a stress round at 730-790 units gave 8.4 fps with the mod against 6.3 fps with its items
switched off (about +34 %), no errors: `measure\smoke_b21\LIVE_AB.md`. Smoke tests of b21: `measure\smoke_b21\SMOKE.md`.

Headless 1.0.17 lab server, 0 clients, 120 fps cap, generated three-faction scenarios driven by a lab mod, all arms
switched through `runtime.json` inside one running server (paired 30-120 s blocks). Percent values are main-thread
frame work (CPU ms per frame without the fps-cap wait) versus the "mod off" arm (Si_ServerPerf loaded with its items
switched off at runtime, other lab mods incl. Si_ServerOptimizer 0.7.1 running; not pure vanilla). "new" in the report = the 0.1.0 defaults + the three target-scan items + `DeadAgentHalt` = the 0.2.0
defaults **without** early wreck removal.

| scenario | mod off | 0.1.0 defaults | "new" | 
|---|---:|---:|---:|
| dense melee, ~425 lab units (2 runs) | 35.3 / 32.6 fps | 54.7 / 50.7 fps (-36.8 %) | 63.8 / 57.1 fps (-47.9 %) |
| dense melee, ~522 lab units (2 runs) | 11.9 / 11.3 fps | 15.6 / 15.5 fps (-27.1 %) | 21.2 / 24.6 fps (-51.4 %) |
| four fronts, ~422 lab units | 50.3 fps | 67.2 fps | 77.0 fps (-40.0 %) |
| four fronts, ~518 lab units | 19.3 fps | 33.2 fps | 38.6 fps (-52.3 %) |
| four fronts, ~614 lab units | 10.6 fps | 13.9 fps | 17.8 fps (-41.1 %) |

- "new" vs the 0.1.0 defaults in the four-front layout: -17.1 / -15.5 / -20.7 % frame work.
- Early wreck removal, always 5 s: -24.1 % (~424) / -41.5 % (~521) frame work vs the 0.1.0 defaults (`b14_dead`);
  "new" at ~521 went from 27.3 to 40.1 fps with it (`b15_best_plat`, build b15, the closest sample of the full 0.2.0
  set; the final b20 runs did not have an "always 5 s" arm). Its gain grows with load:
  the fps-proportional form on top of every item gained -31.6 % in an overloaded melee (21.1 -> 30.0 fps) and little
  once the other items had restored fps (-10.5 % melee, -2.2 % four fronts, noise).
- `DeadAgentHalt` alone: -5 to -19 % in three samples.
- 0 exceptions and nothing FAILED in any arm of any run. Separate server starts differ by about ±15 %, so only compare
  arms within one run. Nothing has been measured with a client.

## Build and verify

From the project folder. Lab builds go to a versioned folder (the test rig measures from `release\bNN`, never from
`bin\`); `release\` is not in git.

```
dotnet build Si_ServerPerf.csproj -c Release -nologo -v q -o release\b21
  -> release\b21\Si_ServerPerf.dll          (0.2.0 = b21, 0.2.1 = b22)

# compile-time references vs 1.0.17 (must print "issues": []); SiCecil lives in the Si_UpdatePipeline project
<SiCecil.exe> refcheck <_silica_refs>\versions\1.0.17\server_Managed ^
  "<_silica_refs>\net35;<Silica Dedicated Server>\Mods" release\b21\Si_ServerPerf.dll ^
  SilicaCore UnityEngine.CoreModule UnityEngine.AnimationModule UnityEngine.ParticleSystemModule UnityEngine.PhysicsModule ^
  Unity.Collections Unity.Mathematics Unity.Burst AstarPathfindingProject Newtonsoft.Json MelonLoader 0Harmony Si_AdminMod ^
  com.rlabrecque.steamworks.net

# string-named Harmony targets / reflected members vs 1.0.17 (Core\targets.txt + Modules\*\targets.txt)
dotnet run --project tools\TargetCheck -c Release -- <_silica_refs>\versions\1.0.17\server_Managed .

# offline tests (plain .NET, no game; exit code 0 = passed; bin/obj go to %TEMP%, see the Tests\Directory.Build.props)
dotnet run -c Release --project Modules\MultiCore\Tests\MultiCoreTests.csproj
dotnet run -c Release --project Modules\MultiCore\Tests\ParallelScanTests.csproj
dotnet run -c Release --project Modules\Targeting\Tests\FriendlyFlatTests.csproj
# MultiCore patches installed through MelonLoader's net6 HarmonyX against the 1.0.17 IL (needs the built DLL)
dotnet build -c Release Modules\MultiCore\Tests\TranspilerCheck -p:MelonNet6Dir=<Silica Dedicated Server>\MelonLoader\net6\
dotnet %TEMP%\Si_ServerPerf_MultiCoreTests\TranspilerCheck\bin\Release\net6.0\TranspilerCheck.dll ^
  <_silica_refs>\versions\1.0.17\server_Managed <Silica Dedicated Server>\MelonLoader\net6 release\b21\Si_ServerPerf.dll
```

References (csproj): game + Unity + Newtonsoft from `_silica_refs\versions\1.0.17\server_Managed`, MelonLoader +
0Harmony from `_silica_refs\net35` (the Mono server's; net6 copies give CS1705), Si_AdminMod from `_silica_refs\test`.
`_silica_refs` defaults to the folder next to this project (`..\_silica_refs\`); elsewhere build with
`-p:SilicaRefs=<dir>\`. If your module needs another game/Unity assembly that is not referenced yet, say so in your
hand-back; do not edit the csproj in your copy. `tools\TargetCheck\TargetCheck.csproj` points at a local Mono.Cecil.dll
(the SiCecil build); adjust its HintPath on another machine.

Target framework netstandard2.1, `Nullable` disabled, every `*.cs` below the project folder is compiled except
`measure\` (measurement scripts, owned by the measurement job) and `tools\`. Test sources below `Modules\*\Tests\`
are wrapped in `#if` symbols only their test projects define, so the mod build sees empty files.

## Layout

```
Si_ServerPerf.csproj
Core\            entry point (ServerPerfMod), module discovery, config, items, runtime switches, commands, lifecycle
Shared\          Perf (frame/round state, logging), Env (headless, melon lookup, version parse), Refl (accessors)
Modules\<Name>\  one folder per module: its IPerfModule class, settings, patches, targets.txt, own helpers
Modules\<Name>\Tests\  offline tests of a module's Unity-free kernels (MultiCore, Targeting)
tools\TargetCheck\  Cecil checker for targets.txt files
measure\         lab method, scenarios, runtime switch files, scripts and reports (not code; do not touch).
                 In git: *.md reports, *.ps1 / *.py scripts, scenario / config / plan JSON, per-run report_*.txt.
                 Raw server logs and per-run probe CSVs stay local (see .gitignore).
release\bNN\     versioned lab builds (local only)
```

## Module contract (for module authors)

**Rule 0: add files only inside `Modules\<YourName>\`.** No edits to Core, Shared, the csproj or another module.
The manager merges by copying your folder in. Namespace `SiServerPerf.Modules.<YourName>`.

### 1. The module class

```csharp
namespace SiServerPerf.Modules.Physics
{
    public sealed class PhysicsSettings                 // your settings: public fields with defaults
    {
        public int MaxCatchUpSteps = 3;
        public float IdleSleepSeconds = 2f;
    }

    [PerfModuleOrder(0)]                                // optional; low first, then by Name. Headless = -100
    public sealed class PhysicsModule : IPerfModule     // public parameterless ctor, found by reflection
    {
        public string Name => "Physics";                // config section, item-id prefix, log prefix
        public string Description => "S3 fewer dynamic rigidbodies + physics catch-up cap";

        public void Apply(ModuleContext ctx)            // called once at OnInitializeMelon
        {
            var s = ctx.Settings<PhysicsSettings>();     // defaults merged into the JSON, existing values kept
            CatchUpCap.Register(ctx, s);
            IdleSleep.Register(ctx, s);
        }
    }
}
```

If `Apply` throws, the core unpatches everything your module patched (its own Harmony instance
`Si_ServerPerf.<Name>`), switches all its items off and logs the exception; the server keeps running.
When the module is disabled in the config (`"Enabled": false`) `Apply` still runs so the items are declared and
listed, but nothing is patched and no hook is registered.

### 2. An item, its switch, its patch

```csharp
internal static class CatchUpCap
{
    internal static bool On;        // THE switch: plain static bool, read by the patch, written only by the core
    static PerfItem Item;

    public static void Register(ModuleContext ctx, PhysicsSettings s)
    {
        Item = ctx.Item("CatchUpCap",                                  // key: letters/digits/_ , unique in module
                        "Cap physics catch-up at N fixed steps per frame",
                        v => On = v,                                   // setter for the switch
                        defaultOn: true,                               // config default
                        flags: ItemFlags.None);                        // Presentation / StartupOnly, see below
        var m = Item.Target(typeof(SomeGameType), "SomeMethod", new[] { typeof(float) });   // null if absent
        Item.Patch(m, typeof(CatchUpCap), prefix: nameof(Prefix));     // no-op if item off/blocked or m == null
    }

    static bool Prefix(SomeGameType __instance, float dt)
    {
        if (!On) return true;                          // switch off -> vanilla, first line of every patch
        try
        {
            // ... your work ...
            return false;
        }
        catch (Exception ex)
        {
            Item.Fail(ex, "SomeMethod prefix");        // fail-open: item off for good, logged once with stack
            return true;                               // and vanilla runs
        }
    }
}
```

- `ctx.Item(...)` reads `Modules.<Module>.Items.<key>` from the config (written with `defaultOn` if absent). An item
  that is false at startup is **not patched at all**.
- `Item.Target(type, name, args, declaredOnly)` resolves with `AccessTools.Method` / `DeclaredMethod`, counts the
  member as "targeted" and lists it as "absent on this build" if it does not resolve. `Item.Target(methodBase, label)`
  for anything you resolved yourself (getters, constructors). Resolution also runs for items that are off.
- `Item.Patch(target, patchClass, prefix:, postfix:, transpiler:, finalizer:, priority:)` installs through Harmony.
  Patch methods are looked up by name in `patchClass` (static, any visibility). It never throws: a Harmony error, a
  missing patch method or a transpiler `Problem` leaves that target NOT APPLIED. Call it once per target (several
  calls on one target are fine). **Never call `Harmony.Patch` yourself.**
- After `Apply`, every item with at least one patched target gets its switch set to true.
- `Item.Block(reason)`: keep the item unpatched (overlap with another mod, a required field missing ...). Shown as
  SKIPPED with the reason.
- `Item.Hooked(label)`: count a non-Harmony effect (event subscription, per-frame helper) as one applied target;
  returns whether the item is active, so `if (Item.Hooked("...")) Application.logMessageReceived += ...;`.
- `Item.OnSwitch(on => ...)`: runs after every switch change (runtime flip or fail-open) to restore / re-apply state
  you changed (re-enable components you disabled, ...). Animators_Disable and SceneLOD_Skip are examples.
- `Item.Note(text)`: fixed fact for the report (deduplicated). `Item.Info = text`: one overwritable runtime status
  (counters, "last switch-off: n re-enabled"); use Info for anything that repeats.
- Shortest correct patch for pure skips: `static bool Prefix() => !On;` (cannot throw, no try needed).

### 3. Fail-open rules

1. First line of every patch body: `if (!On) return true;` (prefix) / `if (!On) return;` (postfix).
2. Everything else inside `try { } catch (Exception ex) { Item.Fail(ex, "where"); <vanilla-equivalent>; }`.
   A prefix returns true after Fail so vanilla runs; a postfix just returns.
3. Code called **from transpiled IL** (helpers that replace a call) does the same: check `On`, try, on exception
   `Item.Fail` and then compute the vanilla result (see `Modules\Headless\Fog.cs`, `FogDetectedLookup.ByTeam`).
4. Transpilers: find the pattern first, mutate only if it matches exactly; otherwise call
   `Item.Problem("expected ..., found ...")` and return the instructions unchanged. During `Item.Patch` that marks the
   target NOT APPLIED and unpatches it. (Harmony re-runs transpilers when another mod patches the same method later;
   then Problem only logs.)
   **Labels pitfall:** with the net35 0Harmony against netstandard2.1, the type `System.Reflection.Emit.Label` cannot
   be named (CS7069 "Reference to type 'Label' claims it is defined in 'mscorlib', but it could not be found"; adding
   the netstandard2.1 mscorlib facade does not help). So `ci.labels`, `new Label`, `ExtractLabels()`, `WithLabels(...)`,
   `Branches(out Label?)` do not compile. What does compile and works: reuse the labelled `CodeInstruction` object and
   change its `opcode`/`operand` (Headless `Fog.cs` turns the dropped call into a `Nop` that keeps its labels);
   `b.MoveLabelsFrom(a)` / `a.MoveLabelsTo(b)` / `b.MoveBlocksFrom(a)`; reading the count by reflection,
   `((ICollection)AccessTools.Field(typeof(CodeInstruction), "labels").GetValue(ci)).Count`.
5. `Item.Fail` is idempotent: it sets the switch false, runs the `OnSwitch(false)` handlers, logs once with the full
   stack. A failed item stays off until restart; runtime requests to re-enable it are refused.
6. Do not throw from `Apply` on purpose for a missing member: use `Target` (absent) or `Block`.

### 4. Flags

- `ItemFlags.Presentation`: applied only when the server is headless (`Application.isBatchMode` or Null graphics
  device) while `Core.RequireHeadless` is true. Use it for skips of presentation work.
- `ItemFlags.StartupOnly`: the item changes state the core cannot undo by flipping the switch. Runtime flips are
  refused (status says "startup-only"); a `runtime.json` present at server start still decides it (nothing has been
  changed yet then); fail-open still switches it off. Prefer a real toggle with an `OnSwitch` restore when feasible.

### 5. Settings

`ctx.Settings<T>()` with `T` a plain class (public fields/properties with initializers, Newtonsoft-serializable,
nested objects fine). Stored under `Modules.<Module>.Settings`. Missing keys are added with their defaults on every
start, existing values are kept. If the section cannot be read into `T`, the defaults are used for that run and the
file is left alone. Settings are read once at startup (not hot-reloaded); runtime behaviour changes go through items.

### 6. Lifecycle hooks and shared helpers

```csharp
ctx.OnUpdate(Action, owner)          // MelonMod.OnUpdate, every frame
ctx.OnLateUpdate(Action, owner)      // MelonMod.OnLateUpdate
ctx.OnFixedUpdate(Action, owner)     // MelonMod.OnFixedUpdate
ctx.OnSceneLoaded(Action<string>, owner)       // OnSceneWasLoaded(scene name)
ctx.OnSceneInitialized(Action<string>, owner)  // OnSceneWasInitialized(scene name)
ctx.OnRoundStart(Action, owner)      // postfix MusicJukeboxHandler.OnGameStarted (as Admin Mod does)
ctx.OnRoundEnd(Action, owner)        // postfix MusicJukeboxHandler.OnGameEnded
ctx.OnQuit(Action, owner)            // OnApplicationQuit (not reached when the process is killed)
ctx.OnBeforeStatus(Action, owner)    // right before every status block (startup, perf status, runtime.json dump, quit)
```
Use `OnBeforeStatus` (usually without owner) to refresh `item.Info` counters, so a status block is current instead
of showing whatever your own timer wrote last.
With `owner` (a `PerfItem`): called only while `owner.On`; an exception calls `owner.Fail`. Not registered if the
owner is not active. Without owner: module-level; an exception removes the hook (logged once).
MelonLoader's Update runs in the same frame as the game's `GameManager.Update` but the order between them is not
defined; if you need a precise point in the game loop, patch the game method (prefix/postfix) through an item.

`Perf` (static, plain fields): `Frame` (Time.frameCount at this mod's OnUpdate), `RealTime`, `UnscaledDt`,
`RoundActive`, `RoundIndex` (1 = first round), `SceneName`; `Perf.Log/Warn/Error`. `ctx.Log/Warn` prefix your
module name. MelonLogger already prefixes "[Si_ServerPerf]".

`Env`: `Headless`, `HeadlessReason`, `FindMelon(name, out version, out asm)` (MelonInfo name or assembly name; works
regardless of load order), `ParseVersion`.

`Refl`: `Field<T,F>(name, item)` / `StaticField<F>(type, name, item)` return Harmony `FieldRef` delegates or null
(absence noted under the item); `Method`, `Getter`. Resolve once in `Register`, keep in a static field.

**Name shadowing:** module namespaces are `SiServerPerf.Modules.<Name>`, and a `Physics` module exists, so inside any
`SiServerPerf.Modules.*` namespace a bare `Physics.Raycast(...)` resolves to that namespace, not to Unity. Always write
`UnityEngine.Physics` fully qualified (same for any Unity/game type whose name equals a module name).

Your own helpers go in your folder. Cross-module coupling: none expected; if you need something from another module,
expose `internal static` members in your namespace and handle "that module is absent / off".

### 7. targets.txt (static verification)

Put a `targets.txt` next to your module listing every string-named target and reflected member (format in
`tools\TargetCheck\Program.cs`: `M|Type|Method|ParamFullName,...|declaredOnly`, `F|Type|Field|FieldType`,
`P|Type|Property`, `Asm:` prefix for other assemblies such as `AstarPathfindingProject:Pathfinding.NavmeshUpdates`).
TargetCheck must print `ALL n RESOLVED`. It flags non-virtual targets under 20 bytes of IL: Mono may inline them at
call sites, where a patch then silently does nothing.

### 8. Template checklist

- [ ] folder `Modules\<Name>\`, namespace `SiServerPerf.Modules.<Name>`, class implements `IPerfModule`
- [ ] one `internal static bool On` + `ctx.Item(...)` per sub-feature; every patch starts with the switch check
- [ ] every non-trivial patch body in try/catch -> `Item.Fail` -> vanilla
- [ ] transpilers: exact match or `Item.Problem` + unchanged IL
- [ ] persistent state changes: `OnSwitch` restore, or `ItemFlags.StartupOnly`
- [ ] `targets.txt`; `dotnet build` 0 warnings; refcheck clean; TargetCheck all resolved
- [ ] no edits outside your folder

## Config: `UserData\cfg\Si_ServerPerf\Si_ServerPerf.json`

Written with defaults on first start; on later starts missing keys are added and existing values kept. A file that
does not parse is never overwritten (defaults are used for that run, error logged).

```json
{
  "Core": {
    "RequireHeadless": true,            // Presentation items only when headless
    "RuntimeFile": true,                // poll runtime.json
    "RuntimePollSeconds": 1.0,
    "ApplyRuntimeFileAtStartup": true,  // a runtime.json already present at start is applied (and logged)
    "WriteRuntimeState": true,          // runtime_state.json after each change
    "ConsoleCommand": true,             // server console "perf"
    "ChatCommand": true                 // Admin Mod "!perf"
  },
  "Modules": {
    "Headless": {
      "Enabled": true,
      "Items": { "SteamDetails_1Hz": true, "Fog_SkipPresentation": true, "...": true },
      "Settings": { "AllowOverlapWithServerOptimizer": false, "SteamDetailsIntervalSeconds": 1.0, "...": 0 },
      "ItemHelp": { "SteamDetails_1Hz": "description (rewritten by the mod on every start, not read)" }
    }
  }
}
```
(JSON has no comments; they are annotations here.)

## Runtime switches (A/B inside one server)

`UserData\cfg\Si_ServerPerf\runtime.json`, polled every `RuntimePollSeconds` (1 s). Re-applied whenever its **text**
changes (so alternating copies of two files with old timestamps work). Replace it atomically (write tmp, move).

```json
{
  "seq": 7,
  "items": {
    "*": true,
    "Headless.*": false,
    "Headless.Fog_SkipPresentation": true
  },
  "dump": true
}
```
- `items` is applied in file order; keys: `Module.Item`, `Module.*`, `*` (or `all`), or a bare item key if unique.
  Top-level `"Module.Item": bool` entries work too; keys starting with `_` are ignored.
- Wildcards skip items that were never patched (off in config, SKIPPED, absent). Explicit ids report why they cannot
  follow (not patched / startup-only / failed).
- **Windows.** Window 0 runs from startup to the first applied change; every applied change closes window n and opens
  n+1. `dump: true` in the NEW file writes the status block of the window that is closing, BEFORE the new switches
  apply, titled `window <n> ENDED (seq <old>, before runtime.json seq <new> applies) - states and counters below belong
  to the window that just ended`. So each block is one unambiguous record: its states and Info counters describe the
  same window. Once any file asked for `dump`, the last window gets its block at quit (`..., server quit)`; not when
  the process is killed). Window 0 is described by the startup report (a runtime.json present at start is applied
  first and gets no separate block). Bump `seq` to force a new window with identical switches.
- After every applied change `runtime_state.json` (same folder) holds `{version, utc, frame, seq, window, items: {id:
  {on, state, patched, targeted, startupOnly, changes, info}}}` = the state of the window that just started.
- Every applied change logs one line: `runtime.json seq 7: Headless.X: off, ... | refused: ... | window 3 starts (window 2 had seq 6)`.

Server console (also an admin client's remote console, admin level ROOT) and, with Admin Mod, chat:
`perf` / `perf status` (status block to the log), `perf on <pattern>`, `perf off <pattern>`, `perf state`.

## Startup report (MelonLoader\Latest.log)

```
==== Si_ServerPerf 0.2.1 startup ====
headless=True (batchmode=True, graphics=Null), RequireHeadless=True | Si_ServerOptimizer: 0.7.1 loaded (Si_ServerOptimizer) | frame 0, round 0 | config ...
module Headless: enabled, 16 items, 14 on - S5 presentation skips + ...
  note: Si_ServerOptimizer 0.7.1 loaded: 1 overlapping item(s) left off so nothing is patched twice: LightGrid_Skip
  Headless.SteamDetails_1Hz                      ON                       1/1  toggle
  Headless.Animators_Disable                     ON                     24/24  toggle,presentation
  Headless.Particles_Disable                     ON                       3/3  startup-only,presentation
  Headless.LightGrid_Skip                        SKIPPED                  0/1  toggle,presentation  | Si_ServerOptimizer 0.7.1 already patches LightGrid.Update
  Headless.CreatureAttackInflate_Skip            off (config)             0/1  toggle,presentation
==== end Si_ServerPerf startup ====
```
States: `ON`, `ON (partial)` (some targets absent / not applied), `off (runtime)`, `off (config)`,
`off (module disabled)`, `SKIPPED` (+ reason), `NOT APPLIED` (+ absent members / problems), `FAILED` (+ where).
Columns: members patched / targeted.

## Headless module

Source: our Si_ServerOptimizer 0.8.0 (uncommitted, `Projects\Silica-wt-serveroptimizer`, never run), re-verified
against 1.0.17; safety per `Projects\Silica_Performance\findings\I_server_cosmetic_skip_safety.md`.

| Item | Patched members (1.0.17) | Kind |
|---|---|---|
| `SteamDetails_1Hz` | prefix `NetworkGameServer.SendUpdatedServerDetailsToSteam` (private); field `m_QueueUpdateGameTags` | toggle |
| `PlayerBans_1Hz` | prefix `NetworkServerSettings.UpdatePlayerBans`; field `BanUpdateDelay` | toggle |
| `Fog_SkipPresentation` | prefix `FogOfWar.ShowFog(bool)`, `FogOfWar.UpdateFogPixelBlend(float)` | toggle, presentation |
| `Fog_DetectedTargetLookup` | transpiler `FogOfWar.OnUpdate`: the one `List<Target>.IndexOf` -> `ByTeam(Team, Target)` via `Team.DetectedTargetIndexes` (fallback `ByList`) | toggle |
| `Animators_Disable` | postfix `Soldier.OnInit`, `CreatureDecapod.OnInit`; prefix `UnitAnimator.CheckActivationDistance`, `CharacterAnimator.EnsureDeadPose`, `CharacterAnimator.UpdateIKTargets`; prefix+postfix `UnitAnimator.Dead`, `CharacterAnimator.Dead`; skip-while-off prefix on 16 parameter writers + `SetWeaponLowered` (24 members) | toggle with restore, presentation |
| `Particles_Disable` | postfix `ProjectileData.InstantiateProjectile(Vector3,Quaternion)` (private), `BaseGameObject.Awake`; scene sweep on scene init | startup-only, presentation |
| `StructureLights_Skip` | prefix `Structure.UpdateStructureLights` | toggle, presentation |
| `MeshCombiner_Skip` | prefix `StructureMeshCombiner.OrderedLateUpdateAll(float)` | toggle, presentation |
| `SceneLOD_Skip` | prefix `SceneLOD.SceneLODCell.Update` (+ disables the cell; switch-off re-enables) | toggle with restore, presentation |
| `LightGrid_Skip` | prefix `LightGrid.Update` (static) | toggle, presentation |
| `VehicleEngineVents_Skip` (new) | prefix `VehicleEngineVents.OrderedUpdate(float)`; destroyed vehicles run vanilla (it unregisters) | toggle, presentation |
| `VehicleCreaking_Skip` (new) | prefix `VehicleCreaking.OrderedFixedUpdate(float)` | toggle, presentation |
| `ConstructionWarningUI_Skip` (new) | prefix `ConstructionWarningUI.UpdateUI(float,Vector3,float,bool)` | toggle, presentation |
| `CreatureAttackFX_Skip` (new) | prefix `OnAttackStart(bool)` of `CreatureAttackFX`, `CreatureAttackGlowBlob`, `...GlowCurves`, `...GlowEye`, `...GlowTongue` | toggle, presentation |
| `CreatureAttackInflate_Skip` (new, **default off**) | prefix `CreatureAttackInflate.OnAttackStart(bool)`; moves/scales `InflateTransform`, hit-zone overlap not checked | toggle, presentation |
| `Diag_LogExceptions` | `Application.logMessageReceived` hook; summary line on every scene load and at quit, also "0 logged exceptions/warnings"; running count in the status block | toggle |

Not patched on purpose: `FogOfWar.OnUpdate` as a whole, `UpdateFogPixelData`, fog `LateUpdate`;
`ConstructionSite.UpdateEffects` / `ConstructionEffect.UpdateEffect` (collider segments + heat damage);
`HumanHandsAnimator`; `UnitAnimator.Awake`; `OnInCompartmentAnimatorLateUpdate`. Not needed on the server:
`UnitVoiceManager` (disables itself), `HideInStrategyMode` (update only registered in strategy mode),
`VehicleParticleFX` (empty stubs). Not covered by findings I, so left out: `AlienStructureFX`, ambient audio.

### Coexistence with Si_ServerOptimizer

At startup the module looks for a loaded "Server Optimizer" melon. Items the loaded version already patches are
SKIPPED with a log line, unless `Headless.Settings.AllowOverlapWithServerOptimizer` is true. For 0.8.0 its own cfg
(`UserData\cfg\Si_ServerOptimizer\Si_ServerOptimizer.cfg`) is read: an item it has set to false is not an overlap.
Unknown or newer versions are treated like 0.8.0 (conservative), with a warning.

| Headless item | SO 0.7.1 (live) | SO 0.7.3 (committed source) | SO 0.8.0 (our uncommitted) |
|---|---|---|---|
| `LightGrid_Skip` | skipped | skipped | skipped |
| `SteamDetails_1Hz`, `PlayerBans_1Hz`, `Fog_SkipPresentation`, `Fog_DetectedTargetLookup`, `Animators_Disable`, `Particles_Disable`, `StructureLights_Skip`, `MeshCombiner_Skip`, `SceneLOD_Skip`, `Diag_LogExceptions` | applied | applied | skipped |
| `VehicleEngineVents_Skip`, `VehicleCreaking_Skip`, `ConstructionWarningUI_Skip`, `CreatureAttackFX_Skip`, `CreatureAttackInflate_Skip` | applied | applied | applied |

0.7.x patches `UnitAnimator.OnAnimatorUpdate` (and 0.7.1 also skips `UnitAnimator.Awake`); `Animators_Disable`
touches neither member. The base set of 0.7.x skips (GameSettings.Load, UI, input, decals, LOD batching, ...) is not
duplicated here: run Si_ServerPerf next to Si_ServerOptimizer 0.7.x (with Si_ServerFpsCap for the frame cap).

### Not verifiable statically (check on the lab server)

Written for the first Headless build. Since then the startup report, config creation and `runtime.json` flips have
run on the lab server in every measured run (`measure\`); the remaining points still hold, and nothing has run with a
client connected.

- Everything at runtime (original note): console/chat command and round hooks were untested.
- Mono JIT inlining: `CharacterAnimator.SetCrouch` (18 B) and `CreatureAnimator.SetFlying` (18 B) are small
  non-virtual targets; where Mono inlines them the skip-while-off prefix does not run (harmless: vanilla writes to a
  disabled Animator, possibly with a Unity warning - Diag counts those).
- The fog transpiler's call-site shape is verified on the 1.0.17 IL with Cecil; Harmony's actual transpile run is not.
- Findings I §8 items (Null-device `Renderer.isVisible`, Animator warnings after a compartment controller swap,
  particle source mix) and the item tests in `_dist\Si_ServerOptimizer_0.8.0_2026-11-03\RELEASE_NOTES.txt` apply
  unchanged.
- Admin Mod chat registration timing (called in OnLateInitializeMelon as Si_PerfLab does).
