/*
 WheeledSleepDiag (default OFF since b9: its avoidance-call prefix runs on every ApplyAvoidanceForces call; read-only): why a parked VehicleWheeled does not go to sleep.

 Vanilla sleep path (VehicleWheeled, 1.0.17):
   OrderedFixedUpdate  flag4 = VehicleInput_ForwardBack != 0 || CurrentEngineSpeed01 != 0 ||
                       VehicleInput_RightLeft != m_fCurrentTurnRate                                         (:1253)
                       per AWAKE jointed wheel: brakes write linearVelocity (:1380), motor/brake wheels get their
                       angularVelocity rewritten (spin axis x replaced by the brake/motor value, :1399); then the wheel
                       counts as slow if !flag4 && |w_local|^2 < SleepAngularThreshold && |v|^2 < SleepLinearThreshold
                       (thresholds squared once in OnInit: 5 deg/s, 0.3 m/s)                                  (:1402)
                       sleeping wheels count as slow; all slow and not flipped -> m_fSleepSlowTime += dt, else 0 (:1486)
                       chassis awake and m_fSleepSlowTime >= 1 -> reset to 0 (:1306-1312)
   OrderedUpdate       m_fSleepSlowTime > 1 -> SleepVehicle(): zero velocities, Sleep() wheels + chassis (:613, :558)
   While awake, every step writes wheel velocities, so PhysX's own auto-sleep can never fire: this path is the ONLY
   way a vehicle sleeps.

 This item samples, once per CensusIntervalSeconds, the exact post-step state that vanilla's test used (postfix on
 VehicleWheeled.OrderedFixedUpdate in one fixed step: the velocities read there are the values vanilla just wrote and
 tested) and classifies every vehicle with at least one awake dynamic body, first match wins:
   kin       IsMovedKinematically (1.0.17 kinematic stuck-recovery drive or recovery hop)
   input     flag4 (fb = VehicleInput_ForwardBack, eng = CurrentEngineSpeed01, turn = VehicleInput_RightLeft !=
             m_fCurrentTurnRate)
   flip      AntiFlip.IsFlipped
   noWheels  no wheel with a ConfigurableJoint
   fast      some awake wheel fails the slow test (ang = angular, lin = linear; steer / fixed = IsSteering)
   timer     every wheel slow, m_fSleepSlowTime < 1 (waiting)
   condMet   every wheel slow, timer >= 1, still awake after the step (OrderedUpdate should sleep it next frame;
             persistent counts mean that handshake fails, e.g. OrderedUpdate not running between two fixed steps)
 Side facts per awake vehicle: partial (chassis asleep, other body awake), extraAwake (an awake body that is neither
 chassis nor jointed wheel, which SleepVehicle never puts to sleep), rewoke (vanilla SleepVehicle put it to sleep in
 the last 10 s), pushed / offNav (CollisionAvoidance.ApplyAvoidanceForces pair push / off-navmesh return push on it
 in the last 1.5 s), slope (chassis tilt >= 5 deg); max chassis speed (m/s, deg/s), max awake wheel local angular
 speed per axis (deg/s, x = spin axis) and linear speed among "fast" vehicles, wheel thresholds (min-max of the
 squared fields, printed as sqrt).

 Log every CensusLogSeconds, one line, sums of vehicle-samples over the interval, per prefab:
   [Physics] wheeledsleep samples=10 | <prefab> veh=.. asleep=.. awake=.. kin=.. input=..(fb/eng/turn) flip=.. noWheels=..
   fast=..(ang/lin, steer/fixed) timer=.. condMet=.. partial=.. extraAwake=.. rewoke=.. pushed=.. offNav=.. slope=..
   fastPushed=.. fastSlope=.. chassisV=.. chassisW=.. wheelW=x/y/z wheelV=.. thr=ang/lin lowTickFix=0|1
*/

