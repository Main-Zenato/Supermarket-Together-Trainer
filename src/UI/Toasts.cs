using System.Collections.Generic;
using UnityEngine;

namespace SupermarketTrainer.UI
{
    /// <summary>Short confirmations printed as till-receipt strips in the bottom-right corner.</summary>
    internal static class Toasts
    {
        private sealed class Toast
        {
            public string Text;
            public float Born;
            public float Y;
        }

        private const float Life = 2.6f;
        private const int Max = 4;
        private static readonly List<Toast> Items = new List<Toast>();

        public static bool Enabled = true;

        public static void Show(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text)) return;
            if (Items.Count > 0 && Items[Items.Count - 1].Text == text)
            {
                Items[Items.Count - 1].Born = Time.unscaledTime; // refresh instead of stacking duplicates
                return;
            }
            Items.Add(new Toast { Text = text, Born = Time.unscaledTime, Y = -1 });
            if (Items.Count > Max) Items.RemoveAt(0);
        }

        public static void Draw(float screenW, float screenH)
        {
            if (Items.Count == 0) return;
            const float w = 300, pad = 14, margin = 24, gap = 10;
            float bottom = screenH - margin;
            bool repaint = Event.current.type == EventType.Repaint;

            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var t = Items[i];
                float age = Time.unscaledTime - t.Born;
                if (age > Life + 0.4f) { Items.RemoveAt(i); continue; }

                float h = Theme.MeasureHeight(t.Text, 14, w - pad * 2) + pad * 2;
                float targetY = bottom - h;
                if (t.Y < 0) t.Y = targetY;
                if (repaint) t.Y = Mathf.Lerp(t.Y, targetY, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                bottom -= h + gap + 6;

                float enter = Mathf.Clamp01(age / 0.28f);
                float exit = Mathf.Clamp01((age - Life) / 0.4f);
                float slide = (1f - Anim.EaseOutBack(enter)) * 60f + exit * 40f;
                float alpha = Mathf.Min(enter * 2f, 1f - exit);

                var r = new Rect(screenW - margin - w + slide, t.Y, w, h);
                var prev = GUI.color;
                GUI.color = new Color(1, 1, 1, alpha);
                Theme.Fill(new Rect(r.x + 3, r.y + 5, r.width, r.height), new Color(0, 0, 0, 0.25f), 4);
                Theme.DrawZigzag(new Rect(r.x, r.y - 6, r.width, 6), Theme.Receipt);
                Theme.Fill(r, Theme.Receipt);
                Theme.Fill(new Rect(r.x, r.y, 4, r.height), Theme.Price);
                Theme.Label(new Rect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2), t.Text, 14,
                    Theme.Ink, false, TextAnchor.UpperLeft, true);
                GUI.color = prev;
            }
        }
    }
}
