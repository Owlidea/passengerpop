using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetroSort.Game
{
    /// <summary>
    /// The StagFunny studio intro at launch, the same one Slingshire plays (mancinik/lib/ui/brand_intro.dart): on
    /// white, the red STAG block with its antlers wipes open from the left, "funny." slides in from the right, a light
    /// band runs over the red block, a short hold, then the logo fades and the white lifts off the menu. A tap skips
    /// to the fade. The timeline and curves are copied from the Flutter version, so both games open the same way.
    /// </summary>
    public sealed class BrandIntro : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>Resources path of the logo (Slingshire's assets/images/stag-funny-logo.png).</summary>
        public const string LogoPath = "Brand/stagfunny_logo";
        private const float Duration = 2.9f;
        private const float FadeFrom = 0.86f;   // the logo fades over the last 14 % of the timeline
        private const float LiftSeconds = 0.3f; // then the white background fades off the menu
        private const float Split = 0.562f;     // where the red block ends and "funny." starts, as a share of the width

        private Action _onDone;
        private bool _reduced, _notified;
        private float _t, _lift = -1f;
        private float _width, _blockWidth;
        private CanvasGroup _whole, _logoGroup;
        private RectTransform _logo, _funny, _shine;
        private Image _block, _funnyImage;
        private Vector2 _shineFrom, _shineAxis;

        /// <summary>
        /// Shows the intro over everything and calls onDone as the white starts to lift. Returns false, without an
        /// intro, when the logo is missing.
        /// </summary>
        public static bool Play(Transform parent, bool reducedMotion, Action onDone)
        {
            var logo = Resources.Load<Texture2D>(LogoPath);
            if (logo == null) return false;
            var go = new GameObject("BrandIntro", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var intro = go.AddComponent<BrandIntro>();
            intro._onDone = onDone;
            intro._reduced = reducedMotion;
            intro.Build(logo);
            intro.Pose(0f);
            return true;
        }

        private void Build(Texture2D tex)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above the game's own UI canvas
            gameObject.AddComponent<GraphicRaycaster>();
            _whole = gameObject.AddComponent<CanvasGroup>();

            // The white catches every tap: none reaches the menu underneath, and any tap skips ahead.
            Image white = UiKit.MakeImage(transform, "White", null, Color.white, true);
            UiKit.Stretch(white.rectTransform);

            // 62 % of the screen width, kept between 160 and 560 dp as in the Flutter version. The canvas has no
            // scaler, so these are screen pixels.
            float dp = Screen.dpi > 0f ? Screen.dpi / 160f : Screen.width / 411f;
            _width = Mathf.Clamp(Screen.width * 0.62f, 160f * dp, 560f * dp);
            float height = _width * tex.height / tex.width;
            _logo = UiKit.Place(UiKit.MakeRect("Logo", transform), 0.5f, 0.5f, 0f, 0f, _width, height);
            _logoGroup = _logo.gameObject.AddComponent<CanvasGroup>();

            // One picture, cut in two: the red block (with the antlers) and "funny.".
            int split = Mathf.RoundToInt(tex.width * Split);
            Sprite left = Sprite.Create(tex, new Rect(0, 0, split, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Sprite right = Sprite.Create(tex, new Rect(split, 0, tex.width - split, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _blockWidth = _width * split / tex.width;

            _block = UiKit.MakeImage(_logo, "Block", left, Color.white);
            UiKit.Place(_block.rectTransform, 0f, 0.5f, 0f, 0f, _blockWidth, height);
            _block.type = Image.Type.Filled;
            _block.fillMethod = Image.FillMethod.Horizontal;
            _block.fillOrigin = (int)Image.OriginHorizontal.Left;
            // The light band is clipped to the block's own pixels (antlers included), like Flutter's srcATop mask.
            _block.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            Image band = UiKit.MakeImage(_block.transform, "Shine", BandSprite(), Color.white);
            _shine = band.rectTransform;
            // Flutter's gradient runs from Alignment(-1, -0.4) to (1, 0.4) of the block: left to right, tilting down.
            _shineFrom = new Vector2(-_blockWidth * 0.5f, height * 0.2f);
            _shineAxis = new Vector2(_blockWidth, -height * 0.4f);
            float axis = _shineAxis.magnitude;
            UiKit.Place(_shine, 0.5f, 0.5f, 0f, 0f, axis * 0.2f, axis * 1.5f);
            _shine.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_shineAxis.y, _shineAxis.x) * Mathf.Rad2Deg);
            _shine.gameObject.SetActive(false);

            _funnyImage = UiKit.MakeImage(_logo, "Funny", right, Color.white);
            _funny = UiKit.Place(_funnyImage.rectTransform, 0f, 0.5f, _blockWidth, 0f, _width - _blockWidth, height);
        }

        /// <summary>A soft white stripe: transparent, 0x88 white, transparent (the Flutter gradient's stops).</summary>
        private static Sprite BandSprite()
        {
            const int w = 64;
            var tex = new Texture2D(w, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[w * 2];
            for (int x = 0; x < w; x++)
            {
                float a = 1f - Mathf.Abs(x / (w - 1f) * 2f - 1f);
                pixels[x] = pixels[x + w] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(0x88 * a));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, 2), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_lift < 0f && _t < FadeFrom) _t = FadeFrom;
        }

        private void Update()
        {
            // The first frames are long while the game loads underneath; never let one eat the animation.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            if (_lift < 0f)
            {
                _t = Mathf.Min(1f, _t + dt / Duration);
                Pose(_t);
                if (_t < 1f) return;
                _lift = 0f;
                Notify();
                return;
            }
            _lift = Mathf.Min(1f, _lift + dt / LiftSeconds);
            _whole.alpha = 1f - _lift * _lift * (3f - 2f * _lift);
            if (_lift >= 1f) Destroy(gameObject);
        }

        private void Notify()
        {
            if (_notified) return;
            _notified = true;
            if (_onDone != null) _onDone();
        }

        /// <summary>Lays the logo out for timeline position t (0..1).</summary>
        private void Pose(float t)
        {
            float fade = 1f - Seg(t, FadeFrom, 1f, EaseIn);
            if (_reduced)
            {
                // Reduced motion: the finished logo just fades in and out.
                _logoGroup.alpha = Seg(t, 0f, 0.15f, Linear) * fade;
                _block.fillAmount = 1f;
                _funny.anchoredPosition = new Vector2(_blockWidth, 0f);
                _funnyImage.color = Color.white;
                return;
            }
            float wipe = Seg(t, 0.04f, 0.30f, EaseOutCubic);
            float game = Seg(t, 0.24f, 0.44f, EaseOutCubic);
            float shine = Seg(t, 0.46f, 0.68f, EaseInOut);
            float scale = 0.94f + 0.06f * Seg(t, 0f, 0.5f, EaseOutBack) + 0.02f * Seg(t, FadeFrom, 1f, Linear);

            _logoGroup.alpha = fade;
            _logo.localScale = new Vector3(scale, scale, 1f);
            _block.fillAmount = wipe;
            _funny.anchoredPosition = new Vector2(_blockWidth + _width * 0.06f * (1f - game), 0f);
            _funnyImage.color = new Color(1f, 1f, 1f, game);
            bool on = shine > 0f && shine < 1f;
            if (_shine.gameObject.activeSelf != on) _shine.gameObject.SetActive(on);
            // The stripe's centre sits at gradient position shine * 1.4 - 0.15 along the axis.
            if (on) _shine.anchoredPosition = _shineFrom + _shineAxis * (shine * 1.4f - 0.15f);
        }

        // ------------------------------------------------------------------ Flutter's timing helpers

        private static float Seg(float t, float a, float b, Func<float, float> curve)
        {
            return curve(Mathf.Clamp01((t - a) / (b - a)));
        }

        private static float Linear(float x) { return x; }
        private static float EaseOutCubic(float x) { return Cubic(0.215f, 0.61f, 0.355f, 1f, x); }
        private static float EaseInOut(float x) { return Cubic(0.42f, 0f, 0.58f, 1f, x); }
        private static float EaseOutBack(float x) { return Cubic(0.175f, 0.885f, 0.32f, 1.275f, x); }
        private static float EaseIn(float x) { return Cubic(0.42f, 0f, 1f, 1f, x); }

        /// <summary>A CSS-style cubic Bézier through (0,0) and (1,1), solved the way Flutter's Cubic does.</summary>
        private static float Cubic(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float lo = 0f, hi = 1f, mid = 0.5f;
            for (int i = 0; i < 40; i++)
            {
                mid = (lo + hi) * 0.5f;
                float e = Bezier(x1, x2, mid);
                if (Mathf.Abs(x - e) < 0.0005f) break;
                if (e < x) lo = mid;
                else hi = mid;
            }
            return Bezier(y1, y2, mid);
        }

        private static float Bezier(float a, float b, float m)
        {
            float n = 1f - m;
            return 3f * a * n * n * m + 3f * b * n * m * m + m * m * m;
        }
    }
}
