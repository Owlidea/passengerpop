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
            var pool = new List<Passenger>();
            for (int q = 0; q < current.Queues.Length; q++)
                for (int i = 0; i < current.Queues[q].Count; i++) pool.Add(current.Queues[q][i]);
            if (pool.Count < 2) return false;

            string before = QueueCode(current);
            var rng = new DeterministicRandom(seed);
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                rng.Shuffle(pool);
                BoardState candidate = current.Clone();
                int offset = 0;
                for (int q = 0; q < candidate.Queues.Length; q++)
                {
                    int len = candidate.Queues[q].Count;
                    candidate.Queues[q].ReplaceAll(pool, offset, len);
                    offset += len;
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
