/*
 FixedWakeTrace (default OFF, diagnostic for IdleForceSleep): names the ordered fixed callback that wakes a
 force-slept wheeled vehicle ("scripts" in the idlesleep line; b10 FD: ~57 000 per 10 s, all bodies, no pending force).

 How: a transpiler on OrderedUpdateManager.UpdateList.Run replaces the one `Action<float>.Invoke(dt)` call (the
 per-entry callback) with FixedWakeTrace.Invoke(fn, dt), which calls fn(dt) exactly once, unchanged (exceptions
 propagate as before into Run's own try/catch). Only while the FIXED list runs (flag set by a prefix/postfix on
 OrderedUpdateManager.FixedUpdate) and the item is on, it also:
   - at the start of the step, takes every force-slept VehicleWheeled (IdleForceSleep.ForcedUnits) whose chassis is
     asleep as the watch list;
   - callback owned by a watched vehicle (delegate Target is a Component under that Unit): chassis IsSleeping before /
     after -> a wake is counted for that callback's method;
   - callback not owned by any unit (static batch methods, managers, KinematicPhysics integrators): all watched
     chassis before / after (sweep) -> each newly awake one is counted for that method;
   - callbacks owned by other, non-watched units: not checked; anything they wake shows as "unattributed" at the
     end of the list, as do wakes in OrderedUpdateManager.FixedUpdate before the list (PerformDelayedTeleportAll),
     which are swept separately ("pre-list").
 For the first 10 wakes per step it also lists the transforms of the woken vehicle whose Transform.hasChanged is set
 and whose parent's is not (the origin of a transform write since the last check; the game never reads or resets
 hasChanged, 1.0.17), then clears hasChanged on that vehicle. A wake attributed to a batch-query method
 (RaycastCommand / SpherecastCommand / OverlapSphereCommand scheduling) together with changed transforms points at a
 transform written in frame code and synced into PhysX when the batch query was scheduled.
   [Physics] fixedwake steps= watchedAvg= wakes= | <Type.Method>=n ... | preList= unattributed= | changed <transform>=n ...
 Phases (key prefix): fixed: = ordered fixed list, frame: = ordered update list (OrderedUpdateManager.Update),
 gm: = all of GameManager.Update (not split further). <pre-list> / <unattributed> per phase as described above.
 Cost while on: one dictionary lookup per fixed/update callback, 2 IsSleeping per callback of a watched vehicle, a sweep of
 the watch list around each unowned callback; diagnostic only.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class FixedWakeTrace
    {
        internal static bool On;
        static PerfItem Item;
        static MethodInfo _invoke;

        static bool _inFixed, _preSwept;
        static int _n;
        static Unit[] _units = new Unit[512];
        static Rigidbody[] _chassis = new Rigidbody[512];
        static bool[] _asleep = new bool[512];
        static readonly Dictionary<Unit, int> _index = new Dictionary<Unit, int>();
        static readonly Dictionary<object, Unit> _owner = new Dictionary<object, Unit>();
        static readonly HashSet<Unit> _primed = new HashSet<Unit>();
        static readonly Dictionary<string, long> _byMethod = new Dictionary<string, long>();
        static readonly Dictionary<MethodInfo, string> _labels = new Dictionary<MethodInfo, string>();
        static readonly Dictionary<string, long> _changed = new Dictionary<string, long>();
        static readonly List<Transform> _tmp = new List<Transform>();
        static long _steps, _watchSum, _wakes, _pre, _unattributed;
        static int _detailsThisStep;
        static float _nextLog;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("FixedWakeTrace",
                "Diagnostic for IdleForceSleep: which ordered fixed callback wakes a force-slept wheeled vehicle (wraps the per-entry " +
                "callback of OrderedUpdateManager's list; plus the changed transforms of the woken vehicle)",
                v => On = v, defaultOn: false);
            _invoke = AccessTools.Method(typeof(Action<float>), "Invoke", new[] { typeof(float) });
            Type listType = AccessTools.Inner(typeof(OrderedUpdateManager), "UpdateList");
            MethodBase run = listType == null ? null
                : Item.Target(listType, "Run", new[] { typeof(float), typeof(bool), typeof(long), typeof(float), typeof(int) }, declaredOnly: true);
            if (listType == null) Item.Note("OrderedUpdateManager.UpdateList absent");
            Item.Patch(run, typeof(FixedWakeTrace), transpiler: nameof(Transpiler));
            var fu = Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes);
            // prefix last (after the other items' bookkeeping), postfix first (before IdleForceSleep re-sleeps)
            Item.Patch(fu, typeof(FixedWakeTrace), prefix: nameof(StepPre), priority: Priority.Last);
            Item.Patch(fu, typeof(FixedWakeTrace), postfix: nameof(StepPost), priority: Priority.First);
            // Frame phase: the census sees ~25 % of wheeled bodies awake in Update although they are asleep again at the
            // next fixed step (vanilla VehicleWheeled.OrderedUpdate -> SleepVehicle re-sleeps them in the frame), so the
            // same tracing runs over OrderedUpdateManager's update list ("frame:") and around GameManager.Update ("gm:").
            var ou = Item.Target(typeof(OrderedUpdateManager), "Update", Type.EmptyTypes);
            Item.Patch(ou, typeof(FixedWakeTrace), prefix: nameof(FramePre), postfix: nameof(StepPost));
            var gm = Item.Target(typeof(GameManager), "Update", Type.EmptyTypes);
            Item.Patch(gm, typeof(FixedWakeTrace), prefix: nameof(GmPre), postfix: nameof(StepPost));
            Item.OnSwitch(_ => Reset());
            ctx.OnSceneLoaded(_ => { Reset(); _primed.Clear(); _owner.Clear(); });
            ctx.OnUpdate(Tick, Item);
        }

        static void Reset()
        {
            _inFixed = false;
            _n = 0;
            _index.Clear();
        }

        // Exactly one `callvirt Action<float>::Invoke(float)` in 1.0.17's UpdateList.Run (the entry callback).
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            int hits = 0, at = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (_invoke != null && list[i].Calls(_invoke)) { hits++; at = i; }
            }
            if (hits != 1)
            {
                Item.Problem("expected 1 Action<float>.Invoke in UpdateList.Run, found " + hits + "; left unchanged");
                return list;
            }
            list[at].opcode = OpCodes.Call;
            list[at].operand = AccessTools.Method(typeof(FixedWakeTrace), nameof(Invoke));
            return list;
        }

        // ---------------------------------------------------------------- step bracket

        static string _phase = "fixed:";

        static void FramePre() => Begin("frame:");
        static void GmPre() => Begin("gm:GameManager.Update ");
        static void StepPre() => Begin("fixed:");

        static void Begin(string phase)
        {
            if (!On) return;
            try
            {
                Reset();
                _phase = phase;
                _detailsThisStep = 0;
                _preSwept = false;
                List<Unit> forced = IdleForceSleep.ForcedUnits;
                int primedNow = 0;
                for (int i = 0; i < forced.Count; i++)
                {
                    Unit u = forced[i];
                    if (!(u is VehicleWheeled) || !u) continue;
                    Rigidbody rb = u.RigidBody;
                    if (!rb || !rb.IsSleeping()) continue;
                    if (!_primed.Contains(u))
                    {
                        if (primedNow >= 20) continue;
                        ClearChanged(u);
                        _primed.Add(u);
                        primedNow++;
                    }
                    if (_n == _units.Length)
                    {
                        Array.Resize(ref _units, _n * 2);
                        Array.Resize(ref _chassis, _n * 2);
                        Array.Resize(ref _asleep, _n * 2);
                    }
                    _units[_n] = u;
                    _chassis[_n] = rb;
                    _asleep[_n] = true;
                    _index[u] = _n;
                    _n++;
                }
                _steps++;
                _watchSum += _n;
                _inFixed = _n > 0;
            }
            catch (Exception ex) { Item.Fail(ex, "OrderedUpdateManager.FixedUpdate prefix"); }
        }

        static void StepPost()
        {
            if (!On || !_inFixed) { _inFixed = false; return; }
            _inFixed = false;
            try
            {
                for (int i = 0; i < _n; i++)
                {
                    if (!_asleep[i]) continue;
                    Rigidbody rb = _chassis[i];
                    if (rb && !rb.IsSleeping()) { _asleep[i] = false; _unattributed++; _wakes++; Add(_phase + "<unattributed>", 1); Detail(_units[i]); }
                }
            }
            catch (Exception ex) { Item.Fail(ex, "OrderedUpdateManager.FixedUpdate postfix"); }
        }

        // ---------------------------------------------------------------- per callback

        public static void Invoke(Action<float> fn, float dt)
        {
            if (!On || !_inFixed)
            {
                fn(dt);
                return;
            }
            int mode = 0, own = -1;
            try
            {
                if (!_preSwept)
                {
                    _preSwept = true;
                    int n = Sweep();
                    _pre += n;
                    if (n > 0) Add(_phase + "<pre-list>", n);
                }
                object target = fn.Target;
                Unit owner = OwnerOf(target);
                if (owner != null)
                {
                    if (_index.TryGetValue(owner, out own) && _asleep[own]) mode = 1;
                }
                else
                {
                    mode = 2;
                }
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "fixed callback trace (before)");
                mode = 0;
            }
            fn(dt);
            if (mode == 0) return;
            try
            {
                if (mode == 1)
                {
                    Rigidbody rb = _chassis[own];
                    if (rb && !rb.IsSleeping())
                    {
                        _asleep[own] = false;
                        _wakes++;
                        Count(fn, 1);
                        Detail(_units[own]);
                    }
                }
                else
                {
                    int n = Sweep();
                    if (n > 0) Count(fn, n);
                }
            }
            catch (Exception ex) { Item.Fail(ex, "fixed callback trace (after)"); }
        }

        static int Sweep()
        {
            int woke = 0;
            for (int i = 0; i < _n; i++)
            {
                if (!_asleep[i]) continue;
                Rigidbody rb = _chassis[i];
                if (rb && !rb.IsSleeping())
                {
                    _asleep[i] = false;
                    woke++;
                    Detail(_units[i]);
                }
            }
            _wakes += woke;
            return woke;
        }

        static Unit OwnerOf(object target)
        {
            if (target == null) return null;
            if (_owner.TryGetValue(target, out Unit u)) return u;
            u = target is Component c && c ? c.GetComponentInParent<Unit>(true) : null;
            if (_owner.Count > 20000) _owner.Clear();
            _owner[target] = u;
            return u;
        }

        static void Count(Action<float> fn, int n)
        {
            MethodInfo m = fn.Method;
            if (!_labels.TryGetValue(m, out string label))
            {
                label = (m.DeclaringType != null ? m.DeclaringType.Name : "?") + "." + m.Name;
                _labels[m] = label;
            }
            Add(_phase + label, n);
        }

        static void Add(string key, long n)
        {
            _byMethod.TryGetValue(key, out long v);
            _byMethod[key] = v + n;
        }

        // Changed-transform origins of a woken vehicle (capped), then clear its hasChanged flags.
        static void Detail(Unit u)
        {
            if (!u || _detailsThisStep >= 10) return;
            _detailsThisStep++;
            _tmp.Clear();
            u.GetComponentsInChildren(true, _tmp);
            for (int i = 0; i < _tmp.Count; i++)
            {
                Transform t = _tmp[i];
                if (!t || !t.hasChanged) continue;
                Transform p = t.parent;
                bool origin = t == u.transform || !p || !p.hasChanged;
                if (!origin) continue;
                string name = t == u.transform ? "<root>" : t.name;
                _changed.TryGetValue(name, out long v);
                _changed[name] = v + 1;
            }
            for (int i = 0; i < _tmp.Count; i++) if (_tmp[i]) _tmp[i].hasChanged = false;
            _tmp.Clear();
        }

        static void ClearChanged(Unit u)
        {
            _tmp.Clear();
            u.GetComponentsInChildren(true, _tmp);
            for (int i = 0; i < _tmp.Count; i++) if (_tmp[i]) _tmp[i].hasChanged = false;
            _tmp.Clear();
        }

        // ---------------------------------------------------------------- log

        static void Tick()
        {
            var s = PhysicsModule.S;
            float now = Time.realtimeSinceStartup;
            if (s == null || s.CensusLogSeconds <= 0f || now < _nextLog) return;
            bool first = _nextLog == 0f;
            _nextLog = now + s.CensusLogSeconds;
            if (!first && _steps > 0)
            {
                var sb = new StringBuilder(512);
                sb.Append("fixedwake brackets=").Append(_steps).Append(" watchedAvg=").Append(_watchSum / _steps).Append(" wakes=").Append(_wakes).Append(" |");
                AppendTop(sb, _byMethod, 16);
                sb.Append(" | preList=").Append(_pre).Append(" unattributed=").Append(_unattributed).Append(" | changed");
                AppendTop(sb, _changed, 10);
                Perf.Log("[Physics] " + sb);
                Item.Info = "last interval: " + _wakes + " wakes of force-slept vehicles in the fixed list";
            }
            _steps = _watchSum = _wakes = _pre = _unattributed = 0;
            _byMethod.Clear();
            _changed.Clear();
        }

        static void AppendTop(StringBuilder sb, Dictionary<string, long> d, int max)
        {
            var list = new List<KeyValuePair<string, long>>(d);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < list.Count && i < max; i++) sb.Append(' ').Append(list[i].Key).Append('=').Append(list[i].Value);
        }
    }
}
