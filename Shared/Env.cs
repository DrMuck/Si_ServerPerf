/*
 Si_ServerPerf shared helpers: headless detection and "is melon X loaded, which version".
*/

using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace SiServerPerf
{
    public static class Env
    {
        static bool? _headless;
        static string _reason = "";

        // Application.isBatchMode or graphics device Null. If Unity cannot answer yet, assume headless (this mod is
        // for the dedicated server only).
        public static bool Headless
        {
            get
            {
                if (_headless == null)
                {
                    bool batch = false;
                    string gfx = "?";
                    try
                    {
                        batch = Application.isBatchMode;
                        GraphicsDeviceType type = SystemInfo.graphicsDeviceType;
                        gfx = type.ToString();
                        _headless = batch || type == GraphicsDeviceType.Null;
                    }
                    catch (Exception ex)
                    {
                        gfx = "unavailable (" + ex.GetType().Name + ")";
                        _headless = true;
                    }
                    _reason = "batchmode=" + batch + ", graphics=" + gfx;
                }
                return _headless.Value;
            }
        }

        public static string HeadlessReason { get { bool _ = Headless; return _reason; } }

        // Finds a loaded melon by its MelonInfo name (e.g. "Server Optimizer", "Admin Mod") or by assembly name
        // (e.g. "Si_ServerOptimizer"). Reads the MelonInfo attribute of every loaded assembly, so it works before that
        // melon has been registered (load order does not matter). Returns false if not loaded.
        public static bool FindMelon(string name, out string version, out string assemblyName)
        {
            version = null;
            assemblyName = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                MelonInfoAttribute info = null;
                try { info = asm.GetCustomAttribute<MelonInfoAttribute>(); }
                catch { }
                if (info == null) continue;
                string asmName = asm.GetName().Name;
                if (string.Equals(info.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(asmName, name, StringComparison.OrdinalIgnoreCase))
                {
                    version = info.Version;
                    assemblyName = asmName;
                    return true;
                }
            }
            return false;
        }

        // Set by the core at startup for the report header.
        public static string ServerOptimizerLine = "Si_ServerOptimizer: not loaded";

        internal static void DetectServerOptimizer()
        {
            ServerOptimizerLine = FindMelon("Server Optimizer", out string v, out string a) || FindMelon("Si_ServerOptimizer", out v, out a)
                ? "Si_ServerOptimizer: " + v + " loaded (" + a + ")"
                : "Si_ServerOptimizer: not loaded";
        }

        // Lenient version compare: "0.7.1" vs "0.8.0". Unparseable -> null.
        public static Version ParseVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return null;
            string s = v.Trim().TrimStart('v', 'V');
            int cut = s.IndexOfAny(new[] { '-', '+', ' ' });
            if (cut > 0) s = s.Substring(0, cut);
            return Version.TryParse(s, out Version r) ? r : null;
        }
    }
}
