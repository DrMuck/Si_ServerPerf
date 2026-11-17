/*
 Animators_Disable (S5, findings I §1). On the headless server the soldier and creature Animators never pose the
 bones anyway (CullCompletely, nothing visible on the Null device), so hit-zone capsules already sit in the prefab
 pose in vanilla. No animator state is networked and player hits are decided on the shooting client. Disabling the
 components keeps that state and removes the per-animator cost (PreLateUpdate.DirectorUpdateAnimationBegin).

 - Soldier.OnInit / CreatureDecapod.OnInit postfix: track the CharacterAnimator / CreatureAnimator Animator; a
   per-frame drain disables it once Unity reports it initialized (or after AnimatorMaxWaitFrames).
 - UnitAnimator.CheckActivationDistance prefix: never re-enable by camera distance (compartment path).
 - CharacterAnimator.EnsureDeadPose prefix: no corpse-pose repair (it would re-enable the Animator).
 - UnitAnimator.Dead / CharacterAnimator.Dead: the vanilla death (one Animator.Update(0.01f)) runs on an enabled
   Animator as before, then it is disabled again.
 - Parameter writers (Clear, Attack, Jump, Hit, SetCrouch, Emerge, ...) are skipped while the Animator is off;
   SetWeaponLowered still stores its flag. CharacterAnimator.UpdateIKTargets is skipped (cosmetic IK + camera test).
 - Not touched: HumanHandsAnimator (weapon state), UnitAnimator.Awake, OnInCompartmentAnimatorLateUpdate (also
   places the seated soldier).
 Runtime switch: off re-enables every Animator this item disabled (the vanilla state; vanilla
 CheckActivationDistance takes over again), on disables all tracked ones again. Not startup-only.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.Headless
{
    internal static class Animators
    {
        internal static bool On;
        static PerfItem Item;
        static int _maxWait = 30;
        static AccessTools.FieldRef<CharacterAnimator, bool> _weaponLowered;

        struct Pending { public Animator Animator; public int Frames; }
        static readonly List<Pending> _pending = new List<Pending>();
        static readonly HashSet<Animator> _tracked = new HashSet<Animator>();
        static readonly HashSet<Animator> _disabledByUs = new HashSet<Animator>();
        static int _nextPrune;
        public static int DisabledTotal;

        static readonly (Type type, string name, Type[] args)[] Writers =
        {
            (typeof(UnitAnimator), "Clear", Type.EmptyTypes),
            (typeof(UnitAnimator), "ClearAttack", new[] { typeof(bool) }),
            (typeof(UnitAnimator), "Attack", new[] { typeof(bool) }),
            (typeof(UnitAnimator), "AttackEnd", new[] { typeof(bool) }),
            (typeof(UnitAnimator), "Custom", new[] { typeof(int) }),
            (typeof(UnitAnimator), "Jump", Type.EmptyTypes),
            (typeof(UnitAnimator), "Fall", Type.EmptyTypes),
            (typeof(UnitAnimator), "Land", new[] { typeof(EUnitAnimatorLandWeight) }),
            (typeof(UnitAnimator), "Hit", new[] { typeof(float), typeof(float) }),
            (typeof(CharacterAnimator), "Clear", Type.EmptyTypes),
            (typeof(CharacterAnimator), "Climb", new[] { typeof(bool) }),
            (typeof(CharacterAnimator), "SetCrouch", new[] { typeof(bool) }),
            (typeof(CreatureAnimator), "Clear", Type.EmptyTypes),
            (typeof(CreatureAnimator), "Submerge", Type.EmptyTypes),
            (typeof(CreatureAnimator), "Emerge", Type.EmptyTypes),
            (typeof(CreatureAnimator), "SetFlying", new[] { typeof(bool) }),
        };

        public static void Register(ModuleContext ctx, HeadlessSettings s)
        {
            Item = ctx.Item("Animators_Disable",
                "Disable soldier/creature Animator components after init and keep them off (activation distance, dead-pose repair, after death); skip parameter writes while off",
                v => On = v, flags: ItemFlags.Presentation);
            ServerOptimizerCompat.Apply(Item);
            _maxWait = Math.Max(1, s.AnimatorMaxWaitFrames);

            var Self = typeof(Animators);
            Item.Patch(Item.Target(typeof(Soldier), nameof(Soldier.OnInit), Type.EmptyTypes, declaredOnly: true), Self, postfix: nameof(OnInitPostfix));
            Item.Patch(Item.Target(typeof(CreatureDecapod), nameof(CreatureDecapod.OnInit), Type.EmptyTypes, declaredOnly: true), Self, postfix: nameof(OnInitPostfix));
            Item.Patch(Item.Target(typeof(UnitAnimator), nameof(UnitAnimator.CheckActivationDistance), new[] { typeof(float) }), Self, prefix: nameof(SkipPrefix));
            Item.Patch(Item.Target(typeof(CharacterAnimator), "EnsureDeadPose", new[] { typeof(float) }, declaredOnly: true), Self, prefix: nameof(SkipPrefix));
            Item.Patch(Item.Target(typeof(UnitAnimator), "Dead", Type.EmptyTypes, declaredOnly: true), Self, prefix: nameof(DeadPrefix), postfix: nameof(DeadPostfix));
            Item.Patch(Item.Target(typeof(CharacterAnimator), "Dead", Type.EmptyTypes, declaredOnly: true), Self, prefix: nameof(DeadPrefix), postfix: nameof(DeadPostfix));
            foreach (var w in Writers)
            {
                Item.Patch(Item.Target(w.type, w.name, w.args, declaredOnly: true), Self, prefix: nameof(WriterPrefix));
            }
            if (AccessTools.DeclaredField(typeof(CharacterAnimator), "WeaponLowered") is FieldInfo f && f.FieldType == typeof(bool))
            {
                _weaponLowered = AccessTools.FieldRefAccess<CharacterAnimator, bool>(f);
            }
            else
            {
                Item.Note("CharacterAnimator.WeaponLowered absent: SetWeaponLowered is not skipped");
            }
            if (_weaponLowered != null)
            {
                Item.Patch(Item.Target(typeof(CharacterAnimator), "SetWeaponLowered", new[] { typeof(bool) }, declaredOnly: true), Self, prefix: nameof(SetWeaponLoweredPrefix));
            }
            Item.Patch(Item.Target(typeof(CharacterAnimator), "UpdateIKTargets", Type.EmptyTypes, declaredOnly: true), Self, prefix: nameof(SkipPrefix));

            ctx.OnUpdate(Process, Item);
            ctx.OnSceneLoaded(_ => ClearTracking());
            Item.OnSwitch(SetAll);
        }

        static void Track(Animator a)
        {
            if (a == null || !_tracked.Add(a)) return;
            if (On) _pending.Add(new Pending { Animator = a, Frames = 0 });
        }

        // per frame while On
        static void Process()
        {
            if (_pending.Count > 0)
            {
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    Pending p = _pending[i];
                    if (p.Animator == null) { _pending.RemoveAt(i); continue; }
                    if (p.Animator.isInitialized || p.Frames >= _maxWait)
                    {
                        Disable(p.Animator);
                        _pending.RemoveAt(i);
                        continue;
                    }
                    p.Frames++;
                    _pending[i] = p;
                }
            }
            if (Perf.Frame >= _nextPrune)
            {
                _nextPrune = Perf.Frame + 1800;
                _tracked.RemoveWhere(a => a == null);
                _disabledByUs.RemoveWhere(a => a == null);
            }
        }

        static void Disable(Animator a)
        {
            if (a.enabled)
            {
                a.enabled = false;
                _disabledByUs.Add(a);
                DisabledTotal++;
            }
        }

        // runtime switch: off = restore vanilla (enabled), on = disable all tracked again
        static void SetAll(bool on)
        {
            _pending.Clear();
            _tracked.RemoveWhere(a => a == null);
            _disabledByUs.RemoveWhere(a => a == null);
            int n = 0;
            if (on)
            {
                foreach (Animator a in _tracked) { _pending.Add(new Pending { Animator = a, Frames = 0 }); n++; }
                Item.Info = "last switch-on: " + n + " tracked Animators queued for disabling";
            }
            else
            {
                foreach (Animator a in _disabledByUs)
                {
                    try { if (!a.enabled) { a.enabled = true; n++; } } catch { }
                }
                _disabledByUs.Clear();
                Item.Info = "last switch-off: " + n + " Animators re-enabled";
            }
        }

        static void ClearTracking()
        {
            _pending.Clear();
            _tracked.Clear();
            _disabledByUs.Clear();
        }

        static void OnInitPostfix(BaseGameObject __instance)
        {
            try
            {
                if (__instance is Soldier soldier)
                {
                    if (soldier.CharacterAnimator != null) Track(soldier.CharacterAnimator.Animator);
                }
                else if (__instance is CreatureDecapod creature)
                {
                    if (creature.CreatureAnimator != null) Track(creature.CreatureAnimator.Animator);
                }
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "OnInit postfix");
            }
        }

        static bool SkipPrefix() => !On;

        static void DeadPrefix(UnitAnimator __instance)
        {
            if (!On) return;
            try
            {
                // vanilla CharacterAnimator.Dead re-enables a disabled Animator under this condition; doing it here also
                // covers the base Dead (creatures), which does not
                Animator a = __instance.Animator;
                if (a != null && !a.enabled && __instance.enabled && a.gameObject.activeInHierarchy) a.enabled = true;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "Dead prefix");
            }
        }

        static void DeadPostfix(UnitAnimator __instance)
        {
            if (!On) return;
            try
            {
                Animator a = __instance.Animator;
                if (a != null) Disable(a);
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "Dead postfix");
            }
        }

        static bool WriterPrefix(UnitAnimator __instance)
        {
            if (!On) return true;
            try
            {
                Animator a = __instance.Animator;
                return a != null && a.enabled;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "parameter writer prefix");
                return true;
            }
        }

        static bool SetWeaponLoweredPrefix(CharacterAnimator __instance, bool weaponLowered)
        {
            if (!On) return true;
            try
            {
                Animator a = __instance.Animator;
                if (a != null && a.enabled) return true;
                _weaponLowered(__instance) = weaponLowered;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "SetWeaponLowered prefix");
                return true;
            }
        }
    }
}
