/*
 Coexistence with data-bomb's Si_ServerOptimizer ("Server Optimizer" melon). Items the loaded version already patches
 are blocked (SKIPPED in the report) unless Settings.AllowOverlapWithServerOptimizer is true.

 Overlap table, from the sources (0.7.1: Projects\Silica_beta_0946\Si_ServerOptimizer, 0.7.3: HEAD of
 Projects\Silica-wt-serveroptimizer, 0.8.0: the uncommitted working tree there):

   Headless item                SO 0.7.1  SO 0.7.3  SO 0.8.0  SO member(s) that overlap
   LightGrid_Skip               yes       yes       yes       LightGrid.Update
   SteamDetails_1Hz             -         -         yes       NetworkGameServer.SendUpdatedServerDetailsToSteam
   PlayerBans_1Hz               -         -         yes       NetworkServerSettings.UpdatePlayerBans
   Fog_SkipPresentation         -         -         yes       FogOfWar.ShowFog, UpdateFogPixelBlend
   Fog_DetectedTargetLookup     -         -         yes       FogOfWar.OnUpdate (transpiler)
   Animators_Disable            -         -         yes       Soldier/CreatureDecapod.OnInit, UnitAnimator/CharacterAnimator/CreatureAnimator members
   Particles_Disable            -         -         yes       ProjectileData.InstantiateProjectile, BaseGameObject.Awake, scene sweep
   StructureLights_Skip         -         -         yes       Structure.UpdateStructureLights
   MeshCombiner_Skip            -         -         yes       StructureMeshCombiner.OrderedLateUpdateAll
   SceneLOD_Skip                -         -         yes       SceneLOD.SceneLODCell.Update
   Diag_LogExceptions           -         -         yes       Application.logMessageReceived hook (would log twice)
   VehicleEngineVents_Skip, VehicleCreaking_Skip, ConstructionWarningUI_Skip,
   CreatureAttackFX_Skip, CreatureAttackInflate_Skip:  no SO version patches them.
 Members both 0.7.x and our Animators item touch: none (0.7.x patches UnitAnimator.OnAnimatorUpdate, which we
 leave alone; 0.7.1 additionally skips UnitAnimator.Awake - see findings I §1.6, our item does not depend on it).
 A version newer than 0.8.0 or one that does not parse is treated like 0.8.0 (conservative), with a warning.
 For 0.8.0 its own cfg (UserData\cfg\Si_ServerOptimizer\Si_ServerOptimizer.cfg) is read: an item it has set to
 false there is not counted as overlap, so ours applies.
*/

using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;

namespace SiServerPerf.Modules.Headless
{
    internal static class ServerOptimizerCompat
    {
        static readonly Version V080 = new Version(0, 8, 0);

        // item key -> (first SO version that patches it, SO member text, SO 0.8.0 cfg key or null)
        static readonly Dictionary<string, (Version since, string members, string soKey)> Table = new Dictionary<string, (Version, string, string)>
        {
            { "LightGrid_Skip",           (new Version(0, 0), "LightGrid.Update", null) },
            { "SteamDetails_1Hz",         (V080, "NetworkGameServer.SendUpdatedServerDetailsToSteam", "SteamDetails_1Hz") },
            { "PlayerBans_1Hz",           (V080, "NetworkServerSettings.UpdatePlayerBans", "PlayerBans_1Hz") },
            { "Fog_SkipPresentation",     (V080, "FogOfWar.ShowFog/UpdateFogPixelBlend", "Fog_SkipPresentation") },
            { "Fog_DetectedTargetLookup", (V080, "FogOfWar.OnUpdate", "Fog_DetectedTargetLookup") },
            { "Animators_Disable",        (V080, "unit Animator patches", "Animators_Disable") },
            { "Particles_Disable",        (V080, "particle patches", "Particles_Disable") },
            { "StructureLights_Skip",     (V080, "Structure.UpdateStructureLights", "StructureLights_Skip") },
            { "MeshCombiner_Skip",        (V080, "StructureMeshCombiner.OrderedLateUpdateAll", "MeshCombiner_Skip") },
            { "SceneLOD_Skip",            (V080, "SceneLODCell.Update", "SceneLOD_Skip") },
            { "Diag_LogExceptions",       (V080, "its exception log hook", "Diag_LogExceptions") },
        };

        static bool _loaded;
        static string _version;
        static Version _v;
        static HeadlessSettings _s;
        static Dictionary<string, bool> _soCfg;
        static readonly List<string> _blocked = new List<string>();

        public static void Init(ModuleContext ctx, HeadlessSettings s)
        {
            _s = s;
            _loaded = Env.FindMelon("Server Optimizer", out _version, out _) || Env.FindMelon("Si_ServerOptimizer", out _version, out _);
            if (!_loaded) return;
            Version v = Env.ParseVersion(_version);
            if (v == null || v > V080)
            {
                ctx.Warn("Si_ServerOptimizer " + _version + " is " + (v == null ? "an unparseable version" : "newer than 0.8.0") +
                         "; treating it like 0.8.0 (every overlapping item stays off). Check its patch list.");
                v = V080;
            }
            _v = v;
            if (_v >= V080) _soCfg = ReadSoCfg(ctx);
        }

        // Call right after ctx.Item(...).
        public static void Apply(PerfItem item)
        {
            if (!_loaded || !Table.TryGetValue(item.Key, out var row) || _v < row.since) return;
            if (row.soKey != null && _soCfg != null && _soCfg.TryGetValue(row.soKey, out bool soOn) && !soOn)
            {
                item.Note("Si_ServerOptimizer " + _version + " has " + row.soKey + " = false in its cfg, so ours applies");
                return;
            }
            if (_s.AllowOverlapWithServerOptimizer)
            {
                item.Note("Si_ServerOptimizer " + _version + " also patches " + row.members + "; applied anyway (AllowOverlapWithServerOptimizer = true)");
                return;
            }
            item.Block("Si_ServerOptimizer " + _version + " already patches " + row.members);
            _blocked.Add(item.Key);
        }

        public static void LogSummary(ModuleContext ctx)
        {
            if (!_loaded)
            {
                ctx.Note("Si_ServerOptimizer not loaded: no overlap checks needed");
                return;
            }
            string line = "Si_ServerOptimizer " + _version + " loaded: " + (_blocked.Count == 0
                ? "no overlapping item blocked"
                : _blocked.Count + " overlapping item(s) left off so nothing is patched twice: " + string.Join(", ", _blocked));
            ctx.Note(line);
            ctx.Log(line);
        }

        // MelonPreferences TOML, category [ServerOptimizer]: lines "Key = true|false".
        static Dictionary<string, bool> ReadSoCfg(ModuleContext ctx)
        {
            string path = Path.Combine(MelonEnvironment.UserDataDirectory, "cfg", "Si_ServerOptimizer", "Si_ServerOptimizer.cfg");
            try
            {
                if (!File.Exists(path)) return null;
                var d = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in File.ReadAllLines(path))
                {
                    string l = raw.Trim();
                    int eq = l.IndexOf('=');
                    if (l.Length == 0 || l[0] == '#' || l[0] == '[' || eq <= 0) continue;
                    string k = l.Substring(0, eq).Trim();
                    string val = l.Substring(eq + 1).Trim();
                    if (val == "true") d[k] = true;
                    else if (val == "false") d[k] = false;
                }
                return d;
            }
            catch (Exception ex)
            {
                ctx.Warn("cannot read " + path + ": " + ex.Message + " (all overlapping items stay off)");
                return null;
            }
        }
    }
}
