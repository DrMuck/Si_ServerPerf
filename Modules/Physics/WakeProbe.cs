/*
 WakeProbe (default OFF, read-only, ~2 IsSleeping() calls per probed call): which method wakes bodies.

 Prefix + postfix on the per-step / per-frame methods that write to unit rigidbodies. Each call reads the unit's main
 body IsSleeping() before and after: "woke" = asleep before, awake after (a velocity/force write, WakeUp, impulse);
 "slept" = the reverse (the game's own Sleep() calls). Writes that only wake at the next transform sync (transform
 position/rotation writes; synced right before PhysX simulates) are NOT visible here; they show up in the Census wS
 column instead. ApplyAvoidanceForces is probed on the body it pushes (mainRB), which is usually another unit.

 Log line every CensusLogSeconds (interval sums):
   [Physics] wakeprobe <label> calls=.. woke=.. slept=.. ; ...
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class WakeProbe
    {
        internal static bool On;
        static PerfItem Item;
        static readonly Dictionary<int, int> _slotByToken = new Dictionary<int, int>();
        static readonly List<string> _labels = new List<string>();
        static long[] _calls = new long[0], _woke = new long[0], _slept = new long[0];
        static int _avoidSlot = -1;
        static float _nextLog, _lastLog;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("WakeProbe",
                "Diagnostic: IsSleeping() of the unit body before/after the soldier, creature, wheeled/hover vehicle, anti-flip and " +
                "avoidance fixed/frame updates; logs woke/slept counts per method every CensusLogSeconds",
                v => On = v, defaultOn: false);
            var f = new[] { typeof(float) };
            Probe(typeof(Soldier), "OrderedFixedUpdate", f, "Soldier.OrderedFixedUpdate");
            Probe(typeof(Soldier), "OrderedFixedUpdatePartTwo", f, "Soldier.PartTwo");
            Probe(typeof(Soldier), "OrderedFixedUpdatePartThree", f, "Soldier.PartThree");
            Probe(typeof(Soldier), "OrderedUpdate", f, "Soldier.OrderedUpdate(frame)");
            Probe(typeof(CreatureDecapod), "OrderedFixedUpdate", f, "Creature.OrderedFixedUpdate");
            Probe(typeof(CreatureDecapod), "OrderedUpdate", f, "Creature.OrderedUpdate(frame)");
            Probe(typeof(VehicleWheeled), "OrderedFixedUpdate", f, "Wheeled.OrderedFixedUpdate");
            Probe(typeof(VehicleWheeled), "OrderedFixedUpdatePartTwo", f, "Wheeled.PartTwo");
            Probe(typeof(VehicleWheeled), "OrderedUpdate", f, "Wheeled.OrderedUpdate(frame)");
            Probe(typeof(VehicleHovered), "OrderedFixedUpdate", f, "Hover.OrderedFixedUpdate");
            Probe(typeof(VehicleAntiFlip), "OrderedFixedUpdate", f, "AntiFlip.OrderedFixedUpdate");
            Probe(typeof(CollisionAvoidance), "OrderedFixedUpdate", f, "Avoidance.OrderedFixedUpdate(self)");

            // Soldier.PartTwo can wake and re-sleep the body inside one call (a write wakes it, the early return at :2875
            // is then missed, the full path runs and ends in Sleep() again): invisible to before/after. m_JumpDelay is
            // only touched on the full path (:2889 / Jump), so "asleep at entry and exit, m_JumpDelay changed" = that case.
            _jumpDelay = Refl.Field<Soldier, float>("m_JumpDelay", Item);
            var two = AccessTools.DeclaredMethod(typeof(Soldier), "OrderedFixedUpdatePartTwo", f);
            if (two != null && _jumpDelay != null)
            {
                _wokeResleptSlot = AddSlot("Soldier.PartTwo full path although asleep at entry and exit (woken + re-slept inside)");
                Item.Patch(two, typeof(PartTwoProbe), prefix: nameof(PartTwoProbe.TwoPre), postfix: nameof(PartTwoProbe.TwoPost));
            }

            var aaf = Item.Target(typeof(CollisionAvoidance), "ApplyAvoidanceForces", AvoidanceIdlePairs.ApplyArgs);
            if (aaf != null)
            {
                _avoidSlot = AddSlot("Avoidance.ApplyAvoidanceForces(pushed body)");
                Item.Patch(aaf, typeof(WakeProbe), prefix: nameof(AvoidPre), postfix: nameof(AvoidPost));
            }
            ctx.OnUpdate(Tick, Item);
        }

        static int AddSlot(string label)
        {
            _labels.Add(label);
            int n = _labels.Count;
            Array.Resize(ref _calls, n);
            Array.Resize(ref _woke, n);
            Array.Resize(ref _slept, n);
            return n - 1;
        }

        static void Probe(Type type, string name, Type[] args, string label)
        {
            MethodBase m = Item.Target(type, name, args, declaredOnly: true);
            if (m == null) return;
            _slotByToken[m.MetadataToken] = AddSlot(label);
            Item.Patch(m, typeof(WakeProbe), prefix: nameof(Pre), postfix: nameof(Post));
        }

        static Rigidbody BodyOf(object inst)
        {
            if (inst is BaseGameObject b) return b.RigidBody;
            if (inst is CollisionAvoidance ca) { Unit u = ca.Unit; return u ? u.RigidBody : null; }
            if (inst is VehicleAntiFlip af) { Vehicle v = af.Vehicle; return v ? v.RigidBody : null; }
            return null;
        }

        // __state: 0 = not sampled, 1 = awake before, 2 = asleep before
        static void Pre(object __instance, out byte __state)
        {
            __state = 0;
            if (!On) return;
            try
            {
                Rigidbody rb = BodyOf(__instance);
                if (rb && !rb.isKinematic) __state = rb.IsSleeping() ? (byte)2 : (byte)1;
            }
            catch (Exception ex) { Item.Fail(ex, "probe prefix"); }
        }

        static void Post(object __instance, MethodBase __originalMethod, byte __state)
        {
            if (!On || __state == 0) return;
            try
            {
                if (!_slotByToken.TryGetValue(__originalMethod.MetadataToken, out int slot)) return;
                Rigidbody rb = BodyOf(__instance);
                if (!rb) return;
                Count(slot, __state, rb.IsSleeping());
            }
            catch (Exception ex) { Item.Fail(ex, "probe postfix"); }
        }

        static HarmonyLib.AccessTools.FieldRef<Soldier, float> _jumpDelay;
        static int _wokeResleptSlot = -1;

        // Own class: Harmony shares __state between all prefixes/postfixes of one patch class on one method; Pre/Post
        // (byte) and these (float) on the same method in one class gave "IL Compile Error" (lab b4).
        internal static class PartTwoProbe
        {
        // __state: NaN = not asleep at entry (or not sampled), else m_JumpDelay at entry
        internal static void TwoPre(Soldier __instance, out float __state)
        {
            __state = float.NaN;
            if (!On) return;
            try
            {
                Rigidbody rb = __instance.RigidBody;
                if (rb && !rb.isKinematic && rb.IsSleeping()) __state = _jumpDelay(__instance);
            }
            catch (Exception ex) { Item.Fail(ex, "PartTwo probe prefix"); }
        }

        internal static void TwoPost(Soldier __instance, float __state)
        {
            if (!On || float.IsNaN(__state)) return;
            try
            {
                Rigidbody rb = __instance.RigidBody;
                if (!rb) return;
                _calls[_wokeResleptSlot]++;
                if (rb.IsSleeping() && _jumpDelay(__instance) != __state) _woke[_wokeResleptSlot]++;
            }
            catch (Exception ex) { Item.Fail(ex, "PartTwo probe postfix"); }
        }
        }

        static void AvoidPre(Rigidbody mainRB, out byte __state)
        {
            __state = 0;
            if (!On) return;
            try { if (mainRB && !mainRB.isKinematic) __state = mainRB.IsSleeping() ? (byte)2 : (byte)1; }
            catch (Exception ex) { Item.Fail(ex, "avoidance probe prefix"); }
        }

        static void AvoidPost(Rigidbody mainRB, byte __state)
        {
            if (!On || __state == 0) return;
            try { if (mainRB) Count(_avoidSlot, __state, mainRB.IsSleeping()); }
            catch (Exception ex) { Item.Fail(ex, "avoidance probe postfix"); }
        }

        static void Count(int slot, byte before, bool asleepNow)
        {
            _calls[slot]++;
            if (before == 2 && !asleepNow) _woke[slot]++;
            else if (before == 1 && asleepNow) _slept[slot]++;
        }

        static void Tick()
        {
            var s = PhysicsModule.S;
            if (s == null || s.CensusLogSeconds <= 0f) return;
            float now = Time.realtimeSinceStartup;
            if (now < _nextLog) return;
            if (_lastLog > 0f) WriteLog();
            _lastLog = now;
            _nextLog = now + s.CensusLogSeconds;
            Array.Clear(_calls, 0, _calls.Length);
            Array.Clear(_woke, 0, _woke.Length);
            Array.Clear(_slept, 0, _slept.Length);
        }

        static void WriteLog()
        {
            var sb = new StringBuilder(512);
            long totalWoke = 0;
            for (int i = 0; i < _labels.Count; i++)
            {
                if (_calls[i] == 0) continue;
                if (sb.Length > 0) sb.Append(" ; ");
                sb.Append(_labels[i]).Append(" calls=").Append(_calls[i]).Append(" woke=").Append(_woke[i]).Append(" slept=").Append(_slept[i]);
                totalWoke += _woke[i];
            }
            if (sb.Length == 0) return;
            Item.Info = "last interval: " + totalWoke + " wakes inside probed methods";
            Perf.Log("[Physics] wakeprobe " + sb);
        }
    }
}
