/*
 Si_ServerPerf module "Targeting": DEV_REPORT S2 (+ friendly fire + Rank). Cause #3 of the server profile and its only
 quadratic term: every AI scan walks its team's whole Team.DetectedTargets list, CheckRiskOfFriendlyFire walks the
 instigator team's whole 1 km TargetGrid cell, both every scan / every aligned frame.

 Items (details and exactness arguments in the header of each file: DetectedGrid.cs, Consumers.cs, FriendlyFire.cs,
 Finder.cs):
   exact (same result as vanilla, cheaper)                         default
     DetectedGrid          per-team grid over DetectedTargets          ON   infrastructure: tracks additions
     CollectEnemies_Grid   TargetFinder.CollectDetectedEnemies         ON   hottest caller (22.5 ms/s at 500 u)
     UrgentClose_Grid      AIAiming.FindUrgentCloseEnemy               ON
     NearestTarget_Grid    Team.FindNearestTarget                      ON
     AlarmProximity_Grid   AlarmProximity.OrderedFixedUpdate           ON
     FriendlyFire_Grid     Target.CheckRiskOfFriendlyFire              ON   13.4 ms/s at 500 u, lab k 2.49
     Rank_Sort             TargetFinder.Rank                           ON
     CollectEnemies_Snapshot / _SnapshotScore / _FastAdd  ride on CollectEnemies_Grid's prefix   ON since 0.2.0 (Snapshot.cs)
     CollectPreFiltered_Snapshot  TargetFinder.CollectPreFilteredEnemies  ON since 0.2.1  (PreFiltered.cs)
     FriendlyFire_StaleGuard ON, FriendlyFire_Flat off (FriendlyFire.cs, FriendlyFireFlat.cs), Scan_Diag ON (counters)
   approximate (change when / how often something is evaluated)
     LosTopK               LOS queries only for the top K candidates   off
     FriendlyFire_Cache    reuse a CheckRiskOfFriendlyFire answer      off
 Not built: the negative cache for target-less idle agents (with the grid an empty scan already only visits the few
 cells around the agent, so a cache would save well under 1 us per scan at the price of a behaviour change), and
 the soldier threat scans (1.0.17 Soldier.GetTeamMeleeThreats / GetTeamFleeThreats are already per-team caches
 refreshed at 4 Hz: O(D) per team per 0.25 s, not per soldier).

 Self-check (Settings.SelfCheckFraction > 0): a sampled fraction of calls runs both the grid path and vanilla and
 counts / logs every difference (status Info "check n / mismatch m"). On a mismatch the vanilla answer is used.
*/

using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;

namespace SiServerPerf.Modules.Targeting
{
    public sealed class TargetingSettings
    {
        // Grid cell edge (m) of the per-team DetectedTargets grid. Queries visit the cells within their range.
        public float DetectedCellSize = 100f;
        // How far (m) a detected target may move between the grid build and a query in the same frame / physics step
        // without the grid missing it. The grid is rebuilt once per frame and per physics step, so the only movement
        // inside one build is one physics step (Physics.Simulate runs after the FixedUpdate scripts) plus script
        // moves inside Update: 16 m covers 400 m/s at the 25 Hz step.
        public float DetectedMarginMeters = 16f;
        // 0 = rebuild at most once per frame and physics step (exact, see above). > 0 = rebuild at most every this many
        // seconds of game time: then exact only while DetectedMarginMeters >= fastest unit speed x this interval
        // (approximate otherwise; the self-check measures it).
        public float DetectedRebuildSeconds = 0f;
        // A query whose range covers more than this fraction of the grid's cells runs vanilla (cheaper there).
        public float MaxCellFraction = 0.5f;

        // Grid cell edge (m) of the friendly-fire grid (built from TargetGrid's own per-frame snapshot).
        public float FriendlyCellSize = 100f;

        // CollectEnemies_Snapshot: read a target's pose with one native call (Transform.GetPositionAndRotation) instead
        // of rotation + position. false = two calls, in case the one-call pose ever differs in the last bit (the
        // self-check would show it as a score mismatch).
        public bool SnapshotPoseOneCall = true;

        // LosTopK: LOS queries are issued only for the K best-scored candidates.
        public int LosTopK = 6;

        // FriendlyFire_Cache: reuse a previous answer of the same weapon (same instigator, projectile, scan team) for
        // this long if the muzzle moved less than CacheMoveMeters, turned less than CacheAngleDeg and the distance
        // changed less than CacheDistMeters.
        public float FriendlyFireCacheSeconds = 0.15f;
        public float FriendlyFireCacheMoveMeters = 1f;
        public float FriendlyFireCacheAngleDeg = 2f;
        public float FriendlyFireCacheDistMeters = 5f;

