/*
 SensorTeamMaps_Job (S7, exact, default off) - WORKS, DOES NOT PAY (lab AI round 2026-11-16: no gain; main thread paid
 3.5 ms/s of snapshot + apply to move 2.8 ms/s to workers). The per-team detected-target index map of the sensor pipeline is
 filled on a Unity job worker instead of the main thread.

 Vanilla (1.0.17): Sensor.TickSensors -> UpdateNativeContainerSizes -> TeamJobNativeContainers.Init(team) for EVERY
 team EVERY frame (GameManager.SensorTickPart1, ordered-update prio 1000): Clear the NativeParallelHashMap<int,int>
 OwnerTeamDetectedTargetIndexes, copy List<float> DetectedTargetsTime element by element into a NativeArray, then
 `map[target.GetInstanceID()] = index` for every entry of Team.DetectedTargetIndexes (~2 hash lookups + an insert in
 Mono per entry, ~sum of all teams' detected lists per frame: ~1 600 entries at 500 units). The only reader is the
 Burst CreateSensorCommandsJob that TargetGrid.DetectWithJobsPart1 schedules right after for each ticked sensor.

 With the item on:
   SNAPSHOT  prefix TeamJobNativeContainers.Init (main): vanilla's container upkeep in vanilla order (create / Clear /
             Capacity / time array), DetectedTargetsTime copied with one memcpy (List._items), then the (instance id,
             index) pairs in dictionary enumeration order into the slot's staging arrays. ~20 ns per entry.
   COMPUTE   TeamMapFillJob (managed IJob on Unity's worker threads): map[id] = index for the staged pairs, in the same
             order -> same content and the same insertion sequence as vanilla.
   APPLY     transpiler on TargetGrid.DetectWithJobsPart1: the CreateSensorCommandsJob schedule gets the team's fill
             job as a dependency (JobHandle.CombineDependencies). The Burst job waits on the worker, not the main
             thread. Prefix Sensor.TickSensorsPart2 (prio -1, after all AI / unit updates) takes every slot back.
   Teams with fewer than Settings.SensorMapsMinEntries entries run vanilla (the schedule costs more than the work).
 Staleness: none. The map is built from the same instant vanilla would use and is consumed in the same frame.

 Safety
   - no main-thread wait of ours on the normal path; the sync point is vanilla's own Complete of the sensor jobs in
     Part2 (they depend on the fill). A worker that has not picked the job up by then: Unity's Complete runs it inline
     on the main thread, i.e. at most the vanilla cost. The job body is a bounded loop (n fixed at snapshot, no locks).
   - every take-back (Part2 prefix, next Init of the same container, Dispose, ClearSensorNativeArrays, switch-off,
     scene load, quit) completes the handle first; vanilla never touches a map a worker still writes.
   - worker exception: caught in the job, stored in the slot, surfaced at the take-back -> Item.Fail -> vanilla from the
     next frame. That frame's sensor jobs have already read the partial map (effect: some detected targets treated as
     undetected for one sensor tick = extra raycasts / redundant DetectTarget refreshes, no wrong target index).
   - runtime switch-off: OnSwitch(false) completes and takes back every slot synchronously; vanilla Init runs from the
     next frame. Slots (staging arrays, self-check scratch maps) survive any number of off/on flips.

 Self-check (Settings.SelfCheckFraction > 0): on sampled team-frames the original Init runs on a scratch
 TeamJobNativeContainers at the same instant (timed: that is the vanilla cost per entry the status line uses for the
 net-gain estimate). At the Part2 take-back the worker-filled map and time array are compared with it:
   identical = equal to vanilla's map and the team's detections did not change since Init
   stale     = equal to vanilla's map, but the live dictionary changed between Init and Part2 (vanilla has exactly the
               same staleness: it also builds the map at Init) - counted for information, not a difference
   different = not equal to vanilla's map (logged, item keeps running; a non-zero count is a bug report)
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MelonLoader;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace SiServerPerf.Modules.MultiCore
{
    internal static class SensorTeamMaps
    {
        internal static bool On;
        internal static PerfItem Item;

        // game-side map writer and id source for the generic kernel
        internal struct MapWriter : IIntMapWriter
        {
            public NativeParallelHashMap<int, int> Map;
            public void Set(int key, int value) { Map[key] = value; }
        }

        struct TargetIds : IInstanceIds<Target>
        {
            public int Id(Target t) => t.GetInstanceID();
        }

        // Managed (non-Burst) job: runs under Mono on a Unity job worker. The struct only carries the slot index; the
        // slot itself was published before Schedule (the job queue is the memory barrier).
        struct TeamMapFillJob : IJob
        {
            public int Slot;
            public void Execute()
            {
                try
                {
                    FillPipeline<MapWriter>.RunWorker(P[Slot]);
                }
                catch (Exception ex)
                {
                    // RunWorker never throws; only an indexing bug gets here. Surfaced at the next take-back.
                    s_OrphanError = ex;
                }
            }
        }

        internal static readonly FillPipeline<MapWriter> P = new FillPipeline<MapWriter>();
        static JobHandle[] s_Handles = new JobHandle[8];
        static volatile Exception s_OrphanError;
        static int s_MinEntries = 128;
        static AccessTools.FieldRef<List<float>, float[]> s_ListItems;
        static bool s_ScheduledThisCall;

        // self-check
        sealed class Check
        {
            public TeamJobNativeContainers Scratch;
            public bool Pending;
            public Team Team;
            public int Times;
        }
        static Check[] s_Checks = new Check[8];
        static Sampler s_Sampler;
        static bool s_InReference;
        static int s_Logged, s_MaxLogs = 20;
        static readonly Dictionary<int, int> s_Live = new Dictionary<int, int>(1024);

        public static void Register(ModuleContext ctx, MultiCoreSettings s)
        {
            s_MinEntries = Math.Max(0, s.SensorMapsMinEntries);
            s_MaxLogs = Math.Max(0, s.SelfCheckMaxLogsPerRound);
            s_Sampler.Init(s.SelfCheckFraction);

            Item = ctx.Item("SensorTeamMaps_Job",
                "EXPERIMENTAL S7: the per-team detected-target index map of the sensor pipeline (TeamJobNativeContainers.Init, every team every frame) is filled on a Unity job worker; the sensor jobs depend on it (same content, same frame)",
                v => On = v, defaultOn: false);

            s_ListItems = Refl.Field<List<float>, float[]>("_items", Item);   // optional: element loop without it

            var init = Item.Target(typeof(TeamJobNativeContainers), nameof(TeamJobNativeContainers.Init), new[] { typeof(Team) }, declaredOnly: true);
            var dispose = Item.Target(typeof(TeamJobNativeContainers), nameof(TeamJobNativeContainers.Dispose), Type.EmptyTypes, declaredOnly: true);
            var sizes = Item.Target(typeof(Sensor), nameof(Sensor.UpdateNativeContainerSizes), Type.EmptyTypes, declaredOnly: true);
            var part1 = Item.Target(typeof(TargetGrid), "DetectWithJobsPart1", new[] { typeof(Sensor) }, declaredOnly: true);
            var part2 = Item.Target(typeof(Sensor), nameof(Sensor.TickSensorsPart2), Type.EmptyTypes, declaredOnly: true);
            var clear = Item.Target(typeof(Sensor), nameof(Sensor.ClearSensorNativeArrays), Type.EmptyTypes, declaredOnly: true);
            if (init == null || part1 == null || part2 == null || dispose == null || clear == null)
            {
                Item.Block("a required sensor-pipeline member is absent on this build");
            }

            // Order matters for safety: the guards (take-back before vanilla touches a map) first, the transpiler that
            // chains the sensor jobs next, the producer (Init replacement) last. Unless every guard and the transpiler
            // are in, the producer is not installed (an unchained or unguarded fill could race vanilla).
            bool guards = Item.Patch(clear, typeof(SensorTeamMaps), prefix: nameof(ClearPrefix));
            guards &= Item.Patch(dispose, typeof(SensorTeamMaps), prefix: nameof(DisposePrefix));
            guards &= Item.Patch(part2, typeof(SensorTeamMaps), prefix: nameof(Part2Prefix));
            bool chained = guards && Item.Patch(part1, typeof(SensorTeamMaps), transpiler: nameof(Part1Transpiler));
            if (chained)
            {
                Item.Patch(sizes, typeof(SensorTeamMaps), postfix: nameof(SizesPostfix));
                Item.Patch(init, typeof(SensorTeamMaps), prefix: nameof(InitPrefix));
            }
            else if (Item.Active)
            {
                Item.Note("a guard patch or the DetectWithJobsPart1 transpiler is not applied: Init replacement not installed (vanilla runs)");
            }
            Item.OnSwitch(on => { if (!on) CompleteAll(); });
            if (s.SelfCheckFraction > 0f) Item.Note("self-check ON: " + (s.SelfCheckFraction * 100f).ToString("0.###") + " % of team-frames also run vanilla Init on a scratch map and compare");
        }

        // ------------------------------------------------------------------ SNAPSHOT (main thread)

        static bool InitPrefix(TeamJobNativeContainers __instance, Team team)
        {
            if (s_InReference || !On) return true;
            long t0 = Stopwatch.GetTimestamp();
            FillStats st = P.Stats;
            try
            {
                FillSlot<MapWriter> s = P.GetOrAdd(__instance);
                if (s.Pending)
                {
                    // previous frame's job that no sensor job depended on (Part2 did not take it back): apply cost
                    long tb = Stopwatch.GetTimestamp();
                    long ct = CompleteSlot(s);
                    long spent = Stopwatch.GetTimestamp() - tb;
                    st.ApplyTicks += spent - ct;
                    t0 += spent;
                }
                if (!On) return true;             // that take-back failed the item
                if ((object)team == null) { st.TeamFramesOther++; return true; }   // vanilla (throws as before)
                Dictionary<Target, int> dict = team.DetectedTargetIndexes;
                List<float> times = team.DetectedTargetsTime;
                if (dict == null || times == null) { st.TeamFramesOther++; return true; }
                int n = dict.Count;
                if (n < s_MinEntries) { st.TeamFramesSmall++; return true; }

                // vanilla container upkeep, vanilla order
                if (!__instance.OwnerTeamDetectedTargetIndexes.IsCreated)
                {
                    __instance.OwnerTeamDetectedTargetIndexes = new NativeParallelHashMap<int, int>(n, Allocator.Persistent);
                }
                __instance.OwnerTeamDetectedTargetIndexes.Clear();
                if (__instance.OwnerTeamDetectedTargetIndexes.Capacity < n)
                {
                    __instance.OwnerTeamDetectedTargetIndexes.Capacity = n;
                }
                int tc = times.Count;
                if (__instance.OwnerTeamDetectedTargetsTime.Length < tc || !__instance.OwnerTeamDetectedTargetsTime.IsCreated)
                {
                    if (__instance.OwnerTeamDetectedTargetsTime.IsCreated) __instance.OwnerTeamDetectedTargetsTime.Dispose();
                    __instance.OwnerTeamDetectedTargetsTime = new NativeArray<float>(tc, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                }
                CopyTimes(times, __instance.OwnerTeamDetectedTargetsTime, tc);

                if (s_Sampler.Enabled && s_Sampler.Sample()) RunReference(s.Index, team, tc);

                var ids = default(TargetIds);
                s.N = TeamMapKernel.Stage(dict, ref ids, ref s.Keys, ref s.Vals);
                s.Map = new MapWriter { Map = __instance.OwnerTeamDetectedTargetIndexes };
                if (s_Handles.Length < P.Count) Array.Resize(ref s_Handles, Math.Max(P.Count, s_Handles.Length * 2));

                P.HandOff(s, Time.frameCount);
                try
                {
                    s_Handles[s.Index] = new TeamMapFillJob { Slot = s.Index }.Schedule();
                }
                catch
                {
                    P.CancelHandOff(s);   // the worker never got it
                    throw;
                }
                s_ScheduledThisCall = true;
                st.TeamFramesJob++;
                st.Entries += s.N;
                return false;
            }
            catch (Exception ex)
            {
                // Fail -> OnSwitch(false) -> CompleteAll: nothing of ours is in flight when vanilla Init now redoes the
                // container from scratch (Clear + refill, idempotent).
                Item.Fail(ex, "TeamJobNativeContainers.Init prefix");
                return true;
            }
            finally
            {
                st.SnapshotTicks += Stopwatch.GetTimestamp() - t0;
            }
        }

        static void CopyTimes(List<float> times, NativeArray<float> dst, int n)
        {
            if (n == 0) return;
            float[] items = s_ListItems != null ? s_ListItems(times) : null;
            if (items != null && items.Length >= n)
            {
                NativeArray<float>.Copy(items, 0, dst, 0, n);
                return;
            }
            for (int i = 0; i < n; i++) dst[i] = times[i];
        }

        static void SizesPostfix()
        {
            if (!On || !s_ScheduledThisCall) return;
            s_ScheduledThisCall = false;
            try { JobHandle.ScheduleBatchedJobs(); }   // start the workers now, not at the next flush
            catch (Exception ex) { Item.Fail(ex, "UpdateNativeContainerSizes postfix"); }
        }

        // ------------------------------------------------------------------ APPLY: chain the sensor jobs

        // DetectWithJobsPart1: `IJobParallelForExtensions.Schedule<CreateSensorCommandsJob>(job, len, 8, default)` ->
        // `ScheduleSensorCommands(job, len, 8, default, sensor)`. Exactly one such call, else Problem + IL unchanged.
        static IEnumerable<CodeInstruction> Part1Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            int hit = -1, hits = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Call && list[i].operand is MethodInfo mi && mi.Name == "Schedule" &&
                    mi.DeclaringType == typeof(IJobParallelForExtensions) && mi.IsGenericMethod &&
                    mi.GetGenericArguments()[0] == typeof(CreateSensorCommandsJob) && mi.GetParameters().Length == 4)
                {
                    hit = i;
                    hits++;
                }
            }
            MethodInfo helper = AccessTools.Method(typeof(SensorTeamMaps), nameof(ScheduleSensorCommands));
            if (hits != 1 || helper == null)
            {
                Item.Problem("DetectWithJobsPart1: expected exactly one IJobParallelForExtensions.Schedule<CreateSensorCommandsJob>(job, int, int, JobHandle), found " + hits);
                return list;
            }
            // The call instruction object (with any labels / blocks it carries) becomes the `ldarg.0` (the sensor);
            // the helper call follows it. List<Label> is not named here: net35 Harmony's Label (mscorlib) and
            // netstandard's do not unify at compile time (same approach as UpdateTiers' TierDispatch).
            CodeInstruction first = list[hit];
            first.opcode = OpCodes.Ldarg_0;
            first.operand = null;
            list.Insert(hit + 1, new CodeInstruction(OpCodes.Call, helper));
            return list;
        }

        // Called from the transpiled DetectWithJobsPart1 instead of the vanilla Schedule (rule: switch check, try,
        // vanilla result on failure). Without a fill in flight for the sensor's team this is the vanilla call.
        static JobHandle ScheduleSensorCommands(CreateSensorCommandsJob job, int arrayLength, int innerloopBatchCount, JobHandle dependsOn, Sensor sensor)
        {
            if (On || P.PendingCount > 0)
            {
                long t0 = Stopwatch.GetTimestamp();
                try
                {
                    // the same container vanilla just put into the job (Sensor.TeamNativeContainers[sensor.Team.TeamId])
                    TeamJobNativeContainers c = Sensor.TeamNativeContainers[sensor.Team.TeamId];
                    FillSlot<MapWriter> s = P.Find(c);
                    if (s != null && s.Pending)
                    {
                        dependsOn = JobHandle.CombineDependencies(dependsOn, s_Handles[s.Index]);
                        P.Stats.Hooks++;
                    }
                    else
                    {
                        P.Stats.HookMisses++;
                    }
                }
                catch (Exception ex)
                {
                    // Fail completes every fill (OnSwitch): scheduling without the dependency is then safe
                    Item.Fail(ex, "DetectWithJobsPart1 schedule hook");
                }
                P.Stats.ApplyTicks += Stopwatch.GetTimestamp() - t0;
            }
            return IJobParallelForExtensions.Schedule(job, arrayLength, innerloopBatchCount, dependsOn);
        }

        // ------------------------------------------------------------------ take-back points

        static void Part2Prefix()
        {
            if (!On && P.PendingCount == 0) return;
            long t0 = Stopwatch.GetTimestamp(), checkTicks = 0;
            try
            {
                for (int i = 0; i < P.Count; i++)
                {
                    FillSlot<MapWriter> s = P[i];
                    if (!s.Pending) continue;
                    checkTicks += CompleteSlot(s);
                    if (!On) return;
                }
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "Sensor.TickSensorsPart2 prefix");
            }
            finally
            {
                P.Stats.ApplyTicks += Stopwatch.GetTimestamp() - t0 - checkTicks;
            }
        }

        static void DisposePrefix(TeamJobNativeContainers __instance)
        {
            if (P.PendingCount == 0) return;
            try
            {
                FillSlot<MapWriter> s = P.Find(__instance);
                if (s != null && s.Pending) CompleteSlot(s);
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "TeamJobNativeContainers.Dispose prefix");
            }
        }

        static void ClearPrefix()
        {
            try
            {
                DropAll();
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "Sensor.ClearSensorNativeArrays prefix");
            }
        }

        // Completes one slot's job and takes the slot back; then the slot's pending self-check (whatever the take-back
        // point: the compare must see this hand-off's map, not a later one). A worker error fails the item (which
        // completes the rest). Returns the Stopwatch ticks spent in the self-check.
        static long CompleteSlot(FillSlot<MapWriter> s)
        {
            ref JobHandle h = ref s_Handles[s.Index];
            bool late = !h.IsCompleted;
            h.Complete();
            h = default;
            Exception err = P.TakeBack(s, Time.frameCount, late);
            Exception orphan = s_OrphanError;
            if (orphan != null) { s_OrphanError = null; err = err ?? orphan; }
            Check c = s.Index < s_Checks.Length ? s_Checks[s.Index] : null;
            long checkTicks = 0;
            if (c != null && c.Pending)
            {
                if (err != null)
                {
                    c.Pending = false;   // a partial map is the documented failure effect, not a self-check result
                }
                else
                {
                    long tc = Stopwatch.GetTimestamp();
                    try { CompareCheck(s, c); }
                    catch (Exception ex) { c.Pending = false; MelonLogger.Warning("[MultiCore] self-check compare failed: " + ex.Message); }
                    checkTicks = Stopwatch.GetTimestamp() - tc;
                    P.Stats.CheckTicks += checkTicks;
                }
            }
            if (err != null) Item.Fail(err, "worker (TeamMapFillJob, slot " + s.Index + ")");
            return checkTicks;
        }

        // Every slot back to the main thread. Re-entrant (Fail -> OnSwitch -> CompleteAll while a CompleteAll runs).
        internal static void CompleteAll()
        {
            for (int i = 0; i < P.Count; i++)
            {
                FillSlot<MapWriter> s = P[i];
                if (s.Pending)
                {
                    try { CompleteSlot(s); }
                    catch (Exception ex) { MelonLogger.Error("[MultiCore] take-back of slot " + i + " failed: " + ex); }
                }
                if (i < s_Checks.Length && s_Checks[i] != null) s_Checks[i].Pending = false;
            }
        }

        // Scene change / quit: complete, then drop the slots and dispose our scratch containers.
        internal static void DropAll()
        {
            CompleteAll();
            for (int i = 0; i < s_Checks.Length; i++)
            {
                Check c = s_Checks[i];
                if (c == null) continue;
                try { c.Scratch?.Dispose(); } catch { }
                s_Checks[i] = null;
            }
            if (P.PendingCount == 0) P.Clear();
        }

        internal static void ResetRoundLogs() => s_Logged = 0;

        internal static void Publish(double dt)
        {
            if (Item == null || !Item.Applied) return;
            Item.Info = P.Stats.Publish(dt);
        }

        // ------------------------------------------------------------------ self-check

        static void RunReference(int slot, Team team, int times)
        {
            long t0 = Stopwatch.GetTimestamp();
            if (s_Checks.Length <= slot) Array.Resize(ref s_Checks, Math.Max(slot + 1, s_Checks.Length * 2));
            Check c = s_Checks[slot] ?? (s_Checks[slot] = new Check { Scratch = new TeamJobNativeContainers() });
            int n = team.DetectedTargetIndexes.Count;
            long r0 = Stopwatch.GetTimestamp();
            s_InReference = true;
            try
            {
                c.Scratch.Init(team);   // the original method (our prefix steps aside while s_InReference)
            }
            finally
            {
                s_InReference = false;
            }
            long r1 = Stopwatch.GetTimestamp();
            P.Stats.RefTicks += r1 - r0;
            P.Stats.RefEntries += n;
            c.Pending = true;
            c.Team = team;
            c.Times = times;
            P.Stats.CheckTicks += Stopwatch.GetTimestamp() - t0;
            P.Stats.SnapshotTicks -= Stopwatch.GetTimestamp() - t0;   // not part of the item's normal cost
        }

        static void CompareCheck(FillSlot<MapWriter> s, Check c)
        {
            c.Pending = false;
            FillStats st = P.Stats;
            st.Checks++;
            var cont = (TeamJobNativeContainers)s.Owner;
            string diff = DiffMaps(cont.OwnerTeamDetectedTargetIndexes, c.Scratch.OwnerTeamDetectedTargetIndexes);
            if (diff == null) diff = DiffTimes(cont.OwnerTeamDetectedTargetsTime, c.Scratch.OwnerTeamDetectedTargetsTime, c.Times);
            if (diff != null)
            {
                st.CheckDifferent++;
                if (s_Logged < s_MaxLogs)
                {
                    s_Logged++;
                    MelonLogger.Warning("[MultiCore] SELF-CHECK MISMATCH SensorTeamMaps_Job team " + (c.Team ? c.Team.TeamShortName : "?") + ": " + diff +
                                        (s_Logged == s_MaxLogs ? " (log limit reached for this round; still counted)" : ""));
                }
            }
            else if (LiveDiffers(c.Team, c.Scratch.OwnerTeamDetectedTargetIndexes))
            {
                st.CheckStale++;
            }
            else
            {
                st.CheckIdentical++;
            }
            c.Team = null;
        }

        static string DiffMaps(NativeParallelHashMap<int, int> ours, NativeParallelHashMap<int, int> vanilla)
        {
            if (!ours.IsCreated || !vanilla.IsCreated) return "map not created (ours " + ours.IsCreated + ", vanilla " + vanilla.IsCreated + ")";
            int no = ours.Count(), nv = vanilla.Count();
            if (no != nv) return "count " + no + " vs vanilla " + nv;
            NativeKeyValueArrays<int, int> kv = vanilla.GetKeyValueArrays(Allocator.Temp);
            try
            {
                for (int i = 0; i < kv.Length; i++)
                {
                    int key = kv.Keys[i];
                    if (!ours.TryGetValue(key, out int v)) return "id " + key + " missing";
                    if (v != kv.Values[i]) return "id " + key + ": index " + v + " vs vanilla " + kv.Values[i];
                }
            }
            finally
            {
                kv.Dispose();
            }
            return null;
        }

        static string DiffTimes(NativeArray<float> ours, NativeArray<float> vanilla, int n)
        {
            if (n == 0) return null;
            if (!ours.IsCreated || !vanilla.IsCreated || ours.Length < n || vanilla.Length < n) return "time array size";
            for (int i = 0; i < n; i++)
            {
                if (!ours[i].Equals(vanilla[i])) return "time[" + i + "] " + ours[i] + " vs vanilla " + vanilla[i];
            }
            return null;
        }

        // Would vanilla build a different map from the live dictionary now (Part2) than it did at Init?
        static bool LiveDiffers(Team team, NativeParallelHashMap<int, int> atInit)
        {
            if ((object)team == null) return false;
            Dictionary<Target, int> d = team.DetectedTargetIndexes;
            if (d == null) return true;
            s_Live.Clear();
            foreach (KeyValuePair<Target, int> kv in d) s_Live[kv.Key.GetInstanceID()] = kv.Value;
            if (s_Live.Count != atInit.Count()) return true;
            foreach (KeyValuePair<int, int> kv in s_Live)
            {
                if (!atInit.TryGetValue(kv.Key, out int v) || v != kv.Value) return true;
            }
            return false;
        }
    }
}
