using System.Collections.Generic;
using SupermarketTrainer.UI;
using UnityEngine;

namespace SupermarketTrainer.Features
{
    internal static class Players
    {
        public static bool PlayerEsp;
        public static bool ThiefEsp;

        private static readonly List<PlayerObjectController> Cache = new List<PlayerObjectController>();
        private static float _nextRefresh;

        public static void Reset() { PlayerEsp = false; ThiefEsp = false; }

        private static List<PlayerObjectController> List()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 1f;
                Cache.Clear();
                Cache.AddRange(Object.FindObjectsByType<PlayerObjectController>(FindObjectsSortMode.None));
                Cache.Sort((a, b) => a.PlayerIdNumber.CompareTo(b.PlayerIdNumber));
            }
            Cache.RemoveAll(p => !p);
            return Cache;
        }

        private static string NameOf(PlayerObjectController p) =>
            string.IsNullOrEmpty(p.PlayerName) ? L.T("Joueur ", "Player ") + p.PlayerIdNumber : p.PlayerName;

        public static void Draw(Ui ui)
        {
            bool host = Game.IsHost;

            ui.Section(L.T("Dans la partie", "In this game"));
            if (!Game.InGame)
            {
                ui.Note(L.T("Rejoins ou héberge une partie pour voir les joueurs.", "Join or host a game to see the players."));
            }
            else
            {
                foreach (var p in List())
                {
                    bool me = p.isLocalPlayer;
                    float dist = Game.Player ? Vector3.Distance(Game.Player.position, p.transform.position) : 0f;
                    string caption = me ? L.T("C'est toi", "That's you")
                        : p.ConnectionID == 0 ? L.T($"Hôte, à {dist:0} m", $"Host, {dist:0} m away")
                        : L.T($"À {dist:0} m", $"{dist:0} m away");
                    if (me)
                    {
                        ui.ListRow("pl." + p.netId, NameOf(p), caption, false);
                        continue;
                    }
                    int b = ui.ListRow("pl." + p.netId, NameOf(p), caption, !host,
                        new Btn(L.T("Rejoindre", "Go to"), ButtonKind.Primary), L.T("Prison 30 s", "Jail 30 s"),
                        new Btn(L.T("Expulser", "Kick"), ButtonKind.Danger));
                    if (b == 0)
                    {
                        Game.Teleport(p.transform.position + p.transform.forward * 1.2f + Vector3.up * 0.2f);
                        Toasts.Show(L.T($"Téléporté près de {NameOf(p)}.", $"Teleported next to {NameOf(p)}."));
                    }
                    if (b == 1 && host)
                    {
                        p.GetComponent<PlayerPermissions>().JPlayer(30);
                        Toasts.Show(L.T($"{NameOf(p)} part en prison pour 30 secondes.", $"{NameOf(p)} is going to jail for 30 seconds."));
                    }
                    if (b == 2 && host)
                    {
                        if (p.ConnectionID == 0) Toasts.Show(L.T("L'hôte ne peut pas être expulsé.", "The host can't be kicked."));
                        else { p.GetComponent<PlayerPermissions>().KPlayer(); Toasts.Show(L.T($"{NameOf(p)} a été expulsé.", $"{NameOf(p)} was kicked.")); }
                    }
                }
                if (!host) ui.Note(L.T("La prison et l'expulsion sont réservées à l'hôte.", "Jail and kick are host only."));
            }

            ui.Section(L.T("Vision à travers les murs", "See through walls"));
            PlayerEsp = ui.Toggle("esp.players", L.T("Afficher les joueurs", "Show players"),
                L.T("Nom et distance de chaque joueur, même derrière les murs.", "Name and distance of every player, even behind walls."), PlayerEsp);
            ThiefEsp = ui.Toggle("esp.thieves", L.T("Repérer les voleurs", "Spot thieves"),
                L.T("Marque les clients qui comptent voler. Seul l'hôte connaît leurs intentions.",
                    "Marks customers who plan to steal. Only the host knows their intentions."), ThiefEsp, !host);
        }

        /// <summary>Screen-space labels, drawn with an identity GUI matrix.</summary>
        public static void DrawEsp()
        {
            if (!Game.InGame || (!PlayerEsp && !ThiefEsp)) return;
            var cam = Camera.main;
            if (!cam) return;

            if (PlayerEsp)
            {
                foreach (var p in List())
                    if (!p.isLocalPlayer) Marker(cam, p.transform.position + Vector3.up * 2.1f, NameOf(p), Theme.Price);
            }

            if (ThiefEsp && Game.IsHost && NPC_Manager.Instance && NPC_Manager.Instance.customersnpcParentOBJ)
            {
                foreach (Transform c in NPC_Manager.Instance.customersnpcParentOBJ.transform)
                {
                    var info = c.GetComponent<NPC_Info>();
                    if (info && info.isAThief)
                        Marker(cam, c.position + Vector3.up * 2f,
                            info.thiefFleeing ? L.T("Voleur en fuite", "Fleeing thief") : L.T("Voleur", "Thief"), Theme.Promo);
                }
            }
        }

        private static void Marker(Camera cam, Vector3 world, string label, Color accent)
        {
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            float dist = Game.Player ? Vector3.Distance(Game.Player.position, world) : sp.z;
            string text = $"{label}  {dist:0} m";
            var size = Theme.Measure(text, 13, true);
            var r = new Rect(sp.x - size.x / 2f - 15, Screen.height - sp.y - 13, size.x + 32, 24);
            Theme.Fill(r, Theme.Ink.WithAlpha(0.82f), 12);
            Theme.Fill(new Rect(r.x + 9, r.center.y - 3, 6, 6), accent, 3);
            Theme.Label(new Rect(r.x + 21, r.y, size.x + 6, r.height), text, 13, Theme.Receipt, true);
        }
    }
}
