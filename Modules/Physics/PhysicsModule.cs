/*
 Si_ServerPerf module "Physics": DEV_REPORT S3 (fewer awake dynamic rigidbodies) + the physics part of S13
 (catch-up cap), plus a census so the rig can see what each item does. Verified against Silica 1.0.17.

 What the 1.0.17 code already does (so this module does NOT re-implement it):
   Soldier         puts its own body to sleep when idle: OrderedFixedUpdatePartTwo sets linearVelocity = 0 and calls
                   Rigidbody.Sleep() when there is no input, no fall/slide, no kinematic ground (Soldier.cs:2989-3001),
                   and returns early while asleep (:2875-2882). Wakes itself on input or when standing on a body.
   VehicleWheeled  puts body + wheel bodies to sleep after 1 s with no input and all wheels slow
                   (m_fSleepSlowTime, OrderedFixedUpdate :1402/1486, OrderedUpdate :613 -> SleepVehicle :558);
                   wakes on input (:1306-1317).
   CreatureDecapod returns early while asleep and no input (:3765-3772); relies on PhysX auto-sleep (no Sleep call).
   VehicleHovered  hover force every step (AddForce :1649, unconditional): awake by design. VehicleAir: flying.
   Structures      no Rigidbody (static colliders).
 So the question is not "nothing sleeps" but "what wakes them again" -> Census (always) + WakeProbe (on demand).

 Items (all switchable at runtime):
   Census                 ON   read-only 1 Hz body census + 2-step wake window + step bursts      (Census.cs)
   WakeProbe              off  per-method wake attribution (IsSleeping before/after)               (WakeProbe.cs)
   WheeledSleepDiag       off  read-only: why each awake parked wheeled vehicle fails vanilla's sleep test (WheeledSleepDiag.cs)
   RestWriteGuards        off  skip redundant same-value writes in soldier/creature fixed updates  (Guards.cs)
   HitZonePoseGuard       off  skip same-value re-pose of the kinematic hit-zone body per frame     (Guards.cs)
   IdleForceSleep         off  end-of-step Sleep() for idle AI wheeled vehicles / ground creatures  (IdleSleep.cs)
   AvoidanceIdlePairSkip  off  no avoidance impulses between two idle AI vehicles                  (Avoidance.cs)
   AvoidanceSkipOntoResting off no avoidance push onto an idle AI vehicle from a non-moving unit   (Avoidance.cs)
   AvoidanceNoIdleDetour  off  no avoidance detour drive for AI units without a destination      (IdleDetour.cs)
   IdleSleepDiag          off  re-wake attribution for IdleForceSleep (probe on every avoidance call) (IdleSleep.cs)
   ParkedWheelHold        off  with IdleForceSleep: keep held vehicles' steering wheels asleep (WheelHold.cs)
   WheelJointWriteGuard   off  same-value guard for the wheel drive-mode / low-tick rotation writes (WheelJointGuard.cs)
   VehicleSolverIterations off experiment: fewer solver iterations on AI wheeled vehicles   (WheelJointGuard.cs)
   FixedWakeTrace         off  which ordered fixed callback wakes a force-slept vehicle (+ changed transforms) (FixedWakeTrace.cs)
   CatchUpCap             ON   Time.maximumDeltaTime = MaxCatchUpSteps x fixedDeltaTime           (TimeStep.cs)
   FixedStep_Override     off  Time.fixedDeltaTime = 1 / FixedStepHz (20 Hz)                        (TimeStep.cs)
   Interpolation_Off      off  no Rigidbody interpolation on the headless server (presentation)    (Interpolation.cs)
   DeadSettle             off  destroyed units: sleep bodies once slow, skip wreck fixed update, drop avoidance (DeadSettle.cs)
   DeadDisposeEarly       ON   VISIBLE: destroyed units disposed after DisposeEarlySeconds (5 s) instead of 15 s, or fps-proportional (DeadDispose.cs)
   DeadAgentHalt          ON   destroyed units' AI agents stop updating (BT, target scans, LOS, paths)  (DeadAgents.cs)
   DeadNoUnitContact      off  destroyed units' bodies exclude contacts with unit layers (not shoved)   (DeadContact.cs)
   (Census also splits units/bodies into destroyed and alive, DeadCensus.cs)
*/

