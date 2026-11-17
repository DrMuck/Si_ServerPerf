/*
 DeadSettle (default OFF, approximate): destroyed units stop costing PhysX during their disposal wait.

 Vanilla (1.0.17):
   - A destroyed unit stays in the world with all bodies, joints, colliders and registrations until
     BaseGameObject.UpdateDestroyedDisposal destroys the GameObject DestroyedDisposeTime (15 s game time) after death
     (BaseGameObject.cs:67, :801-837; Unit.CanDispose Unit.cs:432-441). Units are not returned to a pool: disposal is
     Object.Destroy (:823); PrefabPool only hands out pre-warmed fresh instances (PrefabPool.cs:118-149).
   - Vehicle.OnDestroyed ends kinematic mode (Vehicle.cs:892-899): a wreck is a fully dynamic chassis plus its
     ConfigurableJoint wheel bodies. VehicleWheeled.OrderedFixedUpdate keeps running for it: inputs are cleared
     (Vehicle.UpdateInput :1753-1756), motor and brakes are off (VehicleWheeled.cs:1280-1286, so a wreck rolls freely),
     but every step it still rewrites awake wheels' velocities (:1380, :1399), joint drive/steering (:1345, :1416-1426,
     ApplyLowTickWheelFix :1428-1439), applies roll stabilisation torque unconditionally (AddRelativeTorque :1474-1479,
     non-zero whenever the wreck is tilted) and wakes all wheels whenever the chassis is awake (:1306-1317, :1336).
     Its own sleep path (all wheels slow for 1 s -> SleepVehicle, :1486, :613) therefore rarely completes: census
     night1 found 0 % of lab wheeled bodies asleep, wrecks included.
   - Soldier.OnDestroyed only plays the death animation (Soldier.cs:3807-3818); the soldier fixed update returns early
     while its body sleeps and it has no input (:2875-2882). CreatureDecapod.OnDestroyed (:4961) the same; its fixed
     update returns early while asleep without input (:3765-3772) - inputs are cleared on death (:4431-4436) - but an
     awake corpse keeps applying latch forces, so PhysX auto-sleep never triggers (7 % of creature bodies asleep).
     VehicleHovered / VehicleAir fixed updates return at once for a destroyed unit (VehicleHovered.cs:1198,
     VehicleAir.cs:1802): pure PhysX objects that auto-sleep at rest.
   - CollisionAvoidance.OrderedFixedUpdate unregisters ITSELF from the fixed list once its unit is destroyed
     (Silica.AI/CollisionAvoidance.cs:722-727), and every manager pass skips destroyed agents
     (CollisionAvoidanceManager.cs:151, :185, :215). But the agent stays in the manager's m_Agents and its trigger
     capsule ("AvoidanceCollider", layer SelfOnly, CollisionAvoidance.cs:359-393) stays enabled: it keeps its PhysX
     shape and still fills the 32-hit OverlapSphere slots of every live neighbour (CollisionAvoidanceManager.cs:160-190)
     until the object is destroyed (OnDisable -> Unregister, :778-785).

 Mechanism: forced sleep, plus skipping the wreck's own wake-up code while it sleeps. Per destroyed unit, from
 DeadSettleDelaySeconds (3 s) after its death was seen:
   - when every dynamic body has been slow for DeadSettleSlowSeconds (0.5 s; linear < DeadSettleLinearSpeed on every
     body, angular < DeadSettleAngularSpeed on the main body / DeadSettleWheelAngularSpeed on the others), no body has a
     pending force or torque (Sleep() would discard it), a wheeled wreck's engine speed and steering have run down
     (CurrentEngineSpeed01 == 0, input == applied steering: VehicleWheeled's own wake test :1253 is false), the unit
     is not player-controlled, not seated, and not a burrowed creature (which writes velocity every step, :3711-3713):
       * its avoidance capsule is disabled and the agent unregistered from CollisionAvoidanceManager (setting
         DeadSettleRemoveAvoidance),
       * one Physics.SyncTransforms() for all units settled in this step (transform writes of this step reach PhysX
         before Sleep instead of re-waking at the simulate sync, see WheelHold.cs header), then Rigidbody.Sleep() on
         every dynamic body - at the end of the ordered fixed list, after every game writer of the step;
   - while a settled wheeled wreck's chassis sleeps, VehicleWheeled.OrderedFixedUpdate is skipped (prefix) except for
     its first lines (m_RaycastCommandIndex = -1, every wheel's BrakeRaycastIndex = -1, :1216-1221) so
     OrderedFixedUpdatePartTwo, which still runs and completes the shared raycast job, sees "no brake rays" exactly as
     when vanilla runs. The skipped body does nothing for a dead, sleeping wreck except the roll-stabilisation torque
     and steering/joint writes that wake it; nothing else reads its state for a destroyed unit.
   - Never frozen while moving: a falling aircraft accelerates past 0.3 m/s within 0.03 s, so it cannot stay slow for
     0.5 s in the air; tumbling wrecks and corpses fail the speed tests. Units still not settled DeadSettleGiveUpSeconds
     (12 s) after death are left alone (counted timeLimit); no settling or re-settling after that time.
 Why not the alternatives:
   - Kinematic bodies: same PhysX saving, but the wreck becomes an immovable wall (a fast vehicle hits it like a
     structure, vehicles jam at chokes for up to 12 s), and VehicleWheeled / NetworkTransform / damage code keep writing
     velocities on it (would need guards on every writer), and Vehicle's own kinematic mode (KinematicPhysics) cannot
     be reused for wrecks (it integrates redirected forces with no gravity or damping: a hit wreck would drift away).
   - Disabling wheel joints / wheel bodies only: a joint between a dynamic chassis and kinematic wheels pins the chassis,
     and destroying joints cannot be undone on switch-off.
   Forced sleep keeps the wreck a normal dynamic body: any contact by a moving unit, an explosion impulse or a velocity
   write wakes it through PhysX (the whole jointed island), vanilla code then runs again in full, and the item
   re-settles it once it is slow again (counted rewoken / resettled).
 Colliders stay (except the avoidance trigger): wrecks still block shots, take hits and are hit-scanned as before;
 damage on dead units only adds forces/velocity to dynamic bodies, which simply wakes them. Disposal is untouched:
 the GameObject (with its bodies) is destroyed by vanilla at 15 s; records of destroyed objects are dropped.
 Clients: the server streams the root transform when it changes beyond 2 cm / 0.5 deg / 0.05 m/s
 (NetworkTransformComponent.cs:405-429): a settled wreck sends one last update with zero velocity and then nothing;
 clients run their own wreck physics and are nudged to the server pose (:448-505), so they see the wreck come to rest
 where the server has it - at most a small final correction for a wreck that was still creeping below the thresholds.
 Bandwidth for wrecks drops. Wheels of a wreck are not streamed (client-side bodies), so freezing server wheel spin is
 invisible.
 Switch-off (or a destroyed unit coming back to life via SetHealth): every still-existing settled unit is woken
 (WakeUp on every dynamic body), its avoidance capsule re-enabled and the agent re-registered with the manager
 (CollisionAvoidanceManager.Register, the same call vanilla makes when the capsule is created); the wheeled skip
 ends at once (it checks the switch first).

 Log every CensusLogSeconds (interval counts, "now" = current state):
   [Physics] deadsettle trackedNow= waitingNow= settledNow= settledBodiesNow= doneNow= | seen= settled= resettled=
   rewoken= (kind=n ...) timeLimit= revived= disposedSettled= bodies= avoidRemoved= fixedSkips= syncs=
   settleAgeMean= | waitNow delay= slowWindow= moving= force= engine= player= compartment= burrowed= noBody=
*/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    // Reference identity for Unity objects as dictionary keys (no UnityEngine.Object.Equals, works for destroyed objects).
    internal sealed class RefEq<T> : IEqualityComparer<T> where T : class
    {
        public static readonly RefEq<T> I = new RefEq<T>();
        public bool Equals(T a, T b) => ReferenceEquals(a, b);
        public int GetHashCode(T o) => RuntimeHelpers.GetHashCode(o);
    }

    internal static class DeadSettle
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;

        static AccessTools.FieldRef<VehicleWheeled, int> _rayIndex;
        static AccessTools.FieldRef<VehicleWheeled, float> _turnRate;
        static AccessTools.FieldRef<CollisionAvoidance, CapsuleCollider> _capsule;

        enum St : byte { Waiting, Settled, Done }

        enum Why : byte { Ok, Delay, SlowWindow, Moving, Force, Engine, Player, Compartment, Burrowed, NoBody, Count }
        static readonly string[] WhyNames = { "ok", "delay", "slowWindow", "moving", "force", "engine", "player", "compartment", "burrowed", "noBody" };

        sealed class Rec
        {
            public Unit U;
            public VehicleWheeled Wheeled;
            public byte Kind;
            public St State;
            public Why Last = Why.Delay;
            public float SeenAt, SlowSince = -1f;
            public bool EverSettled;
            public int BodiesSlept;
            public CollisionAvoidance Agent;
            public CapsuleCollider Capsule;
            public bool AvoidRemoved, CapsuleDisabled;
        }

        static readonly Dictionary<Unit, Rec> _recs = new Dictionary<Unit, Rec>(RefEq<Unit>.I);
        static readonly List<Rec> _list = new List<Rec>();
        static readonly Dictionary<VehicleWheeled, Rec> _held = new Dictionary<VehicleWheeled, Rec>(RefEq<VehicleWheeled>.I);
        static readonly List<Rec> _toSettle = new List<Rec>();
        static int _rr, _settledNow, _settledBodiesNow;

        // interval counters
        static long _seen, _settled, _resettled, _rewoken, _timeLimit, _revived, _disposedSettled, _bodies, _avoidRemoved,
                    _fixedSkips, _syncs, _restored;
        static double _ageSum;
        static readonly long[] _rewokenKind = new long[Kinds.Count];
        static float _nextLog;

        internal static int SettledNow => _settledNow;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("DeadSettle",
                "Destroyed units waiting for disposal: once slow (after DeadSettleDelaySeconds) put every dynamic body to sleep, " +
                "skip VehicleWheeled's fixed update while the wreck sleeps, unregister its avoidance capsule; any contact/force " +
                "wakes it as vanilla (re-settled later); colliders and 15 s disposal unchanged",
                v => On = v, defaultOn: false);

            _rayIndex = Refl.Field<VehicleWheeled, int>("m_RaycastCommandIndex", Item);
            _turnRate = Refl.Field<VehicleWheeled, float>("m_fCurrentTurnRate", Item);
            _capsule = Refl.Field<CollisionAvoidance, CapsuleCollider>("m_AvoidanceCapsuleCollider", Item);
            if (_rayIndex == null || _turnRate == null) Item.Block("VehicleWheeled.m_RaycastCommandIndex / m_fCurrentTurnRate absent");
            if (_capsule == null) Item.Note("CollisionAvoidance.m_AvoidanceCapsuleCollider absent: avoidance capsules are left alone");

            var fu = Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes);
            Item.Patch(fu, typeof(DeadSettle), postfix: nameof(FixedPost), priority: Priority.HigherThanNormal);
            var wf = Item.Target(typeof(VehicleWheeled), "OrderedFixedUpdate", new[] { typeof(float) }, declaredOnly: true);
            Item.Patch(wf, typeof(DeadSettle), prefix: nameof(WheeledPre));

            Item.OnSwitch(on => { if (!on) RestoreAll(); });
            ctx.OnUpdate(Tick, Item);
            ctx.OnBeforeStatus(RefreshInfo);
            ctx.OnSceneLoaded(_ => Clear());
        }

        // ---------------------------------------------------------------- VehicleWheeled.OrderedFixedUpdate prefix

        static bool WheeledPre(VehicleWheeled __instance)
        {
            if (!On || _held.Count == 0) return true;
            try
            {
                if (!_held.ContainsKey(__instance)) return true;
                Rigidbody main = __instance.RigidBody;
                if (!main || !main.IsSleeping()) return true;   // woken (contact, force, write): vanilla runs in full
                // vanilla's first lines (VehicleWheeled.cs:1216-1221), read by OrderedFixedUpdatePartTwo
                _rayIndex(__instance) = -1;
                List<VehicleWheelSetup> wheels = __instance.Wheels;
                if (wheels != null)
                {
                    for (int i = 0; i < wheels.Count; i++)
                    {
                        VehicleWheelSetup w = wheels[i];
                        if (w != null) w.BrakeRaycastIndex = -1;
                    }
                }
                _fixedSkips++;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "VehicleWheeled.OrderedFixedUpdate prefix");
                return true;
            }
        }

        // ---------------------------------------------------------------- end of the ordered fixed list

        static void FixedPost()
        {
            if (!On) return;
            try
            {
                float now = Time.fixedTime;
                Discover(now);
                _toSettle.Clear();
                int settled = 0, bodiesNow = 0;
                for (int i = _list.Count - 1; i >= 0; i--)
                {
                    Rec r = _list[i];
                    Unit u = r.U;
                    if (!u)
                    {
                        if (r.State == St.Settled) _disposedSettled++;
                        Drop(i, r);
                        continue;
                    }
                    if (!u.IsDestroyed)
                    {
                        if (Restore(r)) _restored++;
                        _revived++;
                        Drop(i, r);
                        continue;
                    }
                    if (r.State == St.Done) continue;
                    if (r.State == St.Settled)
                    {
                        if (AnyAwake(u))
                        {
                            r.State = St.Waiting;
                            r.SlowSince = -1f;
                            r.Last = Why.SlowWindow;
                            if (r.Wheeled) _held.Remove(r.Wheeled);
                            _rewoken++;
                            _rewokenKind[r.Kind]++;
                        }
                        else
                        {
                            settled++;
                            bodiesNow += r.BodiesSlept;
                            continue;
                        }
                    }
                    // waiting
                    float age = now - r.SeenAt;
                    if (age < S.DeadSettleDelaySeconds) { r.Last = Why.Delay; continue; }
                    if (age >= S.DeadSettleGiveUpSeconds)
                    {
                        r.State = St.Done;
                        if (!r.EverSettled) _timeLimit++;
                        continue;
                    }
                    Why w = Check(r);
                    r.Last = w;
                    if (w != Why.Ok) { r.SlowSince = -1f; continue; }
                    if (r.SlowSince < 0f) r.SlowSince = now;
                    if (now - r.SlowSince < S.DeadSettleSlowSeconds) { r.Last = Why.SlowWindow; continue; }
                    _toSettle.Add(r);
                }
                if (_toSettle.Count > 0)
                {
                    UnityEngine.Physics.SyncTransforms();
                    _syncs++;
                    for (int i = 0; i < _toSettle.Count; i++)
                    {
                        Rec r = _toSettle[i];
                        Settle(r, now);
                        settled++;
                        bodiesNow += r.BodiesSlept;
                    }
                    _toSettle.Clear();
                }
                _settledNow = settled;
                _settledBodiesNow = bodiesNow;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "OrderedUpdateManager.FixedUpdate postfix");
            }
        }

        // Round robin over Unit.Units: every unit is looked at every DeadSettleScanStrideSteps fixed steps.
        static void Discover(float now)
        {
            List<Unit> units = Unit.Units;
            if (units == null || units.Count == 0) return;
            int stride = Mathf.Max(1, S.DeadSettleScanStrideSteps);
            int n = (units.Count + stride - 1) / stride;
            for (int j = 0; j < n; j++)
            {
                if (_rr >= units.Count) _rr = 0;
                Unit u = units[_rr++];
                if (!u || !u.IsDestroyed || _recs.ContainsKey(u)) continue;
                var r = new Rec { U = u, Wheeled = u as VehicleWheeled, Kind = (byte)Kinds.Of(u), SeenAt = now };
                _recs.Add(u, r);
                _list.Add(r);
                _seen++;
            }
        }

        static Why Check(Rec r)
        {
            Unit u = r.U;
            if (u.ControlledBy) return Why.Player;
            if (u.InCompartment) return Why.Compartment;
            Rigidbody main = u.RigidBody;
            if (!main || main.isKinematic) return Why.NoBody;
            if (u is CreatureDecapod c && !c.IsEmerged) return Why.Burrowed;
            VehicleWheeled vw = r.Wheeled;
            if (vw && (vw.CurrentEngineSpeed01 != 0f || vw.VehicleInput_ForwardBack != 0f || vw.VehicleInput_RightLeft != _turnRate(vw)))
                return Why.Engine;
            Rigidbody[] bodies = Census.BodiesOf(u);
            float lin = S.DeadSettleLinearSpeed;
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (!rb || rb.isKinematic) continue;
                if (!Vel.Slow(rb, lin, rb == main ? S.DeadSettleAngularSpeed : S.DeadSettleWheelAngularSpeed)) return Why.Moving;
            }
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (!rb || rb.isKinematic) continue;
                if (rb.GetAccumulatedForce(dt).sqrMagnitude > 0f || rb.GetAccumulatedTorque(dt).sqrMagnitude > 0f) return Why.Force;
            }
            return Why.Ok;
        }

        static void Settle(Rec r, float now)
        {
            Unit u = r.U;
            if (S.DeadSettleRemoveAvoidance && !r.AvoidRemoved) RemoveAvoidance(r);
            Rigidbody[] bodies = Census.BodiesOf(u);
            int n = 0;
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (!rb || rb.isKinematic) continue;
                rb.Sleep();
                n++;
            }
            r.BodiesSlept = n;
            r.State = St.Settled;
            r.Last = Why.Ok;
            if (r.EverSettled) _resettled++;
            else
            {
                _settled++;
                _ageSum += now - r.SeenAt;
                r.EverSettled = true;
            }
            _bodies += n;
            if (r.Wheeled) _held[r.Wheeled] = r;
        }

        static void RemoveAvoidance(Rec r)
        {
            CollisionAvoidance agent = r.U.AI_CollisionAvoidance;
            if (!agent || _capsule == null) return;
            CapsuleCollider cap = _capsule(agent);
            if (!cap) return;
            CollisionAvoidanceManager.Unregister(agent, cap.GetInstanceID());
            r.Agent = agent;
            r.Capsule = cap;
            r.AvoidRemoved = true;
            if (cap.enabled)
            {
                cap.enabled = false;
                r.CapsuleDisabled = true;
            }
            _avoidRemoved++;
        }

        static bool AnyAwake(Unit u)
        {
            Rigidbody[] bodies = Census.BodiesOf(u);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (rb && !rb.isKinematic && !rb.IsSleeping()) return true;
            }
            return false;
        }

        static void Drop(int i, Rec r)
        {
            int last = _list.Count - 1;
            _list[i] = _list[last];
            _list.RemoveAt(last);
            _recs.Remove(r.U);
            if ((object)r.Wheeled != null) _held.Remove(r.Wheeled);
        }

        // Back to vanilla state for one still-existing unit. Returns true if anything was changed back.
        static bool Restore(Rec r)
        {
            bool any = false;
            Unit u = r.U;
            if (r.State == St.Settled && u)
            {
                Rigidbody[] bodies = Census.BodiesOf(u);
                for (int i = 0; i < bodies.Length; i++)
                {
                    Rigidbody rb = bodies[i];
                    if (rb && !rb.isKinematic && rb.IsSleeping()) rb.WakeUp();
                }
                any = true;
            }
            if (r.AvoidRemoved)
            {
                if (r.CapsuleDisabled && r.Capsule) r.Capsule.enabled = true;
                if (r.Agent && r.Capsule) CollisionAvoidanceManager.Register(r.Agent, r.Capsule.GetInstanceID());
                r.AvoidRemoved = r.CapsuleDisabled = false;
                any = true;
            }
            r.State = St.Waiting;
            if ((object)r.Wheeled != null) _held.Remove(r.Wheeled);
            return any;
        }

        static void RestoreAll()
        {
            int n = 0;
            for (int i = 0; i < _list.Count; i++)
            {
                try { if (Restore(_list[i])) n++; }
                catch (Exception ex) { Perf.Warn("Physics.DeadSettle: restore on switch-off: " + ex.Message); }
            }
            _restored += n;
            Clear();
            Item.Info = "last switch-off: " + n + " settled dead units restored (woken, avoidance re-registered)";
        }

        static void Clear()
        {
            _recs.Clear();
            _list.Clear();
            _held.Clear();
            _toSettle.Clear();
            _settledNow = _settledBodiesNow = 0;
            _rr = 0;
        }

        // ---------------------------------------------------------------- log / status

        static void Tick()
        {
            var s = PhysicsModule.S;
            float now = Time.realtimeSinceStartup;
            if (s == null || s.CensusLogSeconds <= 0f || now < _nextLog) return;
            bool first = _nextLog == 0f;
            _nextLog = now + s.CensusLogSeconds;
            if (!first) WriteLog();
            _seen = _settled = _resettled = _rewoken = _timeLimit = _revived = _disposedSettled = _bodies = _avoidRemoved =
                _fixedSkips = _syncs = _restored = 0;
            _ageSum = 0;
            Array.Clear(_rewokenKind, 0, _rewokenKind.Length);
        }

        static void WriteLog()
        {
            int waiting = 0, done = 0;
            var why = new int[(int)Why.Count];
            for (int i = 0; i < _list.Count; i++)
            {
                Rec r = _list[i];
                if (r.State == St.Done) done++;
                else if (r.State == St.Waiting) { waiting++; why[(int)r.Last]++; }
            }
            var sb = new StringBuilder(512);
            sb.Append("deadsettle trackedNow=").Append(_list.Count).Append(" waitingNow=").Append(waiting)
              .Append(" settledNow=").Append(_settledNow).Append(" settledBodiesNow=").Append(_settledBodiesNow).Append(" doneNow=").Append(done)
              .Append(" | seen=").Append(_seen).Append(" settled=").Append(_settled).Append(" resettled=").Append(_resettled)
              .Append(" rewoken=").Append(_rewoken).Append(" (");
            bool firstKind = true;
            for (int k = 0; k < Kinds.Count; k++)
            {
                if (_rewokenKind[k] == 0) continue;
                if (!firstKind) sb.Append(' ');
                sb.Append(Kinds.Names[k]).Append('=').Append(_rewokenKind[k]);
                firstKind = false;
            }
            sb.Append(") timeLimit=").Append(_timeLimit).Append(" revived=").Append(_revived)
              .Append(" disposedSettled=").Append(_disposedSettled).Append(" bodies=").Append(_bodies)
              .Append(" avoidRemoved=").Append(_avoidRemoved).Append(" fixedSkips=").Append(_fixedSkips).Append(" syncs=").Append(_syncs)
              .Append(" restored=").Append(_restored)
              .Append(" settleAgeMean=").Append((_settled > 0 ? _ageSum / _settled : 0.0).ToString("0.00"))
              .Append(" | waitNow");
            for (int i = 1; i < (int)Why.Count; i++) sb.Append(' ').Append(WhyNames[i]).Append('=').Append(why[i]);
            Perf.Log("[Physics] " + sb);
        }

        static void RefreshInfo()
        {
            if (Item == null || !Item.On) return;
            Item.Info = "settled now " + _settledNow + " dead units (" + _settledBodiesNow + " bodies asleep), tracked " + _list.Count +
                        ", wheeled fixed updates skipped " + _fixedSkips + " this interval";
        }
    }
}
