using System;
using MetroSort.Puzzle;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Visual for one passenger. Purely presentational: it is told where to stand and animates itself
    /// (idle breathing and blinking, stepping feet and swinging arms while walking, waving when selected,
    /// cheering on a match, shrinking through the train door when boarding).
    /// Each colour has a distinct hat shape so lines are never told apart by hue alone.
    /// </summary>
    public sealed class PassengerView : MonoBehaviour
    {
        public const string PoolKey = "passenger";
        private static readonly Color[] HairTones =
        {
            new Color(0.26f, 0.17f, 0.11f), new Color(0.11f, 0.10f, 0.12f), new Color(0.55f, 0.34f, 0.17f),
            new Color(0.80f, 0.62f, 0.30f), new Color(0.45f, 0.20f, 0.14f)
        };
        private static readonly Color[] SkinTones =
        {
            new Color(1.00f, 0.87f, 0.75f), new Color(0.97f, 0.79f, 0.64f), new Color(0.82f, 0.62f, 0.46f), new Color(0.58f, 0.40f, 0.29f)
        };
        private static readonly Color[] TrouserTones =
        {
            new Color(0.20f, 0.24f, 0.40f), new Color(0.25f, 0.26f, 0.30f), new Color(0.44f, 0.34f, 0.25f), new Color(0.30f, 0.38f, 0.48f)
        };
        private const float BaseScale = 1.02f;
        private const string ModelPath = "Characters/Passenger";
        /// <summary>Size of passengers who are not at the front of their queue; set by the station layout.</summary>
        public static float WaitingScale = 0.74f;

        public int Id { get; private set; }
        public int ColorIndex { get; private set; }
        public bool Vip { get; private set; }
        public bool Selected;
        public bool Held;

        /// <summary>One renderer of the model with the material-slot names it was authored with.</summary>
        private sealed class Slots
        {
            public Renderer Renderer;
            public string[] Names;
            public Material[] Materials;
            public int Hat = -1;
        }

        private static GameObject _prefab;

        private Transform _visual;
        private Animation _anim;
        private string _clipIdle, _clipWalk, _clipWave, _clipCheer, _clipRefuse, _playing;
        private readonly System.Collections.Generic.List<Slots> _slots = new System.Collections.Generic.List<Slots>();
        private readonly GameObject[] _hats = new GameObject[ColorCodes.Count];
        private readonly GameObject[] _hairStyles = new GameObject[5];
        private GameObject _glow, _skirt, _moustache, _lashes, _eyesOpen, _eyesClosed, _halo, _mysteryMark;
        private Color _skin, _hair, _lower, _legs, _mouth;
        private bool _eyesShut;
        private float _base = BaseScale;
        private bool _accessible = true;
        private bool _mystery;
        private float _scale = 1f;
        private float _phase;

        private Vector3 _from, _to;
        private float _t = 1f, _duration = 0.3f, _hop, _delay, _walkDistance;
        private Action _onArrive;
        private bool _boarding;
        private float _bounce, _shake, _celebrate, _lift, _yaw, _lean;
        private float _blinkTimer, _blink;
        private float _spawn = 1f, _spawnDelay;

        public bool IsMoving { get { return _t < 1f; } }
        public Vector3 Target { get { return _to; } }

        public static GameObject CreateObject()
        {
            var go = new GameObject("Passenger");
            go.AddComponent<PassengerView>().Build();
            return go;
        }

        /// <summary>
        /// Instantiates the rigged model authored in Blender (Tools/art/passenger.py). Optional parts are child
        /// objects found by name; colours are applied per material slot, also by name.
        /// </summary>
        private void Build()
        {
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            // Soft glow on the floor under a picked-up passenger.
            _glow = Prims.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.02f, 0f), new Vector3(1.05f, 0.008f, 1.05f), Palette.HintGlow, "SelectGlow");
            Prims.NoShadow(_glow);
            _glow.SetActive(false);

            if (_prefab == null) _prefab = Resources.Load<GameObject>(ModelPath);
            if (_prefab == null)
            {
                Debug.LogError("Metro Sort: passenger model missing at Resources/" + ModelPath);
                return;
            }
            GameObject model = Instantiate(_prefab, _visual, false);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // the model faces +Z, the game camera looks at -Z

            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("Hat") && n.Length == 4) _hats[n[3] - '0'] = t.gameObject;
                else if (n.StartsWith("Hair") && n.Length == 5) _hairStyles[n[4] - '0'] = t.gameObject;
                else if (n == "Skirt") _skirt = t.gameObject;
                else if (n == "Moustache") _moustache = t.gameObject;
                else if (n == "Lashes") _lashes = t.gameObject;
                else if (n == "EyesOpen") _eyesOpen = t.gameObject;
                else if (n == "EyesClosed") _eyesClosed = t.gameObject;
                else if (n == "Halo") _halo = t.gameObject;
                else if (n == "Mystery") _mysteryMark = t.gameObject;
            }

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = r.sharedMaterials;
                var slots = new Slots { Renderer = r, Names = new string[source.Length], Materials = new Material[source.Length] };
                for (int i = 0; i < source.Length; i++) slots.Names[i] = source[i] != null ? source[i].name : "";
                string n = r.gameObject.name;
                if (n.StartsWith("Hat") && n.Length == 4) slots.Hat = n[3] - '0';
                bool small = n == "EyesOpen" || n == "EyesClosed" || n == "Brows" || n == "Cheeks" || n == "Mouth" || n == "Lashes" || n == "Moustache";
                r.shadowCastingMode = small ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                var skinned = r as SkinnedMeshRenderer;
                if (skinned != null)
                {
                    skinned.updateWhenOffscreen = false;
                    skinned.quality = SkinQuality.Bone1; // every vertex follows exactly one bone
                    // squash, hops and raised arms leave the bind-pose bounds
                    skinned.localBounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2.4f, 3.2f, 2.4f));
                }
                _slots.Add(slots);
            }

            _anim = model.GetComponent<Animation>();
            if (_anim != null)
            {
                _anim.playAutomatically = false;
                _anim.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (AnimationState state in _anim)
                {
                    state.wrapMode = WrapMode.Loop;
                    string n = state.name;
                    if (n.EndsWith("Idle")) _clipIdle = n;
                    else if (n.EndsWith("Walk")) _clipWalk = n;
                    else if (n.EndsWith("Wave")) _clipWave = n;
                    else if (n.EndsWith("Cheer")) _clipCheer = n;
                    else if (n.EndsWith("Refuse")) _clipRefuse = n;
                }
            }
        }

        private static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        private void Play(string clip, float fade)
        {
            if (_anim == null || clip == null || _playing == clip) return;
            _playing = clip;
            _anim.CrossFade(clip, fade);
        }

        private Material MaterialFor(string slot, int hat)
        {
            switch (slot)
            {
                case "Jacket": return MaterialLibrary.Get(JacketColor());
                case "Hat": return MaterialLibrary.Get(Palette.Lines[Mathf.Clamp(hat, 0, Palette.Lines.Length - 1)]);
                case "Skin": return MaterialLibrary.Get(_skin);
                case "Hair": return MaterialLibrary.Get(_hair);
                case "Lower": return MaterialLibrary.Get(_lower);
                case "Legs": return MaterialLibrary.Get(_legs);
                case "Mouth": return MaterialLibrary.Get(_mouth);
                case "Shoes": return MaterialLibrary.Get(new Color(0.17f, 0.17f, 0.22f));
                case "EyeDark": return MaterialLibrary.Get(new Color(0.13f, 0.10f, 0.12f), true);
                case "Blush": return MaterialLibrary.Get(Palette.Blush);
                case "Grey": return MaterialLibrary.Get(new Color(0.52f, 0.55f, 0.62f));
                case "Gold": return MaterialLibrary.Get(Palette.Gold, true);
                case "Outline": return ModelKit.Outline;
                default: return MaterialLibrary.Get(Color.white);
            }
        }

        private void ApplyMaterials()
        {
            foreach (Slots s in _slots)
            {
                for (int i = 0; i < s.Names.Length; i++) s.Materials[i] = MaterialFor(s.Names[i], s.Hat);
                s.Renderer.sharedMaterials = s.Materials;
            }
        }

        private Color JacketColor()
        {
            Color c = Palette.Line(ColorIndex);
            if (_mystery) c = new Color(0.60f, 0.63f, 0.69f);
            else if (!_accessible) c = Color.Lerp(c, new Color(0.62f, 0.65f, 0.70f), 0.45f);
            return c;
        }

        public void Configure(Passenger p)
        {
            Id = p.Id;
            ColorIndex = p.Color;
            Vip = p.Vip;
            gameObject.name = "Passenger_" + p.Id;
            for (int c = 0; c < _hats.Length; c++) Show(_hats[c], c == p.Color);
            Show(_halo, p.Vip);
            _phase = p.Id * 1.37f;

            // Deterministic variety per passenger: woman or man, skin tone, hair colour and style, clothes.
            bool female = (p.Id * 7 + p.Color * 3) % 2 == 0;
            _skin = SkinTones[(p.Id * 7 + 1) % SkinTones.Length];
            _hair = HairTones[(p.Id * 5 + 2) % HairTones.Length];
            _lower = TrouserTones[(p.Id * 3) % TrouserTones.Length];
            _legs = female ? _skin : _lower;
            _mouth = female ? new Color(0.86f, 0.30f, 0.38f) : new Color(0.66f, 0.30f, 0.30f);
            _base = BaseScale * (female ? 0.95f : 1f);
            Show(_skirt, female);
            Show(_lashes, female);
            Show(_moustache, !female && p.Id % 3 == 0);
            int style = female ? 2 + (p.Id * 11 + 1) % 3 : (p.Id * 11 + 1) % 2;
            for (int i = 0; i < _hairStyles.Length; i++) Show(_hairStyles[i], i == style);

            _accessible = true;
            _mystery = false;
            Show(_mysteryMark, false);
            _scale = 1f;
            Selected = false;
            Held = false;
            _t = 1f;
            _delay = 0f;
            _onArrive = null;
            _boarding = false;
            _bounce = _shake = _celebrate = _lift = _yaw = _lean = _blink = 0f;
            _blinkTimer = 1.2f + (p.Id * 0.37f) % 3f;
            _spawn = 1f;
            _spawnDelay = 0f;
            _visual.localRotation = Quaternion.identity;
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one * _base;
            _eyesShut = false;
            Show(_eyesOpen, true);
            Show(_eyesClosed, false);
            ApplyMaterials();

            // Start everyone at a different point of the idle loop so a queue does not breathe in unison.
            _playing = null;
            if (_anim != null && _clipIdle != null)
            {
                _anim.Stop();
                _anim.Play(_clipIdle);
                _anim[_clipIdle].normalizedTime = (p.Id * 0.37f) % 1f;
                _playing = _clipIdle;
            }
        }

        private void ApplyBodyColor()
        {
            ApplyMaterials();
        }

        /// <summary>Front-of-queue passengers are full size and saturated; waiting ones are smaller and muted.</summary>
        public void SetAccessible(bool accessible)
        {
            if (_accessible == accessible) return;
            _accessible = accessible;
            ApplyBodyColor();
        }

        /// <summary>A mystery passenger hides their line (grey jacket, question mark) until revealed.</summary>
        public void SetMystery(bool mystery, bool animate)
        {
            if (_mystery == mystery) return;
            _mystery = mystery;
            Show(_mysteryMark, mystery);
            for (int c = 0; c < _hats.Length; c++) Show(_hats[c], !mystery && c == ColorIndex);
            ApplyBodyColor();
            if (!mystery && animate) Bounce(); // a little pop when the line is revealed
        }

        public void Place(Vector3 position)
        {
            _t = 1f;
            _delay = 0f;
            _onArrive = null;
            _to = position;
            transform.position = position;
        }

        /// <summary>Starts a walk. If another walk was in flight its completion callback fires first.</summary>
        public void WalkTo(Vector3 position, float duration, float hop, Action onArrive, float delay = 0f)
        {
            if (_onArrive != null)
            {
                Action previous = _onArrive;
                _onArrive = null;
                previous();
            }
            _from = transform.position;
            _to = position;
            _duration = Mathf.Max(0.01f, duration);
            _hop = GameSettings.ReducedMotion ? 0f : hop;
            _delay = GameSettings.ReducedMotion ? 0f : delay;
            _walkDistance = Vector3.Distance(_from, _to);
            _t = 0f;
            _onArrive = onArrive;
        }

        /// <summary>Walks into a train door, shrinking on the way so the passenger disappears inside.</summary>
        public void Board(Vector3 door, float duration)
        {
            WalkTo(door, duration, 0.40f, null);
            _boarding = true;
        }

        /// <summary>Hides the passenger and pops them in after a delay (level intro).</summary>
        public void PopIn(float delay)
        {
            if (GameSettings.ReducedMotion) return;
            _spawn = 0f;
            _spawnDelay = delay;
        }

        public void Bounce() { _bounce = 1f; }
        public void Shake() { _shake = 1f; Haptics.Thump(); }
        public void Celebrate() { _celebrate = 1f; }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool calm = GameSettings.ReducedMotion;
            bool walking = false;
            Vector3 dir = Vector3.zero;

            if (_t < 1f && !Held)
            {
                if (_delay > 0f) _delay -= dt;
                else
                {
                    _t = Mathf.Min(1f, _t + dt / _duration);
                    float e = Ease.Smooth(_t);
                    Vector3 p = Vector3.LerpUnclamped(_from, _to, e);
                    p.y += Mathf.Sin(e * Mathf.PI) * _hop;
                    transform.position = p;
                    walking = _t < 1f;
                    dir = _to - _from;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
                    if (_t >= 1f)
                    {
                        if (!_boarding) _bounce = Mathf.Max(_bounce, 0.6f); // landing squash
                        if (_onArrive != null)
                        {
                            Action cb = _onArrive;
                            _onArrive = null;
                            cb();
                        }
                    }
                }
            }

            // Intro pop-in.
            float spawnScale = 1f;
            if (_spawn < 1f)
            {
                if (_spawnDelay > 0f) { _spawnDelay -= dt; spawnScale = 0f; }
                else
                {
                    _spawn = Mathf.Min(1f, _spawn + dt / 0.30f);
                    spawnScale = Mathf.Max(0f, Ease.OutBack(_spawn));
                }
            }

            float targetScale = _accessible ? 1f : WaitingScale;
            _scale = Mathf.MoveTowards(_scale, targetScale, dt * 2.5f);
            _lift = Mathf.MoveTowards(_lift, Selected || Held ? 0.28f : 0f, dt * 2.5f);
            _bounce = Mathf.MoveTowards(_bounce, 0f, dt * 3.5f);
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 3.0f);
            _celebrate = Mathf.MoveTowards(_celebrate, 0f, dt * 1.8f);

            float time = Time.time;
            float bob = calm ? 0f : Mathf.Sin(time * (Selected ? 7f : 2.2f) + _phase) * (Selected ? 0.05f : 0.025f);
            float squash = bob + Mathf.Sin(_bounce * Mathf.PI) * 0.20f;
            float jump = calm ? 0f : Mathf.Abs(Mathf.Sin(_celebrate * Mathf.PI * 2f)) * 0.35f * _celebrate;
            float boardScale = _boarding ? Mathf.Lerp(1f, 0.40f, Ease.Smooth(_t)) : 1f;
            float s = _scale * spawnScale * boardScale * _base;

            bool picked = Selected || Held;
            if (_glow.activeSelf != picked) _glow.SetActive(picked);
            if (picked)
            {
                float pulse = calm ? 1f : 1f + Mathf.Sin(time * 6f) * 0.08f;
                _glow.transform.localScale = new Vector3(1.05f * pulse, 0.008f, 1.05f * pulse);
            }

            // Body motion comes from the model's clips; this picks which one plays.
            bool cheering = _celebrate > 0.02f && !calm;
            bool refusing = _shake > 0.05f && !calm;
            if (cheering) Play(_clipCheer, 0.08f);
            else if (refusing) Play(_clipRefuse, 0.08f);
            else if ((Selected || Held) && !calm) Play(_clipWave, 0.12f);
            else if (walking && !calm) Play(_clipWalk, 0.10f);
            else Play(_clipIdle, 0.18f);
            if (_anim != null && _clipWalk != null && walking)
            {
                // Two steps per loop: match the stride to the ground covered.
                float steps = Mathf.Max(2f, Mathf.Round(_walkDistance / 0.45f));
                _anim[_clipWalk].speed = Mathf.Clamp(steps * 0.5f * _anim[_clipWalk].length / _duration, 0.8f, 3.2f);
            }

            // Turn a little towards the direction of travel and lean into the walk.
            float yawTarget = walking && !calm ? -dir.x * 38f : 0f;
            float leanTarget = walking && !calm ? dir.z * 7f : 0f;
            _yaw = Mathf.MoveTowards(_yaw, yawTarget, dt * 260f);
            _lean = Mathf.MoveTowards(_lean, leanTarget, dt * 60f);

            // Blink now and then; cheering passengers keep a happy squint.
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blink = 1f;
                _blinkTimer = 2.0f + ((Id * 13 + Mathf.FloorToInt(time * 3f)) % 7) * 0.45f;
            }
            _blink = Mathf.MoveTowards(_blink, 0f, dt * 7f);
            bool shut = cheering || (!calm && _blink > 0.25f);
            if (shut != _eyesShut)
            {
                _eyesShut = shut;
                Show(_eyesOpen, !shut);
                Show(_eyesClosed, shut);
            }

            _visual.localScale = new Vector3(s * (1f - squash * 0.5f), s * (1f + squash), s * (1f - squash * 0.5f));
            _visual.localPosition = new Vector3(Mathf.Sin(_shake * Mathf.PI * 6f) * 0.10f * _shake, _lift + jump, 0f);
            _visual.localRotation = Quaternion.Euler(_lean, _yaw, 0f);
        }
    }
}
