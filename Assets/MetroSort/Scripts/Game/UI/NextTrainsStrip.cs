using MetroSort.Puzzle;
using UnityEngine;
using UnityEngine.UI;

namespace MetroSort.Game
{
    /// <summary>
    /// The timetable under the HUD: a navy board with a gold rim where the coming trains wait on a little rail,
    /// each drawn as its line's toy train (Resources/Art/train_icon_N, Tools/art/trainicons.py). The next one is
    /// larger with a glow; extra trains collapse into a "+N" badge. When a train leaves, it pulls away to the
    /// left and the rest roll up one place; the newcomer at the end pops in.
    /// </summary>
    public sealed class NextTrainsStrip : MonoBehaviour
    {
        public const int Slots = 5;
        private const float FirstX = 236f, FirstSize = 112f, Gap = 98f, Size = 90f, RailY = -30f;

        private readonly RectTransform[] _slots = new RectTransform[Slots];
        private readonly Image[] _icons = new Image[Slots];
        private RectTransform _glow, _row;
        private GameObject _more, _last;
        private readonly GameObject[] _seatBadges = new GameObject[Slots];
        private readonly Text[] _seatTexts = new Text[Slots];
        private Text _moreText;
        private int[] _schedule;
        private int _shownNext;
        private float _slide, _slideDir, _pop, _time;

        private static float SlotX(int i)
        {
            if (i <= 0) return FirstX + i * Gap;
            return FirstX + FirstSize * 0.5f + 12f + Size * 0.5f + (i - 1) * Gap;
        }

        public static NextTrainsStrip Make(RectTransform parent)
        {
            RectTransform root = UiKit.Place(UiKit.MakeRect("NextTrains", parent), 0.5f, 1f, 0f, -204f, 900f, 104f);
            var strip = root.gameObject.AddComponent<NextTrainsStrip>();
            Image rim = UiKit.MakeImage(root, "Rim", UiPremium.Pill, new Color(1f, 0.74f, 0.20f));
            UiKit.Stretch(rim.rectTransform);
            Image board = UiKit.MakeImage(root, "Board", UiPremium.Pill, new Color(0.10f, 0.17f, 0.38f, 0.97f));
            UiKit.Stretch(board.rectTransform, 6f, 6f, 6f, 6f);
            var shadow = rim.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.02f, 0.10f, 0.4f);
            shadow.effectDistance = new Vector2(0f, -6f);

