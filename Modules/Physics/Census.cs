/*
 Census (default ON, read-only): the baseline counters for every other Physics item.

 Every CensusIntervalSeconds (1 s) it walks Unit.Units and each unit's rigidbodies (cached per unit, rebuilt every
 30 s or when a body dies; only bodies whose nearest Unit parent is that unit, so seated soldiers are not counted
 twice) and counts per unit kind: dynamic bodies, dynamic asleep, kinematic bodies, kinematic awake (hit-zone bodies
 are the kinematic ones on soldiers/creatures), interpolated bodies.

 Wake window (once per sample, 2 fixed steps): prefix + postfix on OrderedUpdateManager.FixedUpdate (the MonoBehaviour
 message that runs the whole ordered fixed list right before PhysX simulates). Every dynamic body's IsSleeping() is
 read at the end of the fixed scripts of step n, at the start of step n+1 and at its end, giving per kind:
   wS  woke between the end of step n's scripts and step n+1  = PhysX simulate (contacts, transform sync of written
       transforms) + every Update/LateUpdate frame in between (per-frame writers such as HumanModel.rotation)
   sS  fell asleep in that gap (PhysX auto-sleep)
   wF  woke inside the fixed scripts of step n+1 (velocity/force writers, explicit WakeUp, avoidance impulses)
   sF  put to sleep inside the fixed scripts (Soldier/VehicleWheeled own Sleep() calls, IdleForceSleep)
 The same patch counts fixed steps per second and frames that ran 2 or 3+ fixed steps (shows CatchUpCap working).

 Log line (every CensusLogSeconds, key=value for parsing):
   [Physics] census t=.. units=.. dyn=.. dynAsleep=.. kin=.. kinAwake=.. interp=.. | soldier u= dyn= sl= kin= kAw= int=
   wS= sS= wF= sF= | ... | win=n steps/s=.. f2=.. f3=.. maxStepsPerFrame=.. | items ... | dead deadU= .. (DeadCensus.cs) |
   fixedDt= maxDt=
 Window counts and item counters are sums over the log interval; body counts are the last sample.
 The "dead" segment splits units and dynamic bodies into destroyed (waiting for disposal) and alive, see DeadCensus.cs.
*/

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SiServerPerf.Modules.Physics
{
    internal static class Census
    {
        internal static bool On;
        static PerfItem Item;
        static PhysicsSettings S;

        // ---- per-unit body cache (also used by IdleForceSleep for vehicles' child bodies)
        sealed class Entry
        {
            public Rigidbody[] Bodies;
            public float Built;
        }

        static readonly Dictionary<Unit, Entry> _cache = new Dictionary<Unit, Entry>();
        static readonly List<Rigidbody> _tmp = new List<Rigidbody>();
        static readonly List<Unit> _dead = new List<Unit>();
        static float _nextPurge;

        // Rigidbodies owned by this unit (realtime clock for the 30 s rebuild; purges dead units every 30 s).
        internal static Rigidbody[] BodiesOf(Unit u)
        {
            float now = Time.realtimeSinceStartup;
            if (now >= _nextPurge)
            {
                _nextPurge = now + 30f;
                Purge();
            }
            if (!_cache.TryGetValue(u, out Entry e) || now - e.Built > 30f || AnyDead(e.Bodies))
            {
                if (e == null)
                {
                    e = new Entry();
                    _cache[u] = e;
                }
                _tmp.Clear();
                u.GetComponentsInChildren(true, _tmp);
                int n = 0;
                for (int i = 0; i < _tmp.Count; i++)
                {
                    Rigidbody rb = _tmp[i];
                    if (rb && rb.GetComponentInParent<Unit>(true) == u) _tmp[n++] = rb;
                }
                var arr = new Rigidbody[n];
                for (int i = 0; i < n; i++) arr[i] = _tmp[i];
                _tmp.Clear();
                e.Bodies = arr;
                e.Built = now;
            }
            return e.Bodies;
        }

        static bool AnyDead(Rigidbody[] a)
        {
            for (int i = 0; i < a.Length; i++) if (!a[i]) return true;
            return false;
        }

        static void Purge()
        {
            _dead.Clear();
            foreach (var kv in _cache) if (!kv.Key) _dead.Add(kv.Key);
            for (int i = 0; i < _dead.Count; i++) _cache.Remove(_dead[i]);
            _dead.Clear();
        }

        // ---- last sample
        static readonly int[] _units = new int[Kinds.Count], _dyn = new int[Kinds.Count], _dynAsleep = new int[Kinds.Count],
                              _kin = new int[Kinds.Count], _kinAwake = new int[Kinds.Count], _interp = new int[Kinds.Count];

        // ---- wake window (summed over the log interval)
        static readonly int[] _wS = new int[Kinds.Count], _sS = new int[Kinds.Count], _wF = new int[Kinds.Count], _sF = new int[Kinds.Count];
        static int _windows;
        static readonly List<Rigidbody> _snap = new List<Rigidbody>();
        static readonly List<byte> _snapKind = new List<byte>();
        static byte[] _state = new byte[256];   // 0 awake, 1 asleep, 255 gone
        static int _phase;                       // 0 idle, 1 armed, 2 end of step n recorded, 3 start of step n+1 recorded

        // ---- step counting
        static int _steps, _lastStepFrame = -1, _stepsThisFrame, _frames2, _frames3, _maxPerFrame;

        static float _nextSample, _nextLog, _lastLogTime;
        static long _gS, _gW, _pS, _pW, _aC, _aS, _fS, _rS, _fR, _fB;

        public static void Register(ModuleContext ctx, PhysicsSettings s)
        {
            S = s;
            Item = ctx.Item("Census",
                "Read-only: 1 Hz rigidbody census per unit kind (awake/asleep/kinematic/interpolated) + 2-step wake window " +
                "(woke in simulate+frames vs in fixed scripts) + fixed steps per frame; log line every CensusLogSeconds",
                v => On = v, defaultOn: true);
            var fu = Item.Target(typeof(OrderedUpdateManager), "FixedUpdate", Type.EmptyTypes);
            Item.Patch(fu, typeof(Census), prefix: nameof(FixedPre), postfix: nameof(FixedPost));
            ctx.OnUpdate(Tick, Item);
            ctx.OnSceneLoaded(_ => { _cache.Clear(); _phase = 0; });
        }

        static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            if (now >= _nextSample)
            {
                _nextSample = now + Mathf.Max(0.1f, S.CensusIntervalSeconds);
                Sample(now);
            }
            if (S.CensusLogSeconds > 0f && now >= _nextLog)
            {
                if (_lastLogTime > 0f) WriteLog(now);
                _nextLog = now + S.CensusLogSeconds;
                _lastLogTime = now;
                ResetInterval();
            }
        }

        static void Sample(float now)
        {
            Array.Clear(_units, 0, Kinds.Count);
            Array.Clear(_dyn, 0, Kinds.Count);
            Array.Clear(_dynAsleep, 0, Kinds.Count);
            Array.Clear(_kin, 0, Kinds.Count);
            Array.Clear(_kinAwake, 0, Kinds.Count);
            Array.Clear(_interp, 0, Kinds.Count);
            _snap.Clear();
            _snapKind.Clear();
            DeadCensus.Reset();
            List<Unit> units = Unit.Units;
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    Unit u = units[i];
                    if (!u) continue;
                    int k = (int)Kinds.Of(u);
                    _units[k]++;
                    bool dead = u.IsDestroyed;
                    DeadCensus.Unit(k, dead);
                    Rigidbody[] bodies = BodiesOf(u);
                    for (int b = 0; b < bodies.Length; b++)
                    {
                        Rigidbody rb = bodies[b];
                        if (!rb) continue;
                        bool asleep = rb.IsSleeping();
                        DeadCensus.Body(k, dead, rb.isKinematic, asleep);
                        if (rb.isKinematic)
                        {
                            _kin[k]++;
                            if (!asleep) _kinAwake[k]++;
                        }
                        else
                        {
                            _dyn[k]++;
                            if (asleep) _dynAsleep[k]++;
                            _snap.Add(rb);
                            _snapKind.Add((byte)k);
                        }
                        if (rb.interpolation != RigidbodyInterpolation.None) _interp[k]++;
                    }
                }
            }
            if (_state.Length < _snap.Count) _state = new byte[_snap.Count * 2];
            _phase = _snap.Count > 0 ? 1 : 0;

            int d = 0, da = 0, kn = 0, ka = 0, ip = 0;
            for (int k = 0; k < Kinds.Count; k++) { d += _dyn[k]; da += _dynAsleep[k]; kn += _kin[k]; ka += _kinAwake[k]; ip += _interp[k]; }
            Item.Info = "dyn asleep " + (d > 0 ? (100 * da / d) : 0) + "% (" + da + "/" + d + "), kinematic awake " + ka + "/" + kn +
                        ", interpolated " + ip + ", max steps/frame " + _maxPerFrame + ", " + DeadCensus.InfoText() +
                        (Counters.ForcedNow > 0 ? ", force-slept now " + Counters.ForcedNow : "");
        }

        // OrderedUpdateManager.FixedUpdate prefix: start of the ordered fixed list of a step.
        static void FixedPre()
        {
            if (!On) return;
            try
            {
                int f = Time.frameCount;
                if (f == _lastStepFrame)
                {
                    _stepsThisFrame++;
                }
                else
                {
                    _lastStepFrame = f;
                    _stepsThisFrame = 1;
                }
                _steps++;
                if (_stepsThisFrame == 2) _frames2++;
                if (_stepsThisFrame == 3) _frames3++;
                if (_stepsThisFrame > _maxPerFrame) _maxPerFrame = _stepsThisFrame;

                if (_phase != 2) return;
                for (int i = 0; i < _snap.Count; i++)
                {
                    Rigidbody rb = _snap[i];
                    if (_state[i] == 255 || !rb) { _state[i] = 255; continue; }
                    bool asleep = rb.IsSleeping();
                    int k = _snapKind[i];
                    if (_state[i] == 1 && !asleep) _wS[k]++;
                    else if (_state[i] == 0 && asleep) _sS[k]++;
                    _state[i] = asleep ? (byte)1 : (byte)0;
                }
                _phase = 3;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "OrderedUpdateManager.FixedUpdate prefix");
            }
        }

        // OrderedUpdateManager.FixedUpdate postfix: all ordered fixed updates of this step done, PhysX simulates next.
        static void FixedPost()
        {
            if (!On || _phase == 0 || _phase == 2) return;
            try
            {
                if (_phase == 1)
                {
                    for (int i = 0; i < _snap.Count; i++)
                    {
                        Rigidbody rb = _snap[i];
                        _state[i] = !rb ? (byte)255 : rb.IsSleeping() ? (byte)1 : (byte)0;
                    }
                    _phase = 2;
                    return;
                }
                for (int i = 0; i < _snap.Count; i++)
                {
                    Rigidbody rb = _snap[i];
                    if (_state[i] == 255 || !rb) continue;
                    bool asleep = rb.IsSleeping();
                    int k = _snapKind[i];
                    if (_state[i] == 1 && !asleep) _wF[k]++;
                    else if (_state[i] == 0 && asleep) _sF[k]++;
                }
                _windows++;
                _phase = 0;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "OrderedUpdateManager.FixedUpdate postfix");
            }
        }

        static void ResetInterval()
        {
            Array.Clear(_wS, 0, Kinds.Count);
            Array.Clear(_sS, 0, Kinds.Count);
            Array.Clear(_wF, 0, Kinds.Count);
            Array.Clear(_sF, 0, Kinds.Count);
            _windows = 0;
            _steps = 0;
            _frames2 = 0;
            _frames3 = 0;
            _maxPerFrame = 0;
            _gS = Counters.GuardSkips; _gW = Counters.GuardWrites;
            _pS = Counters.PoseSkips; _pW = Counters.PoseWrites;
            _aC = Counters.AvoidCalls; _aS = Counters.AvoidSkips;
            _fS = Counters.ForcedSleeps; _rS = Counters.ReSleeps; _fR = Counters.ForcedReleased; _fB = Counters.SleepBlockedByForce;
        }

        static void WriteLog(float now)
        {
            float secs = Mathf.Max(0.001f, now - _lastLogTime);
            int units = 0, d = 0, da = 0, kn = 0, ka = 0, ip = 0;
            for (int k = 0; k < Kinds.Count; k++) { units += _units[k]; d += _dyn[k]; da += _dynAsleep[k]; kn += _kin[k]; ka += _kinAwake[k]; ip += _interp[k]; }
            var sb = new StringBuilder(512);
            sb.Append("census t=").Append(Time.time.ToString("0")).Append(" units=").Append(units).Append(" dyn=").Append(d)
              .Append(" dynAsleep=").Append(da).Append(" kin=").Append(kn).Append(" kinAwake=").Append(ka).Append(" interp=").Append(ip);
            for (int k = 0; k < Kinds.Count; k++)
            {
                if (_units[k] == 0) continue;
                sb.Append(" | ").Append(Kinds.Names[k]).Append(" u=").Append(_units[k]).Append(" dyn=").Append(_dyn[k])
                  .Append(" sl=").Append(_dynAsleep[k]).Append(" kin=").Append(_kin[k]).Append(" kAw=").Append(_kinAwake[k])
                  .Append(" int=").Append(_interp[k]).Append(" wS=").Append(_wS[k]).Append(" sS=").Append(_sS[k])
                  .Append(" wF=").Append(_wF[k]).Append(" sF=").Append(_sF[k]);
            }
            sb.Append(" | win=").Append(_windows).Append(" steps/s=").Append((_steps / secs).ToString("0.0"))
              .Append(" f2=").Append(_frames2).Append(" f3=").Append(_frames3).Append(" maxStepsPerFrame=").Append(_maxPerFrame);
            sb.Append(" | guardSkip=").Append(Counters.GuardSkips - _gS).Append(" guardWrite=").Append(Counters.GuardWrites - _gW)
              .Append(" poseSkip=").Append(Counters.PoseSkips - _pS).Append(" poseWrite=").Append(Counters.PoseWrites - _pW)
              .Append(" avoidCalls=").Append(Counters.AvoidCalls - _aC).Append(" avoidSkip=").Append(Counters.AvoidSkips - _aS)
              .Append(" forcedSleep=").Append(Counters.ForcedSleeps - _fS).Append(" reSleep=").Append(Counters.ReSleeps - _rS)
              .Append(" forcedReleased=").Append(Counters.ForcedReleased - _fR).Append(" sleepBlockedByForce=").Append(Counters.SleepBlockedByForce - _fB)
              .Append(" forcedNow=").Append(Counters.ForcedNow);
            DeadCensus.Append(sb);
            sb.Append(" | fixedDt=").Append(Time.fixedDeltaTime.ToString("0.####")).Append(" maxDt=").Append(Time.maximumDeltaTime.ToString("0.####"));
            Perf.Log("[Physics] " + sb);
        }
    }
}
