/*
 ParallelScan + ParallelScan_Prefiltered (EXPERIMENTAL, exact, default off): the AI target scans of the behaviour-tree
 ticks are computed on Unity's job workers from a per-frame plain-data snapshot and served to vanilla when it makes the
 call. Kernel + serve logic: ScanKernel.cs; offline tests: Tests\ParallelScanTests.csproj.

   ParallelScan               TargetFinder.CollectDetectedEnemies (mode DetectedEnemies): the full scans of agents without
                              an aim target, inside the agent loop (BTEnemyTargetDetected.Execute -> FindBestTarget).
   ParallelScan_Prefiltered   TargetFinder.CollectPreFilteredEnemies (mode PreFilteredEnemies): the async re-evaluation
                              scans in the finish phase (BTEnemyTargetDetected.FinishExecution -> FindBestTargetAsyncFinish
                              -> Unit.FinishGetBestTarget). Despite the name these are full-size scans too: the team's
                              Burst jobs (DistanceToScoreTeamJob + BumpDownTeamJob) only compact the detected targets
                              within range, every one of them is scored on the main thread. Needs ParallelScan patched
                              (shares its frame batch, snapshot, workers and dirty tracking); either can be on alone.

 b17 (2026-11-17, ParallelScan alone, @550): CollectDetectedEnemies 54.0 -> 9.8 ms/s, frame work -16 %, 19 571 self-checks
 identical. Round 2 (this version) changes, see the hand-back report for the reasoning and expected effects:
   - dirty tracking marks only REAL changes (prefix/postfix value compare). Before, every SetAim(sameTarget) /
     SetAim(null) of a BT tick marked the agent dirty -> tooManyDirty grew with load.
   - corrections compare the live inputs with the snapshot first and rescore only what differs; budget = rescored
     entries per scan (ParallelScanMaxDirty), not dirty entries per team.
   - work claiming: a scan no worker has started is computed by the main thread on the snapshot when vanilla asks
     (cheaper than the synchronous path); a wait happens only for a scan a worker is running.
   - per-agent waste history: an agent whose predictions were not requested twice in a row is only probed every
     ParallelScanProbeEvery-th due tick; wasted counted by unit kind and behaviour tree.
   - targets without ObjectInfo and structures with more than 4 turret targets are scored (vanilla's uncached branch,
     a turret-target pool) instead of aborting the scan ("unsupported" in b17_pscan_inline).
   - carry-over refusals (carryValues / carryOverflow) are served through EmulateTypeValues (exact replay of
     vanilla's 32-entry cache on the main thread + rescoring of the entries whose values differ).
   - adaptive skip: no batch when the last frame was shorter than ParallelScanMinFrameMs (0 = always batch).

 Frame protocol (all inside AIBaseAgent.OrderedUpdateAllAgents, 1.0.17 Silica.AI/AIBaseAgent.cs:544-600):
   PREFIX            predict the agents whose tick is due and that will full-scan (m_BTEnabled, m_BTUpdateDelay - dt <= 0,
                     no aim target, no visible assigned target, no throttled cached target), dry-run their own
                     Unit.GetBestTarget() with RequestScan / get_BestTarget short-circuited (records finder + params,
                     changes nothing), stage the search teams, build the type tables, hand off to ParallelScanJobs jobs.
   SERVE (loop)      prefix on CollectDetectedEnemies, Priority.First (before Targeting's prefixes, which see filled
                     lists and step aside): validate params / list / scanner / config, claim-or-wait, type-value merge
                     (or emulation), Finalize (corrections + replay), fill m_Candidates / m_Scores, commit vanilla's
                     type-value state, skip the original. Anything else: the synchronous path.
   FINISH PHASE      first BTEnemyTargetDetected.FinishExecution of the frame: complete the loop's jobs (their
                     unconsumed captures die), predict every NodesInProgress entry that will call FinishGetBestTarget
                     (m_BestTargetUsedCache false), dry-run it, copy each query's Burst buffer slice, restage teams whose
                     list changed, second hand-off. Served in the CollectPreFilteredEnemies prefix (Priority.First; a
                     main-thread replay item of Targeting, if present, sees the filled lists and steps aside), with the
                     Burst buffer validated unchanged (handledQueriesCount, buffer stride / length, query count).
   FINALIZER         every job completed, waste history and counters, batch closed.
 Dirty tracking (while a batch is open): Target.AddAttacker / RemoveAttacker (AttackedByEnemyNum, Target.cs:439-457),
 AIAiming.set_AimTarget / PauseAim / ResetAimNavParams (Unit.CurrentTarget = AimTarget), DamageManager.set_Health; each
 marks an entry only when the observed value changed. The finish phase's own picks (SetAim between finishes) are such
 changes: later prefiltered scans get them corrected.
 Assumed constant between snapshot and call (no writer inside OrderedUpdateAllAgents in 1.0.17; the self-check
 measures it): poses, team membership / alliances, IsFunctional, IsFlying, TopSpeed, ObjectInfo fields, structure
 turret targets, DamageManager modifiers per (ObjectInfo, size).

 Self-check (MultiCore.Settings.SelfCheckFraction > 0), both modes: a sampled served call writes its result to scratch
 lists and lets the synchronous path run into the real ones; the postfix compares count, order, score bits and the
 type-value cache state.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using BehaviorTrees;
using HarmonyLib;
using MelonLoader;
using Silica.AI;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace SiServerPerf.Modules.MultiCore
{
    internal static class ParallelScan
    {
        internal static bool On;        // ParallelScan (full scans)
        internal static bool PrefOn;    // ParallelScan_Prefiltered
        internal static PerfItem Item, PrefItem;

        const int ModeFull = 0, ModePref = 1;

        // ---- settings
        static int s_Jobs = 6;
        static bool s_Inline;
        static int s_MinScans = 4;
        static int s_MaxRescore = 128;
        static int s_MaxLogs = 20;
        static long s_WaitTicks;
        static float s_MinFrameMs;
        static int s_SkipAfterWasted = 2;
        static int s_ProbeEvery = 4;
        static Sampler s_Sampler;

        // ---- reflection
        static AccessTools.FieldRef<AIBaseAgent, float> s_BtDelay;
        static AccessTools.FieldRef<AIBaseAgent, bool> s_BtEnabled;
        static AccessTools.FieldRef<AIAiming, bool> s_UsedCache;
        static Func<AIAiming, Target> s_Throttled;
        static AccessTools.FieldRef<TargetFinder, List<Target>> s_Cands;
        static AccessTools.FieldRef<TargetFinder, List<float>> s_Scores;
        static AccessTools.FieldRef<TargetFinder, List<LineOfSightQueryHandle>> s_Handles;
        static AccessTools.FieldRef<List<Target>, int> s_ListVersion;
        static AccessTools.FieldRef<bool> s_CfgResolved;
        static AccessTools.FieldRef<float>[] s_Cfg;
        static AccessTools.FieldRef<int[]> s_TvIds;
        static AccessTools.FieldRef<float[]> s_TvTo, s_TvUs, s_TvMod;
        static AccessTools.FieldRef<int> s_TvNum, s_TvFrom;
        static AccessTools.FieldRef<float> s_TvDamage;
        static AccessTools.FieldRef<EDamageType> s_TvType;
        static Func<int, int, float> s_Jitter;
        static AccessTools.FieldRef<Dictionary<ObjectInfo, DamageCalculatorEntry>> s_DcEntries;
        static AccessTools.FieldRef<DamageCalculatorEntry, Dictionary<ObjectInfo, float>> s_DcPrim, s_DcSec;
        static AccessTools.FieldRef<Team, JobHandle> s_BumpDown;
        static AccessTools.FieldRef<Team, NativeArray<DistanceScoreContainer>> s_ScoreBuf;
        static AccessTools.FieldRef<Team, int> s_BufStride, s_BufHandled;

        static readonly string[] CfgNames =
        {
            "TargetScore_AimDirection", "TargetScore_AlreadyOurTarget", "TargetScore_Critical", "TargetScore_DPSToTarget",
            "TargetScore_DPSToUs", "TargetScore_DamageToHealth", "TargetScore_DamageToUs", "TargetScore_Distance",
            "TargetScore_Functional", "TargetScore_JitterPct", "TargetScore_Saturation", "TargetScore_SaturationSeconds",
            "TargetScore_SelfDefence", "TargetScore_SelfDefenceContactSeconds", "TargetScore_SelfDefenceThreatSeconds",
            "TargetScore_SelfDefenceKillSeconds", "TargetScore_SelfDefenceMemorySeconds", "TargetScore_SelfDefenceCloseRange",
            "TargetScore_Speed", "TargetScore_Unit", "TargetScore_WeAreTarget", "TargetScore_WithinView",
            "TargetValueBase_DPS", "TargetValueBase_Speed", "TargetValueBase_WorthMultiplier"
        };

        // ---- frame state (main thread only, except what the jobs read: the claimed ScanJob and the staged team data)
        static bool s_Active;           // batch open: between the OrderedUpdateAllAgents prefix and its finalizer
        static bool s_InDryRun;
        static int s_DryMode;
        static bool s_PrefDone;         // the finish-phase batch of this frame was built
        static int s_Frame = -1;
        static int s_MainThread = -1;
        static readonly ScoreCfg s_CfgNow = new ScoreCfg();

        sealed class Capture
        {
            public readonly ScanJob Job = new ScanJob();
            public int Mode;
            public TargetFinder Finder;
            public TargetScanParams P;
            public Target From;
            public ObjectInfo FromOi;
            public TeamStage Stage;
            public AIBaseAgent Agent;
            public int Kind;            // 0 soldier, 1 vehicle, 2 creature, 3 other
            public int Tree;
            public bool Consumed;
            public bool Dead;           // its batch was closed before vanilla asked
            // prefiltered: Burst buffer state at the copy
            public int BufHandled, BufStride, BufLen, BufQueries;
        }

        static Capture[] s_Scans = new Capture[64];
        static int s_ScanCount;
        static readonly Dictionary<TargetFinder, int>[] s_ByFinder = { new Dictionary<TargetFinder, int>(128), new Dictionary<TargetFinder, int>(128) };
        static JobHandle[] s_Jh = new JobHandle[16];
        static int s_HandleCount;
        static AIBaseAgent s_DryAgent;

        struct ScanBatchJob : IJob
        {
            public int Start, End, Stride, Offset;
            public void Execute() { RunJob(Start + Offset, End, Stride); }
        }

        // WORKER (or main in inline mode). Every worker starts on the earliest scans of the loop order; a scan the main
        // thread already claimed is skipped.
        static void RunJob(int first, int end, int stride)
        {
            Capture[] scans = s_Scans;
            for (int k = first; k < end; k += stride)
            {
                ScanJob j = scans[k].Job;
                if (ScanKernel.TryClaim(j, ScanJob.ClaimWorker)) ScanKernel.Run(j);   // Run never throws
            }
        }

        // ---- per search team snapshot
        sealed class TeamStage
        {
            public Team Team;
            public readonly ScanTeamData D = new ScanTeamData();
            public int Cap;
            public Target[] Ref = new Target[0];
            public BaseGameObject[] Owner = new BaseGameObject[0];
            public Transform[] Tr = new Transform[0];
            public ObjectInfo[] Oi = new ObjectInfo[0];
            public DamageManager[] Dm = new DamageManager[0];
            public Unit[] U = new Unit[0];
            public Structure[] St = new Structure[0];
            public int[] Const = new int[0];          // SF.OiOk | DmOk | UnitOk | Structure | StructOrSite
            public bool[] DirtyMark = new bool[0];
            public int[] DirtyList = new int[16];
            public int NDirty;
            public List<Target> List;
            public int Version;
            public int UsedFrame = -1;
            public readonly Dictionary<ObjectInfo, int> TypeSlots = new Dictionary<ObjectInfo, int>(64);
            public readonly List<ObjectInfo> TypeOi = new List<ObjectInfo>(64);
            public readonly Dictionary<long, int> GroupSlots = new Dictionary<long, int>(64);
            public readonly List<long> GroupKey = new List<long>(64);
            public DamageManager[] GroupRep = new DamageManager[64];
            public int[] GroupRepFrame = new int[64];
            public readonly List<long> KeyA = new List<long>(16);
            public readonly List<long> KeyB = new List<long>(16);
            public readonly List<ScanTables> KeyTables = new List<ScanTables>(16);
            public readonly Team[] MemoTeam = new Team[16];
            public readonly bool[] MemoEnemy = new bool[16];
            public int Memo;

            public void Grow(int n)
            {
                D.Grow(n);
                if (n <= Cap) return;
                int c = TeamMapKernel.NextPow2(Math.Max(n, 64));
                Array.Resize(ref Ref, c); Array.Resize(ref Owner, c); Array.Resize(ref Tr, c); Array.Resize(ref Oi, c);
                Array.Resize(ref Dm, c); Array.Resize(ref U, c); Array.Resize(ref St, c); Array.Resize(ref Const, c);
                Array.Resize(ref DirtyMark, c);
                Cap = c;
            }

            public void ClearDirty()
            {
                for (int q = 0; q < NDirty; q++) DirtyMark[DirtyList[q]] = false;
                NDirty = 0;
            }

            public void ClearRefs()
            {
                Array.Clear(Ref, 0, Cap); Array.Clear(Owner, 0, Cap); Array.Clear(Tr, 0, Cap); Array.Clear(Oi, 0, Cap);
                Array.Clear(Dm, 0, Cap); Array.Clear(U, 0, Cap); Array.Clear(St, 0, Cap);
                Array.Clear(GroupRep, 0, GroupRep.Length);
                TypeSlots.Clear(); TypeOi.Clear(); GroupSlots.Clear(); GroupKey.Clear();
                List = null;
            }
        }

        static TeamStage[] s_Stages = new TeamStage[8];
        static int s_StageCount;
        static readonly List<ScanTables> s_TablePool = new List<ScanTables>(32);
        static int s_TablesUsed;

        // ---- persistent value caches (cleared on round / scene)
        static readonly Dictionary<long, float> s_DpsCache = new Dictionary<long, float>(1024);
        static Dictionary<ObjectInfo, DamageCalculatorEntry> s_DcSeen;
        static int s_DcSeenCount;
        struct ModKey : IEquatable<ModKey>
        {
            public long Group; public int DamageBits; public int Type;
            public bool Equals(ModKey o) => Group == o.Group && DamageBits == o.DamageBits && Type == o.Type;
            public override bool Equals(object o) => o is ModKey k && Equals(k);
            public override int GetHashCode() => (int)Group * 397 ^ (int)(Group >> 32) * 31 ^ DamageBits * 17 ^ Type;
        }
        static readonly Dictionary<ModKey, float> s_ModCache = new Dictionary<ModKey, float>(1024);

        // ---- waste history per agent and mode
        sealed class Hist
        {
            public readonly int[] Wasted = new int[2];
            public readonly int[] Probe = new int[2];
            public readonly int[] ServedFrame = { -1, -1 };
            public readonly int[] WasteFrame = { -1, -1 };
        }
        sealed class RefEq : IEqualityComparer<AIBaseAgent>
        {
            public bool Equals(AIBaseAgent a, AIBaseAgent b) => ReferenceEquals(a, b);
            public int GetHashCode(AIBaseAgent a) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a);
        }
        static readonly Dictionary<AIBaseAgent, Hist> s_Hist = new Dictionary<AIBaseAgent, Hist>(1024, new RefEq());

        // ---- serve scratch (main)
        static readonly TvState s_LiveTv = new TvState();
        static readonly TvState s_ExpectTv = new TvState();
        static readonly FinalizeScratch s_Fx = new FinalizeScratch();
        static readonly DirtyLive s_Live = new DirtyLive();
        static readonly int[] s_CtIds = new int[256];
        static readonly List<Target> s_ChkCands = new List<Target>(24);
        static readonly List<float> s_ChkScores = new List<float>(24);
        static bool s_ChkTvValid;

        internal struct ServeState
        {
            public bool Sampled;
            public int Scan;
        }

        // ---- counters: per mode, named slots; published as rates
        static class C
        {
            public const int Predicted = 0, Captured = 1, Served = 2, Corrected = 3, Emulated = 4, Rescored = 5, MissedNoCapture = 6,
                MissedConsumed = 7, Wasted = 8, Stolen = 9, Waits = 10, WaitTimeouts = 11, HistSkips = 12, Probes = 13, Checks = 14,
                CheckSame = 15, CheckDiff = 16, CheckTvDiff = 17, DiffSlots = 18, WastedSoldier = 19, WastedVehicle = 20,
                WastedCreature = 21, WastedOther = 22, DryErrors = 23,
                TServe = 24, TSteal = 25, TWait = 26, TStage = 27, TDry = 28, TWorker = 29, TOnMain = 30, OnMain = 31, Batches = 32,
                TCheck = 33, Count = 34;
        }
        sealed class Stats
        {
            public readonly long[] V = new long[C.Count];
            public readonly long[] P = new long[C.Count];
            public readonly long[] Reason = new long[ScanReason.Count];
            public readonly long[] PReason = new long[ScanReason.Count];
            public readonly long[] WastedTree = new long[64];
            public double MaxRel, SumRel;
        }
        static readonly Stats[] s_St = { new Stats(), new Stats() };
        // shared (frame-level) counters
        static long g_Frames, g_SkippedFew, g_SkippedFast, g_SkippedCfg, g_Entries, g_NoOi, g_TurretHeavy, g_Poison, g_TStage, g_TTakeBack;
        static long g_MarkAtt, g_MarkCt, g_MarkHealth;
        static long pg_Frames, pg_SkippedFew, pg_SkippedFast, pg_SkippedCfg, pg_Entries, pg_NoOi, pg_TurretHeavy, pg_Poison, pg_TStage, pg_TTakeBack;
        static long pg_MarkAtt, pg_MarkCt, pg_MarkHealth;
        static int s_Logged;
        static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        // ================================================================== registration

        public static void Register(ModuleContext ctx, MultiCoreSettings s)
        {
            s_Jobs = Math.Max(0, Math.Min(32, s.ParallelScanJobs));
            s_Inline = s.ParallelScanInline || s_Jobs == 0;
            s_MinScans = Math.Max(1, s.ParallelScanMinScans);
            s_MaxRescore = Math.Max(0, s.ParallelScanMaxDirty);
            s_MaxLogs = Math.Max(0, s.SelfCheckMaxLogsPerRound);
            s_WaitTicks = (long)(Math.Max(0f, Math.Min(2000f, s.ParallelScanWaitMicros)) * 1e-6 * Stopwatch.Frequency);
            s_MinFrameMs = Math.Max(0f, s.ParallelScanMinFrameMs);
            s_SkipAfterWasted = Math.Max(0, s.ParallelScanSkipAfterWasted);
            s_ProbeEvery = Math.Max(1, s.ParallelScanProbeEvery);
            s_Sampler.Init(s.SelfCheckFraction, 0x9E3779B9u);

            Item = ctx.Item("ParallelScan",
                "the AI full target scans of a frame (TargetFinder.CollectDetectedEnemies from behaviour-tree ticks) are predicted at the start of AIBaseAgent.OrderedUpdateAllAgents, computed on Unity job workers from a plain-data snapshot and served when vanilla calls (exact: params / list / scanner validated, in-loop changes corrected, type-value cache merged or replayed; anything else runs the synchronous path)",
                v => On = v, defaultOn: true);       // on since 0.2.1
            PrefItem = ctx.Item("ParallelScan_Prefiltered",
                "(needs ParallelScan patched) the finish-phase re-evaluation scans (TargetFinder.CollectPreFilteredEnemies after the team Burst filter) on the same snapshot and workers, served exactly the same way",
                v => PrefOn = v, defaultOn: true);   // on since 0.2.1

            Type ai = typeof(GameAI);
            s_BtDelay = Refl.Field<AIBaseAgent, float>("m_BTUpdateDelay", Item);
            s_BtEnabled = Refl.Field<AIBaseAgent, bool>("m_BTEnabled", Item);
            s_Cands = Refl.Field<TargetFinder, List<Target>>("m_Candidates", Item);
            s_Scores = Refl.Field<TargetFinder, List<float>>("m_Scores", Item);
            s_Handles = Refl.Field<TargetFinder, List<LineOfSightQueryHandle>>("m_Handles", Item);
            s_ListVersion = Refl.Field<List<Target>, int>("_version", Item);
            s_CfgResolved = Refl.StaticField<bool>(ai, "s_CfgResolved", Item);
            s_TvIds = Refl.StaticField<int[]>(ai, "s_TypeValues_ObjectInfoID", Item);
            s_TvTo = Refl.StaticField<float[]>(ai, "s_TypeValues_DPSToTarget", Item);
            s_TvUs = Refl.StaticField<float[]>(ai, "s_TypeValues_DPSToUs", Item);
            s_TvMod = Refl.StaticField<float[]>(ai, "s_TypeValues_ModulatedDamage", Item);
            s_TvNum = Refl.StaticField<int>(ai, "s_TypeValues_Num", Item);
            s_TvFrom = Refl.StaticField<int>(ai, "s_TypeValues_FromObjectInfoID", Item);
            s_TvDamage = Refl.StaticField<float>(ai, "s_TypeValues_Damage", Item);
            s_TvType = Refl.StaticField<EDamageType>(ai, "s_TypeValues_DamageType", Item);
            s_DcEntries = Refl.StaticField<Dictionary<ObjectInfo, DamageCalculatorEntry>>(typeof(DamageCalculator), "DamageCalculatorEntries", Item);
            s_DcPrim = Refl.Field<DamageCalculatorEntry, Dictionary<ObjectInfo, float>>("MaxDamagePerSecondToObjectsPrimary", Item);
            s_DcSec = Refl.Field<DamageCalculatorEntry, Dictionary<ObjectInfo, float>>("MaxDamagePerSecondToObjectsSecondary", Item);
            s_Cfg = new AccessTools.FieldRef<float>[ScoreCfg.Count];
            bool cfgOk = true;
            for (int i = 0; i < ScoreCfg.Count; i++)
            {
                s_Cfg[i] = Refl.StaticField<float>(ai, "s_Cfg_" + CfgNames[i], Item);
                cfgOk &= s_Cfg[i] != null;
            }
            MethodInfo throttled = Refl.Method(typeof(AIAiming), "GetThrottledBestTarget", Type.EmptyTypes, Item);
            MethodInfo jitter = Refl.Method(ai, "GetTargetScoreJitter01", new[] { typeof(int), typeof(int) }, Item);
            try
            {
                if (throttled != null) s_Throttled = AccessTools.MethodDelegate<Func<AIAiming, Target>>(throttled);
                if (jitter != null) s_Jitter = AccessTools.MethodDelegate<Func<int, int, float>>(jitter);
            }
            catch (Exception ex) { Item.Note("delegates: " + ex.Message); }
            s_CfgNow.JitterFn = s_Jitter;
            if (s_BtDelay == null || s_BtEnabled == null || s_Cands == null || s_Scores == null || s_Handles == null || s_ListVersion == null ||
                s_CfgResolved == null || s_TvIds == null || s_TvTo == null || s_TvUs == null || s_TvMod == null || s_TvNum == null ||
                s_TvFrom == null || s_TvDamage == null || s_TvType == null || s_DcEntries == null || s_DcPrim == null || s_DcSec == null ||
                !cfgOk || s_Throttled == null || s_Jitter == null)
            {
                Item.Block("a required private member (agent tick, TargetFinder, GameAI scoring statics, DamageCalculator caches) is absent on this build");
            }
            s_UsedCache = Refl.Field<AIAiming, bool>("m_BestTargetUsedCache", PrefItem);
            s_BumpDown = Refl.Field<Team, JobHandle>("bumpDownHandle", PrefItem);
            s_ScoreBuf = Refl.Field<Team, NativeArray<DistanceScoreContainer>>("m_perTeamScoreBuffer", PrefItem);
            s_BufStride = Refl.Field<Team, int>("m_scoreBufferTargetLength", PrefItem);
            s_BufHandled = Refl.Field<Team, int>("handledQueriesCount", PrefItem);
            if (s_UsedCache == null || s_BumpDown == null || s_ScoreBuf == null || s_BufStride == null || s_BufHandled == null)
            {
                PrefItem.Block("team Burst filter fields / AIAiming.m_BestTargetUsedCache absent on this build");
            }

            var all = Item.Target(typeof(AIBaseAgent), nameof(AIBaseAgent.OrderedUpdateAllAgents), new[] { typeof(float) }, declaredOnly: true);
            var req = Item.Target(typeof(TargetFinder), nameof(TargetFinder.RequestScan), new[] { typeof(TargetScanParams).MakeByRefType() }, declaredOnly: true);
            var best = Item.Target(AccessTools.PropertyGetter(typeof(TargetFinder), nameof(TargetFinder.BestTarget)), "TargetFinder.get_BestTarget");
            var collect = Item.Target(typeof(TargetFinder), "CollectDetectedEnemies", new[] { typeof(TargetScanParams).MakeByRefType() }, declaredOnly: true);
            var addAtt = Item.Target(typeof(Target), nameof(Target.AddAttacker), new[] { typeof(Target) }, declaredOnly: true);
            var remAtt = Item.Target(typeof(Target), nameof(Target.RemoveAttacker), new[] { typeof(Target) }, declaredOnly: true);
            var setAim = Item.Target(AccessTools.PropertySetter(typeof(AIAiming), nameof(AIAiming.AimTarget)), "AIAiming.set_AimTarget");
            var pause = Item.Target(typeof(AIAiming), nameof(AIAiming.PauseAim), new[] { typeof(bool) }, declaredOnly: true);
            var reset = Item.Target(typeof(AIAiming), "ResetAimNavParams", new[] { typeof(Vector3).MakeByRefType() }, declaredOnly: true);
            var health = Item.Target(AccessTools.PropertySetter(typeof(DamageManager), nameof(DamageManager.Health)), "DamageManager.set_Health");
            if (all == null || req == null || best == null || collect == null || addAtt == null || remAtt == null || setAim == null ||
                pause == null || reset == null || health == null)
            {
                Item.Block("a required member is absent on this build");
            }
            var finish = PrefItem.Target(typeof(BTEnemyTargetDetected), nameof(BTEnemyTargetDetected.FinishExecution),
                new[] { typeof(int), typeof(AIBaseAgent), typeof(BTNode.WorkCompletedDelegate) }, declaredOnly: true);
            var collectPre = PrefItem.Target(typeof(TargetFinder), "CollectPreFilteredEnemies", new[] { typeof(TargetScanParams).MakeByRefType() }, declaredOnly: true);
            if (finish == null || collectPre == null) PrefItem.Block("BTEnemyTargetDetected.FinishExecution / TargetFinder.CollectPreFilteredEnemies absent on this build");

            // Order: dirty trackers + dry-run guards, serve prefix, then the producer; unless all are in, no producer.
            bool ok = Item.Patch(addAtt, typeof(ParallelScan), prefix: nameof(AttackerPrefix), postfix: nameof(AttackerPostfix));
            ok &= Item.Patch(remAtt, typeof(ParallelScan), prefix: nameof(AttackerPrefix), postfix: nameof(AttackerPostfix));
            ok &= Item.Patch(setAim, typeof(ParallelScan), prefix: nameof(AimPrefix), postfix: nameof(AimPostfix));
            ok &= Item.Patch(pause, typeof(ParallelScan), prefix: nameof(AimPrefix), postfix: nameof(AimPostfix));
            ok &= Item.Patch(reset, typeof(ParallelScan), prefix: nameof(AimPrefix), postfix: nameof(AimPostfix));
            ok &= Item.Patch(health, typeof(ParallelScan), prefix: nameof(HealthPrefix), postfix: nameof(HealthPostfix));
            ok &= Item.Patch(req, typeof(ParallelScan), prefix: nameof(DryRequestScan), priority: Priority.First);
            ok &= Item.Patch(best, typeof(ParallelScan), prefix: nameof(DryBestTarget), priority: Priority.First);
            ok &= Item.Patch(collect, typeof(ParallelScan), prefix: nameof(ServeFull), postfix: nameof(ServePostfix), priority: Priority.First);
            bool producer = ok && Item.Patch(all, typeof(ParallelScan), prefix: nameof(FramePrefix), finalizer: nameof(FrameFinalizer));
            if (!ok && Item.Active) Item.Note("a tracker, dry-run or serve patch is not applied: the per-frame batch is not installed (vanilla runs)");
            if (!producer) PrefItem.Block("needs MultiCore.ParallelScan patched (its frame batch, snapshot and trackers carry this item)");
            bool pok = PrefItem.Patch(collectPre, typeof(ParallelScan), prefix: nameof(ServePref), postfix: nameof(ServePostfix), priority: Priority.First);
            if (pok) PrefItem.Patch(finish, typeof(ParallelScan), prefix: nameof(FinishPrefix));
            Item.OnSwitch(on => { if (!on) Abort(); });
            PrefItem.OnSwitch(on => { if (!on) Abort(); });
            Item.Note("jobs " + (s_Inline ? "0 (inline on the main thread)" : s_Jobs.ToString()) + ", min scans per frame " + s_MinScans +
                      ", rescore budget " + s_MaxRescore + " per scan, wait " + s.ParallelScanWaitMicros.ToString("0", CultureInfo.InvariantCulture) +
                      " us for a running scan, skip after " + s_SkipAfterWasted + " wasted (probe every " + s_ProbeEvery + "), min frame " +
                      s_MinFrameMs.ToString("0.#", CultureInfo.InvariantCulture) + " ms");
            if (s.SelfCheckFraction > 0f) Item.Note("self-check ON: " + (s.SelfCheckFraction * 100f).ToString("0.###") + " % of served calls also run the synchronous path and compare");
        }

        // ================================================================== frame: prefix / finish phase / finalizer

        static void FramePrefix(float dt)
        {
            if (!On && !PrefOn) return;
            if (dt <= 0f || Game.IsLoading) return;   // vanilla returns right away
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (s_Active || s_HandleCount > 0) TakeBackAll();   // previous batch not closed (exception path)
                s_Frame = Time.frameCount;
                s_MainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                s_ScanCount = 0;
                s_ByFinder[0].Clear(); s_ByFinder[1].Clear();
                s_TablesUsed = 0;
                s_PrefDone = false;
                if (!s_CfgResolved()) { g_SkippedCfg++; return; }   // let vanilla resolve the config first
                if (s_MinFrameMs > 0f && Time.unscaledDeltaTime * 1000f < s_MinFrameMs) { g_SkippedFast++; return; }
                for (int i = 0; i < ScoreCfg.Count; i++) s_CfgNow.Set(i, s_Cfg[i]());
                CheckDamageCalculator();
                s_Active = true;   // trackers on from here (the finish-phase batch needs them even without a loop batch)
                if (!On) return;

                long d0 = Stopwatch.GetTimestamp();
                Predict(dt);
                s_St[ModeFull].V[C.TDry] += Stopwatch.GetTimestamp() - d0;
                if (s_ScanCount < s_MinScans)
                {
                    g_SkippedFew++;
                    DropCaptures(0);
                    return;
                }
                for (int k = 0; k < s_ScanCount; k++)
                {
                    TeamStage ts = s_Scans[k].Stage;
                    if (ts.UsedFrame != s_Frame) StageTeam(ts);
                }
                for (int k = 0; k < s_ScanCount; k++) FillJob(s_Scans[k]);
                g_Frames++;
                s_St[ModeFull].V[C.Batches]++;
                Schedule(0, s_ScanCount);
            }
            catch (Exception ex)
            {
                s_InDryRun = false;
                Item.Fail(ex, "AIBaseAgent.OrderedUpdateAllAgents prefix (predict / stage / schedule)");
            }
            finally
            {
                long t = Stopwatch.GetTimestamp() - t0;
                s_St[ModeFull].V[C.TStage] += t;
                g_TStage += t;
            }
        }

        static void Schedule(int start, int end)
        {
            int n = end - start;
            if (n <= 0) return;
            if (s_Inline)
            {
                RunJob(start, end, 1);
                return;
            }
            int jobs = Math.Min(s_Jobs, n);
            if (s_Jh.Length < s_HandleCount + jobs) Array.Resize(ref s_Jh, Math.Max(s_HandleCount + jobs, s_Jh.Length * 2));
            for (int jb = 0; jb < jobs; jb++)
            {
                s_Jh[s_HandleCount] = new ScanBatchJob { Start = start, End = end, Stride = jobs, Offset = jb }.Schedule();
                s_HandleCount++;
            }
            JobHandle.ScheduleBatchedJobs();
        }

        // Completes every job handed off so far (no capture is touched by a worker afterwards).
        static void CompleteHandles()
        {
            try
            {
                for (int i = 0; i < s_HandleCount; i++)
                {
                    s_Jh[i].Complete();
                    s_Jh[i] = default;
                }
            }
            finally
            {
                s_HandleCount = 0;
            }
        }

        // First BTEnemyTargetDetected.FinishExecution of the frame: build the prefiltered batch.
        static void FinishPrefix()
        {
            if (!PrefOn || !s_Active || s_PrefDone) return;
            s_PrefDone = true;
            long t0 = Stopwatch.GetTimestamp();
            Stats st = s_St[ModePref];
            try
            {
                CompleteHandles();   // the loop's jobs: from now on their staged data may be rewritten
                int start = s_ScanCount;
                for (int k = 0; k < start; k++)
                {
                    Capture c = s_Scans[k];
                    if (!c.Consumed) c.Dead = true;
                }
                long d0 = Stopwatch.GetTimestamp();
                PredictPref();
                st.V[C.TDry] += Stopwatch.GetTimestamp() - d0;
                if (s_ScanCount - start < s_MinScans)
                {
                    g_SkippedFew++;
                    for (int k = start; k < s_ScanCount; k++) s_Scans[k].Dead = true;
                    return;
                }
                for (int k = start; k < s_ScanCount; k++)
                {
                    TeamStage ts = s_Scans[k].Stage;
                    if (ts.UsedFrame != s_Frame || !ReferenceEquals(ts.Team.DetectedTargets, ts.List) ||
                        s_ListVersion(ts.Team.DetectedTargets) != ts.Version)
                    {
                        StageTeam(ts);   // restage (the loop's captures of this team are dead)
                    }
                }
                for (int k = start; k < s_ScanCount; k++) FillJob(s_Scans[k]);
                st.V[C.Batches]++;
                Schedule(start, s_ScanCount);
            }
            catch (Exception ex)
            {
                s_InDryRun = false;
                PrefItem.Fail(ex, "BTEnemyTargetDetected.FinishExecution prefix (prefiltered batch)");
            }
            finally
            {
                long t = Stopwatch.GetTimestamp() - t0;
                st.V[C.TStage] += t;
                g_TStage += t;
            }
        }

        static Exception FrameFinalizer(Exception __exception)
        {
            if (s_Active || s_HandleCount > 0)
            {
                try { TakeBackAll(); }
                catch (Exception ex) { Item.Fail(ex, "AIBaseAgent.OrderedUpdateAllAgents finalizer (take-back)"); }
            }
            return __exception;
        }

        // Completes every job, collects worker time and errors, waste history, closes the batch.
        static void TakeBackAll()
        {
            long t0 = Stopwatch.GetTimestamp();
            s_Active = false;
            Exception err = null;
            CompleteHandles();
            for (int k = 0; k < s_ScanCount; k++)
            {
                Capture c = s_Scans[k];
                Stats st = s_St[c.Mode];
                ScanJob j = c.Job;
                if (j.Claim == ScanJob.ClaimWorker || (s_Inline && j.Claim != ScanJob.ClaimNone)) st.V[C.TWorker] += j.WorkTicks;
                if (j.Claim == ScanJob.ClaimWorker && !s_Inline && j.ThreadId == s_MainThread)
                {
                    st.V[C.OnMain]++;            // Unity ran a worker job on the main thread inside a Complete()
                    st.V[C.TOnMain] += j.WorkTicks;
                }
                if (j.Status == ScanJob.Error && err == null) err = j.Error_;
                Hist h = HistOf(c.Agent);
                if (c.Consumed)
                {
                    if (h != null) h.Wasted[c.Mode] = 0;
                }
                else
                {
                    st.V[C.Wasted]++;
                    st.V[C.WastedSoldier + c.Kind]++;
                    st.WastedTree[c.Tree & 63]++;
                    if (h != null && h.ServedFrame[c.Mode] != s_Frame && h.WasteFrame[c.Mode] != s_Frame)
                    {
                        h.WasteFrame[c.Mode] = s_Frame;
                        if (h.Wasted[c.Mode] < 1000) h.Wasted[c.Mode]++;
                    }
                }
            }
            DropCaptures(0);
            for (int i = 0; i < s_StageCount; i++) s_Stages[i].ClearDirty();
            g_TTakeBack += Stopwatch.GetTimestamp() - t0;
            if (err != null) Item.Fail(err, "worker (ScanKernel.Run)");
        }

        static void DropCaptures(int from)
        {
            for (int k = from; k < s_ScanCount; k++)
            {
                Capture c = s_Scans[k];
                c.Finder = null; c.From = null; c.FromOi = null; c.Agent = null; c.P = default; c.Consumed = false; c.Dead = false;
                c.Job.Team = null; c.Job.Tables = null; c.Job.Error_ = null;
            }
            s_ScanCount = from;
            if (from == 0) { s_ByFinder[0].Clear(); s_ByFinder[1].Clear(); }
        }

        internal static void Abort()
        {
            s_InDryRun = false;
            try { TakeBackAll(); }
            catch (Exception ex) { MelonLogger.Error("[MultiCore] ParallelScan take-back failed: " + ex); }
        }

        internal static void OnRoundOrScene()
        {
            Abort();
            for (int i = 0; i < s_StageCount; i++) s_Stages[i].ClearRefs();
            s_StageCount = 0;
            Array.Clear(s_Stages, 0, s_Stages.Length);
            s_DpsCache.Clear();
            s_DcSeen = null;
            s_DcSeenCount = 0;
            s_ModCache.Clear();
            s_Hist.Clear();
            s_Logged = 0;
        }

        // ================================================================== predict + dry run

        static Hist HistOf(AIBaseAgent a)
        {
            if ((object)a == null) return null;
            if (!s_Hist.TryGetValue(a, out Hist h))
            {
                if (s_Hist.Count > 8192) s_Hist.Clear();
                h = new Hist();
                s_Hist.Add(a, h);
            }
            return h;
        }

        // History gate: an agent whose last predictions were not requested is only probed every s_ProbeEvery-th time.
        static bool HistAllows(AIBaseAgent a, int mode)
        {
            if (s_SkipAfterWasted <= 0) return true;
            Hist h = HistOf(a);
            if (h.Wasted[mode] < s_SkipAfterWasted) return true;
            Stats st = s_St[mode];
            if (++h.Probe[mode] % s_ProbeEvery == 0) { st.V[C.Probes]++; return true; }
            st.V[C.HistSkips]++;
            return false;
        }

        static void Predict(float dt)
        {
            List<AIBaseAgent> agents = AIBaseAgent.AIBaseAgents;
            Stats st = s_St[ModeFull];
            s_InDryRun = true;
            s_DryMode = ModeFull;
            try
            {
                for (int i = 0, n = agents.Count; i < n; i++)
                {
                    AIBaseAgent a = agents[i];
                    if (!a || !s_BtEnabled(a)) continue;
                    if (s_BtDelay(a) - dt > 0f) continue;            // ExecuteBehaviorTree: delay -= dt; if (delay > 0) return
                    if (a.CurrentBehaviorTreeType == BTType.None) continue;
                    AIAiming aim = a.AgentAiming;
                    if (!aim || (bool)aim.AimTarget) continue;         // BTEnemyTargetDetected: target2 = AimTarget
                    if ((bool)aim.AssignedAttackTarget && aim.IsAssignedTargetVisible) continue;
                    Unit u = a.Unit;
                    if (!u || u.IsDestroyed) continue;
                    if (!a.isActiveAndEnabled) continue;
                    if ((bool)s_Throttled(aim)) continue;              // FindBestTarget returns the cached target
                    if (!HistAllows(a, ModeFull)) continue;
                    st.V[C.Predicted]++;
                    s_DryAgent = a;
                    try { u.GetBestTarget(); }
                    catch (Exception) { st.V[C.DryErrors]++; }
                }
            }
            finally
            {
                s_InDryRun = false;
                s_DryAgent = null;
            }
        }

        // Every finish of the frame (NodesInProgress, only BTEnemyTargetDetected registers there) whose
        // FindBestTargetAsyncFinish will call Unit.FinishGetBestTarget (m_BestTargetUsedCache false).
        static void PredictPref()
        {
            var nodes = AIBaseAgent.NodesInProgress;
            Stats st = s_St[ModePref];
            s_InDryRun = true;
            s_DryMode = ModePref;
            try
            {
                for (int i = 0, n = nodes.Count; i < n; i++)
                {
                    AIBaseAgent a = nodes[i].Item3;
                    if (!a) continue;
                    AIAiming aim = a.AgentAiming;
                    if (!aim || s_UsedCache(aim)) continue;
                    Unit u = a.Unit;
                    if (!u || u.IsDestroyed) continue;
                    if (!HistAllows(a, ModePref)) continue;
                    st.V[C.Predicted]++;
                    s_DryAgent = a;
                    try { u.FinishGetBestTarget(); }
                    catch (Exception) { st.V[C.DryErrors]++; }
                }
            }
            finally
            {
                s_InDryRun = false;
                s_DryAgent = null;
            }
        }

        // TargetFinder.RequestScan during a dry run: record what vanilla would scan, do nothing.
        static bool DryRequestScan(TargetFinder __instance, ref TargetScanParams scanParams)
        {
            if (!s_InDryRun) return true;
            try
            {
                ETargetScanMode want = s_DryMode == ModeFull ? ETargetScanMode.DetectedEnemies : ETargetScanMode.PreFilteredEnemies;
                if (scanParams.Mode != want || !scanParams.SearchTeam) return false;
                if ((bool)scanParams.Aiming && scanParams.Aiming.HasUnshootableTargets) return false;   // served only without unshootable marks
                if (!WouldScan(__instance) || s_ByFinder[s_DryMode].ContainsKey(__instance)) return false;
                CaptureScan(__instance, ref scanParams, s_DryMode);
            }
            catch (Exception)
            {
                s_St[s_DryMode].V[C.DryErrors]++;
            }
            return false;
        }

        static bool DryBestTarget(ref Target __result)
        {
            if (!s_InDryRun) return true;
            __result = null;
            return false;
        }

        // Would vanilla's RequestScan reach CollectCandidates now? Not while a LOS query of the previous scan is still
        // pending (RequestScan returns early). Touch-free (IsPending). A wrong guess only wastes worker time.
        static bool WouldScan(TargetFinder f)
        {
            List<LineOfSightQueryHandle> h = s_Handles(f);
            for (int i = 0, n = h.Count; i < n; i++)
            {
                LineOfSightQueryHandle q = h[i];
                if (q != null && q.IsPending) return false;
            }
            return true;
        }

        static int KindOf(Unit u)
        {
            if (u is Soldier) return 0;
            if (u is Vehicle) return 1;
            if (u is CreatureDecapod) return 2;
            return 3;
        }

        static void CaptureScan(TargetFinder finder, ref TargetScanParams p, int mode)
        {
            if (s_ScanCount == s_Scans.Length) Array.Resize(ref s_Scans, s_Scans.Length * 2);
            Capture c = s_Scans[s_ScanCount] ?? (s_Scans[s_ScanCount] = new Capture());
            ScanJob j = c.Job;
            j.Pref = mode == ModePref;
            if (j.Pref)
            {
                // the Burst filter buffer slice vanilla will read (Team.GetBestTargetScores completes the filter job, as
                // vanilla's own call does a moment later); copied up to the first score < 0
                Team team = p.SearchTeam;
                NativeArray<DistanceScoreContainer> sub = team.GetBestTargetScores(p.FilterQueryIndex);
                int len = sub.Length;
                j.EnsurePref(len);
                int m = 0;
                for (int i = 0; i < len; i++)
                {
                    DistanceScoreContainer e = sub[i];
                    if (e.score < 0f) break;
                    j.PIdx[m] = e.index;
                    j.PScore[m] = e.score;
                    m++;
                }
                j.PN = m;
                c.BufHandled = s_BufHandled(team);
                c.BufStride = s_BufStride(team);
                c.BufLen = s_ScoreBuf(team).Length;
                c.BufQueries = team.PerTeamQueries.IsCreated ? team.PerTeamQueries.Length : -1;
            }
            c.Mode = mode;
            c.Finder = finder;
            c.P = p;
            c.Consumed = false;
            c.Dead = false;
            c.Agent = s_DryAgent;
            c.Kind = (object)s_DryAgent != null && (bool)s_DryAgent.Unit ? KindOf(s_DryAgent.Unit) : 3;
            c.Tree = (object)s_DryAgent != null ? (int)s_DryAgent.CurrentBehaviorTreeType : 0;
            c.Stage = StageFor(p.SearchTeam);
            Target from = p.LineOfSightFromTarget;
            j.FromOk = (bool)from;
            c.From = j.FromOk ? from : null;
            ObjectInfo foi = j.FromOk ? from.ObjectInfo : null;
            bool foiOk = (bool)foi;
            j.FromOiOk = foiOk;
            c.FromOi = foiOk ? foi : null;
            j.FromOiId = foiOk ? foi.GetInstanceID() : 0;
            j.FromId = j.FromOk ? from.GetInstanceID() : 0;
            j.FromPos = j.FromOk ? from.transform.position : default;
            j.FromPhysR = j.FromOk ? from.PhysicalRadius : 0f;
            j.FromHealth = FromHealth(from);
            for (int i = 0; i < 4; i++)
            {
                if (j.FromOk)
                {
                    Target d = from.GetDamagedBy(i, out float time);
                    j.DmgById[i] = (object)d == null ? 0 : d.GetInstanceID();
                    j.DmgByTime[i] = time;
                }
                else
                {
                    j.DmgById[i] = 0;
                    j.DmgByTime[i] = 0f;
                }
            }
            j.Now = Time.time;
            j.Position = p.Position;
            j.AimDir = p.AimDir;
            j.Range = p.Range;
            j.Damage = p.Damage;
            j.DamageType = (int)p.DamageType;
            j.DamageTypeNone = p.DamageType == EDamageType.None;
            j.Melee = p.DamageType == EDamageType.Bite || p.DamageType == EDamageType.Slash || p.DamageType == EDamageType.Stab || p.DamageType == EDamageType.Collision;
            j.IgnoreAir = p.IgnoreAir;
            j.IgnoreGround = p.IgnoreGround;
            j.IgnoreStructures = p.IgnoreStructures;
            j.IgnoreUndamageable = p.IgnoreUndamageable;
            j.CurrentTargetId = (object)p.CurrentTarget == null ? 0 : p.CurrentTarget.GetInstanceID();
            j.Cfg = s_CfgNow;
            j.Claim = ScanJob.ClaimNone;
            j.Status = ScanJob.Pending;
            j.Reason = ScanReason.None;
            j.Error_ = null;
            j.WorkTicks = 0;
            j.ThreadId = -1;
            s_ByFinder[mode][finder] = s_ScanCount;
            s_ScanCount++;
            s_St[mode].V[C.Captured]++;
        }

        // TargetFinder.GetFromHealth (TargetFinder.cs:525-532).
        static float FromHealth(Target from)
        {
            if (!from || !from.DamageManager) return 0f;
            return from.DamageManager.Health;
        }

        static TeamStage StageFor(Team team)
        {
            for (int i = 0; i < s_StageCount; i++) if (ReferenceEquals(s_Stages[i].Team, team)) return s_Stages[i];
            if (s_StageCount == s_Stages.Length) Array.Resize(ref s_Stages, s_StageCount * 2);
            var s = new TeamStage { Team = team };
            s_Stages[s_StageCount++] = s;
            return s;
        }

        // ================================================================== stage

        static void StageTeam(TeamStage ts)
        {
            Team search = ts.Team;
            List<Target> list = search.DetectedTargets;
            ts.List = list;
            ts.Version = s_ListVersion(list);
            ts.UsedFrame = s_Frame;
            ts.ClearDirty();
            int n = list.Count;
            ts.Grow(n);
            ScanTeamData d = ts.D;
            d.N = n;
            d.CtPoolN = 0;
            ts.Memo = 0;
            ts.KeyA.Clear(); ts.KeyB.Clear(); ts.KeyTables.Clear();
            int frame = s_Frame;
            for (int i = 0; i < n; i++)
            {
                Target t = list[i];
                if (t == null)
                {
                    d.Flags[i] = 0;
                    ts.Ref[i] = null;
                    continue;
                }
                if ((object)t.Owner == null)
                {
                    // vanilla throws (full) / takes the null-team branch (prefiltered) here: scans reaching it go synchronous
                    ts.Ref[i] = t;
                    ts.Owner[i] = null;
                    d.Flags[i] = SF.Poison;
                    g_Poison++;
                    continue;
                }
                Ensure(ts, i, t);
                int cf = ts.Const[i];
                DamageManager dm = ts.Dm[i];
                if ((cf & SF.DmOk) != 0 && dm.IsDestroyed)   // target.IsDestroyed = Owner.IsDestroyed
                {
                    d.Flags[i] = 0;
                    continue;
                }
                BaseGameObject owner = ts.Owner[i];
                if (!Enemy(ts, owner.Team, search))
                {
                    d.Flags[i] = 0;
                    continue;
                }
                int f = SF.Cand | cf;
                ts.Tr[i].GetPositionAndRotation(out Vector3 pos, out Quaternion rot);
                d.Pos[i] = pos;
                d.Rot[i] = rot;
                ObjectInfo oi = ts.Oi[i];
                if ((cf & SF.OiOk) != 0)
                {
                    d.LosC[i] = oi.LineOfSightCenter;
                    d.PhysR[i] = oi.PhysicalRadius;
                    d.EngR[i] = oi.EngagementRange;
                    d.OiScore[i] = oi.Score;
                    if (oi.Critical) f |= SF.Critical;
                }
                else
                {
                    d.LosC[i] = Vector3.zero;
                    d.PhysR[i] = 1f;
                    d.EngR[i] = 0f;
                    d.OiScore[i] = 0;
                    g_NoOi++;
                }
                if (owner.IsFunctional) f |= SF.Functional;
                if ((cf & SF.UnitOk) != 0)
                {
                    Unit u = ts.U[i];
                    if (u.IsFlying) f |= SF.Flying;
                    d.TopSpeed[i] = u.TopSpeed;
                    Target ct = u.CurrentTarget;
                    s_CtIds[0] = (object)ct == null ? 0 : ct.GetInstanceID();
                    d.SetTargets(i, s_CtIds, 1);
                }
                else
                {
                    d.TopSpeed[i] = 0f;
                    int m = 0;
                    if ((cf & SF.Structure) != 0)
                    {
                        List<TurretAimBase> aims = ts.St[i].TurretAims;
                        for (int q = 0, nq = aims.Count; q < nq; q++)
                        {
                            TurretAimBase ta = aims[q];
                            if (!ta) continue;
                            Target ct = ta.CurrentTarget;
                            if ((object)ct == null) continue;
                            if (m == s_CtIds.Length) { f |= SF.Unsupported; break; }
                            s_CtIds[m++] = ct.GetInstanceID();
                        }
                        if (m > 4) g_TurretHeavy++;
                    }
                    d.SetTargets(i, s_CtIds, m);
                }
                d.Health[i] = (cf & SF.DmOk) != 0 ? dm.Health : 0f;
                d.Attacked[i] = t.AttackedByEnemyNum;
                int g = d.GroupSlot[i];
                if (g >= 0 && ts.GroupRepFrame[g] != frame) { ts.GroupRep[g] = dm; ts.GroupRepFrame[g] = frame; }
                d.Flags[i] = f;
                g_Entries++;
            }
        }

        // Per-entry constants: valid while the list slot holds the same Target and its Owner still has the cached
        // ObjectInfo / DamageManager.
        static void Ensure(TeamStage ts, int i, Target t)
        {
            BaseGameObject owner = ts.Owner[i];
            if (ReferenceEquals(ts.Ref[i], t) && (object)owner != null && ReferenceEquals(owner.ObjectInfo, ts.Oi[i]) &&
                ReferenceEquals(owner.DamageManager, ts.Dm[i]))
            {
                return;
            }
            ScanTeamData d = ts.D;
            owner = t.Owner;
            ts.Ref[i] = t;
            ts.Owner[i] = owner;
            ts.Tr[i] = t.transform;
            ObjectInfo oi = owner.ObjectInfo;
            ts.Oi[i] = oi;
            DamageManager dm = owner.DamageManager;
            ts.Dm[i] = dm;
            Unit u = t.OwnerUnit;
            ts.U[i] = u;
            Structure st = t.OwnerStructure;
            ts.St[i] = st;
            int cf = 0;
            bool oiOk = (bool)oi;
            if (oiOk) cf |= SF.OiOk;
            bool dmOk = (bool)dm;
            if (dmOk) cf |= SF.DmOk;
            if ((bool)u) cf |= SF.UnitOk;
            bool stOk = (bool)st;
            if (stOk) cf |= SF.Structure;
            if (stOk || (bool)t.OwnerConstructionSite) cf |= SF.StructOrSite;
            ts.Const[i] = cf;
            d.Id[i] = t.GetInstanceID();
            d.OiId[i] = oiOk ? oi.GetInstanceID() : 0;
            d.TypeSlot[i] = oiOk ? TypeSlot(ts, oi) : 0;
            if (dmOk)
            {
                // AdjustDamage's per-instance input is its size (DamageManager.cs:516-523, 766); entries without
                // ObjectInfo take vanilla's uncached branch with THEIR instance's value: a group of their own
                long key;
                if (oiOk)
                {
                    float size = dm.GetSizeMultiplier(1f);
                    key = ((long)oi.GetInstanceID() << 32) | (uint)BitConverter.SingleToInt32Bits(size);
                }
                else
                {
                    key = ((long)dm.GetInstanceID() << 32) | 0xFFFFFFFFu;
                }
                if (!ts.GroupSlots.TryGetValue(key, out int g))
                {
                    g = ts.GroupKey.Count;
                    ts.GroupSlots.Add(key, g);
                    ts.GroupKey.Add(key);
                    if (ts.GroupRep.Length <= g)
                    {
                        Array.Resize(ref ts.GroupRep, g * 2);
                        Array.Resize(ref ts.GroupRepFrame, g * 2);
                    }
                    ts.GroupRepFrame[g] = -1;
                }
                d.GroupSlot[i] = g;
            }
            else
            {
                d.GroupSlot[i] = -1;
            }
        }

        static int TypeSlot(TeamStage ts, ObjectInfo oi)
        {
            if (!ts.TypeSlots.TryGetValue(oi, out int s))
            {
                s = ts.TypeOi.Count;
                ts.TypeSlots.Add(oi, s);
                ts.TypeOi.Add(oi);
            }
            return s;
        }

        static bool Enemy(TeamStage ts, Team tt, Team search)
        {
            for (int q = 0; q < ts.Memo; q++) if (ReferenceEquals(ts.MemoTeam[q], tt)) return ts.MemoEnemy[q];
            bool e = Team.GetTeamsAreEnemy(tt, search);
            if (ts.Memo < ts.MemoTeam.Length) { ts.MemoTeam[ts.Memo] = tt; ts.MemoEnemy[ts.Memo] = e; ts.Memo++; }
            return e;
        }

        // Our dps cache mirrors vanilla's DamageCalculator caches, which only ever grow until DamageCalculator.Clear.
        static void CheckDamageCalculator()
        {
            Dictionary<ObjectInfo, DamageCalculatorEntry> e = s_DcEntries();
            int n = e != null ? e.Count : 0;
            if (!ReferenceEquals(e, s_DcSeen) || n < s_DcSeenCount) s_DpsCache.Clear();
            s_DcSeen = e;
            s_DcSeenCount = n;
        }

        // vanilla DamageCalculator.GetMaxDamagePerSecondToObject(from, to) for live owners, read from its caches only.
        // NaN = vanilla has not computed this pair yet.
        static float Dps(ObjectInfo fromOi, ObjectInfo toOi)
        {
            if (!fromOi || !toOi) return 0f;
            long key = ((long)fromOi.GetInstanceID() << 32) | (uint)toOi.GetInstanceID();
            if (s_DpsCache.TryGetValue(key, out float v)) return v;
            Dictionary<ObjectInfo, DamageCalculatorEntry> entries = s_DcEntries();
            if (entries == null || !entries.TryGetValue(fromOi, out DamageCalculatorEntry entry) || entry == null) return float.NaN;
            Dictionary<ObjectInfo, float> p = s_DcPrim(entry), s = s_DcSec(entry);
            if (p == null || s == null || !p.TryGetValue(toOi, out float a) || !s.TryGetValue(toOi, out float b)) return float.NaN;
            v = Mathf.Max(a, b);
            s_DpsCache[key] = v;
            return v;
        }

        static float ModValue(TeamStage ts, int g, float damage, EDamageType type)
        {
            var key = new ModKey { Group = ts.GroupKey[g], DamageBits = BitConverter.SingleToInt32Bits(damage), Type = (int)type };
            if (s_ModCache.TryGetValue(key, out float v)) return v;
            DamageManager rep = ts.GroupRep[g];
            if (!rep) return float.NaN;
            v = rep.AdjustDamage(damage, type, limitToHealth: false, storeToCache: false);
            s_ModCache[key] = v;
            return v;
        }

        static void FillJob(Capture c)
        {
            TeamStage ts = c.Stage;
            ScanJob j = c.Job;
            j.Team = ts.D;
            j.Tables = TablesFor(ts, c);
            j.EnsureOutputs(ts.D.N);   // no allocation on the workers
        }

        static ScanTables TablesFor(TeamStage ts, Capture c)
        {
            ScanJob j = c.Job;
            long a = ((long)j.FromOiId << 32) | (uint)BitConverter.SingleToInt32Bits(j.Damage);
            long b = ((long)j.DamageType << 1) | (j.FromOk ? 1L : 0L);
            for (int q = 0; q < ts.KeyA.Count; q++)
            {
                if (ts.KeyA[q] == a && ts.KeyB[q] == b && ts.KeyTables[q].DpsTo.Length >= ts.TypeOi.Count && ts.KeyTables[q].Mod.Length >= ts.GroupKey.Count)
                {
                    return ts.KeyTables[q];
                }
            }
            if (s_TablesUsed == s_TablePool.Count) s_TablePool.Add(new ScanTables());
            ScanTables tb = s_TablePool[s_TablesUsed++];
            int types = ts.TypeOi.Count, groups = ts.GroupKey.Count;
            tb.Grow(types, groups);
            for (int s = 0; s < types; s++)
            {
                if (j.FromOk)
                {
                    ObjectInfo toOi = ts.TypeOi[s];
                    tb.DpsTo[s] = Dps(c.FromOi, toOi);
                    tb.DpsUs[s] = Dps(toOi, c.FromOi);
                }
                else
                {
                    tb.DpsTo[s] = 0f;
                    tb.DpsUs[s] = 0f;
                }
            }
            for (int g = 0; g < groups; g++)
            {
                tb.Mod[g] = j.DamageTypeNone || ts.GroupRepFrame[g] != s_Frame ? 0f : ModValue(ts, g, j.Damage, (EDamageType)j.DamageType);
            }
            Dictionary<ObjectInfo, DamageCalculatorEntry> entries = s_DcEntries();
            tb.FromEntryMissing = (object)c.FromOi != null && (entries == null || !entries.ContainsKey(c.FromOi));
            ts.KeyA.Add(a); ts.KeyB.Add(b); ts.KeyTables.Add(tb);
            return tb;
        }

        // ================================================================== dirty tracking (real changes only)

        static void AttackerPrefix(Target __instance, out int __state)
        {
            __state = s_Active ? __instance.AttackedByEnemyNum : int.MinValue;
        }

        static void AttackerPostfix(Target __instance, int __state)
        {
            if (__state == int.MinValue || !s_Active) return;
            try
            {
                if (__instance.AttackedByEnemyNum == __state) return;
                g_MarkAtt++;
                MarkDirty(__instance);
            }
            catch (Exception ex) { Item.Fail(ex, "Target.AddAttacker/RemoveAttacker postfix"); }
        }

        // The observed value is Unit.CurrentTarget = AgentAiming.AimTarget (AimPaused ? null : m_AimTarget).
        static void AimPrefix(AIAiming __instance, out Target __state)
        {
            __state = s_Active ? __instance.AimTarget : null;
        }

        static void AimPostfix(AIAiming __instance, Target __state)
        {
            if (!s_Active) return;
            try
            {
                if (ReferenceEquals(__instance.AimTarget, __state)) return;
                AIBaseAgent ag = __instance.Agent;
                if (!ag) return;
                Unit u = ag.Unit;
                if (!u) return;
                Target own = u.Target;
                if ((object)own == null) return;
                g_MarkCt++;
                MarkDirty(own);
            }
            catch (Exception ex) { Item.Fail(ex, "AIAiming aim-target postfix"); }
        }

        static void HealthPrefix(DamageManager __instance, out float __state)
        {
            __state = s_Active ? __instance.Health : float.NaN;
        }

        static void HealthPostfix(DamageManager __instance, float __state)
        {
            if (!s_Active || float.IsNaN(__state)) return;
            try
            {
                if (ScanKernel.SameBits(__instance.Health, __state)) return;
                BaseGameObject o = __instance.Owner;
                if ((object)o == null) return;
                Target t = o.Target;
                if ((object)t == null) return;
                g_MarkHealth++;
                MarkDirty(t);
            }
            catch (Exception ex) { Item.Fail(ex, "DamageManager.set_Health postfix"); }
        }

        static void MarkDirty(Target t)
        {
            for (int i = 0; i < s_StageCount; i++)
            {
                TeamStage ts = s_Stages[i];
                if (ts.UsedFrame != s_Frame) continue;
                if (!ts.Team.DetectedTargetIndexes.TryGetValue(t, out int idx) || idx < 0 || idx >= ts.D.N || !ReferenceEquals(ts.Ref[idx], t)) continue;
                if (ts.DirtyMark[idx]) continue;
                ts.DirtyMark[idx] = true;
                if (ts.NDirty == ts.DirtyList.Length) Array.Resize(ref ts.DirtyList, ts.NDirty * 2);
                ts.DirtyList[ts.NDirty++] = idx;
            }
        }

        // ================================================================== serve

        static bool ServeFull(TargetFinder __instance, ref TargetScanParams scanParams, out ServeState __state)
        {
            __state = default;
            if (!s_Active || !On) return true;
            return Serve(ModeFull, __instance, ref scanParams, ref __state);
        }

        static bool ServePref(TargetFinder __instance, ref TargetScanParams scanParams, out ServeState __state)
        {
            __state = default;
            if (!s_Active || !PrefOn) return true;
            return Serve(ModePref, __instance, ref scanParams, ref __state);
        }

        static bool Serve(int mode, TargetFinder finder, ref TargetScanParams scanParams, ref ServeState state)
        {
            Stats st = s_St[mode];
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (!s_ByFinder[mode].TryGetValue(finder, out int k)) { st.V[C.MissedNoCapture]++; return true; }
                Capture c = s_Scans[k];
                if (c.Consumed || c.Dead) { st.V[C.MissedConsumed]++; return true; }
                c.Consumed = true;
                Hist h = HistOf(c.Agent);
                if (h != null) h.ServedFrame[mode] = s_Frame;
                int r = Validate(c, finder, ref scanParams, st);
                if (r != ScanReason.None) { st.Reason[r]++; return true; }
                ScanJob j = c.Job;
                TeamStage ts = c.Stage;

                // type-value cache: vanilla's live state; fast merge, else exact emulation
                LoadTv(s_LiveTv);
                bool emu = false;
                r = ScanKernel.MergeTypeValues(j, s_LiveTv, commit: false);
                if (r == ScanReason.CarryValues || r == ScanReason.CarryOverflow)
                {
                    s_Fx.Ensure(j.NSeq);
                    s_Fx.Tv.CopyFrom(s_LiveTv);
                    r = ScanKernel.EmulateTypeValues(j, s_Fx.Tv, s_Fx.ETo, s_Fx.EUs, s_Fx.EMod);
                    emu = r == ScanReason.None;
                }
                if (r != ScanReason.None) { st.Reason[r]++; return true; }

                // live inputs of the entries that changed after the snapshot and are in this scan's sequence
                s_Live.Clear();
                for (int q = 0; q < ts.NDirty; q++)
                {
                    int e = ts.DirtyList[q];
                    if (j.SeqPos[e] < 0) continue;
                    Target t = ts.Ref[e];
                    bool alive = t != null;
                    DamageManager dm = ts.Dm[e];
                    bool dmOk = alive && (ts.Const[e] & SF.DmOk) != 0;
                    float hl = dmOk ? dm.Health : 0f;
                    bool destroyed = !alive || (dmOk && dm.IsDestroyed);
                    int att = alive ? t.AttackedByEnemyNum : 0;
                    bool tb = alive && j.FromOk && c.From.GetIsTargetedBy(t);
                    s_Live.Add(e, att, tb, hl, destroyed);
                }
                r = ScanKernel.Finalize(j, emu, s_Live, s_Fx, s_MaxRescore, out int[] outE, out float[] outSc, out int nc, out int rescored);
                st.V[C.Rescored] += rescored;
                if (r != ScanReason.None) { st.Reason[r]++; return true; }
                if (emu) st.V[C.Emulated]++;
                if (outE != j.CandEntry) st.V[C.Corrected]++;

                bool sampled = s_Sampler.Enabled && s_Sampler.Sample();
                List<Target> outC = sampled ? s_ChkCands : s_Cands(finder);
                List<float> outS = sampled ? s_ChkScores : s_Scores(finder);
                outC.Clear();
                outS.Clear();
                Target[] refs = ts.Ref;
                for (int q = 0; q < nc; q++)
                {
                    outC.Add(refs[outE[q]]);
                    outS.Add(outSc[q]);
                }
                if (sampled)
                {
                    if (emu) s_ExpectTv.CopyFrom(s_Fx.Tv);
                    else { s_ExpectTv.CopyFrom(s_LiveTv); ScanKernel.MergeTypeValues(j, s_ExpectTv, commit: true); }
                    s_ChkTvValid = true;
                    state = new ServeState { Sampled = true, Scan = k };
                    st.V[C.Checks]++;
                    return true;
                }
                if (emu) StoreTvFull(s_Fx.Tv);
                else { ScanKernel.MergeTypeValues(j, s_LiveTv, commit: true); StoreTv(s_LiveTv); }
                st.V[C.Served]++;
                return false;
            }
            catch (Exception ex)
            {
                try { s_Cands(finder).Clear(); s_Scores(finder).Clear(); } catch { }
                state = default;
                (mode == ModePref ? PrefItem : Item).Fail(ex, mode == ModePref ? "CollectPreFilteredEnemies serve prefix" : "CollectDetectedEnemies serve prefix");
                return true;
            }
            finally
            {
                st.V[C.TServe] += Stopwatch.GetTimestamp() - t0;
            }
        }

        static int Validate(Capture c, TargetFinder finder, ref TargetScanParams p, Stats st)
        {
            if (Time.frameCount != s_Frame) return ScanReason.NotReady;
            if (s_Cands(finder).Count != 0 || s_Scores(finder).Count != 0) return ScanReason.Lists;
            if (!SameParams(ref c.P, ref p)) return ScanReason.Params;
            TeamStage ts = c.Stage;
            List<Target> list = ts.Team.DetectedTargets;
            if (!ReferenceEquals(list, ts.List) || s_ListVersion(list) != ts.Version || list.Count != ts.D.N) return ScanReason.ListChanged;
            if (!s_CfgResolved()) return ScanReason.Cfg;
            for (int i = 0; i < ScoreCfg.Count; i++) if (!ScanKernel.SameBits(s_Cfg[i](), s_CfgNow.Get(i))) return ScanReason.Cfg;
            ScanJob j = c.Job;
            if (j.Pref)
            {
                Team team = ts.Team;
                if (s_BufHandled(team) != c.BufHandled || s_BufStride(team) != c.BufStride || s_ScoreBuf(team).Length != c.BufLen ||
                    (team.PerTeamQueries.IsCreated ? team.PerTeamQueries.Length : -1) != c.BufQueries)
                {
                    return ScanReason.Buffer;
                }
            }
            if ((bool)p.Aiming && p.Aiming.HasUnshootableTargets) return ScanReason.Scanner;
            Target from = c.From;
            if (!ScanKernel.SameBits(FromHealth(p.LineOfSightFromTarget), j.FromHealth)) return ScanReason.Scanner;
            if (j.FromOk)
            {
                if (!from) return ScanReason.Scanner;
                Vector3 fp = from.transform.position;
                if (!ScanKernel.SameBits(fp.x, j.FromPos.x) || !ScanKernel.SameBits(fp.y, j.FromPos.y) || !ScanKernel.SameBits(fp.z, j.FromPos.z)) return ScanReason.Scanner;
                for (int i = 0; i < 4; i++)
                {
                    Target d = from.GetDamagedBy(i, out float time);
                    int id = (object)d == null ? 0 : d.GetInstanceID();
                    if (id != j.DmgById[i] || !ScanKernel.SameBits(time, j.DmgByTime[i])) return ScanReason.Scanner;
                }
            }
            int stt = System.Threading.Volatile.Read(ref j.Status);
            if (stt == ScanJob.Pending)
            {
                if (ScanKernel.TryClaim(j, ScanJob.ClaimMain))
                {
                    // no worker has started it: compute it here on the snapshot (cheaper than the synchronous path)
                    long s0 = Stopwatch.GetTimestamp();
                    ScanKernel.Run(j);
                    st.V[C.Stolen]++;
                    st.V[C.TSteal] += Stopwatch.GetTimestamp() - s0;
                }
                else if (s_WaitTicks > 0)
                {
                    // a worker is on exactly this scan: wait for it (bounded, ~one scan time)
                    long w0 = Stopwatch.GetTimestamp(), until = w0 + s_WaitTicks;
                    st.V[C.Waits]++;
                    do
                    {
                        System.Threading.Thread.SpinWait(20);
                    }
                    while (System.Threading.Volatile.Read(ref j.Status) == ScanJob.Pending && Stopwatch.GetTimestamp() < until);
                    st.V[C.TWait] += Stopwatch.GetTimestamp() - w0;
                }
                stt = System.Threading.Volatile.Read(ref j.Status);
                if (stt == ScanJob.Pending) { st.V[C.WaitTimeouts]++; return ScanReason.NotReady; }
            }
            if (stt != ScanJob.Done) return j.Reason != ScanReason.None ? j.Reason : ScanReason.WorkerError;
            return ScanReason.None;
        }

        static bool SameParams(ref TargetScanParams a, ref TargetScanParams b)
        {
            return a.Mode == b.Mode && ReferenceEquals(a.SearchTeam, b.SearchTeam) && SameV(a.Position, b.Position) && SameV(a.AimDir, b.AimDir) &&
                   ScanKernel.SameBits(a.Range, b.Range) && a.DamageType == b.DamageType && ScanKernel.SameBits(a.Damage, b.Damage) &&
                   a.IgnoreUndamageable == b.IgnoreUndamageable && a.IgnoreAir == b.IgnoreAir && a.IgnoreGround == b.IgnoreGround &&
                   a.IgnoreStructures == b.IgnoreStructures && ReferenceEquals(a.CurrentTarget, b.CurrentTarget) &&
                   ReferenceEquals(a.Aiming, b.Aiming) && ReferenceEquals(a.LineOfSightFromTarget, b.LineOfSightFromTarget) &&
                   (a.Mode != ETargetScanMode.PreFilteredEnemies || a.FilterQueryIndex == b.FilterQueryIndex);
        }

        static bool SameV(Vector3 a, Vector3 b) => ScanKernel.SameBits(a.x, b.x) && ScanKernel.SameBits(a.y, b.y) && ScanKernel.SameBits(a.z, b.z);

        static void LoadTv(TvState s)
        {
            s.From = s_TvFrom();
            s.Type = (int)s_TvType();
            s.Damage = s_TvDamage();
            s.Num = s_TvNum();
            s.Ids = s_TvIds();
            s.To = s_TvTo();
            s.Us = s_TvUs();
            s.Mod = s_TvMod();
        }

        static void StoreTv(TvState s)
        {
            s_TvFrom() = s.From;
            s_TvType() = (EDamageType)s.Type;
            s_TvDamage() = s.Damage;
            s_TvNum() = s.Num;
        }

        // From a TvState with its own arrays (the emulation's) into vanilla's statics.
        static void StoreTvFull(TvState s)
        {
            StoreTv(s);
            Array.Copy(s.Ids, s_TvIds(), 32);
            Array.Copy(s.To, s_TvTo(), 32);
            Array.Copy(s.Us, s_TvUs(), 32);
            Array.Copy(s.Mod, s_TvMod(), 32);
        }

        // Self-check: the synchronous path has written the real lists; compare with what we would have served.
        static void ServePostfix(TargetFinder __instance, ServeState __state)
        {
            if (!__state.Sampled) return;
            long t0 = Stopwatch.GetTimestamp();
            int mode = __state.Scan < s_ScanCount ? s_Scans[__state.Scan].Mode : ModeFull;
            Stats st = s_St[mode];
            try
            {
                List<Target> vc = s_Cands(__instance);
                List<float> vs = s_Scores(__instance);
                int n = vc.Count, k = -1;
                if (n != s_ChkCands.Count) k = Math.Min(n, s_ChkCands.Count);
                int diffSlots = 0;
                double maxRel = 0;
                for (int i = 0; i < Math.Min(n, s_ChkCands.Count); i++)
                {
                    bool same = ReferenceEquals(vc[i], s_ChkCands[i]) && ScanKernel.SameBits(vs[i], s_ChkScores[i]);
                    if (same) continue;
                    if (k < 0) k = i;
                    diffSlots++;
                    if (ReferenceEquals(vc[i], s_ChkCands[i]))
                    {
                        double rel = Math.Abs((double)vs[i] - s_ChkScores[i]) / Math.Max(1e-6, Math.Abs((double)vs[i]));
                        if (rel > maxRel) maxRel = rel;
                    }
                    else
                    {
                        maxRel = Math.Max(maxRel, 1.0);
                    }
                }
                bool tvSame = !s_ChkTvValid || SameTvLive(s_ExpectTv);
                if (k < 0 && tvSame)
                {
                    st.V[C.CheckSame]++;
                }
                else
                {
                    if (k >= 0)
                    {
                        st.V[C.CheckDiff]++;
                        st.V[C.DiffSlots] += Math.Max(1, diffSlots);
                        if (maxRel > st.MaxRel) st.MaxRel = maxRel;
                        st.SumRel += maxRel;
                    }
                    if (!tvSame) st.V[C.CheckTvDiff]++;
                    if (s_Logged < s_MaxLogs)
                    {
                        s_Logged++;
                        Capture c = __state.Scan < s_ScanCount ? s_Scans[__state.Scan] : null;
                        string detail = (mode == ModePref ? "prefiltered" : "full") + ", team " + (c != null && c.Stage != null && (bool)c.Stage.Team ? c.Stage.Team.TeamShortName : "?") +
                                        ": sync " + n + " candidates, ours " + s_ChkCands.Count + (k >= 0 ? ", first difference at slot " + k : "") +
                                        (k >= 0 && k < n && k < s_ChkCands.Count ? " (sync " + Name(vc[k]) + " " + vs[k].ToString("R", CultureInfo.InvariantCulture) + ", ours " + Name(s_ChkCands[k]) + " " + s_ChkScores[k].ToString("R", CultureInfo.InvariantCulture) + ")" : "") +
                                        (tvSame ? "" : ", type-value cache state differs") + ", max rel. score diff " + maxRel.ToString("0.###E+0", CultureInfo.InvariantCulture);
                        MelonLogger.Warning("[MultiCore] SELF-CHECK MISMATCH ParallelScan " + detail + (s_Logged == s_MaxLogs ? " (log limit reached for this round; still counted)" : ""));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[MultiCore] ParallelScan self-check compare failed: " + ex.Message);
            }
            finally
            {
                s_ChkCands.Clear();
                s_ChkScores.Clear();
                s_ChkTvValid = false;
                st.V[C.TCheck] += Stopwatch.GetTimestamp() - t0;
            }
        }

        static bool SameTvLive(TvState e)
        {
            if (s_TvFrom() != e.From || (int)s_TvType() != e.Type || !ScanKernel.SameBits(s_TvDamage(), e.Damage) || s_TvNum() != e.Num) return false;
            int[] ids = s_TvIds();
            float[] to = s_TvTo(), us = s_TvUs(), mod = s_TvMod();
            for (int i = 0; i < e.Num; i++)
            {
                if (ids[i] != e.Ids[i] || !ScanKernel.SameBits(to[i], e.To[i]) || !ScanKernel.SameBits(us[i], e.Us[i]) || !ScanKernel.SameBits(mod[i], e.Mod[i])) return false;
            }
            return true;
        }

        static string Name(Target t) => (bool)t && (bool)t.Owner ? t.Owner.name : "null";

        internal static void ResetRoundLogs() => s_Logged = 0;

        // ================================================================== status

        internal static void Publish(double dt)
        {
            if (!(dt > 0)) dt = 1;
            if (Item != null && Item.Applied)
            {
                var sb = new StringBuilder(900);
                long fr = g_Frames - pg_Frames, few = g_SkippedFew - pg_SkippedFew, fast = g_SkippedFast - pg_SkippedFast, cfg = g_SkippedCfg - pg_SkippedCfg;
                long en = g_Entries - pg_Entries, noOi = g_NoOi - pg_NoOi, th = g_TurretHeavy - pg_TurretHeavy, po = g_Poison - pg_Poison;
                long ma = g_MarkAtt - pg_MarkAtt, mc = g_MarkCt - pg_MarkCt, mh = g_MarkHealth - pg_MarkHealth;
                double stage = (g_TStage - pg_TStage) * TicksToMs / dt, tb = (g_TTakeBack - pg_TTakeBack) * TicksToMs / dt;
                pg_Frames = g_Frames; pg_SkippedFew = g_SkippedFew; pg_SkippedFast = g_SkippedFast; pg_SkippedCfg = g_SkippedCfg; pg_Entries = g_Entries;
                pg_NoOi = g_NoOi; pg_TurretHeavy = g_TurretHeavy; pg_Poison = g_Poison; pg_MarkAtt = g_MarkAtt; pg_MarkCt = g_MarkCt; pg_MarkHealth = g_MarkHealth;
                pg_TStage = g_TStage; pg_TTakeBack = g_TTakeBack;
                sb.Append("frame: batches/s ").Append(F0(fr / dt)).Append(" (skipped: few scans ").Append(F0(few / dt)).Append(", fast frames ").Append(F0(fast / dt))
                  .Append(", cfg ").Append(F0(cfg / dt)).Append(") | staged entries/s ").Append(F0(en / dt)).Append(" (no ObjectInfo ").Append(F0(noOi / dt))
                  .Append(", structures >4 turret targets ").Append(F0(th / dt)).Append(", no Owner ").Append(F0(po / dt)).Append(')')
                  .Append(" | dirty marks/s attackers ").Append(F0(ma / dt)).Append(", current target ").Append(F0(mc / dt)).Append(", health ").Append(F0(mh / dt))
                  .Append(" | main staging (both batches) ").Append(F2(stage)).Append(" ms/s, take-back ").Append(F2(tb)).Append(" ms/s");
                sb.Append(" || full: ");
                ModeLine(sb, s_St[ModeFull], dt);
                Item.Info = sb.ToString();
            }
            if (PrefItem != null && PrefItem.Applied)
            {
                var sb = new StringBuilder(600);
                sb.Append("prefiltered: ");
                ModeLine(sb, s_St[ModePref], dt);
                PrefItem.Info = sb.ToString();
            }
        }

        static void ModeLine(StringBuilder sb, Stats s, double dt)
        {
            long[] v = s.V, p = s.P;
            long D(int i) => v[i] - p[i];
            double Ms(int i) => (v[i] - p[i]) * TicksToMs / dt;
            sb.Append("batches/s ").Append(F0(D(C.Batches) / dt)).Append(", predicted/s ").Append(F0(D(C.Predicted) / dt))
              .Append(" (history skips ").Append(F0(D(C.HistSkips) / dt)).Append(", probes ").Append(F0(D(C.Probes) / dt)).Append(')')
              .Append(", captured/s ").Append(F0(D(C.Captured) / dt)).Append(", served/s ").Append(F0(D(C.Served) / dt))
              .Append(" (corrected ").Append(F0(D(C.Corrected) / dt)).Append(", emulated type values ").Append(F0(D(C.Emulated) / dt))
              .Append(", rescored entries ").Append(F0(D(C.Rescored) / dt)).Append(')')
              .Append(", missed/s ").Append(F0(D(C.MissedNoCapture) / dt)).Append(" (+consumed/dead ").Append(F0(D(C.MissedConsumed) / dt)).Append(')')
              .Append(", wasted/s ").Append(F0(D(C.Wasted) / dt)).Append(" (soldier ").Append(F0(D(C.WastedSoldier) / dt)).Append(", vehicle ")
              .Append(F0(D(C.WastedVehicle) / dt)).Append(", creature ").Append(F0(D(C.WastedCreature) / dt)).Append(", other ").Append(F0(D(C.WastedOther) / dt)).Append(')');
            // the two behaviour trees with the most waste (cumulative)
            int b1 = -1, b2 = -1;
            for (int t = 0; t < 64; t++)
            {
                if (s.WastedTree[t] == 0) continue;
                if (b1 < 0 || s.WastedTree[t] > s.WastedTree[b1]) { b2 = b1; b1 = t; }
                else if (b2 < 0 || s.WastedTree[t] > s.WastedTree[b2]) b2 = t;
            }
            if (b1 >= 0) sb.Append(" [waste by tree, total: ").Append(((BTType)b1).ToString()).Append(' ').Append(s.WastedTree[b1])
                           .Append(b2 >= 0 ? ", " + ((BTType)b2).ToString() + " " + s.WastedTree[b2] : "").Append(']');
            sb.Append(" | fallback/s");
            long fb = 0;
            for (int r = 1; r < ScanReason.Count; r++)
            {
                long x = s.Reason[r] - s.PReason[r];
                s.PReason[r] = s.Reason[r];
                if (x == 0) continue;
                fb += x;
                sb.Append(' ').Append(ScanReason.Names[r]).Append(' ').Append(F1(x / dt));
            }
            if (fb == 0) sb.Append(" 0");
            sb.Append(" | main: staging ").Append(F2(Ms(C.TStage))).Append(" ms/s (predict+dry run ").Append(F2(Ms(C.TDry))).Append("), serve ").Append(F2(Ms(C.TServe)))
              .Append(" ms/s (main-computed scans ").Append(F0(D(C.Stolen) / dt)).Append("/s ").Append(F2(Ms(C.TSteal))).Append(" ms/s, waits ")
              .Append(F0(D(C.Waits) / dt)).Append("/s ").Append(F2(Ms(C.TWait))).Append(" ms/s, timeouts ").Append(F0(D(C.WaitTimeouts) / dt)).Append("/s)")
              .Append(" | worker ").Append(F2(Ms(C.TWorker))).Append(" ms/s (Unity ran ").Append(F0(D(C.OnMain) / dt)).Append(" scans/s on the main thread, ")
              .Append(F2(Ms(C.TOnMain))).Append(" ms/s)");
            if (s_Inline) sb.Append(" (inline: worker time is main time, inside staging)");
            if (v[C.DryErrors] > 0) sb.Append(" | dry-run errors ").Append(v[C.DryErrors]);
            if (s_Sampler.Enabled)
            {
                sb.Append(" | self-check ").Append(v[C.Checks]).Append(": identical ").Append(v[C.CheckSame]).Append(", different ").Append(v[C.CheckDiff])
                  .Append(" (slots ").Append(v[C.DiffSlots]).Append(", max rel ").Append(s.MaxRel.ToString("0.###E+0", CultureInfo.InvariantCulture))
                  .Append(", mean rel ").Append((v[C.CheckDiff] > 0 ? s.SumRel / v[C.CheckDiff] : 0).ToString("0.###E+0", CultureInfo.InvariantCulture)).Append(')')
                  .Append(", type-value state different ").Append(v[C.CheckTvDiff]).Append(" (").Append(F2(Ms(C.TCheck))).Append(" ms/s)");
            }
            Array.Copy(v, p, C.Count);
        }

        static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string F1(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
