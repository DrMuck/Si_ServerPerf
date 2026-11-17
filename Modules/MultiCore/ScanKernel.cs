/*
 ParallelScan kernel: the part of the ParallelScan items that runs on worker threads, plus the pure serve-time steps
 (type-value cache merge / emulation, corrections, AddCandidate replay). Compiled into the mod AND into the offline
 test harness (Tests\ParallelScanTests.csproj).

 Rule for this file: no Unity OBJECT and no game type is touched. It uses only Unity's managed value-type math
 (Vector3, Quaternion, Mathf: plain C# in UnityEngine.CoreModule, no native calls, thread-safe), because vanilla's
 arithmetic is written with exactly those methods and the same Mono JIT then produces the same bits on any thread.
 The offline tests compile it against a copy of those three types (Tests\UnityMathShim.cs).

 Two scan modes, one scoring path:
   full (ParallelScan)                TargetFinder.CollectDetectedEnemies (TargetFinder.cs:349-384, 1.0.17): list order,
                                      vanilla's filters, WorldLineOfSightCenter = rot * LineOfSightCenter + pos
                                      (BaseGameObject.cs:262), Vector3.Distance, range test, range01 = d / range.
   prefiltered (ParallelScan_Prefiltered)  TargetFinder.CollectPreFilteredEnemies (:396-433): the entries of the team's
                                      Burst filter buffer (Team.GetBestTargetScores, copied on main) in buffer order,
                                      stop at the first score < 0, index bounds test, vanilla's filters, range01 = the
                                      buffer's score (computed by Burst; copied, never recomputed).
   score          GameAI.CalculateBestTargetScore (GameAI.cs:671-802) + GetSelfDefenceScore (:804-852) +
                  Target.GetWasDamagedByRecently (Target.cs:494-509) + Target.GetIsTargetedBy (Target.cs:365-383),
                  same expression shapes as vanilla (the bits must match)
   type values    GameAI.ResolveTargetScoreTypeValues (:863-900) replayed on a scan-local copy of the 32-entry cache
                  that starts EMPTY ("reset" assumption); entries without ObjectInfo take vanilla's uncached branch.
                  At serve time MergeTypeValues combines that with vanilla's live cache (exact on reset; exact on
                  carry-over when every shared type is bit-identical and the cache does not overflow); when the merge
                  refuses, EmulateTypeValues replays vanilla's cache exactly over the in-range sequence on the main
                  thread and the entries whose values differ are rescored.
   AddCandidate   TargetFinder.AddCandidate (:534-557) with the cached minimum (same slot as vanilla's loop)
   output         the 24 slots AND the whole in-range sequence (entry, score, type values used), so the main thread can
                  rescore entries whose inputs changed after the snapshot and replay AddCandidate (Finalize).
 A scan that meets anything it cannot decide from plain data (an entry vanilla would throw on, a dps pair vanilla has
 not computed yet, a NaN score) is ABORTED with a reason; the main thread then runs the synchronous path.

 Work claiming: a scan is computed exactly once, by whoever claims it first (ScanJob.Claim, Interlocked): a worker
 walking its stripe, or the main thread when vanilla asks for a scan no worker has started yet (it computes it inline,
 cheaper than the synchronous path because it runs on the snapshot).
*/

using System;
using System.Threading;
using UnityEngine;

namespace SiServerPerf.Modules.MultiCore
{
    // Per-entry flags of ScanTeamData.Flags.
    internal static class SF
    {
        public const int Cand = 1;          // non-null, not destroyed, enemy of the search team (vanilla's first three filters)
        public const int StructOrSite = 2;  // (bool)OwnerStructure || (bool)OwnerConstructionSite (IgnoreStructures filter)
        public const int OiOk = 4;          // (bool)ObjectInfo
        public const int DmOk = 8;          // (bool)DamageManager
        public const int UnitOk = 16;       // (bool)OwnerUnit
        public const int Functional = 32;   // IsFunctional
        public const int Flying = 64;       // OwnerUnit.IsFlying
        public const int Critical = 128;    // ObjectInfo.Critical
        public const int Structure = 256;   // (bool)OwnerStructure (GetIsTargetedBy structure branch)
        public const int Unsupported = 512; // cannot be scored from plain data (> 255 turret targets): abort if reached
        public const int Poison = 1024;     // vanilla would throw (full) / take an unknown branch (prefiltered): listed Target without Owner
    }

    // Snapshot of one search team's Team.DetectedTargets at the staging point. Main writes it before the jobs are
    // scheduled; workers only read it; main rewrites it only after every job reading it is taken back.
    internal sealed class ScanTeamData
    {
        public int N;
        public int Cap;
        public int[] Flags = new int[0];
        public Vector3[] Pos = new Vector3[0];       // Target.transform position (GetPositionAndRotation)
        public Quaternion[] Rot = new Quaternion[0];
        public Vector3[] LosC = new Vector3[0];      // ObjectInfo.LineOfSightCenter (zero without ObjectInfo)
        public float[] TopSpeed = new float[0];      // OwnerUnit.TopSpeed (0 without unit)
        public float[] Health = new float[0];        // DamageManager.Health (0 without)
        public float[] PhysR = new float[0];         // BaseGameObject.PhysicalRadius (ObjectInfo ? PhysicalRadius : 1)
        public float[] EngR = new float[0];          // ObjectInfo ? EngagementRange : 0
        public int[] OiScore = new int[0];           // ObjectInfo.Score
        public int[] Attacked = new int[0];          // Target.AttackedByEnemyNum
        public int[] Ct = new int[0];                // 4 per entry: instance ids of the unit's CurrentTarget / the structure's turret targets
        public int[] CtN = new int[0];               // count; > 4 = all of them in CtPool from CtOff
        public int[] CtOff = new int[0];
        public int[] CtPool = new int[64];
        public int CtPoolN;
        public int[] Id = new int[0];                // Target instance id (jitter, CurrentTarget compares)
        public int[] OiId = new int[0];              // ObjectInfo instance id (type-value cache key), 0 without
        public int[] TypeSlot = new int[0];          // index into ScanTables.DpsTo / DpsUs (ObjectInfo entries)
        public int[] GroupSlot = new int[0];         // index into ScanTables.Mod (-1 without DamageManager)

