/*
 S6: use Team.DetectedTargetIndexes (Target -> index into Team.DetectedTargets) where 1.0.17 still scans the list.

 Team keeps the dictionary in step with the list in every writer (1.0.17: DetectTarget adds both, LostTarget removes
 with swap-back and re-indexes, OnNetInit / Team.Clear / ctor clear both; no other code writes DetectedTargets), and
 DetectTarget never adds a target that is already a key, so the list has no duplicates. For a non-null Target the list
 scan (EqualityComparer -> UnityEngine.Object.Equals -> instance-id compare) and the dictionary (GetHashCode =
 instance id, same Equals) find the same object. The helpers still verify list[i] == target by reference and fall
 back to the vanilla scan when it is not (never expected); a null Target or a null Team runs the vanilla expression.

 Rewritten call sites (each method has exactly one with this IL shape on 1.0.17):
     <load Team> ; call(virt) Team::get_DetectedTargets ; ldarg.N|ldloc.N ; callvirt List<Target>::IndexOf|Contains
  -> <load Team> ; nop ; ldarg.N|ldloc.N ; call DetectedIndexLookups.IndexOf|Contains(Team, Target)
   Team.LostTarget(Target)             IndexOf   (every team, on every target loss / object destruction)
   Target.GetDetectedByTeam(Team,bool) IndexOf
   Team.GetTargetDetected(Target,bool) Contains  (static, per team)
   AITask_Attack.Think()               Contains  (per attack target per task think)
 Anything else in those bodies is left alone; if the shape count is not 1 the method stays vanilla (NOT APPLIED).

 Not done:
   - BaseGameObject.UpdateVisibility: its IndexOf only runs with a strategy team; on the server
     StrategyMode.CurrentStrategyTeam is null (and Headless.Fog_SkipPresentation skips the only caller).
   - FogOfWar.OnUpdate: already Headless.Fog_DetectedTargetLookup.
   - DamageManager.s_QueueSynchHealth as a HashSet: it is three parallel lists drained by
     NetworkLayer.SendSynchHealth (RemoveAt(0) loop) and edited in three DamageManager methods; a shadow set has to
     follow all five writers for a Contains over at most the distinct damaged objects since the last 0.25 s drain.
   - Target.GetTargetByNetID as a dictionary: NetworkComponent.NetworkComponentsIndexed already maps NetID ->
     component, but vanilla only returns Targets that are in Target.Targets (units in compartments leave it); exact
     equivalence needs a membership index. On the server it runs only when decoding player orders (low rate).
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Silica.AI;

namespace SiServerPerf.Modules.Services
{
    internal static class DetectedIndexLookups
    {
        internal static bool On;
        static PerfItem Item;
        static MethodInfo _getDetected, _helperIndexOf, _helperContains;
        static long _served, _fallbacks, _vanillaPath, _verified, _mismatches;
        static bool _verify;

        public static void Register(ModuleContext ctx, ServicesSettings s)
        {
            _verify = s.VerifyAgainstVanilla;
            Item = ctx.Item("Detected_IndexLookups",
                "DetectedTargets.IndexOf/Contains -> Team.DetectedTargetIndexes in Team.LostTarget, Target.GetDetectedByTeam, Team.GetTargetDetected, AITask_Attack.Think (same result, O(1))",
                v => On = v);
            _getDetected = Refl.Getter(typeof(Team), nameof(Team.DetectedTargets), Item);
            if (_getDetected == null) Item.Block("Team.DetectedTargets absent");
            if (Refl.Getter(typeof(Team), nameof(Team.DetectedTargetIndexes), Item) == null) Item.Block("Team.DetectedTargetIndexes absent");
            _helperIndexOf = AccessTools.Method(typeof(DetectedIndexLookups), nameof(IndexOf));
            _helperContains = AccessTools.Method(typeof(DetectedIndexLookups), nameof(Contains));

            var lost = Item.Target(typeof(Team), nameof(Team.LostTarget), new[] { typeof(Target) });
            var byTeam = Item.Target(typeof(Target), nameof(Target.GetDetectedByTeam), new[] { typeof(Team), typeof(bool) });
            var anyTeam = Item.Target(typeof(Team), nameof(Team.GetTargetDetected), new[] { typeof(Target), typeof(bool) });
            var attack = Item.Target(typeof(AITask_Attack), nameof(AITask_Attack.Think), Type.EmptyTypes, declaredOnly: true);
            Item.Patch(lost, typeof(DetectedIndexLookups), transpiler: nameof(Transpiler));
            Item.Patch(byTeam, typeof(DetectedIndexLookups), transpiler: nameof(Transpiler));
            Item.Patch(anyTeam, typeof(DetectedIndexLookups), transpiler: nameof(Transpiler));
            Item.Patch(attack, typeof(DetectedIndexLookups), transpiler: nameof(Transpiler));
        }

        static bool IsListTargetCall(CodeInstruction ci, string name) =>
            (ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && ci.operand is MethodInfo mi &&
            mi.Name == name && mi.DeclaringType == typeof(List<Target>) && mi.GetParameters().Length == 1;

        static bool IsSimpleLoad(CodeInstruction ci) =>
            ci.opcode == OpCodes.Ldarg_0 || ci.opcode == OpCodes.Ldarg_1 || ci.opcode == OpCodes.Ldarg_2 || ci.opcode == OpCodes.Ldarg_3 ||
            ci.opcode == OpCodes.Ldarg_S || ci.opcode == OpCodes.Ldarg ||
            ci.opcode == OpCodes.Ldloc_0 || ci.opcode == OpCodes.Ldloc_1 || ci.opcode == OpCodes.Ldloc_2 || ci.opcode == OpCodes.Ldloc_3 ||
            ci.opcode == OpCodes.Ldloc_S || ci.opcode == OpCodes.Ldloc;

        // CodeInstruction.labels is List<System.Reflection.Emit.Label> from the net35 mscorlib, which a netstandard2.1
        // build cannot name; read its count through the non-generic interface. Unknown = treat as labelled (no rewrite).
        static readonly FieldInfo _labelsField = AccessTools.Field(typeof(CodeInstruction), "labels");
        static bool HasLabels(CodeInstruction ci)
        {
            try { return !(_labelsField?.GetValue(ci) is System.Collections.ICollection c) || c.Count > 0; }
            catch { return true; }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            string where = original != null ? original.DeclaringType?.Name + "." + original.Name : "?";
            var sites = new List<KeyValuePair<int, MethodInfo>>();
            for (int i = 2; i < list.Count; i++)
            {
                MethodInfo helper = IsListTargetCall(list[i], "IndexOf") ? _helperIndexOf
                                  : IsListTargetCall(list[i], "Contains") ? _helperContains : null;
                if (helper == null) continue;
                if (_getDetected == null || !list[i - 2].Calls(_getDetected) || !IsSimpleLoad(list[i - 1])) continue;
                if (HasLabels(list[i - 1]) || HasLabels(list[i])) continue;
                sites.Add(new KeyValuePair<int, MethodInfo>(i, helper));
            }
            if (sites.Count != 1 || _helperIndexOf == null || _helperContains == null)
            {
                Item.Problem(where + ": expected 1 'get_DetectedTargets; load; IndexOf/Contains' site, found " + sites.Count + " (left vanilla)");
                return list;
            }
            foreach (var s in sites)
            {
                int at = s.Key;
                list[at - 2].opcode = OpCodes.Nop;   // the Team stays on the stack
                list[at - 2].operand = null;
                list[at].opcode = OpCodes.Call;
                list[at].operand = s.Value;
            }
            return list;
        }

        // Replaces team.DetectedTargets.IndexOf(target).
        public static int IndexOf(Team team, Target target)
        {
            if (On && (object)team != null && (object)target != null)
            {
                try
                {
                    Dictionary<Target, int> idx = team.DetectedTargetIndexes;
                    List<Target> list = team.DetectedTargets;
                    if (idx != null && list != null)
                    {
                        int r;
                        if (!idx.TryGetValue(target, out int i)) r = -1;
                        else if (i >= 0 && i < list.Count && ReferenceEquals(list[i], target)) r = i;
                        else { _fallbacks++; return list.IndexOf(target); }
                        _served++;
                        if (_verify)
                        {
                            _verified++;
                            int v = list.IndexOf(target);
                            if (v != r) { _mismatches++; return v; }
                        }
                        return r;
                    }
                }
                catch (Exception ex)
                {
                    Item.Fail(ex, "DetectedTargets IndexOf lookup");
                }
            }
            _vanillaPath++;
            return team.DetectedTargets.IndexOf(target);
        }

        // Replaces team.DetectedTargets.Contains(target).
        public static bool Contains(Team team, Target target)
        {
            if (On && (object)team != null && (object)target != null)
            {
                try
                {
                    Dictionary<Target, int> idx = team.DetectedTargetIndexes;
                    List<Target> list = team.DetectedTargets;
                    if (idx != null && list != null)
                    {
                        bool r;
                        if (!idx.TryGetValue(target, out int i)) r = false;
                        else if (i >= 0 && i < list.Count && ReferenceEquals(list[i], target)) r = true;
                        else { _fallbacks++; return list.Contains(target); }
                        _served++;
                        if (_verify)
                        {
                            _verified++;
                            bool v = list.Contains(target);
                            if (v != r) { _mismatches++; return v; }
                        }
                        return r;
                    }
                }
                catch (Exception ex)
                {
                    Item.Fail(ex, "DetectedTargets Contains lookup");
                }
            }
            _vanillaPath++;
            return team.DetectedTargets.Contains(target);
        }

        internal static void RefreshInfo()
        {
            if (Item == null || !Item.Applied) return;
            Item.Info = "lookups served from index " + _served + ", out-of-step fallbacks " + _fallbacks + ", vanilla path (off/null) " + _vanillaPath +
                        (_verify ? " | VERIFY: checked " + _verified + ", mismatches " + _mismatches : "");
        }
    }
}
