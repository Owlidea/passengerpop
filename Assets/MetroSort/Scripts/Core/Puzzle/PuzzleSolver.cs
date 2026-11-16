using System.Collections.Generic;

namespace MetroSort.Puzzle
{
    public sealed class SolveResult
    {
        public bool Solved;
        /// <summary>False when the node budget ran out before the search finished.</summary>
        public bool SearchComplete = true;
        public List<Move> Moves = new List<Move>();
        public int NodesExplored;
    }

    /// <summary>Measurements of the whole reachable state graph of a level.</summary>
    public sealed class LevelAnalysis
    {
        public bool Complete = true;
        public bool Solvable;
        public int ReachableStates;
        public int SolvableStates;
        public int MinSolutionLength;
        public int InitialLegalMoves;
        public int SafeInitialMoves;
        /// <summary>States on the shortest solution where at least one legal move leads to an unwinnable state.</summary>
        public int CriticalDecisions;
        /// <summary>Mean share of legal moves that keep the level winnable, over winnable states.</summary>
        public float AverageSafeMoveRatio = 1f;
        public List<Move> Solution = new List<Move>();

        public float DeadEndRatio
        {
            get { return ReachableStates == 0 ? 0f : 1f - (float)SolvableStates / ReachableStates; }
        }
    }

    /// <summary>
    /// Breadth-first search over canonical states. Pure C#, no Unity dependencies, safe to run on a worker thread
    /// as long as it is given its own clone of the state.
    /// </summary>
    public static class PuzzleSolver
    {
        public const int DefaultMaxNodes = 250000;

        /// <summary>Finds a shortest solution from the given state, honouring the remaining move limit.</summary>
        public static SolveResult Solve(BoardState start, int maxNodes = DefaultMaxNodes)
        {
            var result = new SolveResult();
            if (start.IsWon) { result.Solved = true; return result; }

            int budget = start.MovesRemaining;
            var states = new List<BoardState> { start.Clone() };
            var parents = new List<int> { -1 };
            var moves = new List<Move> { default(Move) };
            var depths = new List<int> { 0 };
            var visited = new HashSet<string> { start.CanonicalKey() };
            var legal = new List<Move>();

            for (int head = 0; head < states.Count; head++)
            {
                BoardState s = states[head];
                states[head] = null; // free memory, only the tree shape is needed afterwards
                int depth = depths[head];
                if (depth >= budget) continue;
                BoardRules.GetLegalMoves(s, legal);
                for (int i = 0; i < legal.Count; i++)
                {
                    BoardState next = s.Clone();
                    BoardRules.Apply(next, legal[i], null);
                    if (!visited.Add(next.CanonicalKey())) continue;
                    states.Add(next);
                    parents.Add(head);
                    moves.Add(legal[i]);
                    depths.Add(depth + 1);
                    if (next.IsWon)
                    {
                        result.Solved = true;
                        result.NodesExplored = states.Count;
                        for (int n = states.Count - 1; n > 0; n = parents[n]) result.Moves.Add(moves[n]);
                        result.Moves.Reverse();
                        return result;
                    }
                    if (states.Count >= maxNodes)
                    {
                        result.SearchComplete = false;
                        result.NodesExplored = states.Count;
                        return result;
                    }
                }
            }
            result.NodesExplored = states.Count;
            return result;
        }

        /// <summary>Returns a move on a verified shortest solution, or false when the position cannot be won.</summary>
        public static bool TryGetHint(BoardState state, out Move hint, int maxNodes = DefaultMaxNodes)
        {
            // Prefer the most readable good move: joining the fullest matching platform, provided the
            // position is still verifiably winnable afterwards.
            var legal = new List<Move>();
            BoardRules.GetLegalMoves(state, legal);
            int bestCount = 0;
            Move best = default(Move);
            for (int i = 0; i < legal.Count; i++)
            {
                if (legal[i].IsToBench) continue;
                int count = state.Platforms[legal[i].Target].Count;
                if (count <= bestCount) continue;
                BoardState next = state.Clone();
                BoardRules.Apply(next, legal[i], null);
                if (!next.IsWon && !Solve(next, maxNodes).Solved) continue;
                bestCount = count;
                best = legal[i];
            }
            if (bestCount > 0)
            {
                hint = best;
                return true;
            }

            SolveResult r = Solve(state, maxNodes);
            if (r.Solved && r.Moves.Count > 0)
            {
                hint = r.Moves[0];
                return true;
            }
            hint = default(Move);
            return false;
        }

