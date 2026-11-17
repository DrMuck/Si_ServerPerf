/*
 Physics step timing.

 What the game sets (1.0.17): GameManager.Start (:645) sets Time.fixedDeltaTime = 1 / PhysicsTickHz (field, 25 ->
 0.04 s) every time a GameManager starts. Nothing in the code sets Time.maximumDeltaTime; the project value is 0.1 s
 (findings D, TimeManager asset). Physics.simulationMode is FixedUpdate, so GameManager.SimulatePhysics is dead and
 Unity's own fixed loop runs: per frame dt is clamped to maximumDeltaTime and fixed steps run until fixed time catches
 up. With 0.1 / 0.04 a slow frame is followed by up to 3 fixed steps (0.1 + carry < 0.14), each one a full PhysX step
 plus the ordered fixed list (~20 us per unit per step, findings F), which makes the next frame slow again.

 CatchUpCap (default ON): Time.maximumDeltaTime = MaxCatchUpSteps x fixedDeltaTime (2 -> 0.08 s, at most 2 steps per
 frame), never above the value found at startup. Re-applied whenever fixedDeltaTime changes. Effect is limited to
 frames longer than 80 ms: game time (Time.time / deltaTime / fixedTime) then advances 80 ms instead of up to 100 ms
 for that frame, i.e. the clamp vanilla already applies above 100 ms starts at 80 ms. Unscaled time (network ticks,
 NetworkComponent.TickNetworkObjects) is not affected. Switch-off restores the startup value.

 FixedStep_Override (default OFF): Time.fixedDeltaTime = 1 / FixedStepHz (20 -> 0.05 s); postfix on GameManager.Start
 re-applies it after the game resets the step. Switch-off restores 1 / GameManager.PhysicsTickHz.
 Tied to the step COUNT, not to dt (1.0.17 code):
   Soldier ground spherecast every 3rd step for AI soldiers (UpdateGroundStep m_AI_GroundStepCheckDelay): 8.3 -> 6.7 Hz
   CollisionAvoidanceManager broadphase every 4th step per agent (BROADPHASE_STRIDE): 6.25 -> 5 Hz; narrowphase and
     avoidance impulses per step (impulse = f * dt, so the same per second)
   CollisionAvoidance off-navmesh check every ceil(agents/8) steps
   VehicleWheeled: ApplyWheelSpinForceScale blend = Clamp01(0.04 / dt) (:1322, hard-coded 25 Hz): x0.8 at 20 Hz for
     prefabs with ApplyWheelSpinForceScale < 1
   OnCollisionStay / OnTriggerStay fire per step: CollisionApplyDamage gates on collision.impulse.sqrMagnitude > 1000
     and impulse per step grows with dt (+25 %), so that gate opens earlier (damage itself is * dt); MiningArmsRoller,
     Vehicle stuck contacts, CreatureDecapod ground latch, SiegeObjective capture (- Time.deltaTime per collider) per step
   NetworkTransformComponent.OrderedFixedUpdate change check per step; bodyless (static) NTCs send a reliable RPC per
     step when moved -> fewer RPCs at 20 Hz
   PhysX itself: 50 ms integration step - wheel ConfigurableJoints and contacts less stiff, deeper penetration of
     fast bodies (discrete collision; ContinuousSpeculative only for a selected creature), soldier step-up snap per step
 Not tied to the step: network send rate (Update, unscaled dt, NetworkComponent.TickRateHz), weapon fire rates and
 projectiles (Update / LateUpdate), fixed-late list (runs once per frame in LateUpdate), KinematicPhysics and the
 ordered fixed handlers (dt = fixedTime difference).
*/

