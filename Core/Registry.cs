/*
 Si_ServerPerf - all modules and items, lookup by id/pattern, startup report and status dump.
*/

using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;

namespace SiServerPerf
{
    internal static class Registry
    {
        public static readonly List<ModuleContext> Modules = new List<ModuleContext>();
        public static readonly List<PerfItem> Items = new List<PerfItem>();
        static readonly Dictionary<string, PerfItem> _byId = new Dictionary<string, PerfItem>(StringComparer.OrdinalIgnoreCase);

        public static void Add(PerfItem item)
        {
            Items.Add(item);
            _byId[item.Id] = item;
        }

        public static PerfItem Get(string id) => id != null && _byId.TryGetValue(id, out var it) ? it : null;

        // "Module.Item" (exact, case-insensitive), "Module.*", "*", or a bare item key if it is unique.
        public static List<PerfItem> Match(string pattern)
        {
            var result = new List<PerfItem>();
            if (string.IsNullOrEmpty(pattern)) return result;
            pattern = pattern.Trim();
            if (pattern == "*" || pattern.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(Items);
                return result;
            }
            if (pattern.EndsWith(".*", StringComparison.Ordinal))
            {
                string mod = pattern.Substring(0, pattern.Length - 2);
                foreach (var it in Items) if (it.Module.Equals(mod, StringComparison.OrdinalIgnoreCase)) result.Add(it);
                return result;
            }
            var exact = Get(pattern);
            if (exact != null)
            {
                result.Add(exact);
                return result;
            }
            foreach (var it in Items) if (it.Key.Equals(pattern, StringComparison.OrdinalIgnoreCase)) result.Add(it);
            if (result.Count > 1) result.Clear(); // ambiguous bare key
            return result;
        }

        public static List<string> ReportLines(string title)
        {
            var lines = new List<string>();
            lines.Add("==== Si_ServerPerf " + ServerPerfMod.Version + " " + title + " ====");
            lines.Add("headless=" + Env.Headless + " (" + Env.HeadlessReason + "), RequireHeadless=" + PerfConfig.Core.RequireHeadless +
                      " | " + Env.ServerOptimizerLine + " | frame " + Perf.Frame + ", round " + Perf.RoundIndex + (Perf.RoundActive ? " (active)" : "") +
                      " | config " + PerfConfig.FilePath);
            foreach (var m in Modules)
            {
                int on = 0;
                foreach (var it in m.Items) if (it.On) on++;
                string state = m.Failed ? "FAILED (" + m.FailInfo + ")" : m.Enabled ? "enabled" : "DISABLED in config";
                lines.Add("module " + m.Name + ": " + state + ", " + m.Items.Count + " items, " + on + " on - " + m.Module.Description);
                foreach (string n in m.Notes) lines.Add("  note: " + n);
                foreach (var it in m.Items) lines.Add("  " + it.Line());
            }
            lines.Add("columns: id, state, members patched/targeted, kind. runtime: " + PerfConfig.RuntimePath +
                      " (poll " + (PerfConfig.Core.RuntimeFile ? PerfConfig.Core.RuntimePollSeconds + " s" : "off") + "), console/chat: perf status | perf on|off <id|Module.*|*>");
            lines.Add("==== end Si_ServerPerf " + title + " ====");
            return lines;
        }

        public static void LogReport(string title)
        {
            // let modules refresh their Info counters first (ctx.OnBeforeStatus), so the block is current
            Lifecycle.Run(Lifecycle.BeforeStatus);
            foreach (string l in ReportLines(title)) MelonLogger.Msg(l);
        }

        public static string StateJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"version\": \"").Append(ServerPerfMod.Version).Append("\",\n");
            sb.Append("  \"utc\": \"").Append(DateTime.UtcNow.ToString("o")).Append("\",\n");
            sb.Append("  \"frame\": ").Append(Perf.Frame).Append(",\n");
            sb.Append("  \"seq\": ").Append(RuntimeControl.LastSeq == null ? "null" : Newtonsoft.Json.JsonConvert.ToString(RuntimeControl.LastSeq)).Append(",\n");
            sb.Append("  \"window\": ").Append(RuntimeControl.Window).Append(",\n");
            sb.Append("  \"items\": {");
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    \"").Append(it.Id).Append("\": { \"on\": ").Append(it.On ? "true" : "false")
                  .Append(", \"state\": ").Append(Newtonsoft.Json.JsonConvert.ToString(it.State))
                  .Append(", \"patched\": ").Append(it.PatchedCount).Append(", \"targeted\": ").Append(it.TargetCount)
                  .Append(", \"startupOnly\": ").Append(it.StartupOnly ? "true" : "false")
                  .Append(", \"changes\": ").Append(it.RuntimeChanges);
                if (!string.IsNullOrEmpty(it.Info)) sb.Append(", \"info\": ").Append(Newtonsoft.Json.JsonConvert.ToString(it.Info));
                sb.Append(" }");
            }
            sb.Append("\n  }\n}\n");
            return sb.ToString();
        }
    }
}