            Text label = UiPremium.Heading(root, Loc.T("Next trains"), 30, new Color(1f, 0.84f, 0.36f), new Color(0.04f, 0.07f, 0.20f, 0.95f));
            UiKit.Place(label.rectTransform, 0f, 0.5f, 30f, 2f, 140f, 84f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.alignment = TextAnchor.MiddleLeft;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 30;

            // The rail the trains stand on: two thin steel lines on wooden sleepers over a dark bed.
            Image bed = UiKit.MakeImage(root, "RailBed", UiKit.Rounded, new Color(0.04f, 0.08f, 0.20f, 0.85f));
            UiKit.Place(bed.rectTransform, 0f, 0.5f, 180f, RailY, 560f, 14f);
            bed.rectTransform.pivot = new Vector2(0f, 0.5f);
            for (float x = 192f; x < 736f; x += 24f)
            {
                Image sleeper = UiKit.MakeImage(root, "Sleeper", UiKit.Rounded, new Color(0.55f, 0.36f, 0.22f, 0.95f));
                sleeper.pixelsPerUnitMultiplier = 8f;
                UiKit.Place(sleeper.rectTransform, 0f, 0.5f, x, RailY, 7f, 18f).pivot = new Vector2(0.5f, 0.5f);
            }
            for (int r = 0; r < 2; r++)
            {
                Image rail = UiKit.MakeImage(root, "Rail", UiKit.Rounded, new Color(0.70f, 0.76f, 0.90f, 0.9f));
                UiKit.Place(rail.rectTransform, 0f, 0.5f, 180f, RailY + (r == 0 ? 3.5f : -3.5f), 560f, 3f);
                rail.rectTransform.pivot = new Vector2(0f, 0.5f);
            }

            // The next train waits in a gold-framed bay with a warm light in it; it is bigger and pokes out of the top.
            Image bay = UiKit.MakeImage(root, "NextBay", UiKit.Rounded, new Color(1f, 0.78f, 0.28f));
            bay.pixelsPerUnitMultiplier = 1.6f;
            strip._glow = UiKit.Place(bay.rectTransform, 0f, 0.5f, FirstX, 0f, 128f, 88f);
            strip._glow.pivot = new Vector2(0.5f, 0.5f);
            Image bayInside = UiKit.MakeImage(bay.transform, "Inside", UiKit.Rounded, new Color(0.20f, 0.30f, 0.62f));
            bayInside.pixelsPerUnitMultiplier = 2f;
            UiKit.Stretch(bayInside.rectTransform, 5f, 5f, 5f, 5f);
            Image bayLight = UiKit.MakeImage(bayInside.transform, "Light", UiKit.Circle, new Color(1f, 0.86f, 0.42f, 0.35f));
            bayLight.type = Image.Type.Simple;
            UiKit.Place(bayLight.rectTransform, 0.5f, 0.5f, 0f, 0f, 104f, 74f).pivot = new Vector2(0.5f, 0.5f);

            strip._row = UiKit.Stretch(UiKit.MakeRect("Trains", root));
            for (int i = 0; i < Slots; i++)
            {
                float size = i == 0 ? FirstSize : Size;
                Image icon = UiKit.MakeImage(strip._row, "Train" + i, null, Color.white);
                icon.preserveAspect = true;
                RectTransform rt = UiKit.Place(icon.rectTransform, 0f, 0.5f, SlotX(i), 6f, size, size);
                rt.pivot = new Vector2(0.5f, 0.5f);
                strip._icons[i] = icon;
                strip._slots[i] = rt;
                // Seat count of the train, shown only on levels whose trains differ in length.
                Image badge = UiKit.MakeImage(rt, "Seats", UiPremium.Round, new Color(1f, 0.80f, 0.24f));
                UiKit.Place(badge.rectTransform, 1f, 0f, -2f, 4f, 34f, 34f).pivot = new Vector2(1f, 0f);
                Text seats = UiPremium.Heading(badge.transform, "", 24, Color.white, new Color(0.45f, 0.22f, 0.0f, 0.95f));
                UiKit.Stretch(seats.rectTransform, 0f, 2f, 0f, 0f);
                seats.alignment = TextAnchor.MiddleCenter;
                strip._seatBadges[i] = badge.gameObject;
                strip._seatTexts[i] = seats;
            }

            Image more = UiKit.MakeImage(root, "More", UiPremium.Round, UiPremium.Gold);
            UiKit.Place(more.rectTransform, 0f, 0.5f, SlotX(Slots) - 8f, 2f, 74f, 74f).pivot = new Vector2(0.5f, 0.5f);
            strip._more = more.gameObject;
            strip._moreText = UiPremium.Heading(more.transform, "", 32, Color.white, new Color(0.50f, 0.26f, 0.0f, 0.95f));
            UiKit.Stretch(strip._moreText.rectTransform, 0f, 6f, 0f, 0f);
            strip._moreText.resizeTextForBestFit = true;
            strip._moreText.resizeTextMinSize = 16;
            strip._moreText.resizeTextMaxSize = 32;

            Image last = UiKit.MakeImage(root, "LastTrains", UiPremium.Tag, UiPremium.Red);
            UiKit.Place(last.rectTransform, 1f, 0.5f, -26f, 2f, 330f, 64f).pivot = new Vector2(1f, 0.5f);
            Text lastText = UiPremium.Heading(last.transform, Loc.T("Last trains"), 28, Color.white, new Color(0.45f, 0.04f, 0.04f, 0.95f));
            UiKit.Stretch(lastText.rectTransform, 24f, 6f, 24f, 4f);
            lastText.resizeTextForBestFit = true;
            lastText.resizeTextMinSize = 16;
            lastText.resizeTextMaxSize = 28;
            strip._last = last.gameObject;

            root.gameObject.SetActive(false);
            return strip;
        }

