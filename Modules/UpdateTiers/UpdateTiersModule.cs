/*
 Si_ServerPerf module "UpdateTiers": DEV_REPORT S1 (rate-tiered ordered updates), S11 (O(1) register/unregister)
 and the fixed-late dt fix (cause #13), against Silica 1.0.17 (Mono dedicated server).

 What 1.0.17 already has, and what this module does with it:
  * OrderedUpdateManager.UpdateList.Run still carries the dead camera-distance skip (every caller passes
    skipEnabled:false; CheckUpdateFrameSkip/CheckFixedUpdateFrameSkip still return false). 1.0.17 added
    UpdateActionData.everyTick, filled from BaseGameObject.FixedUpdateEveryTick (true for Vehicle) and passed for the
    vehicle fixed parts, VehicleAntiFlip, CollisionAvoidance(Unit's flag), AIHarvesterAgent's fixed-late part and
    CollisionAvoidanceManager.PrepareFixedUpdate. That flag only ever exempts entries from the distance skip; nothing
    in 1.0.17 turns the skip on. We do not use the distance skip (meaningless on a server) and we never tier an
    entry whose everyTick flag is set (TierDispatch checks it), so the developer's "must run every tick" list is
    respected as is.
  * Run passes now - callTime and writes callTime only when an entry runs, so a skipped entry gets the accumulated
    dt on its next run. The tier dispatcher relies on exactly that: no dt bookkeeping of our own for list entries.

 Items (each switchable alone; see the per-file headers for the safety reasoning):
   Tier_Structures         update list: Structure / HumanStructure / AlienStructure / ConstructionSite.OrderedUpdate
   Tier_HealDecay          update list: AutoHeal / Decay.OrderedUpdate
   Tier_OrderProcessors    update list: AIOrderProcessor.OrderedUpdate while its order queue is empty
   Tier_IdleSoldiers       update list: Soldier.OrderedUpdate of idle AI soldiers
   Tier_StaticNetTransforms fixed list: NetworkTransformComponent.OrderedFixedUpdate of objects without Rigidbody
   Tier_IdleAgents         prefix AIBaseAgent.OrderedUpdate (inside OrderedUpdateAllAgents), idle AI agents
   Register_O1             S11: prefixes UpdateList.Register / Unregister (same order semantics, O(log N) + memmove)
   FixedLate_StepDt        fixed-late list runs once per physics step with dt = fixedDeltaTime (default off)
 The first five share one transpiler on UpdateList.Run (installed by the first active one, the others are "hooked").
*/

namespace SiServerPerf.Modules.UpdateTiers
{
    public sealed class UpdateTiersSettings
    {
        // Intervals in calls of the owning list: frames for the update list, physics steps for the fixed list.
        // An entry runs at least every N calls; between them it is skipped only while its "quiet" test holds.
        public int StructuresInterval = 4;
        public int HealDecayInterval = 4;
        public int OrderProcessorsInterval = 4;
        public int IdleSoldiersInterval = 4;
        public int StaticNetTransformsInterval = 5;
        public int IdleAgentsInterval = 4;

        // Tier_IdleSoldiers: largest view input |x|+|y| (degrees per call, Soldier.m_InputRotation) that still counts as idle.
        // The AI controller re-aims every frame (closed loop), so skipping calls with a small input only slows a tiny turn.
        public float IdleSoldiersMaxViewInputDeg = 0.5f;

        // Idle units/agents: back to full rate for this long after being damaged by an enemy (Target.LastDamagedByEnemyTime).
        public float WakeAfterDamageSeconds = 5f;
        // Idle units/agents: when their team newly detects an enemy within this 3D distance, full rate for WakeOnDetectSeconds.
        public bool WakeOnDetection = true;
        public float WakeOnDetectRadius = 400f;
        public float WakeOnDetectSeconds = 3f;

        // Register_O1: every N-th register/unregister is also computed the vanilla way and compared; a mismatch logs and
        // switches the item off (vanilla from then on). 1 = check every call (slow, for a correctness run), 0 = never.
        public int RegisterVerifyEveryN = 64;
    }

    [PerfModuleOrder(10)]
    public sealed class UpdateTiersModule : IPerfModule
    {
        public string Name => "UpdateTiers";
        public string Description => "S1 rate-tiered ordered updates + S11 O(1) register/unregister + fixed-late step dt";

        public void Apply(ModuleContext ctx)
        {
            var s = ctx.Settings<UpdateTiersSettings>();
            TierDispatch.Register(ctx, s);
            IdleAgents.Register(ctx, s);
            WakeRegistry.Register(ctx, s);
            RegisterIndex.Register(ctx, s);
            FixedLateDt.Register(ctx);
            TierStatus.Register(ctx);
        }
    }
}
