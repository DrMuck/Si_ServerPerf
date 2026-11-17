/*
 Si_ServerPerf - what a module gets in Apply(): its config section, typed settings, item declaration, lifecycle hooks
 and its own Harmony instance ("Si_ServerPerf.<Name>").
*/

using System;
using System.Collections.Generic;
using MelonLoader;
using Newtonsoft.Json.Linq;

namespace SiServerPerf
{
    public sealed class ModuleContext
    {
        readonly JObject _section;
        readonly Dictionary<Type, object> _settings = new Dictionary<Type, object>();
        internal readonly List<PerfItem> Items = new List<PerfItem>();
        internal readonly List<string> Notes = new List<string>();

        public string Name { get; }
        public IPerfModule Module { get; }
        // Own Harmony instance. Patch through PerfItem.Patch; use this directly only for Unpatch / GetPatchInfo.
        public HarmonyLib.Harmony Harmony { get; }
        // Module switch from the config ("Enabled"). When false, Apply still runs so every item is declared and listed,
        // but nothing is patched and no hook is registered.
        public bool Enabled { get; }
        public bool Failed { get; private set; }
        public string FailInfo { get; private set; }

        internal ModuleContext(IPerfModule module, JObject section)
        {
            Module = module;
            Name = module.Name;
            _section = section;
            Harmony = new HarmonyLib.Harmony("Si_ServerPerf." + Name);
            Enabled = PerfConfig.Bool(section, "Enabled", true);
        }

        // Typed settings of this module: a plain class with public fields/properties and defaults in its initializers
        // (Newtonsoft.Json serializable). Missing keys are added to the JSON file with their defaults; existing values
        // are kept. If the section cannot be read into T, the defaults are used for this run and the file is left alone.
        public T Settings<T>() where T : class, new()
        {
            if (_settings.TryGetValue(typeof(T), out object cached))
            {
                return (T)cached;
            }
            T value = PerfConfig.ReadSettings<T>(_section, Name);
            _settings[typeof(T)] = value;
            return value;
        }

        // Declare an item. key: letters, digits, '_' (unique inside the module). setSwitch: writes the item's static
        // bool, e.g. v => On = v; the core is the only caller. The item's config boolean is "Items.<key>" in this
        // module's section, written with defaultOn when absent.
        public PerfItem Item(string key, string description, Action<bool> setSwitch, bool defaultOn = true, ItemFlags flags = ItemFlags.None)
        {
            foreach (var existing in Items)
            {
                if (existing.Key == key)
                {
                    Perf.Warn(Name + ": item " + key + " declared twice; the second declaration is ignored");
                    return existing;
                }
            }
            var item = new PerfItem(this, key, description, setSwitch, defaultOn, flags);
            item.ConfigOn = PerfConfig.ItemValue(_section, key, defaultOn, description, flags);
            if (!Enabled)
            {
                item.BlockFromCore("module disabled");
            }
            else if (item.Presentation && PerfConfig.Core.RequireHeadless && !Env.Headless)
            {
                item.BlockFromCore("not headless (" + Env.HeadlessReason + "), Core.RequireHeadless = true");
            }
            Items.Add(item);
            Registry.Add(item);
            return item;
        }

        // ------------------------------------------------------------------ lifecycle hooks
        // owner != null: called only while owner.On; an exception calls owner.Fail(ex).
        // owner == null: module-level hook; an exception removes the hook and is logged once.
        // Not registered at all when the module is disabled or the owner is not Active.

        public void OnUpdate(Action fn, PerfItem owner = null) => Lifecycle.Add(Lifecycle.Update, this, owner, fn, "OnUpdate");
        public void OnLateUpdate(Action fn, PerfItem owner = null) => Lifecycle.Add(Lifecycle.Late, this, owner, fn, "OnLateUpdate");
        public void OnFixedUpdate(Action fn, PerfItem owner = null) => Lifecycle.Add(Lifecycle.Fixed, this, owner, fn, "OnFixedUpdate");
        // MelonMod.OnSceneWasLoaded / OnSceneWasInitialized, argument = scene name.
        public void OnSceneLoaded(Action<string> fn, PerfItem owner = null) => Lifecycle.Add(Lifecycle.SceneLoaded, this, owner, fn, "OnSceneLoaded");
        public void OnSceneInitialized(Action<string> fn, PerfItem owner = null) => Lifecycle.Add(Lifecycle.SceneInit, this, owner, fn, "OnSceneInitialized");
        // Round start / end (GameEvents.OnGameStarted / OnGameEnded through MusicJukeboxHandler, as Admin Mod does).
        public void OnRoundStart(Action fn, PerfItem owner = null) => Lifecycle.Add(Lifecycle.RoundStart, this, owner, fn, "OnRoundStart");
        public void OnRoundEnd(Action fn, PerfItem owner = null) => Lifecycle.Add(Lifecycle.RoundEnd, this, owner, fn, "OnRoundEnd");
        public void OnQuit(Action fn, PerfItem owner = null) => Lifecycle.Add(Lifecycle.Quit, this, owner, fn, "OnQuit");
        // Called right before every status block (startup report, perf status, runtime.json dump, quit): refresh
        // item.Info counters here so the block is current. No owner = called whether or not an item is on.
        public void OnBeforeStatus(Action fn, PerfItem owner = null) => Lifecycle.Add(Lifecycle.BeforeStatus, this, owner, fn, "OnBeforeStatus");

        // Module-level line in the startup report.
        public void Note(string message)
        {
            if (!string.IsNullOrEmpty(message) && !Notes.Contains(message)) Notes.Add(message);
        }

        public void Log(string message) => MelonLogger.Msg("[" + Name + "] " + message);
        public void Warn(string message) => MelonLogger.Warning("[" + Name + "] " + message);

        internal void Fail(Exception ex)
        {
            Failed = true;
            FailInfo = ex.GetType().Name + ": " + ex.Message;
            MelonLogger.Error("module " + Name + " failed in Apply; unpatching it, all its items off:\n" + ex);
            try { Harmony.UnpatchSelf(); } catch (Exception uex) { MelonLogger.Error(Name + ": unpatch failed: " + uex.Message); }
            foreach (var item in Items)
            {
                foreach (var t in item.Targets) t.Patched = false;
            }
        }
    }
}
