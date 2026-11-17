/*
 Si_ServerPerf module "MultiCore": main-thread work moved onto Unity's idle job-worker threads (DEV_REPORT S7 / S12).
 EXPERIMENTAL, every item default OFF (config Items.* = false -> not patched). To measure it as its own arm, set
 Modules.MultiCore.Items.<item> = true in Si_ServerPerf.json before the start; it can then be flipped at runtime
 (runtime.json "MultiCore.*": false/true) any number of times inside one round.

 ===================================================================================================== THREADING MODEL
 1. Workers = Unity's own job-worker threads (7 on the lab box, ~10 % busy), via managed (non-Burst) IJob structs that
    Mono runs there. No thread of ours exists. Chosen over an own pool because the consumer of the result is a vanilla
    Burst job: a JobHandle dependency lets it wait on the worker instead of the main thread, and Unity's Complete()
    executes a job that no worker has picked up inline, so a busy pool degrades to the vanilla cost, never to a stall.
 2. Snapshot (main thread, plain arrays) -> hand-off -> compute (worker) -> take-back (main thread), per slot. Exactly
    one side owns a slot at any time; the ownership word is Interlocked and asserted on every transition
    (Kernel.cs: FillPipeline.HandOff / RunWorker / TakeBack). The worker writes only its slot's output.
 3. Jobs touch no Unity API and no game object: staged ints in, NativeParallelHashMap writes out. Worker exceptions
    are caught in the job, stored in the slot and raised on the main thread at take-back -> PerfItem.Fail -> vanilla.
 4. Take-back points: the consumer's own sync point in the same frame, the next use of the slot, switch-off
    (OnSwitch(false) completes all synchronously), vanilla's dispose paths, scene load and quit. Nothing of ours is
    in flight when vanilla code runs on the same data.
 5. No main-thread wait of ours on the normal path; every wait that does happen is bounded by the job's own O(n) work
    (no locks, loop bounds fixed at snapshot). Counted as "late" in the status line.

 Items
   SensorTeamMaps_Job   S7: per-team detected-target index map of the sensor pipeline filled on a worker (exact,
                        same frame). WORKS, DOES NOT PAY: lab AI round 2026-11-16 (0-317 units) showed no gain; the
                        main thread paid 3.5 ms/s (snapshot + apply) to move 2.8 ms/s of worker time. Kept, default off.
                        Details: SensorTeamMaps.cs.
   SensorPipeline_Rate  S7 fixed rate (not threading): the whole sensor pipeline every SensorPipelineIntervalMs instead
                        of every frame; APPROXIMATE (detection / friendly-fire data up to one interval older). Details,
                        readers and worst-case latencies: SensorRate.cs.
   ParallelScan         the behaviour-tree full target scans of a frame (TargetFinder.CollectDetectedEnemies) predicted
                        at the start of AIBaseAgent.OrderedUpdateAllAgents, computed on workers from a per-frame
                        plain-data snapshot, served when vanilla calls (exact: validated, in-loop changes corrected,
                        type-value cache merged; otherwise the synchronous path). Not measured in a game yet. Details:
                        ParallelScan.cs (frame protocol, why it pays), ScanKernel.cs (worker + serve logic).
                        Note on candidate 3 below: that estimate was for a scan the grid already prunes; in a dense
                        melee (no pruning, D ~400, 900 scans/s) the snapshot is per frame and the work per scan.

 Status line (perf status / runtime_state.json, refreshed every StatusIntervalSeconds): team-frames/s by path,
 entries/s, worker ms/s (work moved), main snapshot + apply ms/s (what it costs), chained / unchained sensor jobs,
 late completions, mean staleness in frames, self-check buckets, and - with the self-check on - the vanilla cost per
 entry measured on the scratch map, hence est. vanilla ms/s and net main-thread gain = est. vanilla - snapshot - apply.

 =================================================================================== WHY THIS ITEM AND NOT THE OTHERS
 Candidates ranked by main-thread ms/s at ~500 units x fraction that is pure work over snapshotable data, measured
 numbers from Silica_Performance findings F (round C, ms/frame at ~650 units / 25 fps unless noted) and code reading
 of 1.0.17. Baseline = the rest of Si_ServerPerf ON (Targeting grids, Headless fog fixes).
   1 TeamJobNativeContainers.Init (S7)  ~10-12 ms/s (sum of detected lists x ~130 ns, every team every frame; inside
     Sensor.TickSensors self 0.31 ms/frame); ~75 % is hash-map inserts over a cheap snapshot  -> SensorTeamMaps_Job.
   2 DetectedGrid.Build (Targeting's own per-frame cost)  ~8-10 ms/s; positions could come from a TransformAccessArray
     job, but the team arrays are disposed / re-created by Team.DetectTarget / LostTarget / Clear / OnDestroy (4 more
     guard patches), fixed-step builds (AlarmProximity 25 Hz, turret acquisitions) would stay synchronous, and it needs
     a hook inside Targeting.Fresh(). Second candidate, not built.
   3 TargetFinder scans after the grid  ~5-15 ms/s residual, but <20 % pure: CalculateBestTargetScore reads ~15 Unity
     objects per candidate and a call-order-dependent static 32-entry score cache; a per-target snapshot (~0.5 us x
     sum D per frame) costs more than the scoring. Not moved.
   4 Commander-AI RebuildClusters  0.39 ms/frame at high N, single calls to 21 ms; the cost is transform.position inside
     an O(clusters x S^2) loop. Snapshot O(S) is ~10 us, but the consumer is synchronous inside Think: hoisting the
     positions (exact, single thread) removes ~95 %, incremental neighbour counts make it O(S^2) (~0.1 ms). A worker
     adds nothing there; belongs to Services (S9) as a single-thread fix.
   5 TargetGrid fill jobs + `> 50` batch kick  <= 0.07 ms/frame (TargetGrid.Update self time incl. all schedules) ->
     <= 4 ms/s; the fill is already Burst on workers.  6 CheckRiskOfFriendlyFire after the grid  <5 ms/s, body is
     raycasts + Unity objects.  7 Team best-target jobs / avoidance broadphase Complete() deferral (S12)  waits of
     ~1-5 ms/s each; deferral changes BT FinishExecution / avoidance timing.  8 Fog UpdatePixelsJob  ~1 ms/s.
   9 TargetGrid.UpdateDataForJobs  0.40-0.49 ms/frame (25-30 ms/s), the largest S7 item, but 0 % movable (per-target
     Unity property reads); its lever is a lower rate (approximate), not threads.
  10 CreatureTurret.OrderedLateUpdate  0.36-0.59 ms/frame; iterative segment IK over the transform hierarchy - a worker
     copy cannot be bit-exact (native quaternion composition) and drives muzzle aim. Not moved.
 Net: what is both large and pure is small once Targeting and Headless are in. Realistic for MultiCore today: ~1 % of a
 core at 500 units (item 1), ~2 % with item 2. Everything bigger is main-thread-bound Unity object code.
*/

