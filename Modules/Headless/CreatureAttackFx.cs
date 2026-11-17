/*
 Creature attack FX (S5 "attack glow", findings I §3.3 item 3). Each component below subscribes to
 CreatureDecapod.OnAttackStart and, per attack, registers an OrderedUpdate (lights, glow, enable FX children) that
 unregisters itself when the attack cools down: per-attack OrderedUpdateManager.Register/Unregister churn (REPORT
 #12) plus HDRP light writes. No hit result depends on them: the Colossus weak point (EyeballFiring/WeakPoint,
 enabled by CreatureAttackGlowEye) was registered as a hit zone at Awake with includeInactive, and the capsule
 queries never check activeInHierarchy (findings I §3.2).

 CreatureAttackFX_Skip       OnAttackStart(bool) prefix -> false for CreatureAttackFX, CreatureAttackGlowBlob,
                             CreatureAttackGlowCurves, CreatureAttackGlowEye, CreatureAttackGlowTongue.
                             (GlowTongue with a pulse also registers in OnEnable; that path is left alone.)
 CreatureAttackInflate_Skip  CreatureAttackInflate.OnAttackStart. DEFAULT OFF: unlike the glow components it moves
                             and scales InflateTransform and toggles EnableOnDeflate; whether a hit-zone capsule sits
                             under those transforms was not checked against the prefabs. Enable only after that check.
 Runtime switch: an attack already registered finishes vanilla; the next attack follows the switch.
*/

using System;

namespace SiServerPerf.Modules.Headless
{
    internal static class CreatureAttackFx
    {
        internal static bool On;

        public static void Register(ModuleContext ctx)
        {
            var item = ctx.Item("CreatureAttackFX_Skip",
                "Skip OnAttackStart of CreatureAttackFX / GlowBlob / GlowCurves / GlowEye / GlowTongue (per-attack light + FX registration)",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(item);
            foreach (Type t in new[] { typeof(CreatureAttackFX), typeof(CreatureAttackGlowBlob), typeof(CreatureAttackGlowCurves),
                                       typeof(CreatureAttackGlowEye), typeof(CreatureAttackGlowTongue) })
            {
                item.Patch(item.Target(t, "OnAttackStart", new[] { typeof(bool) }, declaredOnly: true), typeof(CreatureAttackFx), prefix: nameof(Prefix));
            }
        }

        static bool Prefix() => !On;
    }

    internal static class CreatureAttackInflate
    {
        internal static bool On;

        public static void Register(ModuleContext ctx)
        {
            var item = ctx.Item("CreatureAttackInflate_Skip",
                "Skip CreatureAttackInflate.OnAttackStart (inflate/deflate transform animation). Moves transforms: check hit zones before enabling",
                v => On = v, defaultOn: false, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(item);
            item.Patch(item.Target(typeof(global::CreatureAttackInflate), "OnAttackStart", new[] { typeof(bool) }, declaredOnly: true),
                       typeof(CreatureAttackInflate), prefix: nameof(Prefix));
        }

        static bool Prefix() => !On;
    }
}
