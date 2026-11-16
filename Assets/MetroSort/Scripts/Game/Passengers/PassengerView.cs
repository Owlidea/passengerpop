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
        private const float Ink = 0.022f;

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
        private const float BaseScale = 1.10f;
        /// <summary>Size of passengers who are not at the front of their queue; set by the station layout.</summary>
        public static float WaitingScale = 0.74f;

        public int Id { get; private set; }
        public int ColorIndex { get; private set; }
        public bool Vip { get; private set; }
        public bool Selected;
        public bool Held;

        private Transform _visual;
        private readonly GameObject[] _hats = new GameObject[ColorCodes.Count];
        private readonly Transform[] _legs = new Transform[2];
        private readonly Transform[] _arms = new Transform[2];
        private readonly Transform[] _eyes = new Transform[2];
        private readonly GameObject[] _hairStyles = new GameObject[5];
        private readonly GameObject[] _lashes = new GameObject[2];
        private readonly GameObject[] _legParts = new GameObject[2];
        private GameObject _torso, _shoulders, _skirt, _moustache, _mouth;
        private float _base = BaseScale;
        private readonly System.Collections.Generic.List<GameObject> _jacketParts = new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.List<GameObject> _skinParts = new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.List<GameObject> _hairParts = new System.Collections.Generic.List<GameObject>();
        private GameObject _halo;
        private bool _accessible = true;
        private bool _mystery;
        private GameObject _mysteryMark;
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

        private void Build()
        {
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            // Legs swing from the hip, arms from the shoulder.
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                Transform leg = new GameObject("Leg").transform;
                leg.SetParent(_visual, false);
                leg.localPosition = new Vector3(side * 0.10f, 0.56f, 0f);
                GameObject trouser = Prims.Make(PrimitiveType.Capsule, leg, new Vector3(0f, -0.25f, 0f), new Vector3(0.165f, 0.26f, 0.175f), TrouserTones[0], "LegShape");
                _legParts[i] = trouser;
                Prims.Make(PrimitiveType.Sphere, leg, new Vector3(0f, -0.505f, -0.04f), new Vector3(0.17f, 0.11f, 0.28f), new Color(0.16f, 0.16f, 0.20f), "Shoe");
                _legs[i] = leg;

                Transform arm = new GameObject("Arm").transform;
                arm.SetParent(_visual, false);
                arm.localPosition = new Vector3(side * 0.29f, 1.04f, 0f);
                GameObject sleeve = Prims.Make(PrimitiveType.Capsule, arm, new Vector3(0f, -0.20f, 0f), new Vector3(0.125f, 0.21f, 0.13f), Palette.Neutral, "Sleeve");
                _jacketParts.Add(sleeve);
                _skinParts.Add(Prims.Make(PrimitiveType.Sphere, arm, new Vector3(0f, -0.44f, 0f), new Vector3(0.115f, 0.125f, 0.115f), Palette.Skin, "Hand"));
                _arms[i] = arm;
            }

            // Torso: jacket in the line colour, shoulders, shirt collar, belt. Women get a skirt (see Configure).
            _torso = Prims.Make(PrimitiveType.Capsule, _visual, new Vector3(0f, 0.82f, 0f), new Vector3(0.44f, 0.27f, 0.31f), Palette.Neutral, "Torso");
            InkOutline.Add(_torso, Ink);
            _jacketParts.Add(_torso);
            _shoulders = Prims.Make(PrimitiveType.Sphere, _visual, new Vector3(0f, 1.01f, 0f), new Vector3(0.57f, 0.17f, 0.31f), Palette.Neutral, "Shoulders");
            _jacketParts.Add(_shoulders);
            Prims.NoShadow(Prims.Make(PrimitiveType.Sphere, _visual, new Vector3(0f, 1.01f, -0.13f), new Vector3(0.13f, 0.15f, 0.06f), Color.white, "Shirt"));
            Prims.NoShadow(Prims.Make(PrimitiveType.Cylinder, _visual, new Vector3(0f, 0.585f, 0f), new Vector3(0.40f, 0.018f, 0.29f), new Color(0.16f, 0.16f, 0.20f), "Belt"));
            _skirt = Prims.Make(PrimitiveType.Cylinder, _visual, new Vector3(0f, 0.47f, 0f), new Vector3(0.50f, 0.12f, 0.40f), TrouserTones[0], "Skirt");
            InkOutline.Add(_skirt, Ink);
            _skinParts.Add(Prims.Make(PrimitiveType.Cylinder, _visual, new Vector3(0f, 1.105f, 0f), new Vector3(0.13f, 0.04f, 0.13f), Palette.Skin, "Neck"));

            // Head at a natural size for the body, tilted back a little so the face is visible from above.
            Transform head = new GameObject("Head").transform;
            head.SetParent(_visual, false);
            head.localPosition = new Vector3(0f, 1.11f, 0f);
            head.localRotation = Quaternion.Euler(14f, 0f, 0f);
            head.localScale = Vector3.one * 0.84f;

            // Parts that share a colour are merged into one mesh each, so a passenger costs far fewer draw calls.
            GameObject skull = Prims.Make(PrimitiveType.Sphere, head, new Vector3(0f, 0.21f, 0f), new Vector3(0.40f, 0.44f, 0.40f), Palette.Skin, "Skull");
            GameObject earL = Prims.Make(PrimitiveType.Sphere, head, new Vector3(-0.20f, 0.20f, 0.01f), new Vector3(0.07f, 0.10f, 0.06f), Palette.Skin, "Ear");
            GameObject earR = Prims.Make(PrimitiveType.Sphere, head, new Vector3(0.20f, 0.20f, 0.01f), new Vector3(0.07f, 0.10f, 0.06f), Palette.Skin, "Ear");
            GameObject nose = Prims.Make(PrimitiveType.Sphere, head, new Vector3(0f, 0.185f, -0.205f), new Vector3(0.048f, 0.062f, 0.05f), Palette.Skin, "Nose");
            skull.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            GameObject face = Prims.Combine(head, "HeadSkin", skull, earL, earR, nose);
            InkOutline.Add(face, Ink);
            _skinParts.Add(face);

            var brows = new GameObject[2];
            var cheeks = new GameObject[2];
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                // Eye = white with a dark iris; the whole eye squashes to blink.
                Transform eye = new GameObject("Eye").transform;
                eye.SetParent(head, false);
                eye.localPosition = new Vector3(side * 0.078f, 0.235f, -0.168f);
                GameObject white = Prims.Make(PrimitiveType.Sphere, eye, Vector3.zero, new Vector3(0.088f, 0.078f, 0.05f), Color.white, "White");
                GameObject glint = Prims.Make(PrimitiveType.Sphere, eye, new Vector3(-0.012f, 0.012f, -0.035f), new Vector3(0.018f, 0.018f, 0.01f), Color.white, "Glint");
                Prims.Make(PrimitiveType.Sphere, eye, new Vector3(0f, -0.003f, -0.02f), new Vector3(0.05f, 0.056f, 0.03f), new Color(0.16f, 0.12f, 0.10f), "Iris");
                Prims.Combine(eye, "EyeWhite", white, glint);
                GameObject lash = Prims.Make(PrimitiveType.Cube, eye, new Vector3(side * 0.03f, 0.04f, -0.02f), new Vector3(0.07f, 0.014f, 0.012f), new Color(0.10f, 0.08f, 0.08f), "Lashes");
                lash.transform.localRotation = Quaternion.Euler(0f, 0f, side * 22f);
                _lashes[i] = lash;
                _eyes[i] = eye;

                brows[i] = Prims.Make(PrimitiveType.Cube, head, new Vector3(side * 0.078f, 0.297f, -0.182f), new Vector3(0.092f, 0.017f, 0.02f), HairTones[0], "Brow");
                brows[i].transform.localRotation = Quaternion.Euler(0f, 0f, side * -6f);
                cheeks[i] = Prims.Make(PrimitiveType.Sphere, head, new Vector3(side * 0.118f, 0.155f, -0.158f), new Vector3(0.075f, 0.045f, 0.04f), Palette.Blush, "Cheek");
            }
            _hairParts.Add(Prims.Combine(head, "Brows", brows));
            Prims.Combine(head, "Cheeks", cheeks);
            _mouth = Prims.Make(PrimitiveType.Sphere, head, new Vector3(0f, 0.115f, -0.182f), new Vector3(0.085f, 0.024f, 0.02f), new Color(0.72f, 0.32f, 0.33f), "Mouth");
            _moustache = Prims.Make(PrimitiveType.Sphere, head, new Vector3(0f, 0.148f, -0.196f), new Vector3(0.12f, 0.03f, 0.03f), HairTones[0], "Moustache");
            _hairParts.Add(_moustache);

            // Hair: each style is one merged mesh (cap + its extra pieces). 0 short, 1 fringe (men); 2 long, 3 bun, 4 ponytail (women).
            for (int style = 0; style < _hairStyles.Length; style++)
            {
                var pieces = new System.Collections.Generic.List<GameObject>();
                GameObject hairCap = Prims.Make(PrimitiveType.Sphere, head, new Vector3(0f, 0.30f, 0.035f), new Vector3(0.435f, 0.36f, 0.43f), HairTones[0], "HairCap");
                hairCap.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pieces.Add(hairCap);
                if (style == 1 || style == 2 || style == 4)
                    pieces.Add(Prims.Make(PrimitiveType.Sphere, head, new Vector3(0.03f, 0.355f, -0.15f), new Vector3(0.34f, 0.11f, 0.12f), HairTones[0], "Fringe"));
                if (style == 2)
                {
                    pieces.Add(Prims.Make(PrimitiveType.Capsule, head, new Vector3(0f, 0.06f, 0.13f), new Vector3(0.44f, 0.30f, 0.22f), HairTones[0], "LongHair"));
                    for (int side = -1; side <= 1; side += 2)
                        pieces.Add(Prims.Make(PrimitiveType.Capsule, head, new Vector3(side * 0.19f, 0.10f, 0.02f), new Vector3(0.10f, 0.22f, 0.16f), HairTones[0], "SideHair"));
                }
                if (style == 3)
                    pieces.Add(Prims.Make(PrimitiveType.Sphere, head, new Vector3(0f, 0.44f, 0.17f), new Vector3(0.19f, 0.19f, 0.19f), HairTones[0], "Bun"));
                if (style == 4)
                {
                    pieces.Add(Prims.Make(PrimitiveType.Sphere, head, new Vector3(0f, 0.30f, 0.23f), new Vector3(0.14f, 0.14f, 0.14f), HairTones[0], "PonytailKnot"));
                    GameObject tail = Prims.Make(PrimitiveType.Capsule, head, new Vector3(0f, 0.10f, 0.28f), new Vector3(0.12f, 0.20f, 0.12f), HairTones[0], "Ponytail");
                    tail.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
                    pieces.Add(tail);
                }
                GameObject merged = Prims.Combine(head, "HairStyle" + style, pieces.ToArray());
                _hairParts.Add(merged);
                merged.SetActive(false);
                _hairStyles[style] = merged;
            }

            for (int c = 0; c < _hats.Length; c++)
            {
                var hat = new GameObject("Hat" + c);
                hat.transform.SetParent(head, false);
                // Hat shapes are authored around a head top at y = 1.16; rescale and drop them onto this head.
                hat.transform.localScale = Vector3.one * 0.86f;
                hat.transform.localPosition = new Vector3(0f, 0.415f - 1.16f * 0.86f, 0f);
                BuildHat(c, hat.transform, Palette.Lines[c]);
                hat.SetActive(false);
                _hats[c] = hat;
            }

            // Mystery look: a grey cap and a white question mark instead of the line hat.
            _mysteryMark = new GameObject("MysteryMark");
            _mysteryMark.transform.SetParent(head, false);
            Transform mm = _mysteryMark.transform;
            var grey = new Color(0.52f, 0.55f, 0.62f);
            GameObject cap = Prims.Make(PrimitiveType.Sphere, mm, new Vector3(0f, 0.33f, 0.02f), new Vector3(0.46f, 0.34f, 0.46f), grey, "Cap");
            InkOutline.Add(cap, 0.016f);
            float[][] strokes =
            {
                new[] { -0.055f, 0.865f, 0.035f, 0.055f }, new[] { 0f, 0.895f, 0.13f, 0.035f }, new[] { 0.055f, 0.845f, 0.035f, 0.09f },
                new[] { 0.025f, 0.795f, 0.08f, 0.035f }, new[] { 0f, 0.75f, 0.035f, 0.07f }
            };
            var markPieces = new System.Collections.Generic.List<GameObject>();
            foreach (float[] st in strokes)
                markPieces.Add(Prims.Make(PrimitiveType.Cube, mm, new Vector3(st[0], st[1], -0.02f), new Vector3(st[2], st[3], 0.03f), Color.white, "Stroke"));
            markPieces.Add(Prims.Make(PrimitiveType.Sphere, mm, new Vector3(0f, 0.665f, -0.02f), new Vector3(0.045f, 0.045f, 0.035f), Color.white, "Dot"));
            Prims.Combine(mm, "QuestionMark", markPieces.ToArray());
            _mysteryMark.SetActive(false);

            _halo = Prims.Make(PrimitiveType.Cylinder, head, new Vector3(0f, 0.86f, 0f), new Vector3(0.42f, 0.018f, 0.42f), Palette.Gold, "VipHalo");
            Prims.SetGlossy(_halo, Palette.Gold);
            _halo.SetActive(false);
        }

        private static void BuildHat(int color, Transform parent, Color tint)
        {
            BuildHatShape(color, parent, tint);
            var pieces = new System.Collections.Generic.List<GameObject>();
            foreach (MeshFilter piece in parent.GetComponentsInChildren<MeshFilter>()) pieces.Add(piece.gameObject);
            GameObject merged = Prims.Combine(parent, "HatMesh", pieces.ToArray());
            merged.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            InkOutline.Add(merged, 0.016f);
        }

        private static void BuildHatShape(int color, Transform parent, Color tint)
        {
            switch (color)
            {
                case 0: // Red: round cap
                    Prims.Make(PrimitiveType.Sphere, parent, new Vector3(0f, 1.10f, 0f), new Vector3(0.50f, 0.30f, 0.50f), tint);
                    Prims.Make(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.07f, -0.20f), new Vector3(0.34f, 0.02f, 0.30f), tint);
                    break;
                case 1: // Blue: square box hat
                    Prims.Make(PrimitiveType.Cube, parent, new Vector3(0f, 1.24f, 0f), new Vector3(0.34f, 0.26f, 0.34f), tint);
                    break;
                case 2: // Green: tall top hat
                    Prims.Make(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.13f, 0f), new Vector3(0.52f, 0.02f, 0.52f), tint);
                    Prims.Make(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.30f, 0f), new Vector3(0.32f, 0.16f, 0.32f), tint);
                    break;
                case 3: // Yellow: stepped party cone
                    Prims.Make(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.16f, 0f), new Vector3(0.36f, 0.05f, 0.36f), tint);
                    Prims.Make(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.26f, 0f), new Vector3(0.24f, 0.05f, 0.24f), tint);
                    Prims.Make(PrimitiveType.Sphere, parent, new Vector3(0f, 1.36f, 0f), new Vector3(0.14f, 0.14f, 0.14f), tint);
                    break;
                case 4: // Purple: two round ears
                    Prims.Make(PrimitiveType.Sphere, parent, new Vector3(-0.19f, 1.16f, 0f), new Vector3(0.20f, 0.20f, 0.14f), tint);
                    Prims.Make(PrimitiveType.Sphere, parent, new Vector3(0.19f, 1.16f, 0f), new Vector3(0.20f, 0.20f, 0.14f), tint);
                    break;
                default: // Orange: wide flat sun hat
                    Prims.Make(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.13f, 0f), new Vector3(0.74f, 0.02f, 0.74f), tint);
                    Prims.Make(PrimitiveType.Sphere, parent, new Vector3(0f, 1.14f, 0f), new Vector3(0.36f, 0.20f, 0.36f), tint);
                    break;
            }
        }

        public void Configure(Passenger p)
        {
            Id = p.Id;
            ColorIndex = p.Color;
            Vip = p.Vip;
            gameObject.name = "Passenger_" + p.Id;
            for (int c = 0; c < _hats.Length; c++) _hats[c].SetActive(c == p.Color);
            _halo.SetActive(p.Vip);
            _phase = p.Id * 1.37f;

            // Deterministic variety per passenger: woman or man, skin tone, hair colour and style, clothes.
            bool female = (p.Id * 7 + p.Color * 3) % 2 == 0;
            Color skin = SkinTones[(p.Id * 7 + 1) % SkinTones.Length];
            Color hair = HairTones[(p.Id * 5 + 2) % HairTones.Length];
            Color lower = TrouserTones[(p.Id * 3) % TrouserTones.Length];
            foreach (GameObject part in _skinParts) Prims.SetColor(part, skin);
            foreach (GameObject part in _hairParts) Prims.SetColor(part, hair);

            // Silhouette: women are a little shorter with narrower shoulders, a skirt and bare legs;
            // men have broad shoulders, a straight torso and trousers.
            _base = BaseScale * (female ? 0.94f : 1f);
            _torso.transform.localScale = female ? new Vector3(0.36f, 0.27f, 0.27f) : new Vector3(0.44f, 0.27f, 0.31f);
            _shoulders.transform.localScale = female ? new Vector3(0.45f, 0.15f, 0.27f) : new Vector3(0.57f, 0.17f, 0.31f);
            _skirt.SetActive(female);
            Prims.SetColor(_skirt, lower);
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                _arms[i].localPosition = new Vector3(side * (female ? 0.235f : 0.29f), 1.04f, 0f);
                _legs[i].localPosition = new Vector3(side * (female ? 0.085f : 0.10f), 0.56f, 0f);
                _legParts[i].transform.localScale = female ? new Vector3(0.13f, 0.26f, 0.14f) : new Vector3(0.165f, 0.26f, 0.175f);
                Prims.SetColor(_legParts[i], female ? skin : lower);
                _lashes[i].SetActive(female);
            }
            _moustache.SetActive(!female && p.Id % 3 == 0);
            Prims.SetColor(_mouth, female ? new Color(0.86f, 0.30f, 0.38f) : new Color(0.66f, 0.34f, 0.32f));
            _mouth.transform.localScale = female ? new Vector3(0.09f, 0.032f, 0.02f) : new Vector3(0.085f, 0.022f, 0.02f);
            int style = female ? 2 + (p.Id * 11 + 1) % 3 : (p.Id * 11 + 1) % 2;
            for (int i = 0; i < _hairStyles.Length; i++) _hairStyles[i].SetActive(i == style);

            _accessible = true;
            _mystery = false;
            _mysteryMark.SetActive(false);
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
            for (int i = 0; i < 2; i++)
            {
                _legs[i].localRotation = Quaternion.identity;
                _arms[i].localRotation = Quaternion.identity;
                _eyes[i].localScale = Vector3.one;
            }
            ApplyBodyColor();
        }

        private void ApplyBodyColor()
        {
            Color c = Palette.Line(ColorIndex);
            if (_mystery) c = new Color(0.60f, 0.63f, 0.69f);
            else if (!_accessible) c = Color.Lerp(c, new Color(0.62f, 0.65f, 0.70f), 0.45f);
            foreach (GameObject part in _jacketParts) Prims.SetColor(part, c);
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
            _mysteryMark.SetActive(mystery);
            for (int c = 0; c < _hats.Length; c++) _hats[c].SetActive(!mystery && c == ColorIndex);
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
        public void Shake() { _shake = 1f; }
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

            // Stride: -1..1 through each pair of steps while walking.
            float stride = 0f;
            if (walking && !calm)
            {
                float steps = Mathf.Max(2f, Mathf.Round(_walkDistance / 0.45f));
                stride = Mathf.Sin(_t * steps * Mathf.PI);
            }
            bool cheering = _celebrate > 0.02f && !calm;
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                _legs[i].localRotation = Quaternion.Euler(stride * sign * 30f, 0f, 0f);
                Quaternion arm;
                if (cheering) arm = Quaternion.Euler(0f, 0f, sign * (150f + Mathf.Sin(time * 16f + i * 2f) * 12f));
                else if ((Selected || Held) && !calm) arm = i == 1
                    ? Quaternion.Euler(0f, 0f, 140f + Mathf.Sin(time * 10f) * 22f)
                    : Quaternion.Euler(0f, 0f, -12f);
                else arm = Quaternion.Euler(-stride * sign * 32f, 0f, sign * 5f);
                _arms[i].localRotation = arm;
            }

            // Turn a little towards the direction of travel and lean into the walk.
            float yawTarget = walking && !calm ? -dir.x * 38f : 0f;
            float leanTarget = walking && !calm ? dir.z * 7f : 0f;
            _yaw = Mathf.MoveTowards(_yaw, yawTarget, dt * 260f);
            _lean = Mathf.MoveTowards(_lean, leanTarget, dt * 60f);

            // Blink now and then.
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blink = 1f;
                _blinkTimer = 2.0f + ((Id * 13 + Mathf.FloorToInt(time * 3f)) % 7) * 0.45f;
            }
            _blink = Mathf.MoveTowards(_blink, 0f, dt * 7f);
            float eyeOpen = calm ? 1f : 1f - Mathf.Sin(_blink * Mathf.PI) * 0.9f;
            if (cheering) eyeOpen = 0.35f; // happy squint
            for (int i = 0; i < 2; i++) _eyes[i].localScale = new Vector3(1f, Mathf.Max(0.08f, eyeOpen), 1f);

            _visual.localScale = new Vector3(s * (1f - squash * 0.5f), s * (1f + squash), s * (1f - squash * 0.5f));
            _visual.localPosition = new Vector3(Mathf.Sin(_shake * Mathf.PI * 6f) * 0.10f * _shake, _lift + jump, 0f);
            _visual.localRotation = Quaternion.Euler(_lean, _yaw, stride * 6f);
        }
    }
}