        /// <summary>
        /// Explores every reachable state (ignoring the move limit) and classifies each as winnable or dead.
        /// Used offline by the level validator and difficulty scorer.
        /// </summary>
        public static LevelAnalysis Analyze(BoardState start, int maxNodes = DefaultMaxNodes)
        {
            var a = new LevelAnalysis();
            var index = new Dictionary<string, int>();
            var states = new List<BoardState>();
            var successors = new List<List<int>>();
            var won = new List<bool>();
            var parents = new List<int>();
            var parentMoves = new List<Move>();
            var legal = new List<Move>();

            states.Add(start.Clone());
            index[start.CanonicalKey()] = 0;
            successors.Add(new List<int>());
            won.Add(start.IsWon);
            parents.Add(-1);
            parentMoves.Add(default(Move));
            int firstWon = start.IsWon ? 0 : -1;

            for (int head = 0; head < states.Count; head++)
            {
                BoardState s = states[head];
                states[head] = null;
                if (won[head]) continue;
                BoardRules.GetLegalMoves(s, legal);
                if (head == 0) a.InitialLegalMoves = legal.Count;
                for (int i = 0; i < legal.Count; i++)
                {
                    BoardState next = s.Clone();
                    BoardRules.Apply(next, legal[i], null);
                    string key = next.CanonicalKey();
                    int id;
                    if (!index.TryGetValue(key, out id))
                    {
                        if (states.Count >= maxNodes) { a.Complete = false; continue; }
                        id = states.Count;
                        index[key] = id;
                        states.Add(next);
                        successors.Add(new List<int>());
                        won.Add(next.IsWon);
                        parents.Add(head);
                        parentMoves.Add(legal[i]);
                        if (next.IsWon && firstWon < 0) firstWon = id;
                    }
                    successors[head].Add(id);
                }
            }

            int n = states.Count;
            a.ReachableStates = n;

            // Backward reachability from winning states.
            var reverse = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                List<int> succ = successors[i];
                for (int k = 0; k < succ.Count; k++)
                {
                    if (reverse[succ[k]] == null) reverse[succ[k]] = new List<int>();
                    reverse[succ[k]].Add(i);
                }
            }
            var solvable = new bool[n];
            var stack = new Stack<int>();
            for (int i = 0; i < n; i++) if (won[i]) { solvable[i] = true; stack.Push(i); }
            while (stack.Count > 0)
            {
                int cur = stack.Pop();
                List<int> preds = reverse[cur];
                if (preds == null) continue;
                for (int k = 0; k < preds.Count; k++)
                {
                    if (solvable[preds[k]]) continue;
                    solvable[preds[k]] = true;
                    stack.Push(preds[k]);
                }
            }

            double ratioSum = 0;
            int ratioCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (!solvable[i]) continue;
                a.SolvableStates++;
                List<int> succ = successors[i];
                if (succ.Count == 0) continue;
                int safe = 0;
                for (int k = 0; k < succ.Count; k++) if (solvable[succ[k]]) safe++;
                ratioSum += (double)safe / succ.Count;
                ratioCount++;
            }
            if (ratioCount > 0) a.AverageSafeMoveRatio = (float)(ratioSum / ratioCount);

            a.Solvable = solvable[0];
            List<int> rootSucc = successors[0];
            for (int k = 0; k < rootSucc.Count; k++) if (solvable[rootSucc[k]]) a.SafeInitialMoves++;

            if (firstWon >= 0)
            {
                // BFS discovery order means the first winning node found lies on a shortest path.
                for (int node = firstWon; node > 0; node = parents[node])
                {
                    a.Solution.Add(parentMoves[node]);
                    int parent = parents[node];
                    List<int> succ = successors[parent];
                    for (int k = 0; k < succ.Count; k++)
                    {
                        if (!solvable[succ[k]]) { a.CriticalDecisions++; break; }
                    }
                }
                a.Solution.Reverse();
                a.MinSolutionLength = a.Solution.Count;
            }
            return a;
        }
    }
}
