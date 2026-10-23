using System;
using System.Collections.Generic;
using SupermarketTrainer.Features;
using SupermarketTrainer.UI;
using UnityEngine;

namespace SupermarketTrainer
{
    internal static class Menu
    {
        private sealed class Tab
        {
            public string TitleFr, TitleEn, SubtitleFr, SubtitleEn;
            public Action<Ui> Draw;
            public float ScrollTarget;
            public string Title => L.T(TitleFr, TitleEn);
            public string Subtitle => L.T(SubtitleFr, SubtitleEn);
        }

        private const float W = 820, H = 560, Side = 214, TabTop = 104, TabH = 42;

        public static bool IsOpen { get; private set; }

        private static readonly List<Tab> Tabs = new List<Tab>();
        private static readonly Ui Ui = new Ui(Toasts.Show);
        private static int _tab;
        private static Vector2 _pos = new Vector2(-1, -1);
        private static bool _dragging;
        private static Vector2 _dragOffset;
        private static bool _prevInOptions;
        private static CursorLockMode _prevLock;
        private static bool _prevVisible;

        public static void Init()
        {
            Add("Joueur", "Player", "Noclip, vitesse, saut et caméra. Seul ton personnage est concerné.",
                "Noclip, speed, jumping and camera. Only affects your own character.", PlayerMods.Draw);
            Add("Téléportation", "Teleport", "Va là où tu vises, ou enregistre tes endroits favoris.",
                "Go where you aim, or save your favourite spots.", Teleport.Draw);
            Add("Magasin", "Store", "Argent, franchise, rayons, journée et cartons gratuits.",
                "Money, franchise, shelves, the day and free boxes.", Store.Draw);
            Add("Gestion", "Management", "Agrandissements, avantages, factures, prix et enseigne.",
                "Expansions, perks, bills, prices and the store sign.", Management.Draw);
            Add("Clients et employés", "Customers & staff", "Fréquentation, voleurs, rendement et embauche du personnel.",
                "Foot traffic, thieves, staff performance and hiring.", Staff.Draw);
            Add("Monde", "World", "Horloge du magasin, météo, déchets et pannes.",
                "Store clock, weather, trash and breakdowns.", World.Draw);
            Add("Joueurs", "Players", "Les personnes présentes dans ta partie.",
                "The people in your game.", Players.Draw);
            Add("Balais", "Brooms", "Jetons d'événement, boules gachapon et skins de balai.",
                "Event tokens, gachapon capsules and broom skins.", Brooms.Draw);
            Add("Réglages", "Settings", "Touches, langue, taille de l'interface et notifications.",
                "Keys, language, interface size and notifications.", Settings.Draw);
        }

        private static void Add(string titleFr, string titleEn, string subFr, string subEn, Action<Ui> draw) =>
            Tabs.Add(new Tab { TitleFr = titleFr, TitleEn = titleEn, SubtitleFr = subFr, SubtitleEn = subEn, Draw = draw });

        internal static int TabCount => Tabs.Count;

        /// <summary>Shows a tab scrolled to <paramref name="scroll"/> pixels, without animation.</summary>
        internal static void Select(int tab, float scroll = 0f)
        {
            _tab = Mathf.Clamp(tab, 0, Tabs.Count - 1);
            Tabs[_tab].ScrollTarget = scroll;
            Anim.Snap("menu.scroll" + _tab, scroll);
            Anim.Snap("menu.fade", 1f);
        }

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public static void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
            if (Game.InGame)
            {
                _prevInOptions = Game.InOptions;
                Game.InOptions = true; // the game's own flag that freezes camera and movement
            }
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _dragging = false;
            GUIUtility.keyboardControl = 0;
            Settings.CancelRebind();
            if (Game.InGame)
            {
                Game.InOptions = _prevInOptions;
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
            }
        }

