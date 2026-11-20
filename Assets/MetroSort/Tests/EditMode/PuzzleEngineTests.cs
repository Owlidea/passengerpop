using System.Collections.Generic;
using System.IO;
using MetroSort.Boosters;
using MetroSort.Levels;
using MetroSort.Puzzle;
using NUnit.Framework;

namespace MetroSort.Tests
{
    public static class TestLevels
    {
        public static LevelData Make(string[] queues, int platforms, int bench = 0, int moveLimit = 0)
        {
            var level = new LevelData { Id = 999, Bench = bench, MoveLimit = moveLimit };
            level.Stages.Add(queues);
            for (int i = 0; i < platforms; i++) level.Platforms.Add(new PlatformConfig());
            return level;
        }

        /// <summary>The introductory level from the design brief.</summary>
        public static LevelData Intro()
        {
            return Make(new[] { "GBR", "BRG", "GBR" }, 3);
        }

        public static string FindLevelsDirectory()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Assets", "MetroSort", "Resources", "Levels");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        public static void PlaySolution(PuzzleBoard board)
        {
            SolveResult solve = PuzzleSolver.Solve(board.State);
            Assert.IsTrue(solve.Solved, "expected a solvable position");
            foreach (Move m in solve.Moves) Assert.AreEqual(MoveError.None, board.TryMove(m, null));
        }
    }

    public class PuzzleEngineTests
    {
        [Test]
        public void AccessiblePassenger_CanMove()
        {
            var board = new PuzzleBoard(TestLevels.Intro());
            var events = new List<BoardEvent>();
            Assert.AreEqual(MoveError.None, board.TryMove(Move.FromQueue(0, 0), events));
            Assert.AreEqual(1, board.State.Platforms[0].Count);
            Assert.AreEqual(ColorCodes.Letters.IndexOf('G'), board.State.Platforms[0].Color);
            Assert.AreEqual(BoardEventType.PassengerMoved, events[0].Type);
        }

        [Test]
        public void BlockedPassenger_CannotBeMoved_OnlyFrontLeaves()
        {
            var board = new PuzzleBoard(TestLevels.Intro());
            int frontId = board.State.Queues[0][0].Id;
            int secondId = board.State.Queues[0][1].Id;
            var events = new List<BoardEvent>();
            board.TryMove(Move.FromQueue(0, 0), events);
            Assert.AreEqual(frontId, events[0].PassengerId, "only the front passenger may leave a queue");
            Assert.AreEqual(secondId, board.State.Queues[0].Front.Id);
            Assert.AreEqual(MoveError.BadSource, board.Validate(Move.FromQueue(7, 0)));
            Assert.AreEqual(MoveError.BadSource, board.Validate(Move.FromBench(0, 0)));
        }

        [Test]
        public void IncompatiblePlatform_IsRejected_AndStateUnchanged()
        {
            var board = new PuzzleBoard(TestLevels.Intro());
            board.TryMove(Move.FromQueue(0, 0), null); // green on platform 0
            string before = board.State.CanonicalKey();
            int moves = board.State.MovesUsed;
            Assert.AreEqual(MoveError.ColorMismatch, board.TryMove(Move.FromQueue(1, 0), null)); // blue onto green
            Assert.AreEqual(before, board.State.CanonicalKey());
            Assert.AreEqual(moves, board.State.MovesUsed);
            Assert.IsTrue(board.CanUndo);
            board.Undo();
            Assert.IsFalse(board.CanUndo, "a rejected move must not add undo history");
        }

        [Test]
        public void FullPlatform_IsRejected()
        {
            BoardState state = BoardState.Create(TestLevels.Make(new[] { "R", "R", "R", "R" }, 2));
            // Force a full platform; normal play clears it immediately, so build the state by hand.
            PlatformState plat = state.Platforms[0];
            plat.Color = 0;
            plat.Occupants.Add(new Passenger(100, 0, false));
            plat.Occupants.Add(new Passenger(101, 0, false));
            plat.Occupants.Add(new Passenger(102, 0, false));
            Assert.AreEqual(MoveError.PlatformFull, MoveValidator.Validate(state, Move.FromQueue(0, 0)));
            Assert.AreEqual(MoveError.None, MoveValidator.Validate(state, Move.FromQueue(0, 1)));
        }

