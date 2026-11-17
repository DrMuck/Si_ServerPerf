/*
 Si_ServerPerf module "Headless": DEV_REPORT S5 (presentation work the headless server does not need) plus the
 already-written pieces of S6 (FogOfWar DetectedTargets lookup) and S13 (Steam details 1 Hz, ban walk 1 Hz).
 Ported from our Si_ServerOptimizer 0.8.0 (never run in game) and re-verified against Silica 1.0.17; safety per
 Projects\Silica_Performance\findings\I_server_cosmetic_skip_safety.md.

 Deliberately NOT patched: FogOfWar.OnUpdate as a whole, UpdateFogPixelData, LateUpdate (fog gameplay data);
 ConstructionSite.UpdateEffects / ConstructionEffect.UpdateEffect (collider segments + heat damage); any
 HumanHandsAnimator method (weapon state); UnitAnimator.Awake (0.7.1 skipped it -> seated-soldier NRE).
 Not needed on the server: UnitVoiceManager (disables itself in the server build), HideInStrategyMode (its update is
 only registered in strategy mode), VehicleParticleFX (empty stubs).
 Not covered by findings I, so not here: AlienStructureFX, ambient audio.
*/

namespace SiServerPerf.Modules.Headless
{
    public sealed class HeadlessSettings
    {
        // When Si_ServerOptimizer is loaded, items it already patches stay off (no double patching). true = apply ours anyway.
        public bool AllowOverlapWithServerOptimizer = false;
        public float SteamDetailsIntervalSeconds = 1f;
        public float PlayerBansIntervalSeconds = 1f;
        // Animators_Disable: frames to wait for Animator.isInitialized before disabling anyway.
        public int AnimatorMaxWaitFrames = 30;
        // Diag_LogExceptions: distinct exception messages per round written with a short stack.
        public int DiagMaxDistinctPerRound = 5;
        // Diag_LogExceptions: also count Unity warnings/errors that mention "Animator".
        public bool DiagCountAnimatorWarnings = true;
    }

    [PerfModuleOrder(-100)]
    public sealed class HeadlessModule : IPerfModule
    {
        public string Name => "Headless";
        public string Description => "S5 presentation skips + S6 fog lookup + S13 Steam/ban 1 Hz (ported from Si_ServerOptimizer 0.8.0)";

        public void Apply(ModuleContext ctx)
        {
            var s = ctx.Settings<HeadlessSettings>();
            ServerOptimizerCompat.Init(ctx, s);

            SteamDetails.Register(ctx, s);
            PlayerBans.Register(ctx, s);
            FogPresentation.Register(ctx);
            FogDetectedLookup.Register(ctx);
            Animators.Register(ctx, s);
            Particles.Register(ctx);
            StructureLights.Register(ctx);
            MeshCombiner.Register(ctx);
            SceneLod.Register(ctx);
            LightGridSkip.Register(ctx);
            VehicleEngineVentsSkip.Register(ctx);
            VehicleCreakingSkip.Register(ctx);
            ConstructionWarningUiSkip.Register(ctx);
            CreatureAttackFx.Register(ctx);
            CreatureAttackInflate.Register(ctx);
            ExceptionDiag.Register(ctx, s);

            ServerOptimizerCompat.LogSummary(ctx);
        }
    }
}
