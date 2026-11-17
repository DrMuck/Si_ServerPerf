/*
 Particles_Disable (S5, findings I §3). The server build already stubs every scripted particle Play. What still
 simulates are play-on-awake systems on objects that become active: pooled projectiles (ProjectileData keeps the
 particle systems in its stripped template and calls SetActive(true)), FX children switched on by creature attack
 components, looping systems on units and structures. No gameplay code reads particles.

 - ProjectileData.InstantiateProjectile (private) postfix: every particle system of the new projectile (inactive
   children included) gets playOnAwake = false and is stopped and cleared, so pooled reuse never restarts it.
 - BaseGameObject.Awake postfix: the same for every unit/structure/site.
 - Scene sweep on scene init: the same for every particle system already in the loaded scene.
 Startup-only: play-on-awake is changed on pooled objects and cannot be told apart from the prefab value later.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiServerPerf.Modules.Headless
{
    internal static class Particles
    {
        internal static bool On;
        static PerfItem Item;
        static readonly List<ParticleSystem> _buffer = new List<ParticleSystem>();
        public static int Stripped;

        public static void Register(ModuleContext ctx)
        {
            Item = ctx.Item("Particles_Disable",
                "Turn off play-on-awake and stop/clear particle systems on projectiles, units, structures and the loaded scene",
                v => On = v, flags: ItemFlags.Presentation | ItemFlags.StartupOnly);
            ServerOptimizerCompat.Apply(Item);
            var proj = Item.Target(typeof(ProjectileData), "InstantiateProjectile", new[] { typeof(Vector3), typeof(Quaternion) });
            var awake = Item.Target(typeof(BaseGameObject), nameof(BaseGameObject.Awake), Type.EmptyTypes, declaredOnly: true);
            Item.Patch(proj, typeof(Particles), postfix: nameof(ProjectilePostfix));
            Item.Patch(awake, typeof(Particles), postfix: nameof(AwakePostfix));
            if (Item.Hooked("scene sweep on scene init")) ctx.OnSceneInitialized(StripScene, Item);
        }

        static void ProjectilePostfix(ProjectileBasic __result)
        {
            if (!On || __result == null) return;
            try { Strip(__result.gameObject); }
            catch (Exception ex) { Item.Fail(ex, "InstantiateProjectile postfix"); }
        }

        static void AwakePostfix(BaseGameObject __instance)
        {
            if (!On || __instance == null) return;
            try { Strip(__instance.gameObject); }
            catch (Exception ex) { Item.Fail(ex, "BaseGameObject.Awake postfix"); }
        }

        static void Strip(GameObject root)
        {
            if (root == null) return;
            _buffer.Clear();
            try
            {
                root.GetComponentsInChildren(true, _buffer);
                for (int i = 0; i < _buffer.Count; i++) StripOne(_buffer[i]);
            }
            finally
            {
                _buffer.Clear();
            }
        }

        static void StripScene(string sceneName)
        {
            ParticleSystem[] all = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int before = Stripped;
            for (int i = 0; i < all.Length; i++) StripOne(all[i]);
            Perf.Log("[Headless] Particles_Disable: scene " + sceneName + ": stopped " + (Stripped - before) + " of " + all.Length + " particle systems");
        }

        static void StripOne(ParticleSystem ps)
        {
            if (ps == null) return;
            bool changed = false;
            ParticleSystem.MainModule main = ps.main;
            if (main.playOnAwake)
            {
                main.playOnAwake = false;
                changed = true;
            }
            if (ps.isPlaying || ps.particleCount > 0)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                changed = true;
            }
            if (changed) Stripped++;
        }
    }
}
