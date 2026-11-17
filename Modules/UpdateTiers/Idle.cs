/*
 "Quiet" tests for tiered entries. A tiered entry is skipped on a non-due call only if its test holds on that very
 call, so any state change returns it to full rate on the next frame/step (no waiting for the interval).

 Idle AI unit (shared by Tier_IdleSoldiers and Tier_IdleAgents), all of:
   AI controlled (Unit.AIControlled: has an agent, no player, AI not disabled), not destroyed, not in a compartment,
   no player boarded in any of its compartments;
   agent aiming: no aim target, no assigned attack target, no assigned defend target, no ground aim (raw fields, so
   AimPaused cannot hide a target);
   agent pathfinding: path completed, no path, not calculating, nothing queued (long or short range);
   no move problem, no long-range path sync pending (network), not stuck, not moving (rigidbody |v| <= 1 m/s);
   order queue empty;
   its Target: AttackedByEnemyNum == 0 (no enemy is aiming at it) and not damaged by an enemy for
   WakeAfterDamageSeconds; not woken by a nearby new enemy detection (WakeRegistry).
 Anything a human player controls or sits in is never idle. What clients see of a unit (spectators included) comes
 from its network updates, which the tiers do not touch.
*/

using System.Collections.Generic;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.UpdateTiers
{
    internal static class Idle
    {
        // Time.time, refreshed every frame by TierStatus (MelonLoader Update; at most one frame old).
        internal static float Now;
        internal static float WakeAfterDamage = 5f;

        static AccessTools_Refs R;

        sealed class AccessTools_Refs
        {
            public HarmonyLib.AccessTools.FieldRef<AIAiming, Target> AimTarget, AttackTarget, DefendTarget;
            public HarmonyLib.AccessTools.FieldRef<AIAiming, bool> GroundAim;
            public HarmonyLib.AccessTools.FieldRef<AIPathfinding, AIPathfinding.EFindPathCallType> ShortQueue, LongQueue;
            public HarmonyLib.AccessTools.FieldRef<AIBaseAgent, bool> NetQueueLongPath;
            public HarmonyLib.AccessTools.FieldRef<Soldier, Vector2> InputRotation;
            public HarmonyLib.AccessTools.FieldRef<Structure, bool> StructureWaitNav;
            public HarmonyLib.AccessTools.FieldRef<ConstructionSite, bool> SiteWaitNav;
            public HarmonyLib.AccessTools.FieldRef<AlienStructure, bool> AlienLinkInit, AlienLinkDirty;
            public HarmonyLib.AccessTools.FieldRef<NetworkTransformComponent, bool> NetHasChanged;
        }

        // Resolve the private fields once. Returns the names of missing ones (empty = all there).
        internal static List<string> Init()
        {
            var missing = new List<string>();
            var r = new AccessTools_Refs
            {
                AimTarget = F<AIAiming, Target>("m_AimTarget", missing),
                AttackTarget = F<AIAiming, Target>("m_AssignedAttackTarget", missing),
                DefendTarget = F<AIAiming, Target>("m_AssignedDefendTarget", missing),
                GroundAim = F<AIAiming, bool>("m_IsGroundAiming", missing),
                ShortQueue = F<AIPathfinding, AIPathfinding.EFindPathCallType>("QueuedFindPath_ShortRange_Type", missing),
                LongQueue = F<AIPathfinding, AIPathfinding.EFindPathCallType>("QueuedFindPath_LongRange_Type", missing),
                NetQueueLongPath = F<AIBaseAgent, bool>("Net_QueueSendLongRangePath", missing),
                InputRotation = F<Soldier, Vector2>("m_InputRotation", missing),
                StructureWaitNav = F<Structure, bool>("WaitQueueNavmeshRecalculation", missing),
                SiteWaitNav = F<ConstructionSite, bool>("WaitQueueNavmeshRecalculation", missing),
                AlienLinkInit = F<AlienStructure, bool>("_linkInitialized", missing),
                AlienLinkDirty = F<AlienStructure, bool>("_linkDirty", missing),
                NetHasChanged = F<NetworkTransformComponent, bool>("net_HasChanged", missing),
            };
            R = r;
            return missing;
        }

        static HarmonyLib.AccessTools.FieldRef<T, TF> F<T, TF>(string name, List<string> missing)
        {
            var f = Refl.Field<T, TF>(name);
            if (f == null) missing.Add(typeof(T).Name + "." + name);
            return f;
        }

        internal static bool AnyMissing(params object[] refs)
        {
            foreach (object o in refs) if (o == null) return true;
            return false;
        }

        internal static bool FieldsFor(int what)
        {
            var r = R;
            if (r == null) return false;
            switch (what)
            {
                case 0: return !AnyMissing(r.StructureWaitNav, r.SiteWaitNav, r.AlienLinkInit, r.AlienLinkDirty);
                case 1: return !AnyMissing(r.AimTarget, r.AttackTarget, r.DefendTarget, r.GroundAim, r.ShortQueue, r.LongQueue, r.NetQueueLongPath);
                case 2: return FieldsFor(1) && r.InputRotation != null;
                case 3: return r.NetHasChanged != null;
            }
            return false;
        }

        // ------------------------------------------------------------------ players

        // A player controls the unit or sits in one of its compartments.
        internal static bool PlayerInvolved(Unit u)
        {
            if (u.PlayerControlled) return true;
            List<UnitCompartment> comps = u.UnitCompartments;
            if (comps == null) return false;
            for (int i = 0; i < comps.Count; i++)
            {
                UnitCompartment c = comps[i];
                if ((object)c == null) continue;
                List<Unit> boarded = c.BoardedUnits;
                if (boarded == null) continue;
                for (int j = 0; j < boarded.Count; j++)
                {
                    Unit b = boarded[j];
                    if ((object)b != null && b.PlayerControlled) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ units / agents

        // "Why not quiet" codes: the first failing condition of the last idle test (read right after a false result).
        internal const int W_Other = 0, W_Gone = 1, W_NotAI = 2, W_Destroyed = 3, W_InCompartment = 4, W_PlayerBoarded = 5,
            W_AimTarget = 6, W_AttackTarget = 7, W_DefendTarget = 8, W_GroundAim = 9, W_Path = 10, W_PathQueued = 11,
            W_PathCalc = 12, W_MoveProblem = 13, W_NetPathSync = 14, W_Orders = 15, W_AimedAt = 16, W_Damaged = 17,
            W_DetectWake = 18, W_Stuck = 19, W_Moving = 20, W_ViewInput = 21, W_Airborne = 22, W_Weapon = 23, W_Alien = 24,
            W_Count = 25;
        internal static readonly string[] WhyNames =
        {
            "other", "gone", "notAI", "destroyed", "inCompartment", "playerBoarded", "aimTarget", "attackTarget",
            "defendTarget", "groundAim", "path", "pathQueued", "pathCalc", "moveProblem", "netPathSync", "orders",
            "aimedAt", "damaged", "detectWake", "stuck", "moving", "viewInput", "airborne", "weapon", "alienState",
        };
        internal static int Why;
        // view-input statistics of soldiers rejected for W_ViewInput (|x|+|y| in degrees per call), for tuning
        internal static double ViewInputSum;
        internal static long ViewInputN;

        // Largest |m_InputRotation.x| + |.y| (degrees per call) at which a soldier still counts as idle. Set from settings.
        internal static float MaxViewInput = 0.5f;

        static bool No(int why) { Why = why; return false; }

        internal static bool UnitAndAgentIdle(Unit u, AIBaseAgent ag, WakeRegistry.UnitRec rec)
        {
            if ((object)u == null || !u) return No(W_Gone);
            if (!u.AIControlled) return No(W_NotAI);               // agent present, no player, AI enabled
            if ((object)ag == null) return No(W_NotAI);
            if (u.IsDestroyed) return No(W_Destroyed);
            UnitCompartment inComp = u.InCompartment;
            if ((object)inComp != null && inComp) return No(W_InCompartment);
            if (u.UnitCompartments.Count > 0 && PlayerInvolved(u)) return No(W_PlayerBoarded);

            var r = R;
            AIAiming aim = ag.AgentAiming;
            if ((object)aim != null)
            {
                if ((object)r.AimTarget(aim) != null) return No(W_AimTarget);
                if ((object)r.AttackTarget(aim) != null) return No(W_AttackTarget);
                if ((object)r.DefendTarget(aim) != null) return No(W_DefendTarget);
                if (r.GroundAim(aim)) return No(W_GroundAim);
            }
            AIPathfinding pf = ag.AgentPathfinding;
            if ((object)pf == null) return No(W_Path);
            if (!pf.PathCompleted || pf.HasPath) return No(W_Path);
            if (r.LongQueue(pf) != AIPathfinding.EFindPathCallType.None || r.ShortQueue(pf) != AIPathfinding.EFindPathCallType.None) return No(W_PathQueued);
            if (pf.IsCalculatingPath) return No(W_PathCalc);
            if (ag.IsMoveProblem) return No(W_MoveProblem);
            if (r.NetQueueLongPath(ag)) return No(W_NetPathSync);

            AIOrderProcessor op = u.OrderAgent;
            if ((object)op != null && op.Orders.Count > 0) return No(W_Orders);

            Target t = u.Target;
            if ((object)t != null)
            {
                if (t.AttackedByEnemyNum > 0) return No(W_AimedAt);
                if (Now - t.LastDamagedByEnemyTime < WakeAfterDamage) return No(W_Damaged);
            }
            if (rec != null && rec.WakeUntil > Now) return No(W_DetectWake);
            if (u.IsStuck) return No(W_Stuck);
            if (u.IsMoving) return No(W_Moving);                     // native call, last of the generic tests
            return true;
        }

        // Soldier.OrderedUpdate: UpdateFPSHands adds m_InputRotation to the view angles once per CALL (not dt-scaled).
        // The AI late controller (full rate) recomputes that input every frame as a clamped step toward its look point,
        // i.e. a closed loop: applying it on 1 call in N only slows the turn, it cannot drift. A standing soldier keeps a
        // small non-zero input (look point near its own position, aim/suppression error terms), so "exactly zero" kept
        // almost every idle soldier at full rate in the first lab run. Quiet now allows |x|+|y| <= MaxViewInput degrees
        // per call (default 0.5: at most ~1.5 degrees of extra lag per interval); a real turn stays at full rate.
        // Weapon state (firing / reloading) also keeps it running: HumanHandsAnimator.UpdateAnimator fires from here.
        internal static bool SoldierIdle(Soldier s, WakeRegistry.UnitRec rec)
        {
            if ((object)s == null || !s) return No(W_Gone);
            if (!UnitAndAgentIdle(s, s.AIAgent, rec)) return false;
            Vector2 vi = R.InputRotation(s);
            float mag = Mathf.Abs(vi.x) + Mathf.Abs(vi.y);
            if (mag > MaxViewInput)
            {
                ViewInputSum += mag;
                ViewInputN++;
                return No(W_ViewInput);
            }
            if (!s.IsGrounded || s.IsJumping || s.IsClimbing) return No(W_Airborne);
            HumanHandsAnimator h = s.HumanHandsAnimator;
            if ((object)h != null && h && (h.IsFiring || h.IsReloading)) return No(W_Weapon);
            return true;
        }
        // ------------------------------------------------------------------ structures

        // kind: 0 Structure (or another Structure subclass without its own OrderedUpdate), 1 HumanStructure,
        //       2 AlienStructure, 3 ConstructionSite
        internal static bool StructureQuiet(BaseGameObject o, int kind)
        {
            if ((object)o == null || !o) return false;
            var r = R;
            if (kind == 3)
            {
                return !r.SiteWaitNav((ConstructionSite)o);
            }
            var st = (Structure)o;
            if (r.StructureWaitNav(st)) return false;                 // navmesh requeue pending
            List<ConstructionData> q = st.ProductionQueue;
            if (q != null && q.Count > 0) return false;               // production work: full rate
            if (kind == 1)
            {
                if (((HumanStructure)st).IsLinkPending) return false; // relink pending
            }
            else if (kind == 2)
            {
                var a = (AlienStructure)st;
                if (!r.AlienLinkInit(a) || r.AlienLinkDirty(a)) return false;
            }
            return true;
        }

        internal static bool NetTransformQuiet(NetworkTransformComponent n)
        {
            if ((object)n == null || !n) return false;
            return !R.NetHasChanged(n);                               // a pending change is sent on the next run
        }
    }
}
