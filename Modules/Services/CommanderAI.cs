/*
 S9: vanilla commander AI (Silica.AI.AICommander, 1.0.17).

 What 1.0.17 does: AIManager.TickAI -> AICommander.Tick(dt) per commander every frame; when CurrentThinkDelay runs out
 Think() runs ALL of: ClearRequests, RefreshCombatCensus, EnemyIntel.Refresh, UpdatePosture, Resources, Exploration,
 Defense, Offense, Expansion, Unit, Construction handlers and KeepUnitsInPlayArea in one frame. Unchanged since 1.0.15
 apart from the handlers' internals (developer added PerfSections; the O(S^2 x clusters) RebuildClusters is still there).

 Si_RTS_AI (C:\Users\schwe\Projects\Si_RTS_AI) does NOT own the vanilla commander in the sense S9a needs. It keeps it
 enabled on purpose and depends on Think running:
   - AIManager.IsCommanderEnabled(team) is its "AI holds the seat, not a human" signal (ProductionV3, EcoPlanner);
   - AlienConstruction / HumanConstruction PREFIX AIConstructionHandler.Think (vanilla construction skipped when its
     planner executes; the one-shot construction-cost read happens there);
   - HumanHarvesterController and Phase2 ride on POSTFIXES of AIConstructionHandler.Think / AIUnitHandler.Think;
   - Phase31_Production postfixes AIUnitHandler.GetBestUnitToBuildForPreset: vanilla unit production still runs;
   - VanillaOrderGate brackets AICommander.Think to block (only) the move/attack orders issued inside it;
   - EventLog postfixes AICommander.Think (reads Requests).
 Skipping Tick for a Si_RTS_AI team would starve all of that. The "48 ms/s for nothing" in findings F Â§5 is the vanilla
 think whose ORDERS the gate blocks; its production, requests and ride-alongs are still used.

 So CommanderAI_SkipOwnedTeams implements an explicit, opt-in contract and nothing else:
   a mod OWNS a team's vanilla commander iff a static bool M(Team) it publishes returns true, i.e. it needs nothing
   that AICommander.Think produces or triggers. Probe names come from Settings.CommanderOwnerProbes (no assembly
   reference). Extra guard: a probe is refused while its own assembly still has a Harmony patch on AICommander,
   AIEnemyIntel, AIExpansion or any AIHandlerBase subclass (a ride-along would starve). Skipping Tick (not disabling
   the commander) keeps IsEnabled / IsCommanderEnabled true and CurrentThinkDelay frozen; when ownership ends the
   commander resumes where it was. With no probe resolved the prefix is one static check.

 S9b (spread the handlers over frames) is NOT implemented: Requests are produced by the first handlers and consumed by
 AIUnitHandler / AIConstructionHandler in the same Think, and they hold Targets / Units that Unity may destroy between
 frames (later handlers do not re-validate them); Think-level patches of other mods (VanillaOrderGate's order window,
 EventLog's Requests read, Phase2 counters) would see 8 partial thinks instead of one. Not "without changing logic".

 CommanderAI_ThinkStats: measurement only (Stopwatch around AICommander.Think, outermost patch), for the rig.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Services
{
    internal static class CommanderOwnership
    {
        internal static bool On;
        static PerfItem Item;

        sealed class Probe
        {
            public string Name;
            public Assembly Assembly;
            public Func<Team, bool> Fn;
        }

        struct Cached
        {
            public bool Owned;
            public float Until;
        }

        static List<string> _probeNames = new List<string>();
        static readonly List<Probe> _probes = new List<Probe>();
        static readonly Dictionary<Team, Cached> _cache = new Dictionary<Team, Cached>();
        static float _cacheSeconds = 1f;
        static bool _resolved;
        static string _probeState = "probes not resolved yet";

        // counters
        static long _ticksSkipped, _ticksSeen;
        static double _thinksAvoided;
        static int _probeCalls, _ownedNow;

        public static void Register(ModuleContext ctx, ServicesSettings s)
        {
            Item = ctx.Item("CommanderAI_SkipOwnedTeams",
                "Skip AICommander.Tick for a team only while a mod declares (Settings.CommanderOwnerProbes) that it needs nothing from that team's vanilla commander; no probe = vanilla",
                v => On = v);
            _probeNames = s.CommanderOwnerProbes ?? new List<string>();
            _cacheSeconds = Math.Max(0.1f, s.CommanderOwnerCacheSeconds);
            var tick = Item.Target(typeof(AICommander), nameof(AICommander.Tick), new[] { typeof(float) });
            Item.Patch(tick, typeof(CommanderOwnership), prefix: nameof(Prefix));
            ctx.OnSceneLoaded(_ => { _resolved = false; _cache.Clear(); }, Item);
            Item.OnSwitch(_ => _cache.Clear());
        }

        static bool Prefix(AICommander __instance, float dt)
        {
            if (!On) return true;
            try
            {
                if (!_resolved) Resolve();
                if (_probes.Count == 0) return true;
                _ticksSeen++;
                Team team = __instance.Team;
                if ((object)team == null || !Owned(team)) return true;
                _ticksSkipped++;
                if (__instance.IsEnabled)
                {
                    float interval = __instance.ThinkInterval;
                    if (interval > 0f) _thinksAvoided += dt / interval;
                }
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "AICommander.Tick prefix");
                return true;
            }
        }

        static bool Owned(Team team)
        {
            float now = Time.unscaledTime;
            if (_cache.TryGetValue(team, out Cached c) && now < c.Until) return c.Owned;
            bool owned = false;
            for (int i = 0; i < _probes.Count && !owned; i++)
            {
                Probe p = _probes[i];
                if (p.Fn == null) continue;
                try
                {
                    _probeCalls++;
                    owned = p.Fn(team);
                }
                catch (Exception ex)
                {
                    p.Fn = null; // a throwing probe never says "owned" again
                    Item.Note("probe " + p.Name + " threw " + ex.GetType().Name + ": " + ex.Message + " (disabled, vanilla runs)");
                }
            }
            _cache[team] = new Cached { Owned = owned, Until = now + _cacheSeconds };
            return owned;
        }

        // Runs once after every scene load (lazily, at the first Tick): every mod is loaded and has patched by then.
        static void Resolve()
        {
            _resolved = true;
            _probes.Clear();
            _cache.Clear();
            var found = new List<string>();
            var refused = new List<string>();
            foreach (string raw in _probeNames)
            {
                string name = raw?.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                int dot = name.LastIndexOf('.');
                if (dot <= 0 || dot == name.Length - 1) { refused.Add(name + " (not Type.Method)"); continue; }
                string typeName = name.Substring(0, dot), methodName = name.Substring(dot + 1);
                MethodInfo mi = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t;
                    try { t = asm.GetType(typeName, false); } catch { t = null; }
                    if (t == null) continue;
                    mi = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Team) }, null);
                    if (mi != null) break;
                }
                if (mi == null) continue; // owner mod absent or has no such method: nothing to do
                if (mi.ReturnType != typeof(bool)) { refused.Add(name + " (does not return bool)"); continue; }
                string rideAlong = FindRideAlongPatch(mi.DeclaringType.Assembly);
                if (rideAlong != null)
                {
                    refused.Add(name + " (its assembly still patches " + rideAlong + "; skipping Tick would starve it)");
                    continue;
                }
                Func<Team, bool> fn;
                try { fn = (Func<Team, bool>)Delegate.CreateDelegate(typeof(Func<Team, bool>), mi); }
                catch (Exception ex) { refused.Add(name + " (" + ex.GetType().Name + ")"); continue; }
                _probes.Add(new Probe { Name = name, Assembly = mi.DeclaringType.Assembly, Fn = fn });
                found.Add(name);
            }
            _probeState = (found.Count > 0 ? "probes active: " + string.Join(", ", found) : "no owner probe resolved (vanilla for every team)") +
                          (refused.Count > 0 ? "; refused: " + string.Join("; ", refused) : "");
            if (refused.Count > 0) Perf.Warn("Services.CommanderAI_SkipOwnedTeams: " + _probeState);
        }

        // A patch of the owner's own assembly on anything Think() runs means the owner still relies on Think.
        static string FindRideAlongPatch(Assembly owner)
        {
            try
            {
                foreach (MethodBase m in HarmonyLib.Harmony.GetAllPatchedMethods())
                {
                    Type dt = m.DeclaringType;
                    if (dt == null || !IsCommanderPart(dt)) continue;
                    Patches info = HarmonyLib.Harmony.GetPatchInfo(m);
                    if (info == null) continue;
                    if (Has(info.Prefixes, owner) || Has(info.Postfixes, owner) || Has(info.Transpilers, owner) || Has(info.Finalizers, owner))
                        return dt.Name + "." + m.Name;
                }
            }
            catch (Exception ex)
            {
                return "<patch scan failed: " + ex.GetType().Name + ">"; // cannot prove the absence: refuse
            }
            return null;
        }

        static bool IsCommanderPart(Type t) =>
            t == typeof(AICommander) || t == typeof(AIEnemyIntel) || t == typeof(AIExpansion) || typeof(AIHandlerBase).IsAssignableFrom(t);

        static bool Has(IEnumerable<Patch> patches, Assembly owner)
        {
            if (patches == null) return false;
            foreach (Patch p in patches)
            {
                if (p?.PatchMethod?.DeclaringType != null && p.PatchMethod.DeclaringType.Assembly == owner) return true;
            }
            return false;
        }

        internal static void RefreshInfo()
        {
            if (Item == null || !Item.Applied) return;
            int owned = 0;
            float now = Time.unscaledTime;
            foreach (var kv in _cache) if (kv.Value.Owned && now < kv.Value.Until + 2f) owned++;
            _ownedNow = owned;
            Item.Info = _probeState + " | teams owned " + _ownedNow + ", ticks skipped " + _ticksSkipped + "/" + _ticksSeen +
                        " (~" + _thinksAvoided.ToString("F0") + " thinks avoided), probe calls " + _probeCalls;
        }
    }

    internal static class CommanderThinkStats
    {
        internal static bool On;
        static PerfItem Item;
        static long _start;
        static bool _timing;
        static long _count, _ticksTotal, _ticksMax, _roundCount, _roundTicks, _roundMax;
        static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("CommanderAI_ThinkStats",
                "Measurement only: wall time of every AICommander.Think (outermost patch, includes other mods' Think patches); count / avg / max per round in the status",
                v => On = v);
            var think = Item.Target(typeof(AICommander), nameof(AICommander.Think), Type.EmptyTypes);
            Item.Patch(think, typeof(CommanderThinkStats), prefix: nameof(Prefix), priority: Priority.First);
            Item.Patch(think, typeof(CommanderThinkStats), postfix: nameof(Postfix), priority: Priority.Last);
            ctx.OnRoundStart(() => { _roundCount = 0; _roundTicks = 0; _roundMax = 0; }, Item);
        }

        static void Prefix()
        {
            if (!On) return;
            _timing = true;
            _start = Stopwatch.GetTimestamp();
        }

        static void Postfix()
        {
            if (!On || !_timing) return;
            _timing = false;
            long d = Stopwatch.GetTimestamp() - _start;
            _count++; _ticksTotal += d; if (d > _ticksMax) _ticksMax = d;
            _roundCount++; _roundTicks += d; if (d > _roundMax) _roundMax = d;
        }

        internal static void RefreshInfo()
        {
            if (Item == null || !Item.Applied) return;
            double avg = _roundCount > 0 ? _roundTicks * TicksToMs / _roundCount : 0;
            Item.Info = "round: thinks " + _roundCount + ", avg " + avg.ToString("F2") + " ms, max " + (_roundMax * TicksToMs).ToString("F1") +
                        " ms, total " + (_roundTicks * TicksToMs / 1000.0).ToString("F2") + " s | since start: thinks " + _count +
                        ", max " + (_ticksMax * TicksToMs).ToString("F1") + " ms";
        }
    }
}
