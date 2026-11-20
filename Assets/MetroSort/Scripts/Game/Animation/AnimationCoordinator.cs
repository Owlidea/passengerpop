using System;
using System.Collections;
using System.Collections.Generic;
using MetroSort.Puzzle;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Plays BoardEvents as animation. The logical board has already changed by the time anything here runs,
    /// so animations can overlap, be skipped or be cancelled without ever affecting the rules.
    /// </summary>
    public sealed class AnimationCoordinator : MonoBehaviour
    {
        private StationView _station;
        private AudioManager _audio;
        private ObjectPoolManager _pool;
        private int _active;
        private int _epoch;
        private BoardEvent _pendingEnd;
        private readonly List<TrainView> _trains = new List<TrainView>();
        private readonly List<PassengerView> _boarding = new List<PassengerView>();
        private readonly Dictionary<int, int> _busyPlatforms = new Dictionary<int, int>();
        private readonly HashSet<int> _occupiedTracks = new HashSet<int>();
        private int _trainsWaiting;
        /// <summary>Timetable levels: the train standing at each platform, doors open, waiting for its line.</summary>
        /// Arrivals and departures at a platform are numbered, so the n-th departure always takes the n-th train
        /// even when one move sends several trains through the same platform (bench passengers board at once).
        private readonly Dictionary<int, TrainView> _docked = new Dictionary<int, TrainView>();
        private readonly Dictionary<int, int> _arrivalsIssued = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _arrivalsDocked = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _departures = new Dictionary<int, int>();
        private bool _scheduled;

        /// <summary>Raised once all animations for a winning or losing move have finished.</summary>
        public event Action<BoardEvent> Finished;
        public event Action TrainDeparted;

        public bool IsBusy { get { return _active > 0; } }

        /// <summary>True while a completed group is still waiting for, or boarding, its train at this platform.</summary>
        public bool IsPlatformBusy(int platform)
        {
            int count;
            return _busyPlatforms.TryGetValue(platform, out count) && count > 0;
        }

        public void Init(StationView station, AudioManager audio, ObjectPoolManager pool)
        {
            _station = station;
            _audio = audio;
            _pool = pool;
        }

        private static float WalkTime { get { return GameSettings.ReducedMotion ? 0.16f : 0.24f; } }
        private static float TrainScale { get { return GameSettings.FastTrains ? 0.4f : 1f; } }

        /// <summary>Stops everything in flight and returns pooled objects. Call before rebuilding the station.</summary>
        public void CancelAll()
        {
            StopAllCoroutines();
            _epoch++;
            _active = 0;
            _pendingEnd = null;
            foreach (TrainView t in _trains) _station.ReleaseTrain(t);
            _trains.Clear();
            _docked.Clear();
            _arrivalsIssued.Clear();
            _arrivalsDocked.Clear();
            _departures.Clear();
            foreach (PassengerView v in _boarding)
            {
                v.EndRide();
                _station.ReleasePassenger(v);
            }
            _boarding.Clear();
            _busyPlatforms.Clear();
            _occupiedTracks.Clear();
            _trainsWaiting = 0;
        }

        public void Play(List<BoardEvent> events, BoardState stateAfter)
        {
            int epoch = _epoch;
            for (int i = 0; i < events.Count; i++)
            {
                BoardEvent e = events[i];
                switch (e.Type)
                {
                    case BoardEventType.PassengerMoved:
                    {
                        PassengerView v = _station.GetPassenger(e.PassengerId);
                        if (v == null) break;
                        Vector3 target = e.Move.IsToBench ? _station.BenchSlot(e.Slot) : _station.Platforms[e.Platform].SlotPosition(e.Slot);
                        v.Selected = false;
                        v.Held = false;
                        v.SetAccessible(true);
                        v.SetSeated(e.Move.IsToBench);
                        _active++;
                        v.WalkTo(target, WalkTime, 0.35f, () =>
                        {
                            if (epoch != _epoch) return;
                            _audio.Play(Sfx.Step);
                            Haptics.Tap();
                            if (!GameSettings.ReducedMotion) Puff.Burst(_pool, target + new Vector3(0f, 0.05f, 0f), 2, 0.12f);
                            _active--;
                            CheckIdle();
                        });
                        break;
                    }
                    case BoardEventType.PlatformColorAssigned:
                        if (_scheduled && e.Platform < _station.Platforms.Count) StartCoroutine(Arrive(e.Platform, e.Color, epoch));
                        else _audio.Play(Sfx.Assign);
                        break;
                    case BoardEventType.TrainDeparted:
                        StartCoroutine(TrainSequence(e, epoch));
                        break;
                    case BoardEventType.PlatformUnlocked:
                        _audio.Play(Sfx.Booster);
                        if (e.Platform < _station.Platforms.Count) _station.Platforms[e.Platform].Pulse();
                        break;
                    case BoardEventType.QueueGateOpened:
                        _audio.Play(Sfx.Booster);
                        if (!GameSettings.ReducedMotion) Puff.Burst(_pool, _station.GateAnchor(e.Queue) + new Vector3(0f, -0.6f, 0f), 6, 0.35f);
                        break;
                    case BoardEventType.StageAdvanced:
                        _station.SpawnMissing(stateAfter);
                        _audio.Play(Sfx.Booster);
                        break;
                    case BoardEventType.Won:
                    case BoardEventType.Lost:
                        _pendingEnd = e;
                        break;
                }
            }
            _station.SyncQueues(stateAfter, true);
            _station.RefreshPlatforms(stateAfter);
            CheckIdle();
        }

        // ------------------------------------------------------------------ timetable trains

        /// <summary>
        /// Puts the right train at every platform of a timetable level (after a rebuild). animate: they pull in;
        /// otherwise they are simply there. Classic levels have no waiting trains.
        /// </summary>
        public void DockTrains(BoardState state, bool animate)
        {
            _scheduled = state != null && state.HasSchedule;
            if (!_scheduled) return;
            for (int p = 0; p < state.Platforms.Count && p < _station.Platforms.Count; p++)
            {
                PlatformState ps = state.Platforms[p];
                if (ps.Closed || ps.IsLocked || ps.ReservedColor < 0) continue;
                if (animate) StartCoroutine(Arrive(p, ps.ReservedColor, _epoch));
                else
                {
                    TrainView train = NewTrain(p, ps.ReservedColor);
                    train.transform.position = _station.Platforms[p].TrainStop;
                    train.SetDoors(1f);
                    train.Humming = true;
                    int ticket = Next(_arrivalsIssued, p);
                    _docked[DockKey(p, ticket)] = train;
                    _arrivalsDocked[p] = ticket + 1;
                    _occupiedTracks.Add(LaneKey(p));
                }
            }
        }

        private static int DockKey(int platform, int ticket) { return platform * 100000 + ticket; }

        /// <summary>Returns the counter's value for this platform and advances it.</summary>
        private static int Next(Dictionary<int, int> counters, int platform)
        {
            int n;
            counters.TryGetValue(platform, out n);
            counters[platform] = n + 1;
            return n;
        }

        private static int Get(Dictionary<int, int> counters, int platform)
        {
            int n;
            counters.TryGetValue(platform, out n);
            return n;
        }

        /// <summary>On timetable levels every platform has its own stretch of track.</summary>
        private static int LaneKey(int platform) { return 1000 + platform; }

        private int TrackKey(int platform)
        {
            return _scheduled ? LaneKey(platform) : Mathf.RoundToInt(_station.Platforms[platform].TrainStop.z * 10f);
        }

        /// <summary>
        /// Which end of the station a platform's trains use: +1 right, -1 left. A platform sharing its track with
        /// another one uses its own outer end, so a docked train is never driven through.
        /// </summary>
        private int SideOf(int platform)
        {
            PlatformView me = _station.Platforms[platform];
            for (int i = 0; i < _station.Platforms.Count; i++)
            {
                if (i == platform) continue;
                PlatformView other = _station.Platforms[i];
                if (Mathf.Abs(other.TrainStop.z - me.TrainStop.z) < 0.05f)
                    return me.transform.position.x >= other.transform.position.x ? 1 : -1;
            }
            return 1;
        }

        private bool SharesTrack(int platform)
        {
            PlatformView me = _station.Platforms[platform];
            for (int i = 0; i < _station.Platforms.Count; i++)
                if (i != platform && Mathf.Abs(_station.Platforms[i].TrainStop.z - me.TrainStop.z) < 0.05f) return true;
            return false;
        }

        private TrainView NewTrain(int platform, int color)
        {
            TrainView train = _station.AcquireTrain();
            _trains.Add(train);
            train.Configure(color, _station.Platforms[platform].Capacity);
            train.transform.rotation = Quaternion.identity;
            return train;
        }

        /// <summary>The next train of the timetable pulls in once the platform's previous train has gone.</summary>
        private IEnumerator Arrive(int platform, int color, int epoch)
        {
            int ticket = Next(_arrivalsIssued, platform);
            int key = LaneKey(platform);
            // In timetable order: the previous train must have pulled in and left before this one comes.
            while ((_occupiedTracks.Contains(key) || Get(_arrivalsDocked, platform) != ticket) && epoch == _epoch) yield return null;
            if (epoch != _epoch) yield break;
            _occupiedTracks.Add(key);
            PlatformView view = _station.Platforms[platform];
            TrainView train = NewTrain(platform, color);
            Vector3 stop = view.TrainStop;
            Vector3 from = stop + Vector3.right * (18f * SideOf(platform));
            train.transform.position = from;
            float scale = TrainScale;
            _audio.Play(Sfx.TrainArrive);
            DepartureBoard.Flip();
            for (float t = 0f; t < 1f; t += Time.deltaTime / (0.55f * scale))
            {
                if (epoch != _epoch) yield break;
                train.transform.position = Vector3.LerpUnclamped(from, stop, Ease.OutCubic(t));
                yield return null;
            }
            train.transform.position = stop;
            if (!GameSettings.ReducedMotion)
            {
                Puff.Burst(_pool, stop + new Vector3(0f, 0.15f, -0.45f), 5, 1.8f);
                train.Brake();
                for (float t = 0f; t < 1f; t += Time.deltaTime / (0.15f * scale))
                {
                    if (epoch != _epoch) yield break;
                    float rock = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
                    train.transform.position = stop + new Vector3(-rock * 0.05f * SideOf(platform), 0f, 0f);
                    yield return null;
                }
                train.transform.position = stop;
            }
            train.Humming = true;
            _audio.Play(Sfx.Doors);
            for (float t = 0f; t < 1f; t += Time.deltaTime / (0.12f * scale))
            {
                if (epoch != _epoch) yield break;
                train.SetDoors(Ease.OutCubic(t));
                yield return null;
            }
            train.SetDoors(1f);
            view.Pulse();
            _docked[DockKey(platform, ticket)] = train;
            _arrivalsDocked[platform] = ticket + 1;
        }

        /// <summary>Level complete: confetti rains across the whole station for a moment.</summary>
        public void WinShower()
        {
            Haptics.Success();
            _station.Punch(-0.035f);
            if (!GameSettings.ReducedMotion) StartCoroutine(Shower(_epoch));
        }

        private IEnumerator Shower(int epoch)
        {
            for (int i = 0; i < 9 && epoch == _epoch; i++)
            {
                Vector3 at = new Vector3(UnityEngine.Random.Range(-4.5f, 4.5f), 5.5f, UnityEngine.Random.Range(-1f, 6f));
                Fx.Confetti(at, Palette.Lines[i % Palette.Lines.Length], 26, 3.2f, 1.5f);
                if (i % 3 == 0) Fx.Sparkle(at + Vector3.down * 2.5f, Palette.Gold, 10, 4f);
                yield return new WaitForSeconds(0.16f);
            }
        }

        private void CheckIdle()
        {
            if (_active > 0 || _pendingEnd == null) return;
            BoardEvent end = _pendingEnd;
            _pendingEnd = null;
            if (Finished != null) Finished(end);
        }

        private IEnumerator TrainSequence(BoardEvent e, int epoch)
        {
            _active++;
            int busy;
            _busyPlatforms.TryGetValue(e.Platform, out busy);
            _busyPlatforms[e.Platform] = busy + 1;
            PlatformView platform = _station.Platforms[e.Platform];
            // Timetable levels: the train is already standing here (or still pulling in) with its doors open.
            TrainView docked = null;
            int dockKey = 0;
            bool useDocked = false;
            if (_scheduled)
            {
                int ticket = Next(_departures, e.Platform);
                useDocked = ticket < Get(_arrivalsIssued, e.Platform);
                dockKey = DockKey(e.Platform, ticket);
            }
            var riders = new List<PassengerView>(e.PassengerIds.Length);
            foreach (int id in e.PassengerIds)
            {
                PassengerView v = _station.Detach(id);
                if (v == null) continue;
                riders.Add(v);
                _boarding.Add(v);
            }

            // Let the last passenger reach the platform before the celebration starts.
            float waited = 0f;
            while (waited < 1.5f && AnyMoving(riders)) { waited += Time.deltaTime; yield return null; }

            _audio.Play(Sfx.Match);
            _audio.Play(Sfx.Cheer);
            platform.Pulse();
            foreach (PassengerView v in riders) v.Celebrate();
            Color line = Palette.Line(e.Color);
            Vector3 centre = platform.transform.position;
            Fx.Confetti(centre + Vector3.up * 1.2f, line, 44);
            Fx.Ring(centre + Vector3.up * 0.32f, line, platform.Width + 2.4f);
            Fx.Sparkle(centre + Vector3.up * 1.0f, line, 14);
            Haptics.Thump();
            if (!GameSettings.ReducedMotion) _station.Shake(0.04f);

            // One train per track at a time: later trains wait for the line to clear instead of driving through.
            int track = TrackKey(e.Platform);
            if (useDocked)
            {
                while (docked == null && epoch == _epoch)
                {
                    if (_docked.TryGetValue(dockKey, out docked)) _docked.Remove(dockKey);
                    else yield return null;
                }
            }
            TrainView train = docked;
            Vector3 stop = platform.TrainStop;
            Vector3 to = stop + Vector3.right * (18f * (SharesTrack(e.Platform) ? SideOf(e.Platform) : -1f));
            float scale = TrainScale * (_trainsWaiting > 0 ? 0.65f : 1f);
            bool lively = !GameSettings.ReducedMotion;
            if (train == null)
            {
            if (_occupiedTracks.Contains(track))
            {
                _trainsWaiting++;
                while (_occupiedTracks.Contains(track)) yield return null;
                _trainsWaiting--;
            }
            _occupiedTracks.Add(track);

            train = _station.AcquireTrain();
            _trains.Add(train);
            train.Configure(e.Color, platform.Capacity);
            Vector3 from = stop + Vector3.right * 18f;
            train.transform.position = from;
            _audio.Play(Sfx.TrainArrive);
            Sway.Gust(-1f);
            DepartureBoard.Flip();
            for (float t = 0f; t < 1f; t += Time.deltaTime / (0.40f * scale))
            {
                train.transform.position = Vector3.LerpUnclamped(from, stop, Ease.OutCubic(t));
                yield return null;
            }
            train.transform.position = stop;

            // Brake: the train rocks forward and settles, with a puff of dust and a small camera bump.
            if (lively)
            {
                Puff.Burst(_pool, stop + new Vector3(0f, 0.15f, -0.45f), 7, 2.2f);
                _station.Shake(0.07f);
                train.Brake();
                Fx.Sparkle(train.PantographTop, new Color(1f, 0.93f, 0.55f), 7, 2.2f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / (0.15f * scale))
                {
                    float rock = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
                    train.transform.position = stop + new Vector3(-rock * 0.06f, 0f, 0f);
                    train.transform.rotation = Quaternion.Euler(0f, 0f, rock * 1.4f);
                    yield return null;
                }
                train.transform.position = stop;
                train.transform.rotation = Quaternion.identity;
            }

            train.Humming = true;
            _audio.Play(Sfx.Doors);
            for (float t = 0f; t < 1f; t += Time.deltaTime / (0.12f * scale)) { train.SetDoors(Ease.OutCubic(t)); yield return null; }
            train.SetDoors(1f);
            }

            // Boarding: one after another they run to their door and on inside, then pop up at a window and
            // wave. They stay with the train until it has left the station.
            int aboard = 0;
            float run = 0.16f * scale;
            for (int i = 0; i < riders.Count; i++)
            {
                PassengerView v = riders[i];
                int slot = i;
                Vector3 door = train.DoorPosition(slot);
                float ground = v.transform.position.y;
                var threshold = new Vector3(door.x, ground, door.z - 0.30f);
                var inside = new Vector3(door.x, ground, door.z + 0.50f);
                v.RunTo(threshold, run, i * 0.07f * scale, () =>
                {
                    if (epoch != _epoch) return;
                    _audio.Play(Sfx.Step);
                    v.RunTo(inside, run * 0.7f, 0f, () =>
                    {
                        if (epoch != _epoch) return;
                        v.RideAtWindow(train.CarRoot(slot), TrainView.WindowSeat(slot), TrainView.RiderScale);
                        aboard++;
                    });
                });
            }
            float boardingLimit = run * 2f + riders.Count * 0.07f * scale + 0.6f;
            for (float waitedAboard = 0f; aboard < riders.Count && waitedAboard < boardingLimit; waitedAboard += Time.deltaTime) yield return null;
            // Anyone who did not make it (reduced motion, a stalled walk) is put straight at their window.
            for (int i = 0; i < riders.Count; i++)
                if (riders[i].transform.parent != train.CarRoot(i)) riders[i].RideAtWindow(train.CarRoot(i), TrainView.WindowSeat(i), TrainView.RiderScale);

            for (float t = 0f; t < 1f; t += Time.deltaTime / (0.09f * scale)) { train.SetDoors(1f - Ease.Smooth(t)); yield return null; }
            train.SetDoors(0f);

            _audio.Play(Sfx.TrainDepart);
            if (TrainDeparted != null) TrainDeparted();
            // Depart: a short backwards wind-up, then accelerate away trailing puffs.
            if (lively)
            {
                train.Humming = false;
                train.Launch();
                Sway.Gust(-0.8f);
                Fx.Sparkle(train.PantographTop, new Color(1f, 0.93f, 0.55f), 4, 1.8f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / (0.07f * scale))
                {
                    train.transform.position = stop + new Vector3(Mathf.Sin(t * Mathf.PI * 0.5f) * 0.10f, 0f, 0f);
                    yield return null;
                }
            }
            Vector3 launch = train.transform.position;
            float puffTimer = 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / (0.38f * scale))
            {
                train.transform.position = Vector3.LerpUnclamped(launch, to, Ease.InCubic(t));
                puffTimer -= Time.deltaTime;
                if (lively && puffTimer <= 0f && t < 0.6f)
                {
                    puffTimer = 0.05f;
                    Puff.Burst(_pool, train.transform.position + new Vector3(train.HalfLength, 0.25f, 0f), 1, 0.5f);
                }
                yield return null;
            }
            train.transform.rotation = Quaternion.identity;
            foreach (PassengerView v in riders)
            {
                _boarding.Remove(v);
                v.EndRide();
                _station.ReleasePassenger(v);
            }
            _trains.Remove(train);
            _station.ReleaseTrain(train);
            _occupiedTracks.Remove(track);
            int left;
            if (_busyPlatforms.TryGetValue(e.Platform, out left)) _busyPlatforms[e.Platform] = Mathf.Max(0, left - 1);

            if (epoch == _epoch)
            {
                _active--;
                CheckIdle();
            }
        }

        private static bool AnyMoving(List<PassengerView> views)
        {
            for (int i = 0; i < views.Count; i++) if (views[i].IsMoving) return true;
            return false;
        }
    }

    /// <summary>Confetti bursts (see Fx). The pool argument is kept for callers and no longer used.</summary>
    public static class Confetti
    {
        public static void Burst(ObjectPoolManager pool, Vector3 origin, Color color, int count)
        {
            Fx.Confetti(origin, color, count);
        }
    }

    /// <summary>Dust and steam puffs (see Fx). The pool argument is kept for callers and no longer used.</summary>
    public static class Puff
    {
        public static void Burst(ObjectPoolManager pool, Vector3 origin, int count, float spread)
        {
            Fx.Dust(origin, count + 1, spread);
        }
    }
}