using System;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class TimeStep
    {
        internal static bool CapOn, StepOn;
        static PerfItem CapItem, StepItem;
        static PhysicsSettings S;

        static float _origMaxDt = -1f, _wroteMaxDt = -1f, _capForFixedDt = -1f;
        static float _wroteFixedDt = -1f;
        static float _nextCapCheck, _nextStepCheck;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            CapItem = ctx.Item("CatchUpCap",
                "Time.maximumDeltaTime = MaxCatchUpSteps x fixedDeltaTime (default 2 -> at most 2 fixed steps per frame; game: 0.1 s = up to 3)",
                v => CapOn = v, defaultOn: true);
            if (S.MaxCatchUpSteps < 1 || S.MaxCatchUpSteps > 10) CapItem.Block("MaxCatchUpSteps must be 1..10");
            if (CapItem.Hooked("Time.maximumDeltaTime (1 Hz check)")) ctx.OnUpdate(CapTick, CapItem);
            CapItem.OnSwitch(on => { if (on) ApplyCap(); else RestoreCap(); });

            StepItem = ctx.Item("FixedStep_Override",
                "Time.fixedDeltaTime = 1 / FixedStepHz (default 20 Hz; game 25 Hz). Changes everything that counts fixed steps, see TimeStep.cs",
                v => StepOn = v, defaultOn: false);
            if (S.FixedStepHz < 10 || S.FixedStepHz > 60) StepItem.Block("FixedStepHz must be 10..60");
            var start = StepItem.Target(typeof(GameManager), "Start", Type.EmptyTypes);
            StepItem.Patch(start, typeof(TimeStep), postfix: nameof(PostGameManagerStart));
            if (StepItem.Hooked("Time.fixedDeltaTime (1 Hz check)")) ctx.OnUpdate(StepTick, StepItem);
            StepItem.OnSwitch(on => { if (on) ApplyStep(); else RestoreStep(); });
        }

        // ---------------------------------------------------------------- catch-up cap

        static void CapTick()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextCapCheck) return;
            _nextCapCheck = now + 1f;
            if (_wroteMaxDt < 0f || Time.fixedDeltaTime != _capForFixedDt || Time.maximumDeltaTime != _wroteMaxDt) ApplyCap();
        }

        internal static void ApplyCap()
        {
            if (!CapOn) return;
            try
            {
                float fdt = Time.fixedDeltaTime;
                float cur = Time.maximumDeltaTime;
                if (_origMaxDt < 0f || (_wroteMaxDt >= 0f && cur != _wroteMaxDt))
                {
                    // first time, or someone else set it since: that value is the one to cap and to restore
                    _origMaxDt = cur;
                    CapItem.Note("found maximumDeltaTime " + cur.ToString("0.###") + " s, fixedDeltaTime " + fdt.ToString("0.###") + " s");
                }
                float target = Mathf.Min(_origMaxDt, Mathf.Max(fdt, S.MaxCatchUpSteps * fdt));
                if (cur != target) Time.maximumDeltaTime = target;
                _wroteMaxDt = Time.maximumDeltaTime;
                _capForFixedDt = fdt;
                CapItem.Info = "maximumDeltaTime " + _origMaxDt.ToString("0.###") + " -> " + _wroteMaxDt.ToString("0.###") +
                               " s (fixedDeltaTime " + fdt.ToString("0.###") + ", <= " + Mathf.FloorToInt(_wroteMaxDt / fdt + 1e-4f) + " steps/frame)";
            }
            catch (Exception ex) { CapItem.Fail(ex, "apply maximumDeltaTime"); }
        }

        static void RestoreCap()
        {
            try
            {
                if (_origMaxDt > 0f && _wroteMaxDt >= 0f && Time.maximumDeltaTime == _wroteMaxDt) Time.maximumDeltaTime = _origMaxDt;
                if (_origMaxDt > 0f) CapItem.Info = "restored maximumDeltaTime " + Time.maximumDeltaTime.ToString("0.###") + " s";
            }
            catch (Exception ex) { Perf.Warn("Physics.CatchUpCap restore: " + ex.Message); }
            _wroteMaxDt = -1f;
            _capForFixedDt = -1f;
        }

        // ---------------------------------------------------------------- fixed step override

        static float GameFixedDt()
        {
            GameManager gm = GameManager.Instance;
            if (gm && gm.PhysicsTickHz > 0) return 1f / gm.PhysicsTickHz;
            return 0.04f;
        }

        static void PostGameManagerStart()
        {
            if (!StepOn) return;
            ApplyStep();
        }

        static void StepTick()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextStepCheck) return;
            _nextStepCheck = now + 1f;
            if (Time.fixedDeltaTime != _wroteFixedDt) ApplyStep();
        }

        static void ApplyStep()
        {
            if (!StepOn) return;
            try
            {
                float target = 1f / S.FixedStepHz;
                if (Time.fixedDeltaTime != target) Time.fixedDeltaTime = target;
                _wroteFixedDt = Time.fixedDeltaTime;
                StepItem.Info = "fixedDeltaTime " + GameFixedDt().ToString("0.####") + " -> " + _wroteFixedDt.ToString("0.####") + " s (" + S.FixedStepHz + " Hz)";
                ApplyCap();
            }
            catch (Exception ex) { StepItem.Fail(ex, "apply fixedDeltaTime"); }
        }

        static void RestoreStep()
        {
            try
            {
                if (_wroteFixedDt > 0f && Time.fixedDeltaTime == _wroteFixedDt) Time.fixedDeltaTime = GameFixedDt();
                StepItem.Info = "restored fixedDeltaTime " + Time.fixedDeltaTime.ToString("0.####") + " s";
            }
            catch (Exception ex) { Perf.Warn("Physics.FixedStep_Override restore: " + ex.Message); }
            _wroteFixedDt = -1f;
            ApplyCap();
        }
    }
}
