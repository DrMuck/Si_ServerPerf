/*
 Si_ServerPerf - per-frame and lifecycle hooks for modules (see ModuleContext.OnUpdate etc.).
 Round start/end come from postfixes on MusicJukeboxHandler.OnGameStarted / OnGameEnded (the component every game
 mode's GameEvents.OnGameStarted/OnGameEnded reaches; Admin Mod and our other mods use the same hook).
*/

using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;

namespace SiServerPerf
{
    internal static class Lifecycle
    {
        internal sealed class Hook
        {
            public ModuleContext Module;
            public PerfItem Owner;
            public Delegate Fn;
            public string Kind;
            public bool Dead;
        }

        public static readonly List<Hook> Update = new List<Hook>();
        public static readonly List<Hook> Late = new List<Hook>();
        public static readonly List<Hook> Fixed = new List<Hook>();
        public static readonly List<Hook> SceneLoaded = new List<Hook>();
        public static readonly List<Hook> SceneInit = new List<Hook>();
        public static readonly List<Hook> RoundStart = new List<Hook>();
        public static readonly List<Hook> RoundEnd = new List<Hook>();
        public static readonly List<Hook> Quit = new List<Hook>();
        public static readonly List<Hook> BeforeStatus = new List<Hook>();

        public static void Add(List<Hook> list, ModuleContext module, PerfItem owner, Delegate fn, string kind)
        {
            if (fn == null || !module.Enabled || module.Failed) return;
            if (owner != null && !owner.Active) return;
            list.Add(new Hook { Module = module, Owner = owner, Fn = fn, Kind = kind });
        }

        static bool Skip(Hook h) => h.Dead || h.Module.Failed || (h.Owner != null && !h.Owner.On);

        static void OnError(Hook h, Exception ex)
        {
            if (h.Owner != null)
            {
                h.Owner.Fail(ex, h.Kind + " hook");
            }
            else
            {
                h.Dead = true;
                MelonLogger.Error(h.Module.Name + " " + h.Kind + " hook threw; hook removed:\n" + ex);
            }
        }

        public static void Run(List<Hook> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Hook h = list[i];
                if (Skip(h)) continue;
                try { ((Action)h.Fn)(); }
                catch (Exception ex) { OnError(h, ex); }
            }
        }

        public static void Run(List<Hook> list, string arg)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Hook h = list[i];
                if (Skip(h)) continue;
                try { ((Action<string>)h.Fn)(arg); }
                catch (Exception ex) { OnError(h, ex); }
            }
        }

        // ------------------------------------------------------------------ round hooks (core-owned patches)

        static HarmonyLib.Harmony _harmony;
        public static string RoundHookState = "not installed";

        public static void InstallRoundHooks()
        {
            _harmony = new HarmonyLib.Harmony("Si_ServerPerf.Core");
            int ok = 0;
            ok += PatchPostfix(typeof(MusicJukeboxHandler), "OnGameStarted", new[] { typeof(GameMode) }, nameof(PostGameStarted));
            ok += PatchPostfix(typeof(MusicJukeboxHandler), "OnGameEnded", new[] { typeof(GameMode), typeof(Team) }, nameof(PostGameEnded));
            RoundHookState = ok == 2 ? "ok" : ok + "/2 round hooks";
        }

        static int PatchPostfix(Type type, string name, Type[] args, string postfix)
        {
            try
            {
                var m = AccessTools.Method(type, name, args);
                if (m == null)
                {
                    MelonLogger.Warning("round hook target " + type.Name + "." + name + " not found; OnRound* hooks will not fire");
                    return 0;
                }
                _harmony.Patch(m, postfix: new HarmonyMethod(AccessTools.Method(typeof(Lifecycle), postfix)));
                return 1;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("round hook " + type.Name + "." + name + ": " + ex.Message);
                return 0;
            }
        }

        static void PostGameStarted()
        {
            try
            {
                Perf.RoundActive = true;
                Perf.RoundIndex++;
                Run(RoundStart);
            }
            catch (Exception ex) { MelonLogger.Error("round start: " + ex); }
        }

        static void PostGameEnded()
        {
            try
            {
                Perf.RoundActive = false;
                Run(RoundEnd);
            }
            catch (Exception ex) { MelonLogger.Error("round end: " + ex); }
        }
    }
}