using System;

namespace SiServerPerf.Modules.MultiCore
{
    public sealed class MultiCoreSettings
    {
        // SensorTeamMaps_Job: teams with fewer detected entries than this run vanilla (scheduling a job costs a few us, vanilla ~130 ns per entry).
        public int SensorMapsMinEntries = 128;
        // Self-check: fraction (0..1) of team-frames that also run vanilla on a scratch map and compare. 0 = off.
        public float SelfCheckFraction = 0f;
        // Mismatch lines written to the log per round (all mismatches are counted).
        public int SelfCheckMaxLogsPerRound = 20;
        // SensorPipeline_Rate: pipeline interval in ms of game time (default 50 = 20 Hz; clamped 1..1000).
        public float SensorPipelineIntervalMs = 50f;
        // ParallelScan: number of Unity jobs per frame the predicted scans are striped over (0 = inline on the main
        // thread: same snapshot and kernel, no workers - the arm that separates the data-layout gain from the threads).
        public int ParallelScanJobs = 6;
        // ParallelScan: true = inline as with ParallelScanJobs 0.
        public bool ParallelScanInline = false;
        // ParallelScan: frames with fewer predicted scans than this are not batched (staging has a fixed cost).
        public int ParallelScanMinScans = 4;
        // ParallelScan: a served scan rescores at most this many sequence entries (inputs changed after the snapshot,
        // or type values that differ under vanilla's carried-over cache); more -> that call runs synchronously.
        public int ParallelScanMaxDirty = 128;
        // ParallelScan: when vanilla asks for a scan a worker is computing right now, spin up to this long (us) for it
        // (a scan takes ~25-120 us on a worker). A scan no worker has started is computed by the main thread instead.
        public float ParallelScanWaitMicros = 150f;
        // ParallelScan: no batch when the previous frame was shorter than this (ms); 0 = always batch. E.g. 25 = only
        // below ~40 fps.
        public float ParallelScanMinFrameMs = 0f;
        // ParallelScan: an agent whose predictions were not requested this many times in a row is only probed every
        // ParallelScanProbeEvery-th due tick (0 = no history gate).
        public int ParallelScanSkipAfterWasted = 2;
        public int ParallelScanProbeEvery = 4;
        // Item Info (status / runtime_state.json) refresh interval, seconds of real time.
        public float StatusIntervalSeconds = 5f;
    }

    [PerfModuleOrder(40)]
    public sealed class MultiCoreModule : IPerfModule
    {
        public string Name => "MultiCore";
        public string Description => "S7/S12: main-thread work on Unity's job workers: target scans on workers (ParallelScan, default on since 0.2.1); experimental and default off: sensor team maps job, sensor pipeline at a fixed rate";

        static MultiCoreSettings S;
        static float s_NextStatus, s_LastStatus;

        public void Apply(ModuleContext ctx)
        {
            S = ctx.Settings<MultiCoreSettings>();
            SensorTeamMaps.Register(ctx, S);
            SensorRate.Register(ctx, S);
            ParallelScan.Register(ctx, S);

            ctx.OnUpdate(StatusTick);
            ctx.OnSceneLoaded(_ => { SensorTeamMaps.DropAll(); ParallelScan.OnRoundOrScene(); });
            ctx.OnQuit(() => { SensorTeamMaps.DropAll(); ParallelScan.Abort(); });
            ctx.OnRoundStart(() => { SensorTeamMaps.ResetRoundLogs(); ParallelScan.OnRoundOrScene(); });
            ctx.OnRoundEnd(ParallelScan.OnRoundOrScene);
        }

        static void StatusTick()
        {
            float now = Perf.RealTime;
            if (now < s_NextStatus) return;
            float dt = s_LastStatus > 0f ? now - s_LastStatus : 0f;
            s_LastStatus = now;
            s_NextStatus = now + Math.Max(0.5f, S.StatusIntervalSeconds);
            if (dt <= 0f) return;
            SensorTeamMaps.Publish(dt);
            SensorRate.Publish(dt);
            ParallelScan.Publish(dt);
        }
    }
}
