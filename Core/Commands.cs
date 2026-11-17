/*
 Si_ServerPerf - "perf" command: server console (DebugConsole, also reachable remotely by an admin client through
 the game's remote console) and Admin Mod chat "!perf" when Admin Mod is loaded.

   perf | perf status        status block to the log (+ short reply)
   perf on  <pattern>        switch items on   (pattern: Module.Item | Module.* | * | bare item key)
   perf off <pattern>        switch items off
   perf state                rewrite runtime_state.json
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using DebugTools;
using MelonLoader;

namespace SiServerPerf
{
    internal static class Commands
    {
        public static string Handle(string args, out List<string> detail)
        {
            detail = new List<string>();
            string[] t = (args ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int i = 0;
            if (t.Length > 0 && (t[0].Equals("perf", StringComparison.OrdinalIgnoreCase) || t[0].Equals("!perf", StringComparison.OrdinalIgnoreCase))) i = 1;
            string sub = t.Length > i ? t[i].ToLowerInvariant() : "status";
            string rest = t.Length > i + 1 ? t[i + 1] : "";
            switch (sub)
            {
                case "on":
                case "off":
                {
                    if (rest.Length == 0) return "usage: perf " + sub + " <Module.Item|Module.*|*>";
                    detail = RuntimeControl.Set(rest, sub == "on", atStartup: false);
                    MelonLogger.Msg("perf " + sub + " " + rest + ": " + string.Join(", ", detail));
                    RuntimeControl.WriteState();
                    return string.Join(", ", detail);
                }
                case "state":
                    RuntimeControl.WriteState();
                    return "wrote " + PerfConfig.StatePath;
                default:
                {
                    Registry.LogReport("status (command)");
                    int on = 0, applied = 0, failed = 0;
                    foreach (var it in Registry.Items)
                    {
                        if (it.On) on++;
                        if (it.Applied) applied++;
                        if (it.Failed) failed++;
                    }
                    return Registry.Items.Count + " items, " + applied + " patched, " + on + " on, " + failed + " failed; full list in the server log";
                }
            }
        }

        // ------------------------------------------------------------------ server console

        public static void RegisterConsole()
        {
            try
            {
                FieldInfo f = typeof(DebugConsole).GetField("s_Commands", BindingFlags.NonPublic | BindingFlags.Static);
                if (!(f?.GetValue(null) is Dictionary<string, DebugConsole.ICommand> cmds))
                {
                    MelonLogger.Warning("console command not registered: DebugConsole.s_Commands not found");
                    return;
                }
                var cmd = new PerfConsoleCommand();
                cmds[cmd.Key.ToLowerInvariant()] = cmd;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("console command not registered: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ Admin Mod chat

        public static void RegisterChat()
        {
            if (!Env.FindMelon("Admin Mod", out _, out _))
            {
                return;
            }
            try
            {
                AdminBridge.Register();
            }
            catch (Exception ex)
            {
                // Admin Mod present but its API differs: chat command off, everything else unaffected
                MelonLogger.Warning("chat command !perf not registered: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    public sealed class PerfConsoleCommand : DebugConsole.ICommand
    {
        public string Key => "perf";
        public string Description => "Si_ServerPerf: perf status | perf on|off <Module.Item|Module.*|*>";
        public EAdminLevel RequiredAdminLevel => EAdminLevel.ROOT;
        public bool ServerSide => true;

        public void Execute(params string[] parameters)
        {
            try
            {
                string reply = Commands.Handle(parameters == null ? "" : string.Join(" ", parameters), out _);
                DebugConsole.Log("perf: " + reply, DebugConsole.LogLevel.Log);
            }
            catch (Exception ex)
            {
                MelonLogger.Error("perf command: " + ex);
            }
        }
    }

    // Separate class so that Si_AdminMod types are only loaded when Admin Mod is present.
    internal static class AdminBridge
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register()
        {
            SilicaAdminMod.AdminMethods.RegisterAdminCommand("perf", OnChat, SilicaAdminMod.Power.Generic,
                "Si_ServerPerf: !perf status | !perf on|off <Module.Item|Module.*|*>");
        }

        static void OnChat(Player caller, string args)
        {
            string reply;
            try { reply = Commands.Handle(args, out _); }
            catch (Exception ex) { reply = "error: " + ex.Message; MelonLogger.Error("!perf: " + ex); }
            try { SilicaAdminMod.HelperMethods.SendChatMessageToPlayer(caller, "[Perf] " + reply); }
            catch { MelonLogger.Msg("!perf: " + reply); }
        }
    }
}
