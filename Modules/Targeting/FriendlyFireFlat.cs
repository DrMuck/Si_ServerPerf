/*
 FriendlyFire_Flat (exact, default off until measured) on Target.CheckRiskOfFriendlyFire.

 Case: dense cluster, FriendlyFire_Grid in "bypass (no pruning)": night 1, ~521 lab units, 7092 calls/s x 7.4 us =
 52.8 ms/s, cell list L 150-205 per call, 55-91 % of it within maxDist (+50 m) of the muzzle.

 Vanilla (1.0.17 Target.cs:662-795) per call: prologue (instigator.transform.position = 2 native calls, the 1 km cell
 TeamGridCellIndices[team.TeamTargetsId][cx, cz] via a 2-D array of NativeList) then, for EVERY entry of that list,
   :679-684  cell[i], TargetDataNativeArray[index] (52-byte struct copy), instigator.GetInstanceID() (re-read per entry)
   :685-689  LOSPositions[begin] - fromPoint, box test
 and for every entry inside the box (most of the list in a melee):
   :690-694  vector2.magnitude (sqrt), distance test
   :695-699  Targets[index], Unity bool on ignoreTarget (+ ==), onlyUnrepairable -> GameAI.GetCanBeRepairedByRepairWeapon,
             ignore level -> Target.Owner -> BaseGameObject.GetIgnoreFriendlyFireForLevel (Unity bool on ObjectInfo, 2 enum
             fields)
   :700-711  vector2 / magnitude, Vector3.Angle (2 sqrMagnitude, sqrt, Dot, Clamp, Math.Acos), Owner.PhysicalRadius (Owner
             -> Unity bool on ObjectInfo -> field), Mathf.Atan2 (Math.Atan2), Abs; the cone test rejects almost all
 and only for entries inside the cone (a handful per call at most): DamageManager / AdjustDamage (first hit returns),
 2 x Transform.InverseTransformPoint + Bounds.IntersectRay, and for structures up to 4 Physics.Raycast.
 Estimate per call at L ~180 with ~120 in range (Mono, no profiler numbers per line): prologue ~0.2 us; per list entry
 ~8-10 ns -> ~1.7 us; per in-range entry ~40 ns (2 libm icalls acos/atan2 ~25 ns, object chains ~10 ns, vector math)
 -> ~4.8 us; cone tail ~0.3-0.7 us averaged. Sum ~7 us, matching the measured 7.4 us. The raycasts are NOT the cost.

 FriendlyFire_Flat walks vanilla's own cell list (same NativeList, same order) and per entry
   1. reads instance id, LOS begin index, LOS position and Owner.PhysicalRadius from a flat per-target-index array
      filled lazily (first touch per "build"), instead of TargetDataNativeArray / LOSPositions / object chains;
      instigator.GetInstanceID() once per call;
   2. runs vanilla's box and distance tests verbatim (same Vector3 expressions on the same floats);
   3. runs FlatCone.Rejects (FriendlyFireFlatKernel.cs): a conservative cos-space version of vanilla's cone test, no
      acos / atan2, with a 1e-4 cos margin; it only skips entries vanilla's own float test certainly rejects;
   4. for every entry not skipped, runs vanilla's remaining per-entry code VERBATIM from "Target target =
      Targets[index]" on (live objects: ignoreTarget, onlyUnrepairable, ignore level, the exact cone test, damage,
      bounds, raycasts), returning at the first hit like vanilla.
 Removed: per list entry the struct copy + GetInstanceID (~4-6 ns of ~9); per in-range entry the object chains, the
 normalisation, acos and atan2 (~30 of ~40 ns; the pre-test costs one sqrt, ~15 flops). Expected ~2.0-2.5 us per
 call instead of 7.4 -> ~35 ms/s saved at 7092 calls/s, minus the lazy fills (<= 1 per target per frame, ~0.5-1 ms/s).

 Exactness. The answer is "exists an entry, in list order, whose per-entry code returns true"; vanilla returns at the
 first such entry, so the answer AND every side effect (AdjustDamage's cache, which entries run the raycasts) are those
 of the first hit. The flat walk visits the same entries in the same order and only skips entries for which vanilla's
 code would "continue" without side effects:
   * the box / distance tests: same expressions on the same inputs (see below why the inputs are the same);
   * the cone pre-test: Rejects => vanilla's cone test rejects (margin argument in FriendlyFireFlatKernel.cs, checked by
     the offline test). The tests vanilla runs BEFORE the cone (ignoreTarget, onlyUnrepairable, ignore level) are all
     "continue"-only, side-effect free and cannot throw for a target with Owner != null, so skipping an entry that the
     cone test rejects anyway gives the same result, whatever those tests would have said.
 Per-target data and why it cannot be stale while a build is current:
   * instance id, LOS begin index, LOS position: copies of TargetDataNativeArray[index] / LOSPositions[begin]. Both are
     written only inside TargetGrid.Update (UpdateDataForJobs :203-259, its LOS job completed at :258; TargetGrid.Clear
     disposes them and nulls TeamGridCellIndices, which sends the call to vanilla). A postfix on TargetGrid.Update
     starts a new build, so the copy is always the data vanilla reads. Units moving inside the frame do not matter:
     vanilla itself reads these snapshot positions, not live transforms.
   * Owner.PhysicalRadius = ObjectInfo ? ObjectInfo.PhysicalRadius : 1 (BaseGameObject.cs:210). BaseGameObject.ObjectInfo
     is a serialized field no game code reassigns; ObjectInfo.PhysicalRadius is written only in the database init
     (ObjectInfo.cs:1313). Target.Owner is set in Init and nulled only in Target.OnDestroy (Target.cs:917-931), which
     also appends to Target.TargetsToDelete. A build ends on: a new frame, TargetGrid.Update, a change of
     TargetsToDelete.Count, a switch flip. So a target destroyed after its fill starts a new build before the next
     call, and its refill finds Owner == null.
   * Destroyed-but-listed targets (the vanilla race, see FriendlyFire_StaleGuard): radius = NaN, so the pre-test never
     skips them; the verbatim tail meets Owner == null where vanilla throws and does what FriendlyFire_Grid does: with
     FriendlyFire_StaleGuard on, skip the entry; off, abandon the walk and let vanilla run (and throw) itself.
 Team membership and order come from vanilla's own list (read, not copied), so allies changing team, units spawning
 (appended to Target.Targets, in no cell list until the next TargetGrid.Update) or dying (Health / IsDestroyed are
 read live by the tail as in vanilla) behave exactly as in vanilla.

 Composition (prefix priority Low, after FriendlyFire_Cache (High) and FriendlyFire_Grid (Normal)):
   * a previous prefix answered (cache hit, grid served): __runOriginal == false -> nothing to do;
   * FriendlyFire_Grid let the call through for ITS self-check (FriendlyFireGrid.CheckingVanilla): vanilla runs, so the
     grid's check stays a check against vanilla;
   * otherwise (grid off, grid bypass "dense cluster", grid vanilla fallbacks): the flat path answers.
   So "grid when it prunes, flat when it does not". FriendlyFire_Cache (approximate) still answers first when on; on a
   miss its postfix stores the flat answer, which is vanilla's.
 Self-check (TargetingSettings.SelfCheckFraction): a sampled call computes the flat answer, lets vanilla run and
 compares in the postfix (never in a stale window with the guard on, where vanilla could throw). Counters: q/s, served
 ("grid" column), vanilla fallbacks, bypass = answered by the cache / grid or deferred to the grid's check, checks/s,
 mismatch total; Mode: in-range / cone-skipped / to-vanilla-tail entries per served call, fills per frame, bails.
*/

