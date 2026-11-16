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
            foreach (PassengerView v in _boarding) _station.ReleasePassenger(v);
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
                        _active++;
                        v.WalkTo(target, WalkTime, 0.35f, () =>
                        {
                            if (epoch != _epoch) return;
                            _audio.Play(Sfx.Step);
                            if (!GameSettings.ReducedMotion) Puff.Burst(_pool, target + new Vector3(0f, 0.05f, 0f), 2, 0.12f);
                            _active--;
                            CheckIdle();
                        });
                        break;
                    }
                    case BoardEventType.PlatformColorAssigned:
                        _audio.Play(Sfx.Assign);
                        break;
                    case BoardEventType.TrainDeparted:
                        StartCoroutine(TrainSequence(e, epoch));
                        break;
                    case BoardEventType.PlatformUnlocked:
                        _audio.Play(Sfx.Booster);
                        if (e.Platform < _station.Platforms.Count) _station.Platforms[e.Platform].Pulse();
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
            platform.Pulse();
            foreach (PassengerView v in riders) v.Celebrate();
            if (!GameSettings.ReducedMotion) Confetti.Burst(_pool, platform.transform.position + Vector3.up * 1.2f, Palette.Line(e.Color), 22);

            // One train per track at a time: later trains wait for the line to clear instead of driving through.
            int track = Mathf.RoundToInt(platform.TrainStop.z * 10f);
            if (_occupiedTracks.Contains(track))
            {
                _trainsWaiting++;
                while (_occupiedTracks.Contains(track)) yield return null;
                _trainsWaiting--;
            }
            _occupiedTracks.Add(track);

            TrainView train = _station.AcquireTrain();
            _trains.Add(train);
            train.Configure(e.Color, platform.Capacity);
            Vector3 stop = platform.TrainStop;
            Vector3 from = stop + Vector3.right * 18f;
            Vector3 to = stop - Vector3.right * 18f;
            train.transform.position = from;
            // Hurry a little when other trains are queued behind this one.
            float scale = TrainScale * (_trainsWaiting > 0 ? 0.65f : 1f);

            bool lively = !GameSettings.ReducedMotion;
            _audio.Play(Sfx.TrainArrive);
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

            _audio.Play(Sfx.Doors);
            for (float t = 0f; t < 1f; t += Time.deltaTime / (0.12f * scale)) { train.SetDoors(Ease.OutCubic(t)); yield return null; }
            train.SetDoors(1f);

            for (int i = 0; i < riders.Count; i++)
                riders[i].Board(train.DoorPosition(i), 0.20f * scale + 0.03f * i);
            yield return new WaitForSeconds(0.21f * scale + 0.03f * riders.Count);
            foreach (PassengerView v in riders)
            {
                _boarding.Remove(v);
                _station.ReleasePassenger(v);
            }

            for (float t = 0f; t < 1f; t += Time.deltaTime / (0.09f * scale)) { train.SetDoors(1f - Ease.Smooth(t)); yield return null; }
            train.SetDoors(0f);

            _audio.Play(Sfx.TrainDepart);
            if (TrainDeparted != null) TrainDeparted();
            // Depart: a short backwards wind-up, then accelerate away trailing puffs.
            if (lively)
            {
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

    /// <summary>Pooled confetti made of small cubes; no particle shaders required.</summary>
    public sealed class Confetti : MonoBehaviour
    {
        private const string PoolKey = "confetti";
        private Vector3 _velocity;
        private Vector3 _spin;
        private float _life;
        private ObjectPoolManager _pool;

        public static void Burst(ObjectPoolManager pool, Vector3 origin, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = pool.Get(PoolKey, Create);
                go.transform.SetParent(pool.transform, false);
                go.transform.position = origin;
                Color c = i % 3 == 0 ? Color.white : (i % 3 == 1 ? color : Palette.Gold);
                Prims.SetColor(go, c);
                var piece = go.GetComponent<Confetti>();
                piece._pool = pool;
                piece._life = 0.9f + UnityEngine.Random.value * 0.4f;
                piece._velocity = new Vector3(UnityEngine.Random.Range(-2.6f, 2.6f), UnityEngine.Random.Range(3.5f, 6.5f), UnityEngine.Random.Range(-1.6f, 1.6f));
                piece._spin = UnityEngine.Random.onUnitSphere * 540f;
            }
        }

        private static GameObject Create()
        {
            GameObject go = Prims.Make(PrimitiveType.Cube, null, Vector3.zero, new Vector3(0.14f, 0.14f, 0.03f), Color.white, "Confetti");
            Prims.NoShadow(go);
            go.AddComponent<Confetti>();
            return go;
        }

        private void Update()
        {
            if (_pool == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _velocity.y -= 12f * dt;
            transform.position += _velocity * dt;
            transform.Rotate(_spin * dt);
            _life -= dt;
            if (_life <= 0f || transform.position.y < 0f) _pool.Release(PoolKey, gameObject);
        }
    }

    /// <summary>Pooled dust/steam puffs: small pale spheres that drift, swell and vanish.</summary>
    public sealed class Puff : MonoBehaviour
    {
        private const string PoolKey = "puff";
        private Vector3 _velocity;
        private float _life, _maxLife, _size;
        private ObjectPoolManager _pool;

        public static void Burst(ObjectPoolManager pool, Vector3 origin, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = pool.Get(PoolKey, Create);
                go.transform.SetParent(pool.transform, false);
                go.transform.position = origin + new Vector3(UnityEngine.Random.Range(-spread, spread), 0f, UnityEngine.Random.Range(-0.1f, 0.1f));
                go.transform.localScale = Vector3.zero;
                var puff = go.GetComponent<Puff>();
                puff._pool = pool;
                puff._maxLife = puff._life = 0.45f + UnityEngine.Random.value * 0.25f;
                puff._size = 0.22f + UnityEngine.Random.value * 0.16f;
                puff._velocity = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(0.5f, 1.1f), UnityEngine.Random.Range(-0.6f, -0.1f));
            }
        }

        private static GameObject Create()
        {
            GameObject go = Prims.Make(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * 0.2f, new Color(0.96f, 0.97f, 1f), "Puff");
            Prims.NoShadow(go);
            go.AddComponent<Puff>();
            return go;
        }

        private void Update()
        {
            if (_pool == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f) { _pool.Release(PoolKey, gameObject); return; }
            _velocity *= 1f - Mathf.Min(1f, 2.5f * dt);
            transform.position += _velocity * dt;
            float k = _life / _maxLife; // 1 -> 0
            transform.localScale = Vector3.one * (_size * (0.5f + 0.5f * (1f - k)) * Mathf.Clamp01(k * 2.5f));
        }
    }
}
