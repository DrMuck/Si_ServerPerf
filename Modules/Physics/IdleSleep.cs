/*
 IdleForceSleep (default OFF): puts idle AI units to sleep at the end of the fixed step, after every game writer of
 that step ran and right before PhysX simulates (postfix on OrderedUpdateManager.FixedUpdate).

 Who: AI-controlled VehicleWheeled and ground CreatureDecapod. Not soldiers: Soldier.OrderedFixedUpdatePartTwo
 already calls Sleep() under exactly the idle condition (:2989-3001). Not hover/air vehicles (they hold themselves up
 with forces every step) and nothing kinematic, destroyed, seated, player-controlled or with a move order.

 Idle =   no move order, AIControlled, and
   wheeled:  IdleTest.Vehicle (no input, CurrentEngineSpeed01 == 0, not kinematic / recovery hop / force move / flipped,
             chassis < IdleLinearSpeed / IdleAngularSpeed) and every other body (wheels) < IdleWheelLinearSpeed /
             IdleWheelAngularSpeed
   creature: emerged, not flying, not jumping, no ground rigidbody (moving platform), private inputs m_InputMovement,
             m_fAttackInput, m_bQueueJump all zero, body < IdleLinearSpeed / IdleAngularSpeed
 held for IdleSleepSeconds (each unit checked every IdleCheckStrideSteps steps, round robin). Then:
   wheeled:  VehicleWheeled.SleepVehicle() - the game's own routine (zero velocities, wheel joints to Slerp drive,
             Sleep() on wheels and body), the same call vanilla makes after 1 s of all-wheels-slow
   creature: Rigidbody.Sleep()
 Bodies are only put to sleep while they are already at rest AND no body has a pending force/torque
 (Rigidbody.GetAccumulatedForce/Torque: impulses added since the last simulate are not in the velocity yet), so
 Sleep() (zeroes velocities, clears accumulated forces) discards nothing; a blocked attempt counts sleepBlockedByForce.
 Units put to sleep are re-checked every step: still idle but awake again -> slept again (reSleep); no longer idle ->
 released, the game's own code drives them.

 Wake (all vanilla paths): wheeled VehicleWheeled.OrderedFixedUpdate wakes body + wheels as soon as there is input or
 engine speed (:1305-1317, :1336); creature OrderedFixedUpdate wakes on input (:3765-3772); PhysX wakes a sleeping
 body on contact with an awake body, on any force/impulse (explosions, recoil, avoidance pushes) and on velocity writes.

 Re-wake attribution (lab b7: ~7 re-sleeps per force-slept vehicle per second). For every force-slept vehicle the
 item records whether its chassis was asleep at the START of the step (prefix on OrderedUpdateManager.FixedUpdate)
 and probes every ApplyAvoidanceForces call aimed at it (receiver asleep before, awake after = woken by that push).
 When a force-slept, still idle vehicle is found awake at the end of the step the wake is classified, first match:
   avoidResting / avoidMoving / avoidPlayer / avoidOffNav / avoidUnknown   an avoidance call in THIS step woke it
                (by pusher state: resting = chassis < AvoidanceIdleSpeed and AI, moving, player, off-navmesh push)
   force        no avoidance wake, but a pending force/torque on a body (someone's AddForce this step)
   between      already awake at the start of the step: woken during PhysX simulate or by Update/LateUpdate code
                (contact with an awake body, transform writes, native) - sub-split by "pushed in the previous step"
   scripts      woken inside this step's fixed scripts by something else (a velocity write)
 plus which bodies were awake (all / chassis only / wheels only) and slope (chassis tilt >= 5 deg).
 Released units are counted by the first failing idle test, and each round-robin check of a wheeled candidate that is
 not idle is counted the same way (why the rest of the fleet never qualifies).
 Creep check: position at the first forced sleep is kept; the log prints max / mean horizontal drift and max |dy| of
 the currently force-slept vehicles since then (a slope creep from repeated wake/sleep shows up here).
   [Physics] idlesleep forcedNow= forcedSleep= reSleep= blocked= released= | rewake n= avoidResting= avoidMoving= avoidPlayer=
   avoidOffNav= avoidUnknown= force= between= (prevPush=) scripts= | bodies all= chassisOnly= wheelsOnly= | slope= |
   drift xzMax= xzMean= dyMax= n= | released <why>=n | notIdle <why>=n

 Switch-off: every body this item put to sleep and that is still asleep is woken (WakeVehicle / WakeUp); the game
 then puts it back to sleep by its own rules.
*/

