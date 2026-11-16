using System;
using System.Collections.Generic;
using MetroSort.Levels;

namespace MetroSort.Puzzle
{
    /// <summary>
    /// Authoritative logical game for one level attempt. Owns the state, the undo history and the win/lose status.
    /// The presentation layer only ever reads this and animates the BoardEvents it returns.
    /// </summary>
    public sealed class PuzzleBoard
    {
        private struct Snapshot
        {
            public BoardState State;
            public GameStatus Status;
            public LoseReason Reason;
        }

        private readonly List<Snapshot> _history = new List<Snapshot>();

        public LevelData Level { get; private set; }
        public BoardState State { get; private set; }
        public GameStatus Status { get; private set; }
        public LoseReason LoseReason { get; private set; }
        public int ExtraPlatformsUsed { get; private set; }

        public bool CanUndo { get { return _history.Count > 0 && Status != GameStatus.Won; } }
        public bool CanAddExtraPlatform { get { return Status != GameStatus.Won && ExtraPlatformsUsed < Level.ExtraPlatforms; } }

        public PuzzleBoard(LevelData level)
        {
            if (level == null) throw new ArgumentNullException("level");
            Level = level;
            Restart();
        }

        public void Restart()
        {
            _history.Clear();
            ExtraPlatformsUsed = 0;
            State = BoardState.Create(Level);
            Reevaluate(null);
        }

        public MoveError Validate(Move move)
        {
            if (Status != GameStatus.Playing) return MoveError.GameOver;
            return MoveValidator.Validate(State, move);
        }

        /// <summary>Applies the move if legal. On any error the state is left untouched.</summary>
        public MoveError TryMove(Move move, List<BoardEvent> events)
        {
            MoveError err = Validate(move);
            if (err != MoveError.None) return err;
            _history.Add(new Snapshot { State = State.Clone(), Status = Status, Reason = LoseReason });
            BoardRules.Apply(State, move, events);
            Reevaluate(events);
            return MoveError.None;
        }

        /// <summary>Restores the state before the last move, including status, counters and special mechanics.</summary>
        public bool Undo()
        {
            if (!CanUndo) return false;
            Snapshot snap = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            State = snap.State;
            Status = snap.Status;
            LoseReason = snap.Reason;
            return true;
        }

        /// <summary>
        /// Extra Platform booster: adds one standard platform. Booster effects are permanent for the attempt,
        /// so the undo history is cleared (a booster can never be "undone" to get it back).
        /// </summary>
        public bool AddExtraPlatform(List<BoardEvent> events)
        {
            if (!CanAddExtraPlatform) return false;
            _history.Clear();
            ExtraPlatformsUsed++;
            State.Platforms.Add(new PlatformState { Capacity = 3, IsExtra = true });
            if (events != null) events.Add(new BoardEvent { Type = BoardEventType.PlatformAdded, Platform = State.Platforms.Count - 1 });
            Reevaluate(events);
            return true;
        }

        /// <summary>Replaces the state with an externally prepared one (used by the Shuffle booster).</summary>
        public void ReplaceState(BoardState newState)
        {
            if (newState == null) throw new ArgumentNullException("newState");
            _history.Clear();
            State = newState;
            Reevaluate(null);
        }

        public List<Move> GetLegalMoves()
        {
            var list = new List<Move>();
            if (Status == GameStatus.Playing) BoardRules.GetLegalMoves(State, list);
            return list;
        }

        private void Reevaluate(List<BoardEvent> events)
        {
            LoseReason reason;
            GameStatus status = BoardRules.Evaluate(State, out reason);
            Status = status;
            LoseReason = reason;
            if (events == null) return;
            if (status == GameStatus.Won) events.Add(new BoardEvent { Type = BoardEventType.Won });
            else if (status == GameStatus.Lost) events.Add(new BoardEvent { Type = BoardEventType.Lost, Reason = reason });
        }
    }
}
