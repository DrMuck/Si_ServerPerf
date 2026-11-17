/*
 DeadAgentHalt (default ON since 0.2.0, with DeadAgentHaltClearAim false; server-only, nothing a client receives changes except one aim-cleared sync per death):
 the AI agent of a destroyed unit stops thinking.

 Vanilla (1.0.17): nothing stops a dead unit's AI. AIBaseAgent.OrderedUpdateAllAgents ticks every agent in
 AIBaseAgents (Silica.AI/AIBaseAgent.cs:544-570) and AIBaseAgent.OrderedUpdate (:602-633) only checks
 isActiveAndEnabled - not Unit.IsDestroyed - before running, every frame:
   AgentLogic.OrderedUpdate  2 Hz: transport / attack / defend handling, may issue moves (AIBaseLogic.cs:222-260)
   AgentPathfinding.OrderedUpdate
   ExecuteBehaviorTree       2 Hz (:1476-1490). No behaviour-tree node checks its own unit for death
                             (BehaviorTrees/*.cs); BTEnemyTargetDetected (BTEnemyTargetDetected.cs:8-77) calls
                             AgentAiming.FindBestTarget -> Unit.GetBestTarget -> TargetFinder.RequestScan (a full scan
                             of the team's detected list) whenever the dead agent has no aim target - and a dead unit
                             keeps its Team until disposal (Unit.OnDestroy :1297-1300)
   AgentAiming.UpdateAim     line-of-sight queries every 0.2 s for its aim target (AIAiming.cs:687-707)
   UpdateMoveProblemStatus / UpdateIdleStatus / OnOrderedUpdate
 until the GameObject is destroyed 15 s after death (OnDestroy :1509-1511). UpdateTiers.Tier_IdleAgents never tiers
 them (Idle.cs: destroyed -> not idle). The dead unit cannot act on any of it: vehicle inputs are cleared every frame
 when destroyed (Vehicle.UpdateInput :1753-1756), creature inputs too (CreatureDecapod.OrderedLateUpdate :4431-4436),
 turrets check the vehicle's death (VehicleTurret.cs:994, :1532, :1832), the soldier plays its death animation.

 What the item does: at the end of each ordered fixed list it scans Unit.Units round robin (every unit every
 DeadSettleScanStrideSteps steps). A destroyed unit's agent is halted DeadAgentHaltDelaySeconds (1 s) after the death
 was seen (lets a behaviour-tree node that is mid-execution finish through the static NodesInProgress loop, which this
 item does not touch). Halted = a prefix on AIBaseAgent.OrderedUpdate returns false for it (the whole per-agent update
 is skipped). With DeadAgentHaltClearAim (default false since b14; was true) the halt also clears the agent's aim and assigned attack
 target once (AIAiming.SetAim(null) / SetAssignedAttackTarget(null), the same calls the behaviour tree uses): the
 live unit it was aiming at is no longer counted as attacked by a corpse (Target.RemoveAttacker), which feeds target
 scoring and UpdateTiers' idle test. Vanilla's dead agent keeps aiming at whatever its last scans found, so this one
 is a deviation towards "dead units do not threaten" (target saturation, see notes below); false = the pure skip.
 Not halted: harvesters (AIHarvesterAgent, AIAlienHarvesterAgent: docking / resource state) and the alien queen agent
 (nest logic), units a player controls (ControlledBy) and seated units.
 A halted unit that comes back to life (SetHealth) is released at the next 1 s purge. Switch-off releases all at
 once; their agents simply continue (all state is the agent's own, nothing to restore).
   [Physics] deadagents haltedNow= pendingNow= halted= released= skips= clearedAim= clearedAssigned= frozenAim= excluded=
   (per log interval; clearedAim/clearedAssigned = phantom attacker registrations removed by ClearAim, frozenAim = halted
   with ClearAim off while still holding an aim)
 Behaviour notes (b14 guard question, 1.0.17):
   - A dead unit cannot fire or attack, independent of its agent: soldier fire is gated on IsDestroyed
     (HumanHandsAnimator.cs:682-685), vehicle turrets (VehicleTurret.cs:994, :1532, :1832-1836), air vehicles do not
     run their AI input once destroyed (VehicleAir.cs:1064-1067, Vehicle.cs:1753-1756), a dead creature's late update
     clears inputs and returns before UpdateAttacks (CreatureDecapod.cs:4431-4436, :4771-4772). No behaviour-tree node or
     agent code acts on the unit's own death. AIGroup membership is dropped by GameEvents.OnUnitDestroyed
     (Silica.AI/AIGroup.cs:1461-1467), not by a tick. NodesInProgress is filled and finished within one
     OrderedUpdateAllAgents call (Silica.AI/AIBaseAgent.cs:581-599). The path queue is static and served by
     AIManager (AIPathfinding.cs:1176-1210). Aim registrations are removed by AIAiming.OnDestroy at disposal
     (AIAiming.cs:817-830). Navmesh cutting is a no-op (Unit.cs:1071-1073). OnUnitIdle has no listener in the game.
   - What the living see of a dead agent: only its aim, through AttackedByEnemyNum (GameAI.cs:757-777 target saturation;
     Targeting.Snapshot's copy; UpdateTiers' idle test). GetIsTargetedBy (Target.cs:365-380) is only evaluated for
     detected (alive) candidates, AIAiming's attacker look-around (AIAiming.cs:515-523) and AIRepairSupport
     (:305-310) skip destroyed attackers. Vanilla corpses keep re-picking targets every 0.5 s, so live targets near
     corpses count extra attackers and are spread away from; ClearAim removes that at once, ClearAim off freezes it
     until the frozen target dies. Neither is exactly vanilla; off is the closer one.
   - The shared path queue serves 25 requests per 0.1 s (10 under heavy network load, AIPathfinding.cs:1187); requests
     from dead agents no longer compete with live ones once halted.
*/