        /// <summary>Shows the timetable of the board, animating any train that left since the last call.</summary>
        public void Show(BoardState state)
        {
            bool on = state != null && state.HasSchedule;
            gameObject.SetActive(on);
            if (!on) return;
            if (state.Schedule != _schedule)
            {
                // A new level (or a restart): no animation.
                _schedule = state.Schedule;
                _shownNext = state.NextTrain;
                _slide = 0f;
            }
            else if (state.NextTrain != _shownNext)
            {
                if (state.NextTrain > _shownNext) Depart();
                _slideDir = Mathf.Sign(state.NextTrain - _shownNext);
                _slide = GameSettings.ReducedMotion ? 0f : 1f;
                _pop = _slide;
                _shownNext = state.NextTrain;
            }

            int left = state.UpcomingTrains;
            bool mixed = false;
            for (int i = 0; i < state.ScheduleSizes.Length; i++)
                if (state.ScheduleSizes[i] != state.ScheduleSizes[0]) mixed = true;
            for (int i = 0; i < Slots; i++)
            {
                bool shown = i < left;
                _icons[i].gameObject.SetActive(shown);
                if (!shown) continue;
                _seatBadges[i].SetActive(mixed);
                if (mixed) _seatTexts[i].text = state.ScheduleSizes[state.NextTrain + i].ToString();
                int color = state.Schedule[state.NextTrain + i];
                Sprite art = UiKit.Art("train_icon_" + color);
                _icons[i].sprite = art != null ? art : UiKit.Rounded;
                _icons[i].color = art != null ? Color.white : Palette.Line(color);
            }
            _glow.gameObject.SetActive(left > 0);
            _more.SetActive(left > Slots);
            if (left > Slots) _moreText.text = "+" + (left - Slots);
            _last.SetActive(left == 0);
            Place();
        }

        /// <summary>The train that just left pulls away to the left and fades.</summary>
        private void Depart()
        {
            if (GameSettings.ReducedMotion || !_icons[0].gameObject.activeSelf || _icons[0].sprite == null) return;
            Image ghost = UiKit.MakeImage(transform, "Leaving", _icons[0].sprite, _icons[0].color);
            ghost.preserveAspect = true;
            RectTransform rt = UiKit.Place(ghost.rectTransform, 0f, 0.5f, SlotX(0), 6f, FirstSize, FirstSize);
            rt.pivot = new Vector2(0.5f, 0.5f);
            ghost.gameObject.AddComponent<LeavingTrain>();
        }

        private void Place()
        {
            float e = _slide * _slide * (3f - 2f * _slide);  // eased: 1 at the start of a slide, 0 at rest
            for (int i = 0; i < Slots; i++)
            {
                // While sliding, each train starts where its neighbour stood and rolls into its own place.
                float from = SlotX(i + (int)_slideDir), to = SlotX(i);
                float x = Mathf.Lerp(to, from, e);
                float bob = i == 0 ? Mathf.Sin(_time * 3.2f) * 3f : 0f;
                _slots[i].anchoredPosition = new Vector2(x, 6f + bob);
                float size = i == 0 ? Mathf.Lerp(FirstSize, Size, e) : Size;
                float pop = i == Slots - 1 || (i == 0 && _slideDir < 0) ? 1f - 0.6f * _pop * _pop : 1f;
                _slots[i].sizeDelta = new Vector2(size, size) * pop;
            }
            // The bay breathes very slightly with the waiting train.
            float pulse = 1f + 0.025f * Mathf.Sin(_time * 3.2f);
            _glow.localScale = Vector3.one * pulse;
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            if (_slide > 0f) _slide = Mathf.Max(0f, _slide - Time.unscaledDeltaTime / 0.45f);
            if (_pop > 0f) _pop = Mathf.Max(0f, _pop - Time.unscaledDeltaTime / 0.35f);
            if (!GameSettings.ReducedMotion || _slide > 0f) Place();
        }
    }

    /// <summary>The departing train's icon: rolls off to the left, rising a little, and fades out.</summary>
    public sealed class LeavingTrain : MonoBehaviour
    {
        private float _t;
        private Image _image;
        private RectTransform _rt;
        private Vector2 _start;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rt = (RectTransform)transform;
        }

        private void Start() { _start = _rt.anchoredPosition; }

        private void Update()
        {
            _t += Time.unscaledDeltaTime / 0.5f;
            if (_t >= 1f) { Destroy(gameObject); return; }
            float e = _t * _t;
            _rt.anchoredPosition = _start + new Vector2(-150f * e, 26f * Mathf.Sin(_t * Mathf.PI));
            Color c = _image.color;
            c.a = 1f - _t;
            _image.color = c;
        }
    }
}
