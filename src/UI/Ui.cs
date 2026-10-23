using System;
using UnityEngine;

namespace SupermarketTrainer.UI
{
    internal enum ButtonKind { Primary, Secondary, Danger }

    internal readonly struct Btn
    {
        public readonly string Label;
        public readonly ButtonKind Kind;
        public Btn(string label, ButtonKind kind = ButtonKind.Secondary) { Label = label; Kind = kind; }
        public static implicit operator Btn(string label) => new Btn(label);
    }

    /// <summary>
    /// Immediate-mode row widgets stacked top to bottom inside the scrolling content group.
    /// Rows are separated by shelf hairlines rather than boxed into cards.
    /// </summary>
    internal sealed class Ui
    {
        public static string HostOnlyMessage => L.T("Réservé à l'hôte de la partie.", "Only the host can do this.");

        public float Y;
        public float Width;
        public Rect Clip;            // visible region, in group coordinates
        public bool Interactive;     // false while the window is animating closed

        private readonly Action<string> _toast;
        private int _sliderControl;
        private string _sliderId;

        public Ui(Action<string> toast) { _toast = toast; }

        private Event E => Event.current;

        private bool Hover(Rect r) => Interactive && Clip.Contains(E.mousePosition) && r.Contains(E.mousePosition);

        private bool Clicked(Rect r)
        {
            if (!Interactive || E.type != EventType.MouseDown || E.button != 0) return false;
            if (!Clip.Contains(E.mousePosition) || !r.Contains(E.mousePosition)) return false;
            E.Use();
            return true;
        }

        private bool Visible(Rect r) => r.yMax >= Clip.yMin - 4 && r.yMin <= Clip.yMax + 4;

        // ---- Structure ---------------------------------------------------------------------

        public void Section(string title)
        {
            Y += Y > 0 ? 18 : 2;
            var r = new Rect(0, Y, Width, 22);
            if (Visible(r)) Theme.Label(r, title, 13, Theme.Price, true);
            Y += 26;
        }

        public void Note(string text, Color? color = null)
        {
            float h = Theme.MeasureHeight(text, 13, Width - 4) + 4;
            var r = new Rect(2, Y, Width - 4, h);
            if (Visible(r)) Theme.Label(r, text, 13, color ?? Theme.Mist, false, TextAnchor.UpperLeft, true);
            Y += h + 6;
        }

        public void Space(float h) => Y += h;

        private void RowChrome(string id, Rect r, bool hover)
        {
            float hv = Anim.To(id + ".hover", hover ? 1f : 0f, 18f);
            if (hv > 0.01f) Theme.Fill(new Rect(r.x - 10, r.y + 1, r.width + 20, r.height - 2), Theme.Aisle.WithAlpha(hv), 8);
            Theme.Line(new Rect(r.x, r.yMax - 1, r.width, 1), Theme.Shelf.WithAlpha(0.55f));
        }

        private void LockBadge(Rect labelRect, string label, int size)
        {
            float w = Theme.Measure(label, size, true).x;
            var badge = new Rect(labelRect.x + w + 10, labelRect.y + labelRect.height / 2f - 9, 42, 18);
            Theme.Fill(badge, Theme.Shelf, 9);
            Theme.Label(badge, L.T("hôte", "host"), 11, Theme.Mist, true, TextAnchor.MiddleCenter);
        }

        // ---- Toggle ------------------------------------------------------------------------

        public bool Toggle(string id, string label, string desc, bool value, bool locked = false)
        {
            float h = desc == null ? 42 : 54;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            bool hover = Hover(r);

            if (Clicked(r))
            {
                if (locked) _toast?.Invoke(HostOnlyMessage);
                else value = !value;
            }
            if (!Visible(r)) { Anim.Snap(id + ".knob", value ? 1 : 0); return value; }

            RowChrome(id, r, hover);
            var prev = GUI.color;
            if (locked) GUI.color = prev.WithAlpha(0.45f);

            var labelRect = new Rect(r.x + 2, r.y + (desc == null ? 0 : 8), r.width - 80, desc == null ? h : 20);
            Theme.Label(labelRect, label, 15, Theme.Receipt, true);
            if (desc != null) Theme.Label(new Rect(r.x + 2, r.y + 28, r.width - 80, 18), desc, 12, Theme.Mist);
            if (locked) LockBadge(labelRect, label, 15);

            float k = Anim.To(id + ".knob", value ? 1f : 0f, 16f);
            var track = new Rect(r.xMax - 46, r.y + h / 2f - 11, 44, 22);
            Theme.Fill(track, Color.Lerp(Theme.Shelf, Theme.Price, k), 11);
            var knob = new Rect(track.x + 3 + k * 22, track.y + 3, 16, 16);
            Theme.Fill(knob, Color.Lerp(Theme.Receipt, Theme.Ink, k), 8);

            GUI.color = prev;
            return value;
        }