        public void Grow(int n)
        {
            if (n <= Cap) return;
            int c = TeamMapKernel.NextPow2(Math.Max(n, 64));
            Array.Resize(ref Flags, c); Array.Resize(ref Pos, c); Array.Resize(ref Rot, c); Array.Resize(ref LosC, c);
            Array.Resize(ref TopSpeed, c); Array.Resize(ref Health, c); Array.Resize(ref PhysR, c); Array.Resize(ref EngR, c);
            Array.Resize(ref OiScore, c); Array.Resize(ref Attacked, c); Array.Resize(ref Ct, c * 4); Array.Resize(ref CtN, c);
            Array.Resize(ref CtOff, c); Array.Resize(ref Id, c); Array.Resize(ref OiId, c); Array.Resize(ref TypeSlot, c);
            Array.Resize(ref GroupSlot, c);
            Cap = c;
        }

        // Main, staging: the current-target ids of entry i.
        public void SetTargets(int i, int[] ids, int m)
        {
            CtN[i] = m;
            if (m <= 4)
            {
                for (int k = 0; k < m; k++) Ct[i * 4 + k] = ids[k];
                return;
            }
            if (CtPoolN + m > CtPool.Length) Array.Resize(ref CtPool, TeamMapKernel.NextPow2(CtPoolN + m));
            CtOff[i] = CtPoolN;
            Array.Copy(ids, 0, CtPool, CtPoolN, m);
            CtPoolN += m;
        }
    }

    // Type values for one (scan key, team): DamageCalculator dps per target type (NaN = vanilla has not computed that
    // pair yet -> abort), DamageManager.AdjustDamage(damage, type, false) per (ObjectInfo, size) group or per instance
    // (entries without ObjectInfo), and whether vanilla's DamageCalculator already holds the scanner type's entry
    // (vanilla creates it on a dps call for an entry without ObjectInfo; we must not skip a creation).
    internal sealed class ScanTables
    {
        public float[] DpsTo = new float[64];
        public float[] DpsUs = new float[64];
        public float[] Mod = new float[64];
        public bool FromEntryMissing;

        public void Grow(int types, int groups)
        {
            if (DpsTo.Length < types) { int c = TeamMapKernel.NextPow2(types); Array.Resize(ref DpsTo, c); Array.Resize(ref DpsUs, c); }
            if (Mod.Length < groups) Array.Resize(ref Mod, TeamMapKernel.NextPow2(groups));
        }
    }

    // The 25 GameAI.s_Cfg_* values (read on main at the staging point, compared again at serve) + the jitter function
    // (vanilla's own static GameAI.GetTargetScoreJitter01 in the game: pure int math, safe on any thread).
    internal sealed class ScoreCfg
    {
        public float Aim, Already, Crit, DpsTo, DpsUs, DmgHealth, DmgUs, Dist, Func, Jitter, Sat, SatSec, Self,
                     SelfContact, SelfThreat, SelfKill, SelfMem, SelfClose, Speed, Unit, WeAre, View, BaseDps, BaseSpeed, Worth;
        public Func<int, int, float> JitterFn;

        public const int Count = 25;

        public void Set(int i, float v)
        {
            switch (i)
            {
                case 0: Aim = v; break; case 1: Already = v; break; case 2: Crit = v; break; case 3: DpsTo = v; break;
                case 4: DpsUs = v; break; case 5: DmgHealth = v; break; case 6: DmgUs = v; break; case 7: Dist = v; break;
                case 8: Func = v; break; case 9: Jitter = v; break; case 10: Sat = v; break; case 11: SatSec = v; break;
                case 12: Self = v; break; case 13: SelfContact = v; break; case 14: SelfThreat = v; break; case 15: SelfKill = v; break;
                case 16: SelfMem = v; break; case 17: SelfClose = v; break; case 18: Speed = v; break; case 19: Unit = v; break;
                case 20: WeAre = v; break; case 21: View = v; break; case 22: BaseDps = v; break; case 23: BaseSpeed = v; break;
                default: Worth = v; break;
            }
        }

        public float Get(int i)
        {
            switch (i)
            {
                case 0: return Aim; case 1: return Already; case 2: return Crit; case 3: return DpsTo; case 4: return DpsUs;
                case 5: return DmgHealth; case 6: return DmgUs; case 7: return Dist; case 8: return Func; case 9: return Jitter;
                case 10: return Sat; case 11: return SatSec; case 12: return Self; case 13: return SelfContact; case 14: return SelfThreat;
                case 15: return SelfKill; case 16: return SelfMem; case 17: return SelfClose; case 18: return Speed; case 19: return Unit;
                case 20: return WeAre; case 21: return View; case 22: return BaseDps; case 23: return BaseSpeed; default: return Worth;
            }
        }
    }

