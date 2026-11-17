/*
 S8: A* navmesh cut updates (A* Pathfinding Project as shipped in 1.0.17, decompiled from AstarPathfindingProject.dll).

 Astar_CutPollInterval. AstarPath.Update -> NavmeshUpdates.Update every frame:
     if ((updateInterval >= 0 && realtimeSinceStartup - lastUpdateTime > updateInterval) || anyTilesDirty)
         ScheduleTileUpdates();   // every clipper x every recast listener: NavmeshCut.RequiresUpdate (2 transform
                                  // reads + Quaternion.Angle), then MarkTilesDirty / AddWorkItem for moved cuts
 The scenes serialize updateInterval = 0 (findings C §3; SilicaCore never writes it), so the poll runs every frame for
 every structure cut (GraphMask.everything -> 3 recast graphs) and every learned blocker. With 0.5 s:
   - a NEW or MOVED cut is applied up to 0.5 s later (GridLookup.Add marks the root dirty, RequiresUpdate sees it);
   - a REMOVED cut is still immediate: HandleOnDisableCallback marks tiles dirty and resets lastUpdateTime, and dirty
     tiles bypass the interval;
   - NavmeshUpdates.ForceUpdate (CollisionObstacleManager.ApplyChanges, learned blockers) ignores the interval.
 Units do not cut in 1.0.17 either (Unit.EnableNavmeshCutter is still an empty body), so nothing that moves every frame
 depends on the poll. The value is set on AstarPath.active after every map load and restored on switch-off.
 S8 "static cuts not polled": with the interval the poll runs ~2x/s; excluding structure cuts entirely would need a
 per-call patch on NavmeshCut.RequiresUpdate that costs as much as the check it skips. graphMask narrowing changes
 which graphs a structure cuts (soldiers would path through it on the graphs left out): not done.

 Astar_ProbeFlushDebounce (default off). CollisionObstacleManager.ApplyChanges does navmeshUpdates.ForceUpdate() +
 AstarPath.FlushWorkItems() synchronously whenever blockers are pending, then re-queues every AI path that passes a
 pending blocker. Two callers: VehicleStuckRecovery (reporter = the stuck vehicle, re-queues its own path at once)
 and VehicleBlockerProbe.Publish (reporter = null, PREDICTED blockers ahead of moving vehicles). Only the reporter-null
 calls are coalesced: at most one per window; a skipped call leaves its blockers in s_Pending, and the next flush
 (the deferred call from OnUpdate, or any stuck-recovery call, which is never delayed) applies all of them. Cost: the
 vehicles whose paths cross a predicted blocker re-path up to one window later; if one reaches the blocker first it
 gets stuck and its stuck-recovery call flushes immediately (no unit is left without a re-path). Not identical, so off.
*/

using System;
using HarmonyLib;
using Pathfinding;
using Pathfinding.Graphs.Navmesh;
using Silica.AI;
using UnityEngine;

namespace SiServerPerf.Modules.Services
{
    internal static class AstarCutPoll
    {
        internal static bool On;
        static PerfItem Item;
        static float _interval = 0.5f;

        static AstarPath _applied;     // instance we changed
        static float _original;        // its serialized value
        static bool _changed;          // we wrote the field on _applied
        static string _lastMap = "";
        static long _scans, _scansAtLastInfo;
        static float _lastInfoTime;

        public static void Register(ModuleContext ctx, ServicesSettings s)
        {
            Item = ctx.Item("Astar_CutPollInterval",
                "AstarPath.navmeshUpdates.updateInterval raised to Settings.NavmeshCutUpdateInterval (vanilla 0 = cut dirty-poll every frame); removals, dirty tiles and ForceUpdate stay immediate",
                v => On = v);
            _interval = Mathf.Clamp(s.NavmeshCutUpdateInterval, 0.05f, 2f);
            // counter only (how often the cut scan really runs); private, so resolved by name
            var sched = Item.Target(typeof(NavmeshUpdates), "ScheduleTileUpdates", Type.EmptyTypes);
            Item.Patch(sched, typeof(AstarCutPoll), postfix: nameof(CountScan));
            if (Item.Hooked("AstarPath.active.navmeshUpdates.updateInterval (per-frame instance check)"))
            {
                ctx.OnUpdate(Apply, Item);
            }
            Item.OnSwitch(on => { if (on) Apply(); else Restore(); });
        }

        static void CountScan()
        {
            if (!On) return;
            _scans++;
        }

