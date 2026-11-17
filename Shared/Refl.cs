/*
 Si_ServerPerf shared helpers: reflection accessors that return null instead of throwing, so a missing member makes
 the item NOT APPLIED instead of crashing Apply. Resolve once in Apply and keep the delegate in a static field.
*/

using System;
using System.Reflection;
using HarmonyLib;

namespace SiServerPerf
{
    public static class Refl
    {
        // Instance field accessor (read and write through ref). null if the field is absent or has another type;
        // with an item, the absence is listed under that item in the report.
        public static AccessTools.FieldRef<T, F> Field<T, F>(string name, PerfItem item = null)
        {
            FieldInfo f = AccessTools.Field(typeof(T), name);
            if (f == null || f.IsStatic || f.FieldType != typeof(F))
            {
                item?.Note("field " + typeof(T).Name + "." + name + " (" + typeof(F).Name + ") absent on this build");
                return null;
            }
            try { return AccessTools.FieldRefAccess<T, F>(f); }
            catch (Exception ex) { item?.Note("field " + typeof(T).Name + "." + name + ": " + ex.Message); return null; }
        }

        // Static field accessor. Use as `ref F x = ref acc();`.
        public static AccessTools.FieldRef<F> StaticField<F>(Type type, string name, PerfItem item = null)
        {
            FieldInfo f = AccessTools.Field(type, name);
            if (f == null || !f.IsStatic || f.FieldType != typeof(F))
            {
                item?.Note("static field " + type?.Name + "." + name + " (" + typeof(F).Name + ") absent on this build");
                return null;
            }
            try { return AccessTools.StaticFieldRefAccess<F>(f); }
            catch (Exception ex) { item?.Note("static field " + type?.Name + "." + name + ": " + ex.Message); return null; }
        }

        // Method lookup without counting it as a patch target (for calls / delegates / transpiler operands).
        public static MethodInfo Method(Type type, string name, Type[] args = null, PerfItem item = null)
        {
            MethodInfo m = null;
            try { m = type == null ? null : AccessTools.Method(type, name, args); } catch { }
            if (m == null) item?.Note("method " + PerfItem.Label(type, name, args) + " absent on this build");
            return m;
        }

        public static MethodInfo Getter(Type type, string property, PerfItem item = null)
        {
            MethodInfo m = null;
            try { m = type == null ? null : AccessTools.PropertyGetter(type, property); } catch { }
            if (m == null) item?.Note("property " + type?.Name + "." + property + " absent on this build");
            return m;
        }
    }
}
