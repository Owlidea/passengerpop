using System.Collections.Generic;
using MetroSort.Analytics;
using MetroSort.Puzzle;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Contextual tutorials. A tutorial is one short line of text plus an optional number of guided moves.
    /// Guided moves always come from the solver, so the tutorial can never steer the player into a dead end.
    /// </summary>
    public sealed class TutorialManager : MonoBehaviour
    {
        private sealed class Definition
        {
            public string Message;
            public int GuidedMoves;
            public string After;

            public Definition(string message, int guidedMoves, string after)
            {
                Message = message;
                GuidedMoves = guidedMoves;
                After = after;
            }
        }

        private static readonly Dictionary<string, Definition> Definitions = new Dictionary<string, Definition>
        {
            { "select_move", new Definition("Tap a passenger, then tap a platform.", 3, "Three of a colour call their train. Send the rest!") },
            { "match_three", new Definition("Gather three of the same colour on one platform.", 2, "Keep each colour together.") },
            { "queue_advance", new Definition("When the front passenger leaves, the next one steps up.", 1, "Peek at who is waiting behind.") },
            { "capacity", new Definition("Only two platforms! Each holds one colour at a time.", 2, "Finish a train to free its platform.") },
            { "decision", new Definition("Look down the queues before you commit a platform.", 0, null) },
            { "locked", new Definition("A locked platform opens once the trains shown on it have left.", 0, null) },
            { "temporary", new Definition("This platform closes after a few departures. Spend it wisely.", 0, null) },
            { "vip", new Definition("VIPs wear a gold halo and never wait alone. Seat a friend first.", 0, null) },
            { "express", new Definition("The express leaves with just two, but only serves its own line.", 0, null) },
            { "bench", new Definition("Tap a passenger, then the bench to park them. Moves are limited!", 0, null) },
            { "stages", new Definition("A second wave arrives when the queues are empty.", 0, null) },
            { "long", new Definition("The long platform needs four passengers and serves one line only.", 0, null) },
            { "mystery", new Definition("Grey passengers hide their line until they move up the queue.", 0, null) },
            { "gate", new Definition("A closed turnstile opens after the number of trains shown on it have left.", 0, null) }
        };

        private GameApp _app;
        private Definition _definition;
        private string _id;
        private int _guidedLeft;
        private Move _expected;

        public bool IsGuiding { get; private set; }
        public Move Expected { get { return _expected; } }

        public void Init(GameApp app) { _app = app; }

        public void OnLevelStarted()
        {
            IsGuiding = false;
            _definition = null;
            _id = _app.Game.Level.Tutorial;
            if (string.IsNullOrEmpty(_id) || !Definitions.TryGetValue(_id, out _definition))
            {
                ShowText(null);
                return;
            }
            bool done = _app.Services.Progression.IsTutorialDone(_id);
            _guidedLeft = done ? 0 : _definition.GuidedMoves;
            ShowText(_definition.Message);
            if (!done) _app.Services.Analytics.Track(AnalyticsEvents.TutorialStarted, null);
            Advance(false);
        }

        public void OnMoveApplied()
        {
            if (_definition == null) return;
            if (IsGuiding) _guidedLeft--;
            Advance(true);
        }

        public void OnLevelCompleted()
        {
            IsGuiding = false;
            if (_definition == null) return;
            if (!_app.Services.Progression.IsTutorialDone(_id))
            {
                _app.Services.Progression.MarkTutorialDone(_id);
                _app.Services.Analytics.Track(AnalyticsEvents.TutorialCompleted, null);
            }
            ShowText(null);
            _definition = null;
        }

        private void Advance(bool afterMove)
        {
            PuzzleBoard board = _app.Game.Board;
            IsGuiding = _guidedLeft > 0 && board.Status == GameStatus.Playing && PuzzleSolver.TryGetHint(board.State, out _expected, 30000);
            if (!IsGuiding && afterMove && _guidedLeft <= 0 && _definition.After != null && _definition.GuidedMoves > 0)
                ShowText(_definition.After);
        }

        private string _shownText;

        private void ShowText(string text)
        {
            _shownText = text;
            _app.UI.SetTutorialText(text);
        }

        /// <summary>Re-sends the current line to a freshly built UI (after a language change).</summary>
        public void RefreshText()
        {
            _app.UI.SetTutorialText(_shownText);
        }

        public bool Allows(Move move) { return !IsGuiding || move.Equals(_expected); }
        public bool AllowsSource(int source) { return !IsGuiding || source == _expected.Source; }

        /// <summary>Keeps the on-screen pointer on the next thing to tap (tutorial step or paid hint).</summary>
        private void Update()
        {
            if (_app == null) return;
            GameController game = _app.Game;
            if (_app.States.Current != AppState.Playing || game.Board == null)
            {
                _app.UI.SetPointer(false, Vector3.zero);
                return;
            }
            Move move;
            if (IsGuiding) move = _expected;
            else if (game.HasHint) move = game.Hint;
            else
            {
                _app.UI.SetPointer(false, Vector3.zero);
                return;
            }
            InputController input = _app.Input;
            if (input.HasSelection && input.SelectedSource == move.Source)
            {
                _app.UI.SetPointer(true, _app.Station.TargetWorld(move.Target));
                if (IsGuiding) _app.Station.ShowHint(move);
            }
            else
            {
                PassengerView view = _app.Station.SourceView(game.Board.State, move.Source);
                _app.UI.SetPointer(view != null, view != null ? view.Target + Vector3.up * 1.0f : Vector3.zero);
            }
        }
    }
}
