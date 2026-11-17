/*
 DetectedGrid: a uniform XZ grid per team over that team's Team.DetectedTargets, used by the four "*_Grid" consumer
 items instead of walking the whole list.

 Exactness argument (what every consumer relies on)
 1. Candidate SET. The vanilla scans skip every entry farther than their range R from the query point q (3D or 2D
    distance of transform.position or of WorldLineOfSightCenter = position + rotation * ObjectInfo.LineOfSightCenter)
    before doing anything with side effects. The grid stores each entry's transform.position p0 at build time.
    |p - p0| <= Margin (see TargetingSettings.DetectedMarginMeters: rebuilt per frame and per physics step) and
    |WLOSC - p| = |LineOfSightCenter| <= MaxOffset (rotation keeps length) give: every entry vanilla keeps has
    XZ-distance(p0, q) <= R + Margin (+ MaxOffset for WLOSC queries) + 0.5 m slack, and those are exactly the
    entries the circle query returns (XZ distance <= 3D distance; the cell range is a monotonic function of the
    coordinate, so clamping to the border cells never drops an entry). Entries with a non-finite position go to an
    always-visited list. Unity-null entries are dropped: every consumer skips them and they cannot come back.
 2. List mutations between build and query. Team.LostTarget swap-removes, Team.DetectTarget appends, Team.Clear
    empties. Each grid entry is mapped to its CURRENT index through Team.DetectedTargetIndexes (maintained by those
    same three methods; checked with ReferenceEquals(list[i], t)), so removed entries vanish and moved ones get their
    new index. Appends are captured by the DetectedGrid item's postfix on Team.DetectTarget into an "added since
    build" list that every query includes; any count change it cannot explain marks the grid dirty (full rebuild).
 3. ORDER. The current indices are sorted ascending and de-duplicated, so the consumer's vanilla per-entry logic runs
    over the same entries in the same order as the full scan, minus entries it would have skipped by distance.
 The self-check compares exactly this (index sequence after the vanilla filters) or the vanilla return value.
 Never-worse gate (Gate/Learn): per team, the grid is used only while it pays (list >= 64, measured query cost
 < 0.5 of a full walk, enough queries per frame to amortise the build); otherwise queries return -2 and the consumer
 runs vanilla without anything being built. A bypassed team re-measures in one probe frame every 2 s.

 THREADING (for the multi-core stage)
 - Build(): main thread only (reads Team.DetectedTargets, transforms, ObjectInfo).
 - After Build() returns, a TeamGrid's X, Z, CellStart, Items, N, Nx, Nz, OriginX/Z, InvCell, MaxOffset, Always*
   (count only) are immutable until the next Build() of that grid. QueryCells() is static, allocation-free, uses
   no Unity API and touches only those arrays: safe to call from worker threads while no Build of that grid runs.
 - Refs / Added / Always hold Unity object references (side arrays, never read by QueryCells). ResolveIndices()
   reads Team.DetectedTargetIndexes and the Unity objects: main thread only.
 - The Team.DetectTarget postfix (AddAdded / Dirty) runs on the main thread; a worker stage must snapshot
   AddedN before scheduling.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiServerPerf.Modules.Targeting
{
    internal sealed class TeamGrid
    {
        public Team Team;
        public bool Dirty = true;
        public int BuiltFrame = -1;
        public float BuiltFixedTime = float.NaN;
        public float BuiltTime = float.NegativeInfinity;

        // geometry
        public float OriginX, OriginZ, InvCell = 0.01f;
        public int Nx = 1, Nz = 1;

        // entries (snapshot), parallel arrays, N used
        public int N;
        public float[] X = new float[64];
        public float[] Z = new float[64];
        public Target[] Refs = new Target[64];
        public float MaxOffset;

        // counting-sort cells: entries of cell c are Items[CellStart[c] .. CellStart[c+1])
        public int[] CellStart = new int[2];
        public int[] Items = new int[64];

        // entries without a finite position: always visited
        public int AlwaysN;
        public Target[] Always = new Target[8];

        // targets appended to the list after the build (Team.DetectTarget postfix)
        public int AddedN;
        public Target[] Added = new Target[16];

        // ---- never-worse gate (main thread). The grid is used only while it pays; otherwise queries go straight to
        // vanilla and NOTHING is built. Decided from cheap per-team measurements, with hysteresis.
        public bool UseGrid;            // current decision
        public string Reason = "not measured yet";
        public float Ratio = 0.5f;      // EWMA of visited entries / list length on grid queries (1 for a too-large query)
        public float Qpf;               // EWMA of grid-eligible queries per frame for this team
        public int CountFrame = -1, CountThisFrame;
        public float NextProbe;         // unscaled time of the next probe frame while bypassed
        public int ProbeFrame = -1;     // frame in which a bypassed team runs the grid path to measure
        public int LastD;
        public long GridQ, BypassQ;     // decisions, for the Info share

        public void AddAdded(Target t)
        {
            if (AddedN >= 4096) { Dirty = true; AddedN = 0; return; }   // never queried for a long time: rebuild later
            if (AddedN == Added.Length) Array.Resize(ref Added, Added.Length * 2);
            Added[AddedN++] = t;
        }
    }

    internal static class DetectedGrid
    {
        internal static bool On;
        internal static PerfItem Item;
        // counters (main thread), published by Publish()
        static long s_Builds, s_BuildEntries, s_Added, s_Dirty, s_Desync;
        static long p_Builds, p_BuildEntries, p_Added, p_Dirty;

        static float s_Cell = 100f, s_Margin = 16f, s_RebuildSeconds, s_MaxCellFraction = 0.5f;
        static TeamGrid[] s_Grids = new TeamGrid[8];
        static int s_GridCount;

        public static void Register(ModuleContext ctx, TargetingSettings s)
        {
            s_Cell = Mathf.Max(8f, s.DetectedCellSize);
            s_Margin = Mathf.Max(0f, s.DetectedMarginMeters);
            s_RebuildSeconds = Mathf.Max(0f, s.DetectedRebuildSeconds);
            s_MaxCellFraction = Mathf.Clamp(s.MaxCellFraction, 0.05f, 1f);

            Item = ctx.Item("DetectedGrid",
                "Per-team grid over Team.DetectedTargets (rebuilt per frame and physics step, additions tracked via Team.DetectTarget); needed by the *_Grid items, off = they run vanilla",
                v => On = v);

            var m = Item.Target(typeof(Team), nameof(Team.DetectTarget), new[] { typeof(Target), typeof(Sensor), typeof(bool) });
            Item.Patch(m, typeof(DetectedGrid), prefix: nameof(DetectPrefix), postfix: nameof(DetectPostfix));
            Item.OnSwitch(on => { if (on) InvalidateAll(); });
        }

        public static void InvalidateAll()
        {
            for (int i = 0; i < s_GridCount; i++)
            {
                s_Grids[i].Dirty = true;
                s_Grids[i].AddedN = 0;
            }
        }

        // ------------------------------------------------------------------ addition tracking

        static void DetectPrefix(Team __instance, out int __state)
        {
            __state = -1;
            if (!On) return;
            try
            {
                List<Target> list = __instance.DetectedTargets;
                __state = list != null ? list.Count : -1;
            }
            catch (Exception ex) { Item.Fail(ex, "Team.DetectTarget prefix"); }
        }

        static void DetectPostfix(Team __instance, Target target, int __state)
        {
            if (!On || __state < 0) return;
            try
            {
                List<Target> list = __instance.DetectedTargets;
                int c = list.Count;
                if (c == __state) return;
                TeamGrid g = Find(__instance);
                if (g == null) return;   // no grid yet: it is built from the full list on first use
                if (c == __state + 1 && ReferenceEquals(list[c - 1], target)) { g.AddAdded(target); s_Added++; }
                else { g.Dirty = true; s_Dirty++; }
            }
            catch (Exception ex) { Item.Fail(ex, "Team.DetectTarget postfix"); }
        }

        static TeamGrid Find(Team team)
        {
            for (int i = 0; i < s_GridCount; i++)
            {
                if (ReferenceEquals(s_Grids[i].Team, team)) return s_Grids[i];
            }
            return null;
        }

        static TeamGrid GetOrCreate(Team team)
        {
            TeamGrid g = Find(team);
            if (g != null) return g;
            if (s_GridCount == s_Grids.Length) Array.Resize(ref s_Grids, s_Grids.Length * 2);
            g = new TeamGrid { Team = team };
            s_Grids[s_GridCount++] = g;
            return g;
        }

        // ------------------------------------------------------------------ build (main thread)

        static TeamGrid Fresh(Team team)
        {
            TeamGrid g = GetOrCreate(team);
            int frame = Time.frameCount;
            if (!g.Dirty)
            {
                if (s_RebuildSeconds <= 0f)
                {
                    if (g.BuiltFrame == frame && g.BuiltFixedTime == Time.fixedTime) return g;
                }
                else if (Time.time - g.BuiltTime < s_RebuildSeconds)
                {
                    return g;
                }
            }
            Build(g, frame);
            return g;
        }

        static void Build(TeamGrid g, int frame)
        {
            List<Target> list = g.Team.DetectedTargets;
            int count = list.Count;
            if (g.X.Length < count)
            {
                int cap = Mathf.NextPowerOfTwo(count);
                g.X = new float[cap];
                g.Z = new float[cap];
                g.Refs = new Target[cap];
                g.Items = new int[cap];
            }
            else
            {
                Array.Clear(g.Refs, 0, g.N);   // drop references of the previous build
            }
            if (g.AlwaysN > 0) Array.Clear(g.Always, 0, g.AlwaysN);
            if (g.AddedN > 0) Array.Clear(g.Added, 0, g.AddedN);

            int n = 0, always = 0;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            float maxOff = 0f;
            float[] X = g.X, Z = g.Z;
            Target[] refs = g.Refs;
            for (int i = 0; i < count; i++)
            {
                Target t = list[i];
                if (t == null) continue;
                Vector3 p = t.transform.position;
                BaseGameObject owner = t.Owner;
                if ((object)owner != null)
                {
                    float off = owner.LineOfSightCenter.magnitude;
                    if (off > maxOff) maxOff = off;
                }
                if (!IsFinite(p.x) || !IsFinite(p.z))
                {
                    if (always == g.Always.Length) Array.Resize(ref g.Always, g.Always.Length * 2);
                    g.Always[always++] = t;
                    continue;
                }
                X[n] = p.x;
                Z[n] = p.z;
                refs[n] = t;
                n++;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }
            g.N = n;
            g.AlwaysN = always;
            g.AddedN = 0;
            g.MaxOffset = maxOff;

            float inv = 1f / s_Cell;
            if (n == 0) { minX = maxX = minZ = maxZ = 0f; }
            int nx = (int)((maxX - minX) * inv) + 1;
            int nz = (int)((maxZ - minZ) * inv) + 1;
            if (nx < 1) nx = 1; else if (nx > 256) nx = 256;
            if (nz < 1) nz = 1; else if (nz > 256) nz = 256;
            g.OriginX = minX;
            g.OriginZ = minZ;
            g.InvCell = inv;
            g.Nx = nx;
            g.Nz = nz;
            int cells = nx * nz;
            if (g.CellStart.Length < cells + 1) g.CellStart = new int[Mathf.NextPowerOfTwo(cells + 1)];
            int[] start = g.CellStart;
            Array.Clear(start, 0, cells + 1);
            // counting sort by cell, entries keep their list order inside a cell
            for (int e = 0; e < n; e++)
            {
                start[CellOf(X[e], minX, inv, nx) + CellOf(Z[e], minZ, inv, nz) * nx + 1]++;
            }
            for (int c = 0; c < cells; c++) start[c + 1] += start[c];
            int[] items = g.Items;
            // second pass writes through a running cursor kept in start[c] (shifted back afterwards)
            for (int e = 0; e < n; e++)
            {
                int c = CellOf(X[e], minX, inv, nx) + CellOf(Z[e], minZ, inv, nz) * nx;
                items[start[c]++] = e;
            }
            for (int c = cells; c > 0; c--) start[c] = start[c - 1];
            start[0] = 0;

            g.Dirty = false;
            g.BuiltFrame = frame;
            g.BuiltFixedTime = Time.fixedTime;
            g.BuiltTime = Time.time;
            s_Builds++;
            s_BuildEntries += n;
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        // Cell coordinate: monotonic non-decreasing in v, clamped to [0, n-1].
        static int CellOf(float v, float origin, float inv, int n)
        {
            float f = (v - origin) * inv;
            if (!(f > 0f)) return 0;            // also NaN
            if (f >= n) return n - 1;
            return (int)f;
        }

        // ------------------------------------------------------------------ query (worker-safe part)

        // Entries of g whose snapshot XZ position lies within r of (qx, qz). Static, no Unity API, no allocation.
        // out must hold g.N entries. visited counts the entries examined.
        public static int QueryCells(TeamGrid g, float qx, float qz, float r, int[] outEntries, ref long visited)
        {
            float inv = g.InvCell;
            int x0 = CellOf(qx - r, g.OriginX, inv, g.Nx), x1 = CellOf(qx + r, g.OriginX, inv, g.Nx);
            int z0 = CellOf(qz - r, g.OriginZ, inv, g.Nz), z1 = CellOf(qz + r, g.OriginZ, inv, g.Nz);
            float r2 = r * r;
            float[] X = g.X, Z = g.Z;
            int[] start = g.CellStart, items = g.Items;
            int n = 0, nx = g.Nx;
            long vis = 0;
            for (int cz = z0; cz <= z1; cz++)
            {
                int row = cz * nx;
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = row + cx;
                    int end = start[c + 1];
                    for (int k = start[c]; k < end; k++)
                    {
                        int e = items[k];
                        vis++;
                        float dx = X[e] - qx, dz = Z[e] - qz;
                        if (dx * dx + dz * dz <= r2) outEntries[n++] = e;
                    }
                }
            }
            visited += vis;
            return n;
        }

        // Fraction of the grid's cells a query of radius r visits.
        public static float CellFraction(TeamGrid g, float qx, float qz, float r)
        {
            float inv = g.InvCell;
            int x0 = CellOf(qx - r, g.OriginX, inv, g.Nx), x1 = CellOf(qx + r, g.OriginX, inv, g.Nx);
            int z0 = CellOf(qz - r, g.OriginZ, inv, g.Nz), z1 = CellOf(qz + r, g.OriginZ, inv, g.Nz);
            return (float)((x1 - x0 + 1) * (z1 - z0 + 1)) / (g.Nx * g.Nz);
        }

        // ------------------------------------------------------------------ query (main thread)

        static int[] s_Entries = new int[256];

        // Current DetectedTargets indices (ascending, unique) of every entry that can lie within `range` of (qx, qz),
        // measured from transform.position (losCenter false) or WorldLineOfSightCenter (true).
        // Returns -1 when the caller must run vanilla: switch off, no team, query too large, non-finite input,
        // or the Team index dictionary is out of step with the list.
        // Returns -2 when the never-worse gate bypasses the grid for this team (nothing built, caller runs vanilla).
        public static int Collect(Team team, float qx, float qz, float range, bool losCenter, ref int[] outIdx, Stat st)
        {
            if (!On || (object)team == null) return -1;
            if (!IsFinite(qx) || !IsFinite(qz) || !(range >= 0f) || float.IsInfinity(range)) return -1;
            TeamGrid gate = GetOrCreate(team);
            if (!Gate(gate, team.DetectedTargets.Count)) return -2;
            TeamGrid g = Fresh(team);
            float r = range + s_Margin + 0.5f + (losCenter ? g.MaxOffset : 0f);
            if (g.N > 64 && CellFraction(g, qx, qz, r) > s_MaxCellFraction)
            {
                Learn(g, 1.2f);   // built for nothing: counts as worse than vanilla
                return -1;
            }

            if (s_Entries.Length < g.N) s_Entries = new int[Mathf.NextPowerOfTwo(g.N)];
            long vis = 0;
            int ne = QueryCells(g, qx, qz, r, s_Entries, ref vis);
            vis += g.AlwaysN + g.AddedN;
            st.Visited += vis;
            // cost relative to vanilla's full walk: cell visits + ~3 per dictionary lookup, over the list length
            Learn(g, g.LastD > 0 ? (vis + 3f * (ne + g.AlwaysN + g.AddedN)) / g.LastD : 1f);

            int need = ne + g.AlwaysN + g.AddedN;
            if (outIdx.Length < need) outIdx = new int[Mathf.NextPowerOfTwo(need)];
            List<Target> list = team.DetectedTargets;
            Dictionary<Target, int> map = team.DetectedTargetIndexes;
            int listCount = list.Count;
            int m = 0;
            for (int k = 0; k < ne; k++)
            {
                if (!Resolve(map, list, listCount, g.Refs[s_Entries[k]], outIdx, ref m)) return Desync();
            }
            for (int k = 0; k < g.AlwaysN; k++)
            {
                if (!Resolve(map, list, listCount, g.Always[k], outIdx, ref m)) return Desync();
            }
            for (int k = 0; k < g.AddedN; k++)
            {
                if (!Resolve(map, list, listCount, g.Added[k], outIdx, ref m)) return Desync();
            }
            if (g.AddedN > 0 || g.AlwaysN > 0 || ne > 1)
            {
                SortInts(outIdx, m);
                m = Dedupe(outIdx, m);
            }
            return m;
        }

        // ---- never-worse gate
        // Cost model, in "vanilla entry visits": vanilla pays D per query. The grid pays ~D per build (one per frame /
        // physics step) plus c*D per query, c = measured (visited + 3*lookups)/D. It pays when Qpf*(1-c) > ~1.5.
        // Enter grid mode: D >= 64, c < 0.5, Qpf*(1-c) >= 2. Leave: D < 48, c > 0.7 or Qpf*(1-c) < 1.2.
        // While bypassed (and D >= 64) one probe frame every 2 s runs the grid path to re-measure c.
        const int EnterD = 64, LeaveD = 48;
        const float EnterC = 0.5f, LeaveC = 0.7f, EnterGain = 2f, LeaveGain = 1.2f, ProbeSeconds = 2f;

        static bool Gate(TeamGrid g, int d)
        {
            g.LastD = d;
            int frame = Time.frameCount;
            if (frame != g.CountFrame)
            {
                int gap = g.CountFrame < 0 ? 1 : frame - g.CountFrame;
                float decay = 0.9f;
                for (int i = 1; i < gap && i < 40; i++) decay *= 0.9f;   // frames without queries count as 0
                g.Qpf = g.Qpf * decay + 0.1f * g.CountThisFrame;
                g.CountFrame = frame;
                g.CountThisFrame = 0;
            }
            g.CountThisFrame++;

            if (g.UseGrid)
            {
                float gain = g.Qpf * (1f - g.Ratio);
                if (d < LeaveD) Bypass(g, "short list");
                else if (g.Ratio > LeaveC) Bypass(g, "no pruning");
                else if (gain < LeaveGain) Bypass(g, "few queries per build");
                else { g.GridQ++; return true; }
                g.BypassQ++;
                return false;
            }
            if (d < EnterD) { g.Reason = "short list"; g.BypassQ++; return false; }
            if (frame == g.ProbeFrame) { g.GridQ++; return true; }
            float now = Time.unscaledTime;
            if (now >= g.NextProbe)
            {
                g.NextProbe = now + ProbeSeconds;
                g.ProbeFrame = frame;
                g.GridQ++;
                return true;
            }
            g.BypassQ++;
            return false;
        }

        static void Bypass(TeamGrid g, string reason)
        {
            g.UseGrid = false;
            g.Reason = reason;
            g.NextProbe = Time.unscaledTime + ProbeSeconds;
        }

        static void Learn(TeamGrid g, float c)
        {
            g.Ratio = g.Ratio * 0.8f + 0.2f * c;
            if (!g.UseGrid && g.LastD >= EnterD && g.Ratio < EnterC && g.Qpf * (1f - g.Ratio) >= EnterGain)
            {
                g.UseGrid = true;
                g.Reason = "pays";
            }
            else if (!g.UseGrid)
            {
                g.Reason = g.Ratio >= EnterC ? "no pruning" : "few queries per build";
            }
        }

        static int Desync()
        {
            s_Desync++;
            InvalidateAll();
            return -1;
        }

        public static void Publish(float dt)
        {
            if (Item == null || !Item.Applied) return;
            long b = s_Builds - p_Builds, be = s_BuildEntries - p_BuildEntries, a = s_Added - p_Added, d = s_Dirty - p_Dirty;
            p_Builds = s_Builds; p_BuildEntries = s_BuildEntries; p_Added = s_Added; p_Dirty = s_Dirty;
            Item.Info = "grids " + s_GridCount + " | builds/s " + (b / dt).ToString("0.0") +
                        ", entries/build " + (b > 0 ? ((double)be / b).ToString("0") : "-") +
                        " | added/s " + (a / dt).ToString("0.0") + ", dirty/s " + (d / dt).ToString("0.0") +
                        " | index desync total " + s_Desync + TeamModes();
        }

        static string TeamModes()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s_GridCount; i++)
            {
                TeamGrid g = s_Grids[i];
                long tot = g.GridQ + g.BypassQ;
                sb.Append(" | ").Append((bool)g.Team ? g.Team.TeamShortName : "?").Append(g.UseGrid ? " GRID" : " bypass")
                  .Append(" (").Append(g.Reason).Append(") D ").Append(g.LastD)
                  .Append(" cost ").Append(g.Ratio.ToString("0.00")).Append(" q/frame ").Append(g.Qpf.ToString("0.0"))
                  .Append(" grid share ").Append(tot > 0 ? (100.0 * g.GridQ / tot).ToString("0") : "-").Append('%');
                g.GridQ = 0;
                g.BypassQ = 0;
            }
            return sb.ToString();
        }

        // false = the dictionary points at a slot holding another target (out of step): caller falls back.
        static bool Resolve(Dictionary<Target, int> map, List<Target> list, int listCount, Target t, int[] outIdx, ref int m)
        {
            if (!map.TryGetValue(t, out int i)) return true;   // no longer detected
            if (i < 0 || i >= listCount || !ReferenceEquals(list[i], t)) return false;
            outIdx[m++] = i;
            return true;
        }

        public static void SortInts(int[] a, int n)
        {
            if (n > 32)
            {
                Array.Sort(a, 0, n);
                return;
            }
            for (int i = 1; i < n; i++)
            {
                int v = a[i], j = i - 1;
                while (j >= 0 && a[j] > v) { a[j + 1] = a[j]; j--; }
                a[j + 1] = v;
            }
        }

        static int Dedupe(int[] a, int n)
        {
            if (n < 2) return n;
            int w = 1;
            for (int i = 1; i < n; i++)
            {
                if (a[i] != a[w - 1]) a[w++] = a[i];
            }
            return w;
        }
    }
}
