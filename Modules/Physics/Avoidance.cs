/*
 Avoidance push items (both default OFF).

 Vanilla (Silica.AI.CollisionAvoidance, 1.0.17): every AI unit with a CollisionAvoidance component adds a trigger
 capsule 1.1x its physical extents (CreateAvoidanceCollider :359-394). CollisionAvoidanceManager overlaps those
 capsules (broadphase every 4th step per agent, narrowphase every step). HandlePhysicsAvoidancePushing (:455-569) then,
 for every penetrating pair, calls ApplyAvoidanceForces twice per agent - first on the neighbour (:540), then on
 itself (:545) - and once more before the pair loop when the agent is off the navmesh (:487). ApplyAvoidanceForces
 (:266-357) does rigidbody.ApplyForce(Impulse) on every body of a vehicle (wheels included) and, depending on the
 side, SetAngularVelocity (a velocity write: wakes the body without leaving an accumulated force). Nothing in it
 looks at whether either unit is moving, so parked vehicles with overlapping capsules push each other forever and
 a sleeping vehicle in such a pair is woken every step.

 Call roles, recovered per call (AvoidRoles): neighbour call: receiver = the `vehicle` argument (the neighbour),
 pusher = the agent; self call: receiver = the agent, pusher = the neighbour of the immediately preceding neighbour
 call of the same agent (checked by otherTotalMass == neighbour.TotalMass); off-navmesh call: receiver = agent, no
 pusher (shape: onNavmeshEdge, forwardDot = otherDirDot = -1, :487).

 AvoidanceIdlePairSkip: skips a pair (both calls) only when BOTH vehicles are idle (IdleTest.Vehicle: AI, no move
 order, no input, not kinematic/hop/force-move/flipped, chassis < AvoidanceIdleSpeed). Lab b7: only 11 % of calls
 skipped; its log line now says why the rest were not (first failing test, for agent and neighbour separately):
   [Physics] avoidpairs calls= skip= offNav= selfUnmatched= | missSelf <why>=n ... | missOther <why>=n ...

 AvoidanceSkipOntoResting (new): asymmetric. Skips one call when the RECEIVER is idle (same test) and the PUSHER is
 not moving (chassis linear speed < AvoidanceIdleSpeed) and not player-controlled; whatever the pusher's own order
 or stuck state is. The push the other way (onto a non-idle pusher) still runs, so a stuck vehicle still gets its
 nudge; a parked vehicle is only pushed by something that actually moves. Off-navmesh return pushes always run.
 Physical collisions are untouched (a moving body touching a sleeping one wakes it in PhysX).
   [Physics] avoidresting calls= skip= offNav= unknownPusher= keptPlayer= keptMoving= | receiverNotIdle <why>=n ...
*/