using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;

namespace SiServerPerf.Modules.Targeting
{
    internal static class FriendlyFireFlat
    {
        internal static bool On;
        static PerfItem Item;
        static Stat St;
        static AccessTools.FieldRef<List<Target>> s_PendingRef;
        static bool s_UpdateHooked;

        struct Entry
        {
            public int Stamp;    // build id this entry was filled for
            public int Inst;     // TargetData.TargetInstanceId
            public int Los;      // TargetData.LOSBeginIndex (-1 = vanilla skips the entry)
            public float X, Y, Z;// LOSPositions[Los]
            public float R;      // Owner.PhysicalRadius, NaN = Owner null / no Target (pre-test keeps it)
        }

        static Entry[] s_E = new Entry[1024];
        static int s_Build = 1;
        static int s_BuildFrame = -1;
        static int s_BuildPending = -1;
        static int s_GridVersion, s_BuiltGridVersion = -1;

        // diagnostics (cumulative; Mode shows rates per served call)
        static long s_InRange, s_Skipped, s_Fills, s_Frames, s_StaleSkip, s_StaleBail, s_BailOther, s_Answered, s_DeferCheck;
        static long p_Fast, p_InRange, p_Skipped, p_Passed, p_Fills, p_Frames, p_StaleSkip, p_StaleBail, p_BailOther, p_Answered, p_DeferCheck;
        static float s_NextMode;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("FriendlyFire_Flat",
                "Target.CheckRiskOfFriendlyFire walks vanilla's cell list on flat per-target data and skips entries its cone test certainly rejects; vanilla's code runs for the rest (same answer; for dense clusters where FriendlyFire_Grid cannot prune)",
                v => On = v, defaultOn: false);
            St = new Stat(Item, "cell", "fills");
            s_PendingRef = Refl.StaticField<List<Target>>(typeof(Target), "TargetsToDelete", Item);
            if (s_PendingRef == null) Item.Block("Target.TargetsToDelete absent");
            var upd = Item.Target(typeof(TargetGrid), nameof(TargetGrid.Update), Type.EmptyTypes);
            var ff = Item.Target(typeof(Target), nameof(Target.CheckRiskOfFriendlyFire), FriendlyFireGrid.FfArgs);
            // the TargetGrid.Update postfix ends a build; without it the copy could be stale -> never serve
            s_UpdateHooked = Item.Patch(upd, typeof(FriendlyFireFlat), postfix: nameof(UpdatePostfix));
            Item.Patch(ff, typeof(FriendlyFireFlat), prefix: nameof(Prefix), postfix: nameof(Postfix), priority: Priority.Low);
            Item.OnSwitch(on => Invalidate());
        }

