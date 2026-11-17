#if MULTICORE_OFFLINE_TESTS
/*
 Offline tests of the MultiCore kernel (Kernel.cs, the production file, compiled in unchanged). Normal .NET 8, no
 Unity, no game. Run: dotnet run -c Release [-p:MultiCoreTestsOut=<dir>\] [-- <frames> <seed>]

 1. Kernel vs brute force: TeamMapKernel.Stage + Fill must issue exactly the write sequence of vanilla's
    TeamJobNativeContainers.Init (`foreach (kv in DetectedTargetIndexes) map[kv.Key.GetInstanceID()] = kv.Value`) on
    random detected lists built with the game's own bookkeeping (DetectTarget appends, LostTarget swap-removes,
    destroyed targets report id 0), so the final content and the insertion order match.
 2. Protocol: double hand-off, take-back without hand-off, worker on a main-owned slot, cancel, clear with pending.
 3. Threaded stress: many frames of the game's call order (SensorTickPart1: complete last frame's sensor jobs -> Init
    per team -> sensor jobs chained on the fill; main mutates the teams meanwhile; Part2 take-back only on some frames
    so the next Init has to take back; random switch-off / on, scene changes, injected worker delays and exceptions).
    A reader/writer guard on every map detects any overlap of a worker write with a main-thread or consumer access.
    "Jobs" are Tasks on the .NET thread pool; a chained consumer is a continuation (Unity: JobHandle dependency).
 What this cannot cover: Unity's NativeParallelHashMap itself and Unity's job system (Mono worker threads, inline
 execution in Complete); the Harmony patches and the transpiler (TargetCheck + refcheck + the lab cover those).
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SiServerPerf.Modules.MultiCore.Tests
{
    sealed class FakeTarget
    {
        public int Id;
        public bool Destroyed;
    }

    struct FakeIds : IInstanceIds<FakeTarget>
    {
        public int Id(FakeTarget t) => t.Destroyed ? 0 : t.Id;   // Unity: GetInstanceID() of a destroyed object = 0
    }

    // Team.DetectTarget / LostTarget bookkeeping (Team.cs 1619-1720): list + dictionary + times, swap-back removal.
    sealed class FakeTeam
    {
        public readonly List<FakeTarget> List = new List<FakeTarget>();
        public readonly Dictionary<FakeTarget, int> Dict = new Dictionary<FakeTarget, int>();
        public readonly List<float> Times = new List<float>();

        public void Detect(FakeTarget t)
        {
            if (Dict.TryGetValue(t, out int i)) { Times[i] = 8f; return; }
            Dict.Add(t, List.Count);
            List.Add(t);
            Times.Add(8f);
        }

        public void LoseAt(int num)
        {
            FakeTarget t = List[num];
            if (!Dict.Remove(t)) return;
            int last = List.Count - 1;
            List[num] = List[last]; List.RemoveAt(last);
            Times[num] = Times[last]; Times.RemoveAt(last);
            if (num < List.Count) Dict[List[num]] = num;
        }

        public void Mutate(Random r, FakeTarget[] pool, int ops)
        {
            for (int k = 0; k < ops; k++)
            {
                int op = r.Next(10);
                if (op < 5 || List.Count == 0) Detect(pool[r.Next(pool.Length)]);
                else if (op < 8) LoseAt(r.Next(List.Count));
                else if (op < 9) { int i = r.Next(List.Count); Times[i] -= 0.5f; }
                else List[r.Next(List.Count)].Destroyed = r.Next(4) == 0;   // destroyed but still listed: id 0
            }
        }
    }

    // Map with a reader/writer guard: 0 free, >0 readers, -1 writer. Any overlap of a write with anything is a
    // violation (the protocol must make them impossible).
    sealed class MapData
    {
        public readonly Dictionary<int, int> D = new Dictionary<int, int>();
        public int State;
        public long Violations;
        public List<(int, int)> Log;     // write sequence (kernel test)
        public int ThrowAfter = -1;      // injected worker exception after this many writes
        public int Writes;

        public void EnterWrite() { if (Interlocked.CompareExchange(ref State, -1, 0) != 0) Interlocked.Increment(ref Violations); }
        public void ExitWrite() { Interlocked.CompareExchange(ref State, 0, -1); }
        public void EnterRead()
        {
            while (true)
            {
                int s = Volatile.Read(ref State);
                if (s < 0) { Interlocked.Increment(ref Violations); return; }
                if (Interlocked.CompareExchange(ref State, s + 1, s) == s) return;
            }
        }
        public void ExitRead()
        {
            while (true)
            {
                int s = Volatile.Read(ref State);
                if (s <= 0) return;
                if (Interlocked.CompareExchange(ref State, s - 1, s) == s) return;
            }
        }
        public void ClearMain() { EnterWrite(); D.Clear(); Writes = 0; ExitWrite(); }
    }

    sealed class InjectedFault : Exception { public InjectedFault(string m) : base(m) { } }

    struct GuardedMap : IIntMapWriter
    {
        public MapData Data;
        public void Set(int key, int value)
        {
            MapData d = Data;
            d.EnterWrite();
            try
            {
                if (d.ThrowAfter >= 0 && d.Writes >= d.ThrowAfter) throw new InjectedFault("injected at write " + d.Writes);
                d.D[key] = value;
                d.Writes++;
                d.Log?.Add((key, value));
            }
            finally { d.ExitWrite(); }
        }
    }

    static class Program
    {
        static int s_Fail;

        static void Check(bool ok, string what)
        {
            if (ok) return;
            s_Fail++;
            Console.WriteLine("  FAIL: " + what);
        }

        static int Main(string[] args)
        {
            int frames = args.Length > 0 ? int.Parse(args[0]) : 20000;
            int seed = args.Length > 1 ? int.Parse(args[1]) : 12345;
            Console.WriteLine("MultiCore offline tests, .NET " + Environment.Version + ", " + Environment.ProcessorCount + " logical cpus, frames " + frames + ", seed " + seed);
            var sw = Stopwatch.StartNew();
            KernelVsBruteForce(seed, 4000);
            Protocol();
            SamplerAndStats(seed);
            Stress(seed, frames, chained: true);
            Stress(seed + 1, Math.Max(2000, frames / 10), chained: false);   // detector check: must catch the race
            RateGateTests(seed, Math.Max(200000, frames * 5));
            Console.WriteLine(s_Fail == 0 ? "ALL PASSED (" + sw.Elapsed.TotalSeconds.ToString("0.0") + " s)" : s_Fail + " FAILED");
            return s_Fail == 0 ? 0 : 1;
        }

        static FakeTarget[] MakePool(Random r, int n)
        {
            var pool = new FakeTarget[n];
            for (int i = 0; i < n; i++) pool[i] = new FakeTarget { Id = -(1000 + i * 7 + r.Next(5)) * (r.Next(2) * 2 - 1) };   // +- ids like Unity's
            return pool;
        }

        // vanilla TeamJobNativeContainers.Init, the map part
        static void VanillaFill(Dictionary<FakeTarget, int> dict, List<(int, int)> log, Dictionary<int, int> map)
        {
            map.Clear();
            var ids = default(FakeIds);
            foreach (KeyValuePair<FakeTarget, int> kv in dict)
            {
                int k = ids.Id(kv.Key);
                map[k] = kv.Value;
                log?.Add((k, kv.Value));
            }
        }

        // ------------------------------------------------------------------ 1. kernel vs brute force
        static void KernelVsBruteForce(int seed, int cases)
        {
            Console.WriteLine("[1] kernel vs brute force, " + cases + " random detected lists");
            var r = new Random(seed);
            int[] keys = new int[64], vals = new int[64];
            long entries = 0;
            int bad = 0;
            for (int c = 0; c < cases; c++)
            {
                var pool = MakePool(r, 1 + r.Next(c % 10 == 0 ? 5000 : 800));
                var team = new FakeTeam();
                team.Mutate(r, pool, r.Next(4 * pool.Length + 1));
                var refLog = new List<(int, int)>();
                var refMap = new Dictionary<int, int>();
                VanillaFill(team.Dict, refLog, refMap);

                var ids = default(FakeIds);
                int n = TeamMapKernel.Stage(team.Dict, ref ids, ref keys, ref vals);
                var data = new MapData { Log = new List<(int, int)>() };
                var map = new GuardedMap { Data = data };
                TeamMapKernel.Fill(ref map, keys, vals, n);
                entries += n;

                bool ok = n == team.Dict.Count && data.Log.Count == refLog.Count && data.D.Count == refMap.Count && data.Violations == 0;
                for (int i = 0; ok && i < refLog.Count; i++) ok = data.Log[i] == refLog[i];
                foreach (var kv in refMap) { if (!ok) break; ok = data.D.TryGetValue(kv.Key, out int v) && v == kv.Value; }
                // the map must also agree with the list (index of every live entry)
                for (int i = 0; ok && i < team.List.Count; i++)
                {
                    FakeTarget t = team.List[i];
                    if (!t.Destroyed && data.D.TryGetValue(t.Id, out int v)) ok = v == i; else if (!t.Destroyed) ok = false;
                }
                if (!ok && bad++ < 5) Console.WriteLine("  case " + c + ": D " + team.Dict.Count + " mismatch");
            }
            Check(bad == 0, bad + " kernel cases differ from vanilla");
            Check(TeamMapKernel.NextPow2(0) == 64 && TeamMapKernel.NextPow2(65) == 128 && TeamMapKernel.NextPow2(4096) == 4096 && TeamMapKernel.NextPow2(4097) == 8192, "NextPow2");
            Console.WriteLine("  " + cases + " cases, " + entries + " entries, write sequence identical in " + (cases - bad) + "/" + cases);
        }

        // ------------------------------------------------------------------ 2. protocol
        static void Protocol()
        {
            Console.WriteLine("[2] ownership protocol");
            var p = new FillPipeline<GuardedMap>();
            object owner = new object();
            var s = p.GetOrAdd(owner);
            Check(ReferenceEquals(p.Find(owner), s) && p.Count == 1, "GetOrAdd / Find");
            Check(Throws(() => p.TakeBack(s, 0, false)), "take-back without hand-off must throw");
            s.Map = new GuardedMap { Data = new MapData() };
            FillPipeline<GuardedMap>.RunWorker(s);
            Check(s.Status == -1 && s.Error is InvalidOperationException, "worker on a main-owned slot must fail");
            p.HandOff(s, 1);
            Check(s.Pending && p.PendingCount == 1, "pending after hand-off");
            Check(Throws(() => p.HandOff(s, 1)), "double hand-off must throw");
            Check(Throws(() => FillPipeline<GuardedMap>.AssertMain(s)), "AssertMain on a worker-owned slot must throw");
            Check(Throws(() => p.Clear()), "Clear with a pending slot must throw");
            FillPipeline<GuardedMap>.RunWorker(s);
            Check(s.Status == 1, "worker ran");
            Check(p.TakeBack(s, 3, true) == null && !s.Pending && p.PendingCount == 0, "take-back ok");
            Check(p.Stats.Late == 1 && p.Stats.StalenessFrames == 2 && p.Stats.TakeBacks == 1, "take-back counters");
            p.HandOff(s, 4);
            Check(p.TakeBack(s, 4, false) is InvalidOperationException, "a job that completed without running the body is an error");
            p.HandOff(s, 5);
            p.CancelHandOff(s);
            Check(!s.Pending && p.PendingCount == 0, "cancel hand-off");
            s.Map.Data.ThrowAfter = 0;
            s.N = 1; s.Keys[0] = 1; s.Vals[0] = 1;
            p.HandOff(s, 6);
            FillPipeline<GuardedMap>.RunWorker(s);
            Check(p.TakeBack(s, 6, false) is InjectedFault, "worker exception surfaced at take-back");
            p.Clear();
            Check(p.Count == 0 && p.Find(owner) == null, "Clear");
        }

        static bool Throws(Action a)
        {
            try { a(); return false; } catch (InvalidOperationException) { return true; }
        }

        static void SamplerAndStats(int seed)
        {
            Console.WriteLine("[2b] sampler + status line");
            var smp = new Sampler();
            smp.Init(0.1f, (uint)seed);
            int hits = 0;
            for (int i = 0; i < 100000; i++) if (smp.Sample()) hits++;
            Check(hits > 9000 && hits < 11000, "sampler 10 % gave " + hits / 1000.0 + " %");
            var off = new Sampler();
            off.Init(0f);
            Check(!off.Enabled && !off.Sample(), "sampler off");
            var st = new FillStats { TeamFramesJob = 100, Entries = 50000, SnapshotTicks = Stopwatch.Frequency / 1000, RefTicks = Stopwatch.Frequency / 100, RefEntries = 100000, Checks = 3, CheckIdentical = 3 };
            string line = st.Publish(1.0);
            Check(line.Contains("est. vanilla") && line.Contains("self-check 3"), "status line: " + line);
            Console.WriteLine("  " + line);
        }

        // ------------------------------------------------------------------ 3. threaded stress
        sealed class TeamCtx
        {
            public FakeTeam Team = new FakeTeam();
            public object Container = new object();
            public MapData Map = new MapData();
            public Task Fill;                      // "JobHandle" of the fill
            public Dictionary<int, int> Expected;  // vanilla's map at Init time
            public bool Poisoned;                  // an exception was injected into this fill
            public InjectedFault InjectedObj;
        }

        static void Stress(int seed, int frames, bool chained)
        {
            const int Teams = 6, MinEntries = 64;
            Console.WriteLine(chained ? "[3] threaded stress: " + frames + " frames x " + Teams + " teams, workers = .NET thread pool"
                                      : "[4] detector check: same run with the sensor reads NOT chained on the fill (a broken protocol), " + frames + " frames");
            var r = new Random(seed);
            var pool = MakePool(r, 4000);
            var p = new FillPipeline<GuardedMap>();
            var teams = new TeamCtx[Teams];
            for (int t = 0; t < Teams; t++) teams[t] = new TeamCtx();
            var consumers = new List<Task>();
            bool on = true;
            int offUntil = -1;
            long fills = 0, syncFills = 0, consumerReads = 0, consumerMismatch = 0, poisonedReads = 0, injected = 0, surfaced = 0;
            long unexpectedErrors = 0, protocolExceptions = 0, drops = 0, switchOffs = 0, takeBacksNextInit = 0;
            double maxWaitMs = 0, maxInjectedMs = 0;
            var surfacedSet = new HashSet<Exception>();
            var swWait = new Stopwatch();

            void Wait(Task task)
            {
                if (task == null) return;
                swWait.Restart();
                try { task.Wait(); } catch (AggregateException) { }
                double ms = swWait.Elapsed.TotalMilliseconds;
                if (ms > maxWaitMs) maxWaitMs = ms;
            }

            void WaitConsumers()   // vanilla: EnsureSensorJobsCompleted / Part2's Complete of the sensor jobs
            {
                foreach (Task c in consumers) Wait(c);
                consumers.Clear();
            }

            // SensorTeamMaps.CompleteSlot
            void CompleteSlot(TeamCtx tc, FillSlot<GuardedMap> s, int frame)
            {
                bool late = tc.Fill != null && !tc.Fill.IsCompleted;
                Wait(tc.Fill);
                tc.Fill = null;
                Exception err;
                try { err = p.TakeBack(s, frame, late); }
                catch (InvalidOperationException) { protocolExceptions++; return; }
                if (err == null) return;
                if (err is InjectedFault f && ReferenceEquals(f, tc.InjectedObj))
                {
                    if (surfacedSet.Add(f)) surfaced++;
                    tc.InjectedObj = null;
                    // Item.Fail -> OnSwitch(false) -> CompleteAll; the test re-arms after a few frames
                    on = false;
                    offUntil = frame + 3;
                    CompleteAll(frame);
                }
                else unexpectedErrors++;
            }

            void CompleteAll(int frame)
            {
                for (int t = 0; t < Teams; t++)
                {
                    var s = p.Find(teams[t].Container);
                    if (s != null && s.Pending) CompleteSlot(teams[t], s, frame);
                }
            }

            for (int frame = 0; frame < frames; frame++)
            {
                // ---- SensorTickPart1: EnsureSensorJobsCompleted (consumers of the previous frame)
                WaitConsumers();
                if (!on && frame >= offUntil && offUntil >= 0) { on = true; offUntil = -1; }

                // ---- TickSensors -> UpdateNativeContainerSizes -> Init per team
                for (int t = 0; t < Teams; t++)
                {
                    TeamCtx tc = teams[t];
                    var s = on ? p.GetOrAdd(tc.Container) : null;
                    if (s != null && s.Pending) { takeBacksNextInit++; CompleteSlot(tc, s, frame); }
                    if (!on) s = null;   // a take-back above may have failed the item
                    tc.Expected = new Dictionary<int, int>();
                    VanillaFill(tc.Team.Dict, null, tc.Expected);
                    tc.Poisoned = false;
                    int n = tc.Team.Dict.Count;
                    if (s == null || n < MinEntries)
                    {
                        // vanilla synchronous Init on the main thread
                        tc.Map.ClearMain();
                        tc.Map.EnterWrite();
                        foreach (var kv in tc.Expected) tc.Map.D[kv.Key] = kv.Value;
                        tc.Map.ExitWrite();
                        syncFills++;
                        continue;
                    }
                    tc.Map.ClearMain();   // vanilla upkeep on the main thread, before the hand-off
                    var ids = default(FakeIds);
                    s.N = TeamMapKernel.Stage(tc.Team.Dict, ref ids, ref s.Keys, ref s.Vals);
                    s.Map = new GuardedMap { Data = tc.Map };
                    tc.Map.ThrowAfter = -1;
                    if (r.Next(400) == 0) { tc.Map.ThrowAfter = r.Next(Math.Max(1, s.N)); tc.Poisoned = true; injected++; }
                    int delayUs = r.Next(10) == 0 ? r.Next(3000) : 0;
                    if (delayUs / 1000.0 > maxInjectedMs) maxInjectedMs = delayUs / 1000.0;
                    p.HandOff(s, frame);
                    FillSlot<GuardedMap> slot = s;
                    TeamCtx ctx = tc;
                    tc.Fill = Task.Run(() =>
                    {
                        if (delayUs > 0) SpinFor(delayUs);
                        FillPipeline<GuardedMap>.RunWorker(slot);
                        if (slot.Status == -1 && slot.Error is InjectedFault f) ctx.InjectedObj = f;
                    });
                    fills++;
                }

                // ---- sensor ticks: CreateSensorCommandsJob per ticked sensor, chained on its team's fill
                int sensors = r.Next(1, 2 * Teams);
                for (int k = 0; k < sensors; k++)
                {
                    TeamCtx tc = teams[r.Next(Teams)];
                    var expected = tc.Expected;
                    bool poisoned = tc.Poisoned;
                    MapData map = tc.Map;
                    var s = p.Find(tc.Container);
                    Action<Task> read = _ =>
                    {
                        map.EnterRead();
                        bool same = map.D.Count == expected.Count;
                        if (same) foreach (var kv in expected) { if (!map.D.TryGetValue(kv.Key, out int v) || v != kv.Value) { same = false; break; } }
                        map.ExitRead();
                        Interlocked.Increment(ref consumerReads);
                        if (!same)
                        {
                            if (poisoned) Interlocked.Increment(ref poisonedReads);
                            else Interlocked.Increment(ref consumerMismatch);
                        }
                    };
                    if (chained && s != null && s.Pending && tc.Fill != null) consumers.Add(tc.Fill.ContinueWith(read));   // dependency
                    else consumers.Add(Task.Run(() => read(null)));
                }

                // ---- the rest of the frame: the main thread races on, the teams change (never the maps)
                for (int t = 0; t < Teams; t++) teams[t].Team.Mutate(r, pool, r.Next(8));

                // ---- Part2 on 60 % of the frames (else the next Init takes back)
                if (r.Next(10) < 6)
                {
                    WaitConsumers();
                    for (int t = 0; t < Teams; t++)
                    {
                        var s = p.Find(teams[t].Container);
                        if (s != null && s.Pending) CompleteSlot(teams[t], s, frame);
                    }
                }

                // ---- runtime switch-off (OnSwitch(false) -> CompleteAll), scene change (Clear + new containers)
                int ev = r.Next(500);
                if (ev == 0 && on)
                {
                    switchOffs++;
                    on = false;
                    offUntil = frame + 1 + r.Next(20);
                    CompleteAll(frame);
                }
                else if (ev == 1)
                {
                    drops++;
                    WaitConsumers();
                    CompleteAll(frame);
                    try { p.Clear(); } catch (InvalidOperationException) { protocolExceptions++; }
                    for (int t = 0; t < Teams; t++) { teams[t].Container = new object(); teams[t].Map = new MapData(); }
                }
            }
            WaitConsumers();
            CompleteAll(frames);

            long violations = 0;
            for (int t = 0; t < Teams; t++) violations += teams[t].Map.Violations;
            Console.WriteLine("  fills on workers " + fills + ", synchronous " + syncFills + ", sensor reads " + consumerReads +
                              ", taken back by the next Init " + takeBacksNextInit + ", switch-offs " + switchOffs + ", scene drops " + drops);
            Console.WriteLine("  injected worker exceptions " + injected + ", surfaced on main " + surfaced + ", reads of a poisoned map " + poisonedReads +
                              " | late " + p.Stats.Late + ", mean staleness " + (p.Stats.TakeBacks > 0 ? ((double)p.Stats.StalenessFrames / p.Stats.TakeBacks).ToString("0.000") : "-") +
                              " fr | max main wait " + maxWaitMs.ToString("0.00") + " ms (max injected delay " + maxInjectedMs.ToString("0.00") + " ms)");
            Console.WriteLine("  worker time " + (p.Stats.WorkerTicks * FillStats.TicksToMs).ToString("0.0") + " ms over " + p.Stats.TakeBacks + " take-backs");
            if (!chained)
            {
                Console.WriteLine("  detector saw " + violations + " access overlaps and " + consumerMismatch + " wrong reads");
                Check(violations + consumerMismatch > 0, "the race detector did not notice unchained reads");
                return;
            }
            Check(violations == 0, violations + " map access overlaps (worker write vs main / consumer)");
            Check(consumerMismatch == 0, consumerMismatch + " sensor reads saw a map different from vanilla's");
            Check(surfaced == injected, "injected " + injected + " worker exceptions, surfaced " + surfaced);
            Check(unexpectedErrors == 0, unexpectedErrors + " unexpected worker errors");
            Check(protocolExceptions == 0, protocolExceptions + " ownership protocol exceptions");
            Check(p.PendingCount == 0, "slots still pending at the end: " + p.PendingCount);
            Check(fills > frames, "too few worker fills: " + fills);
            Check(maxWaitMs < maxInjectedMs + 250, "main-thread wait " + maxWaitMs + " ms beyond injected delay");
        }

        // ------------------------------------------------------------------ 5. SensorPipeline_Rate gate
        static void RateGateTests(int seed, int frames)
        {
            Console.WriteLine("[5] SensorPipeline_Rate gate, " + frames + " random frames (fps 20-160, hitches, deletions, switch-on)");
            var r = new Random(seed);
            var g = new RateGate();
            g.Arm(0.05f);
            double dtIn = 0, dtOut = 0;
            int badSkip = 0, badDeletion = 0, badForce = 0;
            bool expectForce = true;
            for (int f = 0; f < frames; f++)
            {
                float dt = r.Next(200) == 0 ? 0.1f : 1f / (20 + r.Next(141));
                if (r.Next(1000) == 0) dt = 0f;   // paused frame
                bool del = r.Next(50) == 0;
                if (r.Next(5000) == 0) { dtOut += g.Accum; g.Arm(0.05f); expectForce = true; }   // switch-on / scene load (discards the remainder by design)
                float before = g.Accum + (dt > 0 ? dt : 0);
                if (dt > 0) dtIn += dt;
                bool run = g.Step(dt, del, out float tickDt);
                if (run) dtOut += tickDt;
                if (!run && before >= g.Interval) badSkip++;
                if (!run && del) badDeletion++;
                if (expectForce && !run) badForce++;
                if (run && Math.Abs(tickDt - before) > 1e-4f) badSkip++;
                expectForce = false;
            }
            dtOut += g.Accum;
            Check(Math.Abs(dtIn - dtOut) < 1e-2 * Math.Max(1, dtIn / 1000), "dt conservation: in " + dtIn.ToString("0.000") + " s, handed to the sensors " + dtOut.ToString("0.000") + " s");
            Check(badSkip == 0, badSkip + " skips with the interval elapsed / wrong tick dt");
            Check(badDeletion == 0, badDeletion + " frames with deletions pending did not run");
            Check(badForce == 0, badForce + " forced frames did not run");
            Console.WriteLine("  frames " + g.Frames + ", runs " + g.Runs + " (forced " + g.RunsForced + ", deletions " + g.RunsDeletion + "), skipped " + g.Skips);

            // sensor round-robin throughput: vanilla ceil(dt x S) per frame vs ceil(tickDt x S) per run
            Console.WriteLine("  sensor ticks per sensor per second (vanilla per frame -> 20 Hz pipeline):");
            foreach (int fps in new[] { 30, 60, 115 })
            {
                var line = new System.Text.StringBuilder("    fps " + fps + ":");
                foreach (int S in new[] { 50, 100, 300, 600, 900 })
                {
                    double van = Math.Ceiling(S / (double)fps) * fps / S;
                    int framesPerRun = (int)Math.Ceiling(0.05 * fps - 1e-9);
                    double runHz = fps / (double)framesPerRun, acc = framesPerRun / (double)fps;
                    double ours = Math.Ceiling(acc * S) * runHz / S;
                    line.Append("  S=" + S + " " + van.ToString("0.00") + "->" + ours.ToString("0.00"));
                }
                Console.WriteLine(line.ToString());
            }
        }

        static void SpinFor(int us)
        {
            long until = Stopwatch.GetTimestamp() + us * Stopwatch.Frequency / 1000000;
            while (Stopwatch.GetTimestamp() < until) Thread.SpinWait(20);
        }
    }
}
#endif
