/*
 FriendlyFire_Grid (exact) and FriendlyFire_Cache (approximate) on Target.CheckRiskOfFriendlyFire.

 Vanilla (1.0.17 Target.cs:662-795): takes ONE 1 km TargetGrid cell (the one holding instigator.transform.position) of
 the scan team, TargetGrid.TeamGridCellIndices[team.TeamTargetsId][cx, cz], and walks every target index in it. A
 target is in that list iff TargetGrid.CellRanges[i] is Valid, has that TeamId and its [XMin..XMax]x[ZMin..ZMax]
 covers (cx, cz) (TargetGridFillCellJob, all i < Target.Targets.Count at the last TargetGrid.Update). Per entry it
 box-tests TargetGrid.LOSPositions[LOSBeginIndex] against fromPoint +- maxDist, then runs the cone / damage /
 raycast tests and returns true at the first hit. Called per frame per aligned AI weapon (VehicleTurret,
 TurretWeapon, HumanHandsAnimator, CreatureAttack) and once per enemy team from CheckRiskOfHittingEnemies.

 FriendlyFire_Grid builds, once per TargetGrid.Update (lazily, at the first query after it), its own fine XZ grid
 per team from the SAME data vanilla reads (TargetDataNativeArray, LOSPositions, CellRanges: all written only inside
 TargetGrid.Update, so the snapshot is never stale relative to vanilla). A query visits the fine cells covering
 fromPoint +- (maxDist + 1 m), keeps entries that pass vanilla's own membership test (CellRanges cover (cx, cz)),
 instance-id test and box test, and runs the vanilla per-entry body verbatim inline, stopping at the first hit like
 vanilla. The answer is an OR over entries whose body has no side effects (DamageManager.AdjustDamage's value cache
 aside), so it is the vanilla answer; the visiting order can only change which raycasts run before the hit.
 Never-worse gate (class Gate): per team slot, a probe from the raw TargetGrid arrays every 0.5 s measures vanilla's
 cell length L and how many of those lie near the muzzle (V); the grid and its snapshot build are used only when V/L
 is small and enough queries per frame amortise the build. Otherwise the call goes straight to vanilla.
 Entries with a non-finite LOS position are visited by every query of their team (vanilla's NaN box test passes).
 Coupling: if another module rate-limits or replaces TargetGrid.Update, this stays exact as long as
 TeamGridCellIndices keeps being derived from CellRanges (the self-check will show it if not). The TargetGrid.Update
 postfix uses __runOriginal, so a prefix that skips the original does not mark the snapshot fresh.

 THREADING: BuildGlobal / BuildTeam read NativeArrays and Target.Targets on the main thread. After they return the
 E_* arrays, TeamStart/TeamItems and the per-team CellStart/Items are immutable until the next TargetGrid.Update;
 QueryCandidates() is static, allocation-free, Unity-free and safe on worker threads. The per-entry body
 (Targets[index], DamageManager, Physics.Raycast) is main thread only.

 FriendlyFire_Cache (approximate, default off): reuses the last answer of the same weapon (same instigator,
 projectile, scan-team override, ignore target, unrepairable flag) for FriendlyFireCacheSeconds while the muzzle moved
 < FriendlyFireCacheMoveMeters, turned < FriendlyFireCacheAngleDeg and distance changed < FriendlyFireCacheDistMeters.
 A friendly that steps into the cone during that time is not seen (shot fired) or a weapon holds fire up to that long
 after the cone cleared.
*/

using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;

namespace SiServerPerf.Modules.Targeting
{
    internal static class FriendlyFireGrid
    {
        internal static bool On;
        // true while this call runs vanilla for the grid's self-check (read by FriendlyFire_Flat's later prefix)
        internal static bool CheckingVanilla;
        static PerfItem Item;
        static Stat St;
        static AccessTools.FieldRef<NativeArray<TargetCellRange>> s_CellRanges;

        static bool s_Valid;        // a TargetGrid.Update ran (with this item on) since the last invalidation
        static int s_Version;       // bumped per TargetGrid.Update
        static int s_BuiltVersion = -1;
        static int s_TargetCount;
        static int s_FilledSlots;
        static float s_Cell = 100f;
        static bool s_GlobalOk;

        // global snapshot (structure of arrays), N entries
        static int N;
        static int[] E_Index = new int[256], E_Inst = new int[256];
        static int[] E_XMin = new int[256], E_XMax = new int[256], E_ZMin = new int[256], E_ZMax = new int[256];
        static float[] E_X = new float[256], E_Y = new float[256], E_Z = new float[256];
        static int[] E_Team = new int[256];
        // entries grouped by team slot: TeamItems[TeamStart[s] .. TeamStart[s+1]) in ascending target index
        static int TeamSlots;
        static int[] TeamStart = new int[16];
        static int[] TeamItems = new int[256];
        // shared geometry
        static float OriginX, OriginZ, Inv = 0.01f;
        static int Nx = 1, Nz = 1;