    // Abort / fallback reasons (counters in the status line; index = code).
    internal static class ScanReason
    {
        public const int None = 0;
        public const int Unsupported = 1;     // worker: entry it cannot score (> 255 turret targets)
        public const int DpsUnknown = 2;      // worker / emulation: DamageCalculator pair or entry not computed by vanilla yet
        public const int NaN = 3;             // worker: NaN score (vanilla logs a warning; let it)
        public const int WorkerError = 4;     // worker: exception
        public const int NotReady = 5;        // serve: a worker is still on that scan after the wait budget
        public const int Params = 6;          // serve: live TargetScanParams differ from the dry run's
        public const int Scanner = 7;         // serve: scanner-side input changed (health, damage memory, position, unshootable)
        public const int ListChanged = 8;     // serve: Team.DetectedTargets mutated since the snapshot
        public const int Cfg = 9;             // serve: GameAI score config changed / unresolved
        public const int CarryValues = 10;    // (kept for the counters' layout; the emulation now serves these)
        public const int CarryOverflow = 11;  // (as above)
        public const int HealthDirty = 12;    // serve: an in-sequence entry was destroyed after the snapshot
        public const int TooManyDirty = 13;   // serve: more entries to rescore than the budget (ParallelScanMaxDirty)
        public const int CorrectNaN = 14;     // serve: corrected score NaN
        public const int Lists = 15;          // serve: candidate lists not empty (not called from RequestScan)
        public const int Poison = 16;         // worker: listed Target without Owner reached
        public const int Buffer = 17;         // serve (prefiltered): the team's Burst filter buffer was rewritten since the copy
        public const int Count = 18;

        public static readonly string[] Names =
        {
            "none", "unsupported", "dpsUnknown", "nan", "workerError", "notReady", "params", "scanner", "listChanged", "cfg",
            "carryValues", "carryOverflow", "destroyedInLoop", "tooManyDirty", "correctNaN", "lists", "poison", "buffer"
        };
    }

    // One predicted scan: inputs written by main before the hand-off, outputs written by whoever claimed it, Status
    // published last (Volatile.Write) so a main-thread reader that sees Status == Done also sees the outputs.
    internal sealed class ScanJob
    {
        public const int Pending = 0, Done = 1, Aborted = 2, Error = -1;
        public const int ClaimNone = 0, ClaimWorker = 1, ClaimMain = 2;

        // ---- inputs (main)
        public ScanTeamData Team;
        public ScanTables Tables;
        public ScoreCfg Cfg;
        public bool Pref;                             // prefiltered mode
        public int PN;                                // prefiltered: buffer entries before the first score < 0
        public int[] PIdx = new int[64];
        public float[] PScore = new float[64];
        public Vector3 Position, AimDir;
        public float Range, Damage;
        public int DamageType;
        public bool DamageTypeNone, Melee;            // EDamageType.None / Bite, Slash, Stab, Collision
        public bool IgnoreAir, IgnoreGround, IgnoreStructures, IgnoreUndamageable;
        public int CurrentTargetId;                   // scanParams.CurrentTarget ((object) null -> 0)
        public bool FromOk;                           // (bool)scanParams.LineOfSightFromTarget
        public bool FromOiOk;                         // (bool)fromTarget.ObjectInfo
        public int FromId, FromOiId;
        public Vector3 FromPos;                       // fromTarget.transform.position
        public float FromPhysR, FromHealth, Now;      // Now = Time.time
        public readonly int[] DmgById = new int[4];   // fromTarget.GetDamagedBy(i): instance id ((object) null -> 0) and time
        public readonly float[] DmgByTime = new float[4];

        // ---- outputs
        public int Claim;
        public int Status;
        public int Reason;
        public Exception Error_;
        public long WorkTicks;
        public int ThreadId;                          // managed thread that ran it
        public int NSeq;
        public int[] SeqEntry = new int[64];
        public float[] SeqScore = new float[64], SeqTo = new float[64], SeqUs = new float[64], SeqMod = new float[64], SeqR01 = new float[64];
        public int[] SeqPos = new int[64];            // entry -> index in Seq (-1 = not in the sequence)
        public int NCand;
        public readonly int[] CandEntry = new int[24];
        public readonly float[] CandScore = new float[24];
        public int TvNum;                             // scan-local type-value cache (starts empty)
        public bool TvOverflow;                       // more than 32 distinct ObjectInfo types met
        public readonly int[] TvId = new int[32];
        public readonly float[] TvTo = new float[32], TvUs = new float[32], TvMod = new float[32];

        // Main, before the hand-off: outputs sized so the worker never allocates.
        public void EnsureOutputs(int n)
        {
            int need = Math.Max(n, Pref ? PN : 0);
            if (SeqEntry.Length < need)
            {
                int c = TeamMapKernel.NextPow2(need);
                SeqEntry = new int[c]; SeqScore = new float[c]; SeqTo = new float[c]; SeqUs = new float[c]; SeqMod = new float[c]; SeqR01 = new float[c];
            }
            if (SeqPos.Length < n) SeqPos = new int[TeamMapKernel.NextPow2(n)];
        }

