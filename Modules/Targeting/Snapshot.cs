/*
 CollectEnemies_Snapshot / CollectEnemies_SnapshotScore / CollectEnemies_FastAdd (exact; default on since 0.2.0 after
 the night-1 lab measurements, measure\night1\NIGHT1.md) and Scan_Diag (counters only, default on).

 Target: TargetFinder.CollectDetectedEnemies (1.0.17 TargetFinder.cs:349-384) when the grid cannot prune (dense
 melee: every target inside the query range, D 385-415, ~0.8 us per in-range candidate). All three ride on
 CollectEnemies_Grid's prefix (which must be patched): the grid decides WHICH entries are visited (when its gate says
 it prunes), the snapshot makes each visited entry cheaper; with the grid bypassed the snapshot path walks the whole
 list instead of handing the call to vanilla. Both compose; neither changes order or values.

 What a candidate costs in vanilla (decompile 1.0.17), per entry of the list / per in-range candidate:
   prefilter (every entry)   target==null, target.IsDestroyed (-> !DamageManager + Health), target.Team
                             (-> !Owner + virtual Team), Team.GetTeamsAreEnemy (GameMode virtual, Harmony-patched by
                             some mods), OwnerStructure/OwnerConstructionSite casts + Unity bools,
                             Owner.WorldLineOfSightCenter = 3 native calls (get_transform, get_rotation,
                             get_position) + !ObjectInfo
   score (in range)          GameAI.CalculateBestTargetScore (GameAI.cs:671-802): ResolveTargetScoreValues,
                             ~12 Unity bool checks (fromTarget x3, objectInfo x2, ownerUnit x2, damageManager x2, ...),
                             ResolveTargetScoreTypeValues (2 GetInstanceID + 32-entry linear cache; on a miss
                             2 x DamageCalculator.GetMaxDamagePerSecondToObject + AdjustDamage), GetIsTargetedBy,
                             GetWasDamagedByRecently (Time.time native + 4 compares), GetSelfDefenceScore close-range
                             path for every unit (fromTarget.transform.position + target.transform.position = 4 native
                             calls, 2 x PhysicalRadius), jitter (2 x GetInstanceID)
   AddCandidate (in range)   once 24 are held: a 24-slot linear min search through List<float> indexers for EVERY
                             further candidate (~300 of them in a dense melee)

 Stage 1, CollectEnemies_Snapshot: per team a persistent array parallel to Team.DetectedTargets holding, per entry,
   the Target reference it was filled for and the per-object constants: Owner, Transform, ObjectInfo (+ Unity
   truthiness), DamageManager (+ truthiness), Unit (+ truthiness), structure / construction-site flags, the target's
   and its ObjectInfo's instance ids. An entry is used only while ReferenceEquals(entry.Ref, list[i]) and Owner's
   ObjectInfo / DamageManager are still the cached references; otherwise it is refilled (counted as a fill). Nothing
   positional or state-dependent is cached: per scan the snapshot path still reads live target == null, IsDestroyed
   (cached DamageManager), Owner.Team (virtual, live), and the transform pose with ONE native call
   (Transform.GetPositionAndRotation on the cached Transform; setting SnapshotPoseOneCall = false uses rotation +
   position, 2 calls). GetTeamsAreEnemy is evaluated once per distinct target team per scan (memo; exact because
   it is a pure function of the two teams while the scan runs - nothing in the scan can change alliances).
   Why the cached fields cannot change within the lifetime of the entry: Target.Owner is set in Target.Init and nulled
   only in OnDestroy (then target == null, checked first, as vanilla); Transform and the Owner's runtime type are fixed
   for a component's life; DamageManager is assigned once in BaseGameObject.Awake (GetComponent) and ObjectInfo is
   re-read and compared on every use; instance ids never change; ObjectInfo assets are never destroyed during a
   round. So the snapshot needs no frame invalidation at all and positions are never stale (they are read live).
 Stage 2, CollectEnemies_SnapshotScore (needs stage 1): CalculateBestTargetScore + GetSelfDefenceScore +
   GetWasDamagedByRecently replayed with the cached references: per scan once ResolveTargetScoreValues, the 25
   s_Cfg_* values, fromTarget truthiness / ObjectInfo id / instance id / position / PhysicalRadius / Time.time; per
   candidate live: IsFunctional, ObjectInfo.Critical/Score/EngagementRange/PhysicalRadius (fields of the cached
   asset), IsFlying, TopSpeed, GetIsTargetedBy, damaged-by memory, AttackedByEnemyNum, Health, AdjustDamage via the
   type-value cache. The type-value cache is NOT replaced by a cache of ours: vanilla's static 32-entry cache keeps
   the value of the first target instance of each type since its last reset (reset whenever the scanner type /
   damage changes), so a longer-lived cache could return another instance's value. Instead its state machine is
   replayed on vanilla's own private static fields with the cached ids, and a miss calls vanilla's
   CalculateTargetScoreTypeValues: identical state, identical values. The jitter calls vanilla's
   GetTargetScoreJitter01 (its decompiled float constant is not a reliable round-trip literal).
 CollectEnemies_FastAdd: AddCandidate keeps the (index, value) of vanilla's min search cached while the 24 slots do not
   change and recomputes it with vanilla's own loop after every replacement, so the replaced slot (first index of the
   minimum, ties included) is the one vanilla's loop would pick. A candidate that does not beat the minimum costs one
   compare instead of 24.

 Self-check (SelfCheckFraction > 0): a sampled call computes the complete result of the active path (grid and/or
   snapshot, stage 2, FastAdd) into scratch lists, then lets VANILLA run into the real lists and compares count,
   order (reference) and score bits in a postfix. The scratch run leaves vanilla's type-value cache exactly as vanilla
   would (same algorithm and order), so the vanilla run sees the same values. Any stale or wrong field shows as a
   mismatch (logged with the first differing slot).

 Scan_Diag (counters only): AIAiming.FindBestTarget calls and null results per second, TargetFinder.Select results
   with no clear candidate, split by LosTopK on/off - the question whether LosTopK causes extra full rescans.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.Targeting
{
    internal sealed class TeamSnap
    {
        public Team Team;
        public int Cap;
        public Target[] Ref = new Target[0];
        public BaseGameObject[] Owner = new BaseGameObject[0];
        public Transform[] Tr = new Transform[0];
        public ObjectInfo[] Oi = new ObjectInfo[0];
        public bool[] OiOk = new bool[0];
        public DamageManager[] Dm = new DamageManager[0];
        public bool[] DmOk = new bool[0];
        public Unit[] U = new Unit[0];
        public bool[] UOk = new bool[0];
        public byte[] Kind = new byte[0];   // bit 0 structure, bit 1 construction site
        public int[] Id = new int[0];
        public int[] OiId = new int[0];

        public void Grow(int n)
        {
            if (n <= Cap) return;
            int c = Mathf.NextPowerOfTwo(Math.Max(n, 64));
            Array.Resize(ref Ref, c); Array.Resize(ref Owner, c); Array.Resize(ref Tr, c); Array.Resize(ref Oi, c);
            Array.Resize(ref OiOk, c); Array.Resize(ref Dm, c); Array.Resize(ref DmOk, c); Array.Resize(ref U, c);
            Array.Resize(ref UOk, c); Array.Resize(ref Kind, c); Array.Resize(ref Id, c); Array.Resize(ref OiId, c);
            Cap = c;
        }
    }

    internal static class CollectSnapshot
    {
        internal static bool On;          // stage 1
        internal static bool ScoreOn;     // stage 2
        internal static bool FastAddOn;   // AddCandidate min cache
        static PerfItem Item, ScoreItem, FastItem;
        internal static Stat St, ScoreSt, FastSt;
        static bool s_PoseOneCall = true;

        static TeamSnap[] s_Snaps = new TeamSnap[8];
        static int s_SnapCount;

        // ---- GameAI private statics (resolved once)
        static Action s_ResolveCfg;
        delegate void CalcTypeValuesFn(Target fromTarget, Target target, DamageManager dm, float damage, EDamageType type,
                                       out float dpsToTarget, out float dpsToUs, out float modulatedDamage);
        static CalcTypeValuesFn s_CalcTV;
        static Func<int, int, float> s_Jitter;
        static AccessTools.FieldRef<int[]> s_TvIds;
        static AccessTools.FieldRef<float[]> s_TvDpsTo, s_TvDpsUs, s_TvMod;
        static AccessTools.FieldRef<int> s_TvNum, s_TvFrom;
        static AccessTools.FieldRef<float> s_TvDamage;
        static AccessTools.FieldRef<EDamageType> s_TvType;
        static readonly string[] CfgNames =
        {
            "AimDirection", "AlreadyOurTarget", "Critical", "DPSToTarget", "DPSToUs", "DamageToHealth", "DamageToUs", "Distance",
            "Functional", "JitterPct", "Saturation", "SaturationSeconds", "SelfDefence", "SelfDefenceContactSeconds",
            "SelfDefenceThreatSeconds", "SelfDefenceKillSeconds", "SelfDefenceMemorySeconds", "SelfDefenceCloseRange",
            "Speed", "Unit", "WeAreTarget", "WithinView"
        };
        static readonly string[] CfgBaseNames = { "DPS", "Speed", "WorthMultiplier" };
        static AccessTools.FieldRef<float>[] s_Cfg;   // 22 TargetScore_* then 3 TargetValueBase_*

        public static void Register(ModuleContext ctx, TargetingSettings s)
        {
            s_PoseOneCall = s.SnapshotPoseOneCall;
            Item = ctx.Item("CollectEnemies_Snapshot",
                "CollectDetectedEnemies stage 1: per-team snapshot of per-object constants (refs, flags, ids) + one native pose call per entry + per-scan enemy-team memo; also walks the full list when the grid is bypassed (exact)",
                v => On = v, defaultOn: true);
            ScoreItem = ctx.Item("CollectEnemies_SnapshotScore",
                "CollectDetectedEnemies stage 2 (needs stage 1): CalculateBestTargetScore replayed on the snapshot, vanilla type-value cache state machine kept (exact)",
                v => ScoreOn = v, defaultOn: true);
            FastItem = ctx.Item("CollectEnemies_FastAdd",
                "AddCandidate: cached min slot of the 24 (recomputed with vanilla's loop after each replacement; same slot, ties included)",
                v => FastAddOn = v, defaultOn: true);
            St = new Stat(Item, "D", "entry fills");
            ScoreSt = new Stat(ScoreItem, null, "type-value misses");
            FastSt = new Stat(FastItem, null, "min recomputes");

            bool host = CollectEnemiesGrid.Applied;
            foreach (PerfItem it in new[] { Item, ScoreItem, FastItem })
            {
                if (!host) it.Block("needs CollectEnemies_Grid patched (its prefix carries this item)");
            }

            Type ai = typeof(GameAI);
            MethodInfo resolve = Refl.Method(ai, "ResolveTargetScoreValues", Type.EmptyTypes, ScoreItem);
            MethodInfo calc = Refl.Method(ai, "CalculateTargetScoreTypeValues", new[]
            {
                typeof(Target), typeof(Target), typeof(DamageManager), typeof(float), typeof(EDamageType),
                typeof(float).MakeByRefType(), typeof(float).MakeByRefType(), typeof(float).MakeByRefType()
            }, ScoreItem);
            MethodInfo jitter = Refl.Method(ai, "GetTargetScoreJitter01", new[] { typeof(int), typeof(int) }, ScoreItem);
            try
            {
                if (resolve != null) s_ResolveCfg = AccessTools.MethodDelegate<Action>(resolve);
                if (calc != null) s_CalcTV = AccessTools.MethodDelegate<CalcTypeValuesFn>(calc);
                if (jitter != null) s_Jitter = AccessTools.MethodDelegate<Func<int, int, float>>(jitter);
            }
            catch (Exception ex) { ScoreItem.Note("GameAI delegates: " + ex.Message); }
            s_TvIds = Refl.StaticField<int[]>(ai, "s_TypeValues_ObjectInfoID", ScoreItem);
            s_TvDpsTo = Refl.StaticField<float[]>(ai, "s_TypeValues_DPSToTarget", ScoreItem);
            s_TvDpsUs = Refl.StaticField<float[]>(ai, "s_TypeValues_DPSToUs", ScoreItem);
            s_TvMod = Refl.StaticField<float[]>(ai, "s_TypeValues_ModulatedDamage", ScoreItem);
            s_TvNum = Refl.StaticField<int>(ai, "s_TypeValues_Num", ScoreItem);
            s_TvFrom = Refl.StaticField<int>(ai, "s_TypeValues_FromObjectInfoID", ScoreItem);
            s_TvDamage = Refl.StaticField<float>(ai, "s_TypeValues_Damage", ScoreItem);
            s_TvType = Refl.StaticField<EDamageType>(ai, "s_TypeValues_DamageType", ScoreItem);
            s_Cfg = new AccessTools.FieldRef<float>[CfgNames.Length + CfgBaseNames.Length];
            bool cfgOk = true;
            for (int i = 0; i < CfgNames.Length; i++)
            {
                s_Cfg[i] = Refl.StaticField<float>(ai, "s_Cfg_TargetScore_" + CfgNames[i], ScoreItem);
                cfgOk &= s_Cfg[i] != null;
            }
            for (int i = 0; i < CfgBaseNames.Length; i++)
            {
                s_Cfg[CfgNames.Length + i] = Refl.StaticField<float>(ai, "s_Cfg_TargetValueBase_" + CfgBaseNames[i], ScoreItem);
                cfgOk &= s_Cfg[CfgNames.Length + i] != null;
            }
            if (s_ResolveCfg == null || s_CalcTV == null || s_Jitter == null || s_TvIds == null || s_TvDpsTo == null || s_TvDpsUs == null ||
                s_TvMod == null || s_TvNum == null || s_TvFrom == null || s_TvDamage == null || s_TvType == null || !cfgOk)
            {
                ScoreItem.Block("GameAI scoring internals absent on this build");
            }

            if (Item.Hooked("CollectEnemies_Grid prefix (TargetFinder.CollectDetectedEnemies)")) { }
            if (ScoreItem.Hooked("CollectEnemies_Grid prefix (TargetFinder.CollectDetectedEnemies)")) { }
            if (FastItem.Hooked("CollectEnemies_Grid prefix (TargetFinder.CollectDetectedEnemies)")) { }
            Item.OnSwitch(on => { if (!on) ClearRefs(); });
        }

        internal static PerfItem ItemRef => Item;

        internal static void Fail(Exception ex, string where)
        {
            Item.Fail(ex, where);
            ScoreItem.Fail(ex, where);
        }

        static void ClearRefs()
        {
            for (int i = 0; i < s_SnapCount; i++)
            {
                TeamSnap s = s_Snaps[i];
                Array.Clear(s.Ref, 0, s.Cap); Array.Clear(s.Owner, 0, s.Cap); Array.Clear(s.Tr, 0, s.Cap);
                Array.Clear(s.Oi, 0, s.Cap); Array.Clear(s.Dm, 0, s.Cap); Array.Clear(s.U, 0, s.Cap);
            }
        }

        public static void OnRound() => ClearRefs();

        static TeamSnap For(Team team)
        {
            for (int i = 0; i < s_SnapCount; i++) if (ReferenceEquals(s_Snaps[i].Team, team)) return s_Snaps[i];
            if (s_SnapCount == s_Snaps.Length) Array.Resize(ref s_Snaps, s_SnapCount * 2);
            var s = new TeamSnap { Team = team };
            s_Snaps[s_SnapCount++] = s;
            return s;
        }

        // Entry i for the (non-null) target t: use it if still valid, else refill.
        static void Ensure(TeamSnap s, int i, Target t)
        {
            BaseGameObject owner = s.Owner[i];
            if (ReferenceEquals(s.Ref[i], t) && (object)owner != null && ReferenceEquals(owner.ObjectInfo, s.Oi[i]) &&
                ReferenceEquals(owner.DamageManager, s.Dm[i]))
            {
                return;
            }
            owner = t.Owner;
            s.Ref[i] = t;
            s.Owner[i] = owner;
            s.Tr[i] = t.transform;
            ObjectInfo oi = owner.ObjectInfo;
            s.Oi[i] = oi;
            bool oiOk = (bool)oi;
            s.OiOk[i] = oiOk;
            s.OiId[i] = oiOk ? oi.GetInstanceID() : 0;
            DamageManager dm = owner.DamageManager;
            s.Dm[i] = dm;
            s.DmOk[i] = (bool)dm;
            Unit u = t.OwnerUnit;
            s.U[i] = u;
            s.UOk[i] = (bool)u;
            s.Kind[i] = (byte)(((bool)t.OwnerStructure ? 1 : 0) | ((bool)t.OwnerConstructionSite ? 2 : 0));
            s.Id[i] = t.GetInstanceID();
            St.Extra++;
        }

        // ------------------------------------------------------------------ stage 1: prefilter

        static readonly Team[] s_MemoTeam = new Team[16];
        static readonly bool[] s_MemoEnemy = new bool[16];
        static TeamSnap s_Cur;

        // Same filters, same order, same values as CollectEnemiesGrid.Prefilter (vanilla), from the snapshot.
        // idx == null: full list 0..n-1. Writes index, target, LOS centre, distance and transform position.
        public static int Prefilter(List<Target> list, int[] idx, int n, ref TargetScanParams p, Team searchTeam,
                                    ref int[] oIdx, ref Target[] oT, ref Vector3[] oW, ref float[] oD, ref Vector3[] oP)
        {
            if (oIdx.Length < n || oP.Length < n)
            {
                int cap = Mathf.NextPowerOfTwo(Math.Max(n, oIdx.Length));
                oIdx = new int[cap]; oT = new Target[cap]; oW = new Vector3[cap]; oD = new float[cap]; oP = new Vector3[cap];
            }
            TeamSnap s = For(searchTeam);
            s_Cur = s;
            s.Grow(list.Count);
            int memo = 0;
            bool ignoreStructures = p.IgnoreStructures;
            Vector3 from = p.Position;
            float range = p.Range;
            bool one = s_PoseOneCall;
            int k = 0;
            for (int j = 0; j < n; j++)
            {
                int i = idx == null ? j : idx[j];
                Target target = list[i];
                if (target == null)
                {
                    continue;
                }
                Ensure(s, i, target);
                if (s.DmOk[i] && s.Dm[i].IsDestroyed)                     // target.IsDestroyed
                {
                    continue;
                }
                Team tt = s.Owner[i].Team;                                  // target.Team (Owner alive: target is)
                bool enemy = false, found = false;
                for (int q = 0; q < memo; q++)
                {
                    if (ReferenceEquals(s_MemoTeam[q], tt)) { enemy = s_MemoEnemy[q]; found = true; break; }
                }
                if (!found)
                {
                    enemy = Team.GetTeamsAreEnemy(tt, searchTeam);
                    if (memo < s_MemoTeam.Length) { s_MemoTeam[memo] = tt; s_MemoEnemy[memo] = enemy; memo++; }
                }
                if (!enemy || (ignoreStructures && s.Kind[i] != 0))
                {
                    continue;
                }
                Vector3 pos;
                Quaternion rot;
                Transform tr = s.Tr[i];
                if (one) tr.GetPositionAndRotation(out pos, out rot);
                else { rot = tr.rotation; pos = tr.position; }
                Vector3 c = s.OiOk[i] ? s.Oi[i].LineOfSightCenter : Vector3.zero;   // BaseGameObject.LineOfSightCenter
                Vector3 w = rot * c + pos;                                           // GameMath.TransformPointUnscaled
                float d = Vector3.Distance(w, from);
                if (d > range)
                {
                    continue;
                }
                oIdx[k] = i; oT[k] = target; oW[k] = w; oD[k] = d; oP[k] = pos;
                k++;
            }
            Array.Clear(s_MemoTeam, 0, memo);
            St.Visited += n;
            St.ListLen += list.Count;
            St.Passed += k;
            return k;
        }

        // ------------------------------------------------------------------ prefiltered path (CollectPreFiltered_Snapshot)

        // TargetFinder.CollectPreFilteredEnemies' filters (TargetFinder.cs:403-420), in job-output order, from the snapshot.
        // range01 is the job's score, taken as is. Returns -1 when vanilla must run the whole call (an entry with a
        // C#-null Owner: vanilla's later target.ObjectInfo would throw on it). walked = job entries looked at.
        public static int PrefilterPre(Unity.Collections.NativeArray<DistanceScoreContainer> best, List<Target> list, ref TargetScanParams p, Team searchTeam,
                                       ref int[] oIdx, ref Target[] oT, ref Vector3[] oW, ref float[] oR, ref Vector3[] oP, out int walked)
        {
            int len = best.Length;
            if (oIdx.Length < len || oP.Length < len)
            {
                int cap = Mathf.NextPowerOfTwo(Math.Max(len, 64));
                oIdx = new int[cap]; oT = new Target[cap]; oW = new Vector3[cap]; oR = new float[cap]; oP = new Vector3[cap];
            }
            TeamSnap s = For(searchTeam);
            s_Cur = s;
            int count = list.Count;
            s.Grow(count);
            int memo = 0, k = 0, i = 0;
            bool ignoreStructures = p.IgnoreStructures;
            bool one = s_PoseOneCall;
            for (; i < len; i++)
            {
                DistanceScoreContainer c = best[i];
                float score = c.score;
                if (score < 0f)
                {
                    break;
                }
                int index = c.index;
                if (index < 0 || count <= index)
                {
                    continue;
                }
                Target target = list[index];
                if (target == null)
                {
                    continue;
                }
                if ((object)target.Owner == null)
                {
                    Array.Clear(s_MemoTeam, 0, memo);
                    walked = i + 1;
                    return -1;
                }
                Ensure(s, index, target);
                if (s.DmOk[index] && s.Dm[index].IsDestroyed)          // (bool)target.Owner && target.IsDestroyed
                {
                    continue;
                }
                Team tt = s.Owner[index].Team;
                bool enemy = false, found = false;
                for (int q = 0; q < memo; q++)
                {
                    if (ReferenceEquals(s_MemoTeam[q], tt)) { enemy = s_MemoEnemy[q]; found = true; break; }
                }
                if (!found)
                {
                    enemy = Team.GetTeamsAreEnemy(tt, searchTeam);
                    if (memo < s_MemoTeam.Length) { s_MemoTeam[memo] = tt; s_MemoEnemy[memo] = enemy; memo++; }
                }
                if (!enemy || (ignoreStructures && s.Kind[index] != 0))
                {
                    continue;
                }
                Vector3 pos;
                Quaternion rot;
                Transform tr = s.Tr[index];
                if (one) tr.GetPositionAndRotation(out pos, out rot);
                else { rot = tr.rotation; pos = tr.position; }
                Vector3 lc = s.OiOk[index] ? s.Oi[index].LineOfSightCenter : Vector3.zero;
                oIdx[k] = index; oT[k] = target; oW[k] = rot * lc + pos; oR[k] = score; oP[k] = pos;   // target.Owner.WorldLineOfSightCenter
                k++;
            }
            Array.Clear(s_MemoTeam, 0, memo);
            walked = i;
            return k;
        }

        // ------------------------------------------------------------------ stage 2: score replay

        // per-scan scanner context
        static Target c_From;
        static bool c_FromOk, c_FromPosOk;
        static int c_FromOiId, c_FromId;
        static Vector3 c_FromPos;
        static float c_FromPhysR, c_Now;
        static float cAim, cAlready, cCrit, cDpsTo, cDpsUs, cDmgHealth, cDmgUs, cDist, cFunc, cJitter, cSat, cSatSec, cSelf,
                     cSelfContact, cSelfThreat, cSelfKill, cSelfMem, cSelfClose, cSpeed, cUnit, cWeAre, cView, cBaseDps, cBaseSpeed, cWorth;

        // Stage-2 internals resolved (GameAI private statics + delegates).
        internal static bool ReplayReady => s_ResolveCfg != null && s_CalcTV != null && s_Jitter != null && s_TvIds != null && s_TvNum != null;

        public static void BeginScan(Target fromTarget, bool countForScoreItem = true)
        {
            s_ResolveCfg();
            AccessTools.FieldRef<float>[] f = s_Cfg;
            cAim = f[0](); cAlready = f[1](); cCrit = f[2](); cDpsTo = f[3](); cDpsUs = f[4](); cDmgHealth = f[5](); cDmgUs = f[6]();
            cDist = f[7](); cFunc = f[8](); cJitter = f[9](); cSat = f[10](); cSatSec = f[11](); cSelf = f[12](); cSelfContact = f[13]();
            cSelfThreat = f[14](); cSelfKill = f[15](); cSelfMem = f[16](); cSelfClose = f[17](); cSpeed = f[18](); cUnit = f[19]();
            cWeAre = f[20](); cView = f[21](); cBaseDps = f[22](); cBaseSpeed = f[23](); cWorth = f[24]();
            c_From = fromTarget;
            c_FromOk = (bool)fromTarget;
            ObjectInfo foi = c_FromOk ? fromTarget.ObjectInfo : null;
            c_FromOiId = foi ? foi.GetInstanceID() : 0;
            c_FromId = c_FromOk ? fromTarget.GetInstanceID() : 0;
            c_FromPosOk = false;
            c_Now = Time.time;
            if (countForScoreItem)
            {
                ScoreSt.Calls++;      // one scan served by the replay
                ScoreSt.Fast++;
            }
        }

        public static void EndScan()
        {
            c_From = null;
            s_Cur = null;
        }

        // GameAI.ResolveTargetScoreTypeValues on vanilla's own static state, with the cached ids.
        static void TypeValues(int e, Target target, float damage, EDamageType type, out float dpsToTarget, out float dpsToUs, out float modulatedDamage)
        {
            TeamSnap s = s_Cur;
            int num = c_FromOiId;
            ref int from = ref s_TvFrom();
            ref EDamageType tvType = ref s_TvType();
            ref float tvDamage = ref s_TvDamage();
            ref int tvNum = ref s_TvNum();
            if (num != from || type != tvType || damage != tvDamage)
            {
                from = num;
                tvType = type;
                tvDamage = damage;
                tvNum = 0;
            }
            if (s.OiOk[e])
            {
                int instanceID = s.OiId[e];
                int[] ids = s_TvIds();
                for (int i = 0; i < tvNum; i++)
                {
                    if (ids[i] == instanceID)
                    {
                        dpsToTarget = s_TvDpsTo()[i];
                        dpsToUs = s_TvDpsUs()[i];
                        modulatedDamage = s_TvMod()[i];
                        return;
                    }
                }
                ScoreSt.Extra++;
                s_CalcTV(c_From, target, s.Dm[e], damage, type, out dpsToTarget, out dpsToUs, out modulatedDamage);
                if (tvNum < 32)
                {
                    ids[tvNum] = instanceID;
                    s_TvDpsTo()[tvNum] = dpsToTarget;
                    s_TvDpsUs()[tvNum] = dpsToUs;
                    s_TvMod()[tvNum] = modulatedDamage;
                    tvNum++;
                }
            }
            else
            {
                ScoreSt.Extra++;
                s_CalcTV(c_From, target, s.Dm[e], damage, type, out dpsToTarget, out dpsToUs, out modulatedDamage);
            }
        }

        // GameAI.CalculateBestTargetScore, verbatim except that per-object constants come from the snapshot and the
        // scanner's from the per-scan context. targetPos = target.transform.position read this scan.
        public static float Score(int e, Target target, Vector3 targetPos, float fromHealth, float range01, bool ignoreAir, bool ignoreGround,
                                  EDamageType eDamageType, float damage, bool ignoreUndamageable, float targetAimToDirDot, bool withinView, bool alreadyOurTarget)
        {
            TeamSnap s = s_Cur;
            bool flag = eDamageType switch
            {
                EDamageType.Bite => true,
                EDamageType.Slash => true,
                EDamageType.Stab => true,
                EDamageType.Collision => true,
                _ => false,
            };
            ObjectInfo objectInfo = s.Oi[e];
            bool oiOk = s.OiOk[e];
            DamageManager damageManager = s.Dm[e];
            bool dmOk = s.DmOk[e];
            TypeValues(e, target, damage, eDamageType, out var dpsToTarget, out var dpsToUs, out var modulatedDamage);
            float num = Mathf.Clamp01(1f - range01);
            if (flag)
            {
                num *= num;
            }
            float num2 = num * cDist;
            if (alreadyOurTarget)
            {
                num2 += cAlready;
            }
            if (withinView)
            {
                num2 += cView;
            }
            bool isFunctional = target.IsFunctional;
            if (isFunctional)
            {
                num2 += cFunc;
            }
            if (oiOk)
            {
                if (objectInfo.Critical)
                {
                    num2 += cCrit;
                }
                num2 += (float)objectInfo.Score * cWorth;
            }
            num2 += (targetAimToDirDot * 0.5f + 0.5f) * cAim;
            Unit ownerUnit = s.U[e];
            bool unitOk = s.UOk[e];
            if (unitOk)
            {
                if (ignoreAir && ownerUnit.IsFlying)
                {
                    return 0f;
                }
                if (ignoreGround && !ownerUnit.IsFlying)
                {
                    return 0f;
                }
                num2 += cUnit;
                num2 += Mathf.Clamp01(ownerUnit.TopSpeed / cBaseSpeed) * cSpeed;
            }
            if (c_FromOk)
            {
                Target fromTarget = c_From;
                bool isTargetedBy = fromTarget.GetIsTargetedBy(target);
                if (isTargetedBy)
                {
                    num2 += cWeAre;
                }
                if (fromHealth > 0f)
                {
                    num2 += Mathf.Clamp01(dpsToUs / fromHealth) * cDmgUs;
                }
                num2 += Mathf.Clamp01(dpsToUs / cBaseDps) * cDpsUs;
                num2 += Mathf.Clamp01(dpsToTarget / cBaseDps) * cDpsTo;
                float num3 = 0f;
                if (cSelf > 0f)
                {
                    bool wasDamagedByRecently = WasDamagedByRecently(fromTarget, target, cSelfMem);
                    if (isTargetedBy || wasDamagedByRecently)
                    {
                        num3 = SelfDefence(e, fromHealth, targetPos, ownerUnit, unitOk, objectInfo, oiOk, damageManager, dmOk, dpsToTarget, dpsToUs, wasDamagedByRecently, closeOnly: false);
                    }
                    else if (unitOk && cSelfClose > 0f)
                    {
                        num3 = SelfDefence(e, fromHealth, targetPos, ownerUnit, unitOk, objectInfo, oiOk, damageManager, dmOk, dpsToTarget, dpsToUs, hurtUs: false, closeOnly: true);
                    }
                }
                int num4 = target.AttackedByEnemyNum - (alreadyOurTarget ? 1 : 0);
                if (cSat > 0f && num4 > 0 && dpsToTarget > 0f && num2 > 0f && dmOk && damageManager.Health > 0f)
                {
                    if (num4 > 4)
                    {
                        num4 = 4;
                    }
                    float num5 = damageManager.Health / ((float)num4 * dpsToTarget);
                    float num6 = Mathf.Clamp01(cSatSec / num5);
                    if (alreadyOurTarget)
                    {
                        num6 *= 0.5f;
                    }
                    if (num3 > 0f)
                    {
                        num6 *= 1f - Mathf.Clamp01(num3 / cSelf);
                    }
                    float num7 = (isFunctional ? cFunc : 0f);
                    if (num2 > num7)
                    {
                        num2 = num7 + (num2 - num7) / (1f + num6 * cSat);
                    }
                }
                num2 += num3;
            }
            if (eDamageType != EDamageType.None)
            {
                if (dmOk)
                {
                    if (modulatedDamage <= 0f && ignoreUndamageable)
                    {
                        return 0f;
                    }
                    num2 += Mathf.Clamp01(modulatedDamage / damageManager.Health) * cDmgHealth;
                }
                else if (ignoreUndamageable)
                {
                    return 0f;
                }
            }
            if (c_FromOk && cJitter > 0f)
            {
                num2 *= 1f + (s_Jitter(c_FromId, s.Id[e]) * 2f - 1f) * cJitter;
            }
            return num2;
        }

        // Target.GetWasDamagedByRecently with the per-scan Time.time (constant during the scan).
        static bool WasDamagedByRecently(Target fromTarget, Target attacker, float withinSeconds)
        {
            if (!attacker)
            {
                return false;
            }
            float num = c_Now - withinSeconds;
            for (int i = 0; i < 4; i++)
            {
                Target d = fromTarget.GetDamagedBy(i, out float time);
                if (d == attacker)
                {
                    return time >= num;
                }
            }
            return false;
        }

        // GameAI.GetSelfDefenceScore, verbatim with cached references and the scan's positions.
        static float SelfDefence(int e, float fromHealth, Vector3 targetPos, Unit targetUnit, bool unitOk, ObjectInfo targetObjectInfo, bool oiOk,
                                 DamageManager targetDamageManager, bool dmOk, float DPS_toTarget, float DPS_toUs, bool hurtUs, bool closeOnly)
        {
            if (DPS_toTarget <= 0f)
            {
                return 0f;
            }
            float num = 1f;
            if (!hurtUs)
            {
                if (!c_FromPosOk)
                {
                    c_FromPos = c_From.transform.position;
                    c_FromPhysR = c_From.PhysicalRadius;
                    c_FromPosOk = true;
                }
                float num2 = GameMath.Distance(c_FromPos, targetPos);
                if (closeOnly && num2 > cSelfClose + 60f)
                {
                    return 0f;
                }
                float targetPhysR = oiOk ? targetObjectInfo.PhysicalRadius : 1f;   // BaseGameObject.PhysicalRadius
                float num3 = num2 - c_FromPhysR - targetPhysR;
                if (closeOnly)
                {
                    if (num3 >= cSelfClose)
                    {
                        return 0f;
                    }
                    num = 1f - Mathf.Max(num3, 0f) / cSelfClose;
                }
                float num4 = (oiOk ? targetObjectInfo.EngagementRange : 0f);
                float num5 = num3 - num4;
                if (num5 > 0f)
                {
                    float num6 = (unitOk ? targetUnit.TopSpeed : 0f);
                    if (num6 <= 0f || cSelfContact <= 0f)
                    {
                        return 0f;
                    }
                    float num7 = 1f - num5 / num6 / cSelfContact;
                    if (num7 <= 0f)
                    {
                        return 0f;
                    }
                    num *= num7;
                }
            }
            float num8 = ((fromHealth > 0f) ? Mathf.Clamp01(DPS_toUs * cSelfThreat / fromHealth) : 1f);
            if (hurtUs && num8 < 0.5f)
            {
                num8 = 0.5f;
            }
            float num9 = (dmOk ? targetDamageManager.Health : 0f);
            float num10 = ((num9 > 0f) ? Mathf.Clamp01(DPS_toTarget * cSelfKill / num9) : 1f);
            return cSelf * num * num8 * num10;
        }

        // ------------------------------------------------------------------ FastAdd

        // One scan used FastAdd (status q/s).
        public static void FastAddScan()
        {
            FastSt.Calls++;
            FastSt.Fast++;
        }

        // TargetFinder.AddCandidate with vanilla's min search cached. minIdx < 0 = not computed for the current slots.
        public static void AddCandidate(List<Target> cands, List<float> scores, Target target, float score, ref int minIdx, ref float minVal, Stat st)
        {
            if (cands.Count < 24)
            {
                cands.Add(target);
                scores.Add(score);
                minIdx = -1;
                return;
            }
            if (minIdx < 0)
            {
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
                minIdx = index;
                minVal = num;
                st.Extra++;
            }
            st.Passed++;          // candidates past the 24th answered from the cached minimum
            if (!(score <= minVal))
            {
                cands[minIdx] = target;
                scores[minIdx] = score;
                minIdx = -1;
            }
        }
    }

    // ====================================================================== Scan_Diag (counters only)

    internal static class ScanDiag
    {
        internal static bool On;
        static PerfItem Item;
        static AccessTools.FieldRef<TargetFinder, Target> s_Best;
        static AccessTools.FieldRef<TargetFinder, List<int>> s_Ranked;
        static long s_Find, s_FindNull, s_Sel, s_SelNullCands, s_SelNullTopK;
        static long p_Find, p_FindNull, p_Sel, p_SelNullCands, p_SelNullTopK;
        static int s_K = 6;

        public static void Register(ModuleContext ctx, TargetingSettings s)
        {
            s_K = Math.Max(1, s.LosTopK);
            Item = ctx.Item("Scan_Diag",
                "Counters only: AIAiming.FindBestTarget calls / null results, TargetFinder.Select results with candidates but no clear one (split by LosTopK)",
                v => On = v);
            s_Best = Refl.Field<TargetFinder, Target>("m_BestTarget", Item);
            s_Ranked = Refl.Field<TargetFinder, List<int>>("m_Ranked", Item);
            if (s_Best == null || s_Ranked == null) Item.Block("TargetFinder fields absent");
            var find = Item.Target(typeof(AIAiming), nameof(AIAiming.FindBestTarget), Type.EmptyTypes);
            var sel = Item.Target(typeof(TargetFinder), "Select", Type.EmptyTypes);
            Item.Patch(find, typeof(ScanDiag), postfix: nameof(FindPostfix));
            Item.Patch(sel, typeof(ScanDiag), postfix: nameof(SelectPostfix));
        }

        static void FindPostfix(Target __result)
        {
            if (!On) return;
            s_Find++;
            if ((object)__result == null) s_FindNull++;
        }

        static void SelectPostfix(TargetFinder __instance)
        {
            if (!On) return;
            try
            {
                s_Sel++;
                int n = s_Ranked(__instance).Count;
                if (n > 0 && (object)s_Best(__instance) == null)
                {
                    s_SelNullCands++;
                    if (LosTopK.On && n == s_K) s_SelNullTopK++;
                }
            }
            catch (Exception ex) { Item.Fail(ex, "TargetFinder.Select postfix"); }
        }

        public static void Publish(float dt)
        {
            if (Item == null || !Item.Applied) return;
            long f = s_Find - p_Find, fn = s_FindNull - p_FindNull, sl = s_Sel - p_Sel, sn = s_SelNullCands - p_SelNullCands, sk = s_SelNullTopK - p_SelNullTopK;
            p_Find = s_Find; p_FindNull = s_FindNull; p_Sel = s_Sel; p_SelNullCands = s_SelNullCands; p_SelNullTopK = s_SelNullTopK;
            Item.Info = "FindBestTarget/s " + (f / dt).ToString("0") + ", null/s " + (fn / dt).ToString("0") +
                        (f > 0 ? " (" + (100.0 * fn / f).ToString("0") + "%)" : "") +
                        " | Select/s " + (sl / dt).ToString("0") + ", candidates but none clear/s " + (sn / dt).ToString("0") +
                        ", of which LosTopK-trimmed (K " + s_K + ")/s " + (sk / dt).ToString("0") + " | LosTopK " + (LosTopK.On ? "on" : "off");
        }
    }
}
