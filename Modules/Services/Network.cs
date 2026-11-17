/*
 S10, server-only part: no per-object update serialization while nobody can receive it.

 GameManager.Update -> NetworkComponent.TickNetworkObjects(unscaledDt) every frame -> per ticked component Tick():
 m_NetUpdateCurTime += dt * TickRateHz; at >= 1 and GetCanSendNetUpdates(): SendNetUpdate(), which asks every net
 method's GetShouldSendNetUpdate, serializes the ObjectUpdate packet and calls NetworkLayer.SendPacket. SendPacket
 sends to every Player in Player.Players except NetworkGameServer.GetServerPlayer(); with none it sends nothing, so
 all of that serialization is discarded. The prefix skips TickNetworkObjects under exactly that condition (same loop,
 evaluated once per frame).

 Why a client that connects later cannot tell (1.0.17, all 16 OnSendNetUpdate / GetShouldSendNetUpdate pairs read):
   - 14 are pure writes of current state (Soldier, CreatureDecapod, Vehicle*, Harvester, BomberCraft, CreatureTurret,
     UnitAimAt, TurretAimEnemy, ConstructionSite, Structure, WorldLight, Example) with no server-side side effect;
   - NetworkTransformComponent: OnSendNetUpdate stores the last-sent pose (tolerance base) and clears net_HasChanged.
     Skipped, the flag stays set and the base stays old, so the first tick after a join sends the current pose; the
     join's NetInit (OnSendNetInit -> WritePhysics) writes the current pose and refreshes the base anyway;
   - Drone: stores the last-sent yaw (send gate); stale = one extra send after the join.
   - m_NetUpdateCurTime only phases the sends; it stays where it was and resumes.
   - NetworkLayer.NetBitsSent is only counted per recipient, IsHeavyLoad does not depend on these sends.
 The joining client gets the full state through the normal SendNetInitAll path, untouched. Reliable RPCs, health
 sync, team/AI sync are other paths and are not touched. No wire-format change.

 Not done: writeDebug = false. GameByteStreamReader.GetGameByteStreamReader defaults readDebug = true on the client,
 which expects the per-field type tags; turning them off on the server changes the wire format.
*/

using System;
using System.Collections.Generic;

namespace SiServerPerf.Modules.Services
{
    internal static class NetSkipNoClients
    {
        internal static bool On;
        static PerfItem Item;
        static long _skippedFrames, _ranFrames;
        static bool _lastSkipped;
        static int _transitions;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("Net_SkipUpdatesNoClients",
                "Skip NetworkComponent.TickNetworkObjects (per-object update serialization) while no remote player is in Player.Players; joins still get the full NetInit",
                v => On = v);
            var m = Item.Target(typeof(NetworkComponent), nameof(NetworkComponent.TickNetworkObjects), new[] { typeof(float) });
            Item.Patch(m, typeof(NetSkipNoClients), prefix: nameof(Prefix));
        }

        static bool Prefix()
        {
            if (!On) return true;
            try
            {
                if (!Game.GetIsServer() || HasRemotePlayer())
                {
                    _ranFrames++;
                    if (_lastSkipped) { _lastSkipped = false; _transitions++; }
                    return true;
                }
                _skippedFrames++;
                if (!_lastSkipped) { _lastSkipped = true; _transitions++; }
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "TickNetworkObjects prefix");
                return true;
            }
        }

        // Same condition as NetworkLayer.SendPacket's recipient loop.
        static bool HasRemotePlayer()
        {
            List<Player> players = Player.Players;
            if (players == null || players.Count == 0) return false;
            Player server = NetworkGameServer.GetServerPlayer();
            for (int i = 0; i < players.Count; i++)
            {
                if (!(players[i] == server)) return true;
            }
            return false;
        }

        internal static void RefreshInfo()
        {
            if (Item == null || !Item.Applied) return;
            Item.Info = "frames skipped (no remote player) " + _skippedFrames + ", frames ticked " + _ranFrames + ", state changes " + _transitions +
                        (_lastSkipped ? " | now skipping" : " | now ticking");
        }
    }
}
