/*
 Si_ServerPerf - module contract.

 A module is any non-abstract class in this assembly that implements IPerfModule and has a public parameterless
 constructor. The core finds it by reflection at startup; no shared file lists modules. One folder per module:
 Modules\<Name>\. See README.md ("Module contract") for the full rules.
*/

using System;

namespace SiServerPerf
{
    public interface IPerfModule
    {
        // Config section name, item-id prefix ("<Name>.<ItemKey>") and log prefix. Letters, digits, '_' only.
        string Name { get; }

        // One line for the startup report and the config help.
        string Description { get; }

        // Called once at startup (MelonMod.OnInitializeMelon), after the config is loaded. Declare items, resolve
        // targets and patch through the context. Never call Harmony.Patch directly: go through PerfItem.Patch so the
        // switch, the counters and the fail-open handling work. If Apply throws, the core unpatches everything this
        // module patched, switches its items off and logs the exception once; the server keeps running.
        void Apply(ModuleContext ctx);
    }

    // Optional: the order modules are applied in (low first, then by Name). Default 0.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PerfModuleOrderAttribute : Attribute
    {
        public int Order { get; }
        public PerfModuleOrderAttribute(int order) { Order = order; }
    }

    [Flags]
    public enum ItemFlags
    {
        None = 0,

        // Only applied when the server runs headless (batch mode or Null graphics device) while
        // Core.RequireHeadless is true. Use it for anything that skips presentation work.
        Presentation = 1,

        // The item changes persistent state that the core cannot undo by flipping the switch (for example
        // play-on-awake cleared on pooled particle systems). Runtime switching is refused after the first frame;
        // runtime.json present at server start still decides it (nothing has been changed yet then).
        // Fail-open still switches it off. The status output marks it "startup-only".
        StartupOnly = 2,
    }
}
