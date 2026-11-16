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
        private static Sprite _rounded, _circle, _star;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Sprite Rounded { get { return _rounded != null ? _rounded : (_rounded = MakeRounded(64, 24)); } }
        public static Sprite Circle { get { return _circle != null ? _circle : (_circle = MakeRounded(64, 32)); } }
        public static Sprite Star { get { return _star != null ? _star : (_star = MakeStar(96)); } }

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
            text.fontStyle = style;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>A rounded button with a centred label. Returns the Button; the label is its first Text child.</summary>
        public static Button MakeButton(Transform parent, string label, Color background, UnityAction onClick, float width, float height, int fontSize = 46)
        {
            // A darker "lip" under the face gives buttons a pressable, chunky look.
            var lipColor = new Color(background.r * 0.72f, background.g * 0.72f, background.b * 0.72f, background.a);
            Image lip = MakeImage(parent, "Button_" + label, Rounded, lipColor, true);
            lip.rectTransform.sizeDelta = new Vector2(width, height);
            Image img = MakeImage(lip.transform, "Face", Rounded, background);
            Stretch(img.rectTransform, 0f, Mathf.Min(12f, height * 0.09f), 0f, 0f);
            var button = lip.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            lip.gameObject.AddComponent<PressScale>();
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            Text text = MakeText(img.transform, label, fontSize, Color.white);
            Stretch(text.rectTransform, 12f, 6f, 12f, 6f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = fontSize;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.22f);
            shadow.effectDistance = new Vector2(0f, -3f);
            return button;
        }

        public static Text LabelOf(Button button)
        {
            return button.GetComponentInChildren<Text>(true);
        }

        public static Slider MakeSlider(Transform parent, float value, UnityAction<float> onChanged, float width)
        {
            RectTransform root = MakeRect("Slider", parent);
            root.sizeDelta = new Vector2(width, 70f);
            Image bg = MakeImage(root, "Background", Rounded, new Color(0.86f, 0.88f, 0.92f));
            Stretch(bg.rectTransform, 0f, 22f, 0f, 22f);
            RectTransform fillArea = Stretch(MakeRect("FillArea", root), 0f, 22f, 0f, 22f);
            Image fill = MakeImage(fillArea, "Fill", Rounded, Palette.UiPrimary);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.sizeDelta = Vector2.zero;
            RectTransform handleArea = Stretch(MakeRect("HandleArea", root), 32f, 0f, 32f, 0f);
            Image handle = MakeImage(handleArea, "Handle", Circle, Color.white, true);
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
            Image dim = MakeImage(parent, name, null, Palette.UiDim, true);
            Stretch(dim.rectTransform);
            Image card = MakeImage(dim.transform, "Card", Rounded, Palette.UiPanel, true);
            card.gameObject.AddComponent<PopIn>();
            var cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            cardShadow.effectDistance = new Vector2(0f, -14f);
            Place(card.rectTransform, 0.5f, 0.5f, 0f, 0f, 900f, 600f);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(50, 50, 50, 50);
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
                Text t = MakeText(content, title, 68, Palette.UiInk);
                t.rectTransform.sizeDelta = new Vector2(800f, 100f);
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

        private void OnEnable()
        {
            _t = GameSettings.ReducedMotion ? 1f : 0f;
            Apply();
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
}
