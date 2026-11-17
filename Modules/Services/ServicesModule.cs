/*
 Si_ServerPerf module "Services": small independent server-side fixes from DEV_REPORT S6, S8, S9, S10, S13 / cause #14,
 verified against the Silica 1.0.17 decompile (C:\tmp\silica_decompile_1017; A* 5.x decompiled separately).

 Items (default in brackets):
   CommanderAI_SkipOwnedTeams [on]   S9a  AICommander.Tick skipped only for teams another mod DECLARES it fully owns
   CommanderAI_ThinkStats     [on]   S9   measurement only: AICommander.Think count / avg / max per round
   CommanderAI_ClusterRebuild [on]   S9   AIDefenseHandler.RebuildClusters: positions read once, incremental counts, exact
   Astar_CutPollInterval      [on]   S8   navmeshUpdates.updateInterval 0 -> 0.5 s (cut dirty-poll ~2/s, not per frame)
   Astar_ProbeFlushDebounce   [off]  S8   CollisionObstacleManager.ApplyChanges(null) from the blocker probe at most 1 per window
   Detected_IndexLookups      [on]   S6   DetectedTargets.IndexOf/Contains -> Team.DetectedTargetIndexes in 4 methods
   DestroyedDisposal_EveryN   [on]   #14  BaseGameObject.UpdateDestroyedDisposal every Nth frame with accumulated dt
   TerrainMaterial_Cache      [on]   S13  GamePhysics.GetTerrainPhysicMaterial with cached components / terrain scalars
   Net_SkipUpdatesNoClients   [on]   S10  NetworkComponent.TickNetworkObjects skipped while no remote player is connected

 Dropped on purpose (reasons in the hand-back and in the item files): S9b handler spreading, S8 graphMask / static-cut
 exclusion, S6 BaseGameObject.UpdateVisibility (dead on the server), s_QueueSynchHealth HashSet, GetTargetByNetID
 dictionary, writeDebug = false (wire format), S4/S4b (out of scope).
*/

using System.Collections.Generic;

namespace SiServerPerf.Modules.Services
{
    public sealed class ServicesSettings
    {
        // CommanderAI_SkipOwnedTeams: "Namespace.Type.Method" of public static bool M(Team) in any loaded assembly.
        // M(team) == true means: that mod needs NOTHING from the vanilla AICommander of this team (no handler output,
        // no ride-along patch on Think or a handler). Nothing on the current stack implements it (Si_RTS_AI does not).
        public List<string> CommanderOwnerProbes = new List<string> { "Si_RTS_AI.ServerPerfInterop.OwnsVanillaCommander" };
        // How long one probe answer per team is reused.
        public float CommanderOwnerCacheSeconds = 1f;

        // Astar_CutPollInterval: seconds between navmesh-cut dirty polls (vanilla serialized value is 0 = every frame).
        // Only raised, never lowered: a map that already polls slower keeps its value.
        public float NavmeshCutUpdateInterval = 0.5f;

        // Astar_ProbeFlushDebounce: minimum seconds between two synchronous flushes caused by VehicleBlockerProbe.
        public float ProbeFlushMinIntervalSeconds = 0.5f;

        // DestroyedDisposal_EveryN: run the disposal walk every N frames (N >= 2), passing the accumulated dt.
        public int DestroyedDisposalEveryNFrames = 4;

        // CommanderAI_ClusterRebuild: true = neighbour counts once + incremental decrements (O(n^2) per rebuild);
        // false = recount every cluster from the cached positions (O(C n^2), no native calls). Both exact.
        public bool ClusterIncrementalCounts = true;

        // Rig check for Detected_IndexLookups, TerrainMaterial_Cache and CommanderAI_ClusterRebuild: also compute the vanilla answer on every call,
        // count mismatches in the status and return the vanilla answer when they differ. Costs the vanilla price; off.
        public bool VerifyAgainstVanilla = false;

        // Seconds between refreshes of the counters shown in the status output.
        public float StatusRefreshSeconds = 1f;
    }

    [PerfModuleOrder(10)]
    public sealed class ServicesModule : IPerfModule
    {
        public string Name => "Services";
        public string Description => "S9 commander AI ownership skip + think stats, S8 A* cut poll interval / probe flush debounce, S6 detected-target index lookups, S10 no-client net skip, #14/S13 disposal walk + terrain material cache";

        public void Apply(ModuleContext ctx)
        {
            var s = ctx.Settings<ServicesSettings>();

            CommanderOwnership.Register(ctx, s);
            CommanderThinkStats.Register(ctx);
            ClusterRebuild.Register(ctx, s);
            AstarCutPoll.Register(ctx, s);
            ProbeFlushDebounce.Register(ctx, s);
            DetectedIndexLookups.Register(ctx, s);
            DestroyedDisposal.Register(ctx, s);
            TerrainMaterialCache.Register(ctx, s);
            NetSkipNoClients.Register(ctx);

            ServicesStatus.Register(ctx, s);
        }
    }

    // One module-level per-frame hook that rewrites every item's Info at most once per StatusRefreshSeconds, so
    // patches only bump integer counters and never build strings.
    internal static class ServicesStatus
    {
        static float _every = 1f, _next;

        public static void Register(ModuleContext ctx, ServicesSettings s)
        {
            _every = s.StatusRefreshSeconds > 0.1f ? s.StatusRefreshSeconds : 0.1f;
            ctx.OnUpdate(Tick);
        }

        static void Tick()
        {
            float now = Perf.RealTime;
            if (now < _next) return;
            _next = now + _every;
            CommanderOwnership.RefreshInfo();
            CommanderThinkStats.RefreshInfo();
            ClusterRebuild.RefreshInfo();
            AstarCutPoll.RefreshInfo();
            ProbeFlushDebounce.RefreshInfo();
            DetectedIndexLookups.RefreshInfo();
            DestroyedDisposal.RefreshInfo();
            TerrainMaterialCache.RefreshInfo();
            NetSkipNoClients.RefreshInfo();
        }
    }
}
