/*
 DeadDisposeEarly (default ON since 0.2.0, as "always 5 s": DisposeEarlySeconds 5, DisposeEarlyFpsLow = High = 0) -
 VISIBLE TO PLAYERS: corpses and wrecks of units vanish sooner than the vanilla 15 s.
 The disposal time is always DisposeEarlySeconds (Low = High = 0, the default), or a continuous function of the
 smoothed server fps (proportional control, e.g. Low 25 / High 45). Structures are never affected. Item off = vanilla 15 s.
 Always mode in this code: _wantF = 1 without touching (High - Low), so no division; f still rises 0 -> 1 through the
 slew limit, i.e. 15 -> 5 s over ~20 s after start, every scene change and every switch-on (0.5 s per s over a 10 s
 span); the fps average keeps running only for the log line. Slew 0 would keep it at 15 s for good.

 Vanilla (1.0.17): BaseGameObject.DestroyedDisposeTime is a per-instance serialized field (prefab default 15 s,
 BaseGameObject.cs:67), read only by BaseGameObject.CanDispose (:315-323), Unit.CanDispose (Unit.cs:432-441: > 0 and
 not ControlledBy) and BaseGameObject.UpdateDestroyedDisposal (:801-837), which adds Time.deltaTime per frame to the
 object's entry in BaseGameObjectsDisposalTracker from the first frame CanDispose is true and calls
 Object.Destroy(gameObject) once that sum reaches DestroyedDisposeTime. No shared constant: changing the field on one
 destroyed instance changes only that object's threshold; the tracker keeps counting from its death as before, so the
 field can be lowered and raised again at any time. (Services.DestroyedDisposal_EveryN only changes how often that walk
 runs.) Clients have no timer of their own (UpdateDestroyedDisposal returns on a client, :803-806): the server's
 Object.Destroy sends a reliable ObjectDestroy (NetworkComponent.cs:934-944, :757-772) and the client destroys the
 object on receipt, immediately, without a fade (NetworkLayer.cs:710-737).

 Controller (per frame, real time):
   fps      = 1 / (exponential average of the frame time, time constant DisposeEarlyFpsSmoothSeconds = 10 s)
   wanted f = 0 at fps >= DisposeEarlyFpsHigh (e.g. 45), 1 at fps <= DisposeEarlyFpsLow (e.g. 25), linear in between;
              Low = High = 0 (default; or any High <= Low) -> f = 1 always ("always DisposeEarlySeconds")
   f        moves towards wanted f by at most DisposeEarlySlewSecondsPerSecond (0.5) seconds of target time per second,
            i.e. df/dt <= slew / (15 - DisposeEarlySeconds); f starts at 0 (vanilla) when the item is switched on.
   target   for a unit with original time O: T(O) = O - f * (O - DisposeEarlySeconds)   (O <= DisposeEarlySeconds or
            O = 0 "never": left alone). Logged as T for O = 15 s.
 Per dead unit (tracked from when its death is seen, round robin over Unit.Units every DeadSettleScanStrideSteps steps;
 per kind DisposeEarlySoldiers / Creatures / Vehicles), every fixed step, with age = game time since its death was seen:
   target above the current value -> raised to the target (at most O; the unit simply stays longer)
   target below the current value -> lowered to max(target, age + 1 s), and never raised again by its own age: a unit
            already older than the new target goes 1 s after the target passed it. With the slew limit the target
            sweeps the age band at <= 0.5 s per s, so at most the units that died within that band leave per second -
            about 1.5x the normal turnover, never a whole population at once.
 Revival (SetHealth, checked every second), switch-off and scene change write the original value back.

 Loop stability. Plant: lowering the dispose time by dT removes deathRate * dT dead units, but only after about one
 dispose time (5-15 s) as the population turns over; b15 at ~518 lab units: 15 s -> 5 s moved fps ~27 -> ~40, i.e. about
 1.3 fps per second of dispose time. Controller gain: (15 - 5) s over (High - Low) = 20 fps = 0.5 s per fps. Loop gain
 0.5 * 1.3 = 0.65 < 1: a proportional loop with gain below one stays stable whatever the dead time (it cannot
 amplify its own error round the loop), and settles at fps ~34 / T ~9.6 s for that load (fps = 27.3 + 1.3 (15 - T),
 T = 15 - 0.5 (45 - fps)). The old open/closed valve had an effective gain of ~10 s per 5 fps hysteresis = 2 s per fps
 (loop gain ~2.6) - hence the limit cycle. On top, the 10 s smoothing and the slew (a full 15 -> 5 s swing takes >= 20 s)
 keep the controller slower than the plant's 10-20 s response, so it does not chase the population's own turnover.
 If a heavier load drives fps below Low, f saturates at 1 (= always 5 s) and the loop is open, as with the old item.

 Player-visible effects: wrecks and bodies disappear sooner (pop out, no fade) the lower the server fps; at fps >= High
 nothing changes. They stop blocking paths and shots and can no longer be used as cover after the target time; death
 effects parented to the unit that last longer are cut.
   [Physics] deaddispose target=..s targetMin=..s targetMax=..s wanted=..s fps=.. fpsMin=.. fpsMax=.. low= high= lowered=
   raised= restored= revived= trackedNow= shortNow=      (target* for a 15 s unit; min/max over the log interval;
   lowered / raised = field writes, done when the target moved by more than 0.05 s)
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class DeadDisposeEarly
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;

        const float VanillaRef = 15f;   // BaseGameObject.DestroyedDisposeTime prefab default, only for logging and the slew scale

        sealed class Rec
        {
            public Unit U;
            public float SeenAt, Original, Applied;
            public bool Valid;           // Original > DisposeEarlySeconds (else left alone)
        }

        static readonly List<Rec> _list = new List<Rec>();
        static readonly HashSet<Unit> _set = new HashSet<Unit>(RefEq<Unit>.I);
        static int _rr, _purgeStep, _shortNow;
        static float _emaDt = -1f, _f, _wantF;
        static float _tMin = float.MaxValue, _tMax = float.MinValue, _fpsMin = float.MaxValue, _fpsMax = float.MinValue;
        static long _lowered, _raised, _restored, _revived;
        static float _nextLog;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("DeadDisposeEarly",
                "VISIBLE TO PLAYERS: destroyed units (not structures) are disposed after DisposeEarlySeconds (default 5 s) instead of the unit's own " +
                "(15 s) while DisposeEarlyFpsLow = DisposeEarlyFpsHigh = 0 (default); with High > Low the time falls from 15 s at High to " +
                "DisposeEarlySeconds at Low (smoothed fps, slew-limited). false = vanilla 15 s",
                v => On = v, defaultOn: true);
            var fu = Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes);
            Item.Patch(fu, typeof(DeadDisposeEarly), postfix: nameof(FixedPost));
            Item.OnSwitch(on => { if (!on) RestoreAll("switch-off"); });
            ctx.OnUpdate(Tick, Item);
            ctx.OnSceneLoaded(_ => { RestoreAll("scene change"); });
        }

        static float Early => Mathf.Max(0.1f, S.DisposeEarlySeconds);

        static float TargetFor(float original, float early) => original - _f * (original - early);

        static bool KindOn(BodyKind k)
        {
            switch (k)
            {
                case BodyKind.Soldier: return S.DisposeEarlySoldiers;
                case BodyKind.Creature: return S.DisposeEarlyCreatures;
                case BodyKind.Wheeled:
                case BodyKind.Hover:
                case BodyKind.Air:
                case BodyKind.OtherVehicle: return S.DisposeEarlyVehicles;
                default: return false;
            }
        }

        static void FixedPost()
        {
            if (!On) return;
            try
            {
                float now = Time.fixedTime, early = Early;
                List<Unit> units = Unit.Units;
                if (units != null && units.Count > 0)
                {
                    int stride = Mathf.Max(1, S.DeadSettleScanStrideSteps);
                    int n = (units.Count + stride - 1) / stride;
                    for (int j = 0; j < n; j++)
                    {
                        if (_rr >= units.Count) _rr = 0;
                        Unit u = units[_rr++];
                        if (!u || !u.IsDestroyed || _set.Contains(u)) continue;
                        if (!KindOn(Kinds.Of(u))) continue;
                        float o = u.DestroyedDisposeTime;
                        _set.Add(u);
                        _list.Add(new Rec { U = u, SeenAt = now, Original = o, Applied = o, Valid = o > early });
                    }
                }
                bool purge = ++_purgeStep >= 25;
                if (purge) _purgeStep = 0;
                for (int i = _list.Count - 1; i >= 0; i--)
                {
                    Rec r = _list[i];
                    if (!r.U || (purge && !r.U.IsDestroyed))
                    {
                        if (r.U && r.Applied != r.Original) { r.U.DestroyedDisposeTime = r.Original; _revived++; }
                        if (r.Applied < r.Original) _shortNow--;
                        _set.Remove(r.U);
                        int last = _list.Count - 1;
                        _list[i] = _list[last];
                        _list.RemoveAt(last);
                        continue;
                    }
                    if (!r.Valid) continue;
                    float target = TargetFor(r.Original, early);
                    float val = r.Applied;
                    if (target > val + 0.05f)
                    {
                        val = Mathf.Min(target, r.Original);
                        _raised++;
                    }
                    else if (target < val - 0.05f)
                    {
                        float floor = now - r.SeenAt + 1f;              // never dispose a unit within 1 s of lowering it
                        float v2 = Mathf.Min(val, Mathf.Max(target, floor));
                        if (v2 >= val) continue;                        // already scheduled; its own age never pushes it back
                        val = v2;
                        _lowered++;
                    }
                    else continue;
                    bool wasShort = r.Applied < r.Original, isShort = val < r.Original;
                    if (wasShort != isShort) _shortNow += isShort ? 1 : -1;
                    r.Applied = val;
                    r.U.DestroyedDisposeTime = val;
                }
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "OrderedUpdateManager.FixedUpdate postfix");
            }
        }

        static void RestoreAll(string why)
        {
            int n = 0;
            for (int i = 0; i < _list.Count; i++)
            {
                Rec r = _list[i];
                if (r.Applied == r.Original) continue;
                try
                {
                    if (r.U) { r.U.DestroyedDisposeTime = r.Original; n++; }
                }
                catch (Exception ex) { Perf.Warn("Physics.DeadDisposeEarly: restore (" + why + "): " + ex.Message); }
            }
            _restored += n;
            _list.Clear();
            _set.Clear();
            _rr = 0;
            _shortNow = 0;
            _f = _wantF = 0f;
            _emaDt = -1f;
            if (Item != null) Item.Info = "last " + why + ": " + n + " dead units back to their original DestroyedDisposeTime";
        }

        // Per frame: smoothed fps, wanted fraction, slew-limited fraction.
        static void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                if (_emaDt < 0f) _emaDt = dt;
                else _emaDt += (dt - _emaDt) * Mathf.Clamp01(dt / Mathf.Max(0.1f, S.DisposeEarlyFpsSmoothSeconds));
            }
            float fps = _emaDt > 0f ? 1f / _emaDt : 0f;
            float lo = S.DisposeEarlyFpsLow, hi = S.DisposeEarlyFpsHigh;
            if (hi <= lo) _wantF = 1f;                                  // Low = High = 0: always DisposeEarlySeconds
            else _wantF = Mathf.Clamp01((hi - fps) / (hi - lo));
            float span = Mathf.Max(0.1f, VanillaRef - Early);
            float maxStep = Mathf.Max(0f, S.DisposeEarlySlewSecondsPerSecond) * Mathf.Min(dt, 0.5f) / span;
            _f = Mathf.MoveTowards(_f, _wantF, maxStep);

            float t15 = TargetFor(VanillaRef, Early);
            if (t15 < _tMin) _tMin = t15;
            if (t15 > _tMax) _tMax = t15;
            if (fps > 0f)
            {
                if (fps < _fpsMin) _fpsMin = fps;
                if (fps > _fpsMax) _fpsMax = fps;
            }

            var s = PhysicsModule.S;
            float now = Time.realtimeSinceStartup;
            if (s == null || s.CensusLogSeconds <= 0f || now < _nextLog) return;
            bool first = _nextLog == 0f;
            _nextLog = now + s.CensusLogSeconds;
            if (!first)
            {
                float wanted = VanillaRef - _wantF * (VanillaRef - Early);
                Perf.Log("[Physics] deaddispose target=" + t15.ToString("0.00") + "s targetMin=" + _tMin.ToString("0.00") +
                         "s targetMax=" + _tMax.ToString("0.00") + "s wanted=" + wanted.ToString("0.00") + "s fps=" + fps.ToString("0.0") +
                         " fpsMin=" + _fpsMin.ToString("0.0") + " fpsMax=" + _fpsMax.ToString("0.0") +
                         " low=" + lo.ToString("0.#") + " high=" + hi.ToString("0.#") + " lowered=" + _lowered + " raised=" + _raised +
                         " restored=" + _restored + " revived=" + _revived + " trackedNow=" + _list.Count + " shortNow=" + _shortNow);
                Item.Info = "target " + t15.ToString("0.0") + " s for a 15 s unit (wanted " + wanted.ToString("0.0") + " s) at smoothed fps " +
                            fps.ToString("0.0") + (hi > lo ? " (15 s at >= " + hi.ToString("0.#") + ", " + Early.ToString("0.#") + " s at <= " + lo.ToString("0.#") + ")" : " (always)") +
                            "; " + _shortNow + " dead units shortened now";
            }
            _lowered = _raised = _restored = _revived = 0;
            _tMin = _fpsMin = float.MaxValue;
            _tMax = _fpsMax = float.MinValue;
        }
    }
}