        sealed class TeamCells
        {
            public int BuiltVersion = -1;
            public int[] CellStart = new int[2];
            public int[] Items = new int[16];
            public int AlwaysN;
            public int[] Always = new int[4];
        }

        // ---- never-worse gate, per team slot (main thread)
        // Cost model in "entry visits": vanilla walks the 1 km cell list (L entries) per query. The grid pays one snapshot
        // per TargetGrid.Update (~T targets + the team's cells) plus ~V visits per query (entries in the fine cells
        // around fromPoint +- maxDist). Measured by a probe from TargetGrid's raw arrays (O(T), no build) on the first
        // query of a slot every 0.5 s, whatever the mode. Enter grid mode: L >= 48, V/L < 0.35 and
        // Qpf*(L-V) >= 2*build; leave: L < 32, V/L > 0.5 or Qpf*(L-V) < build. Bypassed queries build nothing.
        sealed class Gate
        {
            public bool Use;
            public string Reason = "not measured yet";
            public float RatioVL = 1f, L, Qpf;
            public int CountFrame = -1, CountThisFrame;
            public float NextProbe;
        }
        static Gate[] s_Gates = new Gate[16];
        const float ProbeSeconds = 0.5f;

        static Gate GateFor(int slot)
        {
            if (slot >= s_Gates.Length) Array.Resize(ref s_Gates, Math.Max(slot + 1, s_Gates.Length * 2));
            Gate g = s_Gates[slot];
            if (g == null) s_Gates[slot] = g = new Gate();
            int frame = Time.frameCount;
            if (frame != g.CountFrame)
            {
                int gap = g.CountFrame < 0 ? 1 : frame - g.CountFrame;
                float decay = 0.9f;
                for (int i = 1; i < gap && i < 40; i++) decay *= 0.9f;
                g.Qpf = g.Qpf * decay + 0.1f * g.CountThisFrame;
                g.CountFrame = frame;
                g.CountThisFrame = 0;
            }
            g.CountThisFrame++;
            return g;
        }

        // Raw-array probe: L = vanilla's cell-list length for (cx, cz), V = those entries near fromPoint (box + half a
        // fine cell, i.e. roughly what the fine grid would visit). Decides the mode. Never touches the snapshot.
        static void Probe(Gate g, int slot, Vector3 from, float maxDist, int cx, int cz)
        {
            NativeArray<TargetData> tda = TargetGrid.TargetDataNativeArray;
            NativeArray<Vector3> los = TargetGrid.LOSPositions;
            NativeArray<TargetCellRange> cr = s_CellRanges();
            int count = s_TargetCount;
            if (!tda.IsCreated || !los.IsCreated || !cr.IsCreated || count > tda.Length || count > cr.Length) return;
            int losLen = los.Length, l = 0, v = 0;
            float r = maxDist + 0.5f * s_Cell;
            for (int i = 0; i < count; i++)
            {
                TargetCellRange c = cr[i];
                if (!c.Valid || c.TeamId != slot || c.XMin > cx || cx > c.XMax || c.ZMin > cz || cz > c.ZMax) continue;
                l++;
                int b = tda[i].LOSBeginIndex;
                if (b < 0 || b >= losLen) continue;
                Vector3 p = los[b];
                if (Mathf.Abs(p.x - from.x) <= r && Mathf.Abs(p.z - from.z) <= r) v++;
            }
            g.L = l;
            g.RatioVL = l > 0 ? (float)v / l : 1f;
            float build = count + 0.25f * Nx * Nz;
            float gain = g.Qpf * (l - v);
            if (g.Use)
            {
                if (l < 32) { g.Use = false; g.Reason = "short cell list"; }
                else if (g.RatioVL > 0.5f) { g.Use = false; g.Reason = "no pruning (dense cluster)"; }
                else if (gain < build) { g.Use = false; g.Reason = "few queries per snapshot"; }
            }
            else
            {
                if (l < 48) g.Reason = "short cell list";
                else if (g.RatioVL >= 0.35f) g.Reason = "no pruning (dense cluster)";
                else if (gain < 2f * build) g.Reason = "few queries per snapshot";
                else { g.Use = true; g.Reason = "pays"; }
            }
        }

        static string ModeText()
        {
            var sb = new System.Text.StringBuilder();
            for (int s = 0; s < s_Gates.Length; s++)
            {
                Gate g = s_Gates[s];
                if (g == null) continue;
                sb.Append(sb.Length > 0 ? " | " : "").Append("slot ").Append(s).Append(g.Use ? " GRID (" : " bypass (").Append(g.Reason)
                  .Append(") cell L ").Append(g.L.ToString("0")).Append(" near/L ").Append(g.RatioVL.ToString("0.00"))
                  .Append(" q/frame ").Append(g.Qpf.ToString("0.0"));
            }
            return sb.ToString();
        }

        public static void Publish() { if (St != null) St.Mode = ModeText(); }
        static TeamCells[] s_Teams = new TeamCells[16];


