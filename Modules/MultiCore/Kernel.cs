/*
 MultiCore kernel: the Unity-free part of the module. Compiled into the mod AND into the offline test harness
 (Tests\MultiCoreTests.csproj), so nothing in this file may reference UnityEngine, MelonLoader, Harmony or the game.

   TeamMapKernel   Stage (main thread, snapshot) and Fill (worker, compute) of the per-team detected-target index map
   FillSlot        one hand-off unit: staging arrays + the map writer + the ownership flag + the worker's result
   FillPipeline    slot table, ownership hand-off (main -> worker -> main), the worker entry point, counters
   FillStats       counters and the status line (rates per second)
   Sampler         xorshift sampler for the self-check (never UnityEngine.Random: that would change gameplay)

 Ownership rule (asserted, not just documented): a slot is owned by exactly one side.
   main  -> HandOff()  -> worker      (main must not touch Keys / Vals / N / Map / the target map until TakeBack)
   worker -> RunWorker() writes the target map and Status / Error / WorkTicks, nothing else
   main  <- TakeBack() <- (after the backend's Complete: Unity JobHandle.Complete in the game, Task.Wait in tests)
 The ownership word is an Interlocked flag; a double hand-off, a take-back without hand-off, or a worker running on a
 main-owned slot throws InvalidOperationException (in the game: Item.Fail -> vanilla). The memory barrier between the
 two sides is the scheduler's own (Unity's job queue / Task), plus the Interlocked flag.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace SiServerPerf.Modules.MultiCore
{
    // Write side of an int -> int hash map (NativeParallelHashMap<int,int> in the game, a Dictionary in the tests).
    internal interface IIntMapWriter
    {
        // Vanilla indexer semantics: insert, or overwrite the value of an existing key.
        void Set(int key, int value);
    }

    // Instance id of a dictionary key (Target.GetInstanceID() in the game).
    internal interface IInstanceIds<T>
    {
        int Id(T obj);
    }

    internal static class TeamMapKernel
    {
        // MAIN THREAD. Snapshot of what TeamJobNativeContainers.Init feeds to the hash map: (id(key), value) of every
        // dictionary entry, in enumeration order. Returns the entry count. Arrays grow to a power of two.
        public static int Stage<TKey, TIds>(Dictionary<TKey, int> dict, ref TIds ids, ref int[] keys, ref int[] vals)
            where TIds : struct, IInstanceIds<TKey>
        {
            int n = dict.Count;
            if (keys.Length < n || vals.Length < n)
            {
                int cap = NextPow2(n);
                keys = new int[cap];
                vals = new int[cap];
            }
            int k = 0;
            foreach (KeyValuePair<TKey, int> kv in dict)
            {
                keys[k] = ids.Id(kv.Key);
                vals[k] = kv.Value;
                k++;
            }
            return k;
        }

        // WORKER. Replays the staged sequence into the map with the vanilla write, in the same order: same final
        // content (last write wins for duplicate ids, e.g. 0 for destroyed targets) and the same insertion sequence.
        public static void Fill<TMap>(ref TMap map, int[] keys, int[] vals, int n) where TMap : struct, IIntMapWriter
        {
            for (int k = 0; k < n; k++)
            {
                map.Set(keys[k], vals[k]);
            }
        }

        public static int NextPow2(int v)
        {
            if (v <= 64) return 64;
            v--;
            v |= v >> 1; v |= v >> 2; v |= v >> 4; v |= v >> 8; v |= v >> 16;
            return v + 1;
        }
    }

    internal sealed class FillSlot<TMap> where TMap : struct, IIntMapWriter
    {
        public readonly int Index;
        public object Owner;              // the container this slot serves (TeamJobNativeContainers in the game)

        // ---- main-owned between TakeBack and HandOff, worker-read between HandOff and TakeBack
        public int[] Keys = new int[64];
        public int[] Vals = new int[64];
        public int N;
        public TMap Map;

        // ---- written by the worker, read by the main thread after TakeBack
        public int Status;                // 0 = body never ran, 1 = done, -1 = exception
        public Exception Error;
        public long WorkTicks;            // Stopwatch ticks spent in the worker body

        public int HandOffFrame = -1;
        internal int OwnerFlag;           // 0 = main, 1 = worker

        public FillSlot(int index) { Index = index; }

        public bool Pending => Volatile.Read(ref OwnerFlag) == 1;
    }

    internal sealed class FillPipeline<TMap> where TMap : struct, IIntMapWriter
    {
        FillSlot<TMap>[] _slots = new FillSlot<TMap>[8];
        int _count;
        int _pending;                     // slots currently handed off (main thread only)

        public readonly FillStats Stats = new FillStats();
        public int Count => _count;
        public int PendingCount => _pending;
        public FillSlot<TMap> this[int i] => _slots[i];

        // MAIN. Linear search by reference (a handful of teams).
        public FillSlot<TMap> Find(object owner)
        {
            for (int i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_slots[i].Owner, owner)) return _slots[i];
            }
            return null;
        }

        public FillSlot<TMap> GetOrAdd(object owner)
        {
            FillSlot<TMap> s = Find(owner);
            if (s != null) return s;
            if (_count == _slots.Length) Array.Resize(ref _slots, _slots.Length * 2);   // old array stays valid for workers
            s = new FillSlot<TMap>(_count) { Owner = owner };
            _slots[_count++] = s;
            return s;
        }

        public static void AssertMain(FillSlot<TMap> s)
        {
            if (Volatile.Read(ref s.OwnerFlag) != 0) throw new InvalidOperationException("MultiCore: slot " + s.Index + " is owned by a worker");
        }

        // MAIN. Ownership main -> worker. Call right before scheduling the job; nothing of the slot may be touched by
        // the main thread until TakeBack.
        public void HandOff(FillSlot<TMap> s, int frame)
        {
            s.Status = 0;
            s.Error = null;
            s.WorkTicks = 0;
            s.HandOffFrame = frame;
            if (Interlocked.CompareExchange(ref s.OwnerFlag, 1, 0) != 0)
            {
                throw new InvalidOperationException("MultiCore: slot " + s.Index + " handed off twice");
            }
            _pending++;
        }

        // MAIN. Undo a HandOff whose job could not be scheduled (the worker never got it).
        public void CancelHandOff(FillSlot<TMap> s)
        {
            if (Interlocked.CompareExchange(ref s.OwnerFlag, 0, 1) == 1) _pending--;
        }

        // WORKER. Never throws: the exception is stored in the slot and surfaced on the main thread by TakeBack.
        public static void RunWorker(FillSlot<TMap> s)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (Volatile.Read(ref s.OwnerFlag) != 1)
                {
                    throw new InvalidOperationException("MultiCore: worker ran on slot " + s.Index + " while the main thread owns it");
                }
                TeamMapKernel.Fill(ref s.Map, s.Keys, s.Vals, s.N);
                s.Status = 1;
            }
            catch (Exception ex)
            {
                s.Error = ex;
                s.Status = -1;
            }
            finally
            {
                s.WorkTicks = Stopwatch.GetTimestamp() - t0;
            }
        }

        // MAIN, after the backend reported the job complete. Ownership worker -> main, counters. Returns the worker's
        // exception (or "never ran") - the caller fails the item; null = the map holds the staged content.
        public Exception TakeBack(FillSlot<TMap> s, int frame, bool late)
        {
            if (Interlocked.CompareExchange(ref s.OwnerFlag, 0, 1) != 1)
            {
                throw new InvalidOperationException("MultiCore: take-back of slot " + s.Index + " that was not handed off");
            }
            _pending--;
            FillStats st = Stats;
            st.TakeBacks++;
            st.WorkerTicks += s.WorkTicks;
            if (late) st.Late++;
            if (frame >= s.HandOffFrame && s.HandOffFrame >= 0) st.StalenessFrames += frame - s.HandOffFrame;
            if (s.Status == 1) return null;
            st.WorkerErrors++;
            return s.Status == -1 ? (s.Error ?? new Exception("worker failed without an exception object"))
                                  : new InvalidOperationException("MultiCore: job completed but the worker body never ran (slot " + s.Index + ")");
        }

        // MAIN. Drop every slot (scene change). All slots must be taken back first.
        public void Clear()
        {
            for (int i = 0; i < _count; i++) AssertMain(_slots[i]);
            Array.Clear(_slots, 0, _count);
            _count = 0;
            _pending = 0;
        }
    }

    // Main-thread counters (plain fields; the worker's time is added at TakeBack on the main thread).
    internal sealed class FillStats
    {
        public static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        public long TeamFramesJob;        // team-frames whose map was filled by a worker
        public long TeamFramesSmall;      // below SensorMapsMinEntries: vanilla synchronous (schedule cost > work)
        public long TeamFramesOther;      // no team / not applicable: vanilla
        public long Entries;              // entries filled by workers
        public long TakeBacks, Late, StalenessFrames, WorkerErrors;
        public long Hooks, HookMisses;    // sensor jobs that got a fill dependency / ran without one (team synchronous)
        public long SnapshotTicks;        // main: our Init replacement (container upkeep, time copy, staging, schedule)
        public long ApplyTicks;           // main: dependency hook-up per sensor + the take-back pass
        public long WorkerTicks;          // worker: fill bodies
        public long CheckTicks;           // main: self-check (reference build + compare), not part of the normal cost
        public long Checks, CheckIdentical, CheckStale, CheckDifferent;
        public long RefTicks, RefEntries; // vanilla Init timed on the self-check's scratch container

        long _pJob, _pSmall, _pOther, _pEntries, _pTakeBacks, _pLate, _pStale, _pHooks, _pMiss, _pSnap, _pApply, _pWork, _pCheck;

        // Rates since the previous call into one status line. dt = seconds of real time.
        public string Publish(double dt)
        {
            if (!(dt > 0)) dt = 1;
            long job = TeamFramesJob - _pJob, small = TeamFramesSmall - _pSmall, other = TeamFramesOther - _pOther;
            long ent = Entries - _pEntries, tb = TakeBacks - _pTakeBacks, late = Late - _pLate, stale = StalenessFrames - _pStale;
            long hooks = Hooks - _pHooks, miss = HookMisses - _pMiss;
            double snap = (SnapshotTicks - _pSnap) * TicksToMs / dt, apply = (ApplyTicks - _pApply) * TicksToMs / dt;
            double work = (WorkerTicks - _pWork) * TicksToMs / dt, check = (CheckTicks - _pCheck) * TicksToMs / dt;
            _pJob = TeamFramesJob; _pSmall = TeamFramesSmall; _pOther = TeamFramesOther; _pEntries = Entries; _pTakeBacks = TakeBacks;
            _pLate = Late; _pStale = StalenessFrames; _pHooks = Hooks; _pMiss = HookMisses; _pSnap = SnapshotTicks; _pApply = ApplyTicks;
            _pWork = WorkerTicks; _pCheck = CheckTicks;

            var sb = new StringBuilder(256);
            sb.Append("team-frames/s ").Append(F0((job + small + other) / dt))
              .Append(" (worker ").Append(F0(job / dt)).Append(", sync small ").Append(F0(small / dt)).Append(", sync other ").Append(F0(other / dt)).Append(')');
            sb.Append(" | entries/s ").Append(F0(ent / dt));
            sb.Append(" | worker ").Append(F2(work)).Append(" ms/s");
            sb.Append(" | main snapshot ").Append(F2(snap)).Append(" ms/s, apply ").Append(F2(apply)).Append(" ms/s");
            sb.Append(" | sensor jobs chained/s ").Append(F0(hooks / dt)).Append(", unchained/s ").Append(F0(miss / dt));
            sb.Append(" | late ").Append(late).Append(", staleness ").Append(tb > 0 ? F2((double)stale / tb) : "-").Append(" fr");
            if (WorkerErrors > 0) sb.Append(" | WORKER ERRORS ").Append(WorkerErrors);
            if (Checks > 0 || check > 0)
            {
                sb.Append(" | self-check ").Append(Checks).Append(": identical ").Append(CheckIdentical).Append(", stale ").Append(CheckStale)
                  .Append(", different ").Append(CheckDifferent).Append(" (").Append(F2(check)).Append(" ms/s)");
            }
            double ns = VanillaNsPerEntry;
            if (ns > 0)
            {
                double vanilla = ns * ent / 1e6 / dt;
                sb.Append(" | est. vanilla ").Append(F2(vanilla)).Append(" ms/s (").Append(F0(ns)).Append(" ns/entry) -> net main gain ~")
                  .Append(F2(vanilla - snap - apply)).Append(" ms/s");
            }
            return sb.ToString();
        }

        // Vanilla Init cost per entry, measured on the self-check's scratch container (0 = no samples yet).
        public double VanillaNsPerEntry => RefEntries > 0 ? RefTicks * TicksToMs * 1e6 / RefEntries : 0;

        static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    internal struct Sampler
    {
        uint _threshold;                  // of 2^24
        uint _state;

        public void Init(float fraction, uint seed = 2463534242u)
        {
            _state = seed == 0 ? 2463534242u : seed;
            if (!(fraction > 0f)) { _threshold = 0; return; }
            if (fraction > 1f) fraction = 1f;
            _threshold = Math.Max(1u, (uint)(fraction * 16777216f));
        }

        public bool Enabled => _threshold != 0;

        public bool Sample()
        {
            if (_threshold == 0) return false;
            uint x = _state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            _state = x;
            return (x & 0xFFFFFFu) < _threshold;
        }
    }
    // SensorPipeline_Rate decision (Unity-free, unit-tested). One Step per frame, at the pipeline's first part.
    // Run when forced (first frame after scene load / switch-on), when target deletions are pending (vanilla removes
    // them from Target.Targets inside TargetGrid.Update; until then the grid would hand out destroyed targets), or
    // when the time since the last run reached the interval. TickDt = the game time since the previous run, so the
    // sensor round-robin (CurrentNumTick = ceil(dt x sensors)) keeps vanilla's per-second throughput.
    internal struct RateGate
    {
        public float Interval;
        public float Accum;
        public bool Force;
        public long Frames, Runs, RunsForced, RunsDeletion, Skips;

        public void Arm(float interval) { Interval = interval; Accum = 0f; Force = true; }

        public bool Step(float dt, bool deletionsPending, out float tickDt)
        {
            Frames++;
            if (dt > 0f) Accum += dt;
            bool run;
            if (Force) { run = true; RunsForced++; }
            else if (deletionsPending) { run = true; RunsDeletion++; }
            else run = Accum >= Interval;
            if (!run) { Skips++; tickDt = 0f; return false; }
            Runs++;
            Force = false;
            tickDt = Accum;
            Accum = 0f;
            return true;
        }
    }
}
