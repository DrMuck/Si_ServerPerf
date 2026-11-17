/*
 DeadNoUnitContact (default OFF; server-side, visible as described below): bodies of destroyed units stop making
 physical contact with units, so live units no longer shove them and DeadSettle's sleep can hold in a melee.

 Why (b13_dead arm B): in a dense melee only 12-13 dead units were settled at a time; per 10 s 44-54 settled units were
 woken again (creature 20-24, wheeled 14-15, hover 8-11) and 44-53 never got slow before the 12 s limit: corpses and
 wrecks are pushed and driven over continuously.

 How: Rigidbody.excludeLayers (Unity 2022.2+, used by the game itself in KinematicPhysics.Hold, KinematicPhysics.cs:
 199-216) on every dynamic body of the dead unit gets the "unit layers" added. Contacts between those bodies' colliders
 and colliders on an excluded layer are not generated; terrain, structures and everything on the protected layers
 still collide, so the corpse keeps lying / rolling on the ground. Queries are NOT affected by excludeLayers:
 raycasts, sphere casts, projectiles and overlap tests still hit the dead unit's colliders exactly as before (shots are
 still blocked by wrecks and corpses on the server; DamageManager, hit zones and splash unchanged).
 Unit layers: an allow-list by name, DeadNoContactLayers (default "Character,Vehicle Avoidance,Ignore Raycast
 Avoidance,Invisible Walls Avoidance"), resolved once per scene with LayerMask.NameToLayer, minus
 DeadNoContactProtectedLayers (default "Default,Terrain,Static,StaticSmall,Invisible Walls,Water,AIObstacle") as a guard,
 so ground and structures can never be excluded. (b14 used sampling of live units' collider layers instead; it also
 picked CollideWithStatic and SelfOnly Avoidance, which were no-ops, see below.) Layer facts, 1.0.17 globalgamemanagers
 (TagManager + PhysicsManager.m_LayerCollisionMatrix, read with UnityPy):
   10 Character                  soldiers' and creatures' solid colliders; collides with Default, TransparentFX, Ignore
                                 Raycast, Water, Character, Vehicle, Invisible Walls, Dynamic, Terrain, Static,
                                 StaticSmall, Vehicle Avoidance, Ignore Raycast Avoidance, Invisible Walls Avoidance
   11 Vehicle                    vehicles while a player drives them (CollisionAvoidance.UpdateCollisionLayer,
                                 Silica.AI/CollisionAvoidance.cs:156-235, swaps them back to the plain layers)
   27/28/29 Vehicle / Ignore Raycast / Invisible Walls Avoidance: the same vehicle colliders while AI-driven
                                 (UpdateCollisionLayer swaps Vehicle->27, Ignore Raycast->28, Invisible Walls->29,
                                 SelfOnly->30 for every collider not tagged "AvoidanceCollider"). Solid colliders, not
                                 the avoidance capsules. 27 does not collide with 27: AI vehicles never touch each other.
   20 CollideWithStatic          collides only with Default, Invisible Walls, Terrain, Static, StaticSmall: a unit-side
                                 layer that ignores every unit layer, so excluding it changes nothing either way.
   30 SelfOnly Avoidance         collides only with SelfOnly (25, the avoidance trigger capsules) - excluding it is a no-op.
 Plain "Vehicle" is NOT excluded by default, so a player's vehicle (simulated on his client) still shoves corpses on the
 server like on his client (no spring-back for players' vehicles); add it to the list to exclude those too.
 Applied once per dead unit when its death is seen (round robin, every unit every
 DeadSettleScanStrideSteps steps), per kind: DeadNoContactCreatures (default true), DeadNoContactVehicles (default
 false: AI vehicles would visibly drive through wrecks), DeadNoContactSoldiers (default false; a dead soldier's own
 capsule is already disabled by vanilla, Soldier.cs:2860-2863, so there is little left to exclude). Not applied to
 units a player controls or that are seated (re-checked until disposal).
 Original excludeLayers values are kept per body and written back on switch-off and when a unit comes back to life.

 Visible consequences (server simulates, clients follow the server's pose of the corpse):
   - live units pass through corpses (and wrecks, if vehicles are on) on the server: AI-driven units are seen walking /
     driving through them on clients;
   - a player's own unit is simulated on his client: his client still collides with the client copy of the corpse and
     pushes it, but the server's copy does not move, so the client copy is pulled back to the server pose
     (NetworkTransformComponent correction, :448-505) - a corpse that cannot be shoved, slowly springing back;
   - corpses also pass through each other (dead bodies are on the same unit layers);
   - nothing changes for shots, hit registration, damage or disposal.
   [Physics] deadcontact appliedUnits= appliedBodies= restored= skippedKind= mask=   (per log interval)
*/

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class DeadNoUnitContact
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;

        struct Held
        {
            public Rigidbody Body;
            public int Original;
        }

        sealed class Rec
        {
            public Unit U;
            public bool Applied, Skip;
            public readonly List<Held> Bodies = new List<Held>(2);
        }

        static readonly Dictionary<Unit, Rec> _recs = new Dictionary<Unit, Rec>(RefEq<Unit>.I);
        static readonly List<Rec> _list = new List<Rec>();
        static int _rr, _purgeStep, _mask;
        static bool _maskDone;
        static string _maskNames = "";
        static long _units, _bodies, _restored, _skippedKind;
        static float _nextLog;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("DeadNoUnitContact",
                "Destroyed units' dynamic bodies exclude contacts with unit layers (Rigidbody.excludeLayers): not shoved by live " +
                "units (sleep holds with DeadSettle), live units pass through corpses on the server; queries/shots unchanged",
                v => On = v, defaultOn: false);
            var fu = Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes);
            Item.Patch(fu, typeof(DeadNoUnitContact), postfix: nameof(FixedPost));
            Item.OnSwitch(on => { if (!on) RestoreAll(); });
            ctx.OnUpdate(Tick, Item);
            ctx.OnSceneLoaded(_ => { _recs.Clear(); _list.Clear(); _rr = 0; _mask = 0; _maskDone = false; });
        }

        static void FixedPost()
        {
            if (!On) return;
            try
            {
                if (!_maskDone) { _maskDone = true; RefreshMask(); }
                List<Unit> units = Unit.Units;
                if (units != null && units.Count > 0)
                {
                    int stride = Mathf.Max(1, S.DeadSettleScanStrideSteps);
                    int n = (units.Count + stride - 1) / stride;
                    for (int j = 0; j < n; j++)
                    {
                        if (_rr >= units.Count) _rr = 0;
                        Unit u = units[_rr++];
                        if (!u || !u.IsDestroyed || _recs.ContainsKey(u)) continue;
                        var r = new Rec { U = u, Skip = !KindOn(Kinds.Of(u)) };
                        if (r.Skip) _skippedKind++;
                        _recs.Add(u, r);
                        _list.Add(r);
                    }
                }
                for (int i = 0; i < _list.Count; i++)
                {
                    Rec r = _list[i];
                    if (r.Applied || r.Skip || !r.U || _mask == 0) continue;
                    if (r.U.ControlledBy || r.U.InCompartment) continue;
                    Apply(r);
                }
                if (++_purgeStep >= 25) { _purgeStep = 0; Purge(); }
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "OrderedUpdateManager.FixedUpdate postfix");
            }
        }

        static bool KindOn(BodyKind k)
        {
            switch (k)
            {
                case BodyKind.Creature: return S.DeadNoContactCreatures;
                case BodyKind.Soldier: return S.DeadNoContactSoldiers;
                case BodyKind.Wheeled:
                case BodyKind.Hover:
                case BodyKind.Air:
                case BodyKind.OtherVehicle: return S.DeadNoContactVehicles;
                default: return false;
            }
        }

        static void Apply(Rec r)
        {
            Rigidbody[] bodies = Census.BodiesOf(r.U);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (!rb || rb.isKinematic) continue;
                int orig = rb.excludeLayers.value;
                r.Bodies.Add(new Held { Body = rb, Original = orig });
                rb.excludeLayers = orig | _mask;
                _bodies++;
            }
            r.Applied = true;
            _units++;
        }

        static bool Restore(Rec r)
        {
            if (!r.Applied) return false;
            for (int i = 0; i < r.Bodies.Count; i++)
            {
                Held h = r.Bodies[i];
                if (h.Body) h.Body.excludeLayers = h.Original;
            }
            r.Bodies.Clear();
            r.Applied = false;
            return true;
        }

        // every second: drop disposed units, restore units that came back to life
        static void Purge()
        {
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                Rec r = _list[i];
                if (r.U && r.U.IsDestroyed) continue;
                if (r.U && Restore(r)) _restored++;
                _recs.Remove(r.U);
                int last = _list.Count - 1;
                _list[i] = _list[last];
                _list.RemoveAt(last);
            }
        }

        static void RestoreAll()
        {
            int n = 0;
            for (int i = 0; i < _list.Count; i++)
            {
                try { if (_list[i].U && Restore(_list[i])) n++; }
                catch (Exception ex) { Perf.Warn("Physics.DeadNoUnitContact: restore on switch-off: " + ex.Message); }
            }
            _restored += n;
            _recs.Clear();
            _list.Clear();
            Item.Info = "last switch-off: " + n + " dead units' excludeLayers restored";
        }

        static int LayerMaskOf(string list)
        {
            int m = 0;
            string[] names = (list ?? "").Split(',');
            for (int i = 0; i < names.Length; i++)
            {
                string nm = names[i].Trim();
                if (nm.Length == 0) continue;
                int l = LayerMask.NameToLayer(nm);
                if (l >= 0) m |= 1 << l;
            }
            return m;
        }

        // Allow-list by name (DeadNoContactLayers) minus the protected layers; resolved once per scene.
        static void RefreshMask()
        {
            int m = LayerMaskOf(S.DeadNoContactLayers) & ~LayerMaskOf(S.DeadNoContactProtectedLayers);
            _mask = m;
            var sb = new StringBuilder();
            for (int l = 0; l < 32; l++)
            {
                if ((m & (1 << l)) == 0) continue;
                if (sb.Length > 0) sb.Append(',');
                string nm = LayerMask.LayerToName(l);
                sb.Append(string.IsNullOrEmpty(nm) ? l.ToString() : nm);
            }
            _maskNames = m == 0 ? "(none: no listed layer exists)" : sb.ToString();
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
                Perf.Log("[Physics] deadcontact appliedUnits=" + _units + " appliedBodies=" + _bodies + " restored=" + _restored +
                         " skippedKind=" + _skippedKind + " trackedNow=" + _list.Count + " mask=" + _maskNames);
                Item.Info = "excluding layers [" + _maskNames + "] for " + _list.Count + " tracked dead units; last interval " + _units + " applied";
            }
            _units = _bodies = _restored = _skippedKind = 0;
        }
    }
}
