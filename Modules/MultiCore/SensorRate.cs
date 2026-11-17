/*
 SensorPipeline_Rate (S7 "fixed rate", APPROXIMATE, default off): the sensor pipeline runs every
 Settings.SensorPipelineIntervalMs (default 50 ms = 20 Hz) instead of every frame.

 The pipeline (1.0.17) = three ordered-update entries of GameManager:
   SensorTickPart1 (prio 1000)  Sensor.EnsureSensorJobsCompleted; TargetGrid.Update (Target.ActuallyDeleteTargets,
                                UpdateDataForJobs = the O(targets) marshal, LOS-position job, cell ranges, (teams+1) x
                                cells fill jobs); Sensor.TickSensors (CurrentNumTick = ceil(Time.deltaTime x sensors),
                                team maps, per-sensor job schedule)
   SensorTickPart2 (prio -1)    per ticked sensor: complete its jobs, raycast batch, Team.DetectTarget for the cheap hits
   SensorTickPart3 (prio -3000) per ticked sensor: complete raycasts, Team.DetectTarget for the clear rays
 Patches: prefix + postfix on all three GameManager methods (skip / time), transpiler on Sensor.TickSensors (its one
 Time.deltaTime read -> the game time since the previous pipeline run while this item drives it). On a skipped frame
 none of the three runs; Part2 and Part3 run exactly on the frames Part1 ran (vanilla also gates them on the
 containers' LastCompleteStage, so even a mismatch would be a no-op). Every job the pipeline schedules is completed
 inside its own run frame (Part2 / Part3), so nothing is in flight on a skipped frame.

 When a run happens (RateGate in Kernel.cs):
   - first frame after scene load, round start and runtime switch-on (forced);
   - any frame with Target deletions pending (Target.TargetsToDelete non-empty). A destroyed Target stays in
     Target.Targets with Owner = null until TargetGrid.Update's ActuallyDeleteTargets; the grid's cell lists still
     index it, and CheckRiskOfFriendlyFire (and Targeting's FriendlyFire_Grid) dereference target.Owner. Running on
     those frames keeps that window exactly as long as vanilla's (end of the frame of death -> prio 1000 next frame);
   - otherwise when the interval has elapsed.
 Game.IsLoading or Time.deltaTime <= 0: vanilla (it returns at once anyway).

 What reads the pipeline's output between runs, and what it gets
   TargetGrid arrays (TargetDataNativeArray, LOSPositions, CellRanges, TeamGridCellIndices): only the sensor jobs
     (inside a run) and Target.CheckRiskOfFriendlyFire / CheckRiskOfHittingEnemies (VehicleTurret, TurretWeapon,
     HumanHandsAnimator, CreatureAttack, every frame while aligned). Between runs they are the last run's data:
     never half-built (written only inside TargetGrid.Update on the main thread), indices valid (Target.Targets only
     appends between deletion passes; new targets are simply not in the grid yet). Friendly positions in the
     friendly-fire test are up to one interval + one frame old (vanilla: same frame).
   Targeting.FriendlyFire_Grid: its snapshot is bumped in a postfix on TargetGrid.Update, which is not called on a
     skipped frame, so it keeps the last snapshot - built from the same (equally old) vanilla data. Exactness vs
     vanilla-with-this-item holds.
   Team detections: refreshed only by Part2/Part3 (and OmniSensor, unaffected). Team.TickTeams decays them every frame
     as before. Sensors still tick once per second each (accumulated dt), but in bursts.
   LineOfSightManager, fog of war, concealment: do not read the pipeline's arrays (fog writes FogPositionVisibleCache,
     which UpdateDataForJobs reads at the next run).
   SensorTeamMaps_Job: its producer runs inside TickSensors and its take-back in TickSensorsPart2, i.e. only on run
     frames; nothing of it is in flight on a skipped frame.
 Worst-case added latency (interval I, frame time F): first detection of an enemy entering range +I+F (vanilla:
 up to 1 s, the sensor's own period); target lost: the refresh moves by up to I+F, so the 8 s decay ends up to I+F
 earlier or later; friendly-fire / per-frame TargetGrid readers: data up to I+F old; a target killed but not destroyed
 (wreck) or disabled stays in the grid up to I+F. Destroyed targets: no change (forced run).
 Units a human controls or sits in: the pipeline is global (one grid, one round-robin over Sensor.Sensors, team-level
 detection); there is no per-unit path that could stay per-frame.

 Switch-off: the next frame is vanilla again (every frame, Time.deltaTime). Switch-on: forced run, accumulator reset.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.MultiCore
{
    internal static class SensorRate
    {
        internal static bool On;
        internal static PerfItem Item;

        static RateGate s_Gate;
        static float s_TickDt;
        static bool s_InRun;
        static int s_RanFrame = -1;
        static AccessTools.FieldRef<List<Target>> s_ToDelete;
        static long s_Part1Ticks, s_Part23Ticks;
        static long p_Frames, p_Runs, p_Forced, p_Deletion, p_Part1, p_Part23;
        static double s_TickDtSum, p_TickDtSum;

        public static void Register(ModuleContext ctx, MultiCoreSettings s)
        {
            float interval = Mathf.Clamp(s.SensorPipelineIntervalMs, 1f, 1000f) / 1000f;
            s_Gate.Arm(interval);

            Item = ctx.Item("SensorPipeline_Rate",
                "APPROXIMATE S7: the sensor pipeline (SensorTickPart1/2/3: target-data marshal, TargetGrid rebuild, sensor jobs) runs every SensorPipelineIntervalMs instead of every frame (plus on frames with target deletions); sensors keep their 1 s period via accumulated dt",
                v => On = v, defaultOn: false);

            s_ToDelete = Refl.StaticField<List<Target>>(typeof(Target), "TargetsToDelete", Item);
            var p1 = Item.Target(typeof(GameManager), "SensorTickPart1", new[] { typeof(float) }, declaredOnly: true);
            var p2 = Item.Target(typeof(GameManager), "SensorTickPart2", new[] { typeof(float) }, declaredOnly: true);
            var p3 = Item.Target(typeof(GameManager), "SensorTickPart3", new[] { typeof(float) }, declaredOnly: true);
            var tick = Item.Target(typeof(Sensor), nameof(Sensor.TickSensors), Type.EmptyTypes, declaredOnly: true);
            if (s_ToDelete == null || p1 == null || p2 == null || p3 == null || tick == null)
            {
                Item.Block("a required sensor-pipeline member is absent on this build");
            }

            // dt first: without it skipping would cut the sensor throughput, so no skip without it. Part2/3 gates next,
            // the Part1 skip last.
            bool dtOk = Item.Patch(tick, typeof(SensorRate), transpiler: nameof(TickSensorsTranspiler));
            bool gates = dtOk && Item.Patch(p2, typeof(SensorRate), prefix: nameof(Part23Prefix), postfix: nameof(Part23Postfix));
            gates = gates && Item.Patch(p3, typeof(SensorRate), prefix: nameof(Part23Prefix), postfix: nameof(Part23Postfix));
            if (gates)
            {
                Item.Patch(p1, typeof(SensorRate), prefix: nameof(Part1Prefix), postfix: nameof(Part1Postfix));
            }
            else if (Item.Active)
            {
                Item.Note("TickSensors dt transpiler or a Part2/Part3 gate not applied: Part1 skip not installed (vanilla runs every frame)");
            }
            Item.OnSwitch(on => { if (on) s_Gate.Arm(s_Gate.Interval); s_InRun = false; });
            Item.Note("interval " + (interval * 1000f).ToString("0", CultureInfo.InvariantCulture) + " ms");
            ctx.OnSceneLoaded(_ => s_Gate.Force = true);
            ctx.OnRoundStart(() => s_Gate.Force = true);
        }

        // ------------------------------------------------------------------ gates

        static bool Part1Prefix(out long __state)
        {
            __state = 0;
            if (!On) return true;
            try
            {
                int frame = Time.frameCount;
                float dt = Time.deltaTime;
                if (dt <= 0f || Game.IsLoading)
                {
                    s_RanFrame = frame;   // vanilla path (returns at once); Part2/3 follow it
                    return true;
                }
                List<Target> del = s_ToDelete();
                if (!s_Gate.Step(dt, del != null && del.Count > 0, out float tickDt)) return false;
                s_TickDt = tickDt;
                s_TickDtSum += tickDt;
                s_InRun = true;
                s_RanFrame = frame;
                __state = Stopwatch.GetTimestamp();
                return true;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "GameManager.SensorTickPart1 prefix");
                s_InRun = false;
                return true;
            }
        }

        static void Part1Postfix(long __state)
        {
            s_InRun = false;
            if (__state != 0) s_Part1Ticks += Stopwatch.GetTimestamp() - __state;
        }

        static bool Part23Prefix(out long __state)
        {
            __state = 0;
            if (!On) return true;
            if (s_RanFrame != Time.frameCount) return false;   // Part1 skipped this frame: Part2/3 skip with it
            __state = Stopwatch.GetTimestamp();
            return true;
        }

        static void Part23Postfix(long __state)
        {
            if (__state != 0) s_Part23Ticks += Stopwatch.GetTimestamp() - __state;
        }

        // ------------------------------------------------------------------ accumulated dt for the sensor round-robin

        // Sensor.TickSensors: `float deltaTime = Time.deltaTime;` -> `float deltaTime = SensorRate.TickDt();`
        static IEnumerable<CodeInstruction> TickSensorsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            MethodInfo getDt = AccessTools.PropertyGetter(typeof(Time), nameof(Time.deltaTime));
            MethodInfo helper = AccessTools.Method(typeof(SensorRate), nameof(TickDt));
            int hit = -1, hits = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if ((list[i].opcode == OpCodes.Call || list[i].opcode == OpCodes.Callvirt) && list[i].operand is MethodInfo mi && mi == getDt)
                {
                    hit = i;
                    hits++;
                }
            }
            if (hits != 1 || getDt == null || helper == null)
            {
                Item.Problem("Sensor.TickSensors: expected exactly one Time.deltaTime read, found " + hits);
                return list;
            }
            list[hit].opcode = OpCodes.Call;   // keep the instruction object (labels / blocks)
            list[hit].operand = helper;
            return list;
        }

        // The game time since the previous pipeline run while this item drives the pipeline, else Time.deltaTime.
        static float TickDt()
        {
            if (On && s_InRun) return s_TickDt;
            return Time.deltaTime;
        }

        // ------------------------------------------------------------------ status

        internal static void Publish(double dt)
        {
            if (Item == null || !Item.Applied || !(dt > 0)) return;
            long frames = s_Gate.Frames - p_Frames, runs = s_Gate.Runs - p_Runs, forced = s_Gate.RunsForced - p_Forced, del = s_Gate.RunsDeletion - p_Deletion;
            double part1 = (s_Part1Ticks - p_Part1) * FillStats.TicksToMs / dt, part23 = (s_Part23Ticks - p_Part23) * FillStats.TicksToMs / dt;
            double dtSum = s_TickDtSum - p_TickDtSum;
            p_Frames = s_Gate.Frames; p_Runs = s_Gate.Runs; p_Forced = s_Gate.RunsForced; p_Deletion = s_Gate.RunsDeletion;
            p_Part1 = s_Part1Ticks; p_Part23 = s_Part23Ticks; p_TickDtSum = s_TickDtSum;
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(200);
            sb.Append("frames/s ").Append((frames / dt).ToString("0", ci))
              .Append(" | pipeline runs/s ").Append((runs / dt).ToString("0.0", ci))
              .Append(" (on deletions ").Append((del / dt).ToString("0.0", ci)).Append(", forced ").Append(forced).Append(')')
              .Append(" | mean dt per run ").Append(runs > 0 ? (dtSum / runs * 1000.0).ToString("0.0", ci) : "-").Append(" ms")
              .Append(" | time in pipeline: part1 ").Append(part1.ToString("0.00", ci)).Append(" ms/s, part2+3 ").Append(part23.ToString("0.00", ci)).Append(" ms/s");
            Item.Info = sb.ToString();
        }
    }
}
