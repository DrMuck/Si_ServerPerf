/*
 S13: Steam server-details push at 1 Hz, temporary-ban expiry walk at 1 Hz (findings I §5).

 NetworkGameServer.Update calls the private SendUpdatedServerDetailsToSteam every frame once the server is ready
 (5 Steam setters, ~0.21 ms per call, 14-25 ms/s). Only the master-server listing reads it. The match-time and
 player-count tag checks live inside the method, so they still run once per interval; a tag update the game queued
 (m_QueueUpdateGameTags, e.g. RequestTagsUpdate on level load) goes out at once.
 NetworkServerSettings.UpdatePlayerBans subtracts Time.unscaledTime instead of the frame delta from BanUpdateDelay
 and so walks the ban list every frame. It only expires temporary bans; enforcement at connect is untouched.
*/

using System;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.Headless
{
    internal static class SteamDetails
    {
        internal static bool On;
        static PerfItem Item;
        static AccessTools.FieldRef<NetworkGameServer, bool> _queueTags;
        static float _interval = 1f;
        static float _next;

        public static void Register(ModuleContext ctx, HeadlessSettings s)
        {
            Item = ctx.Item("SteamDetails_1Hz",
                "Push the Steam server details at most once per interval instead of every frame; tag updates queued by the game go out at once",
                v => On = v);
            ServerOptimizerCompat.Apply(Item);
            _interval = Math.Max(0.1f, s.SteamDetailsIntervalSeconds);
            var m = Item.Target(typeof(NetworkGameServer), "SendUpdatedServerDetailsToSteam", Type.EmptyTypes);
            _queueTags = Refl.Field<NetworkGameServer, bool>("m_QueueUpdateGameTags", Item);
            if (_queueTags == null) Item.Block("NetworkGameServer.m_QueueUpdateGameTags absent");
            Item.Patch(m, typeof(SteamDetails), prefix: nameof(Prefix));
        }

        static bool Prefix(NetworkGameServer __instance)
        {
            if (!On) return true;
            try
            {
                float now = Time.unscaledTime;
                if (now < _next && !_queueTags(__instance)) return false;
                _next = now + _interval;
                return true;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "SendUpdatedServerDetailsToSteam prefix");
                return true;
            }
        }
    }

    internal static class PlayerBans
    {
        internal static bool On;
        static PerfItem Item;
        static AccessTools.FieldRef<NetworkServerSettings, float> _banUpdateDelay;
        static float _interval = 1f;
        static float _next;

        public static void Register(ModuleContext ctx, HeadlessSettings s)
        {
            Item = ctx.Item("PlayerBans_1Hz",
                "Walk the temporary-ban list once per interval (vanilla walks it every frame: BanUpdateDelay -= Time.unscaledTime bug); ban checks at connect untouched",
                v => On = v);
            ServerOptimizerCompat.Apply(Item);
            _interval = Math.Max(0.1f, s.PlayerBansIntervalSeconds);
            var m = Item.Target(typeof(NetworkServerSettings), nameof(NetworkServerSettings.UpdatePlayerBans), Type.EmptyTypes);
            _banUpdateDelay = Refl.Field<NetworkServerSettings, float>("BanUpdateDelay", Item);
            Item.Patch(m, typeof(PlayerBans), prefix: nameof(Prefix));
        }

        static bool Prefix()
        {
            if (!On) return true;
            try
            {
                float now = Time.unscaledTime;
                if (now < _next) return false;
                _next = now + _interval;
                // let the vanilla body pass its own (broken) delay check on every timed call
                NetworkServerSettings settings = NetworkServerSettings.Instance;
                if (settings != null && _banUpdateDelay != null) _banUpdateDelay(settings) = 0f;
                return true;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "UpdatePlayerBans prefix");
                return true;
            }
        }
    }
}
