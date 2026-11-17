/*
 Register_O1 (S11): OrderedUpdateManager.UpdateList.Register / Unregister without the O(N) delegate-equality scans.

 Vanilla 1.0.17:
   Register(func, priority, startTime, everyTick): linear scan for a non-removed entry whose function == func (return
     if found); new UpdateActionData {function, callTime=startTime, priority, everyTick, targetID/targetTransform from
     func.Target as MonoBehaviour}; insert at the first index j with entries[j].priority >= priority (else Add);
     if j <= m_CurrentIndex then m_CurrentIndex++.
   Unregister(func): scan from the end; every non-removed entry with function == func is flagged removed (+ m_HasRemoved)
     while a Run is iterating (m_LoopDepth > 0), else RemoveAt.
   BaseGameObject.OnDestroy unregisters all four hooks unconditionally, so most Unregister calls find nothing.
 Here, per list: Dictionary<Action<float>, entry> of the non-removed entries (delegate Equals, hash = target reference,
 so equal delegates always collide). Register: dictionary dup test, binary search for the same insertion index (the
 list is sorted by priority ascending; removed-but-not-purged entries are included exactly like the vanilla scan),
 List.Insert (memmove), same m_CurrentIndex fix-up. Unregister: dictionary lookup, locate the entry by binary search on
 its priority + reference scan inside that priority run, then flag or RemoveAt exactly as vanilla. Execution order,
 dup semantics and removal semantics are identical, so Run sees the same list.

 Consistency guard: every list mutation the game can make goes through Register / Unregister / Run's RemoveAll of
 flagged entries. The index tracks the expected Count (+1 per insert, -1 per RemoveAt, -flagged after a purge); any
 other Count means someone else touched m_Entries, and the index is rebuilt from the list (O(N), counted). Every
 RegisterVerifyEveryN-th call is also computed the vanilla way; a mismatch logs both answers and fails the item
 (vanilla from then on). Runtime switch: off -> vanilla; on again -> the index is rebuilt on first use.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace SiServerPerf.Modules.UpdateTiers
{
    internal static class RegisterIndex
    {
        internal static bool On;
        internal static PerfItem Item;
        static int s_VerifyEvery;
        static int s_OpCounter;

        static FieldInfo s_EntriesField;
        static AccessTools.FieldRef<object, int> s_LoopDepth, s_CurrentIndex;
        static AccessTools.FieldRef<object, bool> s_HasRemoved;
        static AccessTools.FieldRef<object, Action<float>> s_Function;
        static AccessTools.FieldRef<object, int> s_Priority, s_TargetID;
        static AccessTools.FieldRef<object, bool> s_Removed, s_EveryTick;
        static AccessTools.FieldRef<object, float> s_CallTime;
        static AccessTools.FieldRef<object, Transform> s_TargetTransform;
        static ConstructorInfo s_Ctor;

        sealed class DelegateComparer : IEqualityComparer<Action<float>>
        {
            public static readonly DelegateComparer Instance = new DelegateComparer();
            public bool Equals(Action<float> a, Action<float> b) => a == b;   // vanilla's operator
            public int GetHashCode(Action<float> d)
            {
                object t = d?.Target;
                return t == null ? 0 : RuntimeHelpers.GetHashCode(t);
            }
        }

        sealed class Index
        {
            public object Owner;
            public IList Entries;
            public readonly Dictionary<Action<float>, object> Map = new Dictionary<Action<float>, object>(DelegateComparer.Instance);
            public bool Valid;
            public int Expected;
            public int Flagged;
        }

        static readonly List<Index> s_Indexes = new List<Index>();
        static Index s_Last;

        // stats
        internal static long Registers, Unregisters, Dups, Misses, Rebuilds, OutOfStep, Verified, Ticks;

        public static void Register(ModuleContext ctx, UpdateTiersSettings s)
        {
            s_VerifyEvery = Math.Max(0, s.RegisterVerifyEveryN);
            Item = ctx.Item("Register_O1",
                "S11: UpdateList.Register/Unregister via a delegate dictionary + binary search (identical order and dup/removal semantics); every " +
                s_VerifyEvery + "th call cross-checked against the vanilla scan",
                v => { On = v; InvalidateAll(); },
                defaultOn: true);

            Type list = TierDispatch.ListType ?? AccessTools.Inner(typeof(OrderedUpdateManager), "UpdateList");
            Type entry = TierDispatch.EntryType ?? AccessTools.TypeByName("UpdateActionData");
            var missing = new List<string>();
            if (list == null || entry == null)
            {
                Item.Block("OrderedUpdateManager.UpdateList / UpdateActionData absent");
                return;
            }
            s_EntriesField = AccessTools.Field(list, "m_Entries");
            if (s_EntriesField == null || !typeof(IList).IsAssignableFrom(s_EntriesField.FieldType)) missing.Add("UpdateList.m_Entries");
            s_LoopDepth = Ref<int>(list, "m_LoopDepth", missing);
            s_CurrentIndex = Ref<int>(list, "m_CurrentIndex", missing);
            s_HasRemoved = Ref<bool>(list, "m_HasRemoved", missing);
            s_Function = Ref<Action<float>>(entry, "function", missing);
            s_Priority = Ref<int>(entry, "priority", missing);
            s_TargetID = Ref<int>(entry, "targetID", missing);
            s_Removed = Ref<bool>(entry, "removed", missing);
            s_EveryTick = Ref<bool>(entry, "everyTick", missing);
            s_CallTime = Ref<float>(entry, "callTime", missing);
            s_TargetTransform = Ref<Transform>(entry, "targetTransform", missing);
            s_Ctor = AccessTools.Constructor(entry, Type.EmptyTypes);
            if (s_Ctor == null) missing.Add("UpdateActionData()");
            // every field of UpdateActionData must be one we set, or a new field would be left at its default
            foreach (FieldInfo f in entry.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                switch (f.Name)
                {
                    case "function": case "targetTransform": case "targetID": case "priority": case "callTime":
                    case "everyTick": case "removed": case "perfSection":
                        break;
                    default:
                        missing.Add("unknown UpdateActionData field '" + f.Name + "' (the Register replica would not set it)");
                        break;
                }
            }
            if (missing.Count > 0)
            {
                Item.Block(string.Join(", ", missing));
            }
            var reg = Item.Target(list, "Register", new[] { typeof(Action<float>), typeof(int), typeof(float), typeof(bool) }, declaredOnly: true);
            var unreg = Item.Target(list, "Unregister", new[] { typeof(Action<float>) }, declaredOnly: true);
            Item.Patch(reg, typeof(RegisterIndex), prefix: nameof(RegisterPrefix));
            Item.Patch(unreg, typeof(RegisterIndex), prefix: nameof(UnregisterPrefix));
        }

        static AccessTools.FieldRef<object, F> Ref<F>(Type type, string name, List<string> missing)
        {
            FieldInfo f = AccessTools.Field(type, name);
            if (f == null || f.IsStatic || f.FieldType != typeof(F)) { missing.Add(type.Name + "." + name); return null; }
            try { return AccessTools.FieldRefAccess<F>(type, name); }
            catch (Exception ex) { missing.Add(type.Name + "." + name + " (" + ex.Message + ")"); return null; }
        }

        static void InvalidateAll()
        {
            foreach (Index ix in s_Indexes) ix.Valid = false;
        }

        static Index Get(object owner)
        {
            Index ix = s_Last;
            if (ix == null || !ReferenceEquals(ix.Owner, owner))
            {
                ix = null;
                for (int i = 0; i < s_Indexes.Count; i++)
                {
                    if (ReferenceEquals(s_Indexes[i].Owner, owner)) { ix = s_Indexes[i]; break; }
                }
                if (ix == null)
                {
                    if (s_Indexes.Count >= 8) s_Indexes.Clear();     // lists of destroyed managers
                    ix = new Index { Owner = owner, Entries = (IList)s_EntriesField.GetValue(owner) };
                    s_Indexes.Add(ix);
                }
                s_Last = ix;
            }
            IList list = ix.Entries;
            int c = list.Count;
            if (!ix.Valid)
            {
                Build(ix);
            }
            else if (c != ix.Expected)
            {
                if (ix.Flagged > 0 && c == ix.Expected - ix.Flagged)
                {
                    // Run purged our flagged entries (RemoveAll at the end of the loop that flagged them). Detected on the
                    // first call after the purge - typically from inside the next Run - and before any new flag is added.
                    ix.Expected = c;
                    ix.Flagged = 0;
                }
                else
                {
                    OutOfStep++; Build(ix);                                        // someone else changed the list
                }
            }
            else if (ix.Flagged > 0 && s_LoopDepth(owner) == 0 && !s_HasRemoved(owner))
            {
                ix.Flagged = 0;                                       // purge happened and inserts made up the count
                OutOfStep++; Build(ix);
            }
            return ix;
        }

        static void Build(Index ix)
        {
            Rebuilds++;
            ix.Map.Clear();
            IList list = ix.Entries;
            int flagged = 0;
            for (int i = 0; i < list.Count; i++)
            {
                object e = list[i];
                if (e == null) continue;
                if (s_Removed(e)) { flagged++; continue; }
                Action<float> fn = s_Function(e);
                if (fn != null) ix.Map[fn] = e;
            }
            ix.Expected = list.Count;
            ix.Flagged = flagged;
            ix.Valid = true;
        }

        // first index j with priority[j] >= p (list sorted ascending), Count if none
        static int LowerBound(IList list, int p)
        {
            int lo = 0, hi = list.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (s_Priority(list[mid]) >= p) hi = mid; else lo = mid + 1;
            }
            return lo;
        }

        static int Locate(IList list, object e)
        {
            int p = s_Priority(e);
            for (int i = LowerBound(list, p); i < list.Count; i++)
            {
                object x = list[i];
                if (ReferenceEquals(x, e)) return i;
                if (s_Priority(x) != p) break;
            }
            return -1;
        }

        static bool Sample()
        {
            if (s_VerifyEvery <= 0) return false;
            if (++s_OpCounter >= s_VerifyEvery) { s_OpCounter = 0; return true; }
            return false;
        }

        // ------------------------------------------------------------------ vanilla replicas (verification only)

        static bool VanillaDup(IList list, Action<float> func)
        {
            for (int i = 0; i < list.Count; i++)
            {
                object e = list[i];
                if (!s_Removed(e) && s_Function(e) == func) return true;
            }
            return false;
        }

        static int VanillaInsertIndex(IList list, int priority)
        {
            for (int j = 0; j < list.Count; j++)
            {
                if (s_Priority(list[j]) >= priority) return j;
            }
            return list.Count;
        }

        static int VanillaFindLast(IList list, Action<float> func)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                object e = list[i];
                if (!s_Removed(e) && !(s_Function(e) != func)) return i;
            }
            return -1;
        }

        static bool Mismatch(string what)
        {
            Item.Fail(new InvalidOperationException("Register_O1 verification mismatch: " + what), "verification");
            return true;     // vanilla handles this call
        }

        // ------------------------------------------------------------------ prefixes

        static bool RegisterPrefix(object __instance, Action<float> func, int priority, float startTime, bool everyTick)
        {
            if (!On || func == null) return true;
            long t0 = Stopwatch.GetTimestamp();
            bool mutated = false;
            try
            {
                Index ix = Get(__instance);
                IList list = ix.Entries;
                bool verify = Sample();

                bool dup = false;
                if (ix.Map.TryGetValue(func, out object found))
                {
                    if (!s_Removed(found) && Locate(list, found) >= 0) dup = true;
                    else ix.Map.Remove(func);
                }
                int j = dup ? -1 : LowerBound(list, priority);
                if (verify)
                {
                    Verified++;
                    bool vdup = VanillaDup(list, func);
                    if (vdup != dup) return Mismatch("Register dup test: vanilla " + vdup + ", index " + dup + " (" + Describe(func) + ")");
                    if (!dup)
                    {
                        int vj = VanillaInsertIndex(list, priority);
                        if (vj != j) return Mismatch("Register insert index: vanilla " + vj + ", index " + j + " (" + Describe(func) + ")");
                    }
                }
                Registers++;
                if (dup)
                {
                    Dups++;
                    return false;
                }

                object e = s_Ctor.Invoke(null);
                s_Function(e) = func;
                s_CallTime(e) = startTime;
                s_Priority(e) = priority;
                s_EveryTick(e) = everyTick;
                MonoBehaviour mb = func.Target as MonoBehaviour;
                s_TargetID(e) = mb ? mb.gameObject.GetInstanceID() : 0;
                s_TargetTransform(e) = mb ? mb.gameObject.transform : null;

                mutated = true;
                list.Insert(j, e);
                ix.Expected++;
                ref int cur = ref s_CurrentIndex(__instance);
                if (j <= cur) cur++;
                ix.Map[func] = e;
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "UpdateList.Register prefix");
                InvalidateAll();
                return !mutated;     // never insert twice
            }
            finally
            {
                Ticks += Stopwatch.GetTimestamp() - t0;
            }
        }

        static bool UnregisterPrefix(object __instance, Action<float> func)
        {
            if (!On || func == null) return true;
            long t0 = Stopwatch.GetTimestamp();
            bool mutated = false;
            try
            {
                Index ix = Get(__instance);
                IList list = ix.Entries;
                int idx = -1;
                bool inMap = ix.Map.TryGetValue(func, out object e);
                if (inMap && !s_Removed(e)) idx = Locate(list, e);
                if (Sample())
                {
                    Verified++;
                    int v = VanillaFindLast(list, func);
                    if (v != idx) return Mismatch("Unregister index: vanilla " + v + ", index " + idx + " (" + Describe(func) + ")");
                }
                Unregisters++;
                if (idx < 0)
                {
                    if (inMap) ix.Map.Remove(func);
                    Misses++;
                    return false;
                }
                mutated = true;
                if (s_LoopDepth(__instance) > 0)
                {
                    s_Removed(e) = true;
                    s_HasRemoved(__instance) = true;
                    ix.Flagged++;
                }
                else
                {
                    list.RemoveAt(idx);
                    ix.Expected--;
                }
                ix.Map.Remove(func);
                return false;
            }
            catch (Exception ex)
            {
                Item.Fail(ex, "UpdateList.Unregister prefix");
                InvalidateAll();
                return !mutated;
            }
            finally
            {
                Ticks += Stopwatch.GetTimestamp() - t0;
            }
        }

        static string Describe(Action<float> f)
        {
            try { return f.Method.DeclaringType?.Name + "." + f.Method.Name; } catch { return "?"; }
        }
    }
}