using System;
using System.Collections.Generic;
using System.Text;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class IdleForceSleep
    {
        internal static bool On, DiagOn;
        static PerfItem Item, DiagItem;
        static PhysicsSettings S;

        static Action<VehicleWheeled> _sleepVehicle, _wakeVehicle;
        static HarmonyLib.AccessTools.FieldRef<CreatureDecapod, Vector3> _inputMovement;
        static HarmonyLib.AccessTools.FieldRef<CreatureDecapod, float> _attackInput;
        static HarmonyLib.AccessTools.FieldRef<CreatureDecapod, bool> _queueJump;

        internal enum Wake : byte { AvoidResting, AvoidMoving, AvoidPlayer, AvoidOffNav, AvoidUnknown, Force, Between, Scripts, Count }
        static readonly string[] WakeNames = { "avoidResting", "avoidMoving", "avoidPlayer", "avoidOffNav", "avoidUnknown", "force", "between", "scripts" };

        internal sealed class State
        {
            public float IdleSince = -1f;
            public bool Forced;
            public bool AsleepAtStart;
            public int AvoidWakeStep = -10;
            public Wake AvoidWakeKind;
            public int AvoidCallStep = -10;
            public bool HasPos0;
            public Vector3 Pos0;
        }

        static readonly Dictionary<Unit, State> _state = new Dictionary<Unit, State>();
        static readonly List<Unit> _forced = new List<Unit>();
        static int _rr, _step;

        // interval counters
        static readonly long[] _wake = new long[(int)Wake.Count];
        static readonly long[] _released = new long[(int)IdleWhy.Count], _notIdle = new long[(int)IdleWhy.Count];
        static long _rewake, _prevPush, _bAll, _bChassis, _bWheels, _slope, _fS, _rS, _bF, _rel, _partialFix;
        static float _nextLog;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("IdleForceSleep",
                "End-of-step Sleep() for idle AI wheeled vehicles (game's SleepVehicle) and ground creatures after IdleSleepSeconds " +
                "at rest; woken by the game's own input/contact/force paths; logs what re-wakes them",
                v => On = v, defaultOn: false);

            var sleep = Refl.Method(typeof(VehicleWheeled), "SleepVehicle", Type.EmptyTypes, Item);
            var wake = Refl.Method(typeof(VehicleWheeled), "WakeVehicle", Type.EmptyTypes, Item);
            try
            {
                if (sleep != null) _sleepVehicle = (Action<VehicleWheeled>)Delegate.CreateDelegate(typeof(Action<VehicleWheeled>), sleep);
                if (wake != null) _wakeVehicle = (Action<VehicleWheeled>)Delegate.CreateDelegate(typeof(Action<VehicleWheeled>), wake);
            }
            catch (Exception ex) { Item.Note("SleepVehicle/WakeVehicle delegate: " + ex.Message); }
            _inputMovement = Refl.Field<CreatureDecapod, Vector3>("m_InputMovement", Item);
            _attackInput = Refl.Field<CreatureDecapod, float>("m_fAttackInput", Item);
            _queueJump = Refl.Field<CreatureDecapod, bool>("m_bQueueJump", Item);
            if (_sleepVehicle == null || _wakeVehicle == null) Item.Block("VehicleWheeled.SleepVehicle/WakeVehicle absent");
            if (_inputMovement == null || _attackInput == null || _queueJump == null) Item.Block("CreatureDecapod input fields absent");

            var fu = Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes);
            Item.Patch(fu, typeof(IdleForceSleep), postfix: nameof(FixedPost));

            // Re-wake attribution costs a probe on every ApplyAvoidanceForces call (tens of thousands per second in a
            // parked cluster) plus an IsSleeping per force-slept vehicle per step: own item, default off.
            DiagItem = ctx.Item("IdleSleepDiag",
                "Diagnostic for IdleForceSleep: classify every re-wake of a force-slept vehicle (avoidance push by pusher state, " +
                "pending force, simulate/frame, fixed scripts) in the idlesleep log line; costs a probe on every avoidance call",
                v => DiagOn = v, defaultOn: false);
            var fu2 = DiagItem.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes);
            DiagItem.Patch(fu2, typeof(IdleForceSleep), prefix: nameof(FixedPre));
            var push = DiagItem.Target(typeof(CollisionAvoidance), "HandlePhysicsAvoidancePushing", new[] { typeof(float) });
            var apply = DiagItem.Target(typeof(CollisionAvoidance), "ApplyAvoidanceForces", AvoidRoles.ApplyArgs);
            DiagItem.Patch(push, typeof(AvoidProbe), prefix: nameof(AvoidProbe.PushPrefix));
            DiagItem.Patch(apply, typeof(AvoidProbe), prefix: nameof(AvoidProbe.Pre), postfix: nameof(AvoidProbe.Post));
            Item.OnSwitch(on => { if (!on) ReleaseAll(wake: true); });
            ctx.OnUpdate(Tick, Item);
            ctx.OnSceneLoaded(_ => { _state.Clear(); _forced.Clear(); Counters.ForcedNow = 0; });
        }

        // ---------------------------------------------------------------- avoidance wake probe (own class: own __state)

        internal static class AvoidProbe
        {
            static Unit _lastOther;

            internal static void PushPrefix()
            {
                _lastOther = null;
            }

            // __state: the receiver's State when it is force-slept and asleep before the call, else null
            internal static void Pre(CollisionAvoidance __instance, Vehicle vehicle, Rigidbody mainRB, float otherTotalMass,
                                     bool onNavmeshEdge, float forwardDot, float otherDirDot, out State __state)
            {
                __state = null;
                if (!On || !DiagOn) return;
                try
                {
                    PushKind kind = AvoidRoles.Resolve(__instance, vehicle, mainRB, otherTotalMass, onNavmeshEdge, forwardDot, otherDirDot,
                                                       ref _lastOther, out Unit receiver, out Unit pusher);
                    if (!receiver || !_state.TryGetValue(receiver, out State st) || !st.Forced) return;
                    st.AvoidCallStep = _step;
                    if (!mainRB.IsSleeping()) return;
                    st.AvoidWakeKind = kind == PushKind.OffNav ? Wake.AvoidOffNav
                                     : (kind != PushKind.Neighbour && kind != PushKind.Self) ? Wake.AvoidUnknown
                                     : pusher && pusher.PlayerControlled ? Wake.AvoidPlayer
                                     : AvoidRoles.PusherResting(pusher, S.AvoidanceIdleSpeed) ? Wake.AvoidResting
                                     : Wake.AvoidMoving;
                    __state = st;
                }
                catch (Exception ex) { DiagItem.Fail(ex, "ApplyAvoidanceForces probe prefix"); }
            }

            internal static void Post(Rigidbody mainRB, State __state)
            {
                if (!On || !DiagOn || __state == null) return;
                try { if (mainRB && !mainRB.IsSleeping()) __state.AvoidWakeStep = _step; }
                catch (Exception ex) { DiagItem.Fail(ex, "ApplyAvoidanceForces probe postfix"); }
            }
        }

        // ---------------------------------------------------------------- per step

        static void FixedPre()
        {
            if (!On || !DiagOn) return;
            try
            {
                _step++;
                for (int i = 0; i < _forced.Count; i++)
                {
                    Unit u = _forced[i];
                    if (!u || !_state.TryGetValue(u, out State st)) continue;
                    Rigidbody main = u.RigidBody;
                    st.AsleepAtStart = main && main.IsSleeping();
                }
            }
            catch (Exception ex) { DiagItem.Fail(ex, "OrderedUpdateManager.FixedUpdate prefix"); }
        }

        static void FixedPost()
        {
            if (!On) return;
            try
            {
                float now = Time.fixedTime;
                // 1) units already put to sleep: every step
                for (int i = _forced.Count - 1; i >= 0; i--)
                {
                    Unit u = _forced[i];
                    if (!u || !_state.TryGetValue(u, out State st))
                    {
                        _forced.RemoveAt(i);
                        if (u) _state.Remove(u);
                        Counters.ForcedReleased++;
                        continue;
                    }
                    IdleWhy why = Idle(u);
                    if (why != IdleWhy.Ok)
                    {
                        _released[(int)why]++;
                        st.Forced = false;
                        st.IdleSince = -1f;
                        st.HasPos0 = false;
                        _forced.RemoveAt(i);
                        Counters.ForcedReleased++;
                        continue;
                    }
                    if (!AllAsleep(u))
                    {
                        if (DiagOn) Attribute(u, st);
                        if (TrySleep(u)) Counters.ReSleeps++;
                    }
                }
                // 2) round-robin qualification
                List<Unit> units = Unit.Units;
                if (units != null && units.Count > 0)
                {
                    int stride = Mathf.Max(1, S.IdleCheckStrideSteps);
                    int n = (units.Count + stride - 1) / stride;
                    for (int j = 0; j < n; j++)
                    {
                        if (_rr >= units.Count) _rr = 0;
                        Unit u = units[_rr++];
                        if (!u || !Candidate(u)) continue;
                        if (!_state.TryGetValue(u, out State st))
                        {
                            st = new State();
                            _state[u] = st;
                        }
                        if (st.Forced) continue;
                        IdleWhy why = Idle(u);
                        if (why != IdleWhy.Ok)
                        {
                            if (u is VehicleWheeled) _notIdle[(int)why]++;
                            st.IdleSince = -1f;
                            continue;
                        }
                        if (st.IdleSince < 0f) st.IdleSince = now;
                        if (now - st.IdleSince < S.IdleSleepSeconds) continue;
                        st.Forced = true;
                        st.AsleepAtStart = false;
                        if (!st.HasPos0) { st.Pos0 = u.transform.position; st.HasPos0 = true; }
                        _forced.Add(u);
                        if (!AllAsleep(u) && TrySleep(u)) Counters.ForcedSleeps++;
                    }
                }
                if (_state.Count > 4 * Mathf.Max(64, units != null ? units.Count : 0)) PurgeState();
                Counters.ForcedNow = _forced.Count;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "OrderedUpdateManager.FixedUpdate postfix");
            }
        }

        static void Attribute(Unit u, State st)
        {
            _rewake++;
            Wake w;
            if (st.AvoidWakeStep == _step) w = st.AvoidWakeKind;
            else if (AnyPending(u)) w = Wake.Force;
            else if (!st.AsleepAtStart)
            {
                w = Wake.Between;
                if (st.AvoidCallStep == _step - 1) _prevPush++;
            }
            else w = Wake.Scripts;
            _wake[(int)w]++;

            Rigidbody main = u.RigidBody;
            bool chassisAwake = main && !main.IsSleeping();
            bool otherAwake = false;
            Rigidbody[] bodies = Census.BodiesOf(u);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (rb && rb != main && !rb.isKinematic && !rb.IsSleeping()) { otherAwake = true; break; }
            }
            if (chassisAwake && otherAwake) _bAll++;
            else if (chassisAwake) _bChassis++;
            else if (otherAwake) _bWheels++;
            if (Vector3.Angle(u.transform.up, Vector3.up) >= 5f) _slope++;
        }

        // For FixedWakeTrace: the units this item currently holds asleep (read-only use).
        internal static List<Unit> ForcedUnits => _forced;

        // For ParkedWheelHold: true when this item's end-of-step pass will put u back to sleep (still idle, some body awake,
        // no pending force) - the only case where an early transform sync matters.
        internal static bool WillReSleep(Unit u) => Idle(u) == IdleWhy.Ok && !AllAsleep(u) && !AnyPending(u);

        // For WheeledSleepDiag's drift split.
        internal static bool IsForced(Unit u) => On && u && _state.TryGetValue(u, out State st) && st.Forced;

        static bool Candidate(Unit u) => u is VehicleWheeled || u is CreatureDecapod;

        static IdleWhy Idle(Unit u)
        {
            if (u is VehicleWheeled)
            {
                IdleWhy why = IdleTest.Vehicle(u, S.IdleLinearSpeed, S.IdleAngularSpeed, engine: true);
                if (why != IdleWhy.Ok) return why;
                Rigidbody main = u.RigidBody;
                Rigidbody[] bodies = Census.BodiesOf(u);
                for (int i = 0; i < bodies.Length; i++)
                {
                    Rigidbody rb = bodies[i];
                    if (!rb || rb.isKinematic || rb == main) continue;
                    if (!Vel.Slow(rb, S.IdleWheelLinearSpeed, S.IdleWheelAngularSpeed)) return IdleWhy.WheelFast;
                }
                return IdleWhy.Ok;
            }
            if (u is CreatureDecapod c)
            {
                if (c.IsDestroyed) return IdleWhy.Destroyed;
                if (c.InCompartment) return IdleWhy.Compartment;
                if (!c.AIControlled) return IdleWhy.NotAI;
                if (c.HasMoveOrder) return IdleWhy.MoveOrder;
                Rigidbody main = c.RigidBody;
                if (!main || main.isKinematic) return IdleWhy.NoBody;
                if (c.IsInFly || !c.IsEmerged || c.IsJumping || c.GroundRigidBody) return IdleWhy.Kinematic;
                Vector3 mv = _inputMovement(c);
                if (mv.x != 0f || mv.y != 0f || mv.z != 0f || _attackInput(c) != 0f || _queueJump(c)) return IdleWhy.InputFwd;
                return Vel.Slow(main, S.IdleLinearSpeed, S.IdleAngularSpeed) ? IdleWhy.Ok : IdleWhy.Speed;
            }
            return IdleWhy.NotVehicle;
        }

        static bool AllAsleep(Unit u)
        {
            if (u is VehicleWheeled)
            {
                Rigidbody[] bodies = Census.BodiesOf(u);
                for (int i = 0; i < bodies.Length; i++)
                {
                    Rigidbody rb = bodies[i];
                    if (rb && !rb.isKinematic && !rb.IsSleeping()) return false;
                }
                return true;
            }
            return u.RigidBody.IsSleeping();
        }

        static bool AnyPending(Unit u)
        {
            float dt = Time.fixedDeltaTime;
            if (u is VehicleWheeled)
            {
                Rigidbody[] bodies = Census.BodiesOf(u);
                for (int i = 0; i < bodies.Length; i++)
                {
                    Rigidbody rb = bodies[i];
                    if (rb && !rb.isKinematic && Pending(rb, dt)) return true;
                }
                return false;
            }
            return Pending(u.RigidBody, dt);
        }

        // Sleep() zeroes velocities and clears accumulated forces. Velocity is already checked (Idle); forces added since
        // the last simulate (AddForce/impulses from explosions, recoil, avoidance, game movement code) are not in the
        // velocity yet, so any pending force or torque on any body postpones the sleep to a later step.
        static bool TrySleep(Unit u)
        {
            if (AnyPending(u)) { Counters.SleepBlockedByForce++; return false; }
            if (u is VehicleWheeled w)
            {
                // SleepVehicle() returns at once when the chassis is already asleep (:560), which would leave an awake
                // wheel awake (and PhysX then wakes the whole jointed island at the next simulate). Wake the chassis
                // first so the game's routine runs in full; both happen before the simulate, so nothing moves.
                Rigidbody main = w.RigidBody;
                if (main.IsSleeping()) { main.WakeUp(); _partialFix++; }
                _sleepVehicle(w);
            }
            else u.RigidBody.Sleep();
            return true;
        }

        static bool Pending(Rigidbody rb, float dt) =>
            rb.GetAccumulatedForce(dt).sqrMagnitude > 0f || rb.GetAccumulatedTorque(dt).sqrMagnitude > 0f;

        // ---------------------------------------------------------------- log

        static void Tick()
        {
            var s = PhysicsModule.S;
            float now = Time.realtimeSinceStartup;
            if (s == null || s.CensusLogSeconds <= 0f || now < _nextLog) return;
            bool first = _nextLog == 0f;
            _nextLog = now + s.CensusLogSeconds;
            if (!first) WriteLog();
            Array.Clear(_wake, 0, _wake.Length);
            Array.Clear(_released, 0, _released.Length);
            Array.Clear(_notIdle, 0, _notIdle.Length);
            _rewake = _prevPush = _bAll = _bChassis = _bWheels = _slope = _partialFix = 0;
            _fS = Counters.ForcedSleeps; _rS = Counters.ReSleeps; _bF = Counters.SleepBlockedByForce; _rel = Counters.ForcedReleased;
        }

        static void WriteLog()
        {
            float xzMax = 0f, xzSum = 0f, dyMax = 0f;
            int n = 0;
            for (int i = 0; i < _forced.Count; i++)
            {
                Unit u = _forced[i];
                if (!u || !_state.TryGetValue(u, out State st) || !st.HasPos0) continue;
                Vector3 d = u.transform.position - st.Pos0;
                float xz = new Vector2(d.x, d.z).magnitude;
                xzMax = Mathf.Max(xzMax, xz);
                xzSum += xz;
                dyMax = Mathf.Max(dyMax, Mathf.Abs(d.y));
                n++;
            }
            var sb = new StringBuilder(512);
            sb.Append("idlesleep forcedNow=").Append(_forced.Count).Append(" forcedSleep=").Append(Counters.ForcedSleeps - _fS)
              .Append(" reSleep=").Append(Counters.ReSleeps - _rS).Append(" blocked=").Append(Counters.SleepBlockedByForce - _bF)
              .Append(" released=").Append(Counters.ForcedReleased - _rel).Append(" | rewake n=").Append(_rewake);
            for (int i = 0; i < (int)Wake.Count; i++)
            {
                sb.Append(' ').Append(WakeNames[i]).Append('=').Append(_wake[i]);
                if (i == (int)Wake.Between) sb.Append(" (prevPush=").Append(_prevPush).Append(')');
            }
            sb.Append(" | bodies all=").Append(_bAll).Append(" chassisOnly=").Append(_bChassis).Append(" wheelsOnly=").Append(_bWheels)
              .Append(" partialFix=").Append(_partialFix).Append(" | slope=").Append(_slope)
              .Append(" | drift xzMax=").Append(xzMax.ToString("0.###")).Append(" xzMean=").Append((n > 0 ? xzSum / n : 0f).ToString("0.###"))
              .Append(" dyMax=").Append(dyMax.ToString("0.###")).Append(" n=").Append(n)
              .Append(" | released");
            IdleTest.Append(sb, _released);
            sb.Append(" | notIdle");
            IdleTest.Append(sb, _notIdle);
            Perf.Log("[Physics] " + sb);
        }

        // ---------------------------------------------------------------- switch-off

        static void ReleaseAll(bool wake)
        {
            int n = 0;
            try
            {
                for (int i = 0; i < _forced.Count; i++)
                {
                    Unit u = _forced[i];
                    if (!u || !wake) continue;
                    Rigidbody main = u.RigidBody;
                    if (!main || main.isKinematic || !main.IsSleeping()) continue;
                    if (u is VehicleWheeled w) _wakeVehicle(w);
                    else main.WakeUp();
                    n++;
                }
            }
            catch (Exception ex)
            {
                Perf.Warn("Physics.IdleForceSleep: release on switch-off: " + ex.Message);
            }
            Counters.ForcedWakes += n;
            Counters.ForcedReleased += _forced.Count;
            _forced.Clear();
            _state.Clear();
            Counters.ForcedNow = 0;
            Item.Info = "last switch-off: " + n + " force-slept units woken";
        }

        static void PurgeState()
        {
            var dead = new List<Unit>();
            foreach (var kv in _state) if (!kv.Key || (!kv.Value.Forced && kv.Value.IdleSince < 0f)) dead.Add(kv.Key);
            for (int i = 0; i < dead.Count; i++) _state.Remove(dead[i]);
        }
    }
}
