/*
 Fog of war on the headless server (findings I §2).

 Fog_SkipPresentation (S5): FogOfWar.ShowFog runs every frame from OnUpdate. On the server GetCurrentFogTeam() is
 null and InStrategyMode is false, so the texture/decal/minimap part does nothing and Shown stays false; what remains
 is BaseGameObject.UpdateVisibilityAll(), which only sets cosmetic visibility flags. Skipped, every object keeps its
 all-visible defaults. FogOfWar.UpdateFogPixelBlend schedules one blend job per team per frame whose output only
 feeds FogTexture, which the server never creates (LateUpdate then completes an empty handle list).
 Kept: the rest of OnUpdate (explore pass, UpdateTargetFogPositionVisibleCache), UpdateFogPixelData, LateUpdate's
 explore completion, GetPositionVisible, the explored-runs join sync. Never prefix OnUpdate as a whole.

 Fog_DetectedTargetLookup (S6): OnUpdate asks value.Team.DetectedTargets.IndexOf(baseGameObject.Target) for every
 object and team. Team keeps DetectedTargetIndexes (Target -> index) in step with DetectedTargets (DetectTarget,
 LostTarget with swap-back, Clear), so the transpiler swaps that call for a dictionary lookup.
 1.0.17 IL at the call site:  ldfld FogOfWarData::Team; callvirt Team::get_DetectedTargets; ldloc v7;
 callvirt BaseGameObject::get_Target; callvirt List<Target>::IndexOf. The get_DetectedTargets call is dropped (nop)
 and IndexOf becomes ByTeam(Team, Target). If that exact shape is not there, IndexOf becomes ByList(List, Target)
 (0.8.0's variant: finds the owning team by reference). Anything else: NOT APPLIED.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SiServerPerf.Modules.Headless
{
    internal static class FogPresentation
    {
        internal static bool On;
        static PerfItem Item;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("Fog_SkipPresentation",
                "Skip FogOfWar.ShowFog (+ its UpdateVisibilityAll sweep) and FogOfWar.UpdateFogPixelBlend; explore pass, pixel data and GetPositionVisible keep running",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(Item);
            var show = Item.Target(typeof(FogOfWar), nameof(FogOfWar.ShowFog), new[] { typeof(bool) });
            var blend = Item.Target(typeof(FogOfWar), nameof(FogOfWar.UpdateFogPixelBlend), new[] { typeof(float) });
            Item.Patch(show, typeof(FogPresentation), prefix: nameof(Prefix));
            Item.Patch(blend, typeof(FogPresentation), prefix: nameof(Prefix));
        }

        static bool Prefix() => !On;
    }

    internal static class FogDetectedLookup
    {
        internal static bool On;
        static PerfItem Item;
        static MethodInfo _getDetected;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("Fog_DetectedTargetLookup",
                "FogOfWar.OnUpdate: DetectedTargets.IndexOf -> Team.DetectedTargetIndexes lookup (same result, O(1))",
                v => On = v);
            ServerOptimizerCompat.Apply(Item);
            _getDetected = Refl.Getter(typeof(Team), nameof(Team.DetectedTargets), Item);
            if (Refl.Getter(typeof(Team), nameof(Team.DetectedTargetIndexes), Item) == null) Item.Block("Team.DetectedTargetIndexes absent");
            var m = Item.Target(typeof(FogOfWar), nameof(FogOfWar.OnUpdate), Type.EmptyTypes);
            Item.Patch(m, typeof(FogDetectedLookup), transpiler: nameof(Transpiler));
        }

        static bool IsIndexOf(CodeInstruction ci) =>
            (ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && ci.operand is MethodInfo mi &&
            mi.Name == nameof(List<Target>.IndexOf) && mi.DeclaringType == typeof(List<Target>) && mi.GetParameters().Length == 1;

        static bool IsLdloc(CodeInstruction ci) =>
            ci.opcode == OpCodes.Ldloc || ci.opcode == OpCodes.Ldloc_S || ci.opcode == OpCodes.Ldloc_0 || ci.opcode == OpCodes.Ldloc_1 ||
            ci.opcode == OpCodes.Ldloc_2 || ci.opcode == OpCodes.Ldloc_3;

        static bool IsTargetGetter(CodeInstruction ci) =>
            (ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && ci.operand is MethodInfo mi &&
            mi.ReturnType == typeof(Target) && mi.GetParameters().Length == 0 && !mi.IsStatic;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var hits = new List<int>();
            for (int i = 0; i < list.Count; i++) if (IsIndexOf(list[i])) hits.Add(i);
            if (hits.Count != 1)
            {
                Item.Problem("expected 1 List<Target>.IndexOf call in FogOfWar.OnUpdate, found " + hits.Count);
                return list;
            }
            int at = hits[0];
            bool teamShape = at >= 3 && _getDetected != null && list[at - 3].Calls(_getDetected) && IsLdloc(list[at - 2]) && IsTargetGetter(list[at - 1]);
            if (teamShape)
            {
                // [Team] get_DetectedTargets -> keep the Team on the stack
                list[at - 3].opcode = OpCodes.Nop;
                list[at - 3].operand = null;
                list[at].opcode = OpCodes.Call;
                list[at].operand = AccessTools.Method(typeof(FogDetectedLookup), nameof(ByTeam));
            }
            else
            {
                list[at].opcode = OpCodes.Call;
                list[at].operand = AccessTools.Method(typeof(FogDetectedLookup), nameof(ByList));
                Item.Note("call-site shape differs from 1.0.17, using the list-owner lookup");
            }
            return list;
        }

        // Replaces team.DetectedTargets.IndexOf(target).
        public static int ByTeam(Team team, Target target)
        {
            if (On)
            {
                try
                {
                    if ((object)team != null && (object)target != null)
                    {
                        Dictionary<Target, int> idx = team.DetectedTargetIndexes;
                        List<Target> list = team.DetectedTargets;
                        if (idx != null && list != null)
                        {
                            if (!idx.TryGetValue(target, out int i)) return -1;
                            if (i >= 0 && i < list.Count && ReferenceEquals(list[i], target)) return i;
                            return list.IndexOf(target); // dictionary out of step (not expected): vanilla answer
                        }
                    }
                }
                catch (Exception ex)
                {
                    Item.Fail(ex, "DetectedTargets lookup");
                }
            }
            return team.DetectedTargets.IndexOf(target);
        }

        // Replaces list.IndexOf(target) when only the list is on the stack (0.8.0 variant).
        public static int ByList(List<Target> list, Target target)
        {
            if (On)
            {
                try
                {
                    if ((object)target != null && list != null)
                    {
                        List<Team> teams = GameDatabase.Database != null ? Team.Teams : null;
                        if (teams != null)
                        {
                            for (int t = 0; t < teams.Count; t++)
                            {
                                Team team = teams[t];
                                if ((object)team == null || !ReferenceEquals(team.DetectedTargets, list)) continue;
                                Dictionary<Target, int> idx = team.DetectedTargetIndexes;
                                if (idx == null) break;
                                if (!idx.TryGetValue(target, out int i)) return -1;
                                if (i >= 0 && i < list.Count && ReferenceEquals(list[i], target)) return i;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Item.Fail(ex, "DetectedTargets lookup");
                }
            }
            return list.IndexOf(target);
        }
    }
}
