/*
 Si_ServerPerf - UserData\cfg\Si_ServerPerf\Si_ServerPerf.json

 {
   "Core":    { core settings, see CoreSettings },
   "Modules": {
     "<Module>": {
       "Enabled":  true,
       "Items":    { "<ItemKey>": true, ... },       one boolean per item, default from the item declaration
       "Settings": { ... },                          the module's own settings type, defaults from its initializers
       "ItemHelp": { "<ItemKey>": "description" }    written by the core on every start, not read
     }
   }
 }
 Written with defaults on first start. On every start missing keys are added with their defaults and existing values
 are kept (unknown keys too). A file that does not parse is never overwritten: the defaults are used for that run.
*/

using System;
using System.IO;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiServerPerf
{
    public sealed class CoreSettings
    {
        // Items flagged Presentation are applied only when the server runs headless.
        public bool RequireHeadless = true;
        // Poll runtime.json for switch changes.
        public bool RuntimeFile = true;
        public float RuntimePollSeconds = 1f;
        // Apply a runtime.json that already exists when the server starts (the measurement script places it before
        // launch). Its content is logged. Set false on a live server if a stale file must never matter.
        public bool ApplyRuntimeFileAtStartup = true;
        // Write runtime_state.json (current switch states) after every runtime change.
        public bool WriteRuntimeState = true;
        // Server console command "perf" (DebugConsole; also reachable as remote admin command from a client).
        public bool ConsoleCommand = true;
        // Admin Mod chat command "!perf" (only if Admin Mod is loaded).
        public bool ChatCommand = true;
    }

    internal static class PerfConfig
    {
        static JObject _root;
        static bool _dirty;
        static bool _readOnly;

        public static CoreSettings Core = new CoreSettings();
        public static string Dir { get; private set; }
        public static string FilePath { get; private set; }
        public static string RuntimePath { get; private set; }
        public static string StatePath { get; private set; }

        static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        });

        public static void Load()
        {
            Dir = Path.Combine(MelonEnvironment.UserDataDirectory, "cfg", "Si_ServerPerf");
            FilePath = Path.Combine(Dir, "Si_ServerPerf.json");
            RuntimePath = Path.Combine(Dir, "runtime.json");
            StatePath = Path.Combine(Dir, "runtime_state.json");
            try { Directory.CreateDirectory(Dir); }
            catch (Exception ex) { MelonLogger.Warning("cannot create " + Dir + ": " + ex.Message); }

            _root = null;
            if (File.Exists(FilePath))
            {
                try
                {
                    _root = JObject.Parse(File.ReadAllText(FilePath));
                }
                catch (Exception ex)
                {
                    _readOnly = true;
                    MelonLogger.Error(FilePath + " does not parse (" + ex.Message + "); using defaults for this run and NOT overwriting the file");
                }
            }
            if (_root == null)
            {
                _root = new JObject();
                _dirty = true;
            }
            Core = ReadObject<CoreSettings>(Obj(_root, "Core"), "Core");
        }

        static JObject Obj(JObject parent, string key)
        {
            if (parent[key] is JObject o) return o;
            if (parent[key] != null)
            {
                MelonLogger.Warning("config: '" + key + "' is not an object; using defaults (file entry kept)");
                return new JObject();
            }
            o = new JObject();
            parent[key] = o;
            _dirty = true;
            return o;
        }

        public static JObject ModuleSection(string module) => Obj(Obj(_root, "Modules"), module);

        public static bool Bool(JObject section, string key, bool def)
        {
            JToken t = section[key];
            if (t == null)
            {
                section[key] = def;
                _dirty = true;
                return def;
            }
            if (t.Type == JTokenType.Boolean) return (bool)t;
            MelonLogger.Warning("config: " + key + " is not true/false; using " + def);
            return def;
        }

        public static bool ItemValue(JObject section, string key, bool def, string description, ItemFlags flags)
        {
            JObject items = Obj(section, "Items");
            bool v = Bool(items, key, def);
            JObject help = Obj(section, "ItemHelp");
            string text = description ?? "";
            if ((flags & ItemFlags.StartupOnly) != 0) text += " [startup-only]";
            if ((flags & ItemFlags.Presentation) != 0) text += " [headless only]";
            if (!def) text += " [default off]";
            if ((string)help[key] != text)
            {
                help[key] = text;
                _dirty = true;
            }
            return v;
        }

        public static T ReadSettings<T>(JObject section, string module) where T : class, new()
        {
            return ReadObject<T>(Obj(section, "Settings"), module + ".Settings");
        }

        // Merge the defaults of T into the JSON object (adding missing keys, recursively for nested objects), then
        // deserialize. A value of the wrong type falls back to the defaults for the whole object (logged, file kept).
        static T ReadObject<T>(JObject target, string label) where T : class, new()
        {
            var defaults = new T();
            try
            {
                JObject def = JObject.FromObject(defaults, Serializer);
                if (MergeMissing(target, def)) _dirty = true;
                return target.ToObject<T>(Serializer) ?? defaults;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("config " + label + ": " + ex.Message + "; using defaults for this run");
                return defaults;
            }
        }

        static bool MergeMissing(JObject target, JObject defaults)
        {
            bool changed = false;
            foreach (var p in defaults.Properties())
            {
                JToken have = target[p.Name];
                if (have == null)
                {
                    target[p.Name] = p.Value.DeepClone();
                    changed = true;
                }
                else if (have is JObject ho && p.Value is JObject po)
                {
                    changed |= MergeMissing(ho, po);
                }
            }
            return changed;
        }

        public static void SaveIfDirty()
        {
            if (!_dirty || _readOnly) return;
            try
            {
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, _root.ToString(Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
                _dirty = false;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("cannot write " + FilePath + ": " + ex.Message);
            }
        }
    }
}
