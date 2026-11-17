#if MULTICORE_TRANSPILER_CHECK
// See TranspilerCheck.csproj. Args: <server_Managed dir> <MelonLoader net6 dir> <built Si_ServerPerf.dll>. Exit 0 = OK.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;

static class TranspilerCheckProgram
{
    static int s_Fail, s_Skipped;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  OK    " : "  FAIL  ") + what);
        if (!ok) s_Fail++;
    }

    static int Main(string[] a)
    {
        string managed = a[0], ml = a[1], mod = a[2];
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            foreach (var dir in new[] { ml, managed })
            {
                string f = Path.Combine(dir, name.Name + ".dll");
                if (File.Exists(f)) return ctx.LoadFromAssemblyPath(f);
            }
            return null;
        };
        Assembly core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "SilicaCore.dll"));
        Assembly perf = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(mod));
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        Type tg = core.GetType("TargetGrid"), sensor = core.GetType("Sensor"), team = core.GetType("Team"), tjc = core.GetType("TeamJobNativeContainers");
        Type stm = perf.GetType("SiServerPerf.Modules.MultiCore.SensorTeamMaps");
        MethodInfo M(Type t, string n, params Type[] args) => t.GetMethod(n, Any, null, args, null);
        MethodInfo P(string n) => stm.GetMethod(n, Any);

        Console.WriteLine("[A] DetectWithJobsPart1 transpiler on the 1.0.17 IL");
        MethodInfo part1 = M(tg, "DetectWithJobsPart1", sensor);
        MethodInfo tr = P("Part1Transpiler");
        List<CodeInstruction> orig = PatchProcessor.GetOriginalInstructions(part1);
        var outList = ((IEnumerable<CodeInstruction>)tr.Invoke(null, new object[] { orig.Select(i => new CodeInstruction(i)).ToList() })).ToList();
        int diffs = 0, at = -1;
        for (int i = 0, j = 0; i < orig.Count && j < outList.Count; i++, j++)
        {
            if (orig[i].opcode == outList[j].opcode && Equals(orig[i].operand, outList[j].operand)) continue;
            diffs++;
            at = j;
            Console.WriteLine("  changed at " + i + ": " + orig[i] + "\n       -> " + outList[j] + "\n          " + outList[j + 1]);
            j++;
        }
        Check(outList.Count == orig.Count + 1 && diffs == 1, "exactly one site changed, one instruction added (" + orig.Count + " -> " + outList.Count + ")");
        Check(at >= 0 && outList[at].opcode == OpCodes.Ldarg_0 && outList[at + 1].operand is MethodInfo h && h.Name == "ScheduleSensorCommands",
              "ldarg.0 (sensor) + call ScheduleSensorCommands replace the vanilla Schedule<CreateSensorCommandsJob>");

        Console.WriteLine("[B] every patch of MultiCore.SensorTeamMaps_Job installed through Harmony");
        var harmony = new Harmony("MultiCore.TranspilerCheck");
        void Patch(MethodInfo target, string kind, string name)
        {
            try
            {
                var hm = new HarmonyMethod(P(name));
                if (kind == "prefix") harmony.Patch(target, prefix: hm);
                else if (kind == "postfix") harmony.Patch(target, postfix: hm);
                else harmony.Patch(target, transpiler: hm);
                Check(Harmony.GetPatchInfo(target) != null, kind + " " + name + " on " + target.DeclaringType.Name + "." + target.Name);
            }
            catch (Exception ex)
            {
                string msg = ex.GetBaseException().Message;
                if (msg.Contains("ECall methods must be packaged into a system module"))
                {
                    // CoreCLR refuses to JIT the ORIGINAL (Harmony pins it before detouring) because it calls a Unity
                    // internal call directly; a limit of this harness, not of the patch (Mono has the icalls).
                    s_Skipped++;
                    Console.WriteLine("  SKIP  " + kind + " " + name + " on " + target.DeclaringType.Name + "." + target.Name + ": original not JIT-able outside Unity (" + msg + ")");
                    return;
                }
                Check(false, kind + " " + name + " on " + target?.DeclaringType?.Name + "." + target?.Name + ": " + msg);
            }
        }
        Patch(M(sensor, "ClearSensorNativeArrays"), "prefix", "ClearPrefix");
        Patch(M(tjc, "Dispose"), "prefix", "DisposePrefix");
        Patch(M(sensor, "TickSensorsPart2"), "prefix", "Part2Prefix");
        Patch(part1, "transpiler", "Part1Transpiler");
        Patch(M(sensor, "UpdateNativeContainerSizes"), "postfix", "SizesPostfix");
        Patch(M(tjc, "Init", team), "prefix", "InitPrefix");
        List<CodeInstruction> now = PatchProcessor.GetCurrentInstructions(part1);
        Check(now.Count(i => i.operand is MethodInfo m && m.Name == "ScheduleSensorCommands") == 1 &&
              now.Count(i => i.operand is MethodInfo m && m.Name == "Schedule" && m.IsGenericMethod && m.GetGenericArguments()[0].Name == "CreateSensorCommandsJob") == 0,
              "patched DetectWithJobsPart1 calls the helper once and the vanilla Schedule no more");
        Console.WriteLine("[C] MultiCore.SensorPipeline_Rate: Sensor.TickSensors dt transpiler + GameManager gates");
        Type sr = perf.GetType("SiServerPerf.Modules.MultiCore.SensorRate");
        Type gm = core.GetType("GameManager");
        MethodInfo tick = M(sensor, "TickSensors");
        MethodInfo trDt = sr.GetMethod("TickSensorsTranspiler", Any);
        List<CodeInstruction> o2 = PatchProcessor.GetOriginalInstructions(tick);
        var t2 = ((IEnumerable<CodeInstruction>)trDt.Invoke(null, new object[] { o2.Select(i => new CodeInstruction(i)).ToList() })).ToList();
        int changed = 0; string what = "";
        for (int i = 0; i < Math.Min(o2.Count, t2.Count); i++)
        {
            if (o2[i].opcode == t2[i].opcode && Equals(o2[i].operand, t2[i].operand)) continue;
            changed++; what = o2[i] + " -> " + t2[i];
        }
        Console.WriteLine("  " + what);
        Check(t2.Count == o2.Count && changed == 1 && what.Contains("get_deltaTime") && what.Contains("TickDt"), "exactly the one Time.deltaTime read replaced by SensorRate.TickDt");
        void PatchSr(MethodInfo target, string kind, string name)
        {
            stm = sr;
            Patch(target, kind, name);
        }
        Type keep = stm;
        PatchSr(tick, "transpiler", "TickSensorsTranspiler");
        foreach (string part in new[] { "SensorTickPart1", "SensorTickPart2", "SensorTickPart3" })
        {
            MethodInfo pm = M(gm, part, typeof(float));
            string pre = part == "SensorTickPart1" ? "Part1Prefix" : "Part23Prefix", post = part == "SensorTickPart1" ? "Part1Postfix" : "Part23Postfix";
            PatchSr(pm, "prefix", pre);
            PatchSr(pm, "postfix", post);
        }
        stm = keep;
        Console.WriteLine((s_Fail == 0 ? "TRANSPILER CHECK OK" : s_Fail + " FAILED") + (s_Skipped > 0 ? " (" + s_Skipped + " patch(es) not installable outside Unity, see SKIP)" : ""));
        return s_Fail;
    }
}
#endif
