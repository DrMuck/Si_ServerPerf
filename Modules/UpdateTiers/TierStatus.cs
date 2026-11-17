/*
 Per-frame clock for the idle tests and once-per-second counters in each item's status Info (shown by `perf status`
 and in runtime_state.json), so the rig can confirm an item is doing something:
   Tier_*            "calls/s run R (due) + E (early: not quiet) | skipped S (P% of tiered calls) | classified C"
   Register_O1       "reg/s, unreg/s, dup, miss, rebuilds, verified, ms/s spent"
   FixedLate_StepDt  "early fixed-late runs/s" (= extra physics steps per second that now get their own call)
 Also runs the once-per-second maintenance (wake registry, agent table).
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SiServerPerf.Modules.UpdateTiers
{
    internal static class TierStatus
    {
        static float s_Next;
        static float s_LastT;
        static long s_LastReg, s_LastUnreg, s_LastTicks, s_LastEarly, s_LastEvents, s_LastWakes;

        public static void Register(ModuleContext ctx)
        {
            ctx.OnUpdate(Tick);
        }

        static bool s_SecondFailed;

        static void Tick()
        {
            Idle.Now = Time.time;
            if (s_SecondFailed) return;
            try
            {
                Second();
            }
            catch (Exception ex)
            {
                // keep the clock running (the idle tests need it); stop the counters / maintenance and say why
                s_SecondFailed = true;
                Perf.Warn("UpdateTiers: status/maintenance tick threw, counters frozen from now on: " + ex);
                // the detection wake-up is no longer maintained: the unit tiers go off (fail-open)
                TierDispatch.FailTier(TierDispatch.T_IdleSoldiers, ex, "maintenance tick");
                IdleAgents.FailItem(ex, "maintenance tick");
            }
        }

        static void Second()
        {
            float rt = Time.realtimeSinceStartup;
            if (rt < s_Next) return;
            float span = s_LastT > 0f ? rt - s_LastT : 1f;
            if (span <= 0f) span = 1f;
            s_LastT = rt;
            s_Next = rt + 1f;

            WakeRegistry.Maintain();
            IdleAgents.Prune();

            double vin = Idle.ViewInputN > 0 ? Idle.ViewInputSum / Idle.ViewInputN : 0.0;
            foreach (Tier t in TierDispatch.Tiers)
            {
                Report(t, span, ReferenceEquals(t, TierDispatch.Tiers[TierDispatch.T_IdleSoldiers])
                    ? "viewInput limit " + Idle.MaxViewInput.ToString("0.###") + " deg/call, mean |input| of rejected " + vin.ToString("0.###") + " (n " + Idle.ViewInputN + ")"
                    : null);
            }
            Report(IdleAgents.T, span, "agents tracked " + IdleAgents.Tracked + ", wake: " + WakeRegistry.State +
                   ", detections/s " + Rate(WakeRegistry.Events, ref s_LastEvents, span) + ", wakes/s " + Rate(WakeRegistry.Wakes, ref s_LastWakes, span));

            PerfItem reg = RegisterIndex.Item;
            if (reg != null && reg.Applied)
            {
                long ticks = RegisterIndex.Ticks;
                double ms = (ticks - s_LastTicks) * 1000.0 / Stopwatch.Frequency / span;
                s_LastTicks = ticks;
                reg.Info = "reg/s " + Rate(RegisterIndex.Registers, ref s_LastReg, span) + ", unreg/s " + Rate(RegisterIndex.Unregisters, ref s_LastUnreg, span) +
                           ", " + ms.ToString("0.00") + " ms/s | total dup " + RegisterIndex.Dups + ", miss " + RegisterIndex.Misses +
                           ", rebuilds " + RegisterIndex.Rebuilds + " (out-of-step " + RegisterIndex.OutOfStep + "), verified " + RegisterIndex.Verified +
                           " (0 mismatches, else this item is FAILED) | dup = Register of an already registered delegate (vanilla returns too);" +
                           " miss = Unregister of a delegate not in the list (vanilla scans and finds nothing; BaseGameObject.OnDestroy unregisters all 4 hooks," +
                           " OnDisable+OnDestroy unregister twice); rebuilds = index built from the list: first use per list (new OrderedUpdateManager per scene)" +
                           " and after every switch-on; out-of-step = rebuilt because the list changed outside Register/Unregister/Run (expected 0)";
            }
            PerfItem fl = FixedLateDt.Item;
            if (fl != null && fl.Applied)
            {
                fl.Info = "early fixed-late runs/s " + Rate(FixedLateDt.EarlyRuns, ref s_LastEarly, span) + " (total " + FixedLateDt.EarlyRuns + ")";
            }
        }

        // "early" runs split by the first quiet condition that failed: per second, and since start (top 6 each)
        static string WhyText(Tier t, float span)
        {
            var now = new List<KeyValuePair<string, long>>();
            var tot = new List<KeyValuePair<string, long>>();
            for (int i = 0; i < t.Why.Length; i++)
            {
                long d = t.Why[i] - t.LastWhy[i];
                t.LastWhy[i] = t.Why[i];
                if (d > 0) now.Add(new KeyValuePair<string, long>(Idle.WhyNames[i], (long)Math.Round(d / span)));
                if (t.Why[i] > 0) tot.Add(new KeyValuePair<string, long>(Idle.WhyNames[i], t.Why[i]));
            }
            if (tot.Count == 0) return "";
            return " | early by cause /s: " + Top(now) + " | since start: " + Top(tot);
        }

        static string Top(List<KeyValuePair<string, long>> l)
        {
            if (l.Count == 0) return "-";
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < l.Count && i < 6; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(l[i].Key).Append(' ').Append(l[i].Value);
            }
            return sb.ToString();
        }

        static string Rate(long total, ref long last, float span)
        {
            long d = total - last;
            last = total;
            return ((long)Math.Round(d / span)).ToString();
        }

        static void Report(Tier t, float span, string extra)
        {
            if (t?.Item == null || !t.Item.Applied) return;
            long r = t.Runs - t.LastRuns, b = t.Busy - t.LastBusy, s = t.Skips - t.LastSkips;
            t.LastRuns = t.Runs;
            t.LastBusy = t.Busy;
            t.LastSkips = t.Skips;
            long all = r + b + s;
            string pct = all > 0 ? (100.0 * s / all).ToString("0") : "0";
            t.Item.Info = "calls/s run " + (long)Math.Round(r / span) + " + early " + (long)Math.Round(b / span) +
                          " | skipped " + (long)Math.Round(s / span) + " (" + pct + "%) | interval " + t.Interval +
                          ", classified " + t.Classified + WhyText(t, span) + (extra != null ? " | " + extra : "");
        }
    }
}