        public void EnsurePref(int n)
        {
            if (PIdx.Length < n) { int c = TeamMapKernel.NextPow2(n); PIdx = new int[c]; PScore = new float[c]; }
        }
    }

    // Vanilla GameAI type-value cache state (the four statics + the four 32-entry arrays). In the game the arrays ARE
    // vanilla's static arrays and the scalars are copied in/out by the glue.
    internal sealed class TvState
    {
        public int From;
        public int Type;
        public float Damage;
        public int Num;
        public int[] Ids = new int[32];
        public float[] To = new float[32], Us = new float[32], Mod = new float[32];

        public void CopyFrom(TvState o)
        {
            From = o.From; Type = o.Type; Damage = o.Damage; Num = o.Num;
            Array.Copy(o.Ids, Ids, 32); Array.Copy(o.To, To, 32); Array.Copy(o.Us, Us, 32); Array.Copy(o.Mod, Mod, 32);
        }
    }

    // Main-thread scratch for Finalize (one instance, reused).
    internal sealed class FinalizeScratch
    {
        public float[] Corr = new float[64];
        public int[] Mark = new int[64];         // seq -> dirty index + 1 (0 = not marked), stamp-free: cleared after use
        public int[] Marked = new int[64];
        public float[] ETo = new float[64], EUs = new float[64], EMod = new float[64];   // emulated per-seq type values
        public readonly int[] OutC = new int[24];
        public readonly float[] OutS = new float[24];
        public readonly TvState Tv = new TvState();

        public void Ensure(int n)
        {
            if (Corr.Length >= n) return;
            int c = TeamMapKernel.NextPow2(n);
            Corr = new float[c]; Mark = new int[c]; Marked = new int[c]; ETo = new float[c]; EUs = new float[c]; EMod = new float[c];
        }
    }

    // Live values of the entries that changed after the snapshot (main thread fills them for the entries of one scan).
    internal sealed class DirtyLive
    {
        public int N;
        public int[] Entry = new int[16];
        public int[] Attacked = new int[16];
        public bool[] TargetedBy = new bool[16];
        public float[] Health = new float[16];
        public bool[] Destroyed = new bool[16];

        public void Clear() => N = 0;

        public void Add(int entry, int attacked, bool targetedBy, float health, bool destroyed)
        {
            if (N == Entry.Length)
            {
                int c = N * 2;
                Array.Resize(ref Entry, c); Array.Resize(ref Attacked, c); Array.Resize(ref TargetedBy, c);
                Array.Resize(ref Health, c); Array.Resize(ref Destroyed, c);
            }
            Entry[N] = entry; Attacked[N] = attacked; TargetedBy[N] = targetedBy; Health[N] = health; Destroyed[N] = destroyed;
            N++;
        }
    }

    internal static class ScanKernel
    {
        // ------------------------------------------------------------------ claiming

        public static bool TryClaim(ScanJob j, int who) => Interlocked.CompareExchange(ref j.Claim, who, ScanJob.ClaimNone) == ScanJob.ClaimNone;

        // ------------------------------------------------------------------ worker

        // Never throws (exception -> Status Error). Publishes Status last. The caller has claimed the scan.
        public static void Run(ScanJob j)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int status = ScanJob.Done;
            try
            {
                int r = Compute(j);
                if (r != ScanReason.None) { j.Reason = r; status = ScanJob.Aborted; }
            }
            catch (Exception ex)
            {
                j.Error_ = ex;
                j.Reason = ScanReason.WorkerError;
                status = ScanJob.Error;
            }
            j.WorkTicks = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            j.ThreadId = Thread.CurrentThread.ManagedThreadId;
            Volatile.Write(ref j.Status, status);
        }

        internal static int Compute(ScanJob j)
        {
            ScanTeamData t = j.Team;
            int n = t.N;
            int[] seqPos = j.SeqPos;
            for (int i = 0; i < n; i++) seqPos[i] = -1;
            j.NSeq = 0;
            j.NCand = 0;
            j.TvNum = 0;
            j.TvOverflow = false;
            j.Reason = ScanReason.None;
            int minIdx = -1;
            float minVal = 0f;
            Vector3 p = j.Position;
            bool ignoreStructures = j.IgnoreStructures;
            int count = j.Pref ? j.PN : n;
            for (int q = 0; q < count; q++)
            {
                int i;
                float range01;
                Vector3 w;
                if (j.Pref)
                {
                    i = j.PIdx[q];
                    if (i < 0 || n <= i) continue;                 // `index < 0 || DetectedTargets.Count <= index`
                    int f0 = t.Flags[i];
                    if ((f0 & SF.Poison) != 0) return ScanReason.Poison;
                    if ((f0 & SF.Cand) == 0) continue;
                    if (ignoreStructures && (f0 & SF.StructOrSite) != 0) continue;
                    w = t.Rot[i] * t.LosC[i] + t.Pos[i];               // Owner is truthy: WorldLineOfSightCenter
                    range01 = j.PScore[q];
                }
                else
                {
                    i = q;
                    int f0 = t.Flags[i];
                    if ((f0 & SF.Cand) == 0)
                    {
                        if ((f0 & SF.Poison) != 0) return ScanReason.Poison;
                        continue;
                    }
                    if (ignoreStructures && (f0 & SF.StructOrSite) != 0) continue;
                    w = t.Rot[i] * t.LosC[i] + t.Pos[i];
                    float d = Vector3.Distance(w, p);
                    if (d > j.Range) continue;
                    range01 = d / j.Range;
                }
                int f = t.Flags[i];
                if ((f & SF.Unsupported) != 0) return ScanReason.Unsupported;
                int r = TypeValues(j, i, f, out float to, out float us, out float mod);
                if (r != ScanReason.None) return r;
                float dot = Vector3.Dot((w - p).normalized, j.AimDir);
                float s = Score(j, i, f, range01, dot, to, us, mod, t.Attacked[i], TargetedBy(j, i, f), t.Health[i]);
                int k = j.NSeq++;
                j.SeqEntry[k] = i; j.SeqScore[k] = s; j.SeqTo[k] = to; j.SeqUs[k] = us; j.SeqMod[k] = mod; j.SeqR01[k] = range01;
                seqPos[i] = k;
                if (!(s > 0f))
                {
                    if (float.IsNaN(s)) return ScanReason.NaN;
                    continue;
                }
                AddCandidate(j.CandEntry, j.CandScore, ref j.NCand, i, s, ref minIdx, ref minVal);
            }
            return ScanReason.None;
        }

