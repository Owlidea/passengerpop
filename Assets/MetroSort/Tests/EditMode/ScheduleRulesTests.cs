using System.Collections.Generic;
using MetroSort.Levels;
using MetroSort.Puzzle;
using NUnit.Framework;

namespace MetroSort.Tests
{
    /// <summary>The train-timetable rules: trains arrive in order, passengers board their own line or wait on the bench.</summary>
    public class ScheduleRulesTests
    {
        private static LevelData Make(string[] queues, string trains, int platforms, int bench)
        {
            LevelData level = TestLevels.Make(queues, platforms, bench);
            level.Trains = trains;
            return level;
        }

        private static Move Tap(PuzzleBoard board, int queue)
        {
            Move m;
            Assert.IsTrue(BoardRules.TryAutoMove(board.State, queue, out m), "tap on queue " + queue + " should be possible");
            Assert.AreEqual(MoveError.None, board.TryMove(m, new List<BoardEvent>()));
            return m;
        }

        [Test]
        public void Platforms_StartWithTheFirstScheduledTrains()
        {
            var board = new PuzzleBoard(Make(new[] { "RRR", "BBB" }, "RB", 2, 2));
            Assert.AreEqual(ColorCodes.Letters.IndexOf('R'), board.State.Platforms[0].ReservedColor);
            Assert.AreEqual(ColorCodes.Letters.IndexOf('B'), board.State.Platforms[1].ReservedColor);
            Assert.AreEqual(0, board.State.UpcomingTrains);
        }

        [Test]
        public void Passenger_WithoutTrain_GoesToBench_AndBoardsWhenItArrives()
        {
            // One platform: Red is in, Blue is next.
            var board = new PuzzleBoard(Make(new[] { "BRRR", "BB" }, "RB", 1, 2));
            Move m = Tap(board, 0);
            Assert.IsTrue(m.IsToBench);
            Assert.AreEqual(1, board.State.Bench.Count);
            Tap(board, 0); Tap(board, 0); Tap(board, 0); // red train fills and leaves, blue pulls in
            Assert.AreEqual(0, board.State.Bench.Count, "the waiting passenger boards the blue train by themselves");
            Assert.AreEqual(1, board.State.Platforms[0].Count);
            Tap(board, 1); Tap(board, 1);
            Assert.AreEqual(GameStatus.Won, board.Status);
            Assert.AreEqual(6 + 1, board.State.MovesUsed, "the automatic boarding counts as a move for the stars");
        }

        [Test]
        public void FullBench_WithNoBoardingPossible_JamsTheStation()
        {
            var board = new PuzzleBoard(Make(new[] { "BG", "RRR", "BBGG" }, "RBG", 1, 1));
            Tap(board, 0); // blue waits
            Assert.AreEqual(GameStatus.Playing, board.Status);
            Move m;
            Assert.IsFalse(BoardRules.TryAutoMove(board.State, 0, out m), "green has no train and the bench is full");
            Assert.IsFalse(BoardRules.TryAutoMove(board.State, 2, out m));
            Tap(board, 1);
            Assert.AreEqual(GameStatus.Playing, board.Status);
        }

        [Test]
        public void TimetableUsedUp_ClosesThePlatform()
        {
            var board = new PuzzleBoard(Make(new[] { "RRR" }, "R", 2, 0));
            Assert.IsTrue(board.State.Platforms[1].Closed);
            Tap(board, 0); Tap(board, 0); Tap(board, 0);
            Assert.AreEqual(GameStatus.Won, board.Status);
            Assert.IsTrue(board.State.Platforms[0].Closed);
        }

        [Test]
        public void OnlyTheAutomaticTarget_IsLegal()
        {
            var board = new PuzzleBoard(Make(new[] { "RRR", "RRR" }, "RR", 2, 1));
            Tap(board, 0);
            // Both platforms wait for Red; the passenger joins the fuller one.
            Assert.AreEqual(MoveError.BadTarget, board.Validate(Move.FromQueue(1, 1)));
            Assert.AreEqual(MoveError.None, board.Validate(Move.FromQueue(1, 0)));
            Assert.AreEqual(MoveError.BadTarget, board.Validate(Move.QueueToBench(1)), "no parking while the train is in");
        }

        [Test]
        public void Solver_FindsScheduledSolutions_AndUndoRestoresTheTimetable()
        {
            var board = new PuzzleBoard(Make(new[] { "BRG", "GBR", "RGB" }, "RGB", 2, 3));
            int next = board.State.NextTrain;
            Tap(board, 0);
            Assert.IsTrue(board.Undo());
            Assert.AreEqual(next, board.State.NextTrain);
            TestLevels.PlaySolution(board);
            Assert.AreEqual(GameStatus.Won, board.Status);
        }

        [Test]
        public void ExtraPlatform_BringsTheNextTrainEarly()
        {
            var board = new PuzzleBoard(Make(new[] { "BBRRR", "B" }, "RB", 1, 2));
            Tap(board, 0); Tap(board, 0);
            Assert.AreEqual(2, board.State.Bench.Count);
            Assert.IsTrue(board.AddExtraPlatform(new List<BoardEvent>()));
            Assert.AreEqual(0, board.State.Bench.Count);
            Assert.AreEqual(2, board.State.Platforms[1].Count);
            Assert.IsFalse(board.CanAddExtraPlatform);
        }

        [Test]
        public void ScheduleJson_RoundTrips_AndValidates()
        {
            LevelData level = Make(new[] { "RBR", "BBR" }, "BR", 2, 3);
            LevelData copy = LevelData.FromJson(level.ToJson());
            Assert.AreEqual("BR", copy.Trains);
            Assert.IsTrue(LevelValidator.Validate(copy).IsValid);
            level.Trains = "BRR";
            Assert.IsFalse(LevelValidator.Validate(level).IsValid, "train count must match the passengers");
        }
    }
}