        [Test]
        public void ThreeMatching_TriggerTrain_AndLeaveBoard()
        {
            var board = new PuzzleBoard(TestLevels.Make(new[] { "RB", "RB", "RB" }, 2));
            var events = new List<BoardEvent>();
            board.TryMove(Move.FromQueue(0, 0), events);
            board.TryMove(Move.FromQueue(1, 0), events);
            Assert.AreEqual(2, board.State.Platforms[0].Count);
            events.Clear();
            board.TryMove(Move.FromQueue(2, 0), events);
            BoardEvent train = events.Find(e => e.Type == BoardEventType.TrainDeparted);
            Assert.IsNotNull(train);
            Assert.AreEqual(3, train.PassengerIds.Length);
            Assert.AreEqual(0, train.Color);
            Assert.AreEqual(0, board.State.Platforms[0].Count);
            Assert.AreEqual(ColorCodes.None, board.State.Platforms[0].Color);
            Assert.AreEqual(3, board.State.Boarded);
            Assert.AreEqual(GameStatus.Playing, board.Status);
        }

        [Test]
        public void Queue_AdvancesAfterFrontLeaves()
        {
            var board = new PuzzleBoard(TestLevels.Intro());
            Assert.AreEqual(3, board.State.Queues[0].Count);
            board.TryMove(Move.FromQueue(0, 0), null);
            Assert.AreEqual(2, board.State.Queues[0].Count);
            Assert.AreEqual(ColorCodes.Letters.IndexOf('B'), board.State.Queues[0].Front.Color);
        }

        [Test]
        public void Undo_RestoresPreviousState()
        {
            var board = new PuzzleBoard(TestLevels.Intro());
            string initial = board.State.CanonicalKey();
            board.TryMove(Move.FromQueue(0, 0), null);
            string afterOne = board.State.CanonicalKey();
            board.TryMove(Move.FromQueue(2, 0), null);
            Assert.IsTrue(board.Undo());
            Assert.AreEqual(afterOne, board.State.CanonicalKey());
            Assert.AreEqual(1, board.State.MovesUsed);
            Assert.AreEqual(1, board.State.Platforms[0].Count);
            Assert.IsTrue(board.Undo());
            Assert.AreEqual(initial, board.State.CanonicalKey());
            Assert.AreEqual(0, board.State.MovesUsed);
            Assert.AreEqual(ColorCodes.None, board.State.Platforms[0].Color);
            Assert.IsFalse(board.Undo());
        }

        [Test]
        public void IntroLevel_IsCompletable_AndWinIsDetected()
        {
            var board = new PuzzleBoard(TestLevels.Intro());
            TestLevels.PlaySolution(board);
            Assert.AreEqual(GameStatus.Won, board.Status);
            Assert.AreEqual(9, board.State.Boarded);
            Assert.AreEqual(MoveError.GameOver, board.TryMove(Move.FromQueue(0, 0), null));
            Assert.IsFalse(board.CanUndo, "a won level cannot be undone");
        }

        [Test]
        public void Deadlock_IsDetected_AndUndoRecovers()
        {
            var board = new PuzzleBoard(TestLevels.Make(new[] { "RB", "BR", "RB" }, 1));
            var events = new List<BoardEvent>();
            board.TryMove(Move.FromQueue(0, 0), events);
            board.TryMove(Move.FromQueue(2, 0), events);
            Assert.AreEqual(GameStatus.Lost, board.Status);
            Assert.AreEqual(LoseReason.Deadlock, board.LoseReason);
            Assert.IsNotNull(events.Find(e => e.Type == BoardEventType.Lost));
            Assert.IsTrue(board.Undo());
            Assert.AreEqual(GameStatus.Playing, board.Status);
        }

        [Test]
        public void Restart_RestoresOriginalState()
        {
            var board = new PuzzleBoard(TestLevels.Intro());
            string initial = board.State.CanonicalKey();
            board.TryMove(Move.FromQueue(0, 0), null);
            board.TryMove(Move.FromQueue(1, 1), null);
            board.Restart();
            Assert.AreEqual(initial, board.State.CanonicalKey());
            Assert.AreEqual(0, board.State.MovesUsed);
            Assert.AreEqual(GameStatus.Playing, board.Status);
            Assert.IsFalse(board.CanUndo);
        }

