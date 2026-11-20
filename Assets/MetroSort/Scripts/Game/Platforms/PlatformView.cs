using MetroSort.Puzzle;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>Visual for one transfer platform: slab, slots, line sign, fill pips, highlight and lock barrier.</summary>
    public sealed class PlatformView : MonoBehaviour
    {
        public const float SlotSpacing = 0.95f;
        public const float Depth = 1.5f;

        public int Index { get; private set; }
        public int Capacity { get; private set; }
        public float Width { get; private set; }
        /// <summary>Text shown above the platform by the HUD (lock condition, remaining uses, express).</summary>
        public string Label { get; private set; }
        public Vector3 LabelAnchor { get { return transform.position + new Vector3(0f, 1.9f, 0.4f); } }
        /// <summary>Trains ride in the sunken track, so the car floor lines up with the platform.</summary>
        public Vector3 TrainStop { get { return transform.position + new Vector3(0f, -0.37f, 1.45f); } }

        private GameObject _body, _glow, _sign, _barrier;
        private BoomGate _boom;
        private GameObject[] _pips;
        private float _pulse, _drop;
        private Vector3 _home;
        private Transform _signHead;
        private Color _signColor;
        private float _signSpin;
        private float[] _pipPop;
        private int _shownCount;
        private bool _built;
        private static readonly Vector3 PipSize = new Vector3(0.20f, 0.20f, 0.12f);
        private bool _highlight, _hint;
        private Color _slabColor = Palette.Platform;
        private Color _rimColor = new Color(0.62f, 0.68f, 0.80f);

        public static float WidthFor(int capacity) { return capacity * SlotSpacing + 0.6f; }

        public static PlatformView Create(Transform parent, int index, PlatformState state, Vector3 position)
        {
            var go = new GameObject("Platform_" + index);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var view = go.AddComponent<PlatformView>();
            view.Build(index, state);
            return view;
        }

        private void Build(int index, PlatformState state)
        {
            Index = index;
            Capacity = state.Capacity;
            Width = WidthFor(Capacity);
            Transform t = transform;

            float body = Width - Depth * 0.5f;
            _glow = Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.03f, 0f), new Vector3(Width + 0.35f, 0.06f, Depth + 0.35f), Palette.Highlight, "Glow");
            Prims.NoShadow(_glow);
            _glow.SetActive(false);
            // Pill-shaped slab: a straight middle stretched between two round ends (Models/Platform).
            _body = new GameObject("Body");
            _body.transform.SetParent(t, false);
            ModelKit.Spawn("Platform", "Mid", _body.transform).transform.localScale = new Vector3(body, 1f, 1f);
            ModelKit.Spawn("Platform", "Cap", _body.transform, "CapL", true).transform.localPosition = new Vector3(-body * 0.5f, 0f, 0f);
            ModelKit.Spawn("Platform", "Cap", _body.transform, "CapR").transform.localPosition = new Vector3(body * 0.5f, 0f, 0f);
            // Safety line on the track side; orange on express platforms.
            GameObject line = Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.285f, Depth * 0.5f - 0.12f), new Vector3(body, 0.02f, 0.10f), state.Express ? Palette.Lines[5] : Palette.Gold, "SafetyLine");
            Prims.NoShadow(line);

            for (int i = 0; i < Capacity; i++)
            {
                Vector3 local = SlotLocal(i);
                GameObject spot = ModelKit.Spawn("Platform", "Slot", t, "Slot" + i);
                spot.transform.localPosition = new Vector3(local.x, 0.293f, local.z); // just above the framed top surface
                ModelKit.Paint(spot, PlatformMaterial);
                foreach (MeshRenderer r in spot.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Line sign on a pole at the left end.
            Prims.Make(PrimitiveType.Cylinder, t, new Vector3(-Width * 0.5f + 0.15f, 0.85f, 0.45f), new Vector3(0.07f, 0.6f, 0.07f), Palette.Dark, "Pole");
            // The frame and the coloured face share a pivot so the sign can spin when the line changes.
            _signHead = new GameObject("SignHead").transform;
            _signHead.SetParent(t, false);
            _signHead.localPosition = new Vector3(-Width * 0.5f + 0.15f, 1.55f, 0.45f);
            Prims.MakeRounded(_signHead, new Vector3(0f, 0f, 0.02f), new Vector3(0.74f, 0.54f, 0.08f), 0.035f, Color.white, "SignFrame");
            _sign = Prims.MakeRounded(_signHead, new Vector3(0f, 0f, -0.03f), new Vector3(0.62f, 0.42f, 0.10f), 0.045f, Palette.Neutral, "Sign", true);

            _pips = new GameObject[Capacity];
            _pipPop = new float[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                float x = (i - (Capacity - 1) * 0.5f) * 0.30f;
                _pips[i] = Prims.Make(PrimitiveType.Sphere, t, new Vector3(x, 0.20f, -Depth * 0.5f - 0.02f), PipSize, Palette.Neutral, "Pip" + i);
                Prims.NoShadow(_pips[i]);
            }

            _barrier = new GameObject("Barrier");
            _barrier.transform.SetParent(t, false);
            // The striped arm hangs off the left post, so it can swing up when the platform opens.
            var arm = new GameObject("Arm").transform;
            arm.SetParent(_barrier.transform, false);
            arm.localPosition = new Vector3(-Width * 0.42f, 0.60f, -0.2f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * (Width / 4.2f);
                Prims.Make(PrimitiveType.Cube, arm, new Vector3(x + Width * 0.42f, 0f, 0f), new Vector3(Width / 4.6f, 0.16f, 0.08f), i % 2 == 0 ? Palette.UiRed : Color.white);
            }
            _boom = _barrier.AddComponent<BoomGate>();
            _boom.Arm = arm;
            Prims.Make(PrimitiveType.Cylinder, _barrier.transform, new Vector3(-Width * 0.42f, 0.45f, -0.2f), new Vector3(0.08f, 0.2f, 0.08f), Palette.Dark);
            Prims.Make(PrimitiveType.Cylinder, _barrier.transform, new Vector3(Width * 0.42f, 0.45f, -0.2f), new Vector3(0.08f, 0.2f, 0.08f), Palette.Dark);

            Refresh(state);
            _built = true;
        }

        private Vector3 SlotLocal(int slot)
        {
            return new Vector3((slot - (Capacity - 1) * 0.5f) * SlotSpacing, 0.28f, -0.10f);
        }

        public Vector3 SlotPosition(int slot)
        {
            return transform.position + SlotLocal(Mathf.Clamp(slot, 0, Capacity - 1));
        }

        /// <summary>Mirrors the logical platform state. Safe to call every time the board changes.</summary>
        public void Refresh(PlatformState s) { Refresh(s, false); }

        private bool _scheduled;

        /// <summary>scheduled: the level runs on a train timetable, so the sign names the train at this platform.</summary>
        public void Refresh(PlatformState s, bool scheduled)
        {
            _scheduled = scheduled;
            bool blocked = s.IsLocked || s.Closed;
            _slabColor = blocked ? Palette.PlatformLocked : Palette.Platform;
            // The band around the slab wears the line this platform serves (or is reserved for).
            int rimLine = s.Count > 0 ? s.Color : s.ReservedColor;
            _rimColor = blocked ? new Color(0.55f, 0.57f, 0.62f) : (rimLine >= 0 ? Palette.Line(rimLine) : new Color(0.62f, 0.68f, 0.80f));
            if (blocked) _boom.Close();
            else if (_built) _boom.Open();
            else _barrier.SetActive(false);
            Color line = s.Count > 0 ? Palette.Line(s.Color) : Palette.Neutral;
            Color sign = s.ReservedColor >= 0 ? Palette.Line(s.ReservedColor) : line;
            Prims.SetGlossy(_sign, sign);
            for (int i = 0; i < _pips.Length; i++) Prims.SetColor(_pips[i], i < s.Count ? line : Palette.Neutral);
            if (_built && !GameSettings.ReducedMotion)
            {
                // The sign spins round when the platform takes on (or gives up) a line; each new pip pops.
                if (sign != _signColor) _signSpin = 1f;
                for (int i = _shownCount; i < s.Count && i < _pipPop.Length; i++) _pipPop[i] = 1f;
            }
            _signColor = sign;
            _shownCount = s.Count;

            if (s.Closed) Label = Loc.T("CLOSED");
            else if (s.IsLocked)
                Label = Loc.F(s.LockRemaining == 1 ? "LOCKED\n{0} {1} train" : "LOCKED\n{0} {1} trains", s.LockRemaining,
                    Loc.T(s.LockColor >= 0 ? ColorCodes.Names[s.LockColor] : "any"));
            else if (s.UsesLeft > 0)
                Label = (s.Express ? Loc.T("EXPRESS  ") : "") + Loc.F(s.UsesLeft == 1 ? "{0} use left" : "{0} uses left", s.UsesLeft);
            else if (s.Express)
                Label = s.ReservedColor >= 0 ? Loc.F("EXPRESS\n{0} only", Loc.T(ColorCodes.Names[s.ReservedColor])) : Loc.T("EXPRESS");
            else if (s.ReservedColor >= 0 && _scheduled) Label = Loc.F("{0} line train", Loc.T(ColorCodes.Names[s.ReservedColor]));
            else if (s.ReservedColor >= 0 && !_scheduled) Label = Loc.F("{0} only", Loc.T(ColorCodes.Names[s.ReservedColor]));
            else if (s.IsExtra) Label = Loc.T("EXTRA");
            else Label = "";
            ApplySlabColor();
        }

        public void SetHighlight(bool on, bool hint)
        {
            _highlight = on;
            _hint = hint;
            _glow.SetActive(on);
            if (on) Prims.SetColor(_glow, hint ? Palette.HintGlow : Palette.Highlight);
        }

        public void Pulse() { _pulse = 1f; }

        /// <summary>A freshly added platform falls into place from above and lands with a thump.</summary>
        public void DropIn()
        {
            if (GameSettings.ReducedMotion) return;
            _home = transform.localPosition;
            _drop = 1f;
            transform.localPosition = _home + Vector3.up * 8f;
        }

        private Material PlatformMaterial(string slot)
        {
            switch (slot)
            {
                case "Slab":
                    // Fine anti-slip dots from the platform texture on top of the slab colour.
                    return MaterialLibrary.GetTextured("tex_platform", _slabColor, new Vector4(1f / 1.2f, 1f / 1.2f, 0f, 0f), false)
                        ?? MaterialLibrary.Get(_slabColor);
                case "Skirt": return MaterialLibrary.Get(Palette.PlatformSkirt);
                case "Rim": return MaterialLibrary.Get(_rimColor, true);
                case "Top":
                    // Warm terrazzo inside the white kerb, so the walking surface reads apart from the slab.
                    Color top = Color.Lerp(_slabColor, new Color(0.97f, 0.85f, 0.66f), 0.75f);
                    return MaterialLibrary.GetTextured("tex_platform", top, new Vector4(1f / 1.2f, 1f / 1.2f, 0f, 0f), false)
                        ?? MaterialLibrary.Get(top);
                case "Slot": return MaterialLibrary.Get(Palette.Slot);
                case "SlotRing": return MaterialLibrary.Get(Color.white);
                case "Coping": return MaterialLibrary.Get(Color.Lerp(_slabColor, Color.white, 0.7f), true);
                case "Outline": return ModelKit.Outline;
                default: return null;
            }
        }

        private void ApplySlabColor()
        {
            ModelKit.Paint(_body, PlatformMaterial);
        }

        private void Update()
        {
            if (_pulse > 0f)
            {
                _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 2.5f);
                float s = 1f + Mathf.Sin(_pulse * Mathf.PI) * (GameSettings.ReducedMotion ? 0.02f : 0.08f);
                transform.localScale = new Vector3(s, 1f, s);
            }
            if (_drop > 0f)
            {
                _drop = Mathf.MoveTowards(_drop, 0f, Time.deltaTime / 0.34f);
                float k = 1f - _drop;
                transform.localPosition = _home + Vector3.up * (8f * (1f - k * k));
                if (_drop <= 0f)
                {
                    _pulse = 1f;
                    Haptics.Thump();
                    Fx.Dust(transform.position + new Vector3(-Width * 0.4f, 0.1f, -0.5f), 5, 0.5f);
                    Fx.Dust(transform.position + new Vector3(Width * 0.4f, 0.1f, -0.5f), 5, 0.5f);
                    Fx.Ring(transform.position + Vector3.up * 0.05f, Color.white, Width + 2.2f);
                }
            }
            if (_signSpin > 0f)
            {
                _signSpin = Mathf.MoveTowards(_signSpin, 0f, Time.deltaTime * 2.2f);
                float k = 1f - _signSpin;
                _signHead.localRotation = Quaternion.Euler(0f, 360f * Ease.OutBack(k), 0f);
                _signHead.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.22f);
            }
            for (int i = 0; i < _pipPop.Length; i++)
            {
                if (_pipPop[i] <= 0f) continue;
                _pipPop[i] = Mathf.MoveTowards(_pipPop[i], 0f, Time.deltaTime * 3.5f);
                _pips[i].transform.localScale = PipSize * (1f + Mathf.Sin(_pipPop[i] * Mathf.PI) * 0.7f);
            }
            if (_highlight && !GameSettings.ReducedMotion)
            {
                float g = 1f + Mathf.Sin(Time.time * (_hint ? 7f : 5f)) * 0.03f;
                _glow.transform.localScale = new Vector3((Width + 0.35f) * g, 0.06f, (Depth + 0.35f) * g);
            }
        }
    }

    /// <summary>
    /// A toy metro train: one coupled car per passenger, rounded driver cabs at both ends, sliding doors,
    /// window bands, bogies, roof equipment and a pantograph. Movement along the track is driven by the
    /// AnimationCoordinator; the train itself adds the toy physics: cars bunch up and rock when it brakes,
    /// stretch out when it pulls away, and the pantograph bobs on its spring.
    /// </summary>
    public sealed class TrainView : MonoBehaviour
    {
        public const string PoolKey = "train";
        private const int MaxCars = 4;
        private const float Pitch = 1.50f;
        private const float CarLength = 1.42f;
        private const float SideZ = -0.535f;

        private static readonly Color Glass = new Color(0.70f, 0.90f, 1f);
        private static readonly Color DarkGlass = new Color(0.13f, 0.16f, 0.23f);
        private static readonly Color Silver = new Color(0.90f, 0.92f, 0.96f);
        private static readonly Color Underframe = new Color(0.15f, 0.16f, 0.20f);

        private sealed class Car
        {
            public GameObject Root, Body, Coupler, Pantograph, PantographArm;
            public Transform DoorL, DoorR;
        }

        private readonly Car[] _cars = new Car[MaxCars];
        private GameObject _frontCab, _rearCab;
        private Color _line = Color.white;
        private Color _lamp = Color.white;
        private int _capacity = 3;
        // Springs: _bunch > 0 squeezes the cars together (braking), < 0 pulls them apart (setting off).
        private float _bunch, _bunchSpeed, _panto, _pantoSpeed, _rock, _rockClock;
        /// <summary>Standing at the platform with the motors running: the cars tremble slightly.</summary>
        public bool Humming;
        private const float PantographFoot = 1.33f, PantographX = -0.34f;

        public static GameObject CreateObject()
        {
            var go = new GameObject("Train");
            go.AddComponent<TrainView>().Build();
            return go;
        }

        /// <summary>Assembled from the pieces in Models/Train: car shells, door leaves, gangways, a pantograph, two cabs.</summary>
        private void Build()
        {
            for (int i = 0; i < MaxCars; i++)
            {
                var car = new Car();
                car.Root = new GameObject("Car" + i);
                Transform t = car.Root.transform;
                t.SetParent(transform, false);
                car.Body = ModelKit.Spawn("Train", "Car", t, "Body");
                car.DoorL = ModelKit.Spawn("Train", "DoorL", t).transform;
                car.DoorR = ModelKit.Spawn("Train", "DoorR", t).transform;
                car.Coupler = ModelKit.Spawn("Train", "Coupler", t, "Gangway");
                car.Coupler.transform.localPosition = new Vector3(Pitch * 0.5f, 0f, 0f);
                car.Pantograph = ModelKit.Spawn("Train", "Pantograph", t);
                car.Pantograph.transform.localPosition = new Vector3(PantographX, 0f, 0f);
                car.PantographArm = ModelKit.Spawn("Train", "PantographArm", t);
                car.PantographArm.transform.localPosition = new Vector3(PantographX, 0f, 0f);
                foreach (Transform door in new[] { car.DoorL, car.DoorR })
                    foreach (MeshRenderer r in door.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _cars[i] = car;
            }
            _frontCab = ModelKit.Spawn("Train", "Cab", transform, "FrontCab");
            _rearCab = ModelKit.Spawn("Train", "Cab", transform, "RearCab", true);
        }

        private Material TrainMaterial(string slot)
        {
            switch (slot)
            {
                case "Body": return MaterialLibrary.Get(_line, true);
                case "Stripe": return MaterialLibrary.Get(Color.Lerp(_line, Color.white, 0.55f));
                case "Glass": return MaterialLibrary.Get(Glass, true);
                case "DarkGlass": return MaterialLibrary.Get(DarkGlass, true);
                case "Silver": return MaterialLibrary.Get(Silver, true);
                case "Under": return MaterialLibrary.Get(Underframe);
                case "Wheel": return MaterialLibrary.Get(new Color(0.32f, 0.34f, 0.40f));
                case "Lamp": return MaterialLibrary.Get(_lamp);
                case "Sign": return MaterialLibrary.Get(new Color(1f, 0.72f, 0.25f));
                case "Outline": return ModelKit.Outline;
                default: return null;
            }
        }

        public void Configure(int color, int capacity)
        {
            _capacity = Mathf.Clamp(capacity, 1, MaxCars);
            _line = Palette.Line(color);
            int pantographCar = _capacity >= 3 ? 1 : 0;
            for (int i = 0; i < MaxCars; i++)
            {
                Car car = _cars[i];
                bool used = i < _capacity;
                car.Root.SetActive(used);
                if (!used) continue;
                car.Root.transform.localPosition = new Vector3(CarX(i), 0f, 0f);
                car.Coupler.SetActive(i < _capacity - 1);
                car.Pantograph.SetActive(i == pantographCar);
                car.PantographArm.SetActive(i == pantographCar);
                ModelKit.Paint(car.Root, TrainMaterial);
            }
            _frontCab.transform.localPosition = new Vector3(CarX(0) - CarLength * 0.5f, 0f, 0f);
            _rearCab.transform.localPosition = new Vector3(CarX(_capacity - 1) + CarLength * 0.5f, 0f, 0f);
            _lamp = new Color(1f, 0.97f, 0.75f);
            ModelKit.Paint(_frontCab, TrainMaterial);
            _lamp = new Color(1f, 0.30f, 0.28f);
            ModelKit.Paint(_rearCab, TrainMaterial);
            SetDoors(0f);
            _bunch = _bunchSpeed = _panto = _pantoSpeed = _rock = 0f;
            Humming = false;
            Pose();
        }

        /// <summary>The train has just stopped: the cars run up against each other, rock and settle.</summary>
        public void Brake()
        {
            if (GameSettings.ReducedMotion) return;
            _bunchSpeed += 13f;
            _pantoSpeed -= 9f;
            _rock = 1f;
            _rockClock = 0f;
        }

        /// <summary>The train sets off: the front pulls away first and the cars string out behind it.</summary>
        public void Launch()
        {
            if (GameSettings.ReducedMotion) return;
            _bunchSpeed -= 11f;
            _pantoSpeed += 7f;
            _rock = 0.6f;
            _rockClock = 0f;
        }

        /// <summary>Top of the pantograph in world space, where sparks fly off the wire.</summary>
        public Vector3 PantographTop
        {
            get { return transform.position + new Vector3(CarX(_capacity >= 3 ? 1 : 0) + PantographX, 1.62f, 0f); }
        }

        private void Update()
        {
            if (_bunch == 0f && _bunchSpeed == 0f && _panto == 0f && _pantoSpeed == 0f && _rock == 0f && !Humming) return;
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            _bunchSpeed += (-300f * _bunch - 15f * _bunchSpeed) * dt;
            _bunch += _bunchSpeed * dt;
            _pantoSpeed += (-420f * _panto - 11f * _pantoSpeed) * dt;
            _panto += _pantoSpeed * dt;
            _rock = Mathf.MoveTowards(_rock, 0f, dt * 1.5f);
            _rockClock += dt;
            if (_rock == 0f && Mathf.Abs(_bunch) < 0.002f && Mathf.Abs(_bunchSpeed) < 0.02f && Mathf.Abs(_panto) < 0.002f && Mathf.Abs(_pantoSpeed) < 0.02f)
                _bunch = _bunchSpeed = _panto = _pantoSpeed = 0f;
            Pose();
        }

        /// <summary>Lays the cars out for the current spring state.</summary>
        private void Pose()
        {
            float gap = 1f - _bunch * 0.07f;
            var squash = new Vector3(1f - _bunch * 0.08f, 1f + _bunch * 0.09f, 1f + _bunch * 0.03f);
            for (int i = 0; i < _capacity; i++)
            {
                Transform car = _cars[i].Root.transform;
                // Each car rocks on its wheels a beat after the one in front.
                float roll = Mathf.Sin(_rockClock * 13f - i * 0.9f) * _rock * _rock * 2.6f;
                float hum = Humming && !GameSettings.ReducedMotion ? Mathf.Sin(Time.time * 41f + i * 1.7f) * 0.0045f : 0f;
                car.localPosition = new Vector3(CarX(i) * gap, hum, 0f);
                car.localRotation = Quaternion.Euler(roll, 0f, 0f);
                car.localScale = squash;
                float lift = Mathf.Clamp(1f + _panto * 0.45f, 0.55f, 1.45f);
                Transform arm = _cars[i].PantographArm.transform;
                arm.localScale = new Vector3(1f, lift, 1f);
                arm.localPosition = new Vector3(PantographX, PantographFoot * (1f - lift), 0f);
            }
            float front = CarX(0) * gap - CarLength * 0.5f * squash.x, rear = CarX(_capacity - 1) * gap + CarLength * 0.5f * squash.x;
            _frontCab.transform.localPosition = new Vector3(front, 0f, 0f);
            _rearCab.transform.localPosition = new Vector3(rear, 0f, 0f);
            _frontCab.transform.localScale = squash;
            _rearCab.transform.localScale = squash;
        }

        public float HalfLength { get { return _capacity * Pitch * 0.5f + 0.5f; } }

        private float CarX(int index)
        {
            return (index - (_capacity - 1) * 0.5f) * Pitch;
        }

        /// <summary>Where the passenger in the given slot steps aboard: the door of their own car.</summary>
        /// <summary>The car a rider boards, for attaching them to it once inside.</summary>
        public Transform CarRoot(int slot)
        {
            return _cars[Mathf.Clamp(slot, 0, _capacity - 1)].Root.transform;
        }

        /// <summary>
        /// Where a rider stands (in car space) to lean out of a window: just inside the camera-side wall, feet
        /// below the sill. Each rider has a car of their own and takes its first window.
        /// </summary>
        public static Vector3 WindowSeat(int slot)
        {
            return new Vector3(-0.47f, 0.46f, SideZ + 0.12f);
        }

        public const float RiderScale = 0.60f;

        public Vector3 DoorPosition(int slot)
        {
            return transform.position + new Vector3(CarX(Mathf.Clamp(slot, 0, _capacity - 1)), 0.69f, -0.42f);
        }

        /// <summary>0 = closed, 1 = fully open.</summary>
        public void SetDoors(float open)
        {
            for (int i = 0; i < _capacity; i++)
            {
                // Doors open one car after another for a little ripple along the train.
                float local = Mathf.Clamp01(open * (1f + 0.25f * (_capacity - 1)) - 0.25f * i);
                // Plug doors: the leaves pop out of the body first, then slide apart.
                float plug = Mathf.Sin(Mathf.Clamp01(local * 3f) * Mathf.PI * 0.5f) * 0.035f;
                float offset = 0.108f + Mathf.Clamp01((local - 0.15f) / 0.85f) * 0.20f;
                _cars[i].DoorL.localPosition = new Vector3(-offset, 0.72f, SideZ - 0.012f - plug);
                _cars[i].DoorR.localPosition = new Vector3(offset, 0.72f, SideZ - 0.012f - plug);
            }
        }
    }
}