        /// <summary>Called every LateUpdate: the game re-locks the cursor on its own schedule.</summary>
        public static void Enforce()
        {
            if (!IsOpen) return;
            if (Game.InGame) Game.InOptions = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static float Scale => Mathf.Clamp(Settings.UiScale.Value * Screen.height / 1080f, 0.6f, 2.5f);

        public static void OnGUI()
        {
            Theme.Ensure();
            float scale = Scale;
            float sw = Screen.width / scale, sh = Screen.height / scale;
            var baseMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            GUI.matrix = Matrix4x4.identity;
            Players.DrawEsp();

            GUI.matrix = baseMatrix;
            if (!IsOpen) Hud.Draw();

            float open = Anim.To("menu.open", IsOpen ? 1f : 0f, IsOpen ? 16f : 22f);
            if (open > 0.005f)
            {
                Enforce();
                if (_pos.x < 0) _pos = new Vector2((sw - W) / 2f, (sh - H) / 2f);
                _pos.x = Mathf.Clamp(_pos.x, 40 - W, sw - 40);
                _pos.y = Mathf.Clamp(_pos.y, 0, sh - 40);
                var win = new Rect(_pos.x, _pos.y, W, H);

                float pop = 0.94f + 0.06f * Anim.EaseOutBack(open);
                var c = new Vector3(win.center.x, win.center.y, 0);
                GUI.matrix = baseMatrix * Matrix4x4.TRS(c, Quaternion.identity, new Vector3(pop, pop, 1)) * Matrix4x4.TRS(-c, Quaternion.identity, Vector3.one);
                GUI.color = new Color(1, 1, 1, open);
                DrawWindow(win);
                GUI.color = Color.white;
            }

            GUI.matrix = baseMatrix;
            Toasts.Draw(sw, sh);
            GUI.matrix = Matrix4x4.identity;
        }

        private static void DrawWindow(Rect win)
        {
            var e = Event.current;

            // Soft shadow, then the body.
            for (int i = 3; i >= 1; i--)
                Theme.Fill(new Rect(win.x - i * 5, win.y - i * 3, win.width + i * 10, win.height + i * 10), new Color(0, 0, 0, 0.09f), 18 + i * 2);
            Theme.Fill(win, Theme.Ink, 14);

            GUI.BeginGroup(win);
            var local = new Rect(0, 0, win.width, win.height);
            if (IsOpen && e.type == EventType.MouseDown && local.Contains(e.mousePosition) && e.button != 0) e.Use();

            DrawSidebar();
            DrawHeader(e);
            DrawContent(e);

            // Swallow clicks on empty window space so they never reach the game.
            if (IsOpen && (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel) && local.Contains(e.mousePosition)) e.Use();
            GUI.EndGroup();
        }

        private static void DrawSidebar()
        {
            var e = Event.current;
            Theme.Fill(new Rect(0, 0, Side, H), Theme.Aisle, 14);
            Theme.Fill(new Rect(Side - 14, 0, 14, H), Theme.Aisle);

            Theme.Label(new Rect(24, 18, Side - 40, 18), "Supermarket Together", 12, Theme.Mist);
            Theme.Label(new Rect(23, 34, Side - 40, 34), "Trainer", 28, Theme.Receipt, true);
            Theme.Label(new Rect(24, 68, Side - 40, 18), "by Zenato", 13, Theme.Price, true);

            float tagY = Anim.To("menu.tag", TabTop + _tab * TabH + (TabH - 32) / 2f, 16f);
            float tagW = Side - 28;
            Theme.DrawTag(new Rect(14, tagY, tagW, 32), Theme.Price);

            for (int i = 0; i < Tabs.Count; i++)
            {
                var r = new Rect(14, TabTop + i * TabH, tagW, TabH);
                bool hover = IsOpen && r.Contains(e.mousePosition);
                if (IsOpen && e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    if (_tab != i) { _tab = i; Anim.Snap("menu.fade", 0f); }
                    e.Use();
                }
                bool sel = i == _tab;
                float hv = Anim.To("menu.tabhover" + i, hover && !sel ? 1f : 0f, 18f);
                var text = new Rect(r.x + 28 + hv * 4, r.y, r.width - 34, r.height);
                // Text sits above the moving tag, so ink when it overlaps, receipt white otherwise.
                float overlap = 1f - Mathf.Clamp01(Mathf.Abs(tagY + 16 - r.center.y) / 20f);
                var col = Color.Lerp(Color.Lerp(Theme.Receipt.WithAlpha(0.85f), Theme.Receipt, hv), Theme.Ink, overlap);
                Theme.Label(text, Tabs[i].Title, 15, col, sel || overlap > 0.5f);
            }

            // Session status at the foot of the aisle.
            bool inGame = Game.InGame, host = Game.IsHost;
            var dot = inGame ? (host ? Theme.Price : Theme.Receipt) : Theme.Mist;
            Theme.Line(new Rect(24, H - 70, Side - 48, 1), Theme.Shelf);
            Theme.Fill(new Rect(24, H - 47, 8, 8), dot, 4);
            Theme.Label(new Rect(40, H - 54, Side - 60, 22), Game.SessionLabel, 14, Theme.Receipt, true);
            Theme.Label(new Rect(24, H - 34, Side - 40, 18),
                !inGame ? L.T("Lance une partie pour tout débloquer", "Start a game to unlock everything")
                : host ? L.T("Toutes les options disponibles", "Every option available")
                : L.T("Options d'hôte grisées", "Host options greyed out"), 12, Theme.Mist);
        }

        private static void DrawHeader(Event e)
        {
            var tab = Tabs[_tab];
            var drag = new Rect(Side, 0, W - Side, 84);
            if (IsOpen && e.type == EventType.MouseDown && e.button == 0 && drag.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition;
                e.Use();
            }
            if (_dragging && e.type == EventType.MouseDrag)
            {
                _pos += e.mousePosition - _dragOffset;
                e.Use();
            }
            if (_dragging && e.rawType == EventType.MouseUp) _dragging = false;

            Theme.Label(new Rect(Side + 30, 22, W - Side - 220, 34), tab.Title, 26, Theme.Receipt, true);
            Theme.Label(new Rect(Side + 30, 56, W - Side - 60, 20), tab.Subtitle, 13, Theme.Mist);
            Theme.Label(new Rect(W - 230, 26, 200, 20), Settings.MenuKey.Value + L.T(" pour fermer", " to close"), 12, Theme.Mist, false, TextAnchor.MiddleRight);
        }

        private static void DrawContent(Event e)
        {
            var tab = Tabs[_tab];
            var area = new Rect(Side + 30, 96, W - Side - 30 - 22, H - 96 - 16);

            float fade = Anim.To("menu.fade", 1f, 12f);
            float scroll = Anim.To("menu.scroll" + _tab, tab.ScrollTarget, 18f);

            bool wheel = IsOpen && e.type == EventType.ScrollWheel && area.Contains(e.mousePosition);
            float wheelDelta = wheel ? e.delta.y : 0f;
            if (wheel) e.Use();

            GUI.BeginGroup(area);
            var prev = GUI.color;
            GUI.color = prev.WithAlpha(fade);
            Ui.Width = area.width - 14;
            Ui.Clip = new Rect(0, 0, area.width, area.height);
            Ui.Interactive = IsOpen;
            Ui.Y = Ui.Top = -scroll + (1f - fade) * 10f;
            tab.Draw(Ui);
            float contentH = Ui.Y + scroll - (1f - fade) * 10f + 12;
            GUI.color = prev;

            float max = Mathf.Max(0, contentH - area.height);
            tab.ScrollTarget = Mathf.Clamp(tab.ScrollTarget + wheelDelta * 24f, 0, max);
            if (max > 0)
            {
                var track = new Rect(area.width - 4, 0, 3, area.height);
                float thumbH = Mathf.Max(30, area.height * area.height / contentH);
                float t = Mathf.Clamp01(scroll / max);
                Theme.Fill(track, Theme.Shelf.WithAlpha(0.5f), 1);
                Theme.Fill(new Rect(track.x, t * (area.height - thumbH), 3, thumbH), Theme.Mist, 1);
            }
            GUI.EndGroup();
        }
    }
}