using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class WheeledSleepDiag
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;

        static AccessTools.FieldRef<VehicleWheeled, float> _sleepTime, _turnRate;

        sealed class Agg
        {
            public int Veh, Asleep, Awake, Kin, Input, InFb, InEng, InTurn, Flip, NoWheels, Fast, FastAng, FastLin, FastSteer, FastFixed,
                       Timer, CondMet, Partial, ExtraAwake, Rewoke, Pushed, OffNav, Slope, FastPushed, FastSlope;
            public float ChassisV, ChassisW, WheelWx, WheelWy, WheelWz, WheelV, ThrAngMin = float.MaxValue, ThrAngMax, ThrLinMin = float.MaxValue, ThrLinMax;
            public bool LowTickFix;
            public int Bodies, Wheels;
        }

        static readonly Dictionary<string, Agg> _agg = new Dictionary<string, Agg>();
        static readonly Dictionary<VehicleWheeled, string> _names = new Dictionary<VehicleWheeled, string>();
        static readonly Dictionary<VehicleWheeled, float> _lastPush = new Dictionary<VehicleWheeled, float>();
        static readonly Dictionary<VehicleWheeled, float> _lastOffNav = new Dictionary<VehicleWheeled, float>();
        static readonly Dictionary<VehicleWheeled, float> _lastMovingPush = new Dictionary<VehicleWheeled, float>();
        static readonly Dictionary<VehicleWheeled, float> _lastVanillaSleep = new Dictionary<VehicleWheeled, float>();
        static readonly HashSet<Rigidbody> _wheelSet = new HashSet<Rigidbody>();
        static bool _sampleStep;
        static int _samples;
        static float _nextSample, _nextLog, _lastLog, _nextClean;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("WheeledSleepDiag",
                "Read-only: once per second classifies every awake VehicleWheeled by the reason vanilla's own sleep test fails " +
                "(input / fast wheel / timer / condition met / kinematic / flipped), per prefab, with speeds, slope and avoidance pushes",
                v => On = v, defaultOn: false);
            _sleepTime = Refl.Field<VehicleWheeled, float>("m_fSleepSlowTime", Item);
            _turnRate = Refl.Field<VehicleWheeled, float>("m_fCurrentTurnRate", Item);
            if (_sleepTime == null || _turnRate == null) Item.Block("VehicleWheeled sleep fields absent");

            var f = new[] { typeof(float) };
            Item.Patch(Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes), typeof(WheeledSleepDiag),
                       prefix: nameof(StepPre), postfix: nameof(StepPost));
            Item.Patch(Item.Target(typeof(VehicleWheeled), "OrderedFixedUpdate", f, declaredOnly: true), typeof(WheeledSleepDiag), postfix: nameof(VehPost));
            Item.Patch(Item.Target(typeof(VehicleWheeled), "SleepVehicle", Type.EmptyTypes, declaredOnly: true), typeof(WheeledSleepDiag),
                       prefix: nameof(SleepPre), postfix: nameof(SleepPost));
            Item.Patch(Item.Target(typeof(CollisionAvoidance), "ApplyAvoidanceForces", AvoidanceIdlePairs.ApplyArgs), typeof(WheeledSleepDiag),
                       prefix: nameof(PushPre));
            Item.Patch(Item.Target(typeof(CollisionAvoidance), "HandlePhysicsAvoidancePushing", f), typeof(WheeledSleepDiag),
                       prefix: nameof(PassPre));
            ctx.OnUpdate(Tick, Item);
            ctx.OnSceneLoaded(_ => Clear());
        }

        static void Clear()
        {
            _agg.Clear();
            _names.Clear();
            _lastPush.Clear();
            _lastOffNav.Clear();
            _lastMovingPush.Clear();
            _lastVanillaSleep.Clear();
            _tracks.Clear();
            _samples = 0;
        }

        // ---------------------------------------------------------------- hooks

        static void StepPre()
        {
            if (!On) return;
            float now = Time.realtimeSinceStartup;
            if (now < _nextSample) return;
            _nextSample = now + Mathf.Max(0.1f, S.CensusIntervalSeconds);
            _sampleStep = true;
            _samples++;
        }

        static void StepPost()
        {
            _sampleStep = false;
        }

        static Unit _lastOther;

        static void PassPre()
        {
            _lastOther = null;
        }

        static void PushPre(CollisionAvoidance __instance, Vehicle vehicle, Rigidbody mainRB, float otherTotalMass,
                            bool onNavmeshEdge, float forwardDot, float otherDirDot)
        {
            if (!On) return;
            try
            {
                PushKind kind = AvoidRoles.Resolve(__instance, vehicle, mainRB, otherTotalMass, onNavmeshEdge, forwardDot, otherDirDot,
                                                   ref _lastOther, out Unit receiver, out Unit pusher);
                if (!(receiver is VehicleWheeled w)) return;
                float now = Time.fixedTime;
                if (kind == PushKind.OffNav) { _lastOffNav[w] = now; return; }
                _lastPush[w] = now;
                if (kind != PushKind.Neighbour && kind != PushKind.Self) return;
                if ((pusher && pusher.PlayerControlled) || !AvoidRoles.PusherResting(pusher, S.AvoidanceIdleSpeed)) _lastMovingPush[w] = now;
            }
            catch (Exception ex) { Item.Fail(ex, "ApplyAvoidanceForces prefix"); }
        }

        static void SleepPre(VehicleWheeled __instance, out bool __state)
        {
            __state = false;
            if (!On) return;
            try { Rigidbody rb = __instance.RigidBody; __state = rb && !rb.IsSleeping(); }
            catch (Exception ex) { Item.Fail(ex, "SleepVehicle prefix"); }
        }

        static void SleepPost(VehicleWheeled __instance, bool __state)
        {
            if (!On || !__state) return;
            try { Rigidbody rb = __instance.RigidBody; if (rb && rb.IsSleeping()) _lastVanillaSleep[__instance] = Time.fixedTime; }
            catch (Exception ex) { Item.Fail(ex, "SleepVehicle postfix"); }
        }

        static void VehPost(VehicleWheeled __instance)
        {
            if (!On || !_sampleStep) return;
            try { Classify(__instance); }
            catch (Exception ex) { Item.Fail(ex, "VehicleWheeled.OrderedFixedUpdate postfix"); }
        }

        // ---------------------------------------------------------------- classification

        static Agg AggFor(VehicleWheeled w)
        {
            if (!_names.TryGetValue(w, out string name))
            {
                name = w.gameObject.name;
                int c = name.IndexOf("(Clone)", StringComparison.Ordinal);
                if (c >= 0) name = name.Substring(0, c);
                name = name.Trim();
                _names[w] = name;
            }
            if (!_agg.TryGetValue(name, out Agg a))
            {
                a = new Agg();
                _agg[name] = a;
            }
            return a;
        }

        static void Classify(VehicleWheeled w)
        {
            Rigidbody main = w.RigidBody;
            if (!main || main.isKinematic && !w.IsMovedKinematically) return;
            Agg a = AggFor(w);
            a.Veh++;
            a.LowTickFix = w.ApplyLowTickWheelFix;

            _wheelSet.Clear();
            List<VehicleWheelSetup> wheels = w.Wheels;
            int jointed = 0;
            if (wheels != null)
            {
                for (int i = 0; i < wheels.Count; i++)
                {
                    VehicleWheelSetup ws = wheels[i];
                    if (ws != null && ws.Joint && ws.RigidBody) { jointed++; _wheelSet.Add(ws.RigidBody); }
                }
            }
            Rigidbody[] bodies = Census.BodiesOf(w);
            int dyn = 0, awake = 0, extraAwake = 0;
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (!rb || rb.isKinematic) continue;
                dyn++;
                if (rb.IsSleeping()) continue;
                awake++;
                if (rb != main && !_wheelSet.Contains(rb)) extraAwake++;
            }
            a.Bodies = Mathf.Max(a.Bodies, dyn);
            a.Wheels = Mathf.Max(a.Wheels, jointed);
            float thrA = w.SleepAngularThreshold, thrL = w.SleepLinearThreshold;
            a.ThrAngMin = Mathf.Min(a.ThrAngMin, thrA); a.ThrAngMax = Mathf.Max(a.ThrAngMax, thrA);
            a.ThrLinMin = Mathf.Min(a.ThrLinMin, thrL); a.ThrLinMax = Mathf.Max(a.ThrLinMax, thrL);

            Track(w, awake == 0);
            if (awake == 0 && !w.IsMovedKinematically)
            {
                a.Asleep++;
                return;
            }
            a.Awake++;
            float now = Time.fixedTime;
            if (main.IsSleeping()) a.Partial++;
            if (extraAwake > 0) a.ExtraAwake++;
            bool pushed = _lastPush.TryGetValue(w, out float tp) && now - tp < 1.5f;
            if (pushed) a.Pushed++;
            if (_lastOffNav.TryGetValue(w, out float to) && now - to < 1.5f) a.OffNav++;
            if (_lastVanillaSleep.TryGetValue(w, out float ts) && now - ts < 10f) a.Rewoke++;
            bool slope = Vector3.Angle(w.transform.up, Vector3.up) >= 5f;
            if (slope) a.Slope++;
            a.ChassisV = Mathf.Max(a.ChassisV, main.linearVelocity.magnitude);
            a.ChassisW = Mathf.Max(a.ChassisW, main.angularVelocity.magnitude * Mathf.Rad2Deg);

            if (w.IsMovedKinematically) { a.Kin++; return; }
            bool fb = w.VehicleInput_ForwardBack != 0f, eng = w.CurrentEngineSpeed01 != 0f, turn = w.VehicleInput_RightLeft != _turnRate(w);
            if (fb || eng || turn)
            {
                a.Input++;
                if (fb) a.InFb++;
                if (eng) a.InEng++;
                if (turn) a.InTurn++;
                return;
            }
            VehicleAntiFlip af = w.AntiFlip;
            if (af && af.IsFlipped) { a.Flip++; return; }
            if (jointed == 0) { a.NoWheels++; return; }

            bool fast = false, fastAng = false, fastLin = false, fastSteer = false, fastFixed = false;
            float wx = 0f, wy = 0f, wz = 0f, wv = 0f;
            for (int i = 0; i < wheels.Count; i++)
            {
                VehicleWheelSetup ws = wheels[i];
                if (ws == null || !ws.Joint || !ws.RigidBody) continue;
                Rigidbody rb = ws.RigidBody;
                if (rb.isKinematic || rb.IsSleeping()) continue;
                Transform rt = ws.RigidBodyTransform ? ws.RigidBodyTransform : rb.transform;
                Vector3 wl = Quaternion.Inverse(rt.rotation) * rb.angularVelocity * Mathf.Rad2Deg;
                float v2 = rb.linearVelocity.sqrMagnitude;
                bool angF = wl.sqrMagnitude >= thrA, linF = v2 >= thrL;
                if (!angF && !linF) continue;
                fast = true;
                if (angF) fastAng = true;
                if (linF) fastLin = true;
                if (ws.IsSteering) fastSteer = true; else fastFixed = true;
                wx = Mathf.Max(wx, Mathf.Abs(wl.x)); wy = Mathf.Max(wy, Mathf.Abs(wl.y)); wz = Mathf.Max(wz, Mathf.Abs(wl.z));
                wv = Mathf.Max(wv, Mathf.Sqrt(v2));
            }
            if (fast)
            {
                a.Fast++;
                if (fastAng) a.FastAng++;
                if (fastLin) a.FastLin++;
                if (fastSteer) a.FastSteer++;
                if (fastFixed) a.FastFixed++;
                if (pushed) a.FastPushed++;
                if (slope) a.FastSlope++;
                a.WheelWx = Mathf.Max(a.WheelWx, wx); a.WheelWy = Mathf.Max(a.WheelWy, wy); a.WheelWz = Mathf.Max(a.WheelWz, wz);
                a.WheelV = Mathf.Max(a.WheelV, wv);
                return;
            }
            if (_sleepTime(w) < 1f) a.Timer++;
            else a.CondMet++;
        }

        // ---------------------------------------------------------------- parked drift (one window = one log interval)

        sealed class DriftTrack
        {
            public Vector3 Pos0;
            public bool Parked = true, AllAwake = true, AllForced = true;
        }

        static readonly Dictionary<VehicleWheeled, DriftTrack> _tracks = new Dictionary<VehicleWheeled, DriftTrack>();

        // Parked = no order / input / kinematic / hop / flip (speed not tested) and no push from a moving or player unit
        // in the last 1.5 s, at every sample of the window.
        static void Track(VehicleWheeled w, bool asleep)
        {
            if (!_tracks.TryGetValue(w, out DriftTrack d))
            {
                d = new DriftTrack { Pos0 = w.transform.position };
                _tracks[w] = d;
            }
            if (!d.Parked) return;
            bool moved = _lastMovingPush.TryGetValue(w, out float tm) && Time.fixedTime - tm < 1.5f;
            if (moved || IdleTest.Vehicle(w, 1000f, -1f, false) != IdleWhy.Ok) { d.Parked = false; return; }
            if (!asleep) d.AllForced = false;
            if (asleep) d.AllAwake = false;
            if (!IdleForceSleep.IsForced(w)) d.AllForced = false;
        }

        // [Physics] parkeddrift (cm over the window) | <prefab> awake n= xz=mean/max dy=mean/max forced n= xz= dy=
        static void WriteDrift()
        {
            var groups = new Dictionary<string, float[]>();   // [n, xzSum, xzMax, dySum, dyMax] x 2 (awake, forced)
            foreach (var kv in _tracks)
            {
                VehicleWheeled w = kv.Key;
                DriftTrack d = kv.Value;
                if (!w || !d.Parked || !(d.AllAwake || d.AllForced) || !_names.TryGetValue(w, out string name)) continue;
                Vector3 dp = w.transform.position - d.Pos0;
                float xz = new Vector2(dp.x, dp.z).magnitude * 100f, dy = Mathf.Abs(dp.y) * 100f;
                if (!groups.TryGetValue(name, out float[] g)) { g = new float[10]; groups[name] = g; }
                int o = d.AllAwake ? 0 : 5;
                g[o]++; g[o + 1] += xz; g[o + 2] = Mathf.Max(g[o + 2], xz); g[o + 3] += dy; g[o + 4] = Mathf.Max(g[o + 4], dy);
            }
            if (groups.Count == 0) return;
            var sb = new StringBuilder(256);
            sb.Append("parkeddrift cm/window");
            foreach (var kv in groups)
            {
                float[] g = kv.Value;
                sb.Append(" | ").Append(kv.Key);
                for (int o = 0; o <= 5; o += 5)
                {
                    if (g[o] == 0) continue;
                    sb.Append(o == 0 ? " awake" : " forced").Append(" n=").Append(g[o])
                      .Append(" xz=").Append(F1(g[o + 1] / g[o])).Append('/').Append(F1(g[o + 2]))
                      .Append(" dy=").Append(F1(g[o + 3] / g[o])).Append('/').Append(F1(g[o + 4]));
                }
            }
            Perf.Log("[Physics] " + sb);
        }
        // ---------------------------------------------------------------- log

        static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            if (now >= _nextClean)
            {
                _nextClean = now + 30f;
                Prune(_names); Prune(_lastPush); Prune(_lastOffNav); Prune(_lastMovingPush); Prune(_lastVanillaSleep); Prune(_tracks);
            }
            if (S.CensusLogSeconds <= 0f || now < _nextLog) return;
            if (_lastLog > 0f) { WriteLog(); WriteDrift(); }
            _tracks.Clear();
            _lastLog = now;
            _nextLog = now + S.CensusLogSeconds;
            _agg.Clear();
            _samples = 0;
        }

        static readonly List<VehicleWheeled> _dead = new List<VehicleWheeled>();

        static void Prune<T>(Dictionary<VehicleWheeled, T> d)
        {
            _dead.Clear();
            foreach (var kv in d) if (!kv.Key) _dead.Add(kv.Key);
            for (int i = 0; i < _dead.Count; i++) d.Remove(_dead[i]);
            _dead.Clear();
        }

        static string F1(float v) => v.ToString("0.###");

        static void WriteLog()
        {
            if (_agg.Count == 0) return;
            var sb = new StringBuilder(1024);
            sb.Append("wheeledsleep samples=").Append(_samples);
            int veh = 0, awake = 0;
            foreach (var kv in _agg)
            {
                Agg a = kv.Value;
                veh += a.Veh;
                awake += a.Awake;
                sb.Append(" | ").Append(kv.Key).Append(" veh=").Append(a.Veh).Append(" asleep=").Append(a.Asleep).Append(" awake=").Append(a.Awake)
                  .Append(" kin=").Append(a.Kin).Append(" input=").Append(a.Input).Append("(").Append(a.InFb).Append('/').Append(a.InEng).Append('/').Append(a.InTurn).Append(')')
                  .Append(" flip=").Append(a.Flip).Append(" noWheels=").Append(a.NoWheels)
                  .Append(" fast=").Append(a.Fast).Append("(").Append(a.FastAng).Append('/').Append(a.FastLin).Append(", ").Append(a.FastSteer).Append('/').Append(a.FastFixed).Append(')')
                  .Append(" timer=").Append(a.Timer).Append(" condMet=").Append(a.CondMet)
                  .Append(" partial=").Append(a.Partial).Append(" extraAwake=").Append(a.ExtraAwake).Append(" rewoke=").Append(a.Rewoke)
                  .Append(" pushed=").Append(a.Pushed).Append(" offNav=").Append(a.OffNav).Append(" slope=").Append(a.Slope)
                  .Append(" fastPushed=").Append(a.FastPushed).Append(" fastSlope=").Append(a.FastSlope)
                  .Append(" chassisV=").Append(F1(a.ChassisV)).Append(" chassisW=").Append(F1(a.ChassisW))
                  .Append(" wheelW=").Append(F1(a.WheelWx)).Append('/').Append(F1(a.WheelWy)).Append('/').Append(F1(a.WheelWz))
                  .Append(" wheelV=").Append(F1(a.WheelV))
                  .Append(" thr=").Append(F1(Mathf.Sqrt(a.ThrAngMin))).Append('-').Append(F1(Mathf.Sqrt(a.ThrAngMax)))
                  .Append('/').Append(F1(Mathf.Sqrt(a.ThrLinMin))).Append('-').Append(F1(Mathf.Sqrt(a.ThrLinMax)))
                  .Append(" lowTickFix=").Append(a.LowTickFix ? 1 : 0).Append(" bodies=").Append(a.Bodies).Append(" wheels=").Append(a.Wheels);
            }
            Item.Info = "last interval: " + awake + "/" + veh + " wheeled vehicle-samples awake";
            Perf.Log("[Physics] " + sb);
        }
    }
}
