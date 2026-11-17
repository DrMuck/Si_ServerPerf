/*
 CommanderAI_ClusterRebuild: exact single-threaded replacement of AIDefenseHandler.RebuildClusters (vanilla commander,
 called once per AIDefenseHandler.Think, i.e. once per commander think; live under Si_RTS_AI too).

 1.0.17 vanilla (Silica.AI/AIDefenseHandler.cs:393-518), unchanged in structure since 1.0.15:
   reset Seen on every cluster; r2 = GetBaseClusterRadius()^2; scratch = Team.Structures that are alive and !IsDestroyed
   while scratch not empty:
     for every k: count_k = #{ l in scratch : DistanceSq2D(pos(l), pos(k)) <= r2 } (+1000 if k is a base structure);
                  first k with the highest count is the seed                                  -> O(n^2) per cluster
     walk scratch from the end: every s with !(DistanceSq2D(pos(s), pos(seed)) > r2) is a member: added to
       m_ClusterMembers, base count / value (Max(0, ObjectInfo.Cost)) / position sum accumulated, RemoveAt
     no member -> break; else center = SnapToGrid(sum / members, 10); GetOrCreateCluster(center) gets the fields,
       Extent = max Distance2D(center, member) in m_ClusterMembers order
   release + remove clusters not Seen (back to front); the main cluster = most base structures, then highest Value.
 Every pos() is transform.position (two native calls), read 2 n^2 times per cluster: O(C n^2) native reads.

 Replacement (prefix, returns false):
   1. Read phase, nothing mutated: for every alive structure in Team.Structures order: position, whether ObjectInfo is
      alive, whether it == Team.BaseStructure, Max(0, Cost). Each read exactly once.
   2. Compute phase (pure arithmetic on those arrays): the same loop with the same predicate, the same GameMath calls
      with the same argument order, the same member walk (back to front) and the same float sum order, the same
      tie-break (first index in the order-preserving remaining list), the same break.
      Neighbour counts: Settings.ClusterIncrementalCounts (default true) computes count_k once over all structures and,
      after each cluster, subtracts for every remaining q the removed r with DistanceSq2D(pos(r), pos(q)) <= r2. That is
      vanilla's predicate with vanilla's argument order (other, self), so each count equals the vanilla recount over
      the remaining list without any symmetry assumption; integers, so exact. O(n^2) total instead of O(C n^2).
      false = recount every iteration from the cached positions (O(C n^2), no native calls).
   3. Apply phase, in vanilla order: Seen = false on all clusters; per cluster record GetOrCreateCluster(center) and the
      same field writes; m_ClusterScratch / m_ClusterMembers left exactly as vanilla leaves them; the not-Seen release
      loop and the main-cluster pick replicated line by line.

 Why exact:
   - Nothing inside RebuildClusters moves or destroys a structure. Object.Destroy is deferred to the end of the frame,
     DestroyImmediate is not used, physics does not step, so every transform.position read in one call returns the
     same Vector3; ObjectInfo / Cost / Team.BaseStructure (a lazily filled cache) are constant too.
   - A transform "destroyed mid-loop" therefore cannot happen in vanilla: scratch only holds structures that passed
     (bool)s && !s.IsDestroyed in the same call. If any read threw anyway, vanilla throws in its first selection pass
     (k = 0 reads every l), before any cluster is touched. Ours does all reads in phase 1 before mutating anything;
     an exception there -> Item.Fail and vanilla runs, which hits the same exception at the same point.
   - Phase 2 cannot throw (array arithmetic). A throw in phase 3 can only come from vanilla's own GetOrCreateCluster /
     ReleaseClusterPosts, reached in vanilla's order: Item.Fail and rethrow, as vanilla would throw there.
   - BaseCluster is a private nested class: fields through FieldInfo, the private methods through
     MethodInfo.Invoke (once per cluster, not per pair). Resolved once; any member missing -> SKIPPED.

 VerifyAgainstVanilla: phase 1+2 run dry, vanilla runs, a postfix compares vanilla's Seen clusters (center bitwise,
 count, value, base count, extent) and the left-over scratch count with the dry result; vanilla's result stays.
 Counters: calls, ms in ours, vanilla pair checks avoided (sum n^2 over iterations); with verify also vanilla ms.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Services
{
    internal static class ClusterRebuild
    {
        internal static bool On;
        static PerfItem Item;
        static bool _incremental = true, _verify;

        static AccessTools.FieldRef<AIDefenseHandler, List<Structure>> _scratchRef;
        static AccessTools.FieldRef<AIDefenseHandler, List<Vector3>> _membersRef;
        static FieldInfo _clustersField;
        static MethodInfo _getRadius, _getOrCreate, _release;
        static Func<Vector3, float, Vector3> _snap;
        static FieldInfo _fCenter, _fExtent, _fCount, _fValue, _fBaseCount, _fIsMain, _fSeen;

        // phase-1/2 working arrays (main thread only, grown on demand)
        static Structure[] _s = new Structure[64];
        static Vector3[] _p = new Vector3[64];
        static bool[] _hasInfo = new bool[64], _isBase = new bool[64];
        static int[] _cost = new int[64], _cnt = new int[64], _rem = new int[64], _removed = new int[64];
        static int _n, _remCount;

        struct Rec { public Vector3 Center; public int Count, Value, BaseCount; public float Extent; public int MemberStart, MemberCount; }
        static readonly List<Rec> _recs = new List<Rec>(16);
        static readonly List<Vector3> _memberPos = new List<Vector3>(64);   // members of all records, in vanilla add order
        static readonly object[] _arg1 = new object[1];

        // counters
        static long _calls, _vanillaRuns, _pairChecksAvoided, _ticksOurs, _ticksOursMax, _verified, _mismatches, _ticksVanilla;
        static long _vStart;
        static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        public static void Register(ModuleContext ctx, ServicesSettings s)
        {
            Item = ctx.Item("CommanderAI_ClusterRebuild",
                "AIDefenseHandler.RebuildClusters with every structure position read once and neighbour counts kept incrementally (same arithmetic, same order, same result); vanilla commander think spike",
                v => On = v);
            _incremental = s.ClusterIncrementalCounts;
            _verify = s.VerifyAgainstVanilla;

            Type h = typeof(AIDefenseHandler);
            Type bc = AccessTools.Inner(h, "BaseCluster");
            _scratchRef = Refl.Field<AIDefenseHandler, List<Structure>>("m_ClusterScratch", Item);
            _membersRef = Refl.Field<AIDefenseHandler, List<Vector3>>("m_ClusterMembers", Item);
            _clustersField = AccessTools.Field(h, "m_Clusters");
            _getRadius = Refl.Method(h, "GetBaseClusterRadius", Type.EmptyTypes, Item);
            _getOrCreate = Refl.Method(h, "GetOrCreateCluster", new[] { typeof(Vector3) }, Item);
            _release = bc == null ? null : Refl.Method(h, "ReleaseClusterPosts", new[] { bc }, Item);
            MethodInfo snap = Refl.Method(h, "SnapToGrid", new[] { typeof(Vector3), typeof(float) }, Item);
            try { if (snap != null && snap.IsStatic) _snap = (Func<Vector3, float, Vector3>)Delegate.CreateDelegate(typeof(Func<Vector3, float, Vector3>), snap); }
            catch (Exception ex) { Item.Note("SnapToGrid delegate: " + ex.Message); }
            if (bc != null)
            {
                _fCenter = ObjRef<Vector3>(bc, "Center");
                _fExtent = ObjRef<float>(bc, "Extent");
                _fCount = ObjRef<int>(bc, "StructureCount");
                _fValue = ObjRef<int>(bc, "Value");
                _fBaseCount = ObjRef<int>(bc, "BaseStructureCount");
                _fIsMain = ObjRef<bool>(bc, "IsMain");
                _fSeen = ObjRef<bool>(bc, "Seen");
            }
            if (bc == null || _scratchRef == null || _membersRef == null || _clustersField == null || _getRadius == null || _getOrCreate == null ||
                _release == null || _snap == null || _fCenter == null || _fExtent == null || _fCount == null || _fValue == null ||
                _fBaseCount == null || _fIsMain == null || _fSeen == null || _getOrCreate.ReturnType != bc)
            {
                Item.Block("AIDefenseHandler cluster members not as on 1.0.17");
            }

            var m = Item.Target(h, "RebuildClusters", Type.EmptyTypes, declaredOnly: true);
            Item.Patch(m, typeof(ClusterRebuild), prefix: nameof(Prefix), postfix: nameof(Postfix));
        }

        // BaseCluster is private: plain FieldInfo (a handful of reads/writes per cluster, never per structure pair).
        static FieldInfo ObjRef<F>(Type t, string name)
        {
            FieldInfo f = AccessTools.Field(t, name);
            if (f == null || f.IsStatic || f.FieldType != typeof(F)) { Item.Note("field BaseCluster." + name + " absent"); return null; }
            return f;
        }
        static void Set(FieldInfo f, object o, object v) => f.SetValue(o, v);
        static bool Seen(object c) => (bool)_fSeen.GetValue(c);
        static int Int(FieldInfo f, object c) => (int)f.GetValue(c);

        // MethodInfo.Invoke wraps the callee's exception; rethrow the original so the caller sees what vanilla throws.
        static object Call(MethodInfo m, object target, object[] args)
        {
            try { return m.Invoke(target, args); }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw;
            }
        }

        static bool Prefix(AIDefenseHandler __instance, out bool __state)
        {
            __state = false;
            if (!On) return true;
            long t0 = Stopwatch.GetTimestamp();
            float r2;
            try
            {
                r2 = Read(__instance);       // phase 1 (no mutation)
                Compute(r2);                 // phase 2 (pure)
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "RebuildClusters read/compute");
                return true;                 // vanilla from the start, nothing was changed
            }
            if (_verify)
            {
                _ticksOurs += Stopwatch.GetTimestamp() - t0;
                __state = true;              // postfix compares; vanilla's result is kept
                _vStart = Stopwatch.GetTimestamp();
                return true;
            }
            try
            {
                ApplyResult(__instance);     // phase 3 (vanilla order)
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "RebuildClusters apply (exception from GetOrCreateCluster / ReleaseClusterPosts)");
                throw;                       // vanilla would have thrown at this point too
            }
            long d = Stopwatch.GetTimestamp() - t0;
            _calls++; _ticksOurs += d; if (d > _ticksOursMax) _ticksOursMax = d;
            return false;
        }

        static void Postfix(AIDefenseHandler __instance, bool __state)
        {
            if (!__state) return;
            try
            {
                _ticksVanilla += Stopwatch.GetTimestamp() - _vStart;
                _vanillaRuns++;
                _verified++;
                if (!SameAsVanilla(__instance)) _mismatches++;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "RebuildClusters verify");
            }
        }

        // ---------------------------------------------------------------- phase 1
        static float Read(AIDefenseHandler h)
        {
            float radius = (float)Call(_getRadius, h, null);
            float r2 = radius * radius;
            Team team = h.Team;
            List<Structure> structs = team.Structures;
            int count = structs.Count;
            Grow(count);
            ObjectInfo baseInfo = null;
            bool baseRead = false;
            int n = 0;
            for (int j = 0; j < count; j++)
            {
                Structure s = structs[j];
                if (!s || s.IsDestroyed) continue;
                _s[n] = s;
                _p[n] = s.transform.position;
                ObjectInfo info = s.ObjectInfo;
                bool has = info;
                _hasInfo[n] = has;
                if (has)
                {
                    if (!baseRead) { baseInfo = team.BaseStructure; baseRead = true; }
                    _isBase[n] = info == baseInfo;
                    _cost[n] = Mathf.Max(0, info.Cost);
                }
                else
                {
                    _isBase[n] = false;
                    _cost[n] = 0;
                }
                n++;
            }
            _n = n;
            return r2;
        }

        static void Grow(int need)
        {
            if (_s.Length >= need) return;
            int size = Math.Max(need, _s.Length * 2);
            _s = new Structure[size]; _p = new Vector3[size];
            _hasInfo = new bool[size]; _isBase = new bool[size];
            _cost = new int[size]; _cnt = new int[size]; _rem = new int[size]; _removed = new int[size];
        }

        // ---------------------------------------------------------------- phase 2
        static void Compute(float r2)
        {
            _recs.Clear();
            _memberPos.Clear();
            int m = _n;
            for (int i = 0; i < m; i++) _rem[i] = i;    // remaining, in scratch order
            if (_incremental)
            {
                // count_k over all, vanilla argument order: DistanceSq2D(pos(l), pos(k))
                for (int k = 0; k < m; k++)
                {
                    Vector3 pk = _p[k];
                    int c = 0;
                    for (int l = 0; l < m; l++) if (GameMath.DistanceSq2D(_p[l], pk) <= r2) c++;
                    _cnt[k] = c;
                }
            }
            while (m > 0)
            {
                _pairChecksAvoided += (long)m * m;
                int index = 0, best = -1;
                for (int k = 0; k < m; k++)
                {
                    int sk = _rem[k];
                    int c;
                    if (_incremental) c = _cnt[sk];
                    else
                    {
                        c = 0;
                        Vector3 pk = _p[sk];
                        for (int l = 0; l < m; l++) if (GameMath.DistanceSq2D(_p[_rem[l]], pk) <= r2) c++;
                    }
                    if (_hasInfo[sk] && _isBase[sk]) c += 1000;
                    if (c > best) { best = c; index = k; }
                }
                Vector3 seed = _p[_rem[index]];
                int baseCount = 0, value = 0, members = 0, removed = 0;
                Vector3 sum = Vector3.zero;
                int memberStart = _memberPos.Count;
                // back to front, exactly like vanilla's RemoveAt walk; survivors keep their relative order
                for (int k = m - 1; k >= 0; k--)
                {
                    int sk = _rem[k];
                    if (!(GameMath.DistanceSq2D(_p[sk], seed) > r2))
                    {
                        _memberPos.Add(_p[sk]);
                        if (_hasInfo[sk])
                        {
                            if (_isBase[sk]) baseCount++;
                            value += _cost[sk];
                        }
                        sum += _p[sk];
                        members++;
                        _removed[removed++] = sk;
                        _rem[k] = -1;
                    }
                }
                if (removed > 0)
                {
                    int w = 0;
                    for (int k = 0; k < m; k++) if (_rem[k] >= 0) _rem[w++] = _rem[k];
                    m = w;
                    if (_incremental)
                    {
                        for (int q = 0; q < m; q++)
                        {
                            int sq = _rem[q];
                            Vector3 pq = _p[sq];
                            int dec = 0;
                            for (int r = 0; r < removed; r++) if (GameMath.DistanceSq2D(_p[_removed[r]], pq) <= r2) dec++;
                            _cnt[sq] -= dec;
                        }
                    }
                }
                if (members <= 0)
                {
                    _memberPos.RemoveRange(memberStart, _memberPos.Count - memberStart);
                    _recs.Add(new Rec { Count = 0, MemberStart = memberStart, MemberCount = 0 }); // marks the break
                    break;
                }
                Vector3 center = _snap(sum / members, 10f);
                float extent = 0f;
                for (int i = memberStart; i < _memberPos.Count; i++)
                {
                    float d = GameMath.Distance2D(center, _memberPos[i]);
                    if (d > extent) extent = d;
                }
                _recs.Add(new Rec { Center = center, Count = members, Value = value, BaseCount = baseCount, Extent = extent, MemberStart = memberStart, MemberCount = members });
            }
            _remCount = m;
        }

        // ---------------------------------------------------------------- phase 3
        static void ApplyResult(AIDefenseHandler h)
        {
            IList clusters = (IList)_clustersField.GetValue(h);
            for (int i = 0; i < clusters.Count; i++) Set(_fSeen, clusters[i], false);
            for (int r = 0; r < _recs.Count; r++)
            {
                Rec rec = _recs[r];
                if (rec.Count <= 0) break;
                _arg1[0] = rec.Center;
                object c = Call(_getOrCreate, h, _arg1);
                Set(_fCenter, c, rec.Center);
                Set(_fCount, c, rec.Count);
                Set(_fValue, c, rec.Value);
                Set(_fBaseCount, c, rec.BaseCount);
                Set(_fIsMain, c, false);
                Set(_fSeen, c, true);
                Set(_fExtent, c, rec.Extent);
            }
            // scratch: what vanilla leaves (empty, or the rest after a break); members: the last iteration's
            List<Structure> scratch = _scratchRef(h);
            scratch.Clear();
            for (int i = 0; i < _remCount; i++) scratch.Add(_s[_rem[i]]);
            // vanilla clears m_ClusterMembers only inside the loop: with no structure it keeps the previous content
            if (_recs.Count > 0)
            {
                List<Vector3> members = _membersRef(h);
                members.Clear();
                Rec last = _recs[_recs.Count - 1];
                for (int i = 0; i < last.MemberCount; i++) members.Add(_memberPos[last.MemberStart + i]);
            }
            Array.Clear(_s, 0, _n);   // no references kept between calls
            for (int i = clusters.Count - 1; i >= 0; i--)
            {
                object c = clusters[i];
                if (!Seen(c))
                {
                    _arg1[0] = c;
                    Call(_release, h, _arg1);
                    clusters.RemoveAt(i);
                }
            }
            if (clusters.Count <= 0) return;
            int index2 = 0;
            for (int n = 1; n < clusters.Count; n++)
            {
                object a = clusters[n], b = clusters[index2];
                int ab = Int(_fBaseCount, a), bb = Int(_fBaseCount, b);
                if (ab != bb) { if (ab > bb) index2 = n; }
                else if (Int(_fValue, a) > Int(_fValue, b)) index2 = n;
            }
            Set(_fIsMain, clusters[index2], true);
        }

        // ---------------------------------------------------------------- verify
        static bool SameAsVanilla(AIDefenseHandler h)
        {
            Array.Clear(_s, 0, _n);
            int recs = 0;
            for (int r = 0; r < _recs.Count; r++) if (_recs[r].Count > 0) recs++;
            IList clusters = (IList)_clustersField.GetValue(h);
            if (clusters.Count != recs) return false;   // vanilla keeps exactly the Seen clusters
            if (_scratchRef(h).Count != _remCount) return false;
            var used = new bool[clusters.Count];
            for (int r = 0; r < _recs.Count; r++)
            {
                Rec rec = _recs[r];
                if (rec.Count <= 0) continue;
                bool found = false;
                for (int i = 0; i < clusters.Count && !found; i++)
                {
                    if (used[i]) continue;
                    object c = clusters[i];
                    if (Seen(c) && ((Vector3)_fCenter.GetValue(c)).Equals(rec.Center) && Int(_fCount, c) == rec.Count && Int(_fValue, c) == rec.Value &&
                        Int(_fBaseCount, c) == rec.BaseCount && ((float)_fExtent.GetValue(c)).Equals(rec.Extent))
                    {
                        used[i] = true;
                        found = true;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        internal static void RefreshInfo()
        {
            if (Item == null || !Item.Applied) return;
            string s = "calls " + _calls + ", ours " + (_ticksOurs * TicksToMs).ToString("F1") + " ms total (max " + (_ticksOursMax * TicksToMs).ToString("F2") +
                       " ms), vanilla pair checks avoided " + _pairChecksAvoided + (_incremental ? " (incremental counts)" : " (positions once)");
            if (_verify)
            {
                s += " | VERIFY: checked " + _verified + ", mismatches " + _mismatches + ", vanilla " + (_ticksVanilla * TicksToMs).ToString("F1") +
                     " ms vs ours " + (_ticksOurs * TicksToMs).ToString("F1") + " ms over " + _vanillaRuns + " calls";
            }
            Item.Info = s;
        }
    }
}
