using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetroSort.Game
{
    /// <summary>
    /// The premium popup look (shop, levels, settings, coin bar): a gold-framed sky-blue panel with a red ribbon
    /// title and a round close button, cream offer tiles, round price buttons and toggle switches. All sprites are
    /// drawn at startup, so the look needs no imported art beyond the ribbon and icons it can fall back from.
    /// </summary>
    public static class UiPremium
    {
        private static Sprite _panel, _tile, _pill, _round, _close, _switchTrack, _tag, _dark;

        public static readonly Color Gold = new Color(1.00f, 0.80f, 0.22f);
        public static readonly Color Green = new Color(0.36f, 0.80f, 0.26f);
        public static readonly Color Orange = new Color(1.00f, 0.60f, 0.16f);
        public static readonly Color Blue = new Color(0.20f, 0.56f, 0.96f);
        public static readonly Color Red = new Color(0.94f, 0.30f, 0.30f);
        public static readonly Color Ink = new Color(0.36f, 0.20f, 0.08f);   // warm brown text on cream tiles
        public static readonly Color DeepBlue = new Color(0.07f, 0.20f, 0.48f);

        /// <summary>Popup body: thick bevelled gold frame around a sky-blue gradient.</summary>
        public static Sprite Panel { get { return _panel != null ? _panel : (_panel = MakePanel(160, 54f)); } }
        /// <summary>Cream card for offers and setting rows, with a soft tan rim.</summary>
        public static Sprite Tile { get { return _tile != null ? _tile : (_tile = MakeTile(120, 36f)); } }
        /// <summary>Dark glassy pill for counters (coins on the menu and HUD).</summary>
        public static Sprite Pill { get { return _pill != null ? _pill : (_pill = MakePill(96)); } }
        /// <summary>A round chunky button, tinted by the Image colour.</summary>
        public static Sprite Round { get { return _round != null ? _round : (_round = MakeRound(128, false)); } }
        /// <summary>The red round close button with a white cross.</summary>
        public static Sprite Close { get { return _close != null ? _close : (_close = MakeRound(128, true)); } }
        public static Sprite SwitchTrack { get { return _switchTrack != null ? _switchTrack : (_switchTrack = MakePill(96)); } }
        /// <summary>A small slanted flag for "best value" style labels, tinted by the Image colour.</summary>
        public static Sprite Tag { get { return _tag != null ? _tag : (_tag = MakeTag(160, 64)); } }
        /// <summary>Inner darker blue well, for grouping inside the panel.</summary>
        public static Sprite Well { get { return _dark != null ? _dark : (_dark = MakeWell(96, 30f)); } }
        private static Sprite _rider;
        /// <summary>A white passenger silhouette (head and shoulders) to lay over a coloured Round.</summary>
        public static Sprite Rider { get { return _rider != null ? _rider : (_rider = MakeRider(96)); } }
        private static Sprite _ring;
        /// <summary>A thin white ring with soft edges (the ripple of a tap), tinted by the Image colour.</summary>
        public static Sprite Ring { get { return _ring != null ? _ring : (_ring = MakeRing(128, 0.80f)); } }

        private static Sprite MakeRing(int size, float inner)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f, outer = c - 1f, hole = c * inner;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01((d - hole) / 1.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>
        /// A solid white house for round buttons, in the style of the white gear on the settings button: a roof with
        /// eaves and a chimney, walls, and a door cut in the button's own colour, over a soft shadow.
        /// </summary>
        public static RectTransform House(Transform parent, float size, Color door)
        {
            RectTransform root = UiKit.Place(UiKit.MakeRect("House", parent), 0.5f, 0.5f, 0f, size * 0.02f, size, size);
            HouseShape(root, size, new Color(0.02f, 0.08f, 0.26f, 0.42f), new Vector2(0f, -size * 0.055f), null);
            HouseShape(root, size, Color.white, Vector2.zero, door);
            return root;
        }

        private static void HouseShape(RectTransform root, float s, Color color, Vector2 at, Color? door)
        {
            HousePart(root, "Chimney", color, at + new Vector2(s * 0.21f, s * 0.21f), s * 0.12f, s * 0.22f, s * 0.03f, 0f);
            // A rounded square turned on its corner: the roof, its lower half hidden behind the walls.
            HousePart(root, "Roof", color, at + new Vector2(0f, s * 0.06f), s * 0.60f, s * 0.60f, s * 0.07f, 45f);
            HousePart(root, "Walls", color, at + new Vector2(0f, -s * 0.20f), s * 0.62f, s * 0.44f, s * 0.06f, 0f);
            if (door.HasValue) HousePart(root, "Door", door.Value, at + new Vector2(0f, -s * 0.31f), s * 0.17f, s * 0.24f, s * 0.07f, 0f);
        }

        private static void HousePart(RectTransform root, string name, Color color, Vector2 at, float w, float h, float radius, float angle)
        {
            Image part = UiKit.MakeImage(root, name, UiKit.Rounded, color);
            part.type = Image.Type.Sliced;
            // Rounded's corners are 24 px: scale them to the wanted radius.
            part.pixelsPerUnitMultiplier = 24f / Mathf.Max(0.5f, radius);
            RectTransform rt = UiKit.Place(part.rectTransform, 0.5f, 0.5f, at.x, at.y, w, h);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ------------------------------------------------------------------ sprites

        private static float Inside(float x, float y, float x0, float y0, float x1, float y1, float radius)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float qx = Mathf.Abs(x - cx) - ((x1 - x0) * 0.5f - radius);
            float qy = Mathf.Abs(y - cy) - ((y1 - y0) * 0.5f - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return radius - outside - Mathf.Min(Mathf.Max(qx, qy), 0f);
        }

        private static Sprite Finish(Texture2D tex, Color32[] px, int size, float border)
        {
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Texture2D NewTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Sprite MakePanel(int size, float radius)
        {
            Texture2D tex = NewTex(size);
            var px = new Color32[size * size];
            const float outer = 3f, frame = 15f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, up = fy / size;
                    float body = Inside(fx, fy, 0f, 0f, size, size, radius);
                    float inFrame = Inside(fx, fy, outer, outer, size - outer, size - outer, radius - outer);
                    float inner = Inside(fx, fy, frame, frame, size - frame, size - frame, radius - frame);
                    // Gold frame: bright top, deep amber bottom, a shine line along its middle.
                    Color gold = Color.Lerp(new Color(0.93f, 0.55f, 0.10f), new Color(1.0f, 0.90f, 0.45f), up);
                    float shine = Mathf.Clamp01(1f - Mathf.Abs(inFrame - 4.5f) / 2.2f);
                    gold = Color.Lerp(gold, new Color(1f, 0.98f, 0.80f), shine * 0.55f);
                    // Sky body with a lighter top and a dark inner shadow under the frame.
                    Color sky = Color.Lerp(new Color(0.17f, 0.47f, 0.90f), new Color(0.44f, 0.76f, 1.0f), Mathf.Pow(up, 0.8f));
                    float shadow = Mathf.Clamp01(1f - inner / 12f);
                    sky = Color.Lerp(sky, new Color(0.08f, 0.24f, 0.58f), shadow * 0.55f);
                    Color c = Color.Lerp(gold, sky, Mathf.Clamp01(inner + 0.5f));
                    c = Color.Lerp(new Color(0.42f, 0.22f, 0.05f), c, Mathf.Clamp01(inFrame + 0.5f)); // dark outline
                    c.a = Mathf.Clamp01(body + 0.5f);
                    px[y * size + x] = c;
                }
            return Finish(tex, px, size, radius + 10f);
        }

        private static Sprite MakeTile(int size, float radius)
        {
            Texture2D tex = NewTex(size);
            var px = new Color32[size * size];
            const float rim = 4f, lip = 6f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, up = fy / size;
                    float body = Inside(fx, fy, 0f, 0f, size, size, radius);
                    float face = Inside(fx, fy, rim, rim + lip, size - rim, size - rim, radius - rim);
                    Color c = Color.Lerp(new Color(0.97f, 0.88f, 0.70f), new Color(1.0f, 0.98f, 0.91f), up);
                    float hi = Mathf.Clamp01(1f - Mathf.Abs(face - 3f) / 2f) * Mathf.Clamp01((up - 0.55f) * 4f);
                    c = Color.Lerp(c, Color.white, hi * 0.8f);
                    c = Color.Lerp(new Color(0.86f, 0.66f, 0.40f), c, Mathf.Clamp01(face + 0.5f));
                    c = Color.Lerp(new Color(0.62f, 0.40f, 0.18f), c, Mathf.Clamp01(body - rim + 0.5f));
                    c.a = Mathf.Clamp01(body + 0.5f);
                    px[y * size + x] = c;
                }
            Texture2D t = tex;
            t.SetPixels32(px);
            t.Apply(false, true);
            float border = radius + 4f;
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border + lip, border, border));
        }

        private static Sprite MakeWell(int size, float radius)
        {
            Texture2D tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, up = fy / size;
                    float body = Inside(fx, fy, 0f, 0f, size, size, radius);
                    // Inset look: shadow along the top edge, light along the bottom.
                    float edge = Mathf.Clamp01(1f - body / 8f);
                    Color c = new Color(0.10f, 0.30f, 0.66f, 0.55f);
                    c = Color.Lerp(c, up > 0.5f ? new Color(0.04f, 0.14f, 0.36f, 0.75f) : new Color(0.55f, 0.80f, 1f, 0.6f), edge * 0.8f);
                    c.a *= Mathf.Clamp01(body + 0.5f);
                    px[y * size + x] = c;
                }
            return Finish(tex, px, size, radius + 2f);
        }

        private static Sprite MakePill(int size)
        {
            Texture2D tex = NewTex(size);
            var px = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, up = fy / size;
                    float body = Inside(fx, fy, 0f, 0f, size, size, radius);
                    float face = Inside(fx, fy, 4f, 4f, size - 4f, size - 4f, radius - 4f);
                    // Luminance only, so the Image colour gives the hue (dark navy for counters).
                    float lum = Mathf.Lerp(0.70f, 1.0f, up);
                    float hi = Mathf.Clamp01(1f - Mathf.Abs(face - 3f) / 2f) * Mathf.Clamp01((up - 0.6f) * 5f);
                    lum = Mathf.Lerp(lum, 1.6f, hi * 0.4f);
                    lum = Mathf.Lerp(0.35f, lum, Mathf.Clamp01(face + 0.5f));
                    px[y * size + x] = new Color(Mathf.Min(1f, lum), Mathf.Min(1f, lum), Mathf.Min(1f, lum), Mathf.Clamp01(body + 0.5f));
                }
            return Finish(tex, px, size, radius - 1f);
        }

        private static Sprite MakeRider(int size)
        {
            Texture2D tex = NewTex(size);
            var px = new Color32[size * size];
            float headR = size * 0.18f, hx = size * 0.5f, hy = size * 0.66f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float toHead = Mathf.Sqrt((fx - hx) * (fx - hx) + (fy - hy) * (fy - hy));
                    float head = headR - toHead;
                    // Shoulders: a rounded block cut flat at the bottom, kept clear of the head by a thin gap.
                    float body = Inside(fx, fy, size * 0.20f, size * -0.10f, size * 0.80f, size * 0.47f, size * 0.20f);
                    body = Mathf.Min(body, fy - size * 0.10f);
                    body = Mathf.Min(body, toHead - headR - size * 0.045f);
                    float a = Mathf.Clamp01(Mathf.Max(head, body) + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRound(int size, bool close)
        {
            Texture2D tex = NewTex(size);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r, up = (y + 0.5f) / size;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float body = r - d;
                    float face = (r - 5f) - Mathf.Sqrt(dx * dx + (dy - 4f) * (dy - 4f));
                    Color c;
                    if (close)
                    {
                        Color red = Color.Lerp(new Color(0.78f, 0.12f, 0.14f), new Color(1.0f, 0.42f, 0.38f), up);
                        c = Color.Lerp(new Color(0.55f, 0.06f, 0.08f), red, Mathf.Clamp01(face + 0.5f));
                        // White cross with a soft shadow.
                        float u = dx / r, v = dy / r;
                        float arm = Mathf.Min(Mathf.Abs(u - v), Mathf.Abs(u + v)) * 0.7071f;
                        float len = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                        float cross = Mathf.Clamp01((0.11f - arm) * r) * Mathf.Clamp01((0.42f - len) * r);
                        c = Color.Lerp(c, Color.white, cross);
                    }
                    else
                    {
                        float lum = Mathf.Lerp(0.80f, 1.0f, up);
                        float hi = Mathf.Clamp01(1f - Mathf.Abs(face - 6f) / 3f) * Mathf.Clamp01((up - 0.62f) * 5f);
                        lum = Mathf.Lerp(lum, 1f, hi * 0.9f);
                        lum = Mathf.Lerp(0.58f, lum, Mathf.Clamp01(face + 0.5f));
                        lum = Mathf.Lerp(0.34f, lum, Mathf.Clamp01(body - 4f + 0.5f));
                        c = new Color(lum, lum, lum);
                    }
                    if (close) c = Color.Lerp(new Color(0.40f, 0.04f, 0.06f), c, Mathf.Clamp01(body - 4f + 0.5f));
                    c.a = Mathf.Clamp01(body + 0.5f);
                    px[y * size + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeTag(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, up = fy / h;
                    // A banner with notched ends.
                    float notch = Mathf.Abs(fy - h * 0.5f) * 0.35f;
                    float inside = Mathf.Min(fx - notch, w - fx - notch);
                    inside = Mathf.Min(inside, Mathf.Min(fy - 2f, h - 2f - fy));
                    float lum = Mathf.Lerp(0.78f, 1f, up);
                    lum = Mathf.Lerp(0.45f, lum, Mathf.Clamp01(inside - 3f));
                    px[y * w + x] = new Color(lum, lum, lum, Mathf.Clamp01(inside + 0.5f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24f, 0f, 24f, 0f));
        }

        // ------------------------------------------------------------------ building blocks

        /// <summary>
        /// A popup: dimmer, gold-framed panel of the given size, a ribbon title on its top edge and a round close
        /// button on its top-right corner. Lay the contents out inside body (top-anchored).
        /// </summary>
        public static GameObject Modal(Transform parent, string name, string title, float width, float height, UnityAction onClose, out RectTransform body)
        {
            Image dim = UiKit.MakeImage(parent, name, null, new Color(0.03f, 0.06f, 0.16f, 0.72f), true);
            UiKit.Stretch(dim.rectTransform);
            Image panel = UiKit.MakeImage(dim.transform, "Panel", Panel, Color.white, true);
            panel.pixelsPerUnitMultiplier = 0.8f;
            UiKit.Place(panel.rectTransform, 0.5f, 0.5f, 0f, -30f, width, height);
            panel.gameObject.AddComponent<PopIn>();
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0.05f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -18f);
            body = UiKit.Stretch(UiKit.MakeRect("Body", panel.transform), 34f, 34f, 34f, 118f);

            Ribbon(panel.transform, title, 720f, 0f, 22f);
            Button close = MakeRoundButton(panel.transform, Close, Color.white, onClose, 112f);
            UiKit.Place((RectTransform)close.transform, 1f, 1f, 22f, 26f, 112f, 112f);
            close.name = "Button_Close";
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        /// <summary>The red ribbon banner with an embossed title, centred on the top edge of parent.</summary>
        public static Image Ribbon(Transform parent, string title, float width, float x, float y)
        {
            Sprite art = UiKit.Art("ribbon");
            Image ribbon = UiKit.MakeImage(parent, "Ribbon", art != null ? art : UiKit.ButtonSprite, art != null ? Color.white : Red);
            ribbon.preserveAspect = art != null;
            float h = art != null ? width * art.rect.height / art.rect.width : 150f;
            UiKit.Place(ribbon.rectTransform, 0.5f, 1f, x, y, width, h);
            ribbon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Text t = UiKit.MakeText(ribbon.transform, title, 70, Color.white);
            // The ribbon artwork's band sits in its upper middle; keep the title on it.
            UiKit.Stretch(t.rectTransform, width * 0.18f, h * 0.42f, width * 0.18f, h * 0.14f);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 30;
            t.resizeTextMaxSize = 70;
            UiKit.Emboss(t, new Color(0.45f, 0.04f, 0.06f, 0.95f));
            return ribbon;
        }

        public static Image MakeTile(Transform parent, string name, float ax, float ay, float x, float y, float w, float h)
        {
            Image tile = UiKit.MakeImage(parent, name, Tile, Color.white, false);
            tile.pixelsPerUnitMultiplier = 1.1f;
            UiKit.Place(tile.rectTransform, ax, ay, x, y, w, h);
            var s = tile.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0.02f, 0.10f, 0.30f, 0.35f);
            s.effectDistance = new Vector2(0f, -6f);
            return tile;
        }

        public static Button MakeRoundButton(Transform parent, Sprite sprite, Color color, UnityAction onClick, float size)
        {
            Image img = UiKit.MakeImage(parent, "RoundButton", sprite, color, true);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            img.gameObject.AddComponent<PressScale>();
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.55f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            var drop = img.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0.02f, 0.06f, 0.20f, 0.35f);
            drop.effectDistance = new Vector2(0f, -6f);
            return button;
        }

        /// <summary>Big embossed text in white or gold, as used for amounts and headings.</summary>
        public static Text Heading(Transform parent, string value, int size, Color color, Color ink)
        {
            Text t = UiKit.MakeText(parent, value, size, color);
            UiKit.Emboss(t, ink);
            return t;
        }

        /// <summary>A price button: chunky green with an optional coin in front of the amount.</summary>
        public static Button PriceButton(Transform parent, string label, Color color, UnityAction onClick, float w, float h, bool coin)
        {
            Button b = UiKit.MakeButton(parent, label, color, onClick, w, h, (int)(h * 0.48f));
            if (coin)
            {
                Image icon = UiKit.AddIcon(b.transform, "coin", h * 0.62f, 0f, 0.5f, h * 0.48f, 4f);
                icon.name = "Coin";
                UiKit.Stretch(UiKit.LabelOf(b).rectTransform, h * 0.82f, h * 0.14f, 14f, 6f);
            }
            return b;
        }

        /// <summary>A small flag such as "BEST VALUE" across the top of a card.</summary>
        public static Image Flag(Transform parent, string label, Color color, float ax, float ay, float x, float y, float w)
        {
            Image tag = UiKit.MakeImage(parent, "Flag", Tag, color);
            UiKit.Place(tag.rectTransform, ax, ay, x, y, w, 62f);
            Text t = UiKit.MakeText(tag.transform, label, 30, Color.white);
            UiKit.Stretch(t.rectTransform, 26f, 6f, 26f, 4f);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 16;
            t.resizeTextMaxSize = 30;
            UiKit.Emboss(t, new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.95f));
            return tag;
        }

        /// <summary>Slowly turning light rays behind an offer's artwork.</summary>
        public static void Rays(Transform parent, float size, float x, float y, float alpha)
        {
            Sprite rays = UiKit.Art("rays");
            if (rays == null) return;
            Image img = UiKit.MakeImage(parent, "Rays", rays, new Color(1f, 0.95f, 0.70f, alpha));
            img.preserveAspect = true;
            UiKit.Place(img.rectTransform, 0.5f, 0.5f, x, y, size, size);
            img.gameObject.AddComponent<Spin>().DegreesPerSecond = 14f;
        }
    }

    /// <summary>A soft white streak that sweeps across a button every few seconds (for the main call to action).</summary>
    public sealed class ShineSweep : MonoBehaviour
    {
        public float Interval = 3.4f;
        private RectTransform _rt, _streak;
        private float _t;

        public static void Add(Component button, float delay = 0f)
        {
            var sweep = button.gameObject.AddComponent<ShineSweep>();
            sweep._t = -delay;
        }

        private void Start()
        {
            _rt = (RectTransform)transform;
            // Clip to the button's own rounded shape.
            if (GetComponent<Mask>() == null) gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image streak = UiKit.MakeImage(transform, "Shine", null, new Color(1f, 1f, 1f, 0.34f));
            _streak = streak.rectTransform;
            float h = _rt.rect.height;
            UiKit.Place(_streak, 0.5f, 0.5f, -10000f, 0f, h * 0.42f, h * 2.6f);
            _streak.localRotation = Quaternion.Euler(0f, 0f, -24f);
            _streak.SetSiblingIndex(1);
            _streak.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_streak == null) return;
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Repeat(_t, Interval) / 0.75f;
            bool on = _t > 0f && k < 1f && !GameSettings.ReducedMotion;
            if (_streak.gameObject.activeSelf != on) _streak.gameObject.SetActive(on);
            if (!on) return;
            float w = _rt.rect.width;
            _streak.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.5f - 90f, w * 0.5f + 90f, k * k * (3f - 2f * k)), 0f);
        }
    }

    /// <summary>The star chest rocks a little while it waits, and eagerly once it is full.</summary>
    public sealed class ChestIdle : MonoBehaviour
    {
        public bool Ready;
        private float _t;

        private void Update()
        {
            if (GameSettings.ReducedMotion) return;
            _t += Time.unscaledDeltaTime;
            float rock = Ready ? Mathf.Sin(_t * 10f) * 6f * (0.5f + 0.5f * Mathf.Sin(_t * 2f)) : Mathf.Sin(_t * 2.2f) * 2.5f;
            transform.localRotation = Quaternion.Euler(0f, 0f, rock);
        }
    }

    /// <summary>An on/off switch: a pill that turns green with the knob sliding right.</summary>
    public sealed class ToggleSwitch : MonoBehaviour
    {
        private Image _track;
        private RectTransform _knob;
        private Text _label;
        private float _pos, _target;

        public static ToggleSwitch Make(Transform parent, UnityAction onClick)
        {
            Image track = UiKit.MakeImage(parent, "Switch", UiPremium.SwitchTrack, new Color(0.62f, 0.66f, 0.74f), true);
            track.rectTransform.sizeDelta = new Vector2(170f, 84f);
            var button = track.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (onClick != null) button.onClick.AddListener(onClick);
            var sw = track.gameObject.AddComponent<ToggleSwitch>();
            sw._track = track;
            sw._label = UiKit.MakeText(track.transform, "", 26, Color.white);
            sw._label.resizeTextForBestFit = true;
            sw._label.resizeTextMinSize = 14;
            sw._label.resizeTextMaxSize = 26;
            UiKit.Stretch(sw._label.rectTransform, 14f, 4f, 14f, 2f);
            Image knob = UiKit.MakeImage(track.transform, "Knob", UiPremium.Round, Color.white);
            sw._knob = knob.rectTransform;
            UiKit.Place(sw._knob, 0.5f, 0.5f, -43f, 2f, 72f, 72f);
            return sw;
        }

        public void Set(bool on, string onLabel, string offLabel)
        {
            _target = on ? 1f : 0f;
            _track.color = on ? UiPremium.Green : new Color(0.62f, 0.66f, 0.74f);
            _label.text = Loc.T(on ? onLabel : offLabel);
            _label.alignment = TextAnchor.MiddleCenter;
            UiKit.Stretch(_label.rectTransform, on ? 10f : 82f, 4f, on ? 82f : 10f, 2f);
            if (!isActiveAndEnabled || GameSettings.ReducedMotion) { _pos = _target; Apply(); }
        }

        private void OnEnable() { _pos = _target; Apply(); }

        private void Update()
        {
            if (Mathf.Approximately(_pos, _target)) return;
            _pos = Mathf.MoveTowards(_pos, _target, Time.unscaledDeltaTime * 7f);
            Apply();
        }

        private void Apply()
        {
            if (_knob != null) _knob.anchoredPosition = new Vector2(Mathf.Lerp(-43f, 43f, Ease.OutBack(_pos)), 2f);
        }
    }
}
