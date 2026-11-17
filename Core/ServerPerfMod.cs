/*
 Si_ServerPerf - server-side performance patches for the Silica dedicated server (Mono, 1.0.17, headless).
 Implements the server recommendations S1-S13 of Projects\Silica_Performance\DEV_REPORT.md as switchable modules.

 Startup: config -> modules discovered by reflection -> each module's Apply() declares items and patches through
 them -> switches set -> runtime.json (if present) -> one report block in the log.
 The assembly carries [HarmonyDontPatchAll]: nothing is patched by attribute; every patch goes through PerfItem.Patch.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(SiServerPerf.ServerPerfMod), "Si_ServerPerf", SiServerPerf.ServerPerfMod.Version, "DrMuck")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonOptionalDependencies("Admin Mod", "Si_AdminMod")]
[assembly: HarmonyDontPatchAll]

namespace SiServerPerf
{
    public sealed class ServerPerfMod : MelonMod
    {
        public const string Version = "0.2.1";
        static bool _started;

        public override void OnInitializeMelon()
        {
            try
            {
                Startup();
            }
            catch (Exception ex)
            {
                // Core failure: patches already installed by modules stay (each module guards its own); nothing else runs.
                MelonLogger.Error("startup failed:\n" + ex);
            }
        }

        public override void OnLateInitializeMelon()
        {
            if (!_started) return;
            if (PerfConfig.Core.ConsoleCommand) Commands.RegisterConsole();
            if (PerfConfig.Core.ChatCommand) Commands.RegisterChat();
        }

        static void Startup()
        {
            PerfConfig.Load();
            Env.DetectServerOptimizer();
            bool headless = Env.Headless;

            foreach (IPerfModule module in Discover())
            {
                string name = module.Name;
                if (string.IsNullOrEmpty(name) || Registry.Modules.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    MelonLogger.Error("module " + module.GetType().FullName + ": empty or duplicate Name '" + name + "', skipped");
                    continue;
                }
                var ctx = new ModuleContext(module, PerfConfig.ModuleSection(name));
                Registry.Modules.Add(ctx);
                try
                {
                    module.Apply(ctx);
                }
                catch (Exception ex)
                {
                    ctx.Fail(ex);
                }
            }

            foreach (var item in Registry.Items) item.SetInitial(item.Active);
            Lifecycle.InstallRoundHooks();
            PerfConfig.SaveIfDirty();
            _started = true;

            RuntimeControl.Startup();
            Registry.LogReport("startup");
            RuntimeControl.WriteState();
        }

        static List<IPerfModule> Discover()
        {
            Type[] types;
            try { types = typeof(ServerPerfMod).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                MelonLogger.Error("some types failed to load (a module references something absent): " + ex.LoaderExceptions.FirstOrDefault()?.Message);
                types = ex.Types.Where(t => t != null).ToArray();
            }

            var found = new List<(int order, string name, IPerfModule module)>();
            foreach (Type t in types)
            {
                if (t.IsAbstract || t.IsInterface || !typeof(IPerfModule).IsAssignableFrom(t)) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    MelonLogger.Error("module type " + t.FullName + " has no public parameterless constructor, skipped");
                    continue;
                }
                try
                {
                    var m = (IPerfModule)Activator.CreateInstance(t);
                    int order = t.GetCustomAttribute<PerfModuleOrderAttribute>()?.Order ?? 0;
                    found.Add((order, m.Name ?? "", m));
                }
                catch (Exception ex)
                {
                    MelonLogger.Error("module type " + t.FullName + " could not be created: " + ex.GetBaseException().Message);
                }
            }
            return found.OrderBy(f => f.order).ThenBy(f => f.name, StringComparer.Ordinal).Select(f => f.module).ToList();
        }

        public override void OnUpdate()
        {
            if (!_started) return;
            Perf.Frame = Time.frameCount;
            Perf.RealTime = Time.realtimeSinceStartup;
            Perf.UnscaledDt = Time.unscaledDeltaTime;
            Lifecycle.Run(Lifecycle.Update);
            try { RuntimeControl.Poll(); }
            catch (Exception ex) { MelonLogger.Warning("runtime poll: " + ex.Message); }
        }

        public override void OnLateUpdate()
        {
            if (_started) Lifecycle.Run(Lifecycle.Late);
        }

        public override void OnFixedUpdate()
        {
            if (_started) Lifecycle.Run(Lifecycle.Fixed);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (!_started) return;
            Perf.SceneName = sceneName ?? "";
            Lifecycle.Run(Lifecycle.SceneLoaded, sceneName);
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (_started) Lifecycle.Run(Lifecycle.SceneInit, sceneName);
        }

        public override void OnApplicationQuit()
        {
            if (!_started) return;
            Lifecycle.Run(Lifecycle.Quit);
            try { RuntimeControl.Quit(); }
            catch (Exception ex) { MelonLogger.Warning("quit status: " + ex.Message); }
        }
    }
}
