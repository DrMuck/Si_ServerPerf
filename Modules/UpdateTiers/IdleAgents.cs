/*
 Tier_IdleAgents: S1 inside AIBaseAgent.OrderedUpdateAllAgents (static in 1.0.17, one update-list entry at prio 999
 that loops all agents and calls the public non-virtual AIBaseAgent.OrderedUpdate(dt) on each).

 Prefix on AIBaseAgent.OrderedUpdate(float dt): an idle agent (Idle.UnitAndAgentIdle, plus "not in a submerge
 transition" for aliens) runs every IdleAgentsInterval-th frame; skipped frames add their dt to a per-agent
 accumulator that is handed over (dt += acc) on its next run, so every timer inside stays dt-correct:
 AgentLogic tick (0.5 s timer), pathfinding update (no path -> retry/recompute only), behaviour tree (0.5 s timer,
 m_BTUpdateDelay -= dt), AIAiming.UpdateAim (0.2 s LOS timer; the urgent close-enemy check uses Time.time), the 1 Hz
 move-problem check, the idle-event timer, the alien submerge watch. What changes: the BT / aim timers of an idle agent
 fire up to Interval-1 frames late, so its own target acquisition can start that much later than vanilla. That is why
 this item is DEFAULT OFF: it can shift the first shot of an engagement by a few frames.

 Tiered agent types: exactly AISoldierAgent, AIVehicleAgent (ground) and AIAlienAgent, and only when the unit is not a
 flying type. Not tiered: AIAirVehicleAgent (flight), AIHarvesterAgent / AIAlienHarvesterAgent (docking, harvest
 state machine, IsIdle drives the commander's worker logic), AIAlienQueenAgent, any modded subclass.
 Switching the item off hands every pending accumulator over on the agents' next call (drain), so no time is lost.
*/

using System;
using System.Collections.Generic;
using HarmonyLib;
using Silica.AI;

namespace SiServerPerf.Modules.UpdateTiers
{
    internal static class IdleAgents
    {
        internal static bool On;
        static PerfItem Item;
        internal static Tier T;

        sealed class AgentState
        {
            public bool Tiered;
            public bool Alien;
            public int Countdown;
            public float Acc;
            public WakeRegistry.UnitRec Rec;
        }

        static readonly Dictionary<AIBaseAgent, AgentState> s_States = new Dictionary<AIBaseAgent, AgentState>(RefComparer<AIBaseAgent>.Instance);
        static readonly List<AIBaseAgent> s_Scratch = new List<AIBaseAgent>();
        static readonly AgentState s_Untiered = new AgentState();
        static int s_Phase;
        static int s_DrainUntilFrame = -1;

        public static void Register(ModuleContext ctx, UpdateTiersSettings s)
        {
            T = new Tier("Tier_IdleAgents", s.IdleAgentsInterval);
            Item = ctx.Item(T.Key,
                "Prefix AIBaseAgent.OrderedUpdate (inside OrderedUpdateAllAgents): idle AI soldier/ground-vehicle/alien agents every " + T.Interval +
                " frames with accumulated dt; full rate on any target/path/order/damage/aim-at/nearby detection",
                v => { On = v; T.On = v; if (!v) s_DrainUntilFrame = Perf.Frame + 3; },
                defaultOn: false);
            T.Item = Item;
            if (!Idle.FieldsFor(1)) Item.Block("agent fields absent on this build");
            var m = Item.Target(typeof(AIBaseAgent), nameof(AIBaseAgent.OrderedUpdate), new[] { typeof(float) }, declaredOnly: true);
            Item.Patch(m, typeof(IdleAgents), prefix: nameof(Prefix));
        }

        internal static void FailItem(Exception ex, string where) => Item?.Fail(ex, where);

        static bool Prefix(AIBaseAgent __instance, ref float dt)
        {
            if (!On)
            {
                if (s_DrainUntilFrame >= 0) Drain(__instance, ref dt);
                return true;
            }
            try
            {
                if (!s_States.TryGetValue(__instance, out AgentState st)) st = Classify(__instance);
                if (!st.Tiered) return true;
                if (--st.Countdown <= 0)
                {
                    st.Countdown = T.Interval;
                    T.Runs++;
                }
                else
                {
                    Idle.Why = Idle.W_Other;
                    if (Quiet(__instance, st))
                    {
                        st.Acc += dt;
                        T.Skips++;
                        return false;
                    }
                    T.Busy++;
                    T.Why[Idle.Why]++;
                }
                if (st.Acc != 0f)
                {
                    dt += st.Acc;
                    st.Acc = 0f;
                }
                return true;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "AIBaseAgent.OrderedUpdate prefix");
                return true;
            }
        }

        static void Drain(AIBaseAgent a, ref float dt)
        {
            try
            {
                if (Perf.Frame > s_DrainUntilFrame)
                {
                    s_DrainUntilFrame = -1;
                    return;
                }
                if (s_States.TryGetValue(a, out AgentState st) && st.Acc != 0f)
                {
                    dt += st.Acc;
                    st.Acc = 0f;
                }
            }
            catch
            {
                s_DrainUntilFrame = -1;
            }
        }

        static AgentState Classify(AIBaseAgent a)
        {
            Unit u = a.Unit;
            if ((object)u == null) return s_Untiered;            // Start() not run yet: decide later
            var st = new AgentState();
            Type t = a.GetType();
            bool alien = t == typeof(AIAlienAgent);
            bool ok = t == typeof(AISoldierAgent) || t == typeof(AIVehicleAgent) || alien;
            if (ok && u.IsFlyingType) ok = false;
            if (ok)
            {
                st.Tiered = true;
                st.Alien = alien;
                st.Countdown = 1 + (s_Phase++ % T.Interval);
                st.Rec = WakeRegistry.Get(u);
                T.Classified++;
            }
            s_States[a] = st;
            return st;
        }

        static bool Quiet(AIBaseAgent a, AgentState st)
        {
            Unit u = a.Unit;
            if (!Idle.UnitAndAgentIdle(u, a, st.Rec)) return false;
            if (st.Alien)
            {
                var d = u as CreatureDecapod;
                if ((object)d != null && (d.IsSubmergeTransition || d.IsAttacking)) { Idle.Why = Idle.W_Alien; return false; }
            }
            return true;
        }

        // Once per second from TierStatus: forget destroyed agents.
        internal static void Prune()
        {
            if (s_States.Count == 0) return;
            s_Scratch.Clear();
            foreach (var kv in s_States)
            {
                if (!kv.Key) s_Scratch.Add(kv.Key);
            }
            foreach (AIBaseAgent a in s_Scratch) s_States.Remove(a);
            s_Scratch.Clear();
        }

        internal static int Tracked => s_States.Count;
    }
}
