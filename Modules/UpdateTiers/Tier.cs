/*
 One rate tier: its switch, interval and counters. The counters are plain longs bumped from the hot path (main thread
 only) and rolled into per-second rates by TierStatus.
*/

namespace SiServerPerf.Modules.UpdateTiers
{
    internal sealed class Tier
    {
        public readonly string Key;
        public PerfItem Item;
        public bool On;           // the item's switch (written by the core through the setter)
        public readonly int Interval;

        public long Runs;         // tiered entry ran because it was due (every Interval-th call)
        public long Busy;         // tiered entry ran early because its quiet test failed (state change / not idle)
        public long Skips;        // tiered entry skipped
        public long Classified;   // entries (or agents) assigned to this tier since start
        public readonly long[] Why = new long[Idle.W_Count];      // early runs by first failed quiet condition
        public readonly long[] LastWhy = new long[Idle.W_Count];

        // Previous sample for the per-second rates (TierStatus).
        public long LastRuns, LastBusy, LastSkips;

        public Tier(string key, int interval)
        {
            Key = key;
            Interval = interval < 1 ? 1 : (interval > 64 ? 64 : interval);
        }
    }
}
