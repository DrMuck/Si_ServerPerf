/*
 Wake-up on "enemy detected nearby" for idle AI units (Tier_IdleSoldiers, Tier_IdleAgents).

 GameEvents.OnTargetDetected(team, target, sensor) fires when a team adds a target to DetectedTargets (first
 detection, re-detection after the 8 s decay, and the friend-reveal copies). For an enemy target, every tracked unit
 of that team within WakeOnDetectRadius gets WakeUntil = now + WakeOnDetectSeconds, which makes Idle.UnitAndAgentIdle
 false: the unit runs at full rate from its next call. Damage and "an enemy aims at me" need no event: the idle test
 reads Target.LastDamagedByEnemyTime and Target.AttackedByEnemyNum directly.

 Game.ClearAll -> GameEvents.Clear() wipes all subscriptions on level change, so Maintain() (once per second) re-adds
 the handler whenever the delegate field changed since it last looked.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiServerPerf.Modules.UpdateTiers
{
    internal static class WakeRegistry
    {
        internal sealed class UnitRec
        {
            public Unit Unit;
            public float WakeUntil = -1000f;
        }

        static readonly Dictionary<Unit, UnitRec> s_Map = new Dictionary<Unit, UnitRec>(RefComparer<Unit>.Instance);
        static readonly List<UnitRec> s_List = new List<UnitRec>();
        static readonly List<UnitRec> s_Scratch = new List<UnitRec>();

        static bool s_Enabled;
        static float s_Radius2 = 400f * 400f;
        static float s_WakeSeconds = 3f;
        static Action<Team, Target, Sensor> s_Handler;
        static Delegate s_LastSeen;
        static bool s_Subscribed;

        internal static long Events, Wakes;
        internal static string State = "off";

        public static void Register(ModuleContext ctx, UpdateTiersSettings s)
        {
            s_Enabled = s.WakeOnDetection;
            float r = Mathf.Max(0f, s.WakeOnDetectRadius);
            s_Radius2 = r * r;
            s_WakeSeconds = Mathf.Max(0f, s.WakeOnDetectSeconds);
            Idle.WakeAfterDamage = Mathf.Max(0f, s.WakeAfterDamageSeconds);
            Idle.MaxViewInput = Mathf.Max(0f, s.IdleSoldiersMaxViewInputDeg);
            s_Handler = OnTargetDetected;
            if (!s_Enabled) ctx.Note("WakeOnDetection = false: idle units wake only on damage, aim-at, orders and own acquisition");
        }

        internal static UnitRec Get(Unit u)
        {
            if ((object)u == null) return null;
            if (!s_Map.TryGetValue(u, out UnitRec rec))
            {
                rec = new UnitRec { Unit = u };
                s_Map.Add(u, rec);
                s_List.Add(rec);
            }
            return rec;
        }

        static bool Wanted => s_Enabled && (TierDispatch.UnitTierOn || IdleAgents.On);

        // Once per second from TierStatus: drop records of destroyed objects, keep the event subscription alive.
        internal static void Maintain()
        {
            s_Scratch.Clear();
            for (int i = 0; i < s_List.Count; i++)
            {
                UnitRec rec = s_List[i];
                if (!rec.Unit) s_Scratch.Add(rec);
            }
            if (s_Scratch.Count > 0)
            {
                foreach (UnitRec rec in s_Scratch) s_Map.Remove(rec.Unit);
                s_List.RemoveAll(x => !x.Unit);
                s_Scratch.Clear();
            }

            bool want = Wanted;
            Delegate cur = GameEvents.OnTargetDetected;
            if (want)
            {
                if (!s_Subscribed || !ReferenceEquals(cur, s_LastSeen))
                {
                    GameEvents.OnTargetDetected = (Action<Team, Target, Sensor>)Delegate.Combine(
                        (Action<Team, Target, Sensor>)Delegate.Remove(GameEvents.OnTargetDetected, s_Handler), s_Handler);
                    s_LastSeen = GameEvents.OnTargetDetected;
                    s_Subscribed = true;
                }
                State = "subscribed, " + s_List.Count + " units tracked";
            }
            else if (s_Subscribed)
            {
                GameEvents.OnTargetDetected = (Action<Team, Target, Sensor>)Delegate.Remove(GameEvents.OnTargetDetected, s_Handler);
                s_LastSeen = null;
                s_Subscribed = false;
                State = s_Enabled ? "unsubscribed (no unit tier on)" : "off (setting)";
            }
            else
            {
                State = s_Enabled ? "unsubscribed (no unit tier on)" : "off (setting)";
            }
        }

        static void OnTargetDetected(Team team, Target target, Sensor sensor)
        {
            if (!Wanted) return;
            try
            {
                if ((object)team == null || (object)target == null || !target) return;
                Team tt = target.Team;
                if (ReferenceEquals(tt, team) || !target.GetIsEnemy(team)) return;
                Events++;
                Vector3 p = target.transform.position;
                float until = Idle.Now + s_WakeSeconds;
                for (int i = 0; i < s_List.Count; i++)
                {
                    UnitRec rec = s_List[i];
                    if (rec.WakeUntil >= until) continue;
                    Unit u = rec.Unit;
                    if ((object)u == null || !u || !ReferenceEquals(u.Team, team)) continue;
                    if ((u.transform.position - p).sqrMagnitude > s_Radius2) continue;
                    rec.WakeUntil = until;
                    Wakes++;
                }
            }
            catch (Exception ex)
            {
                // Never break the game's event chain. Without this wake-up the unit tiers are less safe than designed,
                // so they go off too (fail-open: vanilla full rate).
                s_Enabled = false;
                State = "FAILED: " + ex.GetType().Name + ": " + ex.Message;
                TierDispatch.FailTier(TierDispatch.T_IdleSoldiers, ex, "detection wake-up handler");
                IdleAgents.FailItem(ex, "detection wake-up handler");
            }
        }
    }
}