        [Test]
        public void PassengerNeverExistsTwice_DuringAFullPlaythrough()
        {
            var board = new PuzzleBoard(TestLevels.Make(new[] { "GBRY", "BGYR", "GRBY" }, 3));
            SolveResult solve = PuzzleSolver.Solve(board.State);
            if (!solve.Solved) Assert.Inconclusive("fixture not solvable");
            foreach (Move m in solve.Moves)
            {
                board.TryMove(m, null);
                var seen = new HashSet<int>();
                int located = 0;
                foreach (PassengerQueue q in board.State.Queues)
                    for (int i = 0; i < q.Count; i++) { Assert.IsTrue(seen.Add(q[i].Id)); located++; }
                foreach (PlatformState p in board.State.Platforms)
                {
                    Assert.LessOrEqual(p.Count, p.Capacity);
                    foreach (Passenger o in p.Occupants)
                    {
                        Assert.IsTrue(seen.Add(o.Id));
                        Assert.AreEqual(p.Color, o.Color);
                        located++;
                    }
                }
                Assert.AreEqual(board.State.Total, located + board.State.Boarded);
            }
        }

        [Test]
        public void CanonicalKey_IgnoresQueueAndPlatformOrder()
        {
            BoardState a = BoardState.Create(TestLevels.Make(new[] { "RG", "BR", "GB" }, 2));
            BoardState b = BoardState.Create(TestLevels.Make(new[] { "GB", "RG", "BR" }, 2));
            Assert.AreEqual(a.CanonicalKey(), b.CanonicalKey());
            BoardRules.Apply(a, Move.FromQueue(0, 0), null);
            BoardRules.Apply(b, Move.FromQueue(1, 1), null);
            Assert.AreEqual(a.CanonicalKey(), b.CanonicalKey());
        }

        [Test]
        public void Vip_CannotWaitAlone()
        {
            var board = new PuzzleBoard(TestLevels.Make(new[] { "r", "R", "R" }, 2));
            Assert.AreEqual(MoveError.VipNeedsCompany, board.TryMove(Move.FromQueue(0, 0), null));
            Assert.AreEqual(MoveError.None, board.TryMove(Move.FromQueue(1, 0), null));
            Assert.AreEqual(MoveError.None, board.TryMove(Move.FromQueue(0, 0), null));
        }

        [Test]
        public void LockedPlatform_UnlocksAfterRequiredTrains()
        {
            LevelData level = TestLevels.Make(new[] { "BBB", "RRR" }, 1);
            level.Platforms.Add(new PlatformConfig { LockColor = 1, LockTrains = 1 });
            var board = new PuzzleBoard(level);
            Assert.AreEqual(MoveError.PlatformLocked, board.TryMove(Move.FromQueue(1, 1), null));
            var events = new List<BoardEvent>();
            for (int i = 0; i < 3; i++) board.TryMove(Move.FromQueue(0, 0), events);
            Assert.IsNotNull(events.Find(e => e.Type == BoardEventType.PlatformUnlocked && e.Platform == 1));
            Assert.AreEqual(MoveError.None, board.TryMove(Move.FromQueue(1, 1), null));
        }

        [Test]
        public void TemporaryPlatform_ClosesAfterItsUses()
        {
            LevelData level = TestLevels.Make(new[] { "RRR", "BBB" }, 1);
            level.Platforms.Add(new PlatformConfig { Uses = 1 });
            var board = new PuzzleBoard(level);
            for (int i = 0; i < 3; i++) board.TryMove(Move.FromQueue(0, 1), null);
            Assert.IsTrue(board.State.Platforms[1].Closed);
            Assert.AreEqual(MoveError.PlatformClosed, board.TryMove(Move.FromQueue(1, 1), null));
            board.Undo();
            Assert.IsFalse(board.State.Platforms[1].Closed, "undo must restore special mechanic state");
            Assert.AreEqual(1, board.State.Platforms[1].UsesLeft);
        }