        public static void Register(ModuleContext ctx, TargetingSettings s)
        {
            s_Cell = Mathf.Max(8f, s.FriendlyCellSize);
            Item = ctx.Item("FriendlyFire_Grid",
                "Target.CheckRiskOfFriendlyFire visits only fine-grid cells within maxDist instead of the whole 1 km TargetGrid cell (same answer; snapshot = TargetGrid's own)",
                v => On = v);
            St = new Stat(Item, "cell", "builds");
            s_CellRanges = Refl.StaticField<NativeArray<TargetCellRange>>(typeof(TargetGrid), "CellRanges", Item);
            if (s_CellRanges == null) Item.Block("TargetGrid.CellRanges absent");
            var upd = Item.Target(typeof(TargetGrid), nameof(TargetGrid.Update), Type.EmptyTypes);
            var ff = Item.Target(typeof(Target), nameof(Target.CheckRiskOfFriendlyFire), FfArgs);
            Item.Patch(upd, typeof(FriendlyFireGrid), postfix: nameof(UpdatePostfix));
            Item.Patch(ff, typeof(FriendlyFireGrid), prefix: nameof(Prefix), postfix: nameof(Postfix));
            Item.OnSwitch(on => Invalidate());
        }

        internal static readonly Type[] FfArgs =
        {
            typeof(Vector3), typeof(Vector3), typeof(float), typeof(float), typeof(Target), typeof(ProjectileData),
            typeof(Team), typeof(Target), typeof(bool)
        };

        public static void Invalidate()
        {
            s_Valid = false;
            s_BuiltVersion = -1;
        }

        static void UpdatePostfix(bool __runOriginal)
        {
            if (!On) { s_Valid = false; return; }
            if (!__runOriginal) return;                                 // another prefix skipped it: data unchanged
            if (Time.deltaTime <= 0f || Game.IsLoading) return;          // vanilla's own early return
            s_TargetCount = Target.Targets.Count;
            s_FilledSlots = Team.NumTeams + 1;   // the fill jobs refill only these team slots
            s_Version++;
            s_Valid = true;
        }

        // ------------------------------------------------------------------ patch

        static bool Prefix(Vector3 fromPoint, Vector3 direction, float maxDist, float spread, Target instigator, ProjectileData projectileData,
                           Team scanTeamOverride, Target ignoreTarget, bool onlyUnrepairable, ref bool __result, out byte __state)
        {
            __state = 0;
            CheckingVanilla = false;
            if (!On) return true;
            if (FriendlyFireCache.s_Hit) return false;   // the cache prefix already answered this call
            try
            {
                St.Calls++;
                if (!s_Valid || instigator == null) { St.Vanilla++; return true; }
                Team team = (scanTeamOverride ? scanTeamOverride : instigator.Team);
                if ((object)team == null) { St.Vanilla++; return true; }
                int slot = team.TeamTargetsId;
                if (slot < 0 || slot >= s_FilledSlots) { St.Vanilla++; return true; }
                // never-worse gate first: a bypassed slot costs only these few lines, and builds nothing
                Gate gate = GateFor(slot);
                float now = Time.unscaledTime;
                bool probe = now >= gate.NextProbe;
                // FriendlyFire_StaleGuard: while a Unity-destroyed target is still in Target.Targets (until the next
                // TargetGrid.Update), vanilla can throw on it; then take the guarded grid path even for a bypassed slot.
                bool guardNow = StaleGuard.On && StaleGuard.PendingDeletes() > 0;
                if (guardNow) StaleGuard.St.Calls++;
                if (!gate.Use && !probe && !guardNow) { St.Bypass++; return true; }

                List<NativeList<int>[,]> cellLists = TargetGrid.TeamGridCellIndices;
                if (cellLists == null || float.IsNaN(maxDist) || Time.deltaTime <= 0f || Game.IsLoading || slot >= cellLists.Count) { St.Vanilla++; return true; }
                EProjectileIgnoreFriendlyFire ignoreLevel = (projectileData ? projectileData.m_IgnoreFriendlyFireImpact : EProjectileIgnoreFriendlyFire.None);
                float num = (((bool)projectileData && projectileData.m_bSplash) ? projectileData.m_fSplashDamageRadiusMax : 0f);
                maxDist += num;
                if (float.IsNaN(maxDist)) { St.Vanilla++; return true; }
                Vector3 vector = instigator.transform.position - TargetGrid.GridBounds.min;
                int cx = Mathf.Clamp(Mathf.FloorToInt(vector.x * TargetGrid.GridCellMult), 0, TargetGrid.GridSizeX - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt(vector.z * TargetGrid.GridCellMult), 0, TargetGrid.GridSizeZ - 1);
                if (probe)
                {
                    gate.NextProbe = now + ProbeSeconds;
                    Probe(gate, slot, fromPoint, maxDist, cx, cz);
                    if (!gate.Use && !guardNow) { St.Bypass++; return true; }
                }
                if (!EnsureGlobal() || slot >= TeamSlots) { St.Vanilla++; return true; }
                EnsureTeam(slot);

                s_SawStale = false;
                s_SkipStale = StaleGuard.On;
                bool fast = Walk(slot, fromPoint, direction, maxDist, spread, cx, cz, instigator, projectileData, ignoreTarget, onlyUnrepairable,
                                 ignoreLevel, num, ref St.Visited);
                if (s_SawStale)
                {
                    s_SawStale = false;
                    StaleGuard.St.Vanilla++;     // vanilla runs and will meet the same destroyed target
                    St.Vanilla++;
                    return true;
                }
                if (s_SkipStale && guardNow) StaleGuard.St.Fast++;
                St.Fast++;
                St.ListLen += (long)gate.L;
                if (!guardNow && SelfCheck.Sample())   // never sample in a stale window: vanilla could throw
                {
                    St.Checks++;
                    __state = (byte)(fast ? 2 : 1);
                    CheckingVanilla = true;      // FriendlyFire_Flat: let vanilla answer this call
                    return true;
                }
                __result = fast;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "CheckRiskOfFriendlyFire");
                __state = 0;
                CheckingVanilla = false;
                return true;
            }
        }

