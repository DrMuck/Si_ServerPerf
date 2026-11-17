/*
 S1 tier dispatcher: one transpiler on OrderedUpdateManager.UpdateList.Run (all four lists share the class).

 1.0.17 IL of Run, per entry: load entry (local 1) -> `if (entry.removed) { m_CurrentIndex--; continue; }` ->
 the dead skip `if (skipEnabled && !entry.everyTick && ... % skipFrequency != 0) { m_CurrentIndex--; continue; }` ->
 PerfSections.Begin; entry.function(now - entry.callTime) in try/catch/finally { entry.callTime = now; ... }.
 Injected right before the dead skip test (its labels moved to the injection):
     if (TierDispatch.SkipStep(this, entry, this.m_CurrentIndex)) goto <loop condition>;
 SkipStep does `m_CurrentIndex = index - 1` itself when it returns true, i.e. exactly the removed / dead-skip
 `continue` bodies; the branch target is the loop-condition label those bodies already use. The pattern check also
 requires the dead skip body to be that same `m_CurrentIndex--; br <loop condition>`.
 A skipped entry keeps its callTime, so its next run receives the accumulated dt (vanilla arithmetic, nothing of
 ours). Removal flags, priority order, re-entrancy and the index fix-up on Register during the loop are untouched.
 Pattern not found exactly -> Item.Problem, IL unchanged, all dispatcher items NOT APPLIED / SKIPPED.

 Hot path (runs for every entry of every list, ~10 000 calls per frame at 500 units): one bool, one reference
 compare for the list, one array load + reference compare for the per-entry slot cached at the entry's index. Only
 on a miss (new entry, or indices shifted by an insert/remove) a dictionary lookup by entry reference, and only for a
 new entry a classification (delegate method handle -> tier). Untiered entries cost exactly that fast path.

 Tiered entry, per call: countdown--; due (countdown hit 0) -> run and restart the countdown; not due -> skip only if
 its quiet test holds now, otherwise run (counted "early"). Phases: each new entry starts at a round-robin offset, so
 the 1/N that runs on a given call is spread evenly. Entries registered with everyTick (the developer's "run every
 tick" flag) are never tiered.

 Tiers (classification by the delegate's exact method, so a modded subclass with its own override is not tiered):
  Tier_Structures   update list: Structure / HumanStructure / AlienStructure / ConstructionSite.OrderedUpdate.
     Body: navmesh requeue flag, production queue, UpdateStructureLights (presentation), heal/decay enable sync,
     human relink (when dirty), alien link init/DFS (when dirty), AlienStructureFX cyst regen (dt-scaled visual).
     Quiet = no navmesh requeue pending, production queue empty, no relink pending (human), link initialised and not
     dirty (alien). So skipped calls only defer the lights sweep, the heal/decay enable re-sync (re-done by the
     link code itself on change) and the cyst scale animation.
  Tier_HealDecay    update list: AutoHeal / Decay.OrderedUpdate. dt-scaled timers (heal tick 5 s, decay tick 5 s,
     delays 10 s); a skipped call shifts a tick by < N frames. The damage check (health < LastHealth -> restart heal
     delay) runs before the timer subtraction, so a heal can never fire in a window where vanilla would not.
     Quiet = owner is not a unit a player controls or sits in.
  Tier_OrderProcessors update list: AIOrderProcessor.OrderedUpdate. With an empty queue the body is only
     NotifyOrdersEmptied -> MoveSpeedScale = 1 (idempotent). New orders execute in IssueOrder itself, not here.
     Quiet = Orders.Count == 0 and no player controls / sits in the owner.
  Tier_IdleSoldiers update list: Soldier.OrderedUpdate (body yaw/model rotation, animator params, FPS hands, footsteps,
     sights). Quiet = Idle.SoldierIdle (see Idle.cs): idle AI soldier, m_InputRotation zero (UpdateFPSHands
     integrates it per call), grounded, not firing / reloading, not moving.
  Tier_StaticNetTransforms fixed list: NetworkTransformComponent.OrderedFixedUpdate of objects WITHOUT a Rigidbody
     (structures etc.; units always have one and stay at full rate). On the server that is a tolerance poll that
     sends RPC_StaticTransformChanged when a static object moved. Quiet = net_HasChanged false.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Silica.AI;

namespace SiServerPerf.Modules.UpdateTiers
{
    internal static class TierDispatch
    {
        internal const int T_Structures = 0, T_HealDecay = 1, T_OrderProcessors = 2, T_IdleSoldiers = 3, T_StaticNet = 4;
        internal static readonly Tier[] Tiers = new Tier[5];

        // Any dispatcher tier on (fast exit), and the idle-soldier tier on (for the wake registry).
        static bool s_Any;
        internal static bool UnitTierOn;
        static bool s_Dead;

        // list kinds
        const int K_Fixed = 0, K_FixedLate = 1, K_Update = 2, K_Late = 3;

        internal static Type ListType, EntryType;
        static AccessTools.FieldRef<object, Action<float>> s_Function;
        static AccessTools.FieldRef<object, bool> s_EveryTick;
        static FieldInfo s_CurrentIndex;
        static AccessTools.FieldRef<object, int> s_CurrentIndexRef;
        static FieldInfo s_FixedField, s_FixedLateField, s_UpdateField, s_LateField;
        static PerfItem s_Owner;      // item that installed the transpiler
        static MethodInfo s_RunMethod;

        // method handle -> tier id, per list kind
        static readonly Dictionary<IntPtr, int>[] s_MethodTier = { new Dictionary<IntPtr, int>(), new Dictionary<IntPtr, int>(), new Dictionary<IntPtr, int>(), new Dictionary<IntPtr, int>() };
        // method handle -> structure kind (0 Structure, 1 Human, 2 Alien, 3 ConstructionSite)
        static readonly Dictionary<IntPtr, int> s_StructKind = new Dictionary<IntPtr, int>();

        internal sealed class Slot
        {
            public object Entry;
            public Tier Tier;            // null = untiered
            public int TierId;
            public int Countdown;
            public int SubKind;
            public BaseGameObject Obj;   // Structure / ConstructionSite / owner
            public UnityEngine.Component Comp;
            public Unit Unit;
            public WakeRegistry.UnitRec Rec;
        }

        sealed class ListState
        {
            public object List;
            public int Kind;
            public Slot[] Slots = new Slot[512];
            public readonly Dictionary<object, Slot> Map = new Dictionary<object, Slot>(RefComparer<object>.Instance);
        }

        static ListState s_Last;
        static readonly List<ListState> s_States = new List<ListState>();
        static int s_Phase;
        internal static long Misses, Classifications, MapRebuilds;

        // ------------------------------------------------------------------ registration

        public static void Register(ModuleContext ctx, UpdateTiersSettings s)
        {
            Tiers[T_Structures] = new Tier("Tier_Structures", s.StructuresInterval);
            Tiers[T_HealDecay] = new Tier("Tier_HealDecay", s.HealDecayInterval);
            Tiers[T_OrderProcessors] = new Tier("Tier_OrderProcessors", s.OrderProcessorsInterval);
            Tiers[T_IdleSoldiers] = new Tier("Tier_IdleSoldiers", s.IdleSoldiersInterval);
            Tiers[T_StaticNet] = new Tier("Tier_StaticNetTransforms", s.StaticNetTransformsInterval);

            Declare(ctx, T_Structures,
                "Update list: Structure/HumanStructure/AlienStructure/ConstructionSite.OrderedUpdate every " + Tiers[T_Structures].Interval +
                " frames while quiet (no production queue, no navmesh requeue, no relink pending); accumulated dt", true);
            Declare(ctx, T_HealDecay,
                "Update list: AutoHeal/Decay.OrderedUpdate every " + Tiers[T_HealDecay].Interval + " frames (dt-scaled 5 s ticks); full rate on player-controlled/occupied units", true);
            Declare(ctx, T_OrderProcessors,
                "Update list: AIOrderProcessor.OrderedUpdate every " + Tiers[T_OrderProcessors].Interval + " frames while its order queue is empty (body is idempotent then)", true);
            Declare(ctx, T_IdleSoldiers,
                "Update list: Soldier.OrderedUpdate every " + Tiers[T_IdleSoldiers].Interval + " frames for idle AI soldiers (no target/path/order/damage/aim-at, not moving, no view input)", true);
            Declare(ctx, T_StaticNet,
                "Fixed list: NetworkTransformComponent.OrderedFixedUpdate of objects without Rigidbody every " + Tiers[T_StaticNet].Interval + " steps while no change is pending", true);

            // ---- reflection
            var missing = Idle.Init();
            ListType = AccessTools.Inner(typeof(OrderedUpdateManager), "UpdateList");
            EntryType = AccessTools.TypeByName("UpdateActionData");
            if (ListType != null && EntryType != null)
            {
                s_Function = TryFieldRef<Action<float>>(EntryType, "function", missing);
                s_EveryTick = TryFieldRef<bool>(EntryType, "everyTick", missing);
                s_CurrentIndex = AccessTools.Field(ListType, "m_CurrentIndex");
                if (s_CurrentIndex == null || s_CurrentIndex.FieldType != typeof(int)) { missing.Add("UpdateList.m_CurrentIndex"); s_CurrentIndex = null; }
                else s_CurrentIndexRef = TryFieldRef<int>(ListType, "m_CurrentIndex", missing);
            }
            else
            {
                missing.Add(ListType == null ? "OrderedUpdateManager.UpdateList" : "UpdateActionData");
            }
            s_FixedField = ListField("FixedUpdate_List", missing);
            s_FixedLateField = ListField("FixedLateUpdate_List", missing);
            s_UpdateField = ListField("Update_List", missing);
            s_LateField = ListField("LateUpdate_List", missing);

            // ---- tier tables (method handles)
            AddMethod(K_Update, typeof(Structure), "OrderedUpdate", T_Structures, 0, missing);
            AddMethod(K_Update, typeof(HumanStructure), "OrderedUpdate", T_Structures, 1, missing);
            AddMethod(K_Update, typeof(AlienStructure), "OrderedUpdate", T_Structures, 2, missing);
            AddMethod(K_Update, typeof(ConstructionSite), "OrderedUpdate", T_Structures, 3, missing);
            AddMethod(K_Update, typeof(AutoHeal), "OrderedUpdate", T_HealDecay, 0, missing);
            AddMethod(K_Update, typeof(Decay), "OrderedUpdate", T_HealDecay, 0, missing);
            AddMethod(K_Update, typeof(AIOrderProcessor), "OrderedUpdate", T_OrderProcessors, 0, missing);
            AddMethod(K_Update, typeof(Soldier), "OrderedUpdate", T_IdleSoldiers, 0, missing);
            AddMethod(K_Fixed, typeof(NetworkTransformComponent), "OrderedFixedUpdate", T_StaticNet, 0, missing);

            bool core = ListType != null && EntryType != null && s_Function != null && s_EveryTick != null && s_CurrentIndex != null && s_CurrentIndexRef != null &&
                        s_FixedField != null && s_FixedLateField != null && s_UpdateField != null && s_LateField != null;
            if (!core)
            {
                foreach (Tier t in Tiers) t.Item.Block("dispatcher members absent on this build: " + string.Join(", ", missing));
                return;
            }
            if (!Idle.FieldsFor(0)) Tiers[T_Structures].Item.Block("structure fields absent: " + string.Join(", ", missing));
            if (!Idle.FieldsFor(2)) Tiers[T_IdleSoldiers].Item.Block("unit/agent fields absent: " + string.Join(", ", missing));
            if (!Idle.FieldsFor(3)) Tiers[T_StaticNet].Item.Block("NetworkTransformComponent.net_HasChanged absent");
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (!s_MethodTierHas(i)) Tiers[i].Item.Block("handler methods absent: " + string.Join(", ", missing));
            }

            // ---- the shared transpiler: installed by the first active item, the others are hooked to it
            s_RunMethod = AccessTools.DeclaredMethod(ListType, "Run", new[] { typeof(float), typeof(bool), typeof(long), typeof(float), typeof(int) });
            bool installed = false, failed = false;
            string owner = null;
            foreach (Tier t in Tiers)
            {
                PerfItem it = t.Item;
                if (installed)
                {
                    it.Hooked("UpdateList.Run (shared tier dispatcher, installed by " + owner + ")");
                    continue;
                }
                if (failed)
                {
                    it.Block("shared UpdateList.Run transpiler not applied (see " + owner + ")");
                    it.Hooked("UpdateList.Run (shared tier dispatcher)");
                    continue;
                }
                if (!it.Active)
                {
                    continue;
                }
                var run = it.Target(s_RunMethod, "OrderedUpdateManager.UpdateList.Run(Single,Boolean,Int64,Single,Int32)");
                s_Owner = it;
                bool ok = it.Patch(run, typeof(TierDispatch), transpiler: nameof(RunTranspiler));
                owner = it.Id;
                if (ok) installed = true; else failed = true;
            }
            if (!installed && !failed)
            {
                // nothing active: still list the target under the first item so the report shows it resolves
                Tiers[0].Item.Target(s_RunMethod, "OrderedUpdateManager.UpdateList.Run(Single,Boolean,Int64,Single,Int32)");
            }
            Tiers[T_IdleSoldiers].Item.Note("wake-ups: damage by enemy (WakeAfterDamageSeconds), enemy aiming at it, new enemy detection within WakeOnDetectRadius, any order/path/target");
        }

        static void Declare(ModuleContext ctx, int id, string desc, bool defaultOn)
        {
            Tier t = Tiers[id];
            t.Item = ctx.Item(t.Key, desc, v => { t.On = v; Recompute(); }, defaultOn);
        }

        static bool s_MethodTierHas(int tier)
        {
            foreach (var d in s_MethodTier) foreach (var kv in d) if (kv.Value == tier) return true;
            return false;
        }

        static void Recompute()
        {
            bool any = false;
            foreach (Tier t in Tiers) if (t != null && t.On) any = true;
            s_Any = any && !s_Dead;
            UnitTierOn = Tiers[T_IdleSoldiers] != null && Tiers[T_IdleSoldiers].On;
        }

        static AccessTools.FieldRef<object, F> TryFieldRef<F>(Type type, string name, List<string> missing)
        {
            FieldInfo f = AccessTools.Field(type, name);
            if (f == null || f.IsStatic || f.FieldType != typeof(F)) { missing.Add(type.Name + "." + name); return null; }
            try { return AccessTools.FieldRefAccess<F>(type, name); }
            catch (Exception ex) { missing.Add(type.Name + "." + name + " (" + ex.Message + ")"); return null; }
        }

        static FieldInfo ListField(string name, List<string> missing)
        {
            FieldInfo f = AccessTools.Field(typeof(OrderedUpdateManager), name);
            if (f == null || ListType == null || f.FieldType != ListType) { missing.Add("OrderedUpdateManager." + name); return null; }
            return f;
        }

        static void AddMethod(int kind, Type type, string name, int tier, int subKind, List<string> missing)
        {
            MethodInfo m = AccessTools.DeclaredMethod(type, name, new[] { typeof(float) });
            if (m == null) { missing.Add(type.Name + "." + name + "(Single)"); return; }
            IntPtr h = m.MethodHandle.Value;
            s_MethodTier[kind][h] = tier;
            if (tier == T_Structures) s_StructKind[h] = subKind;
        }

        internal static void FailTier(int id, Exception ex, string where)
        {
            Tier t = Tiers[id];
            if (t?.Item != null) t.Item.Fail(ex, where);
        }

        static void FailAll(Exception ex, string where)
        {
            s_Dead = true;
            s_Any = false;
            foreach (Tier t in Tiers) t?.Item?.Fail(ex, where);
        }

        // ------------------------------------------------------------------ transpiler

        static bool IsBrfalse(CodeInstruction ci) => ci.opcode == OpCodes.Brfalse || ci.opcode == OpCodes.Brfalse_S;
        static bool IsBr(CodeInstruction ci) => ci.opcode == OpCodes.Br || ci.opcode == OpCodes.Br_S;
        // Label is not named in code: the net35 Harmony's Label (mscorlib) and netstandard's do not unify at compile time.
        static bool IsLabel(object o) => o != null && o.GetType().FullName == "System.Reflection.Emit.Label";
        // Index of the instruction carrying this label, -1 if none. CodeInstruction.labels is read by reflection for the
        // same reason (its List<Label> type cannot be named at compile time); transpile time only.
        static readonly FieldInfo s_LabelsField = AccessTools.Field(typeof(CodeInstruction), "labels");
        static int TargetIndex(List<CodeInstruction> code, object label)
        {
            if (!IsLabel(label) || s_LabelsField == null) return -1;
            for (int i = 0; i < code.Count; i++)
            {
                if (s_LabelsField.GetValue(code[i]) is System.Collections.IList ls && ls.Contains(label)) return i;
            }
            return -1;
        }
        // by name + declaring type: FieldInfo instances from the IL reader and from AccessTools need not be the same object
        static bool IsCurrentIndex(object o) => o is FieldInfo f && f.Name == "m_CurrentIndex" && f.DeclaringType == ListType;

        static CodeInstruction LoadFromStore(CodeInstruction st)
        {
            if (st.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (st.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (st.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (st.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            if (st.opcode == OpCodes.Stloc_S) return new CodeInstruction(OpCodes.Ldloc_S, st.operand);
            if (st.opcode == OpCodes.Stloc) return new CodeInstruction(OpCodes.Ldloc, st.operand);
            return null;
        }

        static IEnumerable<CodeInstruction> RunTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            try
            {
                // 1. the entry local: List<UpdateActionData>.get_Item followed by stloc
                int getItem = -1, getItemCount = 0;
                for (int i = 0; i < code.Count - 1; i++)
                {
                    if ((code[i].opcode == OpCodes.Callvirt || code[i].opcode == OpCodes.Call) && code[i].operand is MethodInfo mi &&
                        mi.Name == "get_Item" && mi.DeclaringType != null && mi.DeclaringType.IsGenericType &&
                        mi.DeclaringType.GetGenericArguments()[0] == EntryType)
                    {
                        getItem = i;
                        getItemCount++;
                    }
                }
                CodeInstruction ldEntry = getItemCount == 1 ? LoadFromStore(code[getItem + 1]) : null;
                if (ldEntry == null)
                {
                    s_Owner.Problem("UpdateList.Run: expected one m_Entries[i] load into a local, found " + getItemCount);
                    return code;
                }
                // 2. the dead skip test: `ldarg.2 (skipEnabled); brfalse L_run` - skipEnabled is used exactly once
                int a = -1, ldarg2 = 0;
                for (int i = 0; i < code.Count; i++)
                {
                    if (code[i].opcode == OpCodes.Ldarg_2) { ldarg2++; a = i; }
                }
                if (ldarg2 != 1 || a < 1 || a + 1 >= code.Count || !IsBrfalse(code[a + 1]) || !IsLabel(code[a + 1].operand) || a <= getItem)
                {
                    s_Owner.Problem("UpdateList.Run: expected one `ldarg.2; brfalse` (skipEnabled test) after the entry load");
                    return code;
                }
                object lRun = code[a + 1].operand;
                // the removed-entry block right before it ends with `br L_loop` (m_CurrentIndex--; continue)
                if (!IsBr(code[a - 1]) || !IsLabel(code[a - 1].operand))
                {
                    s_Owner.Problem("UpdateList.Run: expected the removed-entry `continue` (br) right before the skip test");
                    return code;
                }
                object lLoop = code[a - 1].operand;
                // Harmony's reader gives every branch its own Label, so branches are compared by target instruction.
                int runIdx = TargetIndex(code, lRun), loopIdx = TargetIndex(code, lLoop);
                // 3. the skip test's last condition `rem; brfalse L_run`, then the skip body
                //    ldarg.0; ldarg.0; ldfld m_CurrentIndex; ldc.i4.1; sub; stfld m_CurrentIndex; br L_loop
                int b = -1;
                for (int i = a + 2; i < code.Count - 1 && runIdx >= 0; i++)
                {
                    if (IsBrfalse(code[i]) && code[i - 1].opcode == OpCodes.Rem && TargetIndex(code, code[i].operand) == runIdx)
                    {
                        b = i;
                        break;
                    }
                }
                if (b < 0 || loopIdx < 0 || b + 7 >= code.Count || code[b + 1].opcode != OpCodes.Ldarg_0 ||
                    !(code[b + 3].opcode == OpCodes.Ldfld && IsCurrentIndex(code[b + 3].operand)) ||
                    !(code[b + 6].opcode == OpCodes.Stfld && IsCurrentIndex(code[b + 6].operand)) ||
                    !IsBr(code[b + 7]) || TargetIndex(code, code[b + 7].operand) != loopIdx || runIdx != b + 8)
                {
                    s_Owner.Problem("UpdateList.Run: dead skip body (`m_CurrentIndex--; continue`) not found after the skip test");
                    return code;
                }

                // if (SkipStep(this, entry, m_CurrentIndex)) goto L_loop;   (SkipStep does the m_CurrentIndex-- itself)
                // The labels of the ldarg.2 (target of the removed-entry test) must land on the injection: rather than
                // moving them (List<Label> cannot be named here, see IsLabel), the labelled instruction object itself
                // becomes the injection's first instruction and a fresh ldarg.2 follows the injection.
                CodeInstruction first = code[a];
                var ldarg2Copy = new CodeInstruction(first.opcode, first.operand);
                first.opcode = OpCodes.Ldarg_0;
                first.operand = null;
                var inject = new List<CodeInstruction>
                {
                    ldEntry,
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, s_CurrentIndex),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TierDispatch), nameof(SkipStep))),
                    new CodeInstruction(OpCodes.Brtrue, lLoop),
                    ldarg2Copy,
                };
                code.InsertRange(a + 1, inject);
                return code;
            }
            catch (Exception ex)
            {
                s_Owner.Problem("UpdateList.Run transpiler threw: " + ex.Message);
                return new List<CodeInstruction>(instructions);
            }
        }

        // ------------------------------------------------------------------ hot path (called from transpiled Run)

        // Called for every non-removed entry. true = skip it: m_CurrentIndex is stepped here (vanilla's `continue` body).
        public static bool SkipStep(object list, object entry, int index)
        {
            if (!s_Any) return false;
            Tier t = null;
            try
            {
                ListState st = s_Last;
                if (st == null || !ReferenceEquals(st.List, list))
                {
                    st = GetState(list);
                    s_Last = st;
                }
                if (st.Kind < 0) return false;
                Slot[] arr = st.Slots;
                Slot slot = (uint)index < (uint)arr.Length ? arr[index] : null;
                if (slot == null || !ReferenceEquals(slot.Entry, entry)) slot = Resolve(st, entry, index);
                t = slot.Tier;
                if (t == null || !t.On) return false;
                if (--slot.Countdown <= 0)
                {
                    slot.Countdown = t.Interval;
                    t.Runs++;
                    return false;
                }
                Idle.Why = Idle.W_Other;
                if (!Quiet(slot))
                {
                    t.Busy++;
                    t.Why[Idle.Why]++;
                    return false;
                }
                s_CurrentIndexRef(list) = index - 1;     // vanilla `continue` body: m_CurrentIndex--
                t.Skips++;
                return true;
            }
            catch (Exception ex)
            {
                if (t != null) t.Item.Fail(ex, "quiet test (" + t.Key + ")");
                else FailAll(ex, "tier dispatcher");
                return false;
            }
        }

        static bool Quiet(Slot s)
        {
            switch (s.TierId)
            {
                case T_Structures:
                    return Idle.StructureQuiet(s.Obj, s.SubKind);
                case T_HealDecay:
                    {
                        Unit u = s.Unit;
                        if ((object)u != null && u && Idle.PlayerInvolved(u)) return false;
                        return (object)s.Comp != null && s.Comp;
                    }
                case T_OrderProcessors:
                    {
                        var op = (AIOrderProcessor)s.Comp;
                        if ((object)op == null || !op || op.Orders.Count > 0) return false;
                        Unit u = s.Unit;
                        if ((object)u != null && u && Idle.PlayerInvolved(u)) return false;
                        return true;
                    }
                case T_IdleSoldiers:
                    return Idle.SoldierIdle((Soldier)s.Unit, s.Rec);
                case T_StaticNet:
                    return Idle.NetTransformQuiet((NetworkTransformComponent)s.Comp);
            }
            return false;
        }

        static ListState GetState(object list)
        {
            for (int i = 0; i < s_States.Count; i++)
            {
                if (ReferenceEquals(s_States[i].List, list)) return s_States[i];
            }
            var st = new ListState { List = list, Kind = KindOf(list) };
            if (s_States.Count >= 8) s_States.Clear();   // old managers (scene changes): start over
            s_States.Add(st);
            return st;
        }

        static int KindOf(object list)
        {
            OrderedUpdateManager mgr = OrderedUpdateManager.Instance;
            if ((object)mgr == null) return -1;
            if (ReferenceEquals(s_FixedField.GetValue(mgr), list)) return K_Fixed;
            if (ReferenceEquals(s_FixedLateField.GetValue(mgr), list)) return K_FixedLate;
            if (ReferenceEquals(s_UpdateField.GetValue(mgr), list)) return K_Update;
            if (ReferenceEquals(s_LateField.GetValue(mgr), list)) return K_Late;
            return -1;
        }

        static Slot Resolve(ListState st, object entry, int index)
        {
            Misses++;
            if (!st.Map.TryGetValue(entry, out Slot slot))
            {
                // slots of entries that left the list are dropped here; what stays is bounded by the index cache
                // (power of two >= the largest list size seen) + 512, so dead objects are not kept alive for long
                if (st.Map.Count >= st.Slots.Length + 512) RebuildMap(st);
                slot = Classify(st.Kind, entry);
                st.Map[entry] = slot;
            }
            if (index >= 0)
            {
                if (index >= st.Slots.Length)
                {
                    int n = st.Slots.Length;
                    while (n <= index) n *= 2;
                    Array.Resize(ref st.Slots, n);
                }
                st.Slots[index] = slot;
            }
            return slot;
        }

        // Drop map entries of entries that are gone: keep what the index cache currently holds.
        static void RebuildMap(ListState st)
        {
            MapRebuilds++;
            st.Map.Clear();
            Slot[] arr = st.Slots;
            for (int i = 0; i < arr.Length; i++)
            {
                Slot s = arr[i];
                if (s != null) st.Map[s.Entry] = s;
            }
        }

        static Slot Classify(int kind, object entry)
        {
            Classifications++;
            var slot = new Slot { Entry = entry };
            if (kind < 0) return slot;
            Action<float> fn = s_Function(entry);
            if (fn == null || s_EveryTick(entry)) return slot;
            MethodInfo m = fn.Method;
            if (m == null) return slot;
            IntPtr h = m.MethodHandle.Value;
            if (!s_MethodTier[kind].TryGetValue(h, out int tierId)) return slot;
            object target = fn.Target;
            switch (tierId)
            {
                case T_Structures:
                    {
                        var o = target as BaseGameObject;
                        if ((object)o == null) return slot;
                        int sub = s_StructKind.TryGetValue(h, out int k) ? k : 0;
                        if ((sub == 3 && !(o is ConstructionSite)) || (sub != 3 && !(o is Structure))) return slot;
                        if (sub == 1 && !(o is HumanStructure)) return slot;
                        if (sub == 2 && !(o is AlienStructure)) return slot;
                        slot.Obj = o;
                        slot.SubKind = sub;
                        break;
                    }
                case T_HealDecay:
                    {
                        var c = target as UnityEngine.Component;
                        if ((object)c == null) return slot;
                        slot.Comp = c;
                        slot.Unit = c.transform.GetBaseGameObject() as Unit;
                        break;
                    }
                case T_OrderProcessors:
                    {
                        var op = target as AIOrderProcessor;
                        if ((object)op == null) return slot;
                        slot.Comp = op;
                        slot.Unit = op.Owner as Unit;
                        break;
                    }
                case T_IdleSoldiers:
                    {
                        var sol = target as Soldier;
                        if ((object)sol == null) return slot;
                        slot.Unit = sol;
                        slot.Rec = WakeRegistry.Get(sol);
                        break;
                    }
                case T_StaticNet:
                    {
                        var n = target as NetworkTransformComponent;
                        if ((object)n == null || (object)n.RigidBody != null) return slot;   // dynamic objects: full rate
                        slot.Comp = n;
                        break;
                    }
                default:
                    return slot;
            }
            Tier t = Tiers[tierId];
            slot.TierId = tierId;
            slot.Tier = t;
            slot.Countdown = 1 + (s_Phase++ % t.Interval);
            t.Classified++;
            return slot;
        }
    }
}