        // The aim dot of sequence element k, recomputed from the snapshot exactly as Compute did (range01 is stored).
        static float Dot(ScanJob j, int k)
        {
            ScanTeamData t = j.Team;
            int i = j.SeqEntry[k];
            Vector3 p = j.Position;
            Vector3 w = t.Rot[i] * t.LosC[i] + t.Pos[i];
            return Vector3.Dot((w - p).normalized, j.AimDir);
        }

        // Score of sequence element k with explicit inputs (main thread, corrections).
        public static float Rescore(ScanJob j, int k, float to, float us, float mod, int attacked, bool targetedBy, float health)
        {
            int i = j.SeqEntry[k];
            return Score(j, i, j.Team.Flags[i], j.SeqR01[k], Dot(j, k), to, us, mod, attacked, targetedBy, health);
        }

        // GameAI.ResolveTargetScoreTypeValues on the scan-local cache (starts empty) + CalculateTargetScoreTypeValues
        // from the tables; entries without ObjectInfo take vanilla's uncached branch.
        static int TypeValues(ScanJob j, int i, int f, out float to, out float us, out float mod)
        {
            if ((f & SF.OiOk) == 0) return Calc(j, i, f, out to, out us, out mod);
            ScanTeamData t = j.Team;
            int id = t.OiId[i];
            int num = j.TvNum;
            int[] ids = j.TvId;
            for (int q = 0; q < num; q++)
            {
                if (ids[q] == id)
                {
                    to = j.TvTo[q]; us = j.TvUs[q]; mod = j.TvMod[q];
                    return ScanReason.None;
                }
            }
            int r = Calc(j, i, f, out to, out us, out mod);
            if (r != ScanReason.None) return r;
            if (num < 32)
            {
                ids[num] = id; j.TvTo[num] = to; j.TvUs[num] = us; j.TvMod[num] = mod;
                j.TvNum = num + 1;
            }
            else
            {
                j.TvOverflow = true;
            }
            return ScanReason.None;
        }

        // GameAI.CalculateTargetScoreTypeValues from plain data (GameAI.cs:902-915; DamageCalculator.cs:29-48).
        static int Calc(ScanJob j, int i, int f, out float to, out float us, out float mod)
        {
            ScanTeamData t = j.Team;
            ScanTables tb = j.Tables;
            if (!j.FromOk)
            {
                to = 0f;
                us = 0f;
            }
            else if ((f & SF.OiOk) != 0)
            {
                int slot = t.TypeSlot[i];
                to = tb.DpsTo[slot];
                us = tb.DpsUs[slot];
                if (float.IsNaN(to) || float.IsNaN(us)) { mod = 0f; return ScanReason.DpsUnknown; }
            }
            else
            {
                // target without ObjectInfo: both GetMaxDamagePerSecondToObject calls return 0; the first one creates the
                // scanner type's DamageCalculatorEntry when it is missing (a side effect we would skip): refuse then
                to = 0f;
                us = 0f;
                if (j.FromOiOk && tb.FromEntryMissing) { mod = 0f; return ScanReason.DpsUnknown; }
            }
            if (!j.DamageTypeNone && (f & SF.DmOk) != 0)
            {
                mod = tb.Mod[t.GroupSlot[i]];
                if (float.IsNaN(mod)) return ScanReason.DpsUnknown;
            }
            else
            {
                mod = 0f;
            }
            return ScanReason.None;
        }

        // fromTarget.GetIsTargetedBy(target) (Target.cs:365-383) from the staged current-target ids.
        public static bool TargetedBy(ScanJob j, int i, int f)
        {
            if (!j.FromOk) return false;   // vanilla only evaluates it when (bool)fromTarget
            if ((f & SF.Functional) == 0) return false;
            ScanTeamData t = j.Team;
            int n = t.CtN[i];
            if ((f & SF.UnitOk) != 0)
            {
                return n > 0 && t.Ct[i * 4] == j.FromId;
            }
            if ((f & SF.Structure) != 0)
            {
                if (n <= 4)
                {
                    for (int k = 0; k < n; k++) if (t.Ct[i * 4 + k] == j.FromId) return true;
                }
                else
                {
                    int o = t.CtOff[i];
                    for (int k = 0; k < n; k++) if (t.CtPool[o + k] == j.FromId) return true;
                }
            }
            return false;
        }

