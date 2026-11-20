using System.Collections.Generic;
using System.Text;
using MetroSort.Puzzle;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Builds and owns the 3D station for the current level and keeps passenger/platform views in step with a
    /// BoardState. It never changes game rules; Rebuild() can always recreate the exact picture from the state.
    /// </summary>
    public sealed class StationView : MonoBehaviour
    {
        public const int NoTarget = -2;
        // Camera tilt at the centre of the view, and its vertical field of view. The perspective makes the queues
        // in front larger than the tracks at the back, which gets passengers' faces readable on a phone.
        private const float Pitch = 50f;
        private const float Fov = 30f;

        public Camera Cam { get; private set; }
        public readonly List<PlatformView> Platforms = new List<PlatformView>();

        private ObjectPoolManager _pool;
        private readonly Dictionary<int, PassengerView> _passengers = new Dictionary<int, PassengerView>();
        private Transform _env, _props, _platformRoot, _actors;
        private string _signature;
        private Vector3[] _queueFront = new Vector3[0];
        private float _queueStep = 0.9f;
        private float _turnstileZ;
        private readonly List<GateFlaps> _flaps = new List<GateFlaps>();
        private Vector3 _benchCenter;
        private int _benchCapacity;
        private GameObject _benchGlow;
        private float _minX, _maxX, _minZ, _maxZ;
        // How far the framing reaches above _maxZ (in view units), and the painted wall's picture width (0 = none).
        private float _aboveTop, _wallSpan;
        // Share of the screen kept clear for the HUD at the top. A painted wall may run up to the HUD card itself.
        private float TopMargin { get { return _wallSpan > 0f ? 0.105f : 0.15f; } }
        // Share of the screen under the booster bar; the ticket gates sit just above it.
        private const float BottomMargin = 0.125f;
        private float _fittedAspect, _shake, _fitSize, _zoom;
        private Vector3 _cameraHome;
        private Transform _gateRoot;
        private GameObject[] _gateBars = new GameObject[0];
        private int[] _gateRemaining = new int[0];
        private int[] _gateColor = new int[0];

        public void Init(Camera cam, ObjectPoolManager pool)
        {
            Cam = cam;
            _pool = pool;
            _actors = new GameObject("Actors").transform;
            _actors.SetParent(transform, false);
        }

        // ------------------------------------------------------------------ sync

        /// <summary>Recreates every view from the logical state. Used on level start, undo, restart and boosters.</summary>
        /// <summary>Mystery queues: passengers at or beyond this position hide their line (0 = show everyone).</summary>
        public int VisibleDepth { get; private set; }
        public int ThemeIndex { get; private set; }
        private StationTheme Theme { get { return Chapters.Themes[ThemeIndex]; } }

        /// <summary>Call before Rebuild whenever the level changes.</summary>
        public void SetLevel(MetroSort.Levels.LevelData level)
        {
            VisibleDepth = level.Visible;
            ThemeIndex = Chapters.IndexFor(level.Id);
        }

        private bool IsHidden(int queueIndex)
        {
            return VisibleDepth > 0 && queueIndex >= VisibleDepth;
        }

        public void Rebuild(BoardState state, bool intro = false, RebuildStyle style = RebuildStyle.None)
        {
            // Where everybody stood, so a booster can animate from the old picture to the new one.
            Dictionary<int, Vector3> before = null;
            int platformsBefore = Platforms.Count;
            if (style != RebuildStyle.None && !GameSettings.ReducedMotion)
            {
                before = new Dictionary<int, Vector3>(_passengers.Count);
                foreach (KeyValuePair<int, PassengerView> kv in _passengers) before[kv.Key] = kv.Value.transform.position;
            }
            // The layout depends on the screen shape, so a different aspect ratio rebuilds the station too.
            string signature = Signature(state) + "/" + Mathf.RoundToInt((Cam != null ? Cam.aspect : 0.5625f) * 25f) + "/t" + ThemeIndex;
            if (signature != _signature)
            {
                BuildEnvironment(state);
                _signature = signature;
            }
            foreach (KeyValuePair<int, PassengerView> kv in _passengers) _pool.Release(PassengerView.PoolKey, kv.Value.gameObject);
            _passengers.Clear();
            // Gate barriers are sized for the queue they close, so they are rebuilt with the passengers.
            for (int g = 0; g < _gateBars.Length; g++)
                if (_gateBars[g] != null) { Destroy(_gateBars[g]); _gateBars[g] = null; }

            for (int q = 0; q < state.Queues.Length; q++)
            {
                for (int i = 0; i < state.Queues[q].Count; i++)
                {
                    PassengerView view = Spawn(state.Queues[q][i], QueueSlot(q, i), i == 0 && state.GateRemaining(q) == 0);
                    view.SetMystery(IsHidden(i), false);
                    // Level intro: each lane files in through its ticket gate, front of the queue first.
                    if (intro && !GameSettings.ReducedMotion)
                    {
                        Vector3 slot = QueueSlot(q, i);
                        var from = new Vector3(slot.x, 0f, _turnstileZ - 2.6f);
                        view.WalkIn(from, 0.05f + q * 0.05f + i * 0.09f, Mathf.Clamp((slot.z - from.z) / 9f, 0.35f, 1.0f));
                    }
                }
                if (intro && q < _flaps.Count && state.Queues[q].Count > 0)
                    _flaps[q].Open(0.05f + q * 0.05f + 0.12f, state.Queues[q].Count * 0.09f + 0.30f);
            }
            for (int b = 0; b < state.Bench.Count; b++) Spawn(state.Bench[b], BenchSlot(b), true).SetSeated(true);
            for (int p = 0; p < state.Platforms.Count && p < Platforms.Count; p++)
            {
                PlatformState ps = state.Platforms[p];
                for (int i = 0; i < ps.Occupants.Count; i++) Spawn(ps.Occupants[i], Platforms[p].SlotPosition(i), true);
            }
            RefreshPlatforms(state);
            ClearHighlights();
            if (intro) Punch(0.06f);
            if (before != null) Transition(before, style, platformsBefore);
        }

        /// <summary>Booster follow-through: undo hops people back, shuffle whirls them round, a new platform drops in.</summary>
        private void Transition(Dictionary<int, Vector3> before, RebuildStyle style, int platformsBefore)
        {
            if (style == RebuildStyle.ExtraPlatform)
            {
                if (Platforms.Count > platformsBefore && Platforms.Count > 0) Platforms[Platforms.Count - 1].DropIn();
                return;
            }
            Vector3 centre = Vector3.zero;
            int moved = 0;
            foreach (KeyValuePair<int, PassengerView> kv in _passengers)
            {
                PassengerView v = kv.Value;
                Vector3 old;
                if (!before.TryGetValue(kv.Key, out old))
                {
                    v.PopIn(0.05f); // back from a train that an undo recalled
                    continue;
                }
                if ((old - v.Target).sqrMagnitude < 0.01f) continue;
                if (style == RebuildStyle.Shuffle)
                    v.FlyFrom(old, (kv.Key % 6) * 0.035f, 1.3f + (kv.Key % 3) * 0.3f, kv.Key % 2 == 0 ? 360f : -360f);
                else
                    v.FlyFrom(old, 0f, 0.5f, 0f);
                centre += v.Target;
                moved++;
            }
            if (style == RebuildStyle.Shuffle && moved > 0)
            {
                centre /= moved;
                Fx.Sparkle(centre + Vector3.up * 1.6f, Palette.Gold, 18, 5f);
                Fx.Ring(centre + Vector3.up * 0.05f, Palette.Gold, 7f);
            }
        }

        public void RefreshPlatforms(BoardState state)
        {
            for (int p = 0; p < state.Platforms.Count && p < Platforms.Count; p++) Platforms[p].Refresh(state.Platforms[p], state.HasSchedule);
            RefreshGates(state);
            RefreshMoods(state);
        }

        /// <summary>
        /// Passengers react to the situation: a platform one short of a full group is excited, and when the
        /// bench is full or only a few moves remain those still waiting get nervous.
        /// </summary>
        private void RefreshMoods(BoardState state)
        {
            bool tight = (state.BenchCapacity > 0 && state.Bench.Count >= state.BenchCapacity) || state.MovesRemaining <= 3;
            for (int q = 0; q < state.Queues.Length; q++)
            {
                for (int i = 0; i < state.Queues[q].Count; i++)
                {
                    PassengerView v;
                    if (!_passengers.TryGetValue(state.Queues[q][i].Id, out v)) continue;
                    v.Eager = false;
                    v.Worried = tight && i == 0;
                }
            }
            for (int b = 0; b < state.Bench.Count; b++)
            {
                PassengerView v;
                if (!_passengers.TryGetValue(state.Bench[b].Id, out v)) continue;
                v.Eager = false;
                v.Worried = tight;
            }
            for (int p = 0; p < state.Platforms.Count; p++)
            {
                PlatformState ps = state.Platforms[p];
                bool eager = ps.IsOpen && ps.Capacity >= 2 && ps.Count == ps.Capacity - 1;
                for (int i = 0; i < ps.Occupants.Count; i++)
                {
                    PassengerView v;
                    if (!_passengers.TryGetValue(ps.Occupants[i].Id, out v)) continue;
                    v.Eager = eager;
                    v.Worried = false;
                }
            }
        }

        // ------------------------------------------------------------------ gated queues

        private const float GateOffset = 0.95f;

        public int GateLaneCount { get { return _gateRemaining.Length; } }

        /// <summary>Caption for a closed turnstile ("2 trains"), or null when the lane is open.</summary>
        public string GateLabel(int queue)
        {
            int n = queue < _gateRemaining.Length ? _gateRemaining[queue] : 0;
            if (n <= 0) return null;
            int color = queue < _gateColor.Length ? _gateColor[queue] : ColorCodes.Any;
            if (color >= 0) return Loc.F(n == 1 ? "{0} {1} train" : "{0} {1} trains", n, Loc.T(ColorCodes.Names[color]));
            return Loc.F(n == 1 ? "{0} train" : "{0} trains", n);
        }

        public Vector3 GateAnchor(int queue)
        {
            Vector3 front = queue < _queueFront.Length ? _queueFront[queue] : Vector3.zero;
            return new Vector3(front.x, 1.25f, front.z + GateOffset);
        }

        /// <summary>Shows a barrier in front of every queue whose turnstile is still closed.</summary>
        private void RefreshGates(BoardState state)
        {
            if (_gateBars.Length != _queueFront.Length)
            {
                _gateBars = new GameObject[_queueFront.Length];
                _gateRemaining = new int[_queueFront.Length];
                _gateColor = new int[_queueFront.Length];
            }
            for (int q = 0; q < _queueFront.Length; q++)
            {
                int remaining = q < state.Queues.Length && !state.Queues[q].IsEmpty ? state.GateRemaining(q) : 0;
                _gateRemaining[q] = remaining;
                _gateColor[q] = state.GateColor(q);
                if (remaining > 0 && _gateBars[q] == null) _gateBars[q] = BuildGate(q, state.Queues[q].Count, _gateColor[q]);
                if (_gateBars[q] == null) continue;
                var boom = _gateBars[q].GetComponent<BoomGate>();
                if (remaining > 0)
                {
                    if (!_gateBars[q].activeSelf || boom.Opening) boom.Close();
                }
                else boom.Open(); // the bar swings up, then the gate folds away
            }
        }

        private GameObject BuildGate(int queue, int depth, int color)
        {
            var root = new GameObject("QueueGate");
            root.transform.SetParent(_gateRoot, false);
            root.transform.localPosition = new Vector3(_queueFront[queue].x, 0f, _queueFront[queue].z);
            var steel = new Color(0.52f, 0.56f, 0.64f);
            // A colour-keyed turnstile wears the colour of the line it waits for.
            // An "any train" turnstile is a black and yellow hazard bar, so it cannot be mistaken for a line colour.
            Color red = color >= 0 ? Palette.Lines[color] : new Color(0.17f, 0.18f, 0.22f);
            Color stripe = color >= 0 ? Color.white : new Color(1.00f, 0.82f, 0.10f);
            // The lane is "switched off" while its turnstile is closed.
            float laneLength = (depth - 1) * _queueStep + 1.0f;
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.014f, -laneLength * 0.5f + 0.5f), new Vector3(0.96f, 0.02f, laneLength), new Color(0.56f, 0.58f, 0.64f), "ClosedLane"));
            // Barrier across the lane, far enough ahead that the front passenger does not hide it.
            const float z = GateOffset;
            Prims.Make(PrimitiveType.Cube, root.transform, new Vector3(-0.56f, 0.38f, z), new Vector3(0.14f, 0.76f, 0.14f), steel, "Post");
            Prims.Make(PrimitiveType.Cube, root.transform, new Vector3(0.56f, 0.38f, z), new Vector3(0.14f, 0.76f, 0.14f), steel, "Post");
            var arm = new GameObject("Arm").transform;
            arm.SetParent(root.transform, false);
            arm.localPosition = new Vector3(-0.56f, 0.62f, z);
            Prims.Make(PrimitiveType.Cube, arm, new Vector3(0.56f, 0f, 0f), new Vector3(1.00f, 0.24f, 0.10f), red, "Bar");
            for (int i = -1; i <= 1; i++)
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, arm, new Vector3(0.56f + i * 0.30f, 0f, -0.005f), new Vector3(0.11f, 0.245f, 0.11f), stripe, "Stripe"));
            root.AddComponent<BoomGate>().Arm = arm;
            return root;
        }

        /// <summary>Moves queue and bench passengers to where the state says they stand.</summary>
        public void SyncQueues(BoardState state, bool animate)
        {
            for (int q = 0; q < state.Queues.Length; q++)
            {
                for (int i = 0; i < state.Queues[q].Count; i++)
                {
                    PassengerView v;
                    if (!_passengers.TryGetValue(state.Queues[q][i].Id, out v)) continue;
                    v.SetAccessible(i == 0 && state.GateRemaining(q) == 0);
                    v.SetMystery(IsHidden(i), animate);
                    MoveView(v, QueueSlot(q, i), animate, i * 0.04f);
                }
            }
            for (int b = 0; b < state.Bench.Count; b++)
            {
                PassengerView v;
                if (!_passengers.TryGetValue(state.Bench[b].Id, out v)) continue;
                v.SetAccessible(true);
                v.SetSeated(true);
                MoveView(v, BenchSlot(b), animate, 0f);
            }
        }

        private static void MoveView(PassengerView v, Vector3 target, bool animate, float delay)
        {
            if ((v.Target - target).sqrMagnitude < 0.0001f && !v.Held) return;
            // The queue shuffles forward one after another with a small hop.
            if (animate) v.WalkTo(target, 0.24f, 0.10f, null, delay);
            else v.Place(target);
        }

        /// <summary>Creates views for passengers that appeared in the queues (multi-stage levels).</summary>
        public void SpawnMissing(BoardState state)
        {
            for (int q = 0; q < state.Queues.Length; q++)
            {
                for (int i = 0; i < state.Queues[q].Count; i++)
                {
                    Passenger p = state.Queues[q][i];
                    if (_passengers.ContainsKey(p.Id)) continue;
                    Vector3 slot = QueueSlot(q, i);
                    PassengerView fresh = Spawn(p, slot, i == 0);
                    fresh.SetMystery(IsHidden(i), false);
                    if (GameSettings.ReducedMotion) { fresh.Bounce(); continue; }
                    // The next stage files in through the ticket gates, like the level intro.
                    var from = new Vector3(slot.x, 0f, _turnstileZ - 2.6f);
                    fresh.WalkIn(from, 0.05f + i * 0.09f, Mathf.Clamp((slot.z - from.z) / 9f, 0.35f, 1.0f));
                    if (q < _flaps.Count) _flaps[q].Open(0.15f, state.Queues[q].Count * 0.09f + 0.30f);
                }
            }
        }

        private PassengerView Spawn(Passenger p, Vector3 position, bool accessible)
        {
            GameObject go = _pool.Get(PassengerView.PoolKey, PassengerView.CreateObject);
            go.transform.SetParent(_actors, false);
            var view = go.GetComponent<PassengerView>();
            view.Configure(p);
            view.Place(position);
            view.SetAccessible(accessible);
            _passengers[p.Id] = view;
            return view;
        }

        public PassengerView GetPassenger(int id)
        {
            PassengerView v;
            return _passengers.TryGetValue(id, out v) ? v : null;
        }

        /// <summary>Removes a view from bookkeeping (it is boarding a train) without destroying it.</summary>
        public PassengerView Detach(int id)
        {
            PassengerView v;
            if (_passengers.TryGetValue(id, out v)) _passengers.Remove(id);
            return v;
        }

        public void ReleasePassenger(PassengerView view)
        {
            if (view != null) _pool.Release(PassengerView.PoolKey, view.gameObject);
        }

        public TrainView AcquireTrain()
        {
            GameObject go = _pool.Get(TrainView.PoolKey, TrainView.CreateObject);
            go.transform.SetParent(_actors, false);
            return go.GetComponent<TrainView>();
        }

        public void ReleaseTrain(TrainView train)
        {
            if (train != null) _pool.Release(TrainView.PoolKey, train.gameObject);
        }

        public PassengerView SourceView(BoardState state, int source)
        {
            if (source >= 0)
            {
                if (source >= state.Queues.Length || state.Queues[source].IsEmpty) return null;
                return GetPassenger(state.Queues[source].Front.Id);
            }
            int bench = -1 - source;
            return bench < state.Bench.Count ? GetPassenger(state.Bench[bench].Id) : null;
        }

        // ------------------------------------------------------------------ positions

        public Vector3 QueueSlot(int queue, int index)
        {
            Vector3 front = queue < _queueFront.Length ? _queueFront[queue] : Vector3.zero;
            return new Vector3(front.x, 0f, front.z - index * _queueStep);
        }

        public Vector3 BenchSlot(int index)
        {
            return _benchCenter + new Vector3((index - (_benchCapacity - 1) * 0.5f) * 1.0f, 0.19f, -0.04f);
        }

        public Vector3 TargetWorld(int target)
        {
            if (target == Move.BenchTarget) return _benchCenter + Vector3.up * 0.6f;
            return Platforms[target].transform.position + Vector3.up * 0.6f;
        }

        // ------------------------------------------------------------------ picking (screen space, no physics)

        /// <summary>Finds the movable passenger nearest to a screen point. Returns a Move.Source code.</summary>
        public bool PickSource(BoardState state, Vector2 screen, out int source)
        {
            source = 0;
            float best = Screen.height * 0.065f;
            best *= best;
            bool found = false;
            for (int q = 0; q < state.Queues.Length; q++)
            {
                if (state.Queues[q].IsEmpty) continue;
                if (TryCloser(state.Queues[q].Front.Id, screen, ref best)) { source = q; found = true; }
            }
            for (int b = 0; b < state.Bench.Count; b++)
                if (TryCloser(state.Bench[b].Id, screen, ref best)) { source = -1 - b; found = true; }
            return found;
        }

        private bool TryCloser(int id, Vector2 screen, ref float bestSqr)
        {
            PassengerView v;
            if (!_passengers.TryGetValue(id, out v)) return false;
            Vector3 sp = Cam.WorldToScreenPoint(v.Target + Vector3.up * 0.6f);
            float d = ((Vector2)sp - screen).sqrMagnitude;
            if (d >= bestSqr) return false;
            bestSqr = d;
            return true;
        }

        /// <summary>Returns a platform index, Move.BenchTarget, or NoTarget.</summary>
        public int PickTarget(Vector2 screen)
        {
            for (int p = 0; p < Platforms.Count; p++)
            {
                PlatformView pv = Platforms[p];
                if (InsideGroundRect(screen, pv.transform.position, pv.Width * 0.5f + 0.2f, PlatformView.Depth * 0.5f + 0.2f, 1.3f)) return p;
            }
            if (_benchCapacity > 0 && InsideGroundRect(screen, _benchCenter, _benchCapacity * 0.5f + 0.5f, 0.7f, 1.2f)) return Move.BenchTarget;
            return NoTarget;
        }

        private bool InsideGroundRect(Vector2 screen, Vector3 center, float halfX, float halfZ, float height)
        {
            // Screen bounds of the box's eight corners: in perspective the near and far edges differ in width.
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector2 p = Cam.WorldToScreenPoint(center + new Vector3((i & 1) == 0 ? -halfX : halfX, (i & 2) == 0 ? 0f : height, (i & 4) == 0 ? -halfZ : halfZ));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return screen.x >= min.x && screen.x <= max.x && screen.y >= min.y && screen.y <= max.y;
        }

        public Vector3 ScreenToGround(Vector2 screen)
        {
            Ray ray = Cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return Vector3.zero;
            float t = -ray.origin.y / ray.direction.y;
            return ray.origin + ray.direction * t;
        }

        // ------------------------------------------------------------------ highlights

        public void ShowTargets(BoardState state, int source)
        {
            for (int p = 0; p < Platforms.Count; p++)
                Platforms[p].SetHighlight(MoveValidator.Validate(state, new Move(source, p)) == MoveError.None, false);
            if (_benchGlow != null) _benchGlow.SetActive(MoveValidator.Validate(state, new Move(source, Move.BenchTarget)) == MoveError.None);
        }

        public void ShowHint(Move hint)
        {
            ClearHighlights();
            if (hint.IsToBench) { if (_benchGlow != null) _benchGlow.SetActive(true); }
            else if (hint.Target < Platforms.Count) Platforms[hint.Target].SetHighlight(true, true);
        }

        public void ClearHighlights()
        {
            for (int p = 0; p < Platforms.Count; p++) Platforms[p].SetHighlight(false, false);
            if (_benchGlow != null) _benchGlow.SetActive(false);
        }

        // ------------------------------------------------------------------ environment

        private static string Signature(BoardState s)
        {
            int maxQueues = 0, maxDepth = 0;
            Measure(s, out maxQueues, out maxDepth);
            var sb = new StringBuilder();
            sb.Append(maxQueues).Append('/').Append(maxDepth).Append('/').Append(s.BenchCapacity).Append('/');
            foreach (PlatformState p in s.Platforms) sb.Append(p.Capacity).Append(p.Express ? 'e' : 'n');
            return sb.ToString();
        }

        private static void Measure(BoardState s, out int maxQueues, out int maxDepth)
        {
            maxQueues = 1;
            maxDepth = 1;
            foreach (Passenger[][] stage in s.AllStages)
            {
                maxQueues = Mathf.Max(maxQueues, stage.Length);
                foreach (Passenger[] q in stage) maxDepth = Mathf.Max(maxDepth, q.Length);
            }
        }

        private void BuildEnvironment(BoardState state)
        {
            if (_env != null) Destroy(_env.gameObject);
            if (_props != null) Destroy(_props.gameObject);
            if (_platformRoot != null) Destroy(_platformRoot.gameObject);
            if (_gateRoot != null) Destroy(_gateRoot.gameObject);
            _gateRoot = new GameObject("Gates").transform;
            _gateRoot.SetParent(transform, false);
            _gateBars = new GameObject[0];
            Platforms.Clear();
            // Platforms animate (pulse, highlight), so they live outside the statically batched environment.
            _platformRoot = new GameObject("Platforms").transform;
            _platformRoot.SetParent(transform, false);
            _env = new GameObject("Environment").transform;
            _env.SetParent(transform, false);
            // Modelled props carry several materials each, so they stay out of the per-material merge below.
            _props = new GameObject("Props").transform;
            _props.SetParent(transform, false);

            int maxQueues, maxDepth;
            Measure(state, out maxQueues, out maxDepth);
            _benchCapacity = state.BenchCapacity;

            // Platforms: one row for up to two, two rows beyond that. A narrower station lets the portrait
            // camera zoom in, so passengers and touch targets stay large on a phone.
            int n = state.Platforms.Count;
            // Timetable levels give each of two platforms its own track, so a waiting train never blocks another.
            int backCount = n <= 2 && !state.HasSchedule ? n : (n + 1) / 2;
            float frontRowZ = 4.6f, backRowZ = backCount == n ? 5.2f : 8.2f;
            float widest = 0f;
            widest = Mathf.Max(widest, LayoutRow(state, 0, backCount, backRowZ));
            if (n > backCount) widest = Mathf.Max(widest, LayoutRow(state, backCount, n - backCount, frontRowZ));
            float lastTrackZ = backRowZ + 1.45f;

            // Queues.
            float spacing = Mathf.Clamp(8.0f / maxQueues, 1.15f, 1.8f);
            float frontZ = _benchCapacity > 0 ? 0.5f : 1.7f;
            // The view is fitted to the station's width, so tall phones have vertical room to spare. Give that
            // room to the queues: waiting passengers stand further apart and their faces stay visible.
            float aspect = Cam != null && Cam.aspect > 0.01f ? Cam.aspect : 0.5625f;
            float previewHalfWidth = Mathf.Max(widest, (maxQueues - 1) * spacing + 1.2f) * 0.5f + 0.6f;
            float viewSize = previewHalfWidth / (aspect * 0.97f);
            float sinPitch = Mathf.Sin(Pitch * Mathf.Deg2Rad), cosPitch = Mathf.Cos(Pitch * Mathf.Deg2Rad);
            // A painted wall is shown whole: the picture (4:1) spans a little more than the view, and the framing
            // reaches its top so it reads as a real backdrop instead of a sliver under the HUD. The queues give up
            // the vertical room; the view is fitted to the width, so platforms and passengers keep their size.
            _wallSpan = Chapters.PaintingFor(ThemeIndex) >= 0 ? Mathf.Max(8f, previewHalfWidth * 2f * 1.12f) : 0f;
            // The picture's top edge meets the bottom of the HUD card rather than the usual safety margin.
            _aboveTop = _wallSpan > 0f ? 0.2f * sinPitch + _wallSpan / 4f : 1.7f * cosPitch;
            float visibleDepth = (2f * viewSize * (1f - TopMargin - BottomMargin) - _aboveTop) / sinPitch;
            float fixedDepth = (lastTrackZ + 0.9f - frontZ) + 1.65f;
            float compactStep = Mathf.Min(1.0f, 5.0f / Mathf.Max(1, maxDepth - 1));
            float roomyStep = (visibleDepth - fixedDepth) / Mathf.Max(1, maxDepth - 1);
            _queueStep = Mathf.Clamp(roomyStep, compactStep, 1.45f);
            PassengerView.WaitingScale = Mathf.Lerp(0.84f, 0.98f, Mathf.InverseLerp(1.0f, 1.4f, _queueStep));
            _queueFront = new Vector3[maxQueues];
            for (int q = 0; q < maxQueues; q++)
            {
                float x = (q - (maxQueues - 1) * 0.5f) * spacing;
                _queueFront[q] = new Vector3(x, 0f, frontZ);
                float laneLength = (maxDepth - 1) * _queueStep + 1.0f;
                GameObject lane = Prims.Make(PrimitiveType.Cube, _env, new Vector3(x, 0.011f, frontZ - laneLength * 0.5f + 0.5f), new Vector3(0.95f, 0.02f, laneLength), Theme.Lane, "Lane");
                Prims.NoShadow(lane);
                GameObject spot = Prims.Make(PrimitiveType.Cylinder, _env, new Vector3(x, 0.016f, frontZ), new Vector3(0.90f, 0.012f, 0.90f), new Color(0.97f, 0.98f, 1f), "FrontSpot");
                Prims.NoShadow(spot);
                // Gold piping along both edges turns the lane into a carpet runner.
                for (int e = -1; e <= 1; e += 2)
                    Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(x + e * 0.455f, 0.024f, frontZ - laneLength * 0.5f + 0.5f), new Vector3(0.04f, 0.012f, laneLength), Palette.Gold, "LaneEdge"));
            }
            float queuesWidth = (maxQueues - 1) * spacing + 1.2f;

            // Brass stanchions and red velvet ropes fence the queue area, as on the title screen.
            {
                float laneLength = (maxDepth - 1) * _queueStep + 1.0f;
                float zTop = frontZ + 0.15f, zEnd = frontZ - laneLength + 0.75f;
                int posts = Mathf.Max(2, Mathf.CeilToInt((zTop - zEnd) / 1.5f) + 1);
                float step = (zTop - zEnd) / (posts - 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    float sx = side * ((maxQueues - 1) * spacing * 0.5f + 0.62f);
                    for (int i = 0; i < posts; i++)
                    {
                        float pz = zTop - i * step;
                        SceneProp("Stanchion", new Vector3(sx, 0f, pz));
                        if (i == 0) continue;
                        GameObject rope = Prims.Make(PrimitiveType.Cylinder, _env, new Vector3(sx, 0.42f, pz + step * 0.5f), new Vector3(0.05f, step * 0.5f - 0.05f, 0.05f), new Color(0.82f, 0.16f, 0.22f), "Rope");
                        rope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        Prims.NoShadow(rope);
                    }
                }
            }

            // Waiting bench (gameplay element, only in levels that enable it).
            _benchGlow = null;
            if (_benchCapacity > 0)
            {
                _benchCenter = new Vector3(0f, 0f, 2.35f);
                float w = _benchCapacity * 1.0f + 0.5f;
                _benchGlow = Prims.Make(PrimitiveType.Cube, _env, _benchCenter + new Vector3(0f, 0.02f, 0f), new Vector3(w + 0.3f, 0.04f, 1.3f), Palette.Highlight, "BenchGlow");
                Prims.NoShadow(_benchGlow);
                _benchGlow.SetActive(false);
                // Wooden seats with a cushion for each place, and a cast-iron arm between and at the ends of them.
                for (int b = 0; b < _benchCapacity; b++)
                    SceneProp("BenchSeat", _benchCenter + new Vector3((b - (_benchCapacity - 1) * 0.5f) * 1.0f, 0f, 0f));
                for (int b = 0; b <= _benchCapacity; b++)
                    SceneProp("BenchArm", _benchCenter + new Vector3((b - _benchCapacity * 0.5f) * 1.0f, 0f, 0f));
            }

            float halfWidth = Mathf.Max(widest, queuesWidth) * 0.5f + 0.6f;
            float laneEndZ = frontZ - (maxDepth - 1) * _queueStep - 0.6f;
            _minX = -halfWidth;
            _maxX = halfWidth;
            _minZ = laneEndZ - 1.05f;
            _maxZ = lastTrackZ + 0.9f;

            // Tracks run in sunken trenches like a real station, so the concourse floor is laid in strips between them.
            var trackZs = new List<float>();
            if (n > backCount) trackZs.Add(frontRowZ + 1.45f);
            trackZs.Add(backRowZ + 1.45f);
            float wallZ = lastTrackZ + 1.3f;
            float stripStart = -26f;
            foreach (float tz in trackZs)
            {
                BuildFloorStrip(stripStart, tz - TrenchHalf);
                stripStart = tz + TrenchHalf;
                BuildTrack(tz, halfWidth);
            }
            BuildFloorStrip(stripStart, wallZ);
            for (int ix = -5; ix <= 5; ix++)
            {
                for (int iz = -7; iz <= 8; iz++)
                {
                    if (((ix + iz) & 1) != 0) continue;
                    float cz = iz * 1.6f;
                    bool clear = cz < wallZ - 0.9f;
                    foreach (float tz in trackZs) if (Mathf.Abs(cz - tz) < TrenchHalf + 0.82f) clear = false;
                    // The floor texture brings its own tiles; the flat checker is only the fallback.
                    if (clear && !_texturedFloor) Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(ix * 1.6f, 0.003f, cz), new Vector3(1.6f, 0.006f, 1.6f), Theme.Tile, "Tile"));
                }
            }

            // In perspective the back of the station sees more width than the front: widen the painted wall to the
            // view at its depth, so its picture never repeats at the sides.
            if (_wallSpan > 0f) FitWallToView(wallZ - 0.21f, sinPitch);
            BuildBackWall(wallZ);
            BuildTurnstiles(maxQueues, spacing, laneEndZ - 0.40f);

            // Decoration kept to the sides so it never hides gameplay.
            float sideX = halfWidth + 0.9f;
            for (int side = -1; side <= 1; side += 2)
            {
                BuildPlant(new Vector3(side * sideX, 0f, 2.6f));
                BuildLamp(new Vector3(side * (sideX + 0.2f), 0f, 0.6f));
                if (_benchCapacity == 0) Prop("Bench", new Vector3(side * (sideX + 0.1f), 0f, -1.0f));
                Prop("Bin", new Vector3(side * (sideX + 0.15f), 0f, -2.6f));
            }

            // Nothing in the environment moves: merge it into one mesh per material so the floor tiles, sleepers,
            // railings and wall details cost a handful of draw calls instead of several hundred.
            if (_benchGlow != null) _benchGlow.transform.SetParent(_platformRoot, true);
            MeshFactory.MergeStatic(_env);

            FitCamera();
        }

        private float LayoutRow(BoardState state, int first, int count, float z)
        {
            const float gap = 0.75f;
            float total = -gap;
            for (int i = 0; i < count; i++) total += PlatformView.WidthFor(state.Platforms[first + i].Capacity) + gap;
            float x = -total * 0.5f;
            for (int i = 0; i < count; i++)
            {
                PlatformState ps = state.Platforms[first + i];
                float w = PlatformView.WidthFor(ps.Capacity);
                Platforms.Add(PlatformView.Create(_platformRoot, first + i, ps, new Vector3(x + w * 0.5f, 0f, z)));
                x += w + gap;
            }
            return total;
        }

        private const float TrenchHalf = 0.88f;
        private const float TrenchDepth = 0.40f;
        private static readonly Color Concrete = new Color(0.50f, 0.52f, 0.60f);
        private static readonly Color Steel = new Color(0.74f, 0.77f, 0.84f);
        private static readonly Color Ballast = new Color(0.66f, 0.61f, 0.54f);
        private static readonly Color BallastDark = new Color(0.52f, 0.47f, 0.41f);
        private static readonly Color SleeperWood = new Color(0.40f, 0.26f, 0.17f);
        private static readonly Color RailSteel = new Color(0.90f, 0.92f, 0.97f);
        private static readonly Color TrenchWallColor = new Color(0.33f, 0.35f, 0.43f);

        private void BuildFloorStrip(float z0, float z1)
        {
            if (z1 <= z0) return;
            GameObject floor = Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, -0.3f, (z0 + z1) * 0.5f), new Vector3(60f, 0.6f, z1 - z0), Theme.Floor, "Floor");
            Prims.NoShadow(floor);
            // Stone tiles (two per texture, 1.6 units each) in the chapter's floor colour, with a soft polish.
            _texturedFloor = MaterialLibrary.Paint(floor, "tex_floor", Color.Lerp(Theme.Floor, Theme.Tile, 0.5f), new Vector4(1f / 3.2f, 1f / 3.2f, 0f, 0f), false, true);
        }

        private bool _texturedFloor;

        /// <summary>A sunken track with rails, sleepers, safety lines along both edges and a tunnel mouth at each end.</summary>
        private void BuildTrack(float z, float halfWidth)
        {
            float bed = -TrenchDepth;
            // Pale ballast, dark wooden sleepers and bright rails: strong contrast so the track reads at a glance.
            GameObject ballast = Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, bed - 0.1f, z), new Vector3(60f, 0.2f, TrenchHalf * 2f), Ballast, "Ballast");
            // Painted track bed: six sleepers per picture, laid at the same 0.62 spacing as the modelled ones,
            // and stretched once across the trench.
            float across = TrenchHalf * 2f;
            bool painted = MaterialLibrary.Paint(ballast, "tex_track", Color.white, new Vector4(1f / 3.72f, 1f / across, 0.5f, -(z - TrenchHalf) / across), true);
            if (!painted)
            {
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, bed + 0.004f, z), new Vector3(60f, 0.008f, 1.02f), BallastDark, "BallastCentre"));
                for (int i = -22; i <= 22; i++)
                {
                    Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(i * 0.62f, bed + 0.035f, z), new Vector3(0.30f, 0.07f, 1.36f), SleeperWood, "Sleeper"));
                    // Loose stones break up the flat ballast between sleepers.
                    float jitter = ((i * 37) % 11) * 0.05f - 0.25f;
                    Prims.NoShadow(Prims.Make(PrimitiveType.Sphere, _env, new Vector3(i * 0.62f + 0.31f, bed + 0.01f, z + jitter), new Vector3(0.13f, 0.06f, 0.11f), BallastDark, "Stone"));
                }
            }
            for (int i = -1; i <= 1; i += 2)
            {
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, -TrenchDepth * 0.5f, z + i * (TrenchHalf - 0.02f)), new Vector3(60f, TrenchDepth, 0.04f), TrenchWallColor, "TrenchWall"));
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, bed + 0.085f, z + i * 0.42f), new Vector3(60f, 0.03f, 0.22f), new Color(0.30f, 0.31f, 0.36f), "RailFoot"));
                GameObject rail = Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, bed + 0.15f, z + i * 0.42f), new Vector3(60f, 0.11f, 0.12f), RailSteel, "Rail");
                Prims.SetGlossy(rail, RailSteel);
                Prims.NoShadow(rail);
                // Yellow safety line and a pale tactile strip on the concourse along the trench edge.
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 0.012f, z + i * (TrenchHalf + 0.09f)), new Vector3(60f, 0.02f, 0.14f), Palette.Gold, "SafetyLine"));
            }
            // Orange power rail on its insulators along the far side of the trench.
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, bed + 0.17f, z + TrenchHalf - 0.16f), new Vector3(60f, 0.07f, 0.08f), new Color(0.46f, 0.48f, 0.54f), "PowerRail"));
            for (int i = -11; i <= 11; i++)
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(i * 1.24f, bed + 0.07f, z + TrenchHalf - 0.16f), new Vector3(0.10f, 0.14f, 0.12f), Color.white, "Insulator"));

            for (int side = -1; side <= 1; side += 2)
            {
                float inner = side * (halfWidth - 0.05f);
                // Stone portal with an arched mouth and a little garden on top (Models/Scene). It is authored for
                // the right-hand end; the left-hand one is mirrored so its mouth also faces the station.
                GameObject portal = SceneProp("TunnelPortal", new Vector3(inner + side * 0.8f, 0f, z));
                if (side < 0) portal.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
        }

        private void BuildBackWall(float wallZ)
        {
            float face = wallZ - 0.21f;
            // Chapters with a painting wear it as the whole back wall: arches, lamps and pictures are painted in,
            // once across the visible width. The wall is made taller by the camera's foreshortening (it is seen
            // from 52 degrees above), so on screen the 4:1 picture keeps its proportions.
            int painting = Chapters.PaintingFor(ThemeIndex);
            float span = _wallSpan > 0f ? _wallSpan : 8f;
            float height = painting >= 0 ? span / 4f / Mathf.Cos(Pitch * Mathf.Deg2Rad) : 3.0f;
            // A painted wall is a thin panel on the usual front face, with no cornice: from above, the wall's top
            // and a cornice would hide the top of the picture.
            GameObject wall = painting >= 0
                ? Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, height * 0.5f, face + 0.01f), new Vector3(60f, height, 0.02f), Theme.Wall, "Wall")
                : Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, height * 0.5f, wallZ), new Vector3(60f, height, 0.4f), Theme.Wall, "Wall");
            bool paintedWall = painting >= 0 && MaterialLibrary.Paint(wall, "wall_" + painting, Color.white, new Vector4(1f / span, 1f / height, 0.5f, 0f), true);
            if (!paintedWall)
            {
                if (painting >= 0)
                {
                    // Missing picture: fall back to the full modelled wall.
                    wall.transform.localPosition = new Vector3(0f, 1.5f, wallZ);
                    wall.transform.localScale = new Vector3(60f, 3.0f, 0.4f);
                }
                Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 3.1f, wallZ - 0.1f), new Vector3(60f, 0.3f, 0.7f), Theme.Pillar, "Cornice");
                // Without the picture (missing texture) frame the modelled wall as usual.
                _aboveTop = 1.7f * Mathf.Cos(Pitch * Mathf.Deg2Rad);
                _wallSpan = 0f;
                // Tiled dado along the bottom of the wall, as in most metro stations.
                GameObject dado = Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 0.55f, face), new Vector3(60f, 1.1f, 0.04f), Theme.Dado, "Dado");
                Prims.NoShadow(dado);
                // Glazed tiles (four per texture) in the chapter's colour; three rows fill the dado exactly.
                bool tiled = MaterialLibrary.Paint(dado, "tex_wall", Theme.Dado, new Vector4(1f / 1.4667f, 1f / 1.4667f, 0f, 0f), false, true);
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 1.13f, face - 0.01f), new Vector3(60f, 0.07f, 0.05f), Color.white, "DadoCap"));
                for (int i = -14; i <= 14 && !tiled; i++)
                    Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(i * 0.7f, 0.55f, face - 0.022f), new Vector3(0.02f, 1.1f, 0.01f), Theme.Joint, "TileJoint"));
                for (int i = 0; i < ColorCodes.Count; i++)
                    Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 2.90f - i * 0.075f, face), new Vector3(60f, 0.055f, 0.02f), Palette.Lines[i], "LineStripe"));
                for (int i = -4; i <= 4; i++)
                {
                    if (i == 0) continue;
                    Prop("Pillar", new Vector3(i * 2.6f, 0f, wallZ - 0.32f));
                }

                // Station name board with a roundel.
                var navy = new Color(0.12f, 0.20f, 0.46f);
                Prims.MakeRounded(_env, new Vector3(0f, 2.12f, face - 0.05f), new Vector3(3.0f, 0.52f, 0.08f), 0.04f, navy, "NameBoard", true);
                float[] wordX = { -0.45f, 0.20f, 0.92f };
                float[] wordW = { 0.50f, 0.62f, 0.56f };
                for (int i = 0; i < wordX.Length; i++)
                    Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(wordX[i], 2.12f, face - 0.10f), new Vector3(wordW[i], 0.14f, 0.01f), Color.white, "NameText"));
                GameObject ring = Prims.Make(PrimitiveType.Cylinder, _env, new Vector3(-1.12f, 2.12f, face - 0.10f), new Vector3(0.40f, 0.012f, 0.40f), Palette.Lines[0], "RoundelRing");
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                GameObject hole = Prims.Make(PrimitiveType.Cylinder, _env, new Vector3(-1.12f, 2.12f, face - 0.115f), new Vector3(0.25f, 0.012f, 0.25f), Color.white, "RoundelCentre");
                hole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(-1.12f, 2.12f, face - 0.13f), new Vector3(0.54f, 0.11f, 0.01f), navy, "RoundelBar"));
            }

            // A painted wall has its own clock, lamps and pictures; the modelled fittings would only clutter it.
            if (paintedWall) return;

            // The painting brings its own clocks, pictures and signs; the modelled ones would cover it.
            if (paintedWall) return;

            // Departure board.
            Prims.MakeRounded(_env, new Vector3(0f, 1.50f, face - 0.05f), new Vector3(2.4f, 0.56f, 0.10f), 0.045f, new Color(0.10f, 0.12f, 0.17f), "Display", true);
            // The rows live under Props (the environment is merged into static meshes) so they can flip.
            var board = new GameObject("DepartureBoard").AddComponent<DepartureBoard>();
            board.transform.SetParent(_props, false);
            board.Rows = new Transform[3];
            board.Chips = new GameObject[3];
            for (int row = 0; row < 3; row++)
            {
                var line = new GameObject("Row" + row).transform;
                line.SetParent(board.transform, false);
                line.localPosition = new Vector3(0f, 1.65f - row * 0.15f, face - 0.11f);
                board.Rows[row] = line;
                board.Chips[row] = Prims.Make(PrimitiveType.Cube, line, new Vector3(-0.85f, 0f, 0f), new Vector3(0.22f, 0.08f, 0.01f), Palette.Lines[(row * 2) % Palette.Lines.Length], "DisplayLine");
                Prims.NoShadow(board.Chips[row]);
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, line, new Vector3(0.15f, 0f, 0f), new Vector3(1.5f, 0.045f, 0.01f), new Color(1f, 0.80f, 0.35f), "DisplayText"));
            }

            // Clock and framed posters between the pillars.
            var clockAt = new Vector3(1.93f, 2.12f, face - 0.06f);
            var hands = Prop("Clock", clockAt).AddComponent<ClockHands>();
            hands.Minute = Prop("ClockMinute", clockAt).transform;
            hands.Second = Prop("ClockSecond", clockAt).transform;
            for (int side = -1; side <= 1 && !paintedWall; side += 2)
            {
                float x = side * 3.9f;
                Color tint = Palette.Lines[side < 0 ? 1 : 5];
                Prims.MakeRounded(_env, new Vector3(x, 1.95f, face - 0.03f), new Vector3(1.25f, 1.45f, 0.06f), 0.04f, Color.white, "PosterFrame");
                GameObject art = Prims.Make(PrimitiveType.Cube, _env, new Vector3(x, 1.95f, face - 0.065f), new Vector3(1.08f, 1.28f, 0.01f), Color.Lerp(tint, Color.white, 0.55f), "PosterArt");
                Prims.NoShadow(art);
                // A painted travel poster, two different ones per chapter; the flat placeholder is the fallback.
                int poster = (ThemeIndex * 2 + (side < 0 ? 0 : 1)) % 6;
                var frame = new Vector4(1f / 1.08f, 1f / 1.28f, -(x - 0.54f) / 1.08f, -(1.95f - 0.64f) / 1.28f);
                if (MaterialLibrary.Paint(art, "poster_" + poster, Color.white, frame, true)) continue;
                GameObject sun = Prims.Make(PrimitiveType.Cylinder, _env, new Vector3(x - 0.18f, 2.18f, face - 0.075f), new Vector3(0.42f, 0.006f, 0.42f), tint, "PosterShape");
                sun.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prims.NoShadow(sun);
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(x, 1.58f, face - 0.075f), new Vector3(0.80f, 0.10f, 0.01f), Palette.Dark, "PosterText"));
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(x - 0.12f, 1.43f, face - 0.075f), new Vector3(0.56f, 0.06f, 0.01f), Palette.Dark, "PosterText2"));
            }
        }

        /// <summary>Ticket gates where the queues enter the concourse: one gate per queue lane.</summary>
        private void BuildTurnstiles(int lanes, float spacing, float z)
        {
            _turnstileZ = z;
            _flaps.Clear();
            float width = lanes * spacing;
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 0.008f, z), new Vector3(width + 0.5f, 0.016f, 1.15f), new Color(0.42f, 0.44f, 0.52f), "GateMat"));
            for (int b = 0; b <= lanes; b++)
            {
                float x = (b - lanes * 0.5f) * spacing;
                Prop("Turnstile", new Vector3(x, 0f, z));
            }
            for (int q = 0; q < lanes; q++)
            {
                float x = (q - (lanes - 1) * 0.5f) * spacing;
                float flap = (spacing - 0.24f) * 0.5f - 0.04f;
                // Under Props, not Environment: the environment is merged into static meshes, and these move.
                var lane = new GameObject("GateFlaps");
                lane.transform.SetParent(_props, false);
                var flaps = lane.AddComponent<GateFlaps>();
                for (int side = -1; side <= 1; side += 2)
                {
                    // Hinged at the post end so the flap can swing open.
                    var hinge = new GameObject(side < 0 ? "HingeL" : "HingeR").transform;
                    hinge.SetParent(lane.transform, false);
                    hinge.localPosition = new Vector3(x + side * (flap + 0.03f), 0.38f, z);
                    GameObject paddle = Prims.Make(PrimitiveType.Cube, hinge, new Vector3(-side * flap * 0.5f, 0f, 0f), new Vector3(flap, 0.34f, 0.04f), new Color(0.72f, 0.90f, 1f), "GateFlap");
                    Prims.SetGlossy(paddle, new Color(0.72f, 0.90f, 1f));
                    if (side < 0) flaps.Left = hinge; else flaps.Right = hinge;
                }
                _flaps.Add(flaps);
            }
            // Railings close off the rest of the concourse edge.
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = side * (width * 0.5f + 0.12f);
                for (int rail = 0; rail < 2; rail++)
                    Prims.Make(PrimitiveType.Cube, _env, new Vector3(x0 + side * 5f, 0.30f + rail * 0.28f, z), new Vector3(10f, 0.05f, 0.05f), Steel, "Railing");
                for (int k = 1; k <= 6; k++)
                    Prims.Make(PrimitiveType.Cube, _env, new Vector3(x0 + side * k * 1.2f, 0.31f, z), new Vector3(0.07f, 0.62f, 0.07f), Steel, "RailingPost");
            }
        }

        private void BuildPlant(Vector3 pos)
        {
            Prop("Plant", pos);
            // The foliage is its own piece so it can sway and be ruffled by passing trains.
            var sway = Prop("PlantLeaves", pos).AddComponent<Sway>();
            sway.Home = pos;
            sway.PivotHeight = 0.42f;
        }

        private void BuildLamp(Vector3 pos)
        {
            Prop("Lamp", pos);
        }

        /// <summary>Places one modelled prop (Models/Props) and colours it for the current chapter.</summary>
        private GameObject Prop(string piece, Vector3 position)
        {
            GameObject go = ModelKit.Spawn("Props", piece, _props);
            go.transform.localPosition = position;
            ModelKit.Paint(go, PropMaterial);
            return go;
        }

        /// <summary>Places one of the larger modelled scene pieces (Models/Scene): tunnel portals, the bench, stanchions.</summary>
        private GameObject SceneProp(string piece, Vector3 position)
        {
            GameObject go = ModelKit.Spawn("Scene", piece, _props);
            go.transform.localPosition = position;
            ModelKit.Paint(go, SceneMaterial);
            return go;
        }

        private Material SceneMaterial(string slot)
        {
            switch (slot)
            {
                case "Stone": return MaterialLibrary.Get(new Color(0.80f, 0.74f, 0.66f));
                case "StoneLight": return MaterialLibrary.Get(new Color(0.90f, 0.85f, 0.77f));
                case "StoneDark": return MaterialLibrary.Get(new Color(0.66f, 0.60f, 0.53f));
                case "Coping": return MaterialLibrary.Get(new Color(0.96f, 0.93f, 0.87f));
                case "Mouth": return MaterialLibrary.Get(new Color(0.05f, 0.05f, 0.09f));
                case "MouthEdge": return MaterialLibrary.Get(new Color(0.20f, 0.20f, 0.27f));
                case "Trim": return MaterialLibrary.Get(Palette.Gold, true);
                case "Accent": return MaterialLibrary.Get(Theme.Dado, true);
                case "White": return MaterialLibrary.Get(Color.white);
                case "Grass": return MaterialLibrary.Get(new Color(0.33f, 0.52f, 0.30f));
                case "Leaf": return MaterialLibrary.Get(Palette.Leaf, false, false, true);
                case "LeafLight": return MaterialLibrary.Get(Color.Lerp(Palette.Leaf, new Color(0.85f, 1f, 0.55f), 0.35f));
                case "FlowerA": return MaterialLibrary.Get(new Color(0.98f, 0.52f, 0.66f));
                case "FlowerB": return MaterialLibrary.Get(new Color(1f, 0.84f, 0.30f));
                case "FlowerC": return MaterialLibrary.Get(Color.white);
                case "Light": return MaterialLibrary.Get(new Color(0.35f, 0.92f, 0.50f));
                case "Dark": return MaterialLibrary.Get(Palette.Dark);
                case "Wood": return MaterialLibrary.Get(Palette.Wood, false, false, true);
                case "WoodDark": return MaterialLibrary.Get(Color.Lerp(Palette.Wood, Palette.Dark, 0.22f), false, false, true);
                case "Iron": return MaterialLibrary.Get(new Color(0.18f, 0.30f, 0.36f), true);
                case "Cushion": return MaterialLibrary.Get(new Color(0.86f, 0.26f, 0.30f), false, false, true);
                case "CushionLight": return MaterialLibrary.Get(new Color(0.95f, 0.42f, 0.44f), false, false, true);
                default: return null;
            }
        }

        private Material PropMaterial(string slot)
        {
            switch (slot)
            {
                case "Steel": return MaterialLibrary.Get(Steel, true);
                case "Dark": return MaterialLibrary.Get(Palette.Dark);
                case "Light": return MaterialLibrary.Get(new Color(0.35f, 0.92f, 0.50f));
                case "Accent": return MaterialLibrary.Get(Theme.Dado);
                case "Pot": return MaterialLibrary.Get(new Color(0.82f, 0.48f, 0.34f));
                case "Soil": return MaterialLibrary.Get(new Color(0.36f, 0.25f, 0.18f));
                case "Leaf": return MaterialLibrary.Get(Palette.Leaf);
                case "LeafLight": return MaterialLibrary.Get(Color.Lerp(Palette.Leaf, new Color(0.85f, 1f, 0.55f), 0.35f));
                case "Wood": return MaterialLibrary.Get(Palette.Wood);
                case "WoodDark": return MaterialLibrary.Get(Color.Lerp(Palette.Wood, Palette.Dark, 0.22f));
                case "Bulb": return MaterialLibrary.Get(new Color(1f, 0.97f, 0.82f));
                case "Pillar": return MaterialLibrary.Get(Theme.Pillar);
                case "PillarDark": return MaterialLibrary.Get(Color.Lerp(Theme.Pillar, Palette.Dark, 0.18f));
                case "Glass": return MaterialLibrary.Get(new Color(0.70f, 0.90f, 1f), true);
                case "Red": return MaterialLibrary.Get(Palette.UiRed);
                case "Outline": return ModelKit.Outline;
                default: return null;
            }
        }

        // ------------------------------------------------------------------ camera

        /// <summary>A brief camera bump, used when a train brakes at the platform.</summary>
        public void Shake(float amount)
        {
            if (!GameSettings.ReducedMotion) _shake = Mathf.Max(_shake, amount);
        }

        /// <summary>A brief zoom that eases back: positive starts wider (level intro), negative pushes in (win).</summary>
        public void Punch(float amount)
        {
            if (!GameSettings.ReducedMotion) _zoom = amount;
        }

        private void Update()
        {
            if (Cam == null) return;
            if (Mathf.Abs(Cam.aspect - _fittedAspect) > 0.001f) FitCamera();
            if (_zoom != 0f && _fitSize > 0f)
            {
                _zoom *= Mathf.Exp(-Time.deltaTime * 4.5f);
                if (Mathf.Abs(_zoom) < 0.0006f) _zoom = 0f;
                Cam.fieldOfView = _fitSize * (1f + _zoom);
            }
            if (_shake > 0f)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 0.35f);
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * _shake;
                Cam.transform.position = _cameraHome + Cam.transform.right * jitter.x + Cam.transform.up * jitter.y;
                if (_shake <= 0f) Cam.transform.position = _cameraHome;
            }
        }

        /// <summary>
        /// Fixed elevated perspective view, framed so the station sits between the top HUD and the booster bar:
        /// the front of the concourse sits on the booster bar, the top of the back wall meets the HUD, and the
        /// whole width stays on screen.
        /// </summary>
        private void FitCamera()
        {
            if (Cam == null) return;
            _fittedAspect = Cam.aspect;
            float cp = Mathf.Cos(Pitch * Mathf.Deg2Rad), sp = Mathf.Sin(Pitch * Mathf.Deg2Rad);
            float yBottom = BottomMargin, yTop = 1f - TopMargin;
            Cam.orthographic = false;
            Cam.fieldOfView = Fov * (1f + _zoom);
            _fitSize = Fov;
            Cam.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            Vector3 forward = Cam.transform.forward;
            float cx = (_minX + _maxX) * 0.5f;
            var bottomPoint = new Vector3(cx, 0f, _minZ);
            var topPoint = new Vector3(cx, _aboveTop / cp, _maxZ);
            Vector3[] corners =
            {
                new Vector3(_minX, 0f, _minZ), new Vector3(_maxX, 0f, _minZ),
                new Vector3(_minX, 0f, _maxZ), new Vector3(_maxX, 0f, _maxZ),
            };
            float keepFov = Cam.fieldOfView;
            Cam.fieldOfView = Fov;
            float distance = 26f, targetZ = (_minZ + _maxZ) * 0.5f;
            float tan = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < 40; i++)
            {
                Cam.transform.position = new Vector3(cx, 0f, targetZ) - forward * distance;
                float vb = Cam.WorldToViewportPoint(bottomPoint).y;
                float vt = Cam.WorldToViewportPoint(topPoint).y;
                float need = (vt - vb) / (yTop - yBottom);
                foreach (Vector3 c in corners)
                    need = Mathf.Max(need, Mathf.Abs(Cam.WorldToViewportPoint(c).x - 0.5f) / 0.485f);
                distance = Mathf.Max(MinDistance, distance * Mathf.Lerp(1f, need, 0.8f));
                // Pin the front of the board to the booster bar; spare height goes to the top.
                Cam.transform.position = new Vector3(cx, 0f, targetZ) - forward * distance;
                vb = Cam.WorldToViewportPoint(bottomPoint).y;
                targetZ += (vb - yBottom) * 2f * distance * tan / sp;
            }
            Cam.fieldOfView = keepFov;
            _cameraHome = new Vector3(cx, 0f, targetZ) - forward * distance;
            Cam.transform.position = _cameraHome;
        }

        // Closest the camera comes, so a tiny station is not blown up to fill the screen.
        private const float MinDistance = 15f;

        /// <summary>
        /// Widens the painted wall to what the camera sees at its depth (plus a margin), so its picture never
        /// repeats at the sides. The framing keeps the picture's original height; a widened picture grows taller
        /// too, and only its top (ceiling) slips under the HUD.
        /// </summary>
        private void FitWallToView(float face, float sinPitch)
        {
            _aboveTop = 0.2f * sinPitch + _wallSpan / 4f;
            FitCamera();
            float width = 0f;
            var plane = new Plane(Vector3.back, new Vector3(0f, 0f, face));
            foreach (float vx in new[] { 0f, 1f })
            {
                Ray ray = Cam.ViewportPointToRay(new Vector3(vx, 1f - TopMargin, 0f));
                float hit;
                if (plane.Raycast(ray, out hit)) width = Mathf.Max(width, Mathf.Abs(ray.GetPoint(hit).x - (_minX + _maxX) * 0.5f) * 2f);
            }
            _wallSpan = Mathf.Max(_wallSpan, width * 1.04f);
        }
    }
}
