/*
 Reference-identity comparer. UnityEngine.Object overrides Equals (native liveness check), and the game's internal
 UpdateActionData is only reachable as object; both are keyed here by reference, which is cheap and exact.
*/

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SiServerPerf.Modules.UpdateTiers
{
    internal sealed class RefComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly RefComparer<T> Instance = new RefComparer<T>();
        public bool Equals(T a, T b) => ReferenceEquals(a, b);
        public int GetHashCode(T o) => o == null ? 0 : RuntimeHelpers.GetHashCode(o);
    }
}
