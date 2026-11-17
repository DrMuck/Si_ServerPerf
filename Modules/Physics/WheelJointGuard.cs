/*
 Two wheeled-vehicle physics items for b18 (both default OFF).

 WheelJointWriteGuard (exact): VehicleWheeled.OrderedFixedUpdate (1.0.17) runs, for every AWAKE jointed wheel of every
 vehicle, every fixed step:
   :1345  joint.rotationDriveMode = RotationDriveMode.XYAndZ      unconditional
   :1380  rigidBody.linearVelocity = MoveTowards(v, ground, ...)  brakes (only when braking and the wheel touches ground)
   :1399  rigidBody.angularVelocity = TransformVectorUnscaled(...)  motor / brake spin write (not when flipped)
   :1411-1415 joint.angularYLimit = ...    already guarded by LastAppliedSteerLimit (Mathf.Approximately)
   :1423-1427 joint.targetRotation = ...   already guarded by LastAppliedTargetRotation (dot < 0.99999)
   :1428-1439 rigidBodyTransform.rotation = ... (ApplyLowTickWheelFix, steering wheels) unconditional
 The only other writer of rotationDriveMode is SleepVehicle (:570, Slerp before putting the wheels to sleep), so on an
 awake vehicle the :1345 write is the same value every step after the first one after a wake.
 Guarded here (transpiler, exact call-site counts from the 1.0.17 IL: 1 set_rotationDriveMode, 1 Transform.set_rotation
 in this method, otherwise NOT APPLIED and the method is left unchanged):
   :1345  written only when joint.rotationDriveMode differs. After SleepVehicle (Slerp) the first awake step still
          writes XYAndZ, so a woken vehicle never keeps a stale Slerp drive.
   :1438  skipped only when the new rotation is bitwise equal to transform.rotation; such a write would only mark the
          transform changed and teleport the wheel body to the pose it already has at the next transform sync. Any
          difference, even one ulp, is written.
 Not guarded: :1380 / :1399. A same-value velocity write still resets the body's PhysX wake counter (autowake), which
 keeps an awake wheel from dozing off on its own while the chassis is awake; skipping it would change sleep timing,
 so it is not a provably same-effect write.
   [Physics] wheeljoint driveSkip= driveWrite= rotSkip= rotWrite=   (per log interval)

 VehicleSolverIterations (approximate, experiment only): Rigidbody.solverIterations (and optionally
 solverVelocityIterations) of every dynamic body of every alive, AI-controlled VehicleWheeled set to
 VehicleSolverIterations (default 4) / VehicleSolverVelocityIterations (default 0 = leave). Nothing in 1.0.17 sets
 solver iterations (no code reference), so every body carries Physics.defaultSolverIterations /
 defaultSolverVelocityIterations from the project settings (findings D: PGS, 6 position / 1 velocity); the values
 found are noted at the first sweep and each body's own values are stored and restored. 1 Hz sweep over Unit.Units:
 applies to new AI vehicles, restores a vehicle that becomes player-controlled, seated or destroyed; switch-off
 restores everything still changed.
 PhysX solves an island with the MAXIMUM iteration count of its bodies, so the lower count only takes effect for
 islands made only of reduced bodies: a lone vehicle (chassis + jointed wheels on terrain - static terrain does not
 join islands), or a jam of AI vehicles only. As soon as a soldier, creature, player vehicle or any other default body
 touches the jam, that whole island is solved at 6 again. So in a mixed melee the saving is partial by construction.
 Expected visible consequences while on: softer, bouncier wheel suspension and joint sag (joint error is corrected
 over more steps), more interpenetration and slower separation in vehicle-vehicle jams and pile-ups, possibly more
 jitter of parked vehicles on slopes; in the extreme, wheels visibly offset from their hubs. One flip measurement only:
 the question is whether the solver is a large part of PhysX time at all.
   [Physics] solveriter target=4/0 default=6/1 vehicles= bodies= restored=
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class WheelJointWriteGuard
    {
        internal static bool On;
        static PerfItem Item;
        static long _driveSkip, _driveWrite, _rotSkip, _rotWrite;
        static float _nextLog;

        static readonly MethodInfo SetDriveMode = AccessTools.PropertySetter(typeof(ConfigurableJoint), nameof(ConfigurableJoint.rotationDriveMode));

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("WheelJointWriteGuard",
                "VehicleWheeled.OrderedFixedUpdate: write joint.rotationDriveMode (:1345) and the low-tick wheel rotation (:1438) only " +
                "when the value differs; identical values otherwise",
                v => On = v, defaultOn: false);
            var m = Item.Target(typeof(VehicleWheeled), "OrderedFixedUpdate", new[] { typeof(float) }, declaredOnly: true);
            Item.Patch(m, typeof(WheelJointWriteGuard), transpiler: nameof(Transpiler));
            ctx.OnUpdate(Tick, Item);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins) =>
            GuardIL.Swap(ins, Item, "VehicleWheeled.OrderedFixedUpdate",
                (SetDriveMode, 1, AccessTools.Method(typeof(WheelJointWriteGuard), nameof(DriveMode))),
                (GuardIL.SetRot, 1, AccessTools.Method(typeof(WheelJointWriteGuard), nameof(Rotation))));

        // helpers called from the transpiled IL: same stack shape as the setters they replace
        public static void DriveMode(ConfigurableJoint joint, RotationDriveMode mode)
        {
            if (On && (object)joint != null)
            {
                try
                {
                    if (joint.rotationDriveMode == mode) { _driveSkip++; return; }
                }
                catch (Exception ex) { Item.Fail(ex, "rotationDriveMode guard"); }
            }
            _driveWrite++;
            joint.rotationDriveMode = mode;
        }

        public static void Rotation(Transform t, Quaternion value)
        {
            if (On && (object)t != null)
            {
                try
                {
                    if (Vel.Same(t.rotation, value)) { _rotSkip++; return; }
                }
                catch (Exception ex) { Item.Fail(ex, "wheel rotation guard"); }
            }
            _rotWrite++;
            t.rotation = value;
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
                Perf.Log("[Physics] wheeljoint driveSkip=" + _driveSkip + " driveWrite=" + _driveWrite + " rotSkip=" + _rotSkip + " rotWrite=" + _rotWrite);
                Item.Info = "last interval: drive mode " + _driveSkip + " skipped / " + _driveWrite + " written, rotation " + _rotSkip + " / " + _rotWrite;
            }
            _driveSkip = _driveWrite = _rotSkip = _rotWrite = 0;
        }
    }

    internal static class VehicleSolverIterations
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;

        struct Orig { public int Pos, Vel; }
        static readonly Dictionary<Rigidbody, Orig> _orig = new Dictionary<Rigidbody, Orig>();
        static readonly Dictionary<Unit, Rigidbody[]> _applied = new Dictionary<Unit, Rigidbody[]>();
        static readonly List<Unit> _drop = new List<Unit>();
        static float _nextSweep, _nextLog;
        static long _restored;
        static bool _noted;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("VehicleSolverIterations",
                "Experiment: Rigidbody.solverIterations (VehicleSolverIterations, default 4) / solverVelocityIterations " +
                "(VehicleSolverVelocityIterations, 0 = leave) on alive AI wheeled vehicles; restored on switch-off; islands use the max",
                v => On = v, defaultOn: false);
            if (S.VehicleSolverIterations < 1 || S.VehicleSolverIterations > 255) Item.Block("VehicleSolverIterations must be 1..255");
            if (S.VehicleSolverVelocityIterations < 0 || S.VehicleSolverVelocityIterations > 255) Item.Block("VehicleSolverVelocityIterations must be 0..255");
            if (Item.Hooked("1 Hz sweep over Unit.Units")) ctx.OnUpdate(Tick, Item);
            Item.OnSwitch(on => { if (!on) RestoreAll(); else _nextSweep = 0f; });
            ctx.OnSceneLoaded(_ => { _orig.Clear(); _applied.Clear(); });
        }

        static bool Wanted(Unit u) => u is VehicleWheeled && !u.IsDestroyed && !u.InCompartment && u.AIControlled;

        static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            if (now >= _nextSweep)
            {
                _nextSweep = now + 1f;
                Sweep();
            }
            if (S.CensusLogSeconds > 0f && now >= _nextLog)
            {
                bool first = _nextLog == 0f;
                _nextLog = now + S.CensusLogSeconds;
                if (!first)
                {
                    int bodies = 0;
                    foreach (var kv in _applied) bodies += kv.Value.Length;
                    Perf.Log("[Physics] solveriter target=" + S.VehicleSolverIterations + "/" + S.VehicleSolverVelocityIterations +
                             " default=" + UnityEngine.Physics.defaultSolverIterations + "/" + UnityEngine.Physics.defaultSolverVelocityIterations +
                             " vehicles=" + _applied.Count + " bodies=" + bodies + " restored=" + _restored);
                    Item.Info = _applied.Count + " vehicles / " + bodies + " bodies at " + S.VehicleSolverIterations + " iterations";
                }
                _restored = 0;
            }
        }

        static void Sweep()
        {
            if (!_noted)
            {
                _noted = true;
                Item.Note("Physics.defaultSolverIterations " + UnityEngine.Physics.defaultSolverIterations +
                          ", defaultSolverVelocityIterations " + UnityEngine.Physics.defaultSolverVelocityIterations + " (1.0.17 code sets none)");
            }
            // restore vehicles that no longer qualify (or are gone)
            _drop.Clear();
            foreach (var kv in _applied) if (!kv.Key || !Wanted(kv.Key)) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) Restore(_drop[i]);
            _drop.Clear();
            // apply to new ones
            List<Unit> units = Unit.Units;
            if (units == null) return;
            int pos = S.VehicleSolverIterations, vel = S.VehicleSolverVelocityIterations;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (!u || _applied.ContainsKey(u) || !Wanted(u)) continue;
                Rigidbody[] all = Census.BodiesOf(u);
                var mine = new List<Rigidbody>(all.Length);
                for (int k = 0; k < all.Length; k++)
                {
                    Rigidbody rb = all[k];
                    if (!rb || rb.isKinematic) continue;
                    if (!_orig.ContainsKey(rb)) _orig[rb] = new Orig { Pos = rb.solverIterations, Vel = rb.solverVelocityIterations };
                    if (rb.solverIterations != pos) rb.solverIterations = pos;
                    if (vel > 0 && rb.solverVelocityIterations != vel) rb.solverVelocityIterations = vel;
                    mine.Add(rb);
                }
                _applied[u] = mine.ToArray();
            }
        }

        static void Restore(Unit u)
        {
            if (!_applied.TryGetValue(u, out Rigidbody[] bodies)) return;
            _applied.Remove(u);
            for (int k = 0; k < bodies.Length; k++)
            {
                Rigidbody rb = bodies[k];
                if ((object)rb == null) continue;
                if (_orig.TryGetValue(rb, out Orig o))
                {
                    _orig.Remove(rb);
                    if (!rb) continue;
                    if (rb.solverIterations != o.Pos) rb.solverIterations = o.Pos;
                    if (rb.solverVelocityIterations != o.Vel) rb.solverVelocityIterations = o.Vel;
                }
            }
            _restored++;
        }

        static void RestoreAll()
        {
            int n = _applied.Count;
            try
            {
                _drop.Clear();
                foreach (var kv in _applied) _drop.Add(kv.Key);
                for (int i = 0; i < _drop.Count; i++) Restore(_drop[i]);
                _drop.Clear();
            }
            catch (Exception ex) { Perf.Warn("Physics.VehicleSolverIterations restore: " + ex.Message); }
            _applied.Clear();
            _orig.Clear();
            Item.Info = "last switch-off: " + n + " vehicles restored";
        }
    }
}
