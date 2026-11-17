#if MULTICORE_OFFLINE_TESTS
/*
 Offline tests of the ParallelScan kernel and serve logic (ScanKernel.cs, the production file, compiled in unchanged
 against UnityMathShim.cs). Normal .NET 8, no Unity, no game. Run:
   dotnet run -c Release --project ParallelScanTests.csproj [-p:MultiCoreTestsOut=<dir>\] [-- <frames> <seed>]

 The reference is an independent transliteration of vanilla 1.0.17 over fake objects: TargetFinder.CollectDetectedEnemies
 (TargetFinder.cs:349-384) + AddCandidate (:534-557), GameAI.CalculateBestTargetScore (GameAI.cs:671-802),
 GetSelfDefenceScore (:804-852), ResolveTargetScoreTypeValues with the GLOBAL 32-entry cache (:863-900), Target.
 GetIsTargetedBy / GetWasDamagedByRecently, the jitter (:917-922).

 1. AddCandidate with the cached minimum == vanilla's linear search on random score streams with many ties.
 2. MergeTypeValues: reset, carry-over with equal values (append order), carry-over with different values (refused),
    overflow (refused).
 3. Frame simulation, threaded: a 3-team world (destroyed refs, dead targets, structures with turrets, flying units,
    targets without ObjectInfo, two size groups per type so modulated damage differs between instances of one type,
    DamageCalculator pairs vanilla has not computed yet). Per frame: snapshot -> all scans computed on worker threads ->
    in loop order: random in-loop changes (attacker counts, current targets, health) marked dirty the way the game
    patches do, random "other scorers" that move the global type-value cache between scans (prefiltered scans,
    turrets), then each scan served (merge + corrections + replay) and compared with the reference run at that
    moment on the live objects and the live cache: candidate order, references, score bits, and the cache state
    vanilla leaves behind. Pass = 0 mismatches over all served scans, and a served share that shows the paths ran.
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SiServerPerf.Modules.MultiCore.Tests
{
    sealed class FType
    {
        public int OiId;
        public Vector3 LosC;
        public float PhysR, EngR;
        public int Score;
        public bool Critical;
    }

    sealed class FObj
    {
        public int Id;
        public int Team;
        public bool Gone;                 // Unity-destroyed: `target == null` is true
        public bool HasOi, HasDm;
        public int Type;
        public float Size;
        public float Health;
        public bool IsUnit, IsStructure, IsSite;
        public bool Functional = true, Flying;
        public float TopSpeed;
        public Vector3 Pos;
        public Quaternion Rot;
        public int Attacked;
        public FObj CurrentTarget;        // unit: AIAiming.AimTarget
        public readonly List<FObj> TurretTargets = new List<FObj>();
        public readonly FObj[] DmgBy = new FObj[4];
        public readonly float[] DmgTime = { -1000f, -1000f, -1000f, -1000f };
        public bool IsDestroyed => HasDm && Health <= 0f;
    }

    sealed class FParams
    {
        public int SearchTeam;
        public Vector3 Position, AimDir;
        public float Range, Damage;
        public int DamageType;            // 0 None, 1 bullet, 2 bite (melee)
        public bool IgnoreUndamageable, IgnoreAir, IgnoreGround, IgnoreStructures;
        public FObj CurrentTarget;
        public FObj From;                 // LineOfSightFromTarget
    }

    sealed class World
    {
        public FType[] Types;
        public readonly List<FObj> Objs = new List<FObj>();
        public readonly List<FObj>[] Lists = { new List<FObj>(), new List<FObj>(), new List<FObj>() };
        public float[,] Dps;              // [fromType, toType]
        public bool[,] DpsKnown;          // vanilla's DamageCalculator caches hold the pair
        public bool[] EntryKnown;         // vanilla's DamageCalculator holds the entry of that scanner type
        public float Now = 100f;
        public ScoreCfg Cfg;
        public static bool Enemy(int a, int b) => a != b;

        public float DpsTrue(FObj from, FObj to)
        {
            if (from == null || to == null || !from.HasOi) return 0f;
            EntryKnown[from.Type] = true;            // vanilla creates the entry on any call with a typed `from`
            if (!to.HasOi) return 0f;
            DpsKnown[from.Type, to.Type] = true;     // vanilla computes and caches the pair on first use
            return Dps[from.Type, to.Type];
        }

        // DamageManager.AdjustDamage(damage, type, false): per (type, size) group.
        public static float ModFn(int type, float size, float damage, int dtype)
        {
            if (type < 0) return damage * 0.5f;
            uint h = (uint)(type * 7919 + dtype * 104729) ^ (uint)BitConverter.SingleToInt32Bits(damage);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            float mult = (h % 1000) / 500f;          // 0 .. 2 (0 for some: undamageable)
            if (h % 7 == 0) mult = 0f;
            return damage * mult * (size > 1f ? 1.25f : 1f);
        }
    }

    // ===================================================================== reference: vanilla over fake objects
    static class Ref
    {
        public static float Jitter01(int fromInstanceID, int targetInstanceID)
        {
            int num = (fromInstanceID * -1640531535) ^ (targetInstanceID * -2048144777);
            int num2 = (num ^ (num >>> 15)) * -2048144777;
            return (float)(uint)((num2 ^ (num2 >>> 13)) & 0xFFFFFF) * 5.960465E-08f;
        }

        static void CalculateTargetScoreTypeValues(World w, FObj fromTarget, FObj target, int dtype, float damage, out float dpsToTarget, out float dpsToUs, out float modulatedDamage)
        {
            if (fromTarget != null)
            {
                dpsToTarget = w.DpsTrue(fromTarget, target);
                dpsToUs = w.DpsTrue(target, fromTarget);
            }
            else
            {
                dpsToTarget = 0f;
                dpsToUs = 0f;
            }
            modulatedDamage = (dtype != 0 && target.HasDm) ? World.ModFn(target.HasOi ? target.Type : -1, target.Size, damage, dtype) : 0f;
        }

        public static void ResolveTargetScoreTypeValues(World w, TvState s, FObj fromTarget, FObj target, float damage, int dtype, out float dpsToTarget, out float dpsToUs, out float modulatedDamage)
        {
            int num = (fromTarget != null && fromTarget.HasOi) ? w.Types[fromTarget.Type].OiId : 0;
            if (num != s.From || dtype != s.Type || damage != s.Damage)
            {
                s.From = num;
                s.Type = dtype;
                s.Damage = damage;
                s.Num = 0;
            }
            if (target.HasOi)
            {
                int instanceID = w.Types[target.Type].OiId;
                for (int i = 0; i < s.Num; i++)
                {
                    if (s.Ids[i] == instanceID)
                    {
                        dpsToTarget = s.To[i];
                        dpsToUs = s.Us[i];
                        modulatedDamage = s.Mod[i];
                        return;
                    }
                }
                CalculateTargetScoreTypeValues(w, fromTarget, target, dtype, damage, out dpsToTarget, out dpsToUs, out modulatedDamage);
                if (s.Num < 32)
                {
                    s.Ids[s.Num] = instanceID;
                    s.To[s.Num] = dpsToTarget;
                    s.Us[s.Num] = dpsToUs;
                    s.Mod[s.Num] = modulatedDamage;
                    s.Num++;
                }
            }
            else
            {
                CalculateTargetScoreTypeValues(w, fromTarget, target, dtype, damage, out dpsToTarget, out dpsToUs, out modulatedDamage);
            }
        }

        static bool GetIsTargetedBy(FObj self, FObj byTarget)
        {
            if (!byTarget.Functional) return false;
            if (byTarget.IsUnit)
            {
                if (byTarget.CurrentTarget == self) return true;
            }
            else if (byTarget.IsStructure)
            {
                foreach (FObj t in byTarget.TurretTargets) if (t == self) return true;
            }
            return false;
        }

        public static bool IsTargetedByPublic(FObj self, FObj byTarget) => GetIsTargetedBy(self, byTarget);

        static bool GetWasDamagedByRecently(World w, FObj self, FObj attacker, float withinSeconds)
        {
            float num = w.Now - withinSeconds;
            for (int i = 0; i < 4; i++)
            {
                if (self.DmgBy[i] == attacker)
                {
                    return self.DmgTime[i] >= num;
                }
            }
            return false;
        }

        static float PhysicalRadius(World w, FObj o) => o.HasOi ? w.Types[o.Type].PhysR : 1f;

        static float GetSelfDefenceScore(World w, FObj fromTarget, float fromHealth, FObj target, float DPS_toTarget, float DPS_toUs, bool hurtUs, bool closeOnly)
        {
            ScoreCfg c = w.Cfg;
            if (DPS_toTarget <= 0f)
            {
                return 0f;
            }
            float num = 1f;
            if (!hurtUs)
            {
                Vector3 to = target.Pos;
                to -= fromTarget.Pos;
                float num2 = Mathf.Sqrt(to.x * to.x + to.y * to.y + to.z * to.z);
                if (closeOnly && num2 > c.SelfClose + 60f)
                {
                    return 0f;
                }
                float num3 = num2 - PhysicalRadius(w, fromTarget) - PhysicalRadius(w, target);
                if (closeOnly)
                {
                    if (num3 >= c.SelfClose)
                    {
                        return 0f;
                    }
                    num = 1f - Mathf.Max(num3, 0f) / c.SelfClose;
                }
                float num4 = (target.HasOi ? w.Types[target.Type].EngR : 0f);
                float num5 = num3 - num4;
                if (num5 > 0f)
                {
                    float num6 = (target.IsUnit ? target.TopSpeed : 0f);
                    if (num6 <= 0f || c.SelfContact <= 0f)
                    {
                        return 0f;
                    }
                    float num7 = 1f - num5 / num6 / c.SelfContact;
                    if (num7 <= 0f)
                    {
                        return 0f;
                    }
                    num *= num7;
                }
            }
            float num8 = ((fromHealth > 0f) ? Mathf.Clamp01(DPS_toUs * c.SelfThreat / fromHealth) : 1f);
            if (hurtUs && num8 < 0.5f)
            {
                num8 = 0.5f;
            }
            float num9 = (target.HasDm ? target.Health : 0f);
            float num10 = ((num9 > 0f) ? Mathf.Clamp01(DPS_toTarget * c.SelfKill / num9) : 1f);
            return c.Self * num * num8 * num10;
        }

        public static float CalculateBestTargetScore(World w, TvState s, FObj fromTarget, float fromHealth, FObj target, float range01, bool ignoreAir, bool ignoreGround,
                                                     int dtype, float damage, bool ignoreUndamageable, float targetAimToDirDot, bool alreadyOurTarget)
        {
            ScoreCfg c = w.Cfg;
            bool flag = dtype == 2;
            ResolveTargetScoreTypeValues(w, s, fromTarget, target, damage, dtype, out var dpsToTarget, out var dpsToUs, out var modulatedDamage);
            float num = Mathf.Clamp01(1f - range01);
            if (flag)
            {
                num *= num;
            }
            float num2 = num * c.Dist;
            if (alreadyOurTarget)
            {
                num2 += c.Already;
            }
            bool isFunctional = target.Functional;
            if (isFunctional)
            {
                num2 += c.Func;
            }
            if (target.HasOi)
            {
                FType oi = w.Types[target.Type];
                if (oi.Critical)
                {
                    num2 += c.Crit;
                }
                num2 += (float)oi.Score * c.Worth;
            }
            num2 += (targetAimToDirDot * 0.5f + 0.5f) * c.Aim;
            if (target.IsUnit)
            {
                if (ignoreAir && target.Flying)
                {
                    return 0f;
                }
                if (ignoreGround && !target.Flying)
                {
                    return 0f;
                }
                num2 += c.Unit;
                num2 += Mathf.Clamp01(target.TopSpeed / c.BaseSpeed) * c.Speed;
            }
            if (fromTarget != null)
            {
                bool isTargetedBy = GetIsTargetedBy(fromTarget, target);
                if (isTargetedBy)
                {
                    num2 += c.WeAre;
                }
                if (fromHealth > 0f)
                {
                    num2 += Mathf.Clamp01(dpsToUs / fromHealth) * c.DmgUs;
                }
                num2 += Mathf.Clamp01(dpsToUs / c.BaseDps) * c.DpsUs;
                num2 += Mathf.Clamp01(dpsToTarget / c.BaseDps) * c.DpsTo;
                float num3 = 0f;
                if (c.Self > 0f)
                {
                    bool wasDamagedByRecently = GetWasDamagedByRecently(w, fromTarget, target, c.SelfMem);
                    if (isTargetedBy || wasDamagedByRecently)
                    {
                        num3 = GetSelfDefenceScore(w, fromTarget, fromHealth, target, dpsToTarget, dpsToUs, wasDamagedByRecently, closeOnly: false);
                    }
                    else if (target.IsUnit && c.SelfClose > 0f)
                    {
                        num3 = GetSelfDefenceScore(w, fromTarget, fromHealth, target, dpsToTarget, dpsToUs, hurtUs: false, closeOnly: true);
                    }
                }
                int num4 = target.Attacked - (alreadyOurTarget ? 1 : 0);
                if (c.Sat > 0f && num4 > 0 && dpsToTarget > 0f && num2 > 0f && target.HasDm && target.Health > 0f)
                {
                    if (num4 > 4)
                    {
                        num4 = 4;
                    }
                    float num5 = target.Health / ((float)num4 * dpsToTarget);
                    float num6 = Mathf.Clamp01(c.SatSec / num5);
                    if (alreadyOurTarget)
                    {
                        num6 *= 0.5f;
                    }
                    if (num3 > 0f)
                    {
                        num6 *= 1f - Mathf.Clamp01(num3 / c.Self);
                    }
                    float num7 = (isFunctional ? c.Func : 0f);
                    if (num2 > num7)
                    {
                        num2 = num7 + (num2 - num7) / (1f + num6 * c.Sat);
                    }
                }
                num2 += num3;
            }
            if (dtype != 0)
            {
                if (target.HasDm)
                {
                    if (modulatedDamage <= 0f && ignoreUndamageable)
                    {
                        return 0f;
                    }
                    num2 += Mathf.Clamp01(modulatedDamage / target.Health) * c.DmgHealth;
                }
                else if (ignoreUndamageable)
                {
                    return 0f;
                }
            }
            if (fromTarget != null && c.Jitter > 0f)
            {
                num2 *= 1f + (Jitter01(fromTarget.Id, target.Id) * 2f - 1f) * c.Jitter;
            }
            return num2;
        }

        static float FromHealth(FParams p) => (p.From == null || !p.From.HasDm) ? 0f : p.From.Health;

        static Vector3 WorldLos(World w, FObj t) => t.Rot * (t.HasOi ? w.Types[t.Type].LosC : Vector3.zero) + t.Pos;

        // TargetFinder.CollectPreFilteredEnemies (TargetFinder.cs:396-433) over the given filter slice.
        public static void CollectPreFilteredEnemies(World w, TvState s, FParams p, List<(float score, int index)> bestTargetScores, List<FObj> cands, List<float> scores, out bool nan)
        {
            nan = false;
            float fromHealth = FromHealth(p);
            List<FObj> detected = w.Lists[p.SearchTeam];
            for (int i = 0; i < bestTargetScores.Count; i++)
            {
                float score = bestTargetScores[i].score;
                if (score < 0f)
                {
                    break;
                }
                int index = bestTargetScores[i].index;
                if (index < 0 || detected.Count <= index)
                {
                    continue;
                }
                FObj target = detected[index];
                if (target.Gone || target.IsDestroyed || !World.Enemy(target.Team, p.SearchTeam) || (p.IgnoreStructures && (target.IsStructure || target.IsSite)))
                {
                    continue;
                }
                float targetAimToDirDot = Vector3.Dot((WorldLos(w, target) - p.Position).normalized, p.AimDir);
                score = CalculateBestTargetScore(w, s, p.From, fromHealth, target, score, p.IgnoreAir, p.IgnoreGround, p.DamageType, p.Damage,
                                                 p.IgnoreUndamageable, targetAimToDirDot, p.CurrentTarget == target);
                if (!(score > 0f))
                {
                    if (float.IsNaN(score)) nan = true;
                    continue;
                }
                AddCandidate(cands, scores, target, score);
            }
        }

        // TargetFinder.CollectDetectedEnemies + AddCandidate.
        public static void CollectDetectedEnemies(World w, TvState s, FParams p, List<FObj> cands, List<float> scores, out bool nan)
        {
            nan = false;
            float fromHealth = FromHealth(p);
            List<FObj> detectedTargets = w.Lists[p.SearchTeam];
            for (int i = 0; i < detectedTargets.Count; i++)
            {
                FObj target = detectedTargets[i];
                if (target.Gone || target.IsDestroyed || !World.Enemy(target.Team, p.SearchTeam) || (p.IgnoreStructures && (target.IsStructure || target.IsSite)))
                {
                    continue;
                }
                Vector3 worldLineOfSightCenter = WorldLos(w, target);
                float num = Vector3.Distance(worldLineOfSightCenter, p.Position);
                if (num > p.Range)
                {
                    continue;
                }
                float targetAimToDirDot = Vector3.Dot((worldLineOfSightCenter - p.Position).normalized, p.AimDir);
                num = CalculateBestTargetScore(w, s, p.From, fromHealth, target, num / p.Range, p.IgnoreAir, p.IgnoreGround, p.DamageType, p.Damage,
                                               p.IgnoreUndamageable, targetAimToDirDot, p.CurrentTarget == target);
                if (!(num > 0f))
                {
                    if (float.IsNaN(num)) nan = true;
                    continue;
                }
                AddCandidate(cands, scores, target, num);
            }
        }

        public static void AddCandidate<T>(List<T> m_Candidates, List<float> m_Scores, T target, float score)
        {
            if (m_Candidates.Count < 24)
            {
                m_Candidates.Add(target);
                m_Scores.Add(score);
                return;
            }
            int index = 0;
            float num = m_Scores[0];
            for (int i = 1; i < m_Scores.Count; i++)
            {
                if (!(m_Scores[i] >= num))
                {
                    num = m_Scores[i];
                    index = i;
                }
            }
            if (!(score <= num))
            {
                m_Candidates[index] = target;
                m_Scores[index] = score;
            }
        }
    }

    // ===================================================================== the test driver
    static class ParallelScanTests
    {
        static int s_Fail;

        static void Check(bool ok, string what)
        {
            if (ok) return;
            s_Fail++;
            if (s_Fail <= 25) Console.WriteLine("  FAIL " + what);
        }

        static int Main(string[] args)
        {
            int frames = args.Length > 0 ? int.Parse(args[0]) : 300;
            int seed = args.Length > 1 ? int.Parse(args[1]) : 12345;
            Console.WriteLine("ParallelScan offline tests: frames " + frames + ", seed " + seed);
            TestAddCandidate(seed);
            TestMerge();
            TestFrames(frames, seed, corrections: true);
            // negative control: the same frames without the main-thread corrections must show mismatches, otherwise the
            // simulation does not exercise the in-loop dependency at all
            int before = s_Fail;
            long m = TestFrames(Math.Min(frames, 100), seed, corrections: false);
            s_Fail = before;
            Check(m > 0, "negative control: without corrections there were no mismatches (simulation too weak)");
            Console.WriteLine("4 negative control without corrections: " + m + " mismatching served scans (expected > 0)");
            Console.WriteLine(s_Fail == 0 ? "ALL PASSED" : "FAILED: " + s_Fail);
            return s_Fail == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------- 1
        static void TestAddCandidate(int seed)
        {
            var r = new System.Random(seed);
            int runs = 0;
            for (int run = 0; run < 3000; run++)
            {
                int n = r.Next(0, 200);
                int levels = 1 + r.Next(12);
                var lc = new List<int>(); var ls = new List<float>();
                var cands = new int[24]; var scores = new float[24];
                int count = 0, minIdx = -1; float minVal = 0f;
                for (int i = 0; i < n; i++)
                {
                    float s = r.Next(levels) * 0.25f + (r.Next(5) == 0 ? (float)r.NextDouble() : 0f);
                    Ref.AddCandidate(lc, ls, i, s);
                    ScanKernel.AddCandidate(cands, scores, ref count, i, s, ref minIdx, ref minVal);
                }
                bool same = count == lc.Count;
                for (int i = 0; same && i < count; i++) same = cands[i] == lc[i] && ScanKernel.SameBits(scores[i], ls[i]);
                Check(same, "AddCandidate run " + run + ": cached-min slots differ from vanilla");
                runs++;
            }
            Console.WriteLine("1 AddCandidate cached minimum vs vanilla: " + runs + " streams");
        }

        // ---------------------------------------------------------------- 2
        static ScanJob JobWithTypes(int from, int type, float damage, params (int id, float v)[] tv)
        {
            var j = new ScanJob { FromOiId = from, DamageType = type, Damage = damage };
            foreach (var (id, v) in tv)
            {
                j.TvId[j.TvNum] = id; j.TvTo[j.TvNum] = v; j.TvUs[j.TvNum] = v + 1; j.TvMod[j.TvNum] = v + 2; j.TvNum++;
            }
            j.NSeq = j.TvNum;   // one in-range entry per type
            return j;
        }

        static void TestMerge()
        {
            // reset: key differs
            var s = new TvState { From = 5, Type = 1, Damage = 10f, Num = 2 };
            s.Ids[0] = 100; s.Ids[1] = 101;
            var j = JobWithTypes(6, 1, 10f, (200, 1f), (201, 2f));
            Check(ScanKernel.MergeTypeValues(j, s, true) == ScanReason.None && s.From == 6 && s.Num == 2 && s.Ids[0] == 200 && s.Ids[1] == 201, "merge reset");
            // NaN damage in vanilla's initial state always resets
            s = new TvState { From = 6, Type = 1, Damage = float.NaN, Num = 0 };
            Check(ScanKernel.MergeTypeValues(j, s, true) == ScanReason.None && s.Damage == 10f && s.Num == 2, "merge reset from NaN damage");
            // carry-over, shared type equal, new type appended after the live ones
            s = new TvState { From = 6, Type = 1, Damage = 10f, Num = 2 };
            s.Ids[0] = 300; s.To[0] = 9f; s.Us[0] = 9f; s.Mod[0] = 9f;
            s.Ids[1] = 201; s.To[1] = 2f; s.Us[1] = 3f; s.Mod[1] = 4f;
            Check(ScanKernel.MergeTypeValues(j, s, true) == ScanReason.None && s.Num == 3 && s.Ids[0] == 300 && s.Ids[1] == 201 && s.Ids[2] == 200 && s.To[2] == 1f,
                  "merge carry-over append");
            // carry-over, shared type with other values -> refused, state untouched
            s = new TvState { From = 6, Type = 1, Damage = 10f, Num = 1 };
            s.Ids[0] = 201; s.To[0] = 2.5f; s.Us[0] = 3f; s.Mod[0] = 4f;
            Check(ScanKernel.MergeTypeValues(j, s, true) == ScanReason.CarryValues && s.Num == 1, "merge carry-over differing values refused");
            // overflow
            s = new TvState { From = 6, Type = 1, Damage = 10f, Num = 31 };
            for (int i = 0; i < 31; i++) s.Ids[i] = 1000 + i;
            Check(ScanKernel.MergeTypeValues(j, s, true) == ScanReason.CarryOverflow && s.Num == 31, "merge carry-over overflow refused");
            // no entry in range: vanilla never calls the cache, so not even the key changes
            s = new TvState { From = 5, Type = 1, Damage = 10f, Num = 2 };
            var empty = JobWithTypes(6, 2, 40f);
            Check(ScanKernel.MergeTypeValues(empty, s, true) == ScanReason.None && s.From == 5 && s.Type == 1 && s.Damage == 10f && s.Num == 2, "merge empty scan leaves state");
            Console.WriteLine("2 MergeTypeValues: reset / carry-over append / differing values / overflow / empty scan");
        }

        // ---------------------------------------------------------------- 3
        sealed class Scan
        {
            public FParams P;
            public List<(float score, int index)> Buf;   // prefiltered: the team's Burst filter slice
            public ScanJob Job = new ScanJob();
        }

        static World MakeWorld(System.Random r, int perTeam)
        {
            var w = new World();
            int nTypes = 40;   // > 32 so a wide scan overflows the cache
            w.Types = new FType[nTypes];
            for (int t = 0; t < nTypes; t++)
            {
                w.Types[t] = new FType
                {
                    OiId = 50000 + t * 3,
                    LosC = new Vector3((float)r.NextDouble() - 0.5f, 1f + (float)r.NextDouble() * 2f, (float)r.NextDouble() - 0.5f),
                    PhysR = 0.5f + (float)r.NextDouble() * 5f,
                    EngR = (float)r.NextDouble() * 60f,
                    Score = r.Next(0, 50),
                    Critical = r.Next(8) == 0
                };
            }
            w.Dps = new float[nTypes, nTypes];
            w.DpsKnown = new bool[nTypes, nTypes];
            for (int a = 0; a < nTypes; a++)
            for (int b = 0; b < nTypes; b++)
            {
                w.Dps[a, b] = r.Next(4) == 0 ? 0f : (float)r.NextDouble() * 300f;
                w.DpsKnown[a, b] = r.Next(20) != 0;   // ~5 % not yet computed by vanilla
            }
            w.EntryKnown = new bool[nTypes];
            for (int a = 0; a < nTypes; a++) w.EntryKnown[a] = r.Next(10) != 0;
            var c = new ScoreCfg();
            for (int i = 0; i < ScoreCfg.Count; i++) c.Set(i, 0.2f + (float)r.NextDouble() * 3f);
            c.SatSec = 4f; c.BaseDps = 120f; c.BaseSpeed = 30f; c.SelfMem = 5f; c.SelfClose = 25f; c.Jitter = 0.1f;
            c.JitterFn = Ref.Jitter01;
            w.Cfg = c;
            int id = -100000;
            for (int team = 0; team < 3; team++)
            {
                for (int k = 0; k < perTeam; k++)
                {
                    var o = new FObj
                    {
                        Id = id -= 7,
                        Team = team,
                        HasOi = r.Next(150) != 0,
                        HasDm = r.Next(30) != 0,
                        Type = r.Next(nTypes),
                        Size = r.Next(4) == 0 ? 1.5f : 1f,
                        Health = 20f + (float)r.NextDouble() * 500f,
                        Pos = new Vector3((float)(r.NextDouble() * 400 - 200), (float)(r.NextDouble() * 20), (float)(r.NextDouble() * 400 - 200)),
                        Rot = RandRot(r),
                        Attacked = r.Next(6),
                        TopSpeed = (float)r.NextDouble() * 40f,
                    };
                    int kind = r.Next(20);
                    if (kind < 16) { o.IsUnit = true; o.Flying = r.Next(6) == 0; }
                    else if (kind < 19) { o.IsStructure = true; o.Functional = r.Next(4) != 0; }
                    else o.IsSite = true;
                    w.Objs.Add(o);
                }
            }
            foreach (FObj o in w.Objs)
            {
                if (o.IsUnit && r.Next(3) == 0) o.CurrentTarget = w.Objs[r.Next(w.Objs.Count)];
                if (o.IsStructure) { int n = r.Next(9); for (int q = 0; q < n; q++) o.TurretTargets.Add(w.Objs[r.Next(w.Objs.Count)]); }
                for (int q = 0; q < 4; q++)
                {
                    if (r.Next(3) == 0) { o.DmgBy[q] = w.Objs[r.Next(w.Objs.Count)]; o.DmgTime[q] = w.Now - (float)r.NextDouble() * 10f; }
                }
                if (r.Next(25) == 0) o.Health = 0f;      // dead, still listed
                if (r.Next(60) == 0) o.Gone = true;      // Unity-destroyed reference still listed
            }
            // detected lists: most units of the other teams + a few own / neutral-ish entries
            for (int team = 0; team < 3; team++)
            {
                foreach (FObj o in w.Objs)
                {
                    if (o.Team != team ? r.Next(10) < 9 : r.Next(15) == 0) w.Lists[team].Add(o);
                }
                Shuffle(r, w.Lists[team]);
            }
            return w;
        }

        static Quaternion RandRot(System.Random r)
        {
            float x = (float)(r.NextDouble() * 2 - 1), y = (float)(r.NextDouble() * 2 - 1), z = (float)(r.NextDouble() * 2 - 1), ww = (float)(r.NextDouble() * 2 - 1);
            float n = (float)Math.Sqrt(x * x + y * y + z * z + ww * ww);
            return new Quaternion(x / n, y / n, z / n, ww / n);
        }

        static void Shuffle<T>(System.Random r, List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int k = r.Next(i + 1); T t = l[i]; l[i] = l[k]; l[k] = t; }
        }

        // The game's StageTeam over fake objects.
        static readonly int[] s_Ids = new int[64];

        static void Stage(World w, int team, ScanTeamData d, Dictionary<long, int> groups, List<(int type, float size)> groupInfo)
        {
            List<FObj> list = w.Lists[team];
            d.N = list.Count;
            d.Grow(d.N);
            d.CtPoolN = 0;
            for (int i = 0; i < d.N; i++)
            {
                FObj o = list[i];
                if (o.Gone || o.IsDestroyed || !World.Enemy(o.Team, team)) { d.Flags[i] = 0; continue; }
                int f = SF.Cand;
                if (o.HasOi) f |= SF.OiOk;
                if (o.HasDm) f |= SF.DmOk;
                if (o.IsUnit) f |= SF.UnitOk;
                if (o.IsStructure) f |= SF.Structure;
                if (o.IsStructure || o.IsSite) f |= SF.StructOrSite;
                if (o.Functional) f |= SF.Functional;
                if (o.IsUnit && o.Flying) f |= SF.Flying;
                d.Pos[i] = o.Pos; d.Rot[i] = o.Rot;
                FType ty = w.Types[o.Type];
                if (o.HasOi)
                {
                    d.LosC[i] = ty.LosC; d.PhysR[i] = ty.PhysR; d.EngR[i] = ty.EngR; d.OiScore[i] = ty.Score;
                    if (ty.Critical) f |= SF.Critical;
                }
                else
                {
                    d.LosC[i] = Vector3.zero; d.PhysR[i] = 1f; d.EngR[i] = 0f; d.OiScore[i] = 0;
                }
                d.TopSpeed[i] = o.IsUnit ? o.TopSpeed : 0f;
                d.Health[i] = o.HasDm ? o.Health : 0f;
                d.Attacked[i] = o.Attacked;
                if (o.IsUnit)
                {
                    s_Ids[0] = o.CurrentTarget == null ? 0 : o.CurrentTarget.Id;
                    d.SetTargets(i, s_Ids, 1);
                }
                else
                {
                    int m = 0;
                    if (o.IsStructure) foreach (FObj t in o.TurretTargets) s_Ids[m++] = t.Id;
                    d.SetTargets(i, s_Ids, m);
                }
                d.Id[i] = o.Id;
                d.OiId[i] = o.HasOi ? ty.OiId : 0;
                d.TypeSlot[i] = o.HasOi ? o.Type : 0;
                if (o.HasDm)
                {
                    long key = o.HasOi ? ((long)o.Type << 32) | (uint)BitConverter.SingleToInt32Bits(o.Size) : ((long)o.Id << 32) | 0xFFFFFFFFu;
                    if (!groups.TryGetValue(key, out int g)) { g = groups.Count; groups.Add(key, g); groupInfo.Add((o.HasOi ? o.Type : -1, o.Size)); }
                    d.GroupSlot[i] = g;
                }
                else
                {
                    d.GroupSlot[i] = -1;
                }
                d.Flags[i] = f;
            }
        }

        static ScanTables Tables(World w, FParams p, List<(int type, float size)> groupInfo)
        {
            var tb = new ScanTables();
            int nTypes = w.Types.Length;
            tb.Grow(nTypes, groupInfo.Count + 1);
            FObj from = p.From;
            for (int s = 0; s < nTypes; s++)
            {
                if (from == null || !from.HasOi) { tb.DpsTo[s] = 0f; tb.DpsUs[s] = 0f; continue; }
                tb.DpsTo[s] = w.DpsKnown[from.Type, s] ? w.Dps[from.Type, s] : float.NaN;
                tb.DpsUs[s] = w.DpsKnown[s, from.Type] ? w.Dps[s, from.Type] : float.NaN;
            }
            for (int g = 0; g < groupInfo.Count; g++)
            {
                tb.Mod[g] = p.DamageType == 0 ? 0f : World.ModFn(groupInfo[g].type, groupInfo[g].size, p.Damage, p.DamageType);
            }
            tb.FromEntryMissing = from != null && from.HasOi && !w.EntryKnown[from.Type];
            return tb;
        }

        static void FillJob(World w, Scan sc, ScanTeamData d, ScanTables tb)
        {
            FParams p = sc.P;
            ScanJob j = sc.Job;
            j.Team = d; j.Tables = tb; j.Cfg = w.Cfg;
            j.Pref = sc.Buf != null;
            if (j.Pref)
            {
                j.EnsurePref(sc.Buf.Count);
                int m = 0;
                foreach (var (score, index) in sc.Buf) { if (score < 0f) break; j.PIdx[m] = index; j.PScore[m] = score; m++; }
                j.PN = m;
            }
            j.Position = p.Position; j.AimDir = p.AimDir; j.Range = p.Range; j.Damage = p.Damage; j.DamageType = p.DamageType;
            j.DamageTypeNone = p.DamageType == 0; j.Melee = p.DamageType == 2;
            j.IgnoreAir = p.IgnoreAir; j.IgnoreGround = p.IgnoreGround; j.IgnoreStructures = p.IgnoreStructures; j.IgnoreUndamageable = p.IgnoreUndamageable;
            j.CurrentTargetId = p.CurrentTarget == null ? 0 : p.CurrentTarget.Id;
            FObj from = p.From;
            j.FromOk = from != null;
            j.FromOiOk = from != null && from.HasOi;
            j.FromId = from != null ? from.Id : 0;
            j.FromOiId = from != null && from.HasOi ? w.Types[from.Type].OiId : 0;
            j.FromPos = from != null ? from.Pos : default;
            j.FromPhysR = from != null ? (from.HasOi ? w.Types[from.Type].PhysR : 1f) : 0f;
            j.FromHealth = from == null || !from.HasDm ? 0f : from.Health;
            for (int i = 0; i < 4; i++)
            {
                j.DmgById[i] = from != null && from.DmgBy[i] != null ? from.DmgBy[i].Id : 0;
                j.DmgByTime[i] = from != null ? from.DmgTime[i] : 0f;
            }
            j.Now = w.Now;
            j.EnsureOutputs(d.N);
            j.Claim = ScanJob.ClaimNone;
            j.Status = ScanJob.Pending;
        }

        // The Burst filter of the team for a query (DistanceToScoreTeamJob + BumpDownTeamJob): in-range entries by
        // transform position, compacted, -1 terminated. Scores deliberately computed in double (Burst fast math is not
        // reproducible in managed code; the kernel only ever copies them).
        static List<(float, int)> Buffer(World w, int team, Vector3 pos, float range, System.Random r)
        {
            var l = new List<(float, int)>();
            List<FObj> list = w.Lists[team];
            for (int i = 0; i < list.Count; i++)
            {
                FObj o = list[i];
                double dx = o.Pos.x - pos.x, dy = o.Pos.y - pos.y, dz = o.Pos.z - pos.z;
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d <= range) l.Add(((float)Math.Min(1.0, d / range), i));
            }
            Shuffle(r, l);
            if (r.Next(10) == 0 && l.Count > 0) l.Insert(r.Next(l.Count), (0.5f, list.Count + 3));   // out-of-bounds index: skipped by vanilla
            l.Add((-1f, 0));
            l.Add((0.3f, 1));   // after the terminator: never read
            return l;
        }

        sealed class Counters
        {
            public long Scans, Served, Corrected, Emulated, Stolen, Mism, Pref, PrefServed;
            public readonly long[] Reasons = new long[ScanReason.Count];
        }

        static readonly FinalizeScratch s_Fx = new FinalizeScratch();
        static readonly DirtyLive s_Dl = new DirtyLive();
        static readonly TvState s_Expect = new TvState();

        // ParallelScan.Serve over fake objects (scanner validation, claim-or-wait, merge or emulation, Finalize with
        // the live inputs of the changed entries), then vanilla at the same moment and the comparison.
        static void ServeAndCompare(World w, Scan sc, TvState live, List<int>[] dirty, bool corrections, Counters n, int frame, int k)
        {
            int team = sc.P.SearchTeam;
            ScanJob j = sc.Job;
            int reason = ScanReason.None;
            FObj fr = sc.P.From;
            if (fr != null)
            {
                if (!ScanKernel.SameBits(fr.HasDm ? fr.Health : 0f, j.FromHealth)) reason = ScanReason.Scanner;
                for (int q = 0; q < 4; q++)
                {
                    if ((fr.DmgBy[q] == null ? 0 : fr.DmgBy[q].Id) != j.DmgById[q] || !ScanKernel.SameBits(fr.DmgTime[q], j.DmgByTime[q])) reason = ScanReason.Scanner;
                }
            }
            if (reason == ScanReason.None && Volatile.Read(ref j.Status) == ScanJob.Pending)
            {
                if (ScanKernel.TryClaim(j, ScanJob.ClaimMain)) { ScanKernel.Run(j); n.Stolen++; }
                else { SpinWait spin = default; while (Volatile.Read(ref j.Status) == ScanJob.Pending) spin.SpinOnce(); }
            }
            int st = Volatile.Read(ref j.Status);
            if (reason == ScanReason.None && st != ScanJob.Done) reason = j.Reason != ScanReason.None ? j.Reason : ScanReason.WorkerError;
            bool emu = false;
            var fx = s_Fx;
            int nc = 0; int[] cE = null; float[] cS = null;
            if (reason == ScanReason.None)
            {
                reason = ScanKernel.MergeTypeValues(j, live, commit: false);
                if (reason == ScanReason.CarryValues || reason == ScanReason.CarryOverflow)
                {
                    fx.Ensure(j.NSeq);
                    fx.Tv.CopyFrom(live);
                    reason = ScanKernel.EmulateTypeValues(j, fx.Tv, fx.ETo, fx.EUs, fx.EMod);
                    emu = reason == ScanReason.None;
                }
            }
            if (reason == ScanReason.None)
            {
                var dl = s_Dl;
                dl.Clear();
                if (corrections)
                {
                    foreach (int e in dirty[team])
                    {
                        if (j.SeqPos[e] < 0) continue;
                        FObj t = w.Lists[team][e];
                        dl.Add(e, t.Attacked, fr != null && Ref.IsTargetedByPublic(fr, t), t.HasDm ? t.Health : 0f, t.Gone || t.IsDestroyed);
                    }
                }
                reason = ScanKernel.Finalize(j, emu, dl, fx, 128, out cE, out cS, out nc, out _);
                if (reason == ScanReason.None && cE != j.CandEntry) n.Corrected++;
            }
            var expect = s_Expect;
            if (reason == ScanReason.None)
            {
                if (emu) { expect.CopyFrom(fx.Tv); n.Emulated++; }
                else { expect.CopyFrom(live); ScanKernel.MergeTypeValues(j, expect, commit: true); }
            }

            // vanilla at this moment (mutates the live cache, as the game would)
            var refC = new List<FObj>(24); var refS = new List<float>(24);
            if (sc.Buf != null) Ref.CollectPreFilteredEnemies(w, live, sc.P, sc.Buf, refC, refS, out _);
            else Ref.CollectDetectedEnemies(w, live, sc.P, refC, refS, out _);

            if (reason != ScanReason.None) { n.Reasons[reason]++; return; }
            n.Served++;
            if (sc.Buf != null) n.PrefServed++;
            bool same = nc == refC.Count;
            for (int q = 0; same && q < nc; q++) same = ReferenceEquals(w.Lists[team][cE[q]], refC[q]) && ScanKernel.SameBits(cS[q], refS[q]);
            bool tvSame = expect.From == live.From && expect.Type == live.Type && ScanKernel.SameBits(expect.Damage, live.Damage) && expect.Num == live.Num;
            for (int q = 0; tvSame && q < live.Num; q++)
            {
                tvSame = expect.Ids[q] == live.Ids[q] && ScanKernel.SameBits(expect.To[q], live.To[q]) && ScanKernel.SameBits(expect.Us[q], live.Us[q]) &&
                         ScanKernel.SameBits(expect.Mod[q], live.Mod[q]);
            }
            if (!same || !tvSame)
            {
                n.Mism++;
                if (corrections) Check(false, "frame " + frame + " scan " + k + (sc.Buf != null ? " (prefiltered)" : "") + ": served result differs from vanilla (candidates " +
                                             (same ? "same" : "DIFFERENT, ours " + nc + " vanilla " + refC.Count) + ", type-value cache " + (tvSame ? "same" : "DIFFERENT") + ")");
            }
        }

        static void MarkDirty(World w, List<int>[] dirty, HashSet<int>[] marked, FObj o)
        {
            for (int t2 = 0; t2 < 3; t2++)
            {
                int idx = w.Lists[t2].IndexOf(o);
                if (idx >= 0 && marked[t2].Add(idx)) dirty[t2].Add(idx);
            }
        }

        static FParams NewParams(System.Random r, World w, int team, FObj lastFrom, float lastDamage, int lastType, float[] damages)
        {
            FObj from = null;
            if (r.Next(10) != 0)
            {
                if (lastFrom != null && lastFrom.Team == team && r.Next(2) == 0) from = SameType(r, w, lastFrom);
                else from = RandomOf(r, w, team);
            }
            var p = new FParams
            {
                SearchTeam = team,
                From = from,
                Position = from != null ? from.Pos : new Vector3(0, 0, 0),
                AimDir = new Vector3(1f, 0f, 0f) + new Vector3((float)r.NextDouble() - 0.5f, 0f, (float)r.NextDouble() - 0.5f),
                Range = 60f + (float)r.NextDouble() * 260f,
                Damage = lastFrom != null && r.Next(2) == 0 ? lastDamage : damages[r.Next(damages.Length)],
                DamageType = lastFrom != null && r.Next(2) == 0 ? lastType : r.Next(3),
                IgnoreAir = r.Next(5) == 0, IgnoreGround = r.Next(12) == 0, IgnoreStructures = r.Next(3) == 0, IgnoreUndamageable = r.Next(3) == 0,
            };
            p.AimDir = p.AimDir.normalized;
            if (r.Next(6) == 0) p.CurrentTarget = w.Objs[r.Next(w.Objs.Count)];
            return p;
        }

        static Task[] Launch(List<Scan> list, int start, int threads)
        {
            int ns = list.Count;
            var tasks = new Task[threads];
            for (int jb = 0; jb < threads; jb++)
            {
                int job = jb;
                tasks[jb] = Task.Run(() =>
                {
                    for (int k = start + job; k < ns; k += threads)
                    {
                        ScanJob j = list[k].Job;
                        if (ScanKernel.TryClaim(j, ScanJob.ClaimWorker)) ScanKernel.Run(j);
                    }
                });
            }
            return tasks;
        }

        static long TestFrames(int frames, int seed, bool corrections)
        {
            var r = new System.Random(seed);
            World w = MakeWorld(r, 170);
            var live = new TvState { Damage = float.NaN };
            float[] damages = { 10f, 25f, 40f };
            var n = new Counters();
            var dataByTeam = new[] { new ScanTeamData(), new ScanTeamData(), new ScanTeamData() };
            var groups = new Dictionary<long, int>();
            var groupInfo = new List<(int, float)>();
            var dirty = new List<int>[3]; var marked = new HashSet<int>[3];
            for (int t = 0; t < 3; t++) { dirty[t] = new List<int>(); marked[t] = new HashSet<int>(); }
            int threads = Math.Max(2, Environment.ProcessorCount - 1);

            for (int f = 0; f < frames; f++)
            {
                foreach (FObj o in w.Objs)
                {
                    o.Pos = o.Pos + new Vector3((float)(r.NextDouble() - 0.5) * 4f, 0f, (float)(r.NextDouble() - 0.5) * 4f);
                    if (r.Next(50) == 0) o.Attacked = r.Next(7);
                    if (r.Next(80) == 0) o.Health = Math.Max(0f, o.Health - 40f);
                }
                w.Now += 0.05f;
                for (int q = 0; q < 20; q++) w.DpsKnown[r.Next(w.Types.Length), r.Next(w.Types.Length)] = true;
                if (r.Next(3) == 0) w.EntryKnown[r.Next(w.Types.Length)] = true;

                // ---- loop phase: full scans
                int nScans = 20 + r.Next(40);
                var list = new List<Scan>(nScans + 40);
                FObj lastFrom = null; float lastDamage = 0; int lastType = 1;
                for (int k = 0; k < nScans; k++)
                {
                    FParams p = NewParams(r, w, r.Next(3), lastFrom, lastDamage, lastType, damages);
                    lastFrom = p.From; lastDamage = p.Damage; lastType = p.DamageType;
                    list.Add(new Scan { P = p });
                }
                groups.Clear(); groupInfo.Clear();
                for (int t = 0; t < 3; t++) { Stage(w, t, dataByTeam[t], groups, groupInfo); dirty[t].Clear(); marked[t].Clear(); }
                foreach (Scan sc in list) FillJob(w, sc, dataByTeam[sc.P.SearchTeam], Tables(w, sc.P, groupInfo));
                Task[] tasks = Launch(list, 0, threads);
                for (int k = 0; k < nScans; k++)
                {
                    InLoopChanges(r, w, list[k].P.From, dirty, marked, live, damages);
                    n.Scans++;
                    ServeAndCompare(w, list[k], live, dirty, corrections, n, f, k);
                }
                Task.WaitAll(tasks);

                // ---- finish phase: prefiltered scans on the same snapshot, picks between them
                int start = list.Count;
                int nPref = 10 + r.Next(30);
                for (int k = 0; k < nPref; k++)
                {
                    FParams p = NewParams(r, w, r.Next(3), lastFrom, lastDamage, lastType, damages);
                    lastFrom = p.From; lastDamage = p.Damage; lastType = p.DamageType;
                    var sc = new Scan { P = p, Buf = Buffer(w, p.SearchTeam, p.Position, p.Range, r) };
                    list.Add(sc);
                    FillJob(w, sc, dataByTeam[p.SearchTeam], Tables(w, p, groupInfo));
                }
                tasks = Launch(list, start, threads);
                for (int k = start; k < list.Count; k++)
                {
                    Scan sc = list[k];
                    n.Scans++; n.Pref++;
                    ServeAndCompare(w, sc, live, dirty, corrections, n, f, k);
                    // the finished agent picks a target: AddAttacker on the new, RemoveAttacker on the old one
                    FObj me = sc.P.From;
                    if (me != null && me.IsUnit && r.Next(3) != 0)
                    {
                        List<FObj> l = w.Lists[me.Team];
                        FObj pick = l[r.Next(l.Count)];
                        FObj old = me.CurrentTarget;
                        if (old != pick)
                        {
                            if (old != null) { old.Attacked = Math.Max(0, old.Attacked - 1); MarkDirty(w, dirty, marked, old); }
                            pick.Attacked++;
                            MarkDirty(w, dirty, marked, pick);
                            me.CurrentTarget = pick;
                            MarkDirty(w, dirty, marked, me);
                        }
                    }
                    InLoopChanges(r, w, null, dirty, marked, live, damages);
                }
                Task.WaitAll(tasks);
            }
            Console.Write((corrections ? "3" : "  (control)") + " frames " + frames + ": scans " + n.Scans + " (prefiltered " + n.Pref + "), served " + n.Served +
                          " (" + (100.0 * n.Served / Math.Max(1, n.Scans)).ToString("0") + " %; prefiltered " + n.PrefServed + "), corrected " + n.Corrected +
                          ", type values emulated " + n.Emulated + ", computed by main " + n.Stolen + ", mismatches " + n.Mism + " | not served:");
            for (int i = 1; i < ScanReason.Count; i++) if (n.Reasons[i] > 0) Console.Write(" " + ScanReason.Names[i] + " " + n.Reasons[i]);
            Console.WriteLine();
            if (!corrections) return n.Mism;
            Check(n.Served > n.Scans / 2, "served share too low (" + n.Served + " of " + n.Scans + ")");
            Check(n.Corrected > 0, "no corrected scan: the correction path never ran");
            Check(n.Emulated > 0, "type-value emulation never ran");
            Check(n.PrefServed > 0, "no prefiltered scan served");
            Check(n.Reasons[ScanReason.Unsupported] == 0, "unsupported entries left (no ObjectInfo / turret-heavy structures must be served)");
            return n.Mism;
        }

        // Random changes inside the loop / finish phase, marked dirty the way the game's (change-only) patches do, plus
        // other scorers moving the global type-value cache.
        static void InLoopChanges(System.Random r, World w, FObj scanner, List<int>[] dirty, HashSet<int>[] marked, TvState live, float[] damages)
        {
            int changes = r.Next(4);
            for (int q = 0; q < changes; q++)
            {
                List<FObj> l = w.Lists[r.Next(3)];
                if (l.Count == 0) continue;
                FObj o = l[r.Next(l.Count)];
                int kind = r.Next(40);
                if (kind < 24) o.Attacked = Math.Max(0, o.Attacked + (r.Next(2) == 0 ? 1 : -1));
                else if (kind < 36 && o.IsUnit) o.CurrentTarget = r.Next(2) == 0 ? null : (scanner ?? w.Objs[r.Next(w.Objs.Count)]);
                else if (kind < 39) { if (!(o.Health > 0f)) continue; o.Health = Math.Max(1f, o.Health - 10f); }   // damaged, alive: corrected with the live health
                else o.Health = 0f;                                             // destroyed in the loop: refused if in the sequence
                MarkDirty(w, dirty, marked, o);
            }
            if (r.Next(4) == 0)
            {
                FObj of = RandomOf(r, w, r.Next(3));
                List<FObj> l = w.Lists[r.Next(3)];
                float dmg = damages[r.Next(damages.Length)];
                int dt = r.Next(3);
                for (int q = 0; q < 1 + r.Next(5); q++)
                {
                    FObj t = l[r.Next(l.Count)];
                    if (!t.Gone) Ref.ResolveTargetScoreTypeValues(w, live, of, t, dmg, dt, out _, out _, out _);
                }
            }
        }

        static FObj RandomOf(System.Random r, World w, int team)
        {
            for (int tries = 0; tries < 100; tries++)
            {
                FObj o = w.Objs[r.Next(w.Objs.Count)];
                if (o.Team == team && !o.Gone && !o.IsDestroyed) return o;
            }
            return null;
        }

        static FObj SameType(System.Random r, World w, FObj like)
        {
            for (int tries = 0; tries < 200; tries++)
            {
                FObj o = w.Objs[r.Next(w.Objs.Count)];
                if (o.Team == like.Team && o.Type == like.Type && o.HasOi == like.HasOi && !o.Gone && !o.IsDestroyed) return o;
            }
            return like;
        }
    }
}
#endif