using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    public sealed class PhysicsSettings
    {
        // Census: sample period (s) and how often one census line is written to the log (s, 0 = never; Info only).
        public float CensusIntervalSeconds = 1f;
        public float CensusLogSeconds = 10f;

        // CatchUpCap: at most this many fixed steps per rendered frame (Time.maximumDeltaTime = N * fixedDeltaTime,
        // never raised above the game's own value). 2 at 25 Hz = 0.08 s; the game ships 0.1 s (= up to 3 steps).
        public int MaxCatchUpSteps = 2;

        // FixedStep_Override: physics rate while the item is on (game: GameManager.PhysicsTickHz = 25).
        public int FixedStepHz = 20;

        // IdleForceSleep: a unit counts as idle when its own inputs are zero, it has no move order and every body is
        // slower than these limits; after IdleSleepSeconds of that it is put to sleep at the end of the fixed step.
        public float IdleSleepSeconds = 2f;
        public float IdleLinearSpeed = 0.1f;   // m/s, chassis / creature body
        public float IdleAngularSpeed = 0.1f;  // rad/s, chassis / creature body
        // Wheel bodies of a parked vehicle (jointed): vanilla's own test is 5 deg/s (0.087 rad/s) and 0.3 m/s per wheel;
        // these looser limits let IdleForceSleep catch wheels that keep twitching on their joints while the chassis is still.
        public float IdleWheelAngularSpeed = 0.5f;  // rad/s
        public float IdleWheelLinearSpeed = 0.3f;   // m/s
        // Each unit is checked every N fixed steps (round robin), units already put to sleep every step.
        public int IdleCheckStrideSteps = 5;

        // AvoidanceIdlePairSkip: a vehicle counts as idle for avoidance below this speed (m/s), no move order, no input.
        public float AvoidanceIdleSpeed = 0.5f;

        // VehicleSolverIterations (experiment): solver iterations for alive AI wheeled vehicles' bodies (game: project default,
        // 6 / 1); velocity iterations 0 = leave unchanged.
        public int VehicleSolverIterations = 4;
        public int VehicleSolverVelocityIterations = 0;

        // DeadSettle (DeadSettle.cs): a destroyed unit is put to sleep no earlier than DeadSettleDelaySeconds after its death
        // was seen, once every dynamic body has stayed below the speed limits for DeadSettleSlowSeconds; never after
        // DeadSettleGiveUpSeconds (vanilla disposes at 15 s). Discovery scans every unit every DeadSettleScanStrideSteps steps
        // (also used by DeadDisposeEarly).
        public float DeadSettleDelaySeconds = 3f;
        public float DeadSettleSlowSeconds = 0.5f;
        public float DeadSettleGiveUpSeconds = 12f;
        public float DeadSettleLinearSpeed = 0.3f;        // m/s, every dynamic body
        public float DeadSettleAngularSpeed = 0.5f;       // rad/s, main body (chassis / corpse)
        public float DeadSettleWheelAngularSpeed = 3f;    // rad/s, other bodies (wheels spin freely on a wreck; not streamed)
        // Disable + unregister the dead unit's avoidance capsule. Default false since b13_dead: the freed overlap slots let
        // live agents find more live neighbours, CollisionAvoidance rose 26.6 -> 32.7 ms/s at 528 units.
        public bool DeadSettleRemoveAvoidance = false;
        public int DeadSettleScanStrideSteps = 5;

        // DeadDisposeEarly (DeadDispose.cs, visible to players): disposal time of destroyed units (game: 15 s).
        public float DisposeEarlySeconds = 5f;
        // Low = High = 0 (default since 0.2.0): always DisposeEarlySeconds. Proportional control (DeadDispose.cs, b17-0.1.0
        // defaults 25 / 45): disposal time = the unit's own (15 s) at smoothed fps >= DisposeEarlyFpsHigh, linear down to
        // DisposeEarlySeconds at <= DisposeEarlyFpsLow. In both modes the target moves at most
        // DisposeEarlySlewSecondsPerSecond seconds per second (b15: the open/closed valve ran a limit cycle), so "always"
        // reaches 5 s ~20 s after start / scene change / switch-on.
        public float DisposeEarlyFpsLow = 0f;
        public float DisposeEarlyFpsHigh = 0f;
        public float DisposeEarlyFpsSmoothSeconds = 10f;
        public float DisposeEarlySlewSecondsPerSecond = 0.5f;
        public bool DisposeEarlySoldiers = true;
        public bool DisposeEarlyCreatures = true;
        public bool DisposeEarlyVehicles = true;

        // DeadAgentHalt (DeadAgents.cs): a dead unit's AI agent stops updating this long after its death was seen; ClearAim
        // also drops its aim / assigned attack target once (the live target is no longer "attacked" by a corpse).
        public float DeadAgentHaltDelaySeconds = 1f;
        public bool DeadAgentHaltClearAim = false;   // b14: true removes corpse "attackers" from target saturation

        // DeadNoUnitContact (DeadContact.cs): per kind; layers excluded from dead bodies' contacts (by name: live soldiers /
        // creatures and AI-driven vehicles; add "Vehicle" for player-driven ones); layers that are never excluded.
        public bool DeadNoContactCreatures = true;
        public bool DeadNoContactVehicles = false;
        public bool DeadNoContactSoldiers = false;
        public string DeadNoContactLayers = "Character,Vehicle Avoidance,Ignore Raycast Avoidance,Invisible Walls Avoidance";
        public string DeadNoContactProtectedLayers = "Default,Terrain,Static,StaticSmall,Invisible Walls,Water,AIObstacle";
    }

    [PerfModuleOrder(0)]
    public sealed class PhysicsModule : IPerfModule
    {
        public string Name => "Physics";
        public string Description => "S3 fewer awake rigidbodies (census, wake probe, write guards, idle sleep, idle avoidance) + catch-up cap / fixed step";

        internal static PhysicsSettings S;

        public void Apply(ModuleContext ctx)
        {
            S = ctx.Settings<PhysicsSettings>();
            Census.Register(ctx, S);
            WakeProbe.Register(ctx);
            WheeledSleepDiag.Register(ctx, S);
            RestWriteGuards.Register(ctx);
            HitZonePoseGuard.Register(ctx);
            IdleForceSleep.Register(ctx, S);
            ParkedWheelHold.Register(ctx, S);
            WheelJointWriteGuard.Register(ctx);
            VehicleSolverIterations.Register(ctx, S);
            AvoidanceIdlePairs.Register(ctx, S);
            AvoidanceSkipOntoResting.Register(ctx, S);
            AvoidanceNoIdleDetour.Register(ctx);
            FixedWakeTrace.Register(ctx);
            TimeStep.Register(ctx, S);
            InterpolationOff.Register(ctx);
            DeadSettle.Register(ctx, S);
            DeadDisposeEarly.Register(ctx, S);
            DeadAgentHalt.Register(ctx, S);
            DeadNoUnitContact.Register(ctx, S);
        }
    }

    // Counters other items bump; the census prints interval deltas. Main thread only.
    internal static class Counters
    {
        public static long GuardSkips, GuardWrites;
        public static long PoseSkips, PoseWrites;
        public static long AvoidCalls, AvoidSkips;
        public static long ForcedSleeps, ReSleeps, ForcedReleased, ForcedWakes, SleepBlockedByForce;
        public static int ForcedNow;
    }

    // Why a vehicle is not "idle" (first failing test). Shared by IdleForceSleep and the avoidance items so their
    // diagnostics use one vocabulary.
    internal enum IdleWhy : byte
    {
        Ok, NotVehicle, Destroyed, Compartment, NotAI, MoveOrder, InputFwd, InputTurn, Engine, Kinematic, Hop, ForceMove, Flipped,
        NoBody, Speed, WheelFast, Count
    }

    internal static class IdleTest
    {
        public static readonly string[] Names =
        {
            "ok", "notVehicle", "destroyed", "compartment", "notAI", "moveOrder", "inputFwd", "inputTurn", "engine", "kinematic", "hop",
            "forceMove", "flipped", "noBody", "speed", "wheelFast",
        };

        // VehicleWheeled.m_fCurrentTurnRate: the steering actually applied, which follows VehicleInput_RightLeft.
        static readonly HarmonyLib.AccessTools.FieldRef<VehicleWheeled, float> _turnRate = Refl.Field<VehicleWheeled, float>("m_fCurrentTurnRate");

        // Vehicle-level idle test. lin/ang: chassis limits (ang < 0 = not tested); engine: also require CurrentEngineSpeed01 == 0.
        public static IdleWhy Vehicle(Unit u, float lin, float ang, bool engine)
        {
            if (!(u is Vehicle v)) return IdleWhy.NotVehicle;
            if (v.IsDestroyed) return IdleWhy.Destroyed;
            if (v.InCompartment) return IdleWhy.Compartment;
            if (!v.AIControlled) return IdleWhy.NotAI;
            if (v.HasMoveOrder) return IdleWhy.MoveOrder;
            // Input = vanilla's own sleep test (VehicleWheeled.OrderedFixedUpdate :1253): forward input, or steering input
            // that differs from the steering already applied. A parked AI vehicle keeps its last VehicleInput_RightLeft
            // forever (UpdateAIInput copies it at :785/:800 and writes it back at :1075 when it has no destination), with
            // ForwardBack 0 and brakes on; that is a held steering angle, not movement, and vanilla sleeps it anyway.
            if (v.VehicleInput_ForwardBack != 0f) return IdleWhy.InputFwd;
            if (v is VehicleWheeled vw && _turnRate != null)
            {
                if (v.VehicleInput_RightLeft != _turnRate(vw)) return IdleWhy.InputTurn;
            }
            else if (v.VehicleInput_RightLeft != 0f) return IdleWhy.InputTurn;
            if (engine && v.CurrentEngineSpeed01 != 0f) return IdleWhy.Engine;
            if (v.IsKinematicMode) return IdleWhy.Kinematic;
            if (v.IsRecoveryHopping) return IdleWhy.Hop;
            if (v.HasForceMove) return IdleWhy.ForceMove;
            VehicleAntiFlip af = v.AntiFlip;
            if (af && af.IsFlipped) return IdleWhy.Flipped;
            Rigidbody rb = v.RigidBody;
            if (!rb || rb.isKinematic) return IdleWhy.NoBody;
            if (rb.linearVelocity.sqrMagnitude > lin * lin) return IdleWhy.Speed;
            if (ang >= 0f && rb.angularVelocity.sqrMagnitude > ang * ang) return IdleWhy.Speed;
            return IdleWhy.Ok;
        }

        public static void Append(System.Text.StringBuilder sb, long[] counts)
        {
            for (int i = 1; i < (int)IdleWhy.Count; i++)
            {
                if (counts[i] == 0) continue;
                sb.Append(' ').Append(Names[i]).Append('=').Append(counts[i]);
            }
        }
    }

    internal enum BodyKind : byte { Soldier, Creature, Wheeled, Hover, Air, OtherVehicle, Other }

    internal static class Kinds
    {
        public const int Count = 7;
        public static readonly string[] Names = { "soldier", "creature", "wheeled", "hover", "air", "vehicle", "other" };

        public static BodyKind Of(Unit u)
        {
            if (u is Soldier) return BodyKind.Soldier;
            if (u is CreatureDecapod) return BodyKind.Creature;
            if (u is VehicleWheeled) return BodyKind.Wheeled;
            if (u is VehicleHovered) return BodyKind.Hover;
            if (u is VehicleAir) return BodyKind.Air;
            if (u is Vehicle) return BodyKind.OtherVehicle;
            return BodyKind.Other;
        }
    }

    internal static class Vel
    {
        public static bool Slow(Rigidbody rb, float lin, float ang)
        {
            Vector3 v = rb.linearVelocity;
            if (v.sqrMagnitude > lin * lin) return false;
            Vector3 w = rb.angularVelocity;
            return w.sqrMagnitude <= ang * ang;
        }

        public static bool Same(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool Same(Quaternion a, Quaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
    }
}
