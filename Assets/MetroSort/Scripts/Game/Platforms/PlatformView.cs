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

        private GameObject _slab, _capL, _capR, _glow, _sign, _barrier;
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
            // A slightly larger, darker pill underneath gives the platform a stepped, moulded edge.
            float skirt = Depth + 0.16f;
            Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.05f, 0f), new Vector3(body, 0.10f, skirt), Palette.PlatformSkirt, "SkirtMid");
            Prims.Make(PrimitiveType.Cylinder, t, new Vector3(-body * 0.5f, 0.05f, 0f), new Vector3(skirt, 0.05f, skirt), Palette.PlatformSkirt, "SkirtL");
            Prims.Make(PrimitiveType.Cylinder, t, new Vector3(body * 0.5f, 0.05f, 0f), new Vector3(skirt, 0.05f, skirt), Palette.PlatformSkirt, "SkirtR");
            _slab = Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.14f, 0f), new Vector3(body, 0.28f, Depth), Palette.Platform, "Slab");
            // Cylinder end caps turn the slab into a soft pill shape.
            _capL = Prims.Make(PrimitiveType.Cylinder, t, new Vector3(-body * 0.5f, 0.14f, 0f), new Vector3(Depth, 0.14f, Depth), Palette.Platform, "CapL");
            _capR = Prims.Make(PrimitiveType.Cylinder, t, new Vector3(body * 0.5f, 0.14f, 0f), new Vector3(Depth, 0.14f, Depth), Palette.Platform, "CapR");
            InkOutline.Add(_slab, 0.03f, false);
            InkOutline.Add(_capL, 0.03f, false);
            InkOutline.Add(_capR, 0.03f, false);
            // Safety line on the track side; orange on express platforms.
            GameObject line = Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.285f, Depth * 0.5f - 0.12f), new Vector3(body, 0.02f, 0.10f), state.Express ? Palette.Lines[5] : Palette.Gold, "SafetyLine");
            Prims.NoShadow(line);

            for (int i = 0; i < Capacity; i++)
            {
                Vector3 local = SlotLocal(i);
                GameObject disc = Prims.Make(PrimitiveType.Cylinder, t, new Vector3(local.x, 0.285f, local.z), new Vector3(0.62f, 0.01f, 0.62f), Palette.Slot, "Slot" + i);
                Prims.NoShadow(disc);
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

            if (s.Closed) Label = "CLOSED";
            else if (s.IsLocked)
                Label = "LOCKED\n" + s.LockRemaining + " " + (s.LockColor >= 0 ? ColorCodes.Names[s.LockColor] : "any") + (s.LockRemaining == 1 ? " train" : " trains");
            else if (s.UsesLeft > 0) Label = (s.Express ? "EXPRESS  " : "") + s.UsesLeft + (s.UsesLeft == 1 ? " use left" : " uses left");
            else if (s.Express) Label = s.ReservedColor >= 0 ? "EXPRESS\n" + ColorCodes.Names[s.ReservedColor] + " only" : "EXPRESS";
            else if (s.ReservedColor >= 0) Label = ColorCodes.Names[s.ReservedColor] + " only";
            else if (s.IsExtra) Label = "EXTRA";
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

        private void ApplySlabColor()
        {
            Prims.SetColor(_slab, _slabColor);
            Prims.SetColor(_capL, _slabColor);
            Prims.SetColor(_capR, _slabColor);
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
            public GameObject Root, Body, RoofStripe, Coupler, Pantograph;
            public Transform DoorL, DoorR;
        }

        private readonly Car[] _cars = new Car[MaxCars];
        private Transform _frontCab, _rearCab;
        private GameObject _frontNose, _rearNose;
        private int _capacity = 3;

        public static GameObject CreateObject()
        {
            var go = new GameObject("Train");
            go.AddComponent<TrainView>().Build();
            return go;
        }

        private void Build()
        {
            for (int i = 0; i < MaxCars; i++) _cars[i] = BuildCar(i);
            _frontCab = BuildCab(-1f, out _frontNose);
            _rearCab = BuildCab(1f, out _rearNose);
        }

        private Car BuildCar(int index)
        {
            var car = new Car();
            car.Root = new GameObject("Car" + index);
            Transform t = car.Root.transform;
            t.SetParent(transform, false);

            car.Body = Prims.MakeRounded(t, new Vector3(0f, 0.78f, 0f), new Vector3(CarLength, 0.92f, 1.06f), 0.17f, Color.white, "Body", true);
            InkOutline.Add(car.Body, 0.03f);
            Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.30f, 0f), new Vector3(CarLength - 0.08f, 0.14f, 0.98f), Underframe, "Underframe");

            // Platform-facing side (-Z): dark window band, two windows, a white waist line.
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.94f, SideZ), new Vector3(CarLength - 0.14f, 0.30f, 0.02f), DarkGlass, "WindowBand"));
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject window = Prims.Make(PrimitiveType.Cube, t, new Vector3(side * 0.47f, 0.94f, SideZ - 0.01f), new Vector3(0.30f, 0.22f, 0.02f), Glass, "Window");
                Prims.SetGlossy(window, Glass);
                Prims.NoShadow(window);
                Prims.NoShadow(Prims.Make(PrimitiveType.Cube, t, new Vector3(side * 0.47f + 0.07f, 0.99f, SideZ - 0.02f), new Vector3(0.07f, 0.07f, 0.01f), Color.white, "Glint"));
            }
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.73f, SideZ - 0.005f), new Vector3(CarLength - 0.10f, 0.045f, 0.02f), Color.white, "WaistLine"));

            // Double sliding door in the middle of the car.
            car.DoorL = BuildDoorLeaf(t, "DoorL");
            car.DoorR = BuildDoorLeaf(t, "DoorR");
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.72f, SideZ + 0.004f), new Vector3(0.46f, 0.66f, 0.02f), new Color(0.05f, 0.06f, 0.09f), "Doorway"));

            // Roof: silver panel, line-coloured stripe, air-conditioning unit.
            Prims.MakeRounded(t, new Vector3(0f, 1.25f, 0f), new Vector3(CarLength - 0.12f, 0.08f, 0.80f), 0.035f, Silver, "Roof", true);
            car.RoofStripe = Prims.Make(PrimitiveType.Cube, t, new Vector3(0f, 1.295f, 0f), new Vector3(CarLength - 0.16f, 0.012f, 0.20f), Color.white, "RoofStripe");
            Prims.NoShadow(car.RoofStripe);
            Prims.MakeRounded(t, new Vector3(0.32f, 1.34f, 0f), new Vector3(0.50f, 0.11f, 0.46f), 0.045f, new Color(0.74f, 0.77f, 0.83f), "AirCon");
            Prims.NoShadow(Prims.Make(PrimitiveType.Cylinder, t, new Vector3(0.32f, 1.40f, 0f), new Vector3(0.24f, 0.006f, 0.24f), Underframe, "Fan"));

            // Pantograph (shown on one car only).
            car.Pantograph = new GameObject("Pantograph");
            Transform pt = car.Pantograph.transform;
            pt.SetParent(t, false);
            pt.localPosition = new Vector3(-0.34f, 1.30f, 0f);
            Prims.Make(PrimitiveType.Cube, pt, new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.04f, 0.30f), Underframe, "Base");
            GameObject armA = Prims.Make(PrimitiveType.Cube, pt, new Vector3(-0.07f, 0.17f, 0f), new Vector3(0.035f, 0.32f, 0.035f), Underframe, "ArmA");
            armA.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            GameObject armB = Prims.Make(PrimitiveType.Cube, pt, new Vector3(0.07f, 0.17f, 0f), new Vector3(0.035f, 0.32f, 0.035f), Underframe, "ArmB");
            armB.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            Prims.Make(PrimitiveType.Cube, pt, new Vector3(0f, 0.32f, 0f), new Vector3(0.05f, 0.03f, 0.62f), new Color(0.55f, 0.58f, 0.64f), "Collector");

            // Two bogies with wheels on both sides.
            for (int end = -1; end <= 1; end += 2)
            {
                Prims.Make(PrimitiveType.Cube, t, new Vector3(end * 0.40f, 0.17f, 0f), new Vector3(0.46f, 0.14f, 0.86f), Underframe, "Bogie");
                for (int z = -1; z <= 1; z += 2)
                {
                    GameObject wheel = Prims.Make(PrimitiveType.Cylinder, t, new Vector3(end * 0.40f, 0.17f, z * 0.45f), new Vector3(0.32f, 0.04f, 0.32f), new Color(0.32f, 0.34f, 0.40f), "Wheel");
                    wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }

            car.Coupler = Prims.Make(PrimitiveType.Cube, t, new Vector3(Pitch * 0.5f, 0.52f, 0f), new Vector3(0.16f, 0.50f, 0.70f), Underframe, "Gangway");
            return car;
        }

        private static Transform BuildDoorLeaf(Transform car, string name)
        {
            GameObject leaf = Prims.Make(PrimitiveType.Cube, car, new Vector3(0f, 0.72f, SideZ - 0.012f), new Vector3(0.21f, 0.64f, 0.025f), Silver, name);
            Prims.NoShadow(leaf);
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, leaf.transform, new Vector3(0f, 0.20f, -0.6f), new Vector3(0.62f, 0.40f, 0.4f), Glass, "DoorWindow"));
            return leaf.transform;
        }

        /// <summary>Rounded driver cab. direction -1 is the leading end, +1 the trailing end.</summary>
        private Transform BuildCab(float direction, out GameObject nose)
        {
            var cab = new GameObject(direction < 0f ? "FrontCab" : "RearCab").transform;
            cab.SetParent(transform, false);
            nose = Prims.Make(PrimitiveType.Sphere, cab, new Vector3(direction * 0.10f, 0.76f, 0f), new Vector3(1.00f, 0.90f, 1.05f), Color.white, "Nose");
            InkOutline.Add(nose, 0.03f);
            Prims.SetGlossy(Prims.Make(PrimitiveType.Sphere, cab, new Vector3(direction * 0.27f, 0.93f, 0f), new Vector3(0.66f, 0.50f, 0.86f), DarkGlass, "Windscreen"), DarkGlass);
            Prims.NoShadow(Prims.Make(PrimitiveType.Cube, cab, new Vector3(direction * 0.50f, 1.09f, 0f), new Vector3(0.05f, 0.07f, 0.40f), new Color(1f, 0.72f, 0.25f), "DestinationSign"));
            Prims.Make(PrimitiveType.Sphere, cab, new Vector3(direction * 0.30f, 0.40f, 0f), new Vector3(0.62f, 0.30f, 0.90f), Underframe, "Bumper");
            Color lamp = direction < 0f ? new Color(1f, 0.97f, 0.75f) : new Color(1f, 0.30f, 0.28f);
            for (int z = -1; z <= 1; z += 2)
                Prims.NoShadow(Prims.Make(PrimitiveType.Sphere, cab, new Vector3(direction * 0.53f, 0.60f, z * 0.27f), new Vector3(0.13f, 0.13f, 0.13f), lamp, "Lamp"));
            return cab;
        }

        public void Configure(int color, int capacity)
        {
            _capacity = Mathf.Clamp(capacity, 1, MaxCars);
            Color line = Palette.Line(color);
            Color pale = Color.Lerp(line, Color.white, 0.55f);
            int pantographCar = _capacity >= 3 ? 1 : 0;
            for (int i = 0; i < MaxCars; i++)
            {
                Car car = _cars[i];
                bool used = i < _capacity;
                car.Root.SetActive(used);
                if (!used) continue;
                car.Root.transform.localPosition = new Vector3(CarX(i), 0f, 0f);
                Prims.SetGlossy(car.Body, line);
                Prims.SetColor(car.RoofStripe, pale);
                car.Coupler.SetActive(i < _capacity - 1);
                car.Pantograph.SetActive(i == pantographCar);
            }
            _frontCab.localPosition = new Vector3(CarX(0) - CarLength * 0.5f, 0f, 0f);
            _rearCab.localPosition = new Vector3(CarX(_capacity - 1) + CarLength * 0.5f, 0f, 0f);
            Prims.SetGlossy(_frontNose, line);
            Prims.SetGlossy(_rearNose, line);
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
