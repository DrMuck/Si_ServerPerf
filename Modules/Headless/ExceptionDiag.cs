/*
 Diag_LogExceptions (ported from Si_ServerOptimizer 0.8.0 ExceptionLog). Hooks Application.logMessageReceived,
 counts every Unity exception (and optionally Unity warnings/errors mentioning "Animator") and writes the first N
 distinct messages of each round with a 4-line stack to the Melon log, so problems the patches might cause (or the
 0.7.1 OnInCompartmentAnimatorLateUpdate NRE) show up in MelonLoader\Latest.log. A summary line is written on every
 scene load (then the counters reset) and on quit, also when nothing was logged ("0 logged exceptions/warnings"), so
 a missing line in an unattended run means the hook did not run, not that there was nothing to report. The status
 block shows the running count of the current period (Info). A killed server gets no quit line (no OnApplicationQuit).
*/

using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace SiServerPerf.Modules.Headless
{
    internal static class ExceptionDiag
    {
        internal static bool On;
        static PerfItem Item;
        const int StackLines = 4;
        const int MaxKeyLength = 300;

        static bool _inHandler;
        static int _maxDistinct = 5;
        static bool _animatorWarnings = true;
        static readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        static int _total;
        static string _roundLabel = "startup";

        public static void Register(ModuleContext ctx, HeadlessSettings s)
        {
            Item = ctx.Item("Diag_LogExceptions",
                "Hook Application.logMessageReceived: count exceptions (and Animator warnings), log the first distinct ones of each round with a short stack",
                v => On = v);
            ServerOptimizerCompat.Apply(Item);
            _maxDistinct = Math.Max(0, s.DiagMaxDistinctPerRound);
            _animatorWarnings = s.DiagCountAnimatorWarnings;
            if (Item.Hooked("Application.logMessageReceived"))
            {
                Application.logMessageReceived += OnLog;
                // no owner: the summary line is written even while the item is switched off (and then says so)
                ctx.OnSceneLoaded(NewRound);
                ctx.OnQuit(() => Summarize("quit"));
                ctx.OnBeforeStatus(() => Item.Info = (On ? "" : "counting OFF | ") + "this period (" + _roundLabel + "): " +
                                                     _total + " logged exceptions/warnings, " + _counts.Count + " distinct");
            }
        }

        static void NewRound(string sceneName)
        {
            Summarize("scene " + sceneName + " loaded");
            _counts.Clear();
            _total = 0;
            _roundLabel = sceneName;
        }

        static void Summarize(string why)
        {
            string head = "[Diag] " + _roundLabel + " (" + why + "): " + _total + " logged exceptions/warnings, " + _counts.Count + " distinct";
            if (!On) head += " (counting was switched " + (Item.Failed ? "off: item FAILED" : "OFF") + ")";
            if (_total == 0)
            {
                MelonLogger.Msg(head);
                return;
            }
            string top = string.Join("; ", _counts.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Value + "x " + kv.Key));
            MelonLogger.Msg(head + ". Top: " + top);
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!On || _inHandler) return;
            bool isException = type == LogType.Exception || type == LogType.Assert;
            bool isAnimatorWarning = _animatorWarnings && (type == LogType.Warning || type == LogType.Error)
                && condition != null && condition.IndexOf("Animator", StringComparison.Ordinal) >= 0;
            if (!isException && !isAnimatorWarning) return;

            _inHandler = true;
            try
            {
                string key = FirstLine(condition);
                _total++;
                if (_counts.TryGetValue(key, out int n))
                {
                    _counts[key] = n + 1;
                    if (n + 1 == 100 || n + 1 == 10000) MelonLogger.Warning("[Diag] seen " + (n + 1) + "x this round: " + key);
                    return;
                }
                _counts[key] = 1;
                if (_counts.Count <= _maxDistinct)
                {
                    MelonLogger.Warning("[Diag] " + type + " #" + _counts.Count + " this round: " + key + ShortStack(stackTrace));
                }
                else if (_counts.Count == _maxDistinct + 1)
                {
                    MelonLogger.Warning("[Diag] more than " + _maxDistinct + " distinct exception messages this round; further ones are only counted (summary at next level load)");
                }
            }
            catch (Exception ex)
            {
                // never throw back into Unity's logger
                _inHandler = false;
                Item.Fail(ex, "log handler");
            }
            finally
            {
                _inHandler = false;
            }
        }

        static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "(empty)";
            string s = text;
            int nl = s.IndexOf('\n');
            if (nl >= 0) s = s.Substring(0, nl);
            s = s.Trim();
            return s.Length > MaxKeyLength ? s.Substring(0, MaxKeyLength) + "..." : s;
        }

        static string ShortStack(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "";
            string[] lines = stackTrace.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int take = Math.Min(StackLines, lines.Length);
            var parts = new List<string>(take);
            for (int i = 0; i < take; i++) parts.Add(lines[i].Trim());
            return "\n    at " + string.Join("\n    at ", parts);
        }
    }
}