        // ---- Slider ------------------------------------------------------------------------

        public float Slider(string id, string label, float value, float min, float max,
            Func<float, string> format, bool locked = false, float step = 0f)
        {
            const float h = 58;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            var track = new Rect(r.x + 2, r.y + 38, r.width - 4, 6);
            var hit = new Rect(track.x - 8, track.y - 12, track.width + 16, track.height + 24);

            int ctl = GUIUtility.GetControlID(FocusType.Passive);
            if (Interactive)
            {
                switch (E.GetTypeForControl(ctl))
                {
                    case EventType.MouseDown when E.button == 0 && Clip.Contains(E.mousePosition) && hit.Contains(E.mousePosition):
                        if (locked) { _toast?.Invoke(HostOnlyMessage); E.Use(); break; }
                        GUIUtility.hotControl = ctl;
                        _sliderControl = ctl;
                        _sliderId = id;
                        value = ValueAt(track, min, max, step);
                        GUI.changed = true;
                        E.Use();
                        break;
                    case EventType.MouseDrag when GUIUtility.hotControl == ctl:
                        value = ValueAt(track, min, max, step);
                        GUI.changed = true;
                        E.Use();
                        break;
                    case EventType.MouseUp when GUIUtility.hotControl == ctl:
                        GUIUtility.hotControl = 0;
                        _sliderId = null;
                        E.Use();
                        break;
                }
            }
            if (!Visible(r)) return value;

            bool active = GUIUtility.hotControl == ctl && _sliderControl == ctl && _sliderId == id;
            RowChrome(id, r, Hover(r) || active);
            var prev = GUI.color;
            if (locked) GUI.color = prev.WithAlpha(0.45f);

            var labelRect = new Rect(r.x + 2, r.y + 6, r.width - 120, 22);
            Theme.Label(labelRect, label, 15, Theme.Receipt, true);
            if (locked) LockBadge(labelRect, label, 15);
            Theme.Label(new Rect(r.xMax - 140, r.y + 6, 138, 22), format(value), 15, Theme.Price, true, TextAnchor.MiddleRight);

            float t = Mathf.InverseLerp(min, max, value);
            float shown = Anim.To(id + ".fill", t, active ? 40f : 18f);
            Theme.Fill(track, Theme.Shelf, 3);
            Theme.Fill(new Rect(track.x, track.y, Mathf.Max(6, track.width * shown), track.height), Theme.Price, 3);
            float grow = Anim.To(id + ".grab", active ? 1f : 0f, 20f);
            float ks = 16 + 4 * grow;
            Theme.Fill(new Rect(track.x + track.width * shown - ks / 2f, track.center.y - ks / 2f, ks, ks), Theme.Receipt, (int)(ks / 2f));

            GUI.color = prev;
            return value;
        }

        private float ValueAt(Rect track, float min, float max, float step)
        {
            float v = Mathf.Lerp(min, max, Mathf.InverseLerp(track.xMin, track.xMax, E.mousePosition.x));
            if (step > 0f) v = Mathf.Round(v / step) * step;
            return Mathf.Clamp(v, min, max);
        }

        // ---- Buttons -----------------------------------------------------------------------

        /// <summary>Row of equal-width buttons. Returns the index clicked, or -1.</summary>
        public int Buttons(string id, bool locked, params Btn[] buttons)
        {
            const float h = 36, gap = 8;
            int clicked = -1;
            float w = (Width - gap * (buttons.Length - 1)) / buttons.Length;
            Y += 6;
            for (int i = 0; i < buttons.Length; i++)
            {
                var r = new Rect(i * (w + gap), Y, w, h);
                if (Button(id + "." + i, r, buttons[i], locked)) clicked = i;
            }
            Y += h + 8;
            return clicked;
        }

