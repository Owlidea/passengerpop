using System.Collections.Generic;
using MetroSort.Puzzle;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MetroSort.Game
{
    /// <summary>
    /// Touch and mouse input for the station.
    /// Tap-to-move: tap a front passenger, then a platform (assisted mode picks the obvious platform for you).
    /// Drag-and-drop: drag a front passenger onto a platform or the bench.
    /// Every attempt goes through GameController.TryMove, so input can never bypass the rules.
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        private GameApp _app;
        private bool _pressing, _dragging;
        private int _pressSource;
        private Vector2 _pressPosition;
        private PassengerView _dragView;

        public bool HasSelection { get; private set; }
        public int SelectedSource { get; private set; }

        public void Init(GameApp app)
        {
            _app = app;
            _app.Game.BoardChanged += Deselect;
        }

        private bool CanPlay()
        {
            GameController game = _app.Game;
            return _app.States.Current == AppState.Playing && game.Board != null && game.Board.Status == GameStatus.Playing;
        }

        private void Update()
        {
            if (!CanPlay()) CancelInteraction();
        }

        // Pointer events arrive from InputCatcher, a transparent full-screen UI element behind the HUD.
        // Going through the UI event system means even a tap that starts and ends within one frame is
        // delivered, and anything drawn above (buttons, panels) blocks it automatically.

        public void PointerDown(Vector2 pos)
        {
            _pressing = false;
            if (!CanPlay()) return;
            BoardState state = _app.Game.Board.State;
            StationView station = _app.Station;
            int source;
            if (station.PickSource(state, pos, out source) && _app.Tutorial.AllowsSource(source))
            {
                _pressing = true;
                _dragging = false;
                _pressSource = source;
                _pressPosition = pos;
            }
            else if (HasSelection)
            {
                int target = station.PickTarget(pos);
                if (target != StationView.NoTarget) Execute(new Move(SelectedSource, target));
                else Deselect();
            }
        }

        public void PointerDrag(Vector2 pos)
        {
            if (!_pressing || !CanPlay()) return;
            if (!_dragging && (pos - _pressPosition).sqrMagnitude > Mathf.Pow(Screen.height * 0.022f, 2f)) BeginDrag(_app.Game.Board.State);
            if (_dragging && _dragView != null) _dragView.transform.position = _app.Station.ScreenToGround(pos) + Vector3.up * 0.15f;
        }

        public void PointerUp(Vector2 pos)
        {
            if (!_pressing) return;
            _pressing = false;
            if (!CanPlay()) { CancelInteraction(); return; }
            if (_dragging) EndDrag(pos);
            else Tap(_app.Game.Board.State, _pressSource);
        }

        private void Tap(BoardState state, int source)
        {
            if (HasSelection && SelectedSource == source)
            {
                Deselect();
                return;
            }
            Select(state, source);
            if (!HasAnyTarget(state, source))
            {
                // Nowhere to go right now: say so instead of leaving a dead selection.
                PassengerView view = _app.Station.SourceView(state, source);
                if (view != null) view.Shake();
                _app.Audio.Play(Sfx.Invalid);
                if (source >= 0 && state.GateRemaining(source) > 0) _app.Game.Say("This turnstile is still closed");
                Deselect();
                return;
            }
            if (state.HasSchedule)
            {
                // One tap: the passenger boards their train if it is in, otherwise takes a seat on the bench.
                Move auto;
                if (BoardRules.TryAutoMove(state, source, out auto)) Execute(auto);
                else Deselect();
                return;
            }
            if (!GameSettings.AssistedMode || _app.Tutorial.IsGuiding) return;
            int target;
            if (TryAutoTarget(state, source, out target)) Execute(new Move(source, target));
        }

        /// <summary>
        /// Assisted mode: join a platform already holding this colour, otherwise take an empty platform when all
        /// empty ones are interchangeable. Anything ambiguous (or a level with a free bench) is left to the player.
        /// </summary>
        private bool TryAutoTarget(BoardState state, int source, out int target)
        {
            target = StationView.NoTarget;
            if (MoveValidator.Validate(state, new Move(source, Move.BenchTarget)) == MoveError.None) return false;
            int best = -1, bestCount = 0, firstEmpty = -1;
            bool emptiesMatch = true;
            for (int p = 0; p < state.Platforms.Count; p++)
            {
                if (MoveValidator.Validate(state, new Move(source, p)) != MoveError.None) continue;
                if (_app.Anim.IsPlatformBusy(p)) continue; // its train has not left yet
                PlatformState ps = state.Platforms[p];
                if (ps.Count > 0)
                {
                    if (ps.Count > bestCount) { best = p; bestCount = ps.Count; }
                }
                else if (firstEmpty < 0) firstEmpty = p;
                else
                {
                    PlatformState f = state.Platforms[firstEmpty];
                    if (f.Capacity != ps.Capacity || f.UsesLeft != ps.UsesLeft || f.ReservedColor != ps.ReservedColor) emptiesMatch = false;
                }
            }
            if (best >= 0 && firstEmpty < 0) { target = best; return true; }
            if (best >= 0 && state.Platforms[best].Capacity == 3) { target = best; return true; }
            if (best < 0 && firstEmpty >= 0 && emptiesMatch) { target = firstEmpty; return true; }
            return false;
        }

        private static bool HasAnyTarget(BoardState state, int source)
        {
            if (MoveValidator.Validate(state, new Move(source, Move.BenchTarget)) == MoveError.None) return true;
            for (int p = 0; p < state.Platforms.Count; p++)
                if (MoveValidator.Validate(state, new Move(source, p)) == MoveError.None) return true;
            return false;
        }

        private void Select(BoardState state, int source)
        {
            Deselect();
            PassengerView view = _app.Station.SourceView(state, source);
            if (view == null) return;
            HasSelection = true;
            SelectedSource = source;
            view.Selected = true;
            view.Bounce();
            _app.Audio.Play(Sfx.Select);
            _app.Station.ShowTargets(state, source);
            for (int p = 0; p < _app.Station.Platforms.Count; p++)
                if (_app.Anim.IsPlatformBusy(p)) _app.Station.Platforms[p].SetHighlight(false, false);
        }

        public void Deselect()
        {
            if (!HasSelection) return;
            HasSelection = false;
            GameController game = _app.Game;
            if (game.Board != null)
            {
                PassengerView view = _app.Station.SourceView(game.Board.State, SelectedSource);
                if (view != null) view.Selected = false;
            }
            if (game.HasHint) _app.Station.ShowHint(game.Hint);
            else _app.Station.ClearHighlights();
        }

        private void Execute(Move move)
        {
            GameController game = _app.Game;
            PassengerView view = _app.Station.SourceView(game.Board.State, move.Source);
            if (view != null) view.Selected = false;
            HasSelection = false;
            if (game.TryMove(move) != MoveError.None) _app.Station.ClearHighlights();
        }

        private void BeginDrag(BoardState state)
        {
            _dragging = true;
            Select(state, _pressSource);
            _dragView = _app.Station.SourceView(state, _pressSource);
            if (_dragView != null) _dragView.Held = true;
        }

        private void EndDrag(Vector2 pos)
        {
            _dragging = false;
            PassengerView view = _dragView;
            _dragView = null;
            if (view != null) view.Held = false;
            int target = _app.Station.PickTarget(pos);
            bool moved = false;
            if (target != StationView.NoTarget)
            {
                HasSelection = false;
                if (view != null) view.Selected = false;
                moved = _app.Game.TryMove(new Move(_pressSource, target)) == MoveError.None;
            }
            if (!moved)
            {
                if (view != null) view.WalkTo(view.Target, 0.2f, 0f, null);
                Deselect();
                _app.Station.ClearHighlights();
            }
        }

        private void CancelInteraction()
        {
            if (_dragView != null)
            {
                _dragView.Held = false;
                _dragView.WalkTo(_dragView.Target, 0.15f, 0f, null);
                _dragView = null;
            }
            _pressing = false;
            _dragging = false;
            Deselect();
        }
    }

    /// <summary>Transparent full-screen UI element that forwards pointer events on the station to the InputController.</summary>
    public sealed class InputCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public GameApp App;

        public void OnPointerDown(PointerEventData eventData) { if (App != null && App.Input != null) App.Input.PointerDown(eventData.position); }
        public void OnDrag(PointerEventData eventData) { if (App != null && App.Input != null) App.Input.PointerDrag(eventData.position); }
        public void OnPointerUp(PointerEventData eventData) { if (App != null && App.Input != null) App.Input.PointerUp(eventData.position); }
    }
}
