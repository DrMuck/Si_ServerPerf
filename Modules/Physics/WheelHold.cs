/*
 ParkedWheelHold (default OFF, needs IdleForceSleep): keeps a force-slept wheeled vehicle asleep across steps.

 Cause (b11 FixedWakeTrace: ~99.99 % of re-wakes inside VehicleWheeled.OrderedFixedUpdate, changed transforms = the
 four steering wheels Wheel_L_01/L_02/R_01/R_02, root unchanged), 1.0.17 VehicleWheeled.cs:
   - The watch/IdleSleepDiag only test the CHASSIS at the start of the step. A steering wheel can be awake while the
     chassis sleeps. OrderedFixedUpdate then skips only SLEEPING wheels (:1336-1344); an awake wheel is processed:
       :1345  joint.rotationDriveMode = XYAndZ   (SleepVehicle set Slerp, :570) - a ConfigurableJoint drive change,
              which reconfigures the PhysX joint and wakes the bodies it connects, i.e. the chassis
       :1380  brake velocity write, :1399 angularVelocity write on the wheel
       :1428-1439 ApplyLowTickWheelFix (on for the LightStriker): rigidBodyTransform.rotation = ... every step for
              an awake steering wheel - the changed transforms the trace lists.
   - IdleForceSleep then finds chassis + wheel awake and calls SleepVehicle (Sleep() on all bodies). The steering
     wheel's rotation written at :1438 is synced into PhysX only right before the simulate step (autoSyncTransforms
     is off), i.e. AFTER that Sleep(): the transform sync re-wakes the wheel, the chassis stays asleep, and the next
     step starts again with an awake steering wheel under a sleeping chassis. Only steering wheels take the :1438
     write, which is why only they show up; non-steering wheels stay asleep.
 Fix, two parts, both on OrderedUpdateManager.FixedUpdate:
   1. prefix (before the ordered fixed list): for every vehicle IdleForceSleep holds whose chassis is asleep and that
      is still idle (IdleTest: AI, no move order, no input, engine 0, not kinematic/hop/force-move/flipped), every
      awake jointed wheel that is slow (IdleWheelLinearSpeed / IdleWheelAngularSpeed) and has no pending force or
      torque is put back to sleep. VehicleWheeled.OrderedFixedUpdate then skips it as vanilla does for any sleeping
      wheel, so the joint write, the velocity writes and the :1438 rotation write do not happen.
   2. postfix, before IdleForceSleep's (priority First): if any held vehicle has an awake body that IdleForceSleep will
      re-sleep this step (still idle, no pending force; b18 - b12 synced every step even when nothing was re-slept), one
      Physics.SyncTransforms() before the re-sleeps, so transform writes made earlier in this step reach PhysX before
      Sleep() instead of after it. Unity performs the same sync before every simulate anyway; this only moves it
      earlier in the same step (writes made after it are synced at simulate as usual).
 Legitimate wakes are unchanged: an order / input sets VehicleWheeled's own flag4 -> WakeUp of chassis and all wheels
 (:1305-1317, :1336) in the same step, after this prefix; a hit, explosion or push leaves a pending force or a
 velocity above the limits, so the wheel is left awake and IdleForceSleep releases the vehicle at the end of the step
 (wheelFast / speed); a player entering makes it non-AI (IdleTest fails, nothing is touched). A sleeping body is not
 integrated, so a held vehicle on a slope keeps its pose; on a real wake it starts from zero velocity with the
 vanilla brakes, as before.
   [Physics] wheelhold heldVehicles= wheelsSlept= leftAwakeFast= leftAwakeForce= syncs=   (per log interval)
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class ParkedWheelHold
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;
        static long _held, _slept, _fast, _force, _syncs, _syncSkipped;
        static float _nextLog;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("ParkedWheelHold",
                "With IdleForceSleep: re-sleep slow awake wheels of a held vehicle before VehicleWheeled runs (stops the steering-wheel " +
                "joint/transform writes that re-wake it) and sync transforms once before the end-of-step re-sleep",
                v => On = v, defaultOn: false);
            var fu = Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes);
            Item.Patch(fu, typeof(ParkedWheelHold), prefix: nameof(Pre));
            Item.Patch(fu, typeof(ParkedWheelHold), postfix: nameof(Post), priority: HarmonyLib.Priority.First);
            ctx.OnUpdate(Tick, Item);
        }

        static void Pre()
        {
            if (!On || !IdleForceSleep.On) return;
            try
            {
                float dt = Time.fixedDeltaTime;
                List<Unit> forced = IdleForceSleep.ForcedUnits;
                for (int i = 0; i < forced.Count; i++)
                {
                    if (!(forced[i] is VehicleWheeled w) || !w) continue;
                    Rigidbody main = w.RigidBody;
                    if (!main || !main.IsSleeping()) continue;
                    if (IdleTest.Vehicle(w, 1000f, -1f, engine: true) != IdleWhy.Ok) continue;
                    List<VehicleWheelSetup> wheels = w.Wheels;
                    if (wheels == null) continue;
                    bool any = false;
                    for (int k = 0; k < wheels.Count; k++)
                    {
                        VehicleWheelSetup ws = wheels[k];
                        if (ws == null || !ws.Joint) continue;
                        Rigidbody rb = ws.RigidBody;
                        if (!rb || rb.isKinematic || rb.IsSleeping()) continue;
                        if (!Vel.Slow(rb, S.IdleWheelLinearSpeed, S.IdleWheelAngularSpeed)) { _fast++; continue; }
                        if (rb.GetAccumulatedForce(dt).sqrMagnitude > 0f || rb.GetAccumulatedTorque(dt).sqrMagnitude > 0f) { _force++; continue; }
                        rb.Sleep();
                        _slept++;
                        any = true;
                    }
                    if (any) _held++;
                }
            }
            catch (Exception ex) { Item.Fail(ex, "OrderedUpdateManager.FixedUpdate prefix"); }
        }

        static void Post()
        {
            if (!On || !IdleForceSleep.On) return;
            try
            {
                List<Unit> forced = IdleForceSleep.ForcedUnits;
                for (int i = 0; i < forced.Count; i++)
                {
                    Unit u = forced[i];
                    if (!u || !(u is VehicleWheeled)) continue;
                    Rigidbody[] bodies = Census.BodiesOf(u);
                    for (int k = 0; k < bodies.Length; k++)
                    {
                        Rigidbody rb = bodies[k];
                        if (rb && !rb.isKinematic && !rb.IsSleeping())
                        {
                            // b12: one sync per step even with nothing to re-sleep (awake body on a vehicle that is
                            // released or force-blocked this step). Sync only when IdleForceSleep will re-sleep it.
                            if (!IdleForceSleep.WillReSleep(u)) { _syncSkipped++; break; }
                            UnityEngine.Physics.SyncTransforms();
                            _syncs++;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex) { Item.Fail(ex, "OrderedUpdateManager.FixedUpdate postfix"); }
        }

        static void Tick()
        {
            var s = PhysicsModule.S;
            float now = Time.realtimeSinceStartup;
            if (s == null || s.CensusLogSeconds <= 0f || now < _nextLog) return;
            bool first = _nextLog == 0f;
            _nextLog = now + s.CensusLogSeconds;
            if (!first)
            {
                Perf.Log("[Physics] wheelhold heldVehicles=" + _held + " wheelsSlept=" + _slept + " leftAwakeFast=" + _fast +
                         " leftAwakeForce=" + _force + " syncs=" + _syncs + " syncSkipped=" + _syncSkipped);
                Item.Info = "last interval: " + _slept + " wheels re-slept before VehicleWheeled, " + _syncs + " early syncs";
            }
            _held = _slept = _fast = _force = _syncs = _syncSkipped = 0;
        }
    }
}
