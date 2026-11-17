/*
 The four DetectedTargets scans served from DetectedGrid (exact, see DetectedGrid.cs for the argument).
 Each prefix replays the vanilla per-entry logic verbatim over the grid's ascending index list; only the entries the
 vanilla loop would have dropped by distance are never visited.

 CollectEnemies_Grid  TargetFinder.CollectDetectedEnemies (private, `in TargetScanParams`). Hottest caller:
                      22.5 ms/s self at 500 units, 26.6 us per call (findings F 3.1). Range = scan range from
                      WorldLineOfSightCenter. Candidate order matters (AddCandidate replaces the lowest score when 24
                      are held; Rank breaks ties by candidate order): preserved.
 UrgentClose_Grid     AIAiming.FindUrgentCloseEnemy (private): first enemy unit within 15 + r + 30 m (transform
                      position) in list order; order preserved.
 NearestTarget_Grid   Team.FindNearestTarget (CreatureDecapod): nearest, ties -> last in list order; order preserved.
 AlarmProximity_Grid  AlarmProximity.OrderedFixedUpdate (25 Hz per alarm): "any offensive enemy within 600 m 2D";
                      a boolean, order-independent. The rest of the method is replayed verbatim (same
                      UnityEngine.Random call sequence). Destroyed owner / no team: vanilla runs.

 Self-check: CollectEnemies compares the index sequence that survives the vanilla filters up to and including the
 distance test against a literal full scan (scoring is not run twice: it has a 32-entry type-value cache); the other
 three run the original method on sampled calls and compare its return value / flag.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.Targeting
{
    internal struct CheckState
    {
        public bool Sampled;
        public Target Fast;
    }

    // ====================================================================== TargetFinder.CollectDetectedEnemies

    internal static class CollectEnemiesGrid
    {
        internal static bool On;
        static PerfItem Item;
        static Stat St;
        static AccessTools.FieldRef<TargetFinder, List<Target>> s_Cands;
        static AccessTools.FieldRef<TargetFinder, List<float>> s_Scores;
        static MethodInfo s_WarnNaN;

        static int[] s_Idx = new int[256];
        // prefiltered sequence (vanilla order): index, target, WorldLineOfSightCenter, distance
        static int[] s_SeqIdx = new int[64];
        static Target[] s_SeqT = new Target[64];
        static Vector3[] s_SeqW = new Vector3[64];
        static float[] s_SeqD = new float[64];
        // self-check reference sequence
        static int[] s_RefIdx = new int[64];
        static Target[] s_RefT = new Target[64];
        static Vector3[] s_RefW = new Vector3[64];
        static float[] s_RefD = new float[64];

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("CollectEnemies_Grid",
                "TargetFinder.CollectDetectedEnemies visits only DetectedGrid cells within the scan range (same candidates, same order)",
                v => On = v);
            St = new Stat(Item, "D");
            s_Cands = Refl.Field<TargetFinder, List<Target>>("m_Candidates", Item);
            s_Scores = Refl.Field<TargetFinder, List<float>>("m_Scores", Item);
            s_WarnNaN = Refl.Method(typeof(TargetFinder), "WarnNaNScore",
                new[] { typeof(float), typeof(Target), typeof(TargetScanParams).MakeByRefType() }, Item);
            if (s_Cands == null || s_Scores == null) Item.Block("TargetFinder.m_Candidates / m_Scores absent");
            var m = Item.Target(typeof(TargetFinder), "CollectDetectedEnemies", new[] { typeof(TargetScanParams).MakeByRefType() });
            Item.Patch(m, typeof(CollectEnemiesGrid), prefix: nameof(Prefix), postfix: nameof(Postfix));
        }

        internal static bool Applied => Item != null && Item.Applied;

        internal struct CollectCheck
        {
            public bool Sampled;
            public bool Snapshot;
        }

        static Vector3[] s_SeqP = new Vector3[64];
        static readonly List<Target> s_ChkCands = new List<Target>(24);
        static readonly List<float> s_ChkScores = new List<float>(24);

        // One prefix for the grid path (this item) and the snapshot path (CollectEnemies_Snapshot*, which ride on it).
        static bool Prefix(TargetFinder __instance, ref TargetScanParams scanParams, out CollectCheck __state)
        {
            __state = default;
            bool snap = CollectSnapshot.On;
            if (!On && !snap) return true;
            Stat st = St;
            List<Target> cands;
            List<float> scores;
            int nSeq;
            List<Target> list;
            bool sampled;
            try
            {
                Team searchTeam = scanParams.SearchTeam;
                cands = s_Cands(__instance);
                scores = s_Scores(__instance);
                if (On) st.Calls++;
                if (snap) CollectSnapshot.St.Calls++;
                // RequestScan clears both lists before CollectCandidates; anything else: vanilla (cannot be undone)
                if ((object)searchTeam == null || cands.Count != 0 || scores.Count != 0)
                {
                    if (On) st.Vanilla++;
                    if (snap) CollectSnapshot.St.Vanilla++;
                    return true;
                }
                list = searchTeam.DetectedTargets;
                int m = -1;
                if (On)
                {
                    Vector3 pos = scanParams.Position;
                    m = DetectedGrid.Collect(searchTeam, pos.x, pos.z, scanParams.Range, true, ref s_Idx, st);
                    if (m < 0) { if (m == -2) st.Bypass++; else st.Vanilla++; }
                    else { st.Fast++; st.ListLen += list.Count; }
                }
                bool grid = m >= 0;
                if (!grid && !snap) return true;
                if (snap)
                {
                    CollectSnapshot.St.Fast++;
                    nSeq = CollectSnapshot.Prefilter(list, grid ? s_Idx : null, grid ? m : list.Count, ref scanParams, searchTeam,
                                                     ref s_SeqIdx, ref s_SeqT, ref s_SeqW, ref s_SeqD, ref s_SeqP);
                }
                else
                {
                    nSeq = Prefilter(list, s_Idx, m, ref scanParams, searchTeam, false, ref s_SeqIdx, ref s_SeqT, ref s_SeqW, ref s_SeqD);
                }
                if (grid) st.Passed += nSeq;
                sampled = SelfCheck.Sample();
                if (sampled)
                {
                    if (snap) CollectSnapshot.St.Checks++; else st.Checks++;
                    if (snap && CollectSnapshot.ScoreOn) CollectSnapshot.ScoreSt.Checks++;
                    if (CollectSnapshot.FastAddOn) CollectSnapshot.FastSt.Checks++;
                    __state = new CollectCheck { Sampled = true, Snapshot = snap };
                }
            }
            catch (Exception ex)
            {
                if (snap) CollectSnapshot.Fail(ex, "CollectDetectedEnemies snapshot prefilter");
                else Item.Fail(ex, "CollectDetectedEnemies prefilter");
                __state = default;
                return true;   // nothing written yet
            }

            // sampled: our full result goes into scratch lists, vanilla runs into the real ones, the postfix compares
            List<Target> outC = sampled ? s_ChkCands : cands;
            List<float> outS = sampled ? s_ChkScores : scores;
            try
            {
                outC.Clear();
                outS.Clear();
                Score(outC, outS, list, nSeq, ref scanParams, snap);
                Array.Clear(s_SeqT, 0, nSeq);
                return sampled;
            }
            catch (Exception ex)
            {
                // undo our additions; vanilla redoes the call over the same entries in the same order
                outC.Clear();
                outS.Clear();
                Array.Clear(s_SeqT, 0, s_SeqT.Length);
                if (snap) CollectSnapshot.Fail(ex, "CollectDetectedEnemies snapshot scoring");
                else Item.Fail(ex, "CollectDetectedEnemies scoring");
                __state = default;
                return true;
            }
            finally
            {
                CollectSnapshot.EndScan();
            }
        }

        static void Postfix(TargetFinder __instance, ref TargetScanParams scanParams, CollectCheck __state)
        {
            if (!__state.Sampled) return;
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
                string path = (__state.Snapshot ? "snapshot" : "grid") + (CollectSnapshot.ScoreOn && __state.Snapshot ? "+score" : "") + (CollectSnapshot.FastAddOn ? "+fastadd" : "");
                string detail = "path " + path + ", team " + ((bool)scanParams.SearchTeam ? scanParams.SearchTeam.TeamShortName : "?") + ": vanilla " + n +
                                " candidates, ours " + s_ChkCands.Count + ", first difference at slot " + k;
                if (k < n && k < s_ChkCands.Count)
                {
                    detail += " (vanilla " + UrgentCloseGrid.Name(vc[k]) + " " + vs[k].ToString("R") + ", ours " + UrgentCloseGrid.Name(s_ChkCands[k]) + " " + s_ChkScores[k].ToString("R") + ")";
                }
                SelfCheck.Report(__state.Snapshot ? CollectSnapshot.ItemRef : Item, __state.Snapshot ? CollectSnapshot.St : St, detail);
                // counted on every stage that took part (the comparison cannot tell which one differed)
                if (__state.Snapshot && CollectSnapshot.ScoreOn) CollectSnapshot.ScoreSt.Mismatch++;
                if (CollectSnapshot.FastAddOn) CollectSnapshot.FastSt.Mismatch++;
            }
            s_ChkCands.Clear();
            s_ChkScores.Clear();
        }
        static void Swap<T>(ref T a, ref T b) { T t = a; a = b; b = t; }

        // Vanilla filters up to and including the range test. idx == null: full scan 0..n-1 (reference).
        static int Prefilter(List<Target> list, int[] idx, int n, ref TargetScanParams p, Team searchTeam, bool full,
                             ref int[] oIdx, ref Target[] oT, ref Vector3[] oW, ref float[] oD)
        {
            if (oIdx.Length < n)
            {
                int cap = Mathf.NextPowerOfTwo(n);
                oIdx = new int[cap]; oT = new Target[cap]; oW = new Vector3[cap]; oD = new float[cap];
            }
            int k = 0;
            for (int j = 0; j < n; j++)
            {
                int i = full ? j : idx[j];
                Target target = list[i];
                if (target == null || target.IsDestroyed || !Team.GetTeamsAreEnemy(target.Team, searchTeam) ||
                    (p.IgnoreStructures && ((bool)target.OwnerStructure || (bool)target.OwnerConstructionSite)))
                {
                    continue;
                }
                Vector3 w = target.Owner.WorldLineOfSightCenter;
                float d = Vector3.Distance(w, p.Position);
                if (d > p.Range)
                {
                    continue;
                }
                oIdx[k] = i; oT[k] = target; oW[k] = w; oD[k] = d;
                k++;
            }
            return k;
        }

        static bool SameSequence(int nSeq, int nRef)
        {
            if (nSeq != nRef) return false;
            for (int k = 0; k < nSeq; k++) if (s_SeqIdx[k] != s_RefIdx[k]) return false;
            return true;
        }

        static string Describe(Team team, ref TargetScanParams p, int listCount, int nSeq, int nRef)
        {
            int k = 0;
            while (k < nSeq && k < nRef && s_SeqIdx[k] == s_RefIdx[k]) k++;
            string s = "team " + team.TeamShortName + ", D " + listCount + ", range " + p.Range.ToString("0.0") +
                       " at " + p.Position + ": grid " + nSeq + " vs vanilla " + nRef + " in range, first difference at #" + k;
            if (k < nRef && (bool)s_RefT[k]) s += " (vanilla has " + s_RefT[k].Owner.name + " idx " + s_RefIdx[k] + " d " + s_RefD[k].ToString("0.0") + " pos " + s_RefT[k].transform.position + ")";
            else if (k < nSeq && (bool)s_SeqT[k]) s += " (grid has " + s_SeqT[k].Owner.name + " idx " + s_SeqIdx[k] + ")";
            return s;
        }

        // Vanilla loop body after the range test, verbatim.
        // Stage 2 (CollectEnemies_SnapshotScore, only with the snapshot prefilter) replaces CalculateBestTargetScore by
        // its snapshot replay; CollectEnemies_FastAdd replaces AddCandidate's min search. Everything else verbatim.
        static void Score(List<Target> cands, List<float> scores, List<Target> list, int nSeq, ref TargetScanParams p, bool snap)
        {
            bool replay = snap && CollectSnapshot.ScoreOn;
            bool fastAdd = CollectSnapshot.FastAddOn;
            int minIdx = -1;
            float minVal = 0f;
            if (replay) CollectSnapshot.BeginScan(p.LineOfSightFromTarget);
            bool flag = p.Mode == ETargetScanMode.DetectedEnemiesWithinView;
            float fromHealth = (!p.LineOfSightFromTarget || !p.LineOfSightFromTarget.DamageManager) ? 0f : p.LineOfSightFromTarget.DamageManager.Health;
            AIAiming aiming = p.Aiming;
            AIAiming unshootableAiming = (!aiming || !aiming.HasUnshootableTargets) ? null : aiming;
            for (int k = 0; k < nSeq; k++)
            {
                Target target = s_SeqT[k];
                Vector3 worldLineOfSightCenter = s_SeqW[k];
                float num = s_SeqD[k];
                bool withinView = flag && GetWithinView(ref p, worldLineOfSightCenter, target);
                float targetAimToDirDot = Vector3.Dot((worldLineOfSightCenter - p.Position).normalized, p.AimDir);
                num = replay
                    ? CollectSnapshot.Score(s_SeqIdx[k], target, s_SeqP[k], fromHealth, num / p.Range, p.IgnoreAir, p.IgnoreGround,
                        p.DamageType, p.Damage, p.IgnoreUndamageable, targetAimToDirDot, withinView, p.CurrentTarget == target)
                    : GameAI.CalculateBestTargetScore(p.LineOfSightFromTarget, fromHealth, target, num / p.Range, p.IgnoreAir, p.IgnoreGround,
                        p.DamageType, p.Damage, p.IgnoreUndamageable, targetAimToDirDot, withinView, p.CurrentTarget == target);
                if (!(num > 0f))
                {
                    if (float.IsNaN(num) && s_WarnNaN != null) s_WarnNaN.Invoke(null, new object[] { num, target, p });
                    continue;
                }
                if ((object)unshootableAiming != null && unshootableAiming.GetIsMarkedUnshootable(target))
                {
                    num *= 0.1f;
                }
                if (fastAdd) CollectSnapshot.AddCandidate(cands, scores, target, num, ref minIdx, ref minVal, CollectSnapshot.FastSt);
                else AddCandidate(cands, scores, target, num);
            }
            if (fastAdd) CollectSnapshot.FastAddScan();
        }

        // TargetFinder.AddCandidate, verbatim.
        static void AddCandidate(List<Target> cands, List<float> scores, Target target, float score)
        {
            if (cands.Count < 24)
            {
                cands.Add(target);
                scores.Add(score);
                return;
            }
            int index = 0;
            float num = scores[0];
            for (int i = 1; i < scores.Count; i++)
            {
                if (!(scores[i] >= num))
                {
                    num = scores[i];
                    index = i;
                }
            }
            if (!(score <= num))
            {
                cands[index] = target;
                scores[index] = score;
            }
        }

        // TargetFinder.GetWithinView, verbatim.
        static bool GetWithinView(ref TargetScanParams scanParams, Vector3 targetCenter, Target target)
        {
            if (!scanParams.YawViewTransform || !scanParams.PitchViewTransform)
            {
                return false;
            }
            if (!target.OwnerUnit)
            {
                return false;
            }
            float yawToPoint = GameMath.GetYawToPoint(scanParams.YawViewTransform, targetCenter);
            if (yawToPoint < scanParams.ViewLimits.x || yawToPoint > scanParams.ViewLimits.y)
            {
                return false;
            }
            Vector3 point = scanParams.PitchViewTransform.InverseTransformPoint(targetCenter);
            point.z = Mathf.Sqrt(Mathf.Abs(point.x * point.z));
            point.x = 0f;
            float pitchToPoint = GameMath.GetPitchToPoint(point);
            pitchToPoint *= -1f;
            if (pitchToPoint >= scanParams.ViewLimits.z)
            {
                return pitchToPoint <= scanParams.ViewLimits.w;
            }
            return false;
        }
    }

    // ====================================================================== AIAiming.FindUrgentCloseEnemy

    internal static class UrgentCloseGrid
    {
        internal static bool On;
        static PerfItem Item;
        static Stat St;
        static AccessTools.FieldRef<AIAiming, Target[]> s_LookedAt;
        static int[] s_Idx = new int[64];

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("UrgentClose_Grid",
                "AIAiming.FindUrgentCloseEnemy visits only DetectedGrid cells within 15 + r + 30 m (same first hit in list order)",
                v => On = v);
            St = new Stat(Item, "D");
            s_LookedAt = Refl.Field<AIAiming, Target[]>("m_UrgentLookedAt", Item);
            if (s_LookedAt == null) Item.Block("AIAiming.m_UrgentLookedAt absent");
            var m = Item.Target(typeof(AIAiming), "FindUrgentCloseEnemy", new[] { typeof(Target) });
            Item.Patch(m, typeof(UrgentCloseGrid), prefix: nameof(Prefix), postfix: nameof(Postfix));
        }

        static bool Prefix(AIAiming __instance, Target owner, ref Target __result, out CheckState __state)
        {
            __state = default;
            if (!On) return true;
            try
            {
                St.Calls++;
                if ((object)owner == null) { St.Vanilla++; return true; }
                Team team = owner.Team;
                if (!team) { St.Vanilla++; return true; }   // vanilla returns null right away
                Vector3 position = owner.transform.position;
                float physicalRadius = owner.PhysicalRadius;
                float num = 15f + physicalRadius + 30f;
                float num2 = num * num;
                int m = DetectedGrid.Collect(team, position.x, position.z, Mathf.Abs(num), false, ref s_Idx, St);
                if (m < 0) { if (m == -2) St.Bypass++; else St.Vanilla++; return true; }
                List<Target> detectedTargets = team.DetectedTargets;
                St.Fast++;
                St.ListLen += detectedTargets.Count;
                St.Passed += m;
                Target aim = __instance.AimTargetIgnorePaused;
                Target[] lookedAt = s_LookedAt(__instance);
                Target found = null;
                for (int j = 0; j < m; j++)
                {
                    Target target = detectedTargets[s_Idx[j]];
                    if (!target || target == aim)
                    {
                        continue;
                    }
                    float num3 = GameMath.DistanceSq(target.transform.position, position);
                    if (!(num3 > num2) && (bool)target.OwnerUnit && !target.IsDestroyed && owner.GetIsEnemy(target.Team))
                    {
                        float num4 = 15f + physicalRadius + target.PhysicalRadius;
                        if (!(num3 > num4 * num4) && !LookedAt(lookedAt, target) && target.GetDetectedByTeam(team))
                        {
                            found = target;
                            break;
                        }
                    }
                }
                if (SelfCheck.Sample())
                {
                    St.Checks++;
                    __state = new CheckState { Sampled = true, Fast = found };
                    return true;
                }
                __result = found;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "FindUrgentCloseEnemy");
                __state = default;
                return true;
            }
        }

        static void Postfix(Target owner, Target __result, CheckState __state)
        {
            if (!__state.Sampled) return;
            if (!ReferenceEquals(__result, __state.Fast))
            {
                SelfCheck.Report(Item, St, "owner " + Name(owner) + ": vanilla " + Name(__result) + ", grid " + Name(__state.Fast));
            }
        }

        static bool LookedAt(Target[] lookedAt, Target attacker)
        {
            for (int i = 0; i < 4; i++)
            {
                if (lookedAt[i] == attacker)
                {
                    return true;
                }
            }
            return false;
        }

        internal static string Name(Target t) => (bool)t && (bool)t.Owner ? t.Owner.name + " @" + t.transform.position : "null";
    }

    // ====================================================================== Team.FindNearestTarget

    internal static class NearestTargetGrid
    {
        internal static bool On;
        static PerfItem Item;
        static Stat St;
        static int[] s_Idx = new int[64];

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("NearestTarget_Grid",
                "Team.FindNearestTarget visits only DetectedGrid cells within range (same nearest, same tie-break)",
                v => On = v);
            St = new Stat(Item, "D");
            var m = Item.Target(typeof(Team), nameof(Team.FindNearestTarget),
                new[] { typeof(Vector3), typeof(float), typeof(EDamageType), typeof(float), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            Item.Patch(m, typeof(NearestTargetGrid), prefix: nameof(Prefix), postfix: nameof(Postfix));
        }

        static bool Prefix(Team __instance, Vector3 position, float range, EDamageType eDamageType, float damage, bool ignoreUndamageable,
                           bool ignoreAir, bool ignoreGround, bool ignoreStructures, ref Target __result, out CheckState __state)
        {
            __state = default;
            if (!On) return true;
            try
            {
                St.Calls++;
                int m = DetectedGrid.Collect(__instance, position.x, position.z, range, false, ref s_Idx, St);
                if (m < 0) { if (m == -2) St.Bypass++; else St.Vanilla++; return true; }
                List<Target> detectedTargets = __instance.DetectedTargets;
                St.Fast++;
                St.ListLen += detectedTargets.Count;
                St.Passed += m;
                Target target = null;
                float num = 0f;
                for (int j = 0; j < m; j++)
                {
                    Target target2 = detectedTargets[s_Idx[j]];
                    if (target2 == null || target2.IsDestroyed || !Team.GetTeamsAreEnemy(target2.Team, __instance) ||
                        (ignoreStructures && ((bool)target2.OwnerStructure || (bool)target2.OwnerConstructionSite)))
                    {
                        continue;
                    }
                    float num2 = Vector3.Distance(target2.transform.position, position);
                    if (num2 > range)
                    {
                        continue;
                    }
                    Unit ownerUnit = target2.OwnerUnit;
                    if ((bool)ownerUnit && ((ignoreAir && ownerUnit.IsFlying) || (ignoreGround && !ownerUnit.IsFlying)))
                    {
                        continue;
                    }
                    if (eDamageType != EDamageType.None)
                    {
                        DamageManager damageManager = target2.DamageManager;
                        if ((bool)damageManager)
                        {
                            if (damageManager.AdjustDamage(damage, eDamageType, limitToHealth: false) <= 0f && ignoreUndamageable)
                            {
                                continue;
                            }
                        }
                        else if (ignoreUndamageable)
                        {
                            continue;
                        }
                    }
                    if (!target || !(num2 > num))
                    {
                        target = target2;
                        num = num2;
                    }
                }
                if (SelfCheck.Sample())
                {
                    St.Checks++;
                    __state = new CheckState { Sampled = true, Fast = target };
                    return true;
                }
                __result = target;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "FindNearestTarget");
                __state = default;
                return true;
            }
        }

        static void Postfix(Vector3 position, float range, Target __result, CheckState __state)
        {
            if (!__state.Sampled) return;
            if (!ReferenceEquals(__result, __state.Fast))
            {
                SelfCheck.Report(Item, St, "at " + position + " range " + range + ": vanilla " + UrgentCloseGrid.Name(__result) + ", grid " + UrgentCloseGrid.Name(__state.Fast));
            }
        }
    }

    // ====================================================================== AlarmProximity.OrderedFixedUpdate

    internal static class AlarmProximityGrid
    {
        internal static bool On;
        static PerfItem Item;
        static Stat St;
        static AccessTools.FieldRef<AlarmProximity, BaseGameObject> s_Owner;
        static AccessTools.FieldRef<AlarmProximity, bool> s_AlarmEnabled;
        static AccessTools.FieldRef<AlarmProximity, float> s_Time;
        static int[] s_Idx = new int[64];

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("AlarmProximity_Grid",
                "AlarmProximity.OrderedFixedUpdate: 'offensive enemy within ProximityDistance' from DetectedGrid cells (same flag; rest of the method verbatim)",
                v => On = v);
            St = new Stat(Item, "D");
            s_Owner = Refl.Field<AlarmProximity, BaseGameObject>("Owner", Item);
            s_AlarmEnabled = Refl.Field<AlarmProximity, bool>("AlarmEnabled", Item);
            s_Time = Refl.Field<AlarmProximity, float>("Time", Item);
            if (s_Owner == null || s_AlarmEnabled == null || s_Time == null) Item.Block("AlarmProximity private fields absent");
            var m = Item.Target(typeof(AlarmProximity), nameof(AlarmProximity.OrderedFixedUpdate), new[] { typeof(float) });
            Item.Patch(m, typeof(AlarmProximityGrid), prefix: nameof(Prefix));
        }

        static bool Prefix(AlarmProximity __instance, float dt)
        {
            if (!On) return true;
            bool flag;
            try
            {
                St.Calls++;
                if (dt <= 0f) return true;
                BaseGameObject owner = s_Owner(__instance);
                if ((bool)owner && owner.IsDestroyed) { St.Vanilla++; return true; }   // vanilla clears FX + unregisters
                Team team = (owner ? owner.Team : null);
                if (!team) return true;
                Vector3 position = __instance.transform.position;
                float num = __instance.ProximityDistance * __instance.ProximityDistance;
                int m = DetectedGrid.Collect(team, position.x, position.z, Mathf.Abs(__instance.ProximityDistance), false, ref s_Idx, St);
                if (m < 0) { if (m == -2) St.Bypass++; else St.Vanilla++; return true; }
                List<Target> list = team.DetectedTargets;
                St.Fast++;
                St.ListLen += list.Count;
                St.Passed += m;
                flag = Scan(list, s_Idx, m, false, team, position, num);
                if (SelfCheck.Sample())
                {
                    St.Checks++;
                    bool vanilla = Scan(list, null, list.Count, true, team, position, num);
                    if (vanilla != flag)
                    {
                        SelfCheck.Report(Item, St, "alarm " + (owner ? owner.name : "?") + " @" + position + ": vanilla " + vanilla + ", grid " + flag);
                        flag = vanilla;
                    }
                }
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "AlarmProximity flag");
                return true;   // nothing changed yet
            }

            // rest of the vanilla method, verbatim
            ref float time = ref s_Time(__instance);
            ref bool alarmEnabled = ref s_AlarmEnabled(__instance);
            AudioEffectHandler audio = __instance.AudioEffectHandler;
            GameObject enableOnAlarm = __instance.EnableOnAlarm;
            float proximityTime = __instance.ProximityTime;
            if (flag)
            {
                time = Mathf.Clamp(time + dt, 0f, proximityTime);
                if (time >= proximityTime && !alarmEnabled && UnityEngine.Random.Range(0f, 1f) < __instance.TriggerChance)
                {
                    alarmEnabled = true;
                    if ((bool)audio)
                    {
                        audio.PlayOnce(0);
                    }
                    if ((bool)enableOnAlarm)
                    {
                        enableOnAlarm.SetActive(value: true);
                    }
                }
            }
            else
            {
                time = Mathf.Clamp(time - dt, 0f, proximityTime);
                if (time <= 0f && alarmEnabled)
                {
                    alarmEnabled = false;
                    if ((bool)audio)
                    {
                        audio.PlayOnce(1);
                    }
                    if ((bool)enableOnAlarm)
                    {
                        enableOnAlarm.SetActive(value: false);
                    }
                }
            }
            if ((bool)audio)
            {
                audio.ModulateLooping(alarmEnabled ? 1f : 0f, 1f, __instance.SoundLerpUp, __instance.SoundLerpDown);
            }
            return false;
        }

        // Vanilla flag loop. idx == null: full scan (reference).
        static bool Scan(List<Target> list, int[] idx, int n, bool full, Team team, Vector3 position, float num)
        {
            for (int j = 0; j < n; j++)
            {
                Target target = list[full ? j : idx[j]];
                if (target.GetIsEnemy(team) && !target.IsDestroyed)
                {
                    ObjectInfo objectInfo = target.ObjectInfo;
                    if ((bool)objectInfo && objectInfo.IsForOffense && !(GameMath.DistanceSq2D(position, target.transform.position) > num))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
