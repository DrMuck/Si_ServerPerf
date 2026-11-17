/*
 Cause #14 / S13: small pure-waste fixes.

 DestroyedDisposal_EveryN. GameManager.Update calls BaseGameObject.UpdateDestroyedDisposal every frame: a walk over
 ALL BaseGameObjects with a virtual CanDispose and a Dictionary lookup each, to age wrecks towards their 15 s
 DestroyedDisposeTime (findings F: 3 ms/s). The prefix lets it run on every Nth frame only and a transpiler swaps its
 single Time.deltaTime read for the dt accumulated since the last run, so the timers advance exactly as before and a
 wreck is removed at most N-1 frames later than vanilla (1.0.17 IL: one call to Time::get_deltaTime, stored in loc0).

 TerrainMaterial_Cache. GamePhysics.GetTerrainPhysicMaterial(Vector3, Terrain) runs per terrain ground probe of every
 soldier (Soldier ground check, every 3rd fixed step for AI), per creature ground / attack contact and per projectile
 terrain impact. Per call: GetComponent<TerrainPhysicsMaterials> twice (once more inside
 GetCachedTerrainMaterialIndexes), terrain.terrainData six times, terrainData.size twice, alphamapWidth/Height. The
 prefix computes the same expression with the component, the transform, size and alphamap dimensions cached per
 Terrain (cleared on every scene load), keeps InverseTransformPoint and the index lookup live, and reads vanilla's own
 s_TerrainMaterialIndexes cache first exactly like GetCachedTerrainMaterialIndexes. A terrain without
 TerrainPhysicsMaterials (TerrainCollider.material fallback) or anything unexpected runs vanilla. Terrain lives in
 UnityEngine.TerrainModule, which the project does not reference, so it is handled as object/Component + reflection
 (resolved once).
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.Services
{
    internal static class DestroyedDisposal
    {
        internal static bool On;
        static PerfItem Item;
        static int _n = 4;
        static int _frame;
        static float _acc;
        static float _dtForRun;
        static long _runs, _skips;

        public static void Register(ModuleContext ctx, ServicesSettings s)
        {
            Item = ctx.Item("DestroyedDisposal_EveryN",
                "BaseGameObject.UpdateDestroyedDisposal (walk over all objects to age wrecks) every Settings.DestroyedDisposalEveryNFrames frames with the accumulated dt; wrecks vanish at most N-1 frames later",
                v => On = v);
            _n = Math.Max(2, Math.Min(30, s.DestroyedDisposalEveryNFrames));
            var m = Item.Target(typeof(BaseGameObject), nameof(BaseGameObject.UpdateDestroyedDisposal), Type.EmptyTypes);
            Item.Patch(m, typeof(DestroyedDisposal), prefix: nameof(Prefix), transpiler: nameof(Transpiler));
            Item.OnSwitch(_ => { _acc = 0f; _frame = 0; });
        }

        static bool Prefix()
        {
            if (!On) return true;
            try
            {
                _acc += Time.deltaTime;
                if (++_frame < _n) { _skips++; return false; }
                _frame = 0;
                _dtForRun = _acc;
                _acc = 0f;
                _runs++;
                return true;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "UpdateDestroyedDisposal prefix");
                return true;
            }
        }

        // Replaces the method's Time.deltaTime read.
        public static float DeltaTime()
        {
            return On ? _dtForRun : Time.deltaTime;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            MethodInfo getDt = AccessTools.PropertyGetter(typeof(Time), nameof(Time.deltaTime));
            int at = -1, hits = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(getDt)) { at = i; hits++; }
            }
            if (hits != 1)
            {
                Item.Problem("expected 1 Time.deltaTime read in UpdateDestroyedDisposal, found " + hits);
                return list;
            }
            list[at].opcode = OpCodes.Call;
            list[at].operand = AccessTools.Method(typeof(DestroyedDisposal), nameof(DeltaTime));
            return list;
        }

        internal static void RefreshInfo()
        {
            if (Item == null || !Item.Applied) return;
            Item.Info = "walks run " + _runs + ", skipped " + _skips + " (every " + _n + " frames)";
        }
    }

    internal static class TerrainMaterialCache
    {
        internal static bool On;
        static PerfItem Item;

        sealed class Entry
        {
            public Component Terrain;
            public Transform Transform;
            public TerrainPhysicsMaterials Materials;
            public float SizeX, SizeZ;
            public int AlphaW, AlphaH;
        }

        static readonly Dictionary<Component, Entry> _cache = new Dictionary<Component, Entry>();
        static Type _terrainType;
        static PropertyInfo _terrainData, _size, _alphaW, _alphaH;
        static IDictionary _vanillaIndexCache;   // GamePhysics.s_TerrainMaterialIndexes (Dictionary<Terrain, byte[]>)
        static long _served, _vanilla, _entries, _verified, _mismatches;
        static bool _verify, _inVerify;
        static MethodBase _target;

        public static void Register(ModuleContext ctx, ServicesSettings s)
        {
            _verify = s.VerifyAgainstVanilla;
            Item = ctx.Item("TerrainMaterial_Cache",
                "GamePhysics.GetTerrainPhysicMaterial(Vector3, Terrain) with TerrainPhysicsMaterials, transform, terrain size and alphamap size cached per Terrain (same result); no TerrainPhysicsMaterials = vanilla",
                v => On = v);
            _terrainType = AccessTools.TypeByName("UnityEngine.Terrain");
            Type terrainDataType = AccessTools.TypeByName("UnityEngine.TerrainData");
            if (_terrainType != null) _terrainData = AccessTools.Property(_terrainType, "terrainData");
            if (terrainDataType != null)
            {
                _size = AccessTools.Property(terrainDataType, "size");
                _alphaW = AccessTools.Property(terrainDataType, "alphamapWidth");
                _alphaH = AccessTools.Property(terrainDataType, "alphamapHeight");
            }
            FieldInfo cacheField = AccessTools.Field(typeof(GamePhysics), "s_TerrainMaterialIndexes");
            try { _vanillaIndexCache = cacheField?.GetValue(null) as IDictionary; } catch { _vanillaIndexCache = null; }
            if (_terrainType == null || _terrainData == null || _size == null || _alphaW == null || _alphaH == null)
                Item.Block("UnityEngine.Terrain / TerrainData members not found");
            else if (_vanillaIndexCache == null)
                Item.Block("GamePhysics.s_TerrainMaterialIndexes absent");

            MethodBase m = _terrainType == null ? null : AccessTools.Method(typeof(GamePhysics), nameof(GamePhysics.GetTerrainPhysicMaterial), new[] { typeof(Vector3), _terrainType });
            _target = Item.Target(m, "GamePhysics.GetTerrainPhysicMaterial(Vector3,Terrain)");
            Item.Patch(m, typeof(TerrainMaterialCache), prefix: nameof(Prefix));
            ctx.OnSceneLoaded(_ => _cache.Clear(), Item);
            Item.OnSwitch(_ => _cache.Clear());
        }

        // terrain is UnityEngine.Terrain (not referenced at compile time).
        static bool Prefix(Vector3 position, object terrain, ref PhysicsMaterial __result)
        {
            if (!On || _inVerify) return true;
            try
            {
                Component t = terrain as Component;
                if ((object)t == null || !t) { _vanilla++; return true; }
                if (!_cache.TryGetValue(t, out Entry e) || !e.Materials || !e.Terrain)
                {
                    e = Build(t);
                    if (e == null) { _vanilla++; return true; }
                }
                // same arithmetic as GetTerrainMaterialIndex(Vector3, Terrain)
                Vector3 v = e.Transform.InverseTransformPoint(position);
                float u = v.x / e.SizeX;
                float w = v.z / e.SizeZ;
                int aw = e.AlphaW, ah = e.AlphaH;
                float fx = u * (float)aw;
                float fz = w * (float)ah;
                int x = Mathf.Clamp((int)fx, 0, aw);
                int z = Mathf.Clamp((int)fz, 0, ah);
                int index = x * aw + z;
                // same as GetCachedTerrainMaterialIndexes: vanilla's dictionary first, then the component's array
                byte[] indexes = _vanillaIndexCache.Contains(t) ? (byte[])_vanillaIndexCache[t] : e.Materials.GetTerrainPhysicsIndices();
                int mat = (indexes != null && index < indexes.Length) ? indexes[index] : 0;
                PhysicsMaterial result = e.Materials.GetPhysicMaterial(mat);
                _served++;
                if (_verify && _target != null)
                {
                    // re-enter the patched method with this prefix passing through: the vanilla answer
                    _inVerify = true;
                    PhysicsMaterial vanillaResult;
                    try { vanillaResult = (PhysicsMaterial)_target.Invoke(null, new object[] { position, terrain }); }
                    finally { _inVerify = false; }
                    _verified++;
                    if (!ReferenceEquals(vanillaResult, result)) { _mismatches++; result = vanillaResult; }
                }
                __result = result;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "GetTerrainPhysicMaterial prefix");
                return true;
            }
        }

        static Entry Build(Component t)
        {
            _cache.Remove(t);
            TerrainPhysicsMaterials mats = t.GetComponent<TerrainPhysicsMaterials>();
            if (!mats) return null; // TerrainCollider.material fallback: vanilla, not cached
            object data = _terrainData.GetValue(t, null);
            if (data == null) return null;
            Vector3 size = (Vector3)_size.GetValue(data, null);
            var e = new Entry
            {
                Terrain = t,
                Transform = t.transform,
                Materials = mats,
                SizeX = size.x,
                SizeZ = size.z,
                AlphaW = (int)_alphaW.GetValue(data, null),
                AlphaH = (int)_alphaH.GetValue(data, null),
            };
            _cache[t] = e;
            _entries++;
            return e;
        }

        internal static void RefreshInfo()
        {
            if (Item == null || !Item.Applied) return;
            Item.Info = "served " + _served + ", vanilla " + _vanilla + ", terrains cached " + _cache.Count + " (built " + _entries + ")" +
                        (_verify ? " | VERIFY: checked " + _verified + ", mismatches " + _mismatches : "");
        }
    }
}
