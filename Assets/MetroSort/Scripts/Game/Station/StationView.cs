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
        private const float Pitch = 52f;

        public Camera Cam { get; private set; }
        public readonly List<PlatformView> Platforms = new List<PlatformView>();

        private ObjectPoolManager _pool;
        private readonly Dictionary<int, PassengerView> _passengers = new Dictionary<int, PassengerView>();
        private Transform _env, _actors;
        private string _signature;
        private Vector3[] _queueFront = new Vector3[0];
        private float _queueStep = 0.9f;
        private Vector3 _benchCenter;
        private int _benchCapacity;
        private GameObject _benchGlow;
        private float _minX, _maxX, _minZ, _maxZ;
        private float _fittedAspect, _shake;
        private Vector3 _cameraHome;

        public void Init(Camera cam, ObjectPoolManager pool)
        {
            Cam = cam;
            _pool = pool;
            _actors = new GameObject("Actors").transform;
            _actors.SetParent(transform, false);
        }

        // ------------------------------------------------------------------ sync

        /// <summary>Recreates every view from the logical state. Used on level start, undo, restart and boosters.</summary>
        public void Rebuild(BoardState state, bool intro = false)
        {
            // The layout depends on the screen shape, so a different aspect ratio rebuilds the station too.
            string signature = Signature(state) + "/" + Mathf.RoundToInt((Cam != null ? Cam.aspect : 0.5625f) * 25f);
            if (signature != _signature)
            {
                BuildEnvironment(state);
                _signature = signature;
            }
            foreach (KeyValuePair<int, PassengerView> kv in _passengers) _pool.Release(PassengerView.PoolKey, kv.Value.gameObject);
            _passengers.Clear();

            for (int q = 0; q < state.Queues.Length; q++)
            {
                for (int i = 0; i < state.Queues[q].Count; i++)
                {
                    PassengerView view = Spawn(state.Queues[q][i], QueueSlot(q, i), i == 0);
                    // Level intro: passengers pop in lane by lane, front to back.
                    if (intro) view.PopIn(0.10f + q * 0.07f + i * 0.045f);
                }
            }
            for (int b = 0; b < state.Bench.Count; b++) Spawn(state.Bench[b], BenchSlot(b), true);
            for (int p = 0; p < state.Platforms.Count && p < Platforms.Count; p++)
            {
                PlatformState ps = state.Platforms[p];
                for (int i = 0; i < ps.Occupants.Count; i++) Spawn(ps.Occupants[i], Platforms[p].SlotPosition(i), true);
            }
            RefreshPlatforms(state);
            ClearHighlights();
        }

        public void RefreshPlatforms(BoardState state)
        {
            for (int p = 0; p < state.Platforms.Count && p < Platforms.Count; p++) Platforms[p].Refresh(state.Platforms[p]);
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
                    v.SetAccessible(i == 0);
                    MoveView(v, QueueSlot(q, i), animate, i * 0.04f);
                }
            }
            for (int b = 0; b < state.Bench.Count; b++)
            {
                PassengerView v;
                if (!_passengers.TryGetValue(state.Bench[b].Id, out v)) continue;
                v.SetAccessible(true);
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
                    Spawn(p, QueueSlot(q, i), i == 0).Bounce();
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
            return _benchCenter + new Vector3((index - (_benchCapacity - 1) * 0.5f) * 1.0f, 0.14f, 0f);
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
            Vector2 a = Cam.WorldToScreenPoint(center + new Vector3(-halfX, 0f, -halfZ));
            Vector2 b = Cam.WorldToScreenPoint(center + new Vector3(halfX, height, halfZ));
            return screen.x >= a.x && screen.x <= b.x && screen.y >= a.y && screen.y <= b.y;
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
            Platforms.Clear();
            _env = new GameObject("Environment").transform;
            _env.SetParent(transform, false);

            int maxQueues, maxDepth;
            Measure(state, out maxQueues, out maxDepth);
            _benchCapacity = state.BenchCapacity;

            // Platforms: one row for up to two, two rows beyond that. A narrower station lets the portrait
            // camera zoom in, so passengers and touch targets stay large on a phone.
            int n = state.Platforms.Count;
            int backCount = n <= 2 ? n : (n + 1) / 2;
            float frontRowZ = 4.6f, backRowZ = n <= 2 ? 5.2f : 8.2f;
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
            float visibleDepth = (2f * viewSize * (1f - 0.15f - 0.19f) - 1.7f * cosPitch) / sinPitch;
            float fixedDepth = (lastTrackZ + 0.9f - frontZ) + 1.65f;
            float compactStep = Mathf.Min(1.0f, 5.0f / Mathf.Max(1, maxDepth - 1));
            float roomyStep = (visibleDepth - fixedDepth) / Mathf.Max(1, maxDepth - 1);
            _queueStep = Mathf.Clamp(roomyStep, compactStep, 1.45f);
            PassengerView.WaitingScale = Mathf.Lerp(0.74f, 0.90f, Mathf.InverseLerp(1.0f, 1.4f, _queueStep));
            _queueFront = new Vector3[maxQueues];
            for (int q = 0; q < maxQueues; q++)
            {
                float x = (q - (maxQueues - 1) * 0.5f) * spacing;
                _queueFront[q] = new Vector3(x, 0f, frontZ);
                float laneLength = (maxDepth - 1) * _queueStep + 1.0f;
                GameObject lane = Prims.Make(PrimitiveType.Cube, _env, new Vector3(x, 0.011f, frontZ - laneLength * 0.5f + 0.5f), new Vector3(0.95f, 0.02f, laneLength), Palette.Lane, "Lane");
                Prims.NoShadow(lane);
                GameObject spot = Prims.Make(PrimitiveType.Cylinder, _env, new Vector3(x, 0.016f, frontZ), new Vector3(0.90f, 0.012f, 0.90f), new Color(0.97f, 0.98f, 1f), "FrontSpot");
                Prims.NoShadow(spot);
            }
            float queuesWidth = (maxQueues - 1) * spacing + 1.2f;

            // Waiting bench (gameplay element, only in levels that enable it).
            _benchGlow = null;
            if (_benchCapacity > 0)
            {
                _benchCenter = new Vector3(0f, 0f, 2.35f);
                float w = _benchCapacity * 1.0f + 0.5f;
                _benchGlow = Prims.Make(PrimitiveType.Cube, _env, _benchCenter + new Vector3(0f, 0.02f, 0f), new Vector3(w + 0.3f, 0.04f, 1.3f), Palette.Highlight, "BenchGlow");
                Prims.NoShadow(_benchGlow);
                _benchGlow.SetActive(false);
                Prims.MakeRounded(_env, _benchCenter + new Vector3(0f, 0.07f, 0f), new Vector3(w, 0.14f, 1.0f), 0.06f, Palette.Wood, "Bench");
                Prims.MakeRounded(_env, _benchCenter + new Vector3(0f, 0.45f, 0.48f), new Vector3(w, 0.5f, 0.12f), 0.05f, Palette.Wood, "BenchBack");
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
                    if (clear) Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(ix * 1.6f, 0.003f, cz), new Vector3(1.6f, 0.006f, 1.6f), Palette.FloorTile, "Tile"));
                }
            }

            BuildBackWall(wallZ);
            BuildTurnstiles(maxQueues, spacing, laneEndZ - 0.40f);

            // Decoration kept to the sides so it never hides gameplay.
            float sideX = halfWidth + 0.9f;
            for (int side = -1; side <= 1; side += 2)
            {
                BuildPlant(new Vector3(side * sideX, 0f, 2.6f));
                BuildLamp(new Vector3(side * (sideX + 0.2f), 0f, 0.6f));
                if (_benchCapacity == 0)
                    Prims.MakeRounded(_env, new Vector3(side * (sideX + 0.1f), 0.22f, -1.0f), new Vector3(0.6f, 0.44f, 1.6f), 0.12f, Palette.Wood, "DecorBench");
            }

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
                Platforms.Add(PlatformView.Create(_env, first + i, ps, new Vector3(x + w * 0.5f, 0f, z)));
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
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, -0.3f, (z0 + z1) * 0.5f), new Vector3(60f, 0.6f, z1 - z0), Palette.Floor, "Floor"));
        }

        /// <summary>A sunken track with rails, sleepers, safety lines along both edges and a tunnel mouth at each end.</summary>
        private void BuildTrack(float z, float halfWidth)
        {
            float bed = -TrenchDepth;
            // Pale ballast, dark wooden sleepers and bright rails: strong contrast so the track reads at a glance.
            Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, bed - 0.1f, z), new Vector3(60f, 0.2f, TrenchHalf * 2f), Ballast, "Ballast");
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, bed + 0.004f, z), new Vector3(60f, 0.008f, 1.02f), BallastDark, "BallastCentre"));
            for (int i = -22; i <= 22; i++)
            {
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(i * 0.62f, bed + 0.035f, z), new Vector3(0.30f, 0.07f, 1.36f), SleeperWood, "Sleeper"));
                // Loose stones break up the flat ballast between sleepers.
                float jitter = ((i * 37) % 11) * 0.05f - 0.25f;
                Prims.NoShadow(Prims.Make(PrimitiveType.Sphere, _env, new Vector3(i * 0.62f + 0.31f, bed + 0.01f, z + jitter), new Vector3(0.13f, 0.06f, 0.11f), BallastDark, "Stone"));
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
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 0.010f, z + i * (TrenchHalf + 0.27f)), new Vector3(60f, 0.016f, 0.14f), new Color(0.93f, 0.93f, 0.90f), "TactileStrip"));
            }
            // Orange power rail on its insulators along the far side of the trench.
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, bed + 0.17f, z + TrenchHalf - 0.16f), new Vector3(60f, 0.07f, 0.08f), new Color(0.98f, 0.55f, 0.16f), "PowerRail"));
            for (int i = -11; i <= 11; i++)
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(i * 1.24f, bed + 0.07f, z + TrenchHalf - 0.16f), new Vector3(0.10f, 0.14f, 0.12f), Color.white, "Insulator"));

            for (int side = -1; side <= 1; side += 2)
            {
                float inner = side * (halfWidth - 0.05f);
                Prims.MakeRounded(_env, new Vector3(inner + side * 0.8f, 0.55f, z), new Vector3(1.6f, 1.9f, 2.5f), 0.12f, Concrete, "TunnelPortal");
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(inner - side * 0.01f, 0.25f, z), new Vector3(0.04f, 1.30f, 1.70f), new Color(0.04f, 0.04f, 0.07f), "TunnelMouth"));
                // Hazard edging on top of the portal, visible from the high camera.
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(inner + side * 0.09f, 1.505f, z), new Vector3(0.14f, 0.02f, 2.3f), Palette.Gold, "PortalEdge"));
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(inner + side * 0.8f, 1.505f, z), new Vector3(0.9f, 0.02f, 1.5f), new Color(0.40f, 0.42f, 0.50f), "PortalRoofPanel"));
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(inner - side * 0.02f, 0.97f, z), new Vector3(0.05f, 0.14f, 1.70f), Palette.Gold, "PortalLintel"));
                Prims.NoShadow(Prims.Make(PrimitiveType.Sphere, _env, new Vector3(inner - side * 0.03f, 1.22f, z - 0.95f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.35f, 0.92f, 0.50f), "Signal"));
            }
        }

        private void BuildBackWall(float wallZ)
        {
            float face = wallZ - 0.21f;
            Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 1.5f, wallZ), new Vector3(60f, 3.0f, 0.4f), Palette.Wall, "Wall");
            // Tiled dado along the bottom of the wall, as in most metro stations.
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 0.55f, face), new Vector3(60f, 1.1f, 0.04f), new Color(0.28f, 0.55f, 0.62f), "Dado"));
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 1.13f, face - 0.01f), new Vector3(60f, 0.07f, 0.05f), Color.white, "DadoCap"));
            for (int i = -14; i <= 14; i++)
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(i * 0.7f, 0.55f, face - 0.022f), new Vector3(0.02f, 1.1f, 0.01f), new Color(0.22f, 0.46f, 0.53f), "TileJoint"));
            Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 3.1f, wallZ - 0.1f), new Vector3(60f, 0.3f, 0.7f), Palette.Pillar, "Cornice");
            for (int i = 0; i < ColorCodes.Count; i++)
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 2.90f - i * 0.075f, face), new Vector3(60f, 0.055f, 0.02f), Palette.Lines[i], "LineStripe"));
            for (int i = -4; i <= 4; i++)
            {
                if (i == 0) continue;
                Prims.MakeRounded(_env, new Vector3(i * 2.6f, 1.5f, wallZ - 0.32f), new Vector3(0.5f, 3.0f, 0.36f), 0.12f, Palette.Pillar, "Pillar");
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

            // Departure board.
            Prims.MakeRounded(_env, new Vector3(0f, 1.50f, face - 0.05f), new Vector3(2.4f, 0.56f, 0.10f), 0.045f, new Color(0.10f, 0.12f, 0.17f), "Display", true);
            for (int row = 0; row < 3; row++)
            {
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(-0.85f, 1.65f - row * 0.15f, face - 0.11f), new Vector3(0.22f, 0.08f, 0.01f), Palette.Lines[(row * 2) % Palette.Lines.Length], "DisplayLine"));
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0.15f, 1.65f - row * 0.15f, face - 0.11f), new Vector3(1.5f, 0.045f, 0.01f), new Color(1f, 0.80f, 0.35f), "DisplayText"));
            }

            // Clock and framed posters between the pillars.
            GameObject clock = Prims.Make(PrimitiveType.Cylinder, _env, new Vector3(1.93f, 2.12f, face - 0.06f), new Vector3(0.56f, 0.03f, 0.56f), Color.white, "Clock");
            clock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(1.93f, 2.19f, face - 0.10f), new Vector3(0.035f, 0.18f, 0.01f), Palette.Dark, "ClockHandM"));
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(1.99f, 2.12f, face - 0.10f), new Vector3(0.14f, 0.035f, 0.01f), Palette.Dark, "ClockHandH"));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 3.9f;
                Color tint = Palette.Lines[side < 0 ? 1 : 5];
                Prims.MakeRounded(_env, new Vector3(x, 1.95f, face - 0.03f), new Vector3(1.25f, 1.45f, 0.06f), 0.04f, Color.white, "PosterFrame");
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(x, 1.95f, face - 0.065f), new Vector3(1.08f, 1.28f, 0.01f), Color.Lerp(tint, Color.white, 0.55f), "PosterArt"));
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
            float width = lanes * spacing;
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(0f, 0.008f, z), new Vector3(width + 0.5f, 0.016f, 1.15f), new Color(0.42f, 0.44f, 0.52f), "GateMat"));
            for (int b = 0; b <= lanes; b++)
            {
                float x = (b - lanes * 0.5f) * spacing;
                GameObject post = Prims.MakeRounded(_env, new Vector3(x, 0.33f, z), new Vector3(0.24f, 0.66f, 0.86f), 0.08f, Steel, "GatePost", true);
                InkOutline.Add(post, 0.02f);
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, _env, new Vector3(x, 0.665f, z + 0.12f), new Vector3(0.16f, 0.02f, 0.34f), Palette.Dark, "CardReader"));
                Prims.NoShadow(Prims.Make(PrimitiveType.Sphere, _env, new Vector3(x, 0.70f, z - 0.28f), new Vector3(0.11f, 0.08f, 0.11f), new Color(0.35f, 0.92f, 0.50f), "GateLight"));
            }
            for (int q = 0; q < lanes; q++)
            {
                float x = (q - (lanes - 1) * 0.5f) * spacing;
                float flap = (spacing - 0.24f) * 0.5f - 0.04f;
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject paddle = Prims.Make(PrimitiveType.Cube, _env, new Vector3(x + side * (flap * 0.5f + 0.03f), 0.38f, z), new Vector3(flap, 0.34f, 0.04f), new Color(0.72f, 0.90f, 1f), "GateFlap");
                    Prims.SetGlossy(paddle, new Color(0.72f, 0.90f, 1f));
                }
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
            Prims.Make(PrimitiveType.Cylinder, _env, pos + new Vector3(0f, 0.22f, 0f), new Vector3(0.55f, 0.22f, 0.55f), Palette.Wood, "Pot");
            Prims.Make(PrimitiveType.Sphere, _env, pos + new Vector3(0f, 0.78f, 0f), new Vector3(0.85f, 0.85f, 0.85f), Palette.Leaf, "Leaves");
            Prims.Make(PrimitiveType.Sphere, _env, pos + new Vector3(0.18f, 1.18f, 0.05f), new Vector3(0.50f, 0.50f, 0.50f), Palette.Leaf, "Leaves2");
        }

        private void BuildLamp(Vector3 pos)
        {
            Prims.Make(PrimitiveType.Cylinder, _env, pos + new Vector3(0f, 1.0f, 0f), new Vector3(0.08f, 1.0f, 0.08f), Palette.Dark, "LampPole");
            Prims.NoShadow(Prims.Make(PrimitiveType.Sphere, _env, pos + new Vector3(0f, 2.1f, 0f), new Vector3(0.34f, 0.34f, 0.34f), new Color(1f, 0.97f, 0.82f), "LampBulb"));
        }

        // ------------------------------------------------------------------ camera

        /// <summary>A brief camera bump, used when a train brakes at the platform.</summary>
        public void Shake(float amount)
        {
            if (!GameSettings.ReducedMotion) _shake = Mathf.Max(_shake, amount);
        }

        private void Update()
        {
            if (Cam == null) return;
            if (Mathf.Abs(Cam.aspect - _fittedAspect) > 0.001f) FitCamera();
            if (_shake > 0f)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 0.35f);
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * _shake;
                Cam.transform.position = _cameraHome + Cam.transform.right * jitter.x + Cam.transform.up * jitter.y;
                if (_shake <= 0f) Cam.transform.position = _cameraHome;
            }
        }

        /// <summary>Fixed elevated orthographic view, framed so the station sits between the top HUD and the booster bar.</summary>
        private void FitCamera()
        {
            if (Cam == null) return;
            // Cam.aspect follows the real output (screen, or a render target), so framing is right everywhere.
            float aspect = Cam.aspect > 0.01f ? Cam.aspect : 0.5625f;
            _fittedAspect = Cam.aspect;
            float sp = Mathf.Sin(Pitch * Mathf.Deg2Rad), cp = Mathf.Cos(Pitch * Mathf.Deg2Rad);
            const float top = 0.15f, bottom = 0.19f;
            float vMin = _minZ * sp;
            float vMax = _maxZ * sp + 1.7f * cp;
            float sizeV = (vMax - vMin) / (2f * (1f - top - bottom));
            float sizeH = (_maxX - _minX) / (2f * aspect * 0.97f);
            float size = Mathf.Max(6f, Mathf.Max(sizeV, sizeH));
            float center = (vMin + vMax) * 0.5f - (bottom - top) * size;
            Cam.orthographic = true;
            Cam.orthographicSize = size;
            Cam.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            var look = new Vector3((_minX + _maxX) * 0.5f, 0f, center / sp);
            _cameraHome = look - Cam.transform.forward * 24f;
            Cam.transform.position = _cameraHome;
        }
    }
}
