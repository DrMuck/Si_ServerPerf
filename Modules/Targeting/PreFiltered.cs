/*
 CollectPreFiltered_Snapshot (exact, default off until measured): the score replay for the PREFILTERED scan path.

 Vanilla (1.0.17): the async re-evaluation of fighting agents (BTEnemyTargetDetected -> AIAiming.FindBestTargetAsyncStart
 -> Unit.StartGetBestTarget queues a TeamFilterBestTargetQuery; Team.RunBestTargetTeamFilterJobs runs the Burst
 DistanceToScoreTeamJob + BumpDownTeamJob, Team.cs:1787-1817, which only range-filter and compact; then
 Unit.FinishGetBestTarget -> FinishGetBestTarget(finder, queryIndex, ...) builds ETargetScanMode.PreFilteredEnemies
 params from base.WorldLineOfSightCenter (Unit.cs:1430-1480) -> TargetFinder.RequestScan -> CollectCandidates ->
 TargetFinder.CollectPreFilteredEnemies (TargetFinder.cs:396-433)). That method walks the job output
 (Team.GetBestTargetScores(FilterQueryIndex): {score = range01, index into DetectedTargets}, ended by score < 0) and for
 every entry runs target == null, (bool)Owner && IsDestroyed, GetTeamsAreEnemy, structure flags,
 (Owner ? Owner.WorldLineOfSightCenter : transform.position) for the aim dot, the full
 GameAI.CalculateBestTargetScore(..., range01 = job score, withinView false, ...) and AddCandidate. ~250-300 in-range
 entries per scan in a dense melee, nothing of it covered by CollectEnemies_Snapshot*.

 This item: prefix on CollectPreFilteredEnemies that runs the same loop over the same job output with
   - the per-object snapshot of CollectEnemies_Snapshot (CollectSnapshot.PrefilterPre: cached Owner / Transform /
     ObjectInfo / DamageManager / flags / ids, one native pose call, per-scan enemy-team memo),
   - the stage-2 score replay (CollectSnapshot.Score; vanilla's type-value cache state machine on its own fields),
   - FastAdd (cached min slot).
 range01 is the job's own float output, passed through unchanged, so no Burst fast-math has to be reproduced. The
 job is completed by the same Team.GetBestTargetScores call vanilla makes (bumpDownHandle.Complete is idempotent).
 Exactness argument: as in Snapshot.cs (only per-object constants cached; poses, team, death, health, attacker counts,
 current targets read live per scan; same order; same float expressions). One deviation guard: an entry whose Target
 is alive but whose Owner is C#-null (Target.OnDestroy window) makes the whole call run vanilla, which would throw on
 it at target.ObjectInfo; nothing has been written at that point.

 Composition / entry point: Harmony priority Priority.LowerThanNormal (300), so a serving prefix of another module
 (e.g. MultiCore.ParallelScan at Priority.First, if it ever serves this path) runs first; if that prefix fills
 m_Candidates / m_Scores and returns false, Harmony skips this one; if the lists are already filled when this prefix
 runs, it steps aside (returns true untouched), exactly like the CollectDetectedEnemies prefix. LosTopK (postfix on
 CollectCandidates) still trims after it.

 Self-check (Targeting SelfCheckFraction): a sampled call writes its result to scratch lists and lets vanilla run into
 the real lists; the postfix compares count, order (reference) and score bits. The scratch run leaves vanilla's
 type-value cache in the state vanilla would leave it, so the vanilla run produces its own values.

 THREADING: main thread only (Unity objects, vanilla static cache). A worker version would take PrefilterPre's output
 arrays plus snapshotted score inputs; that is the ParallelScan module's job, not this item's.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;

namespace SiServerPerf.Modules.Targeting
{
    internal static class PreFilteredSnapshot
    {
        internal static bool On;
        static PerfItem Item;
        static Stat St, FastStat;
        static AccessTools.FieldRef<TargetFinder, List<Target>> s_Cands;
        static AccessTools.FieldRef<TargetFinder, List<float>> s_Scores;
        static MethodInfo s_WarnNaN;

        static int[] s_Idx = new int[64];
        static Target[] s_T = new Target[64];
        static Vector3[] s_W = new Vector3[64];
        static float[] s_R = new float[64];
        static Vector3[] s_P = new Vector3[64];
        static readonly List<Target> s_ChkCands = new List<Target>(24);
        static readonly List<float> s_ChkScores = new List<float>(24);

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("CollectPreFiltered_Snapshot",
                "TargetFinder.CollectPreFilteredEnemies: snapshot prefilter + CalculateBestTargetScore replay + FastAdd over the team job's output (range01 taken from the job; exact)",
                v => On = v, defaultOn: true);   // on since 0.2.1 (with ParallelScan_Prefiltered)
            St = new Stat(Item, "job entries", "entries refilled");
            FastStat = new Stat(null, null);   // FastAdd counters of this path (not published separately)
            s_Cands = Refl.Field<TargetFinder, List<Target>>("m_Candidates", Item);
            s_Scores = Refl.Field<TargetFinder, List<float>>("m_Scores", Item);
            s_WarnNaN = Refl.Method(typeof(TargetFinder), "WarnNaNScore",
                new[] { typeof(float), typeof(Target), typeof(TargetScanParams).MakeByRefType() }, Item);
            if (s_Cands == null || s_Scores == null) Item.Block("TargetFinder.m_Candidates / m_Scores absent");
            if (!CollectSnapshot.ReplayReady) Item.Block("GameAI scoring internals absent (see CollectEnemies_SnapshotScore)");
            if (Refl.Method(typeof(Team), nameof(Team.GetBestTargetScores), new[] { typeof(int) }, Item) == null) Item.Block("Team.GetBestTargetScores absent");
            var m = Item.Target(typeof(TargetFinder), "CollectPreFilteredEnemies", new[] { typeof(TargetScanParams).MakeByRefType() });
            Item.Patch(m, typeof(PreFilteredSnapshot), prefix: nameof(Prefix), postfix: nameof(Postfix), priority: Priority.LowerThanNormal);
        }

        static bool Prefix(TargetFinder __instance, ref TargetScanParams scanParams, out bool __state)
        {
            __state = false;
            if (!On) return true;
            List<Target> cands;
            List<float> scores;
            List<Target> list;
            int n;
            bool sampled;
            try
            {
                St.Calls++;
                Team searchTeam = scanParams.SearchTeam;
                cands = s_Cands(__instance);
                scores = s_Scores(__instance);
                // RequestScan clears both lists; anything already in them (another module served the call): step aside
                if ((object)searchTeam == null || cands.Count != 0 || scores.Count != 0) { St.Vanilla++; return true; }
                list = searchTeam.DetectedTargets;
                NativeArray<DistanceScoreContainer> best;
                try
                {
                    best = searchTeam.GetBestTargetScores(scanParams.FilterQueryIndex);
                }
                catch (Exception)
                {
                    // bad query index / buffer: vanilla makes the same call and throws the same exception itself
                    St.Vanilla++;
                    return true;
                }
                long refills = CollectSnapshot.St.Extra;
                n = CollectSnapshot.PrefilterPre(best, list, ref scanParams, searchTeam, ref s_Idx, ref s_T, ref s_W, ref s_R, ref s_P, out int walked);
                St.Extra += CollectSnapshot.St.Extra - refills;      // refills happen in the shared snapshot
                CollectSnapshot.St.Extra = refills;
                if (n < 0) { St.Vanilla++; CollectSnapshot.EndScan(); return true; }
                St.Fast++;
                St.Visited += walked;
                St.ListLen += list.Count;
                St.Passed += n;
                sampled = SelfCheck.Sample();
                if (sampled)
                {
                    St.Checks++;
                    __state = true;
                }
            }
            catch (Exception ex)
            {
                CollectSnapshot.EndScan();
                Item.Fail(ex, "CollectPreFilteredEnemies prefilter");
                __state = false;
                return true;   // nothing written yet
            }

            List<Target> outC = sampled ? s_ChkCands : cands;
            List<float> outS = sampled ? s_ChkScores : scores;
            try
            {
                outC.Clear();
                outS.Clear();
                Score(outC, outS, n, ref scanParams);
                Array.Clear(s_T, 0, n);
                return sampled;
            }
            catch (Exception ex)
            {
                outC.Clear();
                outS.Clear();
                Array.Clear(s_T, 0, s_T.Length);
                Item.Fail(ex, "CollectPreFilteredEnemies scoring");
                __state = false;
                return true;   // vanilla redoes the call over the same job output
            }
            finally
            {
                CollectSnapshot.EndScan();
            }
        }

        // TargetFinder.CollectPreFilteredEnemies after its filters, verbatim except for the replayed score and FastAdd.
        static void Score(List<Target> cands, List<float> scores, int n, ref TargetScanParams p)
        {
            float fromHealth = (!p.LineOfSightFromTarget || !p.LineOfSightFromTarget.DamageManager) ? 0f : p.LineOfSightFromTarget.DamageManager.Health;
            AIAiming aiming = p.Aiming;
            AIAiming unshootableAiming = (!aiming || !aiming.HasUnshootableTargets) ? null : aiming;
            CollectSnapshot.BeginScan(p.LineOfSightFromTarget, countForScoreItem: false);
            int minIdx = -1;
            float minVal = 0f;
            for (int k = 0; k < n; k++)
            {
                Target target = s_T[k];
                float targetAimToDirDot = Vector3.Dot((s_W[k] - p.Position).normalized, p.AimDir);
                float score = CollectSnapshot.Score(s_Idx[k], target, s_P[k], fromHealth, s_R[k], p.IgnoreAir, p.IgnoreGround, p.DamageType, p.Damage,
                                                    p.IgnoreUndamageable, targetAimToDirDot, withinView: false, p.CurrentTarget == target);
                if (!(score > 0f))
                {
                    if (float.IsNaN(score) && s_WarnNaN != null) s_WarnNaN.Invoke(null, new object[] { score, target, p });
                    continue;
                }
                if ((object)unshootableAiming != null && unshootableAiming.GetIsMarkedUnshootable(target))
                {
                    score *= 0.1f;
                }
                CollectSnapshot.AddCandidate(cands, scores, target, score, ref minIdx, ref minVal, FastStat);
            }
        }

        static void Postfix(TargetFinder __instance, ref TargetScanParams scanParams, bool __state)
        {
            if (!__state) return;
            List<Target> vc = s_Cands(__instance);
            List<float> vs = s_Scores(__instance);
            int n = vc.Count, k = -1;
            if (n != s_ChkCands.Count || vs.Count != s_ChkScores.Count) k = Math.Min(n, s_ChkCands.Count);
            else
            {
                for (int i = 0; i < n; i++)
                {
                    if (!ReferenceEquals(vc[i], s_ChkCands[i]) || BitConverter.SingleToInt32Bits(vs[i]) != BitConverter.SingleToInt32Bits(s_ChkScores[i])) { k = i; break; }
                }
            }
            if (k >= 0)
            {
                string detail = "team " + ((bool)scanParams.SearchTeam ? scanParams.SearchTeam.TeamShortName : "?") + ": vanilla " + n +
                                " candidates, ours " + s_ChkCands.Count + ", first difference at slot " + k;
                if (k < n && k < s_ChkCands.Count)
                {
                    detail += " (vanilla " + UrgentCloseGrid.Name(vc[k]) + " " + vs[k].ToString("R") + ", ours " + UrgentCloseGrid.Name(s_ChkCands[k]) + " " + s_ChkScores[k].ToString("R") + ")";
                }
                SelfCheck.Report(Item, St, detail);
            }
            s_ChkCands.Clear();
            s_ChkScores.Clear();
        }
    }
}
