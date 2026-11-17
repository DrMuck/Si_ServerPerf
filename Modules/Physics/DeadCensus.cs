/*
 Census split alive / destroyed (part of the Census item, read-only, same 1 Hz sample).

 Destroyed units stay in Unit.Units with all bodies, joints and registrations until BaseGameObject.UpdateDestroyedDisposal
 removes them DestroyedDisposeTime (15 s game time) after death (BaseGameObject.cs:67, :801-837; Unit.CanDispose
 Unit.cs:432-441 also waits while ControlledBy is set). This split shows how many of them there are and how many of
 their dynamic bodies PhysX still simulates (awake), per unit kind, so the "20-28 % of world units are dead bodies still
 simulating" estimate (night1 ANALYSIS_BATTLE.md section 1 / C1) can be read off directly.

 Appended to the census line (keys chosen so the existing parsers' regexes - " dyn=(\d+) dynAsleep", "dynAsleep=",
 "wheeled u=\d+ dyn= sl=" - still match the original fields first):
   | dead deadU=.. deadDyn=.. deadSl=.. deadAwake=.. deadKin=.. aliveU=.. aliveDyn=.. aliveSl=.. aliveAwake=..
     dead_<kind>=units/dyn/asleep ... avoidAgents=.. deadSettled=..
 deadAwake = deadDyn - deadSl (dynamic bodies of destroyed units PhysX simulates); avoidAgents = agents registered with
 CollisionAvoidanceManager (falls when DeadSettle unregisters dead ones); deadSettled = units DeadSettle holds now
 (-1 when that item is off). Body counts are the last sample, like the rest of the census.
*/

using System;
using System.Text;
using Silica.AI;

namespace SiServerPerf.Modules.Physics
{
    internal static class DeadCensus
    {
        static readonly int[] _u = new int[Kinds.Count], _dyn = new int[Kinds.Count], _sl = new int[Kinds.Count], _kin = new int[Kinds.Count];
        static int _aliveU, _aliveDyn, _aliveSl;

        public static void Reset()
        {
            Array.Clear(_u, 0, Kinds.Count);
            Array.Clear(_dyn, 0, Kinds.Count);
            Array.Clear(_sl, 0, Kinds.Count);
            Array.Clear(_kin, 0, Kinds.Count);
            _aliveU = _aliveDyn = _aliveSl = 0;
        }

        public static void Unit(int k, bool dead)
        {
            if (dead) _u[k]++;
            else _aliveU++;
        }

        public static void Body(int k, bool dead, bool kinematic, bool asleep)
        {
            if (dead)
            {
                if (kinematic) _kin[k]++;
                else
                {
                    _dyn[k]++;
                    if (asleep) _sl[k]++;
                }
            }
            else if (!kinematic)
            {
                _aliveDyn++;
                if (asleep) _aliveSl++;
            }
        }

        static void Totals(out int u, out int dyn, out int sl, out int kin)
        {
            u = dyn = sl = kin = 0;
            for (int k = 0; k < Kinds.Count; k++) { u += _u[k]; dyn += _dyn[k]; sl += _sl[k]; kin += _kin[k]; }
        }

        public static string InfoText()
        {
            Totals(out int u, out int dyn, out int sl, out _);
            return "dead units " + u + " (dyn " + dyn + ", awake " + (dyn - sl) + "), alive dyn awake " + (_aliveDyn - _aliveSl) + "/" + _aliveDyn;
        }

        public static void Append(StringBuilder sb)
        {
            Totals(out int u, out int dyn, out int sl, out int kin);
            sb.Append(" | dead deadU=").Append(u).Append(" deadDyn=").Append(dyn).Append(" deadSl=").Append(sl)
              .Append(" deadAwake=").Append(dyn - sl).Append(" deadKin=").Append(kin)
              .Append(" aliveU=").Append(_aliveU).Append(" aliveDyn=").Append(_aliveDyn).Append(" aliveSl=").Append(_aliveSl)
              .Append(" aliveAwake=").Append(_aliveDyn - _aliveSl);
            for (int k = 0; k < Kinds.Count; k++)
            {
                if (_u[k] == 0) continue;
                sb.Append(" dead_").Append(Kinds.Names[k]).Append('=').Append(_u[k]).Append('/').Append(_dyn[k]).Append('/').Append(_sl[k]);
            }
            CollisionAvoidanceManager m = CollisionAvoidanceManager.Instance;
            sb.Append(" avoidAgents=").Append(m ? m.AgentCount : 0)
              .Append(" deadSettled=").Append(DeadSettle.On ? DeadSettle.SettledNow : -1);
        }
    }
}