using System;
using System.Collections.Generic;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class DeadAgentHalt
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;

        struct Pending
        {
            public Unit U;
            public AIBaseAgent Agent;
            public float SeenAt;
        }

        static readonly HashSet<AIBaseAgent> _halted = new HashSet<AIBaseAgent>(RefEq<AIBaseAgent>.I);
        static readonly List<AIBaseAgent> _haltedList = new List<AIBaseAgent>();
        static readonly HashSet<Unit> _seen = new HashSet<Unit>(RefEq<Unit>.I);
        static readonly List<Pending> _pending = new List<Pending>();
        static int _rr, _purgeStep;
        static long _haltedN, _released, _skips, _clearedAim, _clearedAssigned, _frozen, _excluded;
        static float _nextLog;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("DeadAgentHalt",
                "Destroyed units' AI agents stop updating (behaviour tree, target scans, LOS queries, pathfinding, logic) " +
                "DeadAgentHaltDelaySeconds after death; harvesters / queen / player units excluded; optional aim clear",
                v => On = v, defaultOn: true);
            var au = Item.Target(typeof(AIBaseAgent), "OrderedUpdate", new[] { typeof(float) }, declaredOnly: true);
            Item.Patch(au, typeof(DeadAgentHalt), prefix: nameof(AgentPre));
            var fu = Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes);
            Item.Patch(fu, typeof(DeadAgentHalt), postfix: nameof(FixedPost));
            Item.OnSwitch(on => { if (!on) ReleaseAll(); });
            ctx.OnUpdate(Tick, Item);
            ctx.OnSceneLoaded(_ => Clear());
        }

        static bool AgentPre(AIBaseAgent __instance)
        {
            if (!On || _halted.Count == 0) return true;
            if (!_halted.Contains(__instance)) return true;
            _skips++;
            return false;
        }

        static void FixedPost()
        {
            if (!On) return;
            try
            {
                float now = Time.fixedTime;
                List<Unit> units = Unit.Units;
                if (units != null && units.Count > 0)
                {
                    int stride = Mathf.Max(1, S.DeadSettleScanStrideSteps);
                    int n = (units.Count + stride - 1) / stride;
                    for (int j = 0; j < n; j++)
                    {
                        if (_rr >= units.Count) _rr = 0;
                        Unit u = units[_rr++];
                        if (!u || !u.IsDestroyed || _seen.Contains(u)) continue;
                        _seen.Add(u);
                        AIBaseAgent ag = u.AIAgent;
                        if (!ag) continue;
                        if (ag is AIHarvesterAgent || ag is AIAlienHarvesterAgent || ag is AIAlienQueenAgent) { _excluded++; continue; }
                        _pending.Add(new Pending { U = u, Agent = ag, SeenAt = now });
                    }
                }
                // halt pending agents whose delay is over
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    Pending p = _pending[i];
                    bool drop = !p.U || !p.Agent || !p.U.IsDestroyed;
                    if (!drop)
                    {
                        if (now - p.SeenAt < S.DeadAgentHaltDelaySeconds) continue;
                        if (p.U.ControlledBy || p.U.InCompartment) continue;   // wait: vanilla until a player lets go
                        Halt(p.Agent);
                    }
                    int last = _pending.Count - 1;
                    _pending[i] = _pending[last];
                    _pending.RemoveAt(last);
                }
                if (++_purgeStep >= 25) { _purgeStep = 0; Purge(); }
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "OrderedUpdateManager.FixedUpdate postfix");
            }
        }

        static void Halt(AIBaseAgent ag)
        {
            if (!_halted.Add(ag)) return;
            _haltedList.Add(ag);
            _haltedN++;
            AIAiming aim = ag.AgentAiming;
            if (!aim) return;
            // An aim / assigned attack target held by the corpse counts as one enemy attacker on that target
            // (Target.AddAttacker -> AttackedByEnemyNum, read by GameAI's saturation term and UpdateTiers' idle test).
            bool hasAim = aim.AimTargetIgnorePaused, hasAssigned = aim.AssignedAttackTarget;
            if (!S.DeadAgentHaltClearAim)
            {
                if (hasAim || hasAssigned) _frozen++;   // stays registered until that target dies or the corpse is disposed
                return;
            }
            if (hasAim) { aim.SetAim(null); _clearedAim++; }
            if (hasAssigned) { aim.SetAssignedAttackTarget(null); _clearedAssigned++; }
        }

        // every second: forget disposed units, release agents whose unit came back to life
        static void Purge()
        {
            for (int i = _haltedList.Count - 1; i >= 0; i--)
            {
                AIBaseAgent ag = _haltedList[i];
                Unit u = ag ? ag.Unit : null;
                if (ag && u && u.IsDestroyed) continue;
                _halted.Remove(ag);
                if (ag && u) _released++;
                int last = _haltedList.Count - 1;
                _haltedList[i] = _haltedList[last];
                _haltedList.RemoveAt(last);
            }
            if (_seen.Count > 0)
            {
                _tmp.Clear();
                foreach (Unit u in _seen) if (!u || !u.IsDestroyed) _tmp.Add(u);
                for (int i = 0; i < _tmp.Count; i++) _seen.Remove(_tmp[i]);
                _tmp.Clear();
            }
        }

        static readonly List<Unit> _tmp = new List<Unit>();

        static void ReleaseAll()
        {
            int n = _haltedList.Count;
            _released += n;
            Clear();
            Item.Info = "last switch-off: " + n + " dead agents released (their vanilla update resumes)";
        }

        static void Clear()
        {
            _halted.Clear();
            _haltedList.Clear();
            _seen.Clear();
            _pending.Clear();
            _rr = 0;
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
                Perf.Log("[Physics] deadagents haltedNow=" + _haltedList.Count + " pendingNow=" + _pending.Count + " halted=" + _haltedN +
                         " released=" + _released + " skips=" + _skips + " clearedAim=" + _clearedAim + " clearedAssigned=" + _clearedAssigned +
                         " frozenAim=" + _frozen + " excluded=" + _excluded);
                Item.Info = "halted now " + _haltedList.Count + " dead agents; last interval " + _skips + " agent updates skipped";
            }
            _haltedN = _released = _skips = _clearedAim = _clearedAssigned = _frozen = _excluded = 0;
        }
    }
}