        [Test]
        public void ExpressPlatform_DepartsWithTwo()
        {
            LevelData level = TestLevels.Make(new[] { "RR" }, 0);
            level.Platforms.Add(new PlatformConfig { Capacity = 2, Express = true });
            var board = new PuzzleBoard(level);
            board.TryMove(Move.FromQueue(0, 0), null);
            board.TryMove(Move.FromQueue(0, 0), null);
            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void ReservedExpressPlatform_OnlyAcceptsItsLine()
        {
            LevelData level = TestLevels.Make(new[] { "BRR", "BB" }, 1);
            level.Platforms.Add(new PlatformConfig { Capacity = 2, Express = true, ReservedColor = 0 });
            var board = new PuzzleBoard(level);
            Assert.AreEqual(MoveError.ColorMismatch, board.TryMove(Move.FromQueue(0, 1), null), "blue may not use the red express");
            board.TryMove(Move.FromQueue(0, 0), null);
            Assert.AreEqual(MoveError.None, board.TryMove(Move.FromQueue(0, 1), null));
            LevelData copy = LevelData.FromJson(level.ToJson());
            Assert.AreEqual(0, copy.Platforms[1].ReservedColor);
            TestLevels.PlaySolution(board);
            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void MultiStage_RevealsNextSet_AndWinsOnlyAfterLastStage()
        {
            LevelData level = TestLevels.Make(new[] { "RR", "B" }, 2);
            level.Stages.Add(new[] { "RB", "B" });
            var board = new PuzzleBoard(level);
            var events = new List<BoardEvent>();
            board.TryMove(Move.FromQueue(0, 0), events);
            board.TryMove(Move.FromQueue(0, 0), events);
            board.TryMove(Move.FromQueue(1, 1), events);
            Assert.AreEqual(1, board.State.Stage);
            Assert.IsNotNull(events.Find(e => e.Type == BoardEventType.StageAdvanced));
            Assert.AreEqual(GameStatus.Playing, board.Status);
            Assert.AreEqual(2, board.State.Platforms[0].Count, "platform contents carry over between stages");
            board.Undo();
            Assert.AreEqual(0, board.State.Stage);
            TestLevels.PlaySolution(board);
            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void Bench_ParksAnyColour_AndMoveLimitCausesLoss()
        {
            var board = new PuzzleBoard(TestLevels.Make(new[] { "BRRR", "BB" }, 1, bench: 1, moveLimit: 3));
            Assert.AreEqual(MoveError.None, board.TryMove(Move.QueueToBench(0), null));
            Assert.AreEqual(MoveError.BenchFull, board.Validate(Move.QueueToBench(1)));
            Assert.AreEqual(MoveError.None, board.TryMove(Move.FromQueue(0, 0), null));
            Assert.AreEqual(MoveError.ColorMismatch, board.Validate(Move.FromBench(0, 0)));
            board.TryMove(Move.FromQueue(0, 0), null);
            Assert.AreEqual(GameStatus.Lost, board.Status);
            Assert.AreEqual(LoseReason.OutOfMoves, board.LoseReason);
        }

        [Test]
        public void ExtraPlatform_RevivesADeadlock_OncePerLevel()
        {
            var board = new PuzzleBoard(TestLevels.Make(new[] { "RB", "BR", "RB" }, 1));
            board.TryMove(Move.FromQueue(0, 0), null);
            board.TryMove(Move.FromQueue(2, 0), null);
            Assert.AreEqual(GameStatus.Lost, board.Status);
            Assert.IsTrue(board.AddExtraPlatform(null));
            Assert.AreEqual(GameStatus.Playing, board.Status);
            Assert.AreEqual(2, board.State.Platforms.Count);
            Assert.IsFalse(board.AddExtraPlatform(null));
            TestLevels.PlaySolution(board);
            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void Hint_IsAlwaysLegal_AndLeadsToAWin()
        {
            var board = new PuzzleBoard(TestLevels.Intro());
            int guard = 0;
            while (board.Status == GameStatus.Playing && guard++ < 50)
            {
                Move hint;
                Assert.IsTrue(PuzzleSolver.TryGetHint(board.State, out hint));
                Assert.AreEqual(MoveError.None, board.TryMove(hint, null));
            }
            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void Hint_ReportsNothing_WhenPositionIsLost()
        {
            BoardState state = BoardState.Create(TestLevels.Make(new[] { "RB", "BR", "RB" }, 1));
            BoardRules.Apply(state, Move.FromQueue(0, 0), null);
            BoardRules.Apply(state, Move.FromQueue(2, 0), null);
            Move hint;
            Assert.IsFalse(PuzzleSolver.TryGetHint(state, out hint));
        }

        [Test]
        public void Shuffle_KeepsPassengers_AndStaysSolvable()
        {
            var board = new PuzzleBoard(TestLevels.Make(new[] { "GBRY", "BGYR", "GRBY" }, 3));
            board.TryMove(Move.FromQueue(0, 0), null);
            BoardState shuffled;
            Assert.IsTrue(ShuffleService.TryShuffle(board.State, 12345, out shuffled));
            Assert.IsTrue(PuzzleSolver.Solve(shuffled).Solved);
            var before = new List<int>();
            var after = new List<int>();
            foreach (PassengerQueue q in board.State.Queues) for (int i = 0; i < q.Count; i++) before.Add(q[i].Id);
            foreach (PassengerQueue q in shuffled.Queues) for (int i = 0; i < q.Count; i++) after.Add(q[i].Id);
            before.Sort();
            after.Sort();
            CollectionAssert.AreEqual(before, after);
            Assert.AreEqual(board.State.Platforms[0].Count, shuffled.Platforms[0].Count);
        }

        [Test]
        public void Shuffle_Fails_WhenNothingCanBeRearranged()
        {
            BoardState state = BoardState.Create(TestLevels.Make(new[] { "R", "R", "R" }, 1));
            BoardRules.Apply(state, Move.FromQueue(0, 0), null);
            BoardRules.Apply(state, Move.FromQueue(1, 0), null);
            BoardState shuffled;
            Assert.IsFalse(ShuffleService.TryShuffle(state, 1, out shuffled));
        }

        [Test]
        public void GeneratedLevels_AreSolvable()
        {
            var p = new GeneratorParams { Colors = 3, GroupsPerColor = 2, Queues = 4, Platforms = 3 };
            for (int seed = 1; seed <= 5; seed++)
            {
                ValidationReport report;
                LevelData level = LevelGenerator.Generate(p, seed, out report);
                Assert.IsNotNull(level, "generator found nothing for seed " + seed);
                Assert.IsTrue(report.IsValid);
                var board = new PuzzleBoard(level);
                TestLevels.PlaySolution(board);
                Assert.AreEqual(GameStatus.Won, board.Status);
                Assert.AreEqual(level.Par, board.State.MovesUsed);
            }
        }

        [Test]
        public void Validator_RejectsBrokenLevels()
        {
            Assert.IsFalse(LevelValidator.Validate(TestLevels.Make(new[] { "RR", "B" }, 2)).IsValid, "incomplete groups");
            Assert.IsFalse(LevelValidator.Validate(TestLevels.Make(new[] { "RBR", "BRB" }, 1)).IsValid, "unsolvable");
            Assert.IsFalse(LevelValidator.Validate(TestLevels.Make(new[] { "RRR" }, 1, 0, 2)).IsValid, "move limit too low");
            Assert.IsTrue(LevelValidator.Validate(TestLevels.Intro()).IsValid);
        }

        [Test]
        public void GatedQueue_OpensAfterRequiredTrains_AndUndoClosesItAgain()
        {
            LevelData level = TestLevels.Make(new[] { "BBB", "RRR" }, 2);
            level.Gates = new[] { 0, 1 };
            var board = new PuzzleBoard(level);
            Assert.AreEqual(MoveError.QueueGated, board.TryMove(Move.FromQueue(1, 1), null));
            Assert.IsFalse(board.GetLegalMoves().Exists(m => m.Source == 1));
            var events = new List<BoardEvent>();
            for (int i = 0; i < 3; i++) Assert.AreEqual(MoveError.None, board.TryMove(Move.FromQueue(0, 0), events));
            Assert.IsNotNull(events.Find(e => e.Type == BoardEventType.QueueGateOpened && e.Queue == 1));
            Assert.AreEqual(0, board.State.GateRemaining(1));
            Assert.IsTrue(board.Undo());
            Assert.AreEqual(1, board.State.GateRemaining(1));
            Assert.AreEqual(MoveError.QueueGated, board.TryMove(Move.FromQueue(1, 1), null));
            board.TryMove(Move.FromQueue(0, 0), null);
            for (int i = 0; i < 3; i++) Assert.AreEqual(MoveError.None, board.TryMove(Move.FromQueue(1, 1), null));
            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void GatedQueue_ThatCanNeverOpen_IsUnsolvable_AndKeysDiffer()
        {
            LevelData open = TestLevels.Make(new[] { "BRB", "RBR" }, 2);
            LevelData gated = TestLevels.Make(new[] { "BRB", "RBR" }, 2);
            gated.Gates = new[] { 0, 1 };
            Assert.IsTrue(LevelValidator.Validate(open).IsValid);
            Assert.IsFalse(LevelValidator.Validate(gated).IsValid);
            Assert.AreNotEqual(BoardState.Create(open).CanonicalKey(), BoardState.Create(gated).CanonicalKey());
            LevelData copy = LevelData.FromJson(gated.ToJson());
            Assert.AreEqual(new[] { 0, 1 }, copy.Gates);
        }

        [Test]
        public void ColourGate_OnlyCountsTrainsOfItsLine()
        {
            LevelData level = TestLevels.Make(new[] { "BBB", "RRR", "GGG" }, 2);
            level.Gates = new[] { 0, 0, 1 };
            level.GateColors = new[] { ColorCodes.Any, ColorCodes.Any, 0 };
            var board = new PuzzleBoard(level);
            var events = new List<BoardEvent>();
            for (int i = 0; i < 3; i++) board.TryMove(Move.FromQueue(0, 0), events);
            Assert.AreEqual(1, board.State.GateRemaining(2), "a blue train must not open a red gate");
            Assert.IsNull(events.Find(e => e.Type == BoardEventType.QueueGateOpened));
            Assert.AreEqual(MoveError.QueueGated, board.TryMove(Move.FromQueue(2, 0), null));
            for (int i = 0; i < 3; i++) board.TryMove(Move.FromQueue(1, 1), events);
            Assert.IsNotNull(events.Find(e => e.Type == BoardEventType.QueueGateOpened && e.Queue == 2));
            Assert.AreEqual(MoveError.None, board.TryMove(Move.FromQueue(2, 0), null));
            LevelData copy = LevelData.FromJson(level.ToJson());
            Assert.AreEqual(level.GateColors, copy.GateColors);
            Assert.AreEqual(BoardState.Create(level).CanonicalKey(), BoardState.Create(copy).CanonicalKey());
            level.GateColors = new int[0];
            Assert.AreNotEqual(BoardState.Create(level).CanonicalKey(), BoardState.Create(copy).CanonicalKey());
        }

        [Test]
        public void LevelJson_RoundTrips()
        {
            LevelData level = TestLevels.Make(new[] { "Rb", "BR" }, 1, bench: 1, moveLimit: 9);
            level.Platforms.Add(new PlatformConfig { Capacity = 2, Express = true, LockColor = ColorCodes.Any, LockTrains = 1, Uses = 2 });
            level.Stages.Add(new[] { "GGG" });
            LevelData copy = LevelData.FromJson(level.ToJson());
            Assert.AreEqual(level.ToJson(), copy.ToJson());
            Assert.AreEqual(2, copy.Stages.Count);
            Assert.AreEqual(ColorCodes.Any, copy.Platforms[1].LockColor);
            Assert.AreEqual(BoardState.Create(level).CanonicalKey(), BoardState.Create(copy).CanonicalKey());
        }

        // Split into blocks of 100 so that no single case outgrows the test runner's time limit under Unity's Mono.
        [TestCase(1), TestCase(101), TestCase(201), TestCase(301), TestCase(401), TestCase(501), TestCase(601), TestCase(701)]
        [Timeout(900000)]
        public void ShippedLevels_AllValidate_AndAreOrdered(int firstId)
        {
            string dir = TestLevels.FindLevelsDirectory();
            Assert.IsNotNull(dir, "level directory not found");
            string[] files = Directory.GetFiles(dir, "level_*.json");
            System.Array.Sort(files, string.CompareOrdinal);
            Assert.GreaterOrEqual(files.Length, 30);
            Assert.LessOrEqual(files.Length, 800, "add a TestCase block for levels above 800");
            for (int i = firstId - 1; i < files.Length && i < firstId + 99; i++)
            {
                LevelData level = LevelData.FromJson(File.ReadAllText(files[i]));
                Assert.AreEqual(i + 1, level.Id, files[i]);
                ValidationReport report = LevelValidator.Validate(level);
                Assert.IsTrue(report.IsValid, report.ToString());
                // Scheduled levels count the bench detours in par (a park and the automatic boarding after it).
                if (level.HasSchedule) Assert.GreaterOrEqual(level.Par, report.Analysis.MinSolutionLength, "par below taps in level " + level.Id);
                else Assert.AreEqual(report.Analysis.MinSolutionLength, level.Par, "par mismatch in level " + level.Id);
            }
        }
    }
}
