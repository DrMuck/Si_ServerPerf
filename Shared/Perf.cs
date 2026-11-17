/*
 Si_ServerPerf shared helpers: frame counter, round/scene state, logging. Plain static fields, cheap to read from
 patches. Written only by the core.
*/

using MelonLoader;

namespace SiServerPerf
{
    public static class Perf
    {
        // UnityEngine.Time.frameCount, sampled at the start of this mod's OnUpdate (MelonLoader's Update, which runs
        // in the same frame as the game's Update; the order between the two is not defined).
        public static int Frame;
        // Time.realtimeSinceStartup at the same point.
        public static float RealTime;
        // Time.unscaledDeltaTime at the same point.
        public static float UnscaledDt;

        // True between a round start and its end (MusicJukeboxHandler.OnGameStarted / OnGameEnded).
        public static bool RoundActive;
        // Incremented on every round start (1 = first round since server start).
        public static int RoundIndex;
        // Last scene loaded (MelonMod.OnSceneWasLoaded).
        public static string SceneName = "";

        public static void Log(string message) => MelonLogger.Msg(message);
        public static void Warn(string message) => MelonLogger.Warning(message);
        public static void Error(string message) => MelonLogger.Error(message);
    }
}
