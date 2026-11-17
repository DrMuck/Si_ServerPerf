/*
 AvoidanceNoIdleDetour (default OFF): an AI unit with no destination does not "detour" around a neighbour.

 Vanilla (1.0.17), the parked-vehicle shuffle:
   CollisionAvoidance.HandlePhysicsAvoidancePushing (:474-475) takes vector = Unit.GetAIMoveToDestination(), which
   is Vector3.zero for a unit that is not following a path, and vector2 = (vector - position).normalized = the
   direction to the WORLD ORIGIN. For every penetrating pair with an equal-or-heavier neighbour ahead in the same
   heading (:548) it compares sides against that direction (:550-555) and may set Avoid = true,
   SpeedLimit = TopSpeed, DetourPoint = neighbour.forward * 50 + position (:557-559). OrderedFixedUpdate then
   returns for State == Idle (:748-751) and leaves that in place.
   VehicleWheeled.UpdateAIInput (every 0.25 s, :777-1089) sees Avoid with a DetourPoint (:802-805), takes the
   driving branch toward a point 50 m ahead, and with no path AgentRemainingPathLength = 0 and a stopped vehicle
   num12 = 0/0 = NaN, so `!(num12 > 1)` drives (:994-1004), capped only by SpeedLimit = TopSpeed (:1062-1073).
   Once it moves, num12 = +inf and it brakes (:1000-1003). Result: parked AI vehicles whose avoidance capsules
   overlap lurch forward and brake, push into the next capsule, and so on; ForwardBack != 0 keeps them awake.
 This item (postfix on HandlePhysicsAvoidancePushing): if the agent's unit is AI-controlled, has no force-move and
 GetAIMoveToDestination() is zero, the Avoid / DetourPoint / SpeedLimit that the pass produced are reset to the values
 OrderedFixedUpdate set right before it (Avoid false, DetourPoint zero, SpeedLimit TopSpeed). The impulses of the
 pass are not touched (AvoidanceSkipOntoResting handles those). Units with a path, force-moves and player units:
 unchanged. Visible difference: parked AI vehicles no longer drive themselves forward to resolve overlapping
 avoidance capsules; a parked group stays where it stopped instead of jostling apart. When an order arrives the
 unit has a destination and avoidance is vanilla again.
   [Physics] idledetour passes= reset= (interval)
*/

using System;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class AvoidanceNoIdleDetour
    {
        internal static bool On;
        static PerfItem Item;
        static Action<CollisionAvoidance, bool> _setAvoid;
        static Action<CollisionAvoidance, Vector3> _setDetour;
        static Action<CollisionAvoidance, float> _setSpeedLimit;
        static long _passes, _resets;
        static float _nextLog;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("AvoidanceNoIdleDetour",
                "CollisionAvoidance: an AI unit with no destination gets no Avoid/DetourPoint (vanilla aims the detour test at the " +
                "world origin and parked vehicles then drive toward a point 50 m ahead); impulses and units with a path unchanged",
                v => On = v, defaultOn: false);
            try
            {
                _setAvoid = Setter<bool>("Avoid");
                _setDetour = Setter<Vector3>("DetourPoint");
                _setSpeedLimit = Setter<float>("SpeedLimit");
            }
            catch (Exception ex) { Item.Note("CollisionAvoidance setters: " + ex.Message); }
            if (_setAvoid == null || _setDetour == null || _setSpeedLimit == null) Item.Block("CollisionAvoidance Avoid/DetourPoint/SpeedLimit setters absent");
            var push = Item.Target(typeof(CollisionAvoidance), "HandlePhysicsAvoidancePushing", new[] { typeof(float) });
            Item.Patch(push, typeof(AvoidanceNoIdleDetour), postfix: nameof(Post));
            ctx.OnUpdate(Tick, Item);
        }

        static Action<CollisionAvoidance, T> Setter<T>(string prop)
        {
            var m = HarmonyLib.AccessTools.PropertySetter(typeof(CollisionAvoidance), prop);
            return m == null ? null : (Action<CollisionAvoidance, T>)Delegate.CreateDelegate(typeof(Action<CollisionAvoidance, T>), m);
        }

        static void Post(CollisionAvoidance __instance)
        {
            if (!On) return;
            try
            {
                _passes++;
                if (!__instance.Avoid) return;
                Unit u = __instance.Unit;
                if (!u || !u.AIControlled || (u is Vehicle v && v.HasForceMove)) return;
                if (u.GetAIMoveToDestination() != Vector3.zero) return;
                _setAvoid(__instance, false);
                _setDetour(__instance, Vector3.zero);
                _setSpeedLimit(__instance, u.TopSpeed);
                _resets++;
            }
            catch (Exception ex) { Item.Fail(ex, "HandlePhysicsAvoidancePushing postfix"); }
        }

        static void Tick()
        {
            var s = PhysicsModule.S;
            float now = Time.realtimeSinceStartup;
            if (s == null || s.CensusLogSeconds <= 0f || now < _nextLog) return;
            bool first = _nextLog == 0f;
            _nextLog = now + s.CensusLogSeconds;
            if (!first && _passes > 0)
            {
                Perf.Log("[Physics] idledetour passes=" + _passes + " reset=" + _resets);
                Item.Info = "last interval: " + _resets + " idle detours reset";
            }
            _passes = _resets = 0;
        }
    }
}