        // Self-check: fraction (0..1) of the calls an item answers itself (the "grid"/served calls, not bypassed or vanilla
        // ones) that also run vanilla and compare. 0 = off. Status shows "checks/s" and their share of served calls;
        // until b18 the status printed the raw count of the last StatusIntervalSeconds window ("check n"), not a rate.
        // FriendlyFire_Grid never samples inside a stale-target window.
        public float SelfCheckFraction = 0f;
        // Mismatch lines written to the log per round (all mismatches are counted).
        public int SelfCheckMaxLogsPerRound = 30;

        // Item Info (status / runtime_state.json) refresh interval, seconds of real time.
        public float StatusIntervalSeconds = 5f;
    }

    [PerfModuleOrder(20)]
    public sealed class TargetingModule : IPerfModule
    {
        public string Name => "Targeting";
        public string Description => "S2 spatial index for target queries + friendly-fire grid + Rank sort (exact), LOS top-K + FF cache (approximate)";

        internal static TargetingSettings S;

        public void Apply(ModuleContext ctx)
        {
            S = ctx.Settings<TargetingSettings>();
            SelfCheck.Init(S);

            DetectedGrid.Register(ctx, S);
            CollectEnemiesGrid.Register(ctx);
            CollectSnapshot.Register(ctx, S);
            PreFilteredSnapshot.Register(ctx);
            UrgentCloseGrid.Register(ctx);
            NearestTargetGrid.Register(ctx);
            AlarmProximityGrid.Register(ctx);
            FriendlyFireGrid.Register(ctx, S);
            StaleGuard.Register(ctx);
            FriendlyFireFlat.Register(ctx);   // FriendlyFire_Flat (FriendlyFireFlat.cs), default off
            RankSort.Register(ctx);
            LosTopK.Register(ctx, S);
            FriendlyFireCache.Register(ctx, S);
            ScanDiag.Register(ctx, S);

            if (S.SelfCheckFraction > 0f)
            {
                ctx.Note("self-check ON: " + (S.SelfCheckFraction * 100f).ToString("0.###") + " % of calls also run vanilla and compare");
            }
            if (S.DetectedRebuildSeconds > 0f)
            {
                ctx.Note("DetectedRebuildSeconds = " + S.DetectedRebuildSeconds + " s: grid items are exact only for units slower than " +
                         (S.DetectedMarginMeters / S.DetectedRebuildSeconds).ToString("0") + " m/s");
            }

            ctx.OnUpdate(StatusTick);
            ctx.OnRoundStart(OnRound);
            ctx.OnRoundEnd(OnRound);
            ctx.OnSceneLoaded(_ => OnRound());
        }

        static float s_NextStatus;
        static float s_LastStatus;

        static void OnRound()
        {
            DetectedGrid.InvalidateAll();
            FriendlyFireGrid.Invalidate();
            FriendlyFireCache.Clear();
            CollectSnapshot.OnRound();
            SelfCheck.ResetRoundLogs();
        }

        static void StatusTick()
        {
            float now = Perf.RealTime;
            if (now < s_NextStatus) return;
            float dt = s_LastStatus > 0f ? now - s_LastStatus : 0f;
            s_LastStatus = now;
            s_NextStatus = now + Math.Max(0.5f, S.StatusIntervalSeconds);
            if (dt <= 0f) return;
            FriendlyFireGrid.Publish();
            Stat.PublishAll(dt);
            DetectedGrid.Publish(dt);
            ScanDiag.Publish(dt);
        }
    }

    // Cheap per-item counters (plain long fields, main thread). Published as rates into PerfItem.Info.
    internal sealed class Stat
    {
        static readonly List<Stat> s_All = new List<Stat>();

        readonly PerfItem _item;
        readonly string _listName;   // what "ListLen" is the length of ("D" = detected list, "cell" = vanilla cell list)

        public long Calls;       // calls that reached the patch with the switch on
        public long Fast;        // answered by the grid path
        public long Vanilla;     // fell back to vanilla (range too big, no data, desync)
        public long Bypass;      // never-worse gate chose vanilla (nothing built)
        public string Mode;      // gate state text set by the item (FriendlyFire_Grid)
        public long Visited;     // grid entries examined by the fast path
        public long ListLen;     // length of the list vanilla would have walked (sum over fast calls)
        public long Passed;      // entries handed to the vanilla per-entry logic
        public long Checks;      // self-check comparisons
        public long Mismatch;    // self-check differences
        public long Extra;       // item-specific (builds, trimmed candidates, cache hits)
        public string ExtraName;