        static void Invalidate()
        {
            s_Build++;
            s_BuildFrame = -1;
        }

        static void UpdatePostfix()
        {
            s_GridVersion++;   // always (also when off or when another prefix skipped the original): costs one rebuild at most
        }

        // ------------------------------------------------------------------ patch

        static bool Prefix(Vector3 fromPoint, Vector3 direction, float maxDist, float spread, Target instigator, ProjectileData projectileData,
                           Team scanTeamOverride, Target ignoreTarget, bool onlyUnrepairable, bool __runOriginal, ref bool __result, out byte __state)
        {
            __state = 0;
            if (!On) return true;
            St.Calls++;
            if (!__runOriginal) { St.Bypass++; s_Answered++; return false; }   // cache / grid answered: keep their answer
            try
            {
                if (FriendlyFireGrid.CheckingVanilla) { St.Bypass++; s_DeferCheck++; return true; }   // grid's self-check
                if (!s_UpdateHooked || instigator == null || Time.deltaTime <= 0f || Game.IsLoading) { St.Vanilla++; return true; }

                List<Target> pendingList = s_PendingRef();
                int pending = pendingList != null ? pendingList.Count : 0;
                int frame = Time.frameCount;
                if (frame != s_BuildFrame || pending != s_BuildPending || s_GridVersion != s_BuiltGridVersion)
                {
                    if (frame != s_BuildFrame) { s_Frames++; MaybePublish(); }
                    s_Build++;
                    s_BuildFrame = frame;
                    s_BuildPending = pending;
                    s_BuiltGridVersion = s_GridVersion;
                }

                // ---- vanilla prologue (Target.cs:668-675), same expressions
                EProjectileIgnoreFriendlyFire ignoreLevel = (projectileData ? projectileData.m_IgnoreFriendlyFireImpact : EProjectileIgnoreFriendlyFire.None);
                float num = (((bool)projectileData && projectileData.m_bSplash) ? projectileData.m_fSplashDamageRadiusMax : 0f);
                maxDist += num;
                Vector3 vector = instigator.transform.position - TargetGrid.GridBounds.min;
                int cx = Mathf.Clamp(Mathf.FloorToInt(vector.x * TargetGrid.GridCellMult), 0, TargetGrid.GridSizeX - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt(vector.z * TargetGrid.GridCellMult), 0, TargetGrid.GridSizeZ - 1);
                Team team = (scanTeamOverride ? scanTeamOverride : instigator.Team);
                List<NativeList<int>[,]> cellLists = TargetGrid.TeamGridCellIndices;
                NativeArray<TargetData> tda = TargetGrid.TargetDataNativeArray;
                NativeArray<Vector3> los = TargetGrid.LOSPositions;
                if ((object)team == null || cellLists == null || !tda.IsCreated || !los.IsCreated) { St.Vanilla++; return true; }
                int slot = team.TeamTargetsId;
                if (slot < 0 || slot >= cellLists.Count) { St.Vanilla++; return true; }
                NativeList<int>[] cells = CellsOf(cellLists, slot, out int sizeZ);
                if (cells == null || cz >= sizeZ || cx * sizeZ + cz >= cells.Length) { St.Vanilla++; return true; }
                NativeList<int> list = cells[cx * sizeZ + cz];
                if (!list.IsCreated) { St.Vanilla++; return true; }
                NativeArray<int> cell = list.AsArray();

                if (s_E.Length < tda.Length) s_E = new Entry[Mathf.NextPowerOfTwo(tda.Length)];   // stamps 0 = unfilled

                bool guardNow = StaleGuard.On && pending > 0;
                int r = Walk(cell, tda, los, fromPoint, direction, maxDist, spread, instigator, projectileData, ignoreTarget, onlyUnrepairable,
                             ignoreLevel, num);
                if (r < 0) { St.Vanilla++; return true; }   // bail: let vanilla run (and throw, where it does)
                bool fast = r == 1;
                St.Fast++;
                St.ListLen += cell.Length;
                if (!guardNow && SelfCheck.Sample())
                {
                    St.Checks++;
                    __state = (byte)(fast ? 2 : 1);
                    return true;
                }
                __result = fast;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "CheckRiskOfFriendlyFire flat");
                __state = 0;
                return true;
            }
        }

        static void Postfix(Vector3 fromPoint, Vector3 direction, float maxDist, float spread, Target instigator, Team scanTeamOverride,
                            bool __result, byte __state)
        {
            if (__state == 0) return;
            bool fast = __state == 2;
            if (fast != __result)
            {
                SelfCheck.Report(Item, St, "instigator " + UrgentCloseGrid.Name(instigator) + " from " + fromPoint + " dir " + direction +
                                 " maxDist " + maxDist + " spread " + spread +
                                 (scanTeamOverride ? " scan team " + scanTeamOverride.TeamShortName : "") + ": vanilla " + __result + ", flat " + fast);
            }
        }

        // ------------------------------------------------------------------ cell lists

        // TeamGridCellIndices[slot] is a NativeList<int>[,] created once in TargetGrid.CreateGridIndices (only while
        // TeamGridCellIndices == null, i.e. once per map; TargetGrid.Clear nulls it) and never reassigned. Its elements
        // are NativeList structs = a pointer to the list header, so a copy of the struct sees every later Clear / Add
        // / reallocation vanilla does through the original. A 1-D copy per 2-D array instance therefore yields the
        // same list as vanilla's [cx, cz]; it is rebuilt whenever the List or the array instance changes. (Reading the
        // 2-D array directly needs the runtime-synthesized NativeList<int>[,]::Get, which the Cecil refcheck cannot
        // resolve; Array.GetValue boxes, so it runs only at a rebuild.)
        static List<NativeList<int>[,]> s_CellListsRef;
        static object[] s_GridRef = new object[8];   // the NativeList<int>[,] instance each s_Flat entry was copied from
        static NativeList<int>[][] s_Flat = new NativeList<int>[8][];
        static int[] s_FlatZ = new int[8];

        static NativeList<int>[] CellsOf(List<NativeList<int>[,]> cellLists, int slot, out int sizeZ)
        {
            sizeZ = 0;
            if (!ReferenceEquals(cellLists, s_CellListsRef))
            {
                Array.Clear(s_GridRef, 0, s_GridRef.Length);
                s_CellListsRef = cellLists;
            }
            if (slot >= s_GridRef.Length)
            {
                int c = Math.Max(slot + 1, s_GridRef.Length * 2);
                Array.Resize(ref s_GridRef, c); Array.Resize(ref s_Flat, c); Array.Resize(ref s_FlatZ, c);
            }
            NativeList<int>[,] grid = cellLists[slot];
            if (grid == null) return null;
            if (!ReferenceEquals(grid, s_GridRef[slot]))
            {
                int nx = grid.GetLength(0), nz = grid.GetLength(1);
                var flat = new NativeList<int>[nx * nz];
                for (int x = 0; x < nx; x++)
                    for (int z = 0; z < nz; z++)
                        flat[x * nz + z] = (NativeList<int>)grid.GetValue(x, z);
                s_Flat[slot] = flat;
                s_FlatZ[slot] = nz;
                s_GridRef[slot] = grid;
                s_GridBuilds++;
            }
            sizeZ = s_FlatZ[slot];
            return s_Flat[slot];
        }

        static long s_GridBuilds;

        // ------------------------------------------------------------------ walk (main thread)

        // 1 = vanilla returns true, 0 = vanilla returns false, -1 = abandon (vanilla must run itself).
        static int Walk(NativeArray<int> cell, NativeArray<TargetData> tda, NativeArray<Vector3> los, Vector3 fromPoint, Vector3 direction,
                        float maxDist, float spread, Target instigator, ProjectileData projectileData, Target ignoreTarget, bool onlyUnrepairable,
                        EProjectileIgnoreFriendlyFire ignoreLevel, float num)
        {
            Entry[] E = s_E;
            int build = s_Build;
            int instigatorId = instigator.GetInstanceID();     // vanilla re-reads it per entry; m_CachedPtr is fixed here
            List<Target> targets = Target.Targets;
            FlatConeQuery q = FlatCone.Prepare(direction.x, direction.y, direction.z, spread, maxDist, num);
            int n = cell.Length;
            int tdaLen = tda.Length;
            long inRange = 0, skipped = 0, passed = 0, visited = 0;
            int result = 0;
            for (int i = 0; i < n; i++)
            {
                int index = cell[i];
                visited++;
                if ((uint)index >= (uint)tdaLen) { result = -1; break; }   // vanilla would read outside the array
                if (E[index].Stamp != build && !Fill(E, index, build, tda, los, targets)) { result = -1; break; }
                if (E[index].Los == -1 || E[index].Inst == instigatorId) continue;                      // :681
                Vector3 vector2 = new Vector3(E[index].X, E[index].Y, E[index].Z) - fromPoint;           // :685
                if (Mathf.Abs(vector2.x) > maxDist || Mathf.Abs(vector2.y) > maxDist || Mathf.Abs(vector2.z) > maxDist) continue;
                float magnitude = vector2.magnitude;                                                     // :690
                if (magnitude > maxDist) continue;
                inRange++;
                if (FlatCone.Rejects(ref q, vector2.x, vector2.y, vector2.z, magnitude, E[index].R, num)) { skipped++; continue; }
                passed++;
                int r = Tail(index, targets, vector2, magnitude, fromPoint, direction, maxDist, spread, instigator, projectileData, ignoreTarget,
                             onlyUnrepairable, ignoreLevel, num);
                if (r != 0) { result = r; break; }
            }
            St.Visited += visited;
            St.Passed += passed;
            s_InRange += inRange;
            s_Skipped += skipped;
            return result;
        }

        static bool Fill(Entry[] E, int index, int build, NativeArray<TargetData> tda, NativeArray<Vector3> los, List<Target> targets)
        {
            TargetData td = tda[index];
            E[index].Inst = td.TargetInstanceId;
            E[index].Los = td.LOSBeginIndex;
            if (td.LOSBeginIndex != -1)
            {
                if ((uint)td.LOSBeginIndex >= (uint)los.Length) return false;    // vanilla would read outside the array
                Vector3 p = los[td.LOSBeginIndex];
                E[index].X = p.x; E[index].Y = p.y; E[index].Z = p.z;
                float r = float.NaN;
                if (index < targets.Count)
                {
                    Target t = targets[index];
                    if ((object)t != null)
                    {
                        BaseGameObject o = t.Owner;
                        if ((object)o != null) r = o.PhysicalRadius;   // the getter vanilla calls at :707
                    }
                }
                E[index].R = r;
            }
            E[index].Stamp = build;
            St.Extra++;
            s_Fills++;
            return true;
        }

        // ------------------------------------------------------------------ vanilla per-entry code (Target.cs:695-792)

        // From "Target target = Targets[index]" on, verbatim: 1 = vanilla's "return true", 0 = its "continue",
        // -1 = vanilla throws here (index out of range, null target, Owner null with the guard off): abandon the walk.
        static int Tail(int index, List<Target> targets, Vector3 vector2, float magnitude, Vector3 fromPoint, Vector3 direction, float maxDist, float spread,
                        Target instigator, ProjectileData projectileData, Target ignoreTarget, bool onlyUnrepairable,
                        EProjectileIgnoreFriendlyFire eProjectileIgnoreFriendlyFire, float num)
        {
            if (index >= targets.Count) { s_BailOther++; return -1; }      // Targets[index] throws in vanilla
            Target target = targets[index];
            if ((object)target == null) { s_BailOther++; return -1; }       // NRE in vanilla below (or a skip; let it decide)
            if ((bool)ignoreTarget && target == ignoreTarget)
            {
                return 0;
            }
            if ((object)target.Owner == null)
            {
                // destroyed since the last TargetGrid.Update; vanilla dereferences Owner below and throws
                if (StaleGuard.On) { s_StaleSkip++; return 0; }               // FriendlyFire_StaleGuard: skip it
                s_StaleBail++;
                return -1;                                                     // vanilla runs and throws itself
            }
            if ((onlyUnrepairable && GameAI.GetCanBeRepairedByRepairWeapon(target.Owner)) ||
                (eProjectileIgnoreFriendlyFire != EProjectileIgnoreFriendlyFire.None && target.GetIgnoreFriendlyFireForLevel(eProjectileIgnoreFriendlyFire)))
            {
                return 0;
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
                return 0;
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
                    return 1;
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
                    return 0;
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
                        return 0;
                    }
                }
            }
            return 1;
        }

        // ------------------------------------------------------------------ status

        static void MaybePublish()
        {
            float now = Time.unscaledTime;
            if (now < s_NextMode) return;
            s_NextMode = now + 5f;
            long fast = St.Fast - p_Fast;
            double f = fast > 0 ? fast : 1;
            long fr = s_Frames - p_Frames;
            St.Mode = "per served call: in range " + ((s_InRange - p_InRange) / f).ToString("0.0") +
                      ", cone-skipped " + ((s_Skipped - p_Skipped) / f).ToString("0.0") +
                      ", to vanilla tail " + ((St.Passed - p_Passed) / f).ToString("0.00") +
                      " | fills/frame " + (fr > 0 ? ((double)(s_Fills - p_Fills) / fr).ToString("0") : "-") +
                      " | stale skipped " + (s_StaleSkip - p_StaleSkip) + ", stale -> vanilla " + (s_StaleBail - p_StaleBail) +
                      ", other -> vanilla " + (s_BailOther - p_BailOther) +
                      " | answered before (cache/grid) " + (s_Answered - p_Answered) + ", grid self-check " + (s_DeferCheck - p_DeferCheck) +
                      " (counts per 5 s)";
            p_Fast = St.Fast; p_InRange = s_InRange; p_Skipped = s_Skipped; p_Passed = St.Passed; p_Fills = s_Fills; p_Frames = s_Frames;
            p_StaleSkip = s_StaleSkip; p_StaleBail = s_StaleBail; p_BailOther = s_BailOther; p_Answered = s_Answered; p_DeferCheck = s_DeferCheck;
        }
    }
}
