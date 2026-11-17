/*
 Si_ServerPerf - one switchable sub-feature of a module.

 Life of an item:
   ctx.Item(key, description, v => On = v)   declares it; reads its config boolean (written with the default if absent)
   item.Block(reason)                         optional: keep it unpatched (overlap with another mod, missing field, ...)
   item.Target(type, name, args)              resolves a member, counts it as "targeted", records it if absent
   item.Patch(target, typeof(P), prefix: ...) installs Harmony patches, only if the item is Active
   -> after Apply the core sets the switch to true for every item with at least one patched target.
 At runtime the core flips the switch (runtime.json, console, chat); a patch that throws calls item.Fail(ex) and the
 core switches the item off for good (until restart) and logs once.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace SiServerPerf
{
    public sealed class PerfItem
    {
        internal sealed class TargetInfo
        {
            public string Label;
            public MethodBase Method;     // null = absent on this build
            public bool Patched;
            public bool IsHook;           // non-Harmony "target" (event subscription etc.), see Hooked()
            public string Problem;
            public readonly List<MethodInfo> PatchMethods = new List<MethodInfo>();
        }

        readonly ModuleContext _module;
        readonly Action<bool> _setSwitch;
        readonly List<Action<bool>> _onSwitch = new List<Action<bool>>();
        internal readonly List<TargetInfo> Targets = new List<TargetInfo>();
        internal readonly List<string> Notes = new List<string>();
        TargetInfo _patching;
        bool _problemDuringPatch;

        public string Module => _module.Name;
        public string Key { get; }
        public string Id { get; }
        public string Description { get; }
        public ItemFlags Flags { get; }
        public bool DefaultOn { get; }

        // Config value at startup.
        public bool ConfigOn { get; internal set; }
        // Why the item was not patched although the config says on (Block, not headless, module disabled/failed).
        public string BlockedReason { get; private set; }
        // Current switch value (mirror of the module's static bool).
        public bool On { get; private set; }
        public bool Failed { get; private set; }
        public string FailInfo { get; private set; }
        public int RuntimeChanges { get; private set; }

        public bool StartupOnly => (Flags & ItemFlags.StartupOnly) != 0;
        public bool Presentation => (Flags & ItemFlags.Presentation) != 0;

        // True while patches may be installed: config on, not blocked, module enabled and not failed.
        public bool Active => ConfigOn && BlockedReason == null && _module.Enabled && !_module.Failed && !Failed;

        public int TargetCount => Targets.Count;
        public int PatchedCount { get { int n = 0; foreach (var t in Targets) if (t.Patched) n++; return n; } }
        // At least one target is patched (or hooked): the switch can do something.
        public bool Applied => PatchedCount > 0;

        internal PerfItem(ModuleContext module, string key, string description, Action<bool> setSwitch, bool defaultOn, ItemFlags flags)
        {
            _module = module;
            Key = key;
            Id = module.Name + "." + key;
            Description = description ?? "";
            _setSwitch = setSwitch;
            DefaultOn = defaultOn;
            Flags = flags;
        }

        // ------------------------------------------------------------------ declaration

        // Keep the item unpatched. First reason wins. Call before Patch().
        public void Block(string reason)
        {
            if (BlockedReason == null)
            {
                BlockedReason = string.IsNullOrEmpty(reason) ? "blocked" : reason;
            }
        }

        // Resolve a method (AccessTools.Method / DeclaredMethod) and count it as a target of this item. Returns null if it
        // does not exist on this build; the startup report then lists it as absent. Resolution runs even when the item
        // is off, so the report shows absent members before anyone enables the item.
        public MethodBase Target(Type type, string name, Type[] args = null, bool declaredOnly = false)
        {
            string label = Label(type, name, args);
            MethodBase m = null;
            try
            {
                if (type != null)
                {
                    m = declaredOnly ? AccessTools.DeclaredMethod(type, name, args) : AccessTools.Method(type, name, args);
                }
            }
            catch (Exception ex)
            {
                Note("resolving " + label + ": " + ex.GetType().Name + ": " + ex.Message);
            }
            return Target(m, label);
        }

        // Count an already resolved member (property getter, constructor, ...) as a target. method == null = absent.
        public MethodBase Target(MethodBase method, string label)
        {
            foreach (var t in Targets)
            {
                if (t.Method != null && t.Method == method)
                {
                    return method;
                }
            }
            Targets.Add(new TargetInfo { Label = label ?? (method != null ? Describe(method) : "?"), Method = method });
            return method;
        }

        // A non-Harmony effect (event subscription, per-frame helper, ...) that counts as one applied target.
        // Only records when the item is Active; returns Active so the caller can do `if (item.Hooked("...")) Subscribe();`.
        public bool Hooked(string label)
        {
            var t = new TargetInfo { Label = label, IsHook = true };
            Targets.Add(t);
            if (!Active)
            {
                return false;
            }
            t.Patched = true;
            return true;
        }

        // Install Harmony patches on a target. Patch methods are looked up by name in patchClass (static, any
        // visibility). Does nothing (returns false) when target is null or the item is not Active. Never throws:
        // a Harmony error, a missing patch method or an item.Problem() raised by a transpiler during this call leaves
        // the target unpatched and listed as NOT APPLIED. Several Patch calls on the same target are fine.
        public bool Patch(MethodBase target, Type patchClass, string prefix = null, string postfix = null,
                          string transpiler = null, string finalizer = null, int priority = -1)
        {
            if (target == null || !Active)
            {
                return false;
            }
            TargetInfo info = null;
            foreach (var t in Targets)
            {
                if (t.Method == target) { info = t; break; }
            }
            if (info == null)
            {
                Target(target, Describe(target));
                info = Targets[Targets.Count - 1];
            }

            var methods = new List<MethodInfo>();
            HarmonyMethod Hm(string name)
            {
                if (name == null) return null;
                MethodInfo mi = AccessTools.Method(patchClass, name);
                if (mi == null || !mi.IsStatic)
                {
                    info.Problem = "patch method " + patchClass?.Name + "." + name + " not found or not static";
                    return null;
                }
                methods.Add(mi);
                var hm = new HarmonyMethod(mi);
                if (priority >= 0) hm.priority = priority;
                return hm;
            }

            HarmonyMethod pre = Hm(prefix), post = Hm(postfix), trans = Hm(transpiler), fin = Hm(finalizer);
            if (info.Problem != null || methods.Count == 0)
            {
                if (methods.Count == 0 && info.Problem == null) info.Problem = "no patch method given";
                Perf.Warn(Id + ": " + info.Label + ": " + info.Problem);
                return false;
            }

            _patching = info;
            _problemDuringPatch = false;
            try
            {
                _module.Harmony.Patch(target, pre, post, trans, fin, null);
            }
            catch (Exception ex)
            {
                info.Problem = "Harmony: " + ex.GetType().Name + ": " + ex.Message;
                Perf.Warn(Id + ": patching " + info.Label + " failed: " + ex);
                _patching = null;
                TryUnpatch(target, methods);
                return false;
            }
            _patching = null;

            if (_problemDuringPatch)
            {
                TryUnpatch(target, methods);
                return false;
            }
            info.Patched = true;
            info.PatchMethods.AddRange(methods);
            return true;
        }

        void TryUnpatch(MethodBase target, List<MethodInfo> methods)
        {
            foreach (MethodInfo mi in methods)
            {
                try { _module.Harmony.Unpatch(target, mi); } catch { }
            }
        }

        // A transpiler that did not find its pattern (or any other "this does not fit this build") calls this and
        // returns the instructions unchanged. During item.Patch the target is then unpatched and counted NOT APPLIED;
        // later (Harmony re-runs transpilers when another mod patches the same method) it is only logged.
        public void Problem(string message)
        {
            if (_patching != null)
            {
                _patching.Problem = message;
                _problemDuringPatch = true;
                Perf.Warn(Id + ": " + _patching.Label + ": " + message);
            }
            else
            {
                Note(message);
                Perf.Warn(Id + ": " + message);
            }
        }

        // Extra line under the item in the startup report / status.
        // For fixed facts found at startup (absent field, fallback taken). Deduplicated, never cleared.
        public void Note(string message)
        {
            if (!string.IsNullOrEmpty(message) && !Notes.Contains(message)) Notes.Add(message);
        }

        // One overwritable status text for changing runtime facts ("last switch-off: 312 Animators re-enabled",
        // counters). Shown in the status output and runtime_state.json. Use this, not Note, for anything repeated.
        public string Info { get; set; }

        // Called after every switch change (runtime flip, fail-open), with the new value. Use it to restore or re-apply
        // state that the patches changed (re-enable components, ...). Exceptions are caught and logged.
        public void OnSwitch(Action<bool> handler)
        {
            if (handler != null) _onSwitch.Add(handler);
        }

        // ------------------------------------------------------------------ runtime

        // Fail-open: call from the catch block of every patch body (and of helpers called by transpiled code), then
        // let vanilla run. Switches the item off for good (until restart), logs once with the stack.
        public void Fail(Exception ex, string where = null)
        {
            if (Failed)
            {
                return;
            }
            Failed = true;
            FailInfo = (where != null ? where + ": " : "") + (ex != null ? ex.GetType().Name + ": " + ex.Message : "failed");
            try { _setSwitch?.Invoke(false); } catch { }
            bool was = On;
            On = false;
            MelonLogger.Error(Id + " FAILED" + (where != null ? " in " + where : "") +
                              ": switched off, vanilla runs from now on (restart to retry).\n" + ex);
            if (was) RunHandlers(false);
        }

        internal void SetInitial(bool on)
        {
            On = on && Applied && !Failed;
            try { _setSwitch?.Invoke(On); }
            catch (Exception ex) { Fail(ex, "switch setter"); }
        }

        // Runtime change. Returns a short result for the log / command reply.
        internal string SetRuntime(bool on, bool atStartup)
        {
            if (Failed) return "failed earlier, stays off";
            if (!Applied)
            {
                string why = BlockedReason ?? (!ConfigOn ? "off in config" : "not applied");
                return on ? "not patched (" + why + "), cannot switch on" : "already off (" + why + ")";
            }
            if (On == on) return on ? "already on" : "already off";
            if (StartupOnly && !atStartup) return "startup-only, unchanged (restart to change)";
            On = on;
            RuntimeChanges++;
            try { _setSwitch?.Invoke(on); }
            catch (Exception ex) { Fail(ex, "switch setter"); return "failed"; }
            RunHandlers(on);
            return on ? "on" : "off";
        }

        void RunHandlers(bool on)
        {
            foreach (var h in _onSwitch)
            {
                try { h(on); }
                catch (Exception ex)
                {
                    MelonLogger.Warning(Id + ": switch handler threw: " + ex);
                }
            }
        }

        internal void BlockFromCore(string reason) => Block(reason);

        // ------------------------------------------------------------------ status

        public string State
        {
            get
            {
                if (Failed) return "FAILED";
                if (!_module.Enabled) return "off (module disabled)";
                if (_module.Failed) return "off (module failed)";
                if (!ConfigOn) return "off (config)";
                if (BlockedReason != null) return "SKIPPED";
                if (!Applied) return "NOT APPLIED";
                if (!On) return "off (runtime)";
                return PatchedCount < TargetCount ? "ON (partial)" : "ON";
            }
        }

        internal string Line()
        {
            string kind = StartupOnly ? "startup-only" : "toggle";
            if (Presentation) kind += ",presentation";
            string s = Id.PadRight(46) + " " + State.PadRight(22) + " " + (PatchedCount + "/" + TargetCount).PadLeft(5) + "  " + kind;
            if (BlockedReason != null && ConfigOn) s += "  | " + BlockedReason;
            if (Failed) s += "  | " + FailInfo;
            var absent = new List<string>();
            var problems = new List<string>();
            foreach (var t in Targets)
            {
                if (t.Method == null && !t.IsHook) absent.Add(t.Label);
                else if (t.Problem != null) problems.Add(t.Label + ": " + t.Problem);
            }
            if (absent.Count > 0) s += "  | absent on this build: " + string.Join(", ", absent);
            if (problems.Count > 0) s += "  | NOT APPLIED: " + string.Join("; ", problems);
            foreach (string n in Notes) s += "  | " + n;
            if (!string.IsNullOrEmpty(Info)) s += "  | " + Info;
            return s;
        }

        internal static string Label(Type type, string name, Type[] args)
        {
            string a = args == null ? "" : "(" + string.Join(",", Array.ConvertAll(args, x => x?.Name ?? "?")) + ")";
            return (type != null ? type.Name : "?") + "." + name + a;
        }

        internal static string Describe(MethodBase m)
        {
            if (m == null) return "?";
            var ps = m.GetParameters();
            return m.DeclaringType?.Name + "." + m.Name + "(" + string.Join(",", Array.ConvertAll(ps, p => p.ParameterType.Name)) + ")";
        }
    }
}