        long _pBypass, _pCalls, _pFast, _pVanilla, _pVisited, _pListLen, _pPassed, _pChecks, _pExtra;
        public long MismatchTotal => Mismatch;

        public Stat(PerfItem item, string listName, string extraName = null)
        {
            _item = item;
            _listName = listName;
            ExtraName = extraName;
            s_All.Add(this);
        }

        public static void PublishAll(float dt)
        {
            for (int i = 0; i < s_All.Count; i++) s_All[i].Publish(dt);
        }

        void Publish(float dt)
        {
            if (_item == null || !_item.Applied) return;
            long byp = Bypass - _pBypass;
            _pBypass = Bypass;
            long calls = Calls - _pCalls, fast = Fast - _pFast, van = Vanilla - _pVanilla, vis = Visited - _pVisited,
                 len = ListLen - _pListLen, pass = Passed - _pPassed, chk = Checks - _pChecks, ext = Extra - _pExtra;
            _pCalls = Calls; _pFast = Fast; _pVanilla = Vanilla; _pVisited = Visited; _pListLen = ListLen; _pPassed = Passed;
            _pChecks = Checks; _pExtra = Extra;
            var sb = new StringBuilder(160);
            sb.Append("q/s ").Append((calls / dt).ToString("0"));
            sb.Append(" (grid ").Append((fast / dt).ToString("0")).Append(", bypass ").Append((byp / dt).ToString("0"))
              .Append(", vanilla ").Append((van / dt).ToString("0")).Append(')');
            if (calls > 0) sb.Append(" grid share ").Append((100.0 * fast / calls).ToString("0")).Append('%');
            if (Mode != null) sb.Append(" | ").Append(Mode);
            if (fast > 0 && _listName != null)
            {
                double v = (double)vis / fast, l = (double)len / fast;
                sb.Append(" | visited/q ").Append(v.ToString("0.0")).Append(" of ").Append(_listName).Append(' ').Append(l.ToString("0.0"));
                if (l > 0) sb.Append(" (").Append((100.0 * v / l).ToString("0.0")).Append("%)");
                sb.Append(", to vanilla logic/q ").Append(((double)pass / fast).ToString("0.0"));
            }
            if (ExtraName != null) sb.Append(" | ").Append(ExtraName).Append("/s ").Append((ext / dt).ToString("0.0"));
            if (SelfCheck.Enabled)
            {
                // checks are a RATE here (per second), and their share of the calls this item answered itself
                sb.Append(" | checks/s ").Append((chk / dt).ToString("0.0"));
                if (fast > 0) sb.Append(" (").Append((100.0 * chk / fast).ToString("0.0")).Append("% of served, SelfCheckFraction ")
                                .Append((SelfCheck.Fraction * 100f).ToString("0.###")).Append("%)");
                sb.Append(", checks total ").Append(Checks).Append(", mismatch total ").Append(Mismatch);
            }
            _item.Info = sb.ToString();
        }
    }

    internal static class SelfCheck
    {
        static uint s_Threshold;          // of 2^24
        public static float Fraction;
        static uint s_State = 2463534242u; // own xorshift: never touch UnityEngine.Random (that would change gameplay)
        static int s_Logged;
        static int s_MaxLogs;

        public static bool Enabled => s_Threshold != 0;

        public static void Init(TargetingSettings s)
        {
            float f = s.SelfCheckFraction;
            if (!(f > 0f)) { s_Threshold = 0; return; }
            if (f > 1f) f = 1f;
            Fraction = f;
            s_Threshold = Math.Max(1u, (uint)(f * 16777216f));
            s_MaxLogs = Math.Max(0, s.SelfCheckMaxLogsPerRound);
        }

        public static void ResetRoundLogs() => s_Logged = 0;

        public static bool Sample()
        {
            if (s_Threshold == 0) return false;
            uint x = s_State;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            s_State = x;
            return (x & 0xFFFFFFu) < s_Threshold;
        }

        public static void Report(PerfItem item, Stat st, string detail)
        {
            st.Mismatch++;
            if (s_Logged >= s_MaxLogs) return;
            s_Logged++;
            MelonLogger.Warning("[Targeting] SELF-CHECK MISMATCH " + item.Id + ": " + detail +
                                (s_Logged == s_MaxLogs ? " (log limit reached for this round; still counted)" : ""));
        }
    }
}
