/*
 Interpolation_Off (default OFF, presentation: headless only). Rigidbody interpolation only smooths the Transform
 between fixed steps for rendering; on a server it is a per-frame pose write for every interpolated awake body.
 1.0.17: prefabs ship Interpolate on soldier and vehicle bodies; Unit.OnPostInit switches it off, then
 Unit.UpdateRigidBodyInterpolationAll (GameManager.Update) re-enables it for units within 800 m of the server's
 camera position, and Soldier.OnExitCompartment sets Interpolate unconditionally (:3886) - with Si_ServerOptimizer
 stripping UpdateRigidBodyInterpolationAll, every soldier that ever left a vehicle keeps it (findings D, F5).
 Census counts interpolated bodies ("int=").

 Patches: prefix Unit.EnableRigidBodyInterpolation / Vehicle.EnableRigidBodyInterpolation forcing enable = false;
 postfix Soldier.OnExitCompartment setting the soldier body back to None. Switch-on sweeps all units once.
 Difference to vanilla: game code that reads transform.position in Update sees the last fixed-step pose instead of
 the interpolated one (at most one step, 40 ms of motion, behind). Physics itself is unaffected.
 Switch-off: nothing to restore (vanilla re-enables where it wants on its next pass).
*/

using System;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class InterpolationOff
    {
        internal static bool On;
        static PerfItem Item;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("Interpolation_Off",
                "No Rigidbody interpolation on the headless server (Unit/Vehicle.EnableRigidBodyInterpolation forced off, " +
                "Soldier.OnExitCompartment reset to None)",
                v => On = v, defaultOn: false, flags: ItemFlags.Presentation);
            var b = new[] { typeof(bool) };
            Item.Patch(Item.Target(typeof(Unit), "EnableRigidBodyInterpolation", b, declaredOnly: true), typeof(InterpolationOff), prefix: nameof(ForceOff));
            Item.Patch(Item.Target(typeof(Vehicle), "EnableRigidBodyInterpolation", b, declaredOnly: true), typeof(InterpolationOff), prefix: nameof(ForceOff));
            Item.Patch(Item.Target(typeof(Soldier), "OnExitCompartment", new[] { typeof(UnitCompartment) }, declaredOnly: true),
                       typeof(InterpolationOff), postfix: nameof(PostExitCompartment));
            Item.OnSwitch(on => { if (on) Sweep(); });
        }

        static void ForceOff(ref bool enable)
        {
            if (On) enable = false;
        }

        static void PostExitCompartment(Soldier __instance)
        {
            if (!On) return;
            try
            {
                Rigidbody rb = __instance.RigidBody;
                if (rb && rb.interpolation != RigidbodyInterpolation.None) rb.interpolation = RigidbodyInterpolation.None;
            }
            catch (Exception ex) { Item.Fail(ex, "Soldier.OnExitCompartment postfix"); }
        }

        static void Sweep()
        {
            int n = 0;
            try
            {
                var units = Unit.Units;
                if (units == null) return;
                for (int i = 0; i < units.Count; i++)
                {
                    Unit u = units[i];
                    if (!u) continue;
                    Rigidbody[] bodies = Census.BodiesOf(u);
                    for (int k = 0; k < bodies.Length; k++)
                    {
                        Rigidbody rb = bodies[k];
                        if (rb && rb.interpolation != RigidbodyInterpolation.None)
                        {
                            rb.interpolation = RigidbodyInterpolation.None;
                            n++;
                        }
                    }
                }
                Item.Info = "last sweep: " + n + " bodies set to no interpolation";
            }
            catch (Exception ex) { Item.Fail(ex, "interpolation sweep"); }
        }
    }
}