        // Per frame: one static read and a reference compare unless the map changed.
        static void Apply()
        {
            if (!On) return;
            AstarPath active = AstarPath.active;
            if ((object)active == null || ReferenceEquals(active, _applied)) return;
            if (Game.GetIsClient()) return;
            NavmeshUpdates nu = active.navmeshUpdates;
            if (nu == null) return;
            _applied = active;
            _original = nu.updateInterval;
            _lastMap = Perf.SceneName;
            if (_original >= 0f && _original < _interval)
            {
                nu.updateInterval = _interval;
                _changed = true;
                Perf.Log("[Services] Astar_CutPollInterval: " + _lastMap + " navmeshUpdates.updateInterval " + _original + " -> " + _interval);
            }
            else
            {
                _changed = false;
                Item.Note("map " + _lastMap + " already has updateInterval " + _original + " (left unchanged)");
            }
        }

        static void Restore()
        {
            try
            {
                // AstarPath's base (VersionedMonoBehaviour) lives in PackageTools, not referenced: go through UnityEngine.Object
                UnityEngine.Object asObj = (object)_applied as UnityEngine.Object;
                if (_changed && (object)asObj != null && asObj && _applied.navmeshUpdates != null)
                {
                    _applied.navmeshUpdates.updateInterval = _original;
                }
            }
            catch (Exception ex)
            {
                Perf.Warn("[Services] Astar_CutPollInterval restore: " + ex.Message);
            }
            _changed = false;
            _applied = null; // re-applied on the next Apply after a switch-on
        }

        internal static void RefreshInfo()
        {
            if (Item == null || !Item.Applied) return;
            float now = Perf.RealTime;
            float span = now - _lastInfoTime;
            double rate = span > 0.01f ? (_scans - _scansAtLastInfo) / span : 0;
            _scansAtLastInfo = _scans;
            _lastInfoTime = now;
            string cur = "?";
            try { if ((object)AstarPath.active != null && AstarPath.active.navmeshUpdates != null) cur = AstarPath.active.navmeshUpdates.updateInterval.ToString("0.###"); } catch { }
            Item.Info = "updateInterval now " + cur + (_changed ? " (was " + _original + ")" : "") + " | cut scans " + _scans + " total, " + rate.ToString("F1") + "/s";
        }
    }

    internal static class ProbeFlushDebounce
    {
        internal static bool On;
        static PerfItem Item;
        static float _window = 0.5f;
        static float _lastPass = float.NegativeInfinity;
        static bool _deferred;
        static bool _inDeferredCall;
        static long _calls, _probeCalls, _deferredCalls, _deferredRuns, _stuckCalls;

        public static void Register(ModuleContext ctx, ServicesSettings s)
        {
            Item = ctx.Item("Astar_ProbeFlushDebounce",
                "CollisionObstacleManager.ApplyChanges(null) from VehicleBlockerProbe at most once per Settings.ProbeFlushMinIntervalSeconds (pending blockers kept, deferred call from OnUpdate); stuck-recovery calls never delayed",
                v => On = v, defaultOn: false);
            _window = Mathf.Clamp(s.ProbeFlushMinIntervalSeconds, 0.05f, 5f);
            var m = Item.Target(typeof(CollisionObstacleManager), nameof(CollisionObstacleManager.ApplyChanges), new[] { typeof(Unit) });
            Item.Patch(m, typeof(ProbeFlushDebounce), prefix: nameof(Prefix));
            if (Item.Applied) ctx.OnUpdate(RunDeferred, Item);
            ctx.OnSceneLoaded(_ => { _deferred = false; _lastPass = float.NegativeInfinity; }, Item);
            // switching off with a call outstanding: run it at once so no pending blocker waits
            Item.OnSwitch(on => { if (!on && _deferred) { _deferred = false; try { CollisionObstacleManager.ApplyChanges(null); } catch { } } });
        }

        static bool Prefix(Unit reporter)
        {
            if (!On) return true;
            try
            {
                _calls++;
                float now = Time.unscaledTime;
                if ((object)reporter != null)
                {
                    // stuck vehicle: vanilla, now; its flush also applies everything a deferred probe call left pending
                    _stuckCalls++;
                    _lastPass = now;
                    _deferred = false;
                    return true;
                }
                _probeCalls++;
                if (_inDeferredCall || now - _lastPass >= _window)
                {
                    _lastPass = now;
                    _deferred = false;
                    return true;
                }
                _deferred = true;
                _deferredCalls++;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "ApplyChanges prefix");
                return true;
            }
        }

        static void RunDeferred()
        {
            if (!_deferred) return;
            if (Time.unscaledTime - _lastPass < _window) return;
            _deferred = false;
            _deferredRuns++;
            _inDeferredCall = true;
            try { CollisionObstacleManager.ApplyChanges(null); }
            finally { _inDeferredCall = false; }
        }

        internal static void RefreshInfo()
        {
            if (Item == null || !Item.Applied) return;
            Item.Info = "ApplyChanges calls " + _calls + " (stuck-recovery " + _stuckCalls + ", probe " + _probeCalls + "), probe calls deferred " +
                        _deferredCalls + ", deferred flushes run " + _deferredRuns + (_deferred ? ", one outstanding" : "");
        }
    }
}
