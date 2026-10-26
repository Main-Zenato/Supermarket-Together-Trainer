using System.Collections.Generic;
using HarmonyLib;
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

        public static void Reset() { PlayerEsp = false; ThiefEsp = false; Pulls.Clear(); }

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

        // ---- Bring a player ----------------------------------------------------------------

        private sealed class Pull
        {
            public PlayerObjectController Player;
            public float Next, Until, LastDistance = float.MaxValue;
            public int Stuck;
        }

        private static readonly List<Pull> Pulls = new List<Pull>();

        internal static bool Pulling(PlayerObjectController p) => Pulls.Exists(x => x.Player == p);

        internal static void StopPull(PlayerObjectController p) => Pulls.RemoveAll(x => x.Player == p);

        internal static void StartPull(PlayerObjectController p)
        {
            StopPull(p);
            Pulls.Add(new Pull { Player = p, Until = Time.unscaledTime + 60f });
        }

        /// <summary>
        /// Each game moves its own player, so nobody can place anyone else. The debt collector's push
        /// makes the pushed player's own game walk them 3 m/s for 1.5 s, so we chain pushes towards us.
        /// Any player may ask for a push, as the game does when someone bumps into another.
        /// </summary>
        public static void Tick()
        {
            if (Pulls.Count == 0) return;
            if (!Game.InGame || !Game.Player) { Pulls.Clear(); return; }
            for (int i = Pulls.Count - 1; i >= 0; i--)
            {
                var pull = Pulls[i];
                var p = pull.Player;
                if (!p || Time.unscaledTime > pull.Until) { Pulls.RemoveAt(i); continue; }
                if (Time.time < pull.Next) continue;
                var to = Game.Player.position - p.transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < 2f)
                {
                    Pulls.RemoveAt(i);
                    Toasts.Show(L.T($"{NameOf(p)} est arrivé.", $"{NameOf(p)} is here."));
                    continue;
                }
                pull.Stuck = dist > pull.LastDistance - 0.5f ? pull.Stuck + 1 : 0;
                if (pull.Stuck >= 2)
                {
                    Pulls.RemoveAt(i);
                    Toasts.Show(L.T($"{NameOf(p)} est bloqué à {dist:0} m. Rapproche-toi en vue de lui et recommence.",
                        $"{NameOf(p)} is stuck {dist:0} m away. Move where they can see you and try again."));
                    continue;
                }
                pull.LastDistance = dist;
                Push(p, to / dist);
                pull.Next = Time.time + 1.6f; // the push lasts 1.5 s of game time; a new one is ignored before
            }
        }

        internal static void Push(PlayerObjectController p, Vector3 direction) =>
            Traverse.Create(p.GetComponent<PlayerNetwork>()).Method("CmdPushPlayer", new[] { typeof(Vector3) }).GetValue(direction);

        internal static string NameOf(PlayerObjectController p) =>
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
                    if (Pulling(p)) caption += L.T(", arrive vers toi", ", coming to you");
                    if (me)
                    {
                        ui.ListRow("pl." + p.netId, NameOf(p), caption, false);
                        continue;
                    }
                    int b = ui.ListRow("pl." + p.netId, NameOf(p), caption, host ? int.MaxValue : 2,
                        new Btn(L.T("Rejoindre", "Go to"), ButtonKind.Primary), Pulling(p) ? L.T("Arrêter", "Stop") : L.T("Amener", "Bring"),
                        L.T("Prison 30 s", "Jail 30 s"), new Btn(L.T("Expulser", "Kick"), ButtonKind.Danger));
                    if (b == 0)
                    {
                        Game.Teleport(p.transform.position + p.transform.forward * 1.2f + Vector3.up * 0.2f);
                        Toasts.Show(L.T($"Téléporté près de {NameOf(p)}.", $"Teleported next to {NameOf(p)}."));
                    }
                    if (b == 1)
                    {
                        if (Pulling(p)) { StopPull(p); Toasts.Show(L.T($"{NameOf(p)} n'est plus amené.", $"No longer bringing {NameOf(p)}.")); }
                        else { StartPull(p); Toasts.Show(L.T($"{NameOf(p)} arrive vers toi.", $"{NameOf(p)} is coming to you.")); }
                    }
                    if (b == 2 && host)
                    {
                        p.GetComponent<PlayerPermissions>().JPlayer(30);
                        Toasts.Show(L.T($"{NameOf(p)} part en prison pour 30 secondes.", $"{NameOf(p)} is going to jail for 30 seconds."));
                    }
                    if (b == 3 && host)
                    {
                        if (p.ConnectionID == 0) Toasts.Show(L.T("L'hôte ne peut pas être expulsé.", "The host can't be kicked."));
                        else { p.GetComponent<PlayerPermissions>().KPlayer(); Toasts.Show(L.T($"{NameOf(p)} a été expulsé.", $"{NameOf(p)} was kicked.")); }
                    }
                }
                ui.Note(L.T("« Amener » est en test. Il utilise la poussée du jeu : le joueur glisse vers toi à 3 m/s et ne peut pas bouger pendant ce temps. Un mur ou un rayon l'arrête : place-toi en vue de lui.",
                    "\"Bring\" is being tested. It uses the game's own push: the player slides towards you at 3 m/s and can't move meanwhile. A wall or a shelf stops them, so stand where they can see you."));
                if (!host) ui.Note(L.T("La prison et l'expulsion sont réservées à l'hôte.", "Jail and kick are host only."));
            }
            Tokens.Draw(ui, List());

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
