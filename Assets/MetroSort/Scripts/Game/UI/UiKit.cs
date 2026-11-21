using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetroSort.Game
{
    /// <summary>Code-only UI building blocks: procedural sprites, text, buttons, sliders and modal cards.</summary>
    public static class UiKit
    {
        private static Font _font;
        private static bool _ownFont;
        private static Sprite _rounded, _circle, _star, _button, _card;
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> Icons = new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    // Baloo 2 ExtraBold (SIL Open Font License, see Resources/Fonts/Baloo2-OFL.txt), Latin subset.
                    _font = Resources.Load<Font>("Fonts/Baloo2-ExtraBold");
                    _ownFont = _font != null;
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        public static Sprite Rounded { get { return _rounded != null ? _rounded : (_rounded = MakeRounded(64, 24)); } }
        public static Sprite Circle { get { return _circle != null ? _circle : (_circle = MakeRounded(64, 32)); } }
        public static Sprite Star { get { return _star != null ? _star : (_star = MakeStar(96)); } }

        /// <summary>Chunky button skin, tinted by the Image colour: dark rim, raised lip, soft gradient and a top highlight.</summary>
        public static Sprite ButtonSprite { get { return _button != null ? _button : (_button = MakeShaded(96, 28f, true)); } }
        /// <summary>White card skin for panels and pills, with a cool grey rim and a thin lip.</summary>
        public static Sprite Card { get { return _card != null ? _card : (_card = MakeShaded(96, 30f, false)); } }

        /// <summary>
        /// An icon by name. Finished artwork in Resources/Art (Tools/art/media_import.py) wins over the
        /// Blender-rendered icon of the same name in Resources/UI (Tools/art/icons.py). Null if neither exists.
        /// </summary>
        public static Sprite Icon(string name)
        {
            Sprite sprite = Art(name);
            if (sprite != null) return sprite;
            if (Icons.TryGetValue("UI/" + name, out sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>("UI/" + name);
            if (sprite == null) Debug.LogWarning("Metro Sort: icon missing at Resources/UI/" + name);
            Icons["UI/" + name] = sprite;
            return sprite;
        }

        /// <summary>Optional artwork from Resources/Art. Null, without a warning, when it has not been supplied.</summary>
        public static Sprite Art(string name)
        {
            Sprite sprite;
            if (Icons.TryGetValue(name, out sprite)) return sprite;
            sprite = Resources.Load<Sprite>("Art/" + name);
            Icons[name] = sprite;
            return sprite;
        }

        /// <summary>Distance from a point to the edge of a rounded rectangle, positive inside.</summary>
        private static float Inside(float x, float y, float x0, float y0, float x1, float y1, float radius)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float qx = Mathf.Abs(x - cx) - ((x1 - x0) * 0.5f - radius);
            float qy = Mathf.Abs(y - cy) - ((y1 - y0) * 0.5f - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return radius - outside - Mathf.Min(Mathf.Max(qx, qy), 0f);
        }

        private static Sprite MakeShaded(int size, float radius, bool button)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float rim = button ? 3.6f : 3.2f, lip = button ? 11f : 5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float body = Inside(px, py, 0f, 0f, size, size, radius);
                    float face = Inside(px, py, rim, rim + lip, size - rim, size - rim, radius - rim);
                    float alpha = Mathf.Clamp01(body + 0.5f);
                    Color c;
                    if (button)
                    {
                        // Luminance only: the Image colour supplies the hue, so rim and lip come out as darker shades of it.
                        float up = py / size;
                        float lum = Mathf.Lerp(0.82f, 0.95f, up);
                        float shine = Mathf.Clamp01(1f - Mathf.Abs(face - 5.5f) / 2.6f) * Mathf.Clamp01((up - 0.60f) * 6f);
                        lum = Mathf.Lerp(lum, 1f, shine * 0.9f);
                        lum = Mathf.Lerp(0.56f, lum, Mathf.Clamp01(face + 0.5f));         // lip under the face
                        lum = Mathf.Lerp(0.36f, lum, Mathf.Clamp01(body - rim + 0.5f));  // dark rim
                        c = new Color(lum, lum, lum, alpha);
                    }
                    else
                    {
                        c = Color.white;
                        c = Color.Lerp(new Color(0.84f, 0.88f, 0.95f), c, Mathf.Clamp01(face + 0.5f));
                        c = Color.Lerp(new Color(0.66f, 0.73f, 0.87f), c, Mathf.Clamp01(body - rim + 0.5f));
                        c.a = alpha;
                    }
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            float border = radius + 6f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border + lip, border, border));
        }

        private static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            float border = Mathf.Min(radius, size * 0.5f - 1f);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite MakeStar(int size)
        {
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0 ? 0.48f : 0.21f) * size;
                poly[i] = new Vector2(size * 0.5f + Mathf.Cos(angle) * r, size * 0.5f + Mathf.Sin(angle) * r);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                            if (InPolygon(poly, x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f)) hits++;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 9));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static bool InPolygon(Vector2[] poly, float px, float py)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > py) != (poly[j].y > py) &&
                    px < (poly[j].x - poly[i].x) * (py - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        // ------------------------------------------------------------------ layout helpers

        public static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchors and pivots at (ax, ay) of the parent, offset by (x, y), with a fixed size.</summary>
        public static RectTransform Place(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image MakeImage(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = MakeRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text MakeText(Transform parent, string value, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            RectTransform rt = MakeRect("Text", parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = Loc.T(value); // every label goes through the localisation table
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            // The game font is already extra bold; asking for Bold on top would smear it with synthetic weight.
            text.fontStyle = _ownFont ? FontStyle.Normal : style;
            if (_ownFont) text.lineSpacing = 0.72f; // the font's own line box is very tall
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>A chunky button with a centred label. Returns the Button; the label is its first Text child.</summary>
        public static Button MakeButton(Transform parent, string label, Color background, UnityAction onClick, float width, float height, int fontSize = 46)
        {
            Image img = MakeImage(parent, "Button_" + label, ButtonSprite, background, true);
            img.rectTransform.sizeDelta = new Vector2(width, height);
            // Small buttons keep their corners round instead of squashing the 9-slice borders.
            img.pixelsPerUnitMultiplier = Mathf.Max(1f, 150f / Mathf.Min(width, height));
            var drop = img.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0.05f, 0.08f, 0.20f, 0.28f);
            drop.effectDistance = new Vector2(0f, -7f);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            img.gameObject.AddComponent<PressScale>();
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            // A glassy highlight across the upper half, white so it reads on any colour, under the label.
            Image gloss = MakeImage(img.transform, "Gloss", Rounded, new Color(1f, 1f, 1f, 0.22f));
            RectTransform g = gloss.rectTransform;
            g.anchorMin = new Vector2(0f, 0.5f);
            g.anchorMax = new Vector2(1f, 1f);
            float side = Mathf.Max(10f, height * 0.16f);
            g.offsetMin = new Vector2(side, height * 0.05f);
            g.offsetMax = new Vector2(-side, -height * 0.11f);
            gloss.pixelsPerUnitMultiplier = 48f / Mathf.Max(8f, height * 0.34f);
            Text text = MakeText(img.transform, label, fontSize, Color.white);
            Stretch(text.rectTransform, 14f, Mathf.Min(16f, height * 0.12f), 14f, 6f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = fontSize;
            Emboss(text, new Color(background.r * 0.45f, background.g * 0.45f, background.b * 0.45f, 0.9f));
            return button;
        }

        /// <summary>Outlines a light label in a dark shade so it reads on any button colour.</summary>
        public static void Emboss(Text text, Color ink)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = ink;
            outline.effectDistance = new Vector2(2f, -2f);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(ink.r, ink.g, ink.b, 0.55f);
            shadow.effectDistance = new Vector2(0f, -4f);
        }

        /// <summary>Adds an icon to a button or panel. ax, ay anchor it inside the parent (0..1).</summary>
        public static Image AddIcon(Transform parent, string icon, float size, float ax = 0.5f, float ay = 0.5f, float x = 0f, float y = 0f)
        {
            Image img = MakeImage(parent, "Icon_" + icon, Icon(icon), Color.white);
            img.preserveAspect = true;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>
        /// A spinning 3D object from a sprite sheet rendered by Tools/art/rewards.py (Resources/Art). Null when
        /// the sheet is missing, so callers can fall back to a flat icon.
        /// </summary>
        public static FlipBook MakeFlipBook(Transform parent, string sheet, float size)
        {
            Sprite sprite = Art(sheet);
            if (sprite == null) return null;
            RectTransform rt = MakeRect("Spin_" + sheet, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = sprite.texture;
            img.raycastTarget = false;
            return rt.gameObject.AddComponent<FlipBook>();
        }

        public static Text LabelOf(Button button)
        {
            return button.GetComponentInChildren<Text>(true);
        }

        public static Slider MakeSlider(Transform parent, float value, UnityAction<float> onChanged, float width)
        {
            RectTransform root = MakeRect("Slider", parent);
            root.sizeDelta = new Vector2(width, 70f);
            Image bg = MakeImage(root, "Background", Rounded, new Color(0.80f, 0.84f, 0.92f));
            Stretch(bg.rectTransform, 0f, 22f, 0f, 22f);
            RectTransform fillArea = Stretch(MakeRect("FillArea", root), 0f, 22f, 0f, 22f);
            Image fill = MakeImage(fillArea, "Fill", Rounded, Palette.UiPrimary);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.sizeDelta = Vector2.zero;
            RectTransform handleArea = Stretch(MakeRect("HandleArea", root), 32f, 0f, 32f, 0f);
            Image handle = MakeImage(handleArea, "Handle", Circle, Color.white, true);
            handle.gameObject.AddComponent<Outline>().effectColor = new Color(0.30f, 0.40f, 0.62f, 0.9f);
            handle.rectTransform.sizeDelta = new Vector2(64f, 0f);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        /// <summary>Full-screen dimmer with a centred white card whose height follows its content.</summary>
        public static GameObject Modal(Transform parent, string name, string title, out RectTransform content)
        {
            Image dim = MakeImage(parent, name, null, new Color(0.03f, 0.06f, 0.16f, 0.72f), true);
            Stretch(dim.rectTransform);
            // The gold-framed sky-blue panel of the shop and settings (UiPremium).
            Image card = MakeImage(dim.transform, "Card", UiPremium.Panel, Color.white, true);
            card.pixelsPerUnitMultiplier = 0.8f;
            card.gameObject.AddComponent<PopIn>();
            var cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            cardShadow.effectDistance = new Vector2(0f, -14f);
            Place(card.rectTransform, 0.5f, 0.5f, 0f, 0f, 900f, 600f);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(56, 56, 52, 64);
            layout.spacing = 26f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            content = card.rectTransform;
            if (!string.IsNullOrEmpty(title))
            {
                // Title ribbon across the top of the card.
                Image ribbon = MakeImage(content, "Title", ButtonSprite, Palette.UiPrimary);
                ribbon.rectTransform.sizeDelta = new Vector2(800f, 124f);
                Text t = MakeText(ribbon.transform, title, 64, Color.white);
                Stretch(t.rectTransform, 20f, 16f, 20f, 6f);
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 30;
                t.resizeTextMaxSize = 64;
                Emboss(t, new Color(0.08f, 0.22f, 0.48f, 0.9f));
            }
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        public static Canvas CreateCanvas(Transform parent)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f; // always 1080 units wide in portrait, taller phones just get more height
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<AdaptiveCanvasScale>();

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(parent, false);
#if ENABLE_INPUT_SYSTEM
                var module = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                module.AssignDefaultActions();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
            return canvas;
        }
    }

    /// <summary>
    /// Portrait screens scale the UI by width (always 1080 units wide). Landscape windows, such as a wide
    /// editor Game view, scale by height instead so the portrait layout stays a sensible size.
    /// </summary>
    public sealed class AdaptiveCanvasScale : MonoBehaviour
    {
        private CanvasScaler _scaler;
        private int _width, _height;

        private void Update()
        {
            if (Screen.width == _width && Screen.height == _height) return;
            _width = Screen.width;
            _height = Screen.height;
            if (_scaler == null) _scaler = GetComponent<CanvasScaler>();
            if (_scaler != null) _scaler.matchWidthOrHeight = _width <= _height ? 0f : 1f;
        }
    }

    /// <summary>Buttons dip slightly while pressed and spring back on release.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private float _target = 1f, _current = 1f;

        public void OnPointerDown(PointerEventData eventData) { _target = 0.93f; }
        public void OnPointerUp(PointerEventData eventData) { _target = 1f; }
        public void OnPointerExit(PointerEventData eventData) { _target = 1f; }

        private void OnDisable()
        {
            _target = _current = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (Mathf.Approximately(_current, _target)) return;
            _current = GameSettings.ReducedMotion ? _target : Mathf.MoveTowards(_current, _target, Time.unscaledDeltaTime * 1.4f);
            transform.localScale = Vector3.one * _current;
        }
    }

    /// <summary>Panels scale up with a soft overshoot each time they are shown.</summary>
    public sealed class PopIn : MonoBehaviour
    {
        private float _t = 1f;
        // Popups are built enabled and hidden straight after: only later showings make a sound.
        private bool _armed;

        private void OnEnable()
        {
            _t = GameSettings.ReducedMotion ? 1f : 0f;
            Apply();
            if (_armed) PlaySound(Sfx.PopupOpen);
        }

        private void OnDisable()
        {
            if (_armed) PlaySound(Sfx.PopupClose);
            _armed = true;
        }

        private static void PlaySound(Sfx sfx)
        {
            GameApp app = GameApp.Instance;
            if (app != null && app.isActiveAndEnabled && app.Audio != null) app.Audio.Play(sfx);
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / 0.22f);
            Apply();
        }

        private void Apply()
        {
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, 1f, Ease.OutBack(_t));
        }
    }

    /// <summary>Keeps a full-screen rect inside the device safe area (notches, home indicator).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect _applied;

        private void Update()
        {
            Rect safe = Screen.safeArea;
            if (safe == _applied || Screen.width <= 0 || Screen.height <= 0) return;
            _applied = safe;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Slow constant rotation, for light rays behind a reward.</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float DegreesPerSecond = 24f;

        private void Update()
        {
            if (GameSettings.ReducedMotion) return;
            transform.Rotate(0f, 0f, DegreesPerSecond * Time.unscaledDeltaTime);
        }
    }

    /// <summary>Gentle idle float and breathing scale, for the logo and the mascot.</summary>
    /// <summary>A light shaft that sways a few degrees and breathes in brightness, very slowly.</summary>
    public sealed class Sway2D : MonoBehaviour
    {
        public float Phase;
        private RawImage _img;
        private float _alpha, _angle;

        private void Awake()
        {
            _img = GetComponent<RawImage>();
            _alpha = _img != null ? _img.color.a : 0f;
            _angle = transform.localEulerAngles.z;
        }

        private void Update()
        {
            if (GameSettings.ReducedMotion) return;
            float t = Time.unscaledTime * 0.35f + Phase;
            transform.localRotation = Quaternion.Euler(0f, 0f, _angle + Mathf.Sin(t) * 2.5f);
            if (_img != null)
            {
                Color c = _img.color;
                c.a = _alpha * (0.65f + 0.35f * Mathf.Sin(t * 1.3f + 1f));
                _img.color = c;
            }
        }
    }

    /// <summary>A heartbeat: two quick swells ("lub-dub") then a rest, on the element's scale.</summary>
    public sealed class Beat : MonoBehaviour
    {
        public float Period = 1.5f, Amount = 0.07f;

        private void Update()
        {
            if (GameSettings.ReducedMotion) return;
            float t = Mathf.Repeat(Time.unscaledTime, Period);
            float s = 1f + Amount * Pulse(t, 0.0f) + Amount * 0.6f * Pulse(t, 0.2f);
            transform.localScale = new Vector3(s, s, 1f);
        }

        private static float Pulse(float t, float at)
        {
            float u = (t - at) / 0.16f;
            return u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);
        }

        private void OnDisable() { transform.localScale = Vector3.one; }
    }

    public sealed class Bob : MonoBehaviour
    {
        public float Height = 10f, Period = 2.6f, Squash = 0.015f;
        private RectTransform _rt;
        private Vector2 _home;

        private void OnEnable()
        {
            _rt = (RectTransform)transform;
            _home = _rt.anchoredPosition;
        }

        private void OnDisable()
        {
            if (_rt == null) return;
            _rt.anchoredPosition = _home;
            _rt.localScale = Vector3.one;
        }

        private void Update()
        {
            if (GameSettings.ReducedMotion) return;
            float s = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Period);
            _rt.anchoredPosition = _home + new Vector2(0f, s * Height);
            _rt.localScale = new Vector3(1f - s * Squash, 1f + s * Squash, 1f);
        }
    }

    /// <summary>
    /// Plays a sprite sheet on a RawImage: one full turn of an object, frames left to right and top to
    /// bottom, frame 0 facing the viewer. The grid must match COLS x ROWS in Tools/art/rewards.py.
    /// </summary>
    public sealed class FlipBook : MonoBehaviour
    {
        public const int Columns = 6, Rows = 4;

        /// <summary>Spin continuously (a tumbling coin) instead of resting face-on between turns.</summary>
        public bool Loop;
        public float TurnsPerSecond = 1.4f;
        /// <summary>When not looping: seconds of rest before the next idle turn; zero means never.</summary>
        public float Interval;

        private RawImage _image;
        private float _turn = 1f, _rest;
        private float _once = -1f, _onceLength;

        /// <summary>Plays every frame once, in order, over seconds, then holds the last frame (a heart breaking).</summary>
        public void PlayOnce(float seconds)
        {
            _once = 0f;
            _onceLength = Mathf.Max(0.05f, seconds);
        }

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            Show(0);
        }

        /// <summary>Starts one turn now (several in a row with turns greater than one).</summary>
        public void Spin(float turns = 1f)
        {
            _turn = 1f - turns;
            _rest = 0f;
        }

        private void Show(int frame)
        {
            frame = ((frame % (Columns * Rows)) + Columns * Rows) % (Columns * Rows);
            int col = frame % Columns, row = frame / Columns;
            _image.uvRect = new Rect(col / (float)Columns, 1f - (row + 1) / (float)Rows, 1f / Columns, 1f / Rows);
        }

        private void Update()
        {
            if (GameSettings.ReducedMotion)
            {
                Show(0);
                return;
            }
            float dt = Time.unscaledDeltaTime;
            if (_once >= 0f)
            {
                _once = Mathf.Min(1f, _once + dt / _onceLength);
                Show(Mathf.Min(Columns * Rows - 1, Mathf.FloorToInt(_once * Columns * Rows)));
                return;
            }
            if (Loop) _turn += dt * TurnsPerSecond;
            else if (_turn < 1f)
            {
                _turn = Mathf.Min(1f, _turn + dt * TurnsPerSecond);
            }
            else if (Interval > 0f)
            {
                _rest += dt;
                if (_rest >= Interval) Spin();
            }
            // Ease the end of a single turn so the object settles face-on instead of stopping dead.
            float phase = Loop ? _turn : (_turn >= 1f ? 0f : _turn - Mathf.Floor(_turn));
            if (!Loop && _turn > 0f && _turn < 1f) phase = 1f - (1f - _turn) * (1f - _turn);
            Show(Mathf.RoundToInt(phase * Columns * Rows));
        }
    }
}
