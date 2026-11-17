/*
 TargetFinder internals.

 Rank_Sort (exact): TargetFinder.Rank is a selection sort, O(C^2) with C <= 24: for each slot it takes the FIRST
 unplaced candidate with the strictly greatest score (start float.MinValue). For scores that are all > float.MinValue
 and not NaN (every caller adds only scores > 0) that is exactly a stable sort by descending score: equal scores keep
 their candidate order. Replaced by a stable insertion sort (~C^2/4 compares, no list indexer churn). Any NaN /
 <= float.MinValue score, or more than 24 candidates (vanilla would throw on m_RankPlaced), runs vanilla.

 LosTopK (approximate, default off): after CollectCandidates, when the scan will issue LOS queries
 (LineOfSightFromTarget with an Owner), keeps only the K best candidates by vanilla's rank order (score desc, then
 candidate order), in their original order. Rank over the kept ones gives vanilla's first K ranks, so Select picks
 the same target whenever one of the top K has a clear line of sight; when all K are blocked the agent gets no
 target this scan where vanilla would have taken a lower-ranked visible one. Saves up to 24 - K LOS queries (each
 = sensors x LOS transforms rays) per scan.
*/

using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SiServerPerf.Modules.Targeting
{
    internal static class RankSort
    {
        internal static bool On;
        static PerfItem Item;
        static Stat St;
        static AccessTools.FieldRef<TargetFinder, List<Target>> s_Cands;
        static AccessTools.FieldRef<TargetFinder, List<float>> s_Scores;
        static AccessTools.FieldRef<TargetFinder, List<int>> s_Ranked;
        static readonly int[] s_Order = new int[24];

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("Rank_Sort",
                "TargetFinder.Rank: stable insertion sort instead of the O(C^2) selection sort (identical order, ties included)",
                v => On = v);
            St = new Stat(Item, null);
            s_Cands = Refl.Field<TargetFinder, List<Target>>("m_Candidates", Item);
            s_Scores = Refl.Field<TargetFinder, List<float>>("m_Scores", Item);
            s_Ranked = Refl.Field<TargetFinder, List<int>>("m_Ranked", Item);
            if (s_Cands == null || s_Scores == null || s_Ranked == null) Item.Block("TargetFinder rank fields absent");
            var m = Item.Target(typeof(TargetFinder), "Rank", Type.EmptyTypes);
            Item.Patch(m, typeof(RankSort), prefix: nameof(Prefix), postfix: nameof(Postfix));
        }

        static bool Prefix(TargetFinder __instance, out bool __state)
        {
            __state = false;
            if (!On) return true;
            try
            {
                St.Calls++;
                List<float> scores = s_Scores(__instance);
                int count = s_Cands(__instance).Count;
                if (count > 24 || count > scores.Count) { St.Vanilla++; return true; }
                for (int i = 0; i < count; i++)
                {
                    if (!(scores[i] > float.MinValue)) { St.Vanilla++; return true; }
                }
                int[] order = s_Order;
                for (int i = 0; i < count; i++)
                {
                    float s = scores[i];
                    int j = i;
                    while (j > 0 && scores[order[j - 1]] < s)
                    {
                        order[j] = order[j - 1];
                        j--;
                    }
                    order[j] = i;
                }
                if (SelfCheck.Sample())
                {
                    St.Checks++;
                    __state = true;     // vanilla ranks, postfix compares with s_Order
                    return true;
                }
                List<int> ranked = s_Ranked(__instance);
                ranked.Clear();
                for (int i = 0; i < count; i++) ranked.Add(order[i]);
                St.Fast++;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "TargetFinder.Rank");
                __state = false;
                return true;
            }
        }

        static void Postfix(TargetFinder __instance, bool __state)
        {
            if (!__state) return;
            List<int> ranked = s_Ranked(__instance);
            int count = s_Cands(__instance).Count;
            bool same = ranked.Count == count;
            for (int i = 0; same && i < count; i++) same = ranked[i] == s_Order[i];
            if (!same) SelfCheck.Report(Item, St, "rank order differs for " + count + " candidates");
        }
    }

    internal static class LosTopK
    {
        internal static bool On;
        static PerfItem Item;
        static Stat St;
        static int s_K = 6;
        static AccessTools.FieldRef<TargetFinder, List<Target>> s_Cands;
        static AccessTools.FieldRef<TargetFinder, List<float>> s_Scores;
        static readonly bool[] s_Keep = new bool[64];

        public static void Register(ModuleContext ctx, TargetingSettings s)
        {
            s_K = Math.Max(1, s.LosTopK);
            Item = ctx.Item("LosTopK",
                "APPROXIMATE: TargetFinder issues LOS queries only for the top LosTopK candidates (no target when all of them are blocked)",
                v => On = v, defaultOn: false);
            St = new Stat(Item, null, "LOS queries saved");
            s_Cands = Refl.Field<TargetFinder, List<Target>>("m_Candidates", Item);
            s_Scores = Refl.Field<TargetFinder, List<float>>("m_Scores", Item);
            if (s_Cands == null || s_Scores == null) Item.Block("TargetFinder candidate fields absent");
            var m = Item.Target(typeof(TargetFinder), "CollectCandidates", new[] { typeof(TargetScanParams).MakeByRefType() });
            Item.Patch(m, typeof(LosTopK), postfix: nameof(Postfix));
        }

        static void Postfix(TargetFinder __instance, ref TargetScanParams scanParams)
        {
            if (!On) return;
            try
            {
                St.Calls++;
                Target los = scanParams.LineOfSightFromTarget;
                if (!los || !los.Owner) return;   // no LOS queries in this scan
                List<Target> cands = s_Cands(__instance);
                List<float> scores = s_Scores(__instance);
                int n = cands.Count, k = s_K;
                if (n <= k || n > s_Keep.Length || scores.Count != n) return;
                for (int i = 0; i < n; i++)
                {
                    if (!(scores[i] > float.MinValue)) return;
                }
                // vanilla rank of i = candidates that Rank places before it
                for (int i = 0; i < n; i++)
                {
                    float si = scores[i];
                    int rank = 0;
                    for (int j = 0; j < n; j++)
                    {
                        float sj = scores[j];
                        if (sj > si || (sj == si && j < i)) rank++;
                    }
                    s_Keep[i] = rank < k;
                }
                int w = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!s_Keep[i]) continue;
                    cands[w] = cands[i];
                    scores[w] = scores[i];
                    w++;
                }
                cands.RemoveRange(w, n - w);
                scores.RemoveRange(w, n - w);
                St.Fast++;
                St.Extra += n - w;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "TargetFinder.CollectCandidates postfix");
            }
        }
    }
}
