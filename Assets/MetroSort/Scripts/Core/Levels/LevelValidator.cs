using System;
using System.Collections.Generic;
using MetroSort.Puzzle;
using MetroSort.Utilities;

namespace MetroSort.Levels
{
    public sealed class ValidationReport
    {
        public int LevelId;
        public readonly List<string> Errors = new List<string>();
        public LevelAnalysis Analysis;
        public float Difficulty;
        public bool IsValid { get { return Errors.Count == 0; } }

        public override string ToString()
        {
            if (!IsValid) return "Level " + LevelId + ": INVALID - " + string.Join("; ", Errors.ToArray());
            return "Level " + LevelId + ": OK states=" + Analysis.ReachableStates + " min=" + Analysis.MinSolutionLength
                   + " dead=" + Analysis.DeadEndRatio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                   + " critical=" + Analysis.CriticalDecisions
                   + " difficulty=" + Difficulty.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Structural checks plus a full solver pass. A level only ships if this reports no errors.</summary>
    public static class LevelValidator
    {
        public static ValidationReport Validate(LevelData level, int maxNodes = PuzzleSolver.DefaultMaxNodes)
        {
            var report = new ValidationReport { LevelId = level.Id };
            List<string> errors = report.Errors;

            if (level.Stages.Count == 0) errors.Add("no stages");
            if (level.Platforms.Count == 0) errors.Add("no platforms");
            if (level.Bench < 0 || level.Bench > 3) errors.Add("bench size must be 0-3");

            for (int s = 0; s < level.Stages.Count; s++)
            {
                string[] queues = level.Stages[s];
                if (queues.Length == 0) errors.Add("stage " + s + " has no queues");
                bool any = false;
                foreach (string q in queues)
                {
                    if (q.Length > 0) any = true;
                    foreach (char c in q)
                    {
                        int color; bool vip;
                        if (!ColorCodes.TryParse(c, out color, out vip)) errors.Add("unknown passenger code '" + c + "'");
                    }
                }
                if (!any) errors.Add("stage " + s + " has no passengers");
            }

            if (level.Gates != null && level.Gates.Length > 0)
            {
                if (level.Stages.Count > 0 && level.Gates.Length > level.Stages[0].Length) errors.Add("more gates than queues");
                foreach (int g in level.Gates) if (g < 0 || g > 9) errors.Add("gate must be 0-9 trains");
                if (level.GateColors != null && level.GateColors.Length > level.Gates.Length) errors.Add("more gate colours than gates");
            }

            var capacities = new List<int>();
            bool anyOpen = false;
            foreach (PlatformConfig p in level.Platforms)
            {
                if (p.Capacity < 2 || p.Capacity > 4) errors.Add("platform capacity must be 2-4");
                if (!capacities.Contains(p.Capacity)) capacities.Add(p.Capacity);
                if (!p.IsLocked) anyOpen = true;
            }
            if (level.Platforms.Count > 0 && !anyOpen) errors.Add("every platform starts locked");
            if (errors.Count > 0) return report;

            // Each colour must be divisible into complete train groups using the available capacities.
            int[] counts = level.ColorCounts();
            for (int c = 0; c < counts.Length; c++)
            {
                if (counts[c] > 0 && !CanPartition(counts[c], capacities))
                    errors.Add(ColorCodes.Names[c] + " has " + counts[c] + " passengers, which cannot form complete groups");
            }
            if (errors.Count > 0) return report;

            BoardState start = BoardState.Create(level);
            LevelAnalysis analysis = PuzzleSolver.Analyze(start, maxNodes);
            report.Analysis = analysis;
            if (analysis.InitialLegalMoves == 0) errors.Add("no legal opening move");
            if (!analysis.Complete && !analysis.Solvable) errors.Add("search budget exhausted before a solution was found");
            else if (!analysis.Solvable) errors.Add("unsolvable");
            if (analysis.Solvable && level.MoveLimit > 0 && level.MoveLimit < analysis.MinSolutionLength)
                errors.Add("move limit " + level.MoveLimit + " is below the minimum solution length " + analysis.MinSolutionLength);
            report.Difficulty = DifficultyScorer.Score(level, analysis);
            return report;
        }

        private static bool CanPartition(int count, List<int> capacities)
        {
            var reachable = new bool[count + 1];
            reachable[0] = true;
            for (int i = 1; i <= count; i++)
                foreach (int cap in capacities)
                    if (i >= cap && reachable[i - cap]) { reachable[i] = true; break; }
            return reachable[count];
        }
    }

    /// <summary>
    /// Turns measurable properties of a level into a 0-100 score. It orders levels; it does not replace playtesting.
    /// </summary>
    public static class DifficultyScorer
    {
        public static float Score(LevelData level, LevelAnalysis a)
        {
            int passengers = level.TotalPassengers;
            int colors = level.DistinctColors;
            int maxDepth = 0;
            foreach (string[] stage in level.Stages)
                foreach (string q in stage) maxDepth = Math.Max(maxDepth, q.Length);
            int openPlatforms = 0;
            foreach (PlatformConfig p in level.Platforms) if (!p.IsLocked) openPlatforms++;

            float score = 0f;
            score += 0.5f * passengers;                               // length
            score += 2.0f * colors;                                   // things to track
            score += 1.0f * maxDepth;                                 // hidden information
            score += 5.0f * Math.Max(0, colors - openPlatforms);      // platform pressure
            score += 45.0f * a.DeadEndRatio;                          // how easy it is to go wrong
            score += 2.5f * a.CriticalDecisions;                      // forced choices on the best path
            score += 12.0f * (1f - a.AverageSafeMoveRatio);           // few alternative solutions
            score += 0.3f * Math.Max(0, a.MinSolutionLength - passengers); // required bench detours
            if (level.MoveLimit > 0) score += Math.Max(0, 6 - (level.MoveLimit - a.MinSolutionLength)) * 1.5f;
            return Math.Max(0f, Math.Min(100f, score));
        }
    }

    public sealed class GeneratorParams
    {
        public int Colors = 3;
        /// <summary>Complete train groups per colour (each group is three passengers).</summary>
        public int GroupsPerColor = 1;
        public int Queues = 3;
        public int Platforms = 3;
        public int Bench;
        public float MinDeadEndRatio = 0f;
        public float MaxDeadEndRatio = 1f;
        public int MinCriticalDecisions;
        public int MaxAttempts = 300;
        public int MaxNodes = 120000;
    }

    /// <summary>
    /// Produces candidate levels and keeps only ones the solver proves winnable inside the requested difficulty band.
    /// Intended for offline use (editor tool / command line), not for the render loop.
    /// </summary>
    public static class LevelGenerator
    {
        public static LevelData Generate(GeneratorParams p, int seed, out ValidationReport report)
        {
            var rng = new DeterministicRandom(seed);
            for (int attempt = 0; attempt < p.MaxAttempts; attempt++)
            {
                LevelData candidate = BuildCandidate(p, rng);
                ValidationReport r = LevelValidator.Validate(candidate, p.MaxNodes);
                if (!r.IsValid || !r.Analysis.Complete) continue;
                float dead = r.Analysis.DeadEndRatio;
                if (dead < p.MinDeadEndRatio || dead > p.MaxDeadEndRatio) continue;
                if (r.Analysis.CriticalDecisions < p.MinCriticalDecisions) continue;
                candidate.Par = r.Analysis.MinSolutionLength;
                candidate.Difficulty = r.Difficulty;
                report = r;
                return candidate;
            }
            report = null;
            return null;
        }

        private static LevelData BuildCandidate(GeneratorParams p, DeterministicRandom rng)
        {
            var pool = new List<char>();
            for (int c = 0; c < p.Colors; c++)
                for (int i = 0; i < p.GroupsPerColor * 3; i++) pool.Add(ColorCodes.Letters[c]);
            rng.Shuffle(pool);

            var queues = new System.Text.StringBuilder[p.Queues];
            for (int q = 0; q < queues.Length; q++) queues[q] = new System.Text.StringBuilder();
            for (int i = 0; i < pool.Count; i++) queues[i % p.Queues].Append(pool[i]);

            var level = new LevelData { Name = "Generated", Group = "Generated", Bench = p.Bench };
            var stage = new string[p.Queues];
            for (int q = 0; q < stage.Length; q++) stage[q] = queues[q].ToString();
            level.Stages.Add(stage);
            for (int i = 0; i < p.Platforms; i++) level.Platforms.Add(new PlatformConfig());
            return level;
        }
    }
}
