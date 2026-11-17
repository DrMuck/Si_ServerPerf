/*
 Si_ServerPerf - runtime switches without a client: UserData\cfg\Si_ServerPerf\runtime.json, polled about once a second.

   {
     "seq": 7,                                        optional, echoed in runtime_state.json
     "items": {                                       applied in file order
       "*": true,                                     every item
       "Headless.*": false,                           every item of a module
       "Headless.Fog_SkipPresentation": true          one item (a bare item key works if it is unique)
     },
     "dump": true                                     optional: write the status block of the window that is
   }                                                  closing to the log BEFORE these switches apply (one
                                                      record per window; the last window gets one at quit)
 Top-level "<id>": true/false entries are accepted as well. The file is re-applied whenever its content changes
 (compared as text, so copying two alternating files with old timestamps works). Writers should replace the file
 atomically (write tmp, move). After each applied change runtime_state.json gets the current switch states.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using Newtonsoft.Json.Linq;

namespace SiServerPerf
{
    internal static class RuntimeControl
    {
        static string _lastText;
        static float _nextPoll;
        static int _readErrors;
        public static string LastSeq;

        public static void Startup()
        {
            if (!PerfConfig.Core.RuntimeFile) return;
            string text = Read();
            if (text == null) return;
            _lastText = text;
            if (PerfConfig.Core.ApplyRuntimeFileAtStartup)
            {
                MelonLogger.Msg("runtime.json present at start, applying it: " + OneLine(text));
                Apply(text, atStartup: true, source: "runtime.json (startup)");
            }
            else
            {
                MelonLogger.Msg("runtime.json present at start, ignored until it changes (Core.ApplyRuntimeFileAtStartup = false)");
            }
        }

        public static void Poll()
        {
            if (!PerfConfig.Core.RuntimeFile) return;
            float now = Perf.RealTime;
            if (now < _nextPoll) return;
            _nextPoll = now + Math.Max(0.1f, PerfConfig.Core.RuntimePollSeconds);
            string text = Read();
            if (text == null || text == _lastText) return;
            _lastText = text;
            Apply(text, atStartup: false, source: "runtime.json");
        }

        static string Read()
        {
            try
            {
                if (!File.Exists(PerfConfig.RuntimePath)) return null;
                string t = File.ReadAllText(PerfConfig.RuntimePath);
                _readErrors = 0;
                return t;
            }
            catch (Exception ex)
            {
                // being replaced right now: try again next poll; complain only if it persists
                if (++_readErrors == 10) MelonLogger.Warning("cannot read " + PerfConfig.RuntimePath + ": " + ex.Message);
                return null;
            }
        }

        public static void Apply(string text, bool atStartup, string source)
        {
            JObject o;
            try { o = JObject.Parse(text); }
            catch (Exception ex)
            {
                MelonLogger.Warning(source + ": does not parse (" + ex.Message + "), ignored");
                return;
            }

            var requests = new List<KeyValuePair<string, bool>>();
            bool dump = false;
            string prevSeq = LastSeq;
            string newSeq = o["seq"] != null ? o["seq"].ToString() : null;
            foreach (var p in o.Properties())
            {
                if (p.Name == "items" && p.Value is JObject items)
                {
                    foreach (var ip in items.Properties())
                    {
                        if (ip.Value.Type == JTokenType.Boolean) requests.Add(new KeyValuePair<string, bool>(ip.Name, (bool)ip.Value));
                    }
                }
                else if (p.Name == "dump")
                {
                    dump = p.Value.Type == JTokenType.Boolean ? (bool)p.Value : p.Value.Type != JTokenType.Null;
                }
                else if (p.Name != "seq" && !p.Name.StartsWith("_", StringComparison.Ordinal) && p.Value.Type == JTokenType.Boolean)
                {
                    requests.Add(new KeyValuePair<string, bool>(p.Name, (bool)p.Value));
                }
            }

            // One record per window: the status block is written BEFORE the new switches apply, so its states and
            // Info counters all belong to the window that is closing now. At startup no block here: the startup
            // report right after shows the state runtime.json established (window 0).
            if (dump && !atStartup)
            {
                DumpRequested = true;
                Registry.LogReport(WindowEndTitle("before " + source + (newSeq != null ? " seq " + newSeq : "") + " applies"));
            }

            LastSeq = newSeq;
            if (!atStartup) Window++;
            var changed = new List<string>();
            var refused = new List<string>();
            foreach (var r in requests)
            {
                foreach (string line in Set(r.Key, r.Value, atStartup))
                {
                    if (line.EndsWith(": on", StringComparison.Ordinal) || line.EndsWith(": off", StringComparison.Ordinal)) changed.Add(line);
                    else if (!line.Contains("already")) refused.Add(line);
                }
            }
            MelonLogger.Msg(source + (LastSeq != null ? " seq " + LastSeq : "") + ": " +
                            (changed.Count > 0 ? string.Join(", ", changed) : "no change") +
                            (refused.Count > 0 ? " | refused: " + string.Join(", ", refused) : "") +
                            " | window " + Window + " starts" + (atStartup ? "" : " (window " + (Window - 1) + " had seq " + (prevSeq ?? "none") + ")"));
            WriteState();
            if (atStartup && dump) DumpRequested = true;
        }

        // Window n = the time between two applied runtime.json changes (0 = from startup to the first change).
        public static int Window;
        // Set once any runtime.json asked for "dump"; then the last window also gets its block at quit.
        public static bool DumpRequested;

        static string WindowEndTitle(string why) =>
            "window " + Window + " ENDED (seq " + (LastSeq ?? "none") + ", " + why + ") - states and counters below belong to the window that just ended";

        // OnApplicationQuit: close the last window so every window has exactly one record.
        public static void Quit()
        {
            if (DumpRequested) Registry.LogReport(WindowEndTitle("server quit"));
        }

        // Switch every item matching pattern; one result line per item ("<id>: on|off|<reason>").
        public static List<string> Set(string pattern, bool on, bool atStartup)
        {
            var lines = new List<string>();
            var items = Registry.Match(pattern);
            if (items.Count == 0)
            {
                lines.Add(pattern + ": no such item");
                return lines;
            }
            bool wildcard = pattern.Contains("*") || pattern.Equals("all", StringComparison.OrdinalIgnoreCase);
            foreach (var it in items)
            {
                // wildcards silently skip items that were never patched (off in config, skipped, absent)
                if (wildcard && !it.Applied) continue;
                lines.Add(it.Id + ": " + it.SetRuntime(on, atStartup));
            }
            return lines;
        }

        public static void WriteState()
        {
            if (!PerfConfig.Core.WriteRuntimeState) return;
            try
            {
                string tmp = PerfConfig.StatePath + ".tmp";
                File.WriteAllText(tmp, Registry.StateJson(), new UTF8Encoding(false));
                if (File.Exists(PerfConfig.StatePath)) File.Delete(PerfConfig.StatePath);
                File.Move(tmp, PerfConfig.StatePath);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("cannot write " + PerfConfig.StatePath + ": " + ex.Message);
            }
        }

        static string OneLine(string s)
        {
            s = s.Replace("\r", " ").Replace("\n", " ");
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s.Length > 400 ? s.Substring(0, 400) + "..." : s;
        }
    }
}
