/*
 Same-value write guards. Both items replace property-setter calls in game methods with helpers that skip the write
 only when the new value is bit-for-bit what the property already holds, so every property keeps exactly the value
 vanilla would give it. What changes is only the side effect of the write itself: PhysX/Unity reset the wake counter
 (or wake the body) on velocity writes and re-pose a body whose Transform was written, even with the same value.

 RestWriteGuards (default OFF). Rest-state writers that run even while the body is asleep or at rest:
   Soldier.OrderedFixedUpdate         RigidBody.detectCollisions x1 (crouch probe)
   Soldier.OrderedFixedUpdatePartTwo  RigidBody.detectCollisions x3 (:2835 runs every step, also for a sleeping
                                      soldier, before the IsSleeping early return), RigidBody.useGravity x1 (:2867,
                                      every step, unguarded; CreatureDecapod guards the same write itself)
   Soldier.OrderedFixedUpdatePartThree RigidBody.detectCollisions x2, Transform.position x1 (:3149 ground snap)
   CreatureDecapod.OrderedFixedUpdate RigidBody.linearVelocity x1 (:3713: zero every step while burrowed),
                                      RigidBody.useGravity x2
 Wake paths stay the game's own: Soldier wakes itself on input or ground body (:2875-2882), the creature on input
 (:3765-3772); PhysX wakes on contact with awake bodies, forces, impulses and real (changed) writes.

 HitZonePoseGuard (default OFF). The kinematic hit-zone Rigidbody sits on HumanModel (soldier) / SwarmBugModel
 (creature); the game re-poses that transform every frame:
   Soldier.OrderedUpdate          HumanModel.transform.rotation x2 (:3260, :3264)
   CreatureDecapod.OrderedUpdate  Transform.rotation x2 (SwarmBugModel :4179, submerge decal), SwarmBugModel
                                  localPosition x1 (:4180)
 A written Transform under a Rigidbody is synced to PhysX before the next simulate (teleport of the kinematic body).
 rotation is a world-space setter (local = inverse(parent) * value), so the guard keeps, per transform, the value it
 last wrote, the localRotation that produced and the parent's world rotation; the write is skipped only when all
 three are bitwise unchanged, i.e. Unity would compute the identical localRotation. localPosition is compared
 directly. Nothing in the game reads Transform.hasChanged (checked 1.0.17).

 Transpilers: exact call-site counts from the 1.0.17 IL; any other count -> Item.Problem, method left unchanged.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class GuardIL
    {
        public static readonly MethodInfo SetDetect = AccessTools.PropertySetter(typeof(Rigidbody), nameof(Rigidbody.detectCollisions));
        public static readonly MethodInfo SetGravity = AccessTools.PropertySetter(typeof(Rigidbody), nameof(Rigidbody.useGravity));
        public static readonly MethodInfo SetLinVel = AccessTools.PropertySetter(typeof(Rigidbody), nameof(Rigidbody.linearVelocity));
        public static readonly MethodInfo SetPos = AccessTools.PropertySetter(typeof(Transform), nameof(Transform.position));
        public static readonly MethodInfo SetRot = AccessTools.PropertySetter(typeof(Transform), nameof(Transform.rotation));
        public static readonly MethodInfo SetLocalPos = AccessTools.PropertySetter(typeof(Transform), nameof(Transform.localPosition));

        // Replace every call to each setter in `swap` with its helper, but only if the number of call sites matches
        // `expected` exactly; otherwise report and return the instructions unchanged.
        public static List<CodeInstruction> Swap(IEnumerable<CodeInstruction> instructions, PerfItem item, string where,
                                                 params (MethodInfo setter, int expected, MethodInfo helper)[] swap)
        {
            var list = new List<CodeInstruction>(instructions);
            var found = new int[swap.Length];
            for (int i = 0; i < list.Count; i++)
            {
                for (int s = 0; s < swap.Length; s++)
                {
                    if (swap[s].setter != null && list[i].Calls(swap[s].setter)) found[s]++;
                }
            }
            for (int s = 0; s < swap.Length; s++)
            {
                if (swap[s].setter == null || swap[s].helper == null || found[s] != swap[s].expected)
                {
                    item.Problem(where + ": expected " + swap[s].expected + " call(s) to " + (swap[s].setter?.Name ?? "?") +
                                 ", found " + found[s] + "; left unchanged");
                    return list;
                }
            }
            for (int i = 0; i < list.Count; i++)
            {
                for (int s = 0; s < swap.Length; s++)
                {
                    if (list[i].Calls(swap[s].setter))
                    {
                        list[i].opcode = OpCodes.Call;
                        list[i].operand = swap[s].helper;
                        break;
                    }
                }
            }
            return list;
        }

        public static MethodInfo H(Type t, string name) => AccessTools.Method(t, name);
    }

    internal static class RestWriteGuards
    {
        internal static bool On;
        static PerfItem Item;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("RestWriteGuards",
                "Skip same-value writes of detectCollisions/useGravity/position (Soldier fixed updates) and linearVelocity/useGravity " +
                "(CreatureDecapod.OrderedFixedUpdate) so resting bodies are not re-woken; values identical to vanilla",
                v => On = v, defaultOn: false);
            var f = new[] { typeof(float) };
            Item.Patch(Item.Target(typeof(Soldier), "OrderedFixedUpdate", f, declaredOnly: true), typeof(RestWriteGuards), transpiler: nameof(TSoldierFixed));
            Item.Patch(Item.Target(typeof(Soldier), "OrderedFixedUpdatePartTwo", f, declaredOnly: true), typeof(RestWriteGuards), transpiler: nameof(TSoldierTwo));
            Item.Patch(Item.Target(typeof(Soldier), "OrderedFixedUpdatePartThree", f, declaredOnly: true), typeof(RestWriteGuards), transpiler: nameof(TSoldierThree));
            Item.Patch(Item.Target(typeof(CreatureDecapod), "OrderedFixedUpdate", f, declaredOnly: true), typeof(RestWriteGuards), transpiler: nameof(TCreatureFixed));
        }

        static readonly Type T = typeof(RestWriteGuards);

        static IEnumerable<CodeInstruction> TSoldierFixed(IEnumerable<CodeInstruction> ins) =>
            GuardIL.Swap(ins, Item, "Soldier.OrderedFixedUpdate", (GuardIL.SetDetect, 1, GuardIL.H(T, nameof(DetectCollisions))));

        static IEnumerable<CodeInstruction> TSoldierTwo(IEnumerable<CodeInstruction> ins) =>
            GuardIL.Swap(ins, Item, "Soldier.OrderedFixedUpdatePartTwo",
                (GuardIL.SetDetect, 3, GuardIL.H(T, nameof(DetectCollisions))),
                (GuardIL.SetGravity, 1, GuardIL.H(T, nameof(UseGravity))));

        static IEnumerable<CodeInstruction> TSoldierThree(IEnumerable<CodeInstruction> ins) =>
            GuardIL.Swap(ins, Item, "Soldier.OrderedFixedUpdatePartThree",
                (GuardIL.SetDetect, 2, GuardIL.H(T, nameof(DetectCollisions))),
                (GuardIL.SetPos, 1, GuardIL.H(T, nameof(Position))));

        static IEnumerable<CodeInstruction> TCreatureFixed(IEnumerable<CodeInstruction> ins) =>
            GuardIL.Swap(ins, Item, "CreatureDecapod.OrderedFixedUpdate",
                (GuardIL.SetLinVel, 1, GuardIL.H(T, nameof(LinearVelocity))),
                (GuardIL.SetGravity, 2, GuardIL.H(T, nameof(UseGravity))));

        // ---- helpers called from the transpiled IL: same stack shape as the setter they replace

        public static void DetectCollisions(Rigidbody rb, bool value)
        {
            if (On && (object)rb != null)
            {
                try
                {
                    if (rb.detectCollisions == value) { Counters.GuardSkips++; return; }
                }
                catch (Exception ex) { Item.Fail(ex, "detectCollisions guard"); }
            }
            Counters.GuardWrites++;
            rb.detectCollisions = value;
        }

        public static void UseGravity(Rigidbody rb, bool value)
        {
            if (On && (object)rb != null)
            {
                try
                {
                    if (rb.useGravity == value) { Counters.GuardSkips++; return; }
                }
                catch (Exception ex) { Item.Fail(ex, "useGravity guard"); }
            }
            Counters.GuardWrites++;
            rb.useGravity = value;
        }

        public static void LinearVelocity(Rigidbody rb, Vector3 value)
        {
            if (On && (object)rb != null)
            {
                try
                {
                    if (Vel.Same(rb.linearVelocity, value)) { Counters.GuardSkips++; return; }
                }
                catch (Exception ex) { Item.Fail(ex, "linearVelocity guard"); }
            }
            Counters.GuardWrites++;
            rb.linearVelocity = value;
        }

        public static void Position(Transform t, Vector3 value)
        {
            if (On && (object)t != null)
            {
                try
                {
                    if (Vel.Same(t.position, value)) { Counters.GuardSkips++; return; }
                }
                catch (Exception ex) { Item.Fail(ex, "position guard"); }
            }
            Counters.GuardWrites++;
            t.position = value;
        }
    }

    internal static class HitZonePoseGuard
    {
        internal static bool On;
        static PerfItem Item;

        struct Pose
        {
            public Quaternion Value, Local, Parent;
        }

        static readonly Dictionary<int, Pose> _last = new Dictionary<int, Pose>();

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("HitZonePoseGuard",
                "Skip the per-frame re-pose of HumanModel / SwarmBugModel (carry the kinematic hit-zone body) when the pose is " +
                "bitwise unchanged; hit-zone capsules, aim points and self-hit exclusion unchanged",
                v => On = v, defaultOn: false);
            var f = new[] { typeof(float) };
            Item.Patch(Item.Target(typeof(Soldier), "OrderedUpdate", f, declaredOnly: true), typeof(HitZonePoseGuard), transpiler: nameof(TSoldier));
            Item.Patch(Item.Target(typeof(CreatureDecapod), "OrderedUpdate", f, declaredOnly: true), typeof(HitZonePoseGuard), transpiler: nameof(TCreature));
            Item.OnSwitch(_ => _last.Clear());
            ctx.OnSceneLoaded(_ => _last.Clear());
        }

        static readonly Type T = typeof(HitZonePoseGuard);

        static IEnumerable<CodeInstruction> TSoldier(IEnumerable<CodeInstruction> ins) =>
            GuardIL.Swap(ins, Item, "Soldier.OrderedUpdate", (GuardIL.SetRot, 2, GuardIL.H(T, nameof(Rotation))));

        static IEnumerable<CodeInstruction> TCreature(IEnumerable<CodeInstruction> ins) =>
            GuardIL.Swap(ins, Item, "CreatureDecapod.OrderedUpdate",
                (GuardIL.SetRot, 2, GuardIL.H(T, nameof(Rotation))),
                (GuardIL.SetLocalPos, 1, GuardIL.H(T, nameof(LocalPosition))));

        public static void Rotation(Transform t, Quaternion value)
        {
            if (On && (object)t != null)
            {
                try
                {
                    int id = t.GetInstanceID();
                    Transform parent = t.parent;
                    Quaternion parentRot = parent ? parent.rotation : Quaternion.identity;
                    if (_last.TryGetValue(id, out Pose p) && Vel.Same(p.Value, value) && Vel.Same(p.Parent, parentRot) &&
                        Vel.Same(p.Local, t.localRotation))
                    {
                        Counters.PoseSkips++;
                        return;
                    }
                    Counters.PoseWrites++;
                    t.rotation = value;
                    if (_last.Count > 8192) _last.Clear();
                    _last[id] = new Pose { Value = value, Local = t.localRotation, Parent = parentRot };
                    return;
                }
                catch (Exception ex) { Item.Fail(ex, "rotation guard"); }
            }
            t.rotation = value;
        }

        public static void LocalPosition(Transform t, Vector3 value)
        {
            if (On && (object)t != null)
            {
                try
                {
                    if (Vel.Same(t.localPosition, value)) { Counters.PoseSkips++; return; }
                }
                catch (Exception ex) { Item.Fail(ex, "localPosition guard"); }
            }
            Counters.PoseWrites++;
            t.localPosition = value;
        }
    }
}
