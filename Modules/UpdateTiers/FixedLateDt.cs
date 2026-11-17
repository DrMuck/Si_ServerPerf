/*
 FixedLate_StepDt (cause #13). Still present in 1.0.17 (OrderedUpdateManager unchanged apart from everyTick):
 Physics.simulationMode is FixedUpdate on the server (GameManager.SimulatePhysics returns at once), so the fixed list
 runs from OrderedUpdateManager.FixedUpdate once per Unity fixed step, but that method only sets QueueFixedLateUpdate,
 and the fixed-late list runs ONCE per frame from LateUpdate with Time.fixedTime. A frame with k physics steps gives
 every fixed-late handler one call with dt = k * fixedDeltaTime (k = 2-3 in overloaded frames), after all k steps;
 an entry registered after the frame's last step gets dt = 0 on its first call.

 Fix: prefix on OrderedUpdateManager.FixedUpdate. When a previous step of this frame queued the fixed-late list
 (QueueFixedLateUpdate set), run it now - after that step's physics simulation and before the next step's fixed list,
 i.e. the order GameManager.SimulatePhysics uses in Script mode (fixed, Simulate, fixedLate, fixed, ...) - with
 now = Time.fixedTime - fixedDeltaTime (the time of the step that just finished), then clear the flag. The frame's last
 step is still handled by vanilla LateUpdate. Result: one fixed-late call per physics step, dt = fixedDeltaTime.
 Script mode (not used by the server) is left as it is.

 Consumers of the fixed-late dt in 1.0.17 (all RegisterFixedLateUpdate call sites):
   AIHarvesterAgent.OrderedFixedLateUpdate - docking correction of AI harvesters: CorrectDocking(..., dt) integrates
     DockStuckTime / StuckDockCorrectTime and nudges the rigidbody's angular/linear velocity with MoveTowards(max
     NormalTurnSpeed/NormalSpeed * 5 * dt). With the fix the nudge is applied after every step (dt = step) instead of
     once per frame with the folded dt: same total, applied between steps; frames with dt = 0 no longer skip it.
   ParentingPair.UpdateChildCulling (Sandbox parenting; ignores dt), HideInStrategyMode.OrderedFixedLateUpdate (only
     registered in strategy mode on a client; ignores dt). BaseGameObject.OrderedFixedLateUpdate has no override in
     1.0.17, so no BaseGameObject registers it.
 DEFAULT OFF: no performance gain, and it changes when harvester docking corrections are applied (a behaviour change,
 even if towards the designed one).
*/

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.UpdateTiers
{
    internal static class FixedLateDt
    {
        internal static bool On;
        internal static PerfItem Item;
        static AccessTools.FieldRef<OrderedUpdateManager, bool> s_Queue;
        static FieldInfo s_LateListField;
        static MethodInfo s_Run;
        static readonly object[] s_Args = new object[5];
        static OrderedUpdateManager s_Mgr;
        static object s_List;
        internal static long EarlyRuns;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("FixedLate_StepDt",
                "Fixed-late list once per physics step (run at the next step's FixedUpdate, dt = fixedDeltaTime) instead of once per frame with the folded dt",
                v => On = v, defaultOn: false);
            s_Queue = Refl.Field<OrderedUpdateManager, bool>("QueueFixedLateUpdate", Item);
            s_LateListField = AccessTools.Field(typeof(OrderedUpdateManager), "FixedLateUpdate_List");
            Type listType = TierDispatch.ListType ?? AccessTools.Inner(typeof(OrderedUpdateManager), "UpdateList");
            s_Run = listType == null ? null : AccessTools.DeclaredMethod(listType, "Run", new[] { typeof(float), typeof(bool), typeof(long), typeof(float), typeof(int) });
            if (s_Queue == null || s_LateListField == null || s_Run == null)
            {
                Item.Block("OrderedUpdateManager.QueueFixedLateUpdate / FixedLateUpdate_List / UpdateList.Run absent");
            }
            var m = Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes, declaredOnly: true);
            Item.Patch(m, typeof(FixedLateDt), prefix: nameof(Prefix));
        }

        static void Prefix(OrderedUpdateManager __instance)
        {
            if (!On) return;
            try
            {
                if (UnityEngine.Physics.simulationMode != UnityEngine.SimulationMode.FixedUpdate) return;   // vanilla's Physics.autoSimulation
                ref bool queued = ref s_Queue(__instance);
                if (!queued) return;
                if (!ReferenceEquals(s_Mgr, __instance))
                {
                    s_List = s_LateListField.GetValue(__instance);
                    s_Mgr = __instance;
                }
                if (s_List == null) return;
                queued = false;
                s_Args[0] = Time.fixedTime - Time.fixedDeltaTime;
                s_Args[1] = false;
                s_Args[2] = 0L;
                s_Args[3] = 0f;
                s_Args[4] = 1;
                s_Run.Invoke(s_List, s_Args);
                EarlyRuns++;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "OrderedUpdateManager.FixedUpdate prefix");
            }
        }
    }
}