        public bool Button(string id, Rect r, Btn b, bool locked = false)
        {
            bool hover = Hover(r);
            bool clicked = Clicked(r);
            if (clicked && locked) { _toast?.Invoke(HostOnlyMessage); clicked = false; }
            if (clicked) Anim.Snap(id + ".press", 1f);
            if (!Visible(r)) return clicked;

            float hv = Anim.To(id + ".hover", hover && !locked ? 1f : 0f, 18f);
            float press = Anim.To(id + ".press", 0f, 10f);
            Color fill, text;
            switch (b.Kind)
            {
                case ButtonKind.Primary: fill = Theme.Price; text = Theme.Ink; break;
                case ButtonKind.Danger: fill = Theme.Promo.WithAlpha(0.16f + 0.2f * hv); text = Theme.Promo; break;
                default: fill = Theme.Shelf; text = Theme.Receipt; break;
            }
            if (b.Kind != ButtonKind.Danger) fill = Color.Lerp(fill, Color.white, 0.12f * hv);
            fill = Color.Lerp(fill, Color.white, 0.25f * press);

            var prev = GUI.color;
            if (locked) GUI.color = prev.WithAlpha(0.45f);
            var draw = new Rect(r.x, r.y + press * 1.5f, r.width, r.height);
            Theme.Fill(draw, fill, 7);
            Theme.Label(draw, b.Label, 14, text, true, TextAnchor.MiddleCenter);
            GUI.color = prev;
            return clicked;
        }

        // ---- Inputs ------------------------------------------------------------------------

        public string TextField(string id, string label, string value, float fieldWidth = 200)
        {
            const float h = 48;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            if (!Visible(r)) return value;
            RowChrome(id, r, false);
            Theme.Label(new Rect(r.x + 2, r.y, r.width - fieldWidth - 10, h), label, 15, Theme.Receipt, true);
            var fr = new Rect(r.xMax - fieldWidth, r.y + 7, fieldWidth, h - 14);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = Theme.Shelf;
            GUI.SetNextControlName(id);
            value = GUI.TextField(fr, value ?? "", 24, Theme.FieldStyle());
            GUI.backgroundColor = prev;
            return value;
        }

        /// <summary>Label with « value » arrows on the right. Returns the new value, wrapped.</summary>
        public int Stepper(string id, string label, string desc, int value, int min, int max, Func<int, string> format = null)
        {
            float h = desc == null ? 46 : 56;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            var left = new Rect(r.xMax - 172, r.y + h / 2f - 15, 34, 30);
            var right = new Rect(r.xMax - 34, r.y + h / 2f - 15, 34, 30);
            if (Button(id + ".l", left, "‹")) value = value <= min ? max : value - 1;
            if (Button(id + ".r", right, "›")) value = value >= max ? min : value + 1;
            if (!Visible(r)) return value;

            RowChrome(id, r, false);
            Theme.Label(new Rect(r.x + 2, r.y + (desc == null ? 0 : 9), r.width - 190, desc == null ? h : 20), label, 15, Theme.Receipt, true);
            if (desc != null) Theme.Label(new Rect(r.x + 2, r.y + 30, r.width - 190, 18), desc, 12, Theme.Mist);
            Theme.Label(new Rect(left.xMax, left.y, right.x - left.xMax, left.height),
                format != null ? format(value) : value.ToString(), 16, Theme.Price, true, TextAnchor.MiddleCenter);
            return value;
        }

        public void Stat(string label, string value)
        {
            const float h = 34;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            if (!Visible(r)) return;
            Theme.Line(new Rect(r.x, r.yMax - 1, r.width, 1), Theme.Shelf.WithAlpha(0.55f));
            Theme.Label(new Rect(r.x + 2, r.y, r.width / 2f, h), label, 14, Theme.Mist);
            Theme.Label(new Rect(r.x + r.width / 2f, r.y, r.width / 2f - 2, h), value, 14, Theme.Receipt, true, TextAnchor.MiddleRight);
        }

        /// <summary>
        /// A list row with a title, a caption and right-aligned compact buttons. The first
        /// button is always available; <paramref name="lockExtras"/> locks the others.
        /// </summary>
        public int ListRow(string id, string title, string caption, bool lockExtras, params Btn[] buttons)
        {
            const float h = 54, bw = 92, gap = 6;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            int clicked = -1;
            float x = r.xMax - buttons.Length * (bw + gap) + gap;
            for (int i = 0; i < buttons.Length; i++)
            {
                var br = new Rect(x + i * (bw + gap), r.y + 11, bw, 32);
                if (Button(id + ".b" + i, br, buttons[i], lockExtras && i > 0)) clicked = i;
            }
            if (!Visible(r)) return clicked;
            RowChrome(id, r, Hover(r));
            float tw = r.width - buttons.Length * (bw + gap) - 8;
            Theme.Label(new Rect(r.x + 2, r.y + 8, tw, 20), title, 15, Theme.Receipt, true);
            Theme.Label(new Rect(r.x + 2, r.y + 29, tw, 18), caption, 12, Theme.Mist);
            return clicked;
        }
    }
}
