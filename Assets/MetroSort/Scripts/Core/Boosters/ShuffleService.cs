using System.Collections.Generic;
using MetroSort.Puzzle;
using MetroSort.Utilities;

namespace MetroSort.Boosters
{
    /// <summary>
    /// Shuffle booster rules: only passengers still waiting in the current stage's queues are rearranged.
    /// Queue lengths, platforms, the bench and all counters stay as they are. A result is accepted only when it
    /// differs from the current arrangement and the solver proves it winnable.
    /// </summary>
    public static class ShuffleService
    {
        public const int MaxAttempts = 40;

        public static bool TryShuffle(BoardState current, int seed, out BoardState shuffled, int maxNodes = 60000)
        {
            shuffled = null;
            // Families stay together: the pool holds travelling units, not single passengers.
            var units = new List<List<Passenger>>();
            for (int q = 0; q < current.Queues.Length; q++)
                for (int i = 0; i < current.Queues[q].Count; i++)
                {
                    Passenger p = current.Queues[q][i];
                    if (p.Group != 0 && i > 0 && current.Queues[q][i - 1].Group == p.Group) units[units.Count - 1].Add(p);
                    else units.Add(new List<Passenger> { p });
                }
            if (units.Count < 2) return false;
            var pool = new List<Passenger>();

            string before = QueueCode(current);
            var rng = new DeterministicRandom(seed);
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                rng.Shuffle(units);
                pool.Clear();
                var starts = new List<int>();
                foreach (List<Passenger> unit in units)
                {
                    starts.Add(pool.Count);
                    pool.AddRange(unit);
                }
                starts.Add(pool.Count);
                BoardState candidate = current.Clone();
                int offset = 0, wanted = 0;
                for (int q = 0; q < candidate.Queues.Length; q++)
                {
                    // Queue lengths stay as they were, give or take a family that would otherwise be split.
                    wanted += candidate.Queues[q].Count;
                    int end = q == candidate.Queues.Length - 1 ? pool.Count : wanted;
                    while (!starts.Contains(end)) end++;
                    if (end < offset) end = offset;
                    candidate.Queues[q].ReplaceAll(pool, offset, end - offset);
                    offset = end;
                }
                if (QueueCode(candidate) == before) continue;
                SolveResult solve = PuzzleSolver.Solve(candidate, maxNodes);
                if (!solve.Solved) continue;
                shuffled = candidate;
                return true;
            }
            return false;
        }

        private static string QueueCode(BoardState s)
        {
            var sb = new System.Text.StringBuilder();
            for (int q = 0; q < s.Queues.Length; q++)
            {
                s.Queues[q].AppendCode(sb);
                sb.Append('|');
            }
            return sb.ToString();
        }
    }
}
