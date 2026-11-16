using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MetroSort.Analytics;
using MetroSort.Boosters;
using MetroSort.Economy;
using MetroSort.Levels;
using MetroSort.Puzzle;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Runs one level attempt: owns the PuzzleBoard, routes moves and boosters to it and tells the presentation
    /// layer what happened. All rule decisions are delegated to the pure C# engine.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private GameApp _app;
        private readonly List<BoardEvent> _events = new List<BoardEvent>(8);
        private int _token, _assists, _retries, _stamp;
        private float _startTime;
        private bool _jobRunning;

        public PuzzleBoard Board { get; private set; }
        public LevelData Level { get { return Board != null ? Board.Level : null; } }
        public bool HasHint { get; private set; }
        public Move Hint { get; private set; }
        public bool IsThinking { get { return _jobRunning; } }

        public event Action BoardChanged;
        public event Action<LevelResult> LevelWon;
        public event Action<LoseReason> LevelLost;
        public event Action<string> Message;

        public void Init(GameApp app)
        {
            _app = app;
            _app.Anim.Finished += OnAnimationFinished;
        }

        // ------------------------------------------------------------------ level flow

        public void StartLevel(int levelId)
        {
            LevelData level = _app.Services.Levels.Get(levelId);
            _retries = Level != null && Level.Id == level.Id ? _retries + 1 : 0;
            Begin(level);
        }

        public void Restart()
        {
            if (Level == null) return;
            Track(AnalyticsEvents.LevelRestarted, null);
            _retries++;
            Begin(Level);
        }

        private void Begin(LevelData level)
        {
            _app.Anim.CancelAll();
            Board = new PuzzleBoard(level);
            _token = _app.Services.Progression.BeginAttempt(level.Id);
            _assists = 0;
            _startTime = Time.time;
            ClearHint();
            _stamp++;
            _app.Station.SetLevel(level);
            _app.Station.Rebuild(Board.State, true);
            _app.States.Set(AppState.Playing);
            _app.Tutorial.OnLevelStarted();
            Track(AnalyticsEvents.LevelStarted, null);
            RaiseChanged();
        }

        /// <summary>Shows a level behind the main menu without starting an attempt.</summary>
        public void ShowBackdrop(int levelId)
        {
            _app.Anim.CancelAll();
            Board = null;
            LevelData backdrop = _app.Services.Levels.Get(levelId);
            _app.Station.SetLevel(backdrop);
            _app.Station.Rebuild(BoardState.Create(backdrop));
        }

        public MoveError TryMove(Move move)
        {
            if (Board == null || _app.States.Current != AppState.Playing) return MoveError.GameOver;
            if (!_app.Tutorial.Allows(move))
            {
                _app.Audio.Play(Sfx.Invalid);
                return MoveError.BadTarget;
            }
            if (!move.IsToBench && _app.Anim.IsPlatformBusy(move.Target))
            {
                // The previous group has not left yet; keep the platform clear until its train departs.
                if (Board.Validate(move) == MoveError.None)
                {
                    _app.Audio.Play(Sfx.Invalid);
                    PassengerView waiting = _app.Station.SourceView(Board.State, move.Source);
                    if (waiting != null) waiting.Shake();
                    if (Message != null) Message("Wait for the train to leave");
                    return MoveError.PlatformBusy;
                }
            }
            _events.Clear();
            MoveError error = Board.TryMove(move, _events);
            if (error != MoveError.None)
            {
                Reject(move, error);
                return error;
            }
            ClearHint();
            _stamp++;
            _app.Station.ClearHighlights();
            _app.Anim.Play(_events, Board.State);
            _app.Tutorial.OnMoveApplied();
            RaiseChanged();
            return MoveError.None;
        }

        private void Reject(Move move, MoveError error)
        {
            _app.Audio.Play(Sfx.Invalid);
            PassengerView view = _app.Station.SourceView(Board.State, move.Source);
            if (view != null) view.Shake();
            if (Message == null) return;
            if (error == MoveError.VipNeedsCompany) Message("VIPs never wait alone");
            else if (error == MoveError.PlatformLocked) Message("That platform is still locked");
            else if (error == MoveError.PlatformClosed) Message("That platform is closed");
            else if (error == MoveError.BenchFull) Message("The bench is full");
            else if (error == MoveError.QueueGated) Message("This turnstile is still closed");
        }

        private void OnAnimationFinished(BoardEvent end)
        {
            if (Board == null || _app.States.Current != AppState.Playing) return;
            if (end.Type == BoardEventType.Won && Board.Status == GameStatus.Won) Win();
            else if (end.Type == BoardEventType.Lost && Board.Status == GameStatus.Lost) Lose();
        }

        private void Win()
        {
            GameServices s = _app.Services;
            int stars = ProgressionManager.ComputeStars(Board.State.MovesUsed, Level.Par, _assists);
            LevelResult result = s.Progression.CompleteLevel(_token, stars);
            if (result.Duplicate) return;
            s.Achievements.EvaluateProgress(Level.Id, s.Save.Data.Stars);
            _app.Tutorial.OnLevelCompleted();
            var p = new Dictionary<string, object>();
            p["stars"] = stars;
            Track(AnalyticsEvents.LevelCompleted, p);
            _app.Audio.Play(Sfx.LevelComplete);
            _app.States.Set(AppState.LevelComplete);
            if (LevelWon != null) LevelWon(result);
        }

        private void Lose()
        {
            var p = new Dictionary<string, object>();
            p["reason"] = Board.LoseReason.ToString();
            Track(AnalyticsEvents.LevelFailed, p);
            _app.Audio.Play(Sfx.LevelFailed);
            _app.States.Set(AppState.LevelFailed);
            if (LevelLost != null) LevelLost(Board.LoseReason);
        }

        // ------------------------------------------------------------------ boosters

        public void UseUndo()
        {
            AppState state = _app.States.Current;
            if (Board == null || _jobRunning || (state != AppState.Playing && state != AppState.LevelFailed)) return;
            if (_app.Tutorial.IsGuiding) return;
            BoosterUseResult r = _app.Services.Boosters.TryUse(BoosterType.Undo, () =>
            {
                if (!Board.Undo()) return false;
                ResyncAfterBooster();
                return true;
            });
            Report(r, BoosterType.Undo, "Nothing to undo");
        }

        public void UseExtraPlatform()
        {
            AppState state = _app.States.Current;
            bool jammed = state == AppState.LevelFailed && Board != null && Board.LoseReason == LoseReason.Deadlock;
            if (Board == null || _jobRunning || (state != AppState.Playing && !jammed)) return;
            BoosterUseResult r = _app.Services.Boosters.TryUse(BoosterType.ExtraPlatform, () =>
            {
                if (!Board.AddExtraPlatform(null)) return false;
                ResyncAfterBooster();
                return true;
            });
            Report(r, BoosterType.ExtraPlatform, "No room for another platform in this level");
        }

        public void UseHint()
        {
            if (Board == null || _jobRunning || _app.States.Current != AppState.Playing) return;
            if (HasHint) { _app.Station.ShowHint(Hint); return; }
            if (!_app.Services.Boosters.CanPay(BoosterType.Hint)) { Report(BoosterUseResult.NotAffordable, BoosterType.Hint, null); return; }
            BoardState snapshot = Board.State.Clone();
            Move found = default(Move);
            bool ok = false;
            StartCoroutine(RunJob(
                () => { Move m; ok = PuzzleSolver.TryGetHint(snapshot, out m, 150000); found = m; },
                () =>
                {
                    if (!ok) { Say("No winning path from here. Try Undo."); return; }
                    _app.Services.Boosters.Pay(BoosterType.Hint);
                    Hint = found;
                    HasHint = true;
                    _app.Station.ShowHint(found);
                    Report(BoosterUseResult.Used, BoosterType.Hint, null);
                }));
        }

        public void UseShuffle()
        {
            AppState state = _app.States.Current;
            bool jammed = state == AppState.LevelFailed && Board != null && Board.LoseReason == LoseReason.Deadlock;
            if (Board == null || _jobRunning || (state != AppState.Playing && !jammed)) return;
            if (_app.Tutorial.IsGuiding) return;
            if (!_app.Services.Boosters.CanPay(BoosterType.Shuffle)) { Report(BoosterUseResult.NotAffordable, BoosterType.Shuffle, null); return; }
            BoardState snapshot = Board.State.Clone();
            BoardState shuffled = null;
            int seed = Environment.TickCount;
            StartCoroutine(RunJob(
                () => { BoardState result; if (ShuffleService.TryShuffle(snapshot, seed, out result, 30000)) shuffled = result; },
                () =>
                {
                    // The booster is only charged once a provably winnable arrangement exists.
                    if (shuffled == null) { Report(BoosterUseResult.NoEffect, BoosterType.Shuffle, "No winnable shuffle here. Nothing was spent."); return; }
                    _app.Services.Boosters.Pay(BoosterType.Shuffle);
                    Board.ReplaceState(shuffled);
                    ResyncAfterBooster();
                    Report(BoosterUseResult.Used, BoosterType.Shuffle, null);
                }));
        }

        /// <summary>Runs solver work on a worker thread; the completion runs on the main thread only if the board is unchanged.</summary>
        private IEnumerator RunJob(Action work, Action done)
        {
            _jobRunning = true;
            int stamp = _stamp;
            RaiseChanged();
            Task task = Task.Run(work);
            while (!task.IsCompleted) yield return null;
            _jobRunning = false;
            if (stamp == _stamp && Board != null)
            {
                if (task.IsFaulted) Say("Something went wrong. Nothing was spent.");
                else done();
            }
            RaiseChanged();
        }

        private void ResyncAfterBooster()
        {
            _app.Anim.CancelAll();
            _app.Station.Rebuild(Board.State);
            ClearHint();
            _stamp++;
            if (Board.Status == GameStatus.Playing) _app.States.Set(AppState.Playing);
        }

        private void Report(BoosterUseResult result, BoosterType type, string noEffectMessage)
        {
            if (result == BoosterUseResult.Used)
            {
                _assists++;
                _app.Audio.Play(Sfx.Booster);
                var p = new Dictionary<string, object>();
                p["booster"] = type.ToString();
                Track(AnalyticsEvents.BoosterUsed, p);
            }
            else if (result == BoosterUseResult.NotAffordable)
            {
                _app.Audio.Play(Sfx.Invalid);
                Say(Loc.F("Not enough coins for {0}", Loc.T(type.ToString())));
            }
            else
            {
                _app.Audio.Play(Sfx.Invalid);
                if (noEffectMessage != null) Say(noEffectMessage);
            }
            RaiseChanged();
        }

        // ------------------------------------------------------------------ helpers

        private void ClearHint()
        {
            HasHint = false;
            Hint = default(Move);
        }

        public void Say(string text)
        {
            if (Message != null) Message(text);
        }

        private void RaiseChanged()
        {
            if (BoardChanged != null) BoardChanged();
        }

        private void Track(string eventName, Dictionary<string, object> p)
        {
            if (Level == null) return;
            if (p == null) p = new Dictionary<string, object>();
            p["level"] = Level.Id;
            p["moves"] = Board.State.MovesUsed;
            p["retries"] = _retries;
            p["seconds"] = Mathf.RoundToInt(Time.time - _startTime);
            _app.Services.Analytics.Track(eventName, p);
        }
    }
}