using System;
using System.Text;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal enum PushKind : byte { None, Neighbour, Self, OffNav, SelfUnmatched }

    internal static class AvoidRoles
    {
        // ApplyAvoidanceForces(Vehicle vehicle, Rigidbody mainRB, float turnSpeed, float totalMass, float otherTotalMass,
        //   Vector3 otherPositionLocal, Quaternion rotation, float forwardDot, Vector3 targetForward, Vector3 pushDir,
        //   float otherDirDot, float forceScale, float dt, bool onNavmeshEdge, Vector3 toNavmeshDir)
        internal static readonly Type[] ApplyArgs =
        {
            typeof(Vehicle), typeof(Rigidbody), typeof(float), typeof(float), typeof(float), typeof(Vector3), typeof(Quaternion),
            typeof(float), typeof(Vector3), typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(Vector3),
        };

        // lastOther: per caller, the neighbour of the last neighbour call (reset by the caller per agent pass).
        public static PushKind Resolve(CollisionAvoidance inst, Vehicle vehicle, Rigidbody mainRB, float otherTotalMass,
                                       bool onNavmeshEdge, float forwardDot, float otherDirDot, ref Unit lastOther,
                                       out Unit receiver, out Unit pusher)
        {
            receiver = null;
            pusher = null;
            Unit self = inst.Unit;
            if (!self || !mainRB) { lastOther = null; return PushKind.None; }
            if (mainRB != self.RigidBody)
            {
                receiver = vehicle ? (Unit)vehicle : mainRB.GetComponent<Unit>();
                pusher = self;
                lastOther = receiver;
                return PushKind.Neighbour;
            }
            receiver = self;
            if (onNavmeshEdge && forwardDot == -1f && otherDirDot == -1f && otherTotalMass == self.TotalMass && !lastOther)
            {
                return PushKind.OffNav;
            }
            Unit other = lastOther;
            lastOther = null;
            if (other && otherTotalMass == other.TotalMass)
            {
                pusher = other;
                return PushKind.Self;
            }
            return PushKind.SelfUnmatched;
        }

        public static bool PusherResting(Unit p, float speed)
        {
            if (!p || p.IsDestroyed) return true;
            if (p.PlayerControlled) return false;
            Rigidbody rb = p.RigidBody;
            if (!rb) return true;
            return rb.linearVelocity.sqrMagnitude <= speed * speed;
        }
    }

    internal static class AvoidanceIdlePairs
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;

        internal static Type[] ApplyArgs => AvoidRoles.ApplyArgs;

        // Pair skipped on the neighbour call; the next self call of the same agent is skipped too.
        static CollisionAvoidance _pendingAgent;
        static Unit _pendingOther;
        static Unit _lastOther;

        static readonly long[] _missSelf = new long[(int)IdleWhy.Count], _missOther = new long[(int)IdleWhy.Count];
        static long _calls, _skips, _offNav, _unmatched;
        static float _nextLog;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("AvoidanceIdlePairSkip",
                "CollisionAvoidance: no avoidance impulses / turn writes between two idle AI vehicles (both directions); " +
                "off-navmesh return push and any pair with a moving or player unit unchanged",
                v => On = v, defaultOn: false);
            var push = Item.Target(typeof(CollisionAvoidance), "HandlePhysicsAvoidancePushing", new[] { typeof(float) });
            var apply = Item.Target(typeof(CollisionAvoidance), "ApplyAvoidanceForces", ApplyArgs);
            if (push != null && apply != null)
            {
                Item.Patch(push, typeof(AvoidanceIdlePairs), prefix: nameof(PushPrefix));
                Item.Patch(apply, typeof(AvoidanceIdlePairs), prefix: nameof(ApplyPrefix));
            }
            else
            {
                Item.Block("CollisionAvoidance.HandlePhysicsAvoidancePushing / ApplyAvoidanceForces absent");
            }
            Item.OnSwitch(_ => Clear());
            ctx.OnUpdate(Tick, Item);
        }

        static void PushPrefix()
        {
            Clear();
        }

        static void Clear()
        {
            _pendingAgent = null;
            _pendingOther = null;
            _lastOther = null;
        }

        static bool ApplyPrefix(CollisionAvoidance __instance, Vehicle vehicle, Rigidbody mainRB, float otherTotalMass,
                                bool onNavmeshEdge, float forwardDot, float otherDirDot)
        {
            if (!On) return true;
            try
            {
                Counters.AvoidCalls++;
                _calls++;
                PushKind kind = AvoidRoles.Resolve(__instance, vehicle, mainRB, otherTotalMass, onNavmeshEdge, forwardDot, otherDirDot,
                                                   ref _lastOther, out Unit receiver, out Unit pusher);
                switch (kind)
                {
                    case PushKind.Neighbour:
                    {
                        _pendingAgent = null;
                        _pendingOther = null;
                        IdleWhy ws = IdleTest.Vehicle(pusher, S.AvoidanceIdleSpeed, -1f, false);
                        IdleWhy wo = IdleTest.Vehicle(receiver, S.AvoidanceIdleSpeed, -1f, false);
                        if (ws != IdleWhy.Ok) _missSelf[(int)ws]++;
                        if (wo != IdleWhy.Ok) _missOther[(int)wo]++;
                        if (ws != IdleWhy.Ok || wo != IdleWhy.Ok) return true;
                        _pendingAgent = __instance;
                        _pendingOther = receiver;
                        Counters.AvoidSkips++;
                        _skips++;
                        return false;
                    }
                    case PushKind.Self:
                    {
                        bool skip = (object)_pendingAgent == __instance && (object)_pendingOther == pusher;
                        _pendingAgent = null;
                        _pendingOther = null;
                        if (!skip) return true;
                        Counters.AvoidSkips++;
                        _skips++;
                        return false;
                    }
                    case PushKind.OffNav:
                        _offNav++;
                        return true;
                    case PushKind.SelfUnmatched:
                        _unmatched++;
                        return true;
                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                Clear();
                Item.Fail(ex, "ApplyAvoidanceForces prefix");
                return true;
            }
        }

        static void Tick()
        {
            var s = PhysicsModule.S;
            float now = Time.realtimeSinceStartup;
            if (s == null || s.CensusLogSeconds <= 0f || now < _nextLog) return;
            bool first = _nextLog == 0f;
            _nextLog = now + s.CensusLogSeconds;
            if (!first && _calls > 0)
            {
                var sb = new StringBuilder(256);
                sb.Append("avoidpairs calls=").Append(_calls).Append(" skip=").Append(_skips).Append(" offNav=").Append(_offNav)
                  .Append(" selfUnmatched=").Append(_unmatched).Append(" | missSelf");
                IdleTest.Append(sb, _missSelf);
                sb.Append(" | missOther");
                IdleTest.Append(sb, _missOther);
                Perf.Log("[Physics] " + sb);
                Item.Info = "last interval: " + _skips + "/" + _calls + " calls skipped";
            }
            _calls = _skips = _offNav = _unmatched = 0;
            Array.Clear(_missSelf, 0, _missSelf.Length);
            Array.Clear(_missOther, 0, _missOther.Length);
        }
    }

    internal static class AvoidanceSkipOntoResting
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;
        static Unit _lastOther;

        static readonly long[] _notIdle = new long[(int)IdleWhy.Count];
        static long _calls, _skips, _offNav, _unknown, _keptPlayer, _keptMoving;
        static float _nextLog;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("AvoidanceSkipOntoResting",
                "CollisionAvoidance: no avoidance impulse / turn write onto an idle AI vehicle from a unit that is not moving " +
                "(< AvoidanceIdleSpeed) and not player-controlled; pushes onto busy units and off-navmesh pushes unchanged",
                v => On = v, defaultOn: false);
            var push = Item.Target(typeof(CollisionAvoidance), "HandlePhysicsAvoidancePushing", new[] { typeof(float) });
            var apply = Item.Target(typeof(CollisionAvoidance), "ApplyAvoidanceForces", AvoidRoles.ApplyArgs);
            if (push != null && apply != null)
            {
                Item.Patch(push, typeof(AvoidanceSkipOntoResting), prefix: nameof(PushPrefix));
                Item.Patch(apply, typeof(AvoidanceSkipOntoResting), prefix: nameof(ApplyPrefix));
            }
            else
            {
                Item.Block("CollisionAvoidance.HandlePhysicsAvoidancePushing / ApplyAvoidanceForces absent");
            }
            Item.OnSwitch(_ => _lastOther = null);
            ctx.OnUpdate(Tick, Item);
        }

        static void PushPrefix()
        {
            _lastOther = null;
        }

        static bool ApplyPrefix(CollisionAvoidance __instance, Vehicle vehicle, Rigidbody mainRB, float otherTotalMass,
                                bool onNavmeshEdge, float forwardDot, float otherDirDot)
        {
            if (!On) return true;
            try
            {
                _calls++;
                PushKind kind = AvoidRoles.Resolve(__instance, vehicle, mainRB, otherTotalMass, onNavmeshEdge, forwardDot, otherDirDot,
                                                   ref _lastOther, out Unit receiver, out Unit pusher);
                if (kind == PushKind.OffNav) { _offNav++; return true; }
                if (kind != PushKind.Neighbour && kind != PushKind.Self) { _unknown++; return true; }
                IdleWhy why = IdleTest.Vehicle(receiver, S.AvoidanceIdleSpeed, -1f, false);
                if (why != IdleWhy.Ok) { _notIdle[(int)why]++; return true; }
                if (pusher && pusher.PlayerControlled) { _keptPlayer++; return true; }
                if (!AvoidRoles.PusherResting(pusher, S.AvoidanceIdleSpeed)) { _keptMoving++; return true; }
                _skips++;
                Counters.AvoidSkips++;
                return false;
            }
            catch (Exception ex)
            {
                _lastOther = null;
                Item.Fail(ex, "ApplyAvoidanceForces prefix");
                return true;
            }
        }

        static void Tick()
        {
            var s = PhysicsModule.S;
            float now = Time.realtimeSinceStartup;
            if (s == null || s.CensusLogSeconds <= 0f || now < _nextLog) return;
            bool first = _nextLog == 0f;
            _nextLog = now + s.CensusLogSeconds;
            if (!first && _calls > 0)
            {
                var sb = new StringBuilder(256);
                sb.Append("avoidresting calls=").Append(_calls).Append(" skip=").Append(_skips).Append(" offNav=").Append(_offNav)
                  .Append(" unknownPusher=").Append(_unknown).Append(" keptPlayer=").Append(_keptPlayer).Append(" keptMoving=").Append(_keptMoving)
                  .Append(" | receiverNotIdle");
                IdleTest.Append(sb, _notIdle);
                Perf.Log("[Physics] " + sb);
                Item.Info = "last interval: " + _skips + "/" + _calls + " calls skipped";
            }
            _calls = _skips = _offNav = _unknown = _keptPlayer = _keptMoving = 0;
            Array.Clear(_notIdle, 0, _notIdle.Length);
        }
    }
}
