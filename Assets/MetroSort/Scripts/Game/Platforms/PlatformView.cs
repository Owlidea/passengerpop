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
        private GameObject[] _pips;
        private float _pulse;
        private bool _highlight, _hint;
        private Color _slabColor = Palette.Platform;

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
            ModelKit.Spawn("Platform", "Cap", _body.transform, "CapL").transform.localPosition = new Vector3(-body * 0.5f, 0f, 0f);
            ModelKit.Spawn("Platform", "Cap", _body.transform, "CapR").transform.localPosition = new Vector3(body * 0.5f, 0f, 0f);
            // Safety line on the track side; orange on express platforms.
            GameObject line = Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.285f, Depth * 0.5f - 0.12f), new Vector3(body, 0.02f, 0.10f), state.Express ? Palette.Lines[5] : Palette.Gold, "SafetyLine");
            Prims.NoShadow(line);

            for (int i = 0; i < Capacity; i++)
            {
                Vector3 local = SlotLocal(i);
                GameObject spot = ModelKit.Spawn("Platform", "Slot", t, "Slot" + i);
                spot.transform.localPosition = new Vector3(local.x, 0.281f, local.z);
                ModelKit.Paint(spot, PlatformMaterial);
                foreach (MeshRenderer r in spot.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Line sign on a pole at the left end.
            Prims.Make(PrimitiveType.Cylinder, t, new Vector3(-Width * 0.5f + 0.15f, 0.85f, 0.45f), new Vector3(0.07f, 0.6f, 0.07f), Palette.Dark, "Pole");
            Prims.MakeRounded(t, new Vector3(-Width * 0.5f + 0.15f, 1.55f, 0.47f), new Vector3(0.74f, 0.54f, 0.08f), 0.035f, Color.white, "SignFrame");
            _sign = Prims.MakeRounded(t, new Vector3(-Width * 0.5f + 0.15f, 1.55f, 0.42f), new Vector3(0.62f, 0.42f, 0.10f), 0.045f, Palette.Neutral, "Sign", true);

            _pips = new GameObject[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                float x = (i - (Capacity - 1) * 0.5f) * 0.30f;
                _pips[i] = Prims.Make(PrimitiveType.Sphere, t, new Vector3(x, 0.20f, -Depth * 0.5f - 0.02f), new Vector3(0.20f, 0.20f, 0.12f), Palette.Neutral, "Pip" + i);
                Prims.NoShadow(_pips[i]);
            }

            _barrier = new GameObject("Barrier");
            _barrier.transform.SetParent(t, false);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * (Width / 4.2f);
                Prims.Make(PrimitiveType.Cube, _barrier.transform, new Vector3(x, 0.60f, -0.2f), new Vector3(Width / 4.6f, 0.16f, 0.08f), i % 2 == 0 ? Palette.UiRed : Color.white);
            }
            Prims.Make(PrimitiveType.Cylinder, _barrier.transform, new Vector3(-Width * 0.42f, 0.45f, -0.2f), new Vector3(0.08f, 0.2f, 0.08f), Palette.Dark);
            Prims.Make(PrimitiveType.Cylinder, _barrier.transform, new Vector3(Width * 0.42f, 0.45f, -0.2f), new Vector3(0.08f, 0.2f, 0.08f), Palette.Dark);

            Refresh(state);
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
        public void Refresh(PlatformState s)
        {
            bool blocked = s.IsLocked || s.Closed;
            _slabColor = blocked ? Palette.PlatformLocked : Palette.Platform;
            _barrier.SetActive(blocked);
            Color line = s.Count > 0 ? Palette.Line(s.Color) : Palette.Neutral;
            Prims.SetGlossy(_sign, s.ReservedColor >= 0 ? Palette.Line(s.ReservedColor) : line);
            for (int i = 0; i < _pips.Length; i++) Prims.SetColor(_pips[i], i < s.Count ? line : Palette.Neutral);

            if (s.Closed) Label = Loc.T("CLOSED");
            else if (s.IsLocked)
                Label = Loc.F(s.LockRemaining == 1 ? "LOCKED\n{0} {1} train" : "LOCKED\n{0} {1} trains", s.LockRemaining,
                    Loc.T(s.LockColor >= 0 ? ColorCodes.Names[s.LockColor] : "any"));
            else if (s.UsesLeft > 0)
                Label = (s.Express ? Loc.T("EXPRESS  ") : "") + Loc.F(s.UsesLeft == 1 ? "{0} use left" : "{0} uses left", s.UsesLeft);
            else if (s.Express)
                Label = s.ReservedColor >= 0 ? Loc.F("EXPRESS\n{0} only", Loc.T(ColorCodes.Names[s.ReservedColor])) : Loc.T("EXPRESS");
            else if (s.ReservedColor >= 0) Label = Loc.F("{0} only", Loc.T(ColorCodes.Names[s.ReservedColor]));
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

        private Material PlatformMaterial(string slot)
        {
            switch (slot)
            {
                case "Slab": return MaterialLibrary.Get(_slabColor);
                case "Skirt": return MaterialLibrary.Get(Palette.PlatformSkirt);
                case "Slot": return MaterialLibrary.Get(Palette.Slot);
                case "SlotRing": return MaterialLibrary.Get(Color.white);
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
            if (_highlight && !GameSettings.ReducedMotion)
            {
                float g = 1f + Mathf.Sin(Time.time * (_hint ? 7f : 5f)) * 0.03f;
                _glow.transform.localScale = new Vector3((Width + 0.35f) * g, 0.06f, (Depth + 0.35f) * g);
            }
        }
    }

    /// <summary>
    /// A toy metro train: one coupled car per passenger, rounded driver cabs at both ends, sliding doors,
    /// window bands, bogies, roof equipment and a pantograph. Movement is driven by the AnimationCoordinator.
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
            public GameObject Root, Body, Coupler, Pantograph;
            public Transform DoorL, DoorR;
        }

        private readonly Car[] _cars = new Car[MaxCars];
        private GameObject _frontCab, _rearCab;
        private Color _line = Color.white;
        private Color _lamp = Color.white;
        private int _capacity = 3;

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
                car.Pantograph.transform.localPosition = new Vector3(-0.34f, 0f, 0f);
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
                ModelKit.Paint(car.Root, TrainMaterial);
            }
            _frontCab.transform.localPosition = new Vector3(CarX(0) - CarLength * 0.5f, 0f, 0f);
            _rearCab.transform.localPosition = new Vector3(CarX(_capacity - 1) + CarLength * 0.5f, 0f, 0f);
            _lamp = new Color(1f, 0.97f, 0.75f);
            ModelKit.Paint(_frontCab, TrainMaterial);
            _lamp = new Color(1f, 0.30f, 0.28f);
            ModelKit.Paint(_rearCab, TrainMaterial);
            SetDoors(0f);
        }

        public float HalfLength { get { return _capacity * Pitch * 0.5f + 0.5f; } }

        private float CarX(int index)
        {
            return (index - (_capacity - 1) * 0.5f) * Pitch;
        }

        /// <summary>Where the passenger in the given slot steps aboard: the door of their own car.</summary>
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
                float offset = 0.108f + local * 0.20f;
                _cars[i].DoorL.localPosition = new Vector3(-offset, 0.72f, SideZ - 0.012f);
                _cars[i].DoorR.localPosition = new Vector3(offset, 0.72f, SideZ - 0.012f);
            }
        }
    }
}