        // GameAI.CalculateBestTargetScore, vanilla's expression shapes, inputs from the snapshot (health explicit so a
        // correction can pass the live value).
        internal static float Score(ScanJob j, int i, int f, float range01, float targetAimToDirDot, float dpsToTarget, float dpsToUs,
                                    float modulatedDamage, int attackedByEnemyNum, bool isTargetedBy, float health)
        {
            ScanTeamData t = j.Team;
            ScoreCfg c = j.Cfg;
            bool alreadyOurTarget = j.CurrentTargetId != 0 && j.CurrentTargetId == t.Id[i];
            bool oiOk = (f & SF.OiOk) != 0;
            bool dmOk = (f & SF.DmOk) != 0;
            bool unitOk = (f & SF.UnitOk) != 0;
            float num = Mathf.Clamp01(1f - range01);
            if (j.Melee)
            {
                num *= num;
            }
            float num2 = num * c.Dist;
            if (alreadyOurTarget)
            {
                num2 += c.Already;
            }
            bool isFunctional = (f & SF.Functional) != 0;
            if (isFunctional)
            {
                num2 += c.Func;
            }
            if (oiOk)
            {
                if ((f & SF.Critical) != 0)
                {
                    num2 += c.Crit;
                }
                num2 += (float)t.OiScore[i] * c.Worth;
            }
            num2 += (targetAimToDirDot * 0.5f + 0.5f) * c.Aim;
            if (unitOk)
            {
                bool flying = (f & SF.Flying) != 0;
                if (j.IgnoreAir && flying)
                {
                    return 0f;
                }
                if (j.IgnoreGround && !flying)
                {
                    return 0f;
                }
                num2 += c.Unit;
                num2 += Mathf.Clamp01(t.TopSpeed[i] / c.BaseSpeed) * c.Speed;
            }
            if (j.FromOk)
            {
                if (isTargetedBy)
                {
                    num2 += c.WeAre;
                }
                float fromHealth = j.FromHealth;
                if (fromHealth > 0f)
                {
                    num2 += Mathf.Clamp01(dpsToUs / fromHealth) * c.DmgUs;
                }
                num2 += Mathf.Clamp01(dpsToUs / c.BaseDps) * c.DpsUs;
                num2 += Mathf.Clamp01(dpsToTarget / c.BaseDps) * c.DpsTo;
                float num3 = 0f;
                if (c.Self > 0f)
                {
                    bool wasDamagedByRecently = WasDamagedByRecently(j, t.Id[i], c.SelfMem);
                    if (isTargetedBy || wasDamagedByRecently)
                    {
                        num3 = SelfDefence(j, i, f, dpsToTarget, dpsToUs, wasDamagedByRecently, closeOnly: false, health);
                    }
                    else if (unitOk && c.SelfClose > 0f)
                    {
                        num3 = SelfDefence(j, i, f, dpsToTarget, dpsToUs, hurtUs: false, closeOnly: true, health);
                    }
                }
                int num4 = attackedByEnemyNum - (alreadyOurTarget ? 1 : 0);
                if (c.Sat > 0f && num4 > 0 && dpsToTarget > 0f && num2 > 0f && dmOk && health > 0f)
                {
                    if (num4 > 4)
                    {
                        num4 = 4;
                    }
                    float num5 = health / ((float)num4 * dpsToTarget);
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
            if (!j.DamageTypeNone)
            {
                if (dmOk)
                {
                    if (modulatedDamage <= 0f && j.IgnoreUndamageable)
                    {
                        return 0f;
                    }
                    num2 += Mathf.Clamp01(modulatedDamage / health) * c.DmgHealth;
                }
                else if (j.IgnoreUndamageable)
                {
                    return 0f;
                }
            }
            if (j.FromOk && c.Jitter > 0f)
            {
                num2 *= 1f + (c.JitterFn(j.FromId, t.Id[i]) * 2f - 1f) * c.Jitter;
            }
            return num2;
        }

        // Target.GetWasDamagedByRecently (attacker is alive here, so `!attacker` is false and `==` is an id compare).
        static bool WasDamagedByRecently(ScanJob j, int attackerId, float withinSeconds)
        {
            float num = j.Now - withinSeconds;
            for (int i = 0; i < 4; i++)
            {
                if (j.DmgById[i] == attackerId)
                {
                    return j.DmgByTime[i] >= num;
                }
            }
            return false;
        }

        // GameAI.GetSelfDefenceScore with snapshot positions. GameMath.Distance(from, to) = `to -= from;
        // Mathf.Sqrt(to.x * to.x + to.y * to.y + to.z * to.z)` (GameMath.cs:492-496).
        static float SelfDefence(ScanJob j, int i, int f, float DPS_toTarget, float DPS_toUs, bool hurtUs, bool closeOnly, float health)
        {
            if (DPS_toTarget <= 0f)
            {
                return 0f;
            }
            ScanTeamData t = j.Team;
            ScoreCfg c = j.Cfg;
            float num = 1f;
            if (!hurtUs)
            {
                Vector3 to = t.Pos[i];
                to -= j.FromPos;
                float num2 = Mathf.Sqrt(to.x * to.x + to.y * to.y + to.z * to.z);
                if (closeOnly && num2 > c.SelfClose + 60f)
                {
                    return 0f;
                }
                float num3 = num2 - j.FromPhysR - t.PhysR[i];
                if (closeOnly)
                {
                    if (num3 >= c.SelfClose)
                    {
                        return 0f;
                    }
                    num = 1f - Mathf.Max(num3, 0f) / c.SelfClose;
                }
                float num4 = ((f & SF.OiOk) != 0 ? t.EngR[i] : 0f);
                float num5 = num3 - num4;
                if (num5 > 0f)
                {
                    float num6 = ((f & SF.UnitOk) != 0 ? t.TopSpeed[i] : 0f);
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
            float fromHealth = j.FromHealth;
            float num8 = ((fromHealth > 0f) ? Mathf.Clamp01(DPS_toUs * c.SelfThreat / fromHealth) : 1f);
            if (hurtUs && num8 < 0.5f)
            {
                num8 = 0.5f;
            }
            float num9 = ((f & SF.DmOk) != 0 ? health : 0f);
            float num10 = ((num9 > 0f) ? Mathf.Clamp01(DPS_toTarget * c.SelfKill / num9) : 1f);
            return c.Self * num * num8 * num10;
        }

        // TargetFinder.AddCandidate with the minimum cached while the 24 slots are unchanged (minIdx < 0 = recompute
        // with vanilla's own loop: first index of the minimum, `!(s >= min)`).
        public static void AddCandidate(int[] cands, float[] scores, ref int count, int entry, float score, ref int minIdx, ref float minVal)
        {
            if (count < 24)
            {
                cands[count] = entry;
                scores[count] = score;
                count++;
                minIdx = -1;
                return;
            }
            if (minIdx < 0)
            {
                int index = 0;
                float num = scores[0];
                for (int i = 1; i < 24; i++)
                {
                    if (!(scores[i] >= num))
                    {
                        num = scores[i];
                        index = i;
                    }
                }
                minIdx = index;
                minVal = num;
            }
            if (!(score <= minVal))
            {
                cands[minIdx] = entry;
                scores[minIdx] = score;
                minIdx = -1;
            }
        }

        // ------------------------------------------------------------------ serve (main thread, pure)

        // Fast merge of the scan-local type-value cache with vanilla's live state. Returns None when the worker's
        // values are what vanilla computes (commit: write the state vanilla leaves behind into s), else CarryValues /
        // CarryOverflow -> use EmulateTypeValues.
        public static int MergeTypeValues(ScanJob j, TvState s, bool commit)
        {
            // vanilla touches the cache only from CalculateBestTargetScore, i.e. once per sequence entry: a scan with no
            // entry leaves the state as it is (no reset either)
            if (j.NSeq == 0) return ScanReason.None;
            bool reset = j.FromOiId != s.From || j.DamageType != s.Type || j.Damage != s.Damage;
            if (reset)
            {
                if (commit)
                {
                    s.From = j.FromOiId;
                    s.Type = j.DamageType;
                    s.Damage = j.Damage;
                    s.Num = j.TvNum;
                    Array.Copy(j.TvId, s.Ids, j.TvNum);
                    Array.Copy(j.TvTo, s.To, j.TvNum);
                    Array.Copy(j.TvUs, s.Us, j.TvNum);
                    Array.Copy(j.TvMod, s.Mod, j.TvNum);
                }
                return ScanReason.None;
            }
            if (j.TvOverflow) return ScanReason.CarryOverflow;
            int sNum = s.Num;
            int fresh = 0;
            for (int k = 0; k < j.TvNum; k++)
            {
                int id = j.TvId[k];
                int q = Find(s.Ids, sNum, id);
                if (q >= 0)
                {
                    if (!SameBits(s.To[q], j.TvTo[k]) || !SameBits(s.Us[q], j.TvUs[k]) || !SameBits(s.Mod[q], j.TvMod[k]))
                    {
                        return ScanReason.CarryValues;
                    }
                }
                else
                {
                    fresh++;
                }
            }
            if (sNum + fresh > 32) return ScanReason.CarryOverflow;
            if (commit && fresh > 0)
            {
                int w = sNum;
                for (int k = 0; k < j.TvNum; k++)
                {
                    int id = j.TvId[k];
                    if (Find(s.Ids, sNum, id) >= 0) continue;
                    s.Ids[w] = id; s.To[w] = j.TvTo[k]; s.Us[w] = j.TvUs[k]; s.Mod[w] = j.TvMod[k];
                    w++;
                }
                s.Num = w;
            }
            return ScanReason.None;
        }

        // Vanilla's ResolveTargetScoreTypeValues replayed on s (mutated into the state vanilla leaves) over the scan's
        // sequence, writing the values vanilla uses for every element into to / us / mod. Per-instance values come
        // from the tables (the same values the worker used). Returns DpsUnknown when vanilla would compute a pair we
        // do not have.
        public static int EmulateTypeValues(ScanJob j, TvState s, float[] to, float[] us, float[] mod)
        {
            if (j.NSeq == 0) return ScanReason.None;
            if (j.FromOiId != s.From || j.DamageType != s.Type || j.Damage != s.Damage)
            {
                s.From = j.FromOiId;
                s.Type = j.DamageType;
                s.Damage = j.Damage;
                s.Num = 0;
            }
            ScanTeamData t = j.Team;
            for (int k = 0; k < j.NSeq; k++)
            {
                int i = j.SeqEntry[k];
                int f = t.Flags[i];
                if ((f & SF.OiOk) != 0)
                {
                    int q = Find(s.Ids, s.Num, t.OiId[i]);
                    if (q >= 0)
                    {
                        to[k] = s.To[q]; us[k] = s.Us[q]; mod[k] = s.Mod[q];
                        continue;
                    }
                    int r = Calc(j, i, f, out float a, out float b, out float m);
                    if (r != ScanReason.None) return r;
                    to[k] = a; us[k] = b; mod[k] = m;
                    if (s.Num < 32)
                    {
                        int w = s.Num++;
                        s.Ids[w] = t.OiId[i]; s.To[w] = a; s.Us[w] = b; s.Mod[w] = m;
                    }
                }
                else
                {
                    int r = Calc(j, i, f, out float a, out float b, out float m);
                    if (r != ScanReason.None) return r;
                    to[k] = a; us[k] = b; mod[k] = m;
                }
            }
            return ScanReason.None;
        }

        static int Find(int[] ids, int n, int id)
        {
            for (int q = 0; q < n; q++) if (ids[q] == id) return q;
            return -1;
        }

        public static bool SameBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

        // The served result. tvOverride: per-sequence type values vanilla uses (from EmulateTypeValues) or null = the
        // worker's. live: the changed entries' live inputs (entries not in the sequence are ignored). Entries whose
        // inputs are unchanged keep the worker's score; the rest are rescored; AddCandidate is replayed when any score
        // changed. Returns a ScanReason; nOut / outC / outS hold the 24 slots, rescored = entries recomputed.
        public static int Finalize(ScanJob j, bool tvOverride, DirtyLive live, FinalizeScratch x, int maxRescore,
                                   out int[] outC, out float[] outS, out int nOut, out int rescored)
        {
            outC = j.CandEntry; outS = j.CandScore; nOut = j.NCand; rescored = 0;
            x.Ensure(j.NSeq);
            int nMarked = 0;
            int[] mark = x.Mark;
            int result = ScanReason.None;
            ScanTeamData t = j.Team;
            if (tvOverride)
            {
                for (int k = 0; k < j.NSeq; k++)
                {
                    if (!SameBits(x.ETo[k], j.SeqTo[k]) || !SameBits(x.EUs[k], j.SeqUs[k]) || !SameBits(x.EMod[k], j.SeqMod[k]))
                    {
                        mark[k] = -1;   // marked, not dirty
                        x.Marked[nMarked++] = k;
                    }
                }
            }
            if (live != null)
            {
                for (int d = 0; d < live.N; d++)
                {
                    int e = live.Entry[d];
                    int k = j.SeqPos[e];
                    if (k < 0) continue;
                    if (live.Destroyed[d]) { result = ScanReason.HealthDirty; goto done; }
                    int f = t.Flags[e];
                    if (live.Attacked[d] == t.Attacked[e] && live.TargetedBy[d] == TargetedBy(j, e, f) && SameBits(live.Health[d], t.Health[e])) continue;
                    if (mark[k] == 0) x.Marked[nMarked++] = k;
                    mark[k] = d + 1;
                }
            }
            if (nMarked == 0) goto done;
            if (nMarked > maxRescore) { result = ScanReason.TooManyDirty; goto done; }
            float[] corr = null;
            for (int q = 0; q < nMarked; q++)
            {
                int k = x.Marked[q];
                int e = j.SeqEntry[k];
                int f = t.Flags[e];
                int m = mark[k];
                int attacked = m > 0 ? live.Attacked[m - 1] : t.Attacked[e];
                bool tb = m > 0 ? live.TargetedBy[m - 1] : TargetedBy(j, e, f);
                float health = m > 0 ? live.Health[m - 1] : t.Health[e];
                float a = tvOverride ? x.ETo[k] : j.SeqTo[k], b = tvOverride ? x.EUs[k] : j.SeqUs[k], md = tvOverride ? x.EMod[k] : j.SeqMod[k];
                float s = Rescore(j, k, a, b, md, attacked, tb, health);
                rescored++;
                if (SameBits(s, j.SeqScore[k])) continue;
                if (corr == null)
                {
                    corr = x.Corr;
                    Array.Copy(j.SeqScore, corr, j.NSeq);
                }
                corr[k] = s;
            }
            if (corr != null)
            {
                int n = Replay(j, corr, x.OutC, x.OutS);
                if (n < 0) { result = ScanReason.CorrectNaN; goto done; }
                outC = x.OutC; outS = x.OutS; nOut = n;
            }
        done:
            for (int q = 0; q < nMarked; q++) mark[x.Marked[q]] = 0;
            if (result != ScanReason.None) { outC = j.CandEntry; outS = j.CandScore; nOut = j.NCand; }
            return result;
        }

        // AddCandidate replayed over the sequence with corrected scores (score[k] for seq k). Writes the 24 slots into
        // cands / scores, returns the count, -1 on a NaN score.
        public static int Replay(ScanJob j, float[] score, int[] cands, float[] scores)
        {
            int count = 0, minIdx = -1;
            float minVal = 0f;
            for (int k = 0; k < j.NSeq; k++)
            {
                float s = score[k];
                if (!(s > 0f))
                {
                    if (float.IsNaN(s)) return -1;
                    continue;
                }
                AddCandidate(cands, scores, ref count, j.SeqEntry[k], s, ref minIdx, ref minVal);
            }
            return count;
        }
    }
}
