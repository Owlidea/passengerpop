using System.Collections;
using MetroSort.Economy;
using MetroSort.Game;
using MetroSort.Persistence;
using MetroSort.Puzzle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MetroSort.Tests
{
    /// <summary>Boots the real game (scene objects, animation, UI) against an in-memory save and drives it through its API.</summary>
    public class GameplayIntegrationTests
    {
        private InMemoryFileStore _store;
        private GameApp _app;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _store = new InMemoryFileStore();
            _app = GameApp.Create(_store);
            GameSettings.FastTrains = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_app != null) Object.Destroy(_app.gameObject);
            yield return null;
        }

        private IEnumerator PlayToEnd()
        {
            // Time-based: on timetable levels a passenger may have to wait for the next train to pull in.
            float guard = 90f;
            while (_app.Game.Board.Status == GameStatus.Playing && (guard -= Time.unscaledDeltaTime) > 0f)
            {
                Move move;
                if (_app.Tutorial.IsGuiding) move = _app.Tutorial.Expected;
                else Assert.IsTrue(PuzzleSolver.TryGetHint(_app.Game.Board.State, out move), "solver found no move");
                MoveError result = _app.Game.TryMove(move);
                // A platform that is still seeing its train off refuses new passengers for a moment; just wait.
                if (result != MoveError.PlatformBusy) Assert.AreEqual(MoveError.None, result);
                yield return null;
            }
            float timeout = 15f;
            while (_app.States.Current == AppState.Playing && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Boot_ShowsMenu_WithAllLevelsLoaded()
        {
            Assert.AreEqual(AppState.Menu, _app.States.Current);
            Assert.GreaterOrEqual(_app.Services.Levels.Count, 30);
            Assert.IsNotNull(_app.Station.Cam);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstFiveLevels_CanBeCompleted_AndRewardExactlyOnce()
        {
            for (int level = 1; level <= 5; level++)
            {
                int wins = 0;
                LevelResult last = null;
                System.Action<LevelResult> onWin = r => { wins++; last = r; };
                _app.Game.LevelWon += onWin;
                _app.Game.StartLevel(level);
                yield return null;
                Assert.AreEqual(AppState.Playing, _app.States.Current);
                yield return PlayToEnd();
                _app.Game.LevelWon -= onWin;

                Assert.AreEqual(AppState.LevelComplete, _app.States.Current, "level " + level);
                Assert.AreEqual(1, wins, "win callback count for level " + level);
                Assert.IsTrue(last.FirstClear);
                Assert.AreEqual(3, last.Stars);
                Assert.AreEqual(level + 1, _app.Services.Progression.HighestUnlocked);
            }
        }

        /// <summary>The one-tap move for a queue (scheduled levels decide the target themselves).</summary>
        private Move TapMove(int queue)
        {
            Move m;
            Assert.IsTrue(BoardRules.TryAutoMove(_app.Game.Board.State, queue, out m), "queue " + queue + " cannot move");
            return m;
        }

        [UnityTest]
        public IEnumerator IllegalMove_ChangesNothing_AndUndoRebuildsViews()
        {
            _app.Services.Progression.MarkTutorialDone("next_train"); // free play, no guided steps
            _app.Game.StartLevel(2);
            yield return null;
            BoardState state = _app.Game.Board.State;
            Assert.AreEqual(MoveError.None, _app.Game.TryMove(TapMove(0)));
            string key = _app.Game.Board.State.CanonicalKey();
            // Sending a passenger to a train of another line is refused and changes nothing.
            state = _app.Game.Board.State;
            int queue = state.Queues[1].IsEmpty ? 2 : 1;
            int wrong = -1;
            for (int p = 0; p < state.Platforms.Count; p++)
                if (MoveValidator.Validate(state, Move.FromQueue(queue, p)) != MoveError.None) wrong = p;
            Assert.GreaterOrEqual(wrong, 0, "expected a platform that refuses the passenger");
            Assert.AreNotEqual(MoveError.None, _app.Game.TryMove(Move.FromQueue(queue, wrong)));
            Assert.AreEqual(key, _app.Game.Board.State.CanonicalKey());

            int undoBefore = _app.Services.Boosters.Count(Boosters.BoosterType.Undo);
            _app.Game.UseUndo();
            yield return null;
            state = _app.Game.Board.State;
            Assert.AreEqual(0, state.MovesUsed);
            Assert.AreEqual(undoBefore - 1, _app.Services.Boosters.Count(Boosters.BoosterType.Undo));
            foreach (PassengerQueue q in state.Queues)
                for (int i = 0; i < q.Count; i++)
                    Assert.IsNotNull(_app.Station.GetPassenger(q[i].Id), "every queued passenger needs a view after undo");
        }

        [UnityTest]
        public IEnumerator Restart_RestoresTheOriginalLevel()
        {
            _app.Services.Progression.MarkTutorialDone("next_train");
            _app.Game.StartLevel(2);
            yield return null;
            string initial = _app.Game.Board.State.CanonicalKey();
            _app.Game.TryMove(TapMove(0));
            _app.Game.TryMove(TapMove(_app.Game.Board.State.Queues[1].IsEmpty ? 0 : 1));
            yield return null;
            _app.Game.Restart();
            yield return null;
            Assert.AreEqual(initial, _app.Game.Board.State.CanonicalKey());
            Assert.AreEqual(AppState.Playing, _app.States.Current);
            Assert.AreEqual(_app.Game.Level.Platforms.Count, _app.Station.Platforms.Count);
        }

        [UnityTest]
        public IEnumerator Progress_SurvivesARelaunch()
        {
            _app.Game.StartLevel(1);
            yield return null;
            yield return PlayToEnd();
            int coins = _app.Services.Economy.Coins;
            Object.Destroy(_app.gameObject);
            yield return null;

            _app = GameApp.Create(_store);
            yield return null;
            Assert.AreEqual(2, _app.Services.Progression.HighestUnlocked);
            Assert.AreEqual(coins, _app.Services.Economy.Coins);
            Assert.AreEqual(3, _app.Services.Progression.GetStars(1));
            Assert.AreEqual(2, _app.CurrentLevelId);
        }
    }
}