        static void Postfix(Vector3 fromPoint, float maxDist, Target instigator, Team scanTeamOverride, bool __result, byte __state)
        {
            if (__state == 0) return;
            bool fast = __state == 2;
            if (fast != __result)
            {
                SelfCheck.Report(Item, St, "instigator " + UrgentCloseGrid.Name(instigator) + " from " + fromPoint + " maxDist " + maxDist +
                                 (scanTeamOverride ? " scan team " + scanTeamOverride.TeamShortName : "") + ": vanilla " + __result + ", grid " + fast);
            }
        }

        // ------------------------------------------------------------------ snapshot (main thread)

        static bool EnsureGlobal()
        {
            if (s_BuiltVersion == s_Version) return s_GlobalOk;
            s_BuiltVersion = s_Version;
            s_GlobalOk = BuildGlobal();
            St.Extra++;
            return s_GlobalOk;
        }

        static bool BuildGlobal()
        {
            NativeArray<TargetData> tda = TargetGrid.TargetDataNativeArray;
            NativeArray<Vector3> los = TargetGrid.LOSPositions;
            NativeArray<TargetCellRange> cr = s_CellRanges();
            List<NativeList<int>[,]> cellLists = TargetGrid.TeamGridCellIndices;
            if (!tda.IsCreated || !los.IsCreated || !cr.IsCreated || cellLists == null) return false;
            int count = s_TargetCount;
            if (count > tda.Length || count > cr.Length) return false;
            int losLen = los.Length;
            int slots = Math.Min(cellLists.Count, s_FilledSlots);
            if (E_Index.Length < count)
            {
                int cap = Mathf.NextPowerOfTwo(count);
                E_Index = new int[cap]; E_Inst = new int[cap]; E_Team = new int[cap];
                E_XMin = new int[cap]; E_XMax = new int[cap]; E_ZMin = new int[cap]; E_ZMax = new int[cap];
                E_X = new float[cap]; E_Y = new float[cap]; E_Z = new float[cap];
                TeamItems = new int[cap];
            }
            if (TeamStart.Length < slots + 1) TeamStart = new int[slots + 1];
            Array.Clear(TeamStart, 0, slots + 1);

            int n = 0;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                TargetData td = tda[i];
                if (td.LOSBeginIndex == -1) continue;
                TargetCellRange r = cr[i];
                if (!r.Valid || r.TeamId < 0 || r.TeamId >= slots) continue;   // in no cell list
                if (td.LOSBeginIndex < 0 || td.LOSBeginIndex >= losLen) return false;   // vanilla would throw: let it
                Vector3 p = los[td.LOSBeginIndex];
                E_Index[n] = i; E_Inst[n] = td.TargetInstanceId; E_Team[n] = r.TeamId;
                E_XMin[n] = r.XMin; E_XMax[n] = r.XMax; E_ZMin[n] = r.ZMin; E_ZMax[n] = r.ZMax;
                E_X[n] = p.x; E_Y[n] = p.y; E_Z[n] = p.z;
                if (IsFinite(p.x) && IsFinite(p.z))
                {
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.z < minZ) minZ = p.z;
                    if (p.z > maxZ) maxZ = p.z;
                }
                TeamStart[r.TeamId + 1]++;
                n++;
            }
            N = n;
            TeamSlots = slots;
            for (int s = 0; s < slots; s++) TeamStart[s + 1] += TeamStart[s];
            // stable placement by team (ascending target index kept); cursor in TeamStart, shifted back after
            for (int e = 0; e < n; e++) TeamItems[TeamStart[E_Team[e]]++] = e;
            for (int s = slots; s > 0; s--) TeamStart[s] = TeamStart[s - 1];
            TeamStart[0] = 0;

            if (minX > maxX) { minX = maxX = minZ = maxZ = 0f; }
            Inv = 1f / s_Cell;
            int nx = (int)((maxX - minX) * Inv) + 1, nz = (int)((maxZ - minZ) * Inv) + 1;
            Nx = nx < 1 ? 1 : (nx > 256 ? 256 : nx);
            Nz = nz < 1 ? 1 : (nz > 256 ? 256 : nz);
            OriginX = minX;
            OriginZ = minZ;
            if (s_Teams.Length < slots) Array.Resize(ref s_Teams, slots);
            return true;
        }

        static void EnsureTeam(int slot)
        {
            TeamCells t = s_Teams[slot];
            if (t == null) s_Teams[slot] = t = new TeamCells();
            if (t.BuiltVersion == s_Version) return;
            t.BuiltVersion = s_Version;
            int cells = Nx * Nz;
            if (t.CellStart.Length < cells + 1) t.CellStart = new int[Mathf.NextPowerOfTwo(cells + 1)];
            int b = TeamStart[slot], e1 = TeamStart[slot + 1], cnt = e1 - b;
            if (t.Items.Length < cnt) t.Items = new int[Mathf.NextPowerOfTwo(cnt)];
            int[] start = t.CellStart, items = t.Items;
            Array.Clear(start, 0, cells + 1);
            t.AlwaysN = 0;
            for (int k = b; k < e1; k++)
            {
                int e = TeamItems[k];
                if (!IsFinite(E_X[e]) || !IsFinite(E_Z[e]))
                {
                    if (t.AlwaysN == t.Always.Length) Array.Resize(ref t.Always, t.Always.Length * 2);
                    t.Always[t.AlwaysN++] = e;
                    continue;
                }
                start[CellOf(E_X[e], OriginX, Inv, Nx) + CellOf(E_Z[e], OriginZ, Inv, Nz) * Nx + 1]++;
            }
            for (int c = 0; c < cells; c++) start[c + 1] += start[c];
            for (int k = b; k < e1; k++)
            {
                int e = TeamItems[k];
                if (!IsFinite(E_X[e]) || !IsFinite(E_Z[e])) continue;
                items[start[CellOf(E_X[e], OriginX, Inv, Nx) + CellOf(E_Z[e], OriginZ, Inv, Nz) * Nx]++] = e;
            }
            for (int c = cells; c > 0; c--) start[c] = start[c - 1];
            start[0] = 0;
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        static int CellOf(float v, float origin, float inv, int n)
        {
            float f = (v - origin) * inv;
            if (!(f > 0f)) return 0;
            if (f >= n) return n - 1;
            return (int)f;
        }

        // ------------------------------------------------------------------ query (worker-safe)

        // Entries of team `slot` that are in vanilla's cell list for (cx, cz), are not the instigator and pass vanilla's
        // box test, as ascending target indices. Static, no Unity API, no allocation (out grows only when too small).
        static int QueryCandidates(int slot, Vector3 from, float maxDist, int cx, int cz, int instigatorId, ref int[] outIdx, ref long visited)
        {
            TeamCells t = s_Teams[slot];
            float r = maxDist + 1f;
            int x0 = CellOf(from.x - r, OriginX, Inv, Nx), x1 = CellOf(from.x + r, OriginX, Inv, Nx);
            int z0 = CellOf(from.z - r, OriginZ, Inv, Nz), z1 = CellOf(from.z + r, OriginZ, Inv, Nz);
            if (r < 0f) { x1 = x0 - 1; }   // negative maxDist: vanilla's box test rejects everything
            int need = TeamStart[slot + 1] - TeamStart[slot];
            if (outIdx.Length < need) outIdx = new int[Mathf.NextPowerOfTwo(need)];
            int n = 0;
            long vis = 0;
            int[] start = t.CellStart, items = t.Items;
            for (int z = z0; z <= z1; z++)
            {
                int row = z * Nx;
                for (int x = x0; x <= x1; x++)
                {
                    int c = row + x, end = start[c + 1];
                    for (int k = start[c]; k < end; k++)
                    {
                        int e = items[k];
                        vis++;
                        if (Keep(e, from, maxDist, cx, cz, instigatorId)) outIdx[n++] = e;
                    }
                }
            }
            for (int k = 0; k < t.AlwaysN; k++)
            {
                int e = t.Always[k];
                vis++;
                if (Keep(e, from, maxDist, cx, cz, instigatorId)) outIdx[n++] = e;
            }
            visited += vis;
            // entries -> ascending target index (entries are numbered in target-index order, so sort entries)
            DetectedGrid.SortInts(outIdx, n);
            return n;
        }

        // Main-thread walk used by the patch: same cells and filters as QueryCandidates, but runs vanilla's per-entry
        // body inline and stops at the first hit, like vanilla. No sort: the answer is an OR over side-effect-free
        // entries, so visiting order cannot change it (it may only change which raycasts run before the hit).
        // Stale-entry handling (see EntryHits): s_SawStale is set when the walk met a Unity-destroyed target with the
        // guard off; the walk then stops and the prefix runs vanilla. s_SkipStale = guard active for this call.
        static bool s_SawStale, s_SkipStale;

        static bool Walk(int slot, Vector3 fromPoint, Vector3 direction, float maxDist, float spread, int cx, int cz, Target instigator,
                         ProjectileData projectileData, Target ignoreTarget, bool onlyUnrepairable, EProjectileIgnoreFriendlyFire ignoreLevel,
                         float num, ref long visited)
        {
            TeamCells t = s_Teams[slot];
            int instigatorId = instigator.GetInstanceID();
            List<Target> targets = Target.Targets;
            long vis = 0;
            float r = maxDist + 1f;
            if (r >= 0f)
            {
                int x0 = CellOf(fromPoint.x - r, OriginX, Inv, Nx), x1 = CellOf(fromPoint.x + r, OriginX, Inv, Nx);
                int z0 = CellOf(fromPoint.z - r, OriginZ, Inv, Nz), z1 = CellOf(fromPoint.z + r, OriginZ, Inv, Nz);
                int[] start = t.CellStart, items = t.Items;
                for (int z = z0; z <= z1; z++)
                {
                    int row = z * Nx;
                    for (int x = x0; x <= x1; x++)
                    {
                        int c = row + x, end = start[c + 1];
                        for (int k = start[c]; k < end; k++)
                        {
                            int e = items[k];
                            vis++;
                            if (Keep(e, fromPoint, maxDist, cx, cz, instigatorId) &&
                                EntryHits(e, targets, fromPoint, direction, maxDist, spread, instigator, projectileData, ignoreTarget, onlyUnrepairable, ignoreLevel, num))
                            {
                                visited += vis;
                                return true;
                            }
                        }
                    }
                }
            }
            for (int k = 0; k < t.AlwaysN; k++)
            {
                int e = t.Always[k];
                vis++;
                if (Keep(e, fromPoint, maxDist, cx, cz, instigatorId) &&
                    EntryHits(e, targets, fromPoint, direction, maxDist, spread, instigator, projectileData, ignoreTarget, onlyUnrepairable, ignoreLevel, num))
                {
                    visited += vis;
                    return true;
                }
            }
            visited += vis;
            return false;
        }

        static bool Keep(int e, Vector3 from, float maxDist, int cx, int cz, int instigatorId)
        {
            if (E_XMin[e] > cx || cx > E_XMax[e] || E_ZMin[e] > cz || cz > E_ZMax[e]) return false;   // not in vanilla's cell list
            if (E_Inst[e] == instigatorId) return false;
            float dx = E_X[e] - from.x, dy = E_Y[e] - from.y, dz = E_Z[e] - from.z;
            if (Mathf.Abs(dx) > maxDist || Mathf.Abs(dy) > maxDist || Mathf.Abs(dz) > maxDist) return false;
            return true;
        }

        // ------------------------------------------------------------------ vanilla per-entry body (main thread)

        // One entry of vanilla's loop, verbatim: true = vanilla's "return true", false = its "continue".
        static bool EntryHits(int e, List<Target> targets, Vector3 fromPoint, Vector3 direction, float maxDist, float spread, Target instigator, ProjectileData projectileData,
                              Target ignoreTarget, bool onlyUnrepairable, EProjectileIgnoreFriendlyFire eProjectileIgnoreFriendlyFire, float num)
        {
            Vector3 vector2 = new Vector3(E_X[e], E_Y[e], E_Z[e]) - fromPoint;
            float magnitude = vector2.magnitude;
            if (magnitude > maxDist)
            {
                return false;
            }
            Target target = targets[E_Index[e]];
            if ((bool)ignoreTarget && target == ignoreTarget)
            {
                return false;
            }
            if ((object)target.Owner == null)
            {
                // Unity-destroyed since the last TargetGrid.Update (Target.OnDestroy nulls Owner; the entry stays in
                // Target.Targets until ActuallyDeleteTargets). Vanilla dereferences Owner below and throws.
                if (s_SkipStale) return false;     // FriendlyFire_StaleGuard: skip it, as vanilla intends
                s_SawStale = true;                 // otherwise: abort, the prefix lets vanilla run (and throw) itself
                return true;
            }
            if ((onlyUnrepairable && GameAI.GetCanBeRepairedByRepairWeapon(target.Owner)) ||
                (eProjectileIgnoreFriendlyFire != EProjectileIgnoreFriendlyFire.None && target.GetIgnoreFriendlyFireForLevel(eProjectileIgnoreFriendlyFire)))
            {
                return false;
            }
            vector2 /= magnitude;
            magnitude -= num;
            if (magnitude < 0f)
            {
                magnitude = 0f;
            }
            float num4 = Vector3.Angle(direction, vector2);
            float num5 = Mathf.Abs(Mathf.Atan2(target.Owner.PhysicalRadius + num, magnitude) * 57.29578f);
            if (num4 - num5 > spread)
            {
                return false;
            }
            DamageManager damageManager = target.DamageManager;
            if ((bool)damageManager && (bool)projectileData && !target.Owner.GetIgnoreFriendlyFireForLevel(projectileData.m_IgnoreFriendlyFireDamage))
            {
                bool flag = false;
                float num6 = damageManager.Health * 0.02f;
                if (projectileData.m_bSplash && damageManager.AdjustDamage(projectileData.m_fSplashDamageMax, projectileData.m_SplashDamageType, limitToHealth: false) > num6)
                {
                    flag = true;
                }
                else if (projectileData.m_bPenetrating && damageManager.AdjustDamage(projectileData.m_fPenetratingDamage, projectileData.m_PenetratingDamageType, limitToHealth: false) > num6)
                {
                    flag = true;
                }
                else if (projectileData.m_bRicochet && damageManager.AdjustDamage(projectileData.m_fRicochetDamage, projectileData.m_ImpactDamageType, limitToHealth: false) > num6)
                {
                    flag = true;
                }
                else if (damageManager.AdjustDamage(projectileData.m_fImpactDamage, projectileData.m_ImpactDamageType, limitToHealth: false) > num6)
                {
                    flag = true;
                }
                if (flag)
                {
                    return true;
                }
            }
            if (num4 > spread)
            {
                Bounds physicalBounds = target.Owner.PhysicalBounds;
                if (num > 0f)
                {
                    physicalBounds.Expand(num);
                }
                Vector3 vector3 = target.transform.InverseTransformPoint(fromPoint + Vector3.RotateTowards(direction, vector2, spread * (MathF.PI / 180f), 0f) * maxDist);
                Vector3 vector4 = target.transform.InverseTransformPoint(fromPoint);
                if (!physicalBounds.IntersectRay(new Ray(vector4, (vector3 - vector4).normalized)))
                {
                    return false;
                }
                if ((bool)damageManager && (bool)target.OwnerStructure)
                {
                    float num7 = Mathf.Clamp(spread, 0f, 5f);
                    Vector3 normalized = Vector3.Cross(Vector3.up, direction).normalized;
                    Vector3 upward = Vector3.Cross(direction, normalized);
                    Quaternion q = GameMath.LookRotation(direction, upward);
                    bool flag2 = false;
                    for (int j = 0; j < 4 && (j <= 0 || !(num7 < 0.5f)); j++)
                    {
                        if (j != 0 || !(num7 >= 0.5f))
                        {
                            Vector3 direction2 = direction;
                            if (j == 1)
                            {
                                direction2 = GameMath.GetRotatedQuaternion(q, Vector3.right * spread) * Vector3.forward;
                            }
                            if (j == 2)
                            {
                                direction2 = GameMath.GetRotatedQuaternion(q, new Vector3(-0.707f, -0.707f, 0f) * spread) * Vector3.forward;
                            }
                            if (j == 3)
                            {
                                direction2 = GameMath.GetRotatedQuaternion(q, new Vector3(-0.707f, 0.707f, 0f) * spread) * Vector3.forward;
                            }
                            flag2 = UnityEngine.Physics.Raycast(fromPoint, direction2, out var hitInfo, maxDist, GamePhysics.TRACELAYERMASK_IGNOREDYNAMIC);
                            if (flag2 && (bool)instigator && hitInfo.transform.GetTarget() == instigator)
                            {
                                flag2 = false;
                            }
                            if (flag2)
                            {
                                break;
                            }
                        }
                    }
                    if (!flag2)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }

    // ====================================================================== guard for a vanilla race

    // FriendlyFire_StaleGuard. Vanilla race (1.0.17): Target.OnDestroy sets Owner = null and queues the Target in
    // Target.TargetsToDelete; it stays in Target.Targets and in TargetGrid's cell lists until the next TargetGrid.Update
    // (ActuallyDeleteTargets). A CheckRiskOfFriendlyFire in between that reaches that entry (box + distance pass) throws
    // a NullReferenceException at target.Owner (GetIgnoreFriendlyFireForLevel / PhysicalRadius); the update list catches
    // it per callback, so that one turret / vehicle skips the rest of its update for the frame and Debug.LogException
    // formats a stack trace. With this item on, FriendlyFire_Grid takes its grid path for every call while
    // TargetsToDelete is not empty (also for bypassed teams) and skips entries whose Owner is null. The answer equals
    // vanilla's whenever vanilla does not throw (a skipped entry contributes nothing, all others are evaluated as
    // vanilla); where vanilla throws, it returns the answer over the remaining entries. Off: the grid path stops at a
    // stale entry and lets vanilla run (and throw) itself, i.e. exact including the exception.
    internal static class StaleGuard
    {
        internal static bool On;
        static PerfItem Item;
        internal static Stat St;
        static AccessTools.FieldRef<List<Target>> s_Pending;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("FriendlyFire_StaleGuard",
                "CheckRiskOfFriendlyFire skips targets destroyed since the last TargetGrid.Update instead of throwing a NullReferenceException (vanilla race; needs FriendlyFire_Grid)",
                v => On = v);
            St = new Stat(Item, null);
            St.Mode = "grid = guarded calls in a stale window, vanilla = grid walks that met a destroyed target with the guard off";
            s_Pending = Refl.StaticField<List<Target>>(typeof(Target), "TargetsToDelete", Item);
            if (s_Pending == null) Item.Block("Target.TargetsToDelete absent");
            Item.Hooked("FriendlyFire_Grid prefix (Target.CheckRiskOfFriendlyFire)");
        }

        // Targets destroyed since the last TargetGrid.Update.
        public static int PendingDeletes()
        {
            List<Target> l = s_Pending != null ? s_Pending() : null;
            return l != null ? l.Count : 0;
        }
    }

    // ====================================================================== approximate: answer cache

    internal static class FriendlyFireCache
    {
        internal static bool On;
        static PerfItem Item;
        static Stat St;
        // set by the cache prefix for the current call; read by FriendlyFireGrid.Prefix (calls never nest)
        internal static bool s_Hit;

        static float s_Ttl = 0.15f, s_Move2 = 1f, s_Angle = 2f, s_Dist = 5f;

        sealed class Entry
        {
            public float Time;
            public Vector3 From, Dir;
            public float MaxDist, Spread;
            public ProjectileData Proj;
            public Team Scan;
            public Target Ignore;
            public bool OnlyUnrepairable;
            public bool Result;
            public Entry Next;
        }

        static readonly Dictionary<int, Entry> s_Map = new Dictionary<int, Entry>(512);
        static Entry s_Pending;   // entry to fill in the postfix (miss)

        public static void Register(ModuleContext ctx, TargetingSettings s)
        {
            s_Ttl = Mathf.Max(0f, s.FriendlyFireCacheSeconds);
            s_Move2 = s.FriendlyFireCacheMoveMeters * s.FriendlyFireCacheMoveMeters;
            s_Angle = Mathf.Max(0f, s.FriendlyFireCacheAngleDeg);
            s_Dist = Mathf.Max(0f, s.FriendlyFireCacheDistMeters);
            Item = ctx.Item("FriendlyFire_Cache",
                "APPROXIMATE: reuse a weapon's CheckRiskOfFriendlyFire answer for FriendlyFireCacheSeconds while its muzzle barely moved",
                v => On = v, defaultOn: false);
            St = new Stat(Item, null, "hits");
            var ff = Item.Target(typeof(Target), nameof(Target.CheckRiskOfFriendlyFire), FriendlyFireGrid.FfArgs);
            Item.Patch(ff, typeof(FriendlyFireCache), prefix: nameof(Prefix), postfix: nameof(Postfix), priority: Priority.High);
            Item.OnSwitch(on => { if (!on) Clear(); });
        }

        public static void Clear()
        {
            s_Map.Clear();
            s_Pending = null;
            s_Hit = false;
        }

        static bool Prefix(Vector3 fromPoint, Vector3 direction, float maxDist, float spread, Target instigator, ProjectileData projectileData,
                           Team scanTeamOverride, Target ignoreTarget, bool onlyUnrepairable, ref bool __result)
        {
            s_Hit = false;
            s_Pending = null;
            if (!On) return true;
            try
            {
                St.Calls++;
                if (instigator == null) return true;
                int id = instigator.GetInstanceID();
                s_Map.TryGetValue(id, out Entry head);
                Entry e = head;
                while (e != null && !(ReferenceEquals(e.Proj, projectileData) && ReferenceEquals(e.Scan, scanTeamOverride) &&
                                      ReferenceEquals(e.Ignore, ignoreTarget) && e.OnlyUnrepairable == onlyUnrepairable))
                {
                    e = e.Next;
                }
                float now = Time.time;
                if (e != null && now - e.Time <= s_Ttl && e.Spread == spread && Mathf.Abs(maxDist - e.MaxDist) <= s_Dist &&
                    (fromPoint - e.From).sqrMagnitude <= s_Move2 && Vector3.Angle(direction, e.Dir) <= s_Angle)
                {
                    St.Fast++;
                    St.Extra++;
                    __result = e.Result;
                    s_Hit = true;
                    return false;
                }
                if (e == null)
                {
                    e = new Entry { Proj = projectileData, Scan = scanTeamOverride, Ignore = ignoreTarget, OnlyUnrepairable = onlyUnrepairable, Next = head };
                    s_Map[id] = e;
                }
                e.From = fromPoint;
                e.Dir = direction;
                e.MaxDist = maxDist;
                e.Spread = spread;
                s_Pending = e;
                St.Vanilla++;
                return true;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "CheckRiskOfFriendlyFire cache");
                s_Hit = false;
                s_Pending = null;
                return true;
            }
        }

        static void Postfix(bool __result)
        {
            if (!On || s_Hit)
            {
                s_Hit = false;
                return;
            }
            Entry e = s_Pending;
            s_Pending = null;
            if (e == null) return;
            e.Result = __result;
            e.Time = Time.time;
        }
    }
}
