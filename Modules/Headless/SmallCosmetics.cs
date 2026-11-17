/*
 Small camera-distance / cosmetic updates with no server reader (S5, findings I §4). All runtime switchable.

 StructureLights_Skip       Structure.UpdateStructureLights: Light / HDAdditionalLightData / NightLight by camera
                            distance. Structure.OrderedUpdate (production queue etc.) keeps running.
 MeshCombiner_Skip          StructureMeshCombiner.OrderedLateUpdateAll: MeshRendererParent/BakedParent swap by
                            distance to Camera.main (only intro/tutorial/scenario prefabs carry it).
 SceneLOD_Skip              SceneLOD.SceneLODCell.Update: scenery Renderer.enabled by camera far plane. The first
                            skipped call also disables the component (no more Unity Update message); switching
                            the item off re-enables exactly those cells.
 LightGrid_Skip             LightGrid.Update (GameManager): LightGridEntry.Enable toggles lights. Si_ServerOptimizer
                            0.7.x/0.8.0 already prefixes it, so with SO loaded this stays off.
 VehicleEngineVents_Skip    VehicleEngineVents.OrderedUpdate: vent transform wobble within 50 m of the camera. A
                            destroyed vehicle still runs vanilla (which unregisters the handler).
 VehicleCreaking_Skip       VehicleCreaking.OrderedFixedUpdate: creak sounds on movement changes (cosmetic state only).
 ConstructionWarningUI_Skip ConstructionWarningUI.UpdateUI: warning-sign animation near the camera. NOT
                            ConstructionSite.UpdateEffects (collider segments + heat damage, findings I §4).
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiServerPerf.Modules.Headless
{
    internal static class StructureLights
    {
        internal static bool On;
        public static void Register(ModuleContext ctx)
        {
            var item = ctx.Item("StructureLights_Skip", "Skip Structure.UpdateStructureLights (camera-distance light toggling)",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(item);
            item.Patch(item.Target(typeof(Structure), nameof(Structure.UpdateStructureLights), Type.EmptyTypes), typeof(StructureLights), prefix: nameof(Prefix));
        }
        static bool Prefix() => !On;
    }

    internal static class MeshCombiner
    {
        internal static bool On;
        public static void Register(ModuleContext ctx)
        {
            var item = ctx.Item("MeshCombiner_Skip", "Skip StructureMeshCombiner.OrderedLateUpdateAll (camera-distance mesh swaps)",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(item);
            item.Patch(item.Target(typeof(StructureMeshCombiner), nameof(StructureMeshCombiner.OrderedLateUpdateAll), new[] { typeof(float) }),
                       typeof(MeshCombiner), prefix: nameof(Prefix));
        }
        static bool Prefix() => !On;
    }

    internal static class SceneLod
    {
        internal static bool On;
        static PerfItem Item;
        static readonly List<SceneLOD.SceneLODCell> _disabled = new List<SceneLOD.SceneLODCell>();

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("SceneLOD_Skip", "Skip and disable SceneLOD.SceneLODCell (camera-distance scenery renderer toggling)",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(Item);
            Item.Patch(Item.Target(typeof(SceneLOD.SceneLODCell), "Update", Type.EmptyTypes), typeof(SceneLod), prefix: nameof(Prefix));
            Item.OnSwitch(on =>
            {
                if (on) return;
                int n = 0;
                foreach (var cell in _disabled)
                {
                    if (cell != null && !cell.enabled) { cell.enabled = true; n++; }
                }
                _disabled.Clear();
                Item.Info = "last switch-off: " + n + " SceneLODCells re-enabled";
            });
            ctx.OnSceneLoaded(_ => _disabled.Clear());
        }

        static bool Prefix(SceneLOD.SceneLODCell __instance)
        {
            if (!On) return true;
            try
            {
                if (__instance != null)
                {
                    __instance.enabled = false;
                    _disabled.Add(__instance);
                }
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "SceneLODCell.Update prefix");
                return true;
            }
        }
    }

    internal static class LightGridSkip
    {
        internal static bool On;
        public static void Register(ModuleContext ctx)
        {
            var item = ctx.Item("LightGrid_Skip", "Skip LightGrid.Update (light toggling grid; no server reader)",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(item);
            item.Patch(item.Target(typeof(LightGrid), nameof(LightGrid.Update), Type.EmptyTypes), typeof(LightGridSkip), prefix: nameof(Prefix));
        }
        static bool Prefix() => !On;
    }

    internal static class VehicleEngineVentsSkip
    {
        internal static bool On;
        static PerfItem Item;
        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("VehicleEngineVents_Skip", "Skip VehicleEngineVents.OrderedUpdate (vent wobble near the camera); destroyed vehicles still run vanilla",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(Item);
            Item.Patch(Item.Target(typeof(VehicleEngineVents), nameof(VehicleEngineVents.OrderedUpdate), new[] { typeof(float) }),
                       typeof(VehicleEngineVentsSkip), prefix: nameof(Prefix));
        }

        static bool Prefix(VehicleEngineVents __instance)
        {
            if (!On) return true;
            try
            {
                Vehicle v = __instance.Vehicle;
                // vanilla unregisters the handler in this case: let it
                if (v == null || v.IsDestroyed || v.RigidBody == null) return true;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "VehicleEngineVents.OrderedUpdate prefix");
                return true;
            }
        }
    }

    internal static class VehicleCreakingSkip
    {
        internal static bool On;
        public static void Register(ModuleContext ctx)
        {
            var item = ctx.Item("VehicleCreaking_Skip", "Skip VehicleCreaking.OrderedFixedUpdate (creak sounds on movement changes)",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(item);
            item.Patch(item.Target(typeof(VehicleCreaking), nameof(VehicleCreaking.OrderedFixedUpdate), new[] { typeof(float) }),
                       typeof(VehicleCreakingSkip), prefix: nameof(Prefix));
        }
        static bool Prefix() => !On;
    }

    internal static class ConstructionWarningUiSkip
    {
        internal static bool On;
        public static void Register(ModuleContext ctx)
        {
            var item = ctx.Item("ConstructionWarningUI_Skip", "Skip ConstructionWarningUI.UpdateUI (warning-sign animation); ConstructionSite.UpdateEffects is NOT touched",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(item);
            item.Patch(item.Target(typeof(ConstructionWarningUI), nameof(ConstructionWarningUI.UpdateUI),
                                   new[] { typeof(float), typeof(Vector3), typeof(float), typeof(bool) }),
                       typeof(ConstructionWarningUiSkip), prefix: nameof(Prefix));
        }
        static bool Prefix() => !On;
    }
}
