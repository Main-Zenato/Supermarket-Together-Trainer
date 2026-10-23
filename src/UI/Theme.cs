using System.Collections.Generic;
using UnityEngine;

namespace SupermarketTrainer.UI
{
    /// <summary>
    /// Palette, typography and runtime-generated textures. Everything is drawn with white
    /// textures tinted through GUI.color so a single texture serves every colour and alpha.
    /// </summary>
    internal static class Theme
    {
        // Shelf-label palette.
        public static readonly Color Ink = Hex(0x18223B);     // window background
        public static readonly Color Aisle = Hex(0x222E4D);   // sidebar, raised surfaces
        public static readonly Color Shelf = Hex(0x33416A);   // hairlines, tracks, inputs
        public static readonly Color Price = Hex(0xFFCF33);   // accent: price-tag yellow
        public static readonly Color Receipt = Hex(0xE9EDF3); // primary text
        public static readonly Color Mist = Hex(0x8E9AB8);    // secondary text
        public static readonly Color Promo = Hex(0xF2545B);   // destructive actions only

        public static Font Font;
        public static Texture2D White;
        public static Texture2D Tag;
        public static Texture2D Zigzag;

        private static readonly Dictionary<int, GUIStyle> Rounded = new Dictionary<int, GUIStyle>();
        private static readonly Dictionary<int, GUIStyle> Text = new Dictionary<int, GUIStyle>();
        private static GUIStyle _tagStyle;
        private static GUIStyle _field;
        private static bool _ready;

        public static void Ensure()
        {
            if (_ready && White != null) return;
            _ready = true;
            Rounded.Clear();
            Text.Clear();
            _field = null;

            Font = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI", "Arial" }, 14);
            White = NewTex(2, 2);
            White.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            White.Apply();
            Tag = BuildTag();
            Zigzag = BuildZigzag();
            _tagStyle = new GUIStyle { normal = { background = Tag }, border = new RectOffset(22, 8, 0, 0) };
        }

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        // ---- Drawing primitives -------------------------------------------------------------

        public static void Fill(Rect r, Color c, int radius = 0)
        {
            if (Event.current.type != EventType.Repaint) return;
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
            if (radius <= 0) GUI.DrawTexture(r, White);
            else RoundedStyle(Mathf.Min(radius, (int)(Mathf.Min(r.width, r.height) / 2f))).Draw(r, false, false, false, false);
            GUI.color = prev;
        }

        public static void Line(Rect r, Color c) => Fill(r, c);

        public static void DrawTag(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
            _tagStyle.Draw(r, false, false, false, false);
            GUI.color = prev;
        }

        public static void DrawZigzag(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
            GUI.DrawTextureWithTexCoords(r, Zigzag, new Rect(0, 0, r.width / r.height / 2f, 1));
            GUI.color = prev;
        }

        public static void Label(Rect r, string text, int size, Color c, bool bold = false,
            TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
        {
            var s = TextStyle(size, bold, align, wrap);
            s.normal.textColor = c;
            GUI.Label(r, text, s);
        }

        public static Vector2 Measure(string text, int size, bool bold = false) =>
            TextStyle(size, bold, TextAnchor.MiddleLeft, false).CalcSize(new GUIContent(text));

        public static float MeasureHeight(string text, int size, float width) =>
            TextStyle(size, false, TextAnchor.UpperLeft, true).CalcHeight(new GUIContent(text), width);

        public static GUIStyle TextStyle(int size, bool bold, TextAnchor align, bool wrap)
        {
            int key = size * 1000 + (bold ? 500 : 0) + (int)align * 10 + (wrap ? 1 : 0);
            if (!Text.TryGetValue(key, out var s))
            {
                s = new GUIStyle
                {
                    font = Font,
                    fontSize = size,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    alignment = align,
                    wordWrap = wrap,
                    clipping = TextClipping.Clip,
                    richText = false,
                    padding = new RectOffset(0, 0, 0, 0),
                };
                Text[key] = s;
            }
            return s;
        }

        public static GUIStyle FieldStyle()
        {
            if (_field != null) return _field;
            var bg = RoundedTexture(6);
            _field = new GUIStyle
            {
                font = Font,
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 0, 0),
                border = new RectOffset(7, 7, 7, 7),
                clipping = TextClipping.Clip,
            };
            _field.normal.background = bg;
            _field.hover.background = bg;
            _field.focused.background = bg;
            _field.normal.textColor = Receipt;
            _field.hover.textColor = Receipt;
            _field.focused.textColor = Receipt;
            return _field;
        }

        // ---- Texture generation -------------------------------------------------------------

        private static GUIStyle RoundedStyle(int radius)
        {
            radius = Mathf.Clamp(radius, 1, 24);
            if (!Rounded.TryGetValue(radius, out var s))
            {
                s = new GUIStyle
                {
                    normal = { background = RoundedTexture(radius) },
                    border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1),
                };
                Rounded[radius] = s;
            }
            return s;
        }

        private static Texture2D RoundedTexture(int r)
        {
            int size = r * 2 + 4;
            var t = NewTex(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, r, size - r);
                float cy = Mathf.Clamp(fy, r, size - r);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f));
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Price tag: pointed left end with a punched hole, rounded right corners.</summary>
        private static Texture2D BuildTag()
        {
            const int w = 64, h = 32, ss = 4;
            const float point = 14f, corner = 5f, holeX = 11f, holeR = 3f;
            var t = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float fx = x + (sx + 0.5f) / ss, fy = y + (sy + 0.5f) / ss;
                    if (InsideTag(fx, fy, w, h, point, corner) &&
                        (fx - holeX) * (fx - holeX) + (fy - h / 2f) * (fy - h / 2f) > holeR * holeR)
                        hits++;
                }
                px[y * w + x] = new Color(1, 1, 1, hits / (float)(ss * ss));
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static bool InsideTag(float x, float y, float w, float h, float point, float corner)
        {
            float mid = h / 2f;
            if (x < point)
            {
                // Bevelled left end: a trapezoid narrowing to a third of the height.
                float half = Mathf.Lerp(mid * 0.35f, mid, x / point);
                return Mathf.Abs(y - mid) <= half;
            }
            float cx = Mathf.Min(x, w - corner);
            float cy = Mathf.Clamp(y, corner, h - corner);
            if (x <= w - corner) return y >= 0 && y <= h;
            return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= corner * corner;
        }

        /// <summary>Repeating saw-tooth, the torn edge of a till receipt.</summary>
        private static Texture2D BuildZigzag()
        {
            const int w = 16, h = 8, ss = 4;
            var t = NewTex(w, h);
            t.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float fx = x + (sx + 0.5f) / ss, fy = y + (sy + 0.5f) / ss;
                    float tooth = Mathf.Abs((fx % w) - w / 2f) / (w / 2f) * h; // V shape
                    if (fy <= h - tooth) hits++;
                }
                px[y * w + x] = new Color(1, 1, 1, hits / (float)(ss * ss));
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }
}
