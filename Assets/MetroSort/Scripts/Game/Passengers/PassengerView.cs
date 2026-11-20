using System;
using MetroSort.Puzzle;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Visual for one passenger. Purely presentational: it is told where to stand and animates itself with the
    /// clips authored in Blender (idle and fidgets, walk, a jump sampled along each hop and a landing, kicking
    /// while carried, cheering on a match, refusing a move, slumping when the level is lost) plus blinking and
    /// mouth changes.
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
        /// <summary>The sculpted, textured passenger (Tools/art/passenger.py --hd); the built one is the fallback.</summary>
        private const string HdModelPath = "Characters/PassengerHD_";
        public static bool UseHd = true;
        private static GameObject[] _hdModels;
        private static Texture2D[] _hdAlbedos;
        private static int _nextVariant;
        private Texture2D _hdAlbedo;
        private bool _hd;
        /// <summary>Size of passengers who are not at the front of their queue; set by the station layout.</summary>
        public static float WaitingScale = 0.74f;

        public int Id { get; private set; }
        public int ColorIndex { get; private set; }
        public bool Vip { get; private set; }
        public bool Selected;
        public bool Held;
        /// <summary>Standing on a platform that needs just one more passenger.</summary>
        public bool Eager;
        /// <summary>The bench is full or the moves are nearly gone.</summary>
        public bool Worried;

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
        private string _clipPicked, _clipSad, _clipJump, _clipLand, _clipLook, _clipTap, _clipYawn, _clipVip, _clipReveal, _clipSit, _clipLean;
        // Seated on the bench, or riding a train and leaning out of a window (see RideAtWindow).
        private bool _seated, _riding;
        private Transform _rideHome;
        private readonly Vector3[] _hatHome = new Vector3[ColorCodes.Count];
        private Vector3 _mysteryHome, _mysteryScale, _haloAxis;
        private Quaternion _mysteryTurn, _haloTurn;
        private float _reveal, _whirl;
        private bool _entering;
        private string _clipEager, _clipWorry;
        private string _action;
        private float _actionLeft, _fidgetTimer;
        private int _fidgets;
        private bool _cheering;
        private GameObject _mouthSmile, _mouthOpen, _mouthSad, _mouthShown;
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

            // Sculpted variants PassengerHD_0, _1, ... each with its painted texture; pooled views take turns
            // so a queue mixes them. Without any, the built model is used.
            if (_hdModels == null)
            {
                var models = new System.Collections.Generic.List<GameObject>();
                var albedos = new System.Collections.Generic.List<Texture2D>();
                for (int i = 0; UseHd && i < 16; i++)
                {
                    var m = Resources.Load<GameObject>(HdModelPath + i);
                    var t = Resources.Load<Texture2D>(HdModelPath + i + "_albedo");
                    if (m == null || t == null) break;
                    models.Add(m);
                    albedos.Add(t);
                }
                _hdModels = models.ToArray();
                _hdAlbedos = albedos.ToArray();
            }
            GameObject model0 = null;
            if (_hdModels.Length > 0)
            {
                int v = _nextVariant++ % _hdModels.Length;
                model0 = _hdModels[v];
                _hdAlbedo = _hdAlbedos[v];
                _hd = true;
            }
            if (_prefab == null) _prefab = Resources.Load<GameObject>(ModelPath);
            if (model0 == null) model0 = _prefab;
            if (model0 == null)
            {
                Debug.LogError("Metro Sort: passenger model missing at Resources/" + ModelPath);
                return;
            }
            GameObject model = Instantiate(model0, _visual, false);
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
                else if (n == "Mouth") _mouthSmile = t.gameObject;
                else if (n == "MouthOpen") _mouthOpen = t.gameObject;
                else if (n == "MouthSad") _mouthSad = t.gameObject;
            }

            // Rest placement of the parts that get animated by hand (hat swap on a reveal, the spinning halo).
            for (int c = 0; c < _hats.Length; c++) if (_hats[c] != null) _hatHome[c] = _hats[c].transform.localPosition;
            if (_mysteryMark != null)
            {
                _mysteryHome = _mysteryMark.transform.localPosition;
                _mysteryScale = _mysteryMark.transform.localScale;
                _mysteryTurn = _mysteryMark.transform.localRotation;
            }
            if (_halo != null)
            {
                _haloTurn = _halo.transform.localRotation;
                _haloAxis = _halo.transform.parent.InverseTransformDirection(Vector3.up);
            }

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = r.sharedMaterials;
                var slots = new Slots { Renderer = r, Names = new string[source.Length], Materials = new Material[source.Length] };
                for (int i = 0; i < source.Length; i++) slots.Names[i] = source[i] != null ? source[i].name : "";
                string n = r.gameObject.name;
                if (n.StartsWith("Hat") && n.Length == 4) slots.Hat = n[3] - '0';
                bool small = n == "EyesOpen" || n == "EyesClosed" || n == "Brows" || n == "Cheeks" || n.StartsWith("Mouth") || n == "Lashes" || n == "Moustache";
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
                    string n = state.name;
                    bool loop = false;
                    if (n.EndsWith("Idle")) { _clipIdle = n; loop = true; }
                    else if (n.EndsWith("Walk")) { _clipWalk = n; loop = true; }
                    else if (n.EndsWith("Wave")) { _clipWave = n; loop = true; }
                    else if (n.EndsWith("Cheer")) { _clipCheer = n; loop = true; }
                    else if (n.EndsWith("Picked")) { _clipPicked = n; loop = true; }
                    else if (n.EndsWith("Sad")) { _clipSad = n; loop = true; }
                    else if (n.EndsWith("Sit")) { _clipSit = n; loop = true; }
                    else if (n.EndsWith("Lean")) { _clipLean = n; loop = true; }
                    else if (n.EndsWith("Refuse")) _clipRefuse = n;
                    else if (n.EndsWith("Jump")) _clipJump = n;
                    else if (n.EndsWith("Land")) _clipLand = n;
                    else if (n.EndsWith("Look")) _clipLook = n;
                    else if (n.EndsWith("Tap")) _clipTap = n;
                    else if (n.EndsWith("Yawn")) _clipYawn = n;
                    else if (n.EndsWith("Reveal")) _clipReveal = n;
                    else if (n.EndsWith("Eager")) { _clipEager = n; loop = true; }
                    else if (n.EndsWith("Worry")) { _clipWorry = n; loop = true; }
                    else if (n.EndsWith("Vip")) { _clipVip = n; loop = true; }
                    state.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
                }
                if (_clipPicked == null) _clipPicked = _clipWave;
                if (_clipJump == null) _clipJump = _clipWalk;
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

        /// <summary>Plays a one-shot clip from its start for the given time (its own length by default).</summary>
        private void StartAction(string clip, float seconds = 0f)
        {
            if (_anim == null || clip == null || GameSettings.ReducedMotion) return;
            _action = clip;
            _actionLeft = seconds > 0f ? seconds : _anim[clip].length;
            _anim[clip].time = 0f;
            if (_playing == clip) _playing = null;
        }

        private void ShowMouth(GameObject mouth)
        {
            if (mouth == null) mouth = _mouthSmile;
            if (mouth == _mouthShown) return;
            _mouthShown = mouth;
            Show(_mouthSmile, mouth == _mouthSmile);
            Show(_mouthOpen, mouth == _mouthOpen);
            Show(_mouthSad, mouth == _mouthSad);
        }

        /// <summary>Draws the dark ink outline around passengers (off: the soft clay look of the key art).</summary>
        public static bool InkOutline = false;
        private static Material _hidden;
        private static Material Hidden
        {
            get
            {
                if (_hidden == null)
                {
                    _hidden = new Material(Shader.Find("Sprites/Default"));
                    _hidden.color = new Color(0f, 0f, 0f, 0f);
                }
                return _hidden;
            }
        }

        /// <summary>Character materials use the occlusion baked into the model; cloth is matt with a sheen.</summary>
        private static Material Mat(Color c, bool glossy = false, bool fabric = false)
        {
            return MaterialLibrary.Get(c, glossy, true, fabric);
        }

        private Material MaterialFor(string slot, int hat)
        {
            if (_hd)
            {
                // The textured body: its regions are recoloured, everything else (face, buttons, shoes) as painted.
                Material m = null;
                switch (slot)
                {
                    case "Jacket": m = MaterialLibrary.GetCharacter(_hdAlbedo, JacketColor(), 2f, 1f); break;
                    case "Hair": m = MaterialLibrary.GetCharacter(_hdAlbedo, _hair, 2f, 0f); break;
                    case "Skin": m = MaterialLibrary.GetCharacter(_hdAlbedo, _skin, 2f, 0f); break;
                    case "Lower": m = MaterialLibrary.GetCharacter(_hdAlbedo, _lower, 2f, 1f); break;
                    case "Detail": m = MaterialLibrary.GetCharacter(_hdAlbedo, Color.white, 1f, 0f); break;
                }
                if (m != null) return m;
            }
            switch (slot)
            {
                case "Jacket": return Mat(JacketColor(), false, true);
                case "Hat": return Mat(Palette.Lines[Mathf.Clamp(hat, 0, Palette.Lines.Length - 1)], false, true);
                case "Skin": return Mat(_skin);
                case "SkinFlat": return Mat(_skin); // eyelids and patches laid over a painted face
                case "Hair": return Mat(_hair, false, true);
                case "Lower": return Mat(_lower, false, true);
                case "Legs": return Mat(_legs, false, _legs != _skin);
                case "Mouth": return Mat(_mouth);
                case "MouthIn": return Mat(new Color(0.42f, 0.12f, 0.16f));
                case "Shoes": return Mat(new Color(0.17f, 0.17f, 0.22f), true);
                case "EyeDark": return Mat(new Color(0.13f, 0.10f, 0.12f), true);
                case "Blush": return Mat(Palette.Blush);
                case "Grey": return Mat(new Color(0.52f, 0.55f, 0.62f), false, true);
                case "Gold": return Mat(Palette.Gold, true);
                // Soft clay look without the ink line; the hull stays in the model and is just not drawn.
                case "Outline": return InkOutline ? ModelKit.Outline : Hidden;
                default: return Mat(Color.white);
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
            EndRide();
            _seated = false;
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
            _base = BaseScale * (female && !_hd ? 0.95f : 1f); // HD variants carry their own build
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
            _mouthShown = null;
            ShowMouth(_mouthSmile);
            _cheering = false;
            _entering = false;
            Eager = Worried = false;
            _whirl = 0f;
            EndReveal();
            _action = null;
            _actionLeft = 0f;
            _fidgets = p.Id;
            _fidgetTimer = 2.5f + (p.Id * 0.73f) % 9f;
            ApplyMaterials();

            // Start everyone at a different point of the idle loop so a queue does not breathe in unison.
            _playing = null;
            string idle = IdleClip;
            if (_anim != null && idle != null)
            {
                _anim.Stop();
                _anim.Play(idle);
                _anim[idle].normalizedTime = (p.Id * 0.37f) % 1f;
                _playing = idle;
            }
        }

        /// <summary>VIPs wait with their own haughty stance.</summary>
        private string IdleClip
        {
            get
            {
                if (Worried && _clipWorry != null) return _clipWorry;
                if (Eager && _clipEager != null) return _clipEager;
                return Vip && _clipVip != null ? _clipVip : _clipIdle;
            }
        }

        /// <summary>The hinted passenger waves to catch the player's eye.</summary>
        public void Beckon()
        {
            Bounce();
            StartAction(_clipWave, 1.6f);
        }

        /// <summary>
        /// Booster transitions: leaps from where the passenger used to stand to where they were just placed,
        /// optionally spinning on the way (degrees).
        /// </summary>
        public void FlyFrom(Vector3 from, float delay, float hop, float spin)
        {
            if (GameSettings.ReducedMotion) return;
            Vector3 spot = _to;
            transform.position = from;
            WalkTo(spot, 0.42f, hop, null, delay);
            _whirl = spin;
        }

        /// <summary>Puts the hats back where the model has them after a reveal (or when the view is reused).</summary>
        private void EndReveal()
        {
            _reveal = 0f;
            for (int c = 0; c < _hats.Length; c++) if (_hats[c] != null) _hats[c].transform.localPosition = _hatHome[c];
            if (_mysteryMark == null) return;
            _mysteryMark.transform.localPosition = _mysteryHome;
            _mysteryMark.transform.localScale = _mysteryScale;
            _mysteryMark.transform.localRotation = _mysteryTurn;
            Show(_mysteryMark, _mystery);
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
            if (_reveal > 0f) EndReveal();
            if (!mystery && animate)
            {
                if (GameSettings.ReducedMotion || _mysteryMark == null) { Bounce(); return; }
                // The grey cap is knocked off into the air and the real hat drops into place.
                _reveal = 1f;
                Show(_mysteryMark, true);
                StartAction(_clipReveal);
                GateFlaps.PlaySound(Sfx.Reveal);
                Fx.Sparkle(transform.position + Vector3.up * 1.6f, Palette.Line(ColorIndex), 8, 2.4f);
            }
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
        /// <summary>Sitting on the bench (legs swinging) instead of standing; only shows once the passenger has arrived.</summary>
        public void SetSeated(bool seated) { _seated = seated; }

        /// <summary>Runs to a spot without the hop used for ordinary moves, e.g. to a train door and on inside.</summary>
        public void RunTo(Vector3 position, float duration, float delay, Action onArrive)
        {
            WalkTo(position, duration, 0f, onArrive, delay);
            _boarding = false;
            _cheering = false;
            _celebrate = 0f;
            _seated = false;
        }

        /// <summary>
        /// Rides along with a train car, leaning out of a window and waving. The car is a closed shell, so the
        /// passenger stands just inside its wall and the Lean clip folds them forward at the hips: head, hat and
        /// arms poke out through the window, face turned up to the camera, while the rest stays hidden inside.
        /// </summary>
        public void RideAtWindow(Transform car, Vector3 localPosition, float scale)
        {
            if (!_riding) _rideHome = transform.parent;
            _riding = true;
            _t = 1f;
            _delay = 0f;
            _onArrive = null;
            _boarding = false;
            _cheering = false;
            _celebrate = 0f;
            Selected = false;
            Held = false;
            transform.SetParent(car, false);
            transform.localPosition = localPosition;
            // Without the Lean clip, tip the whole figure forward instead so something still shows.
            transform.localRotation = _clipLean != null ? Quaternion.identity : Quaternion.Euler(-32f, 0f, 0f);
            transform.localScale = Vector3.one * scale;
            _bounce = 0.6f;
            string ride = _clipLean != null ? _clipLean : _clipWave;
            if (_anim != null && ride != null) _anim[ride].normalizedTime = (Id % 4) * 0.25f; // riders wave out of step
        }

        /// <summary>Undoes RideAtWindow before the passenger goes back to the pool. Safe to call when not riding.</summary>
        public void EndRide()
        {
            if (!_riding) return;
            _riding = false;
            transform.SetParent(_rideHome, false);
            _rideHome = null;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        public void Board(Vector3 door, float duration)
        {
            WalkTo(door, duration, 0.40f, null);
            _boarding = true;
            _cheering = false;
        }

        /// <summary>
        /// Level intro: starts out of sight beyond the ticket gates and walks to the spot the passenger was
        /// placed at, back to the camera, turning round on arrival.
        /// </summary>
        public void WalkIn(Vector3 from, float delay, float duration)
        {
            if (GameSettings.ReducedMotion) return;
            Vector3 spot = _to;
            transform.position = from;
            WalkTo(spot, duration, 0f, null, delay);
            _entering = true;
            _spawn = 0f;
            _spawnDelay = delay;
            _yaw = 180f;
        }

        /// <summary>Hides the passenger and pops them in after a delay (level intro).</summary>
        public void PopIn(float delay)
        {
            if (GameSettings.ReducedMotion) return;
            _spawn = 0f;
            _spawnDelay = delay;
        }

        public void Bounce() { _bounce = 1f; }
        public void Shake() { _shake = 1f; Haptics.Thump(); StartAction(_clipRefuse); }

        /// <summary>Starts cheering; the passenger keeps jumping for joy until they board.</summary>
        public void Celebrate()
        {
            _celebrate = 1f;
            _cheering = !GameSettings.ReducedMotion;
            if (_cheering && _anim != null && _clipCheer != null && _playing != _clipCheer)
                _anim[_clipCheer].normalizedTime = (Id % 3) * 0.14f; // neighbours jump out of step
        }

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
                        _entering = false;
                        _whirl = 0f;
                        if (!_boarding)
                        {
                            // Landing: the clip does the crouch and rebound, the squash adds a little weight.
                            bool landed = _hop > 0.2f && _clipLand != null && !calm;
                            _bounce = Mathf.Max(_bounce, landed ? 0.3f : 0.6f);
                            if (landed) StartAction(_clipLand);
                        }
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
                if (_spawnDelay > 0f)
                {
                    _spawnDelay -= dt;
                    spawnScale = 0f;
                    if (_spawnDelay <= 0f && !_entering) StartAction(_clipLand);
                }
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
            // The cheer clip jumps by itself; the procedural hop only covers a model without one.
            float jump = calm || _clipCheer != null ? 0f : Mathf.Abs(Mathf.Sin(_celebrate * Mathf.PI * 2f)) * 0.35f * _celebrate;
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
            bool cheering = (_cheering || _celebrate > 0.02f) && !calm;
            bool hopping = walking && !calm && (_hop > 0.2f || _boarding);
            bool lost = GameApp.Instance != null && GameApp.Instance.States.Current == AppState.LevelFailed;
            if (_actionLeft > 0f) _actionLeft -= dt;
            if (walking && _action != _clipRefuse) _actionLeft = 0f;
            bool acting = _actionLeft > 0f && !calm;
            bool refusing = acting && _action == _clipRefuse;
            GameObject mouth = _mouthSmile;
            bool sleepy = false;
            if (_riding && !calm)
            {
                Play(_clipLean != null ? _clipLean : (_clipWave != null ? _clipWave : IdleClip), 0.10f);
                mouth = _mouthOpen;
            }
            else if (cheering)
            {
                Play(_clipCheer, 0.08f);
                mouth = _mouthOpen;
            }
            else if (hopping)
            {
                // The jump clip is a pose track: take-off at the start of the hop, touch-down at its end.
                Play(_clipJump, 0.05f);
                if (_anim != null && _clipJump != null && _clipJump != _clipWalk)
                {
                    _anim[_clipJump].speed = 0f;
                    _anim[_clipJump].normalizedTime = Mathf.Min(_t, 0.999f);
                }
                mouth = _mouthOpen;
            }
            else if (refusing)
            {
                Play(_clipRefuse, 0.06f);
                mouth = _mouthSad;
            }
            else if (picked && !calm)
            {
                Play(_clipPicked, 0.10f);
                mouth = _mouthOpen;
                _actionLeft = 0f;
            }
            else if (walking && !calm) Play(_clipWalk, 0.10f);
            else if (_seated && _clipSit != null && !calm && !lost) Play(_clipSit, 0.18f);
            else if (acting)
            {
                Play(_action, 0.10f);
                if (_action == _clipYawn)
                {
                    float at = 1f - _actionLeft / _anim[_action].length;
                    sleepy = at > 0.2f && at < 0.72f;
                    if (sleepy) mouth = _mouthOpen;
                }
            }
            else if (lost && !calm)
            {
                Play(_clipSad, 0.30f);
                mouth = _mouthSad;
            }
            else
            {
                Play(IdleClip, 0.18f);
                if (Worried) mouth = _mouthSad;
                else if (Eager) mouth = _mouthOpen;
                // Now and then a waiting passenger looks around, taps a foot, stretches or waves (VIPs are above that).
                _fidgetTimer -= dt;
                if (_fidgetTimer <= 0f && !calm && _spawn >= 1f && !Vip && !Eager && !Worried)
                {
                    _fidgetTimer = UnityEngine.Random.Range(9f, 22f);
                    switch (_fidgets++ % 4)
                    {
                        case 0: StartAction(_clipLook); break;
                        case 1: StartAction(_clipTap); break;
                        case 2: StartAction(_clipYawn); break;
                        default: StartAction(_clipWave, 1.6f); break;
                    }
                }
            }
            ShowMouth(mouth);
            if (_anim != null && _clipWalk != null && walking && !hopping)
            {
                // Two steps per loop: match the stride to the ground covered.
                float steps = Mathf.Max(2f, Mathf.Round(_walkDistance / 0.45f));
                _anim[_clipWalk].speed = Mathf.Clamp(steps * 0.5f * _anim[_clipWalk].length / _duration, 0.8f, 3.2f);
            }

            // Turn a little towards the direction of travel and lean into the walk.
            float yawTarget = _entering ? 180f : (walking && !calm ? -dir.x * 38f : 0f);
            float leanTarget = walking && !calm && !_entering ? dir.z * 7f : 0f;
            _yaw = Mathf.MoveTowards(_yaw, yawTarget, dt * (Mathf.Abs(_yaw - yawTarget) > 60f ? 900f : 260f));

            // A revealed mystery passenger: the grey cap spins away upwards while the real hat lands.
            if (_reveal > 0f)
            {
                _reveal -= dt / 0.5f;
                if (_reveal <= 0f) EndReveal();
                else
                {
                    float k = 1f - _reveal;
                    Transform cap = _mysteryMark.transform;
                    Vector3 up = cap.parent.InverseTransformVector(Vector3.up);
                    cap.localPosition = _mysteryHome + up * (k * 2.2f - k * k * 0.9f);
                    cap.localScale = _mysteryScale * (1f - k * k);
                    cap.localRotation = Quaternion.AngleAxis(k * 540f, up.normalized) * _mysteryTurn;
                    GameObject hat = ColorIndex >= 0 && ColorIndex < _hats.Length ? _hats[ColorIndex] : null;
                    if (hat != null)
                        hat.transform.localPosition = _hatHome[ColorIndex] + up * ((1f - Ease.OutBack(Mathf.Clamp01(k * 1.5f))) * 0.8f);
                }
            }

            // The VIP halo turns and bobs, and twinkles now and then.
            if (Vip && _halo != null && !calm)
            {
                Transform halo = _halo.transform;
                halo.localRotation = Quaternion.AngleAxis(time * 140f + _phase * 40f, _haloAxis) * _haloTurn;
                if (Mathf.FloorToInt((time + _phase) / 1.3f) != Mathf.FloorToInt((time + _phase - dt) / 1.3f) && _spawn >= 1f)
                    Fx.Sparkle(transform.position + Vector3.up * (2.2f * _scale), Palette.Gold, 2, 0.9f);
            }
            _lean = Mathf.MoveTowards(_lean, leanTarget, dt * 60f);

            // Blink now and then; cheering passengers keep a happy squint.
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blink = 1f;
                _blinkTimer = 2.0f + ((Id * 13 + Mathf.FloorToInt(time * 3f)) % 7) * 0.45f;
            }
            _blink = Mathf.MoveTowards(_blink, 0f, dt * 7f);
            bool shut = cheering || sleepy || (!calm && _blink > 0.25f);
            if (shut != _eyesShut)
            {
                _eyesShut = shut;
                Show(_eyesOpen, !shut);
                Show(_eyesClosed, shut);
            }

            _visual.localScale = new Vector3(s * (1f - squash * 0.5f), s * (1f + squash), s * (1f - squash * 0.5f));
            _visual.localPosition = new Vector3(Mathf.Sin(_shake * Mathf.PI * 6f) * (_clipRefuse != null ? 0.04f : 0.10f) * _shake, _lift + jump, 0f);
            _visual.localRotation = Quaternion.Euler(_lean, _yaw + (_t < 1f ? _whirl * Ease.Smooth(_t) : 0f), 0f);
        }
    }
}
